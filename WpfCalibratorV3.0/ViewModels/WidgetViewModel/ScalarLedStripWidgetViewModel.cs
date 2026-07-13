using System;
using System.Collections.ObjectModel;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.ViewModels.WidgetViewModel
{
    public class ScalarLedStripWidgetViewModel : BaseScalarWidgetViewModel
    {
        // Массив из 10 состояний диодов, на который завязан XAML
        public ObservableCollection<bool> LedStates { get; } = new ObservableCollection<bool>();

        public ScalarLedStripWidgetViewModel(VariableViewModelBase dataSource) : base(dataSource)
        {
            // Инициализируем массив выключенными диодами
            for (int i = 0; i < 10; i++) LedStates.Add(false);

            RecalculateLedStates();
        }

        protected override void OnScalarDataChanged(string propertyName)
        {
            // Пересчитываем диоды, если изменилось значение или границы шкалы датчика
            if (propertyName == nameof(ScalarVariableViewModel.CurrentValue) ||
                propertyName == nameof(ScalarVariableViewModel.ScaleMin) ||
                propertyName == nameof(ScalarVariableViewModel.ScaleMax))
            {
                RecalculateLedStates();
            }
        }

        private void RecalculateLedStates()
        {
            if (ScalarSource == null) return;

            double min = ScalarSource.ScaleMin;
            double max = ScalarSource.ScaleMax;
            if (max <= min) return;

            // Вычисляем процент заполнения шкалы (от 0.0 до 1.0)
            double clamped = Math.Clamp(ScalarSource.CurrentValue, min, max);
            double percentage = (clamped - min) / (max - min);

            // Сколько диодов из 10 должно гореть прямо сейчас
            int ledsToLight = (int)Math.Round(percentage * 10.0);

            // Обновляем массив состояний для WPF
            for (int i = 0; i < 10; i++)
            {
                LedStates[i] = (i < ledsToLight);
            }
        }
    }
}
