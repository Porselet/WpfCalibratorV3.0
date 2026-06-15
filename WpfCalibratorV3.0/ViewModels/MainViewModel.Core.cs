using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using WpfCalibrator.Models;
using WpfCalibrator.Services;
using System.Collections.ObjectModel;


namespace WpfCalibrator.ViewModels;

public partial class MainViewModel : INotifyPropertyChanged
{
    // Сервисы, которые будут управлять работой приложения
    private readonly CommunicationService _commService;
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
        CommunicationService commService,
        ConfigurationManager configManager,
        IDashboardManager dashboardManager = null)
    {
        _commService = commService;
        _configManager = configManager;

        _dashboardManager = dashboardManager ?? new NullDashboardManager();

        // Вызываем метод инициализации при создании ViewModel
        InitializeConfigurations();

        // Инициализация таймера для обновления прицела и опроса сигналов
        _updateTimer.Interval = TimeSpan.FromMilliseconds(100);
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();

        // Загрузка доступных портов
        RefreshAvailablePorts();
        _commService.DataPacketReceived += OnUartPacketReceived;
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
                    param.BoundInputX!.CurrentValue,
                    param.BoundInputY!.CurrentValue,
                    axisXValues,
                    axisYValues
                );
            }
        }

        // 2. Если включено, запускаем опрос сигналов (polling)
        if (_isPollingEnabled && _commService.IsConnected)
        {
            PollNextTelemetryVariable();
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

    // Вспомогательный метод для опроса следующего сигнала
    private async void PollNextTelemetryVariable()
    {
        // 1. Фильтруем активные виджеты: ищем среди них только сигналы телеметрии (IsParam = false)
        var activeSignals = ActiveWidgets
            .Where(w => w.DataSource != null && !w.DataSource.IsParam)
            .Select(w => w.DataSource)
            .ToList();

        if (activeSignals.Count == 0) return;

        // 2. Индекс циклического перебора ( Round-Robin )
        if (_currentPollingIndex >= activeSignals.Count)
        {
            _currentPollingIndex = 0;
        }

        var variableToPoll = activeSignals[_currentPollingIndex];
        _currentPollingIndex++;

        // 🔥 ИСПРАВЛЕНИЕ: Прямая асинхронная отправка параметров строго по вашей карте байт из app_link.c!
        try
        {
            byte cmd = 0x02; // CMD_VAR_READ (Операция чтения)
            byte modelId = variableToPoll.ModelId;
            byte varId = (byte)variableToPoll.Id; // Однобайтовый ID из прошивки
            byte elementsCount = (byte)(variableToPoll.Rows * variableToPoll.Cols);

            // При чтении Payload (данные) пустой — шлем пустой массив байт
            byte[] emptyPayload = Array.Empty<byte>();

            if (_commService != null && _commService.IsConnected)
            {
                // Раскомментируем и вызываем твой реальный Task-метод!
                await _commService.SendPacketAsync(modelId, cmd, varId, elementsCount, emptyPayload);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UART Polling Error]: {ex.Message}");
        }
    }

}