using FluentValidation;
using FluentValidation.Results;
using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;

namespace NatarakiCarRental.Services;

public sealed class VehicleTrackingService
{
    private readonly VehicleLocationRepository _vehicleLocationRepository;
    private readonly CarRepository _carRepository;

    public VehicleTrackingService()
        : this(new VehicleLocationRepository(), new CarRepository())
    {
    }

    public VehicleTrackingService(VehicleLocationRepository vehicleLocationRepository, CarRepository carRepository)
    {
        _vehicleLocationRepository = vehicleLocationRepository;
        _carRepository = carRepository;
    }

    public Task<VehicleLocation?> GetLatestLocationAsync(int carId)
    {
        return _vehicleLocationRepository.GetLatestByCarIdAsync(carId);
    }

    public Task<IReadOnlyList<VehicleLocation>> GetLatestTrackedCarsAsync()
    {
        return _vehicleLocationRepository.GetLatestForActiveCarsAsync();
    }

    public Task<IReadOnlyList<Car>> GetTrackableCarsAsync()
    {
        return _carRepository.GetActiveCarsAsync();
    }

    public async Task<int> AddLocationAsync(VehicleLocation location)
    {
        await ValidateLocationAsync(location);
        location.Source = string.IsNullOrWhiteSpace(location.Source) ? "Simulator" : location.Source.Trim();
        location.RecordedAt = location.RecordedAt == default ? DateTime.Now : location.RecordedAt;
        return await _vehicleLocationRepository.AddAsync(location);
    }

    public Task<int> AddLocationAsync(
        int carId,
        decimal latitude,
        decimal longitude,
        decimal? speedKph = null,
        decimal? heading = null,
        string source = "Simulator")
    {
        return AddLocationAsync(new VehicleLocation
        {
            CarId = carId,
            Latitude = latitude,
            Longitude = longitude,
            SpeedKph = speedKph,
            Heading = heading,
            Source = source,
            RecordedAt = DateTime.Now
        });
    }

    private async Task ValidateLocationAsync(VehicleLocation location)
    {
        List<ValidationFailure> failures = [];

        if (location.CarId <= 0)
        {
            failures.Add(new ValidationFailure(nameof(VehicleLocation.CarId), "A car must be selected for tracking."));
        }

        if (location.Latitude is < -90 or > 90)
        {
            failures.Add(new ValidationFailure(nameof(VehicleLocation.Latitude), "Latitude must be between -90 and 90."));
        }

        if (location.Longitude is < -180 or > 180)
        {
            failures.Add(new ValidationFailure(nameof(VehicleLocation.Longitude), "Longitude must be between -180 and 180."));
        }

        if (location.SpeedKph < 0)
        {
            failures.Add(new ValidationFailure(nameof(VehicleLocation.SpeedKph), "Speed cannot be negative."));
        }

        Car? car = location.CarId > 0 ? await _carRepository.GetCarByIdAsync(location.CarId) : null;
        if (location.CarId > 0 && car is null)
        {
            failures.Add(new ValidationFailure(nameof(VehicleLocation.CarId), "The selected car was not found."));
        }
        else if (car?.IsArchived == true)
        {
            failures.Add(new ValidationFailure(nameof(VehicleLocation.CarId), "Archived cars cannot receive tracking updates."));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }
}
