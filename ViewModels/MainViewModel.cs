using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using ImplementadorCUAD.Commands;
using ImplementadorCUAD.Infrastructure;
using ImplementadorCUAD.Models;
using ImplementadorCUAD.Services;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ImplementadorCUAD.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IAppDbContextFactory _dbContextFactory;
        private readonly FileImportService _fileImportService;
        private readonly GeneralValidationService _generalValidationService;
        private readonly ImplementacionService _implementacionService;
        private ImplementacionValidationResult _validationResult = new();

        private Empleador? _empleadorSeleccionado;
        private Entidad? _entidadSeleccionada;
        private string? _archivoCategorias;
        private string? _archivoPadron;
        private string? _archivoConsumos;
        private readonly ObservableCollection<string> _archivosConsumosDetalle = new ObservableCollection<string>();
        private string? _archivoServicios;
        private string? _archivoCatalogoServicios;
        private int _progreso;
        private bool _estaProcesando;
        private bool _validacionFinalizada;
        private readonly ConcurrentQueue<LogEntry> _logBuffer = new();
        private DispatcherTimer? _logFlushTimer;
        private readonly List<LogEntry> _fullLogForExport = new();
        private bool _logTruncationMessageShown;

        private const int MaxVisibleLogEntries = 50;
        private static readonly LogEntry LogTruncationPlaceholder = new LogEntry(null, "[INFO]", "Para ver todo el log, exporte a archivo.");

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
                    OnPropertyChanged(nameof(ArchivoCategoriasCargado));
                    OnPropertyChanged(nameof(ArchivoCategoriasEstado));
                    OnPropertyChanged(nameof(ArchivoCategoriasIcono));
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
                    OnPropertyChanged(nameof(ArchivoPadronCargado));
                    OnPropertyChanged(nameof(ArchivoPadronEstado));
                    OnPropertyChanged(nameof(ArchivoPadronIcono));
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
                    OnPropertyChanged(nameof(ArchivoConsumosCargado));
                    OnPropertyChanged(nameof(ArchivoConsumosEstado));
                    OnPropertyChanged(nameof(ArchivoConsumosIcono));
                }
            }
        }

        public ObservableCollection<string> ArchivosConsumosDetalle => _archivosConsumosDetalle;

        public string? ArchivoServicios
        {
            get => _archivoServicios;
            set
            {
                if (SetProperty(ref _archivoServicios, value))
                {
                    OnPropertyChanged(nameof(ArchivoServiciosNombre));
                    OnPropertyChanged(nameof(ArchivoServiciosCargado));
                    OnPropertyChanged(nameof(ArchivoServiciosEstado));
                    OnPropertyChanged(nameof(ArchivoServiciosIcono));
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
                    OnPropertyChanged(nameof(ArchivoCatalogoServiciosCargado));
                    OnPropertyChanged(nameof(ArchivoCatalogoServiciosEstado));
                    OnPropertyChanged(nameof(ArchivoCatalogoServiciosIcono));
                }
            }
        }

        public string ArchivoCategoriasNombre => GetNombreArchivo(ArchivoCategorias);
        public string ArchivoPadronNombre => GetNombreArchivo(ArchivoPadron);
        public string ArchivoConsumosNombre => GetNombreArchivo(ArchivoConsumos);
        public string ArchivoConsumosDetalleNombre => GetArchivosConsumosDetalleNombre();
        public string ArchivoServiciosNombre => GetNombreArchivo(ArchivoServicios);
        public string ArchivoCatalogoServiciosNombre => GetNombreArchivo(ArchivoCatalogoServicios);
        public bool ArchivoCategoriasCargado => !string.IsNullOrWhiteSpace(ArchivoCategorias);
        public bool ArchivoPadronCargado => !string.IsNullOrWhiteSpace(ArchivoPadron);
        public bool ArchivoConsumosCargado => !string.IsNullOrWhiteSpace(ArchivoConsumos);
        public bool ArchivoConsumosDetalleCargado => _archivosConsumosDetalle.Count > 0;
        public bool ArchivoServiciosCargado => !string.IsNullOrWhiteSpace(ArchivoServicios);
        public bool ArchivoCatalogoServiciosCargado => !string.IsNullOrWhiteSpace(ArchivoCatalogoServicios);
        public string ArchivoCategoriasEstado => BuildEstadoArchivo(ArchivoCategoriasNombre, ArchivoCategoriasCargado);
        public string ArchivoPadronEstado => BuildEstadoArchivo(ArchivoPadronNombre, ArchivoPadronCargado);
        public string ArchivoConsumosEstado => BuildEstadoArchivo(ArchivoConsumosNombre, ArchivoConsumosCargado);
        public string ArchivoConsumosDetalleEstado => BuildEstadoArchivo(ArchivoConsumosDetalleNombre, ArchivoConsumosDetalleCargado);
        public string ArchivoServiciosEstado => BuildEstadoArchivo(ArchivoServiciosNombre, ArchivoServiciosCargado);
        public string ArchivoCatalogoServiciosEstado => BuildEstadoArchivo(ArchivoCatalogoServiciosNombre, ArchivoCatalogoServiciosCargado);
        public string ArchivoCategoriasIcono => ArchivoCategoriasCargado ? "✓" : "↑";
        public string ArchivoPadronIcono => ArchivoPadronCargado ? "✓" : "↑";
        public string ArchivoConsumosIcono => ArchivoConsumosCargado ? "✓" : "↑";
        public string ArchivoConsumosDetalleIcono => ArchivoConsumosDetalleCargado ? "✓" : "↑";
        public string? ArchivoConsumosDetalleToolTip => GetArchivosConsumosDetalleToolTip();
        public string ArchivoServiciosIcono => ArchivoServiciosCargado ? "✓" : "↑";
        public string ArchivoCatalogoServiciosIcono => ArchivoCatalogoServiciosCargado ? "✓" : "↑";

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

        public ObservableCollection<LogEntry> Logs { get; }
        public ObservableCollection<Empleador> Empleador { get; }
        public ObservableCollection<Entidad> Entidad { get; }

        public ICommand SeleccionarCategoriasCommand { get; }
        public ICommand SeleccionarPadronCommand { get; }
        public ICommand SeleccionarConsumosCommand { get; }
        public ICommand SeleccionarConsumosDetalleCommand { get; }
        public ICommand SeleccionarServiciosCommand { get; }
        public ICommand SeleccionarCatalogoServiciosCommand { get; }
        public ICommand LimpiarCategoriasArchivoCommand { get; }
        public ICommand LimpiarPadronArchivoCommand { get; }
        public ICommand LimpiarConsumosArchivoCommand { get; }
        public ICommand LimpiarConsumosDetalleArchivoCommand { get; }
        public ICommand LimpiarServiciosArchivoCommand { get; }
        public ICommand LimpiarCatalogoServiciosArchivoCommand { get; }
        public ICommand ValidarCommand { get; }
        public ICommand CopiarCommand { get; }
        public ICommand ExportarLogCommand { get; }
        public ICommand LimpiarUiCommand { get; }
        public ICommand LimpiarBaseEntidadCommand { get; }

        public MainViewModel()
        {
            _dbContextFactory = new AppDbContextFactory();
            _fileImportService = new FileImportService(_dbContextFactory);
            _generalValidationService = new GeneralValidationService(_dbContextFactory);
            _implementacionService = new ImplementacionService(new ImplementacionMapperService(), _dbContextFactory);

            Logs = new ObservableCollection<LogEntry>();
            LogRaw("Esperando carga de archivos para validacion...");

            Progreso = 0;

            var conexionesService = new ConexionesConfigService();
            var empleadoresConfig = conexionesService.GetEmpleadores();
            Empleador = new ObservableCollection<Empleador>();
            var idx = 1;
            foreach (var ec in empleadoresConfig)
            {
                Empleador.Add(new Empleador
                {
                    Id = idx,
                    EmrId = idx,
                    Nombre = ec.Nombre,
                    ConnectionString = ec.ConnectionString
                });
                idx++;
            }
            Empleador.Insert(0, new Empleador { Id = 0, EmrId = 0, Nombre = "Seleccionar" });

            using (var db = _dbContextFactory.Create())
            {
                Entidad = new ObservableCollection<Entidad>(db.GetEntidad());
            }
            Entidad.Insert(0, new Entidad { Id = 0, EntId = 0, Nombre = "Seleccionar" });
            EntidadSeleccionada = Entidad.FirstOrDefault();
            EmpleadorSeleccionado = Empleador.FirstOrDefault();

            _archivosConsumosDetalle.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(ArchivoConsumosDetalleNombre));
                OnPropertyChanged(nameof(ArchivoConsumosDetalleCargado));
                OnPropertyChanged(nameof(ArchivoConsumosDetalleEstado));
                OnPropertyChanged(nameof(ArchivoConsumosDetalleIcono));
                OnPropertyChanged(nameof(ArchivoConsumosDetalleToolTip));
            };
            SeleccionarCategoriasCommand = new RelayCommand(_ => SeleccionarArchivo("Categorias"));
            SeleccionarPadronCommand = new RelayCommand(_ => SeleccionarArchivo("Padron"));
            SeleccionarConsumosCommand = new RelayCommand(_ => SeleccionarArchivo("Consumos"));
            SeleccionarConsumosDetalleCommand = new RelayCommand(_ => SeleccionarArchivo("ConsumosDetalle"));
            SeleccionarServiciosCommand = new RelayCommand(_ => SeleccionarArchivo("Servicios"));
            SeleccionarCatalogoServiciosCommand = new RelayCommand(_ => SeleccionarArchivo("CatalogoServicios"));
            LimpiarCategoriasArchivoCommand = new RelayCommand(_ => LimpiarArchivo("Categorias"));
            LimpiarPadronArchivoCommand = new RelayCommand(_ => LimpiarArchivo("Padron"));
            LimpiarConsumosArchivoCommand = new RelayCommand(_ => LimpiarArchivo("Consumos"));
            LimpiarConsumosDetalleArchivoCommand = new RelayCommand(_ => LimpiarArchivo("ConsumosDetalle"));
            LimpiarServiciosArchivoCommand = new RelayCommand(_ => LimpiarArchivo("Servicios"));
            LimpiarCatalogoServiciosArchivoCommand = new RelayCommand(_ => LimpiarArchivo("CatalogoServicios"));
            ValidarCommand = new SimpleAsyncCommand(ValidarArchivosAsync);
            CopiarCommand = new SimpleAsyncCommand(CopiarABaseAsync);
            ExportarLogCommand = new RelayCommand(_ => ExportarLog());
            LimpiarUiCommand = new RelayCommand(_ => LimpiarSoloUi(), _ => !EstaProcesando);
            LimpiarBaseEntidadCommand = new RelayCommand(LimpiarBaseEntidad, PuedeLimpiarBaseEntidad);
        }

        private ImplementacionFileSelection BuildSelection()
        {
            return new ImplementacionFileSelection
            {
                ArchivoCategorias = ArchivoCategorias,
                ArchivoPadron = ArchivoPadron,
                ArchivoConsumos = ArchivoConsumos,
                ArchivosConsumosDetalle = _archivosConsumosDetalle.ToList(),
                ArchivoServicios = ArchivoServicios,
                ArchivoCatalogoServicios = ArchivoCatalogoServicios,
                TargetConnectionString = EmpleadorSeleccionado?.ConnectionString
            };
        }

        private void SeleccionarArchivo(string tipo)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Archivos Excel (*.xls;*.xlsx)|*.xls;*.xlsx|Archivos CSV (*.csv)|*.csv|Archivos TXT (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
                Multiselect = tipo == "ConsumosDetalle"
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
                    _archivosConsumosDetalle.Clear();
                    foreach (var path in dialog.FileNames)
                    {
                        _archivosConsumosDetalle.Add(path);
                    }
                    break;
                case "Servicios":
                    ArchivoServicios = dialog.FileName;
                    break;
                case "CatalogoServicios":
                    ArchivoCatalogoServicios = dialog.FileName;
                    break;
            }
        }

        private void LimpiarArchivo(string tipo)
        {
            switch (tipo)
            {
                case "Categorias":
                    ArchivoCategorias = null;
                    break;
                case "Padron":
                    ArchivoPadron = null;
                    break;
                case "Consumos":
                    ArchivoConsumos = null;
                    break;
                case "ConsumosDetalle":
                    _archivosConsumosDetalle.Clear();
                    break;
                case "Servicios":
                    ArchivoServicios = null;
                    break;
                case "CatalogoServicios":
                    ArchivoCatalogoServicios = null;
                    break;
            }
        }

        private async Task ValidarArchivosAsync()
        {
            if (EstaProcesando)
            {
                return;
            }

            Logs.Clear();
            _fullLogForExport.Clear();
            _logTruncationMessageShown = false;
            while (_logBuffer.TryDequeue(out _)) { }

            if (!HasEntidadSeleccionadaReal())
            {
                DialogService.Show(
                    "Debe seleccionar una entidad para validar.",
                    "Validación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                ValidacionFinalizada = false;
                return;
            }

            if (!HasEmpleadorSeleccionadoReal())
            {
                DialogService.Show(
                    "Aviso: no se seleccionó empleador.",
                    "Validación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            EstaProcesando = true;
            Progreso = 0;

            _logFlushTimer = new DispatcherTimer(DispatcherPriority.Normal, Application.Current.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _logFlushTimer.Tick += (_, _) => FlushLogBuffer();
            _logFlushTimer.Start();

            try
            {
                var selection = BuildSelection();
                var progress = new Progress<int>(p => Progreso = p);

                _validationResult = await Task.Run(
                    () => _fileImportService.ValidateAndLoadFiles(selection, Log, progress));
            }
            catch (SqlException ex)
            {
                Log($"Error de base de datos al cargar o validar archivos: {ex.Message}");
                ValidacionFinalizada = false;
                DialogService.Show(
                    $"Error al consultar la base de datos (CUAD).\n\n{ex.Message}",
                    "Validación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            catch (Exception ex)
            {
                Log($"Error al validar archivos: {ex.Message}");
                ValidacionFinalizada = false;
                DialogService.Show(
                    $"Error inesperado al validar.\n\n{ex.Message}",
                    "Validación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            finally
            {
                _logFlushTimer.Stop();
                _logFlushTimer = null;
                FlushLogBuffer();
                EstaProcesando = false;
                ScheduleDeferredLogFlush();
            }

            if (!_validationResult.HuboCarga)
            {
                ValidacionFinalizada = false;
                return;
            }

            try
            {
                var entidadConsistente = _generalValidationService.ValidateEntidadConsistency(
                    _validationResult,
                    Log,
                    out var entidadComun);

                if (!entidadConsistente)
                {
                    ValidacionFinalizada = false;
                    return;
                }

                if (!MatchesSelectedEntidad(entidadComun))
                {
                    Log($"ERROR: la entidad detectada en archivos ('{entidadComun}') no coincide con la entidad seleccionada.");
                    ValidacionFinalizada = false;
                    return;
                }

                if (HasEmpleadorSeleccionadoReal() && string.IsNullOrWhiteSpace(EmpleadorSeleccionado?.ConnectionString))
                {
                    Log($"No se encontró base de datos para empleador '{EmpleadorSeleccionado?.Nombre ?? "seleccionado"}'.");
                    ValidacionFinalizada = false;
                    return;
                }

                var sinDatosPrevios = _generalValidationService.ValidateNoExistingDataForEntidad(
                    entidadComun,
                    EmpleadorSeleccionado,
                    EmpleadorSeleccionado?.ConnectionString,
                    Log);

                ValidacionFinalizada = sinDatosPrevios;
            }
            catch (SqlException ex)
            {
                Log($"Error de base de datos al validar: {ex.Message}");
                ValidacionFinalizada = false;
                DialogService.Show(
                    $"Error al consultar la base de datos.\n\n{ex.Message}",
                    "Validación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Log($"Error al validar: {ex.Message}");
                ValidacionFinalizada = false;
                DialogService.Show(
                    $"Error inesperado al validar.\n\n{ex.Message}",
                    "Validación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task CopiarABaseAsync()
        {
            if (!HasEntidadSeleccionadaReal())
            {
                DialogService.Show(
                    "Debe seleccionar una entidad antes de implementar.",
                    "Implementación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!HasEmpleadorSeleccionadoReal())
            {
                DialogService.Show(
                    "Debe seleccionar un empleador antes de implementar.",
                    "Implementación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(EmpleadorSeleccionado?.ConnectionString))
            {
                Log($"No se encontró base de datos para empleador '{EmpleadorSeleccionado?.Nombre ?? "seleccionado"}'.");
                DialogService.Show(
                    $"No se encontró base de datos para empleador '{EmpleadorSeleccionado?.Nombre}'.",
                    "Implementación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var entidadSeleccionada = EntidadSeleccionada!;
            var empleadorInfo = EmpleadorSeleccionado?.Nombre ?? "(sin empleador seleccionado)";
            Log($"Contexto de implementacion: Entidad='{entidadSeleccionada.Nombre}' (ID {entidadSeleccionada.EntId}), Empleador='{empleadorInfo}'.");

            if (!ValidacionFinalizada || !_validationResult.HuboCarga)
            {
                var resultado = DialogService.Show(
                    "Algunas validaciones no pasaron o la carga fue descartada. Desea implementar igualmente?",
                    "Confirmar Implementación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (resultado != MessageBoxResult.Yes)
                {
                    Log("Implementación cancelada por el usuario.");
                    return;
                }

                Log("El usuario confirmo implementar con validaciones pendientes.");
            }

            EstaProcesando = true;
            Progreso = 0;

            try
            {
                await _implementacionService.CopyToDatabaseAsync(
                    _validationResult,
                    BuildSelection(),
                    Log,
                    progress => Application.Current?.Dispatcher.InvokeAsync(() => Progreso = progress));
                DialogService.Show("Datos implementados correctamente.", "Implementación", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (SqlException ex)
            {
                Log($"Error de base de datos al implementar: {ex.Message}");
                DialogService.Show(
                    $"Error al escribir en la base de datos.\n\n{ex.Message}",
                    "Implementación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Log($"Error al implementar: {ex.Message}");
                DialogService.Show(
                    $"Error inesperado al implementar.\n\n{ex.Message}",
                    "Implementación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                EstaProcesando = false;
            }
        }

        private bool PuedeLimpiarBaseEntidad(object? parameter)
        {
            return HasEntidadSeleccionadaReal() && HasEmpleadorSeleccionadoReal() && !EstaProcesando;
        }

        private void LimpiarBaseEntidad(object? parameter)
        {
            if (!HasEntidadSeleccionadaReal())
            {
                DialogService.Show(
                    "Debe seleccionar una entidad para limpiar la base.",
                    "Limpieza de base",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!HasEmpleadorSeleccionadoReal() || string.IsNullOrWhiteSpace(EmpleadorSeleccionado?.ConnectionString))
            {
                var nombreEmpleador = EmpleadorSeleccionado?.Nombre ?? "seleccionado";
                Log($"No se encontró base de datos para empleador '{nombreEmpleador}'.");
                DialogService.Show(
                    string.IsNullOrWhiteSpace(EmpleadorSeleccionado?.ConnectionString)
                        ? $"No se encontró base de datos para empleador '{nombreEmpleador}'."
                        : "Debe seleccionar un empleador para limpiar la base.",
                    "Limpieza de base",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var entidadSeleccionada = EntidadSeleccionada!;
            var empleadorInfo = EmpleadorSeleccionado?.Nombre ?? "(sin empleador seleccionado)";
            var entidadNombre = entidadSeleccionada.Nombre ?? entidadSeleccionada.EntId.ToString();

            var confirmacion = DialogService.Show(
                $"Se eliminaran los datos importados de la entidad '{entidadNombre}' (ID {entidadSeleccionada.EntId}) en el contexto del empleador '{empleadorInfo}'.\n\n¿Desea continuar?",
                "Confirmar limpieza de base",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmacion != MessageBoxResult.Yes)
            {
                Log("Limpieza de base cancelada por el usuario.");
                return;
            }

            try
            {
                using var db = _dbContextFactory.Create(EmpleadorSeleccionado?.ConnectionString);
                var eliminados = db.DeleteImportedDataForEntidad(
                    entidadSeleccionada.Nombre ?? string.Empty,
                    entidadSeleccionada.EntId);

                var totalEliminado = eliminados.Padron + eliminados.ConsumoCab + eliminados.ConsumoDet;
                Log($"Limpieza ejecutada para entidad '{entidadNombre}' y empleador '{empleadorInfo}'.");
                Log($"Registros eliminados: Padron={eliminados.Padron}, ConsumoCab={eliminados.ConsumoCab}, ConsumoDet={eliminados.ConsumoDet}, Total={totalEliminado}.");

                if (totalEliminado == 0)
                {
                    Log("No se encontraron registros para eliminar con la entidad seleccionada.");
                }

                DialogService.Show(
                    $"La entidad '{entidadNombre}' fue limpiada correctamente.",
                    "Limpieza de base",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ValidacionFinalizada = false;
                Progreso = 0;
                _validationResult = new ImplementacionValidationResult();
            }
            catch (SqlException ex)
            {
                Log($"Error de base de datos al limpiar: {ex.Message}");
                DialogService.Show(
                    $"Error al consultar o modificar la base de datos.\n\n{ex.Message}",
                    "Limpieza de base",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Log($"Error al limpiar la base para la entidad seleccionada: {ex.Message}");
                DialogService.Show(
                    $"No se pudo limpiar la base.\n\n{ex.Message}",
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
            _validationResult = new ImplementacionValidationResult();

            if (teniaEstado)
            {
                Log(mensaje);
            }
        }

        private void LimpiarSoloUi()
        {
            if (EstaProcesando)
            {
                return;
            }

            EntidadSeleccionada = Entidad.FirstOrDefault();
            EmpleadorSeleccionado = Empleador.FirstOrDefault();
            ArchivoCategorias = null;
            ArchivoPadron = null;
            ArchivoConsumos = null;
            _archivosConsumosDetalle.Clear();
            ArchivoServicios = null;
            ArchivoCatalogoServicios = null;
            ValidacionFinalizada = false;
            Progreso = 0;
            _validationResult = new ImplementacionValidationResult();

            Logs.Clear();
            _fullLogForExport.Clear();
            _logTruncationMessageShown = false;
            while (_logBuffer.TryDequeue(out _)) { }
            LogRaw("Esperando carga de archivos para validacion...");
        }

        private bool MatchesSelectedEntidad(string entidadComun)
        {
            var entidadSeleccionada = EntidadSeleccionada;
            if (entidadSeleccionada == null || entidadSeleccionada.EntId <= 0 || string.IsNullOrWhiteSpace(entidadComun))
            {
                return false;
            }

            var entidadNormalizada = entidadComun.Trim();

            if (!string.IsNullOrWhiteSpace(entidadSeleccionada.Nombre) &&
                string.Equals(entidadNormalizada, entidadSeleccionada.Nombre.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (int.TryParse(entidadNormalizada, out var entidadId))
            {
                return entidadId == entidadSeleccionada.EntId;
            }

            return string.Equals(entidadNormalizada, entidadSeleccionada.EntId.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private void ExportarLog()
        {
            if (_fullLogForExport.Count == 0)
            {
                Log("No hay mensajes de log para exportar.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Guardar log",
                Filter = "Archivo de texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
                FileName = $"LogImplementacion_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                AddExtension = true,
                DefaultExt = "txt"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                File.WriteAllLines(dialog.FileName, _fullLogForExport.Select(l => l.ToExportString()));
                Log($"Log exportado a: {dialog.FileName}");
                DialogService.Show($"Log generado en:\n{dialog.FileName}", "Exportar log", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"Error al exportar el log: {ex.Message}");
                DialogService.Show($"No se pudo exportar el log.\n{ex.Message}", "Exportar log", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var prefix = GetLogPrefix(message);
            var entry = new LogEntry(timestamp, prefix, message);
            AddLogEntry(entry);
        }

        private void LogRaw(string message)
        {
            var prefix = GetLogPrefix(message);
            var entry = new LogEntry(null, prefix, message);
            AddLogEntry(entry);
        }

        private void AddLogEntry(LogEntry entry)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                _logBuffer.Enqueue(entry);
            }
            else
            {
                _fullLogForExport.Add(entry);
                if (_logTruncationMessageShown)
                    return;
                if (Logs.Count < MaxVisibleLogEntries)
                    Logs.Add(entry);
                else
                {
                    Logs.Add(LogTruncationPlaceholder);
                    _logTruncationMessageShown = true;
                }
            }
        }

        private const int MaxLogEntriesPerFlush = 200;

        private int FlushLogBuffer()
        {
            var count = 0;
            while (count < MaxLogEntriesPerFlush && _logBuffer.TryDequeue(out var entry))
            {
                _fullLogForExport.Add(entry);
                if (!_logTruncationMessageShown)
                {
                    if (Logs.Count < MaxVisibleLogEntries)
                        Logs.Add(entry);
                    else
                    {
                        Logs.Add(LogTruncationPlaceholder);
                        _logTruncationMessageShown = true;
                    }
                }
                count++;
            }
            return count;
        }

        private void ScheduleDeferredLogFlush()
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(DeferredFlushNext), DispatcherPriority.Background);
        }

        private void DeferredFlushNext()
        {
            if (FlushLogBuffer() >= MaxLogEntriesPerFlush)
                ScheduleDeferredLogFlush();
        }

        public sealed class LogEntry
        {
            public LogEntry(string? timestamp, string prefix, string messageBody)
            {
                Timestamp = timestamp;
                Prefix = prefix;
                MessageBody = messageBody;
                Message = $"{prefix} {messageBody}";
            }

            public string? Timestamp { get; }
            public string Prefix { get; }
            public string MessageBody { get; }
            public string Message { get; }

            public string ToExportString()
            {
                return string.IsNullOrEmpty(Timestamp)
                    ? Message
                    : $"{Timestamp} - {Message}";
            }
        }

        private static string GetLogPrefix(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "[INFO]";
            }

            var trimmed = message.TrimStart();

            if (trimmed.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
            {
                return "[ERROR]";
            }

            if (trimmed.StartsWith("⚠️") ||
                trimmed.StartsWith("Aviso", StringComparison.OrdinalIgnoreCase))
            {
                return "[WARN]";
            }

            // Validación no pasó: rechazadas, no se pudo validar/cargar, o error de fila
            if (trimmed.IndexOf("rechazada", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmed.IndexOf(" no se pudo ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (trimmed.IndexOf(" fila ", StringComparison.OrdinalIgnoreCase) >= 0 && trimmed.IndexOf(":", StringComparison.Ordinal) >= 0))
            {
                return "[WARN]";
            }

            return "[INFO]";
        }

        private string GetArchivosConsumosDetalleNombre()
        {
            var n = _archivosConsumosDetalle.Count;
            if (n == 0) return string.Empty;
            if (n == 1) return GetNombreArchivo(_archivosConsumosDetalle[0]);
            return $"{n} archivos";
        }

        private string? GetArchivosConsumosDetalleToolTip()
        {
            if (_archivosConsumosDetalle.Count <= 1) return null;
            return string.Join(Environment.NewLine, _archivosConsumosDetalle.Select(p => GetNombreArchivo(p)));
        }

        private static string GetNombreArchivo(string? ruta)
        {
            return string.IsNullOrWhiteSpace(ruta) ? string.Empty : Path.GetFileName(ruta);
        }

        private static string BuildEstadoArchivo(string nombreArchivo, bool cargado)
        {
            return cargado ? $"{nombreArchivo} (Cargado)" : "Pendiente";
        }

        private bool HasEntidadSeleccionadaReal()
        {
            return EntidadSeleccionada != null && EntidadSeleccionada.EntId > 0;
        }

        private bool HasEmpleadorSeleccionadoReal()
        {
            return EmpleadorSeleccionado != null && EmpleadorSeleccionado.EmrId > 0;
        }
    }
}


