using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfCalibrator.Services;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Привязываем ViewModel к окну
            DataContext = new MainViewModel(
                new CommunicationService(),
                new ConfigurationManager(),
                new DashboardManager() // Или заглушка NullDashboardManager
            );



        }

        // Обработчик смены устройства в дереве навигации
        private void TreeView_SelectedItemChanged(object sender, RoutedEventArgs e)
        {
            // TODO: Реализуйте логику выбора устройства
            // Например, вызовите метод MainViewModel.OnDeviceChanged()
        }
    }
}