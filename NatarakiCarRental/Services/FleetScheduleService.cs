using System.Data;
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
    private readonly TransactionRepository _transactionRepository;
    private readonly ActivityLogService _activityLogService;
    private readonly NotificationService _notificationService;
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
            new TransactionRepository(connectionFactory),
            new ActivityLogService(connectionFactory),
            new NotificationService(),
            connectionFactory,
            currentUserId)
    {
    }

    public FleetScheduleService(
        FleetScheduleRepository scheduleRepository,
        CarRepository carRepository,
        CustomerRepository customerRepository,
        TransactionRepository transactionRepository,
        ActivityLogService activityLogService,
        NotificationService notificationService,
        DbConnectionFactory connectionFactory,
        int? currentUserId = null)
    {
        _scheduleRepository = scheduleRepository;
        _carRepository = carRepository;
        _customerRepository = customerRepository;
        _transactionRepository = transactionRepository;
        _activityLogService = activityLogService;
        _notificationService = notificationService;
        _connectionFactory = connectionFactory;
        _currentUserId = currentUserId;
    }

    public Task<IReadOnlyList<FleetSchedule>> GetSchedulesForMonthAsync(int year, int month)
    {
        return _scheduleRepository.GetSchedulesForMonthAsync(year, month);
    }

    public async Task<IReadOnlyList<FleetSchedule>> GetMaintenanceSchedulesAsync()
    {
        var result = await _scheduleRepository.GetMaintenanceSchedulesAsync();
        return result ?? new List<FleetSchedule>();
    }

    public Task<FleetSchedule?> GetByIdAsync(int scheduleId)
    {
        return _scheduleRepository.GetByIdAsync(scheduleId);
    }

    public Task<FleetScheduleOverviewCounts> GetOverviewCountsAsync(DateTime referenceDate)
    {
        return _scheduleRepository.GetOverviewCountsAsync(referenceDate);
    }

    public Task<IReadOnlyList<FleetSchedule>> GetRecentUpcomingSchedulesAsync(DateTime referenceDate, int take)
    {
        return _scheduleRepository.GetRecentUpcomingSchedulesAsync(referenceDate, take);
    }

    public Task<IReadOnlyList<FleetSchedule>> GetEligibleReservationsAsync(DateTime referenceDate)
    {
        return _scheduleRepository.GetEligibleReservationsAsync(referenceDate);
    }

    public Task<bool> IsLinkedToActiveTransactionAsync(int scheduleId)
    {
        return _transactionRepository.HasActiveForFleetScheduleAsync(scheduleId);
    }

    public async Task PrepareForSaveAsync(FleetSchedule schedule, int? excludedScheduleId = null, bool isInternalWorkflow = false, IDbTransaction? transaction = null)
    {
        Normalize(schedule);
        await ValidateAsync(schedule, excludedScheduleId, isInternalWorkflow, transaction);
        GenerateTitle(schedule);
    }

    public async Task<int> CreateAsync(FleetSchedule schedule)
    {
        AccessControlService.EnforcePermission("FleetSchedule.Create");
        await PrepareForSaveAsync(schedule);

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            schedule.CreatedByUserId = _currentUserId;
            int scheduleId = await _scheduleRepository.CreateAsync(schedule, transaction);
            await _activityLogService.LogAsync(
                "Created",
                "FleetSchedule",
                scheduleId,
                $"Created {schedule.ScheduleType.ToLowerInvariant()} schedule '{schedule.Title}' for car #{schedule.CarId}.",
                userId: _currentUserId,
                entityName: schedule.Title,
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
        AccessControlService.EnforcePermission("FleetSchedule.Edit");
        await ValidateTransactionLifecycleLockAsync(schedule);

        FleetSchedule? oldSchedule = await _scheduleRepository.GetByIdAsync(schedule.ScheduleId);
        
        await PrepareForSaveAsync(schedule, excludedScheduleId: schedule.ScheduleId);

        var (oldValue, newValue) = DiffHelper.GetJsonDiff(oldSchedule, schedule);
        if (oldValue == null) return; // Only log and update if ACTUAL changes occurred

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
                "Updated",
                "FleetSchedule",
                schedule.ScheduleId,
                $"Updated schedule '{schedule.Title}' for car #{schedule.CarId}.",
                userId: _currentUserId,
                entityName: schedule.Title,
                oldValue: oldValue,
                newValue: newValue,
                transaction: transaction);
            transaction.Commit();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    public async Task UpdateScheduleFromTransactionAsync(FleetSchedule schedule)
    {
        // This is an internal workflow from Transaction module, bypass standard Edit permission check here 
        // because Transaction module has its own permission checks (e.g. Transactions.StartRental).
        
        await PrepareForSaveAsync(schedule, excludedScheduleId: schedule.ScheduleId, isInternalWorkflow: true);

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            int affectedRows = await _scheduleRepository.UpdateAsync(schedule, transaction);

            if (affectedRows == 0)
            {
                throw new RecordNotFoundException($"Schedule record #{schedule.ScheduleId} was not found or is archived.");
            }

            // Logging is handled by TransactionService for these events
            transaction.Commit();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    public async Task<int> CreateFromInternalAsync(FleetSchedule schedule, IDbTransaction transaction)
    {
        await PrepareForSaveAsync(schedule, transaction: transaction);
        schedule.CreatedByUserId = _currentUserId;
        return await _scheduleRepository.CreateAsync(schedule, transaction);
    }

    public async Task UpdateFromInternalAsync(FleetSchedule schedule, IDbTransaction transaction)
    {
        await PrepareForSaveAsync(schedule, excludedScheduleId: schedule.ScheduleId, isInternalWorkflow: true, transaction: transaction);
        int affectedRows = await _scheduleRepository.UpdateAsync(schedule, transaction);
        
        if (affectedRows == 0)
        {
            throw new RecordNotFoundException($"Schedule record #{schedule.ScheduleId} was not found or is archived.");
        }
    }

    private async Task ValidateTransactionLifecycleLockAsync(FleetSchedule schedule, IDbTransaction? transaction = null)
    {
        FleetSchedule? existingSchedule = await _scheduleRepository.GetByIdAsync(schedule.ScheduleId);

        if (existingSchedule is null || existingSchedule.IsArchived)
        {
            throw new RecordNotFoundException($"Schedule record #{schedule.ScheduleId} was not found or is archived.");
        }

        bool lifecycleChanged = existingSchedule.ScheduleType != schedule.ScheduleType
            || existingSchedule.Status != schedule.Status;

        if (!lifecycleChanged)
        {
            return;
        }

        if (await _transactionRepository.HasActiveForFleetScheduleAsync(schedule.ScheduleId, transaction))
        {
            throw new ValidationException(
                [new ValidationFailure(
                    nameof(FleetSchedule.Status),
                    "This schedule is linked to a transaction. Use the Transaction module to start, complete, cancel, or archive the rental.")]);
        }
    }

    public async Task ArchiveAsync(int scheduleId)
    {
        AccessControlService.EnforcePermission("FleetSchedule.Cancel");
        FleetSchedule? schedule = await _scheduleRepository.GetByIdAsync(scheduleId);
        if (schedule is null || schedule.IsArchived)
        {
            throw new RecordNotFoundException($"Schedule record #{scheduleId} was not found or is archived.");
        }

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            if (await _transactionRepository.HasActiveForFleetScheduleAsync(scheduleId, transaction))
            {
                throw new ValidationException(
                    [new ValidationFailure(
                        nameof(FleetSchedule.ScheduleId),
                        "This schedule is linked to a transaction. Archive or cancel it from the Transaction module first.")]);
            }

            int affectedRows = await _scheduleRepository.ArchiveAsync(scheduleId, transaction);

            if (affectedRows == 0)
            {
                throw new RecordNotFoundException($"Schedule record #{scheduleId} was not found or is already archived.");
            }

            await _activityLogService.LogAsync(
                "Archived",
                "FleetSchedule",
                scheduleId,
                $"Archived schedule '{schedule?.Title ?? $"#{scheduleId}"}'.",
                userId: _currentUserId,
                entityName: schedule?.Title ?? $"#{scheduleId}",
                transaction: transaction);
            transaction.Commit();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    private async Task ValidateAsync(FleetSchedule schedule, int? excludedScheduleId = null, bool isInternalWorkflow = false, IDbTransaction? transaction = null)
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

        if (!isInternalWorkflow && schedule.ScheduleType == FleetScheduleConstants.Type.Rental)
        {
            failures.Add(new ValidationFailure(nameof(FleetSchedule.ScheduleType), "Rental schedules can only be managed through the Transaction module."));
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

        Car? car = await _carRepository.GetCarByIdAsync(schedule.CarId, DateTime.Today, transaction);

        if (car is null || car.IsArchived)
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(FleetSchedule.CarId), "Selected car was not found or is archived.")]);
        }

        if (schedule.ScheduleType is FleetScheduleConstants.Type.Reservation or FleetScheduleConstants.Type.Rental
            && CodingDayValidationHelper.DateRangeContainsCodingDay(schedule.StartDate, schedule.EndDate, car.CodingDay))
        {
            throw new ValidationException(
                [new ValidationFailure(
                    nameof(FleetSchedule.StartDate),
                    $"This car cannot be rented on its coding day: {car.CodingDay}.")]);
        }

        if (schedule.CustomerId.HasValue)
        {
            Customer? customer = await _customerRepository.GetCustomerByIdAsync(schedule.CustomerId.Value, transaction);

            if (customer is null || customer.IsArchived)
            {
                throw new ValidationException(
                    [new ValidationFailure(nameof(FleetSchedule.CustomerId), "Selected customer was not found or is archived.")]);
            }

            if (customer.IsBlacklisted)
            {
                FleetSchedule? existingSchedule = excludedScheduleId.HasValue
                    ? await _scheduleRepository.GetByIdAsync(excludedScheduleId.Value)
                    : null;

                bool isExistingBlacklistedAssignment = existingSchedule?.CustomerId == schedule.CustomerId;

                if (!isExistingBlacklistedAssignment)
                {
                    throw new ValidationException(
                        [new ValidationFailure(nameof(FleetSchedule.CustomerId), "This customer is blacklisted and cannot be assigned to a new schedule.")]);
                }
            }
        }

        FleetSchedule? conflict = !FleetScheduleConstants.Status.Operational.Contains(schedule.Status)
            ? null
            : await _scheduleRepository.GetConflictingScheduleAsync(
                schedule.CarId,
                schedule.StartDate,
                schedule.EndDate,
                excludedScheduleId,
                transaction);

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
        if (string.IsNullOrWhiteSpace(schedule.Status))
        {
            schedule.Status = FleetScheduleVisualHelper.GetDefaultStatusForType(schedule.ScheduleType);
        }
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
