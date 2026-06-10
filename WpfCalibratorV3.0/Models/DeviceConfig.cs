using System.Collections.Generic;

namespace WpfCalibrator.Models;

/// <summary>
/// Описание физического устройства (например, BlackPill).
/// </summary>
public class DeviceConfig
{
    public string DeviceName { get; set; } = "";
    public string DevicePath { get; set; } = "";
    public Dictionary<byte, ModelConfig> Models { get; set; } = new();
}