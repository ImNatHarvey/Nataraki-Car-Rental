using System.Data;
using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Repositories;

public sealed class CustomerRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public CustomerRepository()
        : this(new DbConnectionFactory())
    {
    }

    public CustomerRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Customer?> GetCustomerByIdAsync(int customerId)
    {
        const string sql = """
            SELECT
                CustomerId,
                FirstName,
                LastName,
                Email,
                PhoneNumber,
                Region,
                Province,
                City,
                Barangay,
                StreetAddress,
                IsBlacklisted,
                BlacklistReason,
                IsArchived,
                DriverLicensePath,
                ProofOfBillingPath,
                CreatedAt,
                UpdatedAt,
                ArchivedAt
            FROM dbo.Customers
            WHERE CustomerId = @CustomerId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Customer>(sql, new { CustomerId = customerId });
    }

    public async Task<IReadOnlyList<Customer>> SearchCustomersAsync(string searchText, CustomerListFilter filter)
    {
        string normalizedSearchText = searchText?.Trim() ?? string.Empty;
        bool includeBlacklistReason = filter == CustomerListFilter.Blacklisted;

        const string sql = """
            SELECT
                CustomerId,
                FirstName,
                LastName,
                Email,
                PhoneNumber,
                Region,
                Province,
                City,
                Barangay,
                StreetAddress,
                IsBlacklisted,
                BlacklistReason,
                IsArchived,
                DriverLicensePath,
                ProofOfBillingPath,
                CreatedAt,
                UpdatedAt,
                ArchivedAt
            FROM dbo.Customers
            WHERE (
                    (@Filter = 0 AND IsArchived = 0 AND IsBlacklisted = 0)
                    OR (@Filter = 1 AND IsArchived = 0 AND IsBlacklisted = 1)
                    OR (@Filter = 2 AND IsArchived = 1)
                  )
              AND (
                    @SearchText = N''
                    OR FirstName LIKE @SearchPattern
                    OR LastName LIKE @SearchPattern
                    OR CONCAT(FirstName, N' ', LastName) LIKE @SearchPattern
                    OR Email LIKE @SearchPattern
                    OR PhoneNumber LIKE @SearchPattern
                    OR CONCAT_WS(
                        N' ',
                        Region,
                        Province,
                        City,
                        Barangay,
                        StreetAddress
                    ) LIKE @SearchPattern
                    OR (@IncludeBlacklistReason = 1 AND BlacklistReason LIKE @SearchPattern)
                  )
            ORDER BY CustomerId DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<Customer> customers = await connection.QueryAsync<Customer>(
            sql,
            new
            {
                Filter = (int)filter,
                IncludeBlacklistReason = includeBlacklistReason,
                SearchText = normalizedSearchText,
                SearchPattern = $"%{normalizedSearchText}%"
            });

        return customers.ToList();
    }

    public async Task<CustomerCounts> GetCustomerCountsAsync()
    {
        const string sql = """
            SELECT
                TotalCustomers = COUNT(CASE WHEN IsArchived = 0 THEN 1 END),
                ActiveCustomers = COUNT(CASE WHEN IsArchived = 0 AND IsBlacklisted = 0 THEN 1 END),
                BlacklistedCustomers = COUNT(CASE WHEN IsArchived = 0 AND IsBlacklisted = 1 THEN 1 END),
                ArchivedCustomers = COUNT(CASE WHEN IsArchived = 1 THEN 1 END)
            FROM dbo.Customers;
            """;

        using var connection = _connectionFactory.CreateConnection();
        CustomerCounts? counts = await connection.QuerySingleOrDefaultAsync<CustomerCounts>(sql);

        return counts ?? new CustomerCounts();
    }

    public async Task<IReadOnlyList<Customer>> GetRecentCustomersAsync(int take)
    {
        const string sql = """
            SELECT TOP (@Take)
                CustomerId,
                FirstName,
                LastName,
                Email,
                PhoneNumber,
                Region,
                Province,
                City,
                Barangay,
                StreetAddress,
                IsBlacklisted,
                BlacklistReason,
                IsArchived,
                DriverLicensePath,
                ProofOfBillingPath,
                CreatedAt,
                UpdatedAt,
                ArchivedAt
            FROM dbo.Customers
            WHERE IsArchived = 0
            ORDER BY CreatedAt DESC, CustomerId DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<Customer> customers = await connection.QueryAsync<Customer>(sql, new { Take = take });
        return customers.ToList();
    }

    public async Task<bool> PhoneNumberExistsAsync(string phoneNumber, int? excludingCustomerId = null)
    {
        string normalizedPhoneNumber = (phoneNumber ?? string.Empty).Trim();

        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Customers
            WHERE PhoneNumber = @PhoneNumber
              AND (@ExcludingCustomerId IS NULL OR CustomerId <> @ExcludingCustomerId);
            """;

        using var connection = _connectionFactory.CreateConnection();
        int count = await connection.ExecuteScalarAsync<int>(
            sql,
            new
            {
                PhoneNumber = normalizedPhoneNumber,
                ExcludingCustomerId = excludingCustomerId
            });

        return count > 0;
    }

    public async Task<int> AddCustomerAsync(Customer customer, IDbTransaction? transaction = null)
    {
        const string sql = """
            INSERT INTO dbo.Customers
            (
                FirstName,
                LastName,
                Email,
                PhoneNumber,
                Region,
                Province,
                City,
                Barangay,
                StreetAddress,
                IsBlacklisted,
                BlacklistReason,
                DriverLicensePath,
                ProofOfBillingPath
            )
            OUTPUT INSERTED.CustomerId
            VALUES
            (
                @FirstName,
                @LastName,
                @Email,
                @PhoneNumber,
                @Region,
                @Province,
                @City,
                @Barangay,
                @StreetAddress,
                @IsBlacklisted,
                @BlacklistReason,
                @DriverLicensePath,
                @ProofOfBillingPath
            );
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteScalarAsync<int>(
                sql,
                new
                {
                    customer.FirstName,
                    customer.LastName,
                    Email = NullIfWhiteSpace(customer.Email),
                    customer.PhoneNumber,
                    Region = NullIfWhiteSpace(customer.Region),
                    Province = NullIfWhiteSpace(customer.Province),
                    City = NullIfWhiteSpace(customer.City),
                    Barangay = NullIfWhiteSpace(customer.Barangay),
                    StreetAddress = NullIfWhiteSpace(customer.StreetAddress),
                    customer.IsBlacklisted,
                    BlacklistReason = NullIfWhiteSpace(customer.BlacklistReason),
                    DriverLicensePath = NullIfWhiteSpace(customer.DriverLicensePath),
                    ProofOfBillingPath = NullIfWhiteSpace(customer.ProofOfBillingPath)
                },
                transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<int> UpdateCustomerAsync(Customer customer, IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.Customers
            SET
                FirstName = @FirstName,
                LastName = @LastName,
                Email = @Email,
                PhoneNumber = @PhoneNumber,
                Region = @Region,
                Province = @Province,
                City = @City,
                Barangay = @Barangay,
                StreetAddress = @StreetAddress,
                IsBlacklisted = @IsBlacklisted,
                BlacklistReason = @BlacklistReason,
                DriverLicensePath = @DriverLicensePath,
                ProofOfBillingPath = @ProofOfBillingPath,
                UpdatedAt = sysdatetime()
            WHERE CustomerId = @CustomerId;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(
                sql,
                new
                {
                    customer.CustomerId,
                    customer.FirstName,
                    customer.LastName,
                    Email = NullIfWhiteSpace(customer.Email),
                    customer.PhoneNumber,
                    Region = NullIfWhiteSpace(customer.Region),
                    Province = NullIfWhiteSpace(customer.Province),
                    City = NullIfWhiteSpace(customer.City),
                    Barangay = NullIfWhiteSpace(customer.Barangay),
                    StreetAddress = NullIfWhiteSpace(customer.StreetAddress),
                    customer.IsBlacklisted,
                    BlacklistReason = NullIfWhiteSpace(customer.BlacklistReason),
                    DriverLicensePath = NullIfWhiteSpace(customer.DriverLicensePath),
                    ProofOfBillingPath = NullIfWhiteSpace(customer.ProofOfBillingPath)
                },
                transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<int> ArchiveCustomerAsync(int customerId, IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.Customers
            SET IsArchived = 1,
                ArchivedAt = sysdatetime(),
                UpdatedAt = sysdatetime()
            WHERE CustomerId = @CustomerId
              AND IsArchived = 0;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(sql, new { CustomerId = customerId }, transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<int> RestoreCustomerAsync(int customerId, IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.Customers
            SET IsArchived = 0,
                ArchivedAt = NULL,
                UpdatedAt = sysdatetime()
            WHERE CustomerId = @CustomerId
              AND IsArchived = 1;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(sql, new { CustomerId = customerId }, transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<int> ToggleBlacklistAsync(
        int customerId,
        bool isBlacklisted,
        string? blacklistReason = null,
        IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.Customers
            SET IsBlacklisted = @IsBlacklisted,
                BlacklistReason = @BlacklistReason,
                UpdatedAt = sysdatetime()
            WHERE CustomerId = @CustomerId
              AND IsArchived = 0;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(
                sql,
                new
                {
                    CustomerId = customerId,
                    IsBlacklisted = isBlacklisted,
                    BlacklistReason = isBlacklisted ? NullIfWhiteSpace(blacklistReason) : null
                },
                transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
