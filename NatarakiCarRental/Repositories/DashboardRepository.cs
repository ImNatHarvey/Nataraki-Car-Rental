using System.Data;
using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Repositories;

public sealed class DashboardRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public DashboardRepository() : this(new DbConnectionFactory()) { }

    public DashboardRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DashboardOperationalData> GetDashboardDataAsync(DateTime fromDate, DateTime toDate)
    {
        string sql = $"""
            -- Scalar Counts
            DECLARE @ActiveCars int = (SELECT COUNT(1) FROM dbo.Cars WHERE IsArchived = 0);
            DECLARE @AvailableCars int = (
                SELECT COUNT(1) FROM dbo.Cars c 
                WHERE c.IsArchived = 0 
                AND NOT EXISTS (
                    SELECT 1 FROM dbo.Transactions t 
                    WHERE t.CarId = c.CarId AND t.IsArchived = 0 
                    AND t.TransactionStatus IN (N'{TransactionConstants.Status.Active}', N'{TransactionConstants.Status.Maintenance}')
                    AND t.StartDate <= @ToDate AND t.EndDate >= @FromDate
                )
                AND NOT EXISTS (
                    SELECT 1 FROM dbo.FleetSchedules s 
                    WHERE s.CarId = c.CarId AND s.IsArchived = 0 
                    AND s.Status IN (N'{FleetScheduleConstants.Status.Scheduled}')
                    AND s.StartDate <= @ToDate AND s.EndDate >= @FromDate
                )
            );
            
            DECLARE @ReservationLoadToday int = (
                SELECT COUNT(1) FROM dbo.FleetSchedules 
                WHERE IsArchived = 0 AND ScheduleType = N'{FleetScheduleConstants.Type.Reservation}' 
                AND Status IN (N'{FleetScheduleConstants.Status.Pending}', N'{FleetScheduleConstants.Status.Scheduled}')
                AND StartDate <= @ToDate AND EndDate >= @FromDate
            );
            
            DECLARE @MaintenanceLoad int = (
                SELECT COUNT(1) FROM dbo.Transactions 
                WHERE IsArchived = 0 AND TransactionType = N'Maintenance' 
                AND TransactionStatus = N'{TransactionConstants.Status.Maintenance}'
                AND StartDate <= @ToDate AND EndDate >= @FromDate
            );
            
            DECLARE @PendingPaymentsCount int = (
                SELECT COUNT(1) FROM dbo.Transactions 
                WHERE IsArchived = 0 AND PaymentStatus IN (N'{TransactionConstants.PaymentStatus.Unpaid}', N'{TransactionConstants.PaymentStatus.Partial}')
            );
            
            DECLARE @OverdueTransactionsCount int = (
                SELECT COUNT(1) FROM dbo.Transactions 
                WHERE IsArchived = 0 AND TransactionType = N'Rental' AND TransactionStatus = N'{TransactionConstants.Status.Active}' AND EndDate < @FromDate
            );

            DECLARE @RevenueToday decimal(18,2) = (
                SELECT ISNULL(SUM(tp.Amount), 0)
                FROM dbo.TransactionPayments tp
                JOIN dbo.Transactions t ON tp.TransactionId = t.TransactionId
                WHERE tp.IsArchived = 0 AND t.IsArchived = 0 AND t.TransactionType <> N'Maintenance'
                  AND CONVERT(date, tp.PaymentDate) = CONVERT(date, GETDATE())
            );

            DECLARE @RevenueThisWeek decimal(18,2) = (
                SELECT ISNULL(SUM(tp.Amount), 0)
                FROM dbo.TransactionPayments tp
                JOIN dbo.Transactions t ON tp.TransactionId = t.TransactionId
                WHERE tp.IsArchived = 0 AND t.IsArchived = 0 AND t.TransactionType <> N'Maintenance'
                  AND tp.PaymentDate >= DATEADD(day, -7, GETDATE())
            );

            -- Results
            SELECT 
                ActiveCars = @ActiveCars,
                AvailableCars = @AvailableCars,
                ReservationLoadToday = @ReservationLoadToday,
                MaintenanceLoad = @MaintenanceLoad,
                OffsiteLoad = @MaintenanceLoad, -- Map maintenance to offsite load for dashboard compatibility
                PendingPaymentsCount = @PendingPaymentsCount,
                OverdueTransactionsCount = @OverdueTransactionsCount,
                FleetUtilizationPercentage = CASE WHEN @ActiveCars > 0 THEN (CAST((@ActiveCars - @AvailableCars) AS float) / @ActiveCars) * 100 ELSE 0 END,
                RevenueToday = @RevenueToday,
                RevenueThisWeek = @RevenueThisWeek,
                TopRentedVehicle = ISNULL((SELECT TOP 1 c.CarName FROM dbo.Transactions t JOIN dbo.Cars c ON c.CarId = t.CarId WHERE t.TransactionType = N'Rental' GROUP BY c.CarId, c.CarName ORDER BY COUNT(1) DESC), N'N/A'),
                MostActiveCustomer = ISNULL((SELECT TOP 1 CONCAT(cu.FirstName, N' ', cu.LastName) FROM dbo.Transactions t JOIN dbo.Customers cu ON cu.CustomerId = t.CustomerId WHERE t.TransactionType = N'Rental' GROUP BY cu.CustomerId, cu.FirstName, cu.LastName ORDER BY COUNT(1) DESC), N'N/A');

            -- Upcoming Schedules & Maintenance
            SELECT 
                s.ScheduleId, s.Title, s.StartDate, s.EndDate, s.Status, s.CarId, s.CustomerId, s.ScheduleType,
                CarName = c.CarName, PlateNumber = c.PlateNumber,
                CustomerName = ISNULL(NULLIF(LTRIM(RTRIM(CONCAT(cu.FirstName, N' ', cu.LastName))), N''), cu.CompanyName)
            FROM dbo.FleetSchedules s
            JOIN dbo.Cars c ON c.CarId = s.CarId
            LEFT JOIN dbo.Customers cu ON cu.CustomerId = s.CustomerId
            WHERE s.IsArchived = 0 
              AND s.EndDate >= @FromDate
              AND s.StartDate <= @ToDate
              AND s.Status IN (N'{FleetScheduleConstants.Status.Pending}', N'{FleetScheduleConstants.Status.Scheduled}', N'{FleetScheduleConstants.Status.Rented}', N'{FleetScheduleConstants.Status.Ongoing}')
            ORDER BY s.StartDate ASC;

            -- Vehicles Due / Overdue Returns
            SELECT 
                ExpectedReturn = t.EndDate,
                t.TransactionCode,
                CustomerName = LTRIM(RTRIM(CONCAT(cu.FirstName, N' ', cu.LastName))),
                Contact = cu.PhoneNumber,
                c.CarName,
                c.PlateNumber,
                t.PaymentStatus
            FROM dbo.Transactions t
            JOIN dbo.Customers cu ON cu.CustomerId = t.CustomerId
            JOIN dbo.Cars c ON c.CarId = t.CarId
            WHERE t.IsArchived = 0 
              AND t.TransactionType = N'Rental'
              AND t.TransactionStatus = N'{TransactionConstants.Status.Active}'
              AND t.EndDate <= @ToDate
            ORDER BY t.EndDate ASC;

            -- Ongoing Maintenance (replaced legacy Offsite)
            SELECT 
                TransactionId = t.TransactionId, CarId = t.CarId, TransactionType = t.TransactionType, Status = t.TransactionStatus, LocationName = cu.CompanyName, 
                t.StartDate, ExpectedReturnDate = t.EndDate,
                CarName = c.CarName, PlateNumber = c.PlateNumber,
                ContactPerson = cu.FirstName, ContactNumber = cu.PhoneNumber
            FROM dbo.Transactions t
            JOIN dbo.Cars c ON c.CarId = t.CarId
            JOIN dbo.Customers cu ON cu.CustomerId = t.CustomerId
            WHERE t.IsArchived = 0 AND t.TransactionType = N'Maintenance' AND t.TransactionStatus = N'{TransactionConstants.Status.Maintenance}'
            ORDER BY t.StartDate ASC;

            -- Recent High Priority Activity
            SELECT TOP 10
                ActivityLogId, UserId, UserFullName, Module, Action, EntityId, EntityName, Description, CreatedAt
            FROM dbo.ActivityLogs
            WHERE Action IN (N'Archived', N'Cancelled', N'Completed', N'Blacklisted', N'Restored', N'Maintenance')
               OR Module IN (N'Transaction', N'Customer')
            ORDER BY CreatedAt DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        using var multi = await connection.QueryMultipleAsync(sql, new { FromDate = fromDate, ToDate = toDate });

        var data = await multi.ReadSingleOrDefaultAsync<DashboardOperationalData>() ?? new DashboardOperationalData();
        data.UpcomingSchedules = (await multi.ReadAsync<FleetSchedule>()).ToList();
        data.VehiclesDueToday = (await multi.ReadAsync<OperationsReturnItem>()).ToList();
        
        // Skip reading OngoingMaintenance results for now or map them if needed
        await multi.ReadAsync<dynamic>();
        
        data.HighPriorityActivities = (await multi.ReadAsync<ActivityLog>()).ToList();

        return data;
    }
}
