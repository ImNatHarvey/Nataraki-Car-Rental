using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;

namespace NatarakiCarRental.UserControls.Reports;

public sealed class ReportsOverviewTab : UserControl, IReportTab
{
    private readonly ReportService _reportService = new();
    private readonly FlowLayoutPanel _metricPanel = ReportLayoutHelper.CreateMetricPanel();

    private readonly MetricCardControl _totalRevenueCard = new();
    private readonly MetricCardControl _rentalRevenueCard = new();
    private readonly MetricCardControl _extensionFeesCard = new();
    private readonly MetricCardControl _damageFeesCard = new();
    private readonly MetricCardControl _lateReturnFeesCard = new();
    private readonly MetricCardControl _paidTransactionsCard = new();
    private readonly MetricCardControl _partialUnpaidTransactionsCard = new();
    private readonly MetricCardControl _activeRentalsCard = new();
    private readonly MetricCardControl _completedRentalsCard = new();
    private readonly MetricCardControl _topEarningCarCard = new();
    private readonly MetricCardControl _mostRentedCarCard = new();

    private readonly DataGridView _paymentMethodGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _revenueCategoryGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _statusBreakdownGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _topCarsGrid = ReportLayoutHelper.CreateSummaryGrid();

    public ReportsOverviewTab()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        ReportLayoutHelper.ConfigureReportPage(this);
        InitializeLayout();
        Resize += (_, _) => LayoutCards();
    }

    public async Task LoadAsync(DateTime from, DateTime to)
    {
        try
        {
            ReportSummaryMetrics summary = await _reportService.GetSummaryMetricsAsync(from, to);
            UpdateSummaryCards(summary);
            PopulatePaymentMethods(await _reportService.GetPaymentMethodBreakdownAsync(from, to));
            PopulateRevenueCategories(await _reportService.GetRevenueByCategoryAsync(from, to));
            PopulateStatusBreakdown(await _reportService.GetTransactionStatusBreakdownAsync(from, to));
            PopulateTopCars(await _reportService.GetTopCarsByRevenueAsync(from, to, 5));
            LayoutCards();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning($"Unable to load overview reports.\n\n{exception.Message}", "Reports");
        }
    }

    private void InitializeLayout()
    {
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2
        };

        layout.Controls.Add(CreateMetricPanel());
        layout.Controls.Add(CreateChartsLayout());
        Controls.Add(layout);
    }

    private FlowLayoutPanel CreateMetricPanel()
    {
        ReportLayoutHelper.AddMetricCard(_metricPanel, _totalRevenueCard, IconChar.MoneyBillTrendUp, "Total Revenue", "₱0.00", "Combined payments", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _rentalRevenueCard, IconChar.CarSide, "Rental Revenue", "₱0.00", "Base rental charges", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _extensionFeesCard, IconChar.CalendarPlus, "Extension Fees", "₱0.00", "Extended rental days", ThemeHelper.Warning);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _damageFeesCard, IconChar.Hammer, "Damage Fees", "₱0.00", "Vehicle damage charges", ThemeHelper.Danger);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _lateReturnFeesCard, IconChar.Clock, "Late Return Fees", "₱0.00", "Overdue return penalties", ThemeHelper.Warning);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _paidTransactionsCard, IconChar.CheckCircle, "Paid Transactions", "0", "Fully settled", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _partialUnpaidTransactionsCard, IconChar.Wallet, "Outstanding Transactions", "0", "Partial or unpaid", ThemeHelper.Warning);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _activeRentalsCard, IconChar.Key, "Active Rentals", "0", "Vehicles currently out", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _completedRentalsCard, IconChar.FlagCheckered, "Completed Rentals", "0", "Rentals closed in range", ThemeHelper.GrayIcon);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _topEarningCarCard, IconChar.Trophy, "Top Earning Car", "-", "Highest revenue generated", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _mostRentedCarCard, IconChar.Star, "Most Rented Car", "-", "Highest rental frequency", ThemeHelper.Primary);
        LayoutCards();
        return _metricPanel;
    }

    private TableLayoutPanel CreateChartsLayout()
    {
        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Top,
            Height = 680,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 10, 0, 0)
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Payment Method Breakdown", _paymentMethodGrid), 0, 0);
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Revenue by Category", _revenueCategoryGrid), 1, 0);
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Transaction Status", _statusBreakdownGrid), 0, 1);
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Top 5 Performance (Revenue)", _topCarsGrid), 1, 1);
        return grid;
    }

    private void LayoutCards() => ReportLayoutHelper.LayoutMetricCards(_metricPanel, GetCards());

    private List<Control> GetCards() =>
    [
        _totalRevenueCard, _rentalRevenueCard, _extensionFeesCard, _damageFeesCard,
        _lateReturnFeesCard, _paidTransactionsCard, _partialUnpaidTransactionsCard, _activeRentalsCard,
        _completedRentalsCard, _topEarningCarCard, _mostRentedCarCard
    ];

    private void UpdateSummaryCards(ReportSummaryMetrics metrics)
    {
        _totalRevenueCard.SetMetric(IconChar.MoneyBillTrendUp, "Total Revenue", ReportLayoutHelper.FormatPeso(metrics.TotalRevenue), "Combined payments", ThemeHelper.Primary);
        _rentalRevenueCard.SetMetric(IconChar.CarSide, "Rental Revenue", ReportLayoutHelper.FormatPeso(metrics.RentalRevenue), "Base rental charges", ThemeHelper.Success);
        _extensionFeesCard.SetMetric(IconChar.CalendarPlus, "Extension Fees", ReportLayoutHelper.FormatPeso(metrics.ExtensionFees), "Extended rental days", ThemeHelper.Warning);
        _damageFeesCard.SetMetric(IconChar.Hammer, "Damage Fees", ReportLayoutHelper.FormatPeso(metrics.DamageFees), "Vehicle damage charges", ThemeHelper.Danger);
        _lateReturnFeesCard.SetMetric(IconChar.Clock, "Late Return Fees", ReportLayoutHelper.FormatPeso(metrics.LateReturnFees), "Overdue return penalties", ThemeHelper.Warning);
        _paidTransactionsCard.SetMetric(IconChar.CheckCircle, "Paid Transactions", metrics.PaidTransactions.ToString(), "Fully settled", ThemeHelper.Success);
        _partialUnpaidTransactionsCard.SetMetric(IconChar.Wallet, "Outstanding Transactions", (metrics.PartialTransactions + metrics.UnpaidTransactions).ToString(), "Partial or unpaid", ThemeHelper.Warning);
        _activeRentalsCard.SetMetric(IconChar.Key, "Active Rentals", metrics.ActiveRentals.ToString(), "Vehicles currently out", ThemeHelper.Primary);
        _completedRentalsCard.SetMetric(IconChar.FlagCheckered, "Completed Rentals", metrics.CompletedRentals.ToString(), "Rentals closed in range", ThemeHelper.GrayIcon);
        _topEarningCarCard.SetMetric(IconChar.Trophy, "Top Earning Car", metrics.TopEarningCar ?? "-", $"Revenue: {ReportLayoutHelper.FormatPeso(metrics.TopEarningCarRevenue)}", ThemeHelper.Success);
        _mostRentedCarCard.SetMetric(IconChar.Star, "Most Rented Car", metrics.MostRentedCar ?? "-", $"{metrics.MostRentedCarCount} rental(s)", ThemeHelper.Primary);
    }

    private void PopulatePaymentMethods(IReadOnlyList<PaymentMethodBreakdownItem> items)
    {
        _paymentMethodGrid.Columns.Clear();
        _paymentMethodGrid.Rows.Clear();
        _paymentMethodGrid.Columns.Add("Method", "Method");
        _paymentMethodGrid.Columns.Add("Count", "Count");
        _paymentMethodGrid.Columns.Add("Amount", "Amount");
        _paymentMethodGrid.Columns.Add("Percent", "%");
        foreach (var item in items)
        {
            _paymentMethodGrid.Rows.Add(item.ModeOfPayment, item.PaymentCount, ReportLayoutHelper.FormatPeso(item.TotalAmount), ReportLayoutHelper.FormatPercent(item.Percentage));
        }
        ReportLayoutHelper.AddEmptyRow(_paymentMethodGrid);
    }

    private void PopulateRevenueCategories(IReadOnlyList<RevenueByCategoryItem> items)
    {
        _revenueCategoryGrid.Columns.Clear();
        _revenueCategoryGrid.Rows.Clear();
        _revenueCategoryGrid.Columns.Add("Category", "Category");
        _revenueCategoryGrid.Columns.Add("Count", "Count");
        _revenueCategoryGrid.Columns.Add("Amount", "Amount");
        _revenueCategoryGrid.Columns.Add("Percent", "%");
        foreach (var item in items)
        {
            _revenueCategoryGrid.Rows.Add(item.PaymentCategory, item.PaymentCount, ReportLayoutHelper.FormatPeso(item.TotalAmount), ReportLayoutHelper.FormatPercent(item.Percentage));
        }
        ReportLayoutHelper.AddEmptyRow(_revenueCategoryGrid);
    }

    private void PopulateStatusBreakdown(IReadOnlyList<TransactionStatusBreakdownItem> items)
    {
        _statusBreakdownGrid.Columns.Clear();
        _statusBreakdownGrid.Rows.Clear();
        _statusBreakdownGrid.Columns.Add("Status", "Status");
        _statusBreakdownGrid.Columns.Add("Count", "Count");
        foreach (var item in items)
        {
            _statusBreakdownGrid.Rows.Add(item.Status, item.Count);
        }
        ReportLayoutHelper.AddEmptyRow(_statusBreakdownGrid);
    }

    private void PopulateTopCars(IReadOnlyList<TopCarItem> items)
    {
        _topCarsGrid.Columns.Clear();
        _topCarsGrid.Rows.Clear();
        _topCarsGrid.Columns.Add("Car", "Car");
        _topCarsGrid.Columns.Add("Rentals", "Count");
        _topCarsGrid.Columns.Add("Revenue", "Revenue");
        foreach (var item in items)
        {
            _topCarsGrid.Rows.Add($"{item.CarName} ({item.PlateNumber})", item.RentalCount, ReportLayoutHelper.FormatPeso(item.Revenue));
        }
        ReportLayoutHelper.AddEmptyRow(_topCarsGrid);
    }
}
