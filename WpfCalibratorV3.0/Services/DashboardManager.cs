using System;
using System.Collections.Generic;
using System.Linq;
using WpfCalibrator.Models;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.Services
{
    /// <summary>
    /// Боевой сервис управления приборной панелью и маршалинга макетов [1.14]
    /// </summary>
    public class DashboardManager : IDashboardManager
    {
        private readonly Dictionary<Guid, WidgetViewModel> _widgets = new();

        public void AddWidget(WidgetViewModel widget)
        {
            if (widget != null) _widgets[widget.Id] = widget;
        }

        public void RemoveWidget(Guid widgetId)
        {
            _widgets.Remove(widgetId);
        }

        /// <summary>
        /// Конвертер «Туда»: Снимает слепок окон холста и пакует в структуры Гитхаба [1.14]
        /// </summary>
        public List<SavedWidgetInfo> PackActiveWidgets(IEnumerable<WidgetViewModel> activeWidgets)
        {
            var savedList = new List<SavedWidgetInfo>();
            if (activeWidgets == null) return savedList;

            foreach (var widget in activeWidgets)
            {
                if (widget.DataSource == null) continue;

                var info = new SavedWidgetInfo
                {
                    VarName = widget.DataSource.Name,
                    ControlView = widget.ControlView,
                    Left = widget.Left,
                    Top = widget.Top,
                    Width = widget.Width,
                    Height = widget.Height,
                    EnableVisualAlarm = widget.EnableVisualAlarm,
                    ModelId = widget.DataSource.ModelId,
                    IncrementStep = widget.IncrementStep,
                    IsVertical = widget.IsVertical,
                    ScaleMin = (float)widget.DataSource.ScaleMin,
                    ScaleMax = (float)widget.DataSource.ScaleMax
                };

                // Безопасно вытягиваем лимиты из скаляра через паттерн-матчинг [1.14]
                if (widget.DataSource is ScalarVariableViewModel scalar)
                {
                    info.MinLimit = (float)scalar.MinLimit;
                    info.MaxLimit = (float)scalar.MaxLimit;
                }

                // Запись утренних флагов графики и связей Look-Up таблиц [1.14]
                if (widget.DataSource is TableVariableViewModelBase tableVar)
                {
                    info.TableBindings = new LutBindings
                    {
                        HasBindings = true,
                        ShowRadarTracker = widget.ShowRadarTracker, // Наш утренний флаг [1.14]
                        Show3DSurface = widget.Show3DSurface,       // Наш утренний флаг [1.14]
                        AxisX_VarName = tableVar.BoundAxisX?.Name ?? "",
                        InputX_VarName = tableVar.BoundInputX?.Name ?? ""
                    };

                    if (tableVar is Map3DVariableViewModel map3D)
                    {
                        info.TableBindings.AxisY_VarName = map3D.BoundAxisY?.Name ?? "";
                        info.TableBindings.InputY_VarName = map3D.BoundInputY?.Name ?? "";
                    }
                }

                savedList.Add(info);
            }

            return savedList;
        }
        /// <summary>
        /// Конвертер «Обратно»: Создает живые объекты виджетов из DTO-списка [1.14].
        /// </summary>
        public List<WidgetViewModel> UnpackSavedWidgets(List<SavedWidgetInfo> savedWidgets, Func<string, VariableViewModelBase> findVariableSelector)
        {
            var liveWidgets = new List<WidgetViewModel>();
            if (savedWidgets == null || findVariableSelector == null) return liveWidgets;

            foreach (var info in savedWidgets)
            {
                // Ищем переменную в ОЗУ реестра через переданный делегат [1.14]
                var realVar = findVariableSelector(info.VarName);
                if (realVar == null) continue;

                // Создаем чистый прибор и восстанавливаем его геометрический паспорт
                var newWidget = new WidgetViewModel(realVar)
                {
                    Left = info.Left,
                    Top = info.Top,
                    Width = info.Width,
                    Height = info.Height,
                    ControlView = info.ControlView,
                    IncrementStep = info.IncrementStep,
                    IsVertical = info.IsVertical,
                    EnableVisualAlarm = info.EnableVisualAlarm,
                    ShowRadarTracker = info.TableBindings?.ShowRadarTracker ?? true, // Наш утренний флаг [1.14]
                    Show3DSurface = info.TableBindings?.Show3DSurface ?? false       // Наш утренний флаг [1.14]
                };

                // Восстанавливаем физические масштабы шкал
                realVar.ScaleMin = info.ScaleMin;
                realVar.ScaleMax = info.ScaleMax;

                // Безопасно накатываем лимиты алармов только на скаляры-датчики [1.14]
                if (realVar is ScalarVariableViewModel scalar)
                {
                    scalar.MinLimit = info.MinLimit;
                    scalar.MaxLimit = info.MaxLimit;
                }

                // Полиморфно линкуем оцифрованные оси шкал (только для табличной базы) [1.14]
                if (realVar is TableVariableViewModelBase tableVar && info.TableBindings != null)
                {
                    if (!string.IsNullOrEmpty(info.TableBindings.AxisX_VarName))
                        tableVar.BoundAxisX = findVariableSelector(info.TableBindings.AxisX_VarName) as CurveVariableViewModel;

                    if (!string.IsNullOrEmpty(info.TableBindings.InputX_VarName))
                        tableVar.BoundInputX = findVariableSelector(info.TableBindings.InputX_VarName) as ScalarVariableViewModel;

                    // Эксклюзивная привязка вертикальной оси Y для 3D матриц [1.14]
                    if (tableVar is Map3DVariableViewModel map3D)
                    {
                        if (!string.IsNullOrEmpty(info.TableBindings.AxisY_VarName))
                            map3D.BoundAxisY = findVariableSelector(info.TableBindings.AxisY_VarName) as CurveVariableViewModel;

                        if (!string.IsNullOrEmpty(info.TableBindings.InputY_VarName))
                            map3D.BoundInputY = findVariableSelector(info.TableBindings.InputY_VarName) as ScalarVariableViewModel;
                    }
                }

                liveWidgets.Add(newWidget);
                AddWidget(newWidget); // Регистрируем в локальном словаре менеджера
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
