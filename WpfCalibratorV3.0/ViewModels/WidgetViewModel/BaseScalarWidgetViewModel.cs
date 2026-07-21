
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


        private bool _enableVisualAlarm = false;
        /// <summary>
        /// Разрешает или запрещает динамическое окрашивание фоновой подложки виджета
        /// в критических зонах аларма (настраивается калибровщиком индивидуально для каждого прибора).
        /// </summary>

        public bool EnableVisualAlarm
        {
            get => _enableVisualAlarm;
            set
            {
                if (_enableVisualAlarm != value)
                {
                    _enableVisualAlarm = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}

