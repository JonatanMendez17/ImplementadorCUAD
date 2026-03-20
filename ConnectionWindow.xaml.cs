using System.Windows;
using Microsoft.Data.SqlClient;
using ImplementadorCUAD.Data;
using ImplementadorCUAD.Services;

namespace ImplementadorCUAD
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
                // Validar que la cadena especifique una base de datos (Database / Initial Catalog)
                var builder = new SqlConnectionStringBuilder(connectionString);
                if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
                {
                    MessageBox.Show(
                        "La cadena de conexión no especifica la base de datos (Database o Initial Catalog).\n" +
                        "Ejemplo: Server=...;Database=CUAD;Trusted_Connection=True;",
                        "Conexión",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                using var db = new AppDbContext(builder.ConnectionString);
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}

