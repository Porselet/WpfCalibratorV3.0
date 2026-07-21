using System.Configuration;
using System.Data;
using System.Windows;
using WpfCalibrator.Services;

namespace WpfCalibrator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Создаем батник для демо режима. 
            EnvironmentPreparer.EnsureScriptsCreated();

            base.OnStartup(e);

            // Проверяем, есть ли в аргументах запуска наш ключ
            if (e.Args != null && Array.IndexOf(e.Args, "-demo") >= 0)
            {
                WpfCalibrator.Services.CommunicationService.IsDemoMode = true;
            }
        }
    }

}
