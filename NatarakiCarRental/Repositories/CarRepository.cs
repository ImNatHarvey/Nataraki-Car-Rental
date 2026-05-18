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

    public async Task<IReadOnlyList<Car>> GetActiveCarsAsync()
    {
        return await SearchCarsAsync(string.Empty, includeArchived: false);
    }

    public async Task<IReadOnlyList<Car>> GetArchivedCarsAsync()
    {
        return await SearchCarsAsync(string.Empty, includeArchived: true);
    }

    public async Task<IReadOnlyList<Car>> SearchCarsAsync(string searchText, bool includeArchived)
    {
        string normalizedSearchText = searchText?.Trim() ?? string.Empty;

        const string sql = """
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
                Status,
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
            FROM dbo.Cars
            WHERE IsArchived = @IsArchived
              AND (
                    @SearchText = N''
                    OR CarName LIKE @SearchPattern
                    OR Model LIKE @SearchPattern
                    OR PlateNumber LIKE @SearchPattern
                  )
            ORDER BY CarId DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<Car> cars = await connection.QueryAsync<Car>(
            sql,
            new
            {
                IsArchived = includeArchived,
                SearchText = normalizedSearchText,
                SearchPattern = $"%{normalizedSearchText}%"
            });

        return cars.ToList();
    }

    public async Task<Car?> GetCarByIdAsync(int carId)
    {
        const string sql = """
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
                Status,
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
            FROM dbo.Cars
            WHERE CarId = @CarId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Car>(sql, new { CarId = carId });
    }

    public async Task<CarCounts> GetCarCountsAsync()
    {
        const string sql = """
            SELECT
                TotalCars = COUNT(CASE WHEN IsArchived = 0 THEN 1 END),
                AvailableCars = COUNT(CASE WHEN IsArchived = 0 AND Status = @AvailableStatus THEN 1 END),
                MaintenanceCars = COUNT(CASE WHEN IsArchived = 0 AND Status = @MaintenanceStatus THEN 1 END),
                RentedCars = COUNT(CASE WHEN IsArchived = 0 AND Status = @RentedStatus THEN 1 END),
                ArchivedCars = COUNT(CASE WHEN IsArchived = 1 THEN 1 END)
            FROM dbo.Cars;
            """;

        using var connection = _connectionFactory.CreateConnection();
        CarCounts? counts = await connection.QuerySingleOrDefaultAsync<CarCounts>(
            sql,
            new
            {
                AvailableStatus = CarConstants.Status.Available,
                MaintenanceStatus = CarConstants.Status.Maintenance,
                RentedStatus = CarConstants.Status.Rented
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

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
