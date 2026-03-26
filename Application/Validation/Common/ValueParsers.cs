using System.Globalization;

namespace ImplementadorCUAD.Services.Common;

public static class ValueParsers
{
    public static bool TryParseDecimalFlexible(string text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value) ||
               decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("es-AR"), out value);
    }

    public static bool TryParseDateFlexible(string text, out DateTime date)
    {
        return DateTime.TryParse(text, CultureInfo.GetCultureInfo("es-AR"), DateTimeStyles.None, out date) ||
               DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    public static bool EqualsTrimmed(string? left, string? right)
    {
        var a = (left ?? string.Empty).Trim();
        var b = (right ?? string.Empty).Trim();
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public static bool EqualsDigitsOnly(string? left, string? right)
    {
        static string Digits(string? text) => new string((text ?? string.Empty).Where(char.IsDigit).ToArray());
        return string.Equals(Digits(left), Digits(right), StringComparison.Ordinal);
    }
}
