using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;

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
        if (_commService.IsConnected)
        {
            _commService.Disconnect();
            ConnectionStatusText = "❌ ПРИБОР ОТКЛЮЧЕН";
        }
        else
        {
            try
            {
                _commService.Connect(SelectedPort, 115200);
                ConnectionStatusText = $"⚡ СВЯЗЬ УСТАНОВЛЕНА ({SelectedPort})";
            }
            catch (Exception ex)
            {
                ConnectionStatusText = $"🛑 ОШИБКА: {ex.Message}";
            }
        }
    }

    // 2. Обработчик события подключения (из CommunicationService)
    private void OnPortStateChanged(bool isConnected)
    {
        // Обновляем UI
        OnPropertyChanged(nameof(ConnectionStatusText));
    }

    // 3. Обработчик приема пакета (из CommunicationService)
    private void HandlePacketReceived(byte modelId, byte cmd, byte varId, byte[] data)
    {
        // 1. Находим переменную по ID
        var variable = FindVariableById(modelId, varId);
        if (variable == null) return;

        // 2. Обновляем значение переменной
        if (variable.IsParam)
        {
            // Для параметров: десериализуем байты в значение
            variable.DeserializeFromBytes(data);
        }
        else
        {
            // Для сигналов: обновляем текущее значение (live watch)
            variable.CurrentValue = ConvertBytesToFloat(data);
        }

        // 3. Обновляем UI
        OnPropertyChanged(nameof(ParameterVariables));
        OnPropertyChanged(nameof(TelemetryVariables));
    }

    // Вспомогательный метод для поиска переменной
    private VariableViewModel? FindVariableById(byte modelId, byte varId)
    {
        // TODO: Реализуйте логику поиска переменной по ID
        // Пример:
         return ParameterVariables.FirstOrDefault(v => v.ModelId == modelId && v.Id == varId);
    }

    // Вспомогательный метод для преобразования байтов в float
    private float ConvertBytesToFloat(byte[] bytes)
    {
        // Предполагается, что данные приходят в Little Endian (стандарт для STM32)
        return BitConverter.ToSingle(bytes, 0);
    }

    // Метод, который вызывается при успешной сборке RX-пакета из UART
    private void HandleIncomingDataPacket(byte modelId, int varId, float receivedValue)
    {
        // Ищем переменную в ParameterVariables или TelemetryVariables
        var targetVariable = ParameterVariables.FirstOrDefault(v => v.Id == varId && v.ModelId == modelId)
                          ?? TelemetryVariables.FirstOrDefault(v => v.Id == varId && v.ModelId == modelId);

        if (targetVariable != null)
        {
            // Записываем значение в UI-поток (Dispatcher), чтобы WPF не ругался на мультипоточность
            App.Current.Dispatcher.Invoke(() =>
            {
                targetVariable.CurrentValue = receivedValue;
            });
        }
    }
}