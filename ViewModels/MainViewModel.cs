using Microsoft.Win32;
using MigradorCUAD.Commands;
using MigradorCUAD.Models;
using MigradorCUAD.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using TuProyecto.Data;
using TuProyecto.Models;
namespace MigradorCUAD.ViewModels
{
    public class MainViewModel : ViewModelBase
    {

        //Temporal --------------
        private HashSet<int> _numerosSocioPadron = new();
        private HashSet<int> _numerosConsumo = new();
        private HashSet<string> _categorias = new();


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
        private bool _estaProcesando;

        public int Progreso
        {
            get => _progreso;
            set
            {
                _progreso = value;
                OnPropertyChanged();
            }
        }
        public bool EstaProcesando
        {
            get => _estaProcesando;
            set
            {
                _estaProcesando = value;
                OnPropertyChanged();
            }
        }


        private List<Dictionary<string, string>> _datosValidados = new();


        private bool _validacionFinalizada;
        public bool ValidacionFinalizada
        {
            get => _validacionFinalizada;
            set
            {
                _validacionFinalizada = value;
                OnPropertyChanged();
            }
        }

        // Logs
        public ObservableCollection<string> Logs { get; set; }

        // Listas de selección
        public ObservableCollection<Empleador> Empleadores { get; set; }
        public ObservableCollection<Entidad> Entidades { get; set; }

        // Comandos
        public ICommand SeleccionarCategoriasCommand { get; }
        public ICommand SeleccionarPadronCommand { get; }
        public ICommand SeleccionarConsumosCommand { get; }
        public ICommand SeleccionarConsumosDetalleCommand { get; }
        public ICommand SeleccionarServiciosCommand { get; }
        public ICommand ValidarCommand { get; }
        public ICommand CopiarABaseCommand {  get; }
        public ICommand CopiarCommand { get; }

        // Constructor
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

            CopiarABaseCommand = new RelayCommand(CopiarABase, PuedeCopiarABase);

            //using (var db = new AppDbContext())
            //{
            //    var registro = new DatosPadron
            //    {
            //        Cuit = "20123456789",
            //        RazonSocial = "Empresa Test",
            //        FechaAlta = DateTime.Now,
            //        Importe = 1500
            //    };

            //    db.DatosPadron.Add(registro);
            //    db.SaveChanges();
            //}

            CopiarCommand = new AsyncRelayCommand(CopiarABaseAsync);

        }

        // Métodos de selección de archivos
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

        // Método principal de validación   
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
                // Validación estructural de todos los archivos
                var datosCategorias = ValidarArchivo("Categorias", ArchivoCategorias);
                var datosPadron = ValidarArchivo("Padron", ArchivoPadron);
                var datosConsumos = ValidarArchivo("Consumos", ArchivoConsumos);
                var datosConsumosDetalle = ValidarArchivo("ConsumosDetalle", ArchivoConsumosDetalle);
                var datosServicios = ValidarArchivo("Servicios", ArchivoServicios);

                // Si todos devolvieron registros válidos, se realiza la validación cruzada
                if (datosCategorias != null &&
                    datosPadron != null &&
                    datosConsumos != null &&
                    datosConsumosDetalle != null &&
                    datosServicios != null)
                {
                    // Guardar datos validados de padrón para la copia a base
                    _datosValidados = datosPadron;

                    // Mapear a modelos fuertemente tipados
                    var socios = GenericMapper.MapToList<Socio>(datosPadron);
                    var consumos = GenericMapper.MapToList<Consumo>(datosConsumos);
                    var detalles = GenericMapper.MapToList<ConsumoDetalle>(datosConsumosDetalle);
                    var servicios = GenericMapper.MapToList<Servicio>(datosServicios);

                    // Validación cruzada entre archivos
                    var erroresCruzados = CrossValidator.Validate(
                        socios,
                        consumos,
                        detalles,
                        servicios);

                    foreach (var error in erroresCruzados)
                    {
                        Logs.Add($"❌ {error}");
                    }

                    if (!erroresCruzados.Any())
                    {
                        Logs.Add("✅ Validación cruzada exitosa. Lista para copiar a base.");
                    }
                    else
                    {
                        Logs.Add("⚠️ Validación cruzada finalizada con errores.");
                    }
                }
            }

            ValidacionFinalizada = true;

        }

        // Validación específica del archivo de Categorías
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

        // Validación de tipo de dato individual según configuración
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

        private List<Dictionary<string, string>>? ValidarArchivo(string nombreLogico, string? rutaArchivo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rutaArchivo) || !File.Exists(rutaArchivo))
                {
                    Logs.Add($"❌ Archivo inválido: {nombreLogico}");
                    return null;
                }

                var configService = new ConfiguracionService();
                var columnasConfig = configService.ObtenerColumnas(nombreLogico);

                if (columnasConfig.Count == 0)
                {
                    Logs.Add($"❌ No existe configuración XML para {nombreLogico}");
                    return null;
                }

                var lineas = File.ReadAllLines(rutaArchivo);

                if (lineas.Length == 0)
                {
                    Logs.Add($"❌ Archivo {nombreLogico} vacío.");
                    return null;
                }

                var encabezado = lineas[0].Split(',');

                if (encabezado.Length != columnasConfig.Count)
                {
                    Logs.Add($"❌ Cantidad de columnas incorrecta en {nombreLogico}");
                    return null;
                }

                for (int i = 0; i < encabezado.Length; i++)
                {
                    if (encabezado[i] != columnasConfig[i].Nombre)
                    {
                        Logs.Add($"❌ Columna incorrecta en {nombreLogico}: {encabezado[i]}");
                        return null;
                    }
                }

                var registros = new List<Dictionary<string, string>>();

                for (int i = 1; i < lineas.Length; i++)
                {
                    var valores = lineas[i].Split(',');

                    if (valores.Length != columnasConfig.Count)
                    {
                        Logs.Add($"❌ Fila {i + 1} con columnas incorrectas en {nombreLogico}");
                        continue;
                    }

                    var fila = new Dictionary<string, string>();

                    for (int j = 0; j < columnasConfig.Count; j++)
                    {
                        var valor = valores[j];
                        var config = columnasConfig[j];

                        if (!ValidarTipoDato(valor, config))
                        {
                            Logs.Add($"❌ Error en {nombreLogico} - Fila {i + 1}, Columna {config.Nombre}");
                        }

                        fila.Add(config.Nombre, valor);
                    }

                    registros.Add(fila);
                }

                Logs.Add($"✅ {nombreLogico} validado estructuralmente.");
                return registros;
            }
            catch (Exception ex)
            {
                Logs.Add($"❌ Error validando {nombreLogico}: {ex.Message}");
                return null;
            }
        }

        private bool PuedeCopiarABase(object? parameter)
        {
            return ValidacionFinalizada;
        }

        private void CopiarABase(object? parameter)
        {
            Logs.Add("💾 Iniciando proceso de copia a base de datos...");
        }

        private async Task CopiarABaseAsync()
        {
            EstaProcesando = true;
            Progreso = 0;

            var lista = _datosValidados; // tu lista validada de padrón
            int total = lista.Count;
            int procesados = 0;

            await Task.Run(() =>
            {
                using (var db = new AppDbContext())
                {
                    foreach (var item in lista)
                    {
                        var entidad = new DatosPadron
                        {
                            // Ajusta las claves según los nombres reales de columnas en tu archivo
                            Cuit = item.ContainsKey("Cuit") ? item["Cuit"] : null,
                            RazonSocial = item.ContainsKey("RazonSocial") ? item["RazonSocial"] : null,
                            FechaAlta = item.ContainsKey("FechaAlta")
                                ? DateTime.Parse(item["FechaAlta"])
                                : DateTime.Now,
                            Importe = item.ContainsKey("Importe")
                                ? decimal.Parse(item["Importe"], NumberStyles.Any, CultureInfo.InvariantCulture)
                                : 0m
                        };

                        db.DatosPadron.Add(entidad);
                        procesados++;

                        Progreso = (procesados * 100) / total;
                    }

                    db.SaveChanges();
                }
            });

            EstaProcesando = false;
        }
    }
}
