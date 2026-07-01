using System;
using System.ComponentModel;
using System.Globalization;
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
    private VariableViewModelBase? _dataSource;

    /// <summary>
    /// Источник данных прибора (его цифровая переменная в ОЗУ).
    /// Привязывается в момент создания виджета инженером.
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
    /// Реактивный диспетчер: срабатывает КАЖДЫЙ РАЗ, когда в недрах UART меняется цифра датчика.
    /// </summary>
    private void OnDataSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Нас интересует только изменение живого физического значения скаляра
        if (e.PropertyName == "CurrentValue" && DataSource is ScalarVariableViewModel scalar)
        {
            // ⚡️ Аппаратно пинаем стрелки и треугольники варнингов MoTeC-прибора
            this.NotifyValueAngleChanged();
            this.RefreshAlarmTriangles();
            OnPropertyChanged(nameof(LedStates));
            // Если перед глазами инженера открыт осциллограф — плавно дописываем точку в лог
            if (ControlView == "TimePlot")
            {
                this.AppendPlotPoint(scalar.CurrentValue);
            }

            // Обновляем текстовый блок вывода строки на экран
            OnPropertyChanged(nameof(CurrentValueText));
            // Внутри WidgetViewModel.cs -> OnDataSourcePropertyChanged:
            if (DataSource is TableVariableViewModelBase tableVar)
            {
                // Если обновились координаты смещения радара в ОЗУ — виджет мгновенно перерисовывает мишень!
                if (e.PropertyName == "RadarGridOffsetX") OnPropertyChanged(nameof(RadarGridOffsetX));
                if (e.PropertyName == "RadarGridOffsetY") OnPropertyChanged(nameof(RadarGridOffsetY));
            }

        }
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


    private string _inputBuffer = string.Empty;
    private string _currentValueText = "0";
    /// <summary>
    /// Текстовый буфер для бесфокусного набора цифр с клавиатуры.
    /// </summary>
    public string InputBuffer
    {
        get => _inputBuffer;
        set
        {
            if (_inputBuffer == value) return;
            _inputBuffer = value;
            OnPropertyChanged();

            // Автоматически взводим твой существующий флаг IsEditing:
            // Если в буфере есть текст — значит, идет редактирование и UART заблокирован!
            IsEditing = !string.IsNullOrEmpty(_inputBuffer);

            // Уведомляем интерфейс, что текст на экране обновился
            OnPropertyChanged(nameof(CurrentValueText));
        }
    }

    /// <summary>
    /// Универсальное свойство отображения для TextBox скаляров и логов.
    /// Заменяет собой дёрганую привязку к float.
    /// </summary>
    public string CurrentValueText
    {
        get
        {
            // Если инженер сейчас набирает цифры руками — жестко выводим буфер ввода
            if (IsEditing && !string.IsNullOrEmpty(_inputBuffer))
            {
                return _inputBuffer;
            }

            // В режиме покоя — выводим наше стандартное число из UART с красивым гоночным форматом
            return DataSource.CurrentValue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
        set
        {
            // Этот сеттер будет вызываться только при инициализации, его не трогаем
            _currentValueText = value;
            OnPropertyChanged();
        }
    }

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


    /// <summary>
    /// Фиксация ввода: перекладывает накопленный текстовый буфер в чистую математику ОЗУ.
    /// Не производит самостоятельных выстрелов в UART.
    /// </summary>
    public void ApplyEditing()
    {
        // Безопасный парсинг ввода и передача полиморфного значения [1.14]
        if (string.IsNullOrEmpty(InputBuffer) || DataSource == null) return;

        if (float.TryParse(InputBuffer, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float parsedValue) ||
            float.TryParse(InputBuffer, out parsedValue))
        {
            // Делегируем запись конкретному типу переменной [1.14]
            if (DataSource is TableVariableViewModelBase tableVar) tableVar.CommitEditedValue(parsedValue);
            else if (DataSource is ScalarVariableViewModel scalarVar) scalarVar.CommitEditedValue(parsedValue);
        }

        InputBuffer = string.Empty;
        IsEditing = false;
        OnPropertyChanged(nameof(CurrentValueText));
    }

    /// <summary>
    /// Изменение числа внутри буфера на заданный шаг (Для PageUp/PageDown в режиме ввода).
    /// </summary>
    public void ChangeBufferByStep(float step)
    {
        if (DataSource == null) return;

        // Быстрое переключение через AdjustValue
        if (!IsEditing || string.IsNullOrEmpty(InputBuffer))
        {
            DataSource.AdjustValue(step);
            return;
        }

        // Ручной ввод с ограничением по лимитам
        if (float.TryParse(InputBuffer, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float currentValue))
        {
            float newValue = Math.Clamp(currentValue + step, (float)DataSource.ScaleMin, (float)DataSource.ScaleMax);
            InputBuffer = newValue.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Синхронизация ячейки через TableVariableViewModelBase
            if (DataSource is TableVariableViewModelBase tableVar)
            {
                var anchorCell = tableVar.MatrixCells.FirstOrDefault(c => c.Row == tableVar.SelectedRow && c.Col == tableVar.SelectedCol);
                if (anchorCell != null) anchorCell.ValueText = InputBuffer;
            }
        }
    }

    /// <summary>
    /// Отмена ввода (Нажатие ESC).
    /// </summary>
    public void CancelEditing()
    {
        // 1. Полностью очищаем черновик набора и гасим флаг редактирования
        InputBuffer = string.Empty;
        IsEditing = false;

        // 2. Возвращаем на экран честные числа из памяти ОЗУ
        if (DataSource is TableVariableViewModelBase tableVar)
        {
            // Бежим по ячейкам UniformGrid и сбрасываем их текст обратно на актуальные данные из МК
            int cellIndex = 0;
            foreach (var cell in tableVar.MatrixCells)
            {
                // Вытягиваем живые числа через наш универсальный геттер таблиц
                double ramValue = tableVar.GetTableValue(cell.Row, cell.Col);
                cell.ValueText = ramValue.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        // 3. Уведомляем интерфейс, чтобы обновился текст TextBox для скаляров
        OnPropertyChanged(nameof(CurrentValueText));
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
    /// Статус 10 LED Shift-Light (true = горит) [1.14]
    /// </summary>
    public bool[] LedStates
    {
        get
        {
            var states = new bool[10];
            if (DataSource is ScalarVariableViewModel scalar && scalar.ScaleMax > scalar.ScaleMin)
            {
                // Расчет % от 0 до 100 и зажигание цепочки [1.14]
                double pct = (scalar.CurrentValue - scalar.ScaleMin) / (scalar.ScaleMax - scalar.ScaleMin) * 100.0;
                for (int i = 0; i < 10; i++) states[i] = pct >= ((i + 1) * 10.0);
            }
            return states;
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
        /// Координата X для треугольника минимального аларма (смещенная на центр острия) [1.14]
        /// </summary>
        public double MinAlarmX
        {
            get
            {
                if (DataSource is ScalarVariableViewModel scalar)
                {
                    if (double.IsNegativeInfinity(scalar.MinLimit) || (scalar.ScaleMax <= scalar.ScaleMin)) return -100;

                    double pct = (scalar.MinLimit - scalar.ScaleMin) / (scalar.ScaleMax - scalar.ScaleMin);
                    pct = Math.Clamp(pct, 0.0, 1.0);

                    // Вычитаем 5 пикселей для идеальной центровки острия (из твоей оригинальной верстки)
                    return (pct * 230.0) - 5.0;
                }
                return -100;
            }
        }

        /// <summary>
        /// Координата X для треугольника максимального аларма (смещенная на центр острия) [1.14]
        /// </summary>
        public double MaxAlarmX
        {
            get
            {
                if (DataSource is ScalarVariableViewModel scalar)
                {
                    if (double.IsPositiveInfinity(scalar.MaxLimit) || (scalar.ScaleMax <= scalar.ScaleMin)) return -100;

                    double pct = (scalar.MaxLimit - scalar.ScaleMin) / (scalar.ScaleMax - scalar.ScaleMin);
                    pct = Math.Clamp(pct, 0.0, 1.0);

                    return (pct * 230.0) - 5.0;
                }
                return -100;
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
    /// Накопление строки ввода. Вызывается драйвером клавиатуры на каждый нажатый символ.
    /// Синхронно размножает вводимый текст по всей выделенной области в реальном времени.
    /// </summary>
    public void AppendToBuffer(string text)
    {
        // Накапливаем символ в локальный буфер виджета
        InputBuffer += text;

        // Если наш источник данных — интерактивная таблица (1D или 3D)
        if (DataSource is TableVariableViewModelBase tableSource)
        {
            // Размножаем черновой текст по всем выделенным синей рамкой ячейкам на экране!
            foreach (var cell in tableSource.MatrixCells)
            {
                if (cell.IsSelected)
                {
                    cell.ValueText = InputBuffer;
                }
            }
        }
        // Если это одиночная константа-параметр
        else if (DataSource is ScalarVariableViewModel scalarSource && scalarSource.IsParam)
        {
            OnPropertyChanged(nameof(CurrentValueText));
        }
    }

    /// <summary>
    /// Метод атомарной фиксации ввода: парсит накопленный буфер, швыряет число в AdjustValue() переменной,
    /// гасит флаг редактирования IsEditing и полностью очищает InputBuffer
    /// </summary>
    public void CommitInputBuffer()
    {
        if (string.IsNullOrEmpty(InputBuffer)) return;

        // Пытаемся распарсить накопленный текст в физическое число double
        if (double.TryParse(InputBuffer, out double parsedValue))
        {
            // Если привязана интерактивная таблица (1D или 3D)
            if (DataSource is TableVariableViewModelBase tableSource)
            {
                // Бежим по ячейкам и жестко фиксируем число в памяти
                foreach (var cell in tableSource.MatrixCells)
                {
                    if (cell.IsSelected)
                    {
                        // В будущем здесь вызовется цепочка OnTableDataChanged() для пересчета 3D и UART!
                        cell.ValueText = parsedValue.ToString("F2");
                    }
                }
            }
            // Если привязана одиночная константа-параметр
            else if (DataSource is ScalarVariableViewModel scalarSource && scalarSource.IsParam)
            {
                scalarSource.CurrentValue = parsedValue;
            }
        }

        // Полностью очищаем черновики виджета, гася флаг IsEditing у DataSource
        InputBuffer = string.Empty;
        OnPropertyChanged(nameof(CurrentValueText));
    }



}