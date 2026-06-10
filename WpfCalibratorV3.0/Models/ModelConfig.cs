using System.Collections.Generic;

namespace WpfCalibrator.Models;

/// <summary>
/// Описание одной Simulink-модели.
/// </summary>
public class ModelConfig
{
    public string ModelName { get; set; } = "";
    public byte ModelId { get; set; }
    public List<VariableConfig> Variables { get; set; } = new();
}