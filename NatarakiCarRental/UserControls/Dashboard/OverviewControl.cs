using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;
using FleetScheduleModel = NatarakiCarRental.Models.FleetSchedule;

namespace NatarakiCarRental.UserControls.Dashboard;

public sealed class OverviewControl : UserControl
{
    private const int RecentItemLimit = 5;
    private readonly CarService _carService = new();
    private readonly CustomerService _customerService = new();
    private readonly FleetScheduleService _scheduleService = new(currentUserId: null);
    private readonly MetricCardControl _totalCarsCard = new();
    private readonly MetricCardControl _availableCarsCard = new();
    private readonly MetricCardControl _maintenanceCarsCard = new();
    private readonly MetricCardControl _activeCustomersCard = new();
    private readonly MetricCardControl _blacklistedCustomersCard = new();
    private readonly MetricCardControl _todaysSchedulesCard = new();
    private readonly MetricCardControl _upcomingSchedulesCard = new();
    private readonly MetricCardControl _activeMaintenanceCard = new();
    private readonly DataGridView _recentCustomersGrid = CreateOverviewGrid();
    private readonly DataGridView _recentSchedulesGrid = CreateOverviewGrid();
    private readonly Label _recentCustomersEmptyLabel = CreateEmptyLabel("No recent customers yet.");
    private readonly Label _recentSchedulesEmptyLabel = CreateEmptyLabel("No upcoming schedules yet.");

    public OverviewControl()
    {
        InitializeControl();
        Load += OverviewControl_Load;
    }

    private void InitializeControl()
    {
        BackColor = ThemeHelper.ContentBackground;
        Dock = DockStyle.Fill;
        AutoScroll = true;
        Padding = new Padding(32);

        TableLayoutPanel mainLayout = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 5
        };

        mainLayout.Controls.Add(CreateHeaderPanel());
        mainLayout.Controls.Add(CreateMetricGrid());
        mainLayout.Controls.Add(CreateRecentGrid());
        mainLayout.Controls.Add(CreateTransactionNoticePanel());

        Controls.Add(mainLayout);
    }

    private static Panel CreateHeaderPanel()
    {
        Panel panel = new()
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = ThemeHelper.ContentBackground
        };

        Label titleLabel = new()
        {
            Text = "Overview",
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(260, 34),
            Font = FontHelper.Title(22F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Label subtitleLabel = new()
        {
            Text = "Current fleet, customer, and schedule activity.",
            AutoSize = false,
            Location = new Point(2, 42),
            Size = new Size(520, 24),
            Font = FontHelper.Regular(10.5F),
            ForeColor = ThemeHelper.TextSecondary
        };

        panel.Controls.Add(titleLabel);
        panel.Controls.Add(subtitleLabel);
        return panel;
    }

    private TableLayoutPanel CreateMetricGrid()
    {
        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Top,
            Height = 286,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(0, 12, 0, 10)
        };

        for (int i = 0; i < 4; i++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        AddMetricCard(grid, _totalCarsCard, IconChar.Car, "Active Cars", "0", "Registered active vehicles", ThemeHelper.Primary, 0, 0);
        AddMetricCard(grid, _availableCarsCard, IconChar.CircleCheck, "Available Cars", "0", "Ready for rental", ThemeHelper.Success, 1, 0);
        AddMetricCard(grid, _maintenanceCarsCard, IconChar.ScrewdriverWrench, "Cars Under Maintenance", "0", "Manual vehicle state", ThemeHelper.Warning, 2, 0);
        AddMetricCard(grid, _activeCustomersCard, IconChar.Users, "Active Customers", "0", "Ready for booking", ThemeHelper.Success, 3, 0);
        AddMetricCard(grid, _blacklistedCustomersCard, IconChar.UserSlash, "Blacklisted Customers", "0", "Blocked from new schedules", ThemeHelper.Danger, 0, 1);
        AddMetricCard(grid, _todaysSchedulesCard, IconChar.CalendarDay, "Today's Schedules", "0", "Visible today", ThemeHelper.Primary, 1, 1);
        AddMetricCard(grid, _upcomingSchedulesCard, IconChar.CalendarDays, "Upcoming Schedules", "0", "Pending operational work", ThemeHelper.Warning, 2, 1);
        AddMetricCard(grid, _activeMaintenanceCard, IconChar.Wrench, "Active Maintenance", "0", "Ongoing maintenance schedules", Color.FromArgb(234, 88, 12), 3, 1);

        return grid;
    }

    private static void AddMetricCard(
        TableLayoutPanel grid,
        MetricCardControl card,
        IconChar icon,
        string title,
        string value,
        string helperText,
        Color iconColor,
        int column,
        int row)
    {
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, column == 3 ? 0 : 14, row == 0 ? 14 : 0);
        card.SetMetric(icon, title, value, helperText, iconColor);
        grid.Controls.Add(card, column, row);
    }

    private TableLayoutPanel CreateRecentGrid()
    {
        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Top,
            Height = 286,
            ColumnCount = 2,
            Padding = new Padding(0, 10, 0, 0)
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        Panel recentCustomersPanel = CreateDataPanel("Recent Customers", _recentCustomersGrid, _recentCustomersEmptyLabel);
        Panel recentSchedulesPanel = CreateDataPanel("Recent Fleet Schedules", _recentSchedulesGrid, _recentSchedulesEmptyLabel);
        recentCustomersPanel.Margin = new Padding(0, 0, 14, 0);
        grid.Controls.Add(recentCustomersPanel, 0, 0);
        grid.Controls.Add(recentSchedulesPanel, 1, 0);
        return grid;
    }

    private static Panel CreateDataPanel(string title, DataGridView grid, Label emptyLabel)
    {
        Panel panel = ControlFactory.CreateCardPanel(new Size(0, 0));
        panel.Dock = DockStyle.Fill;
        panel.Padding = new Padding(18);

        Label titleLabel = new()
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 30,
            Font = FontHelper.Title(12F),
            ForeColor = ThemeHelper.TextPrimary
        };

        panel.Controls.Add(grid);
        panel.Controls.Add(emptyLabel);
        panel.Controls.Add(titleLabel);
        return panel;
    }

    private static Panel CreateTransactionNoticePanel()
    {
        Panel panel = ControlFactory.CreateCardPanel(new Size(0, 86));
        panel.Dock = DockStyle.Top;
        panel.Margin = new Padding(0, 18, 0, 0);
        panel.Padding = new Padding(20);

        Label label = new()
        {
            Text = "Transaction metrics will appear once the Transaction module is available.",
            Dock = DockStyle.Fill,
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextSecondary,
            TextAlign = ContentAlignment.MiddleLeft
        };

        panel.Controls.Add(label);
        return panel;
    }

    private static DataGridView CreateOverviewGrid()
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
            GridColor = ThemeHelper.Border,
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
        grid.ColumnHeadersDefaultCellStyle.Font = FontHelper.SemiBold(9F);
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        return grid;
    }

    private static Label CreateEmptyLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Bottom,
            Height = 34,
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };
    }

    private async void OverviewControl_Load(object? sender, EventArgs e)
    {
        Load -= OverviewControl_Load;

        try
        {
            CarCounts carCounts = await _carService.GetCarCountsAsync();
            CustomerCounts customerCounts = await _customerService.GetCustomerCountsAsync();
            FleetScheduleOverviewCounts scheduleCounts = await _scheduleService.GetOverviewCountsAsync(DateTime.Today);
            IReadOnlyList<Customer> recentCustomers = await _customerService.GetRecentCustomersAsync(RecentItemLimit);
            IReadOnlyList<FleetScheduleModel> recentSchedules = await _scheduleService.GetRecentUpcomingSchedulesAsync(DateTime.Today, RecentItemLimit);

            UpdateMetricCards(carCounts, customerCounts, scheduleCounts);
            PopulateRecentCustomers(recentCustomers);
            PopulateRecentSchedules(recentSchedules);
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning($"Unable to load overview data.\n\n{exception.Message}", "Overview");
        }
    }

    private void UpdateMetricCards(CarCounts carCounts, CustomerCounts customerCounts, FleetScheduleOverviewCounts scheduleCounts)
    {
        _totalCarsCard.SetMetric(IconChar.Car, "Active Cars", carCounts.TotalCars.ToString(), "Registered active vehicles", ThemeHelper.Primary);
        _availableCarsCard.SetMetric(IconChar.CircleCheck, "Available Cars", carCounts.AvailableCars.ToString(), "Ready for rental", ThemeHelper.Success);
        _maintenanceCarsCard.SetMetric(IconChar.ScrewdriverWrench, "Cars Under Maintenance", carCounts.MaintenanceCars.ToString(), "Manual vehicle state", ThemeHelper.Warning);
        _activeCustomersCard.SetMetric(IconChar.Users, "Active Customers", customerCounts.ActiveCustomers.ToString(), "Ready for booking", ThemeHelper.Success);
        _blacklistedCustomersCard.SetMetric(IconChar.UserSlash, "Blacklisted Customers", customerCounts.BlacklistedCustomers.ToString(), "Blocked from new schedules", ThemeHelper.Danger);
        _todaysSchedulesCard.SetMetric(IconChar.CalendarDay, "Today's Schedules", scheduleCounts.TodaysSchedules.ToString(), "Visible today", ThemeHelper.Primary);
        _upcomingSchedulesCard.SetMetric(IconChar.CalendarDays, "Upcoming Schedules", scheduleCounts.UpcomingSchedules.ToString(), "Pending operational work", ThemeHelper.Warning);
        _activeMaintenanceCard.SetMetric(IconChar.Wrench, "Active Maintenance", scheduleCounts.ActiveMaintenanceSchedules.ToString(), "Ongoing maintenance schedules", Color.FromArgb(234, 88, 12));
    }

    private void PopulateRecentCustomers(IReadOnlyList<Customer> customers)
    {
        _recentCustomersGrid.Columns.Clear();
        _recentCustomersGrid.Rows.Clear();
        _recentCustomersGrid.Columns.Add("Customer", "Customer");
        _recentCustomersGrid.Columns.Add("Contact", "Contact");
        _recentCustomersGrid.Columns.Add("Status", "Status");

        foreach (Customer customer in customers)
        {
            _recentCustomersGrid.Rows.Add(
                $"{customer.FirstName} {customer.LastName}".Trim(),
                customer.PhoneNumber,
                customer.IsBlacklisted ? "Blacklisted" : "Active");
        }

        _recentCustomersEmptyLabel.Visible = customers.Count == 0;
    }

    private void PopulateRecentSchedules(IReadOnlyList<FleetScheduleModel> schedules)
    {
        _recentSchedulesGrid.Columns.Clear();
        _recentSchedulesGrid.Rows.Clear();
        _recentSchedulesGrid.Columns.Add("Schedule", "Schedule");
        _recentSchedulesGrid.Columns.Add("Car", "Car");
        _recentSchedulesGrid.Columns.Add("Dates", "Dates");

        foreach (FleetScheduleModel schedule in schedules)
        {
            _recentSchedulesGrid.Rows.Add(
                schedule.Title,
                $"{schedule.CarName} ({schedule.PlateNumber})",
                $"{schedule.StartDate:MMM d} - {schedule.EndDate:MMM d}");
        }

        _recentSchedulesEmptyLabel.Visible = schedules.Count == 0;
    }
}
