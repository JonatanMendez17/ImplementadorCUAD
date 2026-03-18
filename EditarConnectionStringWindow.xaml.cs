using System.IO;
using System.Windows;
using System.Xml.Linq;
using ImplementadorCUAD.Services;
using Microsoft.Data.SqlClient;

namespace ImplementadorCUAD
{
    public partial class EditarConnectionStringWindow : Window
    {
        private readonly string _rutaXml = "Configuracion.xml";

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
                // 1) Probar conexión
                await using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                    await using var command = new SqlCommand("SELECT 1;", connection);
                    await command.ExecuteScalarAsync().ConfigureAwait(false);
                }

                // 2) Guardar en Configuracion.xml (nodo Conexiones/Cuad@connectionString)
                if (!File.Exists(_rutaXml))
                {
                    ErrorTextBlock.Text = "No se encontró el archivo Configuracion.xml junto al ejecutable.";
                    return;
                }

                var document = XDocument.Load(_rutaXml);
                var root = document.Root ?? new XElement("Configuracion");
                if (document.Root == null)
                {
                    document.Add(root);
                }

                var conexiones = root.Element("Conexiones");
                if (conexiones == null)
                {
                    conexiones = new XElement("Conexiones");
                    root.Add(conexiones);
                }

                var cuad = conexiones.Element("Cuad");
                if (cuad == null)
                {
                    cuad = new XElement("Cuad");
                    conexiones.AddFirst(cuad);
                }

                cuad.SetAttributeValue("connectionString", connectionString);

                document.Save(_rutaXml);

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

