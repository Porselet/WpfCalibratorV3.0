using System;
using System.Collections.ObjectModel;

namespace WpfCalibrator.ViewModels
{
    /// <summary>
    /// Модель одномерного вектора (Кривой / Калибровочной шкалы оси)
    /// </summary>
    public class CurveVariableViewModel : TableVariableViewModelBase
    {
        // Сырой массив физических значений калибровки в ОЗУ
        private double[] _vectorData = Array.Empty<double>();
        private int _activeIndex = -1;

        /// <summary>
        /// Живой массив данных одномерной шкалы
        /// </summary>
        public double[] VectorData
        {
            get => _vectorData;
            private set
            {
                _vectorData = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Коллекция строк для мгновенного вывода подписей оси в UniformGrid на UI
        /// </summary>
        public ObservableCollection<string> StringValues { get; } = new();


        /// <summary>
        /// Текущий активный индекс режимной точки на оси (рассчитывается аппаратно)
        /// </summary>
        public int ActiveIndex
        {
            get => _activeIndex;
            set
            {
                if (_activeIndex == value) return;
                _activeIndex = value;
                OnPropertyChanged();
            }
        }

        public override double GetTableValue(int r, int c) => VectorData[c];
        protected override void SetTableValue(int r, int c, double val) => VectorData[c] = val;
        // OnTableDataChanged для оси не нужен, 3D-сетки у неё нет


        /// <summary>
        /// Реализация скоростного маршалинга одномерного вектора из UART
        /// </summary>
        public override void UpdateDataFromRawPayload(double[] rawData)
        {
            if (rawData == null || rawData.Length == 0) return;

            // 1. Сохраняем физические числа
            VectorData = rawData;

            // 2. Обновляем текстовые подписи
            StringValues.Clear();
            for (int i = 0; i < rawData.Length; i++)
            {
                StringValues.Add(rawData[i].ToString("F0"));
            }

            // 3. Синхронизируем коллекцию интерактивных ячеек (размерность 1 x Cols)
            if (MatrixCells.Count != rawData.Length)
            {
                MatrixCells.Clear();
                for (int c = 0; c < rawData.Length; c++)
                {
                    MatrixCells.Add(new MatrixCellViewModel
                    {
                        Parent = this,
                        Row = 0, // У одномерного вектора строка всегда нулевая!
                        Col = c
                    });
                }
            }

            // 4. Заливаем свежие строковые значения в ячейки оси на экране
            for (int c = 0; c < rawData.Length; c++)
            {
                MatrixCells[c].ValueText = rawData[c].ToString("F1");
            }

            // 5. Пересчитываем одномерную рамку выделения
            UpdateSelectionHighlight();
        }

        /// <summary>
        /// Линейный (одномерный) пересчет выделения ячеек оси шкал
        /// </summary>
        public override void UpdateSelectionHighlight()
        {
            // У одномерной шкалы строки всегда = 0, поэтому рассчитываем 
            // границы выделения строго по горизонтали (по колонкам Col)
            int startCol = Math.Min(AnchorCol, SelectedCol);
            int endCol = Math.Max(AnchorCol, SelectedCol);

            // Пробегаем по линейке ячеек и включаем рамки выделения инженера
            foreach (var cell in MatrixCells)
            {
                // Ячейка выделена, если её индекс попал в диапазон протяжки мыши
                cell.IsSelected = (cell.Col >= startCol && cell.Col <= endCol);
            }
        }




    }
}
