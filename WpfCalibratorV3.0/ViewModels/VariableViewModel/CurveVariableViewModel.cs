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
        /// Переопределенный метод выделения колонок для 1D-кривой [1.14]
        /// </summary>
        public override void UpdateSelectionHighlight()
        {
            if (MatrixCells == null) return;

            // 1. Считаем границы выделения мыши по горизонтали (колонки)
            int startCol = Math.Min(AnchorCol, SelectedCol);
            int endCol = Math.Max(AnchorCol, SelectedCol);

            // 2. Обходим плоский список ячеек шкалы оцифровки
            foreach (var cell in MatrixCells)
            {
                if (cell == null) continue;

                // Включаем синюю рамку выделения инженера только в границах колонок
                cell.IsSelected = (cell.Col >= startCol && cell.Col <= endCol);
            }

            // 3. 🔥 СВЯЗУЮЩИЙ МОСТ: Прыгаем в базу, чтобы зажечь неоновый прицел моторной точки! [1.14]
            base.UpdateSelectionHighlight();
        }




    }
}
