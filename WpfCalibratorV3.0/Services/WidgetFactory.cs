using WpfCalibrator.ViewModels;
using WpfCalibrator.ViewModels.WidgetViewModel;

namespace WpfCalibrator.Services
{
    public static class WidgetFactory
    {
        /// <summary>
        /// Универсальный диспетчер создания виджетов калибратора [1.14]
        /// </summary>
        public static BaseWidgetViewModel Create(string viewType, VariableViewModelBase dataSource)
        {
            BaseWidgetViewModel widget;

            // Честный и быстрый switch по строке типа виджета
            switch (viewType)
            {
                case "Matrix3DSurface":
                    widget = new Matrix3DWidgetViewModel(dataSource);
                    break;
                case "MatrixTable":
                    widget = new MatrixTableWidgetViewModel(dataSource);
                    break;

                case "RadarTracker":
                    widget = new RadarTrackerWidgetViewModel(dataSource);
                    break;

                // Слайдеры получают свой класс геометрии линеек
                case "SliderHorizontal":
                case "SliderVertical":
                    widget = new ScalarSliderWidgetViewModel(dataSource);
                    break;

                // Круглый будильник получает свой класс тригонометрии стрелок
                case "GaugeCircular270":
                    widget = new ScalarGaugeWidgetViewModel(dataSource);
                    break;
                // Выдаем светодиодной линейке её личный тригонометрический класс
                case "GaugeArc120":
                    widget = new ScalarLedStripWidgetViewModel(dataSource);
                    break;
                // Текстовое поле не считает ни пиксели, ни углы — ему хватает подродителя!
                case "TextBox":
                    widget = new ScalarSliderWidgetViewModel(dataSource); // можно использовать слайдерный или сделать легкий пустой класс
                    break;
                case "TimePlot":
                    widget = new TimePlotWidgetViewModel(dataSource);
                    break;

                default:
                    // Все остальные приборы (датчики, слайдеры, радары) временно улетают в Legacy
                    widget = new LegacyWidgetViewModel(dataSource);
                    break;
            }

            // Автоматически штампуем паспортное поле, чтобы не писать руками во внешнем коде!
            widget.ControlView = viewType;

            return widget;
        }
    }
}
