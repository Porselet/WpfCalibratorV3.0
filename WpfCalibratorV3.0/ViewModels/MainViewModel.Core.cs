using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using WpfCalibrator.Models;
using WpfCalibrator.Services;



namespace WpfCalibrator.ViewModels;

public partial class MainViewModel : INotifyPropertyChanged
{
    // Сервисы, которые будут управлять работой приложения
    private readonly ConfigurationManager _configManager;
    private readonly IDashboardManager _dashboardManager; // <=== Исправляем тип


    // Глобальный таймер для обновления "неонового прицела" и опроса сигналов
    private readonly DispatcherTimer _updateTimer = new();

    // Флаги для отслеживания состояний
    private bool _isReadingParameters = false;
    private bool _isPollingEnabled = true;

    // Текущие индексы для циклического опроса сигналов
    private int _currentPollingIndex = 0;
    private byte _selectedModelId = 0;
    // Коллекции для UI
    public ObservableCollection<string> AvailablePorts { get; } = new();
    public ObservableCollection<DeviceConfig> DiscoveredDevices { get; } = new();
    public ObservableCollection<VariableViewModel> ParameterVariables { get; } = new();
    public ObservableCollection<VariableViewModel> TelemetryVariables { get; } = new();

    // Коллекция активных виджетов на свободном холсте (Flexible Layout)
    private ObservableCollection<WidgetViewModel> _activeWidgets = new();
    public ObservableCollection<WidgetViewModel> ActiveWidgets
    {
        get => _activeWidgets;
        set
        {
            _activeWidgets = value;
            OnPropertyChanged();
        }
    }

    // Список имён всех доступных рабочих столов (для вывода вкладок на UI)
    public ObservableCollection<string> LayoutNames { get; set; } = new();

    private string _currentLayoutName = "";
    public string CurrentLayoutName
    {
        get => _currentLayoutName;
        set
        {
            if (_currentLayoutName != value && !string.IsNullOrEmpty(value))
            {
                // Сначала незаметно сохраняем старый экран, чтобы не потерять расстановку калибровщика
                SaveCurrentLayoutInternal();

                _currentLayoutName = value;
                OnPropertyChanged();

                // Переключаем холст на отображение виджетов новой вкладки
                SwitchToLayout(value);
            }
        }
    }


    public enum DeviceConnectionState
    {
        Disconnected,       // 🔴 Отключено (Физический порт закрыт)
        Connected,          // 🟢 Подключено (Пакеты летят идеально)
        AlertReconnecting   // 🟡 Связь потеряна! (МК молчит, идет авто-реконнект)
    }


    // Текущие выбранные элементы
    private DeviceConfig? _selectedDevice;
    public DeviceConfig? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            _selectedDevice = value;
            OnPropertyChanged();
            OnDeviceChanged(); // Обработчик смены устройства
        }
    }

    private string _selectedPort = "COM1";
    public string SelectedPort
    {
        get => _selectedPort;
        set
        {
            _selectedPort = value;
            OnPropertyChanged();
        }
    }

    // Конструктор с инъекцией зависимостей
    public MainViewModel(
        ConfigurationManager configManager,
        IDashboardManager dashboardManager = null)
    {
        _configManager = configManager;
        _dashboardManager = dashboardManager ?? new NullDashboardManager();

        // Вызываем метод инициализации при создании ViewModel
        InitializeConfigurations();

        // Инициализация таймера для обновления прицела и работы калибровочной математики LUT
        _updateTimer.Interval = TimeSpan.FromMilliseconds(100);
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();

        // Загрузка доступных портов
        RefreshAvailablePorts();

        // ИСПРАВЛЕНО: Привязываем обработчик пакетов к новому Синглтону!
        CommunicationService.AsInterface.DataPacketReceived += OnUartPacketReceived;

        // ======================================================================
        // СВЯЗЫВАЕМ ДИСПЕТЧЕР ОБМЕНА С ГЛАВНЫМ ОКНОМ ДЛЯ СИНХРОНИЗАЦИИ ОЧЕРЕДЕЙ
        // ======================================================================
        Services.BusArbiter.Instance.Initialize(this);

        // Подписываемся на аппаратный детектор обрыва связи MoTeC-style
        Services.BusArbiter.OnConnectionStatusChanged += (bool isCommOk) =>
        {
            // Через асинхронный Dispatcher плавно и безопасно переключаем UI-поток
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (isCommOk)
                {
                    // Если связь восстановилась и МК ответил — зажигаем зелёный!
                    if (ConnectionState == DeviceConnectionState.AlertReconnecting)
                    {
                        ConnectionState = DeviceConnectionState.Connected;
                    }
                }
                else
                {
                    // Если поймали 3 таймаута подряд — переходим в режим тревоги (жёлтый)
                    if (ConnectionState == DeviceConnectionState.Connected)
                    {
                        ConnectionState = DeviceConnectionState.AlertReconnecting;
                    }
                }
            }));
        };



    }




    // Реализация INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }



    // 1. Обработчик тика таймера (обновление прицела и polling)
    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        // 1. Обновляем координаты прицела для всех таблиц
        foreach (var param in ParameterVariables)
        {
            if (param.IsLutLinked)
            {
                var axisXValues = param.BoundAxisX!.MatrixData.Cast<float>().ToArray();
                var axisYValues = param.BoundAxisY!.MatrixData.Cast<float>().ToArray();

                param.CalculateWorkingPoint(
                    (float)param.BoundInputX!.CurrentValue,
                    (float)param.BoundInputY!.CurrentValue,
                    axisXValues,
                    axisYValues
                );
            }
        }

        // 2. Если включено, запускаем опрос сигналов (polling)
        if (_isPollingEnabled && CommunicationService.AsInterface.IsConnected)
        {
            //PollNextTelemetryVariable();
        }
    }

    // 2. Метод для обновления списка доступных COM-портов
    private void RefreshAvailablePorts()
    {
        AvailablePorts.Clear();
        foreach (var port in SerialPort.GetPortNames())
        {
            AvailablePorts.Add(port);
        }

        // Выбираем порт по умолчанию (если он есть в конфиге)
        if (_configManager.LastUsedComPort != null)
        {
            SelectedPort = _configManager.LastUsedComPort;
        }
        else
        {
            SelectedPort = AvailablePorts.FirstOrDefault() ?? "COM1";
        }
    }


    // 1. Главное состояние связи в ОЗУ
    private DeviceConnectionState _connectionState = DeviceConnectionState.Disconnected;

    /// <summary>
    /// Текущее физическое состояние подключения к плате в ОЗУ.
    /// </summary>
    public DeviceConnectionState ConnectionState
    {
        get => _connectionState;
        set
        {
            if (_connectionState == value) return;
            _connectionState = value;
            OnPropertyChanged(nameof(ConnectionState));

            // Автоматически уведомляем WPF об изменении уникальных статусных свойств
            OnPropertyChanged(nameof(DeviceStatusText));
            OnPropertyChanged(nameof(DeviceStatusColor));
        }
    }

    /// <summary>
    /// УНИКАЛЬНОЕ ИМЯ: Текстовое описание состояния железа под кнопкой.
    /// </summary>
    public string DeviceStatusText => _connectionState switch
    {
        DeviceConnectionState.Disconnected => "Отключено",
        DeviceConnectionState.Connected => "Подключено к МК",
        DeviceConnectionState.AlertReconnecting => "СВЯЗЬ ПОТЕРЯНА! Восстановление...",
        _ => "Неизвестно"
    };

    /// <summary>
    /// УНИКАЛЬНОЕ ИМЯ: Цвет светодиода для кружка под кнопкой.
    /// </summary>
    public string DeviceStatusColor => _connectionState switch
    {
        DeviceConnectionState.Disconnected => "#FF3B30", // Сочный Автоспортивный Красный
        DeviceConnectionState.Connected => "#34C759", // Яркий Гоночный Зелёный
        DeviceConnectionState.AlertReconnecting => "#FFCC00", // Предупреждающий Сигнальный Жёлтый
        _ => "#8E8E93"  // Серый
    };




}