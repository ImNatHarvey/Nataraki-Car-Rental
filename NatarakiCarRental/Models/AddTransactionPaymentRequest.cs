namespace NatarakiCarRental.Models;

public sealed class AddTransactionPaymentRequest
{
    public int TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string ModeOfPayment { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? ReceiptFilePath { get; set; }
    public string? Notes { get; set; }
}
