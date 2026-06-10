using System;
using System.Windows.Input;
using WpfCalibrator.Services;

namespace WpfCalibrator.ViewModels;

public partial class MainViewModel
{
    // Базовый класс для команд
    private abstract class BaseCommand : ICommand
    {
        // Обязательный метод: доступность команды
        public abstract bool CanExecute(object? parameter);

        // Обязательный метод: выполнение команды
        public abstract void Execute(object? parameter);

        // Обязательное событие: уведомление об изменении доступности
        public event EventHandler? CanExecuteChanged;

        // Вспомогательный метод для вызова события
        protected void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Конкретная команда для подключения/отключения
    private class ToggleConnectionCommand : BaseCommand
    {
        private readonly MainViewModel _parent;

        public ToggleConnectionCommand(MainViewModel parent)
        {
            _parent = parent;
        }

        public override bool CanExecute(object? parameter) => true;

        public override void Execute(object? parameter)
        {
            _parent.ToggleConnection();
            
        }
    }

    // Свойство для привязки к кнопке в UI
    // Свойство для привязки к кнопке в UI
    //public ICommand ToggleConnectionCommand { get; }

    // Конструктор: создаем команду

}