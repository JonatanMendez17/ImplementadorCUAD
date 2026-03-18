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

                    // Conexión a CUAD OK (a nivel de servidor/base). Ahora inicializamos el ViewModel.
                    try
                    {
                        if (mainWindow.DataContext is ViewModels.MainViewModel vmWithConfig)
                        {
                            vmWithConfig.InitializeAfterConnectionEstablished();
                        }
                    }
                    catch (Microsoft.Data.SqlClient.SqlException ex)
                    {
                        // Errores típicos aquí: tablas/vistas que no existen en la base apuntada.
                        MessageBox.Show(
                            $"No se pudo inicializar la aplicación con la base seleccionada.\n" +
                            $"Verifique que la base CUAD tenga todas las tablas y vistas requeridas.\n\nDetalle técnico:\n{ex.Message}",
                            "Error al leer CUAD",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        // Dejamos la ventana abierta, pero sin datos inicializados.
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
            try
            {
                if (mainWindow.DataContext is ViewModels.MainViewModel vm)
                {
                    vm.InitializeAfterConnectionEstablished();
                }
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
            {
                MessageBox.Show(
                    $"No se pudo inicializar la aplicación con la base seleccionada.\n" +
                    $"Verifique que la base CUAD tenga todas las tablas y vistas requeridas.\n\nDetalle técnico:\n{ex.Message}",
                    "Error al leer CUAD",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                // No cerramos la app para que el usuario vea el mensaje y pueda corregir la configuración.
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
