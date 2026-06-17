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
        // НОВОЕ: Разрешение визуального окрашивания фона при аларме
        public bool EnableVisualAlarm { get; set; } = true;
        // ======================================================================
        // НОВОЕ: ИДЕНТИФИКАТОР МК ДЛЯ РАЗДЕЛЕНИЯ ДВУХ РАЗНЫХ МОДЕЛЕЙ (STM32)
        // ======================================================================
        public byte ModelId { get; set; } = 0;

        // НОВОЕ: Шаг приращения значения ячейки при нажатии PageUp / PageDown
        public float IncrementStep { get; set; } = 1.0f;

        // Привязки осей Look-Up таблиц живут локально внутри описания виджета экрана
        public LutBindings TableBindings { get; set; } = new();

        // НОВОЕ: Флаг вертикальной ориентации для одномерных осей и 1-LUT таблиц
        public bool IsVertical { get; set; } = false;

        // ======================================================================
        // НОВЫЕ ПОЛЯ ДЛЯ СОХРАНЕНИЯ МАСШТАБОВ ШКАЛ И АЛАРМОВ КОНКРЕТНОГО ВИДЖЕТА
        // ======================================================================
        public float ScaleMin { get; set; } = 0f;
        public float ScaleMax { get; set; } = 100f;
        public float MinLimit { get; set; } = float.NegativeInfinity;
        public float MaxLimit { get; set; } = float.PositiveInfinity;

    }

    public class LutBindings
    {
        public bool HasBindings { get; set; } = false;
        public string AxisX_VarName { get; set; } = "";
        public string AxisY_VarName { get; set; } = "";
        public string InputX_VarName { get; set; } = "";
        public string InputY_VarName { get; set; } = "";
        // НОВОЕ: Флаг необходимости вывода плавающего прицела-радара для этой таблицы
        public bool ShowRadarTracker { get; set; } = false;
    }
}
