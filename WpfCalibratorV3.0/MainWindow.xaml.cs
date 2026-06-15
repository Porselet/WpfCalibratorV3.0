using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfCalibrator.Models;
using WpfCalibrator.Services;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator;

public partial class MainWindow : Window
{


    public MainWindow()
    {
        // Оставляем ОДИН вызов инициализации компонентов интерфейса
        InitializeComponent();

        // Собираем зависимости вручную (Pure DI)
        var configManager = new ConfigurationManager();
        var commService = new CommunicationService();

        // Инициализируем вьюмодель, передавая ей созданные сервисы
        var viewModel = new MainViewModel(commService, configManager);

        // Привязываем DataContext к главному окну. 
        // Все вложенные элементы (панель и холст) унаследуют его автоматически!
        DataContext = viewModel;
    }


    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // Базовый обработчик клика по дереву (при необходимости)
    }

    private void GlobalWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var mainVm = this.DataContext as MainViewModel;
        if (mainVm == null) return;

        var activeWidget = mainVm.ActiveWidgets.FirstOrDefault(w => w.ControlView == "MatrixTable");
        var activeTable = activeWidget?.DataSource;

        // Если таблицы нет — ничего не делаем
        if (activeTable == null || activeWidget == null) return;

        // Если мы УЖЕ пишем текст внутри TextBox, и нажата НЕ клавиша Enter — 
        // даем калибровщику спокойно дописать число (стрелочки внутри TextBox будут двигать курсор по цифрам)
        if (activeTable.IsEditing && e.Key != Key.Enter) return;

        bool handled = false;

        switch (e.Key)
        {
            // 1. СТРЕЛОЧКИ ГОНЯЮТ СИНЮЮ РАМКУ ПО СЕТКЕ
            case Key.Up:
                activeTable.SelectedRow--;
                handled = true;
                break;
            case Key.Down:
                activeTable.SelectedRow++;
                handled = true;
                break;
            case Key.Left:
                activeTable.SelectedCol--;
                handled = true;
                break;
            case Key.Right:
                activeTable.SelectedCol++;
                handled = true;
                break;

            // 2. ПРОБЕЛ — ПРЫЖОК НА ЗЕЛЕНЫЙ НЕОН ДВИГАТЕЛЯ
            case Key.Space:
                if (activeTable.ActiveRowIndex >= 0 && activeTable.ActiveColIndex >= 0)
                {
                    activeTable.SelectedRow = activeTable.ActiveRowIndex;
                    activeTable.SelectedCol = activeTable.ActiveColIndex;
                    handled = true;
                }
                break;

            // 3. PAGE UP — ПОДРУЛИВАНИЕ В UART С ЗАДАННЫМ ШАГОМ
            case Key.PageUp:
                float currentValUp = (float)activeTable.MatrixData[activeTable.SelectedRow, activeTable.SelectedCol];
                float newValUp = currentValUp + activeWidget.IncrementStep;
                activeTable.UpdateMatrixValue(activeTable.SelectedRow, activeTable.SelectedCol, newValUp);
                handled = true;
                break;

            // 4. PAGE DOWN — УМЕНЬШЕНИЕ В UART С ЗАДАННЫМ ШАГОМ
            case Key.PageDown:
                float currentValDn = (float)activeTable.MatrixData[activeTable.SelectedRow, activeTable.SelectedCol];
                float newValDn = currentValDn - activeWidget.IncrementStep;
                activeTable.UpdateMatrixValue(activeTable.SelectedRow, activeTable.SelectedCol, newValDn);
                handled = true;
                break;
            // 5. МГНОВЕННЫЙ ВВОД ЦИФР ПО ПЕРВОМУ НАЖАТИЮ (MoTeC-Style)
            // 5. МГНОВЕННЫЙ ВВОД ЦИФР ПО ПЕРВОМУ НАЖАТИЮ (MoTeC-Style с автоочисткой)
            case Key.D0:
            case Key.D1:
            case Key.D2:
            case Key.D3:
            case Key.D4:
            case Key.D5:
            case Key.D6:
            case Key.D7:
            case Key.D8:
            case Key.D9:
            case Key.NumPad0:
            case Key.NumPad1:
            case Key.NumPad2:
            case Key.NumPad3:
            case Key.NumPad4:
            case Key.NumPad5:
            case Key.NumPad6:
            case Key.NumPad7:
            case Key.NumPad8:
            case Key.NumPad9:
            case Key.OemMinus:
            case Key.Subtract:
            case Key.OemPeriod:
            case Key.Decimal:

                // Находим конкретную ячейку, на которой сейчас стоит наш синий маркер
                if (activeTable.MatrixCells != null)
                {
                    var targetCell = activeTable.MatrixCells.FirstOrDefault(c => c.Row == activeTable.SelectedRow && c.Col == activeTable.SelectedCol);
                    if (targetCell != null)
                    {
                        // Включаем режим ввода
                        activeTable.IsEditing = true;

                        // ОЧИЩАЕМ СТАРЫЙ МУСОР: Сразу затираем старое значение в ячейке,
                        // чтобы новая цифра не приписывалась в начало, а замещала текст!
                        targetCell.ValueText = string.Empty;
                    }
                }
                // Оставляем handled = false, чтобы Windows сама впечатала нажатый символ в очищенное поле
                break;


        }

        // Если это была одна из наших горячих клавиш MoTeC —
        // ЖЕСТКО ГАСИМ СОБЫТИЕ, чтобы оно не улетело наверх переключать рабочие столы!
        if (handled)
        {
            e.Handled = true;
        }
    }

    // ==================== ЛОГИКА DROP (СБРОС НА ХОЛСТ) ====================
}
