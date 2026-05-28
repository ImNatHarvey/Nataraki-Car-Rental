using System.Data;
using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Helpers;
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
        string sql = $"""
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
                WHERE IsArchived = 0 AND PaymentStatus = N'{TransactionConstants.PaymentStatus.Paid}' AND CreatedAt >= @From AND CreatedAt <= @To
            );
            
            DECLARE @PartialTransactions int = (
                SELECT COUNT(1) FROM dbo.Transactions 
                WHERE IsArchived = 0 AND PaymentStatus = N'{TransactionConstants.PaymentStatus.Partial}' AND CreatedAt >= @From AND CreatedAt <= @To
            );

            DECLARE @UnpaidTransactions int = (
                SELECT COUNT(1) FROM dbo.Transactions 
                WHERE IsArchived = 0 AND PaymentStatus = N'{TransactionConstants.PaymentStatus.Unpaid}' AND CreatedAt >= @From AND CreatedAt <= @To
            );

            -- Operational Metrics
            DECLARE @ActiveRentals int = (
                SELECT COUNT(1) FROM dbo.Transactions 
                WHERE IsArchived = 0 AND TransactionStatus = N'{TransactionConstants.Status.Active}'
            );
            
            DECLARE @CompletedRentals int = (
                SELECT COUNT(1) FROM dbo.Transactions 
                WHERE IsArchived = 0 AND TransactionStatus = N'{TransactionConstants.Status.Completed}' AND UpdatedAt >= @From AND UpdatedAt <= @To
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
        string sql = $"""
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
              AND t.PaymentStatus IN (N'{TransactionConstants.PaymentStatus.Partial}', N'{TransactionConstants.PaymentStatus.Unpaid}')
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

    public async Task<FleetPerformanceMetrics> GetFleetPerformanceMetricsAsync(DateTime from, DateTime to)
    {
        string sql = $"""
            DECLARE @RangeDays int = DATEDIFF(day, CONVERT(date, @From), CONVERT(date, @To)) + 1;

            WITH ActiveCars AS
            (
                SELECT CarId, CarName, PlateNumber
                FROM dbo.Cars
                WHERE IsArchived = 0
            ),
            RentalSchedules AS
            (
                SELECT
                    CarId,
                    RentedDays = DATEDIFF(
                        day,
                        CASE WHEN StartDate < CONVERT(date, @From) THEN CONVERT(date, @From) ELSE StartDate END,
                        CASE WHEN EndDate > CONVERT(date, @To) THEN CONVERT(date, @To) ELSE EndDate END
                    ) + 1
                FROM dbo.FleetSchedules
                WHERE IsArchived = 0
                  AND ScheduleType = N'{FleetScheduleConstants.Type.Rental}'
                  AND Status IN (N'{FleetScheduleConstants.Status.Rented}', N'{FleetScheduleConstants.Status.Completed}')
                  AND StartDate <= CONVERT(date, @To)
                  AND EndDate >= CONVERT(date, @From)
            ),
            Utilization AS
            (
                SELECT
                    cars.CarId,
                    RentedDays = ISNULL(SUM(schedules.RentedDays), 0)
                FROM ActiveCars AS cars
                LEFT JOIN RentalSchedules AS schedules ON schedules.CarId = cars.CarId
                GROUP BY cars.CarId
            ),
            Revenue AS
            (
                SELECT
                    transactions.CarId,
                    TotalRevenue = SUM(payments.Amount),
                    RentalCount = COUNT(DISTINCT transactions.TransactionId)
                FROM dbo.Transactions AS transactions
                INNER JOIN dbo.TransactionPayments AS payments
                    ON payments.TransactionId = transactions.TransactionId
                WHERE transactions.IsArchived = 0
                  AND payments.IsArchived = 0
                  AND payments.PaymentDate >= @From
                  AND payments.PaymentDate <= @To
                GROUP BY transactions.CarId
            ),
            Rentals AS
            (
                SELECT CarId, RentalCount = COUNT(1)
                FROM dbo.Transactions
                WHERE IsArchived = 0
                  AND StartDate <= CONVERT(date, @To)
                  AND EndDate >= CONVERT(date, @From)
                  AND TransactionStatus IN (N'{TransactionConstants.Status.Active}', N'{TransactionConstants.Status.Completed}')
                GROUP BY CarId
            )
            SELECT
                TotalFleetRevenue = ISNULL((SELECT SUM(TotalRevenue) FROM Revenue), 0),
                AverageRevenuePerCar = CASE
                    WHEN (SELECT COUNT(1) FROM ActiveCars) > 0
                        THEN ISNULL((SELECT SUM(TotalRevenue) FROM Revenue), 0) / (SELECT COUNT(1) FROM ActiveCars)
                    ELSE 0
                END,
                TopEarningCar = (
                    SELECT TOP 1 CONCAT(cars.CarName, N' (', cars.PlateNumber, N')')
                    FROM ActiveCars AS cars
                    INNER JOIN Revenue AS revenue ON revenue.CarId = cars.CarId
                    ORDER BY revenue.TotalRevenue DESC, cars.CarName
                ),
                TopEarningCarRevenue = ISNULL((SELECT TOP 1 TotalRevenue FROM Revenue ORDER BY TotalRevenue DESC), 0),
                MostRentedCar = (
                    SELECT TOP 1 CONCAT(cars.CarName, N' (', cars.PlateNumber, N')')
                    FROM ActiveCars AS cars
                    INNER JOIN Rentals AS rentals ON rentals.CarId = cars.CarId
                    ORDER BY rentals.RentalCount DESC, cars.CarName
                ),
                MostRentedCarCount = ISNULL((SELECT TOP 1 RentalCount FROM Rentals ORDER BY RentalCount DESC), 0),
                AverageUtilizationRate = CASE
                    WHEN @RangeDays > 0 AND (SELECT COUNT(1) FROM ActiveCars) > 0
                        THEN ISNULL((SELECT SUM(RentedDays) FROM Utilization), 0) * 100.0 / (@RangeDays * (SELECT COUNT(1) FROM ActiveCars))
                    ELSE 0
                END,
                ActiveRentals = (
                    SELECT COUNT(1)
                    FROM dbo.Transactions
                    WHERE IsArchived = 0
                      AND TransactionStatus = N'{TransactionConstants.Status.Active}'
                      AND StartDate <= CONVERT(date, @To)
                      AND EndDate >= CONVERT(date, @From)
                ),
                CompletedRentals = (
                    SELECT COUNT(1)
                    FROM dbo.Transactions
                    WHERE IsArchived = 0
                      AND TransactionStatus = N'{TransactionConstants.Status.Completed}'
                      AND StartDate <= CONVERT(date, @To)
                      AND EndDate >= CONVERT(date, @From)
                ),
                CarsUnderMaintenance = (
                    SELECT COUNT(DISTINCT CarId)
                    FROM dbo.FleetSchedules
                    WHERE IsArchived = 0
                      AND ScheduleType = N'{FleetScheduleConstants.Type.Maintenance}'
                      AND Status = N'{FleetScheduleConstants.Status.Ongoing}'
                      AND StartDate <= CONVERT(date, @To)
                      AND EndDate >= CONVERT(date, @From)
                );
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<FleetPerformanceMetrics>(sql, new { From = from, To = to })
            ?? new FleetPerformanceMetrics();
    }

    public async Task<IReadOnlyList<FleetUtilizationItem>> GetFleetUtilizationAsync(DateTime from, DateTime to)
    {
        string sql = $"""
            DECLARE @RangeDays int = DATEDIFF(day, CONVERT(date, @From), CONVERT(date, @To)) + 1;

            WITH RentalSchedules AS
            (
                SELECT
                    CarId,
                    RentedDays = DATEDIFF(
                        day,
                        CASE WHEN StartDate < CONVERT(date, @From) THEN CONVERT(date, @From) ELSE StartDate END,
                        CASE WHEN EndDate > CONVERT(date, @To) THEN CONVERT(date, @To) ELSE EndDate END
                    ) + 1
                FROM dbo.FleetSchedules
                WHERE IsArchived = 0
                  AND ScheduleType = N'{FleetScheduleConstants.Type.Rental}'
                  AND Status IN (N'{FleetScheduleConstants.Status.Rented}', N'{FleetScheduleConstants.Status.Completed}')
                  AND StartDate <= CONVERT(date, @To)
                  AND EndDate >= CONVERT(date, @From)
            ),
            RentalCounts AS
            (
                SELECT CarId, RentalCount = COUNT(1)
                FROM dbo.Transactions
                WHERE IsArchived = 0
                  AND StartDate <= CONVERT(date, @To)
                  AND EndDate >= CONVERT(date, @From)
                  AND TransactionStatus IN (N'{TransactionConstants.Status.Active}', N'{TransactionConstants.Status.Completed}')
                GROUP BY CarId
            )
            SELECT
                cars.CarName,
                cars.PlateNumber,
                RentedDays = ISNULL(SUM(schedules.RentedDays), 0),
                AvailableDays = @RangeDays,
                UtilizationRate = CASE
                    WHEN @RangeDays > 0 THEN ISNULL(SUM(schedules.RentedDays), 0) * 100.0 / @RangeDays
                    ELSE 0
                END,
                RentalCount = ISNULL(MAX(counts.RentalCount), 0),
                cars.Status
            FROM dbo.Cars AS cars
            LEFT JOIN RentalSchedules AS schedules ON schedules.CarId = cars.CarId
            LEFT JOIN RentalCounts AS counts ON counts.CarId = cars.CarId
            WHERE cars.IsArchived = 0
            GROUP BY cars.CarId, cars.CarName, cars.PlateNumber, cars.Status
            ORDER BY UtilizationRate DESC, RentedDays DESC, cars.CarName;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<FleetUtilizationItem>(sql, new { From = from, To = to });
        return results.ToList();
    }

    public async Task<IReadOnlyList<FleetRevenuePerCarItem>> GetFleetRevenuePerCarAsync(DateTime from, DateTime to)
    {
        const string sql = """
            SELECT
                cars.CarName,
                cars.PlateNumber,
                RentalRevenue = ISNULL(SUM(CASE WHEN payments.PaymentCategory = N'Rental Payment' THEN payments.Amount ELSE 0 END), 0),
                ExtensionFees = ISNULL(SUM(CASE WHEN payments.PaymentCategory = N'Extension Fee' THEN payments.Amount ELSE 0 END), 0),
                DamageFees = ISNULL(SUM(CASE WHEN payments.PaymentCategory = N'Damage Fee' THEN payments.Amount ELSE 0 END), 0),
                LateFees = ISNULL(SUM(CASE WHEN payments.PaymentCategory = N'Late Fee' THEN payments.Amount ELSE 0 END), 0),
                TotalRevenue = ISNULL(SUM(payments.Amount), 0),
                AverageRevenuePerRental = CASE
                    WHEN COUNT(DISTINCT transactions.TransactionId) > 0
                        THEN ISNULL(SUM(payments.Amount), 0) / COUNT(DISTINCT transactions.TransactionId)
                    ELSE 0
                END
            FROM dbo.Cars AS cars
            LEFT JOIN dbo.Transactions AS transactions
                ON transactions.CarId = cars.CarId
               AND transactions.IsArchived = 0
            LEFT JOIN dbo.TransactionPayments AS payments
                ON payments.TransactionId = transactions.TransactionId
               AND payments.IsArchived = 0
               AND payments.PaymentDate >= @From
               AND payments.PaymentDate <= @To
            WHERE cars.IsArchived = 0
            GROUP BY cars.CarId, cars.CarName, cars.PlateNumber
            ORDER BY TotalRevenue DESC, cars.CarName;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<FleetRevenuePerCarItem>(sql, new { From = from, To = to });
        return results.ToList();
    }

    public async Task<IReadOnlyList<TopCarItem>> GetLeastUsedCarsAsync(DateTime from, DateTime to, int limit)
    {
        string sql = $"""
            SELECT TOP (@Limit)
                cars.CarName,
                cars.PlateNumber,
                Revenue = ISNULL(SUM(payments.Amount), 0),
                RentalCount = COUNT(DISTINCT transactions.TransactionId),
                AverageRevenue = CASE
                    WHEN COUNT(DISTINCT transactions.TransactionId) > 0
                        THEN ISNULL(SUM(payments.Amount), 0) / COUNT(DISTINCT transactions.TransactionId)
                    ELSE 0
                END
            FROM dbo.Cars AS cars
            LEFT JOIN dbo.Transactions AS transactions
                ON transactions.CarId = cars.CarId
               AND transactions.IsArchived = 0
               AND transactions.StartDate <= CONVERT(date, @To)
               AND transactions.EndDate >= CONVERT(date, @From)
               AND transactions.TransactionStatus IN (N'{TransactionConstants.Status.Active}', N'{TransactionConstants.Status.Completed}')
            LEFT JOIN dbo.TransactionPayments AS payments
                ON payments.TransactionId = transactions.TransactionId
               AND payments.IsArchived = 0
               AND payments.PaymentDate >= @From
               AND payments.PaymentDate <= @To
            WHERE cars.IsArchived = 0
            GROUP BY cars.CarId, cars.CarName, cars.PlateNumber
            ORDER BY RentalCount ASC, Revenue ASC, cars.CarName;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<TopCarItem>(sql, new { From = from, To = to, Limit = limit });
        return results.ToList();
    }

    public async Task<IReadOnlyList<FleetMaintenanceItem>> GetCarsUnderMaintenanceAsync(DateTime from, DateTime to)
    {
        string sql = $"""
            SELECT
                cars.CarName,
                cars.PlateNumber,
                schedules.Title,
                schedules.StartDate,
                schedules.EndDate,
                schedules.Status
            FROM dbo.FleetSchedules AS schedules
            INNER JOIN dbo.Cars AS cars ON cars.CarId = schedules.CarId
            WHERE schedules.IsArchived = 0
              AND cars.IsArchived = 0
              AND schedules.ScheduleType = N'{FleetScheduleConstants.Type.Maintenance}'
              AND schedules.Status = N'{FleetScheduleConstants.Status.Ongoing}'
              AND schedules.StartDate <= CONVERT(date, @To)
              AND schedules.EndDate >= CONVERT(date, @From)
            ORDER BY schedules.StartDate, cars.CarName;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<FleetMaintenanceItem>(sql, new { From = from, To = to });
        return results.ToList();
    }

    public async Task<OperationsMetrics> GetOperationsMetricsAsync(DateTime from, DateTime to)
    {
        string sql = $"""
            SELECT
                UpcomingReturns = (
                    SELECT COUNT(1)
                    FROM dbo.Transactions
                    WHERE IsArchived = 0
                      AND TransactionStatus = N'{TransactionConstants.Status.Active}'
                      AND EndDate >= CONVERT(date, @From)
                      AND EndDate <= CONVERT(date, @To)
                ),
                LateReturns = (
                    SELECT COUNT(1)
                    FROM dbo.Transactions
                    WHERE IsArchived = 0
                      AND TransactionStatus = N'{TransactionConstants.Status.Active}'
                      AND EndDate < CONVERT(date, @Today)
                ),
                ActiveRentals = (
                    SELECT COUNT(1)
                    FROM dbo.Transactions
                    WHERE IsArchived = 0
                      AND TransactionStatus = N'{TransactionConstants.Status.Active}'
                      AND StartDate <= CONVERT(date, @To)
                      AND EndDate >= CONVERT(date, @From)
                ),
                UpcomingReservations = (
                    SELECT COUNT(1)
                    FROM dbo.FleetSchedules
                    WHERE IsArchived = 0
                      AND ScheduleType = N'{FleetScheduleConstants.Type.Reservation}'
                      AND Status IN (N'{FleetScheduleConstants.Status.Pending}', N'{FleetScheduleConstants.Status.Reserved}')
                      AND StartDate >= CONVERT(date, @From)
                      AND StartDate <= CONVERT(date, @To)
                ),
                ReservedCars = (
                    SELECT COUNT(DISTINCT CarId)
                    FROM dbo.FleetSchedules
                    WHERE IsArchived = 0
                      AND ScheduleType = N'{FleetScheduleConstants.Type.Reservation}'
                      AND Status = N'{FleetScheduleConstants.Status.Reserved}'
                      AND StartDate <= CONVERT(date, @To)
                      AND EndDate >= CONVERT(date, @From)
                ),
                CarsUnderMaintenance = (
                    SELECT COUNT(DISTINCT CarId)
                    FROM dbo.FleetSchedules
                    WHERE IsArchived = 0
                      AND ScheduleType = N'{FleetScheduleConstants.Type.Maintenance}'
                      AND Status = N'{FleetScheduleConstants.Status.Ongoing}'
                      AND StartDate <= CONVERT(date, @To)
                      AND EndDate >= CONVERT(date, @From)
                ),
                AvailableCars = (
                    SELECT COUNT(1)
                    FROM dbo.Cars AS cars
                    WHERE cars.IsArchived = 0
                      AND NOT EXISTS (
                            SELECT 1
                            FROM dbo.FleetSchedules AS schedules
                            WHERE schedules.CarId = cars.CarId
                              AND schedules.IsArchived = 0
                              AND schedules.Status IN (N'{FleetScheduleConstants.Status.Pending}', N'{FleetScheduleConstants.Status.Reserved}', N'{FleetScheduleConstants.Status.Rented}', N'{FleetScheduleConstants.Status.Ongoing}')
                              AND schedules.StartDate <= CONVERT(date, @To)
                              AND schedules.EndDate >= CONVERT(date, @From)
                      )
                ),
                CompletedReturns = (
                    SELECT COUNT(1)
                    FROM dbo.Transactions
                    WHERE IsArchived = 0
                      AND TransactionStatus = N'{TransactionConstants.Status.Completed}'
                      AND EndDate >= CONVERT(date, @From)
                      AND EndDate <= CONVERT(date, @To)
                );
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OperationsMetrics>(
            sql,
            new { From = from, To = to, Today = DateTime.Today })
            ?? new OperationsMetrics();
    }

    public async Task<IReadOnlyList<OperationsReturnItem>> GetUpcomingReturnsAsync(DateTime from, DateTime to)
    {
        string sql = $"""
            SELECT
                ExpectedReturn = transactions.EndDate,
                transactions.TransactionCode,
                CustomerName = LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))),
                Contact = customers.PhoneNumber,
                cars.CarName,
                cars.PlateNumber,
                transactions.PaymentStatus
            FROM dbo.Transactions AS transactions
            INNER JOIN dbo.Customers AS customers ON customers.CustomerId = transactions.CustomerId
            INNER JOIN dbo.Cars AS cars ON cars.CarId = transactions.CarId
            WHERE transactions.IsArchived = 0
              AND transactions.TransactionStatus = N'{TransactionConstants.Status.Active}'
              AND transactions.EndDate >= CONVERT(date, @From)
              AND transactions.EndDate <= CONVERT(date, @To)
            ORDER BY transactions.EndDate, transactions.TransactionCode;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<OperationsReturnItem>(sql, new { From = from, To = to });
        return results.ToList();
    }

    public async Task<IReadOnlyList<OperationsReturnItem>> GetLateReturnsAsync(DateTime today)
    {
        string sql = $"""
            SELECT
                ExpectedReturn = transactions.EndDate,
                DaysLate = DATEDIFF(day, transactions.EndDate, CONVERT(date, @Today)),
                EstimatedLateFee = transactions.DailyRate * DATEDIFF(day, transactions.EndDate, CONVERT(date, @Today)),
                transactions.TransactionCode,
                CustomerName = LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))),
                Contact = customers.PhoneNumber,
                cars.CarName,
                cars.PlateNumber,
                transactions.PaymentStatus
            FROM dbo.Transactions AS transactions
            INNER JOIN dbo.Customers AS customers ON customers.CustomerId = transactions.CustomerId
            INNER JOIN dbo.Cars AS cars ON cars.CarId = transactions.CarId
            WHERE transactions.IsArchived = 0
              AND transactions.TransactionStatus = N'{TransactionConstants.Status.Active}'
              AND transactions.EndDate < CONVERT(date, @Today)
            ORDER BY DaysLate DESC, transactions.EndDate;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<OperationsReturnItem>(sql, new { Today = today.Date });
        return results.ToList();
    }

    public async Task<IReadOnlyList<OperationsActiveRentalItem>> GetActiveRentalsReportAsync(DateTime from, DateTime to)
    {
        string sql = $"""
            SELECT
                transactions.TransactionCode,
                CustomerName = LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))),
                Contact = customers.PhoneNumber,
                cars.CarName,
                cars.PlateNumber,
                transactions.StartDate,
                transactions.EndDate,
                transactions.PaymentStatus
            FROM dbo.Transactions AS transactions
            INNER JOIN dbo.Customers AS customers ON customers.CustomerId = transactions.CustomerId
            INNER JOIN dbo.Cars AS cars ON cars.CarId = transactions.CarId
            WHERE transactions.IsArchived = 0
              AND transactions.TransactionStatus = N'{TransactionConstants.Status.Active}'
              AND transactions.StartDate <= CONVERT(date, @To)
              AND transactions.EndDate >= CONVERT(date, @From)
            ORDER BY transactions.EndDate, transactions.TransactionCode;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<OperationsActiveRentalItem>(sql, new { From = from, To = to });
        return results.ToList();
    }

    public async Task<IReadOnlyList<OperationsReservationItem>> GetUpcomingReservationsAsync(DateTime from, DateTime to)
    {
        string sql = $"""
            SELECT
                ScheduleDate = schedules.StartDate,
                CustomerName = ISNULL(LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))), N'-'),
                Contact = ISNULL(customers.PhoneNumber, N'-'),
                cars.CarName,
                cars.PlateNumber,
                schedules.Status,
                PaymentStatus = ISNULL(transactions.PaymentStatus, N'-')
            FROM dbo.FleetSchedules AS schedules
            INNER JOIN dbo.Cars AS cars ON cars.CarId = schedules.CarId
            LEFT JOIN dbo.Customers AS customers ON customers.CustomerId = schedules.CustomerId
            LEFT JOIN dbo.Transactions AS transactions
                ON transactions.FleetScheduleId = schedules.ScheduleId
               AND transactions.IsArchived = 0
            WHERE schedules.IsArchived = 0
              AND schedules.ScheduleType = N'{FleetScheduleConstants.Type.Reservation}'
              AND schedules.Status IN (N'{FleetScheduleConstants.Status.Pending}', N'{FleetScheduleConstants.Status.Reserved}')
              AND schedules.StartDate >= CONVERT(date, @From)
              AND schedules.StartDate <= CONVERT(date, @To)
            ORDER BY schedules.StartDate, schedules.ScheduleId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<OperationsReservationItem>(sql, new { From = from, To = to });
        return results.ToList();
    }

    public async Task<IReadOnlyList<OperationsMaintenanceItem>> GetMaintenanceVisibilityAsync(DateTime from, DateTime to)
    {
        string sql = $"""
            SELECT
                schedules.StartDate,
                schedules.EndDate,
                cars.CarName,
                cars.PlateNumber,
                schedules.Status,
                Source = N'Fleet Schedule'
            FROM dbo.FleetSchedules AS schedules
            INNER JOIN dbo.Cars AS cars ON cars.CarId = schedules.CarId
            WHERE schedules.IsArchived = 0
              AND cars.IsArchived = 0
              AND schedules.ScheduleType = N'{FleetScheduleConstants.Type.Maintenance}'
              AND schedules.StartDate <= CONVERT(date, @To)
              AND schedules.EndDate >= CONVERT(date, @From)
            ORDER BY schedules.StartDate, cars.CarName;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<OperationsMaintenanceItem>(sql, new { From = from, To = to });
        return results.ToList();
    }

    public async Task<IReadOnlyList<OperationsAvailableCarItem>> GetAvailableCarsReportAsync(DateTime from, DateTime to)
    {
        string sql = $"""
            SELECT
                cars.CarName,
                cars.PlateNumber,
                cars.Status,
                cars.RatePerDay,
                cars.SeatingCapacity
            FROM dbo.Cars AS cars
            WHERE cars.IsArchived = 0
              AND NOT EXISTS (
                    SELECT 1
                    FROM dbo.FleetSchedules AS schedules
                    WHERE schedules.CarId = cars.CarId
                      AND schedules.IsArchived = 0
                      AND schedules.Status IN (N'{FleetScheduleConstants.Status.Pending}', N'{FleetScheduleConstants.Status.Reserved}', N'{FleetScheduleConstants.Status.Rented}', N'{FleetScheduleConstants.Status.Ongoing}')
                      AND schedules.StartDate <= CONVERT(date, @To)
                      AND schedules.EndDate >= CONVERT(date, @From)
              )
            ORDER BY cars.CarName, cars.PlateNumber;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<OperationsAvailableCarItem>(sql, new { From = from, To = to });
        return results.ToList();
    }

    public async Task<CustomerAnalyticsMetrics> GetCustomerAnalyticsMetricsAsync(DateTime from, DateTime to)
    {
        string sql = $"""
            WITH CustomerRevenue AS
            (
                SELECT
                    customers.CustomerId,
                    CustomerName = LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))),
                    TotalPaid = SUM(payments.Amount)
                FROM dbo.Customers AS customers
                INNER JOIN dbo.Transactions AS transactions ON transactions.CustomerId = customers.CustomerId
                INNER JOIN dbo.TransactionPayments AS payments ON payments.TransactionId = transactions.TransactionId
                WHERE customers.IsWalkIn = 0
                  AND transactions.IsArchived = 0
                  AND payments.IsArchived = 0
                  AND payments.PaymentDate >= @From
                  AND payments.PaymentDate <= @To
                GROUP BY customers.CustomerId, customers.FirstName, customers.LastName
            ),
            CustomerRentals AS
            (
                SELECT
                    customers.CustomerId,
                    CustomerName = LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))),
                    RentalCount = COUNT(1)
                FROM dbo.Customers AS customers
                INNER JOIN dbo.Transactions AS transactions ON transactions.CustomerId = customers.CustomerId
                WHERE customers.IsWalkIn = 0
                  AND transactions.IsArchived = 0
                  AND transactions.StartDate <= CONVERT(date, @To)
                  AND transactions.EndDate >= CONVERT(date, @From)
                GROUP BY customers.CustomerId, customers.FirstName, customers.LastName
            )
            SELECT
                TotalActiveCustomers = (
                    SELECT COUNT(1)
                    FROM dbo.Customers
                    WHERE IsArchived = 0
                      AND IsBlacklisted = 0
                      AND IsWalkIn = 0
                ),
                NewCustomers = (
                    SELECT COUNT(1)
                    FROM dbo.Customers
                    WHERE IsArchived = 0
                      AND IsWalkIn = 0
                      AND CreatedAt >= @From
                      AND CreatedAt <= @To
                ),
                TopCustomerByRevenue = (SELECT TOP 1 CustomerName FROM CustomerRevenue ORDER BY TotalPaid DESC, CustomerName),
                TopCustomerRevenue = ISNULL((SELECT TOP 1 TotalPaid FROM CustomerRevenue ORDER BY TotalPaid DESC), 0),
                TopCustomerByRentals = (SELECT TOP 1 CustomerName FROM CustomerRentals ORDER BY RentalCount DESC, CustomerName),
                TopCustomerRentalCount = ISNULL((SELECT TOP 1 RentalCount FROM CustomerRentals ORDER BY RentalCount DESC), 0),
                BlacklistedCustomers = (
                    SELECT COUNT(1)
                    FROM dbo.Customers
                    WHERE IsArchived = 0
                      AND IsBlacklisted = 1
                      AND IsWalkIn = 0
                ),
                CustomersWithLateReturns = (
                    SELECT COUNT(DISTINCT CustomerId)
                    FROM dbo.Transactions
                    WHERE IsArchived = 0
                      AND TransactionStatus = N'{TransactionConstants.Status.Active}'
                      AND EndDate < CONVERT(date, @Today)
                ),
                CustomersWithDamageFees = (
                    SELECT COUNT(DISTINCT transactions.CustomerId)
                    FROM dbo.Transactions AS transactions
                    INNER JOIN dbo.TransactionPayments AS payments ON payments.TransactionId = transactions.TransactionId
                    WHERE transactions.IsArchived = 0
                      AND payments.IsArchived = 0
                      AND payments.PaymentCategory = N'Damage Fee'
                      AND payments.PaymentDate >= @From
                      AND payments.PaymentDate <= @To
                ),
                AverageRevenuePerCustomer = CASE
                    WHEN (SELECT COUNT(1) FROM CustomerRevenue) > 0
                        THEN ISNULL((SELECT SUM(TotalPaid) FROM CustomerRevenue), 0) / (SELECT COUNT(1) FROM CustomerRevenue)
                    ELSE 0
                END;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CustomerAnalyticsMetrics>(
            sql,
            new { From = from, To = to, Today = DateTime.Today })
            ?? new CustomerAnalyticsMetrics();
    }

    public async Task<IReadOnlyList<CustomerRevenueReportItem>> GetTopCustomersByRevenueAsync(DateTime from, DateTime to, int limit)
    {
        const string sql = """
            SELECT TOP (@Limit)
                CustomerName = LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))),
                Contact = customers.PhoneNumber,
                TransactionCount = COUNT(DISTINCT transactions.TransactionId),
                TotalPaid = SUM(payments.Amount),
                OutstandingBalance = (
                    SELECT ISNULL(SUM(openTransactions.BalanceAmount), 0)
                    FROM dbo.Transactions AS openTransactions
                    WHERE openTransactions.CustomerId = customers.CustomerId
                      AND openTransactions.IsArchived = 0
                      AND openTransactions.CreatedAt >= @From
                      AND openTransactions.CreatedAt <= @To
                )
            FROM dbo.Customers AS customers
            INNER JOIN dbo.Transactions AS transactions ON transactions.CustomerId = customers.CustomerId
            INNER JOIN dbo.TransactionPayments AS payments ON payments.TransactionId = transactions.TransactionId
            WHERE customers.IsWalkIn = 0
              AND transactions.IsArchived = 0
              AND payments.IsArchived = 0
              AND payments.PaymentDate >= @From
              AND payments.PaymentDate <= @To
            GROUP BY customers.CustomerId, customers.FirstName, customers.LastName, customers.PhoneNumber
            ORDER BY TotalPaid DESC, CustomerName;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<CustomerRevenueReportItem>(sql, new { From = from, To = to, Limit = limit });
        return results.ToList();
    }

    public async Task<IReadOnlyList<CustomerRentalCountReportItem>> GetTopCustomersByRentalCountAsync(DateTime from, DateTime to, int limit)
    {
        string sql = $"""
            SELECT TOP (@Limit)
                CustomerName = LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))),
                Contact = customers.PhoneNumber,
                RentalCount = COUNT(1),
                CompletedRentals = SUM(CASE WHEN transactions.TransactionStatus = N'{TransactionConstants.Status.Completed}' THEN 1 ELSE 0 END),
                ActiveRentals = SUM(CASE WHEN transactions.TransactionStatus = N'{TransactionConstants.Status.Active}' THEN 1 ELSE 0 END),
                LastRentalDate = MAX(transactions.StartDate)
            FROM dbo.Customers AS customers
            INNER JOIN dbo.Transactions AS transactions ON transactions.CustomerId = customers.CustomerId
            WHERE customers.IsWalkIn = 0
              AND transactions.IsArchived = 0
              AND transactions.StartDate <= CONVERT(date, @To)
              AND transactions.EndDate >= CONVERT(date, @From)
            GROUP BY customers.CustomerId, customers.FirstName, customers.LastName, customers.PhoneNumber
            ORDER BY RentalCount DESC, LastRentalDate DESC, CustomerName;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<CustomerRentalCountReportItem>(sql, new { From = from, To = to, Limit = limit });
        return results.ToList();
    }

    public async Task<IReadOnlyList<CustomerOutstandingBalanceReportItem>> GetCustomersWithOutstandingBalancesAsync(DateTime from, DateTime to)
    {
        string sql = $"""
            SELECT
                CustomerName = LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))),
                Contact = customers.PhoneNumber,
                transactions.TransactionCode,
                transactions.TotalAmount,
                transactions.AmountPaid,
                Balance = transactions.BalanceAmount,
                transactions.PaymentStatus
            FROM dbo.Transactions AS transactions
            INNER JOIN dbo.Customers AS customers ON customers.CustomerId = transactions.CustomerId
            WHERE customers.IsWalkIn = 0
              AND transactions.IsArchived = 0
              AND transactions.PaymentStatus IN (N'{TransactionConstants.PaymentStatus.Unpaid}', N'{TransactionConstants.PaymentStatus.Partial}')
              AND transactions.CreatedAt >= @From
              AND transactions.CreatedAt <= @To
            ORDER BY transactions.BalanceAmount DESC, transactions.TransactionCode;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<CustomerOutstandingBalanceReportItem>(sql, new { From = from, To = to });
        return results.ToList();
    }

    public async Task<IReadOnlyList<CustomerLateReturnReportItem>> GetCustomersWithLateReturnsAsync(DateTime today)
    {
        string sql = $"""
            SELECT
                CustomerName = LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))),
                Contact = customers.PhoneNumber,
                transactions.TransactionCode,
                cars.CarName,
                cars.PlateNumber,
                DaysLate = DATEDIFF(day, transactions.EndDate, CONVERT(date, @Today)),
                EstimatedLateFee = transactions.DailyRate * DATEDIFF(day, transactions.EndDate, CONVERT(date, @Today))
            FROM dbo.Transactions AS transactions
            INNER JOIN dbo.Customers AS customers ON customers.CustomerId = transactions.CustomerId
            INNER JOIN dbo.Cars AS cars ON cars.CarId = transactions.CarId
            WHERE customers.IsWalkIn = 0
              AND transactions.IsArchived = 0
              AND transactions.TransactionStatus = N'{TransactionConstants.Status.Active}'
              AND transactions.EndDate < CONVERT(date, @Today)
            ORDER BY DaysLate DESC, transactions.EndDate;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<CustomerLateReturnReportItem>(sql, new { Today = today.Date });
        return results.ToList();
    }

    public async Task<IReadOnlyList<CustomerDamageFeeReportItem>> GetCustomersWithDamageFeesAsync(DateTime from, DateTime to)
    {
        const string sql = """
            SELECT
                CustomerName = LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))),
                Contact = customers.PhoneNumber,
                transactions.TransactionCode,
                cars.CarName,
                cars.PlateNumber,
                DamageFee = payments.Amount,
                payments.PaymentDate
            FROM dbo.TransactionPayments AS payments
            INNER JOIN dbo.Transactions AS transactions ON transactions.TransactionId = payments.TransactionId
            INNER JOIN dbo.Customers AS customers ON customers.CustomerId = transactions.CustomerId
            INNER JOIN dbo.Cars AS cars ON cars.CarId = transactions.CarId
            WHERE customers.IsWalkIn = 0
              AND transactions.IsArchived = 0
              AND payments.IsArchived = 0
              AND payments.PaymentCategory = N'Damage Fee'
              AND payments.PaymentDate >= @From
              AND payments.PaymentDate <= @To
            ORDER BY payments.PaymentDate DESC, DamageFee DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<CustomerDamageFeeReportItem>(sql, new { From = from, To = to });
        return results.ToList();
    }

    public async Task<IReadOnlyList<BlacklistedCustomerReportItem>> GetBlacklistedCustomersReportAsync(DateTime from, DateTime to)
    {
        const string sql = """
            SELECT
                CustomerName = LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))),
                Contact = customers.PhoneNumber,
                BlacklistReason = ISNULL(customers.BlacklistReason, N'-'),
                [Status] = N'Blacklisted',
                LastTransaction = ISNULL((
                    SELECT TOP 1 transactions.TransactionCode
                    FROM dbo.Transactions AS transactions
                    WHERE transactions.CustomerId = customers.CustomerId
                      AND transactions.IsArchived = 0
                    ORDER BY transactions.CreatedAt DESC, transactions.TransactionId DESC
                ), N'-')
            FROM dbo.Customers AS customers
            WHERE customers.IsArchived = 0
              AND customers.IsBlacklisted = 1
              AND customers.IsWalkIn = 0
            ORDER BY customers.LastName, customers.FirstName;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<BlacklistedCustomerReportItem>(sql, new { From = from, To = to });
        return results.ToList();
    }

    public async Task<OperatingProfitabilitySummary> GetOperatingProfitabilityAsync(DateTime from, DateTime to)
    {
        string sql = $"""
            DECLARE @TotalRevenue decimal(18,2) = (
                SELECT ISNULL(SUM(tp.Amount), 0)
                FROM dbo.TransactionPayments tp
                JOIN dbo.Transactions t ON tp.TransactionId = t.TransactionId
                WHERE tp.IsArchived = 0 AND t.IsArchived = 0 AND t.TransactionStatus <> N'{TransactionConstants.Status.Cancelled}'
                  AND tp.PaymentDate >= @From AND tp.PaymentDate <= @To
            );

            DECLARE @TotalOffsiteCost decimal(18,2) = (
                SELECT ISNULL(SUM(ActualCost), 0)
                FROM dbo.OffsiteRecords
                WHERE IsArchived = 0 AND [Status] = N'{OffsiteConstants.Status.Completed}'
                  AND CompletedDate >= @From AND CompletedDate <= @To
            );

            SELECT 
                TotalRevenue = @TotalRevenue,
                TotalOffsiteCost = @TotalOffsiteCost,
                NetAfterOffsiteCost = @TotalRevenue - @TotalOffsiteCost,
                CostToRevenueRatio = CASE WHEN @TotalRevenue > 0 THEN (@TotalOffsiteCost / @TotalRevenue) * 100 ELSE 0 END,
                MaintenanceCost = ISNULL((SELECT SUM(ActualCost) FROM dbo.OffsiteRecords WHERE IsArchived = 0 AND [Status] = N'{OffsiteConstants.Status.Completed}' AND OffsiteType = N'{OffsiteConstants.Type.Maintenance}' AND CompletedDate >= @From AND CompletedDate <= @To), 0),
                RepairCost = ISNULL((SELECT SUM(ActualCost) FROM dbo.OffsiteRecords WHERE IsArchived = 0 AND [Status] = N'{OffsiteConstants.Status.Completed}' AND OffsiteType = N'{OffsiteConstants.Type.Repair}' AND CompletedDate >= @From AND CompletedDate <= @To), 0),
                CleaningCost = ISNULL((SELECT SUM(ActualCost) FROM dbo.OffsiteRecords WHERE IsArchived = 0 AND [Status] = N'{OffsiteConstants.Status.Completed}' AND OffsiteType = N'{OffsiteConstants.Type.Cleaning}' AND CompletedDate >= @From AND CompletedDate <= @To), 0);
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OperatingProfitabilitySummary>(sql, new { From = from, To = to })
            ?? new OperatingProfitabilitySummary();
    }

    public async Task<IReadOnlyList<VehicleCostProfitabilityItem>> GetVehicleProfitabilityAsync(DateTime from, DateTime to)
    {
        string sql = $"""
            SELECT 
                c.CarId,
                CarDisplayName = c.CarName,
                c.PlateNumber,
                MaintenanceCount = COUNT(CASE WHEN o.OffsiteType = N'{OffsiteConstants.Type.Maintenance}' THEN 1 END),
                RepairCount = COUNT(CASE WHEN o.OffsiteType = N'{OffsiteConstants.Type.Repair}' THEN 1 END),
                CleaningCount = COUNT(CASE WHEN o.OffsiteType = N'{OffsiteConstants.Type.Cleaning}' THEN 1 END),
                TotalOffsiteCost = SUM(ISNULL(o.ActualCost, 0)),
                RevenueGenerated = ISNULL((
                    SELECT SUM(tp.Amount)
                    FROM dbo.TransactionPayments tp
                    JOIN dbo.Transactions t ON tp.TransactionId = t.TransactionId
                    WHERE t.CarId = c.CarId 
                      AND tp.IsArchived = 0 
                      AND t.IsArchived = 0 
                      AND t.TransactionStatus <> N'{TransactionConstants.Status.Cancelled}'
                      AND tp.PaymentDate >= @From 
                      AND tp.PaymentDate <= @To
                ), 0),
                NetAfterCost = ISNULL((
                    SELECT SUM(tp.Amount)
                    FROM dbo.TransactionPayments tp
                    JOIN dbo.Transactions t ON tp.TransactionId = t.TransactionId
                    WHERE t.CarId = c.CarId 
                      AND tp.IsArchived = 0 
                      AND t.IsArchived = 0 
                      AND t.TransactionStatus <> N'{TransactionConstants.Status.Cancelled}'
                      AND tp.PaymentDate >= @From 
                      AND tp.PaymentDate <= @To
                ), 0) - SUM(ISNULL(o.ActualCost, 0))
            FROM dbo.Cars c
            LEFT JOIN dbo.OffsiteRecords o ON o.CarId = c.CarId 
                AND o.IsArchived = 0 
                AND o.[Status] = N'{OffsiteConstants.Status.Completed}'
                AND o.CompletedDate >= @From 
                AND o.CompletedDate <= @To
            WHERE c.IsArchived = 0
            GROUP BY c.CarId, c.CarName, c.PlateNumber
            HAVING SUM(ISNULL(o.ActualCost, 0)) > 0 OR (
                SELECT COUNT(1) 
                FROM dbo.TransactionPayments tp
                JOIN dbo.Transactions t ON tp.TransactionId = t.TransactionId
                WHERE t.CarId = c.CarId 
                  AND tp.IsArchived = 0 
                  AND t.IsArchived = 0 
                  AND t.TransactionStatus <> N'{TransactionConstants.Status.Cancelled}'
                  AND tp.PaymentDate >= @From 
                  AND tp.PaymentDate <= @To
            ) > 0
            ORDER BY TotalOffsiteCost DESC, RevenueGenerated DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<VehicleCostProfitabilityItem>(sql, new { From = from, To = to });
        return results.ToList();
    }
}
