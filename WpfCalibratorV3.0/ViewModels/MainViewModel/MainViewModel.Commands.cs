using System;
using System.Windows.Input;

using WpfCalibrator.Services;
using WpfCalibrator.Views;


namespace WpfCalibrator.ViewModels;

public partial class MainViewModel
{
    // 1. Базовый класс для всех команд приложения
    private abstract class BaseCommand : ICommand
    {
        public abstract bool CanExecute(object? parameter);
        public abstract void Execute(object? parameter);
        public event EventHandler? CanExecuteChanged;

        protected void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }




    // 2. Конкретная реализация команды для подключения/отключения UART
    private class ToggleConnectionCommandImpl : BaseCommand
    {
        private readonly MainViewModel _parent;

        public ToggleConnectionCommandImpl(MainViewModel parent)
        {
            _parent = parent;
        }

        public override bool CanExecute(object? parameter) => true;

        public override void Execute(object? parameter)
        {
            _parent.ToggleConnection();
        }
    }

    // 3. Свойство для привязки к кнопке в UI.
    // 🔥 ЧИСТЫЙ СИШНЫЙ ХАК: Инициализируем команду прямо здесь в одну строчку!
    // Теперь нам не нужно лезть в Core.cs и править там конструктор, всё взлетит само.
    public ICommand ToggleConnectionCommand => _toggleConnectionCommand ??= new ToggleConnectionCommandImpl(this);
    private ICommand? _toggleConnectionCommand;

    // Публичное свойство команды клика по ячейке таблицы
    private ICommand? _selectMatrixCellCommand;
    public ICommand SelectMatrixCellCommand => _selectMatrixCellCommand ??= new SelectMatrixCellCommandImpl(this);

    private class SelectMatrixCellCommandImpl : BaseCommand
    {
        private readonly MainViewModel _parent;
        public SelectMatrixCellCommandImpl(MainViewModel parent) => _parent = parent;

        public override bool CanExecute(object? parameter) => true;

        public override void Execute(object? parameter)
        {
            // Из параметра достаем вьюмодель ячейки, по которой кликнули
            var cellVm = parameter as MatrixCellViewModel;
            if (cellVm == null || cellVm.Parent == null) return;

            // Передаем координаты в родительскую таблицу
            cellVm.Parent.SelectedRow = cellVm.Row;
            cellVm.Parent.SelectedCol = cellVm.Col;

            // Сбрасываем режим редактирования, если мы просто перемещаем синий курсор мышкой
            if (!cellVm.Parent.IsEditing)
            {
                // Находим холст через главное окно и принудительно возвращаем ему фокус,
                // чтобы клавиатура не застревала внутри TextBox-ов при кликах мыши
                var mainWindow = System.Windows.Application.Current.MainWindow;
                var canvas = mainWindow?.FindName("CentralCanvas") as System.Windows.FrameworkElement;
                canvas?.Focus();
            }
        }
    }


    // 1. Публичное свойство команды переключения левой панели
    private ICommand? _toggleLeftPanelCommand;
    public ICommand ToggleLeftPanelCommand => _toggleLeftPanelCommand ??= new ToggleLeftPanelCommandImpl(this);

    // 2. Внутренний сишный драйвер команды
    private class ToggleLeftPanelCommandImpl : BaseCommand
    {
        private readonly MainViewModel _parent;
        public ToggleLeftPanelCommandImpl(MainViewModel parent) => _parent = parent;

        // Команда инверсии доступна всегда, независимо от состояния UART
        public override bool CanExecute(object? parameter) => true;

        public override void Execute(object? parameter)
        {
            // Переворачиваем состояние видимости в ОЗУ
            _parent.IsLeftPanelVisible = !_parent.IsLeftPanelVisible;
        }
    }


    private ICommand? _saveToFlashCommand;

    /// <summary>
    /// Ручка управления: отправляет в контроллер STM32 команду-триггер персистентного сохранения калибровок во флеш-память.
    /// </summary>
    public ICommand SaveToFlashCommand => _saveToFlashCommand ??= new SaveToFlashCommandInternal(this);


    /// <summary>
    /// Внутренний сишный драйвер команды сохранения во флеш-память МК.
    /// Наследует абстрактный BaseCommand с полной реализацией контракта.
    /// </summary>
    private class SaveToFlashCommandInternal : BaseCommand
    {
        private readonly MainViewModel _vm;
        public SaveToFlashCommandInternal(MainViewModel vm) => _vm = vm;

        // Обязательный метод контракта: кнопка активна всегда, когда запущен софт
        public override bool CanExecute(object? parameter)
        {
            return true;
        }

        // Физика выстрела пакета в медь провода
        public override void Execute(object? parameter)
        {
            if (Services.BusArbiter.AsInterface.IsRunning)
            {
                // Твой идеальный сишный кадр: xAA x00 x03 x00 x00
                var flashSaveCmd = new Models.NetworkCommand
                {
                    ModelId = 0,                             // Общий уровень
                    Cmd = Models.LinkCommand.FlashSave,       // Команда записи/управления (0x03)
                    VarId = 0,                               // Глобальный триггер
                    PayloadData = Array.Empty<double>(),     // Длина 0 (пустой payload)
                    Rows = 0,
                    Cols = 0
                };

                // Выстреливаем команду в приоритетную очередь Арбитра
                Services.BusArbiter.AsInterface.PushCommand(flashSaveCmd);
            }
        }
    }




    // 1. Публичное свойство команды принудительного сохранения экрана
    private ICommand? _saveLayoutCommand;
    public ICommand SaveLayoutCommand => _saveLayoutCommand ??= new SaveLayoutCommandImpl(this);

    private class SaveLayoutCommandImpl : BaseCommand
    {
        private readonly MainViewModel _parent;
        public SaveLayoutCommandImpl(MainViewModel parent) => _parent = parent;

        // ИСПРАВЛЕНО: Разрешаем сохранять конфигурацию экрана всегда (даже без подключенного МК)
        public override bool CanExecute(object? parameter) => true;

        public override void Execute(object? parameter)
        {
            // ... дальше идет наша логика сброса фокуса, которую мы облегчили на прошлом шаге ...

            // И ТЕПЕРЬ ДЛЯ ВЕРХНЕЙ КНОПКИ: 
            // Так как эта команда теперь вызывается и по Enter, и по клику на верхнюю кнопку меню,
            // мы принудительно вызываем физическую запись JSON на диск ЗДЕСЬ, 
            // чтобы верхняя кнопка честно выполняла свою роль сохранения дизайна!
            _parent.SaveCurrentLayoutInternal();
        }
    }


    // 2. Публичное свойство команды удаления выбранного экрана
    private ICommand? _deleteLayoutCommand;
    public ICommand DeleteLayoutCommand => _deleteLayoutCommand ??= new DeleteLayoutCommandImpl(this);

    private class DeleteLayoutCommandImpl : BaseCommand
    {
        private readonly MainViewModel _parent;
        public DeleteLayoutCommandImpl(MainViewModel parent) => _parent = parent;
        public override bool CanExecute(object? parameter) => _parent.LayoutNames.Count > 1;
        public override void Execute(object? parameter)
        {
            if (parameter is string layoutName)
            {
                _parent.DeleteLayout(layoutName);
            }
        }
    }

    // Публичное свойство команды удаления виджета с холста
    private ICommand? _deleteWidgetCommand;
    public ICommand DeleteWidgetCommand => _deleteWidgetCommand ??= new DeleteWidgetCommandImpl(this);

    private class DeleteWidgetCommandImpl : BaseCommand
    {
        private readonly MainViewModel _parent;
        public DeleteWidgetCommandImpl(MainViewModel parent) => _parent = parent;

        public override bool CanExecute(object? parameter) => true;

        public override void Execute(object? parameter)
        {
            var widgetVm = parameter as WidgetViewModel;
            if (widgetVm != null && _parent.ActiveWidgets.Contains(widgetVm))
            {
                // Удаляем виджет из коллекции активных окон на холсте
                _parent.ActiveWidgets.Remove(widgetVm);

                // Сразу вызываем сохранение экрана, чтобы закрытое окно исчезло из JSON-конфига
                if (_parent.SaveLayoutCommand.CanExecute(null))
                {
                    _parent.SaveLayoutCommand.Execute(null);
                }
            }
        }
    }

    // Публичное свойство команды добавления нового экрана
    private ICommand? _addLayoutCommand;
    public ICommand AddLayoutCommand => _addLayoutCommand ??= new AddLayoutCommandImpl(this);

    private class AddLayoutCommandImpl : BaseCommand
    {
        private readonly MainViewModel _parent;
        public AddLayoutCommandImpl(MainViewModel parent) => _parent = parent;

        public override bool CanExecute(object? parameter) => _parent.SelectedDevice != null;

        public override void Execute(object? parameter)
        {
            // Создаем наше кастомное инженерное окно
            var dialog = new NewLayoutWindow();

            // Делаем главное окно приложения владельцем диалога, чтобы он центрировался красиво поверх него
            if (System.Windows.Application.Current.MainWindow != null)
            {
                dialog.Owner = System.Windows.Application.Current.MainWindow;
            }

            // Если пользователь нажал "ОК" (DialogResult == true)
            if (dialog.ShowDialog() == true)
            {
                // Вызываем метод ядра для создания новой вкладки
                _parent.AddNewLayout(dialog.ResultResult);
            }
        }
    }

    // Публичное свойство команды открытия настроек таблицы
    private ICommand? _openTableSettingsCommand;
    public ICommand OpenTableSettingsCommand => _openTableSettingsCommand ??= new OpenTableSettingsCommandImpl(this);

    private class OpenTableSettingsCommandImpl : BaseCommand
    {
        private readonly MainViewModel _parent;
        public OpenTableSettingsCommandImpl(MainViewModel parent) => _parent = parent;

        public override bool CanExecute(object? parameter) => _parent.SelectedDevice != null;

        public override void Execute(object? parameter)
        {
            // Из параметра команды (CommandParameter) достаем вьюмодель виджета таблицы
            var widgetVm = parameter as WidgetViewModel;
            if (widgetVm == null || widgetVm.DataSource == null) return;

            // Собираем все переменные текущей модели в один плоский список
            var allVariables = new List<VariableViewModelBase>();
            allVariables.AddRange(_parent.ParameterVariables);
            allVariables.AddRange(_parent.TelemetryVariables);

            // Создаем наше окно настроек, передавая туда таблицу и список переменных
            //            var settingsWindow = new TableSettingsWindow(widgetVm.DataSource, allVariables);
            var settingsWindow = new TableSettingsWindow(widgetVm, allVariables);
            // Делаем главное окно владельцем диалога для правильного центрирования
            if (System.Windows.Application.Current.MainWindow != null)
            {
                settingsWindow.Owner = System.Windows.Application.Current.MainWindow;
            }

            // Открываем окно модально. Если пользователь нажал "ПРИМЕНИТЬ" (true)
            // Открываем окно модально. Если пользователь нажал "ПРИМЕНИТЬ" (true)
            if (settingsWindow.ShowDialog() == true)
            {
                // Принудительно заставляем таблицу обновить ячейки на экране ноутбука [1.14]
                if (widgetVm.DataSource is TableVariableViewModelBase tableVar)
                {
                    tableVar.UpdateSelectionHighlight(); // Наш новый базовый метод отрисовки рамок! [1.14]
                }

                // Автоматически сохраняем обновленные связи экрана в JSON
                if (_parent.SaveLayoutCommand.CanExecute(null))
                {
                    _parent.SaveLayoutCommand.Execute(null);
                }
            }

        }
    }




}
