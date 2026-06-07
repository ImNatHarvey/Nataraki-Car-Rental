using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;

namespace NatarakiCarRental.UserControls.Reports;

public sealed class ReportsCustomersTab : UserControl, IReportTab
{
    private readonly ReportService _reportService = new();
    
    private readonly FlowLayoutPanel _growthMetricsPanel = ReportLayoutHelper.CreateMetricPanel();
    private readonly FlowLayoutPanel _riskMetricsPanel = ReportLayoutHelper.CreateMetricPanel();

    private readonly MetricCardControl _activeCard = new();
    private readonly MetricCardControl _newCard = new();
    private readonly MetricCardControl _topRevenueCard = new();
    private readonly MetricCardControl _topRentalsCard = new();
    private readonly MetricCardControl _blacklistedCard = new();
    private readonly MetricCardControl _lateReturnsCard = new();
    private readonly MetricCardControl _damageFeesCard = new();
    private readonly MetricCardControl _averageRevenueCard = new();

    private readonly DataGridView _revenueGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _rentalCountGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _outstandingGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _lateReturnsGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _damageFeesGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _blacklistedGrid = ReportLayoutHelper.CreateSummaryGrid();

    public ReportsCustomersTab()
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
            UpdateSummaryCards(await _reportService.GetCustomerAnalyticsMetricsAsync(from, to));
            PopulateTopCustomersByRevenue(await _reportService.GetTopCustomersByRevenueAsync(from, to, 10));
            PopulateTopCustomersByRentalCount(await _reportService.GetTopCustomersByRentalCountAsync(from, to, 10));
            PopulateCustomerOutstandingBalances(await _reportService.GetCustomersWithOutstandingBalancesAsync(from, to));
            PopulateCustomerLateReturns(await _reportService.GetCustomersWithLateReturnsAsync(DateTime.Today));
            PopulateCustomerDamageFees(await _reportService.GetCustomersWithDamageFeesAsync(from, to));
            PopulateBlacklistedCustomers(await _reportService.GetBlacklistedCustomersReportAsync(from, to));
            LayoutCards();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning($"Unable to load customer reports.\n\n{exception.Message}", "Reports");
        }
    }

    private void InitializeLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 9 };
        
        // 1. Growth & Engagement
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Customer Growth & Engagement"));
        layout.Controls.Add(_growthMetricsPanel);
        layout.Controls.Add(CreateTopTablesLayout());
        
        // 2. Risk & Balances
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Risk & Outstanding Balances"));
        layout.Controls.Add(_riskMetricsPanel);
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Current Outstanding Balances Detail", _outstandingGrid, 340));
        layout.Controls.Add(CreateRiskTablesLayout());

        // 3. Blacklist
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Security & Blacklist Tracking"));
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Blacklisted Customer Registry", _blacklistedGrid, 320));

        InitCards();
        Controls.Add(layout);
    }

    private void InitCards()
    {
        // Growth Group
        ReportLayoutHelper.AddMetricCard(_growthMetricsPanel, _activeCard, IconChar.Users, "Active Customers", "0", "Non-blacklisted", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_growthMetricsPanel, _newCard, IconChar.UserPlus, "New Customers", "0", "Created in range", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_growthMetricsPanel, _topRevenueCard, IconChar.Trophy, "Top Earning", "-", "Highest revenue", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_growthMetricsPanel, _topRentalsCard, IconChar.Star, "Frequent Renter", "-", "Highest count", ThemeHelper.Primary);

        // Risk Group
        ReportLayoutHelper.AddMetricCard(_riskMetricsPanel, _blacklistedCard, IconChar.UserSlash, "Blacklisted", "0", "Current blocked list", ThemeHelper.Danger);
        ReportLayoutHelper.AddMetricCard(_riskMetricsPanel, _lateReturnsCard, IconChar.Clock, "Late Returners", "0", "Currently overdue", ThemeHelper.Warning);
        ReportLayoutHelper.AddMetricCard(_riskMetricsPanel, _damageFeesCard, IconChar.Hammer, "Damage Claims", "0", "In range", ThemeHelper.Danger);
        ReportLayoutHelper.AddMetricCard(_riskMetricsPanel, _averageRevenueCard, IconChar.ChartLine, "Avg / Customer", "₱0.00", "Combined avg", ThemeHelper.Success);
    }

    private TableLayoutPanel CreateTopTablesLayout()
    {
        TableLayoutPanel grid = new() { Dock = DockStyle.Top, Height = 374, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 12, 0, 4) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Top Customers by Total Revenue", _revenueGrid), 0, 0);
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Top Customers by Rental Frequency", _rentalCountGrid), 1, 0);
        return grid;
    }

    private TableLayoutPanel CreateRiskTablesLayout()
    {
        TableLayoutPanel grid = new() { Dock = DockStyle.Top, Height = 374, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 12, 0, 4) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Recent Late Returns Detail", _lateReturnsGrid), 0, 0);
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Recent Damage Fee Detail", _damageFeesGrid), 1, 0);
        return grid;
    }

    private void LayoutCards()
    {
        ReportLayoutHelper.LayoutMetricCards(_growthMetricsPanel, [_activeCard, _newCard, _topRevenueCard, _topRentalsCard]);
        ReportLayoutHelper.LayoutMetricCards(_riskMetricsPanel, [_blacklistedCard, _lateReturnsCard, _damageFeesCard, _averageRevenueCard]);
    }

    private void UpdateSummaryCards(CustomerAnalyticsMetrics metrics)
    {
        _activeCard.SetMetric(IconChar.Users, "Active Customers", metrics.TotalActiveCustomers.ToString(), "Non-blacklisted", ThemeHelper.Success);
        _newCard.SetMetric(IconChar.UserPlus, "New Customers", metrics.NewCustomers.ToString(), "Created in range", ThemeHelper.Primary);
        _topRevenueCard.SetMetric(IconChar.Trophy, "Top Earning", metrics.TopCustomerByRevenue ?? "-", $"Paid: {ReportLayoutHelper.FormatPeso(metrics.TopCustomerRevenue)}", ThemeHelper.Success);
        _topRentalsCard.SetMetric(IconChar.Star, "Frequent Renter", metrics.TopCustomerByRentals ?? "-", $"{metrics.TopCustomerRentalCount} rental(s)", ThemeHelper.Primary);
        _blacklistedCard.SetMetric(IconChar.UserSlash, "Blacklisted", metrics.BlacklistedCustomers.ToString(), "Current blocked list", ThemeHelper.Danger);
        _lateReturnsCard.SetMetric(IconChar.Clock, "Late Returners", metrics.CustomersWithLateReturns.ToString(), "Currently overdue", ThemeHelper.Warning);
        _damageFeesCard.SetMetric(IconChar.Hammer, "Damage Claims", metrics.CustomersWithDamageFees.ToString(), "In range", ThemeHelper.Danger);
        _averageRevenueCard.SetMetric(IconChar.ChartLine, "Avg / Customer", ReportLayoutHelper.FormatPeso(metrics.AverageRevenuePerCustomer), "Combined avg", ThemeHelper.Success);
    }

    private void PopulateTopCustomersByRevenue(IReadOnlyList<CustomerRevenueReportItem> items)
    {
        _revenueGrid.Columns.Clear(); _revenueGrid.Rows.Clear();
        _revenueGrid.Columns.Add("Customer", "Customer");
        _revenueGrid.Columns.Add("Contact", "Contact");
        _revenueGrid.Columns.Add("Transactions", "TX Count");
        _revenueGrid.Columns.Add("Paid", "Total Paid");
        _revenueGrid.Columns.Add("Outstanding", "Outstanding");
        foreach (CustomerRevenueReportItem item in items) _revenueGrid.Rows.Add(item.CustomerName, item.Contact, item.TransactionCount, ReportLayoutHelper.FormatPeso(item.TotalPaid), ReportLayoutHelper.FormatPeso(item.OutstandingBalance));
        ReportLayoutHelper.AddEmptyRow(_revenueGrid);
    }

    private void PopulateTopCustomersByRentalCount(IReadOnlyList<CustomerRentalCountReportItem> items)
    {
        _rentalCountGrid.Columns.Clear(); _rentalCountGrid.Rows.Clear();
        _rentalCountGrid.Columns.Add("Customer", "Customer");
        _rentalCountGrid.Columns.Add("Contact", "Contact");
        _rentalCountGrid.Columns.Add("Rentals", "Count");
        _rentalCountGrid.Columns.Add("Completed", "Closed");
        _rentalCountGrid.Columns.Add("Active", "Active");
        foreach (CustomerRentalCountReportItem item in items) _rentalCountGrid.Rows.Add(item.CustomerName, item.Contact, item.RentalCount, item.CompletedRentals, item.ActiveRentals);
        ReportLayoutHelper.AddEmptyRow(_rentalCountGrid);
    }

    private void PopulateCustomerOutstandingBalances(IReadOnlyList<CustomerOutstandingBalanceReportItem> items)
    {
        _outstandingGrid.Columns.Clear(); _outstandingGrid.Rows.Clear();
        _outstandingGrid.Columns.Add("Customer", "Customer");
        _outstandingGrid.Columns.Add("Code", "Code");
        _outstandingGrid.Columns.Add("Total", "Total");
        _outstandingGrid.Columns.Add("Paid", "Paid");
        _outstandingGrid.Columns.Add("Balance", "Balance");
        _outstandingGrid.Columns.Add("Status", "Payment Status");
        foreach (CustomerOutstandingBalanceReportItem item in items) _outstandingGrid.Rows.Add(item.CustomerName, item.TransactionCode, ReportLayoutHelper.FormatPeso(item.TotalAmount), ReportLayoutHelper.FormatPeso(item.AmountPaid), ReportLayoutHelper.FormatPeso(item.Balance), item.PaymentStatus);
        ReportLayoutHelper.AddEmptyRow(_outstandingGrid);
    }

    private void PopulateCustomerLateReturns(IReadOnlyList<CustomerLateReturnReportItem> items)
    {
        _lateReturnsGrid.Columns.Clear(); _lateReturnsGrid.Rows.Clear();
        _lateReturnsGrid.Columns.Add("Customer", "Customer");
        _lateReturnsGrid.Columns.Add("Code", "Code");
        _lateReturnsGrid.Columns.Add("Car", "Car / Plate");
        _lateReturnsGrid.Columns.Add("DaysLate", "Days Late");
        _lateReturnsGrid.Columns.Add("LateFee", "Late Fee");
        foreach (CustomerLateReturnReportItem item in items) _lateReturnsGrid.Rows.Add(item.CustomerName, item.TransactionCode, $"{item.CarName} ({item.PlateNumber})", item.DaysLate, ReportLayoutHelper.FormatPeso(item.EstimatedLateFee));
        ReportLayoutHelper.AddEmptyRow(_lateReturnsGrid);
    }

    private void PopulateCustomerDamageFees(IReadOnlyList<CustomerDamageFeeReportItem> items)
    {
        _damageFeesGrid.Columns.Clear(); _damageFeesGrid.Rows.Clear();
        _damageFeesGrid.Columns.Add("Customer", "Customer");
        _damageFeesGrid.Columns.Add("Code", "Code");
        _damageFeesGrid.Columns.Add("Car", "Car / Plate");
        _damageFeesGrid.Columns.Add("Damage", "Damage Fee");
        _damageFeesGrid.Columns.Add("Date", "Payment Date");
        foreach (CustomerDamageFeeReportItem item in items) _damageFeesGrid.Rows.Add(item.CustomerName, item.TransactionCode, $"{item.CarName} ({item.PlateNumber})", ReportLayoutHelper.FormatPeso(item.DamageFee), ReportLayoutHelper.FormatDate(item.PaymentDate));
        ReportLayoutHelper.AddEmptyRow(_damageFeesGrid);
    }

    private void PopulateBlacklistedCustomers(IReadOnlyList<BlacklistedCustomerReportItem> items)
    {
        _blacklistedGrid.Columns.Clear(); _blacklistedGrid.Rows.Clear();
        _blacklistedGrid.Columns.Add("Customer", "Customer");
        _blacklistedGrid.Columns.Add("Contact", "Contact");
        _blacklistedGrid.Columns.Add("Reason", "Blacklist Reason");
        _blacklistedGrid.Columns.Add("Status", "Status");
        foreach (BlacklistedCustomerReportItem item in items) _blacklistedGrid.Rows.Add(item.CustomerName, item.Contact, item.BlacklistReason, item.Status);
        ReportLayoutHelper.AddEmptyRow(_blacklistedGrid);
    }
}