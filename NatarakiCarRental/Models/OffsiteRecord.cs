namespace NatarakiCarRental.Models;

public sealed class OffsiteRecord
{
    public int OffsiteRecordId { get; set; }
    public int CarId { get; set; }
    public int? FleetScheduleId { get; set; }
    public string OffsiteType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? LocationName { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal ActualCost { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceAmount { get; set; }
    public string ModeOfPayment { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? ProofFilePath { get; set; }
    public string? WorkResult { get; set; }
    public bool FollowUpRequired { get; set; }
    public string? FollowUpReason { get; set; }
    public string? SuggestedNextAction { get; set; }
    public int? CompletedByUserId { get; set; }
    public string? CompletedByFullName { get; set; }
    public string? Notes { get; set; }
    public int? CreatedByUserId { get; set; }
    public string? CreatedByFullName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsArchived { get; set; }
}
