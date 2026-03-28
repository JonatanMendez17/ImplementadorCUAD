using Implementador.Models;
using Microsoft.Win32;

namespace Implementador.ViewModels.Coordinators;

public sealed class FileSelectionCoordinator
{
    private readonly Dictionary<string, FileInputItemViewModel> _fileItemsByKey;

    public FileSelectionCoordinator(Dictionary<string, FileInputItemViewModel> fileItemsByKey)
    {
        _fileItemsByKey = fileItemsByKey;
    }

    public ImplementationFileSelection BuildSelection(string? targetConnectionString)
    {
        return new ImplementationFileSelection
        {
            ArchivosCategorias = GetPaths(MainViewModel.FileCategorias),
            ArchivosPadron = GetPaths(MainViewModel.FilePadron),
            ArchivosConsumos = GetPaths(MainViewModel.FileConsumos),
            ArchivosConsumosDetalle = GetPaths(MainViewModel.FileConsumosDetalle),
            ArchivosServicios = GetPaths(MainViewModel.FileServicios),
            ArchivosCatalogoServicios = GetPaths(MainViewModel.FileCatalogoServicios),
            TargetConnectionString = targetConnectionString
        };
    }

    public void SelectFile(string key)
    {
        var item = GetFileItem(key);
        var dialog = new OpenFileDialog
        {
            Filter = "Archivos soportados (*.xls;*.xlsx;*.csv;*.txt)|*.xls;*.xlsx;*.csv;*.txt|Archivos Excel (*.xls;*.xlsx)|*.xls;*.xlsx|Archivos CSV (*.csv)|*.csv|Archivos TXT (*.txt)|*.txt",
            Multiselect = item.IsMultiple
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        item.SetFromDialogSelection(dialog.FileNames);
    }

    public void ClearFile(string key)
    {
        GetFileItem(key).Clear();
    }

    public void ClearAllFileInputs()
    {
        foreach (var item in _fileItemsByKey.Values)
        {
            item.Clear();
        }
    }

    private FileInputItemViewModel GetFileItem(string key) => _fileItemsByKey[key];

    private List<string> GetPaths(string key) => GetFileItem(key).Paths.ToList();
}
