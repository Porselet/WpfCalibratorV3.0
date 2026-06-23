using System;
using System.Collections.Generic;
using System.Text;
using WpfCalibrator.Models;
using WpfCalibrator.ViewModels; // <=== Добавляем это

namespace WpfCalibrator.Services
{
    public interface IDashboardManager
    {
        void AddWidget(WidgetViewModel widget);
        void RemoveWidget(Guid widgetId);
        void RestoreSavedWidgets(UserViewConfig config, DeviceConfig device);
    }
}
