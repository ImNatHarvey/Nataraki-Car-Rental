namespace NatarakiCarRental.Models;

public sealed class TransactionPayment
{
    public int TransactionPaymentId { get; set; }
    public int TransactionId { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string ModeOfPayment { get; set; } = string.Empty;
    public string PaymentCategory { get; set; } = "Rental Payment";
    public string? ReferenceNumber { get; set; }
    public string? ReceiptFilePath { get; set; }
    public string? Notes { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsArchived { get; set; }
}
