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
    /// <summary>
    /// Уникальный криптографический идентификатор (GUID) текущего экземпляра виджета на холсте.
    /// Используется менеджером макетов (DashboardManager) для однозначной привязки геометрии окна в JSON.
    /// </summary>

    public Guid Id { get; } = Guid.NewGuid();

    // Переменная, данные которой отображает виджет
    private VariableViewModelBase? _dataSource;


    /// <summary>
    /// Конструктор по умолчанию. Необходим для корректной работы визуального дизайнера XAML (Blend),
    /// а также для корректной инициализации фабрик динамического рендеринга.
    /// </summary>
    public WidgetViewModel()
    {
        // Пустой конструктор для Blend / XAML-Designer
    }
    /// <summary>
    /// Боевой конструктор виджета. Привязывает живую переменную ОЗУ, настраивает сквозную
    /// реактивную лямбда-подписку на прерывания UART и принудительно синхронизирует 
    /// стрелочные индикаторы и аварийные зоны под текущие физические значения.
    /// </summary>

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
        // Если этот виджет создали как 3D-поверхность, сразу же принудительно строим рельеф!
        if (ControlView == "Matrix3DSurface")
        {
            this.Rebuild3DSurfaceMesh();
        }
    }




    // Тип виджета (TextBox, Graph, Gauge...)
    private string _controlView = "TextBox";
    /// <summary>
    /// Строковый идентификатор визуального типа прибора (например, TextBox, Graph, Gauge, TimePlot).
    /// Используется движком WPF в качестве ключа переключения динамических шаблонов DataTemplate.
    /// </summary>

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
    /// <summary>
    /// Флаг ручного редактирования ячейки инженером. Переводит прибор в бесфокусный режим
    /// накопления текстовых символов в локальном буфере, блокируя обновление цифр из сети.
    /// </summary>

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

    /// <summary>
    /// Физическая координата смещения моторной точки по горизонтали (ось X) на координатной сетке.
    /// Безопасно вычисляется интерполятором только в том случае, если DataSource является табличным типом.
    /// </summary>

    public double RadarGridOffsetX => (DataSource is TableVariableViewModelBase t) ? t.RadarGridOffsetX : 0;
    /// <summary>
    /// Физическая координата смещения моторной точки по горизонтали (ось Y) на координатной сетке.
    /// Безопасно вычисляется интерполятором только в том случае, если DataSource является табличным типом.
    /// </summary>

    public double RadarGridOffsetY => (DataSource is TableVariableViewModelBase t) ? t.RadarGridOffsetY : 0;


 

    private bool _showRadarTracker = true;
    /// <summary>
    /// Настройка UI: разрешает или запрещает отображение зелёного неонового маркера-прицела
    /// текущей рабочей точки поверх сетки калибровочной таблицы.
    /// </summary>

    public bool ShowRadarTracker
    {
        get => _showRadarTracker;
        set { if (_showRadarTracker != value) { _showRadarTracker = value; OnPropertyChanged(); } }
    }


    private bool _show3DSurface;
    /// <summary>
    /// Настройка UI: переключает графический виджет таблицы в режим отрисовки 
    /// трехмерной полигональной горы рельефа Helix Toolkit.
    /// </summary>
    public bool Show3DSurface
    {
        get => _show3DSurface;
        set { if (_show3DSurface != value) { _show3DSurface = value; OnPropertyChanged(); } }
    }









    private double _left = 0;
    /// <summary>
    /// Горизонтальная координата X левой границы контейнера виджета на холсте WorkspaceCanvas (в пикселях).
    /// </summary>
    public double Left
    {
        get => _left;
        set { _left = value; OnPropertyChanged(); }
    }

    private double _top = 0;
    /// <summary>
    /// Вертикальная координата Y левой границы контейнера виджета на холсте WorkspaceCanvas (в пикселях).
    /// </summary>

    public double Top
    {
        get => _top;
        set { _top = value; OnPropertyChanged(); }
    }
    private double _width = 100;
    /// <summary>
    /// Ширина  контейнера виджета на холсте WorkspaceCanvas (в пикселях).
    /// </summary>

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
    /// <summary>
    /// Флаг фокуса окна. Срабатывает, когда калибровщик кликает по прибору мышкой,
    /// подсвечивая рамку виджета и передавая ему монопольное право на перехват горячих клавиш.
    /// </summary>

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
    /// <summary>
    /// Масштабирующий шаг изменения значения калибровки (индекса наката) при быстром 
    /// инженерном изменении ячеек кнопками PageUp и PageDown.
    /// </summary>

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

    /// <summary>
    /// Событие, уведомляющее систему привязок данных WPF о том, что какое-то свойство изменило своё значение.
    /// </summary>

    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>
    /// Вспомогательный метод вызова прерывания PropertyChanged. Использует атрибут CallerMemberName
    /// для автоматического подставления имени вызывающего свойства в рантайме.
    /// </summary>


    public void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

   private int _zIndex = 0;

    /// <summary>
    /// Порядок перекрытия слоев окна в пространстве Canvas. Гарантирует, что выделенный активный
    /// прибор всплывает на передний план над остальными окнами приборной панели.
    /// </summary>

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
    /// Флаг ориентации для одномерных таблиц-векторов (оцифровок шкал). РазворачиваетUniformGrid
    /// ячеек на холсте вертикально или горизонтально для удобства верстки макета.
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