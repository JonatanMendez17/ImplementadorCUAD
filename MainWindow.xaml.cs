using System.Windows;
using MigradorCUAD.ViewModels;

namespace MigradorCUAD
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
