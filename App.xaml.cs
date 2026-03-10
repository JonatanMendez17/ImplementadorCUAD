using System.Windows;
using ImplementadorCUAD.Data;
using ImplementadorCUAD.Infrastructure;
using ImplementadorCUAD.Services;
using Microsoft.Data.SqlClient;

namespace ImplementadorCUAD
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Paso 1: crear y mostrar siempre la ventana principal.
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();

            // Paso 2: intentar usar la configuración existente de Configuracion.xml / app.config (sin modal).
            var initialCuadConnection = ConnectionSettings.CuadConnectionString;
            var hasInitialConfig = !string.IsNullOrWhiteSpace(initialCuadConnection);

            if (hasInitialConfig)
            {
                try
                {
                    // Probar la conexión con un timeout corto para no demorar el arranque si la config es inválida.
                    var testConnectionString = WithShortTimeout(initialCuadConnection, 3);
                    using (var db = new AppDbContext(testConnectionString))
                    {
                        db.EnsureConnection();
                    }

                    // Conexión a CUAD OK: inicializar datos en el ViewModel y no mostrar el modal.
                    if (mainWindow.DataContext is ViewModels.MainViewModel vmWithConfig)
                    {
                        vmWithConfig.InitializeAfterConnectionEstablished();
                    }

                    return;
                }
                catch
                {
                    // Si la conexión falla aún con configuración existente, caemos al flujo del modal.
                }
            }

            // Paso 3: no hay configuración válida de CUAD o la conexión falló: pedir al usuario el connection string.
            var configWindow = new ConnectionWindow
            {
                Owner = mainWindow
            };
            var result = configWindow.ShowDialog();

            if (result != true || string.IsNullOrWhiteSpace(configWindow.SelectedConnectionString))
            {
                Shutdown();
                return;
            }

            var userConnectionString = configWindow.SelectedConnectionString;

            // 3.1) Validar que el connection string proporcionado funcione contra CUAD.
            try
            {
                var testConnectionString = WithShortTimeout(userConnectionString, 3);
                using (var db = new AppDbContext(testConnectionString))
                {
                    db.EnsureConnection();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo establecer la conexión con la cadena ingresada.\n\n{ex.Message}",
                    "Error de conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // 3.2) Persistir el connection string de CUAD en Configuracion.xml para futuras ejecuciones.
            new ConexionesConfigService().SetCuadConnectionString(userConnectionString);

            // 3.3) Verificar que, leído desde Configuracion.xml a través de ConnectionSettings/AppDbContext(), también funcione.
            try
            {
                var verifyConnectionString = WithShortTimeout(ConnectionSettings.CuadConnectionString, 3);
                using (var db = new AppDbContext(verifyConnectionString))
                {
                    db.EnsureConnection();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"La conexión sigue fallando incluso luego de actualizar Configuracion.xml.\n\n{ex.Message}",
                    "Error de conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // 3.4) Conexión OK luego de actualizar XML: inicializar datos en el ViewModel.
            if (mainWindow.DataContext is ViewModels.MainViewModel vm)
            {
                vm.InitializeAfterConnectionEstablished();
            }
        }

        /// <summary>
        /// Devuelve una copia del connection string con un Connect Timeout reducido.
        /// Si el string es inválido, se devuelve tal cual sin modificar.
        /// </summary>
        private static string WithShortTimeout(string connectionString, int timeoutSeconds)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString)
                {
                    ConnectTimeout = timeoutSeconds
                };
                return builder.ConnectionString;
            }
            catch
            {
                return connectionString;
            }
        }
    }
}
