using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Windows.Media.Media3D;
using WpfCalibrator.Models;
using WpfCalibrator.ViewModels;
using WpfCalibrator.ViewModels.WidgetViewModel;

namespace WpfCalibrator.Services
{
    /// <summary>
    /// Боевой сервис управления приборной панелью и маршалинга макетов [1.14]
    /// </summary>
    public class DashboardManager : IDashboardManager
    {
        private readonly Dictionary<Guid, BaseWidgetViewModel> _widgets = new();

        public void AddWidget(BaseWidgetViewModel widget)
        {
            if (widget != null) _widgets[widget.Id] = widget;
        }

        public void RemoveWidget(Guid widgetId)
        {
            _widgets.Remove(widgetId);
        }

        /// <summary>
        /// Конвертер «Туда»: Снимает слепок окон холста и пакует в структуры для JSON.
        /// </summary>
        public List<SavedWidgetInfo> PackActiveWidgets(IEnumerable<BaseWidgetViewModel> activeWidgets)
        {
            var savedList = new List<SavedWidgetInfo>();
            if (activeWidgets == null) return savedList;

            foreach (var widget in activeWidgets)
            {
                if (widget.DataSource == null) continue;

                var info = new SavedWidgetInfo
                {
                    VarName = widget.DataSource.Name,
                    ControlView = widget.ControlView.ToString(),
                    Left = widget.Left,
                    Top = widget.Top,
                    Width = widget.Width,
                    Height = widget.Height,
                    EnableVisualAlarm = (widget as BaseScalarWidgetViewModel)?.EnableVisualAlarm ?? false,
                    ModelId = widget.DataSource.ModelId,
                    IsVertical = widget.IsVertical,
                };

                // Для графиков сохраняем имена сигналов
                if (widget is TimePlotWidgetViewModel tpw)
                {
                    info.Signal1Name = tpw.Signal1?.Name;
                    info.Signal2Name = tpw.Signal2?.Name;
                }

                // Для редактируемых виджетов сохраняем шаг
                if (widget is EditableWidgetViewModel editableWidget)
                {
                    info.IncrementStep = editableWidget.IncrementStep;
                }

                // Для табличных виджетов сохраняем только флаги отображения (Radar и 3D)
                if (widget is MatrixTableWidgetViewModel matrixWidget)
                {
                    info.ShowRadarTracker = matrixWidget.ShowRadarTracker;
                    info.Show3DSurface = matrixWidget.Show3DSurface;
                }

                // ❌ УДАЛЕНО:
                // - Сохранение ScaleMin, ScaleMax, MinLimit, MaxLimit (переехали в VariableSettings)
                // - Сохранение TableBindings (переехали в VariableSettings)

                savedList.Add(info);
            }

            return savedList;
        }


        /// <summary>
        /// Конвертер «Обратно»: Создает живые объекты виджетов из DTO-списка.
        /// </summary>
        public List<BaseWidgetViewModel> UnpackSavedWidgets(
            List<SavedWidgetInfo> savedWidgets,
            Func<string, VariableViewModelBase> findVariableSelector,
            UserViewConfig? userConfig = null) // Добавляем параметр для доступа к настройкам
        {
            var liveWidgets = new List<BaseWidgetViewModel>();
            if (savedWidgets == null || findVariableSelector == null) return liveWidgets;

            foreach (var info in savedWidgets)
            {
                var realVar = findVariableSelector(info.VarName);
                if (realVar == null) continue;

                WidgetViewType viewType = WidgetViewType.SingleDigitalIndicator;
                if (Enum.TryParse<WidgetViewType>(info.ControlView, out var parsedType))
                {
                    viewType = parsedType;
                }

                var newWidget = WidgetFactory.Create(viewType, realVar);
                newWidget.ControlView = viewType;
                newWidget.Title = info.VarName;
                newWidget.Left = info.Left;
                newWidget.Top = info.Top;
                newWidget.Width = info.Width;
                newWidget.Height = info.Height;
                newWidget.IsVertical = info.IsVertical;

                // Настройки, специфичные для виджета
                if (newWidget is EditableWidgetViewModel ew)
                {
                    ew.IncrementStep = info.IncrementStep;
                }

                if (newWidget is BaseScalarWidgetViewModel sw)
                {
                    sw.EnableVisualAlarm = info.EnableVisualAlarm;
                }

                // Настройки для табличного виджета (флаги отображения)
                if (newWidget is MatrixTableWidgetViewModel nw)
                {
                    nw.ShowRadarTracker = info.ShowRadarTracker;
                    nw.Show3DSurface = info.Show3DSurface;
                }

                // Восстановление сигналов для графика
                if (newWidget is TimePlotWidgetViewModel tpw)
                {
                    if (!string.IsNullOrEmpty(info.Signal1Name))
                        tpw.Signal1 = findVariableSelector(info.Signal1Name) as ScalarVariableViewModel;
                    if (!string.IsNullOrEmpty(info.Signal2Name))
                        tpw.Signal2 = findVariableSelector(info.Signal2Name) as ScalarVariableViewModel;
                }

                // ❌ УДАЛЕНО:
                // - Применение ScaleMin, ScaleMax, MinLimit, MaxLimit к скаляру
                // - Применение TableBindings к таблицам
                // (Теперь это делается в MainViewModel через ApplyUserSettings)

                liveWidgets.Add(newWidget);
                AddWidget(newWidget);
            }

            return liveWidgets;
        }


        // Также заглушим или поправим пустой метод из интерфейса
        public void RestoreSavedWidgets(UserViewConfig config, DeviceConfig device)
        {
            // Метод пустой, так как вся логика маршалинга переехала в UnpackSavedWidgets [1.14]
        }
    } // Конец класса DashboardManager
} // Конец пространства имен
