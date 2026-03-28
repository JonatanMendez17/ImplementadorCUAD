using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Implementador.Application.Import;
using Implementador.Application.Validation;
using Implementador.Application.Implementation;
using Implementador.Application.Workflows;
using Implementador.Commands;
using Implementador.Infrastructure;
using Implementador.Infrastructure.Configuration;
using Implementador.Infrastructure.Logging;
using Implementador.Models;
using Implementador.Presentation.Dialogs;
using Implementador.ViewModels.Coordinators;

namespace Implementador.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        #region Fields
        private readonly IAppDbContextFactory _dbContextFactory;
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
        private readonly MainWorkflowFacade _workflowFacade;
        private readonly FileSelectionCoordinator _fileSelectionCoordinator;
        private readonly LogUiController _logUiController;
        private DispatcherTimer? _logFlushTimer;
        private readonly AsyncRelayCommand _selectFileCommandImpl;
        private readonly AsyncRelayCommand _clearFileCommandImpl;
        private readonly AsyncRelayCommand _validateCommandImpl;
        private readonly AsyncRelayCommand _copyCommandImpl;
        private readonly AsyncRelayCommand _exportLogCommandImpl;
        private readonly AsyncRelayCommand _clearUiCommandImpl;
        private readonly AsyncRelayCommand _clearDataCommandImpl;

        // Keys de tipos de archivo soportados por la pantalla.
        internal const string FileCategorias = "Categorias";
        internal const string FilePadron = "Padron";
        internal const string FileConsumos = "Consumos";
        internal const string FileConsumosDetalle = "ConsumosDetalle";
        internal const string FileServicios = "Servicios";
        internal const string FileCatalogoServicios = "CatalogoServicios";
        #endregion

        #region Bindable Properties

        public Empleador? EmpleadorSeleccionado
        {
            get => _empleadorSeleccionado;
            set
            {
                if (SetProperty(ref _empleadorSeleccionado, value))
                {
                    InvalidateValidationState("Se actualizo el empleador seleccionado. Se reinicio el estado de validacion.");
                    RefreshCommandStates();
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
                    RefreshCommandStates();
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
                    RefreshCommandStates();
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
        public IEnumerable<FileInputItemViewModel> RequiredFileInputs =>
            FileInputs.Where(i =>
                !string.Equals(i.Key, FileCatalogoServicios, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(i.Key, FileServicios, StringComparison.OrdinalIgnoreCase));
        public IEnumerable<FileInputItemViewModel> OptionalFileInputs =>
            FileInputs.Where(i =>
                string.Equals(i.Key, FileCatalogoServicios, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(i.Key, FileServicios, StringComparison.OrdinalIgnoreCase));

        public ICommand SelectFileCommand { get; }
        public ICommand ClearFileCommand { get; }
        public ICommand ValidateCommand { get; }
        public ICommand CopyCommand { get; }
        public ICommand ExportLogCommand { get; }
        public ICommand ClearUiCommand { get; }
        public ICommand ClearDataCommand { get; }
        #endregion

        #region Constructor
        public MainViewModel(ILogger logger)
        {
            _logger = logger;
            _dbContextFactory = new AppDbContextFactory();
            var loggerFactory = App.LoggerFactory;
            var fileImportService = new FileImportService(_dbContextFactory);
            var generalValidationService = new GeneralValidationService(_dbContextFactory, loggerFactory.CreateLogger<GeneralValidationService>());
            var implementationService = new ImplementationService(new ImplementationMapperService(), _dbContextFactory);
            UiLogStream.LogReceived += OnUiLogReceived;

            Logs = new ObservableCollection<LogEntry>();
            var logController = new MainLogController(Logs);
            _logUiController = new LogUiController(logController, _logger);
            _appLogger = new AppLoggerAdapter(
                _logUiController.LogInformation,
                _logUiController.LogWarning,
                _logUiController.LogError,
                _logUiController.LogSeparator);
            var workflowService = new MainWorkflowService(fileImportService, generalValidationService, implementationService, _dbContextFactory);
            _workflowFacade = new MainWorkflowFacade(workflowService);
            _logUiController.LogRaw("Esperando carga de archivos para validacion...");

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
            RegisterFileItem(new FileInputItemViewModel(FileCategorias, "Categorias Socios", true));
            RegisterFileItem(new FileInputItemViewModel(FilePadron, "Padron Socios", true));
            RegisterFileItem(new FileInputItemViewModel(FileConsumos, "Consumos", true));
            RegisterFileItem(new FileInputItemViewModel(FileConsumosDetalle, "Consumos Detalle", true));
            RegisterFileItem(new FileInputItemViewModel(FileCatalogoServicios, "Catalogo Servicios", true));
            RegisterFileItem(new FileInputItemViewModel(FileServicios, "Consumos Servicios", true));
            _fileSelectionCoordinator = new FileSelectionCoordinator(_fileItemsByKey);

            _selectFileCommandImpl = AsyncRelayCommand.Create(SelectFileFromParameter);
            _clearFileCommandImpl = AsyncRelayCommand.Create(ClearFileFromParameter);
            _validateCommandImpl = new AsyncRelayCommand(_ => ValidateFilesAsync());
            _copyCommandImpl = new AsyncRelayCommand(_ => CopyToDatabaseAsync());
            _exportLogCommandImpl = AsyncRelayCommand.Create(_ => ExportLog());
            _clearUiCommandImpl = AsyncRelayCommand.Create(_ => ClearUi(), _ => !IsProcessing);
            _clearDataCommandImpl = AsyncRelayCommand.Create(ClearData, CanClearEntityData);

            SelectFileCommand = _selectFileCommandImpl;
            ClearFileCommand = _clearFileCommandImpl;
            AssignFileItemCommands();
            ValidateCommand = _validateCommandImpl;
            CopyCommand = _copyCommandImpl;
            ExportLogCommand = _exportLogCommandImpl;
            ClearUiCommand = _clearUiCommandImpl;
            ClearDataCommand = _clearDataCommandImpl;

            EntidadSeleccionada = Entidad.FirstOrDefault();
            EmpleadorSeleccionado = Empleador.FirstOrDefault();
        }
        #endregion

        #region Initialization / Selection
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

        /// <summary>
        /// Convierte el estado actual de FileInputs al modelo usado por los servicios.
        /// </summary>
        private ImplementationFileSelection BuildSelection()
        {
            return _fileSelectionCoordinator.BuildSelection(EmpleadorSeleccionado?.ConnectionString);
        }
        #endregion

        #region File Commands
        private void SelectFile(string type)
        {
            _fileSelectionCoordinator.SelectFile(type);
        }

        private void SelectFileFromParameter(object? parameter)
        {
            if (parameter is not string key || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            SelectFile(key);
        }

        private void ClearFile(string type)
        {
            _fileSelectionCoordinator.ClearFile(type);
        }

        private void ClearFileFromParameter(object? parameter)
        {
            if (parameter is not string key || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            ClearFile(key);
        }
        #endregion

        #region Main Workflows
        private async Task ValidateFilesAsync()
        {
            if (IsProcessing)
            {
                return;
            }

            _logUiController.Clear();

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

            _logFlushTimer = new DispatcherTimer(DispatcherPriority.Normal, WpfApplication.Current.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _logFlushTimer.Tick += (_, _) => _logUiController.FlushLogBuffer();
            _logFlushTimer.Start();

            try
            {
                var selection = BuildSelection();
                var progress = new Progress<int>(p => Progress = p);

                var outcome = await _workflowFacade.ValidateAsync(
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
                _logUiController.LogError($"Error de base de data al cargar o validar archivos: {ex.Message}");
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
                _logUiController.LogError($"Error al validar archivos: {ex.Message}");
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
                _logUiController.FlushLogBuffer();
                IsProcessing = false;
                _logUiController.ScheduleDeferredLogFlush();
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
                _logUiController.LogWarning($"No se encontró base de data para empleador '{EmpleadorSeleccionado?.Nombre ?? "seleccionado"}'.");
                DialogService.Show(
                    $"No se encontró base de data para empleador '{EmpleadorSeleccionado?.Nombre}'.",
                    "Implementación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var entidadSeleccionada = EntidadSeleccionada!;
            var empleadorInfo = EmpleadorSeleccionado?.Nombre ?? "(sin empleador seleccionado)";
            _logUiController.LogInformation($"Contexto de implementacion: Entidad='{entidadSeleccionada.Nombre}' (ID {entidadSeleccionada.EntId}), Empleador='{empleadorInfo}'.");

            if (!ValidationCompleted || !_validationResult.HasLoadedData)
            {
                var resultado = DialogService.Show(
                    "Algunas validaciones no pasaron. ¿Desea implementar igualmente?",
                    "Confirmar Implementación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (resultado != MessageBoxResult.Yes)
                {
                    _logUiController.LogWarning("Implementación cancelada por el usuario.");
                    return;
                }

                _logUiController.LogWarning("El usuario confirmo implementar sin realizar las validaciones.");
            }

            IsProcessing = true;
            Progress = 0;

            ImplementationTime = null;
            var cronometro = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var insertados = await _workflowFacade.CopyToDatabaseAsync(
                    _validationResult,
                    BuildSelection(),
                    _appLogger,
                    progress => WpfApplication.Current?.Dispatcher.InvokeAsync(() => Progress = progress));

                cronometro.Stop();
                var duracion = cronometro.Elapsed;
                var tiempoTexto = duracion.TotalSeconds < 60
                    ? $"{duracion.Seconds}.{duracion.Milliseconds:D3} seg"
                    : $"{(int)duracion.TotalMinutes} min {duracion.Seconds}.{duracion.Milliseconds:D3} seg";

                ImplementationTime = tiempoTexto;

                if (insertados > 0)
                {
                    DialogService.Show("Datos implementados correctamente.", "Implementación", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    DialogService.Show("No se insertaron registros. Los archivos cargados no contienen datos válidos para implementar.", "Implementación", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (SqlException ex)
            {
                cronometro.Stop();
                _logUiController.LogError($"Error de base de data al implementar: {ex.Message}");
                DialogService.Show(
                    $"Error al escribir en la base de data.\n\n{ex.Message}",
                    "Implementación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                cronometro.Stop();
                _logUiController.LogError($"Error al implementar: {ex.Message}");
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
            return HasEntidadSeleccionadaReal() && !IsProcessing;
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
                _logUiController.LogWarning($"No se encontró base de data para empleador '{nombreEmpleador}'.");
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
                _logUiController.LogWarning("Limpieza de base cancelada por el usuario.");
                return;
            }

            try
            {
                var eliminados = _workflowFacade.ClearEntityForEmpleador(
                    entidadSeleccionada,
                    EmpleadorSeleccionado!,
                    _appLogger);

                var totalEliminado = eliminados.Padron + eliminados.ConsumoCab + eliminados.ConsumoDet;
                _logUiController.LogInformation($"Limpieza ejecutada para entidad '{entidadNombre}' y empleador '{empleadorInfo}'.");
                _logUiController.LogInformation($"Registros eliminados: Padron={eliminados.Padron}, ConsumoCab={eliminados.ConsumoCab}, ConsumoDet={eliminados.ConsumoDet}, Total={totalEliminado}.");

                if (totalEliminado == 0)
                {
                    _logUiController.LogWarning("No se encontraron registros para eliminar con la entidad seleccionada.");
                    DialogService.Show(
                        $"No se encontraron registros de la entidad '{entidadNombre}' para eliminar en la base de data.",
                        "Limpieza entidad",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    DialogService.Show(
                        $"La entidad '{entidadNombre}' fue limpiada correctamente de la base de data.",
                        "Limpieza entidad",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                ValidationCompleted = false;
                Progress = 0;
                _validationResult = new ImplementationValidationResult();
            }
            catch (SqlException ex)
            {
                _logUiController.LogError($"Error de base de data al limpiar: {ex.Message}");
                DialogService.Show(
                    $"Error al consultar o modificar la base de data.\n\n{ex.Message}",
                    "Limpieza de base",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                _logUiController.LogError($"Error al limpiar la base para la entidad seleccionada: {ex.Message}");
                DialogService.Show(
                    $"No se pudo limpiar la base.\n\n{ex.Message}",
                    "Limpieza de base",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        #endregion

        #region UI State / Logging
        private void InvalidateValidationState(string message)
        {
            var teniaEstado = _validationResult.HasLoadedData || ValidationCompleted || Progress > 0;
            ValidationCompleted = false;
            Progress = 0;
            _validationResult = new ImplementationValidationResult();

            if (teniaEstado)
            {
                _logUiController.LogInformation(message);
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
            ClearAllFileInputs();
            ValidationCompleted = false;
            Progress = 0;
            ImplementationTime = null;
            _validationResult = new ImplementationValidationResult();

            _logUiController.Clear();
            _logUiController.LogRaw("Esperando carga de archivos para validacion...");
        }

        /// <summary>
        /// Limpia todos los archivos seleccionados en la UI.
        /// </summary>
        private void ClearAllFileInputs()
        {
            _fileSelectionCoordinator.ClearAllFileInputs();
        }

        private void ExportLog()
        {
            _logUiController.FlushAllPendingLogs();

            if (_logUiController.FullLogForExport.Count == 0)
            {
                _logUiController.LogWarning("No hay mensajes de log para exportar.");
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
                File.WriteAllLines(dialog.FileName, _logUiController.FullLogForExport.Select(l => l.ToExportString()));
                _logUiController.LogInformation($"Log exportado a: {dialog.FileName}");
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
                _logUiController.LogError($"Error al exportar el log: {ex.Message}");
                DialogService.Show($"No se pudo exportar el log.\n{ex.Message}", "Exportar log", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnUiLogReceived(UiLogRecord record)
        {
            _logUiController.OnUiLogReceived(_isDisposed, record);
        }
        #endregion

        #region Nested Types
        public sealed class LogEntry
        {
            public LogEntry(string? timestamp, LogSeverity severity, string messageBody)
            {
                Timestamp = timestamp;
                Severity = severity;
                MessageBody = messageBody;
                Message = $"{Prefix} {messageBody}";
            }

            public static LogEntry CreateSeparator() =>
                new(null, LogSeverity.Information, string.Empty) { IsSeparator = true };

            public bool IsSeparator { get; private init; }
            public string? Timestamp { get; }
            public LogSeverity Severity { get; }
            public string Prefix => GetPrefix(Severity);
            public string MessageBody { get; }
            public string Message { get; }

            public string ToExportString()
            {
                if (IsSeparator)
                    return new string('─', 60);
                return string.IsNullOrEmpty(Timestamp)
                    ? Message
                    : $"{Timestamp} - {Message}";
            }
        }
        #endregion

        #region Helpers
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

        /// <summary>
        /// Asigna comandos genéricos (select/clear) a cada item usando su Key como parámetro.
        /// </summary>
        private void AssignFileItemCommands()
        {
            foreach (var item in FileInputs)
            {
                item.SelectCommand = SelectFileCommand;
                item.ClearCommand = ClearFileCommand;
            }
        }

        private void RefreshCommandStates()
        {
            _selectFileCommandImpl.RaiseCanExecuteChanged();
            _clearFileCommandImpl.RaiseCanExecuteChanged();
            _validateCommandImpl.RaiseCanExecuteChanged();
            _copyCommandImpl.RaiseCanExecuteChanged();
            _exportLogCommandImpl.RaiseCanExecuteChanged();
            _clearUiCommandImpl.RaiseCanExecuteChanged();
            _clearDataCommandImpl.RaiseCanExecuteChanged();
        }

        private bool HasEntidadSeleccionadaReal()
        {
            return EntidadSeleccionada != null && EntidadSeleccionada.EntId > 0;
        }

        private bool HasEmpleadorSeleccionadoReal()
        {
            return EmpleadorSeleccionado != null && EmpleadorSeleccionado.EmrId > 0;
        }
        #endregion

        #region IDisposable
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
        #endregion
    }
}



