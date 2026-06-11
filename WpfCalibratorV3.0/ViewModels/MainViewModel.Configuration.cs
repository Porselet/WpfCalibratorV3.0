using System.Collections.Generic;
using System.IO;
using WpfCalibrator.Models;

using WpfCalibrator.Models;
using System.Collections.Generic;
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
        // Полностью очищаем старый список вкладок перед загрузкой новых данных
        LayoutNames.Clear();

        // Если конфигурационный файл пустой или в нем нет ни одного сохраненного экрана
        if (config == null || config.Layouts == null || config.Layouts.Count == 0)
        {
            // Создаем чистый стартовый экран по умолчанию
            LayoutNames.Add("Главный");
            CurrentLayoutName = "Главный";
            return;
        }

        // Восстанавливаем COM-порт, если он сохранен и доступен в системе
        if (!string.IsNullOrEmpty(config.LastUsedComPort) && AvailablePorts.Contains(config.LastUsedComPort))
        {
            SelectedPort = config.LastUsedComPort;
        }

        // Наполняем коллекцию вкладок именами экранов из JSON
        foreach (var layoutName in config.Layouts.Keys)
        {
            LayoutNames.Add(layoutName);
        }

        // Проверяем, существует ли еще экран, который был открыт последним
        if (config.Layouts.ContainsKey(config.ActiveLayoutName))
        {
            CurrentLayoutName = config.ActiveLayoutName;
        }
        else
        {
            // Если имя не найдено, открываем самую первую вкладку в списке
            CurrentLayoutName = LayoutNames[0];
        }
    }




    // Вспомогательный метод для поиска переменной по имени
    private VariableViewModel? FindVariable(string varName)
    {
        return ParameterVariables.FirstOrDefault(v => v.Name == varName)
               ?? TelemetryVariables.FirstOrDefault(v => v.Name == varName);
    }


    // 1. Внутренний метод сохранения текущего состояния активного экрана
    private void SaveCurrentLayoutInternal()
    {
        if (SelectedDevice == null || string.IsNullOrEmpty(CurrentLayoutName)) return;

        // Загружаем актуальный файл конфигурации устройства с диска
        var currentConfig = _configManager.LoadUserConfigForDevice(SelectedDevice.DevicePath) ?? new UserViewConfig();

        currentConfig.LastUsedComPort = SelectedPort ?? "COM1";
        currentConfig.ActiveLayoutName = CurrentLayoutName;

        // Формируем список виджетов, открытых прямо сейчас на холсте
        var widgetsList = new List<SavedWidgetInfo>();
        foreach (var widget in ActiveWidgets)
        {
            if (widget.DataSource == null) continue;

            widgetsList.Add(new SavedWidgetInfo
            {
                VarName = widget.DataSource.Name,
                ControlView = widget.ControlView,
                Left = widget.Left,
                Top = widget.Top,
                Width = widget.Width,
                Height = widget.Height,
                // Фиксируем связи Look-Up осей локально для этой таблицы на этом экране
                TableBindings = new LutBindings
                {
                    HasBindings = widget.DataSource.IsLutLinked,
                    AxisX_VarName = widget.DataSource.BoundAxisX?.Name ?? "",
                    AxisY_VarName = widget.DataSource.BoundAxisY?.Name ?? "",
                    InputX_VarName = widget.DataSource.BoundInputX?.Name ?? "",
                    InputY_VarName = widget.DataSource.BoundInputY?.Name ?? ""
                }
            });
        }

        // Сохраняем сформированный список в словарь под именем текущей вкладки
        currentConfig.Layouts[CurrentLayoutName] = widgetsList;

        // Записываем обновленный JSON обратно на диск
        _configManager.SaveUserConfig(currentConfig, SelectedDevice.DevicePath);
    }

    // 2. Метод физического переключения экранов на холсте
    private void SwitchToLayout(string layoutName)
    {
        if (SelectedDevice == null) return;

        // Временно выключаем опрос телеметрии, чтобы безопасно перерисовать UI без гонок потоков
        bool wasPolling = _isPollingEnabled;
        _isPollingEnabled = false;

        ActiveWidgets.Clear();

        var config = _configManager.LoadUserConfigForDevice(SelectedDevice.DevicePath);
        if (config != null && config.Layouts.TryGetValue(layoutName, out var savedWidgets))
        {
            foreach (var info in savedWidgets)
            {
                var realVar = FindVariable(info.VarName);
                if (realVar == null) continue;

                // Восстанавливаем привязки Look-Up осей, если они сохранены внутри виджета
                if (info.TableBindings != null && info.TableBindings.HasBindings)
                {
                    realVar.BoundAxisX = FindVariable(info.TableBindings.AxisX_VarName);
                    realVar.BoundAxisY = FindVariable(info.TableBindings.AxisY_VarName);
                    realVar.BoundInputX = FindVariable(info.TableBindings.InputX_VarName);
                    realVar.BoundInputY = FindVariable(info.TableBindings.InputY_VarName);
                }

                var widgetVm = new WidgetViewModel
                {
                    DataSource = realVar,
                    ControlView = info.ControlView,
                    Left = info.Left,
                    Top = info.Top,
                    Width = info.Width,
                    Height = info.Height
                };

                ActiveWidgets.Add(widgetVm);

                // Если вывели на холст калибровочный параметр — принудительно вычитываем его актуальные данные из МК
                if (realVar.IsParam && _commService.IsConnected)
                {
                    _ = RequestSingleVariableReadAsync(realVar.ModelId, (byte)realVar.Id, realVar.TotalElements);
                }
            }
        }

        // Возвращаем опрос телеметрии в исходное состояние
        _isPollingEnabled = wasPolling;
    }

    // 3. Метод создания нового экрана из кода или UI
    public void AddNewLayout(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || LayoutNames.Contains(name)) return;

        LayoutNames.Add(name);
        CurrentLayoutName = name; // Наш сеттер из Шага 2 сам сделает автосейв старого экрана и откроет чистый новый
    }

    // 4. Метод удаления экрана
    public void DeleteLayout(string name)
    {
        if (LayoutNames.Count <= 1 || !LayoutNames.Contains(name)) return;

        LayoutNames.Remove(name);

        if (SelectedDevice != null)
        {
            var config = _configManager.LoadUserConfigForDevice(SelectedDevice.DevicePath);
            if (config != null && config.Layouts.Remove(name))
            {
                _configManager.SaveUserConfig(config, SelectedDevice.DevicePath);
            }
        }

        // Автоматически переводим калибровщика на первую оставшуюся вкладку
        CurrentLayoutName = LayoutNames[0];
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

            byte cmd = 0x01; // CMD_VAR_WRITE
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
    /// <summary>
    /// Единовременный принудительный запрос чтения параметра из STM32 при создании виджета
    /// </summary>
    public async Task RequestSingleVariableReadAsync(byte modelId, byte varId, int totalElements)
    {
        if (_commService == null || !_commService.IsConnected) return;

        try
        {
            // Временно притормаживаем фоновую телеметрию, чтобы освободить линию под запрос
            _isPollingEnabled = false;

            byte cmd = 0x02; // CMD_VAR_READ (код 2 строго по app_link.h)
            byte elementsCount = (byte)totalElements;
            byte[] emptyPayload = Array.Empty<byte>();

            // Выстреливаем ОДИН ОДИНОЧНЫЙ пакет запроса в STM32
            await _commService.SendPacketAsync(modelId, cmd, varId, elementsCount, emptyPayload);

            // Даем МК 20 мс на ответ по DMA, прежде чем вернуть телеметрию
            await Task.Delay(20);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Single Read Error]: {ex.Message}");
        }
        finally
        {
            // Возвращаем опрос живых датчиков телеметрии
            _isPollingEnabled = true;
        }
    }









}