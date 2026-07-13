using System;

namespace WpfCalibrator.ViewModels.WidgetViewModel
{
    public class ScalarGaugeWidgetViewModel : BaseScalarWidgetViewModel
    {
        private double _gaugeValueAngle = -135; public double GaugeValueAngle { get => _gaugeValueAngle; set { _gaugeValueAngle = value; OnPropertyChanged(); } }
        private double _gaugeMinAlarmAngle = -135; public double GaugeMinAlarmAngle { get => _gaugeMinAlarmAngle; set { _gaugeMinAlarmAngle = value; OnPropertyChanged(); } }
        private double _gaugeMaxAlarmAngle = 135; public double GaugeMaxAlarmAngle { get => _gaugeMaxAlarmAngle; set { _gaugeMaxAlarmAngle = value; OnPropertyChanged(); } }

        public ScalarGaugeWidgetViewModel(VariableViewModelBase dataSource) : base(dataSource)
        {
            RecalculateGaugeAngles();
        }

        protected override void OnScalarDataChanged(string propertyName)
        {
            if (propertyName == nameof(ScalarVariableViewModel.CurrentValue) || propertyName == "MinLimit" || propertyName == "MaxLimit")
            {
                RecalculateGaugeAngles();
            }
        }

        private void RecalculateGaugeAngles()
        {
            if (ScalarSource == null) return;
            double min = ScalarSource.ScaleMin;
            double max = ScalarSource.ScaleMax;
            if (max <= min) return;

            double ValueToAngle(double val) => (Math.Clamp(val, min, max) - min) / (max - min) * 270.0 - 135.0;

            GaugeValueAngle = ValueToAngle(ScalarSource.CurrentValue);
            GaugeMinAlarmAngle = ValueToAngle(ScalarSource.AlarmMin);
            GaugeMaxAlarmAngle = ValueToAngle(ScalarSource.AlarmMax);
        }
    }
}
