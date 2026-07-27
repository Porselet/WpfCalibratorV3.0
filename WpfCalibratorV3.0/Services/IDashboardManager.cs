using System;
using System.Collections.Generic;
using WpfCalibrator.Models;
using WpfCalibrator.ViewModels;
using WpfCalibrator.ViewModels.WidgetViewModel;

namespace WpfCalibrator.Services;

/// <summary>
/// Контракт управления приборной панелью и маршалинга макетов виджетов [1.14]
/// </summary>
public interface IDashboardManager
{
    void AddWidget(BaseWidgetViewModel widget);
    void RemoveWidget(Guid widgetId);

    /// <summary>
    /// Конвертер «Туда»: Сериализует живые окна холста в DTO-список для сохранения в JSON [1.14]
    /// </summary>
    List<SavedWidgetInfo> PackActiveWidgets(IEnumerable<BaseWidgetViewModel> activeWidgets);

    /// <summary>
    /// Конвертер «Обратно»: Пересоздает живые виджеты из DTO-списка [1.14]
    /// </summary>
    /// <param name="savedWidgets">Список сохранённых виджетов</param>
    /// <param name="findVariableSelector">Делегат для поиска переменной по имени</param>
    /// <param name="userConfig">Пользовательская конфигурация (обязательный параметр)</param>
    List<BaseWidgetViewModel> UnpackSavedWidgets(
        List<SavedWidgetInfo> savedWidgets,
        Func<string, VariableViewModelBase> findVariableSelector,
        UserViewConfig userConfig); // <-- Обязательный параметр

    void RestoreSavedWidgets(UserViewConfig config, DeviceConfig device);
}