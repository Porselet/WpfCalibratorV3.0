using System;

namespace WpfCalibrator.Models;

/// <summary>
/// Описание одной переменной (сигнала или параметра) из JSON-паспорта модели.
/// </summary>
public class VariableConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // "single", "double", "int32" и т.д.
    public int ElementSize { get; set; } // Размер одного элемента в байтах
    public bool IsParam { get; set; } // True, если это параметр (calibratable)
    public int Rows { get; set; } // Количество строк (для матриц)
    public int Cols { get; set; } // Количество столбцов (для матриц)
    public string Comment { get; set; } = "";

    // Добавляем ModelId (обязательное поле для идентификации модели)
    public byte ModelId { get; set; } // <=== Добавляем это

    // Вычисляемые свойства
    public int TotalElements => Rows * Cols;
    public int TotalBytes => TotalElements * ElementSize;
}