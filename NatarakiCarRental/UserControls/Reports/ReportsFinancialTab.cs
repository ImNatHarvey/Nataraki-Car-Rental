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
            PopulatePaymentMethods(await _reportService.GetPaymentMethodBreakdownAsync(from, to));
            PopulateRevenueCategories(await _reportService.GetRevenueByCategoryAsync(from, to));
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
        TableLayoutPanel layout = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 5 };
        layout.Controls.Add(CreateMetricPanel());

        TableLayoutPanel breakdownLayout = new() { Dock = DockStyle.Top, Height = 354, ColumnCount = 2, Padding = new Padding(0, 12, 0, 4) };
        breakdownLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        breakdownLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        breakdownLayout.Controls.Add(ReportLayoutHelper.CreateGridCard("Payment Method Breakdown", _paymentMethodGrid), 0, 0);
        breakdownLayout.Controls.Add(ReportLayoutHelper.CreateGridCard("Revenue by Category", _revenueCategoryGrid), 1, 0);
        layout.Controls.Add(breakdownLayout);

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
        LayoutCards();
        return _metricPanel;
    }

    private void LayoutCards() => ReportLayoutHelper.LayoutMetricCards(_metricPanel, GetCards());

    private List<Control> GetCards() =>
    [
        _totalRevenueCard, _outstandingCard, _paidTxCard, _partialTxCard,
        _unpaidTxCard, _rentalRevenueCard, _extensionFeesCard, _damageLateFeesCard
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
}
