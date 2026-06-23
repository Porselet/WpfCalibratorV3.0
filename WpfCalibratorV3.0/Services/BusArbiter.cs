using System;
using System.Collections.Generic;
using WpfCalibrator.Models;

namespace WpfCalibrator.Services
{
    /// <summary>
    /// Диспетчер обмена (Arbiter) — монопольный хозяин шины и очередей пакетов.
    /// Реализует приоритетное планирование (Калибровки > Телеметрия).
    /// </summary>
    public sealed class BusArbiter
    {
        // 1. СИНГЛТОН: Единая точка доступа к Диспетчеру со всего приложения
        private static readonly Lazy<BusArbiter> _instance = new Lazy<BusArbiter>(() => new BusArbiter());
        public static BusArbiter Instance => _instance.Value;

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

        // Событие для уведомления MainViewModel об изменении состояния связи:
        // true = связь есть, false = связь потеряна
        public static event Action<bool>? OnConnectionStatusChanged;


        // Закрытый конструктор, чтобы никто не мог создать Диспетчер через new()
        private BusArbiter()
        {
        }

        // Ссылка на главное окно/модель для мониторинга виджетов
        private ViewModels.MainViewModel? _mainVm;

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
                var uniqueTelemetrySources = new HashSet<ViewModels.VariableViewModel>();

                foreach (var widget in _mainVm.ActiveWidgets)
                {
                    // Безопасная проверка: страхуемся от пустых виджетов на холсте
                    if (widget != null && widget.DataSource != null && !widget.DataSource.IsParam)
                    {
                        uniqueTelemetrySources.Add(widget.DataSource);
                    }
                }

                // Для каждого уникального датчика на холсте создаем ОДИН постоянный объект команды чтения
                foreach (var source in uniqueTelemetrySources)
                {
                    if (source == null) continue;

                    var readCommand = new NetworkCommand
                    {
                        ModelId = source.ModelId,
                        Cmd = LinkCommand.VarRead,
                        VarId = (byte)source.Id,
                        DataType = source.Type,
                        Rows = source.Rows,
                        Cols = source.Cols,
                        PayloadData = null
                    };

                    _telemetryLoop.Add(readCommand);
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
        /// Главный арбитражный мозг: извлекает СЛЕДУЮЩУЮ транзакцию на отправку.
        /// Калибровки имеют абсолютный приоритет над телеметрией!
        /// </summary>
        public NetworkCommand? GetNextCommand()
        {
            lock (_queueLock)
            {
                // 1. ПРИОРИТЕТ №1: Если инженер нажал кнопку — забираем калибровку без очереди!
                if (_commandQueue.Count > 0)
                {
                    return _commandQueue.Dequeue();
                }

                // 🔥 ЖЕЛЕЗОБЕТОННЫЙ ШИННЫЙ ЗАМОК:
                // Если приоритетных калибровок в очереди нет, но идет стартовая массовая вычитка 
                // параметров (IsLoadingParameters == true) — мы категорически запрещаем фоновой телеметрии 
                // лезть на шину UART! Возвращаем null, полностью освобождая шину для ответов таблиц.
                if (IsLoadingParameters)
                {
                    return null;
                }

                // 2. ПРИОРИТЕТ №2: Если калибровок нет — берем следующий датчик из кольца телеметрии
                if (_telemetryLoop.Count > 0)
                {
                    // Проверяем границы индекса (на случай, если кольцо только что перестроилось)
                    if (_currentTelemetryIndex >= _telemetryLoop.Count)
                    {
                        _currentTelemetryIndex = 0;
                    }

                    var telemetryCmd = _telemetryLoop[_currentTelemetryIndex];

                    // Смещаем указатель на следующий шаг кольца
                    _currentTelemetryIndex = (_currentTelemetryIndex + 1) % _telemetryLoop.Count;

                    return telemetryCmd;
                }

                // Если экран пустой и команд нет — на шине полная тишина
                return null;
            }
        }





        /// <summary>
        /// Возвращает true, если кольцевой проход телеметрии завершен (нужно для окон тишины Task.Delay)
        /// </summary>
        public bool IsTelemetryLoopFinished()
        {
            lock (_queueLock)
            {
                // Круг считается завершенным, когда указатель вернулся на стартовый нулевой элемент
                return _currentTelemetryIndex == 0;
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
        /// Главный бесконечный конвейер распределения пакетов по шине обмена
        /// </summary>
        private async System.Threading.Tasks.Task WorkerLoopAsync()
        {
            while (_isRunning)
            {
                // 1. Спрашиваем у арбитражного мозга: какую команду выдать в шину следующей?
                var nextCmd = GetNextCommand();

                if (nextCmd == null)
                {
                    // Если экран пустой — спим 50 мс и проверяем заново, не забивая ЦП
                    await System.Threading.Tasks.Task.Delay(50);
                    continue;
                }

                // ======================================================================
                // 2. ЭТАП ТРАНСПОРТА: Физическая отправка и ожидание Handshake от платы
                // ======================================================================

                // ТЕСТОВЫЙ МАРКЕР ОЧЕРЕДИ: Печатаем в дебаг, кто именно летит в провод
                //System.Diagnostics.Debug.WriteLine($"[ARBITER-TX] Выстрел кадра! CMD: {nextCmd.Cmd}, VarId: {nextCmd.VarId}, Элементов: {nextCmd.Rows * nextCmd.Cols}. Время: {DateTime.Now:mm:ss.fff}");


                // Используем наш глобальный Синглтон вместо удаленного локального поля!
                bool isSuccess = await CommunicationService.Instance.ExecuteCommandAsync(nextCmd);

                // Если транзакция сорвалась (например, обрыв связи или таймаут), 
                // делаем микро-паузу и идем на следующий круг цикла
                // ======================================================================
                // ПРОВЕРКА УСПЕХА ТРАНСАКЦИИ С АВТО-РЕКОННЕКТОМ
                // ======================================================================
                if (!isSuccess)
                {
                    _consecutiveTimeouts++;

                    // Если плата молчит уже 3 пакета подряд — объявляем аварию на шине!
                    if (_consecutiveTimeouts == 3)
                    {
                        System.Diagnostics.Debug.WriteLine("🚨 [BUS-ALERT] Связь с МК потеряна! Переходим в режим авто-восстановления...");

                        // Стреляем событием наверх в MainViewModel (пусть перекрасит UI в желтый/красный)
                        OnConnectionStatusChanged?.Invoke(false);
                    }

                    // В режиме аварии увеличиваем паузу между попытками до 100 мс, 
                    // чтобы не насиловать процессор и дать Windows время очухаться
                    await System.Threading.Tasks.Task.Delay(100);
                    continue;
                }

                // ЕСЛИ ПАКЕТ ПРИЛЕТЕЛ УСПЕШНО:
                if (_consecutiveTimeouts >= 3)
                {
                    System.Diagnostics.Debug.WriteLine("🏁 [BUS-RECOVER] Связь с МК успешно восстановлена в рантайме!");

                    // Возвращаем статус "Все ОК" (зеленый свет в UI)
                    OnConnectionStatusChanged?.Invoke(true);
                }

                _consecutiveTimeouts = 0; // Кристально обнуляем счетчик аварий



                // ======================================================================
                // 3. МЕТРОНОМ ШИНЫ: ЖЕСТКОЕ ОКНО ТИШИНЫ (INTER-PACKET DELAY)
                // ======================================================================
                // ИСПРАВЛЕНО: Убрали несуществующий token! 
                // Делаем паузу в 20 миллисекунд ОБЯЗАТЕЛЬНОЙ после абсолютно каждого пакета.
                // Неважно, читали мы телеметрию или прогружали стартовые параметры таблиц — 
                // асинхронным триггерам C# требуется окно тишины на закрытие транзакции в ОЗУ.
                await System.Threading.Tasks.Task.Delay(20);
            }
        }



    }
}
