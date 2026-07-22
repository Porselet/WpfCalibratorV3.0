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

            if (_targetVariableViewModel != null)
            {
                TableNameText.Text = _targetVariableViewModel.Name;
            }

            TextIncrementStep.Text = _targetWidget.IncrementStep.ToString("F3", CultureInfo.InvariantCulture);


            ComboStyle.ItemsSource = _availableWidgets;


            // Вызываем нашу новую чистую подфункцию разделения типов [1.14]
            //ApplyWidgetTypeSettings(targetWidget);

            InitializeComboBoxSources(allVariables);
            RestoreExistingBindings();
            LoadWidgetSettings(_targetWidget);
            //SetupWindowLayout();
        }

        /// <summary>
        /// Распределяет переменные по комбобоксам согласно их типам данных и мерности.
        /// </summary>
        /// <param name="allVariables">Список всех зарегистрированных переменных.</param>
        private void InitializeComboBoxSources(List<VariableViewModelBase> allVariables)
        {
            if (allVariables == null) return;

            // Фильтрация переменных для оси и датчиков
            var axisVariables = allVariables.OfType<CurveVariableViewModel>().ToList();
            var telemetryVariables = allVariables.OfType<ScalarVariableViewModel>()
                .Where(v => !v.IsParam)
                .ToList();

            // Привязка источников данных
            ComboAxisX.ItemsSource = axisVariables;
            ComboAxisY.ItemsSource = axisVariables;
            ComboInputX.ItemsSource = telemetryVariables;
            ComboInputY.ItemsSource = telemetryVariables;

            // 🎯 Подключение нового комбобокса для Канала 2
            ComboInputY2.ItemsSource = telemetryVariables;

        }

        /// <summary>
        /// Загружает текущие настройки из переданного виджета в элементы управления окна.
        /// </summary>
        /// <param name="widget">Экземпляр вьюмодели виджета, чьи настройки редактируются.</param>
        private void LoadWidgetSettings(BaseWidgetViewModel widget)
        {
            if (widget == null) return;

            // 1. Выставляем тип прибора в главном комбобоксе стилей
            ComboStyle.SelectedValue = widget.ControlView;

            // 2. Восстанавливаем состояние общих переключателей ориентации
            RadioVertical.IsChecked = widget.IsVertical;
            RadioHorizontal.IsChecked = !widget.IsVertical;

            // 3. Подтягиваем данные из базовой скалярной переменной (если она привязана)
            if (widget.DataSource is ScalarVariableViewModel scalarVar)
            {
                ComboInputX.SelectedValue = scalarVar;
                //TextIncrementStep.Text = scalarVar.IncrementStep.ToString("F3", CultureInfo.InvariantCulture);
                TextScaleMin.Text = scalarVar.ScaleMin.ToString("F1", CultureInfo.InvariantCulture);
                TextScaleMax.Text = scalarVar.ScaleMax.ToString("F1", CultureInfo.InvariantCulture);

                TextMinLimit.Text = double.IsNegativeInfinity(scalarVar.AlarmMin) ? string.Empty : scalarVar.AlarmMin.ToString("F1", CultureInfo.InvariantCulture);
                TextMaxLimit.Text = double.IsPositiveInfinity(scalarVar.AlarmMax) ? string.Empty : scalarVar.AlarmMax.ToString("F1", CultureInfo.InvariantCulture);
            }

            // 4. 🎯 ДЛЯ ГРАФИКА: Подтягиваем индивидуальные привязки Каналов 1 и 2
            if (widget is TimePlotWidgetViewModel timePlotWidget)
            {
                ComboInputX.SelectedValue = timePlotWidget.Signal1;
                ComboInputY2.SelectedValue = timePlotWidget.Signal2;
            }

            // 5. Разворачиваем нужные строки-контейнеры на экране на основе типа прибора
            ApplyWidgetTypeSettings(widget);
        }

        /// <summary>
        /// Подфункция восстановления привязок: безопасно считывает текущие связи осей из ОЗУ [1.14]
        /// </summary>
        private void RestoreExistingBindings()
        {
            if (_targetVariableViewModel == null) return;

            // Распознаем общую табличную базу (1D и 3D) [1.14]
            if (_targetVariableViewModel is TableVariableViewModelBase tableVar)
            {
                if (tableVar.BoundAxisX != null && ComboAxisX.ItemsSource is List<CurveVariableViewModel> axisList)
                    ComboAxisX.SelectedItem = axisList.FirstOrDefault(v => v.Name == tableVar.BoundAxisX.Name);

                if (tableVar.BoundInputX != null && ComboInputX.ItemsSource is List<ScalarVariableViewModel> telemetryList)
                    ComboInputX.SelectedItem = telemetryList.FirstOrDefault(v => v.Name == tableVar.BoundInputX.Name);
            }

            // Эксклюзивные проверки вертикальной оси Y для 3D-матриц [1.14]
            if (_targetVariableViewModel is Map3DVariableViewModel map3D)
            {
                if (map3D.BoundAxisY != null && ComboAxisY.ItemsSource is List<CurveVariableViewModel> axisList)
                    ComboAxisY.SelectedItem = axisList.FirstOrDefault(v => v.Name == map3D.BoundAxisY.Name);

                if (map3D.BoundInputY != null && ComboInputY.ItemsSource is List<ScalarVariableViewModel> telemetryList)
                    ComboInputY.SelectedItem = telemetryList.FirstOrDefault(v => v.Name == map3D.BoundInputY.Name);
            }

            else if (_targetWidget is TimePlotWidgetViewModel timePlot)
            {
                // Логика сопоставления сигналов из Signal1/Signal2 с ComboInputX/ComboInputY2
                if (timePlot.Signal1 != null)
                    ComboInputX.SelectedItem = (ComboInputX.ItemsSource as IEnumerable<ScalarVariableViewModel>)?.FirstOrDefault(v => v.Name == timePlot.Signal1.Name);
                if (timePlot.Signal2 != null)
                    ComboInputY2.SelectedItem = (ComboInputY2.ItemsSource as IEnumerable<ScalarVariableViewModel>)?.FirstOrDefault(v => v.Name == timePlot.Signal2.Name);
            }
        }

        /// <summary>
        /// Подфункция-Хамелеон: включает нужные поля и подгоняет высоту окна под тип прибора [1.14]
        /// </summary>
        private void ApplyWidgetTypeSettings(BaseWidgetViewModel widget)
        {
            // 1. Сначала тушим абсолютно все строки настроек, делая экран чистым
            SetVisibility(Visibility.Collapsed,
                Row_WidgetStyle,
                Row_InputX,
                Row_InputY2,
                Row_AxisX,
                Row_AxisY,
                Row_InputY,
                Row_IncrementStep,
                Row_ScaleRange,
                Row_AlarmRange,
                Row_Orientation,
                Row_TableOptions,
                Row_VisualAlarm);

            
            Action updateLayout = widget.ControlView switch
            {
                WidgetViewType.SingleParam => () =>
                    SetVisibility(Visibility.Visible, Row_WidgetStyle, Row_WidgetStyle, Row_InputX, Row_IncrementStep),

                WidgetViewType.SingleDigitalIndicator => () =>
                    SetVisibility(Visibility.Visible, Row_WidgetStyle, Row_InputX, Row_IncrementStep),

                WidgetViewType.TimePlot => () =>
                    SetVisibility(Visibility.Visible, Row_WidgetStyle, Row_InputX, Row_InputY2, Row_ScaleRange),

                WidgetViewType.MatrixTable => () =>
                    SetVisibility(Visibility.Visible, Row_WidgetStyle, Row_InputX, Row_AxisX, Row_AxisY, Row_IncrementStep),

                WidgetViewType.Matrix3DSurface => () =>
                    SetVisibility(Visibility.Visible, Row_WidgetStyle, Row_InputX, Row_AxisX, Row_AxisY, Row_InputY, Row_TableOptions),

                WidgetViewType.RadarTracker => () =>
                    SetVisibility(Visibility.Visible, Row_WidgetStyle, Row_InputX, Row_TableOptions),

                WidgetViewType.GaugeCircular270 => () =>
                    SetVisibility(Visibility.Visible, Row_WidgetStyle, Row_InputX, Row_ScaleRange, Row_AlarmRange),

                WidgetViewType.GaugeLED => () =>
                    SetVisibility(Visibility.Visible, Row_WidgetStyle, Row_InputX, Row_ScaleRange),
                
                WidgetViewType.SliderHorizontal => () =>
                    SetVisibility(Visibility.Visible, Row_WidgetStyle, Row_InputX, Row_ScaleRange, Row_AlarmRange, Row_VisualAlarm),

                
                WidgetViewType.SliderVertical => () =>
                    SetVisibility(Visibility.Visible, Row_WidgetStyle, Row_InputX, Row_ScaleRange, Row_AlarmRange, Row_VisualAlarm),
            };

            // 2. 🎯 ОДНИМ СИШНЫМ ПИНОМ ВЫПОЛНЯЕМ ВЫБРАННЫЙ БЛОК КОДА!
            updateLayout();
        }

        // Вспомогательный метод для сокращения кода:
        // void SetVisibility(Visibility visibility, params FrameworkElement[] elements) { ... }
        private void SetVisibility(Visibility visibility, params System.Windows.FrameworkElement[] elements)
        {
            foreach (var el in elements) if (el != null) el.Visibility = visibility;
        }



        private void ButtonApply_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // 1. Сохраняем шаг инкремента
            _targetWidget.IncrementStep = ParseInput(TextIncrementStep.Text, 1.0f);

            // 2. Забиваем масштабы шкалы прибора
            if (_targetVariableViewModel is ScalarVariableViewModel scalar)
            {
                scalar.ScaleMin = ParseInput(TextScaleMin.Text, 0f);
                scalar.ScaleMax = ParseInput(TextScaleMax.Text, 100f);

                // 3. Забиваем критические алармы (если пусто — ставим бесконечность)
                scalar.AlarmMin = ParseInput(TextMinLimit.Text, float.NegativeInfinity);
                scalar.AlarmMax = ParseInput(TextMaxLimit.Text, float.PositiveInfinity);
            }
            if (_targetVariableViewModel is TableVariableViewModelBase tableVar)
            {
                tableVar.BoundAxisX = ComboAxisX.SelectedItem as CurveVariableViewModel;
                // ... привязки осей ...
            }
            // Привязка нового комбобокса [1.22]
            if (_targetWidget is TimePlotWidgetViewModel timePlot)
            {
                timePlot.Signal1 = ComboInputX.SelectedItem as ScalarVariableViewModel;
                timePlot.Signal2 = ComboInputY2.SelectedItem as ScalarVariableViewModel;
            }

            _targetWidget.IsVertical = RadioVertical.IsChecked == true;
            if (ComboStyle.SelectedValue is WidgetViewType selectedType)
                _targetWidget.ControlView = selectedType;
            DialogResult = true;
            Close();


        }

        /// <summary>
        /// Сишный хелпер-парсер: заменяет макрос, инвариантно переводит текст инпута в число.
        /// </summary>
        private float ParseInput(string text, float fallback = 0f)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;

            string cleanText = text.Replace(',', '.');
            var style = System.Globalization.NumberStyles.Any;
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            return float.TryParse(cleanText, style, culture, out float result) ? result : fallback;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
