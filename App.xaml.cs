using System.Windows;
using ImplementadorCUAD.Data;
using ImplementadorCUAD.Infrastructure;

namespace ImplementadorCUAD
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Paso 1: mostrar la ventana principal.
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();

            // Paso 2: pedir al usuario el connection string mediante un modal.
            var connectionWindow = new ConnectionWindow
            {
                Owner = mainWindow
            };
            var result = connectionWindow.ShowDialog();

            if (result != true || string.IsNullOrWhiteSpace(connectionWindow.SelectedConnectionString))
            {
                // Usuario canceló o no se obtuvo una conexión válida.
                mainWindow.Close();
                Shutdown();
                return;
            }

            // Paso 3: propagar la conexión seleccionada a toda la aplicación.
            ConnectionSettings.ConnectionString = connectionWindow.SelectedConnectionString;

            // Paso 4: validar nuevamente que la conexión funcione con la configuración global.
            using (var db = new AppDbContext())
            {
                db.EnsureConnection();
            }

            // Paso 5: inicializar datos que dependen de la conexión (entidades, empleador).
            if (mainWindow.DataContext is ViewModels.MainViewModel vm)
            {
                vm.InitializeAfterConnectionEstablished();
            }
        }
    }
}
