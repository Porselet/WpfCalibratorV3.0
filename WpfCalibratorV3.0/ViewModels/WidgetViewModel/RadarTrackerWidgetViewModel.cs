using System.ComponentModel;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.ViewModels.WidgetViewModel
{
    public class RadarTrackerWidgetViewModel : BaseWidgetViewModel
    {
        // Свойства смещения перекрестия радара
        /// <summary>
        /// Физическая координата смещения моторной точки по горизонтали (ось X) на координатной сетке.
        /// Безопасно вычисляется интерполятором только в том случае, если DataSource является табличным типом.
        /// </summary>

        public double RadarGridOffsetX => (DataSource is TableVariableViewModelBase t) ? t.RadarGridOffsetX : 0;
        /// <summary>
        /// Физическая координата смещения моторной точки по горизонтали (ось Y) на координатной сетке.
        /// Безопасно вычисляется интерполятором только в том случае, если DataSource является табличным типом.
        /// </summary>

        public double RadarGridOffsetY => (DataSource is TableVariableViewModelBase t) ? t.RadarGridOffsetY : 0;


        public RadarTrackerWidgetViewModel(VariableViewModelBase dataSource) : base(dataSource)
        {
            // Подписываемся на пульс изменений данных
            if (DataSource != null)
            {
                DataSource.PropertyChanged += OnDataSourcePropertyChanged;
            }

            //RebuildRadarCoordinates();
        }

        protected override void OnDataSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // ПОТОК 2: Логика обработки таблиц (2D Радар + 3D Поверхность Helix)
            if (DataSource is TableVariableViewModelBase tableVar)
            {
                // А) Если обновились координаты смещения радара в ОЗУ — двигаем мишень
                if (e.PropertyName == "RadarGridOffsetX") OnPropertyChanged(nameof(RadarGridOffsetX));
                if (e.PropertyName == "RadarGridOffsetY") OnPropertyChanged(nameof(RadarGridOffsetY));

            }

        }


    }
}
