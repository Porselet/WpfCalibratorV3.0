using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfCalibrator.Models;

namespace WpfCalibrator.ViewModels;

/// <summary>
/// Обертка над переменной Symulink для отображения в UI.
/// </summary>
public partial class VariableViewModel : INotifyPropertyChanged
{
    // Метаданные из JSON-паспорта
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Type { get; init; } = ""; // "single", "double", "int32" и т.д.
    public int ElementSize { get; init; } // Размер одного элемента в байтах
    public bool IsParam { get; init; } // True, если это параметр (calibratable)
    public int Rows { get; init; }
    public int Cols { get; init; }
    public string Comment { get; init; } = "";
    public byte ModelId { get; init; } // <=== Добавлено

    // Вычисляемые свойства
    public int TotalElements => Rows * Cols;
    public int TotalBytes => TotalElements * ElementSize;
    // Настройки отображения (из user_view_config.json)
    public string ControlView { get; set; } = "TextBox"; // Тип виджета: TextBox, Graph, Gauge...
    public float MinValue { get; set; } = 0.0f; // Для слайдеров
    public float MaxValue { get; set; } = 100.0f;

    // Привязки осей для таблиц (Look-up tables)
    public VariableViewModel? BoundAxisX { get; set; }
    public VariableViewModel? BoundAxisY { get; set; }
    public VariableViewModel? BoundInputX { get; set; }
    public VariableViewModel? BoundInputY { get; set; }

    // Логика подсветки (для таблиц)
    private int _activeRowIndex = -1;
    private int _activeColIndex = -1;




    public int ActiveRowIndex
    {
        get => _activeRowIndex;
        set
        {
            _activeRowIndex = value;
            OnPropertyChanged();
        }
    }

    public int ActiveColIndex
    {
        get => _activeColIndex;
        set
        {
            _activeColIndex = value;
            OnPropertyChanged();
        }
    }

    // Добавляем недостающие свойства
    private float[,] _matrixData = new float[0, 0];

    /// <summary>
    /// Двумерный массив данных (строки х столбцы) для отображения в DataGrid.
    /// </summary>
    public float[,] MatrixData
    {
        get => _matrixData;
        set
        {
            _matrixData = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Возвращает true, если для этой таблицы настроены все привязки и можно двигать прицел.
    /// </summary>
    public bool IsLutLinked =>
        BoundAxisX != null &&
        BoundAxisY != null &&
        BoundInputX != null &&
        BoundInputY != null;

    // Реализация INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    // Вспомогательный метод для вызова события
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public VariableViewModel(VariableConfig config, byte modelId)
    {
        Id = config.Id;
        Name = config.Name;
        Type = config.Type;
        ElementSize = config.ElementSize;
        IsParam = config.IsParam;
        Rows = config.Rows;
        Cols = config.Cols;
        Comment = config.Comment;
        ModelId = modelId; // <=== Передаем ID модели
    }

    // Методы для работы с MatrixData
    public void UpdateMatrixValue(int row, int col, float newValue)
    {
        // Проверка границ
        if (row >= 0 && row < Rows && col >= 0 && col < Cols)
        {
            MatrixData[row, col] = newValue;
            OnPropertyChanged(nameof(MatrixData)); // Уведомляем UI об изменении
        }
    }
}