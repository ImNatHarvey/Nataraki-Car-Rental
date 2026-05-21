namespace NatarakiCarRental.Models;

public sealed class TransactionPaymentListItem
{
    public int TransactionPaymentId { get; set; }
    public int TransactionId { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string ModeOfPayment { get; set; } = string.Empty;
    public string PaymentCategory { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? ReceiptFilePath { get; set; }
    public string? Notes { get; set; }
    public string? CreatedByUserName { get; set; }
    public bool IsArchived { get; set; }
}
