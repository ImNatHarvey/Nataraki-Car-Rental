using System.Data;
using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Repositories;

public sealed class UserRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public UserRepository()
        : this(new DbConnectionFactory())
    {
    }

    public UserRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<User?> GetActiveUserByUsernameAsync(string username)
    {
        const string sql = "SELECT * FROM dbo.Users WHERE Username = @Username AND IsActive = 1 AND IsArchived = 0";
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Username = username });
    }

    public async Task<User?> GetByIdAsync(int userId)
    {
        const string sql = "SELECT * FROM dbo.Users WHERE UserId = @UserId";
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { UserId = userId });
    }

    public async Task<User?> GetActiveOwnerAsync()
    {
        const string sql = """
            SELECT TOP 1 *
            FROM dbo.Users
            WHERE IsOwner = 1 AND IsActive = 1 AND IsArchived = 0
            ORDER BY COALESCE(UpdatedAt, CreatedAt) DESC, UserId DESC;
            """;
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<User>(sql);
    }

    public async Task<IReadOnlyList<User>> GetActiveOwnersAsync()
    {
        const string sql = """
            SELECT *
            FROM dbo.Users
            WHERE IsOwner = 1 AND IsActive = 1 AND IsArchived = 0
            ORDER BY COALESCE(UpdatedAt, CreatedAt) DESC, UserId DESC;
            """;
        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<User>(sql);
        return results.ToList();
    }

    public async Task<IReadOnlyList<UserListItem>> SearchAsync(string? searchTerm, int? roleId, bool? isActive, bool includeArchived = false)
    {
        string sql = """
            SELECT 
                u.UserId,
                u.Username,
                FullName = u.FirstName + ' ' + u.LastName,
                RoleName = r.RoleName,
                u.IsActive,
                u.IsOwner,
                u.IsArchived,
                u.LastLoginAt,
                u.CreatedAt
            FROM dbo.Users u
            JOIN dbo.Roles r ON r.RoleId = u.RoleId
            WHERE (u.IsArchived = 0 OR @IncludeArchived = 1)
            """;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            sql += " AND (u.Username LIKE @Search OR u.FirstName LIKE @Search OR u.LastName LIKE @Search OR r.RoleName LIKE @Search)";
        }
        if (roleId.HasValue)
        {
            sql += " AND u.RoleId = @RoleId";
        }
        if (isActive.HasValue)
        {
            sql += " AND u.IsActive = @IsActive";
        }

        sql += " ORDER BY u.CreatedAt DESC";

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<UserListItem>(sql, new 
        { 
            IncludeArchived = includeArchived, 
            Search = $"%{searchTerm}%",
            RoleId = roleId,
            IsActive = isActive
        });
        return results.ToList();
    }

    public async Task<int> AddAsync(User user, IDbTransaction? transaction = null)
    {
        const string sql = """
            INSERT INTO dbo.Users (RoleId, Username, PasswordHash, FirstName, LastName, Email, PhoneNumber, IsActive, IsOwner, IsArchived, CreatedAt)
            VALUES (@RoleId, @Username, @PasswordHash, @FirstName, @LastName, @Email, @PhoneNumber, @IsActive, @IsOwner, 0, sysdatetime());
            SELECT CAST(SCOPE_IDENTITY() as int);
            """;
        using var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, user, transaction);
    }

    public async Task<int> UpdateAsync(User user, IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.Users 
            SET Username = @Username,
                RoleId = @RoleId,
                FirstName = @FirstName,
                LastName = @LastName,
                Email = @Email,
                PhoneNumber = @PhoneNumber,
                IsActive = @IsActive,
                UpdatedAt = sysdatetime()
            WHERE UserId = @UserId;
            """;
        using var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(sql, user, transaction);
    }

    public async Task UpdatePasswordAsync(int userId, string passwordHash, IDbTransaction? transaction = null)
    {
        const string sql = "UPDATE dbo.Users SET PasswordHash = @Hash, UpdatedAt = sysdatetime() WHERE UserId = @UserId";
        using var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, new { UserId = userId, Hash = passwordHash }, transaction);
    }

    public async Task UpdateLastLoginAsync(int userId)
    {
        const string sql = "UPDATE dbo.Users SET LastLoginAt = sysdatetime() WHERE UserId = @UserId";
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, new { UserId = userId });
    }

    public async Task<int> ArchiveAsync(int userId, IDbTransaction? transaction = null)
    {
        const string sql = "UPDATE dbo.Users SET IsArchived = 1, IsActive = 0, ArchivedAt = sysdatetime() WHERE UserId = @UserId AND IsOwner = 0";
        using var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(sql, new { UserId = userId }, transaction);
    }

    public async Task<int> RestoreAsync(int userId, IDbTransaction? transaction = null)
    {
        const string sql = "UPDATE dbo.Users SET IsArchived = 0, IsActive = 1, ArchivedAt = NULL, UpdatedAt = sysdatetime() WHERE UserId = @UserId";
        using var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(sql, new { UserId = userId }, transaction);
    }

    public async Task<bool> ExistsByUsernameAsync(string username, int? excludeUserId = null)
    {
        const string sql = "SELECT COUNT(1) FROM dbo.Users WHERE Username = @Username AND (@ExcludeUserId IS NULL OR UserId <> @ExcludeUserId)";
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { Username = username, ExcludeUserId = excludeUserId }) > 0;
    }
}
