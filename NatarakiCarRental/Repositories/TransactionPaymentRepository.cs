using System.Data;
using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Repositories;

public sealed class TransactionPaymentRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public TransactionPaymentRepository()
        : this(new DbConnectionFactory())
    {
    }

    public TransactionPaymentRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> AddAsync(TransactionPayment payment, IDbTransaction? transaction = null)
    {
        const string sql = """
            INSERT INTO dbo.TransactionPayments
            (
                TransactionId,
                PaymentDate,
                Amount,
                ModeOfPayment,
                PaymentCategory,
                ReferenceNumber,
                ReceiptFilePath,
                Notes,
                CreatedByUserId
            )
            VALUES
            (
                @TransactionId,
                @PaymentDate,
                @Amount,
                @ModeOfPayment,
                @PaymentCategory,
                @ReferenceNumber,
                @ReceiptFilePath,
                @Notes,
                @CreatedByUserId
            );
            SELECT CAST(SCOPE_IDENTITY() as int);
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteScalarAsync<int>(sql, payment, transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<IReadOnlyList<TransactionPaymentListItem>> GetByTransactionIdAsync(int transactionId)
    {
        const string sql = """
            SELECT
                payments.TransactionPaymentId,
                payments.TransactionId,
                payments.PaymentDate,
                payments.Amount,
                payments.ModeOfPayment,
                payments.PaymentCategory,
                payments.ReferenceNumber,
                payments.ReceiptFilePath,
                payments.Notes,
                CreatedByUserName = LTRIM(RTRIM(CONCAT(users.FirstName, N' ', users.LastName))),
                payments.IsArchived
            FROM dbo.TransactionPayments AS payments
            LEFT JOIN dbo.Users AS users ON users.UserId = payments.CreatedByUserId
            WHERE payments.TransactionId = @TransactionId
              AND payments.IsArchived = 0
            ORDER BY payments.PaymentDate DESC, payments.TransactionPaymentId DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<TransactionPaymentListItem> payments = await connection.QueryAsync<TransactionPaymentListItem>(sql, new { TransactionId = transactionId });
        return payments.ToList();
    }

    public async Task<decimal> GetTotalPaidAsync(int transactionId, IDbTransaction? transaction = null)
    {
        const string sql = """
            SELECT ISNULL(SUM(Amount), 0)
            FROM dbo.TransactionPayments
            WHERE TransactionId = @TransactionId
              AND IsArchived = 0;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteScalarAsync<decimal>(sql, new { TransactionId = transactionId }, transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<int> ArchiveAsync(int transactionPaymentId, IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.TransactionPayments
            SET IsArchived = 1
            WHERE TransactionPaymentId = @TransactionPaymentId;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(sql, new { TransactionPaymentId = transactionPaymentId }, transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }
}
