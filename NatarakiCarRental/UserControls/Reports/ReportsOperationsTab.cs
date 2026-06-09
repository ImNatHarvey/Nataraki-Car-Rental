using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;

namespace NatarakiCarRental.UserControls.Reports;

public sealed class ReportsOperationsTab : UserControl, IReportTab
{
    private readonly ReportService _reportService = new();
    
    private readonly FlowLayoutPanel _generalStatusPanel = ReportLayoutHelper.CreateMetricPanel();
    private readonly FlowLayoutPanel _returnsActivityPanel = ReportLayoutHelper.CreateMetricPanel();

    private readonly MetricCardControl _upcomingReturnsCard = new();
    private readonly MetricCardControl _lateReturnsCard = new();
    private readonly MetricCardControl _activeRentalsCard = new();
    private readonly MetricCardControl _upcomingReservationsCard = new();
    private readonly MetricCardControl _maintenanceCard = new();
    private readonly MetricCardControl _availableCarsCard = new();
    private readonly MetricCardControl _completedReturnsCard = new();

    private readonly DataGridView _upcomingReturnsGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _lateReturnsGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _activeRentalsGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _upcomingReservationsGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _maintenanceGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _availableCarsGrid = ReportLayoutHelper.CreateSummaryGrid();

    public ReportsOperationsTab()
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
            UpdateSummaryCards(await _reportService.GetOperationsMetricsAsync(from, to));
            PopulateUpcomingReturns(await _reportService.GetUpcomingReturnsAsync(from, to));
            PopulateLateReturns(await _reportService.GetLateReturnsAsync(DateTime.Today));
            PopulateActiveRentals(await _reportService.GetActiveRentalsReportAsync(from, to));
            PopulateUpcomingReservations(await _reportService.GetUpcomingReservationsAsync(from, to));
            PopulateOperationsMaintenance(await _reportService.GetMaintenanceVisibilityAsync(from, to));
            PopulateAvailableCars(await _reportService.GetAvailableCarsReportAsync(from, to));
            LayoutCards();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning($"Unable to load operations reports.\n\n{exception.Message}", "Reports");
        }
    }

    private void InitializeLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 9 };
        
        // 1. Overview Status
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Operations & Availability Status"));
        layout.Controls.Add(_generalStatusPanel);
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Available Vehicles (No Blocking Schedule)", _availableCarsGrid, 320));
        
        // 2. Returns & Active Rentals
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Returns & Active Rentals"));
        layout.Controls.Add(_returnsActivityPanel);
        layout.Controls.Add(CreateReturnsLayout());
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Current Active Rentals", _activeRentalsGrid, 340));

        // 3. Reservations & Maintenance
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Reservations & Maintenance Tracking"));
        layout.Controls.Add(CreateReservationsMaintenanceLayout());

        InitCards();
        Controls.Add(layout);
    }

    private void InitCards()
    {
        // Status Group
        ReportLayoutHelper.AddMetricCard(_generalStatusPanel, _availableCarsCard, IconChar.CircleCheck, "Available Cars", "0", "No blocking schedule", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_generalStatusPanel, _activeRentalsCard, IconChar.Key, "Active Rentals", "0", "Ongoing rentals", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_generalStatusPanel, _maintenanceCard, IconChar.ScrewdriverWrench, "Under Maintenance", "0", "Schedule-based", ThemeHelper.Danger);

        // Returns Group
        ReportLayoutHelper.AddMetricCard(_returnsActivityPanel, _upcomingReturnsCard, IconChar.CalendarCheck, "Upcoming Returns", "0", "Expected back soon", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_returnsActivityPanel, _lateReturnsCard, IconChar.TriangleExclamation, "Late Returns", "0", "Past expected date", ThemeHelper.Danger);
        ReportLayoutHelper.AddMetricCard(_returnsActivityPanel, _completedReturnsCard, IconChar.FlagCheckered, "Completed Returns", "0", "Closed in range", ThemeHelper.GrayIcon);
        ReportLayoutHelper.AddMetricCard(_returnsActivityPanel, _upcomingReservationsCard, IconChar.CalendarDays, "Upcoming Starts", "0", "Starting in range", ThemeHelper.Warning);
    }

    private TableLayoutPanel CreateReturnsLayout()
    {
        TableLayoutPanel grid = new() { Dock = DockStyle.Top, Height = 374, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 12, 0, 4) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Upcoming Returns Details", _upcomingReturnsGrid), 0, 0);
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Overdue Returns (Action Required)", _lateReturnsGrid), 1, 0);
        return grid;
    }

    private TableLayoutPanel CreateReservationsMaintenanceLayout()
    {
        TableLayoutPanel grid = new() { Dock = DockStyle.Top, Height = 374, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 12, 0, 4) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Upcoming Reservations Details", _upcomingReservationsGrid), 0, 0);
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Operational Maintenance Visibility", _maintenanceGrid), 1, 0);
        return grid;
    }

    private void LayoutCards()
    {
        ReportLayoutHelper.LayoutMetricCards(_generalStatusPanel, [_availableCarsCard, _activeRentalsCard, _maintenanceCard]);
        ReportLayoutHelper.LayoutMetricCards(_returnsActivityPanel, [_upcomingReturnsCard, _lateReturnsCard, _completedReturnsCard, _upcomingReservationsCard]);
    }

    private void UpdateSummaryCards(OperationsMetrics metrics)
    {
        _upcomingReturnsCard.SetMetric(IconChar.CalendarCheck, "Upcoming Returns", metrics.UpcomingReturns.ToString(), "Expected back soon", ThemeHelper.Primary);
        _lateReturnsCard.SetMetric(IconChar.TriangleExclamation, "Late Returns", metrics.LateReturns.ToString(), "Past expected date", ThemeHelper.Danger);
        _activeRentalsCard.SetMetric(IconChar.Key, "Active Rentals", metrics.ActiveRentals.ToString(), "Ongoing rentals", ThemeHelper.Primary);
        _upcomingReservationsCard.SetMetric(IconChar.CalendarDays, "Upcoming Starts", metrics.UpcomingReservations.ToString(), "Starting in range", ThemeHelper.Warning);
        _maintenanceCard.SetMetric(IconChar.ScrewdriverWrench, "Under Maintenance", metrics.CarsUnderMaintenance.ToString(), "Schedule-based", ThemeHelper.Danger);
        _availableCarsCard.SetMetric(IconChar.CircleCheck, "Available Cars", metrics.AvailableCars.ToString(), "No blocking schedule", ThemeHelper.Success);
        _completedReturnsCard.SetMetric(IconChar.FlagCheckered, "Completed Returns", metrics.CompletedReturns.ToString(), "Closed in range", ThemeHelper.GrayIcon);
    }

    private void PopulateUpcomingReturns(IReadOnlyList<OperationsReturnItem> items)
    {
        _upcomingReturnsGrid.Columns.Clear(); _upcomingReturnsGrid.Rows.Clear();
        _upcomingReturnsGrid.Columns.Add("ExpectedReturn", "Expected Return");
        _upcomingReturnsGrid.Columns.Add("Code", "Transaction Code");
        _upcomingReturnsGrid.Columns.Add("Customer", "Customer");
        _upcomingReturnsGrid.Columns.Add("Contact", "Contact");
        _upcomingReturnsGrid.Columns.Add("Car", "Car / Plate");
        foreach (OperationsReturnItem item in items) _upcomingReturnsGrid.Rows.Add(ReportLayoutHelper.FormatDate(item.ExpectedReturn), item.TransactionCode, item.CustomerName, item.Contact, $"{item.CarName} ({item.PlateNumber})");
        ReportLayoutHelper.AddEmptyRow(_upcomingReturnsGrid);
    }

    private void PopulateLateReturns(IReadOnlyList<OperationsReturnItem> items)
    {
        _lateReturnsGrid.Columns.Clear(); _lateReturnsGrid.Rows.Clear();
        _lateReturnsGrid.Columns.Add("ExpectedReturn", "Expected Return");
        _lateReturnsGrid.Columns.Add("DaysLate", "Days Late");
        _lateReturnsGrid.Columns.Add("LateFee", "Late Fee");
        _lateReturnsGrid.Columns.Add("Customer", "Customer");
        _lateReturnsGrid.Columns.Add("Car", "Car / Plate");
        foreach (OperationsReturnItem item in items) _lateReturnsGrid.Rows.Add(ReportLayoutHelper.FormatDate(item.ExpectedReturn), item.DaysLate, ReportLayoutHelper.FormatPeso(item.EstimatedLateFee), item.CustomerName, $"{item.CarName} ({item.PlateNumber})");
        ReportLayoutHelper.AddEmptyRow(_lateReturnsGrid);
    }

    private void PopulateActiveRentals(IReadOnlyList<OperationsActiveRentalItem> items)
    {
        _activeRentalsGrid.Columns.Clear(); _activeRentalsGrid.Rows.Clear();
        _activeRentalsGrid.Columns.Add("Code", "Transaction Code");
        _activeRentalsGrid.Columns.Add("Customer", "Customer");
        _activeRentalsGrid.Columns.Add("Car", "Car / Plate");
        _activeRentalsGrid.Columns.Add("Start", "Start Date");
        _activeRentalsGrid.Columns.Add("End", "End Date");
        foreach (OperationsActiveRentalItem item in items) _activeRentalsGrid.Rows.Add(item.TransactionCode, item.CustomerName, $"{item.CarName} ({item.PlateNumber})", ReportLayoutHelper.FormatDate(item.StartDate), ReportLayoutHelper.FormatDate(item.EndDate));
        ReportLayoutHelper.AddEmptyRow(_activeRentalsGrid);
    }

    private void PopulateUpcomingReservations(IReadOnlyList<OperationsReservationItem> items)
    {
        _upcomingReservationsGrid.Columns.Clear(); _upcomingReservationsGrid.Rows.Clear();
        _upcomingReservationsGrid.Columns.Add("Date", "Schedule Date");
        _upcomingReservationsGrid.Columns.Add("Customer", "Customer");
        _upcomingReservationsGrid.Columns.Add("Car", "Car / Plate");
        _upcomingReservationsGrid.Columns.Add("Status", "Status");
        foreach (OperationsReservationItem item in items) _upcomingReservationsGrid.Rows.Add(ReportLayoutHelper.FormatDate(item.ScheduleDate), item.CustomerName, $"{item.CarName} ({item.PlateNumber})", item.Status);
        ReportLayoutHelper.AddEmptyRow(_upcomingReservationsGrid);
    }

    private void PopulateOperationsMaintenance(IReadOnlyList<OperationsMaintenanceItem> items)
    {
        _maintenanceGrid.Columns.Clear(); _maintenanceGrid.Rows.Clear();
        _maintenanceGrid.Columns.Add("DateRange", "Date Range");
        _maintenanceGrid.Columns.Add("Car", "Car / Plate");
        _maintenanceGrid.Columns.Add("Status", "Status");
        foreach (OperationsMaintenanceItem item in items) _maintenanceGrid.Rows.Add($"{ReportLayoutHelper.FormatDate(item.StartDate)} - {ReportLayoutHelper.FormatDate(item.EndDate)}", $"{item.CarName} ({item.PlateNumber})", item.Status);
        ReportLayoutHelper.AddEmptyRow(_maintenanceGrid);
    }

    private void PopulateAvailableCars(IReadOnlyList<OperationsAvailableCarItem> items)
    {
        _availableCarsGrid.Columns.Clear(); _availableCarsGrid.Rows.Clear();
        _availableCarsGrid.Columns.Add("Car", "Car / Plate");
        _availableCarsGrid.Columns.Add("Status", "Status");
        _availableCarsGrid.Columns.Add("Rate", "Rate / Day");
        foreach (OperationsAvailableCarItem item in items) _availableCarsGrid.Rows.Add($"{item.CarName} ({item.PlateNumber})", item.Status, ReportLayoutHelper.FormatPeso(item.RatePerDay));
        ReportLayoutHelper.AddEmptyRow(_availableCarsGrid);
    }
}