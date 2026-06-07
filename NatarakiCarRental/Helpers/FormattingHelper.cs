using System.Globalization;

namespace NatarakiCarRental.Helpers;

public static class FormattingHelper
{
    public static string FormatPeso(decimal amount) => $"₱{amount:N2}";

    public static string FormatPercent(decimal value) => $"{value:N1}%";

    public static string FormatDate(DateTime date) => date.ToString("MMM d, yyyy");

    public static string ValueOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    public static string EscapePdfText(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    public static string SanitizePdfText(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        
        string sanitized = value
            .Replace("₱", "PHP ", StringComparison.Ordinal)
            .Replace("â‚±", "PHP ", StringComparison.Ordinal)
            .Replace("Ã¢â€šÂ±", "PHP ", StringComparison.Ordinal);
            
        return new string(sanitized.Select(character => character <= 127 ? character : '?').ToArray());
    }

    public static string CarPlate(string carName, string plateNumber) => $"{carName} ({plateNumber})";

    public static string SplitCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        return System.Text.RegularExpressions.Regex.Replace(str, "([a-z])([A-Z])", "$1 $2");
    }

    public static string FormatCompactNumber(decimal value)
    {
        if (value >= 1000000) return $"{(value / 1000000M):N1}M";
        if (value >= 1000) return $"{(value / 1000M):N1}K";
        return value.ToString("N0");
    }
}
