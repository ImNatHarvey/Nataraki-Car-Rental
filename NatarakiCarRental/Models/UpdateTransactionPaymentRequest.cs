namespace NatarakiCarRental.Models;

public sealed class UpdateTransactionPaymentRequest
{
    public int TransactionId { get; set; }
    public decimal AmountPaid { get; set; }
    public string ModeOfPayment { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
