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
    // ======================================================================
    // СИНГЛТОН: Единая, потокобезопасная точка доступа к транспорту
    // ======================================================================
    private static readonly Lazy<CommunicationService> _instance =
        new Lazy<CommunicationService>(() => new CommunicationService());

    public static CommunicationService Instance => _instance.Value;

    // Приватный конструктор — закрывает создание экземпляров извне
    private CommunicationService()
    {
        // Твой старый рабочий код инициализации объекта _serialPort и замков...
        _serialPort = new System.IO.Ports.SerialPort();
        // ... (оставь здесь всё, что у тебя было внутри старого конструктора)
    }
    // Ссылка на полный плоский список переменных из JSON Матлаба (для динамического расчета размера пакетов RX)
    public List<Models.VariableConfig>? AllVariablesConfig { get; set; }


    private SerialPort? _serialPort;
    private CancellationTokenSource _cts = new(); // Убираем readonly
    private readonly object _lock = new(); // Добавляем замок

    // ИСПРАВЛЕНО НАЧИСТО: Передаем наверх строго один высокоуровневый объект ответа!
    public event Action<Models.NetworkCommand>? DataPacketReceived;

    // ======================================================================
    // НОВОЕ: ЗАМКИ СИНХРОНИЗАЦИИ И ТРИГГЕРЫ ОЖИДАНИЯ ДЛЯ ПОЛУДУПЛЕКСА (HANDSHAKE)
    // ======================================================================
    // Асинхронный семафор-шлагбаум: разрешает отправку строго одного кадра за раз
    private readonly System.Threading.SemaphoreSlim _networkSemaphore = new System.Threading.SemaphoreSlim(1, 1);

    // Асинхронный триггер ожидания ответа: связывает поток TX и фоновый поток RX
    private System.Threading.Tasks.TaskCompletionSource<bool>? _responseCompletionSource;

    // Технические переменные для контроля: какой именно зеркальный ответ мы сейчас ждем от STM32
    private byte _expectedCmd;
    private byte _expectedVarId;
    private int _expectedElementSize = 4; // НОВОЕ: Ожидаемый размер одного элемента в байтах
    private int _expectedRows = 1; // Ожидаемое количество строк матрицы ответа
    private int _expectedCols = 1; // Ожидаемое количество колонок матрицы ответа
    private string _expectedDataType = "single"; // Ожидаемый тип данных Матлаба

    // Потокобезопасное хранилище карты памяти выбранного устройства из Матлаба
    public Models.DeviceConfig? CurrentDeviceConfig { get; set; }

    // Конструктор для DI и тестов
    /*    public CommunicationService(SerialPort? serialPort = null)
        {
            _serialPort = serialPort;
        }
    */
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
        WpfCalibrator.Views.UartMonitorWindow.LogPacket("TX -->", "#007ACC", txDesc, packet);

        // 4. Отправка в физический порт
        _serialPort.Write(packet, 0, packet.Length);
    }

    // 3. Прием пакетов (фоновый поток)
    private async System.Threading.Tasks.Task ListenAsync()
    {
        byte[] headerBuffer = new byte[4]; // Буфер под оставшиеся 4 байта заголовка

        // Железный щит: перехватывает системное исключение при жестком закрытии COM-порта извне
        try
        {
            while (_serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    // 1. Читаем самый первый стартовый байт из физического порта
                    byte[] preambleBuffer = new byte[1];
                    int readBytes = await _serialPort.BaseStream.ReadAsync(preambleBuffer, 0, 1);

                    if (readBytes == 0)
                    {
                        // Уступаем квант времени планировщику Windows, чтобы не зациклить ядро ЦП
                        await System.Threading.Tasks.Task.Yield();
                        continue;
                    }

                    byte singleByte = preambleBuffer[0];

                    // 2. Проверка преамбулы кадра MoTeC-style
                    if (singleByte != 0xAA)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UART-GARBAGE] Пропущен байт мусора: 0x{singleByte:X2}");
                        continue;
                    }

                    // 3. Нашли 0xAA! Срочно дочитываем остальные 4 байта заголовка кадра
                    int headerRead = 0;
                    while (headerRead < 4)
                    {
                        int currentRead = await _serialPort.BaseStream.ReadAsync(headerBuffer, headerRead, 4 - headerRead);
                        if (currentRead == 0)
                        {
                            await System.Threading.Tasks.Task.Yield();
                            break;
                        }
                        headerRead += currentRead;
                    }

                    if (headerRead < 4) continue; // Недогруженный заголовок — сброс кадра

                    // Десериализуем поля заголовка
                    byte modelId = headerBuffer[0];
                    byte cmd = headerBuffer[1];
                    byte varId = headerBuffer[2];
                    byte elementsCount = headerBuffer[3];

                    // ======================================================================
                    // 4. ДИНАМИЧЕСКИЙ РАСЧЕТ РАЗМЕРА ПОЛЕЗНОЙ НАГРУЗКИ (PAYLOAD)
                    // ======================================================================
                    int payloadSize = 0;

                    // Данные прилетают И на Чтение (0x02), И на Запись (0x01) согласно нашему Handshake!
                    if (cmd == 0x02 || cmd == 0x01)
                    {
                        // Чистая автономность: берем точный размер типа, который нам оставил поток отправки!
                        payloadSize = elementsCount * _expectedElementSize;
                    }
                    // Если cmd == 0x03 (Flash ACK) — полезной нагрузки нет, payloadSize остается 0

                    // 5. Выделяем буфер под полный кадр: 5 байт заголовка + данные + 1 байт CRC
                    byte[] fullPacket = new byte[5 + payloadSize + 1];
                    fullPacket[0] = 0xAA;
                    Buffer.BlockCopy(headerBuffer, 0, fullPacket, 1, 4);

                    // 6. Вычитываем из порта саму полезную нагрузку (payload) + 1 байт CRC
                    int targetBytesToRead = payloadSize + 1;
                    int totalBytesRead = 0;

                    while (totalBytesRead < targetBytesToRead)
                    {
                        int currentRead = await _serialPort.BaseStream.ReadAsync(fullPacket, 5 + totalBytesRead, targetBytesToRead - totalBytesRead);
                        if (currentRead == 0) break;
                        totalBytesRead += currentRead;
                    }

                    if (totalBytesRead < targetBytesToRead) continue; // Пакет оборван на середине — сброс

                    // 7. РАСЧЕТ И ПРОВЕРКА КОНТРОЛЬНОЙ СУММЫ (CRC-8 SAE J1850)
                    byte receivedCrc = fullPacket[fullPacket.Length - 1];
                    byte calculatedCrc = CalculateCRC8_SAE_J1850(fullPacket, fullPacket.Length - 1);

                    if (calculatedCrc == receivedCrc)
                    {
                        string rxDesc = $"RX [CMD: 0x{cmd:X2}, VarId: {varId}, Len: {elementsCount}]";
                        WpfCalibrator.Views.UartMonitorWindow.LogPacket("<-- RX", "#00FF00", rxDesc, fullPacket);

                        // АСИНХРОННЫЙ ТРИГГЕР: Разблокируем шлагбаум очереди Диспетчера
                        var tcs = _responseCompletionSource;
                        if (tcs != null && cmd == _expectedCmd && varId == _expectedVarId)
                        {
                            tcs.TrySetResult(true);
                        }

                        // 1. Распаковываем сырые байты полезной нагрузки в чистый плоский double[]
                        double[] decodedData = DeserializeResponsePayload(varId, elementsCount, fullPacket, payloadSize);

                        // 2. СИММЕТРИЧНЫЙ ОТВЕТ: Собираем чистый объект команды на основе сохраненных ожиданий!
                        var responseCommand = new Models.NetworkCommand
                        {
                            ModelId = modelId,
                            Cmd = (Models.LinkCommand)cmd,
                            VarId = varId,
                            DataType = _expectedDataType,
                            Rows = _expectedRows,
                            Cols = _expectedCols,
                            PayloadData = decodedData
                        };

                        // 3. Выстреливаем объект наверх в MainViewModel.OnUartPacketReceived
                        DataPacketReceived?.Invoke(responseCommand);

                    }
                    else
                    {
                        // Ошибка CRC — пакет искажен помехой или сдвигом фазы
                        string crcErrDesc = $"[CRC ERROR] Заголовок VarId: {varId}, CMD: {cmd}. Ожидалось: 0x{calculatedCrc:X2}, Пришло: 0x{receivedCrc:X2}";
                        WpfCalibrator.Views.UartMonitorWindow.LogPacket("CRC!", "#FF1111", crcErrDesc, fullPacket);
                    }
                }
                catch (Exception ex) when (_serialPort != null && _serialPort.IsOpen)
                {
                    // Попадаем сюда, только если порт жив, но произошел какой-то внутренний сбой парсинга
                    System.Diagnostics.Debug.WriteLine($"Локальный сбой пакета в UART: {ex.Message}");
                    await System.Threading.Tasks.Task.Delay(10);
                }
            }
        }
        catch (Exception)
        {
            // Попадаем сюда, когда инженер нажал "Отключить" и поток ReadAsync аварийно проснулся.
            // Мы тихо гасим фоновый поток без падения софта и вывода ошибок пользователю.
            System.Diagnostics.Debug.WriteLine("--- [INFO] Фоновый поток UART ListenAsync успешно остановлен по закрытию порта ---");
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


    /// <summary>
    /// Универсальный демаршалер: распаковывает сырые байты ответа STM32 в массив double[] 
    /// строго по правилам матлабовских типов и с учетом Column-Major структуры 2D-таблиц.
    /// </summary>
    private double[] DeserializeResponsePayload(byte varId, byte elementsCount, byte[] fullPacket, int payloadSize)
    {
        if (payloadSize == 0) return Array.Empty<double>();

        // Принимаем геометрию и размеры прямо из ОЗУ нашего сервиса связи!
        int elementSize = _expectedElementSize;
        string dataType = _expectedDataType;
        int rows = _expectedRows;
        int cols = _expectedCols;

        double[] resultPayload = new double[rows * cols];
        int byteOffset = 5; // Данные в нашем кадре fullPacket лежат строго с 5-го байта!

        // ОБРАТНЫЙ COLUMN-MAJOR РАЗБОР: Плата шлет по столбцам (Cols), внутри — по строкам (Rows)
        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                int flatIndex = (r * cols) + c;

                double decodedValue = 0.0;
                switch (dataType)
                {
                    case "double": decodedValue = BitConverter.ToDouble(fullPacket, byteOffset); break;
                    case "int64": decodedValue = BitConverter.ToInt64(fullPacket, byteOffset); break;
                    case "uint64": decodedValue = BitConverter.ToUInt64(fullPacket, byteOffset); break;
                    case "int32": decodedValue = BitConverter.ToInt32(fullPacket, byteOffset); break;
                    case "uint32": decodedValue = BitConverter.ToUInt32(fullPacket, byteOffset); break;
                    case "int16": decodedValue = BitConverter.ToInt16(fullPacket, byteOffset); break;
                    case "uint16": decodedValue = BitConverter.ToUInt16(fullPacket, byteOffset); break;
                    case "int8": decodedValue = (sbyte)fullPacket[byteOffset]; break;
                    case "uint8": decodedValue = fullPacket[byteOffset]; break;
                    case "boolean":
                    case "bool": decodedValue = fullPacket[byteOffset] > 0 ? 1.0 : 0.0; break;
                    case "single":
                    default:
                        decodedValue = BitConverter.ToSingle(fullPacket, byteOffset);
                        break;
                }

                if (flatIndex >= 0 && flatIndex < resultPayload.Length)
                {
                    resultPayload[flatIndex] = decodedValue;
                }
                byteOffset += elementSize;
            }
        }

        return resultPayload;
    }



    /// <summary>
    /// Высокуровневый конвейер: принимает команду от Диспетчера, пакует, 
    /// отправляет в порт и асинхронно ждет зеркальный ответ от STM32 с таймаутом 50мс.
    /// </summary>
    internal async System.Threading.Tasks.Task<bool> ExecuteCommandAsync(Models.NetworkCommand cmd)
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


            // 5. ТАЙМАУТ-ПРЕДОХРАНИТЕЛЬ: Ожидаем ответ 50 миллисекунд
            var timeoutTask = System.Threading.Tasks.Task.Delay(50);
            var completedTask = await System.Threading.Tasks.Task.WhenAny(_responseCompletionSource.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                string errDesc = $"[TIMEOUT] Отсутствует ответ от МК на команду {cmd.Cmd} (VarId: {cmd.VarId})";
                WpfCalibrator.Views.UartMonitorWindow.LogPacket("ERR !", "#FF5555", errDesc, Array.Empty<byte>());
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            string critDesc = $"[CRIT ERROR] Сбой транзакции: {ex.Message}";
            WpfCalibrator.Views.UartMonitorWindow.LogPacket("EXCP", "#FF0000", critDesc, Array.Empty<byte>());
            return false;
        }
        finally
        {
            _responseCompletionSource = null;
            _networkSemaphore.Release(); // Железно открываем шлагбаум
        }
    }


    /// <summary>
    /// Универсальный маршалер: пакует массив double[] из C# в сырые байты для STM32 
    /// с учетом матлабовского DataType и Column-Major структуры 2D-таблиц.
    /// </summary>
    /// <summary>
    /// Универсальный маршалер: пакует массив double[] из C# в сырые байты для STM32 
    /// строго по правилам матлабовской функции GetTypeSizeInBytes и Column-Major структуры 2D-таблиц.
    /// </summary>
    public byte[] SerializeCommandPayload(Models.NetworkCommand cmd)
    {
        // ЖЕЛЕЗОБЕТОННЫЙ ФИКС: Если команда идет на Чтение (VarRead) — payload обязан быть пустым!
        if (cmd.Cmd == Models.LinkCommand.VarRead || cmd.PayloadData == null || cmd.PayloadData.Length == 0)
        {
            return Array.Empty<byte>();
        }
        // Если полезной нагрузки нет (пакет чистого чтения телеметрии VarRead) — возвращаем пустой массив
        if (cmd.PayloadData == null || cmd.PayloadData.Length == 0)
        {
            return Array.Empty<byte>();
        }

        // 1. ЖЕСТКОЕ ВЫЧИСЛЕНИЕ РАЗМЕРА ТИПА В БАЙТАХ (Зеркало функции GetTypeSizeInBytes из MATLAB)
        int elementSize = 4; // Дефолтное значение для single
        string typeLower = cmd.DataType.ToLower().Trim();

        switch (typeLower)
        {
            case "double":
            case "int64":
            case "uint64":
                elementSize = 8;
                break;

            case "single":
            case "int32":
            case "uint32":
                elementSize = 4;
                break;

            case "int16":
            case "uint16":
                elementSize = 2;
                break;

            case "int8":
            case "uint8":
            case "boolean":
            case "bool":
                elementSize = 1;
                break;

            default:
                // Безопасный откат, если прилетел неведомый тип данных
                elementSize = 4;
                break;
        }

        // 2. Выделяем буфер под итоговую полезную нагрузку пакета
        byte[] bytePayload = new byte[cmd.Rows * cmd.Cols * elementSize];
        int byteOffset = 0;

        // 3. COLUMN-MAJOR МАРШАЛИНГ: Бежим по столбцам (Cols), внутри — по строкам (Rows)
        for (int c = 0; c < cmd.Cols; c++)
        {
            for (int r = 0; r < cmd.Rows; r++)
            {
                // Переводим двухмерные координаты в линейный Row-Major индекс C#
                int flatIndex = (r * cmd.Cols) + c;

                double rawValue = (flatIndex >= 0 && flatIndex < cmd.PayloadData.Length)
                    ? cmd.PayloadData[flatIndex]
                    : 0.0;

                // 4. ПОЛИМОРФНАЯ УПАКОВКА В БАЙТЫ (Полное соответствие матлабовской сетке типов)
                byte[] elementBytes;
                switch (typeLower)
                {
                    // --- 8 БАЙТ (64 БИТА) ---
                    case "double":
                        elementBytes = BitConverter.GetBytes(rawValue);
                        break;
                    case "int64":
                        elementBytes = BitConverter.GetBytes((long)rawValue);
                        break;
                    case "uint64":
                        elementBytes = BitConverter.GetBytes((ulong)rawValue);
                        break;

                    // --- 4 БАЙТА (32 БИТА) ---
                    case "int32":
                        elementBytes = BitConverter.GetBytes((int)rawValue);
                        break;
                    case "uint32":
                        elementBytes = BitConverter.GetBytes((uint)rawValue);
                        break;
                    case "single":
                    default:
                        elementBytes = BitConverter.GetBytes((float)rawValue);
                        break;

                    // --- 2 БАЙТА (16 БИТ) ---
                    case "int16":
                        elementBytes = BitConverter.GetBytes((short)rawValue);
                        break;
                    case "uint16":
                        elementBytes = BitConverter.GetBytes((ushort)rawValue);
                        break;

                    // --- 1 БАЙТ (8 БИТ) ---
                    case "int8":
                        elementBytes = new byte[] { (byte)((sbyte)rawValue) };
                        break;
                    case "uint8":
                        elementBytes = new byte[] { (byte)rawValue };
                        break;
                    case "boolean":
                    case "bool":
                        elementBytes = new byte[] { (byte)(rawValue > 0.5 ? 1 : 0) };
                        break;
                }

                // Копируем байты упакованного элемента в общий буфер полезной нагрузки кадра
                Buffer.BlockCopy(elementBytes, 0, bytePayload, byteOffset, elementSize);
                byteOffset += elementSize;
            }
        }

        return bytePayload;
    }



}