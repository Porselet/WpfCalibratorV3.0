using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace WpfCalibrator.Services;

/// <summary> 100626
/// Сервис для управления коммуникацией с устройством через COM-порт.
/// </summary>
public sealed class CommunicationService : IDisposable
{
    private SerialPort? _serialPort;
    private CancellationTokenSource _cts = new(); // Убираем readonly
    private readonly object _lock = new(); // Добавляем замок
    public event Action<byte, byte, byte, byte, byte[]>? DataPacketReceived;
    // Конструктор для DI и тестов
    public CommunicationService(SerialPort? serialPort = null)
    {
        _serialPort = serialPort;
    }

    // 1. Управление портом
    public bool IsConnected => _serialPort?.IsOpen ?? false;

    public void Connect(string portName, int baudRate = 115200)
    {
        lock (_lock)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Dispose();
                _serialPort = null;
            }

            try
            {
                _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = SerialPort.InfiniteTimeout,
                    WriteTimeout = 500,
                    Encoding = System.Text.Encoding.UTF8
                };
                _serialPort.Open();
                _cts.Cancel(); // Остановим старый поток, если был
                _cts = new CancellationTokenSource();
                StartListening(); // Запускаем поток чтения
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to open port {portName}: {ex.Message}");
            }
        }
    }

    public void Disconnect()
    {
        lock (_lock)
        {
            _cts.Cancel();
            _serialPort?.Dispose();
            _serialPort = null;
        }
    }
    /// <summary>
    /// Универсальный полиморфный метод для отправки любых калибровочных массивов (float, int, short, byte) в STM32.
    /// </summary>
    public async Task SendPacketAsync<T>(byte modelId, byte cmd, byte varId, T[] payloadArray) where T : struct
    {
        if (payloadArray == null || payloadArray.Length == 0) return;

        // 1. elementsCount — это просто длина массива (количество элементов uint8_t)
        byte elementsCount = (byte)payloadArray.Length;

        // 2. Вычисляем размер одного элемента динамически (4 для float/int, 2 для short, 1 для byte)
        int elementSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));

        // 3. Выделяем буфер под результирующие байты
        byte[] bytePayload = new byte[payloadArray.Length * elementSize];

        // 4. Быстрое низкоуровневое копирование ОЗУ (прямой системный аналог memcpy)
        Buffer.BlockCopy(payloadArray, 0, bytePayload, 0, bytePayload.Length);

        // 5. Вызываем твой базовый метод отправки
        await SendPacketAsync(modelId, cmd, varId, elementsCount, bytePayload);
    }

    // 2. Отправка пакетов (синхронная оболочка)
    public async Task SendPacketAsync(byte modelId, byte cmd, byte varId, byte elementsCount, byte[] payload)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
        {
            throw new InvalidOperationException("Порт не открыт!");
        }

        // 1. Формируем заголовок пакета строго по app_link.h
        byte[] header = new byte[]
        {
        0xAA,    // Индекс 0: PKT_PREAMBLE_IDX (0xAA)
        modelId, // Индекс 1: PKT_MODEL_ID_IDX
        cmd,     // Индекс 2: PKT_CMD_IDX
        varId,   // Индекс 3: PKT_VAR_ID_IDX
        elementsCount // Индекс 4: PKT_LEN_IDX
        };

        // 2. Вычисляем CRC-8
        byte[] packet = header.Concat(payload).ToArray();
        byte crc = CalculateCRC8_SAE_J1850(packet, packet.Length);

        // 3. Добавляем CRC в конец массива
        Array.Resize(ref packet, packet.Length + 1);
        packet[packet.Length - 1] = crc;

        // 4. Отправка в физический порт
        _serialPort.Write(packet, 0, packet.Length);
    }
    // 3. Прием пакетов (фоновый поток)
    private async Task ListenAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen) return;

                // 1. Если в буфере Windows пусто — спим 5 мс, чтобы не грузить ядро процессора на 100%
                if (_serialPort.BytesToRead == 0)
                {
                    await Task.Delay(5);
                    continue;
                }

                // 2. Читаем один байт и ищем преамбулу 0xAA
                int rawByte = _serialPort.ReadByte();
                if (rawByte == -1 || rawByte != 0xAA) continue;

                // 🔥 ПРЕАМБУЛА НАЙДЕНА (0xAA). Нам нужно дождаться еще минимум 4 байта заголовка.
                // Даем микропаузу 2 мс, чтобы DMA драйвера Windows успел досыпать байты в буфер
                await Task.Delay(2);

                if (_serialPort.BytesToRead < 4) continue; // Если заголовок не долетел — сброс кадра

                // 3. Вычитываем оставшиеся 4 байта заголовка строго по структуре app_link.h
                byte modelId = (byte)_serialPort.ReadByte(); // PKT_MODEL_ID_IDX = 1
                byte cmd = (byte)_serialPort.ReadByte(); // PKT_CMD_IDX = 2
                byte varId = (byte)_serialPort.ReadByte(); // PKT_VAR_ID_IDX = 3
                byte elementsCount = (byte)_serialPort.ReadByte(); // PKT_LEN_IDX = 4

                // 4. Высчитываем размер полезной нагрузки (Payload)
                int payloadSize = 0;

                // Если это ответ от STM32 на команду ЧТЕНИЯ (CMD_VAR_READ = 2), прилетят данные float (по 4 байта)
                if (cmd == 0x02)
                {
                    payloadSize = elementsCount * 4;
                }
                // Если cmd == 0x01 (подтверждение записи) или 0x03 (Flash ACK) — данных в ответе нет (0 байт)

                // 5. Дожидаемся прилета всех байт данных + 1 байт контрольной суммы CRC
                int bytesExpected = payloadSize + 1;

                // Запускаем таймаут-счетчик (на случай, если связь оборвется посреди пакета), чтобы не зависнуть
                int timeoutCounter = 0;
                while (_serialPort.BytesToRead < bytesExpected && timeoutCounter < 10)
                {
                    await Task.Delay(2);
                    timeoutCounter++;
                }

                // Если данные так и не долетели — пакет битый, уходим
                if (_serialPort.BytesToRead < bytesExpected) continue;

                // 6. Вычитываем Payload (данные)
                byte[] payloadBytes = new byte[payloadSize];
                if (payloadSize > 0)
                {
                    _serialPort.Read(payloadBytes, 0, payloadSize);
                }

                // 7. Вычитываем байт принятой контрольной суммы
                byte receivedCrc = (byte)_serialPort.ReadByte();

                // 8. Собираем полный кадр для валидации CRC-8
                byte[] fullPacket = new byte[5 + payloadSize];
                fullPacket[0] = 0xAA;
                fullPacket[1] = modelId;
                fullPacket[2] = cmd;
                fullPacket[3] = varId;
                fullPacket[4] = elementsCount;
                if (payloadSize > 0)
                {
                    Array.Copy(payloadBytes, 0, fullPacket, 5, payloadSize);
                }

                // Считаем CRC-8 строго по твоему сишному алгоритму
                byte calculatedCrc = CalculateCRC8_SAE_J1850(fullPacket, fullPacket.Length);

                if (calculatedCrc == receivedCrc)
                {
                    // 🔥 УСПЕХ: Пакет полностью валиден! Пуляем событие в MainViewModel
                    DataPacketReceived?.Invoke(modelId, cmd, varId, elementsCount, payloadBytes);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"🛑 [C# UART] Ошибка CRC-8! Ожидалось: 0x{calculatedCrc:X2}, пришло: 0x{receivedCrc:X2}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UART Receive Error]: {ex.Message}");
                await Task.Delay(10);
            }
        }
    }
    // 4. Вспомогательные методы
    private byte CalculateCRC8_SAE_J1850(byte[] data, int length)
    {
        byte crc = 0x00; // Начальное значение совпадает
        for (int i = 0; i < length; i++)
        {
            crc ^= data[i];
            for (int bit = 0; bit < 8; bit++)
            {
                // СТРОГО КАК В СИ: Сначала сдвигаем, а потом применяем XOR!
                if ((crc & 0x80) != 0)
                {
                    crc = (byte)((crc << 1) ^ 0x1D);
                }
                else
                {
                    crc = (byte)(crc << 1);
                }
            }
        }
        return crc;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _serialPort?.Dispose();
    }

    // Запускаем фоновый поток чтения
    private void StartListening()
    {
        Task.Run(ListenAsync, _cts.Token);
    }
}