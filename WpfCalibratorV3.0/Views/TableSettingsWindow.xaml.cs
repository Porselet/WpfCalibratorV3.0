using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.Views
{
    public partial class TableSettingsWindow : Window
    {
        private readonly WidgetViewModel _targetWidget;
        private readonly VariableViewModelBase _targetTable;

        // Конструктор теперь принимает сам виджет WidgetViewModel
        // ======================================================================
        // ЧАСТЬ 1: КОНСТРУКТОР И ВЫДЕЛЕННАЯ ФИЛЬТРАЦИЯ СПИСКОВ (ПОДФУНКЦИЯ)
        // ======================================================================
        public TableSettingsWindow(WidgetViewModel targetWidget, List<VariableViewModelBase> allVariables)
        {
            InitializeComponent();
            _targetWidget = targetWidget;
            _targetTable = targetWidget.DataSource;

            if (_targetTable != null)
            {
                TableNameText.Text = _targetTable.Name;
            }

            TextIncrementStep.Text = _targetWidget.IncrementStep.ToString("F3", CultureInfo.InvariantCulture);

            // Вызываем нашу новую чистую подфункцию разделения типов [1.14]
            InitializeComboBoxSources(allVariables);
            RestoreExistingBindings();
            SetupWindowLayout();
        }

        /// <summary>
        /// Подфункция строгой фильтрации: раскладывает переменные по комбобоксам согласно типам ОЗУ [1.14]
        /// </summary>
        private void InitializeComboBoxSources(List<VariableViewModelBase> allVariables)
        {
            if (allVariables == null) return;

            // Шкалами-осями могут быть СТРОГО одномерные векторы [1.14]
            var axisVariables = allVariables.OfType<CurveVariableViewModel>().ToList();

            // Живыми датчиками-входами могут быть СТРОГО скаляры-сигналы телеметрии [1.14]
            var telemetryVariables = allVariables.OfType<ScalarVariableViewModel>()
                                                 .Where(v => !v.IsParam)
                                                 .ToList();

            // Заливаем в UI без единого дубликата и мусора
            ComboAxisX.ItemsSource = axisVariables;
            ComboAxisY.ItemsSource = axisVariables;

            ComboInputX.ItemsSource = telemetryVariables;
            ComboInputY.ItemsSource = telemetryVariables;
        }

        /// <summary>
        /// Подфункция восстановления привязок: безопасно считывает текущие связи осей из ОЗУ [1.14]
        /// </summary>
        private void RestoreExistingBindings()
        {
            if (_targetTable == null) return;

            // Распознаем общую табличную базу (1D и 3D) [1.14]
            if (_targetTable is TableVariableViewModelBase tableVar)
            {
                if (tableVar.BoundAxisX != null && ComboAxisX.ItemsSource is List<CurveVariableViewModel> axisList)
                    ComboAxisX.SelectedItem = axisList.FirstOrDefault(v => v.Name == tableVar.BoundAxisX.Name);

                if (tableVar.BoundInputX != null && ComboInputX.ItemsSource is List<ScalarVariableViewModel> telemetryList)
                    ComboInputX.SelectedItem = telemetryList.FirstOrDefault(v => v.Name == tableVar.BoundInputX.Name);
            }

            // Эксклюзивные проверки вертикальной оси Y для 3D-матриц [1.14]
            if (_targetTable is Map3DVariableViewModel map3D)
            {
                if (map3D.BoundAxisY != null && ComboAxisY.ItemsSource is List<CurveVariableViewModel> axisList)
                    ComboAxisY.SelectedItem = axisList.FirstOrDefault(v => v.Name == map3D.BoundAxisY.Name);

                if (map3D.BoundInputY != null && ComboInputY.ItemsSource is List<ScalarVariableViewModel> telemetryList)
                    ComboInputY.SelectedItem = telemetryList.FirstOrDefault(v => v.Name == map3D.BoundInputY.Name);
            }
        }

        /// <summary>
        /// Подфункция-Хамелеон: включает нужные поля и подгоняет высоту окна под тип прибора [1.14]
        /// </summary>
        private void SetupWindowLayout()
        {
            if (_targetWidget == null || _targetTable == null) return;

            // 1. Считываем настройки графики строго из виджета (для сохранения рабочих столов) [1.14]
            CheckShowRadar.IsChecked = _targetWidget.ShowRadarTracker;
            CheckShow3D.IsChecked = _targetWidget.Show3DSurface;
            RadioVertical.IsChecked = _targetWidget.IsVertical;
            RadioHorizontal.IsChecked = !_targetWidget.IsVertical;

            // 2. По умолчанию гасим абсолютно ВСЕ блоки перед переключением режимов [1.14]
            SetVisibility(Visibility.Collapsed,
                            LabelAxisX, ComboAxisX, 
                            LabelInputX, ComboInputX,
                            LabelAxisY, ComboAxisY, 
                            LabelInputY, ComboInputY,
                            LabelIncrementStep, TextIncrementStep,
                            LabelEnableVisualAlarm, CheckEnableVisualAlarm,
                            LabelLimits, PanelLimits,
                            LabelScaleRange, PanelScaleRange,
                            LabelOrientation, PanelOrientation,
                            LabelStyle, ComboStyle

                        );

            // Переходим к блоку распознавания (Часть 2)
            // ======================================================================
            // 3. КАСКАДНОЕ РАСПОЗНАВАНИЕ КАЛИБРОВОЧНЫХ ПАРАМЕТРОВ ЧЕРЕЗ КЛАССЫ ОЗУ
            // ======================================================================
            if (_targetWidget.ControlView == "RadarTracker")
            {
                TableNameText.Text = $"{_targetTable.Name} (Прицел)";
                this.Height = 120;
            }
            else if (_targetTable.IsParam)
            {
                TableNameText.Text = _targetTable.Name;

                if (_targetTable is Map3DVariableViewModel)
                {
                    // ТИП 1: Двумерная 3D-Карта (видимость элементов)
                    SetVisibility(Visibility.Visible, 
                        LabelAxisX, ComboAxisX, 
                        LabelInputX, ComboInputX, 
                        LabelAxisY, ComboAxisY, 
                        LabelInputY, ComboInputY, 
                        TextIncrementStep, LabelIncrementStep, 
                        LabelShowRadar, CheckShowRadar, 
                        CheckShow3D, LabelShow3D);
                    this.Height = 400;
                }
                else if (_targetTable is CurveVariableViewModel)
                {
                    // ТИП 2: Одномерный Вектор (видимость элементов)
                    SetVisibility(Visibility.Visible, 
                        LabelAxisX, ComboAxisX, 
                        LabelInputX, ComboInputX, 
                        LabelOrientation, PanelOrientation, 
                        TextIncrementStep, LabelIncrementStep, 
                        CheckShowRadar, LabelShowRadar);
                    this.Height = 290;
                }
                else if (_targetTable is ScalarVariableViewModel)
                {
                    // ТИП 3: Одиночная уставка-константа (видимость элементов)
                    SetVisibility(Visibility.Visible, 
                        TextIncrementStep, LabelIncrementStep);
                    this.Height = 200;
                }
            }
            else
            {
                // === ТИП 4: СИГНАЛ ТЕЛЕМЕТРИИ / ЖИВОЙ ДАТЧИК ===
                TableNameText.Text = _targetTable.Name;

                // Включаем блоки стилей, критических алармов и масштаба шкал
                SetVisibility(Visibility.Visible, 
                    LabelStyle, ComboStyle, 
                    LabelLimits, PanelLimits, 
                    LabelScaleRange, PanelScaleRange, 
                    LabelEnableVisualAlarm, CheckEnableVisualAlarm);

                // Безопасно выводим лимиты алармов (если бесконечность — оставляем поле пустым) [1.14]
                TextMinLimit.Text = double.IsNegativeInfinity(_targetTable.MinLimit) ? string.Empty : _targetTable.MinLimit.ToString("F1");
                TextMaxLimit.Text = double.IsPositiveInfinity(_targetTable.MaxLimit) ? string.Empty : _targetTable.MaxLimit.ToString("F1");

                // Выводим границы шкал слайдеров/графиков
                TextScaleMin.Text = _targetTable.ScaleMin.ToString("F1");
                TextScaleMax.Text = _targetTable.ScaleMax.ToString("F1");

                // Подтягиваем состояние аларм-светодиода из виджета
                CheckEnableVisualAlarm.IsChecked = _targetWidget.EnableVisualAlarm;

                // Настраиваем выбранный индекс графического стиля в комбобоксе
                ComboStyle.SelectedIndex = _targetWidget.ControlView switch
                {
                    "TextBox" or "Digital" => 0,
                    "SliderHorizontal" => 1,
                    "SliderVertical" => 2,
                    "GaugeCircular270" => 3,
                    "GaugeArc120" => 4,
                    "TimePlot" => 5,
                    _ => 0
                };

                this.Height = 280;
            }
        } // 🔥 Конец метода SetupWindowLayout

        // Вспомогательный метод для сокращения кода:
        // void SetVisibility(Visibility visibility, params FrameworkElement[] elements) { ... }
        private void SetVisibility(Visibility visibility, params System.Windows.FrameworkElement[] elements)
        {
            foreach (var el in elements) if (el != null) el.Visibility = visibility;
        }



        private void ApplyButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // ======================================================================
            // 1. ВАЛИДАЦИЯ ШАГА ИЗМЕНЕНИЯ С КЛАВИАТУРЫ
            // ======================================================================
            string stepText = TextIncrementStep.Text.Replace(',', '.');
            if (float.TryParse(stepText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float parsedStep) && parsedStep > 0)
            {
                _targetWidget.IncrementStep = parsedStep;
            }
            else if (TextIncrementStep.Visibility == System.Windows.Visibility.Visible)
            {
                System.Windows.MessageBox.Show("Введите корректное положительное число для шага!", "Ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // ======================================================================
            // 2. СОХРАНЕНИЕ ПРИВЯЗОК ОЦЕФРОВКИ ОСЕЙ (Универсальная табличная база)
            // ======================================================================
            if (_targetTable is TableVariableViewModelBase tableVar)
            {
                tableVar.BoundAxisX = ComboAxisX.SelectedItem as CurveVariableViewModel;
                tableVar.BoundInputX = ComboInputX.SelectedItem as ScalarVariableViewModel;

                // Если это тяжелая 3D-матрица — подтягиваем еще и вертикальную ось Y [1.14]
                if (tableVar is Map3DVariableViewModel map3D)
                {
                    map3D.BoundAxisY = ComboAxisY.SelectedItem as CurveVariableViewModel;
                    map3D.BoundInputY = ComboInputY.SelectedItem as ScalarVariableViewModel;
                }
            }
            // ======================================================================
            // 3. АВТОМАТИЗАЦИЯ ВЫВОДА ДОП-ПАНЕЛЕЙ (ПРИЦЕЛ И 3D-РЕЛЬЕФ)
            // ======================================================================
            var mainVm = System.Windows.Application.Current?.MainWindow?.DataContext as ViewModels.MainViewModel;
            if (mainVm != null && mainVm.ActiveWidgets != null)
            {
                // Управление радаром и 3D-поверхностью на основе чекбоксов [1.14]
                _targetWidget.ShowRadarTracker = CheckShowRadar.IsChecked == true;
                _targetWidget.Show3DSurface = CheckShow3D.IsChecked == true;

                // --- УПРАВЛЕНИЕ ПЛАВАЮЩИМ ПРИЦЕЛОМ-РАДАРОМ ---
                _targetWidget.ShowRadarTracker = CheckShowRadar.IsChecked == true;
                var existingRadar = mainVm.ActiveWidgets.FirstOrDefault(w => w.DataSource == _targetTable && w.ControlView == "RadarTracker");

                if (_targetWidget.ShowRadarTracker)
                {
                    if (existingRadar == null)
                    {
                        mainVm.ActiveWidgets.Add(new WidgetViewModel(_targetTable)
                        {
                            //DataSource = _targetTable,
                            ControlView = "RadarTracker",
                            Left = _targetWidget.Left + _targetWidget.Width + 20,
                            Top = _targetWidget.Top,
                            Width = 220,
                            Height = 220
                        });
                    }
                }
                else if (existingRadar != null)
                {
                    mainVm.ActiveWidgets.Remove(existingRadar);
                }
                // --- УПРАВЛЕНИЕ 3D-РЕЛЬЕФОМ HELIX ---
                _targetWidget.Show3DSurface = CheckShow3D.IsChecked == true;
                var existing3D = mainVm.ActiveWidgets.FirstOrDefault(w => w.DataSource == _targetTable && w.ControlView == "Matrix3DSurface");

                if (_targetWidget.Show3DSurface)
                {
                    if (existing3D == null)
                    {
                        mainVm.ActiveWidgets.Add(new WidgetViewModel(_targetTable)
                        {
                            //DataSource = _targetTable,
                            ControlView = "Matrix3DSurface",
                            Left = _targetWidget.Left,
                            Top = _targetWidget.Top + _targetWidget.Height + 20,
                            Width = 400,
                            Height = 300
                        });
                    }
                }
                else if (existing3D != null)
                {
                    mainVm.ActiveWidgets.Remove(existing3D);
                }
            } // Конец блока mainVm
              // ======================================================================
              // 4. ПАРСИНГ КРИТИЧЕСКИХ ЛИМИТОВ ДАТЧИКОВ ТЕЛЕМЕТРИИ
              // ======================================================================
            if (_targetTable != null && !_targetTable.IsParam)
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;

                _targetTable.MinLimit = (float)(double.TryParse(TextMinLimit.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, inv, out double min) ? min : double.NegativeInfinity);
                _targetTable.MaxLimit = (float)(double.TryParse(TextMaxLimit.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, inv, out double max) ? max : double.PositiveInfinity);

                if (double.TryParse(TextScaleMin.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, inv, out double sMin)) _targetTable.ScaleMin = (float)sMin;
                if (double.TryParse(TextScaleMax.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, inv, out double sMax)) _targetTable.ScaleMax = (float)sMax;

                _targetWidget.EnableVisualAlarm = CheckEnableVisualAlarm.IsChecked == true;
            }
            // ======================================================================
            // 5. ВЫБОР ГРАФИЧЕСКОГО СТИЛЯ И ЗАКРЫТИЕ ОКНА
            // ======================================================================
            if (ComboStyle.Visibility == System.Windows.Visibility.Visible && ComboStyle.SelectedIndex >= 0)
            {
                _targetWidget.ControlView = ComboStyle.SelectedIndex switch
                {
                    0 => _targetTable.IsParam ? "TextBox" : "Digital",
                    1 => "SliderHorizontal",
                    2 => "SliderVertical",
                    3 => "GaugeCircular270",
                    4 => "GaugeArc120",
                    5 => "TimePlot",
                    _ => "TextBox"
                };
            }

            if (_targetTable != null && !_targetTable.IsParam) _targetWidget.RefreshAlarmTriangles();
            //if (_targetTable is Map3DVariableViewModel)                 _targetWidget.
                _targetWidget.IsVertical = RadioVertical.IsChecked == true;
            DialogResult = true;
        } // 🔥 Финал метода ApplyButton_Click!


        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
