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



    // ==================== ЛОГИКА DROP (СБРОС НА ХОЛСТ) ====================
}
