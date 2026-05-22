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

    // Financial Tab Specific
    private readonly MetricCardControl _fTotalRevenueCard = new();
    private readonly MetricCardControl _fOutstandingCard = new();
    private readonly MetricCardControl _fPaidTxCard = new();
    private readonly MetricCardControl _fPartialTxCard = new();
    private readonly MetricCardControl _fUnpaidTxCard = new();
    private readonly MetricCardControl _fRentalRevenueCard = new();
    private readonly MetricCardControl _fExtensionFeesCard = new();
    private readonly MetricCardControl _fDamageLateFeesCard = new();
    
    private readonly DataGridView _fPaymentMethodGrid = CreateSummaryGrid();
    private readonly DataGridView _fRevenueCategoryGrid = CreateSummaryGrid();
    private readonly DataGridView _fOutstandingGrid = CreateSummaryGrid();
    private readonly DataGridView _fCarRevenueGrid = CreateSummaryGrid();
    private readonly DataGridView _fCustomerRevenueGrid = CreateSummaryGrid();

    // Fleet Performance Tab
    private readonly MetricCardControl _fleetTotalRevenueCard = new();
    private readonly MetricCardControl _fleetAverageRevenueCard = new();
    private readonly MetricCardControl _fleetTopEarningCarCard = new();
    private readonly MetricCardControl _fleetMostRentedCarCard = new();
    private readonly MetricCardControl _fleetAverageUtilizationCard = new();
    private readonly MetricCardControl _fleetActiveRentalsCard = new();
    private readonly MetricCardControl _fleetCompletedRentalsCard = new();
    private readonly MetricCardControl _fleetMaintenanceCard = new();

    private readonly DataGridView _fleetUtilizationGrid = CreateSummaryGrid();
    private readonly DataGridView _fleetRevenueGrid = CreateSummaryGrid();
    private readonly DataGridView _fleetTopEarningGrid = CreateSummaryGrid();
    private readonly DataGridView _fleetMostRentedGrid = CreateSummaryGrid();
    private readonly DataGridView _fleetLeastUsedGrid = CreateSummaryGrid();
    private readonly DataGridView _fleetMaintenanceGrid = CreateSummaryGrid();

    // Operations Tab
    private readonly MetricCardControl _opsUpcomingReturnsCard = new();
    private readonly MetricCardControl _opsLateReturnsCard = new();
    private readonly MetricCardControl _opsActiveRentalsCard = new();
    private readonly MetricCardControl _opsUpcomingReservationsCard = new();
    private readonly MetricCardControl _opsReservedCarsCard = new();
    private readonly MetricCardControl _opsMaintenanceCard = new();
    private readonly MetricCardControl _opsAvailableCarsCard = new();
    private readonly MetricCardControl _opsCompletedReturnsCard = new();

    private readonly DataGridView _opsUpcomingReturnsGrid = CreateSummaryGrid();
    private readonly DataGridView _opsLateReturnsGrid = CreateSummaryGrid();
    private readonly DataGridView _opsActiveRentalsGrid = CreateSummaryGrid();
    private readonly DataGridView _opsUpcomingReservationsGrid = CreateSummaryGrid();
    private readonly DataGridView _opsMaintenanceGrid = CreateSummaryGrid();
    private readonly DataGridView _opsAvailableCarsGrid = CreateSummaryGrid();

    private readonly FlowLayoutPanel _overviewMetricPanel = new();
    private readonly FlowLayoutPanel _financialMetricPanel = new();
    private readonly FlowLayoutPanel _fleetMetricPanel = new();
    private readonly FlowLayoutPanel _operationsMetricPanel = new();

    public ReportsControl()
    {
        InitializeControl();
        Load += ReportsControl_Load;
        Resize += ReportsControl_Resize;
    }

    private void ReportsControl_Resize(object? sender, EventArgs e)
    {
        LayoutReportMetricSections();
    }

    private void LayoutReportMetricSections()
    {
        LayoutReportMetricCards(_overviewMetricPanel, GetOverviewCards());
        LayoutReportMetricCards(_financialMetricPanel, GetFinancialCards());
        LayoutReportMetricCards(_fleetMetricPanel, GetFleetCards());
        LayoutReportMetricCards(_operationsMetricPanel, GetOperationsCards());
    }

    private static void LayoutReportMetricCards(FlowLayoutPanel panel, IReadOnlyList<Control> cards)
    {
        if (panel.IsDisposed || cards.Count == 0)
        {
            return;
        }

        int availableWidth = Math.Max(panel.ClientSize.Width, panel.Parent?.ClientSize.Width ?? 0);
        if (availableWidth <= 0)
        {
            availableWidth = 1000;
        }

        int columns = availableWidth < 1150 ? 3 : 4;
        int gap = 14;
        int cardHeight = 132;
        int horizontalPadding = panel.Padding.Left + panel.Padding.Right;
        int cardWidth = Math.Max(220, (availableWidth - horizontalPadding - (gap * columns)) / columns);
        int rows = (int)Math.Ceiling(cards.Count / (double)columns);
        int height = panel.Padding.Top + panel.Padding.Bottom + (rows * cardHeight) + ((rows - 1) * gap);

        panel.SuspendLayout();
        panel.Visible = true;
        panel.Height = height;
        foreach (Control card in cards)
        {
            card.Dock = DockStyle.None;
            card.Margin = new Padding(0, 0, gap, gap);
            card.Size = new Size(cardWidth, cardHeight);
        }
        panel.ResumeLayout(true);
    }

    private List<Control> GetOverviewCards() =>
    [
        _totalRevenueCard, _rentalRevenueCard, _extensionFeesCard, _damageFeesCard,
        _lateReturnFeesCard, _paidTransactionsCard, _partialUnpaidTransactionsCard, _activeRentalsCard,
        _completedRentalsCard, _topEarningCarCard, _mostRentedCarCard
    ];

    private List<Control> GetFinancialCards() =>
    [
        _fTotalRevenueCard, _fOutstandingCard, _fPaidTxCard, _fPartialTxCard,
        _fUnpaidTxCard, _fRentalRevenueCard, _fExtensionFeesCard, _fDamageLateFeesCard
    ];

    private List<Control> GetFleetCards() =>
    [
        _fleetTotalRevenueCard, _fleetAverageRevenueCard, _fleetTopEarningCarCard, _fleetMostRentedCarCard,
        _fleetAverageUtilizationCard, _fleetActiveRentalsCard, _fleetCompletedRentalsCard, _fleetMaintenanceCard
    ];

    private List<Control> GetOperationsCards() =>
    [
        _opsUpcomingReturnsCard, _opsLateReturnsCard, _opsActiveRentalsCard, _opsUpcomingReservationsCard,
        _opsReservedCarsCard, _opsMaintenanceCard, _opsAvailableCarsCard, _opsCompletedReturnsCard
    ];

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
        _reportTabs.SelectedIndexChanged += (_, _) => LayoutReportMetricSections();
        
        _reportTabs.TabPages.Add(_overviewPage);
        _reportTabs.TabPages.Add(_financialPage);
        _reportTabs.TabPages.Add(_fleetPage);
        _reportTabs.TabPages.Add(_operationsPage);
        _reportTabs.TabPages.Add(_customersPage);
        _reportTabs.TabPages.Add(_exportsPage);

        SetupOverviewTab();
        SetupFinancialTab();
        SetupFleetTab();
        SetupOperationsTab();
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

        layout.Controls.Add(CreateOverviewMetricPanel());
        layout.Controls.Add(CreateOverviewChartsLayout());
        
        _overviewPage.Controls.Add(layout);
    }

    private void SetupFinancialTab()
    {
        _financialPage.BackColor = ThemeHelper.ContentBackground;
        _financialPage.AutoScroll = true;
        _financialPage.Padding = new Padding(0, 20, 0, 0);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 5
        };

        layout.Controls.Add(CreateFinancialMetricPanel());
        
        TableLayoutPanel breakdownLayout = new() { Dock = DockStyle.Top, Height = 340, ColumnCount = 2, Padding = new Padding(0, 10, 0, 0) };
        breakdownLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        breakdownLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        breakdownLayout.Controls.Add(CreateGridCard("Payment Method Breakdown", _fPaymentMethodGrid), 0, 0);
        breakdownLayout.Controls.Add(CreateGridCard("Revenue by Category", _fRevenueCategoryGrid), 1, 0);
        layout.Controls.Add(breakdownLayout);

        layout.Controls.Add(CreateGridCard("Outstanding Transactions (Unpaid/Partial)", _fOutstandingGrid, 340));
        layout.Controls.Add(CreateGridCard("Revenue by Car Performance", _fCarRevenueGrid, 340));
        layout.Controls.Add(CreateGridCard("Revenue by Customer", _fCustomerRevenueGrid, 340));

        _financialPage.Controls.Add(layout);
    }

    private void SetupFleetTab()
    {
        _fleetPage.BackColor = ThemeHelper.ContentBackground;
        _fleetPage.AutoScroll = true;
        _fleetPage.Padding = new Padding(0, 20, 0, 0);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 5
        };

        layout.Controls.Add(CreateFleetMetricPanel());
        layout.Controls.Add(CreateGridCard("Fleet Utilization", _fleetUtilizationGrid, 360));
        layout.Controls.Add(CreateGridCard("Revenue Per Unit", _fleetRevenueGrid, 360));
        layout.Controls.Add(CreateFleetPerformanceTablesLayout());
        layout.Controls.Add(CreateGridCard("Current Maintenance Schedules", _fleetMaintenanceGrid, 300));

        _fleetPage.Controls.Add(layout);
    }

    private void SetupOperationsTab()
    {
        _operationsPage.BackColor = ThemeHelper.ContentBackground;
        _operationsPage.AutoScroll = true;
        _operationsPage.Padding = new Padding(0, 20, 0, 0);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 6
        };

        layout.Controls.Add(CreateOperationsMetricPanel());
        layout.Controls.Add(CreateOperationsReturnsLayout());
        layout.Controls.Add(CreateGridCard("Active Rentals", _opsActiveRentalsGrid, 340));
        layout.Controls.Add(CreateGridCard("Upcoming Reservations", _opsUpcomingReservationsGrid, 340));
        layout.Controls.Add(CreateGridCard("Maintenance Visibility", _opsMaintenanceGrid, 300));
        layout.Controls.Add(CreateGridCard("Available Cars", _opsAvailableCarsGrid, 320));

        _operationsPage.Controls.Add(layout);
    }

    private FlowLayoutPanel CreateOverviewMetricPanel()
    {
        ConfigureMetricPanel(_overviewMetricPanel);

        AddMetricCard(_overviewMetricPanel, _totalRevenueCard, IconChar.MoneyBillTrendUp, "Total Revenue", "₱0.00", "Combined payments", ThemeHelper.Primary);
        AddMetricCard(_overviewMetricPanel, _rentalRevenueCard, IconChar.CarSide, "Rental Revenue", "₱0.00", "Base rental charges", ThemeHelper.Success);
        AddMetricCard(_overviewMetricPanel, _extensionFeesCard, IconChar.CalendarPlus, "Extension Fees", "₱0.00", "Extended rental days", ThemeHelper.Warning);
        AddMetricCard(_overviewMetricPanel, _damageFeesCard, IconChar.Hammer, "Damage Fees", "₱0.00", "Vehicle damage charges", ThemeHelper.Danger);
        AddMetricCard(_overviewMetricPanel, _lateReturnFeesCard, IconChar.Clock, "Late Return Fees", "₱0.00", "Overdue return penalties", ThemeHelper.Warning);
        AddMetricCard(_overviewMetricPanel, _paidTransactionsCard, IconChar.CheckCircle, "Paid Transactions", "0", "Fully settled", ThemeHelper.Success);
        AddMetricCard(_overviewMetricPanel, _partialUnpaidTransactionsCard, IconChar.Wallet, "Outstanding Transactions", "0", "Partial or unpaid", ThemeHelper.Warning);
        AddMetricCard(_overviewMetricPanel, _activeRentalsCard, IconChar.Key, "Active Rentals", "0", "Vehicles currently out", ThemeHelper.Primary);
        AddMetricCard(_overviewMetricPanel, _completedRentalsCard, IconChar.FlagCheckered, "Completed Rentals", "0", "Rentals closed in range", ThemeHelper.GrayIcon);
        AddMetricCard(_overviewMetricPanel, _topEarningCarCard, IconChar.Trophy, "Top Earning Car", "-", "Highest revenue generated", ThemeHelper.Success);
        AddMetricCard(_overviewMetricPanel, _mostRentedCarCard, IconChar.Star, "Most Rented Car", "-", "Highest rental frequency", ThemeHelper.Primary);
        LayoutReportMetricCards(_overviewMetricPanel, GetOverviewCards());

        return _overviewMetricPanel;
    }

    private FlowLayoutPanel CreateFinancialMetricPanel()
    {
        ConfigureMetricPanel(_financialMetricPanel);

        AddMetricCard(_financialMetricPanel, _fTotalRevenueCard, IconChar.MoneyBillTrendUp, "Total Revenue", "₱0.00", "Actual payments received", ThemeHelper.Primary);
        AddMetricCard(_financialMetricPanel, _fOutstandingCard, IconChar.HandHoldingDollar, "Total Outstanding", "₱0.00", "Uncollected balance", ThemeHelper.Danger);
        AddMetricCard(_financialMetricPanel, _fPaidTxCard, IconChar.CheckDouble, "Fully Paid", "0", "Settled transactions", ThemeHelper.Success);
        AddMetricCard(_financialMetricPanel, _fPartialTxCard, IconChar.ScaleUnbalanced, "Partial", "0", "Some payment received", ThemeHelper.Warning);
        AddMetricCard(_financialMetricPanel, _fUnpaidTxCard, IconChar.FileInvoiceDollar, "Unpaid", "0", "No payments yet", ThemeHelper.Danger);
        AddMetricCard(_financialMetricPanel, _fRentalRevenueCard, IconChar.CarSide, "Rental Revenue", "₱0.00", "Base charges", ThemeHelper.Success);
        AddMetricCard(_financialMetricPanel, _fExtensionFeesCard, IconChar.CalendarPlus, "Extension Fees", "₱0.00", "Rental extensions", ThemeHelper.Warning);
        AddMetricCard(_financialMetricPanel, _fDamageLateFeesCard, IconChar.CircleExclamation, "Damage/Late Fees", "₱0.00", "Return penalties", ThemeHelper.Danger);
        LayoutReportMetricCards(_financialMetricPanel, GetFinancialCards());

        return _financialMetricPanel;
    }

    private FlowLayoutPanel CreateFleetMetricPanel()
    {
        ConfigureMetricPanel(_fleetMetricPanel);

        AddMetricCard(_fleetMetricPanel, _fleetTotalRevenueCard, IconChar.MoneyBillTrendUp, "Total Fleet Revenue", "₱0.00", "Payments by fleet unit", ThemeHelper.Primary);
        AddMetricCard(_fleetMetricPanel, _fleetAverageRevenueCard, IconChar.ChartLine, "Avg Revenue / Car", "₱0.00", "Across active fleet", ThemeHelper.Success);
        AddMetricCard(_fleetMetricPanel, _fleetTopEarningCarCard, IconChar.Trophy, "Top Earning Car", "-", "Highest paid revenue", ThemeHelper.Success);
        AddMetricCard(_fleetMetricPanel, _fleetMostRentedCarCard, IconChar.Star, "Most Rented Car", "-", "Highest rental count", ThemeHelper.Primary);
        AddMetricCard(_fleetMetricPanel, _fleetAverageUtilizationCard, IconChar.GaugeHigh, "Avg Utilization", "0.0%", "Rental days in range", ThemeHelper.Warning);
        AddMetricCard(_fleetMetricPanel, _fleetActiveRentalsCard, IconChar.Key, "Active Rentals", "0", "Currently released", ThemeHelper.Success);
        AddMetricCard(_fleetMetricPanel, _fleetCompletedRentalsCard, IconChar.FlagCheckered, "Completed Rentals", "0", "Closed rentals in range", ThemeHelper.GrayIcon);
        AddMetricCard(_fleetMetricPanel, _fleetMaintenanceCard, IconChar.ScrewdriverWrench, "Cars Under Maintenance", "0", "Ongoing maintenance", ThemeHelper.Warning);
        LayoutReportMetricCards(_fleetMetricPanel, GetFleetCards());

        return _fleetMetricPanel;
    }

    private FlowLayoutPanel CreateOperationsMetricPanel()
    {
        ConfigureMetricPanel(_operationsMetricPanel);

        AddMetricCard(_operationsMetricPanel, _opsUpcomingReturnsCard, IconChar.CalendarCheck, "Upcoming Returns", "0", "Expected back in range", ThemeHelper.Primary);
        AddMetricCard(_operationsMetricPanel, _opsLateReturnsCard, IconChar.TriangleExclamation, "Late Returns", "0", "Past expected return", ThemeHelper.Danger);
        AddMetricCard(_operationsMetricPanel, _opsActiveRentalsCard, IconChar.Key, "Active Rentals", "0", "Ongoing rentals", ThemeHelper.Success);
        AddMetricCard(_operationsMetricPanel, _opsUpcomingReservationsCard, IconChar.CalendarDays, "Upcoming Reservations", "0", "Starts in range", ThemeHelper.Warning);
        AddMetricCard(_operationsMetricPanel, _opsReservedCarsCard, IconChar.Bookmark, "Reserved Cars", "0", "Reserved in range", ThemeHelper.Primary);
        AddMetricCard(_operationsMetricPanel, _opsMaintenanceCard, IconChar.ScrewdriverWrench, "Cars Under Maintenance", "0", "Schedule-based", ThemeHelper.Warning);
        AddMetricCard(_operationsMetricPanel, _opsAvailableCarsCard, IconChar.CircleCheck, "Available Cars", "0", "No blocking schedule", ThemeHelper.Success);
        AddMetricCard(_operationsMetricPanel, _opsCompletedReturnsCard, IconChar.FlagCheckered, "Completed Returns", "0", "Closed in range", ThemeHelper.GrayIcon);
        LayoutReportMetricCards(_operationsMetricPanel, GetOperationsCards());

        return _operationsMetricPanel;
    }

    private static void ConfigureMetricPanel(FlowLayoutPanel panel)
    {
        panel.Dock = DockStyle.Top;
        panel.AutoSize = false;
        panel.WrapContents = true;
        panel.FlowDirection = FlowDirection.LeftToRight;
        panel.Padding = new Padding(8, 8, 0, 0);
        panel.Margin = new Padding(0, 0, 0, 10);
        panel.BackColor = ThemeHelper.ContentBackground;
        panel.Visible = true;
    }

    private TableLayoutPanel CreateOverviewChartsLayout()
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

        grid.Controls.Add(CreateGridCard("Payment Method Breakdown", _paymentMethodGrid), 0, 0);
        grid.Controls.Add(CreateGridCard("Revenue by Category", _revenueCategoryGrid), 1, 0);
        grid.Controls.Add(CreateGridCard("Transaction Status", _statusBreakdownGrid), 0, 1);
        grid.Controls.Add(CreateGridCard("Top 5 Performance (Revenue)", _topCarsGrid), 1, 1);

        return grid;
    }

    private TableLayoutPanel CreateFleetPerformanceTablesLayout()
    {
        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Top,
            Height = 340,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0, 10, 0, 0)
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        grid.Controls.Add(CreateGridCard("Top 5 Earning Cars", _fleetTopEarningGrid), 0, 0);
        grid.Controls.Add(CreateGridCard("Top 5 Most Rented Cars", _fleetMostRentedGrid), 1, 0);
        grid.Controls.Add(CreateGridCard("Least Used Cars", _fleetLeastUsedGrid), 2, 0);
        return grid;
    }

    private TableLayoutPanel CreateOperationsReturnsLayout()
    {
        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Top,
            Height = 360,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 10, 0, 0)
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.Controls.Add(CreateGridCard("Upcoming Returns", _opsUpcomingReturnsGrid), 0, 0);
        grid.Controls.Add(CreateGridCard("Late Returns", _opsLateReturnsGrid), 1, 0);
        return grid;
    }

    private static Panel CreateGridCard(string title, DataGridView grid, int height = 0)
    {
        Panel card = ControlFactory.CreateCardPanel(new Size(0, height));
        card.Dock = height > 0 ? DockStyle.Top : DockStyle.Fill;
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

    private static void AddMetricCard(FlowLayoutPanel panel, MetricCardControl card, IconChar icon, string title, string value, string helperText, Color iconColor)
    {
        card.Dock = DockStyle.None;
        card.SetMetric(icon, title, value, helperText, iconColor);
        if (!panel.Controls.Contains(card))
        {
            panel.Controls.Add(card);
        }
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
            ColumnHeadersHeight = 36,
            EnableHeadersVisualStyles = false,
            GridColor = ThemeHelper.TableGridLine,
            ReadOnly = true,
            RowHeadersVisible = false,
            RowTemplate = { Height = 34 },
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ScrollBars = ScrollBars.Vertical
        };

        grid.DefaultCellStyle.SelectionBackColor = ThemeHelper.Surface;
        grid.DefaultCellStyle.SelectionForeColor = ThemeHelper.TextPrimary;
        
        grid.ColumnHeadersDefaultCellStyle.BackColor = ThemeHelper.Primary;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemeHelper.Primary;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = FontHelper.SemiBold(9F);
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        return grid;
    }

    private async void ReportsControl_Load(object? sender, EventArgs e)
    {
        Load -= ReportsControl_Load;
        LayoutReportMetricSections();
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
            UpdateFinancialSummaryCards(summary);

            var paymentMethods = await _reportService.GetPaymentMethodBreakdownAsync(from, to);
            PopulatePaymentMethods(_paymentMethodGrid, paymentMethods);
            PopulatePaymentMethods(_fPaymentMethodGrid, paymentMethods);

            var revenueCategories = await _reportService.GetRevenueByCategoryAsync(from, to);
            PopulateRevenueCategories(_revenueCategoryGrid, revenueCategories);
            PopulateRevenueCategories(_fRevenueCategoryGrid, revenueCategories);

            var statusBreakdown = await _reportService.GetTransactionStatusBreakdownAsync(from, to);
            PopulateStatusBreakdown(statusBreakdown);

            var topCars = await _reportService.GetTopCarsByRevenueAsync(from, to, 5);
            PopulateTopCars(_topCarsGrid, topCars);

            // Financial Tab Specific
            var outstanding = await _reportService.GetOutstandingTransactionsAsync(from, to);
            PopulateOutstandingTransactions(outstanding);

            var carRevenue = await _reportService.GetRevenueByCarAsync(from, to, 10);
            PopulateCarRevenue(carRevenue);

            var customerRevenue = await _reportService.GetRevenueByCustomerAsync(from, to, 10);
            PopulateCustomerRevenue(customerRevenue);

            FleetPerformanceMetrics fleetMetrics = await _reportService.GetFleetPerformanceMetricsAsync(from, to);
            UpdateFleetSummaryCards(fleetMetrics);

            var fleetUtilization = await _reportService.GetFleetUtilizationAsync(from, to);
            PopulateFleetUtilization(fleetUtilization);

            var fleetRevenue = await _reportService.GetFleetRevenuePerCarAsync(from, to);
            PopulateFleetRevenue(fleetRevenue);

            var fleetTopEarning = await _reportService.GetTopCarsByRevenueAsync(from, to, 5);
            PopulateTopCars(_fleetTopEarningGrid, fleetTopEarning);

            var fleetMostRented = await _reportService.GetMostRentedCarsAsync(from, to, 5);
            PopulateTopCars(_fleetMostRentedGrid, fleetMostRented);

            var fleetLeastUsed = await _reportService.GetLeastUsedCarsAsync(from, to, 5);
            PopulateTopCars(_fleetLeastUsedGrid, fleetLeastUsed);

            var maintenanceCars = await _reportService.GetCarsUnderMaintenanceAsync(from, to);
            PopulateFleetMaintenance(maintenanceCars);

            OperationsMetrics operationsMetrics = await _reportService.GetOperationsMetricsAsync(from, to);
            UpdateOperationsSummaryCards(operationsMetrics);

            var upcomingReturns = await _reportService.GetUpcomingReturnsAsync(from, to);
            PopulateUpcomingReturns(upcomingReturns);

            var lateReturns = await _reportService.GetLateReturnsAsync(DateTime.Today);
            PopulateLateReturns(lateReturns);

            var activeRentals = await _reportService.GetActiveRentalsReportAsync(from, to);
            PopulateActiveRentals(activeRentals);

            var upcomingReservations = await _reportService.GetUpcomingReservationsAsync(from, to);
            PopulateUpcomingReservations(upcomingReservations);

            var operationsMaintenance = await _reportService.GetMaintenanceVisibilityAsync(from, to);
            PopulateOperationsMaintenance(operationsMaintenance);

            var availableCars = await _reportService.GetAvailableCarsReportAsync(from, to);
            PopulateAvailableCars(availableCars);
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
        _partialUnpaidTransactionsCard.SetMetric(IconChar.Wallet, "Outstanding Transactions", (metrics.PartialTransactions + metrics.UnpaidTransactions).ToString(), "Partial or unpaid", ThemeHelper.Warning);
        _activeRentalsCard.SetMetric(IconChar.Key, "Active Rentals", metrics.ActiveRentals.ToString(), "Vehicles currently out", ThemeHelper.Primary);
        
        _completedRentalsCard.SetMetric(IconChar.FlagCheckered, "Completed Rentals", metrics.CompletedRentals.ToString(), "Rentals closed in range", ThemeHelper.GrayIcon);
        _topEarningCarCard.SetMetric(IconChar.Trophy, "Top Earning Car", metrics.TopEarningCar ?? "-", $"Revenue: {FormatPeso(metrics.TopEarningCarRevenue)}", ThemeHelper.Success);
        _mostRentedCarCard.SetMetric(IconChar.Star, "Most Rented Car", metrics.MostRentedCar ?? "-", $"{metrics.MostRentedCarCount} rental(s)", ThemeHelper.Primary);
    }

    private void UpdateFinancialSummaryCards(ReportSummaryMetrics metrics)
    {
        _fTotalRevenueCard.SetMetric(IconChar.MoneyBillTrendUp, "Total Revenue", FormatPeso(metrics.TotalRevenue), "Actual payments received", ThemeHelper.Primary);
        _fOutstandingCard.SetMetric(IconChar.HandHoldingDollar, "Total Outstanding", FormatPeso(metrics.OutstandingBalance), "Uncollected balance", ThemeHelper.Danger);
        _fPaidTxCard.SetMetric(IconChar.CheckDouble, "Fully Paid", metrics.PaidTransactions.ToString(), "Settled transactions", ThemeHelper.Success);
        _fPartialTxCard.SetMetric(IconChar.ScaleUnbalanced, "Partial", metrics.PartialTransactions.ToString(), "Some payment received", ThemeHelper.Warning);
        _fUnpaidTxCard.SetMetric(IconChar.FileInvoiceDollar, "Unpaid", metrics.UnpaidTransactions.ToString(), "No payments yet", ThemeHelper.Danger);
        _fRentalRevenueCard.SetMetric(IconChar.CarSide, "Rental Revenue", FormatPeso(metrics.RentalRevenue), "Base charges", ThemeHelper.Success);
        _fExtensionFeesCard.SetMetric(IconChar.CalendarPlus, "Extension Fees", FormatPeso(metrics.ExtensionFees), "Rental extensions", ThemeHelper.Warning);
        _fDamageLateFeesCard.SetMetric(IconChar.CircleExclamation, "Damage/Late Fees", FormatPeso(metrics.DamageFees + metrics.LateReturnFees), "Return penalties", ThemeHelper.Danger);
    }

    private void UpdateFleetSummaryCards(FleetPerformanceMetrics metrics)
    {
        _fleetTotalRevenueCard.SetMetric(IconChar.MoneyBillTrendUp, "Total Fleet Revenue", FormatPeso(metrics.TotalFleetRevenue), "Payments by fleet unit", ThemeHelper.Primary);
        _fleetAverageRevenueCard.SetMetric(IconChar.ChartLine, "Avg Revenue / Car", FormatPeso(metrics.AverageRevenuePerCar), "Across active fleet", ThemeHelper.Success);
        _fleetTopEarningCarCard.SetMetric(IconChar.Trophy, "Top Earning Car", metrics.TopEarningCar ?? "-", $"Revenue: {FormatPeso(metrics.TopEarningCarRevenue)}", ThemeHelper.Success);
        _fleetMostRentedCarCard.SetMetric(IconChar.Star, "Most Rented Car", metrics.MostRentedCar ?? "-", $"{metrics.MostRentedCarCount} rental(s)", ThemeHelper.Primary);
        _fleetAverageUtilizationCard.SetMetric(IconChar.GaugeHigh, "Avg Utilization", FormatPercent(metrics.AverageUtilizationRate), "Rental days in range", ThemeHelper.Warning);
        _fleetActiveRentalsCard.SetMetric(IconChar.Key, "Active Rentals", metrics.ActiveRentals.ToString(), "Currently released", ThemeHelper.Success);
        _fleetCompletedRentalsCard.SetMetric(IconChar.FlagCheckered, "Completed Rentals", metrics.CompletedRentals.ToString(), "Closed rentals in range", ThemeHelper.GrayIcon);
        _fleetMaintenanceCard.SetMetric(IconChar.ScrewdriverWrench, "Cars Under Maintenance", metrics.CarsUnderMaintenance.ToString(), "Ongoing maintenance", ThemeHelper.Warning);
    }

    private void UpdateOperationsSummaryCards(OperationsMetrics metrics)
    {
        _opsUpcomingReturnsCard.SetMetric(IconChar.CalendarCheck, "Upcoming Returns", metrics.UpcomingReturns.ToString(), "Expected back in range", ThemeHelper.Primary);
        _opsLateReturnsCard.SetMetric(IconChar.TriangleExclamation, "Late Returns", metrics.LateReturns.ToString(), "Past expected return", ThemeHelper.Danger);
        _opsActiveRentalsCard.SetMetric(IconChar.Key, "Active Rentals", metrics.ActiveRentals.ToString(), "Ongoing rentals", ThemeHelper.Success);
        _opsUpcomingReservationsCard.SetMetric(IconChar.CalendarDays, "Upcoming Reservations", metrics.UpcomingReservations.ToString(), "Starts in range", ThemeHelper.Warning);
        _opsReservedCarsCard.SetMetric(IconChar.Bookmark, "Reserved Cars", metrics.ReservedCars.ToString(), "Reserved in range", ThemeHelper.Primary);
        _opsMaintenanceCard.SetMetric(IconChar.ScrewdriverWrench, "Cars Under Maintenance", metrics.CarsUnderMaintenance.ToString(), "Schedule-based", ThemeHelper.Warning);
        _opsAvailableCarsCard.SetMetric(IconChar.CircleCheck, "Available Cars", metrics.AvailableCars.ToString(), "No blocking schedule", ThemeHelper.Success);
        _opsCompletedReturnsCard.SetMetric(IconChar.FlagCheckered, "Completed Returns", metrics.CompletedReturns.ToString(), "Closed in range", ThemeHelper.GrayIcon);
    }

    private void PopulatePaymentMethods(DataGridView grid, IReadOnlyList<PaymentMethodBreakdownItem> items)
    {
        grid.Columns.Clear();
        grid.Rows.Clear();
        grid.Columns.Add("Method", "Method");
        grid.Columns.Add("Count", "Count");
        grid.Columns.Add("Amount", "Amount");
        grid.Columns.Add("Percent", "%");

        foreach (var item in items)
        {
            grid.Rows.Add(item.ModeOfPayment, item.PaymentCount, FormatPeso(item.TotalAmount), $"{item.Percentage:N1}%");
        }
    }

    private void PopulateRevenueCategories(DataGridView grid, IReadOnlyList<RevenueByCategoryItem> items)
    {
        grid.Columns.Clear();
        grid.Rows.Clear();
        grid.Columns.Add("Category", "Category");
        grid.Columns.Add("Count", "Count");
        grid.Columns.Add("Amount", "Amount");
        grid.Columns.Add("Percent", "%");

        foreach (var item in items)
        {
            grid.Rows.Add(item.PaymentCategory, item.PaymentCount, FormatPeso(item.TotalAmount), $"{item.Percentage:N1}%");
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

    private void PopulateTopCars(DataGridView grid, IReadOnlyList<TopCarItem> items)
    {
        grid.Columns.Clear();
        grid.Rows.Clear();
        grid.Columns.Add("Car", "Car");
        grid.Columns.Add("Rentals", "Count");
        grid.Columns.Add("Revenue", "Revenue");

        foreach (var item in items)
        {
            grid.Rows.Add($"{item.CarName} ({item.PlateNumber})", item.RentalCount, FormatPeso(item.Revenue));
        }
    }

    private void PopulateOutstandingTransactions(IReadOnlyList<TransactionListItem> items)
    {
        _fOutstandingGrid.Columns.Clear();
        _fOutstandingGrid.Rows.Clear();
        _fOutstandingGrid.Columns.Add("Code", "Code");
        _fOutstandingGrid.Columns.Add("Customer", "Customer");
        _fOutstandingGrid.Columns.Add("Car", "Car");
        _fOutstandingGrid.Columns.Add("Total", "Total");
        _fOutstandingGrid.Columns.Add("Paid", "Paid");
        _fOutstandingGrid.Columns.Add("Balance", "Balance");
        _fOutstandingGrid.Columns.Add("PayStatus", "Payment");
        _fOutstandingGrid.Columns.Add("TxStatus", "Status");

        foreach (var item in items)
        {
            _fOutstandingGrid.Rows.Add(
                item.TransactionCode,
                item.CustomerName,
                $"{item.CarName} ({item.PlateNumber})",
                FormatPeso(item.TotalAmount),
                FormatPeso(item.AmountPaid),
                FormatPeso(item.BalanceAmount),
                item.PaymentStatus,
                item.TransactionStatus);
        }
    }

    private void PopulateCarRevenue(IReadOnlyList<TopCarItem> items)
    {
        _fCarRevenueGrid.Columns.Clear();
        _fCarRevenueGrid.Rows.Clear();
        _fCarRevenueGrid.Columns.Add("Car", "Car / Plate");
        _fCarRevenueGrid.Columns.Add("Rentals", "Count");
        _fCarRevenueGrid.Columns.Add("Total", "Total Revenue");
        _fCarRevenueGrid.Columns.Add("Avg", "Average / Rental");

        foreach (var item in items)
        {
            _fCarRevenueGrid.Rows.Add($"{item.CarName} ({item.PlateNumber})", item.RentalCount, FormatPeso(item.Revenue), FormatPeso(item.AverageRevenue));
        }
    }

    private void PopulateCustomerRevenue(IReadOnlyList<RevenueByCustomerItem> items)
    {
        _fCustomerRevenueGrid.Columns.Clear();
        _fCustomerRevenueGrid.Rows.Clear();
        _fCustomerRevenueGrid.Columns.Add("Customer", "Customer");
        _fCustomerRevenueGrid.Columns.Add("TXs", "Count");
        _fCustomerRevenueGrid.Columns.Add("Paid", "Total Paid");
        _fCustomerRevenueGrid.Columns.Add("Balance", "Outstanding");

        foreach (var item in items)
        {
            _fCustomerRevenueGrid.Rows.Add(item.CustomerName, item.TransactionCount, FormatPeso(item.TotalPaid), FormatPeso(item.OutstandingBalance));
        }
    }

    private void PopulateFleetUtilization(IReadOnlyList<FleetUtilizationItem> items)
    {
        _fleetUtilizationGrid.Columns.Clear();
        _fleetUtilizationGrid.Rows.Clear();
        _fleetUtilizationGrid.Columns.Add("Car", "Car / Plate");
        _fleetUtilizationGrid.Columns.Add("RentedDays", "Rented Days");
        _fleetUtilizationGrid.Columns.Add("AvailableDays", "Available Days");
        _fleetUtilizationGrid.Columns.Add("Utilization", "Utilization Rate");
        _fleetUtilizationGrid.Columns.Add("RentalCount", "Rental Count");
        _fleetUtilizationGrid.Columns.Add("Status", "Status");

        foreach (FleetUtilizationItem item in items)
        {
            _fleetUtilizationGrid.Rows.Add(
                $"{item.CarName} ({item.PlateNumber})",
                item.RentedDays,
                item.AvailableDays,
                FormatPercent(item.UtilizationRate),
                item.RentalCount,
                item.Status);
        }
    }

    private void PopulateFleetRevenue(IReadOnlyList<FleetRevenuePerCarItem> items)
    {
        _fleetRevenueGrid.Columns.Clear();
        _fleetRevenueGrid.Rows.Clear();
        _fleetRevenueGrid.Columns.Add("Car", "Car / Plate");
        _fleetRevenueGrid.Columns.Add("Rental", "Rental Revenue");
        _fleetRevenueGrid.Columns.Add("Extension", "Extension Fees");
        _fleetRevenueGrid.Columns.Add("Damage", "Damage Fees");
        _fleetRevenueGrid.Columns.Add("Late", "Late Fees");
        _fleetRevenueGrid.Columns.Add("Total", "Total Revenue");
        _fleetRevenueGrid.Columns.Add("Average", "Avg / Rental");

        foreach (FleetRevenuePerCarItem item in items)
        {
            _fleetRevenueGrid.Rows.Add(
                $"{item.CarName} ({item.PlateNumber})",
                FormatPeso(item.RentalRevenue),
                FormatPeso(item.ExtensionFees),
                FormatPeso(item.DamageFees),
                FormatPeso(item.LateFees),
                FormatPeso(item.TotalRevenue),
                FormatPeso(item.AverageRevenuePerRental));
        }
    }

    private void PopulateFleetMaintenance(IReadOnlyList<FleetMaintenanceItem> items)
    {
        _fleetMaintenanceGrid.Columns.Clear();
        _fleetMaintenanceGrid.Rows.Clear();
        _fleetMaintenanceGrid.Columns.Add("Car", "Car / Plate");
        _fleetMaintenanceGrid.Columns.Add("Schedule", "Schedule");
        _fleetMaintenanceGrid.Columns.Add("Dates", "Dates");
        _fleetMaintenanceGrid.Columns.Add("Status", "Status");

        foreach (FleetMaintenanceItem item in items)
        {
            _fleetMaintenanceGrid.Rows.Add(
                $"{item.CarName} ({item.PlateNumber})",
                item.Title,
                $"{item.StartDate:MMM d, yyyy} - {item.EndDate:MMM d, yyyy}",
                item.Status);
        }
    }

    private void PopulateUpcomingReturns(IReadOnlyList<OperationsReturnItem> items)
    {
        _opsUpcomingReturnsGrid.Columns.Clear();
        _opsUpcomingReturnsGrid.Rows.Clear();
        _opsUpcomingReturnsGrid.Columns.Add("ExpectedReturn", "Expected Return");
        _opsUpcomingReturnsGrid.Columns.Add("Code", "Transaction Code");
        _opsUpcomingReturnsGrid.Columns.Add("Customer", "Customer");
        _opsUpcomingReturnsGrid.Columns.Add("Contact", "Contact");
        _opsUpcomingReturnsGrid.Columns.Add("Car", "Car / Plate");
        _opsUpcomingReturnsGrid.Columns.Add("Payment", "Payment Status");

        foreach (OperationsReturnItem item in items)
        {
            _opsUpcomingReturnsGrid.Rows.Add(
                FormatDate(item.ExpectedReturn),
                item.TransactionCode,
                item.CustomerName,
                item.Contact,
                $"{item.CarName} ({item.PlateNumber})",
                item.PaymentStatus);
        }
    }

    private void PopulateLateReturns(IReadOnlyList<OperationsReturnItem> items)
    {
        _opsLateReturnsGrid.Columns.Clear();
        _opsLateReturnsGrid.Rows.Clear();
        _opsLateReturnsGrid.Columns.Add("ExpectedReturn", "Expected Return");
        _opsLateReturnsGrid.Columns.Add("DaysLate", "Days Late");
        _opsLateReturnsGrid.Columns.Add("LateFee", "Estimated Late Fee");
        _opsLateReturnsGrid.Columns.Add("Code", "Transaction Code");
        _opsLateReturnsGrid.Columns.Add("Customer", "Customer");
        _opsLateReturnsGrid.Columns.Add("Contact", "Contact");
        _opsLateReturnsGrid.Columns.Add("Car", "Car / Plate");

        foreach (OperationsReturnItem item in items)
        {
            _opsLateReturnsGrid.Rows.Add(
                FormatDate(item.ExpectedReturn),
                item.DaysLate,
                FormatPeso(item.EstimatedLateFee),
                item.TransactionCode,
                item.CustomerName,
                item.Contact,
                $"{item.CarName} ({item.PlateNumber})");
        }
    }

    private void PopulateActiveRentals(IReadOnlyList<OperationsActiveRentalItem> items)
    {
        _opsActiveRentalsGrid.Columns.Clear();
        _opsActiveRentalsGrid.Rows.Clear();
        _opsActiveRentalsGrid.Columns.Add("Code", "Transaction Code");
        _opsActiveRentalsGrid.Columns.Add("Customer", "Customer");
        _opsActiveRentalsGrid.Columns.Add("Contact", "Contact");
        _opsActiveRentalsGrid.Columns.Add("Car", "Car / Plate");
        _opsActiveRentalsGrid.Columns.Add("Start", "Start Date");
        _opsActiveRentalsGrid.Columns.Add("End", "End Date");
        _opsActiveRentalsGrid.Columns.Add("Payment", "Payment Status");

        foreach (OperationsActiveRentalItem item in items)
        {
            _opsActiveRentalsGrid.Rows.Add(
                item.TransactionCode,
                item.CustomerName,
                item.Contact,
                $"{item.CarName} ({item.PlateNumber})",
                FormatDate(item.StartDate),
                FormatDate(item.EndDate),
                item.PaymentStatus);
        }
    }

    private void PopulateUpcomingReservations(IReadOnlyList<OperationsReservationItem> items)
    {
        _opsUpcomingReservationsGrid.Columns.Clear();
        _opsUpcomingReservationsGrid.Rows.Clear();
        _opsUpcomingReservationsGrid.Columns.Add("Date", "Schedule Date");
        _opsUpcomingReservationsGrid.Columns.Add("Customer", "Customer");
        _opsUpcomingReservationsGrid.Columns.Add("Contact", "Contact");
        _opsUpcomingReservationsGrid.Columns.Add("Car", "Car / Plate");
        _opsUpcomingReservationsGrid.Columns.Add("Status", "Status");
        _opsUpcomingReservationsGrid.Columns.Add("Payment", "Payment Status");

        foreach (OperationsReservationItem item in items)
        {
            _opsUpcomingReservationsGrid.Rows.Add(
                FormatDate(item.ScheduleDate),
                item.CustomerName,
                item.Contact,
                $"{item.CarName} ({item.PlateNumber})",
                item.Status,
                item.PaymentStatus);
        }
    }

    private void PopulateOperationsMaintenance(IReadOnlyList<OperationsMaintenanceItem> items)
    {
        _opsMaintenanceGrid.Columns.Clear();
        _opsMaintenanceGrid.Rows.Clear();
        _opsMaintenanceGrid.Columns.Add("DateRange", "Date Range");
        _opsMaintenanceGrid.Columns.Add("Car", "Car / Plate");
        _opsMaintenanceGrid.Columns.Add("Status", "Status");
        _opsMaintenanceGrid.Columns.Add("Source", "Source");

        foreach (OperationsMaintenanceItem item in items)
        {
            _opsMaintenanceGrid.Rows.Add(
                $"{FormatDate(item.StartDate)} - {FormatDate(item.EndDate)}",
                $"{item.CarName} ({item.PlateNumber})",
                item.Status,
                item.Source);
        }
    }

    private void PopulateAvailableCars(IReadOnlyList<OperationsAvailableCarItem> items)
    {
        _opsAvailableCarsGrid.Columns.Clear();
        _opsAvailableCarsGrid.Rows.Clear();
        _opsAvailableCarsGrid.Columns.Add("Car", "Car / Plate");
        _opsAvailableCarsGrid.Columns.Add("Status", "Status");
        _opsAvailableCarsGrid.Columns.Add("Rate", "Rate Per Day");
        _opsAvailableCarsGrid.Columns.Add("Seats", "Seating Capacity");

        foreach (OperationsAvailableCarItem item in items)
        {
            _opsAvailableCarsGrid.Rows.Add(
                $"{item.CarName} ({item.PlateNumber})",
                item.Status,
                FormatPeso(item.RatePerDay),
                item.SeatingCapacity?.ToString() ?? "-");
        }
    }

    private static string FormatPeso(decimal amount) => $"₱{amount:N2}";

    private static string FormatPercent(decimal value) => $"{value:N1}%";

    private static string FormatDate(DateTime date) => $"{date:MMM d, yyyy}";
}
