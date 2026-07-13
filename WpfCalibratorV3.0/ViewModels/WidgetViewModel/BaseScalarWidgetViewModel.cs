
using System.ComponentModel;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.ViewModels.WidgetViewModel
{
    public abstract class BaseScalarWidgetViewModel : BaseWidgetViewModel
    {
        // Прямой типизированный доступ к скалярной переменной прошивки
        public ScalarVariableViewModel ScalarSource => DataSource as ScalarVariableViewModel;

        public BaseScalarWidgetViewModel(VariableViewModelBase dataSource) : base(dataSource)
        {
            if (DataSource != null)
            {
                DataSource.PropertyChanged += OnDataSourcePropertyChanged;
            }
        }

        private void OnDataSourcePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Сигнализируем наследникам, что данные обновились, пора пересчитывать геометрию
            OnScalarDataChanged(e.PropertyName);
        }

        // Абстрактный метод-коллбэк (в Си это был бы указатель на функцию-обработчик)
        protected abstract void OnScalarDataChanged(string propertyName);
    }
}


/* using System.ComponentModel;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.ViewModels.WidgetViewModel
{
    public class BaseScalarWidgetViewModel : BaseWidgetViewModel
    {
        // Сишные расчетные координаты треугольников для шкал
        private double _minAlarmX;
        public double MinAlarmX
        {
            get => _minAlarmX;
            set { _minAlarmX = value; OnPropertyChanged(nameof(MinAlarmX)); }
        }

        private double _maxAlarmX;
        public double MaxAlarmX
        {
            get => _maxAlarmX;
            set { _maxAlarmX = value; OnPropertyChanged(nameof(MaxAlarmX)); }
        }

        private double _minAlarmY;
        public double MinAlarmY
        {
            get => _minAlarmY;
            set { _minAlarmY = value; OnPropertyChanged(nameof(MinAlarmY)); }
        }

        private double _maxAlarmY;
        public double MaxAlarmY
        {
            get => _maxAlarmY;
            set { _maxAlarmY = value; OnPropertyChanged(nameof(MaxAlarmY)); }
        }

        public BaseScalarWidgetViewModel(VariableViewModelBase dataSource) : base(dataSource)
        {
            if (DataSource != null)
            {
                DataSource.PropertyChanged += OnDataSourcePropertyChanged;
            }
            RecalculateAlarmCoordinates();
        }

        private void OnDataSourcePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Если в ScalarVariableViewModel изменились лимиты, значения или шкалы прошивки
            if (e.PropertyName == nameof(ScalarVariableViewModel.CurrentValue) ||
                e.PropertyName == "MinLimit" ||
                e.PropertyName == "MaxLimit" ||
                e.PropertyName == "ScaleMin" ||
                e.PropertyName == "ScaleMax")
            {
                RecalculateAlarmCoordinates();
            }
        }

        private void RecalculateAlarmCoordinates()
        {
            // Защита: нам нужен строго твой Scalar класс данных
            if (DataSource is not ScalarVariableViewModel scalar) return;

            double min = scalar.ScaleMin;
            double max = scalar.ScaleMax;
            if (max <= min) return;

            // --- Расчет для горизонтального слайдера (ширина шкалы 230 пикселей) ---
            double width = 230;
            MinAlarmX = ((scalar.AlarmMin - min) / (max - min)) * width - 5; // -5 центровка стрелочки
            MaxAlarmX = ((scalar.AlarmMax - min) / (max - min)) * width - 5;

            // --- Расчет для вертикального слайдера (высота шкалы 180 пикселей) ---
            // В WPF ось Y идет сверху вниз, вычитаем из высоты, чтобы шкала росла снизу вверх
            double height = 180;
            MinAlarmY = height - (((scalar.AlarmMin - min) / (max - min)) * height) - 5;
            MaxAlarmY = height - (((scalar.AlarmMax - min) / (max - min)) * height) - 5;
        }
    }
}
 */