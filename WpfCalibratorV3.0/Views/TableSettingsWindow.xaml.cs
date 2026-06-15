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

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
