namespace NatarakiCarRental.Models;

public sealed class CreateMaintenanceTransactionRequest
{
    public int CarId { get; set; }
    public int CustomerId { get; set; } // Points to Offsite Client (CustomerType='Maintenance')
    public string MaintenanceType { get; set; } = "Maintenance"; // Maintenance, Repair, Cleaning, etc.
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal AmountPaid { get; set; }
    public string ModeOfPayment { get; set; } = "Cash";
    public string? ReceiptFilePath { get; set; }
    public string? Notes { get; set; }
}
