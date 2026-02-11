using MigradorCUAD.ViewModels;
using System.Windows;

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
