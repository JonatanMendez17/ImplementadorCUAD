using System.Windows;
using Implementador.Data;

namespace Implementador.Presentation.Dialogs
{
    public partial class ConnectionWindow : Window
    {
        public string? SelectedConnection { get; private set; }

        public ConnectionWindow()
        {
            InitializeComponent();
        }

        private void OnConnectClick(object sender, RoutedEventArgs e)
        {
            var connectionString = ConnectionStringTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                DialogService.Show(
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
                DialogService.Show(
                    $"No se pudo establecer la conexión.\n\n{ex.Message}",
                    "Conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            SelectedConnection = connectionString;
            DialogResult = true;
            Close();
        }

    }
}


