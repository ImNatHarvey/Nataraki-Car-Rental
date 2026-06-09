namespace NatarakiCarRental.Helpers;

public static class TransactionConstants
{
    public static class Status
    {
        public const string Pending = "Pending";
        public const string Scheduled = "Scheduled";
        public const string Active = "Active";
        public const string Maintenance = "Maintenance";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";

        public static readonly string[] All = [Pending, Scheduled, Active, Maintenance, Completed, Cancelled];
    }

    public static class PaymentStatus
    {
        public const string Unpaid = "Unpaid";
        public const string Partial = "Partial";
        public const string Paid = "Paid";

        public static readonly string[] All = [Unpaid, Partial, Paid];
    }

    public static class ModeOfPayment
    {
        public const string Cash = "Cash";
        public const string GCash = "GCash";
        public const string BankTransfer = "Bank Transfer";
        public const string Other = "Other";

        public static readonly string[] All = [Cash, GCash, BankTransfer, Other];
    }
}
