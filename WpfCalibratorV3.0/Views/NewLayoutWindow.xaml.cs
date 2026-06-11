using System.Windows;

namespace WpfCalibrator.Views
{
    public partial class NewLayoutWindow : Window
    {
        // Публичное свойство, откуда MainViewModel заберет введенное имя
        public string ResultResult { get; private set; } = string.Empty;

        public NewLayoutWindow()
        {
            InitializeComponent();
            // Сразу ставим фокус в текстовое поле, чтобы оператор мог сразу писать
            LayoutNameInput.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(LayoutNameInput.Text))
            {
                ResultResult = LayoutNameInput.Text.Trim();
                DialogResult = true; // Закрывает окно и возвращает true в ShowDialog()
            }
            else
            {
                MessageBox.Show("Имя экрана не может быть пустым!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
