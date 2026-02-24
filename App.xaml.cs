using System.Windows;
using MigradorCUAD.Data;

namespace MigradorCUAD
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            using (var db = new AppDbContext())
            {
                db.EnsureConnection();
            }
        }
    }
}
