using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;

namespace NatarakiCarRental.UserControls.Reports;

public sealed class ReportsFinancialTab : UserControl, IReportTab
{
    private readonly ReportService _reportService = new();
    
    private readonly FlowLayoutPanel _revenueMetricsPanel = ReportLayoutHelper.CreateMetricPanel();
    private readonly FlowLayoutPanel _profitabilityMetricsPanel = ReportLayoutHelper.CreateMetricPanel();
    private readonly FlowLayoutPanel _txStatusMetricsPanel = ReportLayoutHelper.CreateMetricPanel();

    private readonly MetricCardControl _totalRevenueCard = new();
    private readonly MetricCardControl _netProfitCard = new();
    private readonly MetricCardControl _outstandingCard = new();
    private readonly MetricCardControl _paidTxCard = new();
    private readonly MetricCardControl _partialTxCard = new();
    private readonly MetricCardControl _unpaidTxCard = new();
    private readonly MetricCardControl _rentalRevenueCard = new();
    private readonly MetricCardControl _extensionFeesCard = new();
    private readonly MetricCardControl _damageLateFeesCard = new();

    private readonly MetricCardControl _totalOffsiteCostCard = new();
    private readonly MetricCardControl _costRevenueRatioCard = new();
    private readonly MetricCardControl _maintenanceCostCard = new();
    private readonly MetricCardControl _repairCostCard = new();
    private readonly MetricCardControl _cleaningCostCard = new();
    
    private readonly Label _profitabilityInsightLabel = new();
    
    private readonly DataGridView _vehicleProfitabilityGrid = ReportLayoutHelper.CreateSummaryGrid();
    private readonly DataGridView _paymentMethodGrid = ReportLayoutHelper.CreateSummaryGrid();
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
            var summaryTask = _reportService.GetSummaryMetricsAsync(from, to);
            var profitabilityTask = _reportService.GetOperatingProfitabilityAsync(from, to);
            
            await Task.WhenAll(summaryTask, profitabilityTask);

            UpdateSummaryCards(summaryTask.Result, profitabilityTask.Result);
            
            PopulatePaymentMethods(await _reportService.GetPaymentMethodBreakdownAsync(from, to));
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
        TableLayoutPanel layout = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 12 };

        // 1. Revenue Section
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Revenue & Collections"));
        layout.Controls.Add(_revenueMetricsPanel);
        
        TableLayoutPanel breakdownLayout = new() { Dock = DockStyle.Top, Height = 354, ColumnCount = 2, Padding = new Padding(0, 12, 0, 4) };
        breakdownLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        breakdownLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        breakdownLayout.Controls.Add(ReportLayoutHelper.CreateGridCard("Payment Method Breakdown", _paymentMethodGrid), 0, 0);
        layout.Controls.Add(breakdownLayout);

        // 2. Profitability Section
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Operating Profitability"));
        layout.Controls.Add(_profitabilityMetricsPanel);
        
        _profitabilityInsightLabel.Dock = DockStyle.Top;
        _profitabilityInsightLabel.Height = 42;
        _profitabilityInsightLabel.Padding = new Padding(12, 8, 0, 12);
        _profitabilityInsightLabel.Font = FontHelper.Regular(10.5F);
        _profitabilityInsightLabel.ForeColor = ThemeHelper.TextSecondary;
        layout.Controls.Add(_profitabilityInsightLabel);
        
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Vehicle Profitability (Revenue vs Offsite Cost)", _vehicleProfitabilityGrid, 420));

        // 3. Transaction Status Section
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Transaction Settlement Status"));
        layout.Controls.Add(_txStatusMetricsPanel);
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Outstanding Transactions (Unpaid/Partial)", _outstandingGrid, 340));

        // 4. Performance Rankings
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Top Performance Rankings"));
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Top Vehicles by Revenue", _carRevenueGrid, 340));
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Top Customers by Revenue", _customerRevenueGrid, 340));

        InitCards();
        Controls.Add(layout);
    }

    private void InitCards()
    {
        // Revenue Panel
        ReportLayoutHelper.AddMetricCard(_revenueMetricsPanel, _totalRevenueCard, IconChar.MoneyBillTrendUp, "Total Revenue", "₱0.00", "Payments received", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_revenueMetricsPanel, _outstandingCard, IconChar.HandHoldingDollar, "Total Outstanding", "₱0.00", "Uncollected balance", ThemeHelper.Danger);
        ReportLayoutHelper.AddMetricCard(_revenueMetricsPanel, _rentalRevenueCard, IconChar.CarSide, "Rental Revenue", "₱0.00", "Base charges", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_revenueMetricsPanel, _extensionFeesCard, IconChar.CalendarPlus, "Extension Fees", "₱0.00", "Extended days", ThemeHelper.Warning);
        ReportLayoutHelper.AddMetricCard(_revenueMetricsPanel, _damageLateFeesCard, IconChar.CircleExclamation, "Damage/Late Fees", "₱0.00", "Penalties", ThemeHelper.Danger);

        // Profitability Panel
        ReportLayoutHelper.AddMetricCard(_profitabilityMetricsPanel, _netProfitCard, IconChar.HandHoldingHeart, "Net After Offsite", "₱0.00", "Revenue - Offsite", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_profitabilityMetricsPanel, _totalOffsiteCostCard, IconChar.Tools, "Total Offsite Cost", "₱0.00", "Maint. & Repairs", ThemeHelper.Warning);
        ReportLayoutHelper.AddMetricCard(_profitabilityMetricsPanel, _costRevenueRatioCard, IconChar.Percent, "Cost-to-Revenue", "0.0%", "Operational efficiency", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_profitabilityMetricsPanel, _maintenanceCostCard, IconChar.Wrench, "Maintenance Cost", "₱0.00", "Routine servicing", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_profitabilityMetricsPanel, _repairCostCard, IconChar.Hammer, "Repair Cost", "₱0.00", "Unscheduled fixes", ThemeHelper.Danger);

        // Transaction Status Panel
        ReportLayoutHelper.AddMetricCard(_txStatusMetricsPanel, _paidTxCard, IconChar.CheckDouble, "Fully Paid", "0", "Settled transactions", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_txStatusMetricsPanel, _partialTxCard, IconChar.ScaleUnbalanced, "Partial Payments", "0", "Some payment received", ThemeHelper.Warning);
        ReportLayoutHelper.AddMetricCard(_txStatusMetricsPanel, _unpaidTxCard, IconChar.FileInvoiceDollar, "Unpaid Transactions", "0", "No payments yet", ThemeHelper.Danger);
    }

    private void LayoutCards()
    {
        ReportLayoutHelper.LayoutMetricCards(_revenueMetricsPanel, [_totalRevenueCard, _outstandingCard, _rentalRevenueCard, _extensionFeesCard, _damageLateFeesCard]);
        ReportLayoutHelper.LayoutMetricCards(_profitabilityMetricsPanel, [_netProfitCard, _totalOffsiteCostCard, _costRevenueRatioCard, _maintenanceCostCard, _repairCostCard]);
        ReportLayoutHelper.LayoutMetricCards(_txStatusMetricsPanel, [_paidTxCard, _partialTxCard, _unpaidTxCard]);
    }

    private void UpdateSummaryCards(ReportSummaryMetrics summary, OperatingProfitabilitySummary profitability)
    {
        _totalRevenueCard.SetMetric(IconChar.MoneyBillTrendUp, "Total Revenue", ReportLayoutHelper.FormatPeso(summary.TotalRevenue), "Payments received", ThemeHelper.Primary);
        _outstandingCard.SetMetric(IconChar.HandHoldingDollar, "Total Outstanding", ReportLayoutHelper.FormatPeso(summary.OutstandingBalance), "Uncollected balance", ThemeHelper.Danger);
        _rentalRevenueCard.SetMetric(IconChar.CarSide, "Rental Revenue", ReportLayoutHelper.FormatPeso(summary.RentalRevenue), "Base charges", ThemeHelper.Success);
        _extensionFeesCard.SetMetric(IconChar.CalendarPlus, "Extension Fees", ReportLayoutHelper.FormatPeso(summary.ExtensionFees), "Extended days", ThemeHelper.Warning);
        _damageLateFeesCard.SetMetric(IconChar.CircleExclamation, "Damage/Late Fees", ReportLayoutHelper.FormatPeso(summary.DamageFees + summary.LateReturnFees), "Penalties", ThemeHelper.Danger);

        _netProfitCard.SetMetric(IconChar.HandHoldingHeart, "Net After Offsite", ReportLayoutHelper.FormatPeso(profitability.NetAfterOffsiteCost), "Revenue - Offsite", ThemeHelper.Success);
        _totalOffsiteCostCard.SetMetric(IconChar.Tools, "Total Offsite Cost", ReportLayoutHelper.FormatPeso(profitability.TotalOffsiteCost), "Maint. & Repairs", ThemeHelper.Warning);
        _costRevenueRatioCard.SetMetric(IconChar.Percent, "Cost-to-Revenue", $"{profitability.CostToRevenueRatio:N1}%", "Operational efficiency", ThemeHelper.Primary);
        _maintenanceCostCard.SetMetric(IconChar.Wrench, "Maintenance Cost", ReportLayoutHelper.FormatPeso(profitability.MaintenanceCost), "Routine servicing", ThemeHelper.Success);
        _repairCostCard.SetMetric(IconChar.Hammer, "Repair Cost", ReportLayoutHelper.FormatPeso(profitability.RepairCost), "Unscheduled fixes", ThemeHelper.Danger);
        _cleaningCostCard.SetMetric(IconChar.Soap, "Cleaning Cost", ReportLayoutHelper.FormatPeso(profitability.CleaningCost), "Sanitization", ThemeHelper.Primary);

        _paidTxCard.SetMetric(IconChar.CheckDouble, "Fully Paid", summary.PaidTransactions.ToString(), "Settled transactions", ThemeHelper.Success);
        _partialTxCard.SetMetric(IconChar.ScaleUnbalanced, "Partial Payments", summary.PartialTransactions.ToString(), "Some payment received", ThemeHelper.Warning);
        _unpaidTxCard.SetMetric(IconChar.FileInvoiceDollar, "Unpaid Transactions", summary.UnpaidTransactions.ToString(), "No payments yet", ThemeHelper.Danger);

        if (profitability.TotalRevenue > 0)
        {
            _profitabilityInsightLabel.Text = $"Offsite costs consumed {profitability.CostToRevenueRatio:N1}% of revenue for this period. Net after offsite costs is {ReportLayoutHelper.FormatPeso(profitability.NetAfterOffsiteCost)}.";
        }
        else
        {
            _profitabilityInsightLabel.Text = profitability.TotalOffsiteCost > 0 ? $"Spent {ReportLayoutHelper.FormatPeso(profitability.TotalOffsiteCost)} on offsite operations without recorded revenue." : "No financial activity recorded.";
        }
    }

    private void PopulateVehicleProfitability(IReadOnlyList<VehicleCostProfitabilityItem> items)
    {
        _vehicleProfitabilityGrid.Columns.Clear(); _vehicleProfitabilityGrid.Rows.Clear();
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
            _vehicleProfitabilityGrid.Rows.Add($"{item.CarDisplayName}\n{item.PlateNumber}", item.MaintenanceCount, item.RepairCount, item.CleaningCount, ReportLayoutHelper.FormatPeso(item.TotalOffsiteCost), ReportLayoutHelper.FormatPeso(item.RevenueGenerated), ReportLayoutHelper.FormatPeso(item.NetAfterCost));
        }
        ReportLayoutHelper.AddEmptyRow(_vehicleProfitabilityGrid);
    }

    private void PopulateOutstandingTransactions(IReadOnlyList<TransactionListItem> items)
    {
        _outstandingGrid.Columns.Clear(); _outstandingGrid.Rows.Clear();
        _outstandingGrid.Columns.Add("Code", "Code");
        _outstandingGrid.Columns.Add("Customer", "Customer");
        _outstandingGrid.Columns.Add("Car", "Car");
        _outstandingGrid.Columns.Add("Total", "Total");
        _outstandingGrid.Columns.Add("Paid", "Paid");
        _outstandingGrid.Columns.Add("Balance", "Balance");
        _outstandingGrid.Columns.Add("PayStatus", "Payment");
        _outstandingGrid.Columns.Add("TxStatus", "Status");
        foreach (var item in items) _outstandingGrid.Rows.Add(item.TransactionCode, item.CustomerName, $"{item.CarName} ({item.PlateNumber})", ReportLayoutHelper.FormatPeso(item.TotalAmount), ReportLayoutHelper.FormatPeso(item.AmountPaid), ReportLayoutHelper.FormatPeso(item.BalanceAmount), item.PaymentStatus, item.TransactionStatus);
        ReportLayoutHelper.AddEmptyRow(_outstandingGrid);
    }

    private void PopulateCarRevenue(IReadOnlyList<TopCarItem> items)
    {
        _carRevenueGrid.Columns.Clear(); _carRevenueGrid.Rows.Clear();
        _carRevenueGrid.Columns.Add("Car", "Car / Plate");
        _carRevenueGrid.Columns.Add("Rentals", "Count");
        _carRevenueGrid.Columns.Add("Total", "Total Revenue");
        _carRevenueGrid.Columns.Add("Avg", "Average / Rental");
        foreach (var item in items) _carRevenueGrid.Rows.Add($"{item.CarName} ({item.PlateNumber})", item.RentalCount, ReportLayoutHelper.FormatPeso(item.Revenue), ReportLayoutHelper.FormatPeso(item.AverageRevenue));
        ReportLayoutHelper.AddEmptyRow(_carRevenueGrid);
    }

    private void PopulateCustomerRevenue(IReadOnlyList<RevenueByCustomerItem> items)
    {
        _customerRevenueGrid.Columns.Clear(); _customerRevenueGrid.Rows.Clear();
        _customerRevenueGrid.Columns.Add("Customer", "Customer");
        _customerRevenueGrid.Columns.Add("TXs", "Count");
        _customerRevenueGrid.Columns.Add("Paid", "Total Paid");
        _customerRevenueGrid.Columns.Add("Balance", "Outstanding");
        foreach (var item in items) _customerRevenueGrid.Rows.Add(item.CustomerName, item.TransactionCount, ReportLayoutHelper.FormatPeso(item.TotalPaid), ReportLayoutHelper.FormatPeso(item.OutstandingBalance));
        ReportLayoutHelper.AddEmptyRow(_customerRevenueGrid);
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
}