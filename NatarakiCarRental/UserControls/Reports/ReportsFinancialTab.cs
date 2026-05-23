using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;

namespace NatarakiCarRental.UserControls.Reports;

public sealed class ReportsFinancialTab : UserControl, IReportTab
{
    private readonly ReportService _reportService = new();
    private readonly FlowLayoutPanel _metricPanel = ReportLayoutHelper.CreateMetricPanel();

    private readonly MetricCardControl _totalRevenueCard = new();
    private readonly MetricCardControl _outstandingCard = new();
    private readonly MetricCardControl _paidTxCard = new();
    private readonly MetricCardControl _partialTxCard = new();
    private readonly MetricCardControl _unpaidTxCard = new();
    private readonly MetricCardControl _rentalRevenueCard = new();
    private readonly MetricCardControl _extensionFeesCard = new();
    private readonly MetricCardControl _damageLateFeesCard = new();

    // Phase 7: Profitability
    private readonly MetricCardControl _totalOffsiteCostCard = new();
    private readonly MetricCardControl _netProfitCard = new();
    private readonly MetricCardControl _costRevenueRatioCard = new();
    private readonly MetricCardControl _maintenanceCostCard = new();
    private readonly MetricCardControl _repairCostCard = new();
    private readonly MetricCardControl _cleaningCostCard = new();
    private readonly Label _profitabilityInsightLabel = new();
    private readonly DataGridView _vehicleProfitabilityGrid = ReportLayoutHelper.CreateSummaryGrid();

    private readonly DataGridView _paymentMethodGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _revenueCategoryGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _outstandingGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _carRevenueGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _customerRevenueGrid = ReportLayoutHelper.CreateSummaryGrid();

    public ReportsFinancialTab()
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
            UpdateSummaryCards(await _reportService.GetSummaryMetricsAsync(from, to));
            UpdateProfitabilityCards(await _reportService.GetOperatingProfitabilityAsync(from, to));
            
            PopulatePaymentMethods(await _reportService.GetPaymentMethodBreakdownAsync(from, to));
            PopulateRevenueCategories(await _reportService.GetRevenueByCategoryAsync(from, to));
            PopulateVehicleProfitability(await _reportService.GetVehicleProfitabilityAsync(from, to));
            PopulateOutstandingTransactions(await _reportService.GetOutstandingTransactionsAsync(from, to));
            PopulateCarRevenue(await _reportService.GetRevenueByCarAsync(from, to, 10));
            PopulateCustomerRevenue(await _reportService.GetRevenueByCustomerAsync(from, to, 10));
            LayoutCards();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning($"Unable to load financial reports.\n\n{exception.Message}", "Reports");
        }
    }

    private void InitializeLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 8 };
        layout.Controls.Add(CreateMetricPanel());

        TableLayoutPanel breakdownLayout = new() { Dock = DockStyle.Top, Height = 354, ColumnCount = 2, Padding = new Padding(0, 12, 0, 4) };
        breakdownLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        breakdownLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        breakdownLayout.Controls.Add(ReportLayoutHelper.CreateGridCard("Payment Method Breakdown", _paymentMethodGrid), 0, 0);
        breakdownLayout.Controls.Add(ReportLayoutHelper.CreateGridCard("Revenue by Category", _revenueCategoryGrid), 1, 0);
        layout.Controls.Add(breakdownLayout);

        layout.Controls.Add(new Label { Text = "Operating Profitability Insight", Dock = DockStyle.Top, Height = 32, Padding = new Padding(8, 8, 0, 0), Font = FontHelper.SemiBold(11.5F), ForeColor = ThemeHelper.TextPrimary });
        _profitabilityInsightLabel.Dock = DockStyle.Top;
        _profitabilityInsightLabel.Height = 42;
        _profitabilityInsightLabel.Padding = new Padding(12, 4, 0, 12);
        _profitabilityInsightLabel.Font = FontHelper.Regular(10.5F);
        _profitabilityInsightLabel.ForeColor = ThemeHelper.TextSecondary;
        layout.Controls.Add(_profitabilityInsightLabel);

        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Top Vehicles by Offsite Cost (Profitability Analysis)", _vehicleProfitabilityGrid, 420));
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Outstanding Transactions (Unpaid/Partial)", _outstandingGrid, 340));
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Revenue by Car Performance", _carRevenueGrid, 340));
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Revenue by Customer", _customerRevenueGrid, 340));
        Controls.Add(layout);
    }

    private FlowLayoutPanel CreateMetricPanel()
    {
        ReportLayoutHelper.AddMetricCard(_metricPanel, _totalRevenueCard, IconChar.MoneyBillTrendUp, "Total Revenue", "₱0.00", "Actual payments received", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _outstandingCard, IconChar.HandHoldingDollar, "Total Outstanding", "₱0.00", "Uncollected balance", ThemeHelper.Danger);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _paidTxCard, IconChar.CheckDouble, "Fully Paid", "0", "Settled transactions", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _partialTxCard, IconChar.ScaleUnbalanced, "Partial", "0", "Some payment received", ThemeHelper.Warning);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _unpaidTxCard, IconChar.FileInvoiceDollar, "Unpaid", "0", "No payments yet", ThemeHelper.Danger);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _rentalRevenueCard, IconChar.CarSide, "Rental Revenue", "₱0.00", "Base charges", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _extensionFeesCard, IconChar.CalendarPlus, "Extension Fees", "₱0.00", "Rental extensions", ThemeHelper.Warning);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _damageLateFeesCard, IconChar.CircleExclamation, "Damage/Late Fees", "₱0.00", "Return penalties", ThemeHelper.Danger);

        // Phase 7 cards
        ReportLayoutHelper.AddMetricCard(_metricPanel, _totalOffsiteCostCard, IconChar.Tools, "Total Offsite Cost", "₱0.00", "Maintenance & Repairs", ThemeHelper.Warning);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _netProfitCard, IconChar.HandHoldingHeart, "Net After Offsite", "₱0.00", "Revenue - Offsite Cost", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _costRevenueRatioCard, IconChar.Percent, "Cost-to-Revenue", "0.0%", "Operational efficiency", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _maintenanceCostCard, IconChar.Wrench, "Maintenance Cost", "₱0.00", "Routine servicing", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _repairCostCard, IconChar.Hammer, "Repair Cost", "₱0.00", "Unscheduled fixes", ThemeHelper.Danger);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _cleaningCostCard, IconChar.Soap, "Cleaning Cost", "₱0.00", "Sanitization & detail", ThemeHelper.Primary);

        LayoutCards();
        return _metricPanel;
    }

    private void LayoutCards() => ReportLayoutHelper.LayoutMetricCards(_metricPanel, GetCards());

    private List<Control> GetCards() =>
    [
        _totalRevenueCard, _outstandingCard, _paidTxCard, _partialTxCard,
        _unpaidTxCard, _rentalRevenueCard, _extensionFeesCard, _damageLateFeesCard,
        _totalOffsiteCostCard, _netProfitCard, _costRevenueRatioCard,
        _maintenanceCostCard, _repairCostCard, _cleaningCostCard
    ];

    private void UpdateSummaryCards(ReportSummaryMetrics metrics)
    {
        _totalRevenueCard.SetMetric(IconChar.MoneyBillTrendUp, "Total Revenue", ReportLayoutHelper.FormatPeso(metrics.TotalRevenue), "Actual payments received", ThemeHelper.Primary);
        _outstandingCard.SetMetric(IconChar.HandHoldingDollar, "Total Outstanding", ReportLayoutHelper.FormatPeso(metrics.OutstandingBalance), "Uncollected balance", ThemeHelper.Danger);
        _paidTxCard.SetMetric(IconChar.CheckDouble, "Fully Paid", metrics.PaidTransactions.ToString(), "Settled transactions", ThemeHelper.Success);
        _partialTxCard.SetMetric(IconChar.ScaleUnbalanced, "Partial", metrics.PartialTransactions.ToString(), "Some payment received", ThemeHelper.Warning);
        _unpaidTxCard.SetMetric(IconChar.FileInvoiceDollar, "Unpaid", metrics.UnpaidTransactions.ToString(), "No payments yet", ThemeHelper.Danger);
        _rentalRevenueCard.SetMetric(IconChar.CarSide, "Rental Revenue", ReportLayoutHelper.FormatPeso(metrics.RentalRevenue), "Base charges", ThemeHelper.Success);
        _extensionFeesCard.SetMetric(IconChar.CalendarPlus, "Extension Fees", ReportLayoutHelper.FormatPeso(metrics.ExtensionFees), "Rental extensions", ThemeHelper.Warning);
        _damageLateFeesCard.SetMetric(IconChar.CircleExclamation, "Damage/Late Fees", ReportLayoutHelper.FormatPeso(metrics.DamageFees + metrics.LateReturnFees), "Return penalties", ThemeHelper.Danger);
    }

    private void UpdateProfitabilityCards(OperatingProfitabilitySummary metrics)
    {
        _totalOffsiteCostCard.SetMetric(IconChar.Tools, "Total Offsite Cost", ReportLayoutHelper.FormatPeso(metrics.TotalOffsiteCost), "Maintenance & Repairs", ThemeHelper.Warning);
        _netProfitCard.SetMetric(IconChar.HandHoldingHeart, "Net After Offsite", ReportLayoutHelper.FormatPeso(metrics.NetAfterOffsiteCost), "Revenue - Offsite Cost", ThemeHelper.Success);
        _costRevenueRatioCard.SetMetric(IconChar.Percent, "Cost-to-Revenue", $"{metrics.CostToRevenueRatio:N1}%", "Operational efficiency", ThemeHelper.Primary);
        _maintenanceCostCard.SetMetric(IconChar.Wrench, "Maintenance Cost", ReportLayoutHelper.FormatPeso(metrics.MaintenanceCost), "Routine servicing", ThemeHelper.Success);
        _repairCostCard.SetMetric(IconChar.Hammer, "Repair Cost", ReportLayoutHelper.FormatPeso(metrics.RepairCost), "Unscheduled fixes", ThemeHelper.Danger);
        _cleaningCostCard.SetMetric(IconChar.Soap, "Cleaning Cost", ReportLayoutHelper.FormatPeso(metrics.CleaningCost), "Sanitization & detail", ThemeHelper.Primary);

        if (metrics.TotalRevenue > 0)
        {
            _profitabilityInsightLabel.Text = $"Offsite costs consumed {metrics.CostToRevenueRatio:N1}% of revenue for this period. Net after offsite costs is {ReportLayoutHelper.FormatPeso(metrics.NetAfterOffsiteCost)}.";
        }
        else if (metrics.TotalOffsiteCost > 0)
        {
            _profitabilityInsightLabel.Text = $"No revenue recorded for this period, but {ReportLayoutHelper.FormatPeso(metrics.TotalOffsiteCost)} was spent on offsite operations.";
        }
        else
        {
            _profitabilityInsightLabel.Text = "No financial or offsite activity recorded for the selected period.";
        }
    }

    private void PopulateVehicleProfitability(IReadOnlyList<VehicleCostProfitabilityItem> items)
    {
        _vehicleProfitabilityGrid.Columns.Clear();
        _vehicleProfitabilityGrid.Rows.Clear();
        _vehicleProfitabilityGrid.Columns.Add("Car", "Car / Plate");
        _vehicleProfitabilityGrid.Columns.Add("Maint", "Maint.");
        _vehicleProfitabilityGrid.Columns.Add("Repair", "Repair");
        _vehicleProfitabilityGrid.Columns.Add("Clean", "Clean");
        _vehicleProfitabilityGrid.Columns.Add("TotalCost", "Total Cost");
        _vehicleProfitabilityGrid.Columns.Add("Revenue", "Revenue");
        _vehicleProfitabilityGrid.Columns.Add("Net", "Net Profit");

        if (_vehicleProfitabilityGrid.Columns["Maint"] is DataGridViewColumn c1) c1.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        if (_vehicleProfitabilityGrid.Columns["Repair"] is DataGridViewColumn c2) c2.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        if (_vehicleProfitabilityGrid.Columns["Clean"] is DataGridViewColumn c3) c3.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        foreach (var item in items)
        {
            _vehicleProfitabilityGrid.Rows.Add(
                $"{item.CarDisplayName}\n{item.PlateNumber}",
                item.MaintenanceCount,
                item.RepairCount,
                item.CleaningCount,
                ReportLayoutHelper.FormatPeso(item.TotalOffsiteCost),
                ReportLayoutHelper.FormatPeso(item.RevenueGenerated),
                ReportLayoutHelper.FormatPeso(item.NetAfterCost));
        }
        ReportLayoutHelper.AddEmptyRow(_vehicleProfitabilityGrid);
    }

    private void PopulateOutstandingTransactions(IReadOnlyList<TransactionListItem> items)
    {
        _outstandingGrid.Columns.Clear();
        _outstandingGrid.Rows.Clear();
        _outstandingGrid.Columns.Add("Code", "Code");
        _outstandingGrid.Columns.Add("Customer", "Customer");
        _outstandingGrid.Columns.Add("Car", "Car");
        _outstandingGrid.Columns.Add("Total", "Total");
        _outstandingGrid.Columns.Add("Paid", "Paid");
        _outstandingGrid.Columns.Add("Balance", "Balance");
        _outstandingGrid.Columns.Add("PayStatus", "Payment");
        _outstandingGrid.Columns.Add("TxStatus", "Status");
        foreach (var item in items)
        {
            _outstandingGrid.Rows.Add(item.TransactionCode, item.CustomerName, $"{item.CarName} ({item.PlateNumber})", ReportLayoutHelper.FormatPeso(item.TotalAmount), ReportLayoutHelper.FormatPeso(item.AmountPaid), ReportLayoutHelper.FormatPeso(item.BalanceAmount), item.PaymentStatus, item.TransactionStatus);
        }
        ReportLayoutHelper.AddEmptyRow(_outstandingGrid);
    }

    private void PopulateCarRevenue(IReadOnlyList<TopCarItem> items)
    {
        _carRevenueGrid.Columns.Clear();
        _carRevenueGrid.Rows.Clear();
        _carRevenueGrid.Columns.Add("Car", "Car / Plate");
        _carRevenueGrid.Columns.Add("Rentals", "Count");
        _carRevenueGrid.Columns.Add("Total", "Total Revenue");
        _carRevenueGrid.Columns.Add("Avg", "Average / Rental");
        foreach (var item in items)
        {
            _carRevenueGrid.Rows.Add($"{item.CarName} ({item.PlateNumber})", item.RentalCount, ReportLayoutHelper.FormatPeso(item.Revenue), ReportLayoutHelper.FormatPeso(item.AverageRevenue));
        }
        ReportLayoutHelper.AddEmptyRow(_carRevenueGrid);
    }

    private void PopulateCustomerRevenue(IReadOnlyList<RevenueByCustomerItem> items)
    {
        _customerRevenueGrid.Columns.Clear();
        _customerRevenueGrid.Rows.Clear();
        _customerRevenueGrid.Columns.Add("Customer", "Customer");
        _customerRevenueGrid.Columns.Add("TXs", "Count");
        _customerRevenueGrid.Columns.Add("Paid", "Total Paid");
        _customerRevenueGrid.Columns.Add("Balance", "Outstanding");
        foreach (var item in items)
        {
            _customerRevenueGrid.Rows.Add(item.CustomerName, item.TransactionCount, ReportLayoutHelper.FormatPeso(item.TotalPaid), ReportLayoutHelper.FormatPeso(item.OutstandingBalance));
        }
        ReportLayoutHelper.AddEmptyRow(_customerRevenueGrid);
    }

    private static void PopulatePaymentBreakdown(DataGridView grid, IReadOnlyList<PaymentMethodBreakdownItem> items)
    {
        grid.Columns.Clear();
        grid.Rows.Clear();
        grid.Columns.Add("Method", "Method");
        grid.Columns.Add("Count", "Count");
        grid.Columns.Add("Amount", "Amount");
        grid.Columns.Add("Percent", "%");
        foreach (var item in items)
        {
            grid.Rows.Add(item.ModeOfPayment, item.PaymentCount, ReportLayoutHelper.FormatPeso(item.TotalAmount), ReportLayoutHelper.FormatPercent(item.Percentage));
        }
        ReportLayoutHelper.AddEmptyRow(grid);
    }

    private void PopulatePaymentMethods(IReadOnlyList<PaymentMethodBreakdownItem> items) => PopulatePaymentBreakdown(_paymentMethodGrid, items);

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
}
