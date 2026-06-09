namespace NatarakiCarRental.Helpers;

public static class OffsiteConstants
{
    public static class Type
    {
        public const string Maintenance = "Maintenance";
        public const string Repair = "Repair";
        public const string Cleaning = "Cleaning";
        public const string Inspection = "Inspection";
        public const string Other = "Other";

        public static readonly string[] All = [Maintenance, Repair, Cleaning, Inspection, Other];
        public static readonly string[] MaintenanceCategory = [Maintenance, Repair, Cleaning];
    }

    public static class Status
    {
        public const string Pending = "Pending";
        public const string Reserved = "Reserved";
        public const string Ongoing = "Ongoing";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";

        public static readonly string[] All = [Pending, Reserved, Ongoing, Completed, Cancelled];
    }

    public static class WorkResult
    {
        public const string Completed = "Completed";
        public const string NeedsFollowUp = "Needs Follow-up";
        public const string NotRepaired = "Not Repaired";

        public static readonly string[] All = [Completed, NeedsFollowUp, NotRepaired];
    }
}
