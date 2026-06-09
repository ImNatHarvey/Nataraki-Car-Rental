using System.Data;
using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Repositories;

public sealed class TransactionRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public TransactionRepository()
        : this(new DbConnectionFactory())
    {
    }

    public TransactionRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CreateAsync(Transaction transaction, IDbTransaction? dbTransaction = null)
    {
        const string sql = """
            INSERT INTO dbo.Transactions
            (
                TransactionType,
                TransactionCode,
                FleetScheduleId,
                CustomerId,
                CarId,
                StartDate,
                EndDate,
                DailyRate,
                TotalDays,
                TotalAmount,
                AmountPaid,
                BalanceAmount,
                ModeOfPayment,
                PaymentStatus,
                TransactionStatus,
                Notes,
                CreatedByUserId
            )
            OUTPUT INSERTED.TransactionId
            VALUES
            (
                @TransactionType,
                @TransactionCode,
                @FleetScheduleId,
                @CustomerId,
                @CarId,
                @StartDate,
                @EndDate,
                @DailyRate,
                @TotalDays,
                @TotalAmount,
                @AmountPaid,
                @BalanceAmount,
                @ModeOfPayment,
                @PaymentStatus,
                @TransactionStatus,
                @Notes,
                @CreatedByUserId
            );
            """;

        IDbConnection connection = dbTransaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteScalarAsync<int>(sql, transaction, dbTransaction);
        }
        finally
        {
            if (dbTransaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<Transaction?> GetByIdAsync(int transactionId, IDbTransaction? dbTransaction = null)
    {
        const string sql = """
            SELECT
                transactions.TransactionId,
                transactions.TransactionType,
                transactions.TransactionCode,
                transactions.FleetScheduleId,
                transactions.CustomerId,
                transactions.CarId,
                CustomerName = CASE 
                    WHEN customers.CustomerType = N'Maintenance' AND customers.CompanyName IS NOT NULL THEN customers.CompanyName
                    ELSE LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName)))
                END,
                CustomerPhone = customers.PhoneNumber,
                CustomerAddress = LTRIM(RTRIM(CONCAT(customers.StreetAddress, N' ', customers.Barangay, N' ', customers.City, N' ', customers.Province))),
                cars.CarName,
                cars.PlateNumber,
                transactions.StartDate,
                transactions.EndDate,
                transactions.DailyRate,
                transactions.TotalDays,
                transactions.TotalAmount,
                transactions.AmountPaid,
                transactions.BalanceAmount,
                transactions.ModeOfPayment,
                transactions.PaymentStatus,
                transactions.TransactionStatus,
                transactions.Notes,
                transactions.CreatedByUserId,
                transactions.CreatedAt,
                transactions.UpdatedAt,
                transactions.ArchivedAt,
                transactions.IsArchived
            FROM dbo.Transactions AS transactions
            INNER JOIN dbo.Customers AS customers ON customers.CustomerId = transactions.CustomerId
            INNER JOIN dbo.Cars AS cars ON cars.CarId = transactions.CarId
            WHERE transactions.TransactionId = @TransactionId;
            """;

        IDbConnection connection = dbTransaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.QuerySingleOrDefaultAsync<Transaction>(sql, new { TransactionId = transactionId }, dbTransaction);
        }
        finally
        {
            if (dbTransaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<Transaction?> GetByFleetScheduleIdAsync(int fleetScheduleId, IDbTransaction? dbTransaction = null)
    {
        const string sql = """
            SELECT TOP 1
                transactions.TransactionId,
                transactions.TransactionType,
                transactions.TransactionCode,
                transactions.FleetScheduleId,
                transactions.CustomerId,
                transactions.CarId,
                CustomerName = CASE 
                    WHEN customers.CustomerType = N'Maintenance' AND customers.CompanyName IS NOT NULL THEN customers.CompanyName
                    ELSE LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName)))
                END,
                CustomerPhone = customers.PhoneNumber,
                CustomerAddress = LTRIM(RTRIM(CONCAT(customers.StreetAddress, N' ', customers.Barangay, N' ', customers.City, N' ', customers.Province))),
                cars.CarName,
                cars.PlateNumber,
                transactions.StartDate,
                transactions.EndDate,
                transactions.DailyRate,
                transactions.TotalDays,
                transactions.TotalAmount,
                transactions.AmountPaid,
                transactions.BalanceAmount,
                transactions.ModeOfPayment,
                transactions.PaymentStatus,
                transactions.TransactionStatus,
                transactions.Notes,
                transactions.CreatedByUserId,
                transactions.CreatedAt,
                transactions.UpdatedAt,
                transactions.ArchivedAt,
                transactions.IsArchived
            FROM dbo.Transactions AS transactions
            INNER JOIN dbo.Customers AS customers ON customers.CustomerId = transactions.CustomerId
            INNER JOIN dbo.Cars AS cars ON cars.CarId = transactions.CarId
            WHERE transactions.FleetScheduleId = @FleetScheduleId
              AND transactions.IsArchived = 0
            ORDER BY transactions.TransactionId DESC;
            """;

        IDbConnection connection = dbTransaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.QuerySingleOrDefaultAsync<Transaction>(sql, new { FleetScheduleId = fleetScheduleId }, dbTransaction);
        }
        finally
        {
            if (dbTransaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<Transaction?> GetByCodeAsync(string transactionCode)
    {
        const string sql = """
            SELECT
                transactions.TransactionId,
                transactions.TransactionType,
                transactions.TransactionCode,
                transactions.FleetScheduleId,
                transactions.CustomerId,
                transactions.CarId,
                CustomerName = CASE 
                    WHEN customers.CustomerType = N'Maintenance' AND customers.CompanyName IS NOT NULL THEN customers.CompanyName
                    ELSE LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName)))
                END,
                CustomerPhone = customers.PhoneNumber,
                CustomerAddress = LTRIM(RTRIM(CONCAT(customers.StreetAddress, N' ', customers.Barangay, N' ', customers.City, N' ', customers.Province))),
                cars.CarName,
                cars.PlateNumber,
                transactions.StartDate,
                transactions.EndDate,
                transactions.DailyRate,
                transactions.TotalDays,
                transactions.TotalAmount,
                transactions.AmountPaid,
                transactions.BalanceAmount,
                transactions.ModeOfPayment,
                transactions.PaymentStatus,
                transactions.TransactionStatus,
                transactions.Notes,
                transactions.CreatedByUserId,
                transactions.CreatedAt,
                transactions.UpdatedAt,
                transactions.ArchivedAt,
                transactions.IsArchived
            FROM dbo.Transactions AS transactions
            INNER JOIN dbo.Customers AS customers ON customers.CustomerId = transactions.CustomerId
            INNER JOIN dbo.Cars AS cars ON cars.CarId = transactions.CarId
            WHERE transactions.TransactionCode = @TransactionCode;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Transaction>(sql, new { TransactionCode = transactionCode });
    }

    public async Task<bool> HasActiveForFleetScheduleAsync(int fleetScheduleId, IDbTransaction? dbTransaction = null)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM dbo.Transactions
                WHERE FleetScheduleId = @FleetScheduleId
                  AND IsArchived = 0
            ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END;
            """;

        IDbConnection connection = dbTransaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteScalarAsync<bool>(sql, new { FleetScheduleId = fleetScheduleId }, dbTransaction);
        }
        finally
        {
            if (dbTransaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<IReadOnlyList<TransactionListItem>> SearchAsync(
        string searchText,
        string? transactionStatus,
        string? paymentStatus,
        bool includeArchived = false,
        string? transactionType = null,
        int maxRows = 100)
    {
        string normalizedSearchText = searchText?.Trim() ?? string.Empty;
        const string sql = """
            SELECT TOP (@MaxRows)
                transactions.TransactionId,
                transactions.TransactionType,
                transactions.TransactionCode,
                transactions.FleetScheduleId,
                transactions.CustomerId,
                transactions.CarId,
                CustomerName = CASE 
                    WHEN customers.CustomerType = N'Maintenance' AND customers.CompanyName IS NOT NULL THEN customers.CompanyName
                    ELSE LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName)))
                END,
                CustomerPhone = customers.PhoneNumber,
                CustomerAddress = LTRIM(RTRIM(CONCAT(customers.StreetAddress, N' ', customers.Barangay, N' ', customers.City, N' ', customers.Province))),
                cars.CarName,
                cars.PlateNumber,
                transactions.StartDate,
                transactions.EndDate,
                transactions.TotalAmount,
                transactions.AmountPaid,
                transactions.BalanceAmount,
                transactions.PaymentStatus,
                transactions.TransactionStatus,
                transactions.IsArchived
            FROM dbo.Transactions AS transactions
            INNER JOIN dbo.Customers AS customers ON customers.CustomerId = transactions.CustomerId
            INNER JOIN dbo.Cars AS cars ON cars.CarId = transactions.CarId
            WHERE transactions.IsArchived = @IncludeArchived
              AND (@TransactionType IS NULL OR transactions.TransactionType = @TransactionType)
              AND (@TransactionStatus IS NULL OR transactions.TransactionStatus = @TransactionStatus)
              AND (@PaymentStatus IS NULL OR transactions.PaymentStatus = @PaymentStatus)
              AND (
                    @SearchText = N''
                    OR transactions.TransactionCode LIKE @SearchPattern
                    OR customers.FirstName LIKE @SearchPattern
                    OR customers.LastName LIKE @SearchPattern
                    OR customers.CompanyName LIKE @SearchPattern
                    OR CONCAT(customers.FirstName, N' ', customers.LastName) LIKE @SearchPattern
                    OR cars.CarName LIKE @SearchPattern
                    OR cars.PlateNumber LIKE @SearchPattern
                  )
            ORDER BY transactions.CreatedAt DESC, transactions.TransactionId DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<TransactionListItem> transactions = await connection.QueryAsync<TransactionListItem>(
            sql,
            new
            {
                MaxRows = Math.Clamp(maxRows, 1, 500),
                SearchText = normalizedSearchText,
                SearchPattern = $"%{normalizedSearchText}%",
                TransactionType = NullIfWhiteSpace(transactionType),
                TransactionStatus = NullIfWhiteSpace(transactionStatus),
                PaymentStatus = NullIfWhiteSpace(paymentStatus),
                IncludeArchived = includeArchived
            });
        return transactions.ToList();
    }

    public async Task<TransactionMetrics> GetMetricsAsync(DateTime referenceDate)
    {
        string sql = $"""
            SELECT
                TotalTransactions = COUNT(CASE WHEN IsArchived = 0 AND TransactionType = N'Rental' THEN 1 END),
                ActiveTransactions = COUNT(CASE WHEN IsArchived = 0 AND TransactionType = N'Rental' AND TransactionStatus = N'{TransactionConstants.Status.Active}' THEN 1 END),
                UnpaidTransactions = COUNT(CASE WHEN IsArchived = 0 AND TransactionType = N'Rental' AND PaymentStatus = N'{TransactionConstants.PaymentStatus.Unpaid}' THEN 1 END),
                CompletedTransactions = COUNT(CASE
                    WHEN IsArchived = 0
                     AND TransactionType = N'Rental'
                     AND TransactionStatus = N'{TransactionConstants.Status.Completed}'
                     AND YEAR(ISNULL(UpdatedAt, CreatedAt)) = YEAR(@ReferenceDate)
                     AND MONTH(ISNULL(UpdatedAt, CreatedAt)) = MONTH(@ReferenceDate)
                    THEN 1 END),
                MaintenanceTransactions = COUNT(CASE WHEN IsArchived = 0 AND TransactionType = N'Maintenance' AND TransactionStatus = N'Maintenance' THEN 1 END)
            FROM dbo.Transactions;
            """;

        using var connection = _connectionFactory.CreateConnection();
        TransactionMetrics? metrics = await connection.QuerySingleOrDefaultAsync<TransactionMetrics>(
            sql,
            new { ReferenceDate = referenceDate.Date });
        return metrics ?? new TransactionMetrics();
    }

    public async Task<IReadOnlyList<TransactionListItem>> GetRecentAsync(int take)
    {
        const string sql = """
            SELECT TOP (@Take)
                transactions.TransactionId,
                transactions.TransactionType,
                transactions.TransactionCode,
                transactions.FleetScheduleId,
                transactions.CustomerId,
                transactions.CarId,
                CustomerName = CASE 
                    WHEN customers.CustomerType = N'Maintenance' AND customers.CompanyName IS NOT NULL THEN customers.CompanyName
                    ELSE LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName)))
                END,
                CustomerPhone = customers.PhoneNumber,
                CustomerAddress = LTRIM(RTRIM(CONCAT(customers.StreetAddress, N' ', customers.Barangay, N' ', customers.City, N' ', customers.Province))),
                cars.CarName,
                cars.PlateNumber,
                transactions.StartDate,
                transactions.EndDate,
                transactions.TotalAmount,
                transactions.AmountPaid,
                transactions.BalanceAmount,
                transactions.PaymentStatus,
                transactions.TransactionStatus,
                transactions.IsArchived
            FROM dbo.Transactions AS transactions
            INNER JOIN dbo.Customers AS customers ON customers.CustomerId = transactions.CustomerId
            INNER JOIN dbo.Cars AS cars ON cars.CarId = transactions.CarId
            WHERE transactions.IsArchived = 0
              AND transactions.TransactionType = N'Rental'
            ORDER BY transactions.CreatedAt DESC, transactions.TransactionId DESC;
            """;
        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<TransactionListItem> rows = await connection.QueryAsync<TransactionListItem>(sql, new { Take = take });
        return rows.ToList();
    }

    public async Task<int> UpdateStatusAsync(int transactionId, string status, IDbTransaction? dbTransaction = null)
    {
        const string sql = """
            UPDATE dbo.Transactions
            SET TransactionStatus = @Status,
                UpdatedAt = sysdatetime()
            WHERE TransactionId = @TransactionId
              AND IsArchived = 0;
            """;

        IDbConnection connection = dbTransaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(sql, new { TransactionId = transactionId, Status = status }, dbTransaction);
        }
        finally
        {
            if (dbTransaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<int> UpdatePaymentSummaryAsync(
        int transactionId,
        decimal amountPaid,
        decimal balanceAmount,
        string paymentStatus,
        IDbTransaction? dbTransaction = null)
    {
        const string sql = """
            UPDATE dbo.Transactions
            SET AmountPaid = @AmountPaid,
                BalanceAmount = @BalanceAmount,
                PaymentStatus = @PaymentStatus,
                UpdatedAt = sysdatetime()
            WHERE TransactionId = @TransactionId
              AND IsArchived = 0;
            """;
        IDbConnection connection = dbTransaction?.Connection ?? _connectionFactory.CreateConnection();
        try
        {
            return await connection.ExecuteAsync(
                sql,
                new { TransactionId = transactionId, AmountPaid = amountPaid, BalanceAmount = balanceAmount, PaymentStatus = paymentStatus },
                dbTransaction);
        }
        finally
        {
            if (dbTransaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<int> UpdateInspectionDetailsAsync(
        int transactionId,
        string condition,
        string? notes,
        decimal additionalCharge,
        IDbTransaction? dbTransaction = null)
    {
        const string sql = """
            UPDATE dbo.Transactions
            SET ReturnCondition = @Condition,
                ReturnNotes = @Notes,
                AdditionalCharge = @AdditionalCharge,
                UpdatedAt = sysdatetime()
            WHERE TransactionId = @TransactionId;
            """;

        IDbConnection connection = dbTransaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(
                sql,
                new { TransactionId = transactionId, Condition = condition, Notes = notes, AdditionalCharge = additionalCharge },
                dbTransaction);
        }
        finally
        {
            if (dbTransaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<int> UpdateCommercialSummaryAsync(
        int transactionId,
        decimal totalAmount,
        decimal amountPaid,
        decimal balanceAmount,
        string paymentStatus,
        IDbTransaction? dbTransaction = null)
    {
        const string sql = """
            UPDATE dbo.Transactions
            SET TotalAmount = @TotalAmount,
                AmountPaid = @AmountPaid,
                BalanceAmount = @BalanceAmount,
                PaymentStatus = @PaymentStatus,
                UpdatedAt = sysdatetime()
            WHERE TransactionId = @TransactionId;
            """;

        IDbConnection connection = dbTransaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(
                sql,
                new
                {
                    TransactionId = transactionId,
                    TotalAmount = totalAmount,
                    AmountPaid = amountPaid,
                    BalanceAmount = balanceAmount,
                    PaymentStatus = paymentStatus
                },
                dbTransaction);
        }
        finally
        {
            if (dbTransaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<int> ArchiveAsync(int transactionId, IDbTransaction? dbTransaction = null)
    {
        const string sql = """
            UPDATE dbo.Transactions
            SET IsArchived = 1,
                ArchivedAt = sysdatetime(),
                UpdatedAt = sysdatetime()
            WHERE TransactionId = @TransactionId
              AND IsArchived = 0;
            """;

        IDbConnection connection = dbTransaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(sql, new { TransactionId = transactionId }, dbTransaction);
        }
        finally
        {
            if (dbTransaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<int> RestoreAsync(int transactionId, IDbTransaction? dbTransaction = null)
    {
        const string sql = """
            UPDATE dbo.Transactions
            SET IsArchived = 0,
                ArchivedAt = NULL,
                UpdatedAt = sysdatetime()
            WHERE TransactionId = @TransactionId
              AND IsArchived = 1;
            """;

        IDbConnection connection = dbTransaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(sql, new { TransactionId = transactionId }, dbTransaction);
        }
        finally
        {
            if (dbTransaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<bool> ExistsByTransactionCodeAsync(string transactionCode, IDbTransaction? dbTransaction = null)
    {
        const string sql = "SELECT COUNT(1) FROM dbo.Transactions WHERE TransactionCode = @TransactionCode;";
        IDbConnection connection = dbTransaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            int count = await connection.ExecuteScalarAsync<int>(sql, new { TransactionCode = transactionCode }, dbTransaction);
            return count > 0;
        }
        finally
        {
            if (dbTransaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<int> GetNextSequenceForYearAsync(int year, IDbTransaction dbTransaction)
    {
        const string sql = """
            SELECT ISNULL(MAX(TRY_CONVERT(int, RIGHT(TransactionCode, 6))), 0) + 1
            FROM dbo.Transactions WITH (UPDLOCK, HOLDLOCK)
            WHERE TransactionCode LIKE @Prefix + N'%';
            """;

        return await dbTransaction.Connection!.ExecuteScalarAsync<int>(
            sql,
            new { Prefix = $"TXN-{year}-" },
            dbTransaction);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
