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
}
