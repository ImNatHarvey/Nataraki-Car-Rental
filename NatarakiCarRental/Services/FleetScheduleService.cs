using FluentValidation;
using FluentValidation.Results;
using Microsoft.Data.SqlClient;
using NatarakiCarRental.Data;
using NatarakiCarRental.Exceptions;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;

namespace NatarakiCarRental.Services;

public sealed class FleetScheduleService
{
    private readonly FleetScheduleRepository _scheduleRepository;
    private readonly CarRepository _carRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly ActivityLogService _activityLogService;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly int? _currentUserId;

    public FleetScheduleService(int? currentUserId)
        : this(new DbConnectionFactory(), currentUserId)
    {
    }

    private FleetScheduleService(DbConnectionFactory connectionFactory, int? currentUserId)
        : this(
            new FleetScheduleRepository(connectionFactory),
            new CarRepository(connectionFactory),
            new CustomerRepository(connectionFactory),
            new ActivityLogService(connectionFactory),
            connectionFactory,
            currentUserId)
    {
    }

    public FleetScheduleService(
        FleetScheduleRepository scheduleRepository,
        CarRepository carRepository,
        CustomerRepository customerRepository,
        ActivityLogService activityLogService,
        DbConnectionFactory connectionFactory,
        int? currentUserId = null)
    {
        _scheduleRepository = scheduleRepository;
        _carRepository = carRepository;
        _customerRepository = customerRepository;
        _activityLogService = activityLogService;
        _connectionFactory = connectionFactory;
        _currentUserId = currentUserId;
    }

    public Task<IReadOnlyList<FleetSchedule>> GetSchedulesForMonthAsync(int year, int month)
    {
        return _scheduleRepository.GetSchedulesForMonthAsync(year, month);
    }

    public Task<FleetSchedule?> GetByIdAsync(int scheduleId)
    {
        return _scheduleRepository.GetByIdAsync(scheduleId);
    }

    public async Task<int> CreateAsync(FleetSchedule schedule)
    {
        Normalize(schedule);
        await ValidateAsync(schedule);
        GenerateTitle(schedule);

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            schedule.CreatedByUserId = _currentUserId;
            int scheduleId = await _scheduleRepository.CreateAsync(schedule, transaction);
            await _activityLogService.LogAsync(
                "Create schedule",
                "FleetSchedule",
                scheduleId,
                $"Created {schedule.ScheduleType.ToLowerInvariant()} schedule '{schedule.Title}' for car #{schedule.CarId}.",
                userId: _currentUserId,
                transaction: transaction);
            transaction.Commit();
            return scheduleId;
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    public async Task UpdateAsync(FleetSchedule schedule)
    {
        Normalize(schedule);
        await ValidateAsync(schedule, excludedScheduleId: schedule.ScheduleId);
        GenerateTitle(schedule);

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            int affectedRows = await _scheduleRepository.UpdateAsync(schedule, transaction);

            if (affectedRows == 0)
            {
                throw new RecordNotFoundException($"Schedule record #{schedule.ScheduleId} was not found or is archived.");
            }

            await _activityLogService.LogAsync(
                "Update schedule",
                "FleetSchedule",
                schedule.ScheduleId,
                $"Updated schedule '{schedule.Title}' for car #{schedule.CarId}.",
                userId: _currentUserId,
                transaction: transaction);
            transaction.Commit();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    public async Task ArchiveAsync(int scheduleId)
    {
        FleetSchedule? schedule = await _scheduleRepository.GetByIdAsync(scheduleId);
        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            int affectedRows = await _scheduleRepository.ArchiveAsync(scheduleId, transaction);

            if (affectedRows == 0)
            {
                throw new RecordNotFoundException($"Schedule record #{scheduleId} was not found or is already archived.");
            }

            await _activityLogService.LogAsync(
                "Archive schedule",
                "FleetSchedule",
                scheduleId,
                $"Archived schedule '{schedule?.Title ?? $"#{scheduleId}"}'.",
                userId: _currentUserId,
                transaction: transaction);
            transaction.Commit();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    private async Task ValidateAsync(FleetSchedule schedule, int? excludedScheduleId = null)
    {
        List<ValidationFailure> failures = [];

        if (schedule.CarId <= 0)
        {
            failures.Add(new ValidationFailure(nameof(FleetSchedule.CarId), "Car is required."));
        }

        if (!FleetScheduleConstants.Type.All.Contains(schedule.ScheduleType))
        {
            failures.Add(new ValidationFailure(nameof(FleetSchedule.ScheduleType), "Schedule type is invalid."));
        }

        if (!FleetScheduleConstants.Status.All.Contains(schedule.Status))
        {
            failures.Add(new ValidationFailure(nameof(FleetSchedule.Status), "Status is invalid."));
        }

        if (!IsStatusAllowedForType(schedule.ScheduleType, schedule.Status))
        {
            failures.Add(new ValidationFailure(nameof(FleetSchedule.Status), "Status is invalid for the selected schedule type."));
        }

        if (schedule.StartDate.Date > schedule.EndDate.Date)
        {
            failures.Add(new ValidationFailure(nameof(FleetSchedule.EndDate), "End date must be on or after start date."));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        Car? car = await _carRepository.GetCarByIdAsync(schedule.CarId);

        if (car is null || car.IsArchived)
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(FleetSchedule.CarId), "Selected car was not found or is archived.")]);
        }

        if (schedule.CustomerId.HasValue)
        {
            Customer? customer = await _customerRepository.GetCustomerByIdAsync(schedule.CustomerId.Value);

            if (customer is null || customer.IsArchived)
            {
                throw new ValidationException(
                    [new ValidationFailure(nameof(FleetSchedule.CustomerId), "Selected customer was not found or is archived.")]);
            }

            if (excludedScheduleId is null && customer.IsBlacklisted)
            {
                throw new ValidationException(
                    [new ValidationFailure(nameof(FleetSchedule.CustomerId), "This customer is blacklisted and cannot be assigned to a new schedule.")]);
            }
        }

        FleetSchedule? conflict = !FleetScheduleConstants.Status.Operational.Contains(schedule.Status)
            ? null
            : await _scheduleRepository.GetConflictingScheduleAsync(
                schedule.CarId,
                schedule.StartDate,
                schedule.EndDate,
                excludedScheduleId);

        if (conflict is not null)
        {
            throw new ValidationException(
                [new ValidationFailure(
                    nameof(FleetSchedule.StartDate),
                    $"{car.CarName} ({car.PlateNumber}) already has '{conflict.Title}' scheduled from {conflict.StartDate:MMM d, yyyy} to {conflict.EndDate:MMM d, yyyy}.")]);
        }
    }

    private static void Normalize(FleetSchedule schedule)
    {
        schedule.Title = schedule.Title?.Trim() ?? string.Empty;
        schedule.ScheduleType = schedule.ScheduleType?.Trim() ?? string.Empty;
        schedule.Status = schedule.Status?.Trim() ?? string.Empty;
        schedule.StartDate = schedule.StartDate.Date;
        schedule.EndDate = schedule.EndDate.Date;
        schedule.Notes = string.IsNullOrWhiteSpace(schedule.Notes) ? null : schedule.Notes.Trim();
    }

    private static void GenerateTitle(FleetSchedule schedule)
    {
        int days = (schedule.EndDate.Date - schedule.StartDate.Date).Days + 1;
        string dayLabel = days == 1 ? "1 Day" : $"{days} Days";
        schedule.Title = $"{schedule.ScheduleType} ({schedule.Status}) - {dayLabel}";
    }

    private static bool IsStatusAllowedForType(string scheduleType, string status)
    {
        string[] allowedStatuses = scheduleType switch
        {
            FleetScheduleConstants.Type.Reservation => FleetScheduleConstants.Status.ReservationOptions,
            FleetScheduleConstants.Type.Rental => FleetScheduleConstants.Status.RentalOptions,
            FleetScheduleConstants.Type.Maintenance => FleetScheduleConstants.Status.MaintenanceOptions,
            _ => []
        };

        return allowedStatuses.Contains(status);
    }

    private static void RollbackQuietly(SqlTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch
        {
            // Preserve the original exception that caused rollback.
        }
    }
}
