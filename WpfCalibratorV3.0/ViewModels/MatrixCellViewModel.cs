using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfCalibrator.ViewModels;

/// <summary>
/// Обертка для ячейки таблицы.
/// </summary>
public class MatrixCellViewModel : INotifyPropertyChanged
{
    // Родительская таблица (для обновления MatrixData)
    public VariableViewModel? Parent { get; set; }

    // Координаты ячейки
    public int Row { get; set; }
    public int Col { get; set; }

    // Текущее значение ячейки (строка для отображения)
    private string _valueText = string.Empty;
    public string ValueText
    {
        get => _valueText;
        set
        {
            if (_valueText != value)
            {
                _valueText = value;
                OnPropertyChanged();

                // Передаем измененное инженером число обратно в главную матрицу переменной
                string normalizedValue = value.Replace('.', ',');
                if (float.TryParse(normalizedValue, out float numericValue))
                {
                    Parent?.UpdateMatrixValue(Row, Col, numericValue);
                }
            }
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    // Активность ячейки (для подсветки прицела)
    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged(); // Железно уведомляем XAML, что нужно включить неон!
            }
        }
    }

    // Реализация INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}