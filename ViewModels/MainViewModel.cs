using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ImplementadorCUAD.Commands;
using ImplementadorCUAD.Infrastructure;
using ImplementadorCUAD.Models;
using ImplementadorCUAD.Services;

namespace ImplementadorCUAD.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly IAppDbContextFactory _dbContextFactory;
        private readonly FileImportService _fileImportService;
        private readonly GeneralValidationService _generalValidationService;
        private readonly ImplementationService _implementationService;
        private readonly IAppLogger _appLogger;
        private readonly ILogger _logger;
        private bool _isDisposed;
        private ImplementationValidationResult _validationResult = new();

        private Empleador? _empleadorSeleccionado;
        private Entidad? _entidadSeleccionada;
        private string? _archivoCategorias;
        private string? _archivoPadron;
        private string? _archivoConsumos;
        private readonly ObservableCollection<string> _archivosConsumosDetalle = new ObservableCollection<string>();
        private string? _archivoServicios;
        private string? _archivoCatalogoServicios;
        private int _progress;
        private bool _isProcessing;
        private bool _validationCompleted;
        private string? _implementationTime;
        private MainLogController _logController;
        private readonly MainWorkflowService _workflowService;
        private DispatcherTimer? _logFlushTimer;

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

        public string ArchivoCategoriasNombre => GetFileName(ArchivoCategorias);
        public string ArchivoPadronNombre => GetFileName(ArchivoPadron);
        public string ArchivoConsumosNombre => GetFileName(ArchivoConsumos);
        public string ArchivoConsumosDetalleNombre => GetArchivosConsumosDetalleNombre();
        public string ArchivoServiciosNombre => GetFileName(ArchivoServicios);
        public string ArchivoCatalogoServiciosNombre => GetFileName(ArchivoCatalogoServicios);
        public bool ArchivoCategoriasCargado => !string.IsNullOrWhiteSpace(ArchivoCategorias);
        public bool ArchivoPadronCargado => !string.IsNullOrWhiteSpace(ArchivoPadron);
        public bool ArchivoConsumosCargado => !string.IsNullOrWhiteSpace(ArchivoConsumos);
        public bool ArchivoConsumosDetalleCargado => _archivosConsumosDetalle.Count > 0;
        public bool ArchivoServiciosCargado => !string.IsNullOrWhiteSpace(ArchivoServicios);
        public bool ArchivoCatalogoServiciosCargado => !string.IsNullOrWhiteSpace(ArchivoCatalogoServicios);
        public string ArchivoCategoriasEstado => BuildFileStatus(ArchivoCategoriasNombre, ArchivoCategoriasCargado);
        public string ArchivoPadronEstado => BuildFileStatus(ArchivoPadronNombre, ArchivoPadronCargado);
        public string ArchivoConsumosEstado => BuildFileStatus(ArchivoConsumosNombre, ArchivoConsumosCargado);
        public string ArchivoConsumosDetalleEstado => BuildFileStatus(ArchivoConsumosDetalleNombre, ArchivoConsumosDetalleCargado);
        public string ArchivoServiciosEstado => BuildFileStatus(ArchivoServiciosNombre, ArchivoServiciosCargado);
        public string ArchivoCatalogoServiciosEstado => BuildFileStatus(ArchivoCatalogoServiciosNombre, ArchivoCatalogoServiciosCargado);
        public string ArchivoCategoriasIcono => ArchivoCategoriasCargado ? "✓" : "↑";
        public string ArchivoPadronIcono => ArchivoPadronCargado ? "✓" : "↑";
        public string ArchivoConsumosIcono => ArchivoConsumosCargado ? "✓" : "↑";
        public string ArchivoConsumosDetalleIcono => ArchivoConsumosDetalleCargado ? "✓" : "↑";
        public string? ArchivoConsumosDetalleToolTip => GetArchivosConsumosDetalleToolTip();
        public string ArchivoServiciosIcono => ArchivoServiciosCargado ? "✓" : "↑";
        public string ArchivoCatalogoServiciosIcono => ArchivoCatalogoServiciosCargado ? "✓" : "↑";

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool ValidationCompleted
        {
            get => _validationCompleted;
            set => SetProperty(ref _validationCompleted, value);
        }

        public string? ImplementationTime
        {
            get => _implementationTime;
            set => SetProperty(ref _implementationTime, value);
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
        public ICommand ValidateCommand { get; }
        public ICommand CopyCommand { get; }
        public ICommand ExportLogCommand { get; }
        public ICommand ClearUiCommand { get; }
        public ICommand ClearDataCommand { get; }

        public MainViewModel(ILogger logger)
        {
            _logger = logger;
            _dbContextFactory = new AppDbContextFactory();
            var loggerFactory = App.LoggerFactory;
            _fileImportService = new FileImportService(_dbContextFactory, loggerFactory.CreateLogger<FileImportService>());
            _generalValidationService = new GeneralValidationService(_dbContextFactory, loggerFactory.CreateLogger<GeneralValidationService>());
            _implementationService = new ImplementationService(new ImplementationMapperService(), _dbContextFactory);
            _appLogger = new AppLoggerAdapter(LogInformation, LogWarning, LogError);
            UiLogStream.LogReceived += OnUiLogReceived;

            Logs = new ObservableCollection<LogEntry>();
            _logController = new MainLogController(Logs);
            _workflowService = new MainWorkflowService(_fileImportService, _generalValidationService, _implementationService, _dbContextFactory);
            LogRaw("Esperando carga de archivos para validacion...");

            Progress = 0;

            // Inicializar colecciones con valores por defecto; se completan
            // cuando la conexión a la base ya fue validada e inicializada.
            Empleador = new ObservableCollection<Empleador>
            {
                new Empleador { Id = 0, EmrId = 0, Nombre = "Seleccionar" }
            };

            Entidad = new ObservableCollection<Entidad>
            {
                new Entidad { Id = 0, EntId = 0, Nombre = "Seleccionar" }
            };

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
            SeleccionarCategoriasCommand = new RelayCommand(_ => SelectFile("Categorias"));
            SeleccionarPadronCommand = new RelayCommand(_ => SelectFile("Padron"));
            SeleccionarConsumosCommand = new RelayCommand(_ => SelectFile("Consumos"));
            SeleccionarConsumosDetalleCommand = new RelayCommand(_ => SelectFile("ConsumosDetalle"));
            SeleccionarServiciosCommand = new RelayCommand(_ => SelectFile("Servicios"));
            SeleccionarCatalogoServiciosCommand = new RelayCommand(_ => SelectFile("CatalogoServicios"));
            LimpiarCategoriasArchivoCommand = new RelayCommand(_ => ClearFile("Categorias"));
            LimpiarPadronArchivoCommand = new RelayCommand(_ => ClearFile("Padron"));
            LimpiarConsumosArchivoCommand = new RelayCommand(_ => ClearFile("Consumos"));
            LimpiarConsumosDetalleArchivoCommand = new RelayCommand(_ => ClearFile("ConsumosDetalle"));
            LimpiarServiciosArchivoCommand = new RelayCommand(_ => ClearFile("Servicios"));
            LimpiarCatalogoServiciosArchivoCommand = new RelayCommand(_ => ClearFile("CatalogoServicios"));
            ValidateCommand = new SimpleAsyncCommand(ValidateFilesAsync);
            CopyCommand = new SimpleAsyncCommand(CopyToDatabaseAsync);
            ExportLogCommand = new RelayCommand(_ => ExportLog());
            ClearUiCommand = new RelayCommand(_ => ClearUi(), _ => !IsProcessing);
            ClearDataCommand = new RelayCommand(ClearData, CanClearEntityData);
        }

        /// <summary>
        /// Carga empleadores desde `Configuration.xml` y entidades desde la base.
        /// Debe llamarse sólo cuando la conexión a la base ya fue validada.
        /// </summary>
        public void InitializeAfterConnection()
        {
            // 1) Empleadores desde Configuration.xml
            var conexionesService = new ConnectionsConfigService();
            var empleadoresConfig = conexionesService.GetEmpleadores();

            Empleador.Clear();
            Empleador.Add(new Empleador { Id = 0, EmrId = 0, Nombre = "Seleccionar" });

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

            // 2) Entidades desde la base usando la configuración actual
            using (var db = _dbContextFactory.Create())
            {
                var entidades = db.GetEntidad();
                Entidad.Clear();
                Entidad.Add(new Entidad { Id = 0, EntId = 0, Nombre = "Seleccionar" });
                foreach (var ent in entidades)
                {
                    Entidad.Add(ent);
                }
            }

            EntidadSeleccionada = Entidad.FirstOrDefault();
            EmpleadorSeleccionado = Empleador.FirstOrDefault();
        }

        private ImplementationFileSelection BuildSelection()
        {
            return new ImplementationFileSelection
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

        private void SelectFile(string type)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Archivos Excel (*.xls;*.xlsx)|*.xls;*.xlsx|Archivos CSV (*.csv)|*.csv|Archivos TXT (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
                Multiselect = type == "ConsumosDetalle"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            switch (type)
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

        private void ClearFile(string type)
        {
            switch (type)
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

        private async Task ValidateFilesAsync()
        {
            if (IsProcessing)
            {
                return;
            }

            _logController.Clear();

            if (!HasEntidadSeleccionadaReal())
            {
                DialogService.Show(
                    "Debe seleccionar una entidad para validar.",
                    "Validación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                ValidationCompleted = false;
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

            IsProcessing = true;
            Progress = 0;

            _logFlushTimer = new DispatcherTimer(DispatcherPriority.Normal, Application.Current.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _logFlushTimer.Tick += (_, _) => FlushLogBuffer();
            _logFlushTimer.Start();

            try
            {
                var selection = BuildSelection();
                var progress = new Progress<int>(p => Progress = p);

                var outcome = await _workflowService.ValidateAsync(
                    selection,
                    EntidadSeleccionada,
                    EmpleadorSeleccionado,
                    _appLogger,
                    progress);

                _validationResult = outcome.ValidationResult;
                ValidationCompleted = outcome.ValidationCompleted;
            }
            catch (SqlException ex)
            {
                LogError($"Error de base de data al cargar o validar archivos: {ex.Message}");
                ValidationCompleted = false;
                DialogService.Show(
                    $"Error al consultar la base de data (base).\n\n{ex.Message}",
                    "Validación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            catch (Exception ex)
            {
                LogError($"Error al validar archivos: {ex.Message}");
                ValidationCompleted = false;
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
                IsProcessing = false;
                ScheduleDeferredLogFlush();
            }

            if (!_validationResult.HasLoadedData)
            {
                ValidationCompleted = false;
                return;
            }
        }

        private async Task CopyToDatabaseAsync()
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
                LogWarning($"No se encontró base de data para empleador '{EmpleadorSeleccionado?.Nombre ?? "seleccionado"}'.");
                DialogService.Show(
                    $"No se encontró base de data para empleador '{EmpleadorSeleccionado?.Nombre}'.",
                    "Implementación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var entidadSeleccionada = EntidadSeleccionada!;
            var empleadorInfo = EmpleadorSeleccionado?.Nombre ?? "(sin empleador seleccionado)";
            LogInformation($"Contexto de implementacion: Entidad='{entidadSeleccionada.Nombre}' (ID {entidadSeleccionada.EntId}), Empleador='{empleadorInfo}'.");

            if (!ValidationCompleted || !_validationResult.HasLoadedData)
            {
                var resultado = DialogService.Show(
                    "Algunas validaciones no pasaron. ¿Desea implementar igualmente?",
                    "Confirmar Implementación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (resultado != MessageBoxResult.Yes)
                {
                    LogWarning("Implementación cancelada por el usuario.");
                    return;
                }

                LogWarning("El usuario confirmo implementar con validaciones pendientes.");
            }

            IsProcessing = true;
            Progress = 0;

            ImplementationTime = null;
            var cronometro = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await _workflowService.CopyToDatabaseAsync(
                    _validationResult,
                    BuildSelection(),
                    _appLogger,
                    progress => Application.Current?.Dispatcher.InvokeAsync(() => Progress = progress));

                cronometro.Stop();
                var duracion = cronometro.Elapsed;
                var tiempoTexto = duracion.TotalSeconds < 60
                    ? $"{duracion.Seconds}.{duracion.Milliseconds:D3} seg"
                    : $"{(int)duracion.TotalMinutes} min {duracion.Seconds}.{duracion.Milliseconds:D3} seg";

                ImplementationTime = tiempoTexto;
                DialogService.Show("Datos implementados correctamente.", "Implementación", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (SqlException ex)
            {
                cronometro.Stop();
                LogError($"Error de base de data al implementar: {ex.Message}");
                DialogService.Show(
                    $"Error al escribir en la base de data.\n\n{ex.Message}",
                    "Implementación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                cronometro.Stop();
                LogError($"Error al implementar: {ex.Message}");
                DialogService.Show(
                    $"Error inesperado al implementar.\n\n{ex.Message}",
                    "Implementación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private bool CanClearEntityData(object? parameter)
        {
            return HasEntidadSeleccionadaReal() && HasEmpleadorSeleccionadoReal() && !IsProcessing;
        }

        private void ClearData(object? parameter)
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
                LogWarning($"No se encontró base de data para empleador '{nombreEmpleador}'.");
                DialogService.Show(
                    string.IsNullOrWhiteSpace(EmpleadorSeleccionado?.ConnectionString)
                        ? $"No se encontró base de data para empleador '{nombreEmpleador}'."
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
                $"Se eliminaran los data de la base para la Entidad '{entidadNombre}'\n\n¿Desea continuar?",
                "Confirmar limpieza",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmacion != MessageBoxResult.Yes)
            {
                LogWarning("Limpieza de base cancelada por el usuario.");
                return;
            }

            try
            {
                var eliminados = _workflowService.ClearEntityForEmpleador(
                    entidadSeleccionada,
                    EmpleadorSeleccionado!,
                    _appLogger);

                var totalEliminado = eliminados.Padron + eliminados.ConsumoCab + eliminados.ConsumoDet;
                LogInformation($"Limpieza ejecutada para entidad '{entidadNombre}' y empleador '{empleadorInfo}'.");
                LogInformation($"Registros eliminados: Padron={eliminados.Padron}, ConsumoCab={eliminados.ConsumoCab}, ConsumoDet={eliminados.ConsumoDet}, Total={totalEliminado}.");

                if (totalEliminado == 0)
                {
                    LogWarning("No se encontraron registros para eliminar con la entidad seleccionada.");
                }

                DialogService.Show(
                    $"La entidad '{entidadNombre}' fue limpiada correctamente de la base de data",
                    "Limpieza entidad",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ValidationCompleted = false;
                Progress = 0;
                _validationResult = new ImplementationValidationResult();
            }
            catch (SqlException ex)
            {
                LogError($"Error de base de data al limpiar: {ex.Message}");
                DialogService.Show(
                    $"Error al consultar o modificar la base de data.\n\n{ex.Message}",
                    "Limpieza de base",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                LogError($"Error al limpiar la base para la entidad seleccionada: {ex.Message}");
                DialogService.Show(
                    $"No se pudo limpiar la base.\n\n{ex.Message}",
                    "Limpieza de base",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void InvalidateValidationState(string message)
        {
            var teniaEstado = _validationResult.HasLoadedData || ValidationCompleted || Progress > 0;
            ValidationCompleted = false;
            Progress = 0;
            _validationResult = new ImplementationValidationResult();

            if (teniaEstado)
            {
                LogInformation(message);
            }
        }

        private void ClearUi()
        {
            if (IsProcessing)
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
            ValidationCompleted = false;
            Progress = 0;
            ImplementationTime = null;
            _validationResult = new ImplementationValidationResult();

            _logController.Clear();
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

        private void ExportLog()
        {
            FlushAllPendingLogs();

            if (_logController.FullLogForExport.Count == 0)
            {
                LogWarning("No hay mensajes de log para exportar.");
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
                File.WriteAllLines(dialog.FileName, _logController.FullLogForExport.Select(l => l.ToExportString()));
                LogInformation($"Log exportado a: {dialog.FileName}");
                var result = DialogService.Show(
                    $"Log generado en:\n{dialog.FileName}\n\nNota: si exporta mientras la validación sigue en proceso, este archivo puede no incluir todos los logs todavía.",
                    "Exportar log",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information,
                    primaryButtonText: "Abrir",
                    secondaryButtonText: "Cerrar");

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                LogError($"Error al exportar el log: {ex.Message}");
                DialogService.Show($"No se pudo exportar el log.\n{ex.Message}", "Exportar log", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FlushAllPendingLogs()
        {
            _logController.FlushAllPendingLogs();
        }

        private void LogInformation(string message)
        {
            Log(message, LogSeverity.Information);
        }

        private void LogWarning(string message)
        {
            Log(message, LogSeverity.Warning);
        }

        private void LogError(string message)
        {
            Log(message, LogSeverity.Error);
        }

        private void LogRaw(string message)
        {
            var entry = new LogEntry(null, LogSeverity.Information, message);
            AddLogEntry(entry);
        }

        private void Log(string message, LogSeverity severity)
        {
            WriteToILogger(message, severity);
        }

        private void OnUiLogReceived(UiLogRecord record)
        {
            if (_isDisposed)
            {
                return;
            }

            var timestamp = record.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
            var entry = new LogEntry(timestamp, record.Severity, record.Message);
            AddLogEntry(entry);
        }

        private void WriteToILogger(string message, LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Warning:
                    _logger.LogWarning("{Message}", message);
                    break;
                case LogSeverity.Error:
                    _logger.LogError("{Message}", message);
                    break;
                default:
                    _logger.LogInformation("{Message}", message);
                    break;
            }
        }

        private void AddLogEntry(LogEntry entry)
        {
            _logController.AddLogEntry(entry);
        }

        private int FlushLogBuffer()
        {
            return _logController.FlushLogBuffer();
        }

        private void ScheduleDeferredLogFlush()
        {
            _logController.ScheduleDeferredLogFlush();
        }

        public sealed class LogEntry
        {
            public LogEntry(string? timestamp, LogSeverity severity, string messageBody)
            {
                Timestamp = timestamp;
                Severity = severity;
                MessageBody = messageBody;
                Message = $"{Prefix} {messageBody}";
            }

            public string? Timestamp { get; }
            public LogSeverity Severity { get; }
            public string Prefix => GetPrefix(Severity);
            public string MessageBody { get; }
            public string Message { get; }

            public string ToExportString()
            {
                return string.IsNullOrEmpty(Timestamp)
                    ? Message
                    : $"{Timestamp} - {Message}";
            }
        }

        private static string GetPrefix(LogSeverity severity)
        {
            return severity switch
            {
                LogSeverity.Warning => "[WARN]",
                LogSeverity.Error => "[ERROR]",
                _ => "[INFO]"
            };
        }

        private string GetArchivosConsumosDetalleNombre()
        {
            var n = _archivosConsumosDetalle.Count;
            if (n == 0) return string.Empty;
            if (n == 1) return GetFileName(_archivosConsumosDetalle[0]);
            return $"{n} archivos";
        }

        private string? GetArchivosConsumosDetalleToolTip()
        {
            if (_archivosConsumosDetalle.Count <= 1) return null;
            return string.Join(Environment.NewLine, _archivosConsumosDetalle.Select(p => GetFileName(p)));
        }

        private static string GetFileName(string? ruta)
        {
            return string.IsNullOrWhiteSpace(ruta) ? string.Empty : Path.GetFileName(ruta);
        }

        private static string BuildFileStatus(string fileName, bool loaded)
        {
            return loaded ? $"{fileName} (Cargado)" : "Pendiente";
        }

        private bool HasEntidadSeleccionadaReal()
        {
            return EntidadSeleccionada != null && EntidadSeleccionada.EntId > 0;
        }

        private bool HasEmpleadorSeleccionadoReal()
        {
            return EmpleadorSeleccionado != null && EmpleadorSeleccionado.EmrId > 0;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            UiLogStream.LogReceived -= OnUiLogReceived;
            _logFlushTimer?.Stop();
            _logFlushTimer = null;
        }
    }
}


