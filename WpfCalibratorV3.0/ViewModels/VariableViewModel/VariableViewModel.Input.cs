using System;
using System.Globalization;
using System.Linq;

namespace WpfCalibrator.ViewModels
{
    /// <summary>
    /// Логика бесфокусного ввода данных с клавиатуры (Часть 5: Управление текстовым буфером).
    /// Полностью синхронизировано с именами полей, коллекцией MatrixCells и методом UpdateMatrixValue.
    /// </summary>
    public partial class VariableViewModel
    {
        /// <summary>
        /// Накопление строки ввода. Вызывается на каждый нажатый символ.
        /// </summary>
        /// <summary>
        /// Накопление строки ввода. Вызывается на каждый нажатый символ.
        /// Синхронно размножает вводимый текст по всей выделенной области в реальном времени.
        /// </summary>
        public void AppendToBuffer(string text)
        {
            // Накапливаем символы во временный буфер (автоматически взводит IsEditing)
            InputBuffer += text;

            // Если это 2D-таблица — раскатываем набираемый текст по ВСЕМ выделенным ячейкам прямоугольника!
            if (TotalElements > 1 && MatrixCells != null)
            {
                foreach (var cell in MatrixCells)
                {
                    // Если ячейка находится внутри неонового прицела выделения — 
                    // заставляем её мгновенно выводить текущее содержимое буфера на экран!
                    if (cell.IsSelected)
                    {
                        cell.ValueText = InputBuffer;
                    }
                }
            }
        }


        /// <summary>
        /// Фиксация ввода и пуш в железо (Нажатие ENTER или уход стрелочкой).
        /// </summary>
        /// <summary>
        /// Фиксация ввода и безопасный пуш в железо без перегрузки очереди UART.
        /// </summary>
        /// <summary>
        /// Фиксация ввода и безопасный пуш в железо без перегрузки очереди UART.
        /// </summary>
        /// <summary>
        /// Фиксация ввода: перекладывает накопленный текстовый буфер в чистую математику ОЗУ.
        /// Не производит самостоятельных выстрелов в UART.
        /// </summary>
        public void ApplyEditing()
        {
            if (string.IsNullOrEmpty(InputBuffer)) return;

            if (float.TryParse(InputBuffer, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float parsedValue) ||
                float.TryParse(InputBuffer, out parsedValue))
            {
                // Сбрасываем флаги ввода
                InputBuffer = string.Empty;
                IsEditing = false;

                if (TotalElements > 1)
                {
                    if (parsedValue > MaxValue) parsedValue = MaxValue;
                    if (parsedValue < MinValue) parsedValue = MinValue;

                    for (int r = 0; r < Rows; r++)
                    {
                        for (int c = 0; c < Cols; c++)
                        {
                            var cell = MatrixCells?.FirstOrDefault(m => m.Row == r && m.Col == c);
                            if (cell != null && cell.IsSelected)
                            {
                                MatrixData[r, c] = parsedValue;
                                cell.ValueText = parsedValue.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                            }
                        }
                    }
                }
                else
                {
                    if (parsedValue > MaxValue) parsedValue = MaxValue;
                    if (parsedValue < MinValue) parsedValue = MinValue;

                    CurrentValue = parsedValue;
                }
            }
            else
            {
                InputBuffer = string.Empty;
                IsEditing = false;
            }
        }

        /// <summary>
        /// Изменение числа внутри буфера на заданный шаг (Для PageUp/PageDown в режиме ввода).
        /// </summary>
        public void ChangeBufferByStep(float step)
        {
            if (!IsEditing || string.IsNullOrEmpty(InputBuffer))
            {
                if (TotalElements > 1)
                {
                    IncrementSelectedCell(step);
                }
                else
                {
                    CurrentValue += step;
                }
                return;
            }

            if (float.TryParse(InputBuffer, NumberStyles.Any, CultureInfo.InvariantCulture, out float currentValue) ||
                float.TryParse(InputBuffer, out currentValue))
            {
                float newValue = currentValue + step;

                // 🔥 Зажимаем число в твои оригинальные лимиты MaxValue / MinValue
                if (newValue > MaxValue) newValue = MaxValue;
                if (newValue < MinValue) newValue = MinValue;

                InputBuffer = newValue.ToString(CultureInfo.InvariantCulture);

                if (TotalElements > 1 && MatrixCells != null)
                {
                    var anchorCell = MatrixCells.FirstOrDefault(c => c.Row == SelectedRow && c.Col == SelectedCol);
                    if (anchorCell != null) anchorCell.ValueText = InputBuffer;
                }
            }
        }

        /// <summary>
        /// Отмена ввода (Нажатие ESC).
        /// </summary>
        public void CancelEditing()
        {
            InputBuffer = string.Empty;

            // Возвращаем на экран старые честные числа из памяти, которые затерлись при наборе
            if (TotalElements > 1)
            {
                RebuildMatrixCells(); // Твой родной метод перерисовки таблицы из MatrixData
            }
            else
            {
                OnPropertyChanged(nameof(CurrentValueText)); // Сбросит текст скаляра обратно на актуальный CurrentValue
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

            double startValue = MatrixData[r, startCol];
            double endValue = MatrixData[r, endCol];
            double stepDelta = (endValue - startValue) / deltaCols;

            // Сишный заполняющий цикл градиента по колонкам
            for (int c = startCol; c <= endCol; c++)
            {
                double calculatedValue = startValue + (stepDelta * (c - startCol));

                MatrixData[r, c] = calculatedValue;

                // Находим визуальную ячейку и обновляем экран
                var cell = MatrixCells.FirstOrDefault(m => m.Row == r && m.Col == c);
                if (cell != null)
                {
                    cell.ValueText = calculatedValue.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
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



    }
}
