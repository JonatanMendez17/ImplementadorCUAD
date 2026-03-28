using Implementador.Models;
using Implementador.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Implementador.ViewModels.Coordinators;

internal sealed class LogUiController
{
    private readonly MainLogController _logController;
    private readonly ILogger _logger;

    public LogUiController(MainLogController logController, ILogger logger)
    {
        _logController = logController;
        _logger = logger;
    }

    public IReadOnlyList<MainViewModel.LogEntry> FullLogForExport => _logController.FullLogForExport;

    public void Clear()
    {
        _logController.Clear();
    }

    public void FlushAllPendingLogs()
    {
        _logController.FlushAllPendingLogs();
    }

    public int FlushLogBuffer()
    {
        return _logController.FlushLogBuffer();
    }

    public void ScheduleDeferredLogFlush()
    {
        _logController.ScheduleDeferredLogFlush();
    }

    public void LogInformation(string message)
    {
        WriteToILogger(message, LogSeverity.Information);
    }

    public void LogWarning(string message)
    {
        WriteToILogger(message, LogSeverity.Warning);
    }

    public void LogError(string message)
    {
        WriteToILogger(message, LogSeverity.Error);
    }

    public void LogSeparator()
    {
        AddLogEntry(MainViewModel.LogEntry.CreateSeparator());
    }

    public void LogRaw(string message)
    {
        AddLogEntry(new MainViewModel.LogEntry(null, LogSeverity.Information, message));
    }

    public void OnUiLogReceived(bool isDisposed, UiLogRecord record)
    {
        if (isDisposed)
        {
            return;
        }

        var timestamp = record.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
        AddLogEntry(new MainViewModel.LogEntry(timestamp, record.Severity, record.Message));
    }

    private void AddLogEntry(MainViewModel.LogEntry entry)
    {
        _logController.AddLogEntry(entry);
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
}

