using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfCalibrator.Models;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.Views;

public partial class WorkspaceCanvas : UserControl
{
    private bool _isMovingWidget = false;
    private Point _widgetStartMousePosition;
    private WidgetViewModel? _draggedWidgetDataContext;

    public WorkspaceCanvas()
    {
        InitializeComponent();
    }

    // ==================== ИСПРАВЛЕННАЯ ЛОГИКА ЗАКРЫТИЯ (❌) ====================


    // ==================== ИСПРАВЛЕННАЯ ЛОГИКА ДВИЖЕНИЯ ОКНА МЫШКОЙ ====================

    // Офсеты (смещения) курсора относительно левого верхнего угла самого движимого окна
    private double _mouseOffsetInWidgetX;
    private double _mouseOffsetInWidgetY;

    private void WidgetHeader_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Если оператор кликнул по кнопке закрытия ❌, игнорируем перемещение
        if (e.OriginalSource is Button || e.OriginalSource is TextBlock tb && tb.Text == "❌") return;

        if (sender is Border headerBorder)
        {
            // Достаем WidgetViewModel через контейнер
            var container = GetParentOfType<ContentPresenter>(headerBorder);
            _draggedWidgetDataContext = container?.Content as WidgetViewModel;

            if (_draggedWidgetDataContext != null)
            {
                _isMovingWidget = true;

                // 1. Получаем абсолютные координаты мыши на холсте Canvas в момент клика
                Point mouseOnCanvas = e.GetPosition(this);

                // 2. Вычисляем и запоминаем, в какую точку внутри окна кликнул оператор
                _mouseOffsetInWidgetX = mouseOnCanvas.X - _draggedWidgetDataContext.Left;
                _mouseOffsetInWidgetY = mouseOnCanvas.Y - _draggedWidgetDataContext.Top;

                headerBorder.CaptureMouse(); // Жестко захватываем фокус мыши
                e.Handled = true;
            }
        }
    }

    private void WidgetHeader_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMovingWidget || _draggedWidgetDataContext == null) return;

        // Получаем текущую абсолютную координату мыши на холсте Canvas
        Point currentMousePosition = e.GetPosition(this);

        // Рассчитываем новые чистые координаты окна (Позиция мыши минус стартовый офсет клика)
        double newLeft = currentMousePosition.X - _mouseOffsetInWidgetX;
        double newTop = currentMousePosition.Y - _mouseOffsetInWidgetY;

        // Инженерная магнитная сетка: жесткий шаг 10 пикселей для выравнивания окон
        const double gridStep = 10.0;
        _draggedWidgetDataContext.Left = Math.Round(newLeft / gridStep) * gridStep;
        _draggedWidgetDataContext.Top = Math.Round(newTop / gridStep) * gridStep;

        e.Handled = true;
    }

    private void WidgetHeader_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isMovingWidget && sender is Border headerBorder)
        {
            _isMovingWidget = false;
            headerBorder.ReleaseMouseCapture(); // Освобождаем мышь
            _draggedWidgetDataContext = null;
            e.Handled = true;
        }
    }
    // ==================== ВСПОМОГАТЕЛЬНЫЙ МЕТОД (ПОИСК РОДИТЕЛЯ) ====================
    private T? GetParentOfType<T>(DependencyObject element) where T : DependencyObject
    {
        while (element != null)
        {
            if (element is T parent) return parent;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    // ==================== ЛОГИКА DROP (СБРОС ИЗ ДЕРЕВА) ====================
    /* 
     * //Старая, рабочая версия
    private void Canvas_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(VariableConfig)) && sender is Canvas canvas)
        {
            var variable = (VariableConfig)e.Data.GetData(typeof(VariableConfig));
            if (variable == null || DataContext is not MainViewModel vm) return;

            Point dropPosition = e.GetPosition(canvas);

            const double gridStep = 10.0;
            double snappedX = Math.Round(dropPosition.X / gridStep) * gridStep;
            double snappedY = Math.Round(dropPosition.Y / gridStep) * gridStep;

            if (variable.IsParam)
            {
                string viewType = variable.TotalElements > 1 ? "MatrixTable" : "TextBox";
                CreateWidgetOnWorkspace(vm, variable, snappedX, snappedY, viewType);
            }
            else
            {
                ShowWidgetSelectorMenu(canvas, vm, variable, snappedX, snappedY);
            }

            e.Handled = true;
        }
    }
    //*/
    private void Canvas_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(VariableConfig)) && sender is Canvas canvas)
        {
            var variable = (VariableConfig)e.Data.GetData(typeof(VariableConfig));
            if (variable == null || DataContext is not MainViewModel vm) return;

            // ЗАЩИТА ОТ ДУБЛИКАТОВ (MoTeC-Style):
            // Если это калибровочный ПАРАМЕТР, проверяем, нет ли его уже на текущем рабочем столе
            // ЗАЩИТА ОТ ДУБЛИКАТОВ (ИСПРАВЛЕНО: Учитываем ModelId каждого МК!)
            // Если это калибровочный ПАРАМЕТР, проверяем, нет ли его уже на текущем рабочем столе
            if (variable.IsParam)
            {
                // Теперь мы проверяем СВЯЗКУ: совпадает и Id переменной, И ModelId физической платы!
                var existingWidget = vm.ActiveWidgets.FirstOrDefault(w =>
                    w.DataSource != null &&
                    w.DataSource.Id == variable.Id &&
                    w.DataSource.ModelId == variable.ModelId);

                if (existingWidget != null)
                {
                    // Параметр от этой конкретной платы уже на экране! Не плодим клонов — просто выводим старое окно на передний план
                    int maxCurrentZ = vm.ActiveWidgets.Count > 0 ? vm.ActiveWidgets.Max(w => w.ZIndex) : 0;
                    existingWidget.ZIndex = maxCurrentZ + 1;

                    // Переносим жирную рамку фокуса на него
                    foreach (var w in vm.ActiveWidgets) w.IsActiveWidget = false;
                    existingWidget.IsActiveWidget = true;

                    e.Handled = true;
                    return; // Тихо выходим, запрещая создание дубликата!
                }
            }

            Point dropPosition = e.GetPosition(canvas);

            const double gridStep = 10.0;
            double snappedX = Math.Round(dropPosition.X / gridStep) * gridStep;
            double snappedY = Math.Round(dropPosition.Y / gridStep) * gridStep;

            if (variable.IsParam)
            {
                // Калибровочные параметры (Матрицы или Скаляры) создаются тихо
                string viewType = variable.TotalElements > 1 ? "MatrixTable" : "TextBox";
                CreateWidgetOnWorkspace(vm, variable, snappedX, snappedY, viewType);
            }
            else
            {
                // ТИХИЙ РЕЖИМ ДЛЯ ДАТЧИКОВ: Вместо ShowWidgetSelectorMenu сразу создаем Digital!
                CreateWidgetOnWorkspace(vm, variable, snappedX, snappedY, "Digital");
            }

            e.Handled = true;
        }
    }



    private void ShowWidgetSelectorMenu(Canvas canvas, MainViewModel vm, VariableConfig variable, double x, double y)
    {
        // Создаем меню и сразу красим его подложку в глубокий серый цвет
        var menu = new ContextMenu
        {
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E")),
            BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#555555")),
            BorderThickness = new Thickness(1)
        };

        // Вспомогательная сишная лямбда-функция для быстрой штамповки контрастных пунктов меню
        MenuItem CreateDarkMenuItem(string header, RoutedEventHandler clickHandler)
        {
            var item = new MenuItem
            {
                Header = header,
                Foreground = System.Windows.Media.Brushes.White, // Белый контрастный текст
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E")),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 13,
                Padding = new Thickness(10, 6, 20, 6)
            };
            item.Click += clickHandler;
            return item;
        }

        // Собираем пункты меню через наш темный шаблон
        var itemDisplay = CreateDarkMenuItem("🔢 Крупные цифры", (s, e) => CreateWidgetOnWorkspace(vm, variable, x, y, "Digital"));
        var itemSlider = CreateDarkMenuItem("📊 Линейный индикатор (Слайдер)", (s, e) => CreateWidgetOnWorkspace(vm, variable, x, y, "Slider"));
        var itemGauge = CreateDarkMenuItem("🧭 Стрелочный прибор (Gauge)", (s, e) => CreateWidgetOnWorkspace(vm, variable, x, y, "Gauge"));

        menu.Items.Add(itemDisplay);
        menu.Items.Add(itemSlider);
        menu.Items.Add(itemGauge);

        // Открываем строго под курсором мыши
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }


    private async void CreateWidgetOnWorkspace(MainViewModel vm, VariableConfig variable, double x, double y, string viewType)
    {
        // ИСПРАВЛЕНИЕ: Ищем уже существующую Вью-Модель переменной в коллекциях ядра, 
        // вместо того чтобы создавать дубликат через new!
        var realVariableVm = vm.ParameterVariables.FirstOrDefault(v => v.Id == variable.Id && v.ModelId == variable.ModelId)
                           ?? vm.TelemetryVariables.FirstOrDefault(v => v.Id == variable.Id && v.ModelId == variable.ModelId);

        // Если вдруг в глобальных коллекциях её нет (например, первый запуск), 
        // только тогда создаем как резервный вариант
        if (realVariableVm == null)
        {
            realVariableVm = new VariableViewModel(variable, variable.ModelId);
        }

        // Создаем графический контейнер, который теперь смотрит на ЕДИНСТВЕННЫЙ правильный источник данных
        var widget = new WidgetViewModel
        {
            Left = x,
            Top = y,
            ControlView = viewType,
            DataSource = realVariableVm
        };

        if (variable.IsParam && variable.TotalElements > 1) // Карты LUT
        {
            widget.Width = 500;
            widget.Height = 280;
        }
        else if (viewType == "Gauge") // Стрелочный круглый прибор требует квадратное окно побольше
        {
            widget.Width = 180;
            widget.Height = 210;
        }
        else // Скаляры, Крупные цифры и линейные Слайдеры
        {
            widget.Width = 260;
            widget.Height = 110;
        }

        
        vm.ActiveWidgets.Add(widget);

        // 🔥 ДОБАВЛЕННЫЙ ТРИГГЕР: Если это калибровочный параметр (скаляр или таблица), 
        // запрашиваем его текущие данные из ОЗУ микроконтроллера СТРОГО ОДИН РАЗ
        if (variable.IsParam)
        {
            // Вызываем наш новый метод из MainViewModel
            await vm.RequestSingleVariableReadAsync(variable.ModelId, (byte)variable.Id, variable.TotalElements);
        }
    }


    private bool _isResizing = false;
    private Point _resizeStartPoint;
    private double _widgetStartWidth;
    private double _widgetStartHeight;
    private WidgetViewModel? _resizingWidget;

    // 1. Нажатие на маркер в углу окна
    private void ResizeMarker_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var element = sender as FrameworkElement;
        if (element == null) return;

        // Ищем вьюмодель виджета, размер которого меняем
        _resizingWidget = element.DataContext as WidgetViewModel;
        if (_resizingWidget == null) return;

        _isResizing = true;
        _resizeStartPoint = e.GetPosition(this); // Позиция относительно всего холста
        _widgetStartWidth = _resizingWidget.Width;
        _widgetStartHeight = _resizingWidget.Height;

        // Захватываем мышь, чтобы движения не срывались при быстром перемещении
        element.CaptureMouse();
        e.Handled = true;
    }

    // 2. Движение мыши при изменении размера
    private void ResizeMarker_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isResizing || _resizingWidget == null) return;

        Point currentPoint = e.GetPosition(this);

        // Вычисляем, насколько далеко сдвинулся курсор от точки старта
        double deltaX = currentPoint.X - _resizeStartPoint.X;
        double deltaY = currentPoint.Y - _resizeStartPoint.Y;

        // Магнитная сетка (шаг 10 пикселей)
        const double gridStep = 10.0;

        double newWidth = _widgetStartWidth + deltaX;
        double newHeight = _widgetStartHeight + deltaY;

        // Округляем до ближайшего шага сетки
        newWidth = Math.Round(newWidth / gridStep) * gridStep;
        newHeight = Math.Round(newHeight / gridStep) * gridStep;

        // Ограничиваем минимальные размеры, чтобы окно не схлопнулось
        if (newWidth < 100) newWidth = 100;
        if (newHeight < 40) newHeight = 40;

        // Обновляем размеры во вьюмодели — XAML мгновенно растянет окно!
        _resizingWidget.Width = newWidth;
        _resizingWidget.Height = newHeight;

        e.Handled = true;
    }

    // 3. Отпускание кнопки мыши
    private void ResizeMarker_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isResizing)
        {
            var element = sender as FrameworkElement;
            element?.ReleaseMouseCapture();

            _isResizing = false;
            _resizingWidget = null;

            // После изменения размеров принудительно сохраняем состояние экрана
            if (DataContext is MainViewModel vm)
            {
                // Вызываем наше внутреннее сохранение, чтобы изменения записались в JSON
                // Метод приватный, но лежит в этом же классе в partial, так что вызов сработает.
                // Если компилятор ругнется на приватность, мы можем вызвать команду:
                if (vm.SaveLayoutCommand.CanExecute(null))
                {
                    vm.SaveLayoutCommand.Execute(null);
                }
            }
        }
        e.Handled = true;
    }







// Глобальный перехват клика мыши по любой ячейке на холсте
private void GlobalMatrixTable_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Находим, по какому именно визуальному элементу кликнул калибровщик
        var visualElement = e.OriginalSource as FrameworkElement;
        if (visualElement == null) return;

        // Проверяем, лежит ли внутри этого элемента вьюмодель ячейки таблицы MatrixCellViewModel
        var cellVm = visualElement.DataContext as MatrixCellViewModel;

        // Если кликнули мимо таблицы (например, по обычному скаляру или пустому холсту) — ничего не делаем
        if (cellVm == null || cellVm.Parent == null) return;

        // 1. Перемещаем синий курсор MoTeC в памяти C#
        cellVm.Parent.SelectedRow = cellVm.Row;
        cellVm.Parent.SelectedCol = cellVm.Col;
        // ======================================================================
        // НОВОЕ: УПРАВЛЕНИЕ ЯКОРЕМ ГРУППОВОГО ВЫДЕЛЕНИЯ
        // ======================================================================
        bool isShiftPressed = System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) ||
                             System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift);

        if (!isShiftPressed)
        {
            // Если Shift НЕ зажат — обычный клик сбрасывает группу и ставит якорь на эту же ячейку!
            cellVm.Parent.AnchorRow = cellVm.Row;
            cellVm.Parent.AnchorCol = cellVm.Col;
        }

        // Принудительно вызываем обновление подсветки для новой геометрии
        cellVm.Parent.UpdateSelectionHighlight();
        // 2. ЗАХВАТ КЛАВИАТУРНОГО ФОКУСА ДЛЯ СТРЕЛОЧЕК:
        // Если таблица сейчас НЕ в режиме редактирования текста (IsEditing == false)
        // Внутри метода GlobalMatrixTable_PreviewMouseLeftButtonDown (в самом конце):
        if (!cellVm.Parent.IsEditing)
        {
            // 1. Гасим событие, чтобы Windows не активировала текстовый курсор внутри TextBox
            e.Handled = true;

            // 2. ЖЕЛЕЗОБЕТОННЫЙ ХАК ДЛЯ ФОКУСА:
            // Принудительно забираем фокус у TextBox и отдаем его самому холсту WorkspaceCanvas!
            // Это заставит Windows слать стрелочки клавиатуры строго в наш метод.
            this.Focus();

            // Дополнительно говорим системному менеджеру фокуса WPF, что холст теперь главный:
            System.Windows.Input.Keyboard.Focus(this);
        }

    }

}
