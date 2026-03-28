using System.Windows;
using Implementador.Data;
using Microsoft.Data.SqlClient;

namespace Implementador.Presentation.Dialogs
{
    public partial class ConnectionWindow : Window
    {
        public string? SelectedConnection { get; private set; }
        public string? WarningMessage { get; set; }

        public ConnectionWindow()
        {
            InitializeComponent();
            DataContext = this;
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
                var fastConnectionString = WithShortTimeout(connectionString, 4);
                using var db = new AppDbContext(fastConnectionString);
                db.EnsureConnection();
            }
            catch (SqlException ex)
            {
                var mensaje = ex.Number switch
                {
                    // Base de datos no existe o no accesible
                    4060 or 911 => $"La base de datos especificada no existe o no es accesible.\nVerifique el nombre de la base de datos.",
                    // Login fallido
                    18456 or 18452 => "Las credenciales de acceso son incorrectas.\nVerifique el usuario y la contraseña.",
                    // Servidor no encontrado o timeout
                    53 or -2 or 10060 => "No se pudo alcanzar el servidor.\nVerifique el nombre del servidor y que esté disponible en la red.",
                    // Otros
                    _ => "No se pudo conectar. Verifique todos los parámetros del connection string."
                };
                DialogService.Show(mensaje, "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            catch (Exception)
            {
                DialogService.Show(
                    "No se pudo conectar. Verifique que el connection string sea válido.",
                    "Error de conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            SelectedConnection = connectionString;
            DialogResult = true;
            Close();
        }

        private static string WithShortTimeout(string connectionString, int timeoutSeconds)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = timeoutSeconds };
                return builder.ConnectionString;
            }
            catch
            {
                return connectionString;
            }
        }
    }
}


