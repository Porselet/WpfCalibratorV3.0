using System;

namespace WpfCalibrator.ViewModels
{
    /// <summary>
    /// Модель одиночного параметра (Скаляра / Датчика телеметрии)
    /// </summary>
    public class ScalarVariableViewModel : VariableViewModelBase
    {
        private double _currentValue;

        /// <summary>
        /// Текущее живое физическое значение параметра
        /// </summary>
        public double CurrentValue
        {
            get => _currentValue;
            set
            {
                // Если значение не изменилось — выходим, бережем такты процессора
                if (Math.Abs(_currentValue - value) < 0.0001) return;

                _currentValue = value;
                CheckAlarmStatus();
                // Мгновенно уведомляем графику WPF о том, что цифра обновилась
                OnPropertyChanged();
                OnPropertyChanged(nameof(ValueText));
            }
        }


        public double MinLimit { get; set; } // + OnPropertyChanged с обновлением SliderTicks
        public double MaxLimit { get; set; } // + OnPropertyChanged с обновлением SliderTicks
        public System.Windows.Media.DoubleCollection SliderTicks { get; set; }


        /// <summary>
        /// Форматированная строка для вывода в TextBox или цифровые индикаторы
        /// </summary>
        public string ValueText => CurrentValue.ToString("F2");

        /// <summary>
        /// Реализация нативного разбора бинарного пакета для скаляра
        /// </summary>
        public override void UpdateDataFromRawPayload(double[] rawData)
        {
            // Жесткая защита от бинарного мусора в UART: скаляр обязан содержать минимум 1 элемент
            if (rawData == null || rawData.Length == 0) return;

            // Забираем самое первое число из прилетевшего массива
            CurrentValue = rawData[0];
        }

        public override void AdjustValue(double step)
        {
            if (!IsParam) return;

            // Складываем напрямую. Если step отрицательный, число само уменьшится!
            double newValue = CurrentValue + step;

            // Универсальная защита: проверяем границы по лимитам шкалы
            if (newValue > ScaleMax) newValue = ScaleMax;
            if (newValue < ScaleMin) newValue = ScaleMin;

            CurrentValue = newValue;
        }


        private bool _isAlarmActive;
        public bool IsAlarmActive
        {
            get => _isAlarmActive;
            set { if (_isAlarmActive != value) { _isAlarmActive = value; OnPropertyChanged(); } }
        }

        // ВАЖНО: вызываем этот метод в сеттере CurrentValue!
        private void CheckAlarmStatus()
        {
            if (IsParam) { IsAlarmActive = false; return; }
            IsAlarmActive = CurrentValue < MinLimit || CurrentValue > MaxLimit;
        }


        public override void CommitEditedValue(double parsedValue)
        {
            if (!IsParam) return;

            double finalValue = parsedValue;
            if (finalValue > ScaleMax) finalValue = ScaleMax;
            if (finalValue < ScaleMin) finalValue = ScaleMin;

            CurrentValue = finalValue;

            // 🔥 Тут в будущем будет одиночный выстрел команды VarWrite в UART!
        }



    }
}
