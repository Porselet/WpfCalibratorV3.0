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
        var dashboardManager = new DashboardManager();
        // Инициализируем вьюмодель, передавая ей созданные сервисы
        var viewModel = new MainViewModel(configManager, dashboardManager);

        // Привязываем DataContext к главному окну. 
        // Все вложенные элементы (панель и холст) унаследуют его автоматически!
        DataContext = viewModel;

        // ГЛОБАЛЬНЫЙ ПЕРЕХВАТ КЛИКА МЫШИ ДЛЯ СМЕНЫ АКТИВНОГО ВИДЖЕТА
        // ГЛОБАЛЬНЫЙ ПЕРЕХВАТ КЛИКА МЫШИ ДЛЯ СМЕНЫ АКТИВНОГО ВИДЖЕТА И СЛОЯ ГЛУБИНЫ
        this.PreviewMouseLeftButtonDown += (s, e) =>
        {
            if (this.DataContext is MainViewModel vm)
            {
                var element = e.OriginalSource as System.Windows.FrameworkElement;
                var clickedWidget = element?.DataContext as WidgetViewModel;

                if (clickedWidget != null)
                {
                    // 1. УПРАВЛЕНИЕ СЛОЯМИ (Z-Index Хак):
                    // Находим, какой максимальный ZIndex сейчас есть среди всех окон на холсте
                    int maxCurrentZ = vm.ActiveWidgets.Count > 0 ? vm.ActiveWidgets.Max(w => w.ZIndex) : 0;

                    // Назначаем кликнутому виджету слой еще выше, чтобы он вылетел на самый передний план!
                    clickedWidget.ZIndex = maxCurrentZ + 1;

                    // 2. УПРАВЛЕНИЕ НАВИГАЦИЕЙ КЛАВИАТУРЫ (Наш прошлый Шаг 79)
                    // Переносим жирную неоновую рамку фокуса только на параметры
                    if (clickedWidget.DataSource != null && clickedWidget.DataSource.IsParam)
                    {
                        foreach (var w in vm.ActiveWidgets) w.IsActiveWidget = false;
                        clickedWidget.IsActiveWidget = true;
                    }
                }
            }
        };
    }


    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // Базовый обработчик клика по дереву (при необходимости)
    }

    private void MenuUartMonitor_Click(object sender, RoutedEventArgs e)
    {
        // Открываем наше новое инженерное окно синглтоном
        WpfCalibrator.Views.UartMonitorWindow.ShowWindow();
    }

    /// <summary>
    /// Глобальный перехватчик клавиатуры верхнего уровня Windows.
    /// </summary>
    private void GlobalWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 1. ПРЕДОХРАНИТЕЛЬ: Если фокус стоит в левом выпадающем списке портов — 
        // не трогаем клавиатуру, даем инженеру штатно выбрать COM-порт стрелками
        if (e.OriginalSource is System.Windows.Controls.ComboBox) return;

        if (this.DataContext is ViewModels.MainViewModel vm)
        {
            // 2. Находим, какой виджет на холсте сейчас выбран инженером и обведен неоновым фокусом
            var activeWidget = vm.ActiveWidgets.FirstOrDefault(w => w.IsActiveWidget);

            if (activeWidget != null)
            {
                // 🔥 НАШ ЕДИНЫЙ ДРАЙВЕР В ДЕЙСТВИИ:
                // Передаем управление бесфокусному контроллеру ввода.
                // Если он распознал клавишу (цифру, стрелку, Enter, Esc, PageUp) и обработал её — 
                // мы жестко говорим операционной системе: "e.Handled = true", прерывая дальнейший сбой фокуса!
                if (Services.InputController.ProcessKeyDown(activeWidget, e))
                {
                    e.Handled = true;
                    return;
                }
            }
        }
    }


}
