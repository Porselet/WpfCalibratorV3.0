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
        /// Локальное обновление подсветки для одномерной шкалы
        /// </summary>
        public abstract void UpdateSelectionHighlight();

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
                        if (newValue > ScaleMax) newValue = ScaleMax;
                        if (newValue < ScaleMin) newValue = ScaleMin;

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
            if (finalValue > ScaleMax) finalValue = ScaleMax;
            if (finalValue < ScaleMin) finalValue = ScaleMin;

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




    }
}
