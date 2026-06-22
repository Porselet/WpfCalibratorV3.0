using System.Collections.Generic;
using System.Collections.Generic;
using System.IO;
using WpfCalibrator.Models;
using WpfCalibrator.Services;

namespace WpfCalibrator.ViewModels;

using System.Linq;
using System.Threading.Tasks;


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

                // НОВОЕ: Забираем флаг вертикальной ориентации из виджета и бережно пишем в JSON!
                IsVertical = widget.IsVertical,
                // НОВОЕ: Передаем шаг изменения из виджета в структуру JSON
                IncrementStep = widget.IncrementStep,
                // Фиксируем связи Look-Up осей локально для этой таблицы на этом экране

                // НОВОЕ: Забираем масштабы и алармы из переменной виджета и пишем в JSON!
                ScaleMin = widget.DataSource?.ScaleMin ?? 0f,
                ScaleMax = widget.DataSource?.ScaleMax ?? 100f,
                MinLimit = widget.DataSource?.MinLimit ?? float.NegativeInfinity,
                MaxLimit = widget.DataSource?.MaxLimit ?? float.PositiveInfinity,
                // НОВОЕ: Забираем флаг индивидуального аларма из виджета и пишем в JSON!
                EnableVisualAlarm = widget.EnableVisualAlarm,
                ModelId = widget.DataSource?.ModelId ?? 0,
                TableBindings = new LutBindings
                {
                    HasBindings = widget.DataSource.IsLutLinked,
                    AxisX_VarName = widget.DataSource.BoundAxisX?.Name ?? "",
                    AxisY_VarName = widget.DataSource.BoundAxisY?.Name ?? "",
                    InputX_VarName = widget.DataSource.BoundInputX?.Name ?? "",
                    InputY_VarName = widget.DataSource.BoundInputY?.Name ?? "",

                    // НОВОЕ: Передаем состояние флага Радара из вьюмодели таблицы в JSON
                    ShowRadarTracker = widget.DataSource.ShowRadarTracker
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
                // ВНУТРИ ЦИКЛА МЕТОДА SwitchToLayout ЗАМЕНИ СТРОКУ ПОИСКА ПЕРЕМЕННОЙ:
                // Ищем переменную, у которой совпадает и Имя, и Идентификатор МК!
                // ВНУТРИ ЦИКЛА МЕТОДА SwitchToLayout:
                // Используем твой родной рабочий метод поиска по имени
                var realVar = FindVariable(info.VarName);

                // Дополнительная проверка безопасности: если переменная нашлась, 
                // но её ModelId не совпадает с дисковым (параметр от другой платы) — пропускаем её
                if (realVar != null && realVar.ModelId != info.ModelId)
                {
                    // Пытаемся найти её в общей структуре (если FindVariable искал только в активном устройстве)
                    // Но для стабильности пока просто страхуемся, чтобы не перепутать платы
                    realVar = null;
                }



                if (realVar == null) continue;
                // НОВОЕ: Восстанавливаем сохраненные масштабы шкал и алармы из JSON прямо в переменную!
                realVar.ScaleMin = info.ScaleMin;
                realVar.ScaleMax = info.ScaleMax;
                realVar.MinLimit = info.MinLimit;
                realVar.MaxLimit = info.MaxLimit;

                // ТИХАЯ ЗАЩИТА ОТ ДУБЛИКАТОВ (Оставляем для таблиц и скаляров)
                if (info.ControlView != "RadarTracker" && realVar.IsParam &&
                    ActiveWidgets.Any(w => w.DataSource != null && w.DataSource.Name == realVar.Name && w.ControlView != "RadarTracker"))
                {
                    continue;
                }

                // Создаем виджет, восстанавливая его ГЕОМЕТРИЮ (Left, Top, Width, Height) прямо из JSON на диске!
                var widgetVm = new WidgetViewModel
                {
                    DataSource = realVar,
                    ControlView = info.ControlView,
                    Left = info.Left,
                    Top = info.Top,
                    Width = info.Width,
                    Height = info.Height,
                    IncrementStep = info.IncrementStep,
                    // НОВОЕ: Достаем флаг вертикальной ориентации из JSON обратно в ОЗУ виджета!
                    IsVertical = info.IsVertical,

                    // НОВОЕ: Восстанавливаем флаг разрешения визуального аларма из JSON!
                    EnableVisualAlarm = info.EnableVisualAlarm


                };

                // Восстанавливаем привязки Look-Up осей
                if (info.TableBindings != null && info.TableBindings.HasBindings)
                {
                    realVar.BoundAxisX = FindVariable(info.TableBindings.AxisX_VarName);
                    realVar.BoundAxisY = FindVariable(info.TableBindings.AxisY_VarName);
                    realVar.BoundInputX = FindVariable(info.TableBindings.InputX_VarName);
                    realVar.BoundInputY = FindVariable(info.TableBindings.InputY_VarName);
                    realVar.ShowRadarTracker = info.TableBindings.ShowRadarTracker;
                }

                ActiveWidgets.Add(widgetVm);

                // СРАЗУ ПОСЛЕ СТРОКИ: ActiveWidgets.Add(widgetVm);

                // АВТО-РОЖДЕНИЕ ПРИЦЕЛА ПРИ СТАРТЕ: 
                // Если загруженный виджет — это MatrixTable, и у его таблицы сохранен флаг Радара
/*                if (info.ControlView == "MatrixTable" && realVar.ShowRadarTracker)
                {
                    // Проверяем на всякий случай, не создали ли мы его уже
                    var existingRadar = ActiveWidgets.FirstOrDefault(w =>
                        w.ControlView == "RadarTracker" && w.DataSource?.Name == realVar.Name);

                    if (existingRadar == null)
                    {
                        var radarWidget = new WidgetViewModel
                        {
                            DataSource = realVar, // Связываем с данными этой же таблицы
                            ControlView = "RadarTracker",
                            Left = widgetVm.Left + widgetVm.Width + 20, // Ставим аккуратно справа
                            Top = widgetVm.Top,
                            Width = 220, // Компактные стартовые размеры (Viewbox сам смасштабирует!)
                            Height = 220,
                            IncrementStep = widgetVm.IncrementStep
                        };

                        // Добавляем радар на холст прямо в процессе загрузки экрана!
                        ActiveWidgets.Add(radarWidget);
                    }
                }
*/
                // Если вывели на холст калибровочный параметр — принудительно вычитываем его актуальные данные из МК
                if (realVar.IsParam && CommunicationService.Instance.IsConnected)
                {
                    _ = RefreshAllLayoutParametersAsync();
                }
            }
        }

        // Возвращаем опрос телеметрии в исходное состояние
        _isPollingEnabled = wasPolling;
    }




    public async Task RefreshAllLayoutParametersAsync()
    {

        if (!CommunicationService.Instance.IsConnected || SelectedDevice == null) return;

        try
        {
            await Task.Delay(800);
            // Собираем в уникальный список (HashSet) вообще все параметры, которые нужно обновить
            var parametersToUpdate = new HashSet<VariableViewModel>();

            foreach (var widget in ActiveWidgets.ToList())
            {
                if (widget.DataSource == null || !widget.DataSource.IsParam) continue;

                // Добавляем саму таблицу или скалярный параметр
                parametersToUpdate.Add(widget.DataSource);

                // If это LUT-таблица, добавляем её оси в очередь на чтение
                if (widget.DataSource.BoundAxisX != null) parametersToUpdate.Add(widget.DataSource.BoundAxisX);
                if (widget.DataSource.BoundAxisY != null) parametersToUpdate.Add(widget.DataSource.BoundAxisY);
            }

            // ИСПРАВЛЕНО: Вместо прямой отправки байт вслепую, мы просто ставим задачи в очередь Арбитра!
            foreach (var param in parametersToUpdate)
            {
                // 🔥 АППАРАТНЫЙ ФИКС СГРЫЗАНИЯ ЗАГЛОВКОВ:
                // Даем МК 60 миллисекунд полностью выплюнуть предыдущую ось в провод, 
                // чтобы следующий пакет таблицы зашел в чистый и свободный DMA-стрим!
                await System.Threading.Tasks.Task.Delay(300);

                var readCmd = new Models.NetworkCommand
                {
                    ModelId = param.ModelId,
                    Cmd = Models.LinkCommand.VarRead, // Команда чтения
                    VarId = (byte)param.Id,
                    DataType = param.Type,
                    Rows = param.Rows,
                    Cols = param.Cols,
                    PayloadData = null // При чтении данные нам вернет сам STM32 в ответе
                };

                // Заталкиваем команду в приоритетную очередь калибровок Диспетчера.
                // Диспетчер сам поштучно, на максимальной скорости и с соблюдением Handshake,
                // вычитает все параметры один за другим!
                Services.BusArbiter.Instance.PushCommand(readCmd);

                // 🔥 УЛЬТИМАТИВНЫЙ ФИКС "МЕДЛЕННОГО" МК:
                // Делаем асингенную паузу в 80 миллисекунд МЕЖДУ запросами параметров при старте!
                // Это гарантированно уберет гонку пакетов на шине, даст DMA в STM32 абсолютное время 
                // полностью вытолкнуть предыдущую ось в провод, и блокирующий замок while в Си 
                // для нашей большой таблицы пролетит вообще без единой микросекунды задержки!
                await System.Threading.Tasks.Task.Delay(80);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Ошибка постановки параметров в очередь: {ex.Message}");
        }

        // Так как этот метод теперь работает мгновенно (просто закидывает задачи в ОЗУ-очередь),
        // мы возвращаем пустой Task.CompletedTask, чтобы не ломать асинхронную сигнатуру Task.
        await Task.CompletedTask;
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












}