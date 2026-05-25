namespace NatarakiCarRental.Helpers;

public static class CodingDayValidationHelper
{
    public static bool DateRangeContainsCodingDay(DateTime startDate, DateTime endDate, string? codingDay)
    {
        if (!TryParseCodingDay(codingDay, out DayOfWeek blockedDay))
        {
            return false;
        }

        DateTime current = startDate.Date;
        DateTime end = endDate.Date;

        while (current <= end)
        {
            if (current.DayOfWeek == blockedDay)
            {
                return true;
            }

            current = current.AddDays(1);
        }

        return false;
    }

    public static bool TryParseCodingDay(string? codingDay, out DayOfWeek dayOfWeek)
    {
        dayOfWeek = default;

        if (string.IsNullOrWhiteSpace(codingDay)
            || codingDay.Equals(CarConstants.CodingDay.NotApplicable, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Enum.TryParse(codingDay.Trim(), ignoreCase: true, out dayOfWeek)
            && dayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;
    }
}
