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


    /// <summary>
    /// Координата X для треугольника минимального аларма (расчет на уровне виджета)
    /// </summary>
    public double MinAlarmX
    {
        get
        {
            if (DataSource == null || float.IsNegativeInfinity(DataSource.MinLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -100; // Прячем за экран

            double pct = (DataSource.MinLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            return (pct * 230.0) - 5.0;
        }
    }

    /// <summary>
    /// Координата X для треугольника максимального аларма (расчет на уровне виджета)
    /// </summary>
    public double MaxAlarmX
    {
        get
        {
            if (DataSource == null || float.IsPositiveInfinity(DataSource.MaxLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -100; // Прячем за экран

            double pct = (DataSource.MaxLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            return (pct * 230.0) - 5.0;
        }
    }

    /// <summary>
    /// Метод для принудительного обновления графики треугольников извне
    /// </summary>
    public void RefreshAlarmTriangles()
    {
        OnPropertyChanged(nameof(MinAlarmX));
        OnPropertyChanged(nameof(MaxAlarmX));

        // НОВОЕ: Пинаем графику вертикальных треугольников
        OnPropertyChanged(nameof(MinAlarmY));
        OnPropertyChanged(nameof(MaxAlarmY));

        // НОВОЕ: Обновляем углы треугольников на круглом приборе!
        OnPropertyChanged(nameof(GaugeMinAlarmAngle));
        OnPropertyChanged(nameof(GaugeMaxAlarmAngle));

        // НОВОЕ: Пинаем треугольники алармов дугового прибора!
        OnPropertyChanged(nameof(ArcGaugeMinAlarmAngle));
        OnPropertyChanged(nameof(ArcGaugeMaxAlarmAngle));

        OnPropertyChanged(nameof(PlotMinLimitY));
        OnPropertyChanged(nameof(PlotMaxLimitY));
    }


    /// <summary>
    /// Открытый метод для принудительного уведомления UI об изменении угла живой стрелки
    /// </summary>
    public void NotifyValueAngleChanged()
    {
        OnPropertyChanged(nameof(GaugeValueAngle));
        OnPropertyChanged(nameof(ArcGaugeValueAngle));

        OnPropertyChanged(nameof(ArcBarFillLength));
    }

    private System.Windows.Media.PointCollection _plotPoints = new System.Windows.Media.PointCollection();
    /// <summary>
    /// Коллекция точек для отрисовки ползущего осциллографа TimePlot
    /// </summary>
    public System.Windows.Media.PointCollection PlotPoints
    {
        get => _plotPoints;
        set { _plotPoints = value; OnPropertyChanged(); }
    }
    /// <summary>
    /// Добавляет новое значение в историю заезда и сдвигает график осциллографа влево
    /// </summary>
    public void AppendPlotPoint(double newValue)
    {
        if (DataSource == null) return;

        // 1. Масштабируем значение в пиксели Y (0..100). Инвертируем (1.0 - pct), так как в WPF Y=0 - это верх окна!
        double range = DataSource.ScaleMax - DataSource.ScaleMin;
        double pct = (range > 0) ? (newValue - DataSource.ScaleMin) / range : 0.5;
        if (pct < 0) pct = 0;
        if (pct > 1) pct = 1;
        double pixelY = (1.0 - pct) * 100.0;

        // 2. Локально копируем коллекцию, чтобы не вызывать мерцания UI при поштучном изменении
        var currentPoints = new System.Windows.Media.PointCollection(_plotPoints);

        if (currentPoints.Count == 0)
        {
            // Если график пустой, заполняем его стартовой линией на всю ширину экрана (100 точек с шагом 2px)
            for (int i = 0; i < 100; i++)
            {
                currentPoints.Add(new System.Windows.Point(i * 2.0, pixelY));
            }
        }
        else
        {
            // Если точки есть, сдвигаем их все влево на 2 пикселя
            for (int i = 0; i < currentPoints.Count; i++)
            {
                var p = currentPoints[i];
                currentPoints[i] = new System.Windows.Point(p.X - 2.0, p.Y);
            }

            // Удаляем самую старую точку, которая улетела за левый край экрана (X < 0)
            if (currentPoints.Count > 0 && currentPoints[0].X < 0)
            {
                currentPoints.RemoveAt(0);
            }

            // Добавляем свежую точку на самый правый край (X = 200 пикселей)
            currentPoints.Add(new System.Windows.Point(200.0, pixelY));
        }

        // 3. Аппаратно обновляем свойство для мгновенной перерисовки в XAML
        PlotPoints = currentPoints;
    }
    /// <summary>
    /// Пиксельная координата Y для линии минимального аларма (0..100)
    /// </summary>
    public double PlotMinLimitY
    {
        get
        {
            if (DataSource == null || float.IsNegativeInfinity(DataSource.MinLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -100; // Уводим линию далеко за экран, если лимит отключен

            double pct = (DataSource.MinLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            // Инвертируем координату Y (1.0 - pct), так как Y=0 — это верх рабочей области
            return (1.0 - pct) * 100.0;
        }
    }

    /// <summary>
    /// Пиксельная координата Y для линии максимального аларма (0..100)
    /// </summary>
    public double PlotMaxLimitY
    {
        get
        {
            if (DataSource == null || float.IsPositiveInfinity(DataSource.MaxLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -100;

            double pct = (DataSource.MaxLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            return (1.0 - pct) * 100.0;
        }
    }


    /// <summary>
    /// Координата Y для треугольника минимального аларма на вертикальном слайдере (0..180 пикселей)
    /// </summary>
    public double MinAlarmY
    {
        get
        {
            if (DataSource == null || float.IsNegativeInfinity(DataSource.MinLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -100; // Прячем за экран

            // Находим процентное положение лимита на шкале
            double pct = (DataSource.MinLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            // Инвертируем координату Y (1.0 - pct), чтобы рост значения двигал треугольник снизу вверх!
            // И вычитаем 5 пикселей для центровки острия треугольника по высоте
            return ((1.0 - pct) * 180.0) - 5.0;
        }
    }

    /// <summary>
    /// Координата Y для треугольника максимального аларма на вертикальном слайдере (0..180 пикселей)
    /// </summary>
    public double MaxAlarmY
    {
        get
        {
            if (DataSource == null || float.IsPositiveInfinity(DataSource.MaxLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -100;

            double pct = (DataSource.MaxLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            return ((1.0 - pct) * 180.0) - 5.0;
        }
    }


    private bool _enableVisualAlarm = false;
    /// <summary>
    /// Разрешение окрашивать фон этого конкретного виджета при критическом аларме
    /// </summary>
    public bool EnableVisualAlarm
    {
        get => _enableVisualAlarm;
        set
        {
            if (_enableVisualAlarm != value)
            {
                _enableVisualAlarm = value;
                OnPropertyChanged();
            }
        }
    }


    /// <summary>
    /// Текущий угол поворота живой стрелки прибора в градусах (Ноль = 150° (8 часов), Макс = 60° (4 часа))
    /// </summary>
    /// <summary>
    /// Текущий угол поворота живой стрелки прибора в градусах (Возврат к проверенной логике прибавки)
    /// </summary>
    public double GaugeValueAngle
    {
        get
        {
            if (DataSource == null || (DataSource.ScaleMax <= DataSource.ScaleMin)) return 210;

            double pct = (DataSource.CurrentValue - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            // Разворачиваем стрелку по часовой стрелке на 240 градусов от стартовых 210°
            return (pct * 240.0) + 240;
        }
    }

    /// <summary>
    /// Угол поворота для красного треугольника минимального аларма (Gauge Min)
    /// </summary>
    public double GaugeMinAlarmAngle
    {
        get
        {
            if (DataSource == null || float.IsNegativeInfinity(DataSource.MinLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -999;

            double pct = (DataSource.MinLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            return (pct * 240.0) + 240;
        }
    }

    /// <summary>
    /// Угол поворота для красного треугольника максимального аларма (Gauge Max)
    /// </summary>
    public double GaugeMaxAlarmAngle
    {
        get
        {
            if (DataSource == null || float.IsPositiveInfinity(DataSource.MaxLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -999;

            double pct = (DataSource.MaxLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            return (pct * 240.0) + 240;
        }
    }




    /// <summary>
    /// Угол поворота/заполнения для дугового прибора MoTeC Style (Ноль = 180° (9 часов), Финиш = 360° (3 часа))
    /// </summary>
    public double ArcGaugeValueAngle
    {
        get
        {
            if (DataSource == null || (DataSource.ScaleMax <= DataSource.ScaleMin)) return 180;

            double pct = (DataSource.CurrentValue - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            // Разворачиваем геометрию ровно на 180 градусов верхнего полукруга
            return (pct * 180.0) + 180;
        }
    }

    /// <summary>
    /// Угол поворота для красного треугольника минимального аларма на дуге MoTeC
    /// </summary>
    public double ArcGaugeMinAlarmAngle
    {
        get
        {
            if (DataSource == null || float.IsNegativeInfinity(DataSource.MinLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -999;

            double pct = (DataSource.MinLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            return (pct * 180.0) + 180;
        }
    }

    /// <summary>
    /// Угол поворота для красного треугольника максимального аларма на дуге MoTeC
    /// </summary>
    public double ArcGaugeMaxAlarmAngle
    {
        get
        {
            if (DataSource == null || float.IsPositiveInfinity(DataSource.MaxLimit) || (DataSource.ScaleMax <= DataSource.ScaleMin))
                return -999;

            double pct = (DataSource.MaxLimit - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            return (pct * 180.0) + 180;
        }
    }

    /// <summary>
    /// Длина заполнения гоночного барграфа MoTeC (от 0.0 на нуле до 3.14 на максимуме)
    /// </summary>
    public double ArcBarFillLength
    {
        get
        {
            if (DataSource == null || (DataSource.ScaleMax <= DataSource.ScaleMin)) return 0;

            double pct = (DataSource.CurrentValue - DataSource.ScaleMin) / (DataSource.ScaleMax - DataSource.ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            // Число Пи (3.1415) — это длина полной дуги верхнего полукруга
            return pct * 3.14159;
        }
    }

    /// <summary>
    /// Шаговое увеличение значения активного скалярного параметра (PageUp) с учетом шага виджета и CTRL
    /// </summary>
    public void IncrementScalarValue()
    {
        if (DataSource == null || !DataSource.IsParam || DataSource.TotalElements > 1) return;

        // Проверяем: если зажат CTRL — ускоряем шаг виджета в 10 раз, иначе шаг стандартный
        bool isCtrlPressed = System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) ||
                             System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl);

        float delta = this.IncrementStep * (isCtrlPressed ? 10f : 1f);
        double newValue = DataSource.CurrentValue + delta;

        // Защита от дурака: удерживаем значение в границах шкалы
        if (newValue > DataSource.ScaleMax) newValue = DataSource.ScaleMax;

        DataSource.CurrentValue = newValue;

        // ВНУТРИ МЕТОДА IncrementScalarValue ПОСЛЕ ИЗМЕНЕНИЯ ЗНАЧЕНИЯ:
        DataSource.CurrentValue = newValue;

        // ФОРМИРУЕМ КОМАНДУ ЗАПИСИ ОДИНОЧНОГО СКАЛЯРА ДЛЯ ДИСПЕТЧЕРА
        var writeCmd = new Models.NetworkCommand
        {
            ModelId = DataSource.ModelId,
            Cmd = Models.LinkCommand.VarWrite, // Операция записи (0x01)
            VarId = (byte)DataSource.Id,
            DataType = DataSource.Type,
            Rows = 1, // Для скаляра всегда 1
            Cols = 1,
            PayloadData = new double[] { newValue } // Кладем одно измененное число в массив double
        };

        // Заталкиваем команду в приоритетную очередь Арбитра
        Services.BusArbiter.Instance.PushCommand(writeCmd);

    }

    /// <summary>
    /// Шаговое уменьшение значения активного скалярного параметра (PageDown) с учетом шага виджета и CTRL
    /// </summary>
    public void DecrementScalarValue()
    {
        if (DataSource == null || !DataSource.IsParam || DataSource.TotalElements > 1) return;

        bool isCtrlPressed = System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) ||
                             System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl);

        float delta = this.IncrementStep * (isCtrlPressed ? 10f : 1f);
        double newValue = DataSource.CurrentValue - delta;

        if (newValue < DataSource.ScaleMin) newValue = DataSource.ScaleMin;

        // ВНУТРИ МЕТОДА IncrementScalarValue ПОСЛЕ ИЗМЕНЕНИЯ ЗНАЧЕНИЯ:
        DataSource.CurrentValue = newValue;

        // ФОРМИРУЕМ КОМАНДУ ЗАПИСИ ОДИНОЧНОГО СКАЛЯРА ДЛЯ ДИСПЕТЧЕРА
        var writeCmd = new Models.NetworkCommand
        {
            ModelId = DataSource.ModelId,
            Cmd = Models.LinkCommand.VarWrite, // Операция записи (0x01)
            VarId = (byte)DataSource.Id,
            DataType = DataSource.Type,
            Rows = 1, // Для скаляра всегда 1
            Cols = 1,
            PayloadData = new double[] { newValue } // Кладем одно измененное число в массив double
        };

        // Заталкиваем команду в приоритетную очередь Арбитра
        Services.BusArbiter.Instance.PushCommand(writeCmd);

    }


}