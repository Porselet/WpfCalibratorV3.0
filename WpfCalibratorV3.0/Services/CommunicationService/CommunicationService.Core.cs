using System;
using System.IO.Ports;
using System.Threading;
using WpfCalibrator.Models;

namespace WpfCalibrator.Services
{
    /// <summary>
    /// АППАРАТНОЕ ЯДРО СЕРВИСА СВЯЗИ (Часть 1: Конфигурация и управление дескриптором порта).
    /// Отвечает за жизненный цикл подключения к BlackPill и безопасное переключение потоков Windows.
    /// </summary>
    public sealed partial class CommunicationService : ICommunicationService, IDisposable
    {
        // ======================================================================
        // 🎯 СИНГЛТОН СВЯЗИ (Глобальный заголовок, как в Си)
        // ======================================================================
        private static readonly Lazy<CommunicationService> _instance = new Lazy<CommunicationService>(() => new CommunicationService());
        public static ICommunicationService AsInterface => _instance.Value;

        // ======================================================================
        // 💾 НИЗКОУРОВНЕВОЕ ЖЕЛЕЗО И ПОТОКИ (Аппаратный слой памяти)
        // ======================================================================
        private SerialPort? _serialPort;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Task? _listeningTask;
        private readonly object _lock = new object();

        // ======================================================================
        // 🛡 АСИНХРОННЫЕ ТРИГГЕРЫ ОЖИДАНИЯ HANDSHAKE (ОЗУ Шлагбаум)
        // ======================================================================
        private TaskCompletionSource<bool>? _responseCompletionSource;

        // Volatile защищает регистры-ожидания от агрессивных оптимизаций JIT-компилятора .NET 10
        private volatile int _expectedCmd;
        private volatile int _expectedVarId;
        private volatile int _expectedElementSize = 4;

        private string _expectedDataType;
        private int _expectedRows;
        private int _expectedCols;

        // ======================================================================
        // 📢 РЕАЛИЗАЦИЯ ИНТЕРФЕЙСНЫХ СВОЙСТВ И СОБЫТИЙ (Контракт)
        // ======================================================================
        public bool IsConnected => _serialPort?.IsOpen ?? false;
        public DeviceConfig? CurrentDeviceConfig { get; set; }

        public event Action<NetworkCommand>? DataPacketReceived;
        public event Action<string, string, string, byte[]>? OnLogPacket;

        // Асинхронный семафор-шлагбаум: разрешает отправку строго одного кадра за раз
        private readonly System.Threading.SemaphoreSlim _networkSemaphore = new System.Threading.SemaphoreSlim(1, 1);
        /// <summary>
        /// Закрытый конструктор синглтона калибратора.
        /// </summary>
        private CommunicationService() { }

        // ======================================================================
        // 🚀 АППАРАТНЫЕ МЕТОДЫ УПРАВЛЕНИЯ ПОРТОМ (Вход/Выход)
        // ======================================================================

        /// <summary>
        /// АППАРАТНЫЙ ПУСК: Инициализирует SerialPort, открывает канал в ОС Windows 
        /// и запускает фоновый конвейер неблокирующего вычерпывания шины UART.
        /// </summary>
        public void Connect(string portName, int baudRate)
        {
            if (_serialPort != null && _serialPort.IsOpen) return;

            try
            {
                if (_serialPort != null)
                {
                    _cts?.Dispose();
                }

                _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = SerialPort.InfiniteTimeout,
                    WriteTimeout = 500,
                    Encoding = System.Text.Encoding.UTF8
                };

                _serialPort.Open();

                _cts = new CancellationTokenSource();
                StartListening();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UART-ERROR] Не удалось открыть порт {portName}: {ex.Message}");
            }
        }

        /// <summary>
        /// АППАРАТНЫЙ СТОП: Шлет сигнал отмены токену, жестко гасит фоновый поток чтения,
        /// закрывает системный дескриптор в Windows и очищает ОЗУ.
        /// </summary>
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
        /// Внутренний пускач фоновой задачи приёма.
        /// </summary>
        private void StartListening()
        {
            if (_listeningTask != null && !_listeningTask.IsCompleted) return;
            _listeningTask = Task.Run(ListenAsync, _cts.Token);
        }

        /// <summary>
        /// Внутренний системный хелпер для отправки пакетов в окно UartMonitorWindow.
        /// </summary>
        private void LogPacket(string prefix, string colorHex, string description, byte[] packet)
        {
            OnLogPacket?.Invoke(prefix, colorHex, description, packet);
        }
    }
}
