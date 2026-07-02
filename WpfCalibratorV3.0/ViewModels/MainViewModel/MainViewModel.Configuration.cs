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
    private VariableViewModelBase? FindVariable(string varName)
    {
        return ParameterVariables.FirstOrDefault(v => v.Name == varName)
               ?? TelemetryVariables.FirstOrDefault(v => v.Name == varName);
    }


    // ======================================================================
    // ЧАСТЬ 1: ЛАКОНИЧНОЕ СОХРАНЕНИЕ РАБОЧИХ СТОЛОВ ЧЕРЕЗ СЕРВИСЫ
    // ======================================================================
    private void SaveCurrentLayoutInternal()
    {
        if (string.IsNullOrEmpty(CurrentLayoutName) || SelectedDevice == null) return;

        // 1. Просим ConfigurationManager загрузить текущий файл с диска [1.14]
        var currentConfig = _configManager.LoadUserConfigForDevice(SelectedDevice.DevicePath);
        if (currentConfig == null) return;

        // 2. Снимаем слепок окон холста силами DashboardManager [1.14]
        currentConfig.Layouts[CurrentLayoutName] = _dashboardManager.PackActiveWidgets(ActiveWidgets);
        currentConfig.ActiveLayoutName = CurrentLayoutName;

        // 3. Пишем обновленный конфиг обратно на диск [1.14]
        _configManager.SaveUserConfig(currentConfig, SelectedDevice.DevicePath);
    }

    public void AddNewLayout(string name)
    {
        if (string.IsNullOrEmpty(name) || SelectedDevice == null) return;

        var currentConfig = _configManager.LoadUserConfigForDevice(SelectedDevice.DevicePath);
        if (currentConfig == null || currentConfig.Layouts.ContainsKey(name)) return;

        // Создаем чистую вкладку в JSON-конфиге [1.14]
        currentConfig.Layouts[name] = new List<Models.SavedWidgetInfo>();
        LayoutNames.Add(name);
        CurrentLayoutName = name;

        // Загоняем изменения на жесткий диск
        _configManager.SaveUserConfig(currentConfig, SelectedDevice.DevicePath);
    }





    // ======================================================================
    // ЧАСТЬ 2: РАСПАКОВКА И ВОССТАНОВЛЕНИЕ ОКАН НА ХОЛСТЕ СИЛАМИ СЕРВИСОВ [1.14]
    // ======================================================================
    private void SwitchToLayout(string layoutName)
    {
        if (string.IsNullOrEmpty(layoutName) || SelectedDevice == null) return;

        // 1. Очищаем холст от старых виджетов
        ActiveWidgets.Clear();
        _currentLayoutName = layoutName;

        // 2. Читаем актуальный файл конфигурации устройства с жесткого диска [1.14]
        var currentConfig = _configManager.LoadUserConfigForDevice(SelectedDevice.DevicePath);
        if (currentConfig == null || !currentConfig.Layouts.ContainsKey(layoutName)) return;

        var savedWidgets = currentConfig.Layouts[layoutName];

        // 3. Вызываем наш DashboardManager для воссоздания живых виджетов и линковки осей! [1.14]
        // Передаем делегат поиска переменных FindVariable из MainViewModel
        var liveWidgets = _dashboardManager.UnpackSavedWidgets(savedWidgets, FindVariable);

        // 4. Закидываем воссозданные приборы на холст WPF
        foreach (var widget in liveWidgets)
        {
            ActiveWidgets.Add(widget);
        }
        _ = RefreshAllLayoutParametersAsync();
    }




    public async Task RefreshAllLayoutParametersAsync()
    {

        if (!CommunicationService.AsInterface.IsConnected || SelectedDevice == null) return;

        try
        {
            await Task.Delay(200);
            // Собираем в уникальный список (HashSet) вообще все параметры, которые нужно обновить
            var parametersToUpdate = new HashSet<VariableViewModelBase>();

            foreach (var widget in ActiveWidgets.ToList())
            {
                if (widget.DataSource == null || !widget.DataSource.IsParam) continue;
                var tableVar = widget.DataSource as TableVariableViewModelBase;
                // Добавляем саму таблицу или скалярный параметр
                parametersToUpdate.Add(widget.DataSource);

                if (tableVar?.BoundAxisX != null) parametersToUpdate.Add(tableVar.BoundAxisX);

                // Для оси Y делаем дополнительную проверку на 3D карту [1.14]
                if (tableVar is Map3DVariableViewModel map3D && map3D.BoundAxisY != null)
                    parametersToUpdate.Add(map3D.BoundAxisY);

            }

            // ИСПРАВЛЕНО: Вместо прямой отправки байт вслепую, мы просто ставим задачи в очередь Арбитра!
            foreach (var param in parametersToUpdate)
            {
                // 🔥 АППАРАТНЫЙ ФИКС СГРЫЗАНИЯ ЗАГЛОВКОВ:
                // Даем МК 60 миллисекунд полностью выплюнуть предыдущую ось в провод, 
                // чтобы следующий пакет таблицы зашел в чистый и свободный DMA-стрим!
                await System.Threading.Tasks.Task.Delay(300);

                // На бумаге: Дефолтная мерность для одиночного датчика (Скаляра)
                int pollRows = 1;
                int pollCols = 1;

                // Вычисляем реальные габариты, только если датчик оказался таблицей/кривой
                if (param is TableVariableViewModelBase tableVar)
                {
                    pollCols = tableVar.Cols;
                    pollRows = (tableVar is Map3DVariableViewModel map3D) ? map3D.Rows : 1;
                }

                var readCmd = new Models.NetworkCommand
                {
                    ModelId = param.ModelId,
                    Cmd = Models.LinkCommand.VarRead, // Команда чтения
                    VarId = (byte)param.Id,
                    DataType = param.Type,
                    Rows = pollRows,
                    Cols = pollCols,
                    PayloadData = null // При чтении данные нам вернет сам STM32 в ответе
                };

                // Заталкиваем команду в приоритетную очередь калибровок Диспетчера.
                // Диспетчер сам поштучно, на максимальной скорости и с соблюдением Handshake,
                // вычитает все параметры один за другим!
                Services.BusArbiter.AsInterface.PushCommand(readCmd);

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
        // ======================================================================
        // ЧАСТЬ 3: ПОЛИМОРФНАЯ ФАБРИКА ПАРСИНГА XML-ПРОШИВКИ (OnDeviceChanged) [1.14]
        // ======================================================================
        if (SelectedDevice != null)
        {
            ParameterVariables.Clear();
            TelemetryVariables.Clear();
            LayoutNames.Clear();

            foreach (var modelPair in SelectedDevice.Models)
            {
                byte currentModelId = modelPair.Key;
                var modelConfig = modelPair.Value;

                foreach (var variable in modelConfig.Variables)
                {
                    // 🔥 Внедряем фабрику: разделяем типы данных по их физической мерности в XML! [1.14]
                    VariableViewModelBase vm;

                    if (variable.Rows == 1 && variable.Cols == 1)
                    {
                        vm = new ScalarVariableViewModel(); // Одиночная константа / живой датчик [1.14]
                    }
                    else if (variable.Rows == 1 && variable.Cols > 1)
                    {
                        vm = new CurveVariableViewModel { Rows = variable.Rows, Cols = variable.Cols }; // 1D-Кривая оцифровки [1.14]
                    }
                    else
                    {
                        vm = new Map3DVariableViewModel { Rows = variable.Rows, Cols = variable.Cols }; // Тяжелая 3D-Матрица [1.14]
                    }

                    // Накатываем общие паспортные данные в абстрактный корень [1.14]
                    vm.Id = (byte)variable.Id;
                    vm.Name = variable.Name;
                    vm.ModelId = currentModelId;
                    vm.IsParam = variable.IsParam;
                    vm.Type = variable.Type;
                    vm.ElementSize = variable.ElementSize;

                    // Распределяем по глобальным реестрам ОЗУ для навигатора
                    if (vm.IsParam)
                        ParameterVariables.Add(vm);
                    else
                        TelemetryVariables.Add(vm);
                }
            }

            // Восстанавливаем вкладки макетов из JSON для этого блока
            var currentConfig = _configManager.LoadUserConfigForDevice(SelectedDevice.DevicePath);
            if (currentConfig != null)
            {
                foreach (var name in currentConfig.Layouts.Keys) LayoutNames.Add(name);

                // Разворачиваем активный рабочий стол
                string defaultLayout = currentConfig.ActiveLayoutName;
                if (!string.IsNullOrEmpty(defaultLayout) && LayoutNames.Contains(defaultLayout))
                    CurrentLayoutName = defaultLayout;
                else
                    CurrentLayoutName = LayoutNames.FirstOrDefault() ?? "";
            }
        }
    }











}