using MigradorCUAD.Commands;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MigradorCUAD.Models;
using Microsoft.Win32;
using System.IO;
using MigradorCUAD.Services;
using System.Globalization;




namespace MigradorCUAD.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
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
        public ObservableCollection<string> Logs { get; set; }


        public ObservableCollection<Empleador> Empleadores { get; set; }
        public ObservableCollection<Entidad> Entidades { get; set; }

        public ICommand SeleccionarCategoriasCommand { get; }
        public ICommand SeleccionarPadronCommand { get; }
        public ICommand SeleccionarConsumosCommand { get; }
        public ICommand SeleccionarConsumosDetalleCommand { get; }
        public ICommand SeleccionarServiciosCommand { get; }
        public ICommand ValidarCommand { get; }


        public MainViewModel()
        {
            Logs = new ObservableCollection<string>();
            Progreso = 0;

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

            SeleccionarCategoriasCommand = new RelayCommand(_ => SeleccionarArchivo("Categorias"));
            SeleccionarPadronCommand = new RelayCommand(_ => SeleccionarArchivo("Padron"));
            SeleccionarConsumosCommand = new RelayCommand(_ => SeleccionarArchivo("Consumos"));
            SeleccionarConsumosDetalleCommand = new RelayCommand(_ => SeleccionarArchivo("ConsumosDetalle"));
            SeleccionarServiciosCommand = new RelayCommand(_ => SeleccionarArchivo("Servicios"));

            ValidarCommand = new RelayCommand(_ => ValidarArchivos());

        }

        private void SeleccionarArchivo(string tipo)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Archivos CSV (*.csv)|*.csv|Archivos TXT (*.txt)|*.txt|Todos los archivos (*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                switch (tipo)
                {
                    case "Categorias":
                        ArchivoCategorias = dialog.FileName;
                        break;

                    case "Padron":
                        ArchivoPadron = dialog.FileName;
                        break;

                    case "Consumos":
                        ArchivoConsumos = dialog.FileName;
                        break;

                    case "ConsumosDetalle":
                        ArchivoConsumosDetalle = dialog.FileName;
                        break;

                    case "Servicios":
                        ArchivoServicios = dialog.FileName;
                        break;
                }
            }
        }

        private void ValidarArchivos()
        {
            Logs.Clear();

            if (EmpleadorSeleccionado == null)
            {
                Logs.Add("❌ Debe seleccionar un empleador.");
            }

            if (EntidadSeleccionada == null)
            {
                Logs.Add("❌ Debe seleccionar una entidad.");
            }

            if (string.IsNullOrWhiteSpace(ArchivoCategorias))
                Logs.Add("❌ Archivo de Categorías no seleccionado.");

            if (string.IsNullOrWhiteSpace(ArchivoPadron))
                Logs.Add("❌ Archivo de Padrón no seleccionado.");

            if (string.IsNullOrWhiteSpace(ArchivoConsumos))
                Logs.Add("❌ Archivo de Consumos no seleccionado.");

            if (string.IsNullOrWhiteSpace(ArchivoConsumosDetalle))
                Logs.Add("❌ Archivo de Consumos Detalle no seleccionado.");

            if (string.IsNullOrWhiteSpace(ArchivoServicios))
                Logs.Add("❌ Archivo de Servicios no seleccionado.");

            if (Logs.Count == 0)
            {
                ValidarArchivoCategorias();
            }
        }

        private void ValidarArchivoCategorias()
        {
            try
            {
                var configService = new ConfiguracionService();
                var columnasConfig = configService.ObtenerColumnas("Categorias");

                var lineas = File.ReadAllLines(ArchivoCategorias!);

                if (lineas.Length == 0)
                {
                    Logs.Add("❌ Archivo vacío.");
                    return;
                }

                var encabezado = lineas[0].Split(',');

                // Validar columnas
                if (encabezado.Length != columnasConfig.Count)
                {
                    Logs.Add("❌ Cantidad de columnas incorrecta.");
                    return;
                }

                for (int i = 0; i < encabezado.Length; i++)
                {
                    if (encabezado[i] != columnasConfig[i].Nombre)
                    {
                        Logs.Add($"❌ Nombre de columna incorrecto: {encabezado[i]}");
                        return;
                    }
                }

                // Validar registros
                for (int i = 1; i < lineas.Length; i++)
                {
                    var valores = lineas[i].Split(',');

                    for (int j = 0; j < columnasConfig.Count; j++)
                    {
                        var valor = valores[j];
                        var config = columnasConfig[j];

                        if (!ValidarTipoDato(valor, config))
                        {
                            Logs.Add($"❌ Error en fila {i + 1}, columna {config.Nombre}");
                        }
                    }
                }

                Logs.Add("✅ Archivo Categorías validado correctamente.");
            }
            catch (Exception ex)
            {
                Logs.Add($"❌ Error: {ex.Message}");
            }
        }

        private bool ValidarTipoDato(string valor, ColumnaConfiguracion config)
        {
            if (valor.Length > config.LargoMaximo)
                return false;

            switch (config.TipoDato.ToLower())
            {
                case "int":
                    return int.TryParse(valor, out _);

                case "decimal":
                    return decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out _);

                case "fecha":
                    return DateTime.TryParse(valor, out _);

                case "texto":
                    return !string.IsNullOrWhiteSpace(valor);

                default:
                    return false;
            }
        }
    }
}
