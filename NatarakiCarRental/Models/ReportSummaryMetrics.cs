namespace NatarakiCarRental.Models;

public sealed class ReportSummaryMetrics
{
    public decimal TotalRevenue { get; set; }
    public decimal RentalRevenue { get; set; }
    public decimal ExtensionFees { get; set; }
    public decimal DamageFees { get; set; }
    public decimal LateReturnFees { get; set; }
    
    public int PaidTransactions { get; set; }
    public int PartialUnpaidTransactions { get; set; }
    
    public int ActiveRentals { get; set; }
    public int CompletedRentals { get; set; }
    
    public string? TopEarningCar { get; set; }
    public decimal TopEarningCarRevenue { get; set; }
    
    public string? MostRentedCar { get; set; }
    public int MostRentedCarCount { get; set; }
}
