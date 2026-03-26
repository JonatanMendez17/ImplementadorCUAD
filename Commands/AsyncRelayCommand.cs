using System.Windows.Input;

namespace ImplementadorCUAD.Commands
{
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _executeAsync;
        private readonly Predicate<object?>? _canExecute;
        private readonly bool _allowConcurrentExecutions;
        private bool _isExecuting;

        public AsyncRelayCommand(
            Func<object?, Task> executeAsync,
            Predicate<object?>? canExecute = null,
            bool allowConcurrentExecutions = false)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
            _allowConcurrentExecutions = allowConcurrentExecutions;
        }

        public bool CanExecute(object? parameter)
        {
            if (!_allowConcurrentExecutions && _isExecuting)
            {
                return false;
            }

            return _canExecute == null || _canExecute(parameter);
        }

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            if (!_allowConcurrentExecutions)
            {
                _isExecuting = true;
                RaiseCanExecuteChanged();
            }

            try
            {
                await _executeAsync(parameter);
            }
            finally
            {
                if (!_allowConcurrentExecutions)
                {
                    _isExecuting = false;
                    RaiseCanExecuteChanged();
                }
            }
        }

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public static AsyncRelayCommand Create(
            Action<object?> execute,
            Predicate<object?>? canExecute = null)
        {
            return new AsyncRelayCommand(
                parameter =>
                {
                    execute(parameter);
                    return Task.CompletedTask;
                },
                canExecute);
        }
    }
}
