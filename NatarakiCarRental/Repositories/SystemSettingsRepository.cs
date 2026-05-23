using System.Data;
using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Repositories;

public sealed class SystemSettingsRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public SystemSettingsRepository() : this(new DbConnectionFactory())
    {
    }

    public SystemSettingsRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<SystemSetting>> GetAllAsync()
    {
        const string sql = "SELECT * FROM dbo.SystemSettings";
        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<SystemSetting>(sql);
        return results.ToList();
    }

    public async Task<string?> GetValueAsync(string key)
    {
        const string sql = "SELECT SettingValue FROM dbo.SystemSettings WHERE SettingKey = @Key";
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<string?>(sql, new { Key = key });
    }

    public async Task SetManyAsync(Dictionary<string, string?> settings, int updatedByUserId)
    {
        const string sql = """
            IF EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = @SettingKey)
            BEGIN
                UPDATE dbo.SystemSettings 
                SET SettingValue = @SettingValue, UpdatedAt = sysdatetime(), UpdatedByUserId = @UpdatedByUserId 
                WHERE SettingKey = @SettingKey;
            END
            ELSE
            BEGIN
                INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, UpdatedByUserId) 
                VALUES (@SettingKey, @SettingValue, @UpdatedByUserId);
            END
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var kvp in settings)
            {
                await connection.ExecuteAsync(sql, new { SettingKey = kvp.Key, SettingValue = kvp.Value, UpdatedByUserId = updatedByUserId }, transaction);
            }
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
