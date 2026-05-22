namespace NatarakiCarRental.Models;

public sealed class UpdateOffsiteRecordRequest
{
    public int OffsiteRecordId { get; set; }
    public string OffsiteType { get; set; } = string.Empty;
    public string? LocationName { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public decimal EstimatedCost { get; set; }
    public string? ProofFilePath { get; set; }
}
