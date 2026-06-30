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

                // Мгновенно уведомляем графику WPF о том, что цифра обновилась
                OnPropertyChanged();
                OnPropertyChanged(nameof(ValueText));
            }
        }

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
    }
}
