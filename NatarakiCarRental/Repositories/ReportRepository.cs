using System.Data;
using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Repositories;

public sealed class ReportRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public ReportRepository() : this(new DbConnectionFactory()) { }

    public ReportRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ReportSummaryMetrics> GetSummaryMetricsAsync(DateTime from, DateTime to)
    {
        const string sql = """
            -- Revenue Metrics
            DECLARE @TotalRevenue decimal(18,2) = (
                SELECT ISNULL(SUM(Amount), 0) FROM dbo.TransactionPayments 
                WHERE IsArchived = 0 AND PaymentDate >= @From AND PaymentDate <= @To
            );
            
            DECLARE @RentalRevenue decimal(18,2) = (
                SELECT ISNULL(SUM(Amount), 0) FROM dbo.TransactionPayments 
                WHERE IsArchived = 0 AND PaymentCategory = N'Rental Payment' AND PaymentDate >= @From AND PaymentDate <= @To
            );
            
            DECLARE @ExtensionFees decimal(18,2) = (
                SELECT ISNULL(SUM(Amount), 0) FROM dbo.TransactionPayments 
                WHERE IsArchived = 0 AND PaymentCategory = N'Extension Fee' AND PaymentDate >= @From AND PaymentDate <= @To
            );
            
            DECLARE @DamageFees decimal(18,2) = (
                SELECT ISNULL(SUM(Amount), 0) FROM dbo.TransactionPayments 
                WHERE IsArchived = 0 AND PaymentCategory = N'Damage Fee' AND PaymentDate >= @From AND PaymentDate <= @To
            );
            
            DECLARE @LateReturnFees decimal(18,2) = (
                SELECT ISNULL(SUM(Amount), 0) FROM dbo.TransactionPayments 
                WHERE IsArchived = 0 AND PaymentCategory = N'Late Fee' AND PaymentDate >= @From AND PaymentDate <= @To
            );

            DECLARE @OutstandingBalance decimal(18,2) = (
                SELECT ISNULL(SUM(BalanceAmount), 0) FROM dbo.Transactions
                WHERE IsArchived = 0 AND CreatedAt >= @From AND CreatedAt <= @To
            );

            -- Transaction Metrics (based on CreatedAt)
            DECLARE @PaidTransactions int = (
                SELECT COUNT(1) FROM dbo.Transactions 
                WHERE IsArchived = 0 AND PaymentStatus = N'Paid' AND CreatedAt >= @From AND CreatedAt <= @To
            );
            
            DECLARE @PartialTransactions int = (
                SELECT COUNT(1) FROM dbo.Transactions 
                WHERE IsArchived = 0 AND PaymentStatus = N'Partial' AND CreatedAt >= @From AND CreatedAt <= @To
            );

            DECLARE @UnpaidTransactions int = (
                SELECT COUNT(1) FROM dbo.Transactions 
                WHERE IsArchived = 0 AND PaymentStatus = N'Unpaid' AND CreatedAt >= @From AND CreatedAt <= @To
            );

            -- Operational Metrics
            DECLARE @ActiveRentals int = (
                SELECT COUNT(1) FROM dbo.Transactions 
                WHERE IsArchived = 0 AND TransactionStatus = N'Active'
            );
            
            DECLARE @CompletedRentals int = (
                SELECT COUNT(1) FROM dbo.Transactions 
                WHERE IsArchived = 0 AND TransactionStatus = N'Completed' AND UpdatedAt >= @From AND UpdatedAt <= @To
            );

            -- Car Performance
            SELECT 
                TotalRevenue = @TotalRevenue,
                RentalRevenue = @RentalRevenue,
                ExtensionFees = @ExtensionFees,
                DamageFees = @DamageFees,
                LateReturnFees = @LateReturnFees,
                OutstandingBalance = @OutstandingBalance,
                PaidTransactions = @PaidTransactions,
                PartialTransactions = @PartialTransactions,
                UnpaidTransactions = @UnpaidTransactions,
                ActiveRentals = @ActiveRentals,
                CompletedRentals = @CompletedRentals,
                
                TopEarningCar = (
                    SELECT TOP 1 CONCAT(c.CarName, ' (', c.PlateNumber, ')')
                    FROM dbo.Cars c
                    JOIN dbo.Transactions t ON t.CarId = c.CarId
                    JOIN dbo.TransactionPayments p ON p.TransactionId = t.TransactionId
                    WHERE p.IsArchived = 0 AND p.PaymentDate >= @From AND p.PaymentDate <= @To
                    GROUP BY c.CarId, c.CarName, c.PlateNumber
                    ORDER BY SUM(p.Amount) DESC
                ),
                TopEarningCarRevenue = (
                    SELECT TOP 1 SUM(p.Amount)
                    FROM dbo.Cars c
                    JOIN dbo.Transactions t ON t.CarId = c.CarId
                    JOIN dbo.TransactionPayments p ON p.TransactionId = t.TransactionId
                    WHERE p.IsArchived = 0 AND p.PaymentDate >= @From AND p.PaymentDate <= @To
                    GROUP BY c.CarId
                    ORDER BY SUM(p.Amount) DESC
                ),
                MostRentedCar = (
                    SELECT TOP 1 CONCAT(c.CarName, ' (', c.PlateNumber, ')')
                    FROM dbo.Cars c
                    JOIN dbo.Transactions t ON t.CarId = c.CarId
                    WHERE t.IsArchived = 0 AND t.CreatedAt >= @From AND t.CreatedAt <= @To
                    GROUP BY c.CarId, c.CarName, c.PlateNumber
                    ORDER BY COUNT(t.TransactionId) DESC
                ),
                MostRentedCarCount = (
                    SELECT TOP 1 COUNT(t.TransactionId)
                    FROM dbo.Cars c
                    JOIN dbo.Transactions t ON t.CarId = c.CarId
                    WHERE t.IsArchived = 0 AND t.CreatedAt >= @From AND t.CreatedAt <= @To
                    GROUP BY c.CarId
                    ORDER BY COUNT(t.TransactionId) DESC
                );
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ReportSummaryMetrics>(sql, new { From = from, To = to }) 
            ?? new ReportSummaryMetrics();
    }

    public async Task<IReadOnlyList<PaymentMethodBreakdownItem>> GetPaymentMethodBreakdownAsync(DateTime from, DateTime to)
    {
        const string sql = """
            DECLARE @TotalRangeRevenue decimal(18,2) = (
                SELECT ISNULL(SUM(Amount), 0) FROM dbo.TransactionPayments 
                WHERE IsArchived = 0 AND PaymentDate >= @From AND PaymentDate <= @To
            );

            SELECT 
                ModeOfPayment,
                TotalAmount = SUM(Amount),
                PaymentCount = COUNT(1),
                Percentage = CASE WHEN @TotalRangeRevenue > 0 THEN (SUM(Amount) / @TotalRangeRevenue) * 100 ELSE 0 END
            FROM dbo.TransactionPayments
            WHERE IsArchived = 0 AND PaymentDate >= @From AND PaymentDate <= @To
            GROUP BY ModeOfPayment
            ORDER BY TotalAmount DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<PaymentMethodBreakdownItem>(sql, new { From = from, To = to });
        return results.ToList();
    }

    public async Task<IReadOnlyList<RevenueByCategoryItem>> GetRevenueByCategoryAsync(DateTime from, DateTime to)
    {
        const string sql = """
            DECLARE @TotalRangeRevenue decimal(18,2) = (
                SELECT ISNULL(SUM(Amount), 0) FROM dbo.TransactionPayments 
                WHERE IsArchived = 0 AND PaymentDate >= @From AND PaymentDate <= @To
            );

            SELECT 
                PaymentCategory,
                TotalAmount = SUM(Amount),
                PaymentCount = COUNT(1),
                Percentage = CASE WHEN @TotalRangeRevenue > 0 THEN (SUM(Amount) / @TotalRangeRevenue) * 100 ELSE 0 END
            FROM dbo.TransactionPayments
            WHERE IsArchived = 0 AND PaymentDate >= @From AND PaymentDate <= @To
            GROUP BY PaymentCategory
            ORDER BY TotalAmount DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<RevenueByCategoryItem>(sql, new { From = from, To = to });
        return results.ToList();
    }

    public async Task<IReadOnlyList<TransactionListItem>> GetOutstandingTransactionsAsync(DateTime from, DateTime to)
    {
        const string sql = """
            SELECT 
                t.TransactionId,
                t.TransactionCode,
                CustomerName = LTRIM(RTRIM(CONCAT(cu.FirstName, N' ', cu.LastName))),
                CarName = c.CarName,
                c.PlateNumber,
                t.StartDate,
                t.EndDate,
                t.TotalAmount,
                t.AmountPaid,
                t.BalanceAmount,
                t.PaymentStatus,
                t.TransactionStatus
            FROM dbo.Transactions t
            JOIN dbo.Customers cu ON cu.CustomerId = t.CustomerId
            JOIN dbo.Cars c ON c.CarId = t.CarId
            WHERE t.IsArchived = 0 
              AND t.PaymentStatus IN (N'Partial', N'Unpaid')
              AND t.CreatedAt >= @From AND t.CreatedAt <= @To
            ORDER BY t.BalanceAmount DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<TransactionListItem>(sql, new { From = from, To = to });
        return results.ToList();
    }

    public async Task<IReadOnlyList<TopCarItem>> GetRevenueByCarAsync(DateTime from, DateTime to, int limit)
    {
        const string sql = """
            SELECT TOP (@Limit)
                c.CarName,
                c.PlateNumber,
                Revenue = SUM(p.Amount),
                RentalCount = COUNT(DISTINCT t.TransactionId),
                AverageRevenue = CASE WHEN COUNT(DISTINCT t.TransactionId) > 0 THEN SUM(p.Amount) / COUNT(DISTINCT t.TransactionId) ELSE 0 END
            FROM dbo.Cars c
            JOIN dbo.Transactions t ON t.CarId = c.CarId
            JOIN dbo.TransactionPayments p ON p.TransactionId = t.TransactionId
            WHERE p.IsArchived = 0 AND p.PaymentDate >= @From AND p.PaymentDate <= @To
            GROUP BY c.CarId, c.CarName, c.PlateNumber
            ORDER BY Revenue DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<TopCarItem>(sql, new { From = from, To = to, Limit = limit });
        return results.ToList();
    }

    public async Task<IReadOnlyList<RevenueByCustomerItem>> GetRevenueByCustomerAsync(DateTime from, DateTime to, int limit)
    {
        const string sql = """
            SELECT TOP (@Limit)
                CustomerName = LTRIM(RTRIM(CONCAT(cu.FirstName, N' ', cu.LastName))),
                TransactionCount = COUNT(DISTINCT t.TransactionId),
                TotalPaid = SUM(p.Amount),
                OutstandingBalance = (
                    SELECT ISNULL(SUM(t2.BalanceAmount), 0)
                    FROM dbo.Transactions t2
                    WHERE t2.CustomerId = cu.CustomerId AND t2.IsArchived = 0
                      AND t2.CreatedAt >= @From AND t2.CreatedAt <= @To
                )
            FROM dbo.Customers cu
            JOIN dbo.Transactions t ON t.CustomerId = cu.CustomerId
            JOIN dbo.TransactionPayments p ON p.TransactionId = t.TransactionId
            WHERE p.IsArchived = 0 AND p.PaymentDate >= @From AND p.PaymentDate <= @To
            GROUP BY cu.CustomerId, cu.FirstName, cu.LastName
            ORDER BY TotalPaid DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<RevenueByCustomerItem>(sql, new { From = from, To = to, Limit = limit });
        return results.ToList();
    }

    public async Task<IReadOnlyList<TransactionStatusBreakdownItem>> GetTransactionStatusBreakdownAsync(DateTime from, DateTime to)
    {
        const string sql = """
            SELECT 
                [Status] = TransactionStatus,
                [Count] = COUNT(1)
            FROM dbo.Transactions
            WHERE IsArchived = 0 AND CreatedAt >= @From AND CreatedAt <= @To
            GROUP BY TransactionStatus
            ORDER BY [Count] DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<TransactionStatusBreakdownItem>(sql, new { From = from, To = to });
        return results.ToList();
    }

    public async Task<IReadOnlyList<TopCarItem>> GetTopCarsByRevenueAsync(DateTime from, DateTime to, int limit)
    {
        const string sql = """
            SELECT TOP (@Limit)
                c.CarName,
                c.PlateNumber,
                Revenue = SUM(p.Amount),
                RentalCount = COUNT(DISTINCT t.TransactionId)
            FROM dbo.Cars c
            JOIN dbo.Transactions t ON t.CarId = c.CarId
            JOIN dbo.TransactionPayments p ON p.TransactionId = t.TransactionId
            WHERE p.IsArchived = 0 AND p.PaymentDate >= @From AND p.PaymentDate <= @To
            GROUP BY c.CarId, c.CarName, c.PlateNumber
            ORDER BY Revenue DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<TopCarItem>(sql, new { From = from, To = to, Limit = limit });
        return results.ToList();
    }

    public async Task<IReadOnlyList<TopCarItem>> GetMostRentedCarsAsync(DateTime from, DateTime to, int limit)
    {
        const string sql = """
            SELECT TOP (@Limit)
                c.CarName,
                c.PlateNumber,
                Revenue = (
                    SELECT ISNULL(SUM(p2.Amount), 0)
                    FROM dbo.TransactionPayments p2
                    JOIN dbo.Transactions t2 ON t2.TransactionId = p2.TransactionId
                    WHERE t2.CarId = c.CarId AND p2.IsArchived = 0 AND p2.PaymentDate >= @From AND p2.PaymentDate <= @To
                ),
                RentalCount = COUNT(t.TransactionId)
            FROM dbo.Cars c
            JOIN dbo.Transactions t ON t.CarId = c.CarId
            WHERE t.IsArchived = 0 AND t.CreatedAt >= @From AND t.CreatedAt <= @To
            GROUP BY c.CarId, c.CarName, c.PlateNumber
            ORDER BY RentalCount DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<TopCarItem>(sql, new { From = from, To = to, Limit = limit });
        return results.ToList();
    }
}
