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

        private const string EMPTY_SELECTION = "—";

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

            // --- 1. СОЗДАЁМ ПУСТЫЕ ОБЪЕКТЫ ---
            var emptyScalar = new ScalarVariableViewModel { Name = EMPTY_SELECTION };
            var emptyCurve = new CurveVariableViewModel { Name = EMPTY_SELECTION };

            // --- 2. ФИЛЬТРУЕМ СПИСКИ ---
            var axisVariables = allVariables.OfType<CurveVariableViewModel>().ToList();
            axisVariables.Insert(0, emptyCurve); // Пустой пункт в начало

            var telemetryVariables = allVariables.OfType<ScalarVariableViewModel>()
                .Where(v => !v.IsParam)
                .ToList();
            telemetryVariables.Insert(0, emptyScalar); // Пустой пункт в начало



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
            if (widget is EditableWidgetViewModel editableWidget)
            {
                TextBox_CalibrationStep_KeyboardIncrementValue.Text = editableWidget.IncrementStep.ToString("F3", CultureInfo.InvariantCulture);
            }
            // Загрузка настроек для второго канала графика (если это TimePlot)
            if (widget is TimePlotWidgetViewModel timePlot && timePlot.Signal2 != null)
            {
                var signal2 = timePlot.Signal2;
                TextBox_GraphicScale_MinimumDisplayBoundary2.Text = signal2.ScaleMin.ToString("F1", CultureInfo.InvariantCulture);
                TextBox_GraphicScale_MaximumDisplayBoundary2.Text = signal2.ScaleMax.ToString("F1", CultureInfo.InvariantCulture);

                TextBox_HardwareAlarm_CriticalMinimumThreshold2.Text = double.IsNegativeInfinity(signal2.AlarmMin)
                    ? string.Empty
                    : signal2.AlarmMin.ToString("F1", CultureInfo.InvariantCulture);

                TextBox_HardwareAlarm_CriticalMaximumThreshold2.Text = double.IsPositiveInfinity(signal2.AlarmMax)
                    ? string.Empty
                    : signal2.AlarmMax.ToString("F1", CultureInfo.InvariantCulture);
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

                bool hasSignal2 = timePlot.Signal2 != null;
                SetEnabled(Row_ScaleRange2, hasSignal2);
                SetEnabled(Row_AlarmRange2, hasSignal2);
            }

            // 3. Для таблиц восстанавливаем привязки осей и их датчиков телеметрии
            if (_targetVariableViewModel is TableVariableViewModelBase tableVar)
            {
                // --- Восстанавливаем BoundInputX ---
                if (tableVar.BoundInputX != null)
                {
                    var item = ComboBox_CalibrationTable_TelemetrySignalHorizontalAxisXSource.ItemsSource
                        .OfType<ScalarVariableViewModel>()
                        .FirstOrDefault(v => v.Name == tableVar.BoundInputX.Name);
                    ComboBox_CalibrationTable_TelemetrySignalHorizontalAxisXSource.SelectedItem = item;
                }
                else
                {
                    // Выбираем пустой элемент
                    ComboBox_CalibrationTable_TelemetrySignalHorizontalAxisXSource.SelectedItem =
                        ComboBox_CalibrationTable_TelemetrySignalHorizontalAxisXSource.ItemsSource
                            .OfType<ScalarVariableViewModel>()
                            .FirstOrDefault(v => v.Name == EMPTY_SELECTION);
                }

                // --- Восстанавливаем BoundAxisX (аналогично) ---
                if (tableVar.BoundAxisX != null)
                {
                    var item = ComboBox_CalibrationTable_HorizontalScaleBreakpointLut.ItemsSource
                        .OfType<CurveVariableViewModel>()
                        .FirstOrDefault(v => v.Name == tableVar.BoundAxisX.Name);
                    ComboBox_CalibrationTable_HorizontalScaleBreakpointLut.SelectedItem = item;
                }
                else
                {
                    ComboBox_CalibrationTable_HorizontalScaleBreakpointLut.SelectedItem =
                        ComboBox_CalibrationTable_HorizontalScaleBreakpointLut.ItemsSource
                            .OfType<CurveVariableViewModel>()
                            .FirstOrDefault(v => v.Name == EMPTY_SELECTION);
                }

                // --- Для 3D-карт: Y привязки ---
                if (tableVar is Map3DVariableViewModel map3D)
                {
                    // BoundInputY
                    if (map3D.BoundInputY != null)
                    {
                        var item = ComboBox_CalibrationTable_TelemetrySignalVerticalAxisYSource.ItemsSource
                            .OfType<ScalarVariableViewModel>()
                            .FirstOrDefault(v => v.Name == map3D.BoundInputY.Name);
                        ComboBox_CalibrationTable_TelemetrySignalVerticalAxisYSource.SelectedItem = item;
                    }
                    else
                    {
                        ComboBox_CalibrationTable_TelemetrySignalVerticalAxisYSource.SelectedItem =
                            ComboBox_CalibrationTable_TelemetrySignalVerticalAxisYSource.ItemsSource
                                .OfType<ScalarVariableViewModel>()
                                .FirstOrDefault(v => v.Name == EMPTY_SELECTION);
                    }

                    // BoundAxisY
                    if (map3D.BoundAxisY != null)
                    {
                        var item = ComboBox_CalibrationTable_VerticalScaleBreakpointLut.ItemsSource
                            .OfType<CurveVariableViewModel>()
                            .FirstOrDefault(v => v.Name == map3D.BoundAxisY.Name);
                        ComboBox_CalibrationTable_VerticalScaleBreakpointLut.SelectedItem = item;
                    }
                    else
                    {
                        ComboBox_CalibrationTable_VerticalScaleBreakpointLut.SelectedItem =
                            ComboBox_CalibrationTable_VerticalScaleBreakpointLut.ItemsSource
                                .OfType<CurveVariableViewModel>()
                                .FirstOrDefault(v => v.Name == EMPTY_SELECTION);
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
                Row_ScaleRange2,
                Row_AlarmRange2,
                Row_Orientation,
                Row_TableOptions,
                Row_VisualAlarm);

            // 2. Определяем, что у нас за данные (сигнал или параметр)
            bool isParam = _targetVariableViewModel?.IsParam ?? false;

            // 3. Свитч по типу виджета
            Action updateLayout = widget.ControlView switch
            {
                // ============================================================
                // ПАРАМЕТРЫ (Configurable)
                // ============================================================

                // Одиночный параметр
                WidgetViewType.SingleParam => () =>
                {
                    // Скрываем выбор типа виджета (он не нужен для параметров)
                    Row_ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.Visibility = Visibility.Collapsed;
                    // Шкалы для параметров не показываем
                    Row_ScaleRange.Visibility = Visibility.Collapsed;
                    // Алармы для параметров не показываем
                    Row_AlarmRange.Visibility = Visibility.Collapsed;
                    // Показываем шаг изменения
                    Row_TextBox_CalibrationStep_KeyboardIncrementValue.Visibility = Visibility.Visible;
                }
                ,

                // ============================================================
                // СИГНАЛЫ ТЕЛЕМЕТРИИ (Read-Only)
                // ============================================================

                // Цифровой индикатор
                WidgetViewType.SingleDigitalIndicator => () =>
                {
                    Row_ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.Visibility = Visibility.Visible;
                    Row_AlarmRange.Visibility = Visibility.Visible;
                    // Шаг не нужен для сигналов
                    Row_TextBox_CalibrationStep_KeyboardIncrementValue.Visibility = Visibility.Collapsed;
                }
                ,

                // Слайдеры
                WidgetViewType.SliderHorizontal => () =>
                {
                    Row_ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.Visibility = Visibility.Visible;
                    Row_ScaleRange.Visibility = Visibility.Visible;
                    Row_AlarmRange.Visibility = Visibility.Visible;
                    Row_Orientation.Visibility = Visibility.Visible;
                    Row_VisualAlarm.Visibility = Visibility.Visible;
                    Row_TextBox_CalibrationStep_KeyboardIncrementValue.Visibility = Visibility.Collapsed;
                }
                ,

                WidgetViewType.SliderVertical => () =>
                {
                    Row_ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.Visibility = Visibility.Visible;
                    Row_ScaleRange.Visibility = Visibility.Visible;
                    Row_AlarmRange.Visibility = Visibility.Visible;
                    Row_Orientation.Visibility = Visibility.Visible;
                    Row_VisualAlarm.Visibility = Visibility.Visible;
                    Row_TextBox_CalibrationStep_KeyboardIncrementValue.Visibility = Visibility.Collapsed;
                }
                ,

                // Стрелочный прибор
                WidgetViewType.GaugeCircular270 => () =>
                {
                    Row_ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.Visibility = Visibility.Visible;
                    Row_ScaleRange.Visibility = Visibility.Visible;
                    Row_AlarmRange.Visibility = Visibility.Visible;
                    Row_TextBox_CalibrationStep_KeyboardIncrementValue.Visibility = Visibility.Collapsed;
                }
                ,

                // Светодиодная шкала
                WidgetViewType.GaugeLED => () =>
                {
                    Row_ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.Visibility = Visibility.Visible;
                    Row_ScaleRange.Visibility = Visibility.Visible;
                    // Для LED алармы не показываем (по матрице)
                    Row_AlarmRange.Visibility = Visibility.Collapsed;
                    Row_TextBox_CalibrationStep_KeyboardIncrementValue.Visibility = Visibility.Collapsed;
                }
                ,

                // ============================================================
                // ГРАФИК (всегда сигнал)
                // ============================================================
                WidgetViewType.TimePlot => () =>
                {
                    Row_ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.Visibility = Visibility.Visible;
                    Row_ComboBox_GraphPlot_TelemetrySignalChannel1Source.Visibility = Visibility.Visible;
                    Row_ComboBox_GraphPlot_TelemetrySignalChannel2Source.Visibility = Visibility.Visible;
                    Row_ScaleRange.Visibility = Visibility.Visible;
                    // Для второго канала показываем всегда, но управляем IsEnabled отдельно
                    Row_ScaleRange2.Visibility = Visibility.Visible;
                    Row_AlarmRange2.Visibility = Visibility.Visible;
                    Row_TextBox_CalibrationStep_KeyboardIncrementValue.Visibility = Visibility.Collapsed;
                    // Алармы для графика не показываем (они на линиях)
                    Row_AlarmRange.Visibility = Visibility.Collapsed;
                }
                ,

                // ============================================================
                // ТАБЛИЦЫ (всегда параметры)
                // ============================================================

                // Умная таблица (1D и 2D)
                WidgetViewType.MatrixTable => () =>
                {
                    // Скрываем выбор типа виджета для таблиц
                    Row_ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.Visibility = Visibility.Collapsed;

                    // Определяем, какая таблица (1D или 2D)
                    if (_targetVariableViewModel is Map3DVariableViewModel)
                    {
                        // 3D-матрица: показываем все оси X и Y
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
                        // 1D-кривая: только ось X
                        SetVisibility(Visibility.Visible,
                            Row_ComboBox_CalibrationTable_TelemetrySignalHorizontalAxisXSource,
                            Row_ComboBox_CalibrationTable_HorizontalScaleBreakpointLut,
                            Row_TextBox_CalibrationStep_KeyboardIncrementValue,
                            Row_TableOptions);
                    }
                }
                ,

                // 3D-поверхность Helix
                WidgetViewType.Matrix3DSurface => () =>
                {
                    // Скрываем выбор типа виджета
                    Row_ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.Visibility = Visibility.Collapsed;

                    SetVisibility(Visibility.Visible,
                        Row_ComboBox_CalibrationTable_TelemetrySignalHorizontalAxisXSource,
                        Row_ComboBox_CalibrationTable_HorizontalScaleBreakpointLut,
                        Row_ComboBox_CalibrationTable_TelemetrySignalVerticalAxisYSource,
                        Row_ComboBox_CalibrationTable_VerticalScaleBreakpointLut,
                        Row_TextBox_CalibrationStep_KeyboardIncrementValue,
                        Row_TableOptions);
                }
                ,

                // Радар-трекер
                WidgetViewType.RadarTracker => () =>
                {
                    // Скрываем выбор типа виджета
                    Row_ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.Visibility = Visibility.Collapsed;
                    Row_TableOptions.Visibility = Visibility.Visible;
                }
                ,

                // Заглушка для неизвестных типов
                _ => () => { }
            };

            // 4. Выполняем выбранный блок
            updateLayout();

            // 5. Дополнительно управляем активностью второго канала графика
            if (widget is TimePlotWidgetViewModel timePlot)
            {
                bool hasSignal2 = timePlot.Signal2 != null;
                SetEnabled(Row_ScaleRange2, hasSignal2);
                SetEnabled(Row_AlarmRange2, hasSignal2);
            }
        }
        // Вспомогательный метод для сокращения кода:
        // void SetVisibility(Visibility visibility, params FrameworkElement[] elements) { ... }
        private void SetVisibility(Visibility visibility, params System.Windows.FrameworkElement[] elements)
        {
            foreach (var el in elements) if (el != null) el.Visibility = visibility;
        }

        private void SetEnabled(System.Windows.FrameworkElement element, bool isEnabled)
        {
            if (element != null)
                element.IsEnabled = isEnabled;
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

            // 1. Сохраняем настройки виджета (геометрия, флаги, сигналы)
            SaveWidgetSettings();

            // 2. Сохраняем настройки переменной в VariableSettings
            SaveVariableSettings();

            // 3. Синхронизация дополнительных панелей (Radar, 3D)
            SynchronizeSecondaryLutPanelsOnWorkspace();

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Сохраняет настройки самого виджета (геометрия, флаги, сигналы)
        /// </summary>
        private void SaveWidgetSettings()
        {
            if (_targetWidget == null) return;

            // Тип виджета
            if (ComboBox_WidgetGeometry_ScalarSignalVisualComponentStyleType.SelectedValue is WidgetViewType selectedType)
            {
                _targetWidget.ControlView = selectedType;
            }

            _targetWidget.IsVertical = RadioButton_WidgetLayout_VerticalOrientation.IsChecked == true;

            // Флаги для скалярных виджетов
            if (_targetWidget is BaseScalarWidgetViewModel scalarWidget)
            {
                scalarWidget.EnableVisualAlarm = CheckBox_UiRenderOptions_EnableRedBackgroundEmergencyFlash.IsChecked == true;
            }

            // Шаг для редактируемых виджетов (только параметры!)
            if (_targetWidget is EditableWidgetViewModel editableWidget)
            {
                editableWidget.IncrementStep = ParseInput(TextBox_CalibrationStep_KeyboardIncrementValue.Text, 1.0f);
            }

            // Сигналы для графика
            if (_targetWidget is TimePlotWidgetViewModel timePlot)
            {
                timePlot.Signal1 = ComboBox_GraphPlot_TelemetrySignalChannel1Source.SelectedItem as ScalarVariableViewModel;
                timePlot.Signal2 = ComboBox_GraphPlot_TelemetrySignalChannel2Source.SelectedItem as ScalarVariableViewModel;
            }

            // Флаги для табличных виджетов
            if (_targetWidget is MatrixTableWidgetViewModel matrixWidget)
            {
                matrixWidget.ShowRadarTracker = CheckBox_UiRenderOptions_EnableNeonRadarTrackerTarget.IsChecked == true;
                matrixWidget.Show3DSurface = CheckBox_UiRenderOptions_EnableHelix3DPolygonSurface.IsChecked == true;
            }
        }

        /// <summary>
        /// Сохраняет настройки переменной в VariableSettings (шкалы, алармы, привязки)
        /// </summary>
        private void SaveVariableSettings()
        {
            var mainVm = Application.Current?.MainWindow?.DataContext as MainViewModel;
            if (mainVm?.SelectedDevice == null) return;

            var configManager = new ConfigurationManager();
            var config = configManager.LoadUserConfigForDevice(mainVm.SelectedDevice.DevicePath);
            if (config == null) return;

            // Получаем или создаём настройки для целевой переменной
            if (!config.VariableSettings.TryGetValue(_targetVariableViewModel.Name, out var settings))
            {
                settings = new VariableDisplaySettings();
                config.VariableSettings[_targetVariableViewModel.Name] = settings;
            }

            // ---- 1. Для скаляров (сигналы телеметрии) ----
            if (_targetVariableViewModel is ScalarVariableViewModel scalarVar)
            {
                // Сохраняем только если это сигнал (IsParam == false)
                // Для параметров поля скрыты, но на всякий случай проверяем
                if (!scalarVar.IsParam)
                {
                    settings.ScaleMin = ParseInput(TextBox_GraphicScale_MinimumDisplayBoundary.Text, 0f);
                    settings.ScaleMax = ParseInput(TextBox_GraphicScale_MaximumDisplayBoundary.Text, 100f);
                    settings.AlarmMin = ParseInput(TextBox_HardwareAlarm_CriticalMinimumThreshold.Text, float.NegativeInfinity);
                    settings.AlarmMax = ParseInput(TextBox_HardwareAlarm_CriticalMaximumThreshold.Text, float.PositiveInfinity);
                }
            }

            // ---- 2. Для таблиц (параметры) ----
            if (_targetVariableViewModel is TableVariableViewModelBase tableVar)
            {
                SaveTableBindings(settings);
            }

            // ---- 3. Для второго канала графика ----
            if (_targetWidget is TimePlotWidgetViewModel timePlot && timePlot.Signal2 != null)
            {
                SaveSecondChannelSettings(config, timePlot.Signal2);
            }

            // Сохраняем конфиг на диск
            configManager.SaveUserConfig(config, mainVm.SelectedDevice.DevicePath);
            // Костыль: перезагружаем устройство, чтобы применить настройки
            mainVm.SelectedDevice = mainVm.SelectedDevice;
        }

        /// <summary>
        /// Сохраняет привязки таблицы (оси и входные сигналы)
        /// </summary>
        private void SaveTableBindings(VariableDisplaySettings settings)
        {
            if (_targetVariableViewModel is not TableVariableViewModelBase) return;

            var bindings = new LutBindings();
            bool hasBindings = false;

            // --- Проверяем InputX ---
            var inputX = ComboBox_CalibrationTable_TelemetrySignalHorizontalAxisXSource.SelectedItem as ScalarVariableViewModel;
            if (inputX != null && inputX.Name != EMPTY_SELECTION)
            {
                bindings.InputX_VarName = inputX.Name;
                hasBindings = true;
            }

            // --- Проверяем AxisX ---
            var axisX = ComboBox_CalibrationTable_HorizontalScaleBreakpointLut.SelectedItem as CurveVariableViewModel;
            if (axisX != null && axisX.Name != EMPTY_SELECTION)
            {
                bindings.AxisX_VarName = axisX.Name;
                hasBindings = true;
            }

            // --- Для 3D-карт: проверяем Y ---
            if (_targetVariableViewModel is Map3DVariableViewModel)
            {
                var inputY = ComboBox_CalibrationTable_TelemetrySignalVerticalAxisYSource.SelectedItem as ScalarVariableViewModel;
                if (inputY != null && inputY.Name != EMPTY_SELECTION)
                {
                    bindings.InputY_VarName = inputY.Name;
                    hasBindings = true;
                }

                var axisY = ComboBox_CalibrationTable_VerticalScaleBreakpointLut.SelectedItem as CurveVariableViewModel;
                if (axisY != null && axisY.Name != EMPTY_SELECTION)
                {
                    bindings.AxisY_VarName = axisY.Name;
                    hasBindings = true;
                }
            }

            // Сохраняем только если есть хотя бы одна реальная привязка
            if (hasBindings)
            {
                bindings.HasBindings = true;
                settings.TableBindings = bindings;
            }
            else
            {
                settings.TableBindings = null;
            }
        }
        /// <summary>
        /// Сохраняет настройки для второго канала графика
        /// </summary>
        private void SaveSecondChannelSettings(UserViewConfig config, ScalarVariableViewModel signal2)
        {
            if (signal2 == null) return;

            if (!config.VariableSettings.TryGetValue(signal2.Name, out var settings))
            {
                settings = new VariableDisplaySettings();
                config.VariableSettings[signal2.Name] = settings;
            }

            settings.ScaleMin = ParseInput(TextBox_GraphicScale_MinimumDisplayBoundary2.Text, 0f);
            settings.ScaleMax = ParseInput(TextBox_GraphicScale_MaximumDisplayBoundary2.Text, 100f);
            settings.AlarmMin = ParseInput(TextBox_HardwareAlarm_CriticalMinimumThreshold2.Text, float.NegativeInfinity);
            settings.AlarmMax = ParseInput(TextBox_HardwareAlarm_CriticalMaximumThreshold2.Text, float.PositiveInfinity);
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
