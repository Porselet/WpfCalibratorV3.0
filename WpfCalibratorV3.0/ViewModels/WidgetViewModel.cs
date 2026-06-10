using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
    public double Left { get; set; } = 0;
    public double Top { get; set; } = 0;
    public double Width { get; set; } = 100;
    public double Height { get; set; } = 30;

    // Реализация INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}