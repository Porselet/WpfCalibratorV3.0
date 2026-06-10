using System.Collections.Generic;

namespace WpfCalibrator.Models;

/// <summary>
/// Корневой объект пользовательского конфига.
/// </summary>
public class UserViewConfig
{
    public string LastUsedComPort { get; set; } = "COM1";
    public int LastUsedBaudRate { get; set; } = 115200;

    // Словарь настроек для каждой переменной
    public Dictionary<string, VarViewItem> VariableViews { get; set; } = new();
}

/// <summary>
/// Настройки отображения для одной переменной.
/// </summary>
public class VarViewItem
{
    public string VarName { get; set; } = ""; // Имя переменной (например, "TEST_Data")
    public string ControlView { get; set; } = "TextBox"; // Тип виджета: TextBox, Graph, Gauge...
    public float MinValue { get; set; } = 0.0f; // Для слайдеров
    public float MaxValue { get; set; } = 100.0f;

    // Настройки для 2D-текстур (Look-up tables)
    public LutBindings TableBindings { get; set; } = new();

    // Добавляем недостающие свойства для привязок осей
    public string AxisX_VarName { get; set; } = ""; // Имя переменной-оси X
    public string AxisY_VarName { get; set; } = ""; // Имя переменной-оси Y
    public string InputX_VarName { get; set; } = ""; // Имя сигнала для оси X
    public string InputY_VarName { get; set; } = ""; // Имя сигнала для оси Y

}

/// <summary>
/// Настройки привязок осей для 2D-матриц.
/// </summary>
public class LutBindings
{
    public bool HasBindings { get; set; } = false;
    public string AxisX_VarName { get; set; } = ""; // Имя переменной-оси X
    public string AxisY_VarName { get; set; } = ""; // Имя переменной-оси Y
    public string InputX_VarName { get; set; } = ""; // Имя сигнала для оси X
    public string InputY_VarName { get; set; } = ""; // Имя сигнала для оси Y
}