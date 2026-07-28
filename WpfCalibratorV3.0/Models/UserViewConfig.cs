using System.Collections.Generic;

namespace WpfCalibrator.Models
{
    /// <summary>
    /// Корневой объект пользовательского конфигурационного файла (user_view_config.json).
    /// </summary>
    public class UserViewConfig
    {
        public string LastUsedComPort { get; set; } = "COM1";
        public int LastUsedBaudRate { get; set; } = 115200;
        public string ActiveLayoutName { get; set; } = "Главный";

        /// <summary>
        /// Настройки для каждой переменной (ключ — имя переменной).
        /// Здесь хранятся: шкалы, алармы, привязки осей (LutBindings).
        /// </summary>
        public Dictionary<string, VariableDisplaySettings> VariableSettings { get; set; } = new();

        /// <summary>
        /// Словарь рабочих экранов (макетов). 
        /// Ключ — имя экрана, значение — список виджетов на нём.
        /// </summary>
        public Dictionary<string, List<SavedWidgetInfo>> Layouts { get; set; } = new();
    }

    /// <summary>
    /// Настройки отображения для конкретной переменной.
    /// Все свойства nullable, чтобы в JSON сериализовать только те, которые были изменены.
    /// </summary>
    public class VariableDisplaySettings
    {
        // ======================================================================
        // 1. НАСТРОЙКИ ДЛЯ СКАЛЯРА (ScalarVariableViewModel)
        // ======================================================================
        public float? ScaleMin { get; set; }
        public float? ScaleMax { get; set; }
        public float? AlarmMin { get; set; }
        public float? AlarmMax { get; set; }

        // ======================================================================
        // 2. ПРИВЯЗКИ ДЛЯ ТАБЛИЦ (TableVariableViewModelBase)
        //    Перенесены из LutBindings, так как это свойства переменной, а не виджета.
        // ======================================================================
        public LutBindings TableBindings { get; set; } 
    }

    /// <summary>
    /// Привязки осей и входных сигналов для табличных переменных.
    /// </summary>
    public class LutBindings
    {
        public bool HasBindings { get; set; } = false;
        public string AxisX_VarName { get; set; } = "";
        public string AxisY_VarName { get; set; } = "";
        public string InputX_VarName { get; set; } = "";
        public string InputY_VarName { get; set; } = "";
    }

    /// <summary>
    /// Информация о сохранённом виджете на холсте.
    /// Содержит только геометрию и настройки, уникальные для данного экземпляра виджета.
    /// </summary>
    public class SavedWidgetInfo
    {
        // ======================================================================
        // 1. ИДЕНТИФИКАЦИЯ
        // ======================================================================
        public string VarName { get; set; } = "";
        public string ControlView { get; set; } = "TextBox";
        public byte ModelId { get; set; } = 0;

        // ======================================================================
        // 2. ГЕОМЕТРИЯ НА ХОЛСТЕ
        // ======================================================================
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; } = 100;
        public double Height { get; set; } = 30;

        // ======================================================================
        // 3. НАСТРОЙКИ, СПЕЦИФИЧНЫЕ ДЛЯ ЭКЗЕМПЛЯРА ВИДЖЕТА
        // ======================================================================
        public bool IsVertical { get; set; } = false;
        public bool EnableVisualAlarm { get; set; } = true;
        public float IncrementStep { get; set; } = 1.0f;

        // ======================================================================
        // 4. СВЯЗИ С ДРУГИМИ ПЕРЕМЕННЫМИ (ДЛЯ ГРАФИКОВ)
        //    Хранятся как имена, а не объекты, чтобы корректно сериализоваться.
        // ======================================================================
        public string? Signal1Name { get; set; }  // Для TimePlot (канал 1)
        public string? Signal2Name { get; set; }  // Для TimePlot (канал 2)

        // Свойства для виджета таблицы
        public bool ShowRadarTracker { get; set; } = false;
        public bool Show3DSurface { get; set; } = false;

        public double DurationSeconds { get; set; } = 10.0;
    }
}