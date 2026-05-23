using System.Data;
using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Repositories;

public sealed class PermissionRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public PermissionRepository() : this(new DbConnectionFactory())
    {
    }

    public PermissionRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Permission>> GetAllAsync()
    {
        const string sql = "SELECT * FROM dbo.Permissions ORDER BY ModuleName, PermissionName";
        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<Permission>(sql);
        return results.ToList();
    }

    public async Task<IReadOnlyList<string>> GetKeysByRoleIdAsync(int roleId)
    {
        const string sql = """
            SELECT p.PermissionKey 
            FROM dbo.Permissions p
            JOIN dbo.RolePermissions rp ON rp.PermissionId = p.PermissionId
            WHERE rp.RoleId = @RoleId
            """;
        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<string>(sql, new { RoleId = roleId });
        return results.ToList();
    }

    public async Task SetRolePermissionsAsync(int roleId, IEnumerable<string> permissionKeys, IDbTransaction? transaction = null)
    {
        using var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        
        // 1. Clear existing
        const string deleteSql = "DELETE FROM dbo.RolePermissions WHERE RoleId = @RoleId";
        await connection.ExecuteAsync(deleteSql, new { RoleId = roleId }, transaction);

        // 2. Insert new
        const string insertSql = """
            INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
            SELECT @RoleId, PermissionId FROM dbo.Permissions WHERE PermissionKey = @Key
            """;
        
        foreach (var key in permissionKeys)
        {
            await connection.ExecuteAsync(insertSql, new { RoleId = roleId, Key = key }, transaction);
        }
    }
}
