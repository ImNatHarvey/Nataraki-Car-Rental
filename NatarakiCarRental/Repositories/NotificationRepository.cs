using System.Data;
using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Repositories;

public sealed class NotificationRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public NotificationRepository() : this(new DbConnectionFactory())
    {
    }

    public NotificationRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> AddAsync(Notification notification, IDbTransaction? transaction = null)
    {
        const string sql = """
            INSERT INTO dbo.Notifications (Title, Message, Type, RelatedEntityId, RelatedModule)
            VALUES (@Title, @Message, @Type, @RelatedEntityId, @RelatedModule);
            SELECT CAST(SCOPE_IDENTITY() as int);
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        try
        {
            return await connection.ExecuteScalarAsync<int>(sql, notification, transaction);
        }
        finally
        {
            if (transaction == null) connection.Dispose();
        }
    }

    public async Task<IReadOnlyList<Notification>> GetLatestAsync(int limit = 50)
    {
        const string sql = """
            SELECT TOP (@Limit) *
            FROM dbo.Notifications
            ORDER BY CreatedAt DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var notifications = await connection.QueryAsync<Notification>(sql, new { Limit = limit });
        return notifications.ToList();
    }

    public async Task<int> GetUnreadCountAsync()
    {
        const string sql = "SELECT COUNT(1) FROM dbo.Notifications WHERE IsRead = 0;";
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql);
    }

    public async Task MarkAsReadAsync(int notificationId)
    {
        const string sql = "UPDATE dbo.Notifications SET IsRead = 1 WHERE NotificationId = @NotificationId;";
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, new { NotificationId = notificationId });
    }

    public async Task MarkAllAsReadAsync()
    {
        const string sql = "UPDATE dbo.Notifications SET IsRead = 1 WHERE IsRead = 0;";
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql);
    }

    public async Task DeleteAsync(int notificationId)
    {
        const string sql = "DELETE FROM dbo.Notifications WHERE NotificationId = @NotificationId;";
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, new { NotificationId = notificationId });
    }
}
