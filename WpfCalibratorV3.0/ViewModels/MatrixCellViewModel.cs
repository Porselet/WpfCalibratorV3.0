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
    private string _valueText = "0.0";
    public string ValueText
    {
        get => _valueText;
        set
        {
            if (_valueText != value)
            {
                _valueText = value;
                OnPropertyChanged();

                // Преобразуем текст в число и обновляем родительскую матрицу
                if (float.TryParse(value, out float numericValue))
                {
                    Parent?.UpdateMatrixValue(Row, Col, numericValue);
                }
            }
        }
    }

    // Активность ячейки (для подсветки прицела)
    public bool IsActive { get; set; } = false;

    // Реализация INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}