using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using WpfCalibrator.Models;
using WpfCalibrator.Services;
using WpfCalibrator.ViewModels;
using WpfCalibrator.ViewModels.WidgetViewModel;

namespace WpfCalibrator.Views
{
    public partial class TableSettingsWindow : Window
    {
        private readonly BaseWidgetViewModel _targetWidget;
        private readonly VariableViewModelBase _targetVariableViewModel;



        /// <summary>
        /// Структура локализации типа виджета для комбобокса. [1.14]
        /// </summary>
        public struct WidgetTypeDisplayPair
        {
            public WidgetViewType Type { get; set; }
            public string DisplayName { get; set; }

            public WidgetTypeDisplayPair(WidgetViewType type, string displayName)
            {
                Type = type;
                DisplayName = displayName;
            }
        }

        // Наш пуленепробиваемый массив всех доступных приборов
        private readonly WidgetTypeDisplayPair[] _availableWidgets = new[]
        {
            new WidgetTypeDisplayPair(WidgetViewType.SingleDigitalIndicator, "Цифровой индикатор (Digital)"),
            new WidgetTypeDisplayPair(WidgetViewType.SliderHorizontal,       "Слайдер горизонтальный"),
            new WidgetTypeDisplayPair(WidgetViewType.SliderVertical,         "Слайдер вертикальный"),
            new WidgetTypeDisplayPair(WidgetViewType.GaugeCircular270,       "Стрелочный прибор 270°"),
            new WidgetTypeDisplayPair(WidgetViewType.GaugeLED,               "Светодиодная шкала"),
            new WidgetTypeDisplayPair(WidgetViewType.TimePlot,               "Осциллограф реального времени")
        };



        // Конструктор теперь принимает сам виджет BaseWidgetViewModel
        // ======================================================================
        // ЧАСТЬ 1: КОНСТРУКТОР И ВЫДЕЛЕННАЯ ФИЛЬТРАЦИЯ СПИСКОВ (ПОДФУНКЦИЯ)
        // ======================================================================
        public TableSettingsWindow(BaseWidgetViewModel targetWidget, List<VariableViewModelBase> allVariables)
        {
            InitializeComponent();
            _targetWidget = targetWidget;
            _targetVariableViewModel = targetWidget.DataSource;

            // 1. Сначала настраиваем правила разбора структуры в ОЗУ
            ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.DisplayMemberPath = nameof(WidgetTypeDisplayPair.DisplayName);
            ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.SelectedValuePath = nameof(WidgetTypeDisplayPair.Type);

            // 2. И только после этого заливаем массив данных!
            ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.ItemsSource = _availableWidgets;


            InitializeComboBoxSources(allVariables);
            //RestoreExistingBindings();
            LoadWidgetSettings(_targetWidget);
            //SetupWindowLayout();
        }

        /// <summary>
        /// Распределяет переменные прошивки по комбобоксам согласно их типам данных.
        /// </summary>
        private void InitializeComboBoxSources(List<VariableViewModelBase> allVariables)
        {
            if (allVariables == null) return;

            // Фильтруем одномерные таблицы осей (оцифровки шкал)
            var axisVariables = allVariables.OfType<CurveVariableViewModel>().ToList();

            // Фильтруем чистые скалярные датчики телеметрии BlackPill
            var telemetryVariables = allVariables.OfType<ScalarVariableViewModel>()
                .Where(v => !v.IsParam)
                .ToList();

            // 1. Указываем правила отображения имени переменной для всех списков
            ComboBox_GraphPlot_TelemetrySignalChannel1Source.DisplayMemberPath = nameof(ScalarVariableViewModel.Name);
            ComboBox_GraphPlot_TelemetrySignalChannel2Source.DisplayMemberPath = nameof(ScalarVariableViewModel.Name);

            ComboBox_CalibrationTable_TelemetrySignalHorizontalAxisXSource.DisplayMemberPath = nameof(ScalarVariableViewModel.Name);
            ComboBox_CalibrationTable_TelemetrySignalVerticalAxisYSource.DisplayMemberPath = nameof(ScalarVariableViewModel.Name);

            ComboBox_CalibrationTable_HorizontalScaleBreakpointLut.DisplayMemberPath = nameof(CurveVariableViewModel.Name);
            ComboBox_CalibrationTable_VerticalScaleBreakpointLut.DisplayMemberPath = nameof(CurveVariableViewModel.Name);

            // 2. Вливаем живые данные в элементы управления
            ComboBox_GraphPlot_TelemetrySignalChannel1Source.ItemsSource = telemetryVariables;
            ComboBox_GraphPlot_TelemetrySignalChannel2Source.ItemsSource = telemetryVariables;

            ComboBox_CalibrationTable_TelemetrySignalHorizontalAxisXSource.ItemsSource = telemetryVariables;
            ComboBox_CalibrationTable_TelemetrySignalVerticalAxisYSource.ItemsSource = telemetryVariables;

            ComboBox_CalibrationTable_HorizontalScaleBreakpointLut.ItemsSource = axisVariables;
            ComboBox_CalibrationTable_VerticalScaleBreakpointLut.ItemsSource = axisVariables;
        }

        /// <summary>
        /// Загружает текущие настройки из переданного виджета в элементы управления окна.
        /// </summary>
        /// <param name="widget">Экземпляр вьюмодели виджета, чьи настройки редактируются.</param>
        private void LoadWidgetSettings(BaseWidgetViewModel widget)
        {
            if (widget == null) return;

            // 1. Выставляем тип прибора в главном комбобоксе стилей
            ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.SelectedValue = widget.ControlView;

            // 2. Загружаем физические лимиты, шаги и алармы из привязанной переменной BlackPill
            if (widget.DataSource is ScalarVariableViewModel scalarVar)
            {
                //TextBox_CalibrationStep_KeyboardIncrementValue.Text = scalarVar.IncrementStep.ToString("F3", CultureInfo.InvariantCulture);
                TextBox_GraphicScale_MinimumDisplayBoundary.Text = scalarVar.ScaleMin.ToString("F1", CultureInfo.InvariantCulture);
                TextBox_GraphicScale_MaximumDisplayBoundary.Text = scalarVar.ScaleMax.ToString("F1", CultureInfo.InvariantCulture);

                TextBox_HardwareAlarm_CriticalMinimumThreshold.Text = double.IsNegativeInfinity(scalarVar.AlarmMin)
                    ? string.Empty
                    : scalarVar.AlarmMin.ToString("F1", CultureInfo.InvariantCulture);

                TextBox_HardwareAlarm_CriticalMaximumThreshold.Text = double.IsPositiveInfinity(scalarVar.AlarmMax)
                    ? string.Empty
                    : scalarVar.AlarmMax.ToString("F1", CultureInfo.InvariantCulture);
            }

            // 3. Восстанавливаем состояние чекбоксов, осей и сигналов каналов из ОЗУ
            RestoreExistingBindings();

            // 4. Пинаем нашу развилку, чтобы на экране раскрылись только нужные строки!
            ApplyWidgetTypeSettings(widget);
        }


        /// <summary>
        /// Восстанавливает текущие привязки сигналов и осей из вьюмодели в элементы управления.
        /// </summary>
        private void RestoreExistingBindings()
        {
            if (_targetWidget == null) return;

            // 1. Восстанавливаем состояние общих переключателей и чекбоксов
            RadioButton_WidgetLayout_VerticalOrientation.IsChecked = _targetWidget.IsVertical;
            RadioButton_WidgetLayout_HorizontalOrientation.IsChecked = !_targetWidget.IsVertical;

            if (_targetWidget is BaseScalarWidgetViewModel scalarWidget)
            {
                CheckBox_UiRenderOptions_EnableRedBackgroundEmergencyFlash.IsChecked = scalarWidget.EnableVisualAlarm;
            }

            // 2. Для графиков (TimePlot) восстанавливаем два независимых скалярных канала
            if (_targetWidget is TimePlotWidgetViewModel timePlot)
            {
                ComboBox_GraphPlot_TelemetrySignalChannel1Source.SelectedValue = timePlot.Signal1;
                ComboBox_GraphPlot_TelemetrySignalChannel2Source.SelectedValue = timePlot.Signal2;
            }

            // 3. Для таблиц восстанавливаем привязки осей и их датчиков телеметрии
            if (_targetVariableViewModel is TableVariableViewModelBase tableVar)
            {
                ComboBox_CalibrationTable_TelemetrySignalHorizontalAxisXSource.SelectedValue = tableVar.BoundInputX;
                ComboBox_CalibrationTable_HorizontalScaleBreakpointLut.SelectedValue = tableVar.BoundAxisX;

                // Если это расширенная 3D-матрица — подтягиваем вертикальную ось Y
                if (tableVar is Map3DVariableViewModel map3D)
                {
                    ComboBox_CalibrationTable_TelemetrySignalVerticalAxisYSource.SelectedValue = map3D.BoundInputY;
                    ComboBox_CalibrationTable_VerticalScaleBreakpointLut.SelectedValue = map3D.BoundAxisY;

                    if (_targetWidget is MatrixTableWidgetViewModel matrixWidget)
                    {
                        CheckBox_UiRenderOptions_EnableNeonRadarTrackerTarget.IsChecked = matrixWidget.ShowRadarTracker;
                        CheckBox_UiRenderOptions_EnableHelix3DPolygonSurface.IsChecked = matrixWidget.Show3DSurface;
                    }
                }
            }
        }

        /// <summary>
        /// Управляет динамической видимостью строк-контейнеров на основе типа прибора и мерности данных.
        /// </summary>
        private void ApplyWidgetTypeSettings(BaseWidgetViewModel widget)
        {
            if (widget == null) return;

            // 1. Сначала тушим абсолютно все строки настроек, делая экран чистым
            SetVisibility(Visibility.Collapsed,
                Row_ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType,
                Row_ComboBox_GraphPlot_TelemetrySignalChannel1Source,
                Row_ComboBox_GraphPlot_TelemetrySignalChannel2Source,
                Row_ComboBox_CalibrationTable_TelemetrySignalHorizontalAxisXSource,
                Row_ComboBox_CalibrationTable_HorizontalScaleBreakpointLut,
                Row_ComboBox_CalibrationTable_TelemetrySignalVerticalAxisYSource,
                Row_ComboBox_CalibrationTable_VerticalScaleBreakpointLut,
                Row_TextBox_CalibrationStep_KeyboardIncrementValue,
                Row_ScaleRange,
                Row_AlarmRange,
                Row_Orientation,
                Row_TableOptions,
                Row_VisualAlarm);

            // 2. Свитч возвращает указатель на блок кода (Action), обходя ограничения void-типов [1.22]
            Action updateLayout = widget.ControlView switch
            {
                // Одиночный настраиваемый параметр
                WidgetViewType.SingleParam => () =>
                    SetVisibility(Visibility.Visible, Row_TextBox_CalibrationStep_KeyboardIncrementValue),

                // Чистый readonly цифровой индикатор логов
                WidgetViewType.SingleDigitalIndicator => () =>
                    SetVisibility(Visibility.Visible, Row_ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType, Row_TextBox_CalibrationStep_KeyboardIncrementValue),

                // Горизонтальный линейный ползунок
                WidgetViewType.SliderHorizontal => () =>
                    SetVisibility(Visibility.Visible, Row_ScaleRange, Row_AlarmRange, Row_Orientation, Row_VisualAlarm),

                // Вертикальный линейный ползунок
                WidgetViewType.SliderVertical => () =>
                    SetVisibility(Visibility.Visible, Row_ScaleRange, Row_AlarmRange, Row_Orientation, Row_VisualAlarm),

                // Круглый стрелочный будильник 270 градусов
                WidgetViewType.GaugeCircular270 => () =>
                    SetVisibility(Visibility.Visible, Row_ScaleRange, Row_AlarmRange),

                // Светодиодная шкала тахометра 120 градусов
                WidgetViewType.GaugeLED => () =>
                    SetVisibility(Visibility.Visible, Row_ScaleRange),

                // 🎯 ВЫСОКОСКОРОСТНОЙ ОСЦИЛЛОГРАФ: Раскрываем Канал 1, Канал 2 и границы шкал времени!
                WidgetViewType.TimePlot => () =>
                    SetVisibility(Visibility.Visible,
                        Row_ComboBox_GraphPlot_TelemetrySignalChannel1Source,
                        Row_ComboBox_GraphPlot_TelemetrySignalChannel2Source,
                        Row_ScaleRange),

                // 🎯 УМНАЯ ТАБЛИЦА: Проверяем реальный класс данных в ОЗУ, отсекая мусорные оси [1.22]
                WidgetViewType.MatrixTable => () =>
                {
                    if (_targetVariableViewModel is Map3DVariableViewModel)
                    {
                        // Тяжелая 3D-матрица (5х6): включаем полный набор осей X и Y
                        SetVisibility(Visibility.Visible,
                            Row_ComboBox_CalibrationTable_TelemetrySignalHorizontalAxisXSource,
                            Row_ComboBox_CalibrationTable_HorizontalScaleBreakpointLut,
                            Row_ComboBox_CalibrationTable_TelemetrySignalVerticalAxisYSource,
                            Row_ComboBox_CalibrationTable_VerticalScaleBreakpointLut,
                            Row_TextBox_CalibrationStep_KeyboardIncrementValue,
                            Row_TableOptions);
                    }
                    else if (_targetVariableViewModel is CurveVariableViewModel)
                    {
                        // Плоская 1D-кривая (1х10): ось Y намертво прячем, защищая привязки! [1.22]
                        SetVisibility(Visibility.Visible,
                            Row_ComboBox_CalibrationTable_TelemetrySignalHorizontalAxisXSource,
                            Row_ComboBox_CalibrationTable_HorizontalScaleBreakpointLut,
                            Row_TextBox_CalibrationStep_KeyboardIncrementValue,
                            Row_TableOptions);
                    }
                }
                ,

                // Тяжелая трехмерная Helix-сцена
                WidgetViewType.Matrix3DSurface => () =>
                    SetVisibility(Visibility.Visible,
                        Row_ComboBox_CalibrationTable_TelemetrySignalHorizontalAxisXSource,
                        Row_ComboBox_CalibrationTable_HorizontalScaleBreakpointLut,
                        Row_ComboBox_CalibrationTable_TelemetrySignalVerticalAxisYSource,
                        Row_ComboBox_CalibrationTable_VerticalScaleBreakpointLut,
                        Row_TextBox_CalibrationStep_KeyboardIncrementValue,
                        Row_TableOptions),

                // Полярный радар-трекер траектории
                WidgetViewType.RadarTracker => () =>
                    SetVisibility(Visibility.Visible, Row_TableOptions)
            };

            // 3. Выполняем выбранный блок кода — SizeToContent="Height" в XAML плавно сожмет окно! [1.22]
            updateLayout();
        }

        // Вспомогательный метод для сокращения кода:
        // void SetVisibility(Visibility visibility, params FrameworkElement[] elements) { ... }
        private void SetVisibility(Visibility visibility, params System.Windows.FrameworkElement[] elements)
        {
            foreach (var el in elements) if (el != null) el.Visibility = visibility;
        }



        /// <summary>
        /// Сишный хелпер-парсер: инвариантно переводит текст из инпута в число float.
        /// Заменяет собой макросы и убирает трехэтажные проверки с заменой запятых.
        /// </summary>
        private float ParseInput(string text, float fallback = 0f)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;

            string cleanText = text.Replace(',', '.');
            var style = System.Globalization.NumberStyles.Any;
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            return float.TryParse(cleanText, style, culture, out float result) ? result : fallback;
        }

        /// <summary>
        /// Обработчик кнопки «Применить»: считывает данные из элементов UI и фиксирует их в ОЗУ виджета.
        /// </summary>
        private void ButtonApply_Click(object sender, RoutedEventArgs e)
        {
            if (_targetWidget == null) return;

            // 1. Сохраняем строго типизированный идентификатор визуального типа прибора
            if (ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.SelectedValue is WidgetViewType selectedType)
            {
                _targetWidget.ControlView = selectedType;
            }

            // 2. Восстанавливаем общие флаги разметки и геометрии
            _targetWidget.IsVertical = RadioButton_WidgetLayout_VerticalOrientation.IsChecked == true;

            if (_targetWidget is BaseScalarWidgetViewModel scalarWidget)
            {
                scalarWidget.EnableVisualAlarm = CheckBox_UiRenderOptions_EnableRedBackgroundEmergencyFlash.IsChecked == true;
            }

            // 3. Записываем физические шаги, масштабы и алармы через наш хелпер-парсер
            _targetWidget.IncrementStep = ParseInput(TextBox_CalibrationStep_KeyboardIncrementValue.Text, 1.0f);

            if (_targetWidget.DataSource is ScalarVariableViewModel scalarVar)
            {
                scalarVar.ScaleMin = ParseInput(TextBox_GraphicScale_MinimumDisplayBoundary.Text, 0f);
                scalarVar.ScaleMax = ParseInput(TextBox_GraphicScale_MaximumDisplayBoundary.Text, 100f);

                // Если поля лимитов пустые — выставляем бесконечность
                scalarVar.AlarmMin = ParseInput(TextBox_HardwareAlarm_CriticalMinimumThreshold.Text, float.NegativeInfinity);
                scalarVar.AlarmMax = ParseInput(TextBox_HardwareAlarm_CriticalMaximumThreshold.Text, float.PositiveInfinity);
            }

            // 4. 🎯 ЕСЛИ ЭТО ОСЦИЛЛОГРАФ TIMEPLOT — ФИКСИРУЕМ КАНАЛ 1 И КАНАЛ 2!
            if (_targetWidget is TimePlotWidgetViewModel timePlot)
            {
                timePlot.Signal1 = ComboBox_GraphPlot_TelemetrySignalChannel1Source.SelectedItem as ScalarVariableViewModel;
                timePlot.Signal2 = ComboBox_GraphPlot_TelemetrySignalChannel2Source.SelectedItem as ScalarVariableViewModel;
            }

            // 5. ДЛЯ МНОГОМЕРНЫХ ТАБЛИЦ — ФИКСИРУЕМ ПРИВЯЗКИ ОСЕЙ И ДАТЧИКОВ
            if (_targetVariableViewModel is TableVariableViewModelBase tableVar)
            {
                tableVar.BoundInputX = ComboBox_CalibrationTable_TelemetrySignalHorizontalAxisXSource.SelectedItem as ScalarVariableViewModel;
                tableVar.BoundAxisX = ComboBox_CalibrationTable_HorizontalScaleBreakpointLut.SelectedItem as CurveVariableViewModel;

                // Если это расширенная 3D-матрица — дописываем вертикальные свойства Y
                if (tableVar is Map3DVariableViewModel map3D)
                {
                    map3D.BoundInputY = ComboBox_CalibrationTable_TelemetrySignalVerticalAxisYSource.SelectedItem as ScalarVariableViewModel;
                    map3D.BoundAxisY = ComboBox_CalibrationTable_VerticalScaleBreakpointLut.SelectedItem as CurveVariableViewModel;

                    if (_targetWidget is MatrixTableWidgetViewModel matrixWidget)
                    {
                        matrixWidget.ShowRadarTracker = CheckBox_UiRenderOptions_EnableNeonRadarTrackerTarget.IsChecked == true;
                        matrixWidget.Show3DSurface = CheckBox_UiRenderOptions_EnableHelix3DPolygonSurface.IsChecked == true;
                    }
                }
            }
            // 🎯 ЗАПУСКАЕМ ОРКЕСТРАЦИЮ ДОП-ПАНЕЛЕЙ НА ХОЛСТУ
            SynchronizeSecondaryLutPanelsOnWorkspace();
            // Закрываем диалоговое окно со статусом успешного сохранения
            DialogResult = true;
            Close();
        }


        /// <summary>
        /// Синхронизирует состояние дополнительных панелей (Радара и 3D-поверхности Helix) на рабочем столе.
        /// Автоматически создает или удаляет окна рядом с родительским виджетом на основе чекбоксов.
        /// </summary>
        private void SynchronizeSecondaryLutPanelsOnWorkspace()
        {
            var app = System.Windows.Application.Current;
            if (app?.MainWindow?.DataContext is not ViewModels.MainViewModel mainVm || mainVm.ActiveWidgets == null) return;

            bool reqRadar = CheckBox_UiRenderOptions_EnableNeonRadarTrackerTarget.IsChecked == true;
            bool req3D = CheckBox_UiRenderOptions_EnableHelix3DPolygonSurface.IsChecked == true;

            // 1. Оркестрация Радара на холсте
            var existRadar = mainVm.ActiveWidgets.FirstOrDefault(w =>
                w.DataSource == _targetWidget.DataSource && w.ControlView == WidgetViewType.RadarTracker);

            if (reqRadar && existRadar == null)
            {
                var radar = WidgetFactory.Create(WidgetViewType.RadarTracker, _targetWidget.DataSource);
                radar.Left = _targetWidget.Left + _targetWidget.Width + 20;
                radar.Top = _targetWidget.Top;
                mainVm.ActiveWidgets.Add(radar);
            }
            else if (!reqRadar && existRadar != null)
            {
                mainVm.ActiveWidgets.Remove(existRadar);
            }

            // 2. Оркестрация 3D-Поверхности Helix на холсте
            var exist3D = mainVm.ActiveWidgets.FirstOrDefault(w =>
                w.DataSource == _targetWidget.DataSource && w.ControlView == WidgetViewType.Matrix3DSurface);

            if (req3D && exist3D == null)
            {
                var surface = WidgetFactory.Create(WidgetViewType.Matrix3DSurface, _targetWidget.DataSource);
                surface.Left = _targetWidget.Left;
                surface.Top = _targetWidget.Top + _targetWidget.Height + 20;
                mainVm.ActiveWidgets.Add(surface);
            }
            else if (!req3D && exist3D != null)
            {
                mainVm.ActiveWidgets.Remove(exist3D);
            }
        }



        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
