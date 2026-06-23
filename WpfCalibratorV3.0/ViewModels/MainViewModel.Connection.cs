using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using WpfCalibrator.Models;
using WpfCalibrator.Services;

namespace WpfCalibrator.ViewModels;

public partial class MainViewModel
{

    private string _connectionStatusText = "❌ ПРИБОР ОТКЛЮЧЕН";
    public string ConnectionStatusText
    {
        get => _connectionStatusText;
        set
        {
            _connectionStatusText = value;
            OnPropertyChanged();
        }
    }
    // 1. Логика подключения
    public void ToggleConnection()
    {
        if (CommunicationService.Instance.IsConnected)
        {
            // Вежливо ОСТАНАВЛИВАЕМ фоновый планировщик обмена ПЕРЕД закрытием порта
            Services.BusArbiter.Instance.Stop();

            CommunicationService.Instance.Disconnect();
            ConnectionStatusText = "❌ ПРИБОР ОТКЛЮЧЕН";
            ConnectionState = DeviceConnectionState.Disconnected;
        }
        else
        {
            try
            {
                CommunicationService.Instance.Connect(SelectedPort, 115200);
                ConnectionStatusText = $"⚡ СВЯЗЬ УСТАНОВЛЕНА ({SelectedPort})";

                // НОВОЕ: Передаем оригинальный список переменных из Матлаба прямо в парсер приемника!
                // Замени _configManager.CurrentConfig.Variables на точное имя свойства в твоем менеджере, 
                // если оно называется по-другому (например, _configManager.Variables)
                //CommunicationService.Instance.AllVariablesConfig = _configManager.CurrentConfig.Variables;

                // 🔥 НОВОЕ: Безопасно, в UI-потоке, передаем живую карту выбранного прибора в сервис связи!
                CommunicationService.Instance.CurrentDeviceConfig = SelectedDevice;

                // Намертво запускаем бесконечный фоновый цикл планировщика пакетов
                Services.BusArbiter.Instance.Start();
                ConnectionState = DeviceConnectionState.Connected;
                _ = RefreshAllLayoutParametersAsync();
            }

            catch (Exception ex)
            {
                ConnectionStatusText = $"🛑 ОШИБКА: {ex.Message}";
            }
        }
    }












    private void OnUartPacketReceived(NetworkCommand response)
    {
        // 1. Ищем переменную в нашей живой программе по её VarId
        var targetVariable = ParameterVariables.FirstOrDefault(v => v.Id == response.VarId)
                          ?? TelemetryVariables.FirstOrDefault(v => v.Id == response.VarId);

        if (targetVariable == null) return;

        // 2. Взводим наш флаг-щит сетевого обновления
        targetVariable.IsUpdatingFromNetwork = true;

        // ======================================================================
        // 3. РАСПРЕДЕЛЯЕМ ДАННЫЕ В ОЗУ ВЬЮМОДЕЛИ C# (СТРОГО ПОТОКОБЕЗОПАСНО!)
        // ======================================================================
        if (response.PayloadData == null || response.PayloadData.Length == 0) return;

        // Принудительно переносим обновление графики в главный UI-поток Windows!
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (response.PayloadData.Length == 1)
            {
                // Если скаляр — записываем его в свойство CurrentValue.
                // Теперь сеттер выполнится внутри UI-потока, безопасно переберет ActiveWidgets,
                // и стрелки MoTeC вместе с графиками TimePlot мгновенно полетят в космос!
                targetVariable.CurrentValue = response.PayloadData[0];
            }
            else
            {
                // Если это многомерная таблица (LUT) — взводим флаг сетевого обновления
                targetVariable.IsUpdatingFromNetwork = true;

                // Сочно заливаем массив double[] в двухмерную матрицу MatrixData
                int idx = 0;
                for (int r = 0; r < response.Rows; r++)
                {
                    for (int c = 0; c < response.Cols; c++)
                    {
                        targetVariable.MatrixData[r, c] = (float)response.PayloadData[idx++];
                    }
                }

                // Перерисовываем ячейки таблицы на экране ноутбука
                targetVariable.RebuildMatrixCells(true);

                // Опускаем щит
                targetVariable.IsUpdatingFromNetwork = false;
            }
        });

        // 4. Опускаем щит
        targetVariable.IsUpdatingFromNetwork = false;
    }

}