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

    }
}
