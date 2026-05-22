namespace NatarakiCarRental.Models;

public sealed class PaymentMethodBreakdownItem
{
    public string ModeOfPayment { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int PaymentCount { get; set; }
    public decimal Percentage { get; set; }
}

public sealed class RevenueByCategoryItem
{
    public string PaymentCategory { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int PaymentCount { get; set; }
    public decimal Percentage { get; set; }
}

public sealed class TransactionStatusBreakdownItem
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class TopCarItem
{
    public string CarName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int RentalCount { get; set; }
    public decimal AverageRevenue { get; set; }
}

public sealed class RevenueByCustomerItem
{
    public string CustomerName { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal OutstandingBalance { get; set; }
}

public sealed class FleetPerformanceMetrics
{
    public decimal TotalFleetRevenue { get; set; }
    public decimal AverageRevenuePerCar { get; set; }
    public string? TopEarningCar { get; set; }
    public decimal TopEarningCarRevenue { get; set; }
    public string? MostRentedCar { get; set; }
    public int MostRentedCarCount { get; set; }
    public decimal AverageUtilizationRate { get; set; }
    public int ActiveRentals { get; set; }
    public int CompletedRentals { get; set; }
    public int CarsUnderMaintenance { get; set; }
}

public sealed class FleetUtilizationItem
{
    public string CarName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public int RentedDays { get; set; }
    public int AvailableDays { get; set; }
    public decimal UtilizationRate { get; set; }
    public int RentalCount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class FleetRevenuePerCarItem
{
    public string CarName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public decimal RentalRevenue { get; set; }
    public decimal ExtensionFees { get; set; }
    public decimal DamageFees { get; set; }
    public decimal LateFees { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageRevenuePerRental { get; set; }
}

public sealed class FleetMaintenanceItem
{
    public string CarName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class OperationsMetrics
{
    public int UpcomingReturns { get; set; }
    public int LateReturns { get; set; }
    public int ActiveRentals { get; set; }
    public int UpcomingReservations { get; set; }
    public int ReservedCars { get; set; }
    public int CarsUnderMaintenance { get; set; }
    public int AvailableCars { get; set; }
    public int CompletedReturns { get; set; }
}

public sealed class OperationsReturnItem
{
    public DateTime ExpectedReturn { get; set; }
    public int DaysLate { get; set; }
    public decimal EstimatedLateFee { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string CarName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
}

public sealed class OperationsActiveRentalItem
{
    public string TransactionCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string CarName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}

public sealed class OperationsReservationItem
{
    public DateTime ScheduleDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string CarName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
}

public sealed class OperationsMaintenanceItem
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string CarName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public sealed class OperationsAvailableCarItem
{
    public string CarName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal RatePerDay { get; set; }
    public int? SeatingCapacity { get; set; }
}
