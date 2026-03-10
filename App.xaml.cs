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

            try
            {
                using (var db = new AppDbContext())
                {
                    db.EnsureConnection();
                }
            }
            catch (SqlException ex)
            {
                HandleStartupConnectionError(ex);
            }
            catch (Exception ex)
            {
                HandleStartupConnectionError(ex);
            }
        }

        private void HandleStartupConnectionError(Exception ex)
        {
            var detalle = ex.Message;
            var mensaje =
                "No se pudo conectar a la base de datos CUAD.\n\n" +
                "Revise la configuración de conexión antes de continuar.\n\n" +
                $"Detalle técnico: {detalle}";

            var resultado = DialogService.Show(
                mensaje,
                "Error de conexión",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Error,
                primaryButtonText: "Configurar conexión",
                secondaryButtonText: "Cerrar");

            if (resultado == MessageBoxResult.OK)
            {
                try
                {
                    var configPath = System.IO.Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Configuracion.xml");

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = configPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception openEx)
                {
                    ShowErrorMessage(
                        "No se pudo abrir el archivo de configuración.\n\n" + openEx.Message);
                }
            }

            Shutdown();
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
