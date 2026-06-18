using System.Collections.ObjectModel;

namespace WpfCalibrator.ViewModels
{
    public partial class VariableViewModel
    {
        private ObservableCollection<MatrixCellViewModel> _matrixCells = new();
        public ObservableCollection<MatrixCellViewModel> MatrixCells => _matrixCells;
        public void RebuildMatrixCells(bool isFromUart = false)
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


            // // 2. ТОЧЕЧНОЕ ОБНОВЛЕНИЕ ПО ТАЙМЕРУ
            int index = 0;
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    var cell = MatrixCells[index++];

                    // Переключаем неоновый прицел (это работает всегда)
                    bool shouldBeActive = (r == ActiveRowIndex && c == ActiveColIndex);
                    if (cell.IsActive != shouldBeActive)
                    {
                        cell.IsActive = shouldBeActive;
                    }

                    // Вычисляем условия защиты
                    bool isCurrentCellBeingEdited = IsEditing && (r == SelectedRow && c == SelectedCol);
                    bool isCellSelected = cell.IsSelected; // Используем уже извлеченную ячейку cell!

                    // БРОНЕБОЙНАЯ ЗАЩИТА: Щит от затирания включается ТОЛЬКО если данные прилетели из UART!
                    bool shouldProtectFromUart = isCurrentCellBeingEdited || (isFromUart && isCellSelected);

                    // Если щит активен, мы Пропускаем обновление текста (continue), 
                    // сохраняя ручной ввод или инкремент от затирания фоновой телеметрией
                    if (shouldProtectFromUart)
                    {
                        continue;
                    }

                    // Если мы сами нажали PageUp (isFromUart == false), или ячейка вне группы — 
                    // спокойно и плавно обновляем текст на экране из ОЗУ матрицы!
                    string freshText = MatrixData[r, c].ToString("F1");
                    if (cell.ValueText != freshText)
                    {
                        cell.ValueText = freshText;
                    }
                }
            }

            UpdateSelectionHighlight();
        }

        /// <summary>
        /// Пересчитывает и обновляет визуальное выделение ячеек таблицы на экране (Прямоугольник MoTeC-style)
        /// </summary>
        public void UpdateSelectionHighlight()
        {
            // Если индексы некорректны, сбрасываем подсветку
            if (SelectedRow < 0 || SelectedCol < 0 || AnchorRow < 0 || AnchorCol < 0) return;

            // Вычисляем математические границы нашего прямоугольника выделения
            int minRow = Math.Min(AnchorRow, SelectedRow);
            int maxRow = Math.Max(AnchorRow, SelectedRow);
            int minCol = Math.Min(AnchorCol, SelectedCol);
            int maxCol = Math.Max(AnchorCol, SelectedCol);

            // Бежим циклом по всей сетке ячеек таблицы
            foreach (var cell in MatrixCells)
            {
                // Ячейка выделена, если её координаты r и c попадают внутрь рассчитанных границ прямоугольника
                bool shouldBeSelected = (cell.Row >= minRow && cell.Row <= maxRow) &&
                                        (cell.Col >= minCol && cell.Col <= maxCol);

                cell.IsSelected = shouldBeSelected;
            }
        }



        private int _anchorRow = -1;
        private int _anchorCol = -1;

        /// <summary>
        /// Индекс строки «якоря» — стартовой точки группового выделения ячеек
        /// </summary>
        public int AnchorRow
        {
            get => _anchorRow;
            set { if (_anchorRow != value) { _anchorRow = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Индекс колонки «якоря» — стартовой точки группового выделения ячеек
        /// </summary>
        public int AnchorCol
        {
            get => _anchorCol;
            set { if (_anchorCol != value) { _anchorCol = value; OnPropertyChanged(); } }
        }



    }
}
