using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace WpfCalibrator.ViewModels;

/// <summary>
/// Обертка для виджета на приборной панели.
/// </summary>
public class WidgetViewModel : INotifyPropertyChanged
{
    // Уникальный идентификатор виджета
    public Guid Id { get; } = Guid.NewGuid();

    // Переменная, данные которой отображает виджет
    public VariableViewModel? DataSource { get; set; }

    // Тип виджета (TextBox, Graph, Gauge...)
    public string ControlView { get; set; } = "TextBox";

    // Координаты и размер (для свободного позиционирования)
    // ИСПРАВЛЕНО: Теперь свойства уведомляют XAML о движении
    private double _left = 0;
    public double Left
    {
        get => _left;
        set { _left = value; OnPropertyChanged(); }
    }

    private double _top = 0;
    public double Top
    {
        get => _top;
        set { _top = value; OnPropertyChanged(); }
    }
    public double Width { get; set; } = 100;
    public double Height { get; set; } = 30;

    // Реализация INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ... внутри класса WidgetViewModel:



    private void ExecuteSubmitChanges()
    {
        // Виджет сообщает системе: "Мои данные изменились, отправьте меня в UART!"
        // Мы можем сгенерировать событие или вызвать метод центрального менеджера
        if (DataSource != null)
        {
            // Передаем управление в центральный сервис
            // Чуть позже мы свяжем это с вашим CommunicationService
        }
    }

}