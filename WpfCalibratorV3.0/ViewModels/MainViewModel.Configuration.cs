using System.Collections.Generic;
using System.IO;
using WpfCalibrator.Models;

namespace WpfCalibrator.ViewModels;

public partial class MainViewModel
{
    // Метод для загрузки конфигураций при старте приложения
    public void InitializeConfigurations()
    {
        // Сканируем папку Devices и загружаем все устройства
        var discoveredDevices = _configManager.DiscoverDevices();
        DiscoveredDevices.Clear();
        foreach (var device in discoveredDevices)
        {
            DiscoveredDevices.Add(device);
        }

        // Выбираем первое устройство по умолчанию
        SelectedDevice = DiscoveredDevices.FirstOrDefault();
    }

    // Метод для загрузки настроек интерфейса при выборе устройства
    private void ApplyUserConfig(UserViewConfig config)
    {
        // 1. Восстановим привязки осей для таблиц
        foreach (var param in ParameterVariables)
        {
            if (param.Rows > 1 && param.Cols > 1) // Только для таблиц
            {
                var lutConfig = config.VariableViews.GetValueOrDefault(param.Name);
                if (lutConfig != null && lutConfig.TableBindings != null)
                {
                    param.BoundAxisX = FindVariable(lutConfig.AxisX_VarName);
                    param.BoundAxisY = FindVariable(lutConfig.AxisY_VarName);
                    param.BoundInputX = FindVariable(lutConfig.InputX_VarName);
                    param.BoundInputY = FindVariable(lutConfig.InputY_VarName);
                }
            }
        }

        // 2. Восстановим виджеты приборной панели
        _dashboardManager.RestoreSavedWidgets(config, SelectedDevice);
    }

    // Вспомогательный метод для поиска переменной по имени
    private VariableViewModel? FindVariable(string varName)
    {
        return ParameterVariables.FirstOrDefault(v => v.Name == varName)
               ?? TelemetryVariables.FirstOrDefault(v => v.Name == varName);
    }




    private void OnDeviceChanged()
    {
        if (SelectedDevice == null) return;

        // Выбираем первую модель по умолчанию
        _selectedModelId = SelectedDevice.Models.Keys.FirstOrDefault(); 

        // Загружаем конфигурации для этого устройства
        var userConfig = _configManager.LoadUserConfigForDevice(SelectedDevice.DevicePath);

        // Очищаем существующие коллекции переменных
        ParameterVariables.Clear();
        TelemetryVariables.Clear();

        // Заполняем коллекции переменными из выбранной модели
        var selectedModel = SelectedDevice.Models[_selectedModelId];
        foreach (var variable in selectedModel.Variables)
        {
            // Получаем ID модели из ключа словаря
            byte modelId = _selectedModelId;
            var vm = new VariableViewModel(variable, modelId);
            if (variable.IsParam)
                ParameterVariables.Add(vm);
            else
                TelemetryVariables.Add(vm);
        }

        // Восстановление настроек интерфейса (привязки осей и виджеты)
        ApplyUserConfig(userConfig);
    }
}