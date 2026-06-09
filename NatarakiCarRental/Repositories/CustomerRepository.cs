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

    public async Task<Customer?> GetCustomerByIdAsync(int customerId, IDbTransaction? transaction = null)
    {
        const string sql = """
            SELECT
                CustomerId,
                CustomerType,
                FirstName,
                LastName,
                CompanyName,
                ContactPerson,
                Email,
                PhoneNumber,
                Region,
                Province,
                City,
                Barangay,
                StreetAddress,
                IsBlacklisted,
                BlacklistReason,
                IsWalkIn,
                IsArchived,
                Notes,
                MaintenancePreferences,
                DriverLicensePath,
                ProofOfBillingPath,
                ValidIdFilePath,
                SelfieWithValidIdFilePath,
                CreatedAt,
                UpdatedAt,
                ArchivedAt
            FROM dbo.Customers
            WHERE CustomerId = @CustomerId;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.QuerySingleOrDefaultAsync<Customer>(sql, new { CustomerId = customerId }, transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<Customer?> GetCustomerByPhoneNumberAsync(string phoneNumber)
    {
        const string sql = """
            SELECT
                CustomerId,
                CustomerType,
                FirstName,
                LastName,
                CompanyName,
                ContactPerson,
                Email,
                PhoneNumber,
                Region,
                Province,
                City,
                Barangay,
                StreetAddress,
                IsBlacklisted,
                BlacklistReason,
                IsWalkIn,
                IsArchived,
                Notes,
                MaintenancePreferences,
                DriverLicensePath,
                ProofOfBillingPath,
                ValidIdFilePath,
                SelfieWithValidIdFilePath,
                CreatedAt,
                UpdatedAt,
                ArchivedAt
            FROM dbo.Customers
            WHERE PhoneNumber = @PhoneNumber;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Customer>(sql, new { PhoneNumber = phoneNumber });
    }

    public async Task<Customer> GetOrCreateWalkInCustomerAsync(IDbTransaction? transaction = null)
    {
        const string sql = """
            IF EXISTS (
                SELECT 1
                FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK)
                WHERE IsWalkIn = 1
            )
            BEGIN
                SELECT TOP 1
                    CustomerId,
                    CustomerType,
                    FirstName,
                    LastName,
                    CompanyName,
                    ContactPerson,
                    Email,
                    PhoneNumber,
                    Region,
                    Province,
                    City,
                    Barangay,
                    StreetAddress,
                    IsBlacklisted,
                    BlacklistReason,
                    IsWalkIn,
                    IsArchived,
                    Notes,
                    MaintenancePreferences,
                    DriverLicensePath,
                    ProofOfBillingPath,
                    ValidIdFilePath,
                    SelfieWithValidIdFilePath,
                    CreatedAt,
                    UpdatedAt,
                    ArchivedAt
                FROM dbo.Customers
                WHERE IsWalkIn = 1
                ORDER BY IsArchived, CustomerId;
            END
            ELSE
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK)
                    WHERE PhoneNumber = N'00000000000'
                )
                BEGIN
                    UPDATE dbo.Customers
                    SET IsWalkIn = 1,
                        UpdatedAt = sysdatetime()
                    WHERE CustomerId =
                    (
                        SELECT TOP 1 CustomerId
                        FROM dbo.Customers
                        WHERE PhoneNumber = N'00000000000'
                        ORDER BY IsArchived, CustomerId
                    );
                END
                ELSE
                BEGIN
                    INSERT INTO dbo.Customers
                    (
                        CustomerType,
                        FirstName,
                        LastName,
                        PhoneNumber,
                        IsBlacklisted,
                        BlacklistReason,
                        IsWalkIn,
                        IsArchived
                    )
                    VALUES
                    (
                        N'Rental',
                        N'Walk-In',
                        N'Customer',
                        N'00000000000',
                        0,
                        NULL,
                        1,
                        0
                    );
                END;

                SELECT TOP 1
                    CustomerId,
                    CustomerType,
                    FirstName,
                    LastName,
                    CompanyName,
                    ContactPerson,
                    Email,
                    PhoneNumber,
                    Region,
                    Province,
                    City,
                    Barangay,
                    StreetAddress,
                    IsBlacklisted,
                    BlacklistReason,
                    IsWalkIn,
                    IsArchived,
                    Notes,
                    MaintenancePreferences,
                    DriverLicensePath,
                    ProofOfBillingPath,
                    ValidIdFilePath,
                    SelfieWithValidIdFilePath,
                    CreatedAt,
                    UpdatedAt,
                    ArchivedAt
                FROM dbo.Customers
                WHERE IsWalkIn = 1
                ORDER BY IsArchived, CustomerId;
            END;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.QuerySingleAsync<Customer>(sql, transaction: transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<IReadOnlyList<Customer>> SearchCustomersAsync(string searchText, CustomerListFilter filter)
    {
        string normalizedSearchText = searchText?.Trim() ?? string.Empty;
        bool includeBlacklistReason = filter == CustomerListFilter.Blacklisted;

        const string sql = """
            SELECT
                CustomerId,
                CustomerType,
                FirstName,
                LastName,
                CompanyName,
                ContactPerson,
                Email,
                PhoneNumber,
                Region,
                Province,
                City,
                Barangay,
                StreetAddress,
                IsBlacklisted,
                BlacklistReason,
                IsWalkIn,
                IsArchived,
                Notes,
                MaintenancePreferences,
                DriverLicensePath,
                ProofOfBillingPath,
                ValidIdFilePath,
                SelfieWithValidIdFilePath,
                CreatedAt,
                UpdatedAt,
                ArchivedAt
            FROM dbo.Customers
            WHERE IsWalkIn = 0
              AND (
                    (@Filter = 0 AND IsArchived = 0 AND IsBlacklisted = 0 AND CustomerType = N'Rental')
                    OR (@Filter = 1 AND IsArchived = 0 AND IsBlacklisted = 1 AND CustomerType = N'Rental')
                    OR (@Filter = 2 AND IsArchived = 1)
                    OR (@Filter = 3 AND IsArchived = 0 AND IsBlacklisted = 0 AND CustomerType = N'Maintenance')
                  )
              AND (
                    @SearchText = N''
                    OR FirstName LIKE @SearchPattern
                    OR LastName LIKE @SearchPattern
                    OR CompanyName LIKE @SearchPattern
                    OR ContactPerson LIKE @SearchPattern
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
                TotalCustomers = COUNT(CASE WHEN IsArchived = 0 AND CustomerType = N'Rental' THEN 1 END),
                ActiveCustomers = COUNT(CASE WHEN IsArchived = 0 AND IsBlacklisted = 0 AND CustomerType = N'Rental' THEN 1 END),
                BlacklistedCustomers = COUNT(CASE WHEN IsArchived = 0 AND IsBlacklisted = 1 AND CustomerType = N'Rental' THEN 1 END),
                ArchivedCustomers = COUNT(CASE WHEN IsArchived = 1 THEN 1 END)
            FROM dbo.Customers
            WHERE IsWalkIn = 0;
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
                CustomerType,
                FirstName,
                LastName,
                CompanyName,
                ContactPerson,
                Email,
                PhoneNumber,
                Region,
                Province,
                City,
                Barangay,
                StreetAddress,
                IsBlacklisted,
                BlacklistReason,
                IsWalkIn,
                IsArchived,
                Notes,
                MaintenancePreferences,
                DriverLicensePath,
                ProofOfBillingPath,
                ValidIdFilePath,
                SelfieWithValidIdFilePath,
                CreatedAt,
                UpdatedAt,
                ArchivedAt
            FROM dbo.Customers
            WHERE IsArchived = 0
              AND IsWalkIn = 0
              AND CustomerType = N'Rental'
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
                CustomerType,
                FirstName,
                LastName,
                CompanyName,
                ContactPerson,
                Email,
                PhoneNumber,
                Region,
                Province,
                City,
                Barangay,
                StreetAddress,
                IsBlacklisted,
                BlacklistReason,
                Notes,
                MaintenancePreferences,
                DriverLicensePath,
                ProofOfBillingPath,
                ValidIdFilePath,
                SelfieWithValidIdFilePath
            )
            OUTPUT INSERTED.CustomerId
            VALUES
            (
                @CustomerType,
                @FirstName,
                @LastName,
                @CompanyName,
                @ContactPerson,
                @Email,
                @PhoneNumber,
                @Region,
                @Province,
                @City,
                @Barangay,
                @StreetAddress,
                @IsBlacklisted,
                @BlacklistReason,
                @Notes,
                @MaintenancePreferences,
                @DriverLicensePath,
                @ProofOfBillingPath,
                @ValidIdFilePath,
                @SelfieWithValidIdFilePath
            );
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteScalarAsync<int>(
                sql,
                new
                {
                    CustomerType = string.IsNullOrWhiteSpace(customer.CustomerType) ? "Rental" : customer.CustomerType,
                    FirstName = NullIfWhiteSpace(customer.FirstName),
                    LastName = NullIfWhiteSpace(customer.LastName),
                    CompanyName = NullIfWhiteSpace(customer.CompanyName),
                    ContactPerson = NullIfWhiteSpace(customer.ContactPerson),
                    Email = NullIfWhiteSpace(customer.Email),
                    customer.PhoneNumber,
                    Region = NullIfWhiteSpace(customer.Region),
                    Province = NullIfWhiteSpace(customer.Province),
                    City = NullIfWhiteSpace(customer.City),
                    Barangay = NullIfWhiteSpace(customer.Barangay),
                    StreetAddress = NullIfWhiteSpace(customer.StreetAddress),
                    customer.IsBlacklisted,
                    BlacklistReason = NullIfWhiteSpace(customer.BlacklistReason),
                    Notes = NullIfWhiteSpace(customer.Notes),
                    MaintenancePreferences = NullIfWhiteSpace(customer.MaintenancePreferences),
                    DriverLicensePath = NullIfWhiteSpace(customer.DriverLicensePath),
                    ProofOfBillingPath = NullIfWhiteSpace(customer.ProofOfBillingPath),
                    ValidIdFilePath = NullIfWhiteSpace(customer.ValidIdFilePath),
                    SelfieWithValidIdFilePath = NullIfWhiteSpace(customer.SelfieWithValidIdFilePath)
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
                CustomerType = @CustomerType,
                FirstName = @FirstName,
                LastName = @LastName,
                CompanyName = @CompanyName,
                ContactPerson = @ContactPerson,
                Email = @Email,
                PhoneNumber = @PhoneNumber,
                Region = @Region,
                Province = @Province,
                City = @City,
                Barangay = @Barangay,
                StreetAddress = @StreetAddress,
                IsBlacklisted = @IsBlacklisted,
                BlacklistReason = @BlacklistReason,
                Notes = @Notes,
                MaintenancePreferences = @MaintenancePreferences,
                DriverLicensePath = @DriverLicensePath,
                ProofOfBillingPath = @ProofOfBillingPath,
                ValidIdFilePath = @ValidIdFilePath,
                SelfieWithValidIdFilePath = @SelfieWithValidIdFilePath,
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
                    CustomerType = string.IsNullOrWhiteSpace(customer.CustomerType) ? "Rental" : customer.CustomerType,
                    FirstName = NullIfWhiteSpace(customer.FirstName),
                    LastName = NullIfWhiteSpace(customer.LastName),
                    CompanyName = NullIfWhiteSpace(customer.CompanyName),
                    ContactPerson = NullIfWhiteSpace(customer.ContactPerson),
                    Email = NullIfWhiteSpace(customer.Email),
                    customer.PhoneNumber,
                    Region = NullIfWhiteSpace(customer.Region),
                    Province = NullIfWhiteSpace(customer.Province),
                    City = NullIfWhiteSpace(customer.City),
                    Barangay = NullIfWhiteSpace(customer.Barangay),
                    StreetAddress = NullIfWhiteSpace(customer.StreetAddress),
                    customer.IsBlacklisted,
                    BlacklistReason = NullIfWhiteSpace(customer.BlacklistReason),
                    Notes = NullIfWhiteSpace(customer.Notes),
                    MaintenancePreferences = NullIfWhiteSpace(customer.MaintenancePreferences),
                    DriverLicensePath = NullIfWhiteSpace(customer.DriverLicensePath),
                    ProofOfBillingPath = NullIfWhiteSpace(customer.ProofOfBillingPath),
                    ValidIdFilePath = NullIfWhiteSpace(customer.ValidIdFilePath),
                    SelfieWithValidIdFilePath = NullIfWhiteSpace(customer.SelfieWithValidIdFilePath)
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
