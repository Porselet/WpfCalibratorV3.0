using WpfCalibrator.Models;
using WpfCalibrator.ViewModels.WidgetViewModel;

namespace WpfCalibrator.ViewModels.WidgetViewModel
{
    /// <summary>
    /// Фабрика для полиморфного создания экземпляров виджетов.
    /// </summary>
    public static class WidgetFactory
    {
        /// <summary>
        /// Создает конкретную вьюмодель виджета на основе перечисления типов приборов. [1.14]
        /// </summary>
        public static BaseWidgetViewModel Create(WidgetViewType viewType, VariableViewModelBase dataSource)
        {
            BaseWidgetViewModel widget;

            // 🎯 ТИПИЗИРОВАННЫЙ СВИТЧ: Больше никаких опечаток в строках!
            switch (viewType)
            {
                case WidgetViewType.Matrix3DSurface:
                    widget = new Matrix3DWidgetViewModel(dataSource);
                    break;

                case WidgetViewType.RadarTracker:
                    widget = new RadarTrackerWidgetViewModel(dataSource);
                    break;

                case WidgetViewType.SliderHorizontal:
                case WidgetViewType.SliderVertical:
                    widget = new ScalarSliderWidgetViewModel(dataSource);
                    break;

                case WidgetViewType.GaugeCircular270:
                    widget = new ScalarGaugeWidgetViewModel(dataSource);
                    break;

                case WidgetViewType.GaugeLED:
                    widget = new ScalarLedStripWidgetViewModel(dataSource);
                    break;

                case WidgetViewType.TimePlot:
                    widget = new TimePlotWidgetViewModel(dataSource);
                    break;
                case WidgetViewType.SingleParam:
                    widget = new EditableWidgetViewModel(dataSource);
                    break;
                case WidgetViewType.SingleDigitalIndicator:
                    widget = new ScalarGaugeWidgetViewModel(dataSource);
                    break;



                case WidgetViewType.MatrixTable:
                    // Когда допишем плоскую таблицу, укажем её класс, пока заглушка
                    widget = new MatrixTableWidgetViewModel(dataSource);
                    break;

                default:
                    widget = new LegacyWidgetViewModel(dataSource);
                    break;
            }

            // Записываем тип обратно в паспорт прибора
            widget.ControlView = viewType;
            return widget;
        }
    }
}
