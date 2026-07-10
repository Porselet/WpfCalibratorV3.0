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

                // Сюда в будущем ты будешь добавлять новые чистые классы, например:
                // case "Digital":
                //     widget = new DigitalSensorWidgetViewModel(dataSource);
                //     break;

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
