using NatarakiCarRental.Models;

namespace NatarakiCarRental.Models;

public sealed class DashboardOperationalData
{
    // Lists for Today's Operational Insights
    public IReadOnlyList<FleetSchedule> UpcomingSchedules { get; set; } = [];
    public IReadOnlyList<OperationsReturnItem> VehiclesDueToday { get; set; } = [];
    public IReadOnlyList<OffsiteRecordListItem> OngoingOffsite { get; set; } = [];
    public IReadOnlyList<ActivityLog> HighPriorityActivities { get; set; } = [];

    // Smart Status Panels
    public double FleetUtilizationPercentage { get; set; }
    public int AvailableCars { get; set; }
    public int ActiveCars { get; set; }
    public int ReservationLoadToday { get; set; }
    public int MaintenanceLoad { get; set; }
    public int OffsiteLoad { get; set; }
    public int PendingPaymentsCount { get; set; }
    public int OverdueTransactionsCount { get; set; }

    // Mini Analytics Snapshot
    public decimal RevenueToday { get; set; }
    public decimal RevenueThisWeek { get; set; }
    public string TopRentedVehicle { get; set; } = "N/A";
    public string MostActiveCustomer { get; set; } = "N/A";
    public string MostUsedVehicleBrand { get; set; } = "N/A";
}
