namespace ImplementadorCUAD.Services.Common;

public static class RowValueReader
{
    public static bool TryGetFirstValue(
        Dictionary<string, string> row,
        out string value,
        params string[] possibleKeys)
    {
        foreach (var key in possibleKeys)
        {
            if (row.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    public static string GetFirstValue(
        Dictionary<string, string> row,
        params string[] possibleKeys)
    {
        return TryGetFirstValue(row, out var value, possibleKeys) ? value : string.Empty;
    }
}
