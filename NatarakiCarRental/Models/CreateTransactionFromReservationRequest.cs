namespace NatarakiCarRental.Models;

public sealed class CreateTransactionFromReservationRequest
{
    public int FleetScheduleId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? DailyRate { get; set; }
    public string ModeOfPayment { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
