using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfCalibrator.Models;
using WpfCalibrator.Services;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator;

public partial class MainWindow : Window
{


    public MainWindow()
    {
        // Оставляем ОДИН вызов инициализации компонентов интерфейса
        InitializeComponent();

        // Собираем зависимости вручную (Pure DI)
        var configManager = new ConfigurationManager();
        var commService = new CommunicationService();

        // Инициализируем вьюмодель, передавая ей созданные сервисы
        var viewModel = new MainViewModel(commService, configManager);

        // Привязываем DataContext к главному окну. 
        // Все вложенные элементы (панель и холст) унаследуют его автоматически!
        DataContext = viewModel;

        // ГЛОБАЛЬНЫЙ ПЕРЕХВАТ КЛИКА МЫШИ ДЛЯ СМЕНЫ АКТИВНОГО ВИДЖЕТА
        // ГЛОБАЛЬНЫЙ ПЕРЕХВАТ КЛИКА МЫШИ ДЛЯ СМЕНЫ АКТИВНОГО ВИДЖЕТА И СЛОЯ ГЛУБИНЫ
        this.PreviewMouseLeftButtonDown += (s, e) =>
        {
            if (this.DataContext is MainViewModel vm)
            {
                var element = e.OriginalSource as System.Windows.FrameworkElement;
                var clickedWidget = element?.DataContext as WidgetViewModel;

                if (clickedWidget != null)
                {
                    // 1. УПРАВЛЕНИЕ СЛОЯМИ (Z-Index Хак):
                    // Находим, какой максимальный ZIndex сейчас есть среди всех окон на холсте
                    int maxCurrentZ = vm.ActiveWidgets.Count > 0 ? vm.ActiveWidgets.Max(w => w.ZIndex) : 0;

                    // Назначаем кликнутому виджету слой еще выше, чтобы он вылетел на самый передний план!
                    clickedWidget.ZIndex = maxCurrentZ + 1;

                    // 2. УПРАВЛЕНИЕ НАВИГАЦИЕЙ КЛАВИАТУРЫ (Наш прошлый Шаг 79)
                    // Переносим жирную неоновую рамку фокуса только на параметры
                    if (clickedWidget.DataSource != null && clickedWidget.DataSource.IsParam)
                    {
                        foreach (var w in vm.ActiveWidgets) w.IsActiveWidget = false;
                        clickedWidget.IsActiveWidget = true;
                    }
                }
            }
        };
    }


    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // Базовый обработчик клика по дереву (при необходимости)
    }

    private void GlobalWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var mainVm = this.DataContext as MainViewModel;
        if (mainVm == null) return;

        // 1. Ищем виджет, выделенный жирной рамкой
        var activeWidget = mainVm.ActiveWidgets.FirstOrDefault(w => w.IsActiveWidget);
        if (activeWidget == null)
        {
            activeWidget = mainVm.ActiveWidgets.FirstOrDefault(w => w.DataSource != null && w.DataSource.IsParam);
            if (activeWidget != null) activeWidget.IsActiveWidget = true;
        }

        var activeTable = activeWidget?.DataSource;
        if (activeTable == null || activeWidget == null) return;

        // 2. ХАК ДЛЯ ВВОДА ТЕКСТА: Если мы пишем цифры внутри TextBox, 
        // и нажата НЕ клавиша Enter, Esc, Tab, PageUp, PageDown — 
        // мы ВООБЩЕ выходим из метода и отдаем клавишу операционной системе, чтобы она печаталась!
        if (activeTable.IsEditing &&
            e.Key != Key.Enter && e.Key != Key.Escape && e.Key != Key.Tab &&
            e.Key != Key.PageUp && e.Key != Key.PageDown)
        {
            return;
        }

        bool handled = false;

        // 3. НАВИГАЦИЯ СТРЕЛОЧКАМИ ДЛЯ 3D-ТАБЛИЦ
        if (activeWidget.ControlView == "MatrixTable" && !activeTable.IsEditing)
        {
            switch (e.Key)
            {
                case Key.Up: activeTable.SelectedRow--; handled = true; break;
                case Key.Down: activeTable.SelectedRow++; handled = true; break;
                case Key.Left: activeTable.SelectedCol--; handled = true; break;
                case Key.Right: activeTable.SelectedCol++; handled = true; break;
                case Key.Space:
                    if (activeTable.ActiveRowIndex >= 0 && activeTable.ActiveColIndex >= 0)
                    {
                        activeTable.SelectedRow = activeTable.ActiveRowIndex;
                        activeTable.SelectedCol = activeTable.ActiveColIndex;
                        handled = true;
                    }
                    break;
            }
        }

        // 4. ИЗМЕНЕНИЕ ЗНАЧЕНИЙ ПО PAGE UP / PAGE DOWN
        if (e.Key == Key.PageUp || e.Key == Key.PageDown)
        {
            float sign = (e.Key == Key.PageUp) ? 1.0f : -1.0f;
            float delta = activeWidget.IncrementStep * sign;

            if (activeWidget.ControlView == "MatrixTable")
            {
                float currentVal = (float)activeTable.MatrixData[activeTable.SelectedRow, activeTable.SelectedCol];
                activeTable.UpdateMatrixValue(activeTable.SelectedRow, activeTable.SelectedCol, currentVal + delta);
            }
            else
            {
                activeTable.CurrentValue += delta;
                // Шлем скаляр в UART сразу по изменению PageUp/PageDown!
                _ = mainVm.SendTableToUartAsync(activeTable);
            }
            handled = true;
        }

        // 5. ФИКСАЦИЯ КАЛИБРОВКИ ПО НАЖАТИЮ ENTER (ЖЕЛЕЗОБЕТОННЫЙ ВОЗВРАТ)
        if (e.Key == Key.Enter && activeTable.IsEditing)
        {
            // Принудительно заставляем WPF зафиксировать текст из TextBox в память C#
            System.Windows.Input.FocusManager.SetFocusedElement(this, this);
            System.Windows.Input.Keyboard.Focus(this); // Возвращаем фокус Окну для стрелочек

            // Выключаем режим ввода ячейки/скаляра
            activeTable.IsEditing = false;

            // Если это одиночный скаляр — принудительно выстреливаем его в UART по Enter!
            // (Для таблиц отправка сработает автоматически через UpdateMatrixValue по потере фокуса)
            if (activeWidget.ControlView != "MatrixTable")
            {
                _ = mainVm.SendTableToUartAsync(activeTable);
            }

            handled = true;
        }

        // 6. СБРОС ВВОДА ПО ESCAPE
        if (e.Key == Key.Escape && activeTable.IsEditing)
        {
            System.Windows.Input.FocusManager.SetFocusedElement(this, this);
            System.Windows.Input.Keyboard.Focus(this);
            activeTable.IsEditing = false;
            handled = true;
        }

        // 7. ВВОД ПЕРВОЙ ЦИФРЫ И TAB
        switch (e.Key)
        {
            case Key.D0:
            case Key.D1:
            case Key.D2:
            case Key.D3:
            case Key.D4:
            case Key.D5:
            case Key.D6:
            case Key.D7:
            case Key.D8:
            case Key.D9:
            case Key.NumPad0:
            case Key.NumPad1:
            case Key.NumPad2:
            case Key.NumPad3:
            case Key.NumPad4:
            case Key.NumPad5:
            case Key.NumPad6:
            case Key.NumPad7:
            case Key.NumPad8:
            case Key.NumPad9:
            case Key.OemMinus:
            case Key.Subtract:
            case Key.OemPeriod:
            case Key.Decimal:

                if (activeTable.IsEditing) break;

                activeTable.IsEditing = true;

                if (activeWidget.ControlView == "MatrixTable")
                {
                    if (activeTable.MatrixCells != null)
                    {
                        var targetCell = activeTable.MatrixCells.FirstOrDefault(c => c.Row == activeTable.SelectedRow && c.Col == activeTable.SelectedCol);
                        if (targetCell != null) targetCell.ValueText = string.Empty; // Очистка в одно касание
                    }
                }
                else
                {
                    // Для скаляра тоже делаем очистку перед вводом новой цифры
                    activeTable.CurrentValue = 0; // Или сбрасываем строку, если завязано на текст
                }
                break;

            case Key.Tab:
                var parameterWidgets = mainVm.ActiveWidgets.Where(w => w.DataSource != null && w.DataSource.IsParam).ToList();
                if (parameterWidgets.Count > 0)
                {
                    var currentActive = parameterWidgets.FirstOrDefault(w => w.IsActiveWidget);
                    int nextIndex = 0;
                    if (currentActive != null)
                    {
                        currentActive.IsActiveWidget = false;
                        nextIndex = (parameterWidgets.IndexOf(currentActive) + 1) % parameterWidgets.Count;
                    }
                    parameterWidgets[nextIndex].IsActiveWidget = true;
                }
                handled = true;
                break;
        }

        if (handled)
        {
            e.Handled = true;
        }
    }

    // ==================== ЛОГИКА DROP (СБРОС НА ХОЛСТ) ====================
}
