namespace NatarakiCarRental.Models;

public sealed class CompleteTransactionRequest
{
    public int TransactionId { get; set; }
    public string ReturnCondition { get; set; } = "Good";
    public int? DaysLate { get; set; }
    public decimal AdditionalCharge { get; set; }
    public bool ChargePaid { get; set; }
    public string? ReceiptFilePath { get; set; }
    public bool BlacklistCustomer { get; set; }
    public string? BlacklistReason { get; set; }
}
