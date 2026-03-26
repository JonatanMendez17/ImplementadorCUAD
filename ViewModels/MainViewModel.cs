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
        private readonly Dictionary<string, FileInputItemViewModel> _fileItemsByKey = new(StringComparer.Ordinal);
        private int _progress;
        private bool _isProcessing;
        private bool _validationCompleted;
        private string? _implementationTime;
        private MainLogController _logController;
        private readonly MainWorkflowService _workflowService;
        private DispatcherTimer? _logFlushTimer;

        private const string FileCategorias = "Categorias";
        private const string FilePadron = "Padron";
        private const string FileConsumos = "Consumos";
        private const string FileConsumosDetalle = "ConsumosDetalle";
        private const string FileServicios = "Servicios";
        private const string FileCatalogoServicios = "CatalogoServicios";

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
        public ObservableCollection<FileInputItemViewModel> FileInputs { get; }

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

            FileInputs = new ObservableCollection<FileInputItemViewModel>();
            RegisterFileItem(new FileInputItemViewModel(FileCategorias, "Categorias Socios", false));
            RegisterFileItem(new FileInputItemViewModel(FilePadron, "Padron Socios", false));
            RegisterFileItem(new FileInputItemViewModel(FileConsumos, "Consumos", false));
            RegisterFileItem(new FileInputItemViewModel(FileConsumosDetalle, "Consumos Detalle", true));
            RegisterFileItem(new FileInputItemViewModel(FileCatalogoServicios, "Catalogo Servicios", false));
            RegisterFileItem(new FileInputItemViewModel(FileServicios, "Consumos Servicios", false));

            EntidadSeleccionada = Entidad.FirstOrDefault();
            EmpleadorSeleccionado = Empleador.FirstOrDefault();

            SeleccionarCategoriasCommand = new RelayCommand(_ => SelectFile(FileCategorias));
            SeleccionarPadronCommand = new RelayCommand(_ => SelectFile(FilePadron));
            SeleccionarConsumosCommand = new RelayCommand(_ => SelectFile(FileConsumos));
            SeleccionarConsumosDetalleCommand = new RelayCommand(_ => SelectFile(FileConsumosDetalle));
            SeleccionarServiciosCommand = new RelayCommand(_ => SelectFile(FileServicios));
            SeleccionarCatalogoServiciosCommand = new RelayCommand(_ => SelectFile(FileCatalogoServicios));
            LimpiarCategoriasArchivoCommand = new RelayCommand(_ => ClearFile(FileCategorias));
            LimpiarPadronArchivoCommand = new RelayCommand(_ => ClearFile(FilePadron));
            LimpiarConsumosArchivoCommand = new RelayCommand(_ => ClearFile(FileConsumos));
            LimpiarConsumosDetalleArchivoCommand = new RelayCommand(_ => ClearFile(FileConsumosDetalle));
            LimpiarServiciosArchivoCommand = new RelayCommand(_ => ClearFile(FileServicios));
            LimpiarCatalogoServiciosArchivoCommand = new RelayCommand(_ => ClearFile(FileCatalogoServicios));
            AssignFileItemCommands();
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
                ArchivoCategorias = GetSinglePath(FileCategorias),
                ArchivoPadron = GetSinglePath(FilePadron),
                ArchivoConsumos = GetSinglePath(FileConsumos),
                ArchivosConsumosDetalle = GetPaths(FileConsumosDetalle),
                ArchivoServicios = GetSinglePath(FileServicios),
                ArchivoCatalogoServicios = GetSinglePath(FileCatalogoServicios),
                TargetConnectionString = EmpleadorSeleccionado?.ConnectionString
            };
        }

        private void SelectFile(string type)
        {
            var item = GetFileItem(type);
            var dialog = new OpenFileDialog
            {
                Filter = "Archivos Excel (*.xls;*.xlsx)|*.xls;*.xlsx|Archivos CSV (*.csv)|*.csv|Archivos TXT (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
                Multiselect = item.IsMultiple
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (item.IsMultiple)
            {
                item.SetFromDialogSelection(dialog.FileNames);
                return;
            }

            SetSingleFilePath(type, dialog.FileName);
        }

        private void ClearFile(string type)
        {
            var item = GetFileItem(type);
            if (item.IsMultiple)
            {
                item.Clear();
                return;
            }

            SetSingleFilePath(type, null);
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
            SetSingleFilePath(FileCategorias, null);
            SetSingleFilePath(FilePadron, null);
            SetSingleFilePath(FileConsumos, null);
            GetFileItem(FileConsumosDetalle).Clear();
            SetSingleFilePath(FileServicios, null);
            SetSingleFilePath(FileCatalogoServicios, null);
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

        private void RegisterFileItem(FileInputItemViewModel item)
        {
            _fileItemsByKey[item.Key] = item;
            FileInputs.Add(item);
            item.Paths.CollectionChanged += (_, _) => item.RaiseDerivedProperties();
        }

        private FileInputItemViewModel GetFileItem(string key)
        {
            return _fileItemsByKey[key];
        }

        private string? GetSinglePath(string key)
        {
            return GetFileItem(key).SinglePath;
        }

        private List<string> GetPaths(string key)
        {
            return GetFileItem(key).Paths.ToList();
        }

        private void SetSingleFilePath(string key, string? value)
        {
            var item = GetFileItem(key);
            if (!item.IsMultiple)
            {
                item.SinglePath = value;
            }
        }

        private void AssignFileItemCommands()
        {
            GetFileItem(FileCategorias).SelectCommand = SeleccionarCategoriasCommand;
            GetFileItem(FileCategorias).ClearCommand = LimpiarCategoriasArchivoCommand;
            GetFileItem(FilePadron).SelectCommand = SeleccionarPadronCommand;
            GetFileItem(FilePadron).ClearCommand = LimpiarPadronArchivoCommand;
            GetFileItem(FileConsumos).SelectCommand = SeleccionarConsumosCommand;
            GetFileItem(FileConsumos).ClearCommand = LimpiarConsumosArchivoCommand;
            GetFileItem(FileConsumosDetalle).SelectCommand = SeleccionarConsumosDetalleCommand;
            GetFileItem(FileConsumosDetalle).ClearCommand = LimpiarConsumosDetalleArchivoCommand;
            GetFileItem(FileCatalogoServicios).SelectCommand = SeleccionarCatalogoServiciosCommand;
            GetFileItem(FileCatalogoServicios).ClearCommand = LimpiarCatalogoServiciosArchivoCommand;
            GetFileItem(FileServicios).SelectCommand = SeleccionarServiciosCommand;
            GetFileItem(FileServicios).ClearCommand = LimpiarServiciosArchivoCommand;
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


