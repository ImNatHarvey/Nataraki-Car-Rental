using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;

namespace NatarakiCarRental.UserControls.Reports;

public sealed class ReportsControl : UserControl
{
    private readonly ReportService _reportService = new();
    private readonly DateTimePicker _fromDatePicker = CreateDatePicker();
    private readonly DateTimePicker _toDatePicker = CreateDatePicker();
    private readonly Button _applyFilterButton = ControlFactory.CreatePrimaryButton("Apply Filter", 120, 32);
    
    private readonly TabControl _reportTabs = new();
    private readonly TabPage _overviewPage = new("Overview");
    private readonly TabPage _financialPage = new("Financial");
    private readonly TabPage _fleetPage = new("Fleet Performance");
    private readonly TabPage _operationsPage = new("Operations");
    private readonly TabPage _customersPage = new("Customers");
    private readonly TabPage _exportsPage = new("Exports");

    // Overview Metrics
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

    // Overview Grids
    private readonly DataGridView _paymentMethodGrid = CreateSummaryGrid();
    private readonly DataGridView _revenueCategoryGrid = CreateSummaryGrid();
    private readonly DataGridView _statusBreakdownGrid = CreateSummaryGrid();
    private readonly DataGridView _topCarsGrid = CreateSummaryGrid();

    public ReportsControl()
    {
        InitializeControl();
        Load += ReportsControl_Load;
    }

    private void InitializeControl()
    {
        BackColor = ThemeHelper.ContentBackground;
        Dock = DockStyle.Fill;
        Padding = new Padding(32);

        TableLayoutPanel mainLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        mainLayout.Controls.Add(CreateHeaderPanel(), 0, 0);
        mainLayout.Controls.Add(CreateFilterPanel(), 0, 1);
        mainLayout.Controls.Add(CreateTabControl(), 0, 2);

        Controls.Add(mainLayout);
    }

    private static Panel CreateHeaderPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        panel.Controls.Add(new Label
        {
            Text = "Reports & Analytics",
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(300, 34),
            Font = FontHelper.Title(22F),
            ForeColor = ThemeHelper.TextPrimary
        });
        panel.Controls.Add(new Label
        {
            Text = "Financial, fleet, and operational insights.",
            AutoSize = false,
            Location = new Point(2, 42),
            Size = new Size(520, 24),
            Font = FontHelper.Regular(10.5F),
            ForeColor = ThemeHelper.TextSecondary
        });
        return panel;
    }

    private Panel CreateFilterPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        
        Label fromLabel = new() { Text = "From:", AutoSize = true, Location = new Point(0, 14), Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextSecondary };
        _fromDatePicker.Location = new Point(45, 10);
        _fromDatePicker.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        Label toLabel = new() { Text = "To:", AutoSize = true, Location = new Point(170, 14), Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextSecondary };
        _toDatePicker.Location = new Point(200, 10);
        _toDatePicker.Value = DateTime.Today;

        _applyFilterButton.Location = new Point(330, 9);
        _applyFilterButton.Click += async (_, _) => await RefreshReportsAsync();

        panel.Controls.Add(fromLabel);
        panel.Controls.Add(_fromDatePicker);
        panel.Controls.Add(toLabel);
        panel.Controls.Add(_toDatePicker);
        panel.Controls.Add(_applyFilterButton);
        
        return panel;
    }

    private TabControl CreateTabControl()
    {
        _reportTabs.Dock = DockStyle.Fill;
        _reportTabs.Font = FontHelper.SemiBold(10F);
        
        _reportTabs.TabPages.Add(_overviewPage);
        _reportTabs.TabPages.Add(_financialPage);
        _reportTabs.TabPages.Add(_fleetPage);
        _reportTabs.TabPages.Add(_operationsPage);
        _reportTabs.TabPages.Add(_customersPage);
        _reportTabs.TabPages.Add(_exportsPage);

        SetupOverviewTab();
        SetupPlaceholderTab(_financialPage, "Financial reports");
        SetupPlaceholderTab(_fleetPage, "Fleet performance reports");
        SetupPlaceholderTab(_operationsPage, "Operational reports");
        SetupPlaceholderTab(_customersPage, "Customer reports");
        SetupPlaceholderTab(_exportsPage, "Export options");

        return _reportTabs;
    }

    private void SetupOverviewTab()
    {
        _overviewPage.BackColor = ThemeHelper.ContentBackground;
        _overviewPage.AutoScroll = true;
        _overviewPage.Padding = new Padding(0, 20, 0, 0);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3
        };

        layout.Controls.Add(CreateOverviewMetricGrid());
        layout.Controls.Add(CreateOverviewChartsLayout());
        
        _overviewPage.Controls.Add(layout);
    }

    private TableLayoutPanel CreateOverviewMetricGrid()
    {
        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Top,
            Height = 430,
            ColumnCount = 4,
            RowCount = 3,
            Padding = new Padding(0, 0, 0, 10)
        };

        for (int i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        for (int i = 0; i < 3; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));

        AddMetricCard(grid, _totalRevenueCard, IconChar.MoneyBillTrendUp, "Total Revenue", "₱0.00", "Combined payments", ThemeHelper.Primary, 0, 0);
        AddMetricCard(grid, _rentalRevenueCard, IconChar.CarSide, "Rental Revenue", "₱0.00", "Base rental charges", ThemeHelper.Success, 1, 0);
        AddMetricCard(grid, _extensionFeesCard, IconChar.CalendarPlus, "Extension Fees", "₱0.00", "Extended rental days", ThemeHelper.Warning, 2, 0);
        AddMetricCard(grid, _damageFeesCard, IconChar.Hammer, "Damage Fees", "₱0.00", "Vehicle damage charges", ThemeHelper.Danger, 3, 0);

        AddMetricCard(grid, _lateReturnFeesCard, IconChar.Clock, "Late Return Fees", "₱0.00", "Overdue return penalties", ThemeHelper.Warning, 0, 1);
        AddMetricCard(grid, _paidTransactionsCard, IconChar.CheckCircle, "Paid Transactions", "0", "Fully settled", ThemeHelper.Success, 1, 1);
        AddMetricCard(grid, _partialUnpaidTransactionsCard, IconChar.Wallet, "Outstanding Transactions", "0", "Partial or unpaid", ThemeHelper.Warning, 2, 1);
        AddMetricCard(grid, _activeRentalsCard, IconChar.Key, "Active Rentals", "0", "Vehicles currently out", ThemeHelper.Primary, 3, 1);

        AddMetricCard(grid, _completedRentalsCard, IconChar.FlagCheckered, "Completed Rentals", "0", "Rentals closed in range", ThemeHelper.GrayIcon, 0, 2);
        AddMetricCard(grid, _topEarningCarCard, IconChar.Trophy, "Top Earning Car", "-", "Highest revenue generated", ThemeHelper.Success, 1, 2);
        AddMetricCard(grid, _mostRentedCarCard, IconChar.Star, "Most Rented Car", "-", "Highest rental frequency", ThemeHelper.Primary, 2, 2);

        return grid;
    }

    private TableLayoutPanel CreateOverviewChartsLayout()
    {
        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Top,
            Height = 320,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 10, 0, 0)
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        grid.Controls.Add(CreateGridCard("Payment Method Breakdown", _paymentMethodGrid), 0, 0);
        grid.Controls.Add(CreateGridCard("Revenue by Category", _revenueCategoryGrid), 1, 0);
        grid.Controls.Add(CreateGridCard("Transaction Status", _statusBreakdownGrid), 0, 1);
        grid.Controls.Add(CreateGridCard("Top 5 Performance (Revenue)", _topCarsGrid), 1, 1);

        return grid;
    }

    private static Panel CreateGridCard(string title, DataGridView grid)
    {
        Panel card = ControlFactory.CreateCardPanel(new Size(0, 0));
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(16);
        card.Margin = new Padding(0, 0, 14, 14);

        Label titleLabel = new()
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 28,
            Font = FontHelper.SemiBold(11F),
            ForeColor = ThemeHelper.TextPrimary
        };

        card.Controls.Add(grid);
        card.Controls.Add(titleLabel);
        return card;
    }

    private void SetupPlaceholderTab(TabPage page, string reportName)
    {
        page.BackColor = ThemeHelper.ContentBackground;
        Label label = new()
        {
            Text = $"{reportName} will be available in the next phase.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = FontHelper.Regular(12F),
            ForeColor = ThemeHelper.TextSecondary
        };
        page.Controls.Add(label);
    }

    private static void AddMetricCard(TableLayoutPanel grid, MetricCardControl card, IconChar icon, string title, string value, string helperText, Color iconColor, int column, int row)
    {
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, column == 3 ? 0 : 14, 14);
        card.SetMetric(icon, title, value, helperText, iconColor);
        grid.Controls.Add(card, column, row);
    }

    private static DateTimePicker CreateDatePicker()
    {
        return new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Width = 110,
            Font = FontHelper.Regular(10F)
        };
    }

    private static DataGridView CreateSummaryGrid()
    {
        DataGridView grid = new()
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToResizeColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = ThemeHelper.Surface,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersHeight = 32,
            EnableHeadersVisualStyles = false,
            GridColor = ThemeHelper.TableGridLine,
            ReadOnly = true,
            RowHeadersVisible = false,
            RowTemplate = { Height = 30 },
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ScrollBars = ScrollBars.Vertical
        };

        grid.DefaultCellStyle.SelectionBackColor = ThemeHelper.Surface;
        grid.DefaultCellStyle.SelectionForeColor = ThemeHelper.TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.BackColor = ThemeHelper.Surface;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = ThemeHelper.TextSecondary;
        grid.ColumnHeadersDefaultCellStyle.Font = FontHelper.SemiBold(9F);
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        return grid;
    }

    private async void ReportsControl_Load(object? sender, EventArgs e)
    {
        Load -= ReportsControl_Load;
        await RefreshReportsAsync();
    }

    private async Task RefreshReportsAsync()
    {
        try
        {
            DateTime from = _fromDatePicker.Value.Date;
            DateTime to = _toDatePicker.Value.Date.AddDays(1).AddSeconds(-1);

            ReportSummaryMetrics summary = await _reportService.GetSummaryMetricsAsync(from, to);
            UpdateSummaryCards(summary);

            var paymentMethods = await _reportService.GetPaymentMethodBreakdownAsync(from, to);
            PopulatePaymentMethods(paymentMethods);

            var revenueCategories = await _reportService.GetRevenueByCategoryAsync(from, to);
            PopulateRevenueCategories(revenueCategories);

            var statusBreakdown = await _reportService.GetTransactionStatusBreakdownAsync(from, to);
            PopulateStatusBreakdown(statusBreakdown);

            var topCars = await _reportService.GetTopCarsByRevenueAsync(from, to);
            PopulateTopCars(topCars);
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning($"Unable to load reports.\n\n{exception.Message}", "Reports");
        }
    }

    private void UpdateSummaryCards(ReportSummaryMetrics metrics)
    {
        _totalRevenueCard.SetMetric(IconChar.MoneyBillTrendUp, "Total Revenue", FormatPeso(metrics.TotalRevenue), "Combined payments", ThemeHelper.Primary);
        _rentalRevenueCard.SetMetric(IconChar.CarSide, "Rental Revenue", FormatPeso(metrics.RentalRevenue), "Base rental charges", ThemeHelper.Success);
        _extensionFeesCard.SetMetric(IconChar.CalendarPlus, "Extension Fees", FormatPeso(metrics.ExtensionFees), "Extended rental days", ThemeHelper.Warning);
        _damageFeesCard.SetMetric(IconChar.Hammer, "Damage Fees", FormatPeso(metrics.DamageFees), "Vehicle damage charges", ThemeHelper.Danger);
        
        _lateReturnFeesCard.SetMetric(IconChar.Clock, "Late Return Fees", FormatPeso(metrics.LateReturnFees), "Overdue return penalties", ThemeHelper.Warning);
        _paidTransactionsCard.SetMetric(IconChar.CheckCircle, "Paid Transactions", metrics.PaidTransactions.ToString(), "Fully settled", ThemeHelper.Success);
        _partialUnpaidTransactionsCard.SetMetric(IconChar.Wallet, "Outstanding Transactions", metrics.PartialUnpaidTransactions.ToString(), "Partial or unpaid", ThemeHelper.Warning);
        _activeRentalsCard.SetMetric(IconChar.Key, "Active Rentals", metrics.ActiveRentals.ToString(), "Vehicles currently out", ThemeHelper.Primary);
        
        _completedRentalsCard.SetMetric(IconChar.FlagCheckered, "Completed Rentals", metrics.CompletedRentals.ToString(), "Rentals closed in range", ThemeHelper.GrayIcon);
        _topEarningCarCard.SetMetric(IconChar.Trophy, "Top Earning Car", metrics.TopEarningCar ?? "-", $"Revenue: {FormatPeso(metrics.TopEarningCarRevenue)}", ThemeHelper.Success);
        _mostRentedCarCard.SetMetric(IconChar.Star, "Most Rented Car", metrics.MostRentedCar ?? "-", $"{metrics.MostRentedCarCount} rental(s)", ThemeHelper.Primary);
    }

    private void PopulatePaymentMethods(IReadOnlyList<PaymentMethodBreakdownItem> items)
    {
        _paymentMethodGrid.Columns.Clear();
        _paymentMethodGrid.Rows.Clear();
        _paymentMethodGrid.Columns.Add("Method", "Method");
        _paymentMethodGrid.Columns.Add("Count", "TXs");
        _paymentMethodGrid.Columns.Add("Amount", "Amount");

        foreach (var item in items)
        {
            _paymentMethodGrid.Rows.Add(item.ModeOfPayment, item.TransactionCount, FormatPeso(item.TotalAmount));
        }
    }

    private void PopulateRevenueCategories(IReadOnlyList<RevenueByCategoryItem> items)
    {
        _revenueCategoryGrid.Columns.Clear();
        _revenueCategoryGrid.Rows.Clear();
        _revenueCategoryGrid.Columns.Add("Category", "Category");
        _revenueCategoryGrid.Columns.Add("Amount", "Amount");

        foreach (var item in items)
        {
            _revenueCategoryGrid.Rows.Add(item.PaymentCategory, FormatPeso(item.TotalAmount));
        }
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
            _topCarsGrid.Rows.Add($"{item.CarName} ({item.PlateNumber})", item.RentalCount, FormatPeso(item.Revenue));
        }
    }

    private static string FormatPeso(decimal amount) => $"₱{amount:N2}";
}
