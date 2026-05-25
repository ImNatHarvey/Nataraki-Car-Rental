using System.Text;
using System.Text.RegularExpressions;

namespace NatarakiCarRental.Helpers;

public static partial class PlateNumberHelper
{
    public static string FormatPhilippinePlateInput(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        StringBuilder letters = new(3);
        StringBuilder digits = new(4);

        foreach (char character in raw.ToUpperInvariant())
        {
            if (letters.Length < 3)
            {
                if (char.IsLetter(character))
                {
                    letters.Append(character);
                }

                continue;
            }

            if (digits.Length < 4 && char.IsDigit(character))
            {
                digits.Append(character);
            }
        }

        return digits.Length == 0
            ? letters.ToString()
            : $"{letters} {digits}";
    }

    public static bool IsValidPhilippinePlate(string? plateNumber)
    {
        return !string.IsNullOrWhiteSpace(plateNumber)
            && PhilippinePlateRegex().IsMatch(plateNumber);
    }

    public static string? GetCodingDayFromPlate(string? plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
        {
            return null;
        }

        char lastDigit = plateNumber.LastOrDefault(char.IsDigit);
        return lastDigit switch
        {
            '1' or '2' => CarConstants.CodingDay.Monday,
            '3' or '4' => CarConstants.CodingDay.Tuesday,
            '5' or '6' => CarConstants.CodingDay.Wednesday,
            '7' or '8' => CarConstants.CodingDay.Thursday,
            '9' or '0' => CarConstants.CodingDay.Friday,
            _ => null
        };
    }

    [GeneratedRegex("^[A-Z]{3} \\d{3,4}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhilippinePlateRegex();
}
