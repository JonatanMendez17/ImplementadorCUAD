using MigradorCUAD.Commands;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MigradorCUAD.Models;

namespace MigradorCUAD.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        // Titulo
        private string _titulo;
        public string Titulo
        {
            get => _titulo;
            set
            {
                _titulo = value;
                OnPropertyChanged();
            }
        }

        // Empleador
        private string? _empleadorSeleccionado;
        public string? EmpleadorSeleccionado
        {
            get => _empleadorSeleccionado;
            set
            {
                _empleadorSeleccionado = value;
                OnPropertyChanged();
            }
        }

        // Entidad
        private string? _entidadSeleccionada;
        public string? EntidadSeleccionada
        {
            get => _entidadSeleccionada;
            set
            {
                _entidadSeleccionada = value;
                OnPropertyChanged();
            }
        }

        // Archivos
        private string? _archivoCategorias;
        public string? ArchivoCategorias
        {
            get => _archivoCategorias;
            set
            {
                _archivoCategorias = value;
                OnPropertyChanged();
            }
        }

        private string? _archivoPadron;
        public string? ArchivoPadron
        {
            get => _archivoPadron;
            set
            {
                _archivoPadron = value;
                OnPropertyChanged();
            }
        }

        private string? _archivoConsumos;
        public string? ArchivoConsumos
        {
            get => _archivoConsumos;
            set
            {
                _archivoConsumos = value;
                OnPropertyChanged();
            }
        }

        private string? _archivoConsumosDetalle;
        public string? ArchivoConsumosDetalle
        {
            get => _archivoConsumosDetalle;
            set
            {
                _archivoConsumosDetalle = value;
                OnPropertyChanged();
            }
        }

        private string? _archivoServicios;
        public string? ArchivoServicios
        {
            get => _archivoServicios;
            set
            {
                _archivoServicios = value;
                OnPropertyChanged();
            }
        }

        // Progreso
        private int _progreso;
        public int Progreso
        {
            get => _progreso;
            set
            {
                _progreso = value;
                OnPropertyChanged();
            }
        }

        // Logs
        public ObservableCollection<string> Logs { get; }


        public ICommand CambiarTituloCommand { get; }
        public ICommand ProbarCommand { get; }


        public ObservableCollection<Empleador> Empleadores { get; set; }
        public ObservableCollection<Entidad> Entidades { get; set; }


        public MainViewModel()
        {
            Logs = new ObservableCollection<string>();
            Progreso = 0;

            Titulo = "MigradorCUAD";

            CambiarTituloCommand = new RelayCommand(_ =>
            {
                Titulo = "Título cambiado desde el ViewModel";
            });

            ProbarCommand = new RelayCommand(_ => Probar());

            Empleadores = new ObservableCollection<Empleador>
            {
                new Empleador { Id = 1, Nombre = "Liquidador Tierra del Fuego" },
                new Empleador { Id = 2, Nombre = "Liquidador Santa Fe" }
            };

            Entidades = new ObservableCollection<Entidad>
            {
                new Entidad { Id = 1, Nombre = "Entidad A" },
                new Entidad { Id = 2, Nombre = "Entidad B" }
            };

        }


        private void Probar()
        {
            MessageBox.Show("MigradorCUAD está vivo 🚀");
        }

    }
}
