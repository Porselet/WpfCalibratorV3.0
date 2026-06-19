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

        // 3. Распределяем данные в ОЗУ вьюмодели C#
        if (response.PayloadData.Length == 1)
        {
            // Если скаляр — просто обновляем его одиночное double-значение!
            targetVariable.CurrentValue = response.PayloadData[0];
        }
        else
        {
            // Если это многомерная таблица — сочно заливаем массив double[] в двухмерную матрицу MatrixData
            int idx = 0;
            for (int r = 0; r < response.Rows; r++)
            {
                for (int c = 0; c < response.Cols; c++)
                {
                    targetVariable.MatrixData[r, c] = (float)response.PayloadData[idx++];
                }
            }
            // Перерисовываем ячейки на экране ноутбука
            targetVariable.RebuildMatrixCells(true);
        }

        // 4. Опускаем щит
        targetVariable.IsUpdatingFromNetwork = false;
    }

}