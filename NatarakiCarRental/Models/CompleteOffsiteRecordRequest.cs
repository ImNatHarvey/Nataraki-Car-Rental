namespace NatarakiCarRental.Models;

public sealed class CompleteOffsiteRecordRequest
{
    public int OffsiteRecordId { get; set; }
    public DateTime CompletedDate { get; set; }
    public string WorkResult { get; set; } = string.Empty;
    public decimal AmountPaid { get; set; }
    public string? ProofFilePath { get; set; }
    public bool FollowUpRequired { get; set; }
    public string? FollowUpReason { get; set; }
    public string? SuggestedNextAction { get; set; }
    public int? CompletedByUserId { get; set; }
}
