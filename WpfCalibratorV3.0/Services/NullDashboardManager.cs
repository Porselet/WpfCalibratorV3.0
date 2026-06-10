using System;
using WpfCalibrator.Models;
using WpfCalibrator.ViewModels; // <=== Добавляем это

namespace WpfCalibrator.Services;

/// <summary>
/// Заглушка для тестирования, когда DashboardManager не нужен.
/// </summary>
public class NullDashboardManager : IDashboardManager
{
    public void AddWidget(WidgetViewModel widget) { }
    public void RemoveWidget(Guid widgetId) { }
    public void RestoreSavedWidgets(UserViewConfig config, DeviceConfig device) { }
}



