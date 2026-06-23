using System;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WpfCalibrator.Services;

/// <summary> 100626
/// Сервис для управления коммуникацией с устройством через COM-порт.
/// </summary>
public  sealed partial class CommunicationService : ICommunicationService, IDisposable
{
    // ======================================================================
    // СИНГЛТОН: Единая, потокобезопасная точка доступа к транспорту
    // ======================================================================
    private static readonly Lazy<CommunicationService> _instance =
        new Lazy<CommunicationService>(() => new CommunicationService());

    //public static CommunicationService Instance => _instance.Value;

    public static ICommunicationService AsInterface => _instance.Value;

    // Приватный конструктор — закрывает создание экземпляров извне
    private CommunicationService()
    {
        // Твой старый рабочий код инициализации объекта _serialPort и замков...
        _serialPort = new System.IO.Ports.SerialPort();
        // ... (оставь здесь всё, что у тебя было внутри старого конструктора)
    }
    // Ссылка на полный плоский список переменных из JSON Матлаба (для динамического расчета размера пакетов RX)
    private List<Models.VariableConfig>? AllVariablesConfig { get; set; }

    public event Action<string, string, string, byte[]>? OnLogPacket;

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


    // Потокобезопасное хранилище карты памяти выбранного устройства из Матлаба
    public Models.DeviceConfig? CurrentDeviceConfig { get; set; }


    public bool IsConnected => _serialPort?.IsOpen ?? false;

    public void Connect(string portName, int baudRate = 115200)
    {
        if (_serialPort != null && _serialPort.IsOpen)
        {
            return; // Если порт уже работает — уходим!
        }

        try
        {
            // ИСПРАВЛЕНО НАЧИСТО: Гасим старый токен отмены ТОЛЬКО если 
            // предыдущий порт реально существовал! Если мы подключаемся с нуля — 
            // токен не трогаем, полностью исключая лавину IOException при старте!
            if (_serialPort != null)
            {
                //_cts?.Cancel();
                _cts?.Dispose();
            }

            _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = SerialPort.InfiniteTimeout,
                WriteTimeout = 500,
                Encoding = System.Text.Encoding.UTF8
            };

            _serialPort.Open();

            // Создаем кристально чистый свежий токен для нового подключения
            _cts = new CancellationTokenSource();
            StartListening();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to open port {portName}: {ex.Message}");
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


    // 3. Прием пакетов (фоновый поток)

    /// <summary>
    /// Бесконечный фоновый поток вычерпывания физического буфера COM-порта.
    /// Полностью переведен на неблокирующий стерильный конвейер WaitForBytesAsync.
    /// </summary>
    /// <summary>
    /// Бесконечный фоновый поток вычерпывания физического буфера COM-порта.
    /// Полностью переведен на неблокирующий стерильный конвейер WaitForBytesAsync.
    /// </summary>
    /// <summary>
    /// Бесконечный фоновый поток вычерпывания физического буфера COM-порта.
    /// Полностью переведен на неблокирующий стерильный конвейер WaitForBytesAsync.
    /// </summary>
    private async System.Threading.Tasks.Task ListenAsync()
    {
        while (_serialPort != null && _serialPort.IsOpen)
        {
            try
            {
                // ======================================================================
                // 1. ИЩЕМ ПРЕАМБУЛУ 0xAA (Жестко ждем 1 байт маркера старта)
                // ======================================================================
                byte[]? preambleResult = await WaitForBytesAsync(1, 300);
                if (preambleResult == null) continue;

                byte singleByte = preambleResult[0];

                if (singleByte != 0xAA)
                {
                    System.Diagnostics.Debug.WriteLine($"[UART-GARBAGE] Пропущен байт мусора: 0x{singleByte:X2}");
                    continue;
                }

                // ======================================================================
                // 2. СТЕРИЛЬНЫЙ ПЕРЕХВАТ ЗАГЛОВКА (Жестко дочитываем остальные 4 байта)
                // ======================================================================
                byte[]? headerResult = await WaitForBytesAsync(4, 100);
                if (headerResult == null)
                {
                    System.Diagnostics.Debug.WriteLine("[UART-ERROR] Обрыв кадра: заголовок не долетел.");
                    continue;
                }

                byte modelId = headerResult[0];
                byte cmd = headerResult[1];
                byte varId = headerResult[2];
                byte elementsCount = headerResult[3];

                // Рассчитываем точную геометрию кадра полезной нагрузки
                int payloadSize = elementsCount * _expectedElementSize;
                int totalPacketSize = 5 + payloadSize + 1; // 5 байт заголовка + payload + 1 байт CRC

                // Выделяем монолитный буфер под весь пакет и упаковываем туда заголовок
                byte[] fullPacket = new byte[totalPacketSize];
                fullPacket[0] = 0xAA;
                System.Array.Copy(headerResult, 0, fullPacket, 1, 4);

                // ======================================================================
                // 3. СТЕРИЛЬНЫЙ ПЕРЕХВАТ ДАННЫХ И CRC (Жестко ждем весь остаток кадра куском!)
                // ======================================================================
                byte[]? payloadResult = await WaitForBytesAsync(payloadSize + 1, 400);
                if (payloadResult == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[UART-ERROR] Обрыв кадра: данные VarId {varId} не долетели по таймауту.");
                    continue;
                }

                // Допаковываем прилетевшие данные float и CRC в наш полный пакет
                System.Array.Copy(payloadResult, 0, fullPacket, 5, payloadResult.Length);

                // Извлекаем финальный байт контрольной суммы кадра
                byte receivedCrc = fullPacket[fullPacket.Length - 1];

                // ======================================================================
                // 4. МАТЕМАТИЧЕСКАЯ ВАЛИДАЦИЯ ТВОИМ РОДНЫМ МЕТОДОМ CalculateCRC8_SAE_J1850
                // ======================================================================
                byte calculatedCrc = CalculateCRC8_SAE_J1850(fullPacket, fullPacket.Length - 1);

                if (calculatedCrc == receivedCrc)
                {
                    string rxDesc = $"RX [CMD: 0x{cmd:X2}, VarId: {varId}, Len: {elementsCount}]";
                    //WpfCalibrator.Views.UartMonitorWindow.LogPacket("<-- RX", "#00FF00", rxDesc, fullPacket);
                    OnLogPacket?.Invoke("<-- RX", "#00FF00", rxDesc, fullPacket);
                    // АСИНХРОННЫЙ ТРИГГЕР: Разблокируем шлагбаум
                    var tcs = _responseCompletionSource;
                    if (tcs != null && (int)cmd == _expectedCmd && (int)varId == _expectedVarId)
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
                    string crcErrDesc = $"[CRC ERROR] Заголовок VarId: {varId}, CMD: {cmd}. Ожидалось: 0x{calculatedCrc:X2}, Пришло: 0x{receivedCrc:X2}";
                    System.Diagnostics.Debug.WriteLine(crcErrDesc);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UART-CRITICAL-EXCEPTION]: {ex.Message}");
                await System.Threading.Tasks.Task.Delay(20);
            }
        }
    }


    // 4. Вспомогательные методы

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