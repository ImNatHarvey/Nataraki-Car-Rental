using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;

namespace NatarakiCarRental.Services;

public sealed class DashboardService
{
    private readonly DashboardRepository _dashboardRepository;

    public DashboardService() : this(new DashboardRepository()) { }

    public DashboardService(DashboardRepository dashboardRepository)
    {
        _dashboardRepository = dashboardRepository;
    }

    public async Task<DashboardOperationalData> GetDashboardOperationalDataAsync(DateTime fromDate, DateTime toDate)
    {
        AccessControlService.EnforcePermission("Overview.View");
        return await _dashboardRepository.GetDashboardDataAsync(fromDate, toDate);
    }
}
