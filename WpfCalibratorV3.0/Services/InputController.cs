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
            // 1. ПЕРЕХВАТ ENTER (Атомарная фиксация ОЗУ и ОДИН залповый выстрел в UART)
            // ======================================================================
            if (e.Key == Key.Enter && variable.IsEditing)
            {
                // Шаг А: Заставляем переменную перенести буфер в массивы памяти C#
                variable.ApplyEditing();

                // Шаг Б: Вызываем выделенный метод для сборки и выстрела пакета кадра
                SendBulkUpdateToNetwork(activeWidget);

                return true;
            }


            // 2. ПЕРЕХВАТ ESC (Полный отмена набора)
            if (e.Key == Key.Escape && variable.IsEditing)
            {
                variable.CancelEditing();
                return true;
            }

            // 3. ОБРАБОТКА PAGE_UP / PAGE_DOWN (Относительный шаг)
            if (e.Key == Key.PageUp || e.Key == Key.PageDown)
            {
                // Если зажат Ctrl — умножаем гоночный шаг на 10!
                float step = activeWidget.IncrementStep;
                if (isCtrlPressed) step *= 10f;

                if (e.Key == Key.PageDown) step = -step;

                variable.ChangeBufferByStep(step);
                SendBulkUpdateToNetwork(activeWidget);
                return true;
            }

            // 4. НАВИГАЦИЯ СТРЕЛОЧКАМИ И ПРОБЕЛОМ (Твой оригинальный Блок №3 из Пастбина)
            if (activeWidget.ControlView == "MatrixTable")
            {
                var activeTable = variable; // В твоей архитектуре VariableViewModel крутит ячейки таблицы
                bool isNavKey = e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Space;

                if (isNavKey)
                {
                    // 🔥 АВТОСОХРАНЕНИЕ MOTEC: Если инженер набрал число и нажал стрелку — 
                    // автоматически фиксируем и пушим буфер в UART перед переходом на другую ячейку!
                    if (variable.IsEditing)
                    {
                        variable.ApplyEditing();
                    }

                    switch (e.Key)
                    {
                        case Key.Up:
                            if (activeTable.SelectedRow > 0) activeTable.SelectedRow--;
                            break;

                        case Key.Down:
                            if (activeTable.SelectedRow < activeTable.Rows - 1) activeTable.SelectedRow++;
                            break;

                        case Key.Left:
                            if (activeTable.SelectedCol > 0) activeTable.SelectedCol--;
                            break;

                        case Key.Right:
                            if (activeTable.SelectedCol < activeTable.Cols - 1) activeTable.SelectedCol++;
                            break;

                        case Key.Space:
                            // 🔥 ПРЫЖОК НА РАБОЧУЮ ТОЧКУ (Jump to Working Point):
                            // Переносим неоновую рамку выделения строго на режимную ячейку, 
                            // которую фоновый UART подсвечивает зеленым цветом!
                            activeTable.SelectedRow = activeTable.ActiveRowIndex;
                            activeTable.SelectedCol = activeTable.ActiveColIndex;

                            // Сбрасываем и точку якоря (чтобы прямоугольник выделения схлопнулся вокруг зеленой ячейки)
                            if (!isShiftPressed)
                            {
                                activeTable.AnchorRow = activeTable.SelectedRow;
                                activeTable.AnchorCol = activeTable.SelectedCol;
                            }

                            //return true;
                            break;

                    }

                    // Твоя оригинальная логика Shift-выделения прямоугольника
                    if (!isShiftPressed)
                    {
                        activeTable.AnchorRow = activeTable.SelectedRow;
                        activeTable.AnchorCol = activeTable.SelectedCol;
                    }

                    // Перерисовываем неоновую рамку выделения на холсте
                    activeTable.UpdateSelectionHighlight();
                    return true;
                }
            }

            // ======================================================================
            // БЛОК 4.5: ГЛОБАЛЬНЫЙ ПЕРЕКЛЮЧАТЕЛЬ ВИДЖЕТОВ (Клавиша TAB)
            // ======================================================================
            if (e.Key == Key.Tab)
            {
                // 🔥 АВТОСОХРАНЕНИЕ: Если инженер что-то набирал на текущем приборе и нажал Tab — 
                // фиксируем буфер в UART перед тем, как бросить этот виджет!
                if (variable.IsEditing)
                {
                    variable.ApplyEditing();
                }

                // Извлекаем прямую ссылку на коллекцию виджетов из главного окна
                if (System.Windows.Application.Current?.MainWindow?.DataContext is ViewModels.MainViewModel vm)
                {
                    // Фильтруем коллекцию в ОЗУ: выбираем СТРОГО те виджеты, которые являются параметрами (IsParam == true)
                    var paramWidgets = vm.ActiveWidgets?
                        .Where(w => w.DataSource != null && w.DataSource.IsParam)
                        .ToList();

                    if (paramWidgets != null && paramWidgets.Count > 1)
                    {
                        // 1. Находим индекс текущего активного прибора в отфильтрованном списке параметров
                        int currentIndex = paramWidgets.IndexOf(activeWidget);

                        // Вычисляем индекс следующего параметра. Если дошли до конца списка — закольцовываем в 0
                        int nextIndex = (currentIndex + 1) % paramWidgets.Count;

                        // 2. Сбрасываем неоновую рамку активности ВООБЩЕ СО ВСЕХ приборов на холсте
                        foreach (var w in vm.ActiveWidgets)
                        {
                            w.IsActiveWidget = false;
                        }

                        // 3. Зажигаем рамку активности на следующем по очереди КАЛИБРОВОЧНОМ ПАРАМЕТРЕ!
                        paramWidgets[nextIndex].IsActiveWidget = true;
                    }
                }

                return true; // Прерываем событие Windows, чтобы фокус не улетал в системные кнопки
            }

            // ======================================================================
            // БЛОК 4.7: ГОРЯЧИЕ КЛАВИШИ ИНТЕРПОЛЯЦИИ ТАБЛИЦ (Клавиши H и V)
            // ======================================================================
            if (!variable.IsEditing && activeWidget.ControlView == "MatrixTable")
            {
                // Перехват клавиши H (Horizontal - Горизонтальное сглаживание)
                if (e.Key == Key.H)
                {
                    variable.InterpolateHorizontal(); // 💾 Обновили локальное ОЗУ
                    SendBulkUpdateToNetwork(activeWidget);
                    //variable.UpdateMatrixValue(-1, -1, -1.0f); // 🚀 ОДИН залповый выстрел Bulk-пакета в UART!
                    return true;
                }

                // Перехват клавиши V (Vertical - Вертикальное сглаживание)
                if (e.Key == Key.V)
                {
                    variable.InterpolateVertical(); // 💾 Обновили локальное ОЗУ
                    SendBulkUpdateToNetwork(activeWidget);
                    return true;
                }
            }



            // 5. ПЕРЕХВАТ ЦИФР И СИМВОЛОВ (Твой Блок №7 из Пастбина — Накопление в InputBuffer)
            string pressedChar = ConvertKeyToChar(e.Key);
            if (!string.IsNullOrEmpty(pressedChar))
            {
                variable.AppendToBuffer(pressedChar);
                return true;
            }

            return false; // Любые другие системные клавиши пропускаем мимо
        }

        /// <summary>
        /// Внутренний сишный маршалер: собирает плоский слепок данных из ОЗУ 
        /// и отправляет ОДИН Bulk-пакет записи в приоритетную очередь Арбитра.
        /// </summary>
        private static void SendBulkUpdateToNetwork(WidgetViewModel activeWidget)
        {
            var variable = activeWidget.DataSource;
            if (variable == null) return;

            // Если переменная является параметром и конвейер обмена запущен
            if (variable.IsParam && !variable.IsUpdatingFromNetwork && BusArbiter.AsInterface.IsRunning)
            {
                double[] flatPayload;

                // Разделяем физику скаляров и 2D-таблиц
                if (variable.TotalElements > 1)
                {
                    // ТАБЛИЦЫ: Вытаскиваем всю двухмерную матрицу MatrixData в плоский массив double[]
                    flatPayload = new double[variable.Rows * variable.Cols];
                    int idx = 0;
                    for (int r = 0; r < variable.Rows; r++)
                    {
                        for (int c = 0; c < variable.Cols; c++)
                        {
                            flatPayload[idx++] = variable.MatrixData[r, c];
                        }
                    }
                }
                else
                {
                    // СКАЛЯРЫ: Одиночный массив из одного элемента
                    flatPayload = new double[] { variable.CurrentValue };
                }

                // Сборка монолитного TX-кадра
                var writeCmd = new Models.NetworkCommand
                {
                    ModelId = variable.ModelId,
                    Cmd = Models.LinkCommand.VarWrite,
                    VarId = (byte)variable.Id,
                    DataType = variable.Type,
                    Rows = variable.Rows,
                    Cols = variable.Cols,
                    PayloadData = flatPayload
                };

                // ВЫСТРЕЛ В МЕДЬ ПРОВОДА: Ровно один гарантированный пакет на нажатие Enter!
                BusArbiter.AsInterface.PushCommand(writeCmd);
            }
        }


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
