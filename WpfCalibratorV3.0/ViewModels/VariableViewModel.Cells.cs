using System.Collections.ObjectModel;

namespace WpfCalibrator.ViewModels;

public partial class VariableViewModel
{
    // Коллекция ячеек для отображения в UI (DataGrid)
    private ObservableCollection<MatrixCellViewModel> _cells = new();

    public ObservableCollection<MatrixCellViewModel> Cells
    {
        get => _cells;
        set
        {
            _cells = value;
            OnPropertyChanged();
        }
    }

    // Генерация коллекции ячеек из MatrixData
    public void RebuildMatrixCells()
    {
        // Очищаем старую коллекцию
        Cells.Clear();

        // Генерируем новую коллекцию для каждой ячейки матрицы
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                // Создаем ячейку с текущим значением из MatrixData
                var cell = new MatrixCellViewModel
                {
                    Parent = this,
                    Row = r,
                    Col = c,
                    ValueText = MatrixData[r, c].ToString("F1") // Формат: 1 знак после запятой
                };

                // Подсвечиваем ячейку, если она совпадает с режимной точкой
                cell.IsActive = (r == ActiveRowIndex && c == ActiveColIndex);

                // Добавляем в коллекцию
                Cells.Add(cell);
            }
        }

        // Дополнительно: уведомляем UI о полном обновлении коллекции
        OnPropertyChanged(nameof(Cells));
    }
}