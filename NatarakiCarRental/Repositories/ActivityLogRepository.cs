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
        string? actionType,
        string? entityName,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        int maxRows = 500)
    {
        string normalizedSearchText = searchText?.Trim() ?? string.Empty;

        const string sql = """
            SELECT TOP (@MaxRows)
                logs.ActivityLogId,
                logs.UserId,
                UserDisplayName =
                    CASE
                        WHEN logs.UserId IS NULL THEN N'System'
                        WHEN users.UserId IS NULL THEN N'Unknown User'
                        WHEN LEN(LTRIM(RTRIM(CONCAT(users.FirstName, N' ', users.LastName)))) > 0
                            THEN LTRIM(RTRIM(CONCAT(users.FirstName, N' ', users.LastName)))
                        ELSE users.Username
                    END,
                logs.ActionType,
                logs.EntityName,
                logs.EntityId,
                logs.Description,
                logs.CreatedAt
            FROM dbo.ActivityLogs AS logs
            LEFT JOIN dbo.Users AS users
                ON users.UserId = logs.UserId
            WHERE (@ActionType IS NULL OR logs.ActionType = @ActionType)
              AND (@EntityName IS NULL OR logs.EntityName = @EntityName)
              AND (@DateFrom IS NULL OR logs.CreatedAt >= @DateFrom)
              AND (@DateTo IS NULL OR logs.CreatedAt <= @DateTo)
              AND (
                    @SearchText = N''
                    OR users.Username LIKE @SearchPattern
                    OR users.FirstName LIKE @SearchPattern
                    OR users.LastName LIKE @SearchPattern
                    OR CONCAT(users.FirstName, N' ', users.LastName) LIKE @SearchPattern
                    OR logs.ActionType LIKE @SearchPattern
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
                ActionType = NullIfWhiteSpace(actionType),
                EntityName = NullIfWhiteSpace(entityName),
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
                ActionType,
                EntityName,
                EntityId,
                Description
            )
            VALUES
            (
                @UserId,
                @ActionType,
                @EntityName,
                @EntityId,
                @Description
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
                CarActions = COUNT(CASE WHEN EntityName = N'Car' THEN 1 END),
                CustomerActions = COUNT(CASE WHEN EntityName = N'Customer' THEN 1 END),
                TransactionActions = COUNT(CASE WHEN EntityName = N'Transaction' THEN 1 END),
                FleetActions = COUNT(CASE WHEN EntityName = N'FleetSchedule' THEN 1 END)
            FROM dbo.ActivityLogs;
            """;

        using var connection = _connectionFactory.CreateConnection();
        ActivityLogMetrics? metrics = await connection.QuerySingleOrDefaultAsync<ActivityLogMetrics>(sql);
        return metrics ?? new ActivityLogMetrics();
    }

    public async Task<IReadOnlyList<string>> GetActionTypesAsync()
    {
        const string sql = """
            SELECT DISTINCT ActionType
            FROM dbo.ActivityLogs
            WHERE LEN(LTRIM(RTRIM(ActionType))) > 0
            ORDER BY ActionType;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<string> values = await connection.QueryAsync<string>(sql);
        return values.ToList();
    }

    public async Task<IReadOnlyList<string>> GetActionTypesByEntityAsync(string entityName)
    {
        const string sql = """
            SELECT DISTINCT ActionType
            FROM dbo.ActivityLogs
            WHERE EntityName = @EntityName
              AND LEN(LTRIM(RTRIM(ActionType))) > 0
            ORDER BY ActionType;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<string> values = await connection.QueryAsync<string>(sql, new { EntityName = entityName });
        return values.ToList();
    }

    public async Task<IReadOnlyList<string>> GetEntityNamesAsync()
    {
        const string sql = """
            SELECT DISTINCT EntityName
            FROM dbo.ActivityLogs
            WHERE EntityName IS NOT NULL
              AND LEN(LTRIM(RTRIM(EntityName))) > 0
            ORDER BY EntityName;
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
