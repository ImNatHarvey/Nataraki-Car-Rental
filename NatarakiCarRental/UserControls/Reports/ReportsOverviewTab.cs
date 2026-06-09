using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;

namespace NatarakiCarRental.UserControls.Reports;

public sealed class ReportsOverviewTab : UserControl, IReportTab
{
    private readonly ReportService _reportService = new();
    
    private readonly FlowLayoutPanel _financialMetricsPanel = ReportLayoutHelper.CreateMetricPanel();
    private readonly FlowLayoutPanel _fleetMetricsPanel = ReportLayoutHelper.CreateMetricPanel();

    private readonly MetricCardControl _totalRevenueCard = new();
    private readonly MetricCardControl _netProfitCard = new();
    private readonly MetricCardControl _activeRentalsCard = new();
    private readonly MetricCardControl _availableCarsCard = new();
    private readonly MetricCardControl _reservedCarsCard = new();
    private readonly MetricCardControl _maintenanceCard = new();
    private readonly MetricCardControl _lateReturnsCard = new();
    private readonly MetricCardControl _outstandingPaymentsCard = new();

    private readonly DataGridView _paymentMethodGrid = ReportLayoutHelper.CreateSummaryGrid();
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
            var summaryTask = _reportService.GetSummaryMetricsAsync(from, to);
            var profitabilityTask = _reportService.GetOperatingProfitabilityAsync(from, to);
            var operationsTask = _reportService.GetOperationsMetricsAsync(from, to);

            await Task.WhenAll(summaryTask, profitabilityTask, operationsTask);

            UpdateSummaryCards(summaryTask.Result, profitabilityTask.Result, operationsTask.Result);
            
            PopulatePaymentMethods(await _reportService.GetPaymentMethodBreakdownAsync(from, to));
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
        TableLayoutPanel layout = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 6 };

        // Financial Section
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Financial Performance"));
        layout.Controls.Add(_financialMetricsPanel);
        
        // Fleet Section
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Fleet & Operations Status"));
        layout.Controls.Add(_fleetMetricsPanel);

        // Details Section
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Business Insights"));
        layout.Controls.Add(CreateChartsLayout());

        InitCards();
        Controls.Add(layout);
    }

    private void InitCards()
    {
        ReportLayoutHelper.AddMetricCard(_financialMetricsPanel, _totalRevenueCard, IconChar.MoneyBillTrendUp, "Total Revenue", "₱0.00", "Combined payments", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_financialMetricsPanel, _netProfitCard, IconChar.HandHoldingHeart, "Net After Offsite", "₱0.00", "Revenue - Offsite Cost", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_financialMetricsPanel, _outstandingPaymentsCard, IconChar.Wallet, "Pending Payments", "0", "Unpaid or Partial", ThemeHelper.Warning);
        
        ReportLayoutHelper.AddMetricCard(_fleetMetricsPanel, _activeRentalsCard, IconChar.Key, "Active Rentals", "0", "Vehicles currently out", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_fleetMetricsPanel, _availableCarsCard, IconChar.CircleCheck, "Available Cars", "0", "Ready for rental", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_fleetMetricsPanel, _reservedCarsCard, IconChar.Bookmark, "Reserved Cars", "0", "Upcoming bookings", ThemeHelper.Warning);
        ReportLayoutHelper.AddMetricCard(_fleetMetricsPanel, _maintenanceCard, IconChar.ScrewdriverWrench, "Under Maintenance", "0", "Scheduled or Ongoing", ThemeHelper.Danger);
        ReportLayoutHelper.AddMetricCard(_fleetMetricsPanel, _lateReturnsCard, IconChar.Clock, "Overdue Rentals", "0", "Past return date", ThemeHelper.Danger);
    }

    private TableLayoutPanel CreateChartsLayout()
    {
        TableLayoutPanel grid = new() { Dock = DockStyle.Top, Height = 680, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 12, 0, 4) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Payment Method Breakdown", _paymentMethodGrid), 0, 0);
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Transaction Status", _statusBreakdownGrid), 0, 1);
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Top 5 Performance (Revenue)", _topCarsGrid), 1, 1);
        return grid;
    }

    private void LayoutCards()
    {
        ReportLayoutHelper.LayoutMetricCards(_financialMetricsPanel, [_totalRevenueCard, _netProfitCard, _outstandingPaymentsCard]);
        ReportLayoutHelper.LayoutMetricCards(_fleetMetricsPanel, [_activeRentalsCard, _availableCarsCard, _reservedCarsCard, _maintenanceCard, _lateReturnsCard]);
    }

    private void UpdateSummaryCards(ReportSummaryMetrics summary, OperatingProfitabilitySummary profitability, OperationsMetrics operations)
    {
        _totalRevenueCard.SetMetric(IconChar.MoneyBillTrendUp, "Total Revenue", ReportLayoutHelper.FormatPeso(summary.TotalRevenue), "Combined payments", ThemeHelper.Primary);
        _netProfitCard.SetMetric(IconChar.HandHoldingHeart, "Net After Offsite", ReportLayoutHelper.FormatPeso(profitability.NetAfterOffsiteCost), "Revenue - Offsite Cost", ThemeHelper.Success);
        _outstandingPaymentsCard.SetMetric(IconChar.Wallet, "Pending Payments", (summary.PartialTransactions + summary.UnpaidTransactions).ToString(), "Unpaid or Partial", ThemeHelper.Warning);

        _activeRentalsCard.SetMetric(IconChar.Key, "Active Rentals", operations.ActiveRentals.ToString(), "Vehicles currently out", ThemeHelper.Primary);
        _availableCarsCard.SetMetric(IconChar.CircleCheck, "Available Cars", operations.AvailableCars.ToString(), "Ready for rental", ThemeHelper.Success);
        _reservedCarsCard.SetMetric(IconChar.Bookmark, "Reserved Cars", operations.ReservedCars.ToString(), "Upcoming bookings", ThemeHelper.Warning);
        _maintenanceCard.SetMetric(IconChar.ScrewdriverWrench, "Under Maintenance", operations.CarsUnderMaintenance.ToString(), "Scheduled or Ongoing", ThemeHelper.Danger);
        _lateReturnsCard.SetMetric(IconChar.Clock, "Overdue Rentals", operations.LateReturns.ToString(), "Past return date", ThemeHelper.Danger);
    }

    private void PopulatePaymentMethods(IReadOnlyList<PaymentMethodBreakdownItem> items)
    {
        _paymentMethodGrid.Columns.Clear(); _paymentMethodGrid.Rows.Clear();
        _paymentMethodGrid.Columns.Add("Method", "Method");
        _paymentMethodGrid.Columns.Add("Count", "Count");
        _paymentMethodGrid.Columns.Add("Amount", "Amount");
        _paymentMethodGrid.Columns.Add("Percent", "%");
        foreach (var item in items) _paymentMethodGrid.Rows.Add(item.ModeOfPayment, item.PaymentCount, ReportLayoutHelper.FormatPeso(item.TotalAmount), ReportLayoutHelper.FormatPercent(item.Percentage));
        ReportLayoutHelper.AddEmptyRow(_paymentMethodGrid);
    }

    private void PopulateStatusBreakdown(IReadOnlyList<TransactionStatusBreakdownItem> items)
    {
        _statusBreakdownGrid.Columns.Clear(); _statusBreakdownGrid.Rows.Clear();
        _statusBreakdownGrid.Columns.Add("Status", "Status");
        _statusBreakdownGrid.Columns.Add("Count", "Count");
        foreach (var item in items) _statusBreakdownGrid.Rows.Add(item.Status, item.Count);
        ReportLayoutHelper.AddEmptyRow(_statusBreakdownGrid);
    }

    private void PopulateTopCars(IReadOnlyList<TopCarItem> items)
    {
        _topCarsGrid.Columns.Clear(); _topCarsGrid.Rows.Clear();
        _topCarsGrid.Columns.Add("Car", "Car");
        _topCarsGrid.Columns.Add("Rentals", "Count");
        _topCarsGrid.Columns.Add("Revenue", "Revenue");
        foreach (var item in items) _topCarsGrid.Rows.Add($"{item.CarName} ({item.PlateNumber})", item.RentalCount, ReportLayoutHelper.FormatPeso(item.Revenue));
        ReportLayoutHelper.AddEmptyRow(_topCarsGrid);
    }
}