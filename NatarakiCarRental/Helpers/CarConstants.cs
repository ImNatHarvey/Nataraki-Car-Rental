namespace NatarakiCarRental.Helpers;

public static class CarConstants
{
    public static class Status
    {
        public const string Available = "Available";
        public const string Rented = "Rented";
        public const string Maintenance = "Maintenance";
        public const string Reserved = "Reserved";
        public const string Archived = "Archived";

        public static readonly string[] ManualSelectable = [Available, Maintenance];
        public static readonly string[] All = [Available, Rented, Maintenance, Reserved, Archived];
    }

    public static class Transmission
    {
        public const string Automatic = "Automatic";
        public const string Manual = "Manual";
        public const string Cvt = "CVT";

        public static readonly string[] All = [Automatic, Manual, Cvt];
    }

    public static class FuelType
    {
        public const string Gasoline = "Gasoline";
        public const string Diesel = "Diesel";
        public const string Hybrid = "Hybrid";
        public const string Electric = "Electric";

        public static readonly string[] All = [Gasoline, Diesel, Hybrid, Electric];
    }

    public static class CodingDay
    {
        public const string Monday = "Monday";
        public const string Tuesday = "Tuesday";
        public const string Wednesday = "Wednesday";
        public const string Thursday = "Thursday";
        public const string Friday = "Friday";
        public const string NotApplicable = "None / Not Applicable";

        public static readonly string[] All = [Monday, Tuesday, Wednesday, Thursday, Friday, NotApplicable];
    }
}
