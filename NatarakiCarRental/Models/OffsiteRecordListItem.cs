namespace NatarakiCarRental.Models;

public sealed class OffsiteRecordListItem
{
    public int OffsiteRecordId { get; set; }
    public int CarId { get; set; }
    public string CarName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
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
    public string? ProofFilePath { get; set; }
    public string? WorkResult { get; set; }
    public bool FollowUpRequired { get; set; }
    public string? FollowUpReason { get; set; }
    public string? SuggestedNextAction { get; set; }
}
