using System;
using System.Threading.Tasks;
using System.Windows;
using ImplementadorCUAD.Data;
using ImplementadorCUAD.Services;
using Microsoft.Data.SqlClient;

namespace ImplementadorCUAD
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Manejo global de errores no controlados para mostrar un mensaje amigable.
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            if (!EnsureInitialConnection())
            {
                return;
            }

            StartMainWindow();
        }

        private bool EnsureInitialConnection()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    db.EnsureConnection();
                }

                return true;
            }
            catch (SqlException ex)
            {
                return HandleStartupConnectionError(ex);
            }
            catch (Exception ex)
            {
                return HandleStartupConnectionError(ex);
            }
        }

        private void StartMainWindow()
        {
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        private bool HandleStartupConnectionError(Exception ex)
        {
            var detalle = ex.Message;
            var mensaje =
                "No se pudo conectar a la base de datos CUAD.\n\n" +
                "¿Desea configurar ahora la conexión a CUAD?\n\n" +
                $"Detalle técnico: {detalle}";

            var resultado = MessageBox.Show(
                mensaje,
                "Error de conexión",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (resultado != MessageBoxResult.Yes)
            {
                Shutdown();
                return false;
            }

            var configWindow = new ConnectionConfigWindow();

            var dialogResult = configWindow.ShowDialog();
            if (dialogResult != true)
            {
                Shutdown();
                return false;
            }

            try
            {
                using (var db = new AppDbContext())
                {
                    db.EnsureConnection();
                }

                return true;
            }
            catch (Exception retryEx)
            {
                ShowErrorMessage(
                    "La conexión sigue fallando después de actualizar la configuración.\n\n" +
                    retryEx.Message);
                Shutdown();
                return false;
            }
        }

        private static void ShowErrorMessage(string message, string? technical = null)
        {
            var userMessage = "Ocurrió un error inesperado en la aplicación.";
            if (!string.IsNullOrWhiteSpace(message))
            {
                userMessage += Environment.NewLine + Environment.NewLine +
                               "Detalle: " + message;
            }

            MessageBox.Show(
                userMessage,
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            ShowErrorMessage(e.Exception?.Message);
            e.Handled = true;
        }

        private static void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                ShowErrorMessage(ex.Message);
            }
            else
            {
                ShowErrorMessage("Se produjo un error no controlado.");
            }
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            ShowErrorMessage(e.Exception?.Message);
            e.SetObserved();
        }
    }
}
