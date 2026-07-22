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
        /// Конвертер «Туда»: Снимает слепок окон холста и пакует в структуры Гитхаба [1.14]
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
                if (widget is EditableWidgetViewModel editableWidget)
                {
                    info.IncrementStep = editableWidget.IncrementStep; 
                }
                // Безопасно вытягиваем лимиты из скаляра через паттерн-матчинг [1.14]
                if (widget.DataSource is ScalarVariableViewModel scalar)
                {
                    info.MinLimit = (float)scalar.AlarmMin;
                    info.MaxLimit = (float)scalar.AlarmMax;
                    info.ScaleMin = (float)scalar.ScaleMin;
                    info.ScaleMax = (float)scalar.ScaleMax;
                }

                // Запись утренних флагов графики и связей Look-Up таблиц [1.14]
                if (widget.DataSource is TableVariableViewModelBase tableVar)
                {
                    info.TableBindings = new LutBindings
                    {
                        HasBindings = true,
                        ShowRadarTracker = (widget as MatrixTableWidgetViewModel)?.ShowRadarTracker ?? false, // Наш утренний флаг [1.14]
                        Show3DSurface = (widget as MatrixTableWidgetViewModel)?.Show3DSurface ?? false,       // Наш утренний флаг [1.14]
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
        public List<BaseWidgetViewModel> UnpackSavedWidgets(List<SavedWidgetInfo> savedWidgets, Func<string, VariableViewModelBase> findVariableSelector)
        {
            var liveWidgets = new List<BaseWidgetViewModel>();
            if (savedWidgets == null || findVariableSelector == null) return liveWidgets;

            foreach (var info in savedWidgets)
            {
                // Ищем переменную в ОЗУ реестра через переданный делегат [1.14]
                var realVar = findVariableSelector(info.VarName);
                if (realVar == null) continue;
                BaseWidgetViewModel newWidget;
                WidgetViewType viewType = WidgetViewType.SingleDigitalIndicator; // Дефолтное значение

                if (Enum.TryParse<WidgetViewType>(info.ControlView, out var parsedType))
                {
                    viewType = parsedType;
                }

                newWidget = WidgetFactory.Create(viewType, realVar);
                // ... заполнение свойств ...
                newWidget.ControlView = viewType; // Теперь безопасно!
                newWidget.Title = info.VarName;
                newWidget.Left = info.Left;
                newWidget.Top = info.Top;
                newWidget.Width = info.Width;
                newWidget.Height = info.Height;

                
                newWidget.IsVertical = info.IsVertical;

                if (newWidget is EditableWidgetViewModel ew)
                {
                    ew.IncrementStep = info.IncrementStep;
                }

                if (newWidget is BaseScalarWidgetViewModel sw)
                {
                    sw.EnableVisualAlarm = info.EnableVisualAlarm;
                }
                if (newWidget is MatrixTableWidgetViewModel nw)
                {
                    nw.ShowRadarTracker = info.TableBindings?.ShowRadarTracker ?? true;
                    nw.Show3DSurface = info.TableBindings?.Show3DSurface ?? false;
                }



                // Безопасно накатываем лимиты алармов только на скаляры-датчики [1.14]
                if (realVar is ScalarVariableViewModel scalar)
                {
                    scalar.AlarmMin = info.MinLimit;
                    scalar.AlarmMax = info.MaxLimit;
                    scalar.ScaleMin = info.ScaleMin;
                    scalar.ScaleMax = info.ScaleMax;
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
