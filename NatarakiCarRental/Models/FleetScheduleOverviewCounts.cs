namespace NatarakiCarRental.Models;

public sealed class FleetScheduleOverviewCounts
{
    public int TodaysSchedules { get; set; }
    public int UpcomingSchedules { get; set; }
    public int ActiveMaintenanceSchedules { get; set; }
}
