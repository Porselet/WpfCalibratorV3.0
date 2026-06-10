using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace WpfCalibrator.Services;

/// <summary>
/// Сервис для управления коммуникацией с устройством через COM-порт.
/// </summary>
public sealed class CommunicationService : IDisposable
{
    private SerialPort? _serialPort;
    private CancellationTokenSource _cts = new(); // Убираем readonly
    private readonly object _lock = new(); // Добавляем замок

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
                    ReadTimeout = 500,
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

    // 2. Отправка пакетов (синхронная оболочка)
    public async Task SendPacketAsync(byte modelId, byte cmd, byte varId, byte elementsCount, byte[] payload)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
        {
            throw new InvalidOperationException("Порт не открыт!");
        }

        // 1. Формируем заголовок пакета (5 байт)
        byte[] header = new byte[]
        {
        0xAA, // Preamble
        modelId,
        cmd,
        varId,
        elementsCount
        };

        // 2. Вычисляем CRC-8 (используйте тот же алгоритм, что и в прошивке!)
        byte[] packet = header.Concat(payload).ToArray();
        byte crc = ComputeCrc(packet);

        // 3. Добавляем CRC в конец массива
        Array.Resize(ref packet, packet.Length + 1);
        packet[packet.Length - 1] = crc; // Важно: присваиваем значение элементу массива

        // 4. Отправка (синхронная оболочка)
        _serialPort.Write(packet, 0, packet.Length);
    }

    // 3. Прием пакетов (фоновый поток)
    private async Task ListenAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                if (_serialPort == null) return;

                // Ждем появления преамбулы (0xAA)
                byte? firstByte = await Task.Run(() =>
                {
                    int rawByte = _serialPort.ReadByte();
                    return rawByte == -1 ? (byte?)null : (byte)rawByte;
                }); 

                if (firstByte != 0xAA) continue;

                // Читаем заголовок (еще 4 байта)
                byte[] header = new byte[4];
                await Task.Run(() => _serialPort.Read(header, 0, 4)); // Обёртка

                // Дальнейшая обработка пакета...
            }
        }
    }

    // 4. Вспомогательные методы
    private byte ComputeCrc(byte[] data)
    {
        // Реализация CRC-8 (зеркальная с прошивкой)
        // Используйте тот же полином и алгоритм, что и в BlackPill
        // Пример реализации (замените на реальную):
        byte crc = 0;
        foreach (byte b in data)
        {
            crc ^= b;
            // Логика XOR-шифтинга (зависит от полинома)
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