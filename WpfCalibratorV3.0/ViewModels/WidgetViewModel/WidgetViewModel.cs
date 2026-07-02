using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace WpfCalibrator.ViewModels;

/// <summary>
/// Обертка для виджета на приборной панели.
/// </summary>
public partial class WidgetViewModel : INotifyPropertyChanged
{
    // Уникальный идентификатор виджета
    public Guid Id { get; } = Guid.NewGuid();

    // Переменная, данные которой отображает виджет
    private VariableViewModelBase? _dataSource;

 
    // ======================================================================
    // ЧАСТЬ 1: КОНСТРУКТОРЫ СВЯЗИ UI С ПОЛИМОРФНЫМ ОЗУ
    // ======================================================================
    public WidgetViewModel()
    {
        // Пустой конструктор для Blend / XAML-Designer
    }
    public WidgetViewModel(VariableViewModelBase dataSource)
    {
        // Намертво подписываем обработчик OnDataSourcePropertyChanged на изменения в UART
        DataSource = dataSource;

        // Синхронизируем стартовый шаг PageUp/PageDown (например, 1.0 для таблиц)
        IncrementStep = 1.0f;
        // 🚀 СВЯЗУЮЩИЙ МОСТ: Слушаем UART-изменения из бэкэнда данных!
        DataSource.PropertyChanged += (s, e) =>
        {
            // Если в ОЗУ изменилось физическое число, заставляем UI-текст пересчитаться! [1.14]
            if (e.PropertyName == "CurrentValue")
            {
                OnPropertyChanged(nameof(CurrentValueText));
                NotifyValueAngleChanged(); // Поворачиваем стрелку круглого прибора!
            }
        };
        // Аппаратно выставляем стрелки круглых и дуговых приборов под текущее рантайм-значение МК
        NotifyValueAngleChanged();
        RefreshAlarmTriangles();
    }




    // Тип виджета (TextBox, Graph, Gauge...)
    private string _controlView = "TextBox";
    public string ControlView
    {
        get => _controlView;
        set
        {
            if (_controlView != value)
            {
                _controlView = value;
                // Генерируем уведомление для UI. 
                // Как только оператор кликнет в меню, XAML мгновенно пересчитает триггеры!
                OnPropertyChanged(nameof(ControlView));
            }
        }
    }
    private bool _isEditing = false;
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing != value)
            {
                _isEditing = value;
                OnPropertyChanged();
            }
        }
    }


    public double RadarGridOffsetX => (DataSource is TableVariableViewModelBase t) ? t.RadarGridOffsetX : 0;
    public double RadarGridOffsetY => (DataSource is TableVariableViewModelBase t) ? t.RadarGridOffsetY : 0;


 

    private bool _showRadarTracker = true;
    /// <summary>
    /// Настройка UI: true — отображать неоновый маркер режимной точки на приборе, 
    /// false — скрыть маркер (например, для чистой визуализации 3D-рельефа) [1.14].
    /// </summary>
    public bool ShowRadarTracker
    {
        get => _showRadarTracker;
        set { if (_showRadarTracker != value) { _showRadarTracker = value; OnPropertyChanged(); } }
    }


    private bool _show3DSurface;
    /// <summary>
    /// Настройка UI: true — переключить виджет в режим отображения 3D-рельефа Helix Toolkit,
    /// false — отображать классическую плоскую таблицу ячеек.
    /// </summary>
    public bool Show3DSurface
    {
        get => _show3DSurface;
        set { if (_show3DSurface != value) { _show3DSurface = value; OnPropertyChanged(); } }
    }









    // Координаты и размер (для свободного позиционирования)
    // ИСПРАВЛЕНО: Теперь свойства уведомляют XAML о движении
    private double _left = 0;
    public double Left
    {
        get => _left;
        set { _left = value; OnPropertyChanged(); }
    }

    private double _top = 0;
    public double Top
    {
        get => _top;
        set { _top = value; OnPropertyChanged(); }
    }
    private double _width = 100;
    public double Width
    {
        get => _width;
        set
        {
            if (_width != value)
            {
                _width = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isActiveWidget = false;
    public bool IsActiveWidget
    {
        get => _isActiveWidget;
        set
        {
            if (_isActiveWidget != value)
            {
                _isActiveWidget = value;
                OnPropertyChanged();
            }
        }
    }


    private float _incrementStep = 1.0f;
    public float IncrementStep
    {
        get => _incrementStep;
        set
        {
            if (_incrementStep != value)
            {
                _incrementStep = value;
                OnPropertyChanged();
            }
        }
    }


    private double _height = 30;
    public double Height
    {
        get => _height;
        set
        {
            if (_height != value)
            {
                _height = value;
                OnPropertyChanged();
            }
        }
    }

    // Реализация INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

   private int _zIndex = 0;
    public int ZIndex
    {
        get => _zIndex;
        set
        {
            if (_zIndex != value)
            {
                _zIndex = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isVertical = false;
    /// <summary>
    /// Флаг вертикальной ориентации для одномерных таблиц-векторов на холсте
    /// </summary>
    public bool IsVertical
    {
        get => _isVertical;
        set
        {
            if (_isVertical != value)
            {
                _isVertical = value;
                OnPropertyChanged();
            }
        }
    }







}