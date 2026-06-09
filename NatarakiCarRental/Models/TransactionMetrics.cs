namespace NatarakiCarRental.Models;

public sealed class TransactionMetrics
{
    public int TotalTransactions { get; set; }
    public int ActiveTransactions { get; set; }
    public int UnpaidTransactions { get; set; }
    public int CompletedTransactions { get; set; }
    public int MaintenanceTransactions { get; set; }
}
