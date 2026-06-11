using System;
using System.Windows.Input;
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




    // 1. Публичное свойство команды принудительного сохранения экрана
    private ICommand? _saveLayoutCommand;
    public ICommand SaveLayoutCommand => _saveLayoutCommand ??= new SaveLayoutCommandImpl(this);

    private class SaveLayoutCommandImpl : BaseCommand
    {
        private readonly MainViewModel _parent;
        public SaveLayoutCommandImpl(MainViewModel parent) => _parent = parent;
        public override bool CanExecute(object? parameter) => _parent.SelectedDevice != null;
        public override void Execute(object? parameter) => _parent.SaveCurrentLayoutInternal();
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


}
