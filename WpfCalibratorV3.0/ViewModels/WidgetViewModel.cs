using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace WpfCalibrator.ViewModels;

/// <summary>
/// Обертка для виджета на приборной панели.
/// </summary>
public class WidgetViewModel : INotifyPropertyChanged
{
    // Уникальный идентификатор виджета
    public Guid Id { get; } = Guid.NewGuid();

    // Переменная, данные которой отображает виджет
    public VariableViewModel? DataSource { get; set; }

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
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ... внутри класса WidgetViewModel:

    private double _radarGridOffsetX = 0;
    public double RadarGridOffsetX
    {
        get => _radarGridOffsetX;
        set { if (_radarGridOffsetX != value) { _radarGridOffsetX = value; OnPropertyChanged(); } }
    }

    private double _radarGridOffsetY = 0;
    public double RadarGridOffsetY
    {
        get => _radarGridOffsetY;
        set { if (_radarGridOffsetY != value) { _radarGridOffsetY = value; OnPropertyChanged(); } }
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



}