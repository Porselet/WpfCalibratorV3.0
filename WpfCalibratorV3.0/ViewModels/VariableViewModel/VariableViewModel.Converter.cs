using System;

namespace WpfCalibrator.ViewModels;

public partial class VariableViewModel
{


    private double _currentValue = 0f;
    public double CurrentValue
    {
        get => _currentValue;
        set
        {
            if (_currentValue != value)
            {
                _currentValue = value;
                OnPropertyChanged(nameof(CurrentValue));
                CheckAlarmStatus();
                OnPropertyChanged(nameof(LedStates));
                if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
                {
                    // ИСПРАВЛЕНО: Находим ВСЕ виджеты на холсте, которые отображают этот датчик!
                    var targetWidgets = mainVm.ActiveWidgets.Where(w => w.DataSource == this).ToList();

                    foreach (var widget in targetWidgets)
                    {
                        // Пинаем живую стрелку прибора
                        widget.NotifyValueAngleChanged();

                        // Заодно пинаем треугольники алармов, чтобы они тоже плавно реагировали
                        widget.RefreshAlarmTriangles();

                        // НОВОЕ: Добавляем текущее значение в ползущий осциллограф графика!
                        if (widget.ControlView == "TimePlot")
                        {
                            widget.AppendPlotPoint(_currentValue);
                        }

                    }

                    // 2. АВТО-ОТПРАВКА СКАЛЯРА В UART (Для калибровочных констант)
                    // ИСПРАВЛЕНО: Пакет летит в шину только если порт открыт И это НЕ фоновое обновление из сети!
                    // 2. АВТО-ОТПРАВКА СКАЛЯРА В UART (Для калибровочных констант)
                    // ИСПРАВЛЕНО: Пакет летит в шину ТОЛЬКО если это ручной ввод (НЕ обновление из сети!)
                    if (IsParam && !IsUpdatingFromNetwork)
                    {
                        //_ = mainVm.SendTableToUartAsync(this);
                    }

                }
            }
        }
    }

    // Флаг-предохранитель: true означает, что свойство прямо сейчас заполняется данными из UART
    private bool _isUpdatingFromNetwork = false;


    // Сериализация данных в байтовый массив (для отправки на устройство)

    // Десериализация байтового массива в значение (для приема с устройства)
    public void DeserializeFromBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length < TotalBytes)
            return;

        if (TotalElements == 1)
        {
            // Для скаляров
            CurrentValue = BitConverter.ToSingle(bytes, 0);
        }
        else
        {
            // Для матриц: раскладываем байты в MatrixData (Column-Major)
            int index = 0;
            for (int c = 0; c < Cols; c++)
            {
                for (int r = 0; r < Rows; r++)
                {
                    MatrixData[r, c] = BitConverter.ToSingle(bytes, index * ElementSize);
                    index++;
                }
            }
        }
    }


    private bool _isAlarmActive = false;
    /// <summary>
    /// Флаг активной тревоги (true, если живой сигнал вышел за критические лимиты Мин/Макс)
    /// </summary>
    public bool IsAlarmActive
    {
        get => _isAlarmActive;
        set { if (_isAlarmActive != value) { _isAlarmActive = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Математическая верификация текущего сигнала на безопасность
    /// </summary>
    private void CheckAlarmStatus()
    {
        // Если это калибровочный параметр (константа или таблица), алармы на него не действуют
        if (IsParam)
        {
            IsAlarmActive = false;
            return;
        }

        // Проверяем, вылетело ли значение за наши рамки (учитывая бесконечности)
        bool isLow = _currentValue < MinLimit;
        bool isHigh = _currentValue > MaxLimit;

        IsAlarmActive = isLow || isHigh;
    }



}