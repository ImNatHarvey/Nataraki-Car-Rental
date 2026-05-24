using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Services;

public sealed class ReportExportService
{
    private const int PdfDetailLimit = 12;
    private const int PdfHeaderLineCount = 7;
    private readonly ReportService _reportService;

    public ReportExportService() : this(new ReportService())
    {
    }

    public ReportExportService(ReportService reportService)
    {
        _reportService = reportService;
    }

    public async Task ExportFinancialPdfAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        AccessControlService.EnforcePermission("Reports.Export");
        FinancialExportData data = await LoadFinancialDataAsync(from, to);
        List<string> lines = CreateHeader("Financial Reports", from, to, generatedBy);
        AddFinancialSummary(lines, data.Summary);
        AddProfitabilitySummary(lines, data.Profitability);
        AddVehicleProfitability(lines, data.VehicleProfitability, PdfDetailLimit);
        AddPaymentMethods(lines, data.PaymentMethods, PdfDetailLimit);
        AddPaymentCategories(lines, data.PaymentCategories, PdfDetailLimit);
        AddOutstandingTransactions(lines, data.OutstandingTransactions, PdfDetailLimit);
        WritePdf(path, lines);
    }

    public async Task ExportFinancialExcelAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        AccessControlService.EnforcePermission("Reports.Export");
        FinancialExportData data = await LoadFinancialDataAsync(from, to);
        WriteXlsx(path,
        [
            CreateFinancialSummarySheet(data.Summary, from, to, generatedBy),
            CreateProfitabilitySummarySheet(data.Profitability, from, to, generatedBy),
            CreatePaymentMethodsSheet(data.PaymentMethods),
            CreatePaymentCategoriesSheet(data.PaymentCategories),
            CreateVehicleProfitabilitySheet(data.VehicleProfitability),
            CreateOutstandingTransactionsSheet(data.OutstandingTransactions),
            CreateRevenueByCarSheet(data.RevenueByCar),
            CreateRevenueByCustomerSheet(data.RevenueByCustomer)
        ]);
    }

    public async Task ExportFleetPdfAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        AccessControlService.EnforcePermission("Reports.Export");
        FleetExportData data = await LoadFleetDataAsync(from, to);
        List<string> lines = CreateHeader("Fleet Performance Reports", from, to, generatedBy);
        AddFleetSummary(lines, data.Metrics);
        AddTopCars(lines, "Top Earning Cars", data.TopEarningCars, PdfDetailLimit);
        AddFleetUtilization(lines, data.Utilization, PdfDetailLimit);
        AddMaintenance(lines, data.Maintenance, PdfDetailLimit);
        WritePdf(path, lines);
    }

    public async Task ExportFleetExcelAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        AccessControlService.EnforcePermission("Reports.Export");
        FleetExportData data = await LoadFleetDataAsync(from, to);
        WriteXlsx(path,
        [
            CreateFleetSummarySheet(data.Metrics, from, to, generatedBy),
            CreateFleetUtilizationSheet(data.Utilization),
            CreateFleetRevenueSheet(data.RevenuePerCar),
            CreateTopCarsSheet("Top Earning Cars", data.TopEarningCars),
            CreateTopCarsSheet("Most Rented Cars", data.MostRentedCars),
            CreateTopCarsSheet("Least Used Cars", data.LeastUsedCars),
            CreateFleetMaintenanceSheet("Maintenance Visibility", data.Maintenance)
        ]);
    }

    public async Task ExportOperationsPdfAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        AccessControlService.EnforcePermission("Reports.Export");
        OperationsExportData data = await LoadOperationsDataAsync(from, to);
        List<string> lines = CreateHeader("Operations Reports", from, to, generatedBy);
        AddOperationsSummary(lines, data.Metrics);
        AddUpcomingReturns(lines, data.UpcomingReturns, PdfDetailLimit);
        AddLateReturns(lines, data.LateReturns, PdfDetailLimit);
        AddActiveRentals(lines, data.ActiveRentals, PdfDetailLimit);
        AddUpcomingReservations(lines, data.UpcomingReservations, PdfDetailLimit);
        AddAvailableCars(lines, data.AvailableCars, PdfDetailLimit);
        WritePdf(path, lines);
    }

    public async Task ExportOperationsExcelAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        AccessControlService.EnforcePermission("Reports.Export");
        OperationsExportData data = await LoadOperationsDataAsync(from, to);
        WriteXlsx(path,
        [
            CreateOperationsSummarySheet(data.Metrics, from, to, generatedBy),
            CreateUpcomingReturnsSheet(data.UpcomingReturns),
            CreateLateReturnsSheet(data.LateReturns),
            CreateActiveRentalsSheet(data.ActiveRentals),
            CreateUpcomingReservationsSheet(data.UpcomingReservations),
            CreateOperationsMaintenanceSheet(data.Maintenance),
            CreateAvailableCarsSheet(data.AvailableCars)
        ]);
    }

    public async Task ExportCustomerPdfAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        AccessControlService.EnforcePermission("Reports.Export");
        CustomerExportData data = await LoadCustomerDataAsync(from, to);
        List<string> lines = CreateHeader("Customer Reports", from, to, generatedBy);
        AddCustomerSummary(lines, data.Metrics);
        AddCustomerRevenue(lines, data.TopRevenue, PdfDetailLimit);
        AddCustomerRentals(lines, data.TopRentals, PdfDetailLimit);
        AddCustomerOutstanding(lines, data.OutstandingBalances, PdfDetailLimit);
        AddBlacklistedCustomers(lines, data.BlacklistedCustomers, PdfDetailLimit);
        WritePdf(path, lines);
    }

    public async Task ExportCustomerExcelAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        AccessControlService.EnforcePermission("Reports.Export");
        CustomerExportData data = await LoadCustomerDataAsync(from, to);
        WriteXlsx(path,
        [
            CreateCustomerSummarySheet(data.Metrics, from, to, generatedBy),
            CreateCustomerRevenueSheet(data.TopRevenue),
            CreateCustomerRentalCountSheet(data.TopRentals),
            CreateCustomerOutstandingSheet(data.OutstandingBalances),
            CreateCustomerLateReturnsSheet(data.LateReturns),
            CreateCustomerDamageFeesSheet(data.DamageFees),
            CreateBlacklistedCustomersSheet(data.BlacklistedCustomers)
        ]);
    }

    public async Task ExportFullPdfAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        AccessControlService.EnforcePermission("Reports.Export");
        ReportSummaryMetrics overview = await _reportService.GetSummaryMetricsAsync(from, to);
        FinancialExportData financial = await LoadFinancialDataAsync(from, to);
        FleetExportData fleet = await LoadFleetDataAsync(from, to);
        OperationsExportData operations = await LoadOperationsDataAsync(from, to);
        CustomerExportData customers = await LoadCustomerDataAsync(from, to);

        List<string> lines = CreateHeader("Full Reports Bundle", from, to, generatedBy);
        AddOverviewSummary(lines, overview);
        AddFinancialSummary(lines, financial.Summary);
        AddProfitabilitySummary(lines, financial.Profitability);
        AddVehicleProfitability(lines, financial.VehicleProfitability, PdfDetailLimit);
        AddFleetSummary(lines, fleet.Metrics);
        AddOperationsSummary(lines, operations.Metrics);
        AddCustomerSummary(lines, customers.Metrics);
        lines.Add("Detailed records are available in the Excel full reports bundle.");
        WritePdf(path, lines);
    }

    public async Task ExportFullExcelAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        AccessControlService.EnforcePermission("Reports.Export");
        ReportSummaryMetrics overview = await _reportService.GetSummaryMetricsAsync(from, to);
        FinancialExportData financial = await LoadFinancialDataAsync(from, to);
        FleetExportData fleet = await LoadFleetDataAsync(from, to);
        OperationsExportData operations = await LoadOperationsDataAsync(from, to);
        CustomerExportData customers = await LoadCustomerDataAsync(from, to);

        WriteXlsx(path,
        [
            CreateOverviewSummarySheet(overview, from, to, generatedBy),
            CreateFinancialSummarySheet(financial.Summary, from, to, generatedBy),
            CreateProfitabilitySummarySheet(financial.Profitability, from, to, generatedBy),
            CreatePaymentMethodsSheet(financial.PaymentMethods),
            CreatePaymentCategoriesSheet(financial.PaymentCategories),
            CreateVehicleProfitabilitySheet(financial.VehicleProfitability),
            CreateOutstandingTransactionsSheet(financial.OutstandingTransactions),
            CreateFleetUtilizationSheet(fleet.Utilization),
            CreateFleetRevenueSheet(fleet.RevenuePerCar),
            CreateOperationsSummarySheet(operations.Metrics, from, to, generatedBy),
            CreateUpcomingReturnsSheet(operations.UpcomingReturns),
            CreateLateReturnsSheet(operations.LateReturns),
            CreateCustomerSummarySheet(customers.Metrics, from, to, generatedBy),
            CreateCustomerRevenueSheet(customers.TopRevenue),
            CreateBlacklistedCustomersSheet(customers.BlacklistedCustomers)
        ]);
    }

    private async Task<FinancialExportData> LoadFinancialDataAsync(DateTime from, DateTime to)
    {
        ReportSummaryMetrics summary = await _reportService.GetSummaryMetricsAsync(from, to);
        OperatingProfitabilitySummary profitability = await _reportService.GetOperatingProfitabilityAsync(from, to);
        IReadOnlyList<PaymentMethodBreakdownItem> paymentMethods = await _reportService.GetPaymentMethodBreakdownAsync(from, to);
        IReadOnlyList<RevenueByCategoryItem> paymentCategories = await _reportService.GetRevenueByCategoryAsync(from, to);
        IReadOnlyList<VehicleCostProfitabilityItem> vehicleProfitability = await _reportService.GetVehicleProfitabilityAsync(from, to);
        IReadOnlyList<TransactionListItem> outstanding = await _reportService.GetOutstandingTransactionsAsync(from, to);
        IReadOnlyList<TopCarItem> revenueByCar = await _reportService.GetRevenueByCarAsync(from, to, 50);
        IReadOnlyList<RevenueByCustomerItem> revenueByCustomer = await _reportService.GetRevenueByCustomerAsync(from, to, 50);
        return new FinancialExportData(summary, profitability, paymentMethods, paymentCategories, vehicleProfitability, outstanding, revenueByCar, revenueByCustomer);
    }

    private async Task<FleetExportData> LoadFleetDataAsync(DateTime from, DateTime to)
    {
        FleetPerformanceMetrics metrics = await _reportService.GetFleetPerformanceMetricsAsync(from, to);
        IReadOnlyList<FleetUtilizationItem> utilization = await _reportService.GetFleetUtilizationAsync(from, to);
        IReadOnlyList<FleetRevenuePerCarItem> revenue = await _reportService.GetFleetRevenuePerCarAsync(from, to);
        IReadOnlyList<TopCarItem> topEarning = await _reportService.GetTopCarsByRevenueAsync(from, to, 5);
        IReadOnlyList<TopCarItem> mostRented = await _reportService.GetMostRentedCarsAsync(from, to, 5);
        IReadOnlyList<TopCarItem> leastUsed = await _reportService.GetLeastUsedCarsAsync(from, to, 5);
        IReadOnlyList<FleetMaintenanceItem> maintenance = await _reportService.GetCarsUnderMaintenanceAsync(from, to);
        return new FleetExportData(metrics, utilization, revenue, topEarning, mostRented, leastUsed, maintenance);
    }

    private async Task<OperationsExportData> LoadOperationsDataAsync(DateTime from, DateTime to)
    {
        OperationsMetrics metrics = await _reportService.GetOperationsMetricsAsync(from, to);
        IReadOnlyList<OperationsReturnItem> upcomingReturns = await _reportService.GetUpcomingReturnsAsync(from, to);
        IReadOnlyList<OperationsReturnItem> lateReturns = await _reportService.GetLateReturnsAsync(DateTime.Today);
        IReadOnlyList<OperationsActiveRentalItem> activeRentals = await _reportService.GetActiveRentalsReportAsync(from, to);
        IReadOnlyList<OperationsReservationItem> reservations = await _reportService.GetUpcomingReservationsAsync(from, to);
        IReadOnlyList<OperationsMaintenanceItem> maintenance = await _reportService.GetMaintenanceVisibilityAsync(from, to);
        IReadOnlyList<OperationsAvailableCarItem> availableCars = await _reportService.GetAvailableCarsReportAsync(from, to);
        return new OperationsExportData(metrics, upcomingReturns, lateReturns, activeRentals, reservations, maintenance, availableCars);
    }

    private async Task<CustomerExportData> LoadCustomerDataAsync(DateTime from, DateTime to)
    {
        CustomerAnalyticsMetrics metrics = await _reportService.GetCustomerAnalyticsMetricsAsync(from, to);
        IReadOnlyList<CustomerRevenueReportItem> topRevenue = await _reportService.GetTopCustomersByRevenueAsync(from, to, 50);
        IReadOnlyList<CustomerRentalCountReportItem> topRentals = await _reportService.GetTopCustomersByRentalCountAsync(from, to, 50);
        IReadOnlyList<CustomerOutstandingBalanceReportItem> outstanding = await _reportService.GetCustomersWithOutstandingBalancesAsync(from, to);
        IReadOnlyList<CustomerLateReturnReportItem> lateReturns = await _reportService.GetCustomersWithLateReturnsAsync(DateTime.Today);
        IReadOnlyList<CustomerDamageFeeReportItem> damageFees = await _reportService.GetCustomersWithDamageFeesAsync(from, to);
        IReadOnlyList<BlacklistedCustomerReportItem> blacklisted = await _reportService.GetBlacklistedCustomersReportAsync(from, to);
        return new CustomerExportData(metrics, topRevenue, topRentals, outstanding, lateReturns, damageFees, blacklisted);
    }

    private static ExcelSheet CreateOverviewSummarySheet(ReportSummaryMetrics metrics, DateTime from, DateTime to, string generatedBy)
    {
        return CreateSummarySheet("Overview Summary", from, to, generatedBy,
        [
            ("Total Revenue", FormatPeso(metrics.TotalRevenue)),
            ("Rental Revenue", FormatPeso(metrics.RentalRevenue)),
            ("Extension Fees", FormatPeso(metrics.ExtensionFees)),
            ("Damage Fees", FormatPeso(metrics.DamageFees)),
            ("Late Return Fees", FormatPeso(metrics.LateReturnFees)),
            ("Paid Transactions", metrics.PaidTransactions.ToString(CultureInfo.InvariantCulture)),
            ("Partial Transactions", metrics.PartialTransactions.ToString(CultureInfo.InvariantCulture)),
            ("Unpaid Transactions", metrics.UnpaidTransactions.ToString(CultureInfo.InvariantCulture)),
            ("Active Rentals", metrics.ActiveRentals.ToString(CultureInfo.InvariantCulture)),
            ("Completed Rentals", metrics.CompletedRentals.ToString(CultureInfo.InvariantCulture)),
            ("Top Earning Car", metrics.TopEarningCar ?? "-"),
            ("Most Rented Car", metrics.MostRentedCar ?? "-")
        ]);
    }

    private static ExcelSheet CreateFinancialSummarySheet(ReportSummaryMetrics metrics, DateTime from, DateTime to, string generatedBy)
    {
        return CreateSummarySheet("Financial Summary", from, to, generatedBy,
        [
            ("Total Revenue", FormatPeso(metrics.TotalRevenue)),
            ("Rental Revenue", FormatPeso(metrics.RentalRevenue)),
            ("Extension Fees", FormatPeso(metrics.ExtensionFees)),
            ("Damage Fees", FormatPeso(metrics.DamageFees)),
            ("Late Fees", FormatPeso(metrics.LateReturnFees)),
            ("Outstanding Balance", FormatPeso(metrics.OutstandingBalance)),
            ("Paid Transactions", metrics.PaidTransactions.ToString(CultureInfo.InvariantCulture)),
            ("Partial Transactions", metrics.PartialTransactions.ToString(CultureInfo.InvariantCulture)),
            ("Unpaid Transactions", metrics.UnpaidTransactions.ToString(CultureInfo.InvariantCulture))
        ]);
    }

    private static ExcelSheet CreateFleetSummarySheet(FleetPerformanceMetrics metrics, DateTime from, DateTime to, string generatedBy)
    {
        return CreateSummarySheet("Fleet Summary", from, to, generatedBy,
        [
            ("Total Fleet Revenue", FormatPeso(metrics.TotalFleetRevenue)),
            ("Average Revenue Per Car", FormatPeso(metrics.AverageRevenuePerCar)),
            ("Top Earning Car", metrics.TopEarningCar ?? "-"),
            ("Top Earning Car Revenue", FormatPeso(metrics.TopEarningCarRevenue)),
            ("Most Rented Car", metrics.MostRentedCar ?? "-"),
            ("Most Rented Car Count", metrics.MostRentedCarCount.ToString(CultureInfo.InvariantCulture)),
            ("Average Utilization Rate", FormatPercent(metrics.AverageUtilizationRate)),
            ("Active Rentals", metrics.ActiveRentals.ToString(CultureInfo.InvariantCulture)),
            ("Completed Rentals", metrics.CompletedRentals.ToString(CultureInfo.InvariantCulture)),
            ("Cars Under Maintenance", metrics.CarsUnderMaintenance.ToString(CultureInfo.InvariantCulture))
        ]);
    }

    private static ExcelSheet CreateOperationsSummarySheet(OperationsMetrics metrics, DateTime from, DateTime to, string generatedBy)
    {
        return CreateSummarySheet("Operations Summary", from, to, generatedBy,
        [
            ("Upcoming Returns", metrics.UpcomingReturns.ToString(CultureInfo.InvariantCulture)),
            ("Late Returns", metrics.LateReturns.ToString(CultureInfo.InvariantCulture)),
            ("Active Rentals", metrics.ActiveRentals.ToString(CultureInfo.InvariantCulture)),
            ("Upcoming Reservations", metrics.UpcomingReservations.ToString(CultureInfo.InvariantCulture)),
            ("Reserved Cars", metrics.ReservedCars.ToString(CultureInfo.InvariantCulture)),
            ("Cars Under Maintenance", metrics.CarsUnderMaintenance.ToString(CultureInfo.InvariantCulture)),
            ("Available Cars", metrics.AvailableCars.ToString(CultureInfo.InvariantCulture)),
            ("Completed Returns", metrics.CompletedReturns.ToString(CultureInfo.InvariantCulture))
        ]);
    }

    private static ExcelSheet CreateCustomerSummarySheet(CustomerAnalyticsMetrics metrics, DateTime from, DateTime to, string generatedBy)
    {
        return CreateSummarySheet("Customer Summary", from, to, generatedBy,
        [
            ("Total Active Customers", metrics.TotalActiveCustomers.ToString(CultureInfo.InvariantCulture)),
            ("New Customers", metrics.NewCustomers.ToString(CultureInfo.InvariantCulture)),
            ("Top Customer by Revenue", metrics.TopCustomerByRevenue ?? "-"),
            ("Top Customer Revenue", FormatPeso(metrics.TopCustomerRevenue)),
            ("Top Customer by Rentals", metrics.TopCustomerByRentals ?? "-"),
            ("Top Customer Rental Count", metrics.TopCustomerRentalCount.ToString(CultureInfo.InvariantCulture)),
            ("Blacklisted Customers", metrics.BlacklistedCustomers.ToString(CultureInfo.InvariantCulture)),
            ("Customers with Late Returns", metrics.CustomersWithLateReturns.ToString(CultureInfo.InvariantCulture)),
            ("Customers with Damage Fees", metrics.CustomersWithDamageFees.ToString(CultureInfo.InvariantCulture)),
            ("Average Revenue per Customer", FormatPeso(metrics.AverageRevenuePerCustomer))
        ]);
    }

    private static ExcelSheet CreateSummarySheet(string name, DateTime from, DateTime to, string generatedBy, IEnumerable<(string Metric, string Value)> metrics)
    {
        SystemSettingsModel settings = NatarakiCarRental.Helpers.AppBrandingManager.CurrentSettings;
        List<IReadOnlyList<object?>> rows =
        [
            [settings.ReportHeaderName, string.Empty]
        ];

        string contactLine = CreateBusinessContactLine(settings);
        if (!string.IsNullOrWhiteSpace(contactLine))
        {
            rows.Add([contactLine, string.Empty]);
        }

        rows.AddRange(
        [
            ["Date Range", $"{FormatDate(from)} - {FormatDate(to)}"],
            ["Generated", DateTime.Now.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)],
            ["Generated By", generatedBy],
            [string.Empty, string.Empty],
            ["Metric", "Value"]
        ]);

        rows.AddRange(metrics.Select(metric => (IReadOnlyList<object?>)[metric.Metric, metric.Value]));
        return new ExcelSheet(name, ["Field", "Value"], rows, FirstRowIsHeader: false);
    }

    private static ExcelSheet CreatePaymentMethodsSheet(IReadOnlyList<PaymentMethodBreakdownItem> items) =>
        new("Payment Methods", ["Method", "Count", "Amount", "Percent"],
            items.Select(item => (IReadOnlyList<object?>)[item.ModeOfPayment, item.PaymentCount, FormatPeso(item.TotalAmount), FormatPercent(item.Percentage)]).ToList());

    private static ExcelSheet CreatePaymentCategoriesSheet(IReadOnlyList<RevenueByCategoryItem> items) =>
        new("Payment Categories", ["Category", "Count", "Amount", "Percent"],
            items.Select(item => (IReadOnlyList<object?>)[item.PaymentCategory, item.PaymentCount, FormatPeso(item.TotalAmount), FormatPercent(item.Percentage)]).ToList());

    private static ExcelSheet CreateOutstandingTransactionsSheet(IReadOnlyList<TransactionListItem> items) =>
        new("Outstanding Transactions", ["Code", "Customer", "Car / Plate", "Total", "Paid", "Balance", "Payment", "Status"],
            items.Select(item => (IReadOnlyList<object?>)[item.TransactionCode, item.CustomerName, CarPlate(item.CarName, item.PlateNumber), FormatPeso(item.TotalAmount), FormatPeso(item.AmountPaid), FormatPeso(item.BalanceAmount), item.PaymentStatus, item.TransactionStatus]).ToList());

    private static ExcelSheet CreateRevenueByCarSheet(IReadOnlyList<TopCarItem> items) =>
        new("Revenue by Car", ["Car / Plate", "Rental Count", "Total Revenue", "Average / Rental"],
            items.Select(item => (IReadOnlyList<object?>)[CarPlate(item.CarName, item.PlateNumber), item.RentalCount, FormatPeso(item.Revenue), FormatPeso(item.AverageRevenue)]).ToList());

    private static ExcelSheet CreateRevenueByCustomerSheet(IReadOnlyList<RevenueByCustomerItem> items) =>
        new("Revenue by Customer", ["Customer", "Transaction Count", "Total Paid", "Outstanding Balance"],
            items.Select(item => (IReadOnlyList<object?>)[item.CustomerName, item.TransactionCount, FormatPeso(item.TotalPaid), FormatPeso(item.OutstandingBalance)]).ToList());

    private static ExcelSheet CreateFleetUtilizationSheet(IReadOnlyList<FleetUtilizationItem> items) =>
        new("Fleet Utilization", ["Car / Plate", "Rented Days", "Available Days", "Utilization Rate", "Rental Count", "Status"],
            items.Select(item => (IReadOnlyList<object?>)[CarPlate(item.CarName, item.PlateNumber), item.RentedDays, item.AvailableDays, FormatPercent(item.UtilizationRate), item.RentalCount, item.Status]).ToList());

    private static ExcelSheet CreateFleetRevenueSheet(IReadOnlyList<FleetRevenuePerCarItem> items) =>
        new("Revenue Per Unit", ["Car / Plate", "Rental Revenue", "Extension Fees", "Damage Fees", "Late Fees", "Total Revenue", "Average / Rental"],
            items.Select(item => (IReadOnlyList<object?>)[CarPlate(item.CarName, item.PlateNumber), FormatPeso(item.RentalRevenue), FormatPeso(item.ExtensionFees), FormatPeso(item.DamageFees), FormatPeso(item.LateFees), FormatPeso(item.TotalRevenue), FormatPeso(item.AverageRevenuePerRental)]).ToList());

    private static ExcelSheet CreateTopCarsSheet(string name, IReadOnlyList<TopCarItem> items) =>
        new(name, ["Car / Plate", "Rental Count", "Revenue", "Average Revenue"],
            items.Select(item => (IReadOnlyList<object?>)[CarPlate(item.CarName, item.PlateNumber), item.RentalCount, FormatPeso(item.Revenue), FormatPeso(item.AverageRevenue)]).ToList());

    private static ExcelSheet CreateFleetMaintenanceSheet(string name, IReadOnlyList<FleetMaintenanceItem> items) =>
        new(name, ["Car / Plate", "Schedule", "Start Date", "End Date", "Status"],
            items.Select(item => (IReadOnlyList<object?>)[CarPlate(item.CarName, item.PlateNumber), item.Title, FormatDate(item.StartDate), FormatDate(item.EndDate), item.Status]).ToList());

    private static ExcelSheet CreateUpcomingReturnsSheet(IReadOnlyList<OperationsReturnItem> items) =>
        new("Upcoming Returns", ["Expected Return", "Transaction Code", "Customer", "Contact", "Car / Plate", "Payment Status"],
            items.Select(item => (IReadOnlyList<object?>)[FormatDate(item.ExpectedReturn), item.TransactionCode, item.CustomerName, item.Contact, CarPlate(item.CarName, item.PlateNumber), item.PaymentStatus]).ToList());

    private static ExcelSheet CreateLateReturnsSheet(IReadOnlyList<OperationsReturnItem> items) =>
        new("Late Returns", ["Expected Return", "Days Late", "Estimated Late Fee", "Transaction Code", "Customer", "Contact", "Car / Plate"],
            items.Select(item => (IReadOnlyList<object?>)[FormatDate(item.ExpectedReturn), item.DaysLate, FormatPeso(item.EstimatedLateFee), item.TransactionCode, item.CustomerName, item.Contact, CarPlate(item.CarName, item.PlateNumber)]).ToList());

    private static ExcelSheet CreateActiveRentalsSheet(IReadOnlyList<OperationsActiveRentalItem> items) =>
        new("Active Rentals", ["Transaction Code", "Customer", "Contact", "Car / Plate", "Start Date", "End Date", "Payment Status"],
            items.Select(item => (IReadOnlyList<object?>)[item.TransactionCode, item.CustomerName, item.Contact, CarPlate(item.CarName, item.PlateNumber), FormatDate(item.StartDate), FormatDate(item.EndDate), item.PaymentStatus]).ToList());

    private static ExcelSheet CreateUpcomingReservationsSheet(IReadOnlyList<OperationsReservationItem> items) =>
        new("Upcoming Reservations", ["Schedule Date", "Customer", "Contact", "Car / Plate", "Status", "Payment Status"],
            items.Select(item => (IReadOnlyList<object?>)[FormatDate(item.ScheduleDate), item.CustomerName, item.Contact, CarPlate(item.CarName, item.PlateNumber), item.Status, item.PaymentStatus]).ToList());

    private static ExcelSheet CreateOperationsMaintenanceSheet(IReadOnlyList<OperationsMaintenanceItem> items) =>
        new("Maintenance Visibility", ["Date Range", "Car / Plate", "Status", "Source"],
            items.Select(item => (IReadOnlyList<object?>)[$"{FormatDate(item.StartDate)} - {FormatDate(item.EndDate)}", CarPlate(item.CarName, item.PlateNumber), item.Status, item.Source]).ToList());

    private static ExcelSheet CreateAvailableCarsSheet(IReadOnlyList<OperationsAvailableCarItem> items) =>
        new("Available Cars", ["Car / Plate", "Status", "Rate Per Day", "Seating Capacity"],
            items.Select(item => (IReadOnlyList<object?>)[CarPlate(item.CarName, item.PlateNumber), item.Status, FormatPeso(item.RatePerDay), item.SeatingCapacity?.ToString(CultureInfo.InvariantCulture) ?? "-"]).ToList());

    private static ExcelSheet CreateCustomerRevenueSheet(IReadOnlyList<CustomerRevenueReportItem> items) =>
        new("Top Customers", ["Customer", "Contact", "Transaction Count", "Total Paid", "Outstanding Balance"],
            items.Select(item => (IReadOnlyList<object?>)[item.CustomerName, item.Contact, item.TransactionCount, FormatPeso(item.TotalPaid), FormatPeso(item.OutstandingBalance)]).ToList());

    private static ExcelSheet CreateCustomerRentalCountSheet(IReadOnlyList<CustomerRentalCountReportItem> items) =>
        new("Rental Count", ["Customer", "Contact", "Rental Count", "Completed Rentals", "Active Rentals", "Last Rental Date"],
            items.Select(item => (IReadOnlyList<object?>)[item.CustomerName, item.Contact, item.RentalCount, item.CompletedRentals, item.ActiveRentals, item.LastRentalDate.HasValue ? FormatDate(item.LastRentalDate.Value) : "-"]).ToList());

    private static ExcelSheet CreateCustomerOutstandingSheet(IReadOnlyList<CustomerOutstandingBalanceReportItem> items) =>
        new("Outstanding Balances", ["Customer", "Contact", "Transaction Code", "Total Amount", "Amount Paid", "Balance", "Payment Status"],
            items.Select(item => (IReadOnlyList<object?>)[item.CustomerName, item.Contact, item.TransactionCode, FormatPeso(item.TotalAmount), FormatPeso(item.AmountPaid), FormatPeso(item.Balance), item.PaymentStatus]).ToList());

    private static ExcelSheet CreateCustomerLateReturnsSheet(IReadOnlyList<CustomerLateReturnReportItem> items) =>
        new("Late Return Customers", ["Customer", "Contact", "Transaction Code", "Car / Plate", "Days Late", "Estimated Late Fee"],
            items.Select(item => (IReadOnlyList<object?>)[item.CustomerName, item.Contact, item.TransactionCode, CarPlate(item.CarName, item.PlateNumber), item.DaysLate, FormatPeso(item.EstimatedLateFee)]).ToList());

    private static ExcelSheet CreateCustomerDamageFeesSheet(IReadOnlyList<CustomerDamageFeeReportItem> items) =>
        new("Damage Fee Customers", ["Customer", "Contact", "Transaction Code", "Car / Plate", "Damage Fee", "Payment Date"],
            items.Select(item => (IReadOnlyList<object?>)[item.CustomerName, item.Contact, item.TransactionCode, CarPlate(item.CarName, item.PlateNumber), FormatPeso(item.DamageFee), FormatDate(item.PaymentDate)]).ToList());

    private static ExcelSheet CreateBlacklistedCustomersSheet(IReadOnlyList<BlacklistedCustomerReportItem> items) =>
        new("Blacklisted Customers", ["Customer", "Contact", "Blacklist Reason", "Status", "Last Transaction"],
            items.Select(item => (IReadOnlyList<object?>)[item.CustomerName, item.Contact, item.BlacklistReason, item.Status, item.LastTransaction]).ToList());

    private static void AddProfitabilitySummary(List<string> lines, OperatingProfitabilitySummary metrics)
    {
        AddSection(lines, "Operating Profitability");
        lines.Add($"Total Revenue: {FormatPdfPeso(metrics.TotalRevenue)}");
        lines.Add($"Total Offsite Cost: {FormatPdfPeso(metrics.TotalOffsiteCost)}");
        lines.Add($"Net After Offsite: {FormatPdfPeso(metrics.NetAfterOffsiteCost)}");
        lines.Add($"Cost-to-Revenue Ratio: {FormatPercent(metrics.CostToRevenueRatio)}");
        lines.Add($"Maintenance Cost: {FormatPdfPeso(metrics.MaintenanceCost)}");
        lines.Add($"Repair Cost: {FormatPdfPeso(metrics.RepairCost)}");
        lines.Add($"Cleaning Cost: {FormatPdfPeso(metrics.CleaningCost)}");

        if (metrics.TotalRevenue > 0)
        {
            lines.Add($"Insight: Offsite costs consumed {metrics.CostToRevenueRatio:N1}% of revenue. Net after offsite costs is {FormatPdfPeso(metrics.NetAfterOffsiteCost)}.");
        }
    }

    private static void AddVehicleProfitability(List<string> lines, IReadOnlyList<VehicleCostProfitabilityItem> items, int limit)
    {
        AddSection(lines, "Top Vehicles by Offsite Cost (Profitability)");
        AddEmptyState(lines, items);
        foreach (var item in items.Take(limit))
        {
            lines.Add($"{CarPlate(item.CarDisplayName, item.PlateNumber)} - Cost: {FormatPdfPeso(item.TotalOffsiteCost)} - Revenue: {FormatPdfPeso(item.RevenueGenerated)} - Net: {FormatPdfPeso(item.NetAfterCost)}");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static ExcelSheet CreateProfitabilitySummarySheet(OperatingProfitabilitySummary metrics, DateTime from, DateTime to, string generatedBy) =>
        CreateSummarySheet("Operating Profitability", from, to, generatedBy,
        [
            ("Total Revenue", FormatPeso(metrics.TotalRevenue)),
            ("Total Offsite Cost", FormatPeso(metrics.TotalOffsiteCost)),
            ("Net After Offsite", FormatPeso(metrics.NetAfterOffsiteCost)),
            ("Cost-to-Revenue Ratio", FormatPercent(metrics.CostToRevenueRatio)),
            ("Maintenance Cost", FormatPeso(metrics.MaintenanceCost)),
            ("Repair Cost", FormatPeso(metrics.RepairCost)),
            ("Cleaning Cost", FormatPeso(metrics.CleaningCost))
        ]);

    private static ExcelSheet CreateVehicleProfitabilitySheet(IReadOnlyList<VehicleCostProfitabilityItem> items) =>
        new("Vehicle Profitability", ["Car / Plate", "Maintenance Count", "Repair Count", "Cleaning Count", "Total Offsite Cost", "Revenue Generated", "Net Profit"],
            items.Select(item => (IReadOnlyList<object?>)[CarPlate(item.CarDisplayName, item.PlateNumber), item.MaintenanceCount, item.RepairCount, item.CleaningCount, FormatPeso(item.TotalOffsiteCost), FormatPeso(item.RevenueGenerated), FormatPeso(item.NetAfterCost)]).ToList());

    private static void AddOverviewSummary(List<string> lines, ReportSummaryMetrics metrics)
    {
        AddSection(lines, "Overview");
        lines.Add($"Total Revenue: {FormatPdfPeso(metrics.TotalRevenue)}");
        lines.Add($"Active Rentals: {metrics.ActiveRentals}");
        lines.Add($"Completed Rentals: {metrics.CompletedRentals}");
        lines.Add($"Top Earning Car: {metrics.TopEarningCar ?? "-"} ({FormatPdfPeso(metrics.TopEarningCarRevenue)})");
        lines.Add($"Most Rented Car: {metrics.MostRentedCar ?? "-"} ({metrics.MostRentedCarCount} rental(s))");
    }

    private static void AddFinancialSummary(List<string> lines, ReportSummaryMetrics metrics)
    {
        AddSection(lines, "Financial Summary");
        lines.Add($"Total Revenue: {FormatPdfPeso(metrics.TotalRevenue)}");
        lines.Add($"Rental Revenue: {FormatPdfPeso(metrics.RentalRevenue)}");
        lines.Add($"Extension Fees: {FormatPdfPeso(metrics.ExtensionFees)}");
        lines.Add($"Damage Fees: {FormatPdfPeso(metrics.DamageFees)}");
        lines.Add($"Late Fees: {FormatPdfPeso(metrics.LateReturnFees)}");
        lines.Add($"Outstanding Balance: {FormatPdfPeso(metrics.OutstandingBalance)}");
        lines.Add($"Paid / Partial / Unpaid: {metrics.PaidTransactions} / {metrics.PartialTransactions} / {metrics.UnpaidTransactions}");
    }

    private static void AddFleetSummary(List<string> lines, FleetPerformanceMetrics metrics)
    {
        AddSection(lines, "Fleet Summary");
        lines.Add($"Total Fleet Revenue: {FormatPdfPeso(metrics.TotalFleetRevenue)}");
        lines.Add($"Average Revenue Per Car: {FormatPdfPeso(metrics.AverageRevenuePerCar)}");
        lines.Add($"Top Earning Car: {metrics.TopEarningCar ?? "-"} ({FormatPdfPeso(metrics.TopEarningCarRevenue)})");
        lines.Add($"Most Rented Car: {metrics.MostRentedCar ?? "-"} ({metrics.MostRentedCarCount} rental(s))");
        lines.Add($"Average Utilization Rate: {FormatPercent(metrics.AverageUtilizationRate)}");
        lines.Add($"Cars Under Maintenance: {metrics.CarsUnderMaintenance}");
    }

    private static void AddOperationsSummary(List<string> lines, OperationsMetrics metrics)
    {
        AddSection(lines, "Operations Summary");
        lines.Add($"Upcoming Returns: {metrics.UpcomingReturns}");
        lines.Add($"Late Returns: {metrics.LateReturns}");
        lines.Add($"Active Rentals: {metrics.ActiveRentals}");
        lines.Add($"Upcoming Reservations: {metrics.UpcomingReservations}");
        lines.Add($"Reserved Cars: {metrics.ReservedCars}");
        lines.Add($"Cars Under Maintenance: {metrics.CarsUnderMaintenance}");
        lines.Add($"Available Cars: {metrics.AvailableCars}");
        lines.Add($"Completed Returns: {metrics.CompletedReturns}");
    }

    private static void AddCustomerSummary(List<string> lines, CustomerAnalyticsMetrics metrics)
    {
        AddSection(lines, "Customer Summary");
        lines.Add($"Total Active Customers: {metrics.TotalActiveCustomers}");
        lines.Add($"New Customers: {metrics.NewCustomers}");
        lines.Add($"Top Customer by Revenue: {metrics.TopCustomerByRevenue ?? "-"} ({FormatPdfPeso(metrics.TopCustomerRevenue)})");
        lines.Add($"Top Customer by Rentals: {metrics.TopCustomerByRentals ?? "-"} ({metrics.TopCustomerRentalCount} rental(s))");
        lines.Add($"Blacklisted Customers: {metrics.BlacklistedCustomers}");
        lines.Add($"Late Return Customers: {metrics.CustomersWithLateReturns}");
        lines.Add($"Damage Fee Customers: {metrics.CustomersWithDamageFees}");
        lines.Add($"Average Revenue per Customer: {FormatPdfPeso(metrics.AverageRevenuePerCustomer)}");
    }

    private static void AddPaymentMethods(List<string> lines, IReadOnlyList<PaymentMethodBreakdownItem> items, int limit)
    {
        AddSection(lines, "Payment Method Breakdown");
        AddEmptyState(lines, items);
        foreach (PaymentMethodBreakdownItem item in items.Take(limit))
        {
            lines.Add($"{item.ModeOfPayment}: {item.PaymentCount} payment(s), {FormatPdfPeso(item.TotalAmount)}, {FormatPercent(item.Percentage)}");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static void AddPaymentCategories(List<string> lines, IReadOnlyList<RevenueByCategoryItem> items, int limit)
    {
        AddSection(lines, "Payment Category Breakdown");
        AddEmptyState(lines, items);
        foreach (RevenueByCategoryItem item in items.Take(limit))
        {
            lines.Add($"{item.PaymentCategory}: {item.PaymentCount} payment(s), {FormatPdfPeso(item.TotalAmount)}, {FormatPercent(item.Percentage)}");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static void AddOutstandingTransactions(List<string> lines, IReadOnlyList<TransactionListItem> items, int limit)
    {
        AddSection(lines, "Outstanding Transactions");
        AddEmptyState(lines, items);
        foreach (TransactionListItem item in items.Take(limit))
        {
            lines.Add($"{item.TransactionCode} - {item.CustomerName} - Balance {FormatPdfPeso(item.BalanceAmount)} - {item.PaymentStatus}");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static void AddTopCars(List<string> lines, string title, IReadOnlyList<TopCarItem> items, int limit)
    {
        AddSection(lines, title);
        AddEmptyState(lines, items);
        foreach (TopCarItem item in items.Take(limit))
        {
            lines.Add($"{CarPlate(item.CarName, item.PlateNumber)} - {item.RentalCount} rental(s) - {FormatPdfPeso(item.Revenue)}");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static void AddFleetUtilization(List<string> lines, IReadOnlyList<FleetUtilizationItem> items, int limit)
    {
        AddSection(lines, "Fleet Utilization");
        AddEmptyState(lines, items);
        foreach (FleetUtilizationItem item in items.Take(limit))
        {
            lines.Add($"{CarPlate(item.CarName, item.PlateNumber)} - {item.RentedDays} rented day(s) - {FormatPercent(item.UtilizationRate)} - {item.Status}");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static void AddMaintenance(List<string> lines, IReadOnlyList<FleetMaintenanceItem> items, int limit)
    {
        AddSection(lines, "Maintenance Visibility");
        AddEmptyState(lines, items);
        foreach (FleetMaintenanceItem item in items.Take(limit))
        {
            lines.Add($"{CarPlate(item.CarName, item.PlateNumber)} - {FormatDate(item.StartDate)} to {FormatDate(item.EndDate)} - {item.Status}");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static void AddUpcomingReturns(List<string> lines, IReadOnlyList<OperationsReturnItem> items, int limit)
    {
        AddSection(lines, "Upcoming Returns");
        AddEmptyState(lines, items);
        foreach (OperationsReturnItem item in items.Take(limit))
        {
            lines.Add($"{FormatDate(item.ExpectedReturn)} - {item.TransactionCode} - {item.CustomerName} - {CarPlate(item.CarName, item.PlateNumber)} - {item.PaymentStatus}");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static void AddLateReturns(List<string> lines, IReadOnlyList<OperationsReturnItem> items, int limit)
    {
        AddSection(lines, "Late Returns");
        AddEmptyState(lines, items);
        foreach (OperationsReturnItem item in items.Take(limit))
        {
            lines.Add($"{item.DaysLate} day(s) late - {item.TransactionCode} - {item.CustomerName} - Est. {FormatPdfPeso(item.EstimatedLateFee)}");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static void AddActiveRentals(List<string> lines, IReadOnlyList<OperationsActiveRentalItem> items, int limit)
    {
        AddSection(lines, "Active Rentals");
        AddEmptyState(lines, items);
        foreach (OperationsActiveRentalItem item in items.Take(limit))
        {
            lines.Add($"{item.TransactionCode} - {item.CustomerName} - {CarPlate(item.CarName, item.PlateNumber)} - {FormatDate(item.StartDate)} to {FormatDate(item.EndDate)}");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static void AddUpcomingReservations(List<string> lines, IReadOnlyList<OperationsReservationItem> items, int limit)
    {
        AddSection(lines, "Upcoming Reservations");
        AddEmptyState(lines, items);
        foreach (OperationsReservationItem item in items.Take(limit))
        {
            lines.Add($"{FormatDate(item.ScheduleDate)} - {item.CustomerName} - {CarPlate(item.CarName, item.PlateNumber)} - {item.Status} - {item.PaymentStatus}");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static void AddAvailableCars(List<string> lines, IReadOnlyList<OperationsAvailableCarItem> items, int limit)
    {
        AddSection(lines, "Available Cars");
        AddEmptyState(lines, items);
        foreach (OperationsAvailableCarItem item in items.Take(limit))
        {
            lines.Add($"{CarPlate(item.CarName, item.PlateNumber)} - {item.Status} - {FormatPdfPeso(item.RatePerDay)} / day - {item.SeatingCapacity?.ToString(CultureInfo.InvariantCulture) ?? "-"} seats");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static void AddCustomerRevenue(List<string> lines, IReadOnlyList<CustomerRevenueReportItem> items, int limit)
    {
        AddSection(lines, "Top Customers by Revenue");
        AddEmptyState(lines, items);
        foreach (CustomerRevenueReportItem item in items.Take(limit))
        {
            lines.Add($"{item.CustomerName} - {item.TransactionCount} transaction(s) - Paid {FormatPdfPeso(item.TotalPaid)} - Balance {FormatPdfPeso(item.OutstandingBalance)}");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static void AddCustomerRentals(List<string> lines, IReadOnlyList<CustomerRentalCountReportItem> items, int limit)
    {
        AddSection(lines, "Top Customers by Rental Count");
        AddEmptyState(lines, items);
        foreach (CustomerRentalCountReportItem item in items.Take(limit))
        {
            lines.Add($"{item.CustomerName} - {item.RentalCount} rental(s) - Completed {item.CompletedRentals} - Active {item.ActiveRentals}");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static void AddCustomerOutstanding(List<string> lines, IReadOnlyList<CustomerOutstandingBalanceReportItem> items, int limit)
    {
        AddSection(lines, "Customers with Outstanding Balances");
        AddEmptyState(lines, items);
        foreach (CustomerOutstandingBalanceReportItem item in items.Take(limit))
        {
            lines.Add($"{item.CustomerName} - {item.TransactionCode} - Balance {FormatPdfPeso(item.Balance)} - {item.PaymentStatus}");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static void AddBlacklistedCustomers(List<string> lines, IReadOnlyList<BlacklistedCustomerReportItem> items, int limit)
    {
        AddSection(lines, "Blacklisted Customers");
        AddEmptyState(lines, items);
        foreach (BlacklistedCustomerReportItem item in items.Take(limit))
        {
            lines.Add($"{item.CustomerName} - {item.Contact} - {item.Status} - {item.BlacklistReason}");
        }
        AddExcelNote(lines, items.Count, limit);
    }

    private static List<string> CreateHeader(string reportTitle, DateTime from, DateTime to, string generatedBy)
    {
        SystemSettingsModel settings = NatarakiCarRental.Helpers.AppBrandingManager.CurrentSettings;
        return
        [
            settings.ReportHeaderName,
            CreateBusinessContactLine(settings),
            reportTitle,
            $"Date Range: {FormatDate(from)} - {FormatDate(to)}",
            $"Generated: {DateTime.Now:MMM d, yyyy h:mm tt}",
            $"Generated By: {generatedBy}",
            string.Empty
        ];
    }

    private static string CreateBusinessContactLine(SystemSettingsModel settings)
    {
        string address = !string.IsNullOrWhiteSpace(settings.BusinessAddress)
            ? settings.BusinessAddress
            : string.Join(", ", new[]
            {
                settings.BusinessStreetAddress,
                settings.BusinessBarangayName,
                settings.BusinessCityName,
                settings.BusinessProvinceName,
                settings.BusinessRegionName
            }.Where(part => !string.IsNullOrWhiteSpace(part)));

        string[] parts =
        [
            settings.ContactNumber,
            settings.EmailAddress,
            address
        ];

        return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()));
    }

    private const string SectionMarker = "##SEC##";

    private static void AddSection(List<string> lines, string title)
    {
        lines.Add(string.Empty);
        lines.Add(SectionMarker + title);
    }

    private static void AddEmptyState<T>(List<string> lines, IReadOnlyCollection<T> items)
    {
        if (items.Count == 0)
        {
            lines.Add("No records found for this date range.");
        }
    }

    private static void AddExcelNote(List<string> lines, int count, int limit)
    {
        if (count > limit)
        {
            lines.Add($"Showing top {limit} rows. Full detailed rows are available in the Excel export.");
        }
    }

    private static void WriteXlsx(string path, IReadOnlyList<ExcelSheet> sheets)
    {
        using FileStream fileStream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using ZipArchive archive = new(fileStream, ZipArchiveMode.Create);

        WriteZipEntry(archive, "[Content_Types].xml", CreateContentTypesXml(sheets.Count));
        WriteZipEntry(archive, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);
        WriteZipEntry(archive, "xl/workbook.xml", CreateWorkbookXml(sheets));
        WriteZipEntry(archive, "xl/_rels/workbook.xml.rels", CreateWorkbookRelationshipsXml(sheets.Count));
        WriteZipEntry(archive, "xl/styles.xml", CreateStylesXml());

        for (int index = 0; index < sheets.Count; index++)
        {
            WriteZipEntry(archive, $"xl/worksheets/sheet{index + 1}.xml", CreateWorksheetXml(sheets[index]));
        }
    }

    private static string CreateContentTypesXml(int sheetCount)
    {
        StringBuilder builder = new();
        builder.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.AppendLine("""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">""");
        builder.AppendLine("""  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>""");
        builder.AppendLine("""  <Default Extension="xml" ContentType="application/xml"/>""");
        builder.AppendLine("""  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>""");
        builder.AppendLine("""  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>""");
        for (int index = 1; index <= sheetCount; index++)
        {
            builder.AppendLine($"""  <Override PartName="/xl/worksheets/sheet{index}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");
        }
        builder.AppendLine("</Types>");
        return builder.ToString();
    }

    private static string CreateWorkbookXml(IReadOnlyList<ExcelSheet> sheets)
    {
        StringBuilder builder = new();
        builder.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.AppendLine("""<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""");
        builder.AppendLine("  <sheets>");
        for (int index = 0; index < sheets.Count; index++)
        {
            builder.AppendLine($"""    <sheet name="{XmlEscape(SanitizeSheetName(sheets[index].Name))}" sheetId="{index + 1}" r:id="rId{index + 1}"/>""");
        }
        builder.AppendLine("  </sheets>");
        builder.AppendLine("</workbook>");
        return builder.ToString();
    }

    private static string CreateWorkbookRelationshipsXml(int sheetCount)
    {
        StringBuilder builder = new();
        builder.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.AppendLine("""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");
        for (int index = 1; index <= sheetCount; index++)
        {
            builder.AppendLine($"""  <Relationship Id="rId{index}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{index}.xml"/>""");
        }
        builder.AppendLine($"""  <Relationship Id="rId{sheetCount + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>""");
        builder.AppendLine("</Relationships>");
        return builder.ToString();
    }

    private static string CreateStylesXml()
    {
        return """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <fonts count="2">
                <font><sz val="11"/><name val="Calibri"/></font>
                <font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
              </fonts>
              <fills count="3">
                <fill><patternFill patternType="none"/></fill>
                <fill><patternFill patternType="gray125"/></fill>
                <fill><patternFill patternType="solid"><fgColor rgb="FF2563EB"/><bgColor indexed="64"/></patternFill></fill>
              </fills>
              <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
              <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
              <cellXfs count="2">
                <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
                <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
              </cellXfs>
            </styleSheet>
            """;
    }

    private static string CreateWorksheetXml(ExcelSheet sheet)
    {
        List<IReadOnlyList<object?>> rows = [];
        if (sheet.FirstRowIsHeader)
        {
            rows.Add(sheet.Headers);
        }
        rows.AddRange(sheet.Rows);

        StringBuilder builder = new();
        builder.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.AppendLine("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");
        builder.AppendLine("""  <sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>""");
        builder.AppendLine(CreateColumnWidths(rows));
        builder.AppendLine("  <sheetData>");

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            int rowNumber = rowIndex + 1;
            builder.AppendLine($"""    <row r="{rowNumber}">""");
            for (int columnIndex = 0; columnIndex < rows[rowIndex].Count; columnIndex++)
            {
                string reference = $"{GetColumnName(columnIndex + 1)}{rowNumber}";
                string value = Convert.ToString(rows[rowIndex][columnIndex], CultureInfo.InvariantCulture) ?? string.Empty;
                string style = rowIndex == 0 && sheet.FirstRowIsHeader ? " s=\"1\"" : string.Empty;
                builder.AppendLine($"""      <c r="{reference}" t="inlineStr"{style}><is><t>{XmlEscape(value)}</t></is></c>""");
            }
            builder.AppendLine("    </row>");
        }

        builder.AppendLine("  </sheetData>");
        builder.AppendLine("</worksheet>");
        return builder.ToString();
    }

    private static string CreateColumnWidths(IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        int columnCount = rows.Count == 0 ? 1 : rows.Max(row => row.Count);
        StringBuilder builder = new("  <cols>");
        for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            int maxLength = rows
                .Where(row => columnIndex < row.Count)
                .Select(row => Convert.ToString(row[columnIndex], CultureInfo.InvariantCulture)?.Length ?? 0)
                .DefaultIfEmpty(12)
                .Max();
            double width = Math.Clamp(maxLength + 3, 12, 42);
            builder.Append(CultureInfo.InvariantCulture, $"""<col min="{columnIndex + 1}" max="{columnIndex + 1}" width="{width}" customWidth="1"/>""");
        }
        builder.Append("</cols>");
        return builder.ToString();
    }

    private static void WritePdf(string path, IReadOnlyList<string> lines)
    {
        List<string> bodyLines = lines.Skip(PdfHeaderLineCount).SelectMany(WrapPdfLine).ToList();
        if (bodyLines.Count == 0)
        {
            bodyLines.Add("No report data available.");
        }

        List<IReadOnlyList<string>> pages = [];
        List<string> currentPage = [];
        foreach (string line in bodyLines)
        {
            if (currentPage.Count >= 34)
            {
                pages.Add(currentPage);
                currentPage = [];
            }
            currentPage.Add(line);
        }

        if (currentPage.Count > 0)
        {
            pages.Add(currentPage);
        }

        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using StreamWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);
        List<long> offsets = [];
        int pageCount = pages.Count;
        int regularFontObjectNumber = 3 + (pageCount * 2);
        int boldFontObjectNumber = regularFontObjectNumber + 1;

        writer.WriteLine("%PDF-1.4");
        WritePdfObject(writer, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
        WritePdfObject(writer, offsets, 2, $"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pageCount).Select(index => $"{3 + (index * 2)} 0 R"))}] /Count {pageCount} >>");

        for (int index = 0; index < pageCount; index++)
        {
            int pageObject = 3 + (index * 2);
            int contentObject = pageObject + 1;
            string content = CreatePdfPageContent(lines, pages[index], index + 1, pageCount);
            WritePdfObject(writer, offsets, pageObject, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 {regularFontObjectNumber} 0 R /F2 {boldFontObjectNumber} 0 R >> >> /Contents {contentObject} 0 R >>");
            WritePdfStreamObject(writer, offsets, contentObject, content);
        }

        WritePdfObject(writer, offsets, regularFontObjectNumber, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        WritePdfObject(writer, offsets, boldFontObjectNumber, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");

        writer.Flush();
        long xrefOffset = stream.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {offsets.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        foreach (long offset in offsets)
        {
            writer.WriteLine($"{offset:0000000000} 00000 n ");
        }
        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {offsets.Count + 1} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xrefOffset);
        writer.WriteLine("%%EOF");
    }

    private static string CreatePdfPageContent(IReadOnlyList<string> headerLines, IReadOnlyList<string> bodyLines, int pageNumber, int pageCount)
    {
        StringBuilder builder = new();
        AppendRectangle(builder, 0, 742, 612, 50, "0.145 0.388 0.922");
        AppendText(builder, headerLines.ElementAtOrDefault(0) ?? NatarakiCarRental.Helpers.AppBrandingManager.CurrentSettings.ReportHeaderName, 306, 772, 18, bold: true, centered: true, color: "1 1 1");
        AppendText(builder, headerLines.ElementAtOrDefault(1) ?? string.Empty, 306, 752, 9, bold: false, centered: true, color: "1 1 1");

        AppendText(builder, headerLines.ElementAtOrDefault(2) ?? "Report", 50, 720, 11, bold: true, color: "0.12 0.16 0.22");
        AppendText(builder, headerLines.ElementAtOrDefault(3) ?? string.Empty, 50, 704, 9, bold: false);
        AppendText(builder, headerLines.ElementAtOrDefault(4) ?? string.Empty, 50, 690, 9, bold: false);
        AppendText(builder, headerLines.ElementAtOrDefault(5) ?? string.Empty, 50, 676, 9, bold: false);
        AppendLine(builder, 50, 662, 562, 662, "0.80 0.84 0.90");

        double y = 640;
        foreach (string line in bodyLines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                y -= 8;
                continue;
            }

            string displayLine = line;
            bool isHeading = IsPdfSectionHeading(line);
            if (isHeading)
            {
                displayLine = line.Replace(SectionMarker, string.Empty, StringComparison.Ordinal);
                y -= 4;
                AppendRectangle(builder, 50, y - 3, 512, 18, "0.145 0.388 0.922");
                AppendText(builder, displayLine, 58, y + 2, 10, bold: true, color: "1 1 1");
                y -= 24;
                continue;
            }

            bool isNote = line.StartsWith("Showing top", StringComparison.Ordinal) ||
                          line.StartsWith("Full detailed", StringComparison.Ordinal) ||
                          line.StartsWith("Detailed records", StringComparison.Ordinal);
            AppendText(builder, displayLine, 58, y, isNote ? 8 : 9, bold: false, color: isNote ? "0.39 0.45 0.55" : "0.12 0.16 0.22");
            AppendLine(builder, 50, y - 5, 562, y - 5, "0.90 0.93 0.96");
            y -= 16;
        }

        AppendLine(builder, 50, 42, 562, 42, "0.80 0.84 0.90");
        AppendText(builder, $"Generated by {NatarakiCarRental.Helpers.AppBrandingManager.CurrentSettings.ReportHeaderName}", 50, 28, 8, bold: false, color: "0.39 0.45 0.55");
        AppendText(builder, $"Page {pageNumber} of {pageCount}", 562, 28, 8, bold: false, centered: false, rightAligned: true, color: "0.39 0.45 0.55");
        return builder.ToString();
    }

    private static bool IsPdfSectionHeading(string line)
    {
        return line.StartsWith(SectionMarker, StringComparison.Ordinal);
    }

    private static IEnumerable<string> WrapPdfLine(string line)
    {
        const int maxLength = 92;
        if (line.Length <= maxLength)
        {
            yield return line;
            yield break;
        }

        string remaining = line;
        while (remaining.Length > maxLength)
        {
            int split = remaining.LastIndexOf(' ', maxLength);
            if (split <= 0)
            {
                split = maxLength;
            }

            yield return remaining[..split];
            remaining = remaining[split..].TrimStart();
        }

        yield return remaining;
    }

    private static void AppendText(StringBuilder builder, string text, double x, double y, int size, bool bold, bool centered = false, bool rightAligned = false, string color = "0 0 0")
    {
        double textWidth = EstimatePdfTextWidth(text, size);
        if (centered)
        {
            x -= textWidth / 2;
        }
        else if (rightAligned)
        {
            x -= textWidth;
        }

        builder.AppendLine($"{color} rg");
        builder.AppendLine("BT");
        builder.AppendLine($"/{(bold ? "F2" : "F1")} {size} Tf");
        builder.AppendLine(FormattableString.Invariant($"{x:0.##} {y:0.##} Td"));
        builder.AppendLine($"({ToPdfLiteralString(text)}) Tj");
        builder.AppendLine("ET");
    }

    private static void AppendRectangle(StringBuilder builder, double x, double y, double width, double height, string color)
    {
        builder.AppendLine($"{color} rg");
        builder.AppendLine(FormattableString.Invariant($"{x:0.##} {y:0.##} {width:0.##} {height:0.##} re f"));
    }

    private static void AppendLine(StringBuilder builder, double x1, double y1, double x2, double y2, string color)
    {
        builder.AppendLine($"{color} RG");
        builder.AppendLine("0.5 w");
        builder.AppendLine(FormattableString.Invariant($"{x1:0.##} {y1:0.##} m {x2:0.##} {y2:0.##} l S"));
    }

    private static double EstimatePdfTextWidth(string text, int size) => text.Length * size * 0.48;

    private static string ToPdfLiteralString(string text) => EscapePdfText(SanitizePdfText(text));

    private static void WritePdfObject(StreamWriter writer, List<long> offsets, int objectNumber, string value)
    {
        writer.Flush();
        offsets.Add(writer.BaseStream.Position);
        writer.WriteLine($"{objectNumber} 0 obj");
        writer.WriteLine(value);
        writer.WriteLine("endobj");
    }

    private static void WritePdfStreamObject(StreamWriter writer, List<long> offsets, int objectNumber, string content)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(content);
        writer.Flush();
        offsets.Add(writer.BaseStream.Position);
        writer.WriteLine($"{objectNumber} 0 obj");
        writer.WriteLine($"<< /Length {bytes.Length} >>");
        writer.WriteLine("stream");
        writer.Flush();
        writer.BaseStream.Write(bytes, 0, bytes.Length);
        writer.WriteLine();
        writer.WriteLine("endstream");
        writer.WriteLine("endobj");
    }

    private static void WriteZipEntry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string SanitizeSheetName(string name)
    {
        char[] invalid = ['\\', '/', '?', '*', '[', ']', ':'];
        string sanitized = invalid.Aggregate(name, (current, character) => current.Replace(character, ' '));
        return sanitized.Length <= 31 ? sanitized : sanitized[..31];
    }

    private static string GetColumnName(int columnNumber)
    {
        StringBuilder columnName = new();
        while (columnNumber > 0)
        {
            int modulo = (columnNumber - 1) % 26;
            columnName.Insert(0, Convert.ToChar('A' + modulo));
            columnNumber = (columnNumber - modulo) / 26;
        }
        return columnName.ToString();
    }

    private static string XmlEscape(string value)
    {
        return SecurityElementEscape(value);
    }

    private static string SecurityElementEscape(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    private static string EscapePdfText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static string SanitizePdfText(string value)
    {
        return value
            .Replace("₱", "PHP ", StringComparison.Ordinal)
            .Replace("â‚±", "PHP ", StringComparison.Ordinal)
            .Replace("–", "-", StringComparison.Ordinal)
            .Replace("—", "-", StringComparison.Ordinal)
            .Replace("’", "'", StringComparison.Ordinal)
            .Replace("“", "\"", StringComparison.Ordinal)
            .Replace("”", "\"", StringComparison.Ordinal);
    }

    private static string CarPlate(string carName, string plateNumber) => $"{carName} ({plateNumber})";

    private static string FormatPeso(decimal amount) => $"₱{amount:N2}";

    private static string FormatPdfPeso(decimal amount) => $"PHP {amount:N2}";

    private static string FormatPercent(decimal value) => $"{value:N1}%";

    private static string FormatDate(DateTime date) => $"{date:MMM d, yyyy}";

    private sealed record ExcelSheet(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<object?>> Rows, bool FirstRowIsHeader = true);

    private sealed record FinancialExportData(
        ReportSummaryMetrics Summary,
        OperatingProfitabilitySummary Profitability,
        IReadOnlyList<PaymentMethodBreakdownItem> PaymentMethods,
        IReadOnlyList<RevenueByCategoryItem> PaymentCategories,
        IReadOnlyList<VehicleCostProfitabilityItem> VehicleProfitability,
        IReadOnlyList<TransactionListItem> OutstandingTransactions,
        IReadOnlyList<TopCarItem> RevenueByCar,
        IReadOnlyList<RevenueByCustomerItem> RevenueByCustomer);

    private sealed record FleetExportData(
        FleetPerformanceMetrics Metrics,
        IReadOnlyList<FleetUtilizationItem> Utilization,
        IReadOnlyList<FleetRevenuePerCarItem> RevenuePerCar,
        IReadOnlyList<TopCarItem> TopEarningCars,
        IReadOnlyList<TopCarItem> MostRentedCars,
        IReadOnlyList<TopCarItem> LeastUsedCars,
        IReadOnlyList<FleetMaintenanceItem> Maintenance);

    private sealed record OperationsExportData(
        OperationsMetrics Metrics,
        IReadOnlyList<OperationsReturnItem> UpcomingReturns,
        IReadOnlyList<OperationsReturnItem> LateReturns,
        IReadOnlyList<OperationsActiveRentalItem> ActiveRentals,
        IReadOnlyList<OperationsReservationItem> UpcomingReservations,
        IReadOnlyList<OperationsMaintenanceItem> Maintenance,
        IReadOnlyList<OperationsAvailableCarItem> AvailableCars);

    private sealed record CustomerExportData(
        CustomerAnalyticsMetrics Metrics,
        IReadOnlyList<CustomerRevenueReportItem> TopRevenue,
        IReadOnlyList<CustomerRentalCountReportItem> TopRentals,
        IReadOnlyList<CustomerOutstandingBalanceReportItem> OutstandingBalances,
        IReadOnlyList<CustomerLateReturnReportItem> LateReturns,
        IReadOnlyList<CustomerDamageFeeReportItem> DamageFees,
        IReadOnlyList<BlacklistedCustomerReportItem> BlacklistedCustomers);
}
