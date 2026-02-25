using Microsoft.Win32;
using MigradorCUAD.Commands;
using MigradorCUAD.Data;
using MigradorCUAD.Models;
using MigradorCUAD.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace MigradorCUAD.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly FileImportService _fileImportService;
        private readonly GeneralValidationService _generalValidationService;
        private readonly MigrationService _migrationService;
        private MigrationValidationResult _validationResult = new();

        private Empleador? _empleadorSeleccionado;
        private Entidad? _entidadSeleccionada;
        private string? _archivoCategorias;
        private string? _archivoPadron;
        private string? _archivoConsumos;
        private string? _archivoConsumosDetalle;
        private string? _archivoServicios;
        private string? _archivoCatalogoServicios;
        private int _progreso;
        private bool _estaProcesando;
        private bool _validacionFinalizada;

        public Empleador? EmpleadorSeleccionado
        {
            get => _empleadorSeleccionado;
            set => SetProperty(ref _empleadorSeleccionado, value);
        }

        public Entidad? EntidadSeleccionada
        {
            get => _entidadSeleccionada;
            set => SetProperty(ref _entidadSeleccionada, value);
        }

        public string? ArchivoCategorias
        {
            get => _archivoCategorias;
            set => SetProperty(ref _archivoCategorias, value);
        }

        public string? ArchivoPadron
        {
            get => _archivoPadron;
            set => SetProperty(ref _archivoPadron, value);
        }

        public string? ArchivoConsumos
        {
            get => _archivoConsumos;
            set => SetProperty(ref _archivoConsumos, value);
        }

        public string? ArchivoConsumosDetalle
        {
            get => _archivoConsumosDetalle;
            set => SetProperty(ref _archivoConsumosDetalle, value);
        }

        public string? ArchivoServicios
        {
            get => _archivoServicios;
            set => SetProperty(ref _archivoServicios, value);
        }

        public string? ArchivoCatalogoServicios
        {
            get => _archivoCatalogoServicios;
            set => SetProperty(ref _archivoCatalogoServicios, value);
        }

        public int Progreso
        {
            get => _progreso;
            set => SetProperty(ref _progreso, value);
        }

        public bool EstaProcesando
        {
            get => _estaProcesando;
            set => SetProperty(ref _estaProcesando, value);
        }

        public bool ValidacionFinalizada
        {
            get => _validacionFinalizada;
            set => SetProperty(ref _validacionFinalizada, value);
        }

        public ObservableCollection<string> Logs { get; }
        public ObservableCollection<Empleador> Empleador { get; }
        public ObservableCollection<Entidad> Entidades { get; }

        public ICommand SeleccionarCategoriasCommand { get; }
        public ICommand SeleccionarPadronCommand { get; }
        public ICommand SeleccionarConsumosCommand { get; }
        public ICommand SeleccionarConsumosDetalleCommand { get; }
        public ICommand SeleccionarServiciosCommand { get; }
        public ICommand SeleccionarCatalogoServiciosCommand { get; }
        public ICommand ValidarCommand { get; }
        public ICommand CopiarABaseCommand { get; }
        public ICommand CopiarCommand { get; }
        public ICommand ExportarLogCommand { get; }
        public ICommand LimpiarPantallaCommand { get; }

        public MainViewModel()
        {
            _fileImportService = new FileImportService();
            _generalValidationService = new GeneralValidationService();
            _migrationService = new MigrationService(new MigrationMapperService());

            Logs = new ObservableCollection<string>();
            Progreso = 0;

            using (var db = new AppDbContext())
            {
                Empleador = new ObservableCollection<Empleador>(db.GetEmpleadores());
                Entidades = new ObservableCollection<Entidad>(db.GetEntidades());
            }

            SeleccionarCategoriasCommand = new RelayCommand(_ => SeleccionarArchivo("Categorias"));
            SeleccionarPadronCommand = new RelayCommand(_ => SeleccionarArchivo("Padron"));
            SeleccionarConsumosCommand = new RelayCommand(_ => SeleccionarArchivo("Consumos"));
            SeleccionarConsumosDetalleCommand = new RelayCommand(_ => SeleccionarArchivo("ConsumosDetalle"));
            SeleccionarServiciosCommand = new RelayCommand(_ => SeleccionarArchivo("Servicios"));
            SeleccionarCatalogoServiciosCommand = new RelayCommand(_ => SeleccionarArchivo("CatalogoServicios"));
            ValidarCommand = new RelayCommand(_ => ValidarArchivos());
            CopiarABaseCommand = new RelayCommand(CopiarABase, PuedeCopiarABase);
            CopiarCommand = new SimpleAsyncCommand(CopiarABaseAsync);
            ExportarLogCommand = new RelayCommand(_ => ExportarLog());
            LimpiarPantallaCommand = new RelayCommand(_ => LimpiarPantalla());
        }

        private MigrationFileSelection BuildSelection()
        {
            return new MigrationFileSelection
            {
                ArchivoCategorias = ArchivoCategorias,
                ArchivoPadron = ArchivoPadron,
                ArchivoConsumos = ArchivoConsumos,
                ArchivoConsumosDetalle = ArchivoConsumosDetalle,
                ArchivoServicios = ArchivoServicios,
                ArchivoCatalogoServicios = ArchivoCatalogoServicios
            };
        }

        private void SeleccionarArchivo(string tipo)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Archivos Excel (*.xls;*.xlsx)|*.xls;*.xlsx|Archivos CSV (*.csv)|*.csv|Archivos TXT (*.txt)|*.txt|Todos los archivos (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

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
                case "CatalogoServicios":
                    ArchivoCatalogoServicios = dialog.FileName;
                    break;
            }
        }

        private void ValidarArchivos()
        {
            Logs.Clear();

            // Modo prueba: validaciones deshabilitadas para permitir carga directa.
            //if (EmpleadorSeleccionado == null) { ... }
            //if (EntidadSeleccionada == null) { ... }
            //if (string.IsNullOrWhiteSpace(ArchivoCategorias)) { ... }
            //if (string.IsNullOrWhiteSpace(ArchivoPadron)) { ... }
            //if (string.IsNullOrWhiteSpace(ArchivoConsumos)) { ... }
            //if (string.IsNullOrWhiteSpace(ArchivoConsumosDetalle)) { ... }
            //if (string.IsNullOrWhiteSpace(ArchivoServicios)) { ... }
            //if (string.IsNullOrWhiteSpace(ArchivoCatalogoServicios)) { ... }

            _validationResult = _fileImportService.ValidateAndLoadFiles(BuildSelection(), Logs.Add);

            if (!_validationResult.HuboCarga)
            {
                ValidacionFinalizada = false;
                return;
            }

            var entidadConsistente = _generalValidationService.ValidateEntidadConsistency(
                _validationResult,
                Logs.Add,
                out var entidadComun);

            if (!entidadConsistente)
            {
                ValidacionFinalizada = false;
                return;
            }

            var sinDatosPrevios = _generalValidationService.ValidateNoExistingDataForEntidad(
                entidadComun,
                EmpleadorSeleccionado,
                Logs.Add);

            ValidacionFinalizada = sinDatosPrevios;
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

            try
            {
                await _migrationService.CopyToDatabaseAsync(
                    _validationResult,
                    BuildSelection(),
                    Logs.Add,
                    progress => Progreso = progress);
            }
            finally
            {
                EstaProcesando = false;
            }
        }

        private void LimpiarPantalla()
        {
            EmpleadorSeleccionado = null;
            EntidadSeleccionada = null;
            ArchivoCategorias = null;
            ArchivoPadron = null;
            ArchivoConsumos = null;
            ArchivoConsumosDetalle = null;
            ArchivoServicios = null;
            ArchivoCatalogoServicios = null;
            Logs.Clear();
            Progreso = 0;
            EstaProcesando = false;
            ValidacionFinalizada = false;
            _validationResult = new MigrationValidationResult();
        }

        private void ExportarLog()
        {
            if (Logs.Count == 0)
            {
                Logs.Add("⚠️ No hay mensajes de log para exportar.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Guardar log de validación",
                Filter = "Archivo de texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
                FileName = "LogMigracion.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllLines(dialog.FileName, Logs);
                    Logs.Add($"✅ Log exportado a: {dialog.FileName}");
                }
                catch (Exception ex)
                {
                    Logs.Add($"❌ Error al exportar el log: {ex.Message}");
                }
            }
        }
    }
}
