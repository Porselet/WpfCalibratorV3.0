using System;
using System.Collections.ObjectModel;
using System.Data;

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

        // Привязанные оцифрованные оси шкал (Обороты X и Нагрузка Y)
        public CurveVariableViewModel? BoundAxisX { get; set; }
        public CurveVariableViewModel? BoundAxisY { get; set; }

        /// <summary>
        /// Коллекция ячеек для нашего нового резинового UniformGrid со скроллбарами
        /// </summary>
        // Примечание: Если твоя старая ячейка называется иначе (например, MatrixCell), 
        // просто замени тип внутри ObservableCollection на твой старый класс.
        public ObservableCollection<MatrixCellViewModel> MatrixCells { get; } = new();

        /// <summary>
        /// Альтернативная системная таблица данных для альтернативных режимов отображения
        /// </summary>
        public DataTable MatrixDataTable { get; } = new();

        /// <summary>
        /// Сюда при переносе Helix Toolkit улетит трехмерная Mesh-геометрия рельефа карты
        /// </summary>
        public object? Surface3DGeometry { get; private set; }

        /// <summary>
        /// Реализация Column-Major десериализации тяжелых массивов из UART
        /// </summary>
        public override void UpdateDataFromRawPayload(double[] rawData)
        {
            if (rawData == null || rawData.Length == 0) return;
            if (Rows <= 0 || Cols <= 0) return;

            // Инициализируем двумерное зеркало под текущую сетку прошивки
            MatrixData = new double[Rows, Cols];

            // Наш скоростной Си-парсер маршалинга по столбцам (Column-Major)
            int index = 0;
            for (int c = 0; c < Cols; c++)
            {
                for (int r = 0; r < Rows; r++)
                {
                    if (index >= rawData.Length) break;
                    MatrixData[r, c] = rawData[index++];
                }
            }

            // 🔥 Тут на следующем шаге мы вызовем синхронизацию коллекции MatrixCells,
            // чтобы обновить цифры в ячейках на экране без лагов интерфейса!
        }

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

        public override void UpdateSelectionHighlight()
        {
            // Оставляем пока пустым {}, это наш безопасный плацдарм
        }

    }
}
