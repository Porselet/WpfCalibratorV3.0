using System;
using System.Linq;
using System.Reflection.Metadata;
using System.Windows.Input;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.Services
{
    /// <summary>
    /// НИЗКОУРОВНЕВЫЙ ДРАЙВЕР КЛАВИАТУРЫ (Контроллер бесфокусного ввода MoTeC-style).
    /// Изолирует логику навигации, модификаторов Ctrl/Shift и текстового буфера от файлов окон WPF.
    /// </summary>
    public static class InputController
    {
        /// <summary>
        /// Главная точка входа: обрабатывает нажатие клавиши для активного виджета.
        /// </summary>
        /// <returns>true — если клавиша была перехвачена и обработана драйвером; false — пропустить клавишу дальше.</returns>
        public static bool ProcessKeyDown(WidgetViewModel activeWidget, KeyEventArgs e)
        {
            if (activeWidget == null || activeWidget.DataSource == null) return false;

            var variable = activeWidget.DataSource;
            bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            // ======================================================================
            // 1. ПЕРЕХВАТ ENTER И ESC (Работаем через буфер виджета)
            // ======================================================================
            if (e.Key == Key.Enter && !string.IsNullOrEmpty(activeWidget.InputBuffer))
            {
                activeWidget.CommitInputBuffer(); // Наш новый метод фиксации в ОЗУ
                // SendBulkUpdateToNetwork(activeWidget); // Выстрел в UART
                return true;
            }

            if (e.Key == Key.Escape && !string.IsNullOrEmpty(activeWidget.InputBuffer))
            {
                activeWidget.ClearGraphBuffer(); // Сброс черновика набора
                return true;
            }

            // ======================================================================
            // 2. ОБРАБОТКА PAGE_UP / PAGE_DOWN (Наш схлопнутый AdjustValue)
            // ======================================================================
            if (e.Key == Key.PageUp || e.Key == Key.PageDown)
            {
                // Тут у тебя в коде использовался IncrementStep, пока передаем 1.0f или твой шаг
                double step = 1.0;
                if (isCtrlPressed) step *= 10.0;
                if (e.Key == Key.PageDown) step = -step;

                // Если идет ручной накат цифр в буфер
                if (!string.IsNullOrEmpty(activeWidget.InputBuffer))
                {
                    // Логика изменения буфера на лету (ChangeBufferByStep)
                }
                else
                {
                    // 🔥 НАШ ПОЛИМОРФНЫЙ СХЛОПНУТЫЙ ШЛЮЗ: Меняет и скаляры, и 3D-карты!
                    variable.AdjustValue(step);
                    // SendBulkUpdateToNetwork(activeWidget); // Выстрел пачки в UART
                }
                return true;
            }
            // ======================================================================
            // 3. НАВИГАЦИЯ СТРЕЛОЧКАМИ ДЛЯ ТАБЛИЦ (Приведение к TableVariable)
            // ======================================================================
            if (activeWidget.ControlView == "MatrixTable" && variable is TableVariableViewModelBase activeTable)
            {
                bool isNavKey = e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right;
                if (isNavKey)
                {
                    if (!string.IsNullOrEmpty(activeWidget.InputBuffer)) activeWidget.CommitInputBuffer();

                    if (e.Key == Key.Up && activeTable.SelectedRow > 0) activeTable.SelectedRow--;
                    if (e.Key == Key.Down && activeTable.SelectedRow < activeTable.Rows - 1) activeTable.SelectedRow++;
                    if (e.Key == Key.Left && activeTable.SelectedCol > 0) activeTable.SelectedCol--;
                    if (e.Key == Key.Right && activeTable.SelectedCol < activeTable.Cols - 1) activeTable.SelectedCol++;

                    if (!isShiftPressed)
                    {
                        activeTable.AnchorRow = activeTable.SelectedRow;
                        activeTable.AnchorCol = activeTable.SelectedCol;
                    }

                    activeTable.UpdateSelectionHighlight();
                    return true;
                }
            }
            // ======================================================================
            // 3.5 ПРЫЖОК ПО ПРОБЕЛУ НА ЗЕЛЕНУЮ РЕЖИМНУЮ ТОЧКУ (Jump to Working Point)
            // ======================================================================
            if (e.Key == Key.Space && variable is TableVariableViewModelBase spaceTable)
            {
                // Закомментировал, так как метода в виджете еще нет [1.14]
                if (!string.IsNullOrEmpty(activeWidget.InputBuffer)) activeWidget.CommitInputBuffer();

                // Переносим рамку на ячейку, которую UART подсвечивает зеленым
                spaceTable.SelectedRow = spaceTable.SelectedRow; // Сюда подставишь свой ActiveRowIndex
                spaceTable.SelectedCol = spaceTable.SelectedCol; // Сюда подставишь свой ActiveColIndex

                if (!isShiftPressed)
                {
                    spaceTable.AnchorRow = spaceTable.SelectedRow;
                    spaceTable.AnchorCol = spaceTable.SelectedCol;
                }

                spaceTable.UpdateSelectionHighlight();
                return true;
            }
            // БЛОК 4: TAB - Переключение фокуса (только для параметров)
            if (e.Key == Key.Tab)
            {
                // Маркер ошибки для следующего шага
                if (!string.IsNullOrEmpty(activeWidget.InputBuffer)) activeWidget.CommitInputBuffer();

                if (System.Windows.Application.Current?.MainWindow?.DataContext is ViewModels.MainViewModel vm)
                {
                    // Логика переключения между активными параметрами
                    var paramWidgets = vm.ActiveWidgets?
                        .Where(w => w.DataSource?.IsParam == true)
                        .ToList();

                    if (paramWidgets?.Count > 1)
                    {
                        int nextIndex = (paramWidgets.IndexOf(activeWidget) + 1) % paramWidgets.Count;
                        vm.ActiveWidgets.ToList().ForEach(w => w.IsActiveWidget = false);
                        paramWidgets[nextIndex].IsActiveWidget = true;
                    }
                }
                return true;
            }
            // БЛОК 4.7: Горячие клавиши H/V [1.14]
            if (string.IsNullOrEmpty(activeWidget.InputBuffer) && activeWidget.ControlView == "MatrixTable")
            {
                // H - Горизонтальная интерполяция [1.14]
                if (e.Key == Key.H && variable is TableVariableViewModelBase tableVar)
                {
                    tableVar.InterpolateHorizontal();
                    return true;
                }
                // V - Вертикальная интерполяция (только 3D) [1.14]
                if (e.Key == Key.V && variable is Map3DVariableViewModel map3DVar)
                {
                    map3DVar.InterpolateVertical();
                    return true;
                }
            }
            // ======================================================================
            // БЛОК 5: ПЕРЕХВАТ ЦИФР И СИМВОЛОВ (Накопление в InputBuffer виджета)
            // ======================================================================
            // Метод ConvertKeyToChar — это твой оригинальный конвертер нажатий клавиш Windows в строки
            string pressedChar = ConvertKeyToChar(e.Key);

            if (!string.IsNullOrEmpty(pressedChar))
            {
                // Накапливаем символы прямо в черновик виджета на холсте! [1.14]
                activeWidget.AppendToBuffer(pressedChar);
                return true;
            }

            return false; // Все остальные системные клавиши пропускаем мимо

        }
        private static void SendBulkUpdateToNetwork(WidgetViewModel activeWidget)
        {
            if (activeWidget?.DataSource == null) return;
            var variable = activeWidget.DataSource;

            // В твоем оригинальном коде использовался флаг IsUpdatingFromNetwork (если он остался в базе)
            // Если его нет, проверку !variable.IsUpdatingFromNetwork можно временно опустить
            if (variable.IsParam && BusArbiter.AsInterface.IsRunning)
            {
                double[] flatPayload = Array.Empty<double>();

                // 🚀 ПОЛИМОРФНЫЙ МАРШАЛИНГ: Каждый класс сам отдаёт свой плоский слепок ОЗУ!
                if (variable is Map3DVariableViewModel map3D)
                {
                    flatPayload = map3D.GetFlatPayloadForTx(); // Твой Column-Major метод сборки матрицы 32х32 [1.14]
                }
                else if (variable is CurveVariableViewModel curve)
                {
                    flatPayload = curve.VectorData; // 1D-вектор отдаёт свой голый массив осей напрямую [1.14]
                }
                else if (variable is ScalarVariableViewModel scalar)
                {
                    flatPayload = new double[] { scalar.CurrentValue }; // Скаляр отдаёт одиночный элемент [1.14]
                }

                // Переходим к сборке TX-команды (Часть 2)
                // ======================================================================
                // ЧАСТЬ 2: СБОРКА МОНОЛИТНОГО TX-КАДРА И ВЫСТРЕЛ В ОЧЕРЕДЬ АРБИТРА
                // ======================================================================
                var writeCmd = new Models.NetworkCommand
                {
                    ModelId = variable.ModelId,
                    Cmd = Models.LinkCommand.VarWrite, // Команда записи (0x01)
                    VarId = (byte)variable.Id,
                    DataType = variable.Type,
                    Rows = variable.Rows,
                    Cols = variable.Cols,
                    PayloadData = flatPayload
                };

                // ВЫСТРЕЛ В МЕДЬ ПРОВОДА: Ровно один гарантированный пакет на нажатие Enter!
                BusArbiter.AsInterface.PushCommand(writeCmd);
            }
        } // Конец метода SendBulkUpdateToNetwork


        /// <summary>
        /// Вспомогательный сишный switch-маппер: переводит коды клавиш Windows в текстовые символы.
        /// </summary>
        private static string ConvertKeyToChar(Key key)
        {
            switch (key)
            {
                // Цифры основного ряда
                case Key.D0: return "0";
                case Key.D1: return "1";
                case Key.D2: return "2";
                case Key.D3: return "3";
                case Key.D4: return "4";
                case Key.D5: return "5";
                case Key.D6: return "6";
                case Key.D7: return "7";
                case Key.D8: return "8";
                case Key.D9: return "9";

                // Цифры боковой клавиатуры (NumPad)
                case Key.NumPad0: return "0";
                case Key.NumPad1: return "1";
                case Key.NumPad2: return "2";
                case Key.NumPad3: return "3";
                case Key.NumPad4: return "4";
                case Key.NumPad5: return "5";
                case Key.NumPad6: return "6";
                case Key.NumPad7: return "7";
                case Key.NumPad8: return "8";
                case Key.NumPad9: return "9";

                // Разделители и знаки (с автоматическим приведением к точке инвариантной культуры)
                case Key.OemPeriod: return ".";
                case Key.Decimal: return ".";
                case Key.OemComma: return ".";
                case Key.OemMinus: return "-";
                case Key.Subtract: return "-";

                default: return string.Empty;
            }
        }
    }
}
