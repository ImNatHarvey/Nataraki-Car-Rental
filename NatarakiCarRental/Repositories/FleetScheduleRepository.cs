using System.Data;
using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Repositories;

public sealed class FleetScheduleRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public FleetScheduleRepository()
        : this(new DbConnectionFactory())
    {
    }

    public FleetScheduleRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<IReadOnlyList<FleetSchedule>> GetSchedulesForMonthAsync(int year, int month)
    {
        DateTime firstDay = new(year, month, 1);
        DateTime lastDay = firstDay.AddMonths(1).AddDays(-1);
        return GetSchedulesInRangeAsync(firstDay, lastDay);
    }

    public async Task<FleetScheduleOverviewCounts> GetOverviewCountsAsync(DateTime referenceDate)
    {
        const string sql = """
            SELECT
                TodaysSchedules = COUNT(CASE
                    WHEN IsArchived = 0
                     AND StartDate <= @ReferenceDate
                     AND EndDate >= @ReferenceDate
                    THEN 1 END),
                UpcomingSchedules = COUNT(CASE
                    WHEN IsArchived = 0
                     AND Status IN @OperationalStatuses
                     AND StartDate > @ReferenceDate
                    THEN 1 END),
                ActiveMaintenanceSchedules = COUNT(CASE
                    WHEN IsArchived = 0
                     AND ScheduleType = @MaintenanceType
                     AND Status = @OngoingStatus
                     AND StartDate <= @ReferenceDate
                     AND EndDate >= @ReferenceDate
                    THEN 1 END)
            FROM dbo.FleetSchedules;
            """;

        using var connection = _connectionFactory.CreateConnection();
        FleetScheduleOverviewCounts? counts = await connection.QuerySingleOrDefaultAsync<FleetScheduleOverviewCounts>(
            sql,
            new
            {
                ReferenceDate = referenceDate.Date,
                MaintenanceType = FleetScheduleConstants.Type.Maintenance,
                OngoingStatus = FleetScheduleConstants.Status.Maintenance,
                OperationalStatuses = FleetScheduleConstants.Status.Operational
            });

        return counts ?? new FleetScheduleOverviewCounts();
    }

    public async Task<IReadOnlyList<FleetSchedule>> GetRecentUpcomingSchedulesAsync(DateTime referenceDate, int take)
    {
        const string sql = """
            SELECT TOP (@Take)
                schedules.ScheduleId,
                schedules.CarId,
                schedules.CustomerId,
                cars.CarName,
                cars.PlateNumber,
                CustomerName = NULLIF(LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))), N''),
                schedules.Title,
                schedules.ScheduleType,
                schedules.Status,
                schedules.StartDate,
                schedules.EndDate,
                schedules.Notes,
                schedules.CreatedByUserId,
                schedules.CreatedAt,
                schedules.UpdatedAt,
                schedules.IsArchived
            FROM dbo.FleetSchedules AS schedules
            INNER JOIN dbo.Cars AS cars ON cars.CarId = schedules.CarId
            LEFT JOIN dbo.Customers AS customers ON customers.CustomerId = schedules.CustomerId
            WHERE schedules.IsArchived = 0
              AND schedules.Status IN @OperationalStatuses
              AND schedules.EndDate >= @ReferenceDate
            ORDER BY schedules.StartDate, schedules.ScheduleId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<FleetSchedule> schedules = await connection.QueryAsync<FleetSchedule>(
            sql,
            new
            {
                ReferenceDate = referenceDate.Date,
                Take = take,
                OperationalStatuses = FleetScheduleConstants.Status.Operational
            });

        return schedules.ToList();
    }

    public async Task<IReadOnlyList<FleetSchedule>> GetSchedulesForCarAsync(int carId)
    {
        const string sql = """
            SELECT
                schedules.ScheduleId,
                schedules.CarId,
                schedules.CustomerId,
                cars.CarName,
                cars.PlateNumber,
                CustomerName = NULLIF(LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))), N''),
                schedules.Title,
                schedules.ScheduleType,
                schedules.Status,
                schedules.StartDate,
                schedules.EndDate,
                schedules.Notes,
                schedules.CreatedByUserId,
                schedules.CreatedAt,
                schedules.UpdatedAt,
                schedules.IsArchived
            FROM dbo.FleetSchedules AS schedules
            INNER JOIN dbo.Cars AS cars ON cars.CarId = schedules.CarId
            LEFT JOIN dbo.Customers AS customers ON customers.CustomerId = schedules.CustomerId
            WHERE schedules.CarId = @CarId
              AND schedules.IsArchived = 0
            ORDER BY schedules.StartDate DESC, schedules.ScheduleId DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<FleetSchedule> schedules = await connection.QueryAsync<FleetSchedule>(sql, new { CarId = carId });
        return schedules.ToList();
    }

    public async Task<IReadOnlyList<FleetSchedule>> GetEligibleReservationsAsync(DateTime referenceDate)
    {
        const string sql = """
            SELECT
                schedules.ScheduleId,
                schedules.CarId,
                schedules.CustomerId,
                cars.CarName,
                cars.PlateNumber,
                CustomerName = NULLIF(LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))), N''),
                schedules.Title,
                schedules.ScheduleType,
                schedules.Status,
                schedules.StartDate,
                schedules.EndDate,
                schedules.Notes,
                schedules.CreatedByUserId,
                schedules.CreatedAt,
                schedules.UpdatedAt,
                schedules.IsArchived
            FROM dbo.FleetSchedules AS schedules
            INNER JOIN dbo.Cars AS cars ON cars.CarId = schedules.CarId
            LEFT JOIN dbo.Customers AS customers ON customers.CustomerId = schedules.CustomerId
            WHERE schedules.IsArchived = 0
              AND schedules.ScheduleType = @ReservationType
              AND schedules.Status IN @ReservationStatuses
              AND NOT EXISTS (
                    SELECT 1
                    FROM dbo.Transactions AS transactions
                    WHERE transactions.FleetScheduleId = schedules.ScheduleId
                      AND transactions.IsArchived = 0
              )
            ORDER BY schedules.StartDate, schedules.ScheduleId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<FleetSchedule> schedules = await connection.QueryAsync<FleetSchedule>(
            sql,
            new
            {
                ReferenceDate = referenceDate.Date,
                ReservationType = FleetScheduleConstants.Type.Reservation,
                ReservationStatuses = FleetScheduleConstants.Status.ReservationOptions
                    .Where(status => status != FleetScheduleConstants.Status.Cancelled)
                    .ToArray()
            });
        return schedules.ToList();
    }

    public async Task<IReadOnlyList<FleetSchedule>> GetMaintenanceSchedulesAsync()
    {
        const string sql = """
            SELECT
                schedules.ScheduleId,
                schedules.CarId,
                schedules.CustomerId,
                cars.CarName,
                cars.PlateNumber,
                CustomerName = NULLIF(LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))), N''),
                schedules.Title,
                schedules.ScheduleType,
                schedules.Status,
                schedules.StartDate,
                schedules.EndDate,
                schedules.Notes,
                schedules.CreatedByUserId,
                schedules.CreatedAt,
                schedules.UpdatedAt,
                schedules.IsArchived
            FROM dbo.FleetSchedules AS schedules
            INNER JOIN dbo.Cars AS cars ON cars.CarId = schedules.CarId
            LEFT JOIN dbo.Customers AS customers ON customers.CustomerId = schedules.CustomerId
            WHERE schedules.IsArchived = 0
              AND schedules.ScheduleType = @MaintenanceType
              AND schedules.Status IN @MaintenanceStatuses
            ORDER BY schedules.StartDate, schedules.ScheduleId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<FleetSchedule> schedules = await connection.QueryAsync<FleetSchedule>(
            sql,
            new
            {
                MaintenanceType = FleetScheduleConstants.Type.Maintenance,
                MaintenanceStatuses = new[] { FleetScheduleConstants.Status.Pending }
            });

        return (schedules ?? Enumerable.Empty<FleetSchedule>()).ToList();
    }

    public async Task<FleetSchedule?> GetByIdAsync(int scheduleId)
    {
        const string sql = """
            SELECT
                schedules.ScheduleId,
                schedules.CarId,
                schedules.CustomerId,
                cars.CarName,
                cars.PlateNumber,
                CustomerName = NULLIF(LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))), N''),
                schedules.Title,
                schedules.ScheduleType,
                schedules.Status,
                schedules.StartDate,
                schedules.EndDate,
                schedules.Notes,
                schedules.CreatedByUserId,
                schedules.CreatedAt,
                schedules.UpdatedAt,
                schedules.IsArchived
            FROM dbo.FleetSchedules AS schedules
            INNER JOIN dbo.Cars AS cars ON cars.CarId = schedules.CarId
            LEFT JOIN dbo.Customers AS customers ON customers.CustomerId = schedules.CustomerId
            WHERE schedules.ScheduleId = @ScheduleId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<FleetSchedule>(sql, new { ScheduleId = scheduleId });
    }

    public async Task<int> CreateAsync(FleetSchedule schedule, IDbTransaction? transaction = null)
    {
        const string sql = """
            INSERT INTO dbo.FleetSchedules
            (
                CarId,
                CustomerId,
                Title,
                ScheduleType,
                Status,
                StartDate,
                EndDate,
                Notes,
                CreatedByUserId
            )
            OUTPUT INSERTED.ScheduleId
            VALUES
            (
                @CarId,
                @CustomerId,
                @Title,
                @ScheduleType,
                @Status,
                @StartDate,
                @EndDate,
                @Notes,
                @CreatedByUserId
            );
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteScalarAsync<int>(sql, schedule, transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<int> UpdateAsync(FleetSchedule schedule, IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.FleetSchedules
            SET
                CarId = @CarId,
                CustomerId = @CustomerId,
                Title = @Title,
                ScheduleType = @ScheduleType,
                Status = @Status,
                StartDate = @StartDate,
                EndDate = @EndDate,
                Notes = @Notes,
                UpdatedAt = sysdatetime()
            WHERE ScheduleId = @ScheduleId
              AND IsArchived = 0;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(sql, schedule, transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<int> ArchiveAsync(int scheduleId, IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.FleetSchedules
            SET IsArchived = 1,
                UpdatedAt = sysdatetime()
            WHERE ScheduleId = @ScheduleId
              AND IsArchived = 0;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(sql, new { ScheduleId = scheduleId }, transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public Task<bool> HasConflictExcludingAsync(
        int carId,
        DateTime startDate,
        DateTime endDate,
        int excludedScheduleId)
    {
        return HasConflictAsync(carId, startDate, endDate, excludedScheduleId);
    }

    public async Task<bool> HasConflictAsync(
        int carId,
        DateTime startDate,
        DateTime endDate,
        int? excludedScheduleId = null,
        IDbTransaction? transaction = null)
    {
        string sql = $"""
            SELECT COUNT(1)
            FROM dbo.FleetSchedules WITH (UPDLOCK, HOLDLOCK)
            WHERE CarId = @CarId
              AND IsArchived = 0
              AND Status IN @OperationalStatuses
              AND NOT (ScheduleType = N'{FleetScheduleConstants.Type.Maintenance}' AND Status = N'{FleetScheduleConstants.Status.Pending}')
              AND (@ExcludedScheduleId IS NULL OR ScheduleId <> @ExcludedScheduleId)
              AND StartDate <= @EndDate
              AND EndDate >= @StartDate;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            int count = await connection.ExecuteScalarAsync<int>(
                sql,
                new
                {
                    CarId = carId,
                    StartDate = startDate.Date,
                    EndDate = endDate.Date,
                    ExcludedScheduleId = excludedScheduleId,
                    OperationalStatuses = FleetScheduleConstants.Status.Operational
                },
                transaction);

            return count > 0;
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<FleetSchedule?> GetConflictingScheduleAsync(
        int carId,
        DateTime startDate,
        DateTime endDate,
        int? excludedScheduleId = null,
        IDbTransaction? transaction = null)
    {
        string sql = $"""
            SELECT TOP (1)
                schedules.ScheduleId,
                schedules.CarId,
                schedules.CustomerId,
                cars.CarName,
                cars.PlateNumber,
                CustomerName = NULLIF(LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))), N''),
                schedules.Title,
                schedules.ScheduleType,
                schedules.Status,
                schedules.StartDate,
                schedules.EndDate,
                schedules.Notes,
                schedules.CreatedByUserId,
                schedules.CreatedAt,
                schedules.UpdatedAt,
                schedules.IsArchived
            FROM dbo.FleetSchedules AS schedules WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.Cars AS cars ON cars.CarId = schedules.CarId
            LEFT JOIN dbo.Customers AS customers ON customers.CustomerId = schedules.CustomerId
            WHERE schedules.CarId = @CarId
              AND schedules.IsArchived = 0
              AND schedules.Status IN @OperationalStatuses
              AND NOT (schedules.ScheduleType = N'{FleetScheduleConstants.Type.Maintenance}' AND schedules.Status = N'{FleetScheduleConstants.Status.Pending}')
              AND (@ExcludedScheduleId IS NULL OR schedules.ScheduleId <> @ExcludedScheduleId)
              AND schedules.StartDate <= @EndDate
              AND schedules.EndDate >= @StartDate
            ORDER BY schedules.StartDate, schedules.ScheduleId;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.QuerySingleOrDefaultAsync<FleetSchedule>(
                sql,
                new
                {
                    CarId = carId,
                    StartDate = startDate.Date,
                    EndDate = endDate.Date,
                    ExcludedScheduleId = excludedScheduleId,
                    OperationalStatuses = FleetScheduleConstants.Status.Operational
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

    public async Task<FleetSchedule?> GetActiveOrUpcomingOperationalScheduleAsync(int carId, DateTime referenceDate)
    {
        const string sql = """
            SELECT TOP (1)
                schedules.ScheduleId,
                schedules.CarId,
                schedules.CustomerId,
                cars.CarName,
                cars.PlateNumber,
                CustomerName = NULLIF(LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))), N''),
                schedules.Title,
                schedules.ScheduleType,
                schedules.Status,
                schedules.StartDate,
                schedules.EndDate,
                schedules.Notes,
                schedules.CreatedByUserId,
                schedules.CreatedAt,
                schedules.UpdatedAt,
                schedules.IsArchived
            FROM dbo.FleetSchedules AS schedules
            INNER JOIN dbo.Cars AS cars ON cars.CarId = schedules.CarId
            LEFT JOIN dbo.Customers AS customers ON customers.CustomerId = schedules.CustomerId
            WHERE schedules.CarId = @CarId
              AND schedules.IsArchived = 0
              AND schedules.Status IN @OperationalStatuses
              AND schedules.EndDate >= @ReferenceDate
            ORDER BY schedules.StartDate, schedules.ScheduleId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<FleetSchedule>(
            sql,
            new
            {
                CarId = carId,
                ReferenceDate = referenceDate.Date,
                OperationalStatuses = FleetScheduleConstants.Status.Operational
            });
    }

    public async Task<FleetSchedule?> GetActiveOrUpcomingOperationalScheduleForCustomerAsync(int customerId, DateTime referenceDate)
    {
        const string sql = """
            SELECT TOP (1)
                schedules.ScheduleId,
                schedules.CarId,
                schedules.CustomerId,
                cars.CarName,
                cars.PlateNumber,
                CustomerName = NULLIF(LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))), N''),
                schedules.Title,
                schedules.ScheduleType,
                schedules.Status,
                schedules.StartDate,
                schedules.EndDate,
                schedules.Notes,
                schedules.CreatedByUserId,
                schedules.CreatedAt,
                schedules.UpdatedAt,
                schedules.IsArchived
            FROM dbo.FleetSchedules AS schedules
            INNER JOIN dbo.Cars AS cars ON cars.CarId = schedules.CarId
            LEFT JOIN dbo.Customers AS customers ON customers.CustomerId = schedules.CustomerId
            WHERE schedules.CustomerId = @CustomerId
              AND schedules.IsArchived = 0
              AND schedules.Status IN @OperationalStatuses
              AND schedules.EndDate >= @ReferenceDate
            ORDER BY schedules.StartDate, schedules.ScheduleId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<FleetSchedule>(
            sql,
            new
            {
                CustomerId = customerId,
                ReferenceDate = referenceDate.Date,
                OperationalStatuses = FleetScheduleConstants.Status.Operational
            });
    }

    private async Task<IReadOnlyList<FleetSchedule>> GetSchedulesInRangeAsync(DateTime startDate, DateTime endDate)
    {
        const string sql = """
            SELECT
                schedules.ScheduleId,
                schedules.CarId,
                schedules.CustomerId,
                cars.CarName,
                cars.PlateNumber,
                CustomerName = NULLIF(LTRIM(RTRIM(CONCAT(customers.FirstName, N' ', customers.LastName))), N''),
                schedules.Title,
                schedules.ScheduleType,
                schedules.Status,
                schedules.StartDate,
                schedules.EndDate,
                schedules.Notes,
                schedules.CreatedByUserId,
                schedules.CreatedAt,
                schedules.UpdatedAt,
                schedules.IsArchived
            FROM dbo.FleetSchedules AS schedules
            INNER JOIN dbo.Cars AS cars ON cars.CarId = schedules.CarId
            LEFT JOIN dbo.Customers AS customers ON customers.CustomerId = schedules.CustomerId
            WHERE schedules.IsArchived = 0
              AND schedules.StartDate <= @EndDate
              AND schedules.EndDate >= @StartDate
            ORDER BY schedules.StartDate, schedules.ScheduleId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<FleetSchedule> schedules = await connection.QueryAsync<FleetSchedule>(
            sql,
            new
            {
                StartDate = startDate.Date,
                EndDate = endDate.Date
            });

        return schedules.ToList();
    }
}
