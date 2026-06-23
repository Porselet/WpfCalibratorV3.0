using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Linq;

namespace WpfCalibrator.Models;

/// <summary>
/// Описание одной Simulink-модели.
/// </summary>
public class ModelConfig
{
    public string ModelName { get; set; } = "";
    public byte ModelId { get; set; }
    public List<VariableConfig> Variables { get; set; } = new();

    // ---- ЭТИ СВОЙСТВА МЫ ДОБАВИЛИ ДЛЯ ОТОБРАЖЕНИЯ В ДЕРЕВЕ ----

    // Список только параметров (IsParam = true)
    [JsonIgnore]
    public List<VariableConfig> ParameterNodes => Variables.Where(v => v.IsParam).ToList();

    // Список только сигналов телеметрии (IsParam = false)
    [JsonIgnore]
    public List<VariableConfig> TelemetryNodes => Variables.Where(v => !v.IsParam).ToList();

    // Виртуальная структура папок («Параметры» и «Сигналы»), которую прочитает TreeView слева
    [JsonIgnore]
    public IEnumerable<object> TreeCategories => new object[]
    {
        new TreeFolder { Name = "⚙️ Параметры", Items = ParameterNodes },
        new TreeFolder { Name = "📈 Сигналы (Телеметрия)", Items = TelemetryNodes }
    };
}

/// <summary>
/// Вспомогательный класс для создания виртуальных папок в дереве навигации
/// </summary>
public class TreeFolder
{
    public string Name { get; set; } = "";
    public List<VariableConfig> Items { get; set; } = new();
}
