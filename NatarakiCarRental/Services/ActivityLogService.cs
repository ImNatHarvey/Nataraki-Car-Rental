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

    public Task<IReadOnlyList<string>> GetEntityNamesAsync()
    {
        return _activityLogRepository.GetEntityNamesAsync();
    }

    public async Task LogAsync(
        string actionType,
        string entityName,
        int? entityId,
        string description,
        int? userId = null,
        IDbTransaction? transaction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        await _activityLogRepository.InsertAsync(
            new ActivityLog
            {
                UserId = userId,
                ActionType = actionType.Trim(),
                EntityName = string.IsNullOrWhiteSpace(entityName) ? null : entityName.Trim(),
                EntityId = entityId,
                Description = description.Trim()
            },
            transaction);
    }
}
