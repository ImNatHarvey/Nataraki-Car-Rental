using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Repositories;

public sealed class VehicleLocationRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public VehicleLocationRepository()
        : this(new DbConnectionFactory())
    {
    }

    public VehicleLocationRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<VehicleLocation?> GetLatestByCarIdAsync(int carId)
    {
        const string sql = """
            SELECT TOP (1)
                location.VehicleLocationId,
                location.CarId,
                location.Latitude,
                location.Longitude,
                location.SpeedKph,
                location.Heading,
                location.Source,
                location.RecordedAt,
                location.IsArchived,
                car.CarName,
                car.PlateNumber
            FROM dbo.VehicleLocations AS location
            INNER JOIN dbo.Cars AS car ON car.CarId = location.CarId
            WHERE location.CarId = @CarId
              AND location.IsArchived = 0
            ORDER BY location.RecordedAt DESC, location.VehicleLocationId DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<VehicleLocation>(sql, new { CarId = carId });
    }

    public async Task<IReadOnlyList<VehicleLocation>> GetLatestForActiveCarsAsync()
    {
        const string sql = """
            SELECT
                latest.VehicleLocationId,
                latest.CarId,
                latest.Latitude,
                latest.Longitude,
                latest.SpeedKph,
                latest.Heading,
                latest.Source,
                latest.RecordedAt,
                latest.IsArchived,
                car.CarName,
                car.PlateNumber
            FROM dbo.Cars AS car
            CROSS APPLY (
                SELECT TOP (1)
                    location.VehicleLocationId,
                    location.CarId,
                    location.Latitude,
                    location.Longitude,
                    location.SpeedKph,
                    location.Heading,
                    location.Source,
                    location.RecordedAt,
                    location.IsArchived
                FROM dbo.VehicleLocations AS location
                WHERE location.CarId = car.CarId
                  AND location.IsArchived = 0
                ORDER BY location.RecordedAt DESC, location.VehicleLocationId DESC
            ) AS latest
            WHERE car.IsArchived = 0
            ORDER BY car.CarName, car.PlateNumber;
            """;

        using var connection = _connectionFactory.CreateConnection();
        IEnumerable<VehicleLocation> locations = await connection.QueryAsync<VehicleLocation>(sql);
        return locations.ToList();
    }

    public async Task<int> AddAsync(VehicleLocation location)
    {
        const string sql = """
            INSERT INTO dbo.VehicleLocations
            (
                CarId,
                Latitude,
                Longitude,
                SpeedKph,
                Heading,
                Source,
                RecordedAt,
                IsArchived
            )
            OUTPUT INSERTED.VehicleLocationId
            VALUES
            (
                @CarId,
                @Latitude,
                @Longitude,
                @SpeedKph,
                @Heading,
                @Source,
                COALESCE(@RecordedAt, sysdatetime()),
                0
            );
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            sql,
            new
            {
                location.CarId,
                location.Latitude,
                location.Longitude,
                location.SpeedKph,
                location.Heading,
                Source = string.IsNullOrWhiteSpace(location.Source) ? "Simulator" : location.Source.Trim(),
                RecordedAt = location.RecordedAt == default ? (DateTime?)null : location.RecordedAt
            });
    }
}
