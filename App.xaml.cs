using System.Windows;
using ImplementadorCUAD.Data;

namespace ImplementadorCUAD
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
