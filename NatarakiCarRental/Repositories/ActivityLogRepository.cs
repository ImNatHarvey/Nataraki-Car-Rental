using System.Data;
using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Repositories;

public sealed class ActivityLogRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public ActivityLogRepository()
        : this(new DbConnectionFactory())
    {
    }

    public ActivityLogRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ActivityLog>> SearchLogsAsync(
        string searchText,
        string? action,
        string? module,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        int maxRows = 500)
    {
        string normalizedSearchText = searchText?.Trim() ?? string.Empty;

        const string sql = """
            SELECT TOP (@MaxRows)
                logs.ActivityLogId,
                logs.UserId,
                UserFullName = logs.UserFullName,
                Module = logs.Module,
                Action = logs.Action,
                logs.EntityName,
                logs.EntityId,
                logs.Description,
                logs.OldValue,
                logs.NewValue,
                logs.CreatedAt
            FROM dbo.ActivityLogs AS logs
            WHERE (@Action IS NULL OR logs.Action = @Action)
              AND (@Module IS NULL OR logs.Module = @Module)
              AND (@DateFrom IS NULL OR logs.CreatedAt >= @DateFrom)
              AND (@DateTo IS NULL OR logs.CreatedAt <= @DateTo)
              AND (
                    @SearchText = N''
                    OR logs.UserFullName LIKE @SearchPattern
                    OR logs.Action LIKE @SearchPattern
                    OR logs.Module LIKE @SearchPattern
                    OR logs.EntityName LIKE @SearchPattern
                    OR logs.Description LIKE @SearchPattern
                  )
            ORDER BY logs.CreatedAt DESC, logs.ActivityLogId DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<ActivityLog> logs = await connection.QueryAsync<ActivityLog>(
            sql,
            new
            {
                MaxRows = Math.Clamp(maxRows, 1, 1000),
                SearchText = normalizedSearchText,
                SearchPattern = $"%{normalizedSearchText}%",
                Action = NullIfWhiteSpace(action),
                Module = NullIfWhiteSpace(module),
                DateFrom = dateFrom,
                DateTo = dateTo
            });

        return logs.ToList();
    }

    public async Task InsertAsync(ActivityLog log, IDbTransaction? transaction = null)
    {
        const string sql = """
            INSERT INTO dbo.ActivityLogs
            (
                UserId,
                UserFullName,
                Module,
                Action,
                EntityName,
                EntityId,
                Description,
                OldValue,
                NewValue
            )
            VALUES
            (
                @UserId,
                @UserFullName,
                @Module,
                @Action,
                @EntityName,
                @EntityId,
                @Description,
                @OldValue,
                @NewValue
            );
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(sql, log, transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<ActivityLogMetrics> GetMetricsAsync()
    {
        const string sql = """
            SELECT
                TotalLogs = COUNT(1),
                TodaysLogs = COUNT(CASE WHEN CONVERT(date, CreatedAt) = CONVERT(date, SYSDATETIME()) THEN 1 END),
                CarActions = COUNT(CASE WHEN Module = N'Car' THEN 1 END),
                CustomerActions = COUNT(CASE WHEN Module = N'Customer' THEN 1 END),
                TransactionActions = COUNT(CASE WHEN Module = N'Transaction' THEN 1 END),
                FleetActions = COUNT(CASE WHEN Module = N'FleetSchedule' THEN 1 END)
            FROM dbo.ActivityLogs;
            """;

        using var connection = _connectionFactory.CreateConnection();
        ActivityLogMetrics? metrics = await connection.QuerySingleOrDefaultAsync<ActivityLogMetrics>(sql);
        return metrics ?? new ActivityLogMetrics();
    }

    public async Task<IReadOnlyList<string>> GetActionTypesAsync()
    {
        const string sql = """
            SELECT DISTINCT Action
            FROM dbo.ActivityLogs
            WHERE LEN(LTRIM(RTRIM(Action))) > 0
            ORDER BY Action;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<string> values = await connection.QueryAsync<string>(sql);
        return values.ToList();
    }

    public async Task<IReadOnlyList<string>> GetActionTypesByEntityAsync(string module)
    {
        const string sql = """
            SELECT DISTINCT Action
            FROM dbo.ActivityLogs
            WHERE Module = @Module
              AND LEN(LTRIM(RTRIM(Action))) > 0
            ORDER BY Action;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<string> values = await connection.QueryAsync<string>(sql, new { Module = module });
        return values.ToList();
    }

    public async Task<IReadOnlyList<string>> GetEntityNamesAsync()
    {
        const string sql = """
            SELECT DISTINCT Module
            FROM dbo.ActivityLogs
            WHERE Module IS NOT NULL
              AND LEN(LTRIM(RTRIM(Module))) > 0
            ORDER BY Module;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<string> values = await connection.QueryAsync<string>(sql);
        return values.ToList();
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
