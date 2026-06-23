using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfCalibrator.Models;

namespace WpfCalibrator.ViewModels;

/// <summary>
/// Обертка над переменной Symulink для отображения в UI.
/// </summary>
public partial class VariableViewModel : INotifyPropertyChanged
{

    // Метаданные из JSON-паспорта
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Type { get; init; } = ""; // "single", "double", "int32" и т.д.
    public int ElementSize { get; init; } // Размер одного элемента в байтах
    public bool IsParam { get; init; } // True, если это параметр (calibratable)

    /// <summary>
    /// Флаг-предохранитель: true временно блокирует отправку пакета записи обратно в UART при сетевом обновлении телеметрии
    /// </summary>
    //public bool IsUpdatingFromNetwork { get; set; } = false;

    public int Rows { get; init; }
    public int Cols { get; init; }
    public string Comment { get; init; } = "";
    public byte ModelId { get; init; } // <=== Добавлено

    // Вычисляемые свойства
    public int TotalElements => Rows * Cols;
    public int TotalBytes => TotalElements * ElementSize;
    // Настройки отображения (из user_view_config.json)
    public string ControlView { get; set; } = "TextBox"; // Тип виджета: TextBox, Graph, Gauge...
    public float MinValue { get; set; } = 0.0f; // Для слайдеров
    public float MaxValue { get; set; } = 100.0f;



    // ======================================================================
    // НОВЫЕ СВОЙСТВА ДЛЯ СУБСЕТОЧНОГО ПРИЦЕЛА И 1-LUT КАЛИБРОВОК
    // ======================================================================

    private bool _showRadarTracker = false;
    /// <summary>
    /// Флаг необходимости отображения субсеточного прицела-радара для этой таблицы
    /// </summary>
    public bool ShowRadarTracker
    {
        get => _showRadarTracker;
        set { if (_showRadarTracker != value) { _showRadarTracker = value; OnPropertyChanged(); } }
    }

    private bool _isVertical = false;
    /// <summary>
    /// Флаг вертикальной ориентации (только для одномерных осей и 1-LUT таблиц)
    /// </summary>
    public bool IsVertical
    {
        get => _isVertical;
        set { if (_isVertical != value) { _isVertical = value; OnPropertyChanged(); } }
    }

    // Ссылки на связанные объекты для одномерного режима (зеркально X-осям 2D-матриц)
    private VariableViewModel? _boundInputX;
    public VariableViewModel? BoundInputX
    {
        get => _boundInputX;
        set { if (_boundInputX != value) { _boundInputX = value; OnPropertyChanged(); } }
    }

    private VariableViewModel? _boundAxisX;
    public VariableViewModel? BoundAxisX
    {
        get => _boundAxisX;
        set { if (_boundAxisX != value) { _boundAxisX = value; OnPropertyChanged(); } }
    }


    // Привязки осей для таблиц (Look-up tables)
    // ======================================================================
    // ОТРЕФАКТОРЕННЫЕ СВЯЗИ ДЛЯ ВЕРТИКАЛЬНОЙ ОСИ Y (2D-LUT)
    // ======================================================================

    private VariableViewModel? _boundAxisY;
    public VariableViewModel? BoundAxisY
    {
        get => _boundAxisY;
        set
        {
            if (_boundAxisY != value)
            {
                _boundAxisY = value;
                OnPropertyChanged();
            }
        }
    }
    /// <summary>
    /// Флаг-предохранитель: true блокирует отправку пакета записи обратно в UART при сетевом обновлении
    /// </summary>
    public bool IsUpdatingFromNetwork { get; set; } = false;

    private VariableViewModel? _boundInputY;
    public VariableViewModel? BoundInputY
    {
        get => _boundInputY;
        set
        {
            if (_boundInputY != value)
            {
                _boundInputY = value;
                OnPropertyChanged();
            }
        }
    }

    // Логика подсветки (для таблиц)
    private int _activeRowIndex = -1;
    private int _activeColIndex = -1;

    private float _minLimit = float.NegativeInfinity;
    /// <summary>
    /// Критический минимум сигнала (по умолчанию минус бесконечность)
    /// </summary>
    public float MinLimit
    {
        get => _minLimit;
        set { if (_minLimit != value) 
            { 
                _minLimit = value;
                // ДОБАВЬТЕ ВНУТРЬ СЕТТЕРОВ СВОЙСТВ MinLimit И MaxLimit (прямо под OnPropertyChanged();):
                OnPropertyChanged(nameof(SliderTicks)); // Заставляем риски на слайдере перерисоваться!
                                                        // Добавь внутрь set каждого из этих 4-х свойств (прямо под OnPropertyChanged();):
                OnPropertyChanged(nameof(MinAlarmPercent));
                OnPropertyChanged(nameof(MaxAlarmPercent));

            }
        }
    }

    private float _maxLimit = float.PositiveInfinity;
    /// <summary>
    /// Критический максимум сигнала (по умолчанию плюс бесконечность)
    /// </summary>
    public float MaxLimit
    {
        get => _maxLimit;
        set { if (_maxLimit != value) 
            { 
                _maxLimit = value;
                // ДОБАВЬТЕ ВНУТРЬ СЕТТЕРОВ СВОЙСТВ MinLimit И MaxLimit (прямо под OnPropertyChanged();):
                OnPropertyChanged(nameof(SliderTicks)); // Заставляем риски на слайдере перерисоваться!
                                                        // Добавь внутрь set каждого из этих 4-х свойств (прямо под OnPropertyChanged();):
                OnPropertyChanged(nameof(MinAlarmPercent));
                OnPropertyChanged(nameof(MaxAlarmPercent));
            }
        }
    }

    private float _scaleMin = 0f;
    /// <summary>
    /// Минимальное отображаемое значение на шкале прибора (левая граница / старт)
    /// </summary>
    public float ScaleMin
    {
        get => _scaleMin;
        set { if (_scaleMin != value) { _scaleMin = value; OnPropertyChanged();
                // Добавь внутрь set каждого из этих 4-х свойств (прямо под OnPropertyChanged();):
                OnPropertyChanged(nameof(MinAlarmPercent));
                OnPropertyChanged(nameof(MaxAlarmPercent));
            } }
    }

    private float _scaleMax = 100f;
    /// <summary>
    /// Максимальное отображаемое значение на шкале прибора (правая граница / финиш)
    /// </summary>
    public float ScaleMax
    {
        get => _scaleMax;
        set { if (_scaleMax != value) { _scaleMax = value; OnPropertyChanged(); } }
    }

    public int ActiveRowIndex
    {
        get => _activeRowIndex;
        set
        {
            _activeRowIndex = value;
            OnPropertyChanged();
        }
    }

    public int ActiveColIndex
    {
        get => _activeColIndex;
        set
        {
            _activeColIndex = value;
            OnPropertyChanged();
        }
    }

    // Добавляем недостающие свойства
    private float[,] _matrixData = new float[0, 0];

    /// <summary>
    /// Двумерный массив данных (строки х столбцы) для отображения в DataGrid.
    /// </summary>
    public float[,] MatrixData
    {
        get => _matrixData;
        set
        {
            _matrixData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Возвращает true, если для этой таблицы настроены все привязки и можно двигать прицел.
    /// </summary>
    public bool IsLutLinked =>
        BoundAxisX != null &&
        BoundAxisY != null &&
        BoundInputX != null &&
        BoundInputY != null;

    // Реализация INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    // Вспомогательный метод для вызова события
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public VariableViewModel(VariableConfig config, byte modelId)
    {
        Id = config.Id;
        Name = config.Name;
        Type = config.Type;
        ElementSize = config.ElementSize;
        IsParam = config.IsParam;
        Rows = config.Rows;
        Cols = config.Cols;
        Comment = config.Comment;
        ModelId = modelId; // <=== Передаем ID модели

        if (this.Rows > 0 && this.Cols > 0)
        {
            this.MatrixData = new float[this.Rows, this.Cols];

            // Сразу вызываем твой метод перестройки ячеек, чтобы в Cells появилось нужное кол-во объектов MatrixCellViewModel
            // (Убедись, что метод RebuildMatrixCells() в твоем файле Cells.cs доступен для вызова)
            this.RebuildMatrixCells();
        }
    }

    // Методы для работы с MatrixData





    private int _selectedRow = 0;
    public int SelectedRow
    {
        get => _selectedRow;
        set
        {
            // Убираем жесткую проверку старого значения, чтобы метод вызывался всегда
            if (value >= 0 && value < Rows)
            {
                _selectedRow = value;
                OnPropertyChanged();
                UpdateSelectionHighlight(); // Принудительно перекрашиваем рамки в XAML
            }
        }
    }

    private int _selectedCol = 0;
    public int SelectedCol
    {
        get => _selectedCol;
        set
        {
            if (value >= 0 && value < Cols)
            {
                _selectedCol = value;
                OnPropertyChanged();
                UpdateSelectionHighlight(); // Принудительно перекрашиваем рамки в XAML
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



    private double _radarGridOffsetX = 0;
    public double RadarGridOffsetX
    {
        get => _radarGridOffsetX;
        set
        {
            if (_radarGridOffsetX != value)
            {
                _radarGridOffsetX = value;
                OnPropertyChanged();
            }
        }
    }

    private double _radarGridOffsetY = 0;
    public double RadarGridOffsetY
    {
        get => _radarGridOffsetY;
        set
        {
            if (_radarGridOffsetY != value)
            {
                _radarGridOffsetY = value;
                OnPropertyChanged();
            }
        }
    }


    /// <summary>
    /// Коллекция точек для отрисовки красных рисок лимитов на слайдере
    /// </summary>
    public System.Windows.Media.DoubleCollection SliderTicks
    {
        get
        {
            var ticks = new System.Windows.Media.DoubleCollection();

            // Добавляем минимум, если он не равен бесконечности
            if (!float.IsNegativeInfinity(MinLimit)) ticks.Add(MinLimit);

            // Добавляем максимум, если он не равен бесконечности
            if (!float.IsPositiveInfinity(MaxLimit)) ticks.Add(MaxLimit);

            return ticks;
        }
    }


    /// <summary>
    /// Процентное положение минимального аларма на шкале (от 0.0 до 1.0)
    /// </summary>
    public double MinAlarmPercent
    {
        get
        {
            if (float.IsNegativeInfinity(MinLimit) || (ScaleMax <= ScaleMin)) return -100; // Прячем за экран, если лимит не задан
            double pct = (MinLimit - ScaleMin) / (ScaleMax - ScaleMin);
            return Math.Max(0, Math.Min(1, pct)); // Зажимаем в границы 0..1
        }
    }

    /// <summary>
    /// Процентное положение максимального аларма на шкале (от 0.0 до 1.0)
    /// </summary>
    public double MaxAlarmPercent
    {
        get
        {
            if (float.IsPositiveInfinity(MaxLimit) || (ScaleMax <= ScaleMin)) return -100; // Прячем за экран, если лимит не задан
            double pct = (MaxLimit - ScaleMin) / (ScaleMax - ScaleMin);
            return Math.Max(0, Math.Min(1, pct));
        }
    }



    /// <summary>
    /// Координата X для треугольника минимального аларма (смещенная на центр острия)
    /// </summary>
    public double MinAlarmX
    {
        get
        {
            if (float.IsNegativeInfinity(MinLimit) || (ScaleMax <= ScaleMin)) return -100;

            double pct = (MinLimit - ScaleMin) / (ScaleMax - ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            // Вычитаем 5 пикселей (половину ширины треугольника 10px) для идеальной центровки острия!
            return (pct * 230.0) - 5.0;
        }
    }

    /// <summary>
    /// Координата X для треугольника максимального аларма (смещенная на центр острия)
    /// </summary>
    public double MaxAlarmX
    {
        get
        {
            if (float.IsPositiveInfinity(MaxLimit) || (ScaleMax <= ScaleMin)) return -100;

            double pct = (MaxLimit - ScaleMin) / (ScaleMax - ScaleMin);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;

            return (pct * 230.0) - 5.0;
        }
    }


    /// <summary>
    /// Текущее значение датчика в процентах от его настроенной шкалы (0..100)
    /// </summary>
    /// <summary>
    /// Статус горения 10 светодиодов Shift-Light линейки (true = горит)
    /// </summary>
    public bool[] LedStates
    {
        get
        {
            var states = new bool[10];
            if (ScaleMax <= ScaleMin) return states;

            // Переводим живое значение в чистый процент от 0 до 100
            double pct = (CurrentValue - ScaleMin) / (ScaleMax - ScaleMin) * 100.0;

            // Зажигаем лампочки по цепочке (каждые 10% шкалы — новый диод)
            for (int i = 0; i < 10; i++)
            {
                states[i] = pct >= ((i + 1) * 10.0);
            }
            return states;
        }
    }

    /// <summary>
    /// Пакетный инкремент значений ВСЕХ выделенных ячеек таблицы по нажатию PageUp (с учетом CTRL x10)
    /// </summary>
    public void IncrementSelectedCell(float customStep)
    {
        if (!IsParam) return;

        // Проверяем статус клавиши CTRL для форсажа x10
        bool isCtrlPressed = System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) ||
                             System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl);

        float delta = customStep * (isCtrlPressed ? 10f : 1f);
        bool hasChanges = false;

        // Бежим циклом по всей двухмерной матрице калибровок
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                // Находим соответствующую визуальную ячейку в нашей коллекции, чтобы проверить её статус выделения
                var cell = MatrixCells.FirstOrDefault(m => m.Row == r && m.Col == c);
                if (cell != null && cell.IsSelected)
                {
                    float newValue = MatrixData[r, c] + delta;

                    // Защита от дурака: удерживаем значение каждой ячейки в границах шкалы
                    if (newValue > ScaleMax) newValue = ScaleMax;
                    if (newValue < ScaleMin) newValue = ScaleMin;

                    MatrixData[r, c] = newValue;
                    hasChanges = true;
                }
            }
        }
        // ВНУТРИ МЕТОДОВ IncrementSelectedCell и DecrementSelectedCell ПОСЛЕ ЦИКЛА:
        if (hasChanges)
        {
            // 1. Мгновенно обновляем текст в ячейках на экране компьютера своими силами
            RebuildMatrixCells(false);

            // 2. МАРШАЛИНГ В DOUBLE: Вытаскиваем всю двухмерную матрицу MatrixData 
            // в один плоский одномерный массив double[] для Диспетчера.
            // Переводим строго в Row-Major порядке C# (строка за строкой), 
            // так как наш MATLAB-маршалер в сервисе связи сам развернет его в Column-Major!
            double[] flatPayload = new double[Rows * Cols];
            int idx = 0;
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    flatPayload[idx++] = MatrixData[r, c];
                }
            }

            // 3. ФОРМИРУЕМ ВЫСОКОУРОВНЕВУЮ КОМАНДУ ЗАПИСИ
            var writeCmd = new Models.NetworkCommand
            {
                ModelId = this.ModelId,
                Cmd = Models.LinkCommand.VarWrite, // Операция записи (0x01)
                VarId = (byte)this.Id,
                DataType = this.Type,
                Rows = this.Rows,
                Cols = this.Cols,
                PayloadData = flatPayload
            };

            // 4. ПУШ В ОЧЕРЕДЬ: Заталкиваем калибровку в приоритетную очередь Арбитра.
            // Он мгновенно приостановит фоновую телеметрию и выстрелит этот пакет следующим!
            Services.BusArbiter.AsInterface.PushCommand(writeCmd);
        }

    }

    /// <summary>
    /// Пакетный декремент значений ВСЕХ выделенных ячеек таблицы по нажатию PageDown (с учетом CTRL x10)
    /// </summary>
    public void DecrementSelectedCell(float customStep)
    {
        if (!IsParam) return;

        bool isCtrlPressed = System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) ||
                             System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl);

        float delta = customStep * (isCtrlPressed ? 10f : 1f);
        bool hasChanges = false;

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                var cell = MatrixCells.FirstOrDefault(m => m.Row == r && m.Col == c);
                if (cell != null && cell.IsSelected)
                {
                    float newValue = MatrixData[r, c] - delta;

                    if (newValue > ScaleMax) newValue = ScaleMax;
                    if (newValue < ScaleMin) newValue = ScaleMin;

                    MatrixData[r, c] = newValue;
                    hasChanges = true;
                }
            }
        }

        // ВНУТРИ МЕТОДОВ IncrementSelectedCell и DecrementSelectedCell ПОСЛЕ ЦИКЛА:
        if (hasChanges)
        {
            // 1. Мгновенно обновляем текст в ячейках на экране компьютера своими силами
            RebuildMatrixCells(false);

            // 2. МАРШАЛИНГ В DOUBLE: Вытаскиваем всю двухмерную матрицу MatrixData 
            // в один плоский одномерный массив double[] для Диспетчера.
            // Переводим строго в Row-Major порядке C# (строка за строкой), 
            // так как наш MATLAB-маршалер в сервисе связи сам развернет его в Column-Major!
            double[] flatPayload = new double[Rows * Cols];
            int idx = 0;
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    flatPayload[idx++] = MatrixData[r, c];
                }
            }

            // 3. ФОРМИРУЕМ ВЫСОКОУРОВНЕВУЮ КОМАНДУ ЗАПИСИ
            var writeCmd = new Models.NetworkCommand
            {
                ModelId = this.ModelId,
                Cmd = Models.LinkCommand.VarWrite, // Операция записи (0x01)
                VarId = (byte)this.Id,
                DataType = this.Type,
                Rows = this.Rows,
                Cols = this.Cols,
                PayloadData = flatPayload
            };

            // 4. ПУШ В ОЧЕРЕДЬ: Заталкиваем калибровку в приоритетную очередь Арбитра.
            // Он мгновенно приостановит фоновую телеметрию и выстрелит этот пакет следующим!
            Services.BusArbiter.AsInterface.PushCommand(writeCmd);
        }

    }



}