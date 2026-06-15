using System.Collections.Generic;

namespace WpfCalibrator.Models
{
    public class UserViewConfig
    {
        public string LastUsedComPort { get; set; } = "COM1";
        public int LastUsedBaudRate { get; set; } = 115200;

        // Запоминаем, какой экран MoTeC-style был открыт последним
        public string ActiveLayoutName { get; set; } = "Главный";

        // Словарь рабочих экранов. Ключ — имя экрана, значение — список виджетов на нём
        public Dictionary<string, List<SavedWidgetInfo>> Layouts { get; set; } = new();
    }

    public class SavedWidgetInfo
    {
        public string VarName { get; set; } = "";
        public string ControlView { get; set; } = "TextBox";
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; } = 100;
        public double Height { get; set; } = 30;

        // НОВОЕ: Шаг приращения значения ячейки при нажатии PageUp / PageDown
        public float IncrementStep { get; set; } = 1.0f;

        // Привязки осей Look-Up таблиц живут локально внутри описания виджета экрана
        public LutBindings TableBindings { get; set; } = new();
    }

    public class LutBindings
    {
        public bool HasBindings { get; set; } = false;
        public string AxisX_VarName { get; set; } = "";
        public string AxisY_VarName { get; set; } = "";
        public string InputX_VarName { get; set; } = "";
        public string InputY_VarName { get; set; } = "";
    }
}
