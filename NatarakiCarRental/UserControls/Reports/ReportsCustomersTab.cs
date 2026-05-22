using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;

namespace NatarakiCarRental.UserControls.Reports;

public sealed class ReportsCustomersTab : UserControl, IReportTab
{
    private readonly ReportService _reportService = new();
    private readonly FlowLayoutPanel _metricPanel = ReportLayoutHelper.CreateMetricPanel();

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
        TableLayoutPanel layout = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 5 };
        layout.Controls.Add(CreateMetricPanel());
        layout.Controls.Add(CreateTopTablesLayout());
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Customers with Outstanding Balances", _outstandingGrid, 340));
        layout.Controls.Add(CreateRiskTablesLayout());
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Blacklisted Customers (Current)", _blacklistedGrid, 320));
        Controls.Add(layout);
    }

    private FlowLayoutPanel CreateMetricPanel()
    {
        ReportLayoutHelper.AddMetricCard(_metricPanel, _activeCard, IconChar.Users, "Active Customers", "0", "Current non-blacklisted", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _newCard, IconChar.UserPlus, "New Customers", "0", "Created in range", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _topRevenueCard, IconChar.Trophy, "Top Customer by Revenue", "-", "Highest total paid", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _topRentalsCard, IconChar.Star, "Top Customer by Rentals", "-", "Highest rental count", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _blacklistedCard, IconChar.UserSlash, "Blacklisted Customers", "0", "Current blocked list", ThemeHelper.Danger);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _lateReturnsCard, IconChar.Clock, "Late Return Customers", "0", "Currently overdue", ThemeHelper.Warning);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _damageFeesCard, IconChar.Hammer, "Damage Fee Customers", "0", "Damage fees in range", ThemeHelper.Danger);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _averageRevenueCard, IconChar.ChartLine, "Avg Revenue / Customer", "₱0.00", "Customers with payments", ThemeHelper.Success);
        LayoutCards();
        return _metricPanel;
    }

    private TableLayoutPanel CreateTopTablesLayout()
    {
        TableLayoutPanel grid = new() { Dock = DockStyle.Top, Height = 374, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 12, 0, 4) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Top Customers by Revenue", _revenueGrid), 0, 0);
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Top Customers by Rental Count", _rentalCountGrid), 1, 0);
        return grid;
    }

    private TableLayoutPanel CreateRiskTablesLayout()
    {
        TableLayoutPanel grid = new() { Dock = DockStyle.Top, Height = 374, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 12, 0, 4) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Customers with Late Returns", _lateReturnsGrid), 0, 0);
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Customers with Damage Fees", _damageFeesGrid), 1, 0);
        return grid;
    }

    private void LayoutCards() => ReportLayoutHelper.LayoutMetricCards(_metricPanel, GetCards());

    private List<Control> GetCards() =>
    [
        _activeCard, _newCard, _topRevenueCard, _topRentalsCard,
        _blacklistedCard, _lateReturnsCard, _damageFeesCard, _averageRevenueCard
    ];

    private void UpdateSummaryCards(CustomerAnalyticsMetrics metrics)
    {
        _activeCard.SetMetric(IconChar.Users, "Active Customers", metrics.TotalActiveCustomers.ToString(), "Current non-blacklisted", ThemeHelper.Success);
        _newCard.SetMetric(IconChar.UserPlus, "New Customers", metrics.NewCustomers.ToString(), "Created in range", ThemeHelper.Primary);
        _topRevenueCard.SetMetric(IconChar.Trophy, "Top Customer by Revenue", metrics.TopCustomerByRevenue ?? "-", $"Paid: {ReportLayoutHelper.FormatPeso(metrics.TopCustomerRevenue)}", ThemeHelper.Success);
        _topRentalsCard.SetMetric(IconChar.Star, "Top Customer by Rentals", metrics.TopCustomerByRentals ?? "-", $"{metrics.TopCustomerRentalCount} rental(s)", ThemeHelper.Primary);
        _blacklistedCard.SetMetric(IconChar.UserSlash, "Blacklisted Customers", metrics.BlacklistedCustomers.ToString(), "Current blocked list", ThemeHelper.Danger);
        _lateReturnsCard.SetMetric(IconChar.Clock, "Late Return Customers", metrics.CustomersWithLateReturns.ToString(), "Currently overdue", ThemeHelper.Warning);
        _damageFeesCard.SetMetric(IconChar.Hammer, "Damage Fee Customers", metrics.CustomersWithDamageFees.ToString(), "Damage fees in range", ThemeHelper.Danger);
        _averageRevenueCard.SetMetric(IconChar.ChartLine, "Avg Revenue / Customer", ReportLayoutHelper.FormatPeso(metrics.AverageRevenuePerCustomer), "Customers with payments", ThemeHelper.Success);
    }

    private void PopulateTopCustomersByRevenue(IReadOnlyList<CustomerRevenueReportItem> items)
    {
        _revenueGrid.Columns.Clear(); _revenueGrid.Rows.Clear();
        _revenueGrid.Columns.Add("Customer", "Customer");
        _revenueGrid.Columns.Add("Contact", "Contact");
        _revenueGrid.Columns.Add("Transactions", "Transaction Count");
        _revenueGrid.Columns.Add("Paid", "Total Paid");
        _revenueGrid.Columns.Add("Outstanding", "Outstanding Balance");
        foreach (CustomerRevenueReportItem item in items)
        {
            _revenueGrid.Rows.Add(item.CustomerName, item.Contact, item.TransactionCount, ReportLayoutHelper.FormatPeso(item.TotalPaid), ReportLayoutHelper.FormatPeso(item.OutstandingBalance));
        }
        ReportLayoutHelper.AddEmptyRow(_revenueGrid);
    }

    private void PopulateTopCustomersByRentalCount(IReadOnlyList<CustomerRentalCountReportItem> items)
    {
        _rentalCountGrid.Columns.Clear(); _rentalCountGrid.Rows.Clear();
        _rentalCountGrid.Columns.Add("Customer", "Customer");
        _rentalCountGrid.Columns.Add("Contact", "Contact");
        _rentalCountGrid.Columns.Add("Rentals", "Rental Count");
        _rentalCountGrid.Columns.Add("Completed", "Completed Rentals");
        _rentalCountGrid.Columns.Add("Active", "Active Rentals");
        _rentalCountGrid.Columns.Add("LastRental", "Last Rental Date");
        foreach (CustomerRentalCountReportItem item in items)
        {
            _rentalCountGrid.Rows.Add(item.CustomerName, item.Contact, item.RentalCount, item.CompletedRentals, item.ActiveRentals, item.LastRentalDate.HasValue ? ReportLayoutHelper.FormatDate(item.LastRentalDate.Value) : "-");
        }
        ReportLayoutHelper.AddEmptyRow(_rentalCountGrid);
    }

    private void PopulateCustomerOutstandingBalances(IReadOnlyList<CustomerOutstandingBalanceReportItem> items)
    {
        _outstandingGrid.Columns.Clear(); _outstandingGrid.Rows.Clear();
        _outstandingGrid.Columns.Add("Customer", "Customer");
        _outstandingGrid.Columns.Add("Contact", "Contact");
        _outstandingGrid.Columns.Add("Code", "Transaction Code");
        _outstandingGrid.Columns.Add("Total", "Total Amount");
        _outstandingGrid.Columns.Add("Paid", "Amount Paid");
        _outstandingGrid.Columns.Add("Balance", "Balance");
        _outstandingGrid.Columns.Add("Status", "Payment Status");
        foreach (CustomerOutstandingBalanceReportItem item in items)
        {
            _outstandingGrid.Rows.Add(item.CustomerName, item.Contact, item.TransactionCode, ReportLayoutHelper.FormatPeso(item.TotalAmount), ReportLayoutHelper.FormatPeso(item.AmountPaid), ReportLayoutHelper.FormatPeso(item.Balance), item.PaymentStatus);
        }
        ReportLayoutHelper.AddEmptyRow(_outstandingGrid);
    }

    private void PopulateCustomerLateReturns(IReadOnlyList<CustomerLateReturnReportItem> items)
    {
        _lateReturnsGrid.Columns.Clear(); _lateReturnsGrid.Rows.Clear();
        _lateReturnsGrid.Columns.Add("Customer", "Customer");
        _lateReturnsGrid.Columns.Add("Contact", "Contact");
        _lateReturnsGrid.Columns.Add("Code", "Transaction Code");
        _lateReturnsGrid.Columns.Add("Car", "Car / Plate");
        _lateReturnsGrid.Columns.Add("DaysLate", "Days Late");
        _lateReturnsGrid.Columns.Add("LateFee", "Estimated Late Fee");
        foreach (CustomerLateReturnReportItem item in items)
        {
            _lateReturnsGrid.Rows.Add(item.CustomerName, item.Contact, item.TransactionCode, $"{item.CarName} ({item.PlateNumber})", item.DaysLate, ReportLayoutHelper.FormatPeso(item.EstimatedLateFee));
        }
        ReportLayoutHelper.AddEmptyRow(_lateReturnsGrid);
    }

    private void PopulateCustomerDamageFees(IReadOnlyList<CustomerDamageFeeReportItem> items)
    {
        _damageFeesGrid.Columns.Clear(); _damageFeesGrid.Rows.Clear();
        _damageFeesGrid.Columns.Add("Customer", "Customer");
        _damageFeesGrid.Columns.Add("Contact", "Contact");
        _damageFeesGrid.Columns.Add("Code", "Transaction Code");
        _damageFeesGrid.Columns.Add("Car", "Car / Plate");
        _damageFeesGrid.Columns.Add("Damage", "Damage Fee");
        _damageFeesGrid.Columns.Add("Date", "Payment Date");
        foreach (CustomerDamageFeeReportItem item in items)
        {
            _damageFeesGrid.Rows.Add(item.CustomerName, item.Contact, item.TransactionCode, $"{item.CarName} ({item.PlateNumber})", ReportLayoutHelper.FormatPeso(item.DamageFee), ReportLayoutHelper.FormatDate(item.PaymentDate));
        }
        ReportLayoutHelper.AddEmptyRow(_damageFeesGrid);
    }

    private void PopulateBlacklistedCustomers(IReadOnlyList<BlacklistedCustomerReportItem> items)
    {
        _blacklistedGrid.Columns.Clear(); _blacklistedGrid.Rows.Clear();
        _blacklistedGrid.Columns.Add("Customer", "Customer");
        _blacklistedGrid.Columns.Add("Contact", "Contact");
        _blacklistedGrid.Columns.Add("Reason", "Blacklist Reason");
        _blacklistedGrid.Columns.Add("Status", "Status");
        _blacklistedGrid.Columns.Add("LastTransaction", "Last Transaction");
        foreach (BlacklistedCustomerReportItem item in items)
        {
            _blacklistedGrid.Rows.Add(item.CustomerName, item.Contact, item.BlacklistReason, item.Status, item.LastTransaction);
        }
        ReportLayoutHelper.AddEmptyRow(_blacklistedGrid);
    }
}
