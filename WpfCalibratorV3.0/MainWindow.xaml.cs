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
        this.PreviewMouseLeftButtonDown += (s, e) =>
        {
            if (this.DataContext is MainViewModel vm)
            {
                // Находим визуальный элемент, по которому кликнули
                var element = e.OriginalSource as System.Windows.FrameworkElement;
                // Ищем, к какому виджету WidgetViewModel принадлежит этот элемент UI
                var clickedWidget = element?.DataContext as WidgetViewModel;

                // Если кликнули по параметру — переносим фокус на него!
                if (clickedWidget != null && clickedWidget.DataSource != null && clickedWidget.DataSource.IsParam)
                {
                    // Сбрасываем фокус у всех окон на холсте
                    foreach (var w in vm.ActiveWidgets) w.IsActiveWidget = false;

                    // Зажигаем рамку у того, по которому кликнули
                    clickedWidget.IsActiveWidget = true;
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

        // УМНЫЙ ПОИСК MOTEC: Находим тот виджет на холсте, который СЕЙЧАС выделен жирной рамкой!
        var activeWidget = mainVm.ActiveWidgets.FirstOrDefault(w => w.IsActiveWidget);

        // Если ни один виджет еще не выбран (например, только открыли программу) — 
        // берем первый попавшийся параметр по умолчанию в качестве страховки
        if (activeWidget == null)
        {
            activeWidget = mainVm.ActiveWidgets.FirstOrDefault(w => w.DataSource != null && w.DataSource.IsParam);
            if (activeWidget != null) activeWidget.IsActiveWidget = true; // Сразу подсвечиваем его
        }

        var activeTable = activeWidget?.DataSource;
        if (activeTable == null || activeWidget == null) return;

        // Если мы УЖЕ пишем текст внутри TextBox, и нажата НЕ клавиша Enter — 
        // даем калибровщику спокойно дописать число
        if (activeTable.IsEditing && e.Key != Key.Enter) return;

        bool handled = false;

        // 1. НАВИГАЦИЯ СТРЕЛОЧКАМИ (Активна ТОЛЬКО для многомерных таблиц)
        if (activeWidget.ControlView == "MatrixTable")
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

        // 2. ИЗМЕНЕНИЕ ЗНАЧЕНИЙ КЛАВИШАМИ PAGE UP / PAGE DOWN (Работает и для таблиц, и для скаляров!)
        if (e.Key == Key.PageUp || e.Key == Key.PageDown)
        {
            float sign = (e.Key == Key.PageUp) ? 1.0f : -1.0f;
            float delta = activeWidget.IncrementStep * sign;

            if (activeWidget.ControlView == "MatrixTable")
            {
                // Изменяем конкретную выбранную ячейку 3D-карты
                float currentVal = (float)activeTable.MatrixData[activeTable.SelectedRow, activeTable.SelectedCol];
                activeTable.UpdateMatrixValue(activeTable.SelectedRow, activeTable.SelectedCol, currentVal + delta);
            }
            else
            {
                // ИСПРАВЛЕНО: Изменяем одиночный скалярный параметр (TextBox) прямо на холсте!
                activeTable.CurrentValue += delta;
            }
            handled = true;
        }

        // 3. ВВОД ЦИФР С КЛАВИАТУРЫ (Работает и для таблиц, и для скаляров!)
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

                if (activeWidget.ControlView == "MatrixTable")
                {
                    if (activeTable.MatrixCells != null)
                    {
                        var targetCell = activeTable.MatrixCells.FirstOrDefault(c => c.Row == activeTable.SelectedRow && c.Col == activeTable.SelectedCol);
                        if (targetCell != null)
                        {
                            activeTable.IsEditing = true;
                            targetCell.ValueText = string.Empty; // Чистим под ввод в одно касание
                        }
                    }
                }
                else
                {
                    // ИСПРАВЛЕНО: Открываем прямой ввод для одиночного скаляра
                    activeTable.IsEditing = true;

                    // Чтобы скаляр тоже стирался в одно касание, находим TextBox одиночного параметра 
                    // и выделяем его текст целиком через диспетчер фокуса
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var focusedTextBox = FocusManager.GetFocusedElement(this) as TextBox;
                        focusedTextBox?.SelectAll();
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
                break;

            // Навигация верхнего уровня (Tab), которую мы написали на прошлом шаге
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
