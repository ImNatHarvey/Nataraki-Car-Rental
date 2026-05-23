using System.Data;
using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Repositories;

public sealed class RoleRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public RoleRepository() : this(new DbConnectionFactory())
    {
    }

    public RoleRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Role>> GetAllAsync(bool includeArchived = false)
    {
        const string sql = "SELECT * FROM dbo.Roles WHERE IsArchived = 0 OR @IncludeArchived = 1 ORDER BY RoleName";
        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<Role>(sql, new { IncludeArchived = includeArchived });
        return results.ToList();
    }

    public async Task<Role?> GetByIdAsync(int roleId)
    {
        const string sql = "SELECT * FROM dbo.Roles WHERE RoleId = @RoleId";
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Role>(sql, new { RoleId = roleId });
    }

    public async Task<int> AddAsync(Role role, IDbTransaction? transaction = null)
    {
        const string sql = """
            INSERT INTO dbo.Roles (RoleName, Description, IsSystemRole, IsActive, IsArchived, CreatedAt)
            VALUES (@RoleName, @Description, @IsSystemRole, @IsActive, 0, sysdatetime());
            SELECT CAST(SCOPE_IDENTITY() as int);
            """;
        
        using var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, role, transaction);
    }

    public async Task<int> UpdateAsync(Role role, IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.Roles 
            SET RoleName = @RoleName, Description = @Description, IsActive = @IsActive, UpdatedAt = sysdatetime()
            WHERE RoleId = @RoleId AND IsSystemRole = 0;
            """;
        
        using var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(sql, role, transaction);
    }

    public async Task<int> ArchiveAsync(int roleId, IDbTransaction? transaction = null)
    {
        const string sql = "UPDATE dbo.Roles SET IsArchived = 1, UpdatedAt = sysdatetime() WHERE RoleId = @RoleId AND IsSystemRole = 0";
        using var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(sql, new { RoleId = roleId }, transaction);
    }

    public async Task<int> GetUserCountAsync(int roleId)
    {
        const string sql = "SELECT COUNT(1) FROM dbo.Users WHERE RoleId = @RoleId AND IsArchived = 0";
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { RoleId = roleId });
    }
}
