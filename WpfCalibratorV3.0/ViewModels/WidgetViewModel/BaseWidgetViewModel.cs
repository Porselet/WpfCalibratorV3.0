using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using WpfCalibrator.Models;

namespace WpfCalibrator.ViewModels.WidgetViewModel;
/// <summary>
/// Обертка для виджета на приборной панели.
/// </summary>
public abstract partial class BaseWidgetViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Уникальный криптографический идентификатор (GUID) текущего экземпляра виджета на холсте.
    /// Используется менеджером макетов (DashboardManager) для однозначной привязки геометрии окна в JSON.
    /// </summary>

    public Guid Id { get; } = Guid.NewGuid();

    // Переменная, данные которой отображает виджет
    private VariableViewModelBase? _dataSource;


    private string _title = "";
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                // Вызываем твой родной метод уведомления WPF:
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    /// <summary>
    /// Конструктор по умолчанию. Необходим для корректной работы визуального дизайнера XAML (Blend),
    /// а также для корректной инициализации фабрик динамического рендеринга.
    /// </summary>
    public BaseWidgetViewModel()
    {
        // Пустой конструктор для Blend / XAML-Designer
    }
    /// <summary>
    /// Боевой конструктор виджета. Привязывает живую переменную ОЗУ, настраивает сквозную
    /// реактивную лямбда-подписку на прерывания UART и принудительно синхронизирует 
    /// стрелочные индикаторы и аварийные зоны под текущие физические значения.
    /// </summary>

    public BaseWidgetViewModel(VariableViewModelBase dataSource)
    {
        // Намертво подписываем обработчик OnDataSourcePropertyChanged на изменения в UART
        DataSource = dataSource;



    }




    private WidgetViewType _controlView;

    /// <summary>
    /// Получает или задает строго типизированный идентификатор визуального типа прибора. [1.14]
    /// Используется C#-фабрикой и механизмами развилок окон настроек.
    /// </summary>
    public WidgetViewType ControlView
    {
        get => _controlView;
        set
        {
            if (_controlView != value)
            {
                _controlView = value;
                OnPropertyChanged(nameof(ControlView));
            }
        }
    }





    /// <summary>
    /// Физический источник данных для прибора (его цифровая переменная в ОЗУ контроллера).
    /// При установке автоматически отписывается от старого объекта для предотвращения утечек памяти
    /// и подписывает реактивный диспетчер на события PropertyChanged нового датчика.
    /// </summary>

    public VariableViewModelBase? DataSource
    {
        get => _dataSource;
        set
        {
            // Если датчик тот же самый — ничего не делаем
            if (_dataSource == value) return;

            // Отписываемся от старого (страховка для сборщика мусора при удалении виджета)
            if (_dataSource != null) _dataSource.PropertyChanged -= OnDataSourcePropertyChanged;

            _dataSource = value;

            // Намертво привязываем уши виджета к новому датчику
            if (_dataSource != null) _dataSource.PropertyChanged += OnDataSourcePropertyChanged;

            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Реактивный сетевой диспетчер прерываний: вызывается при изменении любого свойства в связанной переменной.
    /// Перехватывает обновления из потока приёма UART и маршрутизирует их по двум независимым потокам
    /// (для одиночных скаляров-датчиков и для смещения прицела радарных UniformGrid-мишеней).
    /// </summary>

    protected virtual void OnDataSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
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