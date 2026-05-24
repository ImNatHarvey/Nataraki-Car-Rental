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
        const string sql = """
            SELECT *
            FROM dbo.Roles
            WHERE IsArchived = 0 OR @IncludeArchived = 1
            ORDER BY RoleName;
            """;
        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<Role>(sql, new { IncludeArchived = includeArchived });
        return results.ToList();
    }

    public async Task NormalizeDuplicateOwnerRolesAsync()
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
            BEGIN
                ;WITH OwnerCandidates AS
                (
                    SELECT
                        r.RoleId,
                        KeepRank = ROW_NUMBER() OVER
                        (
                            ORDER BY
                                CASE WHEN EXISTS (SELECT 1 FROM dbo.Users u WHERE u.RoleId = r.RoleId AND u.IsOwner = 1) THEN 0 ELSE 1 END,
                                r.IsArchived,
                                r.RoleId
                        )
                    FROM dbo.Roles r
                    WHERE UPPER(LTRIM(RTRIM(r.RoleName))) = N'OWNER'
                )
                UPDATE r
                SET RoleName = N'Owner',
                    IsSystemRole = 1,
                    IsActive = 1,
                    IsArchived = 0,
                    UpdatedAt = sysdatetime()
                FROM dbo.Roles r
                JOIN OwnerCandidates c ON c.RoleId = r.RoleId
                WHERE c.KeepRank = 1;

                ;WITH OwnerCandidates AS
                (
                    SELECT
                        r.RoleId,
                        KeepRank = ROW_NUMBER() OVER
                        (
                            ORDER BY
                                CASE WHEN EXISTS (SELECT 1 FROM dbo.Users u WHERE u.RoleId = r.RoleId AND u.IsOwner = 1) THEN 0 ELSE 1 END,
                                r.IsArchived,
                                r.RoleId
                        )
                    FROM dbo.Roles r
                    WHERE UPPER(LTRIM(RTRIM(r.RoleName))) = N'OWNER'
                )
                UPDATE r
                SET IsArchived = 1,
                    IsActive = 0,
                    IsSystemRole = 0,
                    UpdatedAt = sysdatetime()
                FROM dbo.Roles r
                JOIN OwnerCandidates c ON c.RoleId = r.RoleId
                WHERE c.KeepRank > 1
                  AND NOT EXISTS (SELECT 1 FROM dbo.Users u WHERE u.RoleId = r.RoleId);

                ;WITH OwnerCandidates AS
                (
                    SELECT
                        r.RoleId,
                        KeepRank = ROW_NUMBER() OVER
                        (
                            ORDER BY
                                CASE WHEN EXISTS (SELECT 1 FROM dbo.Users u WHERE u.RoleId = r.RoleId AND u.IsOwner = 1) THEN 0 ELSE 1 END,
                                r.IsArchived,
                                r.RoleId
                        )
                    FROM dbo.Roles r
                    WHERE UPPER(LTRIM(RTRIM(r.RoleName))) = N'OWNER'
                )
                UPDATE r
                SET RoleName = CONCAT(N'Owner Duplicate ', r.RoleId),
                    IsSystemRole = 0,
                    UpdatedAt = sysdatetime()
                FROM dbo.Roles r
                JOIN OwnerCandidates c ON c.RoleId = r.RoleId
                WHERE c.KeepRank > 1
                  AND EXISTS (SELECT 1 FROM dbo.Users u WHERE u.RoleId = r.RoleId);
            END;
            """;
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql);
    }

    public async Task<Role?> GetByIdAsync(int roleId)
    {
        const string sql = "SELECT * FROM dbo.Roles WHERE RoleId = @RoleId";
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Role>(sql, new { RoleId = roleId });
    }

    public async Task<bool> ExistsByNameAsync(string roleName, int? excludeRoleId = null)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Roles
            WHERE UPPER(LTRIM(RTRIM(RoleName))) = UPPER(LTRIM(RTRIM(@RoleName)))
              AND (@ExcludeRoleId IS NULL OR RoleId <> @ExcludeRoleId);
            """;
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { RoleName = roleName, ExcludeRoleId = excludeRoleId }) > 0;
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
            SET RoleName = @RoleName, Description = @Description, UpdatedAt = sysdatetime()
            WHERE RoleId = @RoleId AND IsSystemRole = 0;
            """;
        
        using var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(sql, role, transaction);
    }

    public async Task<int> ArchiveAsync(int roleId, IDbTransaction? transaction = null)
    {
        const string sql = "UPDATE dbo.Roles SET IsArchived = 1, IsActive = 0, UpdatedAt = sysdatetime() WHERE RoleId = @RoleId AND IsSystemRole = 0";
        using var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(sql, new { RoleId = roleId }, transaction);
    }

    public async Task<int> RestoreAsync(int roleId, IDbTransaction? transaction = null)
    {
        const string sql = "UPDATE dbo.Roles SET IsArchived = 0, IsActive = 1, UpdatedAt = sysdatetime() WHERE RoleId = @RoleId AND IsSystemRole = 0";
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
