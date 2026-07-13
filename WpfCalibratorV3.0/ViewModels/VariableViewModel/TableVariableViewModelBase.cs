using System;

namespace WpfCalibrator.ViewModels
{
    /// <summary>
    /// Промежуточный базовый класс для всех интерактивных калибровочных таблиц (1D и 3D)
    /// </summary>
    public abstract class TableVariableViewModelBase : VariableViewModelBase
    {
        private int _selectedRow;
        private int _selectedCol;
        private int _anchorRow;
        private int _anchorCol;
        private bool _isEditing;

        public int SelectedRow { get => _selectedRow; set { _selectedRow = value; OnPropertyChanged(); } }
        public int SelectedCol { get => _selectedCol; set { _selectedCol = value; OnPropertyChanged(); } }
        public int AnchorRow { get => _anchorRow; set { _anchorRow = value; OnPropertyChanged(); } }
        public int AnchorCol { get => _anchorCol; set { _anchorCol = value; OnPropertyChanged(); } }
        public bool IsEditing { get => _isEditing; set { _isEditing = value; OnPropertyChanged(); } }

        // А вот таблицы честно перемножают свою живую геометрию сетки!
        public int Rows { get; set; }
        public int Cols { get; set; }

        public override int TotalElements => Rows * Cols;


        private ScalarVariableViewModel? _boundInputX;
        /// <summary>
        /// Физический датчик-вход (например, RPM), который двигает маркер по горизонтали.
        /// Принимает ТОЛЬКО скаляры-сигналы (телеметрию, у которых IsParam == false).
        /// </summary>
        public ScalarVariableViewModel? BoundInputX
        {
            get => _boundInputX;
            set
            {
                // Если нам подсовывают константу-параметр (IsParam == true), 
                // то эта модель не подходит! Игнорируем привязку, защищая логику.
                if (value != null && value.IsParam) return;

                if (_boundInputX == value) return;
                _boundInputX = value;
                OnPropertyChanged();
            }
        }


        private CurveVariableViewModel? _boundAxisX;
        /// <summary>
        /// Ссылка на одномерную ось калибровки (шкалу оцифровки) по горизонтали X
        /// </summary>
        public CurveVariableViewModel? BoundAxisX
        {
            get => _boundAxisX;
            set { if (_boundAxisX != value) { _boundAxisX = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Флаг успешной линковки таблицы: true, если привязана и шкала оцифровки, 
        /// и живой датчик-сигнал для отрисовки интерактивного неонового прицела!
        /// </summary>
        public bool IsLutLinked => BoundAxisX != null && BoundInputX != null;


        private int _activeRowIndex = -1;
        /// <summary>
        /// Индекс строки, в которой СЕЙЧАС находится двигатель (зеленый маркер) [1.14]
        /// </summary>
        public int ActiveRowIndex
        {
            get => _activeRowIndex;
            set { if (_activeRowIndex != value) { _activeRowIndex = value; OnPropertyChanged(); } }
        }

        private int _activeColIndex = -1;
        /// <summary>
        /// Индекс колонки, в которой СЕЙЧАС находится двигатель (зеленый маркер) [1.14]
        /// </summary>
        public int ActiveColIndex
        {
            get => _activeColIndex;
            set { if (_activeColIndex != value) { _activeColIndex = value; OnPropertyChanged(); } }
        }

        private double _radarGridOffsetX;
        /// <summary>
        /// Дробное пиксельное смещение мишени радара по горизонтали X [1.14]
        /// </summary>
        public double RadarGridOffsetX
        {
            get => _radarGridOffsetX;
            set { if (Math.Abs(_radarGridOffsetX - value) < 0.001) return; _radarGridOffsetX = value; OnPropertyChanged(); }
        }

        private double _radarGridOffsetY;
        /// <summary>
        /// Дробное пиксельное смещение мишени радара по вертикали Y [1.14]
        /// </summary>
        public double RadarGridOffsetY
        {
            get => _radarGridOffsetY;
            set { if (Math.Abs(_radarGridOffsetY - value) < 0.001) return; _radarGridOffsetY = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Виртуальный триггер для 3D-карты: плавно перемещает лазерный луч Helix [1.14]
        /// </summary>
        protected virtual void UpdateLaserBeamPosition(double exactCol, double exactRow) { }




        // ======================================================================
        // ⛓️ АБСТРАКТНЫЙ МОСТ ДОСТУПА К ДАННЫМ ОЗУ
        // ======================================================================
        // Наследники сами свяжут эти методы со своими массивами VectorData или MatrixData!
        public abstract double GetTableValue(int r, int c);
        protected abstract void SetTableValue(int r, int c, double val);
        // 🔥 ПЕРЕНЕСЛИ СЮДА: Теперь коллекция ячеек является общим свойством 
        // для всех типов интерактивных таблиц (и 1D, и 3D)!
        public System.Collections.ObjectModel.ObservableCollection<MatrixCellViewModel> MatrixCells { get; } = new();


        /// <summary>
        /// Базовый метод: зажигает неоновый прицел моторной точки на основе ActiveRowIndex/ColIndex [1.14]
        /// </summary>
        public virtual void UpdateSelectionHighlight()
        {
            if (MatrixCells == null || MatrixCells.Count == 0) return;

            foreach (var cell in MatrixCells)
            {
                if (cell == null) continue;

                // Взводим флаг для DataTrigger в XAML
                cell.IsActive = (cell.Row == ActiveRowIndex && cell.Col == ActiveColIndex);
            }
        }


        public override void AdjustValue(double step)
        {
            // Бежим по плоской коллекции ячеек UniformGrid
            foreach (var cell in MatrixCells)
            {
                // Меняем только то, что обведено синей рамкой инженера
                if (cell.IsSelected)
                {
                    if (double.TryParse(cell.ValueText, out double currentValue))
                    {
                        double newValue = currentValue + step;

                        // Жесткая отсечка по краям
                        //if (newValue > ScaleMax) newValue = ScaleMax;
                        //if (newValue < ScaleMin) newValue = ScaleMin;

                        cell.ValueText = newValue.ToString("F2");
                    }
                }
            }
        }


        /// <summary>
        /// ДИСПЕТЧЕР: Запускает горизонтальную интерполяцию по строкам для выделенной области.
        /// </summary>
        public void InterpolateHorizontal()
        {
            if (TotalElements <= 1 || MatrixCells == null) return;

            // Построчно обходим всю матрицу
            for (int r = 0; r < Rows; r++)
            {
                // Выделяем из ОЗУ ячейки только для текущей строки, которые выбраны инженером
                var selectedInRow = MatrixCells
                    .Where(c => c.Row == r && c.IsSelected)
                    .OrderBy(c => c.Col)
                    .ToList();

                // Вызываем мелкий математический метод сглаживания одной строки
                InterpolateSingleRow(r, selectedInRow);
            }

            OnTableDataChanged();
        }

        /// <summary>
        /// МАТЕМАТИКА СТРОКИ: Сглаживает одну конкретную строку между крайними выделенными колонками.
        /// </summary>
        private void InterpolateSingleRow(int r, System.Collections.Generic.List<MatrixCellViewModel> selectedCells)
        {
            // Если в строке выделено меньше 2 ячеек — строить градиент не между чем, выходим
            if (selectedCells.Count < 2) return;

            int startCol = selectedCells.First().Col;
            int endCol = selectedCells.Last().Col;
            int deltaCols = endCol - startCol;

            double startValue = GetTableValue(r, startCol);
            double endValue = GetTableValue(r, endCol);
            double stepDelta = (endValue - startValue) / deltaCols;

            // Сишный заполняющий цикл градиента по колонкам
            for (int c = startCol; c <= endCol; c++)
            {
                double calculatedValue = startValue + (stepDelta * (c - startCol));

                SetTableValue(r, c, calculatedValue);

                // Находим визуальную ячейку и обновляем экран
                var cell = MatrixCells.FirstOrDefault(m => m.Row == r && m.Col == c);
                if (cell != null)
                {
                    cell.ValueText = calculatedValue.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                }
            }

        }

        /// <summary>
        /// Виртуальный триггер, срабатывающий при любом массовом изменении данных таблицы (например, после интерполяции).
        /// Наследники переопределяют его для обновления специфичной графики (1D-шкал или 3D-сеток Helix).
        /// </summary>
        protected virtual void OnTableDataChanged()
        {
            // По умолчанию ничего не делаем, чтобы 1D-оси не раздували код пустыми вызовами
        }

        public override void CommitEditedValue(double parsedValue)
        {
            // Зажимаем число в физические лимиты шкалы прибора
            double finalValue = parsedValue;
            //if (finalValue > ScaleMax) finalValue = ScaleMax;
            //if (finalValue < ScaleMin) finalValue = ScaleMin;

            // Бежим по общей коллекции ячеек UniformGrid
            foreach (var cell in MatrixCells)
            {
                // Фиксируем цифры только в тех ячейках, которые сейчас выделены на экране
                if (cell.IsSelected)
                {
                    cell.ValueText = finalValue.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

                    // Записываем значение в ОЗУ-массивы через наши абстрактные геттеры/сеттеры
                    SetTableValue(cell.Row, cell.Col, finalValue);
                }
            }

            // Пинаем виртуальный триггер изменения данных (для пересчета 3D рельефа Helix и UART)
            OnTableDataChanged();
        }

        /// <summary>
        /// Точечное изменение ячейки инженером на экране.
        /// Одинаково работает и для ячеек 1D-осей (row=0), и для тяжелых 3D-матриц!
        /// </summary>
        public void UpdateMatrixValue(int row, int col, double newValue)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Cols) return;

            // 1. Вытягиваем старое число из ОЗУ через наш абстрактный мост
            double oldValue = GetTableValue(row, col);

            // Железобетонный фикс: блокируем паразитные пакеты, если число не изменилось
            if (Math.Abs(oldValue - newValue) < 0.0001) return;

            // 2. Пишем свежее число в ОЗУ-массив наследника
            SetTableValue(row, col, newValue);

            // 3. Если это параметр — пакуем плоский слепок и выстреливаем в UART
            if (IsParam && !IsUpdatingFromNetwork && Services.BusArbiter.AsInterface.IsRunning)
            {
                double[] flatPayload = Array.Empty<double>();

                // Маршалим данные в зависимости от типа таблицы
                if (this is Map3DVariableViewModel map3D)
                {
                    flatPayload = map3D.GetFlatPayloadForTx(); // 3D-карта пакует Column-Major слепок [1.14]
                }
                else if (this is CurveVariableViewModel curve)
                {
                    flatPayload = curve.VectorData; // 1D-вектор просто отдает свой массив осей [1.14]
                }

                var writeCmd = new Models.NetworkCommand
                {
                    ModelId = this.ModelId,
                    Cmd = Models.LinkCommand.VarWrite,
                    VarId = (byte)this.Id,
                    DataType = this.Type,
                    Rows = this.Rows,
                    Cols = this.Cols,
                    PayloadData = flatPayload
                };

                Services.BusArbiter.AsInterface.PushCommand(writeCmd);
            }

            // Пинаем графический движок (если это 3D — перестроится Helix сетка) [1.14]
            OnTableDataChanged();
        }

        // ======================================================================
        // 📐 ВЫЧИСЛЕНИЕ РЕЖИМНОЙ ТОЧКИ ДВИГАТЕЛЯ И ДЕЛЬТ РАДАРА MoTeC-STYLE
        // ======================================================================

        /// <summary>
        /// Главный диспетчер расчёта рабочей точки. Вызывается по таймеру телеметрии из UART [1.14].
        /// </summary>
        public void CalculateWorkingPoint(double currentInputX, double currentInputY, double[] axisXData, double[] axisYData)
        {
            // 1. Бинарно-линейный поиск квадранта сетки
            FindBaseIndices(currentInputX, currentInputY, axisXData, axisYData,
                out int colIdx, out int rowIdx, out int baseColIdx, out int baseRowIdx);

            // 2. Взводим индексы ячеек для неоновой рамки в UI [1.14]
            ActiveRowIndex = rowIdx;
            ActiveColIndex = colIdx;

            // 3. Считаем пиксельные смещения для мишени Радара-прицела
            if (axisXData != null && baseColIdx < axisXData.Length - 1)
            {
                double startX = axisXData[baseColIdx];
                double endX = axisXData[baseColIdx + 1];
                double pctX = (endX <= startX) ? 0 : (currentInputX - startX) / (endX - startX);
                double exactCol = baseColIdx + Math.Clamp(pctX, 0, 1);

                // Вычисляем горизонтальную дельту радара (в пикселях UniformGrid)
                RadarGridOffsetX = (exactCol - colIdx) * 50.0; // 50px — ширина твоей XAML ячейки!

                // 4. Расчёт вертикальной оси (только для 3D-матриц) [1.14]
                if (axisYData != null && baseRowIdx < axisYData.Length - 1)
                {
                    double startY = axisYData[baseRowIdx];
                    double endY = axisYData[baseRowIdx + 1];
                    double pctY = (endY <= startY) ? 0 : (currentInputY - startY) / (endY - startY);
                    double exactRow = baseRowIdx + Math.Clamp(pctY, 0, 1);

                    RadarGridOffsetY = -(exactRow - rowIdx) * 30.0; // 30px — высота ячейки!

                    // 🔥 Пинаем виртуальный лазерный луч 3D-карты Helix!
                    UpdateLaserBeamPosition(exactCol, exactRow);
                }
            }
        }

        /// <summary>
        /// Сишный перебор: находит границы квадранта, в котором сейчас находится мотор [1.14].
        /// </summary>
        private void FindBaseIndices(double currentInputX, double currentInputY, double[] axisXData, double[] axisYData,
                                     out int colIdx, out int rowIdx, out int baseColIdx, out int baseRowIdx)
        {
            colIdx = 0; rowIdx = 0; baseColIdx = 0; baseRowIdx = 0;
            if (axisXData == null || axisXData.Length < 2) return;

            // Горизонтальный поиск X (Обороты)
            for (int c = 0; c < axisXData.Length; c++)
            {
                if (currentInputX >= axisXData[c]) colIdx = c;
            }
            baseColIdx = (colIdx < axisXData.Length - 1) ? colIdx : (axisXData.Length - 2);

            // Вертикальный поиск Y (Наддув) — только если массив Y передан (3D режим) [1.14]
            if (axisYData != null && axisYData.Length > 1)
            {
                for (int r = 0; r < axisYData.Length; r++)
                {
                    if (currentInputY >= axisYData[r]) rowIdx = r;
                }
                baseRowIdx = (rowIdx < axisYData.Length - 1) ? rowIdx : (axisYData.Length - 2);
            }
        }
        /// <summary>
        /// Принудительно заставляет интерфейс WPF полностью перестроить сетку и заголовки шкал
        /// </summary>
        public void NotifyStructureChanged()
        {
            // 1. Уведомляем об изменении самих осей
            OnPropertyChanged(nameof(BoundAxisX));
            OnPropertyChanged(nameof(BoundInputX));

            // 2. 🔥 ХИТРЫЙ ХАК: Выстреливаем PropertyChanged для MatrixCells.
            // Если этого мало, можно на мгновение подменить коллекцию ячеек, 
            // но обычно вызова уведомления ItemsSource достаточно, чтобы DataGrid перерисовал шапки!
            OnPropertyChanged(nameof(MatrixCells));
        }

        /// <summary>
        /// Принудительно синхронизирует внутренние бинарные массивы данных с текстовым содержимым ячеек UI.
        /// </summary>
        public abstract void SyncDataFromCells();

    }
}
