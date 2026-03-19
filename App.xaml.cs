using System.Windows;
using Microsoft.Data.SqlClient;
using ImplementadorCUAD.Data;
using ImplementadorCUAD.Infrastructure;
using ImplementadorCUAD.Services;

namespace ImplementadorCUAD
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();

            var initialCuadConnection = ConnectionSettings.CuadConnectionString;
            var hasInitialConfig = !string.IsNullOrWhiteSpace(initialCuadConnection);

            if (hasInitialConfig)
            {
                try
                {
                    var testConnectionString = WithShortTimeout(initialCuadConnection, 3);
                    using (var db = new AppDbContext(testConnectionString))
                    {
                        db.EnsureConnection();
                    }

                    try
                    {
                        if (mainWindow.DataContext is ViewModels.MainViewModel vmWithConfig)
                        {
                            vmWithConfig.InitializeAfterConnectionEstablished();
                        }
                    }
                    catch (SqlException ex)
                    {
                        MessageBox.Show(
                            $"No se pudo inicializar la aplicación con la base seleccionada.\n" +
                            $"Verifique que la base CUAD tenga todas las tablas y vistas requeridas.\n\nDetalle técnico:\n{ex.Message}",
                            "Error al leer CUAD",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Ocurrió un error inesperado al inicializar la aplicación.\n\n{ex.Message}",
                            "Error al iniciar",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    return;
                }
                catch
                {
                    // Si la conexión falla con la configuración existente, caemos al flujo del modal.
                }
            }

            // No hay configuración válida de CUAD o la conexión falló: pedir al usuario el connection string.
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

            // ConnectionWindow ya validó la conexión. Persistir en XML e invalidar cache.
            var userConnectionString = configWindow.SelectedConnectionString;
            new ConexionesConfigService().SetCuadConnectionString(userConnectionString);
            ConnectionSettings.InvalidateCache();

            try
            {
                if (mainWindow.DataContext is ViewModels.MainViewModel vm)
                {
                    vm.InitializeAfterConnectionEstablished();
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show(
                    $"No se pudo inicializar la aplicación con la base seleccionada.\n" +
                    $"Verifique que la base CUAD tenga todas las tablas y vistas requeridas.\n\nDetalle técnico:\n{ex.Message}",
                    "Error al leer CUAD",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ocurrió un error inesperado al inicializar la aplicación.\n\n{ex.Message}",
                    "Error al iniciar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

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
