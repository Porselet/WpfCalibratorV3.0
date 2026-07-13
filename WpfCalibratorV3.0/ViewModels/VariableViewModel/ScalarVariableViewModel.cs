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


        /// <summary> 1. АБСОЛЮТНЫЕ ФИЗИЧЕСКИЕ ГРАНИЦЫ ДАТЧИКА (Края шкалы слайдера) </summary>
        private double _scaleMin = 0, _scaleMax = 100;
        public double ScaleMin { get => _scaleMin; set { if (Math.Abs(_scaleMin - value) > 0.001) { _scaleMin = value; OnPropertyChanged(); OnPropertyChanged("MinAlarmPercent"); } } }
        public double ScaleMax { get => _scaleMax; set { if (Math.Abs(_scaleMax - value) > 0.001) { _scaleMax = value; OnPropertyChanged(); } } }

        /// <summary> 2. АВАРИЙНЫЕ ГРАНИЦЫ (Приводит к AlarmStatus.Danger и двигает треугольники) </summary>
        public double AlarmMin { get; set; } = 1000;
        public double AlarmMax { get; set; } = 6200;

        /// <summary> 3. ЦЕЛЕВОЙ / РАБОЧИЙ ДИАПАЗОН ("Зеленая зона" нормы на приборе) </summary>
        public double TargetMin { get; set; } = 2000;
        public double TargetMax { get; set; } = 5000;


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
            IsAlarmActive = CurrentValue < AlarmMin || CurrentValue > AlarmMax;
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
