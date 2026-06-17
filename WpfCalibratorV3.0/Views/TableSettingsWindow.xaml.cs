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
        private readonly VariableViewModel _targetTable;

        // Конструктор теперь принимает сам виджет WidgetViewModel
        public TableSettingsWindow(WidgetViewModel targetWidget, List<VariableViewModel> allVariables)
        {
            InitializeComponent();
            _targetWidget = targetWidget;
            _targetTable = targetWidget.DataSource;

            // Выводим имя калибруемой таблицы в заголовок окна
            TableNameText.Text = _targetTable.Name;

            // Подставляем текущий сохраненный шаг изменения в текстовое поле
            TextIncrementStep.Text = _targetWidget.IncrementStep.ToString("F3", CultureInfo.InvariantCulture);

            // Разделяем кучу переменных на параметры (для осей) и телеметрию (для датчиков)
            var parameterVars = allVariables.Where(v => v.IsParam).ToList();
            var telemetryVars = allVariables.Where(v => !v.IsParam).ToList();

            // Заполняем списки выбора осей
            ComboAxisX.ItemsSource = parameterVars;
            ComboAxisY.ItemsSource = parameterVars;

            // Заполняем списки выбора датчиков
            ComboInputX.ItemsSource = telemetryVars;
            ComboInputY.ItemsSource = telemetryVars;

            // Подставляем уже существующие привязки
            if (_targetTable.BoundAxisX != null) ComboAxisX.SelectedItem = parameterVars.FirstOrDefault(v => v.Name == _targetTable.BoundAxisX.Name);
            if (_targetTable.BoundAxisY != null) ComboAxisY.SelectedItem = parameterVars.FirstOrDefault(v => v.Name == _targetTable.BoundAxisY.Name);
            if (_targetTable.BoundInputX != null) ComboInputX.SelectedItem = telemetryVars.FirstOrDefault(v => v.Name == _targetTable.BoundInputX.Name);
            if (_targetTable.BoundInputY != null) ComboInputY.SelectedItem = telemetryVars.FirstOrDefault(v => v.Name == _targetTable.BoundInputY.Name);

            // 1. ВНУТРИ КОНСТРУКТОРА TableSettingsWindow (в самый конец, под шаг):
            CheckShowRadar.IsChecked = _targetTable.ShowRadarTracker;
            // ВНУТРИ МЕТОДА ИНИЦИАЛИЗАЦИИ ДАННЫХ ОКНА:
            if (_targetWidget.IsVertical)
            {
                RadioVertical.IsChecked = true;
            }
            else
            {
                RadioHorizontal.IsChecked = true;
            }
            // ВНУТРИ МЕТОДА ИНИЦИАЛИЗАЦИИ ДАННЫХ ОКНА:
            if (!_targetTable.IsParam)
            {
                // Если минимум равен минус бесконечности — оставляем текстовое поле пустым, 
                // иначе выводим красивое число с одним знаком после запятой
                TextMinLimit.Text = float.IsNegativeInfinity(_targetTable.MinLimit) ? string.Empty : _targetTable.MinLimit.ToString("F1");

                // Аналогично для максимума
                TextMaxLimit.Text = float.IsPositiveInfinity(_targetTable.MaxLimit) ? string.Empty : _targetTable.MaxLimit.ToString("F1");
            }


            // ВНУТРИ МЕТОДА ИНИЦИАЛИЗАЦИИ ДАННЫХ ОКНА НАСТРОЕК:


            // ОБНОВЛЕННАЯ МАТРИЦА ХАМЕЛЕОНА (С поддержкой СТИЛЕЙ, ЛИМИТОВ и РАДАР-фильтра)

            // 1. По умолчанию гасим вообще ВСЕ блоки, включая новые
            LabelAxisX.Visibility = Visibility.Collapsed; ComboAxisX.Visibility = Visibility.Collapsed;
            LabelInputX.Visibility = Visibility.Collapsed; ComboInputX.Visibility = Visibility.Collapsed;
            LabelAxisY.Visibility = Visibility.Collapsed; ComboAxisY.Visibility = Visibility.Collapsed;
            LabelInputY.Visibility = Visibility.Collapsed; ComboInputY.Visibility = Visibility.Collapsed;
            LabelOrientation.Visibility = Visibility.Collapsed; PanelOrientation.Visibility = Visibility.Collapsed;
            TextIncrementStep.Visibility = Visibility.Collapsed; CheckShowRadar.Visibility = Visibility.Collapsed;
            // Добавь к остальным Collapsed-строкам в начале метода:
            TextIncrementStep.Visibility = Visibility.Collapsed;
            LabelIncrementStep.Visibility = Visibility.Collapsed; // Гасим надпись шага

            CheckShowRadar.Visibility = Visibility.Collapsed;
            LabelShowRadar.Visibility = Visibility.Collapsed;    // Гасим надпись радара

            // Гасим новые блоки стилей и лимитов
            LabelStyle.Visibility = Visibility.Collapsed; ComboStyle.Visibility = Visibility.Collapsed;
            LabelLimits.Visibility = Visibility.Collapsed; PanelLimits.Visibility = Visibility.Collapsed;
            LabelScaleRange.Visibility = Visibility.Collapsed;
            PanelScaleRange.Visibility = Visibility.Collapsed;


            LabelEnableVisualAlarm.Visibility = Visibility.Collapsed;
            CheckEnableVisualAlarm.Visibility = Visibility.Collapsed;


            // 2. Распознаем тип прибора
            if (_targetWidget.ControlView == "RadarTracker")
            {
                // === ХАК ДЛЯ РАДАРА: Окно полностью пустое, выводим только сообщение ===
                TableNameText.Text = $"{_targetTable.Name} (Прицел)";
                this.Height = 120; // Крошечное аккуратное окошко, где написано, что настроек пока нет
            }
            else if (_targetTable.IsParam)
            {
                // === ЭТО КАЛИБРОВОЧНЫЙ ПАРАМЕТР ===
                TableNameText.Text = _targetTable.Name;

                if (_targetTable.Rows > 1 && _targetTable.Cols > 1)
                {
                    // ТИП 1: 2D-Карта
                    LabelAxisX.Visibility = Visibility.Visible; ComboAxisX.Visibility = Visibility.Visible;
                    LabelInputX.Visibility = Visibility.Visible; ComboInputX.Visibility = Visibility.Visible;
                    LabelAxisY.Visibility = Visibility.Visible; ComboAxisY.Visibility = Visibility.Visible;
                    LabelInputY.Visibility = Visibility.Visible; ComboInputY.Visibility = Visibility.Visible;
                    TextIncrementStep.Visibility = Visibility.Visible;
                    CheckShowRadar.Visibility = Visibility.Visible;
                    this.Height = 360;
                }
                else if ((_targetTable.Rows == 1 && _targetTable.Cols > 1) || (_targetTable.Rows > 1 && _targetTable.Cols == 1))
                {
                    // ТИП 2: 1D-Таблица / Ось
                    LabelAxisX.Visibility = Visibility.Visible; ComboAxisX.Visibility = Visibility.Visible;
                    LabelInputX.Visibility = Visibility.Visible; ComboInputX.Visibility = Visibility.Visible;
                    LabelOrientation.Visibility = Visibility.Visible; PanelOrientation.Visibility = Visibility.Visible;
                    TextIncrementStep.Visibility = Visibility.Visible;
                    CheckShowRadar.Visibility = Visibility.Visible;
                    this.Height = 290;
                }
                else
                {
                    // ТИП 3: Скалярная константа
                    TextIncrementStep.Visibility = Visibility.Visible;
                    TextIncrementStep.Visibility = Visibility.Visible;
                    LabelIncrementStep.Visibility = Visibility.Visible; // Включаем надпись
                    // Включаем выбор стиля (TextBox или Slider) для константы!
                    LabelStyle.Visibility = Visibility.Visible; ComboStyle.Visibility = Visibility.Visible;

                    this.Height = 200;
                }
            }
            else
            {
                // === ТИП 4: СИГНАЛ ТЕЛЕМЕТРИИ / ДАТЧИК ===
                TableNameText.Text = _targetTable.Name;

                // Включаем выбор ГРАФИЧЕСКОГО стиля (Digital, Gauge, Slider, TimePlot)
                LabelStyle.Visibility = Visibility.Visible;
                ComboStyle.Visibility = Visibility.Visible;

                // Включаем ввод КРИТИЧЕСКИХ МАРКЕРОВ Мин/Макс
                LabelLimits.Visibility = Visibility.Visible;
                PanelLimits.Visibility = Visibility.Visible;

                // ВНУТРИ ВЕТКИ ДАТЧИКА ТЕЛЕМЕТРИИ:
                LabelScaleRange.Visibility = Visibility.Visible;
                PanelScaleRange.Visibility = Visibility.Visible;

                // Выводим текущие значения масштаба из памяти в текстовые поля
                TextScaleMin.Text = _targetTable.ScaleMin.ToString("F1");
                TextScaleMax.Text = _targetTable.ScaleMax.ToString("F1");

                // ВНУТРИ МЕТОДА ИНИЦИАЛИЗАЦИИ (для Датчиков и Скаляров):
                LabelStyle.Visibility = Visibility.Visible;
                ComboStyle.Visibility = Visibility.Visible;
                // ВНУТРИ ВЕТКИ ДАТЧИКА ТЕЛЕМЕТРИИ:
                LabelEnableVisualAlarm.Visibility = Visibility.Visible;
                CheckEnableVisualAlarm.Visibility = Visibility.Visible;

                // Выводим текущее состояние флага из ОЗУ виджета в CheckBox
                CheckEnableVisualAlarm.IsChecked = _targetWidget.EnableVisualAlarm;

                // Переводим текущий вид виджета в выбранную строчку комбобокса
                switch (_targetWidget.ControlView)
                {
                    case "TextBox":
                    case "Digital": ComboStyle.SelectedIndex = 0; break;
                    case "SliderHorizontal": ComboStyle.SelectedIndex = 1; break;
                    case "SliderVertical": ComboStyle.SelectedIndex = 2; break;
                    case "GaugeCircular270": ComboStyle.SelectedIndex = 3; break;
                    case "GaugeArc120": ComboStyle.SelectedIndex = 4; break;
                    case "TimePlot": ComboStyle.SelectedIndex = 5; break;
                    default: ComboStyle.SelectedIndex = 0; break;
                }

                // Увеличим общую высоту окна хамелеона для датчика, чтобы всё влезло без накладок:
                this.Height = 280;
            }



        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Валидация и сохранение шага изменения параметров
            string stepText = TextIncrementStep.Text.Replace(',', '.'); // нормализуем под инвариантную культуру
            if (float.TryParse(stepText, NumberStyles.Any, CultureInfo.InvariantCulture, out float parsedStep) && parsedStep > 0)
            {
                _targetWidget.IncrementStep = parsedStep;
            }
            else
            {
                MessageBox.Show("Введите корректное положительное число для шага изменения!", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Записываем выбранные связи осей и датчиков напрямую во вьюмодель нашей таблицы
            _targetTable.BoundAxisX = ComboAxisX.SelectedItem as VariableViewModel;
            _targetTable.BoundInputX = ComboInputX.SelectedItem as VariableViewModel;
            _targetTable.BoundAxisY = ComboAxisY.SelectedItem as VariableViewModel;
            _targetTable.BoundInputY = ComboInputY.SelectedItem as VariableViewModel;

            _targetTable.ShowRadarTracker = CheckShowRadar.IsChecked == true;

            // СРАЗУ ПОСЛЕ СТРОКИ: _targetTable.ShowRadarTracker = CheckShowRadar.IsChecked == true;

            // АВТОМАТИЗАЦИЯ ПРИЦЕЛА: Находим MainViewModel через главное окно
            if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
            {
                // Ищем, выведен ли уже радар для этой таблицы на рабочий стол
                var existingRadar = mainVm.ActiveWidgets.FirstOrDefault(w =>
                    w.ControlView == "RadarTracker" && w.DataSource?.Name == _targetTable.Name);

                if (_targetTable.ShowRadarTracker)
                {
                    // Если галочка поставлена, а радара на столе еще нет — создаем его!
                    if (existingRadar == null)
                    {
                        var radarWidget = new WidgetViewModel
                        {
                            DataSource = _targetTable, // Прицел жестко связан с данными этой таблицы
                            ControlView = "RadarTracker", // Специальный тип отображения
                            Left = _targetWidget.Left + _targetWidget.Width + 20, // Появляется справа от таблицы
                            Top = _targetWidget.Top,
                            Width = 220,  // Компактный квадратный прицел-радар
                            Height = 220,
                            IncrementStep = _targetWidget.IncrementStep
                        };

                        mainVm.ActiveWidgets.Add(radarWidget);
                    }
                }
                else
                {
                    // Если галочку убрали — молча стираем прицел-радар с рабочего стола
                    if (existingRadar != null)
                    {
                        mainVm.ActiveWidgets.Remove(existingRadar);
                    }
                }
            }
            // ВНУТРИ МЕТОДА ApplyButton_Click (в самый конец, перед DialogResult = true;):
            if (!_targetTable.IsParam)
            {
                // Безопасно парсим МИНИМУМ. Если поле пустое или ввели мусор — возвращаем минус бесконечность
                if (float.TryParse(TextMinLimit.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float minVal))
                {
                    _targetTable.MinLimit = minVal;
                }
                else
                {
                    _targetTable.MinLimit = float.NegativeInfinity;
                }

                // Безопасно парсим МАКСИМУМ. Если поле пустое — возвращаем плюс бесконечность
                if (float.TryParse(TextMaxLimit.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float maxVal))
                {
                    _targetTable.MaxLimit = maxVal;
                }
                else
                {
                    _targetTable.MaxLimit = float.PositiveInfinity;
                }
                // ВНУТРИ МЕТОДА ApplyButton_Click (внутри условия if (!_targetTable.IsParam)):
                if (float.TryParse(TextScaleMin.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float scaleMinVal))
                {
                    _targetTable.ScaleMin = scaleMinVal;
                }

                if (float.TryParse(TextScaleMax.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float scaleMaxVal))
                {
                    _targetTable.ScaleMax = scaleMaxVal;
                }
                // ВНУТРИ МЕТОДА ApplyButton_Click (внутри условия if (!_targetTable.IsParam)):
                _targetWidget.EnableVisualAlarm = CheckEnableVisualAlarm.IsChecked == true;

            }


            // ВНУТРИ МЕТОДА ApplyButton_Click (перед самым закрытием диалога):
            if (ComboStyle.Visibility == Visibility.Visible && ComboStyle.SelectedIndex >= 0)
            {
                if (_targetTable.IsParam)
                {
                    // Для скалярных параметров у нас по ТЗ всего два вида: Текстбокс или Слайдер
                    _targetWidget.ControlView = (ComboStyle.SelectedIndex == 2 || ComboStyle.SelectedIndex == 1)
                        ? "SliderHorizontal" // или вертикальный, если докрутим
                        : "TextBox";
                }
                else
                {
                    // Для датчиков телеметрии переключаем строго по нашему ТЗ:
                    switch (ComboStyle.SelectedIndex)
                    {
                        case 0: _targetWidget.ControlView = "Digital"; break;
                        case 1: _targetWidget.ControlView = "SliderHorizontal"; break;
                        case 2: _targetWidget.ControlView = "SliderVertical"; break;
                        case 3: _targetWidget.ControlView = "GaugeCircular270"; break;
                        case 4: _targetWidget.ControlView = "GaugeArc120"; break;
                        case 5: _targetWidget.ControlView = "TimePlot"; break;
                    }
                }
            }
            // ВНУТРИ МЕТОДА ApplyButton_Click ПЕРЕД DialogResult = true;:
            if (!_targetTable.IsParam)
            {
                // Заставляем виджет мгновенно пересчитать координаты и сдвинуть треугольники!
                _targetWidget.RefreshAlarmTriangles();
            }


            // ВНУТРИ МЕТОДА ApplyButton_Click (перед закрытием диалога):
            bool isVerticalSelected = RadioVertical.IsChecked == true;

            // Записываем флаг и в саму таблицу (для математики), и в виджет (для XAML-верстки)
            _targetTable.IsVertical = isVerticalSelected;
            _targetWidget.IsVertical = isVerticalSelected;



            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
