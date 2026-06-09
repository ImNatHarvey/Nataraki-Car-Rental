using System.Data;
using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Repositories;

public sealed class CarRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public CarRepository()
        : this(new DbConnectionFactory())
    {
    }

    public CarRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string CarStatusCte = $"""
        CarStatus AS (
            SELECT 
                c.*,
                CASE 
                    WHEN c.IsArchived = 1 THEN N'{CarConstants.Status.Archived}'
                    WHEN EXISTS (
                        SELECT 1 FROM dbo.Transactions t 
                        WHERE t.CarId = c.CarId AND t.TransactionStatus = N'{TransactionConstants.Status.Active}' AND t.IsArchived = 0
                    ) OR EXISTS (
                        SELECT 1 FROM dbo.FleetSchedules s 
                        WHERE s.CarId = c.CarId AND s.ScheduleType = N'{FleetScheduleConstants.Type.Rental}' AND s.Status IN @ActiveRentalStatuses AND s.IsArchived = 0 AND s.StartDate <= @ReferenceDate AND s.EndDate >= @ReferenceDate
                    ) THEN N'{CarConstants.Status.Rented}'
                    WHEN EXISTS (
                        SELECT 1 FROM dbo.OffsiteRecords o 
                        WHERE o.CarId = c.CarId AND o.Status = N'{OffsiteConstants.Status.Ongoing}' AND o.IsArchived = 0
                          AND o.OffsiteType IN @MaintenanceTypes AND o.StartDate <= @ReferenceDate AND ISNULL(o.ExpectedReturnDate, @ReferenceDate) >= @ReferenceDate
                    ) OR EXISTS (
                        SELECT 1 FROM dbo.Transactions t
                        WHERE t.CarId = c.CarId AND t.TransactionType = N'Maintenance' AND t.TransactionStatus = N'{TransactionConstants.Status.Maintenance}' AND t.IsArchived = 0
                          AND t.StartDate <= @ReferenceDate AND t.EndDate >= @ReferenceDate
                    ) THEN N'{CarConstants.Status.Maintenance}'
                    WHEN EXISTS (
                        SELECT 1 FROM dbo.FleetSchedules s 
                        WHERE s.CarId = c.CarId AND s.ScheduleType = N'{FleetScheduleConstants.Type.Reservation}' AND s.Status IN @ActiveReservationStatuses AND s.IsArchived = 0 AND s.StartDate <= @ReferenceDate AND s.EndDate >= @ReferenceDate
                    ) THEN N'{CarConstants.Status.Scheduled}'
                    ELSE N'{CarConstants.Status.Available}'
                END AS ComputedStatus
            FROM dbo.Cars c
        )
        """;

    public async Task<IReadOnlyList<Car>> SearchCarsAsync(string searchText, bool includeArchived, DateTime referenceDate, string? status = null, int pageNumber = 1, int pageSize = 50)
    {
        string normalizedSearchText = searchText?.Trim() ?? string.Empty;
        int offset = Math.Max(0, (pageNumber - 1) * pageSize);

        string sql = $"""
            WITH {CarStatusCte}
            SELECT
                CarId,
                CarName,
                Brand,
                Model,
                PlateNumber,
                [Year],
                Color,
                Transmission,
                FuelType,
                SeatingCapacity,
                RatePerDay,
                Status = ComputedStatus,
                CodingDay,
                Mileage,
                RegistrationExpirationDate,
                InsuranceExpirationDate,
                ImagePath,
                OrCrPath,
                IsArchived,
                CreatedAt,
                UpdatedAt,
                ArchivedAt
            FROM CarStatus
            WHERE IsArchived = @IsArchived
              AND (@Status IS NULL OR ComputedStatus = @Status)
              AND (
                    @SearchText = N''
                    OR CarName LIKE @SearchPattern
                    OR Model LIKE @SearchPattern
                    OR PlateNumber LIKE @SearchPattern
                  )
            ORDER BY CarId DESC
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<Car> cars = await connection.QueryAsync<Car>(
            sql,
            new
            {
                IsArchived = includeArchived,
                Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim(),
                SearchText = normalizedSearchText,
                SearchPattern = $"%{normalizedSearchText}%",
                ReferenceDate = referenceDate.Date,
                MaintenanceTypes = OffsiteConstants.Type.MaintenanceCategory,
                ActiveRentalStatuses = new[] { FleetScheduleConstants.Status.Ongoing, FleetScheduleConstants.Status.Rented },
                ActiveReservationStatuses = new[] { FleetScheduleConstants.Status.Pending },
                Offset = offset,
                PageSize = pageSize
            });

        return cars.ToList();
    }

    public async Task<int> CountCarsAsync(string searchText, bool includeArchived, DateTime referenceDate, string? status = null)
    {
        string normalizedSearchText = searchText?.Trim() ?? string.Empty;

        string sql = $"""
            WITH {CarStatusCte}
            SELECT COUNT(1)
            FROM CarStatus
            WHERE IsArchived = @IsArchived
              AND (@Status IS NULL OR ComputedStatus = @Status)
              AND (
                    @SearchText = N''
                    OR CarName LIKE @SearchPattern
                    OR Model LIKE @SearchPattern
                    OR PlateNumber LIKE @SearchPattern
                  );
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            sql,
            new
            {
                IsArchived = includeArchived,
                Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim(),
                SearchText = normalizedSearchText,
                SearchPattern = $"%{normalizedSearchText}%",
                ReferenceDate = referenceDate.Date,
                MaintenanceTypes = OffsiteConstants.Type.MaintenanceCategory,
                ActiveRentalStatuses = new[] { FleetScheduleConstants.Status.Ongoing, FleetScheduleConstants.Status.Rented },
                ActiveReservationStatuses = new[] { FleetScheduleConstants.Status.Pending }
            });
    }

    public async Task<Car?> GetCarByIdAsync(int carId, DateTime referenceDate, IDbTransaction? transaction = null)
    {
        string sql = $"""
            WITH {CarStatusCte}
            SELECT
                CarId,
                CarName,
                Brand,
                Model,
                PlateNumber,
                [Year],
                Color,
                Transmission,
                FuelType,
                SeatingCapacity,
                RatePerDay,
                Status = ComputedStatus,
                CodingDay,
                Mileage,
                RegistrationExpirationDate,
                InsuranceExpirationDate,
                ImagePath,
                OrCrPath,
                IsArchived,
                CreatedAt,
                UpdatedAt,
                ArchivedAt
            FROM CarStatus
            WHERE CarId = @CarId;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.QuerySingleOrDefaultAsync<Car>(
                sql,
                new
                {
                    CarId = carId,
                    ReferenceDate = referenceDate.Date,
                    MaintenanceTypes = OffsiteConstants.Type.MaintenanceCategory,
                    ActiveRentalStatuses = new[] { FleetScheduleConstants.Status.Ongoing, FleetScheduleConstants.Status.Rented },
                    ActiveReservationStatuses = new[] { FleetScheduleConstants.Status.Pending, FleetScheduleConstants.Status.Scheduled }
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

    public async Task<CarCounts> GetCarCountsAsync(DateTime referenceDate)
    {
        string sql = $"""
            WITH {CarStatusCte}
            SELECT
                TotalCars = COUNT(CASE WHEN IsArchived = 0 THEN 1 END),
                AvailableCars = COUNT(CASE WHEN IsArchived = 0 AND ComputedStatus = N'{CarConstants.Status.Available}' THEN 1 END),
                MaintenanceCars = COUNT(CASE WHEN IsArchived = 0 AND ComputedStatus = N'{CarConstants.Status.Maintenance}' THEN 1 END),
                RentedCars = COUNT(CASE WHEN IsArchived = 0 AND ComputedStatus = N'{CarConstants.Status.Rented}' THEN 1 END),
                ArchivedCars = COUNT(CASE WHEN IsArchived = 1 THEN 1 END)
            FROM CarStatus;
            """;

        using var connection = _connectionFactory.CreateConnection();
        CarCounts? counts = await connection.QuerySingleOrDefaultAsync<CarCounts>(
            sql,
            new
            {
                ReferenceDate = referenceDate.Date,
                MaintenanceTypes = OffsiteConstants.Type.MaintenanceCategory,
                ActiveRentalStatuses = new[] { FleetScheduleConstants.Status.Ongoing, FleetScheduleConstants.Status.Rented },
                ActiveReservationStatuses = new[] { FleetScheduleConstants.Status.Pending, FleetScheduleConstants.Status.Scheduled }
            });

        return counts ?? new CarCounts();
    }

    public async Task<bool> PlateNumberExistsAsync(string plateNumber, int? excludingCarId = null)
    {
        string normalizedPlateNumber = (plateNumber ?? string.Empty).Trim().ToUpperInvariant();

        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Cars
            WHERE PlateNumber = @PlateNumber
              AND (@ExcludingCarId IS NULL OR CarId <> @ExcludingCarId);
            """;

        using var connection = _connectionFactory.CreateConnection();
        int count = await connection.ExecuteScalarAsync<int>(
            sql,
            new
            {
                PlateNumber = normalizedPlateNumber,
                ExcludingCarId = excludingCarId
            });

        return count > 0;
    }

    public async Task<int> AddCarAsync(Car car, IDbTransaction? transaction = null)
    {
        const string sql = """
            INSERT INTO dbo.Cars
            (
                CarName,
                Brand,
                Model,
                PlateNumber,
                [Year],
                Color,
                Transmission,
                FuelType,
                SeatingCapacity,
                RatePerDay,
                Status,
                CodingDay,
                Mileage,
                RegistrationExpirationDate,
                InsuranceExpirationDate,
                ImagePath,
                OrCrPath
            )
            OUTPUT INSERTED.CarId
            VALUES
            (
                @CarName,
                @Brand,
                @Model,
                @PlateNumber,
                @Year,
                @Color,
                @Transmission,
                @FuelType,
                @SeatingCapacity,
                @RatePerDay,
                @Status,
                @CodingDay,
                @Mileage,
                @RegistrationExpirationDate,
                @InsuranceExpirationDate,
                @ImagePath,
                @OrCrPath
            );
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteScalarAsync<int>(
            sql,
            new
            {
                car.CarName,
                Brand = NullIfWhiteSpace(car.Brand),
                car.Model,
                PlateNumber = car.PlateNumber.Trim().ToUpperInvariant(),
                car.Year,
                Color = NullIfWhiteSpace(car.Color),
                Transmission = NullIfWhiteSpace(car.Transmission),
                FuelType = NullIfWhiteSpace(car.FuelType),
                car.SeatingCapacity,
                car.RatePerDay,
                car.Status,
                CodingDay = NullIfWhiteSpace(car.CodingDay),
                car.Mileage,
                car.RegistrationExpirationDate,
                car.InsuranceExpirationDate,
                ImagePath = NullIfWhiteSpace(car.ImagePath),
                OrCrPath = NullIfWhiteSpace(car.OrCrPath)
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

    public async Task<int> UpdateCarAsync(Car car, IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.Cars
            SET
                CarName = @CarName,
                Brand = @Brand,
                Model = @Model,
                PlateNumber = @PlateNumber,
                [Year] = @Year,
                Color = @Color,
                Transmission = @Transmission,
                FuelType = @FuelType,
                SeatingCapacity = @SeatingCapacity,
                RatePerDay = @RatePerDay,
                Status = @Status,
                CodingDay = @CodingDay,
                Mileage = @Mileage,
                RegistrationExpirationDate = @RegistrationExpirationDate,
                InsuranceExpirationDate = @InsuranceExpirationDate,
                ImagePath = @ImagePath,
                OrCrPath = @OrCrPath,
                UpdatedAt = sysdatetime()
            WHERE CarId = @CarId;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(
            sql,
            new
            {
                car.CarId,
                car.CarName,
                Brand = NullIfWhiteSpace(car.Brand),
                car.Model,
                PlateNumber = car.PlateNumber.Trim().ToUpperInvariant(),
                car.Year,
                Color = NullIfWhiteSpace(car.Color),
                Transmission = NullIfWhiteSpace(car.Transmission),
                FuelType = NullIfWhiteSpace(car.FuelType),
                car.SeatingCapacity,
                car.RatePerDay,
                car.Status,
                CodingDay = NullIfWhiteSpace(car.CodingDay),
                car.Mileage,
                car.RegistrationExpirationDate,
                car.InsuranceExpirationDate,
                ImagePath = NullIfWhiteSpace(car.ImagePath),
                OrCrPath = NullIfWhiteSpace(car.OrCrPath)
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

    public async Task<int> ArchiveCarAsync(int carId, IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.Cars
            SET IsArchived = 1,
                ArchivedAt = sysdatetime(),
                UpdatedAt = sysdatetime()
            WHERE CarId = @CarId
              AND IsArchived = 0;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(sql, new { CarId = carId }, transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<int> RestoreCarAsync(int carId, IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.Cars
            SET IsArchived = 0,
                ArchivedAt = NULL,
                UpdatedAt = sysdatetime()
            WHERE CarId = @CarId
              AND IsArchived = 1;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(sql, new { CarId = carId }, transaction);
        }
        finally
        {
            if (transaction is null)
            {
                connection.Dispose();
            }
        }
    }

    public async Task<int> UpdateStatusAsync(int carId, string status, IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.Cars
            SET Status = @Status,
                UpdatedAt = sysdatetime()
            WHERE CarId = @CarId
              AND IsArchived = 0;
            """;

        IDbConnection connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            return await connection.ExecuteAsync(sql, new { CarId = carId, Status = status }, transaction);
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
