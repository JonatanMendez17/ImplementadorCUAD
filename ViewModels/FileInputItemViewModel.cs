using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace ImplementadorCUAD.ViewModels;

public sealed class FileInputItemViewModel : ViewModelBase
{
    private string? _singlePath;

    public FileInputItemViewModel(string key, string title, bool isMultiple)
    {
        Key = key;
        Title = title;
        IsMultiple = isMultiple;
        Paths = new ObservableCollection<string>();
    }

    public string Key { get; }
    public string Title { get; }
    public bool IsMultiple { get; }
    public ObservableCollection<string> Paths { get; }
    public ICommand? SelectCommand { get; set; }
    public ICommand? ClearCommand { get; set; }
    public object SelectCommandParameter => Key;
    public object ClearCommandParameter => Key;

    public string? SinglePath
    {
        get => _singlePath;
        set
        {
            if (SetProperty(ref _singlePath, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public bool IsLoaded => IsMultiple ? Paths.Count > 0 : !string.IsNullOrWhiteSpace(SinglePath);
    public string Icon => IsLoaded ? "✓" : "↑";
    public string DisplayName => IsMultiple ? GetMultipleDisplayName() : GetFileName(SinglePath);
    public string Status => IsLoaded ? $"{DisplayName} (Cargado)" : "Pendiente";
    public string? ToolTip => IsMultiple && Paths.Count > 1
        ? string.Join(Environment.NewLine, Paths.Select(GetFileName))
        : null;

    public void SetFromDialogSelection(string[] selectedPaths)
    {
        if (IsMultiple)
        {
            Paths.Clear();
            foreach (var path in selectedPaths)
            {
                Paths.Add(path);
            }

            RaiseDerivedProperties();
            return;
        }

        SinglePath = selectedPaths.FirstOrDefault();
    }

    public void Clear()
    {
        if (IsMultiple)
        {
            if (Paths.Count > 0)
            {
                Paths.Clear();
                RaiseDerivedProperties();
            }

            return;
        }

        SinglePath = null;
    }

    public void RaiseDerivedProperties()
    {
        OnPropertyChanged(nameof(IsLoaded));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(ToolTip));
    }

    private string GetMultipleDisplayName()
    {
        if (Paths.Count == 0) return string.Empty;
        if (Paths.Count == 1) return GetFileName(Paths[0]);
        return $"{Paths.Count} archivos";
    }

    private static string GetFileName(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFileName(path);
    }
}
