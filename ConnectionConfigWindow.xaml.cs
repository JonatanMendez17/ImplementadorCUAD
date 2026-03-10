using System;
using System.Windows;
using ImplementadorCUAD.Infrastructure;
using ImplementadorCUAD.Services;
using Microsoft.Data.SqlClient;

namespace ImplementadorCUAD
{
    public partial class ConnectionConfigWindow : Window
    {
        public ConnectionConfigWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var actual = new ConexionesConfigService().GetCuadConnectionString();
                if (!string.IsNullOrWhiteSpace(actual))
                {
                    ConnectionStringTextBox.Text = actual;
                    ConnectionStringTextBox.CaretIndex = actual.Length;
                }
            }
            catch (Exception ex)
            {
                ErrorTextBlock.Text =
                    "No se pudo leer la configuración actual de la conexión.\n\n" +
                    $"Detalle técnico: {ex.Message}";
                ErrorTextBlock.Visibility = Visibility.Visible;
            }

            ConnectionStringTextBox.Focus();
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            ErrorTextBlock.Visibility = Visibility.Collapsed;
            ErrorTextBlock.Text = string.Empty;

            var connectionString = ConnectionStringTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                ErrorTextBlock.Text = "La cadena de conexión no puede estar vacía.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                }

                var service = new ConexionesConfigService();
                service.SetCuadConnectionString(connectionString);
                ConnectionSettings.Reload();

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ErrorTextBlock.Text =
                    "No se pudo conectar con la cadena indicada. Verifique los datos e intente nuevamente.\n\n" +
                    $"Detalle técnico: {ex.Message}";
                ErrorTextBlock.Visibility = Visibility.Visible;
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

