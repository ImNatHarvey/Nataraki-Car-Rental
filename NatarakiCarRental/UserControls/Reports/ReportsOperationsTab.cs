using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;

namespace NatarakiCarRental.UserControls.Reports;

public sealed class ReportsOperationsTab : UserControl, IReportTab
{
    private readonly ReportService _reportService = new();
    private readonly FlowLayoutPanel _metricPanel = ReportLayoutHelper.CreateMetricPanel();

    private readonly MetricCardControl _upcomingReturnsCard = new();
    private readonly MetricCardControl _lateReturnsCard = new();
    private readonly MetricCardControl _activeRentalsCard = new();
    private readonly MetricCardControl _upcomingReservationsCard = new();
    private readonly MetricCardControl _reservedCarsCard = new();
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
        TableLayoutPanel layout = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 6 };
        layout.Controls.Add(CreateMetricPanel());
        layout.Controls.Add(CreateReturnsLayout());
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Active Rentals", _activeRentalsGrid, 340));
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Upcoming Reservations", _upcomingReservationsGrid, 340));
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Maintenance Visibility", _maintenanceGrid, 300));
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Available Cars", _availableCarsGrid, 320));
        Controls.Add(layout);
    }

    private FlowLayoutPanel CreateMetricPanel()
    {
        ReportLayoutHelper.AddMetricCard(_metricPanel, _upcomingReturnsCard, IconChar.CalendarCheck, "Upcoming Returns", "0", "Expected back in range", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _lateReturnsCard, IconChar.TriangleExclamation, "Late Returns", "0", "Past expected return", ThemeHelper.Danger);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _activeRentalsCard, IconChar.Key, "Active Rentals", "0", "Ongoing rentals", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _upcomingReservationsCard, IconChar.CalendarDays, "Upcoming Reservations", "0", "Starts in range", ThemeHelper.Warning);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _reservedCarsCard, IconChar.Bookmark, "Reserved Cars", "0", "Reserved in range", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _maintenanceCard, IconChar.ScrewdriverWrench, "Cars Under Maintenance", "0", "Schedule-based", ThemeHelper.Warning);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _availableCarsCard, IconChar.CircleCheck, "Available Cars", "0", "No blocking schedule", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _completedReturnsCard, IconChar.FlagCheckered, "Completed Returns", "0", "Closed in range", ThemeHelper.GrayIcon);
        LayoutCards();
        return _metricPanel;
    }

    private TableLayoutPanel CreateReturnsLayout()
    {
        TableLayoutPanel grid = new() { Dock = DockStyle.Top, Height = 374, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 12, 0, 4) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Upcoming Returns", _upcomingReturnsGrid), 0, 0);
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Late Returns", _lateReturnsGrid), 1, 0);
        return grid;
    }

    private void LayoutCards() => ReportLayoutHelper.LayoutMetricCards(_metricPanel, GetCards());

    private List<Control> GetCards() =>
    [
        _upcomingReturnsCard, _lateReturnsCard, _activeRentalsCard, _upcomingReservationsCard,
        _reservedCarsCard, _maintenanceCard, _availableCarsCard, _completedReturnsCard
    ];

    private void UpdateSummaryCards(OperationsMetrics metrics)
    {
        _upcomingReturnsCard.SetMetric(IconChar.CalendarCheck, "Upcoming Returns", metrics.UpcomingReturns.ToString(), "Expected back in range", ThemeHelper.Primary);
        _lateReturnsCard.SetMetric(IconChar.TriangleExclamation, "Late Returns", metrics.LateReturns.ToString(), "Past expected return", ThemeHelper.Danger);
        _activeRentalsCard.SetMetric(IconChar.Key, "Active Rentals", metrics.ActiveRentals.ToString(), "Ongoing rentals", ThemeHelper.Success);
        _upcomingReservationsCard.SetMetric(IconChar.CalendarDays, "Upcoming Reservations", metrics.UpcomingReservations.ToString(), "Starts in range", ThemeHelper.Warning);
        _reservedCarsCard.SetMetric(IconChar.Bookmark, "Reserved Cars", metrics.ReservedCars.ToString(), "Reserved in range", ThemeHelper.Primary);
        _maintenanceCard.SetMetric(IconChar.ScrewdriverWrench, "Cars Under Maintenance", metrics.CarsUnderMaintenance.ToString(), "Schedule-based", ThemeHelper.Warning);
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
        _upcomingReturnsGrid.Columns.Add("Payment", "Payment Status");
        foreach (OperationsReturnItem item in items)
        {
            _upcomingReturnsGrid.Rows.Add(ReportLayoutHelper.FormatDate(item.ExpectedReturn), item.TransactionCode, item.CustomerName, item.Contact, $"{item.CarName} ({item.PlateNumber})", item.PaymentStatus);
        }
        ReportLayoutHelper.AddEmptyRow(_upcomingReturnsGrid);
    }

    private void PopulateLateReturns(IReadOnlyList<OperationsReturnItem> items)
    {
        _lateReturnsGrid.Columns.Clear(); _lateReturnsGrid.Rows.Clear();
        _lateReturnsGrid.Columns.Add("ExpectedReturn", "Expected Return");
        _lateReturnsGrid.Columns.Add("DaysLate", "Days Late");
        _lateReturnsGrid.Columns.Add("LateFee", "Estimated Late Fee");
        _lateReturnsGrid.Columns.Add("Code", "Transaction Code");
        _lateReturnsGrid.Columns.Add("Customer", "Customer");
        _lateReturnsGrid.Columns.Add("Contact", "Contact");
        _lateReturnsGrid.Columns.Add("Car", "Car / Plate");
        foreach (OperationsReturnItem item in items)
        {
            _lateReturnsGrid.Rows.Add(ReportLayoutHelper.FormatDate(item.ExpectedReturn), item.DaysLate, ReportLayoutHelper.FormatPeso(item.EstimatedLateFee), item.TransactionCode, item.CustomerName, item.Contact, $"{item.CarName} ({item.PlateNumber})");
        }
        ReportLayoutHelper.AddEmptyRow(_lateReturnsGrid);
    }

    private void PopulateActiveRentals(IReadOnlyList<OperationsActiveRentalItem> items)
    {
        _activeRentalsGrid.Columns.Clear(); _activeRentalsGrid.Rows.Clear();
        _activeRentalsGrid.Columns.Add("Code", "Transaction Code");
        _activeRentalsGrid.Columns.Add("Customer", "Customer");
        _activeRentalsGrid.Columns.Add("Contact", "Contact");
        _activeRentalsGrid.Columns.Add("Car", "Car / Plate");
        _activeRentalsGrid.Columns.Add("Start", "Start Date");
        _activeRentalsGrid.Columns.Add("End", "End Date");
        _activeRentalsGrid.Columns.Add("Payment", "Payment Status");
        foreach (OperationsActiveRentalItem item in items)
        {
            _activeRentalsGrid.Rows.Add(item.TransactionCode, item.CustomerName, item.Contact, $"{item.CarName} ({item.PlateNumber})", ReportLayoutHelper.FormatDate(item.StartDate), ReportLayoutHelper.FormatDate(item.EndDate), item.PaymentStatus);
        }
        ReportLayoutHelper.AddEmptyRow(_activeRentalsGrid);
    }

    private void PopulateUpcomingReservations(IReadOnlyList<OperationsReservationItem> items)
    {
        _upcomingReservationsGrid.Columns.Clear(); _upcomingReservationsGrid.Rows.Clear();
        _upcomingReservationsGrid.Columns.Add("Date", "Schedule Date");
        _upcomingReservationsGrid.Columns.Add("Customer", "Customer");
        _upcomingReservationsGrid.Columns.Add("Contact", "Contact");
        _upcomingReservationsGrid.Columns.Add("Car", "Car / Plate");
        _upcomingReservationsGrid.Columns.Add("Status", "Status");
        _upcomingReservationsGrid.Columns.Add("Payment", "Payment Status");
        foreach (OperationsReservationItem item in items)
        {
            _upcomingReservationsGrid.Rows.Add(ReportLayoutHelper.FormatDate(item.ScheduleDate), item.CustomerName, item.Contact, $"{item.CarName} ({item.PlateNumber})", item.Status, item.PaymentStatus);
        }
        ReportLayoutHelper.AddEmptyRow(_upcomingReservationsGrid);
    }

    private void PopulateOperationsMaintenance(IReadOnlyList<OperationsMaintenanceItem> items)
    {
        _maintenanceGrid.Columns.Clear(); _maintenanceGrid.Rows.Clear();
        _maintenanceGrid.Columns.Add("DateRange", "Date Range");
        _maintenanceGrid.Columns.Add("Car", "Car / Plate");
        _maintenanceGrid.Columns.Add("Status", "Status");
        _maintenanceGrid.Columns.Add("Source", "Source");
        foreach (OperationsMaintenanceItem item in items)
        {
            _maintenanceGrid.Rows.Add($"{ReportLayoutHelper.FormatDate(item.StartDate)} - {ReportLayoutHelper.FormatDate(item.EndDate)}", $"{item.CarName} ({item.PlateNumber})", item.Status, item.Source);
        }
        ReportLayoutHelper.AddEmptyRow(_maintenanceGrid);
    }

    private void PopulateAvailableCars(IReadOnlyList<OperationsAvailableCarItem> items)
    {
        _availableCarsGrid.Columns.Clear(); _availableCarsGrid.Rows.Clear();
        _availableCarsGrid.Columns.Add("Car", "Car / Plate");
        _availableCarsGrid.Columns.Add("Status", "Status");
        _availableCarsGrid.Columns.Add("Rate", "Rate Per Day");
        _availableCarsGrid.Columns.Add("Seats", "Seating Capacity");
        foreach (OperationsAvailableCarItem item in items)
        {
            _availableCarsGrid.Rows.Add($"{item.CarName} ({item.PlateNumber})", item.Status, ReportLayoutHelper.FormatPeso(item.RatePerDay), item.SeatingCapacity?.ToString() ?? "-");
        }
        ReportLayoutHelper.AddEmptyRow(_availableCarsGrid);
    }
}
