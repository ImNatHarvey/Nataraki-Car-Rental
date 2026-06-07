using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;

namespace NatarakiCarRental.UserControls.Reports;

public sealed class ReportsFleetPerformanceTab : UserControl, IReportTab
{
    private readonly ReportService _reportService = new();
    
    private readonly FlowLayoutPanel _availabilityPanel = ReportLayoutHelper.CreateMetricPanel();
    private readonly FlowLayoutPanel _performancePanel = ReportLayoutHelper.CreateMetricPanel();

    private readonly MetricCardControl _totalRevenueCard = new();
    private readonly MetricCardControl _averageRevenueCard = new();
    private readonly MetricCardControl _topEarningCarCard = new();
    private readonly MetricCardControl _mostRentedCarCard = new();
    private readonly MetricCardControl _averageUtilizationCard = new();
    private readonly MetricCardControl _activeRentalsCard = new();
    private readonly MetricCardControl _completedRentalsCard = new();
    private readonly MetricCardControl _maintenanceCard = new();

    private readonly DataGridView _utilizationGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _revenueGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _topEarningGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _mostRentedGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _leastUsedGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _maintenanceGrid = ReportLayoutHelper.CreateSummaryGrid();

    public ReportsFleetPerformanceTab()
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
            UpdateSummaryCards(await _reportService.GetFleetPerformanceMetricsAsync(from, to));
            PopulateFleetUtilization(await _reportService.GetFleetUtilizationAsync(from, to));
            PopulateFleetRevenue(await _reportService.GetFleetRevenuePerCarAsync(from, to));
            PopulateTopCars(_topEarningGrid, await _reportService.GetTopCarsByRevenueAsync(from, to, 5));
            PopulateTopCars(_mostRentedGrid, await _reportService.GetMostRentedCarsAsync(from, to, 5));
            PopulateTopCars(_leastUsedGrid, await _reportService.GetLeastUsedCarsAsync(from, to, 5));
            PopulateFleetMaintenance(await _reportService.GetCarsUnderMaintenanceAsync(from, to));
            LayoutCards();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning($"Unable to load fleet performance reports.\n\n{exception.Message}", "Reports");
        }
    }

    private void InitializeLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 8 };
        
        // 1. Availability & Status
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Fleet Availability & Status"));
        layout.Controls.Add(_availabilityPanel);
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Fleet Utilization (Active vs Available Days)", _utilizationGrid, 360));
        
        // 2. Financial Performance
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Vehicle Revenue Performance"));
        layout.Controls.Add(_performancePanel);
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Revenue Per Unit Breakdown", _revenueGrid, 360));

        // 3. Rankings & Maintenance
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Performance Rankings & Maintenance"));
        layout.Controls.Add(CreatePerformanceTablesLayout());
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Current Maintenance Visibility", _maintenanceGrid, 300));

        InitCards();
        Controls.Add(layout);
    }

    private void InitCards()
    {
        // Availability Group
        ReportLayoutHelper.AddMetricCard(_availabilityPanel, _averageUtilizationCard, IconChar.GaugeHigh, "Avg Utilization", "0.0%", "Days rented in range", ThemeHelper.Warning);
        ReportLayoutHelper.AddMetricCard(_availabilityPanel, _activeRentalsCard, IconChar.Key, "Active Rentals", "0", "Currently released", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_availabilityPanel, _completedRentalsCard, IconChar.FlagCheckered, "Completed Rentals", "0", "Closed in range", ThemeHelper.GrayIcon);
        ReportLayoutHelper.AddMetricCard(_availabilityPanel, _maintenanceCard, IconChar.ScrewdriverWrench, "Under Maintenance", "0", "Ongoing/Scheduled", ThemeHelper.Warning);

        // Performance Group
        ReportLayoutHelper.AddMetricCard(_performancePanel, _totalRevenueCard, IconChar.MoneyBillTrendUp, "Total Fleet Revenue", "₱0.00", "Combined unit revenue", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_performancePanel, _averageRevenueCard, IconChar.ChartLine, "Avg Revenue / Car", "₱0.00", "Across active fleet", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_performancePanel, _topEarningCarCard, IconChar.Trophy, "Top Earning Car", "-", "Highest total paid", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_performancePanel, _mostRentedCarCard, IconChar.Star, "Most Rented Car", "-", "Highest rental frequency", ThemeHelper.Primary);
    }

    private TableLayoutPanel CreatePerformanceTablesLayout()
    {
        TableLayoutPanel grid = new() { Dock = DockStyle.Top, Height = 354, ColumnCount = 3, RowCount = 1, Padding = new Padding(0, 12, 0, 4) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Top 5 Earning", _topEarningGrid), 0, 0);
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Top 5 Frequency", _mostRentedGrid), 1, 0);
        grid.Controls.Add(ReportLayoutHelper.CreateGridCard("Least Utilized", _leastUsedGrid), 2, 0);
        return grid;
    }

    private void LayoutCards()
    {
        ReportLayoutHelper.LayoutMetricCards(_availabilityPanel, [_averageUtilizationCard, _activeRentalsCard, _completedRentalsCard, _maintenanceCard]);
        ReportLayoutHelper.LayoutMetricCards(_performancePanel, [_totalRevenueCard, _averageRevenueCard, _topEarningCarCard, _mostRentedCarCard]);
    }

    private void UpdateSummaryCards(FleetPerformanceMetrics metrics)
    {
        _totalRevenueCard.SetMetric(IconChar.MoneyBillTrendUp, "Total Fleet Revenue", ReportLayoutHelper.FormatPeso(metrics.TotalFleetRevenue), "Combined unit revenue", ThemeHelper.Primary);
        _averageRevenueCard.SetMetric(IconChar.ChartLine, "Avg Revenue / Car", ReportLayoutHelper.FormatPeso(metrics.AverageRevenuePerCar), "Across active fleet", ThemeHelper.Success);
        _topEarningCarCard.SetMetric(IconChar.Trophy, "Top Earning Car", metrics.TopEarningCar ?? "-", $"Revenue: {ReportLayoutHelper.FormatPeso(metrics.TopEarningCarRevenue)}", ThemeHelper.Success);
        _mostRentedCarCard.SetMetric(IconChar.Star, "Most Rented Car", metrics.MostRentedCar ?? "-", $"{metrics.MostRentedCarCount} rental(s)", ThemeHelper.Primary);
        _averageUtilizationCard.SetMetric(IconChar.GaugeHigh, "Avg Utilization", ReportLayoutHelper.FormatPercent(metrics.AverageUtilizationRate), "Days rented in range", ThemeHelper.Warning);
        _activeRentalsCard.SetMetric(IconChar.Key, "Active Rentals", metrics.ActiveRentals.ToString(), "Currently released", ThemeHelper.Success);
        _completedRentalsCard.SetMetric(IconChar.FlagCheckered, "Completed Rentals", metrics.CompletedRentals.ToString(), "Closed in range", ThemeHelper.GrayIcon);
        _maintenanceCard.SetMetric(IconChar.ScrewdriverWrench, "Under Maintenance", metrics.CarsUnderMaintenance.ToString(), "Ongoing/Scheduled", ThemeHelper.Warning);
    }

    private void PopulateFleetUtilization(IReadOnlyList<FleetUtilizationItem> items)
    {
        _utilizationGrid.Columns.Clear(); _utilizationGrid.Rows.Clear();
        _utilizationGrid.Columns.Add("Car", "Car / Plate");
        _utilizationGrid.Columns.Add("RentedDays", "Rented Days");
        _utilizationGrid.Columns.Add("AvailableDays", "Available Days");
        _utilizationGrid.Columns.Add("Utilization", "Utilization Rate");
        _utilizationGrid.Columns.Add("RentalCount", "Rental Count");
        _utilizationGrid.Columns.Add("Status", "Status");
        foreach (FleetUtilizationItem item in items) _utilizationGrid.Rows.Add($"{item.CarName} ({item.PlateNumber})", item.RentedDays, item.AvailableDays, ReportLayoutHelper.FormatPercent(item.UtilizationRate), item.RentalCount, item.Status);
        ReportLayoutHelper.AddEmptyRow(_utilizationGrid);
    }

    private void PopulateFleetRevenue(IReadOnlyList<FleetRevenuePerCarItem> items)
    {
        _revenueGrid.Columns.Clear(); _revenueGrid.Rows.Clear();
        _revenueGrid.Columns.Add("Car", "Car / Plate");
        _revenueGrid.Columns.Add("Rental", "Rental Revenue");
        _revenueGrid.Columns.Add("Extension", "Extension Fees");
        _revenueGrid.Columns.Add("Damage", "Damage Fees");
        _revenueGrid.Columns.Add("Late", "Late Fees");
        _revenueGrid.Columns.Add("Total", "Total Revenue");
        _revenueGrid.Columns.Add("Average", "Avg / Rental");
        foreach (FleetRevenuePerCarItem item in items) _revenueGrid.Rows.Add($"{item.CarName} ({item.PlateNumber})", ReportLayoutHelper.FormatPeso(item.RentalRevenue), ReportLayoutHelper.FormatPeso(item.ExtensionFees), ReportLayoutHelper.FormatPeso(item.DamageFees), ReportLayoutHelper.FormatPeso(item.LateFees), ReportLayoutHelper.FormatPeso(item.TotalRevenue), ReportLayoutHelper.FormatPeso(item.AverageRevenuePerRental));
        ReportLayoutHelper.AddEmptyRow(_revenueGrid);
    }

    private static void PopulateTopCars(DataGridView grid, IReadOnlyList<TopCarItem> items)
    {
        grid.Columns.Clear(); grid.Rows.Clear();
        grid.Columns.Add("Car", "Car");
        grid.Columns.Add("Rentals", "Count");
        grid.Columns.Add("Revenue", "Revenue");
        foreach (var item in items) grid.Rows.Add($"{item.CarName} ({item.PlateNumber})", item.RentalCount, ReportLayoutHelper.FormatPeso(item.Revenue));
        ReportLayoutHelper.AddEmptyRow(grid);
    }

    private void PopulateFleetMaintenance(IReadOnlyList<FleetMaintenanceItem> items)
    {
        _maintenanceGrid.Columns.Clear(); _maintenanceGrid.Rows.Clear();
        _maintenanceGrid.Columns.Add("Car", "Car / Plate");
        _maintenanceGrid.Columns.Add("Schedule", "Schedule");
        _maintenanceGrid.Columns.Add("Dates", "Dates");
        _maintenanceGrid.Columns.Add("Status", "Status");
        foreach (FleetMaintenanceItem item in items) _maintenanceGrid.Rows.Add($"{item.CarName} ({item.PlateNumber})", item.Title, $"{ReportLayoutHelper.FormatDate(item.StartDate)} - {ReportLayoutHelper.FormatDate(item.EndDate)}", item.Status);
        ReportLayoutHelper.AddEmptyRow(_maintenanceGrid);
    }
}