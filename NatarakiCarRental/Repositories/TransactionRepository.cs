using System.Data;
using Dapper;
using NatarakiCarRental.Data;
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
                TransactionCode,
                FleetScheduleId,
                CustomerId,
                CarId,
                StartDate,
                EndDate,
                DailyRate,
                TotalDays,
                TotalAmount,
                ModeOfPayment,
                PaymentStatus,
                TransactionStatus,
                Notes,
                CreatedByUserId
            )
            OUTPUT INSERTED.TransactionId
            VALUES
            (
                @TransactionCode,
                @FleetScheduleId,
                @CustomerId,
                @CarId,
                @StartDate,
                @EndDate,
                @DailyRate,
                @TotalDays,
                @TotalAmount,
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
                transactions.TransactionCode,
                transactions.FleetScheduleId,
                transactions.CustomerId,
                transactions.CarId,
                CustomerName = LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))),
                cars.CarName,
                cars.PlateNumber,
                transactions.StartDate,
                transactions.EndDate,
                transactions.DailyRate,
                transactions.TotalDays,
                transactions.TotalAmount,
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

    public async Task<Transaction?> GetByCodeAsync(string transactionCode)
    {
        const string sql = """
            SELECT
                transactions.TransactionId,
                transactions.TransactionCode,
                transactions.FleetScheduleId,
                transactions.CustomerId,
                transactions.CarId,
                CustomerName = LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))),
                cars.CarName,
                cars.PlateNumber,
                transactions.StartDate,
                transactions.EndDate,
                transactions.DailyRate,
                transactions.TotalDays,
                transactions.TotalAmount,
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

    public async Task<IReadOnlyList<TransactionListItem>> SearchAsync(
        string searchText,
        string? transactionStatus,
        string? paymentStatus,
        int maxRows = 100)
    {
        string normalizedSearchText = searchText?.Trim() ?? string.Empty;
        const string sql = """
            SELECT TOP (@MaxRows)
                transactions.TransactionId,
                transactions.TransactionCode,
                transactions.FleetScheduleId,
                transactions.CustomerId,
                transactions.CarId,
                CustomerName = LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))),
                cars.CarName,
                cars.PlateNumber,
                transactions.StartDate,
                transactions.EndDate,
                transactions.TotalAmount,
                transactions.PaymentStatus,
                transactions.TransactionStatus,
                transactions.IsArchived
            FROM dbo.Transactions AS transactions
            INNER JOIN dbo.Customers AS customers ON customers.CustomerId = transactions.CustomerId
            INNER JOIN dbo.Cars AS cars ON cars.CarId = transactions.CarId
            WHERE transactions.IsArchived = 0
              AND (@TransactionStatus IS NULL OR transactions.TransactionStatus = @TransactionStatus)
              AND (@PaymentStatus IS NULL OR transactions.PaymentStatus = @PaymentStatus)
              AND (
                    @SearchText = N''
                    OR transactions.TransactionCode LIKE @SearchPattern
                    OR customers.FirstName LIKE @SearchPattern
                    OR customers.LastName LIKE @SearchPattern
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
                TransactionStatus = NullIfWhiteSpace(transactionStatus),
                PaymentStatus = NullIfWhiteSpace(paymentStatus)
            });
        return transactions.ToList();
    }

    public async Task<TransactionMetrics> GetMetricsAsync(DateTime referenceDate)
    {
        const string sql = """
            SELECT
                TotalTransactions = COUNT(CASE WHEN IsArchived = 0 THEN 1 END),
                ActiveTransactions = COUNT(CASE WHEN IsArchived = 0 AND TransactionStatus = N'Active' THEN 1 END),
                UnpaidTransactions = COUNT(CASE WHEN IsArchived = 0 AND PaymentStatus = N'Unpaid' THEN 1 END),
                CompletedTransactions = COUNT(CASE
                    WHEN IsArchived = 0
                     AND TransactionStatus = N'Completed'
                     AND YEAR(ISNULL(UpdatedAt, CreatedAt)) = YEAR(@ReferenceDate)
                     AND MONTH(ISNULL(UpdatedAt, CreatedAt)) = MONTH(@ReferenceDate)
                    THEN 1 END)
            FROM dbo.Transactions;
            """;

        using var connection = _connectionFactory.CreateConnection();
        TransactionMetrics? metrics = await connection.QuerySingleOrDefaultAsync<TransactionMetrics>(
            sql,
            new { ReferenceDate = referenceDate.Date });
        return metrics ?? new TransactionMetrics();
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
