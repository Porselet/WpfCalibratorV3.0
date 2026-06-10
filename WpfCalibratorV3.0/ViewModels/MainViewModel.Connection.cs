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




    private void OnUartPacketReceived(byte modelId, byte cmd, byte varId, byte elementsCount, byte[] payload)
    {
        // 1. Ищем, какому прибору на холсте принадлежат эти данные (по совпадению ID и ID модели)
        // Ищем сначала в параметрах, потом в телеметрии
        var targetVariable = ParameterVariables.FirstOrDefault(v => v.Id == varId && v.ModelId == modelId)
                          ?? TelemetryVariables.FirstOrDefault(v => v.Id == varId && v.ModelId == modelId);

        // Если прилетел пакет для переменной, которой нет в текущем конфиге — игнорируем мусор
        if (targetVariable == null) return;

        // 2. БЕЗОПАСНЫЙ ПРОБРОС В UI-ПОТОК (Dispatcher). 
        // UART работает в фоновом потоке Windows. Если попытаться записать данные в UI напрямую, 
        // WPF выдаст ошибку "Поток не имеет доступа к объекту". Dispatcher решает эту проблему.
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // Если прилетел ответ на чтение телеметрии (CMD_VAR_READ = 2 по твоей прошивке app_link.h)
            if (cmd == 0x02)
            {
                if (elementsCount == 1) // Одиночный скаляр float
                {
                    // Конвертируем 4 байта обратно во float (Little Endian для STM32)
                    targetVariable.CurrentValue = BitConverter.ToSingle(payload, 0);
                }
                else // Многомерная таблица LUT
                {
                    // Заполняем твой двумерный массив MatrixData в Column-Major порядке (строго по столбцам)
                    int index = 0;
                    for (int c = 0; c < targetVariable.Cols; c++)
                    {
                        for (int r = 0; r < targetVariable.Rows; r++)
                        {
                            if (index + 4 <= payload.Length)
                            {
                                targetVariable.MatrixData[r, c] = BitConverter.ToSingle(payload, index);
                                index += 4;
                            }
                        }
                    }
                    // Перерисовываем ячейки на экране, чтобы обновить текст в таблице
                    targetVariable.RebuildMatrixCells();
                }
            }
        });
    }

}