using System.Windows;
using ImplementadorCUAD.Infrastructure;
using ImplementadorCUAD.Services;
using Microsoft.Data.SqlClient;

namespace ImplementadorCUAD
{
    public partial class EditarConnectionStringWindow : Window
    {
        public EditarConnectionStringWindow()
        {
            InitializeComponent();

            var service = new ConexionesConfigService();
            var actual = service.GetCuadConnectionString();
            if (!string.IsNullOrWhiteSpace(actual))
            {
                ConnectionStringTextBox.Text = actual;
            }
        }

        private async void OnProbarYGuardarClick(object sender, RoutedEventArgs e)
        {
            ErrorTextBlock.Text = string.Empty;

            var connectionString = ConnectionStringTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                ErrorTextBlock.Text = "Debe informar un connection string.";
                return;
            }

            try
            {
                await using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                    await using var command = new SqlCommand("SELECT 1;", connection);
                    await command.ExecuteScalarAsync().ConfigureAwait(false);
                }

                new ConexionesConfigService().SetCuadConnectionString(connectionString);
                ConnectionSettings.InvalidateCache();

                MessageBox.Show(
                    "La conexión se probó correctamente y se guardó en Configuracion.xml.\n\nCierre y vuelva a abrir la aplicación para usar la nueva configuración.",
                    "Connection string guardado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (SqlException ex)
            {
                ErrorTextBlock.Text = $"Error de conexión SQL: {ex.Message}";
            }
            catch (Exception ex)
            {
                ErrorTextBlock.Text = $"No se pudo guardar la configuración: {ex.Message}";
            }
        }
    }
}
