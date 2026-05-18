namespace NatarakiCarRental.Models;

public sealed class Transaction
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
    public decimal DailyRate { get; set; }
    public int TotalDays { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceAmount { get; set; }
    public string ModeOfPayment { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string TransactionStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public bool IsArchived { get; set; }
}
