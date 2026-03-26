using ImplementadorCUAD.Infrastructure;

namespace ImplementadorCUAD.Services.Validation;

public abstract class RowValidatorBase
{
    protected List<Dictionary<string, string>> FilterValidRows(
        string scope,
        List<Dictionary<string, string>> sourceRows,
        IAppLogger log,
        Func<Dictionary<string, string>, int, List<string>> validateRow,
        out int rejected)
    {
        var accepted = new List<Dictionary<string, string>>();
        rejected = 0;

        for (int i = 0; i < sourceRows.Count; i++)
        {
            var row = sourceRows[i];
            var rowNumber = i + 2;
            var errors = validateRow(row, rowNumber);

            if (errors.Count == 0)
            {
                accepted.Add(row);
                continue;
            }

            rejected++;
            log.Warn($"{scope} row {rowNumber}: {string.Join(" | ", errors)}");
        }

        return accepted;
    }
}
