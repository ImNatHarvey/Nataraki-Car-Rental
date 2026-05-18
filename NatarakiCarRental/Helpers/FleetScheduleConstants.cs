namespace NatarakiCarRental.Helpers;

public static class FleetScheduleConstants
{
    public static class Type
    {
        public const string Reservation = "Reservation";
        public const string Rental = "Rental";
        public const string Maintenance = "Maintenance";

        public static readonly string[] All = [Reservation, Rental, Maintenance];
    }

    public static class Status
    {
        public const string Pending = "Pending";
        public const string Reserved = "Reserved";
        public const string Rented = "Rented";
        public const string Ongoing = "Ongoing";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";

        public static readonly string[] ReservationOptions = [Pending, Reserved, Cancelled];
        public static readonly string[] RentalOptions = [Rented, Completed, Cancelled];
        public static readonly string[] MaintenanceOptions = [Ongoing, Completed, Cancelled];
        public static readonly string[] Operational = [Pending, Reserved, Rented, Ongoing];
        public static readonly string[] All = [Pending, Reserved, Rented, Ongoing, Completed, Cancelled];
    }
}
