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
    private void PollNextTelemetryVariable()
    {
        // Логика опроса (polling) сигналов
        // TODO: Реализуйте перебор индексов и отправку запросов на чтение
    }
}