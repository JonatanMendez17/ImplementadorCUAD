using System.Windows.Input;

public class SimpleAsyncCommand : ICommand
{
    private readonly Func<Task> _execute;
    private bool _isExecuting;

    public SimpleAsyncCommand(Func<Task> execute)
    {
        _execute = execute;
    }

    public bool CanExecute(object? parameter)
        => !_isExecuting;

    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;

        _isExecuting = true;
        try
        {
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CanExecuteChanged;
}