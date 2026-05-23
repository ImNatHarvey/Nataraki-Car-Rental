using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;

namespace NatarakiCarRental.Services;

public sealed class ReportService
{
    private readonly ReportRepository _reportRepository;

    public ReportService() : this(new ReportRepository()) { }

    public ReportService(ReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public Task<ReportSummaryMetrics> GetSummaryMetricsAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetSummaryMetricsAsync(from, to);
    }

    public Task<IReadOnlyList<PaymentMethodBreakdownItem>> GetPaymentMethodBreakdownAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetPaymentMethodBreakdownAsync(from, to);
    }

    public Task<IReadOnlyList<RevenueByCategoryItem>> GetRevenueByCategoryAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetRevenueByCategoryAsync(from, to);
    }

    public Task<IReadOnlyList<TransactionStatusBreakdownItem>> GetTransactionStatusBreakdownAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetTransactionStatusBreakdownAsync(from, to);
    }

    public Task<IReadOnlyList<TopCarItem>> GetTopCarsByRevenueAsync(DateTime from, DateTime to, int limit = 5)
    {
        return _reportRepository.GetTopCarsByRevenueAsync(from, to, limit);
    }

    public Task<IReadOnlyList<TopCarItem>> GetMostRentedCarsAsync(DateTime from, DateTime to, int limit = 5)
    {
        return _reportRepository.GetMostRentedCarsAsync(from, to, limit);
    }

    public Task<IReadOnlyList<TransactionListItem>> GetOutstandingTransactionsAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetOutstandingTransactionsAsync(from, to);
    }

    public Task<IReadOnlyList<TopCarItem>> GetRevenueByCarAsync(DateTime from, DateTime to, int limit = 10)
    {
        return _reportRepository.GetRevenueByCarAsync(from, to, limit);
    }

    public Task<IReadOnlyList<RevenueByCustomerItem>> GetRevenueByCustomerAsync(DateTime from, DateTime to, int limit = 10)
    {
        return _reportRepository.GetRevenueByCustomerAsync(from, to, limit);
    }

    public Task<FleetPerformanceMetrics> GetFleetPerformanceMetricsAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetFleetPerformanceMetricsAsync(from, to);
    }

    public Task<IReadOnlyList<FleetUtilizationItem>> GetFleetUtilizationAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetFleetUtilizationAsync(from, to);
    }

    public Task<IReadOnlyList<FleetRevenuePerCarItem>> GetFleetRevenuePerCarAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetFleetRevenuePerCarAsync(from, to);
    }

    public Task<IReadOnlyList<TopCarItem>> GetLeastUsedCarsAsync(DateTime from, DateTime to, int limit = 5)
    {
        return _reportRepository.GetLeastUsedCarsAsync(from, to, limit);
    }

    public Task<IReadOnlyList<FleetMaintenanceItem>> GetCarsUnderMaintenanceAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetCarsUnderMaintenanceAsync(from, to);
    }

    public Task<OperationsMetrics> GetOperationsMetricsAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetOperationsMetricsAsync(from, to);
    }

    public Task<IReadOnlyList<OperationsReturnItem>> GetUpcomingReturnsAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetUpcomingReturnsAsync(from, to);
    }

    public Task<IReadOnlyList<OperationsReturnItem>> GetLateReturnsAsync(DateTime today)
    {
        return _reportRepository.GetLateReturnsAsync(today);
    }

    public Task<IReadOnlyList<OperationsActiveRentalItem>> GetActiveRentalsReportAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetActiveRentalsReportAsync(from, to);
    }

    public Task<IReadOnlyList<OperationsReservationItem>> GetUpcomingReservationsAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetUpcomingReservationsAsync(from, to);
    }

    public Task<IReadOnlyList<OperationsMaintenanceItem>> GetMaintenanceVisibilityAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetMaintenanceVisibilityAsync(from, to);
    }

    public Task<IReadOnlyList<OperationsAvailableCarItem>> GetAvailableCarsReportAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetAvailableCarsReportAsync(from, to);
    }

    public Task<CustomerAnalyticsMetrics> GetCustomerAnalyticsMetricsAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetCustomerAnalyticsMetricsAsync(from, to);
    }

    public Task<IReadOnlyList<CustomerRevenueReportItem>> GetTopCustomersByRevenueAsync(DateTime from, DateTime to, int limit = 10)
    {
        return _reportRepository.GetTopCustomersByRevenueAsync(from, to, limit);
    }

    public Task<IReadOnlyList<CustomerRentalCountReportItem>> GetTopCustomersByRentalCountAsync(DateTime from, DateTime to, int limit = 10)
    {
        return _reportRepository.GetTopCustomersByRentalCountAsync(from, to, limit);
    }

    public Task<IReadOnlyList<CustomerOutstandingBalanceReportItem>> GetCustomersWithOutstandingBalancesAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetCustomersWithOutstandingBalancesAsync(from, to);
    }

    public Task<IReadOnlyList<CustomerLateReturnReportItem>> GetCustomersWithLateReturnsAsync(DateTime today)
    {
        return _reportRepository.GetCustomersWithLateReturnsAsync(today);
    }

    public Task<IReadOnlyList<CustomerDamageFeeReportItem>> GetCustomersWithDamageFeesAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetCustomersWithDamageFeesAsync(from, to);
    }

    public Task<IReadOnlyList<BlacklistedCustomerReportItem>> GetBlacklistedCustomersReportAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetBlacklistedCustomersReportAsync(from, to);
    }

    public Task<OperatingProfitabilitySummary> GetOperatingProfitabilityAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetOperatingProfitabilityAsync(from, to);
    }

    public Task<IReadOnlyList<VehicleCostProfitabilityItem>> GetVehicleProfitabilityAsync(DateTime from, DateTime to)
    {
        return _reportRepository.GetVehicleProfitabilityAsync(from, to);
    }
}
