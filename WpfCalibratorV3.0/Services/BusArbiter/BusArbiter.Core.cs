using System;
using System.Collections.Generic;
using WpfCalibrator.Models;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.Services
{
    /// <summary>
    /// Диспетчер обмена (Arbiter) — монопольный хозяин шины и очередей пакетов.
    /// Реализует приоритетное планирование (Калибровки > Телеметрия).
    /// </summary>
    public sealed partial class BusArbiter : IBusArbiter
    {
        // 1. СИНГЛТОН: Единая точка доступа к Диспетчеру со всего приложения
        private static readonly Lazy<BusArbiter> _instance = new Lazy<BusArbiter>(() => new BusArbiter());

        public static IBusArbiter AsInterface => _instance.Value;

        public static BusArbiter Inst => _instance.Value;

        // 2. ОЧЕРЕДИ ПАКЕТОВ (ВЫСОКОУРОВНЕВЫЕ ОБЪЕКТЫ КОМАНД)
        // Приоритетная очередь для команд инженера (Запись 0x01, Флеш 0x03)
        private readonly Queue<NetworkCommand> _commandQueue = new Queue<NetworkCommand>();

        // Кольцевой список для фонового циклического опроса датчиков телеметрии (Чтение 0x02)
        private readonly List<NetworkCommand> _telemetryLoop = new List<NetworkCommand>();

        // Текущий индекс датчика в кольцевом списке телеметрии
        private int _currentTelemetryIndex = 0;

        // Потокобезопасный замок для защиты очередей от одновременного доступа из разных потоков
        private readonly object _queueLock = new object();

        private bool _isRunning = false;
        // НОВОЕ: Открываем статус работы планировщика для вьюмоделей
        public bool IsRunning => _isRunning;

        // Флаг-замок: блокирует фоновый опрос телеметрии на время прогрузки калибровочных таблиц
        public bool IsLoadingParameters { get; set; } = false;

        private int _consecutiveTimeouts = 0; // Счетчик последовательных таймаутов

        // Ссылка на главное окно/модель для мониторинга виджетов
        private ViewModels.MainViewModel? _mainVm;

        // Событие для уведомления MainViewModel об изменении состояния связи:
        // true = связь есть, false = связь потеряна
        public static event Action<bool>? OnConnectionStatusChanged;


        // Закрытый конструктор, чтобы никто не мог создать Диспетчер через new()
        private BusArbiter()
        {
        }


        /// <summary>
        /// Инициализирует Диспетчер и привязывает его к динамической коллекции виджетов холста
        /// </summary>
        public void Initialize(ViewModels.MainViewModel mainVm)
        {
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));

            // Намертво подписываемся на пульс холста: добавление, удаление, смена экранов
            _mainVm.ActiveWidgets.CollectionChanged += OnActiveWidgetsChanged;

            // Сразу собираем первое стартовое кольцо опроса
            RebuildTelemetryLoop();
        }

        /// <summary>
        /// Автоматический триггер: срабатывает при любом изменении состава виджетов на экране
        /// </summary>
        private void OnActiveWidgetsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Виджеты изменились или сменился рабочий стол -> мгновенно перестраиваем кольцо опроса
            RebuildTelemetryLoop();
        }

        /// <summary>
        /// Потокобезопасный пересчет и сборка постоянных объектов команд для телеметрии
        /// </summary>


        /// <summary>
        /// Потокобезопасный пересчет и сборка постоянных объектов команд для телеметрии
        /// </summary>
        private void RebuildTelemetryLoop()
        {
            // ЖЕЛЕЗОБЕТОННЫЙ ЩИТ: Если главная модель или коллекция виджетов еще не созданы в ОЗУ — выходим!
            if (_mainVm == null || _mainVm.ActiveWidgets == null) return;

            lock (_queueLock)
            {
                // Очищаем старое кольцо опроса
                _telemetryLoop.Clear();
                _currentTelemetryIndex = 0;

                // Извлекаем из активных виджетов только уникальные переменные ТЕЛЕМЕТРИИ (IsParam == false)
                var uniqueTelemetrySources = new HashSet<ViewModels.VariableViewModelBase>();

                // Модернизированный обход виджетов в RebuildTelemetryLoop:
                foreach (var widget in _mainVm.ActiveWidgets)
                {
                    if (widget?.DataSource == null) continue;

                    // 1. Собираем стандартные датчики
                    if (!widget.DataSource.IsParam)
                        uniqueTelemetrySources.Add(widget.DataSource);

                    // 2. 🔥 НАШ СВЯЗУЮЩИЙ ШЛЮЗ ДЛЯ ПРИЦЕЛА ТАБЛИЦ (1D/3D) [1.14]
                    if (widget.DataSource is TableVariableViewModelBase tableVar)
                    {
                        // Добавляем X (Обороты)
                        if (tableVar.BoundInputX != null && !tableVar.BoundInputX.IsParam)
                            uniqueTelemetrySources.Add(tableVar.BoundInputX);

                        // Добавляем Y (Наддув) для 3D
                        if (tableVar is Map3DVariableViewModel map3D && map3D.BoundInputY != null && !map3D.BoundInputY.IsParam)
                            uniqueTelemetrySources.Add(map3D.BoundInputY);
                    }
                }


                // Для каждого уникального датчика на холсте создаем ОДИН постоянный объект команды чтения
                foreach (var source in uniqueTelemetrySources)
                {
                    if (source == null) continue;

                    // На бумаге: Дефолтная мерность для одиночного датчика (Скаляра)
                    int pollRows = 1;
                    int pollCols = 1;

                    // Вычисляем реальные габариты, только если датчик оказался таблицей/кривой
                    if (source is TableVariableViewModelBase tableVar)
                    {
                        pollCols = tableVar.Cols;
                        pollRows = (tableVar is Map3DVariableViewModel map3D) ? map3D.Rows : 1;
                    }

                    // ======================================================================
                    // СБОРКА КОМАНДЫ ДЛЯ СВЯЗИ С ОБНОВЛЕННОЙ МЕРНОСТЬЮ
                    // ======================================================================
                    var readCmd = new Models.NetworkCommand
                    {
                        ModelId = source.ModelId,
                        Cmd = Models.LinkCommand.VarRead, // Команда чтения (0x02)
                        VarId = (byte)source.Id,
                        DataType = source.Type,
                        Rows = pollRows, // 🔥 БЕЗОПАСНО: 1 для скаляров/кривых, Rows для 3D карт!
                        Cols = pollCols, // 🔥 БЕЗОПАСНО: 1 для скаляров, Cols для осей/кривых/карт!
                        PayloadData = null
                    };

                    // Заталкиваем команду в очередь Арбитра
                    //Services.BusArbiter.AsInterface.PushCommand(readCmd);

                    _telemetryLoop.Add(readCmd);
                }
            }
        }

        /// <summary>
        /// Потокобезопасный пуш калибровочной команды в приоритетную очередь с защитой от дубликатов
        /// </summary>
        public void PushCommand(NetworkCommand command)
        {
            if (command == null) return;

            lock (_queueLock)
            {
                // 🔥 ЖЕСТКИЙ АВТОСПОРТИВНЫЙ ФИКС: Проверяем, нет ли уже ТОЧНО ТАКОЙ ЖЕ команды в очереди!
                // Если параллельный поток интерфейса пытается повторно запушить чтение/запись того же VarId
                // до того, как Диспетчер успел обработать прошлый пакет — мы просто тихо игнорируем дубликат!
                foreach (var existingCmd in _commandQueue)
                {
                    if (existingCmd.Cmd == command.Cmd && existingCmd.VarId == command.VarId)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ARBITER-BLOCK] Заблокирован дубликат команды {command.Cmd} для VarId: {command.VarId}");
                        return; // Выходим из метода, не плодим мусор в ОЗУ!
                    }
                }

                // Если команда уникальна — со спокойной совестью ставим в очередь
                _commandQueue.Enqueue(command);
            }
        }






        /// <summary>
        /// Потокобезопасно запускает ОДИН фоновый цикл планировщика обмена
        /// </summary>
        public void Start()
        {
            lock (_queueLock)
            {
                // Железный предохранитель: если поток УЖЕ запущен — 
                // игнорируем повторный вызов и ни в коем случае не плодим дубликаты задач!
                if (_isRunning)
                {
                    System.Diagnostics.Debug.WriteLine("[ARBITER] Попытка повторного запуска заблокирована. Планировщик уже работает.");
                    return;
                }

                _isRunning = true;

                // Запускаем строго ОДНУ фоновую задачу на всё время жизни приложения
                System.Threading.Tasks.Task.Run(async () => await WorkerLoopAsync());
                System.Diagnostics.Debug.WriteLine("[ARBITER] Фоновый конвейер обмена УСПЕШНО ЗАПУЩЕН.");
            }
        }

        /// <summary>
        /// Потокобезопасно останавливает цикл планировщика
        /// </summary>
        public void Stop()
        {
            lock (_queueLock)
            {
                _isRunning = false;
                System.Diagnostics.Debug.WriteLine("[ARBITER] Сигнал остановки планировщика отправлен.");
            }
        }

        /// <summary>
        /// Главный бесконечный конвейер распределения пакетов по шине обмена
        /// </summary>

        /// <summary>
        /// Освобождает системные ресурсы планировщика шины.
        /// Принудительно останавливает фоновый цикл обмена и очищает приоритетную очередь команд.
        /// </summary>
        public void Dispose()
        {
            // Жестко останавливаем фоновый поток планировщика
            Stop();

            lock (_queueLock)
            {
                // Очищаем ОЗУ приоритетной очереди, чтобы помочь сборщику мусора .NET
                _commandQueue?.Clear();
            }

            System.Diagnostics.Debug.WriteLine("--- [INFO] Ресурсы BusArbiter успешно утилизированы деструктором ---");
        }


    }
}
