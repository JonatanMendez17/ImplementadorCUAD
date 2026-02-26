using Microsoft.Win32;
using MigradorCUAD.Commands;
using MigradorCUAD.Data;
using MigradorCUAD.Models;
using MigradorCUAD.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
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
            set
            {
                if (SetProperty(ref _empleadorSeleccionado, value))
                {
                    InvalidateValidationState("Se actualizo el empleador seleccionado. Se reinicio el estado de validacion.");
                }
            }
        }

        public Entidad? EntidadSeleccionada
        {
            get => _entidadSeleccionada;
            set
            {
                if (SetProperty(ref _entidadSeleccionada, value))
                {
                    InvalidateValidationState("Se actualizo la entidad seleccionada. Se reinicio el estado de validacion.");
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string? ArchivoCategorias
        {
            get => _archivoCategorias;
            set
            {
                if (SetProperty(ref _archivoCategorias, value))
                {
                    OnPropertyChanged(nameof(ArchivoCategoriasNombre));
                }
            }
        }

        public string? ArchivoPadron
        {
            get => _archivoPadron;
            set
            {
                if (SetProperty(ref _archivoPadron, value))
                {
                    OnPropertyChanged(nameof(ArchivoPadronNombre));
                }
            }
        }

        public string? ArchivoConsumos
        {
            get => _archivoConsumos;
            set
            {
                if (SetProperty(ref _archivoConsumos, value))
                {
                    OnPropertyChanged(nameof(ArchivoConsumosNombre));
                }
            }
        }

        public string? ArchivoConsumosDetalle
        {
            get => _archivoConsumosDetalle;
            set
            {
                if (SetProperty(ref _archivoConsumosDetalle, value))
                {
                    OnPropertyChanged(nameof(ArchivoConsumosDetalleNombre));
                }
            }
        }

        public string? ArchivoServicios
        {
            get => _archivoServicios;
            set
            {
                if (SetProperty(ref _archivoServicios, value))
                {
                    OnPropertyChanged(nameof(ArchivoServiciosNombre));
                }
            }
        }

        public string? ArchivoCatalogoServicios
        {
            get => _archivoCatalogoServicios;
            set
            {
                if (SetProperty(ref _archivoCatalogoServicios, value))
                {
                    OnPropertyChanged(nameof(ArchivoCatalogoServiciosNombre));
                }
            }
        }

        public string ArchivoCategoriasNombre => GetNombreArchivo(ArchivoCategorias);
        public string ArchivoPadronNombre => GetNombreArchivo(ArchivoPadron);
        public string ArchivoConsumosNombre => GetNombreArchivo(ArchivoConsumos);
        public string ArchivoConsumosDetalleNombre => GetNombreArchivo(ArchivoConsumosDetalle);
        public string ArchivoServiciosNombre => GetNombreArchivo(ArchivoServicios);
        public string ArchivoCatalogoServiciosNombre => GetNombreArchivo(ArchivoCatalogoServicios);

        public int Progreso
        {
            get => _progreso;
            set => SetProperty(ref _progreso, value);
        }

        public bool EstaProcesando
        {
            get => _estaProcesando;
            set
            {
                if (SetProperty(ref _estaProcesando, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
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
        public ICommand CopiarCommand { get; }
        public ICommand ExportarLogCommand { get; }
        public ICommand LimpiarBaseEntidadCommand { get; }

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
            CopiarCommand = new SimpleAsyncCommand(CopiarABaseAsync);
            ExportarLogCommand = new RelayCommand(_ => ExportarLog());
            LimpiarBaseEntidadCommand = new RelayCommand(LimpiarBaseEntidad, PuedeLimpiarBaseEntidad);
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

            if (EntidadSeleccionada == null)
            {
                Logs.Add("Debe seleccionar una entidad para validar.");
                ValidacionFinalizada = false;
                return;
            }

            if (EmpleadorSeleccionado == null)
            {
                Logs.Add("Aviso: no se selecciono empleador.");
            }

            // Modo prueba: validaciones de archivos obligatorios deshabilitadas para permitir carga parcial.
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

            if (!MatchesSelectedEntidad(entidadComun))
            {
                Logs.Add($"ERROR: la entidad detectada en archivos ('{entidadComun}') no coincide con la entidad seleccionada.");
                ValidacionFinalizada = false;
                return;
            }

            Logs.Add($"OK: la entidad de archivos coincide con la seleccionada ({EntidadSeleccionada.Nombre} / {EntidadSeleccionada.EntId}).");

            var sinDatosPrevios = _generalValidationService.ValidateNoExistingDataForEntidad(
                entidadComun,
                EmpleadorSeleccionado,
                Logs.Add);

            ValidacionFinalizada = sinDatosPrevios;
        }

        private async Task CopiarABaseAsync()
        {
            if (EntidadSeleccionada == null)
            {
                Logs.Add("Debe seleccionar una entidad antes de migrar.");
                return;
            }

            var empleadorInfo = EmpleadorSeleccionado?.Nombre ?? "(sin empleador seleccionado)";
            Logs.Add($"Contexto de migracion: Entidad='{EntidadSeleccionada.Nombre}' (ID {EntidadSeleccionada.EntId}), Empleador='{empleadorInfo}'.");

            if (!ValidacionFinalizada || !_validationResult.HuboCarga)
            {
                var resultado = MessageBox.Show(
                    "Algunas validaciones no pasaron o la carga fue descartada. Desea migrar igualmente?",
                    "Confirmar migracion",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (resultado != MessageBoxResult.Yes)
                {
                    Logs.Add("Migracion cancelada por el usuario.");
                    return;
                }

                Logs.Add("El usuario confirmo migrar con validaciones pendientes.");
            }

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

        private bool PuedeLimpiarBaseEntidad(object? parameter)
        {
            return EntidadSeleccionada != null && !EstaProcesando;
        }

        private void LimpiarBaseEntidad(object? parameter)
        {
            if (EntidadSeleccionada == null)
            {
                Logs.Add("Debe seleccionar una entidad para limpiar la base.");
                return;
            }

            var empleadorInfo = EmpleadorSeleccionado?.Nombre ?? "(sin empleador seleccionado)";
            var entidadNombre = EntidadSeleccionada.Nombre ?? EntidadSeleccionada.EntId.ToString();

            var confirmacion = MessageBox.Show(
                $"Se eliminaran los datos importados de la entidad '{entidadNombre}' (ID {EntidadSeleccionada.EntId}) en el contexto del empleador '{empleadorInfo}'.\n\n¿Desea continuar?",
                "Confirmar limpieza de base",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmacion != MessageBoxResult.Yes)
            {
                Logs.Add("Limpieza de base cancelada por el usuario.");
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var eliminados = db.DeleteImportedDataForEntidad(
                    EntidadSeleccionada.Nombre ?? string.Empty,
                    EntidadSeleccionada.EntId);

                var totalEliminado = eliminados.Padron + eliminados.ConsumoCab + eliminados.ConsumoDet;
                Logs.Add($"Limpieza ejecutada para entidad '{entidadNombre}' y empleador '{empleadorInfo}'.");
                Logs.Add($"Registros eliminados: Padron={eliminados.Padron}, ConsumoCab={eliminados.ConsumoCab}, ConsumoDet={eliminados.ConsumoDet}, Total={totalEliminado}.");

                if (totalEliminado == 0)
                {
                    Logs.Add("No se encontraron registros para eliminar con la entidad seleccionada.");
                }

                ValidacionFinalizada = false;
                Progreso = 0;
                _validationResult = new MigrationValidationResult();
            }
            catch (Exception ex)
            {
                Logs.Add($"Error al limpiar la base para la entidad seleccionada: {ex.Message}");
                MessageBox.Show(
                    $"No se pudo limpiar la base.\n{ex.Message}",
                    "Limpieza de base",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void InvalidateValidationState(string mensaje)
        {
            var teniaEstado = _validationResult.HuboCarga || ValidacionFinalizada || Progreso > 0;
            ValidacionFinalizada = false;
            Progreso = 0;
            _validationResult = new MigrationValidationResult();

            if (teniaEstado)
            {
                Logs.Add(mensaje);
            }
        }

        private bool MatchesSelectedEntidad(string entidadComun)
        {
            if (EntidadSeleccionada == null || string.IsNullOrWhiteSpace(entidadComun))
            {
                return false;
            }

            var entidadNormalizada = entidadComun.Trim();

            if (!string.IsNullOrWhiteSpace(EntidadSeleccionada.Nombre) &&
                string.Equals(entidadNormalizada, EntidadSeleccionada.Nombre.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (int.TryParse(entidadNormalizada, out var entidadId))
            {
                return entidadId == EntidadSeleccionada.EntId;
            }

            return string.Equals(entidadNormalizada, EntidadSeleccionada.EntId.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private void ExportarLog()
        {
            if (Logs.Count == 0)
            {
                Logs.Add("No hay mensajes de log para exportar.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Guardar log",
                Filter = "Archivo de texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
                FileName = $"LogMigracion_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                AddExtension = true,
                DefaultExt = "txt"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                File.WriteAllLines(dialog.FileName, Logs);
                Logs.Add($"Log exportado a: {dialog.FileName}");
                MessageBox.Show($"Log generado en:\n{dialog.FileName}", "Exportar log", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logs.Add($"Error al exportar el log: {ex.Message}");
                MessageBox.Show($"No se pudo exportar el log.\n{ex.Message}", "Exportar log", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string GetNombreArchivo(string? ruta)
        {
            return string.IsNullOrWhiteSpace(ruta) ? string.Empty : Path.GetFileName(ruta);
        }
    }
}


