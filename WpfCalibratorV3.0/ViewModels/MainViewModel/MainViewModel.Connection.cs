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
        if (CommunicationService.AsInterface.IsConnected)
        {
            // Вежливо ОСТАНАВЛИВАЕМ фоновый планировщик обмена ПЕРЕД закрытием порта
            Services.BusArbiter.AsInterface.Stop();

            CommunicationService.AsInterface.Disconnect();
            ConnectionStatusText = "❌ ПРИБОР ОТКЛЮЧЕН";
            ConnectionState = DeviceConnectionState.Disconnected;
        }
        else
        {
            try
            {
                CommunicationService.AsInterface.Connect(SelectedPort, 115200);
                ConnectionStatusText = $"⚡ СВЯЗЬ УСТАНОВЛЕНА ({SelectedPort})";

                // НОВОЕ: Передаем оригинальный список переменных из Матлаба прямо в парсер приемника!
                // Замени _configManager.CurrentConfig.Variables на точное имя свойства в твоем менеджере, 
                // если оно называется по-другому (например, _configManager.Variables)
                //CommunicationService.Instance.AllVariablesConfig = _configManager.CurrentConfig.Variables;

                // 🔥 НОВОЕ: Безопасно, в UI-потоке, передаем живую карту выбранного прибора в сервис связи!
                CommunicationService.AsInterface.CurrentDeviceConfig = SelectedDevice;

                // Намертво запускаем бесконечный фоновый цикл планировщика пакетов
                Services.BusArbiter.AsInterface.Start();
                ConnectionState = DeviceConnectionState.Connected;
                _ = RefreshAllLayoutParametersAsync();
            }

            catch (Exception ex)
            {
                ConnectionStatusText = $"🛑 ОШИБКА: {ex.Message}";
            }
        }
    }


    private void OnUartPacketReceived(Models.NetworkCommand response)
    {
        if (response?.PayloadData == null || response.PayloadData.Length == 0) return;

        var targetVariable = ParameterVariables.FirstOrDefault(v => v.Id == response.VarId && v.ModelId == response.ModelId)
                          ?? TelemetryVariables.FirstOrDefault(v => v.Id == response.VarId && v.ModelId == response.ModelId);

        if (targetVariable == null) return;

        try
        {
            targetVariable.IsUpdatingFromNetwork = true;
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                targetVariable.UpdateDataFromRawPayload(response.PayloadData);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UART RX ERROR] Ошибка разбора данных: {ex.Message}");
        }
        finally
        {
            targetVariable.IsUpdatingFromNetwork = false;
        }
    }
}