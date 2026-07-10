using System;
using System.Collections.ObjectModel;
using System.Data;
using WpfCalibrator.ViewModels.WidgetViewModel;


namespace WpfCalibrator.ViewModels
{
    /// <summary>
    /// Тяжелая модель двумерной 3D-матрицы калибровок (LUT-карта 32х32)
    /// </summary>
    public class Map3DVariableViewModel : TableVariableViewModelBase
    {
        // Двумерный массив физических данных для внутренних математических расчетов
        private double[,] _matrixData = new double[0, 0];

        /// <summary>
        /// Зеркало памяти двумерной таблицы МК
        /// </summary>
        public double[,] MatrixData
        {
            get => _matrixData;
            private set
            {
                _matrixData = value;
                OnPropertyChanged();
            }
        }

        private ScalarVariableViewModel? _boundInputY;
        /// <summary>
        /// Физический датчик-вход (например, MAP), который двигает маркер по вертикали Y.
        /// Принимает ТОЛЬКО скаляры-сигналы телеметрии.
        /// </summary>
        public ScalarVariableViewModel? BoundInputY
        {
            get => _boundInputY;
            set
            {
                // Калибровочные константы-параметры сюда не пройдут фейсконтроль!
                if (value != null && value.IsParam) return;

                if (_boundInputY == value) return;
                _boundInputY = value;
                OnPropertyChanged();
            }
        } 

        private CurveVariableViewModel? _boundAxisY;
        /// <summary>
        /// Ссылка на одномерную ось калибровки по вертикали Y
        /// </summary>
        public CurveVariableViewModel? BoundAxisY
        {
            get => _boundAxisY;
            set { if (_boundAxisY != value) { _boundAxisY = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Флаг полной линковки 3D-карты: для активации неонового прицела 
        /// обязаны быть привязаны обе оси (X, Y) и оба живых датчика телеметрии!
        /// </summary>
        public new bool IsLutLinked => BoundAxisX != null && BoundInputX != null &&
                                       BoundAxisY != null && BoundInputY != null;



        /// <summary>
        /// Альтернативная системная таблица данных для альтернативных режимов отображения
        /// </summary>
        public DataTable MatrixDataTable { get; } = new();

        /// <summary>
        /// Сюда при переносе Helix Toolkit улетит трехмерная Mesh-геометрия рельефа карты
        /// </summary>
        public object? Surface3DGeometry { get; private set; }

 
        /// <summary>
        /// Метод обратной сборки: пакует ячейки в плоский payload для отправки TX-пакета записи в UART
        /// </summary>
        public double[] GetFlatPayloadForTx()
        {
            double[] flatData = new double[TotalElements];
            int index = 0;

            for (int c = 0; c < Cols; c++)
            {
                for (int r = 0; r < Rows; r++)
                {
                    flatData[index++] = MatrixData[r, c];
                }
            }
            return flatData;
        }



        /// <summary>
        /// Реализация Column-Major десериализации тяжелых массивов из UART
        /// </summary>
        public override void UpdateDataFromRawPayload(double[] rawData)
        {
            // 🔥 СЕТЕВОЙ ЩИТ: Если инженер прямо сейчас крутит PageUp/Down, 
            // полностью игнорируем любые затирающие фоновые пакеты из UART! [1.14]
            //if (IsUpdatingFromNetwork) return;


            if (rawData == null || rawData.Length == 0) return;
            //if (IsUpdatingFromNetwork) return; // Сетевой щит [1.14]
            if (Rows <= 0 || Cols <= 0) return;

            // 1. Обновляем базовый двумерный массив ОЗУ
            MatrixData = new double[Rows, Cols];
            int index = 0;
            for (int c = 0; c < Cols; c++)
            {
                for (int r = 0; r < Rows; r++)
                {
                    if (index >= rawData.Length) break;
                    MatrixData[r, c] = rawData[index++];
                }
            }

            // 2. Синхронизируем плоскую коллекцию ячеек для WPF UniformGrid
            // Чтобы не пересоздавать объекты (WPF этого не любит), лениво наполняем сетку
            if (MatrixCells.Count != TotalElements)
            {
                MatrixCells.Clear();
                for (int r = 0; r < Rows; r++)
                {
                    for (int c = 0; c < Cols; c++)
                    {
                        MatrixCells.Add(new MatrixCellViewModel
                        {
                            Parent = this,
                            Row = r,
                            Col = c
                        });
                    }
                }
            }

            // 3. Заливаем свежие строковые значения в ячейки на экране в один проход
            int cellIndex = 0;
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    MatrixCells[cellIndex++].ValueText = MatrixData[r, c].ToString("F2");
                }
            }

            // 4. Пересчитываем рамку выделения
            UpdateSelectionHighlight();
            // 🔥 ЖИВОЙ СЕТЕВОЙ ПИНОК:
            // Принудительно уведомляем систему, что массив 3D-данных обновился из UART!
            OnPropertyChanged(nameof(MatrixData));
        }

        /// <summary>
        /// Двумерный пересчет неоновой подсветки выделенной области ячеек таблицы
        /// </summary>
        public override void UpdateSelectionHighlight()
        {
            // Рассчитываем границы прямоугольника выделения мыши (поддержка Drag-выделения в любую сторону)
            int startRow = Math.Min(AnchorRow, SelectedRow);
            int endRow = Math.Max(AnchorRow, SelectedRow);
            int startCol = Math.Min(AnchorCol, SelectedCol);
            int endCol = Math.Max(AnchorCol, SelectedCol);

            // Пробегаем по всей сетке ячеек и выставляем флаги подсветки
            foreach (var cell in MatrixCells)
            {
                bool rowInBounds = (cell.Row >= startRow && cell.Row <= endRow);
                bool colInBounds = (cell.Col >= startCol && cell.Col <= endCol);

                // Включаем синюю рамку выделения инженера
                cell.IsSelected = rowInBounds && colInBounds;
            }
            base.UpdateSelectionHighlight();
        }

        public override double GetTableValue(int r, int c) => MatrixData[r, c];
        protected override void SetTableValue(int r, int c, double val) => MatrixData[r, c] = val;

        /// <summary>
        /// 3D-карта перехватывает триггер изменения ОЗУ и пинает свой виджет на перерисовку рельефа! [1.14]
        /// </summary>
        protected override void OnTableDataChanged()
        {
            // Находим виджет этой карты на экране и принудительно перестраиваем Helix-сцену!
            var mainVm = System.Windows.Application.Current?.MainWindow?.DataContext as MainViewModel;
            Matrix3DWidgetViewModel myWidget = (mainVm?.ActiveWidgets?.FirstOrDefault(w => w.DataSource == this && (w is Matrix3DWidgetViewModel)) as Matrix3DWidgetViewModel);

            myWidget?.Refresh();

            // Тут же в будущем будет выстрел TX-команды записи обновленной карты по UART!
        }


        /// <summary>
        /// ДИСПЕТЧЕР: Запускает вертикальную интерполяцию по колонкам для выделенной области.
        /// </summary>
        public void InterpolateVertical()
        {
            if (TotalElements <= 1 || MatrixCells == null) return;

            // Поколоночно обходим всю матрицу
            for (int c = 0; c < Cols; c++)
            {
                // Выделяем из ОЗУ ячейки только для текущей колонки, которые выбраны инженером
                var selectedInCol = MatrixCells
                    .Where(cell => cell.Col == c && cell.IsSelected)
                    .OrderBy(cell => cell.Row)
                    .ToList();

                // Вызываем мелкий математический метод сглаживания одной колонки
                InterpolateSingleColumn(c, selectedInCol);
            }

            OnTableDataChanged();
        }

        /// <summary>
        /// МАТЕМАТИКА КОЛОНКИ: Сглаживает одну конкретную колонку между крайними выделенными строками.
        /// </summary>
        private void InterpolateSingleColumn(int c, System.Collections.Generic.List<MatrixCellViewModel> selectedCells)
        {
            // Если в колонке выделено меньше 2 ячеек — выходим
            if (selectedCells.Count < 2) return;

            int startRow = selectedCells.First().Row;
            int endRow = selectedCells.Last().Row;
            int deltaRows = endRow - startRow;

            double startValue = MatrixData[startRow, c];
            double endValue = MatrixData[endRow, c];
            double stepDelta = (endValue - startValue) / deltaRows;

            // Сишный заполняющий цикл градиента по строкам
            for (int r = startRow; r <= endRow; r++)
            {
                double calculatedValue = startValue + (stepDelta * (r - startRow));

                MatrixData[r, c] = calculatedValue;

                var cell = MatrixCells.FirstOrDefault(m => m.Row == r && m.Col == c);
                if (cell != null)
                {
                    cell.ValueText = calculatedValue.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }


        /// <summary>
        /// Переопределяем триггер лазера: плавно двигаем красную точку прицела по 3D-полигонам Helix! [1.14]
        /// </summary>
        protected override void UpdateLaserBeamPosition(double exactCol, double exactRow)
        {
            // Безопасно пинаем виджет 3D-поверхности, у которого живет лазерный меш [1.14]
            var mainVm = System.Windows.Application.Current?.MainWindow?.DataContext as MainViewModel;
            Matrix3DWidgetViewModel my3DWidget = (mainVm?.ActiveWidgets?.FirstOrDefault(w => w.DataSource == this && (w is Matrix3DWidgetViewModel)) as Matrix3DWidgetViewModel);

            my3DWidget?.UpdateLaserBeamPosition(exactCol, exactRow);
        }
        /// <summary>
        /// Шаг 1: Поиск минимального, максимального значений матрицы и дельты диапазона
        /// </summary>
        public void FindMatrixExtremes(out double minVal, out double maxVal, out double delta)
        {
            minVal = double.MaxValue;
            maxVal = double.MinValue;

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    double v = MatrixData[r, c];
                    if (v < minVal) minVal = v;
                    if (v > maxVal) maxVal = v;
                }
            }
            delta = maxVal - minVal;
        }


        /// <summary>
        /// Переливает текст из плоских ячеек UniformGrid обратно в двумерный массив MatrixData [1.14]
        /// </summary>
        public override void SyncDataFromCells()
        {
            if (MatrixCells == null || MatrixData == null) return;

            foreach (var cell in MatrixCells)
            {
                if (cell == null) continue;

                // 🚀 ПУЛЕНЕПРОБИВАЕМЫЙ ПАРСЕР: 
                // 1. Берем строку, вырезаем пробелы и принудительно меняем любые запятые на точки!
                string safeText = cell.ValueText?.Replace(',', '.').Trim() ?? "0";

                // 2. Используем NumberStyles.Float (запрещает разделители тысяч, разрешает только точку и минус)
                if (double.TryParse(safeText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedVal))
                {
                    // Теперь строка "11.00" или "11,00" ГАРАНТИРОВАННО превратится в число 11.0! [1.14]
                    MatrixData[cell.Row, cell.Col] = parsedVal;
                }
            }
        }


    }
}
