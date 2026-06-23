using System;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WpfCalibrator.Services;

public sealed partial class CommunicationService : ICommunicationService, IDisposable
{
    // Сюда переносим из основного файла:
    // - Метод ExecuteCommandAsync
    // - Метод SerializeCommand (или как у тебя называется сборка пакета)
    // - Переменные _expectedCmd, _expectedVarId, _currentTransactionTcs
    // Технические переменные для контроля: какой именно зеркальный ответ мы сейчас ждем от STM32
    private volatile byte _expectedCmd;
    private volatile byte _expectedVarId;
    private volatile int _expectedElementSize = 4; // НОВОЕ: Ожидаемый размер одного элемента в байтах


    private int _expectedRows = 1; // Ожидаемое количество строк матрицы ответа
    private int _expectedCols = 1; // Ожидаемое количество колонок матрицы ответа
    private string _expectedDataType = "single"; // Ожидаемый тип данных Матлаба
    /// <summary>
    /// Высокуровневый конвейер: принимает команду от Диспетчера, пакует, 
    /// отправляет в порт и асинхронно ждет зеркальный ответ от STM32 с таймаутом 50мс.
    /// </summary>
    public async System.Threading.Tasks.Task<bool> ExecuteCommandAsync(Models.NetworkCommand cmd)
    {
        if (cmd == null) return false;

        // 1. ШЛАГБАУМ: Запрашиваем монопольный доступ к шине
        await _networkSemaphore.WaitAsync();

        try
        {
            // 2. МАРШАЛИНГ: Пакуем данные ТОЛЬКО если это реальная запись (VarWrite)!
            // Если команда идет на Чтение (VarRead) — payload ОБЯЗАН быть абсолютно пустым!
            byte[] payloadBytes = Array.Empty<byte>();
            byte elementsCount = (byte)(cmd.Rows * cmd.Cols);

            if (cmd.Cmd == Models.LinkCommand.VarWrite)
            {
                payloadBytes = SerializeCommandPayload(cmd);
            }

            // 3. НАСТРОЙКА ОЖИДАНИЯ: Запоминаем, какой именно ответ от платы мы теперь ждем.
            // Переводим перечисление enum в сырой байт (0x01 или 0x02)
            _expectedCmd = (byte)cmd.Cmd;
            _expectedVarId = cmd.VarId;
            _responseCompletionSource = new System.Threading.Tasks.TaskCompletionSource<bool>();


            // 4. 🔥 ЖЕСТКИЙ ФИКС ОТПРАВКИ: Передаем СТРОГО (byte)cmd.Cmd!
            // Это принудительно заставит низкоуровневый метод отправить в STM32 честный байт 0x02 (Чтение),
            // не давая ему возможности подменить команду на 0x01 из-за внутренних флагов IsParam!
            // ВНУТРИ МЕТОДА ExecuteCommandAsync ПЕРЕД ВЫЗОВОМ SendPacketAsync:
            int currentSize = 4;
            string typeLower = cmd.DataType.ToLower().Trim();
            if (typeLower == "double" || typeLower == "int64" || typeLower == "uint64") currentSize = 8;
            else if (typeLower == "int16" || typeLower == "uint16") currentSize = 2;
            else if (typeLower == "int8" || typeLower == "uint8" || typeLower == "boolean" || typeLower == "bool") currentSize = 1;

            // ЗАПОМИНАЕМ ГЕОМЕТРИЮ ДЛЯ ПРИЕМНИКА (АБСОЛЮТНАЯ ПОТОКОБЕЗОПАСНОСТЬ):
            _expectedElementSize = currentSize;
            _expectedRows = cmd.Rows;
            _expectedCols = cmd.Cols;
            _expectedDataType = typeLower;

            _expectedCmd = (byte)cmd.Cmd;
            _expectedVarId = cmd.VarId;
            _responseCompletionSource = new System.Threading.Tasks.TaskCompletionSource<bool>();

            await SendPacketAsync(cmd.ModelId, (byte)cmd.Cmd, cmd.VarId, elementsCount, payloadBytes);


            // 5. ДИНАМИЧЕСКИЙ ТАЙМАУТ-ПРЕДОХРАНИТЕЛЬ: 
            // Вычисляем время ожидания на основе тяжести пакета.
            // Базовые 60 мс на задержки ОС Windows + 1.5 мс на каждый элемент float/double в проводе.
            int totalElements = cmd.Rows * cmd.Cols;
            int dynamicTimeoutMs = 160 + (int)(totalElements * 1.5) + 300;

            // На всякий случай ограничиваем максимальный таймаут сверху (например, 1.5 секунды), 
            // чтобы при полном обрыве кабеля софт не зависал бесконечно.
            if (dynamicTimeoutMs > 1500) dynamicTimeoutMs = 1500;

            var timeoutTask = System.Threading.Tasks.Task.Delay(dynamicTimeoutMs);
            var completedTask = await System.Threading.Tasks.Task.WhenAny(_responseCompletionSource.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // Случился АППАРАТНЫЙ ТАЙМАУТ (Плата или буфер Windows не успели за dynamicTimeoutMs)
                string errDesc = $"[TIMEOUT] Отсутствует ответ от МК на команду {cmd.Cmd} (VarId: {cmd.VarId}) за {dynamicTimeoutMs}мс";

                // Выводим красную строку в наш текстовый терминал пакетов
                //WpfCalibrator.Views.UartMonitorWindow.LogPacket("ERR !", "#FF5555", errDesc, Array.Empty<byte>());
                OnLogPacket?.Invoke("ERR !", "#FF5555", errDesc, Array.Empty<byte>());
                return false; // Транзакция сорвалась
            }


            return true;
        }
        catch (Exception ex)
        {
            string critDesc = $"[CRIT ERROR] Сбой транзакции: {ex.Message}";
            //WpfCalibrator.Views.UartMonitorWindow.LogPacket("EXCP", "#FF0000", critDesc, Array.Empty<byte>());
            OnLogPacket?.Invoke("EXCP", "#FF0000", critDesc, Array.Empty<byte>());
            return false;
        }
        finally
        {
            _responseCompletionSource = null;
            _networkSemaphore.Release(); // Железно открываем шлагбаум
        }
    }

    /// <summary>
    /// Универсальный полиморфный метод для отправки любых калибровочных массивов (float, int, short, byte) в STM32.
    /// </summary>

    // 2. Отправка пакетов (синхронная оболочка)

    // БЫЛО: public async Task SendPacketAsync(byte modelId, byte cmd, byte varId, byte elementsCount, byte[] payload)
    // СТАЛО: Жестко закрываем метод извне! Теперь отправка байт доступна ТОЛЬКО через ExecuteCommandAsync!
    private async System.Threading.Tasks.Task SendPacketAsync(byte modelId, byte cmd, byte varId, byte elementsCount, byte[] payload)
    {
        // ... твой оригинальный рабочий сишный код сборки кадра и записи в порт ...

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
        // ВНУТРИ МЕТОДА SendPacketAsync ПЕРЕД ЗАПИСЬЮ В ПОРТ:

        string txDesc = $"TX [CMD: 0x{cmd:X2}, VarId: {varId}]";
        //WpfCalibrator.Views.UartMonitorWindow.LogPacket("TX -->", "#007ACC", txDesc, packet);
        OnLogPacket?.Invoke("TX -->", "#007ACC", txDesc, packet);
        // 4. Отправка в физический порт
        _serialPort.Write(packet, 0, packet.Length);
    }



}

