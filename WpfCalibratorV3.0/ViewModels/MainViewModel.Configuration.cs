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


    /// <summary>
    /// Универсальный метод отправки калибровочной таблицы из MainViewModel
    /// </summary>
    public async Task SendTableToUartAsync(VariableViewModel variable)
    {
        if (variable == null || _commService == null || !_commService.IsConnected) return;

        try
        {
            // 1. Ставим фоновый опрос телеметрии на паузу (теперь доступ прямой!)
            _isPollingEnabled = false;

            byte cmd = 0x02; // CMD_VAR_WRITE
            byte modelId = variable.ModelId;
            byte varId = (byte)variable.Id;

            int rCount = variable.Rows;
            int cCount = variable.Cols;
            int totalElements = rCount * cCount;

            // 2. Проверяем тип данных и вызываем твой новый обобщенный метод из CommunicationService
            if (variable.Type == "single") // float
            {
                float[] flatArray = new float[totalElements];
                int index = 0;
                for (int c = 0; c < cCount; c++)
                    for (int r = 0; r < rCount; r++)
                        flatArray[index++] = (float)variable.MatrixData[r, c];

                await _commService.SendPacketAsync(modelId, cmd, varId, flatArray);
            }
            else if (variable.Type == "int16" || variable.Type == "int16_t") // short / int16_t
            {
                short[] flatArray = new short[totalElements];
                int index = 0;
                for (int c = 0; c < cCount; c++)
                    for (int r = 0; r < rCount; r++)
                        flatArray[index++] = (short)variable.MatrixData[r, c];

                await _commService.SendPacketAsync(modelId, cmd, varId, flatArray);
            }
            else if (variable.Type == "int32" || variable.Type == "int32_t") // int / int32_t
            {
                int[] flatArray = new int[totalElements];
                int index = 0;
                for (int c = 0; c < cCount; c++)
                    for (int r = 0; r < rCount; r++)
                        flatArray[index++] = (int)variable.MatrixData[r, c];

                await _commService.SendPacketAsync(modelId, cmd, varId, flatArray);
            }

            // 3. Даем STM32 фору в 50 мс на обработку DMA IDLE и memcpy
            await Task.Delay(50);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка отправки матрицы: {ex.Message}");
        }
        finally
        {
            // 4. Снимаем паузу и возвращаем фоновый опрос сигналов
            _isPollingEnabled = true;
        }
    }
}