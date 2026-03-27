using Implementador.Models;
using Microsoft.Win32;

namespace Implementador.ViewModels.Coordinators;

public sealed class FileSelectionCoordinator
{
    private readonly Dictionary<string, FileInputItemViewModel> _fileItemsByKey;
    private readonly string _fileCategorias;
    private readonly string _filePadron;
    private readonly string _fileConsumos;
    private readonly string _fileConsumosDetalle;
    private readonly string _fileServicios;
    private readonly string _fileCatalogoServicios;

    public FileSelectionCoordinator(
        Dictionary<string, FileInputItemViewModel> fileItemsByKey,
        string fileCategorias,
        string filePadron,
        string fileConsumos,
        string fileConsumosDetalle,
        string fileServicios,
        string fileCatalogoServicios)
    {
        _fileItemsByKey = fileItemsByKey;
        _fileCategorias = fileCategorias;
        _filePadron = filePadron;
        _fileConsumos = fileConsumos;
        _fileConsumosDetalle = fileConsumosDetalle;
        _fileServicios = fileServicios;
        _fileCatalogoServicios = fileCatalogoServicios;
    }

    public ImplementationFileSelection BuildSelection(string? targetConnectionString)
    {
        return new ImplementationFileSelection
        {
            ArchivoCategorias = GetSinglePath(_fileCategorias),
            ArchivoPadron = GetSinglePath(_filePadron),
            ArchivoConsumos = GetSinglePath(_fileConsumos),
            ArchivosConsumosDetalle = GetPaths(_fileConsumosDetalle),
            ArchivoServicios = GetSinglePath(_fileServicios),
            ArchivoCatalogoServicios = GetSinglePath(_fileCatalogoServicios),
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

        if (item.IsMultiple)
        {
            item.SetFromDialogSelection(dialog.FileNames);
            return;
        }

        SetSingleFilePath(key, dialog.FileName);
    }

    public void ClearFile(string key)
    {
        var item = GetFileItem(key);
        if (item.IsMultiple)
        {
            item.Clear();
            return;
        }

        SetSingleFilePath(key, null);
    }

    public void ClearAllFileInputs()
    {
        foreach (var item in _fileItemsByKey.Values)
        {
            item.Clear();
        }
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
}

