using System.Collections.ObjectModel;

namespace WpfCalibrator.ViewModels
{
    public partial class VariableViewModel
    {
        private ObservableCollection<MatrixCellViewModel> _matrixCells = new();
        public ObservableCollection<MatrixCellViewModel> MatrixCells => _matrixCells;
        public void RebuildMatrixCells()
        {
            if (Rows <= 0 || Cols <= 0) return;

            int totalNeeded = Rows * Cols;

            // 1. ПЕРВЫЙ СТАРТ: Если коллекция пустая, один раз создаем TextBox-ы
            // Использовано правильное имя свойства из вашего ядра — MatrixCells
            if (MatrixCells == null || MatrixCells.Count == 0 || MatrixCells.Count != totalNeeded)
            {
                // Если по какой-то причине в коде была очистка, сбрасываем и наполняем
                if (MatrixCells != null)
                {
                    // Так как MatrixCells в ядре может быть только для чтения (get), 
                    // мы очищаем внутренний список, а не пересоздаем его через new()
                    MatrixCells.Clear();
                }

                for (int r = 0; r < Rows; r++)
                {
                    for (int c = 0; c < Cols; c++)
                    {
                        var cell = new MatrixCellViewModel
                        {
                            Parent = this,
                            Row = r,
                            Col = c,
                            ValueText = MatrixData[r, c].ToString("F1")
                        };
                        cell.IsActive = (r == ActiveRowIndex && c == ActiveColIndex);
                        cell.IsSelected = (r == SelectedRow && c == SelectedCol);
                        MatrixCells.Add(cell);
                    }
                }
                OnPropertyChanged(nameof(MatrixCells));
                return;
            }

            // 2. РАБОТА ПО ТАЙМЕРУ: Быстро и точечно обновляем неон и данные
            int index = 0;
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    var cell = MatrixCells[index++];

                    // Переключаем неоновый прицел
                    bool shouldBeActive = (r == ActiveRowIndex && c == ActiveColIndex);
                    if (cell.IsActive != shouldBeActive)
                    {
                        cell.IsActive = shouldBeActive;
                    }

                    // ОЖИВЛЯЕМ ДАННЫЕ: Переводим float из MatrixData в строку UI.
                    string freshText = MatrixData[r, c].ToString("F1");
                    if (cell.ValueText != freshText)
                    {
                        cell.ValueText = freshText;
                    }
                }
            }
            UpdateSelectionHighlight();
        }

        public void UpdateSelectionHighlight()
        {
            if (MatrixCells == null || MatrixCells.Count == 0) return;

            int index = 0;
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    // Берем ячейку по индексу в плоском списке UniformGrid
                    if (index < MatrixCells.Count)
                    {
                        var cell = MatrixCells[index++];

                        // Проверяем, совпадает ли она с выбранными координатами калибровщика
                        bool shouldBeSelected = (r == SelectedRow && c == SelectedCol);
                        if (cell.IsSelected != shouldBeSelected)
                        {
                            cell.IsSelected = shouldBeSelected;
                        }
                    }
                }
            }
        }


    }
}
