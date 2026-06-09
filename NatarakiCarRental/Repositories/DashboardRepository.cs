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
                    SELECT 1 FROM dbo.FleetSchedules s 
                    WHERE s.CarId = c.CarId AND s.IsArchived = 0 
                    AND s.Status IN ('Rented', 'Ongoing', 'Scheduled')
                    AND s.StartDate <= @ToDate AND s.EndDate >= @FromDate
                )
            );
            
            DECLARE @ReservationLoadToday int = (
                SELECT COUNT(1) FROM dbo.FleetSchedules 
                WHERE IsArchived = 0 AND ScheduleType = 'Reservation' 
                AND Status IN ('Pending', 'Scheduled')
                AND StartDate <= @ToDate AND EndDate >= @FromDate
            );
            
            DECLARE @MaintenanceLoad int = (
                SELECT COUNT(1) FROM dbo.FleetSchedules 
                WHERE IsArchived = 0 AND ScheduleType = 'Maintenance' 
                AND Status = 'Maintenance'
                AND StartDate <= @ToDate AND EndDate >= @FromDate
            );
            
            DECLARE @OffsiteLoad int = (
                SELECT COUNT(1) FROM dbo.OffsiteRecords 
                WHERE IsArchived = 0 AND Status = 'Ongoing'
            );
            
            DECLARE @PendingPaymentsCount int = (
                SELECT COUNT(1) FROM dbo.Transactions 
                WHERE IsArchived = 0 AND PaymentStatus IN ('Unpaid', 'Partial')
            );
            
            DECLARE @OverdueTransactionsCount int = (
                SELECT COUNT(1) FROM dbo.Transactions 
                WHERE IsArchived = 0 AND TransactionStatus = 'Active' AND EndDate < @FromDate
            );

            -- Results
            SELECT 
                ActiveCars = @ActiveCars,
                AvailableCars = @AvailableCars,
                ReservationLoadToday = @ReservationLoadToday,
                MaintenanceLoad = @MaintenanceLoad,
                OffsiteLoad = @OffsiteLoad,
                PendingPaymentsCount = @PendingPaymentsCount,
                OverdueTransactionsCount = @OverdueTransactionsCount,
                FleetUtilizationPercentage = CASE WHEN @ActiveCars > 0 THEN (CAST((@ActiveCars - @AvailableCars) AS float) / @ActiveCars) * 100 ELSE 0 END;

            -- Upcoming Schedules & Maintenance
            SELECT 
                s.ScheduleId, s.Title, s.StartDate, s.EndDate, s.Status, s.CarId, s.CustomerId, s.ScheduleType,
                CarName = c.CarName, PlateNumber = c.PlateNumber,
                CustomerName = ISNULL(CONCAT(cu.FirstName, ' ', cu.LastName), 'Walk-In')
            FROM dbo.FleetSchedules s
            JOIN dbo.Cars c ON c.CarId = s.CarId
            LEFT JOIN dbo.Customers cu ON cu.CustomerId = s.CustomerId
            WHERE s.IsArchived = 0 
              AND s.EndDate >= @FromDate
              AND s.StartDate <= @ToDate
              AND s.Status IN ('Pending', 'Scheduled', 'Rented', 'Ongoing')
            ORDER BY s.StartDate ASC;

            -- Vehicles Due / Overdue Returns
            SELECT 
                ExpectedReturn = t.EndDate,
                t.TransactionCode,
                CustomerName = CONCAT(cu.FirstName, ' ', cu.LastName),
                Contact = cu.PhoneNumber,
                c.CarName,
                c.PlateNumber,
                t.PaymentStatus
            FROM dbo.Transactions t
            JOIN dbo.Customers cu ON cu.CustomerId = t.CustomerId
            JOIN dbo.Cars c ON c.CarId = t.CarId
            WHERE t.IsArchived = 0 
              AND t.TransactionStatus = 'Active'
              AND t.EndDate <= @ToDate
            ORDER BY t.EndDate ASC;

            -- Ongoing Offsite
            SELECT 
                o.OffsiteRecordId, o.CarId, o.OffsiteType, o.Status, o.LocationName, 
                o.StartDate, o.ExpectedReturnDate,
                CarName = c.CarName, PlateNumber = c.PlateNumber,
                ContactPerson = o.ContactPerson, ContactNumber = o.ContactNumber
            FROM dbo.OffsiteRecords o
            JOIN dbo.Cars c ON c.CarId = o.CarId
            WHERE o.IsArchived = 0 AND o.Status = 'Ongoing'
            ORDER BY o.StartDate ASC;

            -- Recent High Priority Activity
            SELECT TOP 10
                ActivityLogId, UserId, UserFullName, Module, Action, EntityId, EntityName, Description, CreatedAt
            FROM dbo.ActivityLogs
            WHERE Action IN ('Archived', 'Cancelled', 'Completed', 'Blacklisted', 'Restored', 'Maintenance')
               OR Module = 'OffsiteRecord'
            ORDER BY CreatedAt DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        using var multi = await connection.QueryMultipleAsync(sql, new { FromDate = fromDate, ToDate = toDate });

        var data = await multi.ReadSingleOrDefaultAsync<DashboardOperationalData>() ?? new DashboardOperationalData();
        data.UpcomingSchedules = (await multi.ReadAsync<FleetSchedule>()).ToList();
        data.VehiclesDueToday = (await multi.ReadAsync<OperationsReturnItem>()).ToList();
        data.OngoingOffsite = (await multi.ReadAsync<OffsiteRecordListItem>()).ToList();
        data.HighPriorityActivities = (await multi.ReadAsync<ActivityLog>()).ToList();

        return data;
    }
}
