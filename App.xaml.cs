using System.Windows;
using MigradorCUAD.Data;

namespace MigradorCUAD
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Crear base de datos y aplicar migraciones/seed automáticamente
            using (var db = new AppDbContext())
            {
                // Para entorno de desarrollo: crear si no existe
                db.Database.EnsureCreated();

            }
        }
    }
}
