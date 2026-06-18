using System;
using System.Windows;

namespace WpfCalibrator.Views
{
    public partial class UartMonitorWindow : Window
    {
        // Статическая ссылка для реализации Синглтона
        private static UartMonitorWindow? _instance;

        public static void ShowWindow()
        {
            if (_instance == null)
            {
                _instance = new UartMonitorWindow();
                _instance.Closed += (s, e) => _instance = null;
                _instance.Show();
            }
            else
            {
                _instance.Activate();
                if (_instance.WindowState == WindowState.Minimized)
                    _instance.WindowState = WindowState.Normal;
            }
        }

        public UartMonitorWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Потокобезопасное добавление новой текстовой строки пакета в монолитный терминал
        /// </summary>
        public static void LogPacket(string direction, string colorHex, string description, byte[] fullPacket)
        {
            // Пробрасываем вызов в главный UI-поток WPF
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                if (_instance == null) return;

                // Защита от переполнения ОЗУ: если текст стал слишком огромным, очищаем терминал
                if (_instance.TerminalTextBox.Text.Length > 50000)
                {
                    _instance.TerminalTextBox.Clear();
                    _instance.TerminalTextBox.AppendText("--- Лог очищен автоматически для экономии памяти ---\n");
                }

                // Формируем чистую текстовую строчку: Время | Направление | Описание | HEX-байты
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string hexBytes = BitConverter.ToString(fullPacket); // Переведет байты в "AA-02-01-..."

                string logLine = $"[{timestamp}] {direction} {description} -> HEX: {hexBytes}\r\n";

                // Дописываем строку в конец текстового поля
                _instance.TerminalTextBox.AppendText(logLine);

                // Если включена автопрокрутка — роняем скролл в самый низ
                if (_instance.CheckAutoScroll.IsChecked == true)
                {
                    _instance.TerminalTextBox.ScrollToEnd();
                }
            }));
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            TerminalTextBox.Clear();
        }
    }
}
