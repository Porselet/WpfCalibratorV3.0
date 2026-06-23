using System;
using WpfCalibrator.Models;

namespace WpfCalibrator.Services
{
    /// <summary>
    /// Интерфейс планировщика и арбитра шины обмена.
    /// Определяет правила управления очередями команд калибровок и кольцом опроса телеметрии.
    /// </summary>
    public interface IBusArbiter : IDisposable
    {
        /// <summary>
        /// Состояние конвейера: true, если фоновый поток WorkerLoopAsync запущен и опрашивает шину.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Управляющий флаг-замок: блокирует опрос фонового кольца телеметрии во время массовой загрузки параметров.
        /// </summary>
        bool IsLoadingParameters { get; set; }

        /// <summary>
        /// Запускает фоновый асинхронный поток циклического допроса шины UART.
        /// </summary>
        void Start();

        /// <summary>
        /// Останавливает фоновый поток планировщика и замораживает обмен данными.
        /// </summary>
        void Stop();

        /// <summary>
        /// Заталкивает ручную команду (запись/чтение параметров или таблиц) в приоритетную очередь Dequeue.
        /// </summary>
        /// <param name="cmd">Объект сформированной сетевой команды.</param>
        void PushCommand(NetworkCommand cmd);

        /// <summary>
        /// Событие для уведомления MainViewModel об изменении состояния связи:
        /// true = связь есть, false = связь потеряна        
        /// </summary>
        public static event Action<bool>? OnConnectionStatusChanged;

        /// <summary>
        /// Инициализирует Диспетчер и привязывает его к динамической коллекции виджетов холста
        /// </summary>
        public void Initialize(ViewModels.MainViewModel mainVm);

    }
}
