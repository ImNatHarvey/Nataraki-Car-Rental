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
