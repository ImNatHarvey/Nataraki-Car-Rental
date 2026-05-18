namespace NatarakiCarRental.Models;

public sealed class TransactionListItem
{
    public int TransactionId { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public int FleetScheduleId { get; set; }
    public int CustomerId { get; set; }
    public int CarId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CarName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string TransactionStatus { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
}
