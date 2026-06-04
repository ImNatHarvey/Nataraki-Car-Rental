using FluentValidation;
using FluentValidation.Results;
using Microsoft.Data.SqlClient;
using NatarakiCarRental.Data;
using NatarakiCarRental.Exceptions;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;
using NatarakiCarRental.Validators;

namespace NatarakiCarRental.Services;

public sealed class CarService
{
    private readonly CarRepository _carRepository;
    private readonly FleetScheduleRepository _fleetScheduleRepository;
    private readonly ActivityLogService _activityLogService;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly int? _currentUserId;

    public CarService()
        : this(currentUserId: null)
    {
    }

    public CarService(int? currentUserId)
        : this(new DbConnectionFactory(), currentUserId)
    {
    }

    private CarService(DbConnectionFactory connectionFactory, int? currentUserId)
        : this(
            new CarRepository(connectionFactory),
            new FleetScheduleRepository(connectionFactory),
            new ActivityLogService(connectionFactory),
            connectionFactory,
            currentUserId)
    {
    }

    public CarService(CarRepository carRepository, ActivityLogService activityLogService)
        : this(carRepository, new FleetScheduleRepository(), activityLogService, new DbConnectionFactory(), currentUserId: null)
    {
    }

    public CarService(
        CarRepository carRepository,
        FleetScheduleRepository fleetScheduleRepository,
        ActivityLogService activityLogService,
        DbConnectionFactory connectionFactory,
        int? currentUserId = null)
    {
        _carRepository = carRepository;
        _fleetScheduleRepository = fleetScheduleRepository;
        _activityLogService = activityLogService;
        _connectionFactory = connectionFactory;
        _currentUserId = currentUserId;
    }

    public async Task<IReadOnlyList<Car>> GetActiveCarsAsync()
    {
        return await _carRepository.GetActiveCarsAsync(DateTime.Today);
    }

    public async Task<IReadOnlyList<Car>> GetArchivedCarsAsync()
    {
        return await _carRepository.GetArchivedCarsAsync(DateTime.Today);
    }

    public async Task<IReadOnlyList<Car>> SearchCarsAsync(string searchText, bool includeArchived)
    {
        return await _carRepository.SearchCarsAsync(searchText, includeArchived, DateTime.Today);
    }

    public Task<CarCounts> GetCarCountsAsync()
    {
        return _carRepository.GetCarCountsAsync(DateTime.Today);
    }

    public async Task<Car?> GetCarByIdAsync(int carId)
    {
        return await _carRepository.GetCarByIdAsync(carId, DateTime.Today);
    }

    public Task<bool> PlateNumberExistsAsync(string plateNumber, int? excludingCarId = null)
    {
        return _carRepository.PlateNumberExistsAsync(plateNumber, excludingCarId);
    }

    public async Task<int> AddCarAsync(Car car)
    {
        AccessControlService.EnforcePermission("Cars.Create");
        NormalizeCar(car);

        CarValidator validator = new();
        validator.ValidateAndThrow(car);

        bool plateExists = await _carRepository.PlateNumberExistsAsync(car.PlateNumber);

        if (plateExists)
        {
            throw new ValidationException("Plate number already exists.");
        }

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            int carId = await _carRepository.AddCarAsync(car, transaction);
            await _activityLogService.LogAsync(
                "Added",
                "Car",
                carId,
                $"Added car {car.CarName} ({car.PlateNumber}).",
                userId: _currentUserId,
                entityName: $"{car.Brand} {car.Model} ({car.PlateNumber})",
                transaction: transaction);

            transaction.Commit();
            return carId;
        }
        catch (SqlException exception) when (IsUniqueConstraintViolation(exception))
        {
            RollbackQuietly(transaction);
            throw CreateDuplicatePlateValidationException();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    public async Task UpdateCarAsync(Car car)
    {
        AccessControlService.EnforcePermission("Cars.Edit");
        NormalizeCar(car);

        CarValidator validator = new();
        validator.ValidateAndThrow(car);

        bool plateExists = await _carRepository.PlateNumberExistsAsync(car.PlateNumber, car.CarId);

        if (plateExists)
        {
            throw new ValidationException("Plate number already exists.");
        }

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            int affectedRows = await _carRepository.UpdateCarAsync(car, transaction);

            if (affectedRows == 0)
            {
                throw new RecordNotFoundException($"Car record #{car.CarId} was not found.");
            }

            await _activityLogService.LogAsync(
                "Updated",
                "Car",
                car.CarId,
                $"Updated car {car.CarName} ({car.PlateNumber}).",
                userId: _currentUserId,
                entityName: $"{car.Brand} {car.Model} ({car.PlateNumber})",
                transaction: transaction);

            transaction.Commit();
        }
        catch (SqlException exception) when (IsUniqueConstraintViolation(exception))
        {
            RollbackQuietly(transaction);
            throw CreateDuplicatePlateValidationException();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    public async Task ArchiveCarAsync(int carId)
    {
        AccessControlService.EnforcePermission("Cars.ArchiveRestore");
        Car? car = await _carRepository.GetCarByIdAsync(carId, DateTime.Today);
        FleetSchedule? blockingSchedule = await _fleetScheduleRepository.GetActiveOrUpcomingOperationalScheduleAsync(carId, DateTime.Today);

        if (blockingSchedule is not null)
        {
            throw new ValidationException(
                [new ValidationFailure(
                    nameof(Car.CarId),
                    $"This car cannot be archived because it has an active or upcoming schedule: '{blockingSchedule.Title}' from {blockingSchedule.StartDate:MMM d, yyyy} to {blockingSchedule.EndDate:MMM d, yyyy}.")]);
        }

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            int affectedRows = await _carRepository.ArchiveCarAsync(carId, transaction);

            if (affectedRows == 0)
            {
                throw new RecordNotFoundException($"Car record #{carId} was not found or is already archived.");
            }

            await _activityLogService.LogAsync(
                "Archived",
                "Car",
                carId,
                $"Archived car {DescribeCar(car, carId)}.",
                userId: _currentUserId,
                entityName: car != null ? $"{car.Brand} {car.Model} ({car.PlateNumber})" : $"#{carId}",
                transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    public async Task RestoreCarAsync(int carId)
    {
        AccessControlService.EnforcePermission("Cars.ArchiveRestore");
        Car? car = await _carRepository.GetCarByIdAsync(carId, DateTime.Today);
        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            int affectedRows = await _carRepository.RestoreCarAsync(carId, transaction);

            if (affectedRows == 0)
            {
                throw new RecordNotFoundException($"Car record #{carId} was not found or is not archived.");
            }

            await _activityLogService.LogAsync(
                "Restored",
                "Car",
                carId,
                $"Restored car {DescribeCar(car, carId)}.",
                userId: _currentUserId,
                entityName: car != null ? $"{car.Brand} {car.Model} ({car.PlateNumber})" : $"#{carId}",
                transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    private static void NormalizeCar(Car car)
    {
        car.CarName = car.CarName?.Trim() ?? string.Empty;
        car.Brand = car.Brand?.Trim() ?? string.Empty;
        car.Model = car.Model?.Trim() ?? string.Empty;
        car.PlateNumber = PlateNumberHelper.FormatPhilippinePlateInput(car.PlateNumber);
        car.Color = NullIfWhiteSpace(car.Color);
        car.Transmission = NullIfWhiteSpace(car.Transmission);
        car.FuelType = NullIfWhiteSpace(car.FuelType);
        car.CodingDay = NullIfWhiteSpace(car.CodingDay);
        car.ImagePath = NullIfWhiteSpace(car.ImagePath);
        car.OrCrPath = NullIfWhiteSpace(car.OrCrPath);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string DescribeCar(Car? car, int carId)
    {
        return car is null ? $"#{carId}" : $"{car.CarName} ({car.PlateNumber})";
    }

    private static bool IsUniqueConstraintViolation(SqlException exception)
    {
        return exception.Number is 2601 or 2627;
    }

    private static ValidationException CreateDuplicatePlateValidationException()
    {
        return new ValidationException(
            [new ValidationFailure(nameof(Car.PlateNumber), "Plate number already exists.")]);
    }

    private static void RollbackQuietly(SqlTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch
        {
            // Preserve the original exception that caused the rollback.
        }
    }
}
