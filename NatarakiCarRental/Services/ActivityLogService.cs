using System.Data;
using NatarakiCarRental.Data;
using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;

namespace NatarakiCarRental.Services;

public sealed class ActivityLogService
{
    private readonly ActivityLogRepository _activityLogRepository;

    public ActivityLogService()
        : this(new DbConnectionFactory())
    {
    }

    public ActivityLogService(DbConnectionFactory connectionFactory)
    {
        _activityLogRepository = new ActivityLogRepository(connectionFactory);
    }

    public Task<IReadOnlyList<ActivityLog>> SearchLogsAsync(
        string searchText,
        string? actionType = null,
        string? entityName = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        int maxRows = 500)
    {
        return _activityLogRepository.SearchLogsAsync(searchText, actionType, entityName, dateFrom, dateTo, maxRows);
    }

    public Task<ActivityLogMetrics> GetMetricsAsync()
    {
        return _activityLogRepository.GetMetricsAsync();
    }

    public Task<IReadOnlyList<string>> GetActionTypesAsync()
    {
        return _activityLogRepository.GetActionTypesAsync();
    }

    public Task<IReadOnlyList<string>> GetActionTypesByEntityAsync(string module)
    {
        return _activityLogRepository.GetActionTypesByEntityAsync(module);
    }

    public Task<IReadOnlyList<string>> GetEntityNamesAsync()
    {
        return _activityLogRepository.GetEntityNamesAsync();
    }

    public async Task LogAsync(
        string action,
        string module,
        int? entityId,
        string description,
        int? userId = null,
        string? userFullName = null,
        string? entityName = null,
        string? oldValue = null,
        string? newValue = null,
        IDbTransaction? transaction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (string.IsNullOrWhiteSpace(userFullName) && userId.HasValue)
        {
            userFullName = AccessControlService.CurrentUser?.UserId == userId 
                ? AccessControlService.CurrentUser.FullName 
                : null;
        }

        await _activityLogRepository.InsertAsync(
            new ActivityLog
            {
                UserId = userId,
                UserFullName = userFullName ?? "System",
                Module = string.IsNullOrWhiteSpace(module) ? "System" : module.Trim(),
                Action = action.Trim(),
                ActionType = action.Trim(),
                EntityName = string.IsNullOrWhiteSpace(entityName) ? null : entityName.Trim(),
                EntityId = entityId,
                Description = description.Trim(),
                OldValue = oldValue,
                NewValue = newValue
            },
            transaction);
    }
}
