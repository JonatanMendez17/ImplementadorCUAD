using System;
using System.Windows;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ImplementadorCUAD.Data;
using ImplementadorCUAD.Infrastructure;
using ImplementadorCUAD.Services;

namespace ImplementadorCUAD
{
    public partial class App : Application
    {
        public static ILoggerFactory LoggerFactory { get; private set; } = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information).AddDebug();
        });

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Information)
                    .AddDebug()
                    .AddProvider(new UiLoggerProvider());
            });
            
            ConnectionSettings.SetLoggerFactory(LoggerFactory);

            // Manejo global para evitar cierres abruptos sin informar al usuario.
            DispatcherUnhandledException += (_, args) =>
            {
                try
                {
                    args.Handled = true;
                    DialogService.Show(
                        $"Se produjo un error inesperado en la aplicación.\n\n{args.Exception.Message}",
                        "Error inesperado",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch
                {
                    // Si falla mostrar el message, no escalamos para no romper el manejador.
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                try
                {
                    // Best-effort: si la app está por terminar no hay garantía de que el UI llegue.
                    if (Application.Current != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            DialogService.Show(
                                $"Se produjo un error inesperado en la aplicación.\n\n{ex?.Message ?? ex?.ToString()}",
                                "Error inesperado",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        });
                    }
                }
                catch
                {
                    // No hacemos nada: es posible que el proceso ya esté finalizando.
                }
            };

            var mainWindow = new MainWindow(new ViewModels.MainViewModel(LoggerFactory.CreateLogger("ImplementadorCUAD")));
            MainWindow = mainWindow;
            mainWindow.Show();

            var initialBaseConnection = ConnectionSettings.BaseConnectionString;
            var hasInitialConfig = !string.IsNullOrWhiteSpace(initialBaseConnection);

            if (hasInitialConfig)
            {
                try
                {
                    var testConnectionString = WithShortTimeout(initialBaseConnection, 3);
                    using (var db = new AppDbContext(testConnectionString))
                    {
                        db.EnsureConnection();
                    }

                    try
                    {
                        if (mainWindow.DataContext is ViewModels.MainViewModel vmWithConfig)
                        {
                            vmWithConfig.InitializeAfterConnection();
                        }
                    }
                    catch (SqlException ex)
                    {
                        DialogService.Show(
                            $"No se pudo inicializar la aplicación con la base seleccionada.\n" +
                            $"Verifique que la base tenga todas las tablas y vistas requeridas.\n\nDetalle técnico:\n{ex.Message}",
                            "Error al leer base",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }
                    catch (Exception ex)
                    {
                        DialogService.Show(
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

            // No hay configuración válida de la base o la conexión falló: pedir al usuario el connection string.
            var configWindow = new ConnectionWindow
            {
                Owner = mainWindow
            };
            var result = configWindow.ShowDialog();

            if (result != true || string.IsNullOrWhiteSpace(configWindow.SelectedConnection))
            {
                Shutdown();
                return;
            }

            // ConnectionWindow ya validó la conexión. Persistir en XML e invalidar cache.
            var userConnectionString = configWindow.SelectedConnection;
            try
            {
                new ConnectionsConfigService().SetConexionBaseConnectionString(userConnectionString);
                ConnectionSettings.InvalidateCache();
            }
            catch (Exception ex)
            {
                DialogService.Show(
                    $"No se pudo guardar la configuración en '{ConnectionsConfigService.RutaConfiguracionXml}'.\n\n{ex.Message}",
                    "Error de configuración",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            try
            {
                if (mainWindow.DataContext is ViewModels.MainViewModel vm)
                {
                    vm.InitializeAfterConnection();
                }
            }
            catch (SqlException ex)
            {
                DialogService.Show(
                    $"No se pudo inicializar la aplicación con la base seleccionada.\n" +
                    $"Verifique que la base tenga todas las tablas y vistas requeridas.\n\nDetalle técnico:\n{ex.Message}",
                    "Error al leer base",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            catch (Exception ex)
            {
                DialogService.Show(
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
