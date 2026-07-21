using System;
using System.Collections.ObjectModel;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.ViewModels.WidgetViewModel
{
    public class ScalarSliderWidgetViewModel : BaseScalarWidgetViewModel
    {
        private double _minAlarmX; public double MinAlarmX { get => _minAlarmX; set { _minAlarmX = value; OnPropertyChanged(); } }
        private double _maxAlarmX; public double MaxAlarmX { get => _maxAlarmX; set { _maxAlarmX = value; OnPropertyChanged(); } }
        private double _minAlarmY; public double MinAlarmY { get => _minAlarmY; set { _minAlarmY = value; OnPropertyChanged(); } }
        private double _maxAlarmY; public double MaxAlarmY { get => _maxAlarmY; set { _maxAlarmY = value; OnPropertyChanged(); } }

        public ScalarSliderWidgetViewModel(VariableViewModelBase dataSource) : base(dataSource)
        {
            RecalculateSliderPixels();
        }

        protected override void OnScalarDataChanged(string propertyName)
        {
            if (propertyName == "MinLimit" || propertyName == "MaxLimit" || propertyName == "ScaleMin" || propertyName == "ScaleMax")
            {
                RecalculateSliderPixels();
            }
        }

        private void RecalculateSliderPixels()
        {
            if (ScalarSource == null) return;
            double min = ScalarSource.ScaleMin;
            double max = ScalarSource.ScaleMax;
            if (max <= min) return;

            MinAlarmX = ((ScalarSource.AlarmMin - min) / (max - min)) * 230 - 5;
            MaxAlarmX = ((ScalarSource.AlarmMax - min) / (max - min)) * 230 - 5;
            MinAlarmY = 180 - (((ScalarSource.AlarmMin - min) / (max - min)) * 180) - 5;
            MaxAlarmY = 180 - (((ScalarSource.AlarmMax - min) / (max - min)) * 180) - 5;
        }
    }
}
