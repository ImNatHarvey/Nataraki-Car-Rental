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
}
