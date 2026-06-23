using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WpfCalibrator.Models;

namespace WpfCalibrator.Services;

/// <summary>
/// Сервис для управления конфигурационными файлами.
/// </summary>
public sealed class ConfigurationManager
{
    private const string DEVICES_FOLDER = @"Devices";
    private const string USER_CONFIG_FILENAME = "user_view_config.json";

    // 1. Сканирование папки Devices
    public IEnumerable<DeviceConfig> DiscoverDevices()
    {
        var devicesDir = Path.Combine(AppContext.BaseDirectory, DEVICES_FOLDER);
        if (!Directory.Exists(devicesDir))
            Directory.CreateDirectory(devicesDir);

        var deviceFolders = Directory.GetDirectories(devicesDir);
        var devices = new List<DeviceConfig>();

        foreach (var folder in deviceFolders)
        {
            var deviceName = Path.GetFileName(folder);
            var device = new DeviceConfig
            {
                DeviceName = deviceName,
                DevicePath = folder
            };

            // Загружаем все модели для этого устройства
            var modelFiles = Directory.GetFiles(folder, "app_mapping_*.json");
            foreach (var file in modelFiles)
            {
                var jsonContent = File.ReadAllText(file);
                var modelConfig = JsonSerializer.Deserialize<ModelConfig>(jsonContent, GetJsonOptions());
                jsonContent = jsonContent.Trim();
                // ВНУТРИ МЕТОДА DiscoverDevices():
                if (modelConfig != null)
                {
                    // НОВОЕ: Намертво прописываем каждой переменной ID её родного микроконтроллера!
                    if (modelConfig.Variables != null)
                    {
                        foreach (var variable in modelConfig.Variables)
                        {
                            // Принудительно раздаем байт ModelId из заголовка считанной модели
                            // Используем рефлексию или прямое присвоение, так как у тебя там модификатор init
                            // Если компилятор ругнется на init, мы обойдем это, но сначала пробуем прямое присвоение:
                            variable.ModelId = modelConfig.ModelId;
                        }
                    }

                    device.Models[modelConfig.ModelId] = modelConfig;
                }

            }

            devices.Add(device);
        }

        return devices;
    }

    // 2. Загрузка пользовательского конфига
    public UserViewConfig LoadUserConfigForDevice(string devicePath)
    {
        var configPath = Path.Combine(devicePath, USER_CONFIG_FILENAME);
        if (!File.Exists(configPath))
            return new UserViewConfig();

        try
        {
            var jsonContent = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<UserViewConfig>(jsonContent, GetJsonOptions())
                   ?? new UserViewConfig();
        }
        catch (Exception ex)
        {
            // Логирование ошибки (можно добавить)
            return new UserViewConfig();
        }
    }

    // 3. Сохранение пользовательского конфига
    public void SaveUserConfig(UserViewConfig config, string devicePath)
    {
        var configPath = Path.Combine(devicePath, USER_CONFIG_FILENAME);
        EnsureDirectoryExists(devicePath);

        var jsonContent = JsonSerializer.Serialize(config, GetJsonOptions());
        File.WriteAllText(configPath, jsonContent);
    }

    // Вспомогательный метод для создания папки, если её нет
    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    // Настройки для сериализации JSON
    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            // Включаем регистронезависимость вместо принудительного camelCase



            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            WriteIndented = true, // Чтобы файл красиво разбивался по строкам

            // РАЗРЕШАЕМ БЕСКОНЕЧНОСТИ (Фикс ошибки System.ArgumentException):
            // Строчку со Strict мы полностью удалили, чтобы она не ломала логику!
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,

            // Позволяет сохранять русские комментарии и имена переменных в JSON в понятном текстовом виде, 
            // а не превращать их в нечитаемые байт-коды вроде \u0410\u0431
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    // Добавляем свойство для хранения последнего порта
    public string? LastUsedComPort { get; set; }

    // Метод для загрузки настроек (пример)

}