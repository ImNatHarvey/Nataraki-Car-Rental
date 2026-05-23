using Microsoft.Data.SqlClient;
using NatarakiCarRental.Data;
using NatarakiCarRental.Exceptions;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;
using FluentValidation;
using FluentValidation.Results;

namespace NatarakiCarRental.Services;

public sealed class OffsiteService
{
    private readonly OffsiteRepository _offsiteRepository;
    private readonly CarRepository _carRepository;
    private readonly FleetScheduleService _fleetScheduleService;
    private readonly ActivityLogService _activityLogService;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly int? _currentUserId;

    public OffsiteService(int? currentUserId)
        : this(new DbConnectionFactory(), currentUserId)
    {
    }

    private OffsiteService(DbConnectionFactory connectionFactory, int? currentUserId)
        : this(
            new OffsiteRepository(connectionFactory),
            new CarRepository(connectionFactory),
            new FleetScheduleService(currentUserId),
            new ActivityLogService(connectionFactory),
            connectionFactory,
            currentUserId)
    {
    }

    public OffsiteService(
        OffsiteRepository offsiteRepository,
        CarRepository carRepository,
        FleetScheduleService fleetScheduleService,
        ActivityLogService activityLogService,
        DbConnectionFactory connectionFactory,
        int? currentUserId = null)
    {
        _offsiteRepository = offsiteRepository;
        _carRepository = carRepository;
        _fleetScheduleService = fleetScheduleService;
        _activityLogService = activityLogService;
        _connectionFactory = connectionFactory;
        _currentUserId = currentUserId;
    }

    public Task<IReadOnlyList<OffsiteRecordListItem>> GetListAsync(string? search, string? status, string? type, bool includeArchived, int page, int pageSize)
    {
        return _offsiteRepository.GetListAsync(search, status, type, includeArchived, page, pageSize);
    }

    public Task<int> CountAsync(string? search, string? status, string? type, bool includeArchived)
    {
        return _offsiteRepository.CountAsync(search, status, type, includeArchived);
    }

    public Task<OffsiteRecord?> GetByIdAsync(int offsiteRecordId)
    {
        return _offsiteRepository.GetByIdAsync(offsiteRecordId);
    }

    public async Task<int> CreateAsync(CreateOffsiteRecordRequest request)
    {
        await ValidateCreateRequestAsync(request);

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            int? scheduleId = request.FleetScheduleId;

            // 1. Handle FleetSchedule logic
            if (!scheduleId.HasValue)
            {
                // Create a new Maintenance schedule automatically if not provided from an existing one
                FleetSchedule schedule = new()
                {
                    CarId = request.CarId,
                    ScheduleType = FleetScheduleConstants.Type.Maintenance,
                    Status = FleetScheduleConstants.Status.Ongoing,
                    StartDate = request.StartDate,
                    EndDate = request.ExpectedReturnDate ?? request.StartDate,
                    Notes = $"Operational Offsite: {request.OffsiteType}"
                };
                
                // Use the fleet service logic so it handles title generation and conflict validation
                // Pass true for internal workflow if needed to bypass some UI-only checks
                scheduleId = await _fleetScheduleService.CreateAsync(schedule);
            }
            else
            {
                // If scheduleId was provided (Create from Schedule tab), 
                // we should ensure it matches our criteria (though validation should have caught this)
                FleetSchedule? existingSchedule = await _fleetScheduleService.GetByIdAsync(scheduleId.Value);
                if (existingSchedule == null || existingSchedule.IsArchived || existingSchedule.ScheduleType != FleetScheduleConstants.Type.Maintenance)
                {
                    throw new ValidationException([new ValidationFailure("FleetScheduleId", "Selected maintenance schedule is invalid or archived.")]);
                }

                // Update the existing schedule status to Ongoing if it's not already
                if (existingSchedule.Status != FleetScheduleConstants.Status.Ongoing)
                {
                    existingSchedule.Status = FleetScheduleConstants.Status.Ongoing;
                    await _fleetScheduleService.UpdateAsync(existingSchedule);
                }
            }

            // 2. Handle Proof File path
            string? finalProofPath = null;
            if (!string.IsNullOrWhiteSpace(request.ProofFilePath) && File.Exists(request.ProofFilePath))
            {
                finalProofPath = await UploadPathHelper.SaveOffsiteProofAsync(request.ProofFilePath);
            }

            // 3. Create OffsiteRecord
            OffsiteRecord record = new()
            {
                CarId = request.CarId,
                FleetScheduleId = scheduleId,
                OffsiteType = request.OffsiteType,
                Status = "Ongoing",
                LocationName = request.LocationName,
                ContactPerson = request.ContactPerson,
                ContactNumber = request.ContactNumber,
                StartDate = request.StartDate,
                ExpectedReturnDate = request.ExpectedReturnDate,
                EstimatedCost = 0,
                ActualCost = request.AmountPaid,
                ProofFilePath = finalProofPath,
                CreatedByUserId = _currentUserId
            };

            int recordId = await _offsiteRepository.AddAsync(record, transaction);

            await _activityLogService.LogAsync(
                "Create Offsite Record",
                "OffsiteRecord",
                recordId,
                $"Created {record.OffsiteType} offsite record for car #{record.CarId}.",
                userId: _currentUserId,
                transaction: transaction);

            transaction.Commit();
            return recordId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task UpdateAsync(UpdateOffsiteRecordRequest request)
    {
        OffsiteRecord? existing = await _offsiteRepository.GetByIdAsync(request.OffsiteRecordId);
        if (existing == null || existing.IsArchived)
            throw new RecordNotFoundException($"Offsite record #{request.OffsiteRecordId} not found.");

        await ValidateUpdateRequestAsync(request, existing);

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            // 1. Update linked FleetSchedule if exists
            if (existing.FleetScheduleId.HasValue)
            {
                FleetSchedule? schedule = await _fleetScheduleService.GetByIdAsync(existing.FleetScheduleId.Value);
                if (schedule != null)
                {
                    schedule.StartDate = request.StartDate;
                    schedule.EndDate = request.ExpectedReturnDate ?? request.StartDate;
                    schedule.Notes = $"Operational Offsite: {request.OffsiteType}";
                    await _fleetScheduleService.UpdateAsync(schedule);
                }
            }

            // 2. Handle Proof File replacement
            string? finalProofPath = existing.ProofFilePath;
            if (!string.IsNullOrWhiteSpace(request.ProofFilePath) && request.ProofFilePath != existing.ProofFilePath)
            {
                if (File.Exists(request.ProofFilePath))
                {
                    finalProofPath = await UploadPathHelper.SaveOffsiteProofAsync(request.ProofFilePath);
                }
            }

            // 3. Update OffsiteRecord
            existing.OffsiteType = request.OffsiteType;
            existing.LocationName = request.LocationName;
            existing.ContactPerson = request.ContactPerson;
            existing.ContactNumber = request.ContactNumber;
            existing.StartDate = request.StartDate;
            existing.ExpectedReturnDate = request.ExpectedReturnDate;
            existing.EstimatedCost = 0;
            existing.ActualCost = request.AmountPaid;
            existing.ProofFilePath = finalProofPath;

            await _offsiteRepository.UpdateAsync(existing, transaction);

            await _activityLogService.LogAsync(
                "Update Offsite Record",
                "OffsiteRecord",
                existing.OffsiteRecordId,
                $"Updated {existing.OffsiteType} offsite record for car #{existing.CarId}.",
                userId: _currentUserId,
                transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task CompleteAsync(int recordId, DateTime completedDate, decimal actualCost, string? notes)
    {
        OffsiteRecord? existing = await _offsiteRepository.GetByIdAsync(recordId);
        if (existing == null || existing.IsArchived || existing.Status != "Ongoing")
            throw new RecordNotFoundException("Record not found or not in Ongoing status.");

        if (completedDate.Date < existing.StartDate.Date)
            throw new ValidationException([new ValidationFailure("CompletedDate", "Completed date cannot be before start date.")]);

        if (actualCost < 0)
            throw new ValidationException([new ValidationFailure("ActualCost", "Actual cost cannot be negative.")]);

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            // 1. Complete linked FleetSchedule
            if (existing.FleetScheduleId.HasValue)
            {
                FleetSchedule? schedule = await _fleetScheduleService.GetByIdAsync(existing.FleetScheduleId.Value);
                if (schedule != null)
                {
                    schedule.Status = FleetScheduleConstants.Status.Completed;
                    schedule.EndDate = completedDate; // Actual end date
                    await _fleetScheduleService.UpdateAsync(schedule);
                }
            }

            // 2. Complete OffsiteRecord
            await _offsiteRepository.CompleteAsync(recordId, completedDate, actualCost, notes, transaction);

            await _activityLogService.LogAsync(
                "Complete Offsite Record",
                "OffsiteRecord",
                recordId,
                $"Completed offsite record for car #{existing.CarId}. Cost: ₱{actualCost:N2}",
                userId: _currentUserId,
                transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task CancelAsync(int recordId)
    {
        OffsiteRecord? existing = await _offsiteRepository.GetByIdAsync(recordId);
        if (existing == null || existing.IsArchived || existing.Status != "Ongoing")
            throw new RecordNotFoundException("Record not found or not in Ongoing status.");

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            // 1. Cancel linked FleetSchedule
            if (existing.FleetScheduleId.HasValue)
            {
                FleetSchedule? schedule = await _fleetScheduleService.GetByIdAsync(existing.FleetScheduleId.Value);
                if (schedule != null)
                {
                    schedule.Status = FleetScheduleConstants.Status.Cancelled;
                    await _fleetScheduleService.UpdateAsync(schedule);
                }
            }

            // 2. Cancel OffsiteRecord
            await _offsiteRepository.CancelAsync(recordId, transaction);

            await _activityLogService.LogAsync(
                "Cancel Offsite Record",
                "OffsiteRecord",
                recordId,
                $"Cancelled offsite record for car #{existing.CarId}.",
                userId: _currentUserId,
                transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task ArchiveAsync(int recordId)
    {
        OffsiteRecord? existing = await _offsiteRepository.GetByIdAsync(recordId);
        if (existing == null || existing.IsArchived) return;

        if (existing.Status == "Ongoing")
            throw new ValidationException([new ValidationFailure("Status", "Cannot archive an ongoing offsite record. Complete or cancel it first.")]);

        await _offsiteRepository.ArchiveAsync(recordId);
        await _activityLogService.LogAsync("Archive Offsite Record", "OffsiteRecord", recordId, $"Archived offsite record #{recordId}.");
    }

    public async Task RestoreAsync(int recordId)
    {
        OffsiteRecord? existing = await _offsiteRepository.GetByIdAsync(recordId);
        if (existing == null || !existing.IsArchived) return;

        if (existing.Status == "Ongoing")
        {
            bool hasConflict = await _offsiteRepository.HasActiveOffsiteForCarAsync(existing.CarId);
            if (hasConflict)
                throw new ValidationException([new ValidationFailure("CarId", "Cannot restore because this car already has an ongoing offsite record.")]);
        }

        await _offsiteRepository.RestoreAsync(recordId);
        await _activityLogService.LogAsync("Restore Offsite Record", "OffsiteRecord", recordId, $"Restored offsite record #{recordId}.");
    }

    private async Task ValidateCreateRequestAsync(CreateOffsiteRecordRequest request)
    {
        List<ValidationFailure> failures = [];

        if (request.CarId <= 0) failures.Add(new ValidationFailure("CarId", "Car is required."));
        if (string.IsNullOrWhiteSpace(request.OffsiteType)) failures.Add(new ValidationFailure("OffsiteType", "Offsite type is required."));
        if (request.AmountPaid < 0) failures.Add(new ValidationFailure("AmountPaid", "Amount paid cannot be negative."));
        if (request.ExpectedReturnDate.HasValue && request.ExpectedReturnDate.Value.Date < request.StartDate.Date)
            failures.Add(new ValidationFailure("ExpectedReturnDate", "Expected return date cannot be before start date."));

        if (failures.Count > 0) throw new ValidationException(failures);

        Car? car = await _carRepository.GetCarByIdAsync(request.CarId);
        if (car == null || car.IsArchived)
            throw new ValidationException([new ValidationFailure("CarId", "Selected car was not found or is archived.")]);

        bool hasActive = await _offsiteRepository.HasActiveOffsiteForCarAsync(request.CarId);
        if (hasActive)
            throw new ValidationException([new ValidationFailure("CarId", "This car already has an ongoing offsite record.")]);
    }

    private async Task ValidateUpdateRequestAsync(UpdateOffsiteRecordRequest request, OffsiteRecord existing)
    {
        List<ValidationFailure> failures = [];

        if (string.IsNullOrWhiteSpace(request.OffsiteType)) failures.Add(new ValidationFailure("OffsiteType", "Offsite type is required."));
        if (request.AmountPaid < 0) failures.Add(new ValidationFailure("AmountPaid", "Amount paid cannot be negative."));
        if (request.ExpectedReturnDate.HasValue && request.ExpectedReturnDate.Value.Date < request.StartDate.Date)
            failures.Add(new ValidationFailure("ExpectedReturnDate", "Expected return date cannot be before start date."));

        if (failures.Count > 0) throw new ValidationException(failures);
    }
}
