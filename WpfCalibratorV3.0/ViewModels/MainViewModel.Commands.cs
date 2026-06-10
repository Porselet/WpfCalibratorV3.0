using System;
using System.Windows.Input;
using WpfCalibrator.Services;

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
}
