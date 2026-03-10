using System.Windows;
using ImplementadorCUAD.Data;

namespace ImplementadorCUAD
{
    public partial class ConnectionWindow : Window
    {
        public string? SelectedConnectionString { get; private set; }

        public ConnectionWindow()
        {
            InitializeComponent();
        }

        private void OnConnectClick(object sender, RoutedEventArgs e)
        {
            var connectionString = ConnectionStringTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                MessageBox.Show(
                    "Debe ingresar un connection string.",
                    "Conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var db = new AppDbContext(connectionString);
                db.EnsureConnection();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo establecer la conexión.\n\n{ex.Message}",
                    "Conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            SelectedConnectionString = connectionString;
            DialogResult = true;
            Close();
        }
    }
}

