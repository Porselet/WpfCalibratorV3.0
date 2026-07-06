using System.Configuration;
using System.Data;
using System.Windows;

namespace WpfCalibrator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Проверяем, есть ли в аргументах запуска наш ключ
            if (e.Args.Contains("-demo"))
            {
                WpfCalibrator.Services.CommunicationService.IsDemoMode = true;
            }
        }
    }

}
