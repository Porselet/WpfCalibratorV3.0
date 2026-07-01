using System;
using System.Collections.Generic;
using WpfCalibrator.Models;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.Services;

/// <summary>
/// Контракт управления приборной панелью и маршалинга макетов виджетов [1.14]
/// </summary>
public interface IDashboardManager
{
    void AddWidget(WidgetViewModel widget);
    void RemoveWidget(Guid widgetId);

    /// <summary>
    /// Конвертер «Туда»: Сериализует живые окна холста в DTO-список для сохранения в JSON [1.14]
    /// </summary>
    List<SavedWidgetInfo> PackActiveWidgets(IEnumerable<WidgetViewModel> activeWidgets);

    /// <summary>
    /// Конвертер «Обратно»: Пересоздает живые виджеты из DTO-списка [1.14]
    /// </summary>
    List<WidgetViewModel> UnpackSavedWidgets(List<SavedWidgetInfo> savedWidgets, Func<string, VariableViewModelBase> findVariableSelector);

    void RestoreSavedWidgets(UserViewConfig config, DeviceConfig device);
}
