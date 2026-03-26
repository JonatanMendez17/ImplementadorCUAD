using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using ImplementadorCUAD.Models;
using ImplementadorCUAD.ViewModels;

namespace ImplementadorCUAD.Services
{
    /// <summary>
    /// Controla el buffer de logs (para evitar bloqueos de UI) y la exportación.
    /// </summary>
    internal sealed class MainLogController
    {
        private const int MaxVisibleLogEntries = 50;
        private const int MaxLogEntriesPerFlush = 200;

        private static readonly MainViewModel.LogEntry LogTruncationPlaceholder =
            new MainViewModel.LogEntry(null, LogSeverity.Information, "Para ver todo el log, exporte a archivo.");

        private readonly ConcurrentQueue<MainViewModel.LogEntry> _logBuffer = new();
        private readonly List<MainViewModel.LogEntry> _fullLogForExport = new();
        private readonly ObservableCollection<MainViewModel.LogEntry> _uiLogs;
        private bool _logTruncationMessageShown;

        public MainLogController(ObservableCollection<MainViewModel.LogEntry> uiLogs)
        {
            _uiLogs = uiLogs;
        }

        public void Clear()
        {
            _uiLogs.Clear();
            _fullLogForExport.Clear();
            _logTruncationMessageShown = false;
            while (_logBuffer.TryDequeue(out _)) { }
        }

        public IReadOnlyList<MainViewModel.LogEntry> FullLogForExport => _fullLogForExport;

        public void AddLogEntry(MainViewModel.LogEntry entry)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                _logBuffer.Enqueue(entry);
                return;
            }

            _fullLogForExport.Add(entry);
            if (_logTruncationMessageShown)
                return;

            if (_uiLogs.Count < MaxVisibleLogEntries)
                _uiLogs.Add(entry);
            else
            {
                _uiLogs.Add(LogTruncationPlaceholder);
                _logTruncationMessageShown = true;
            }
        }

        public int FlushLogBuffer()
        {
            var count = 0;
            while (count < MaxLogEntriesPerFlush && _logBuffer.TryDequeue(out var entry))
            {
                _fullLogForExport.Add(entry);

                if (!_logTruncationMessageShown)
                {
                    if (_uiLogs.Count < MaxVisibleLogEntries)
                        _uiLogs.Add(entry);
                    else
                    {
                        _uiLogs.Add(LogTruncationPlaceholder);
                        _logTruncationMessageShown = true;
                    }
                }

                count++;
            }

            return count;
        }

        public void FlushAllPendingLogs()
        {
            while (FlushLogBuffer() > 0) { }
        }

        public void ScheduleDeferredLogFlush()
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(DeferredFlushNext), DispatcherPriority.Background);
        }

        private void DeferredFlushNext()
        {
            if (FlushLogBuffer() >= MaxLogEntriesPerFlush)
                ScheduleDeferredLogFlush();
        }
    }
}

