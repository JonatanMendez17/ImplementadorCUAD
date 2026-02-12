using Microsoft.EntityFrameworkCore;
using System.Windows;
using TuProyecto.Data;

namespace MigradorCUAD
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Crear base de datos y aplicar migraciones automáticamente
            using (var db = new AppDbContext())
            {
                // db.Database.Migrate();   // usa migraciones (si las tienes)
                // o, solo para pruebas sin migraciones:
                db.Database.EnsureCreated();
            }
        }
    }
}
