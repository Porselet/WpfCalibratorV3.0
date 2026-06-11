using System;
using System.Collections.Generic;
using WpfCalibrator.Models;
using WpfCalibrator.ViewModels; // <=== Добавляем это

namespace WpfCalibrator.Services;

/// <summary>
/// Сервис для управления приборной панелью (виджетами).
/// </summary>
public class DashboardManager : IDashboardManager
{
    // Коллекция виджетов (будет пополняться)
    private readonly Dictionary<Guid, WidgetViewModel> _widgets = new();

    // Метод для добавления виджета
    public void AddWidget(WidgetViewModel widget)
    {
        _widgets.Add(widget.Id, widget);
    }

    // Метод для удаления виджета
    public void RemoveWidget(Guid widgetId)
    {
        _widgets.Remove(widgetId);
    }

    // Метод для восстановления виджетов из настроек
    public void RestoreSavedWidgets(UserViewConfig config, DeviceConfig device)
    {
        // Очищаем тело метода — вся логика переехала в MainViewModel.Configuration.cs
    }
}