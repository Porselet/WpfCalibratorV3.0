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
        InitializeComponent();

        InitializeComponent();

        var configManager = new ConfigurationManager();
        var commService = new CommunicationService();
        var viewModel = new MainViewModel(commService, configManager);

        // Привязываем DataContext к самому главному окну
        DataContext = viewModel;

        // СИШНЫЙ ХАК: Явно передаем этот же указатель во внутренние модули,
        // чтобы они не теряли видимость общих коллекций
        LeftPanel.DataContext = viewModel;    // Проверьте имена компонентов в вашем XAML
        CentralCanvas.DataContext = viewModel;
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // Базовый обработчик клика по дереву (при необходимости)
    }



    // ==================== ЛОГИКА DROP (СБРОС НА ХОЛСТ) ====================
}
