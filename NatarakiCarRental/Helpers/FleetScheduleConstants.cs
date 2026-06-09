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
        public const string Scheduled = "Scheduled";
        public const string Rented = "Rented";
        public const string Ongoing = "Ongoing";
        public const string Maintenance = "Maintenance";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";

        public static readonly string[] ReservationOptions = [Pending, Scheduled, Cancelled];
        public static readonly string[] RentalOptions = [Rented, Completed, Cancelled];
        public static readonly string[] MaintenanceOptions = [Scheduled, Maintenance, Completed, Cancelled];
        public static readonly string[] Operational = [Pending, Scheduled, Rented, Ongoing, Maintenance];
        public static readonly string[] All = [Pending, Scheduled, Rented, Ongoing, Maintenance, Completed, Cancelled];
    }
}
