using System.Data;
using Microsoft.Data.SqlClient;
using NatarakiCarRental.Data;
using NatarakiCarRental.Exceptions;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;
using NatarakiCarRental.Validators;
using FluentValidation;
using FluentValidation.Results;

namespace NatarakiCarRental.Services;

public sealed class OffsiteService
{
    private static readonly HashSet<string> ValidWorkResults = new(StringComparer.OrdinalIgnoreCase)
    {
        "Completed",
        "Needs Follow-up",
        "Not Repaired"
    };

    private static readonly HashSet<string> AllowedProofExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".pdf"
    };

    private readonly OffsiteRepository _offsiteRepository;
    private readonly CarRepository _carRepository;
    private readonly FleetScheduleService _fleetScheduleService;
    private readonly ActivityLogService _activityLogService;
    private readonly NotificationService _notificationService = new();
    private readonly DbConnectionFactory _connectionFactory;
    private readonly OffsiteRecordValidator _validator = new();
    private readonly int? _currentUserId;

    public OffsiteService(int? currentUserId = null)
        : this(new DbConnectionFactory(), currentUserId)
    {
    }


    private OffsiteService(DbConnectionFactory connectionFactory, int? currentUserId)
        : this(
            new OffsiteRepository(connectionFactory),
            new CarRepository(connectionFactory),
            new FleetScheduleService(currentUserId),
            new ActivityLogService(connectionFactory),
            new NotificationService(),
            connectionFactory,
            currentUserId)
    {
    }

    public OffsiteService(
        OffsiteRepository offsiteRepository,
        CarRepository carRepository,
        FleetScheduleService fleetScheduleService,
        ActivityLogService activityLogService,
        NotificationService notificationService,
        DbConnectionFactory connectionFactory,
        int? currentUserId = null)
    {
        _offsiteRepository = offsiteRepository;
        _carRepository = carRepository;
        _fleetScheduleService = fleetScheduleService;
        _activityLogService = activityLogService;
        _notificationService = notificationService;
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

    public Task<OffsiteMetrics> GetMetricsAsync(DateTime referenceDate)
    {
        return _offsiteRepository.GetMetricsAsync(referenceDate);
    }

    public Task<OffsiteRecord?> GetByIdAsync(int offsiteRecordId)
    {
        return _offsiteRepository.GetByIdAsync(offsiteRecordId);
    }

    public Task<OffsiteRecord?> GetByFleetScheduleIdAsync(int fleetScheduleId)
    {
        return _offsiteRepository.GetByFleetScheduleIdAsync(fleetScheduleId);
    }

    public async Task<int> CreateAsync(CreateOffsiteRecordRequest request)
    {
        AccessControlService.EnforcePermission("Offsite.Create");
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
                    Status = FleetScheduleConstants.Status.Maintenance,
                    StartDate = request.StartDate,
                    EndDate = request.ExpectedReturnDate ?? request.StartDate,
                    Notes = $"Operational Offsite: {request.OffsiteType}"
                };
                
                scheduleId = await _fleetScheduleService.CreateFromInternalAsync(schedule, transaction);
            }
            else
            {
                // If scheduleId was provided (Create from Schedule tab), 
                // we should ensure it matches our criteria
                FleetSchedule? existingSchedule = await _fleetScheduleService.GetByIdAsync(scheduleId.Value);
                if (existingSchedule == null || existingSchedule.IsArchived || existingSchedule.ScheduleType != FleetScheduleConstants.Type.Maintenance)
                {
                    throw new ValidationException([new ValidationFailure("FleetScheduleId", "Selected maintenance schedule is invalid or archived.")]);
                }

                if (existingSchedule.Status != FleetScheduleConstants.Status.Pending)
                {
                    throw new ValidationException([new ValidationFailure("FleetScheduleId", "Only pending maintenance schedules can be started.")]);
                }

                // Update the existing schedule status to Maintenance
                existingSchedule.Status = FleetScheduleConstants.Status.Maintenance;
                await _fleetScheduleService.UpdateFromInternalAsync(existingSchedule, transaction);
            }

            // 2. Handle Proof File path
            string? finalProofPath = null;
            if (!string.IsNullOrWhiteSpace(request.ProofFilePath) && File.Exists(request.ProofFilePath))
            {
                finalProofPath = await UploadPathHelper.SaveOffsiteProofAsync(request.ProofFilePath);
            }

            string initialStatus = request.AmountPaid > 0 ? OffsiteConstants.Status.Reserved : OffsiteConstants.Status.Pending;

            // 3. Create OffsiteRecord
            OffsiteRecord record = new()
            {
                CarId = request.CarId,
                FleetScheduleId = scheduleId,
                OffsiteType = request.OffsiteType,
                Status = initialStatus,
                LocationName = request.LocationName,
                ContactPerson = request.ContactPerson,
                ContactNumber = request.ContactNumber,
                StartDate = request.StartDate,
                ExpectedReturnDate = request.ExpectedReturnDate,
                EstimatedCost = 0,
                ActualCost = 0,
                AmountPaid = request.AmountPaid,
                BalanceAmount = 0 - request.AmountPaid, // Balance will be calculated at completion when actual cost is known
                ModeOfPayment = request.ModeOfPayment,
                PaymentStatus = request.AmountPaid > 0 ? "Partial" : "Unpaid",
                ProofFilePath = finalProofPath,
                CreatedByUserId = _currentUserId
            };

            int recordId = await _offsiteRepository.AddAsync(record, transaction);

            await _activityLogService.LogAsync(
                "Created",
                "OffsiteRecord",
                recordId,
                $"Created {record.OffsiteType} offsite record for car #{record.CarId}.",
                userId: _currentUserId,
                entityName: $"OFF-{recordId:D4}",
                transaction: transaction);

            await _notificationService.NotifyAsync(
                "Maintenance Started",
                $"Maintenance ({record.OffsiteType}) started for car #{record.CarId}.",
                type: "Info",
                entityId: recordId,
                module: "OffsiteRecord",
                transaction: transaction);

            transaction.Commit();
            return recordId;
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    public async Task UpdateAsync(UpdateOffsiteRecordRequest request)
    {
        AccessControlService.EnforcePermission("Offsite.Edit");
        OffsiteRecord? existing = await _offsiteRepository.GetByIdAsync(request.OffsiteRecordId);
        if (existing == null || existing.IsArchived)
            throw new RecordNotFoundException($"Offsite record #{request.OffsiteRecordId} not found.");

        await ValidateUpdateRequestAsync(request, existing);

        OffsiteRecord oldRecord = new OffsiteRecord
        {
            OffsiteRecordId = existing.OffsiteRecordId,
            CarId = existing.CarId,
            FleetScheduleId = existing.FleetScheduleId,
            OffsiteType = existing.OffsiteType,
            Status = existing.Status,
            LocationName = existing.LocationName,
            ContactPerson = existing.ContactPerson,
            ContactNumber = existing.ContactNumber,
            StartDate = existing.StartDate,
            ExpectedReturnDate = existing.ExpectedReturnDate,
            CompletedDate = existing.CompletedDate,
            EstimatedCost = existing.EstimatedCost,
            ActualCost = existing.ActualCost,
            Notes = existing.Notes,
            ProofFilePath = existing.ProofFilePath,
            IsArchived = existing.IsArchived
        };

        // Create a temporary object with updated values for comparison
        OffsiteRecord updatedRecord = new OffsiteRecord
        {
            OffsiteRecordId = existing.OffsiteRecordId,
            CarId = existing.CarId,
            FleetScheduleId = existing.FleetScheduleId,
            OffsiteType = request.OffsiteType,
            Status = existing.Status,
            LocationName = request.LocationName,
            ContactPerson = request.ContactPerson,
            ContactNumber = request.ContactNumber,
            StartDate = request.StartDate,
            ExpectedReturnDate = request.ExpectedReturnDate,
            CompletedDate = existing.CompletedDate,
            EstimatedCost = existing.EstimatedCost,
            ActualCost = request.AmountPaid,
            Notes = existing.Notes,
            ProofFilePath = string.IsNullOrWhiteSpace(request.ProofFilePath) ? existing.ProofFilePath : request.ProofFilePath,
            IsArchived = existing.IsArchived
        };

        var (oldValue, newValue) = DiffHelper.GetJsonDiff(oldRecord, updatedRecord);
        if (oldValue == null) return; // Only log and update if ACTUAL changes occurred

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
                    await _fleetScheduleService.UpdateFromInternalAsync(schedule, transaction);
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
                "Updated",
                "OffsiteRecord",
                existing.OffsiteRecordId,
                $"Updated {existing.OffsiteType} offsite record for car #{existing.CarId}.",
                userId: _currentUserId,
                entityName: $"OFF-{existing.OffsiteRecordId:D4}",
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

    public Task CompleteAsync(int recordId, DateTime completedDate, decimal actualCost, string? notes)
    {
        return CompleteAsync(new CompleteOffsiteRecordRequest
        {
            OffsiteRecordId = recordId,
            CompletedDate = completedDate,
            WorkResult = "Completed",
            AmountPaid = actualCost,
            SuggestedNextAction = null,
            CompletedByUserId = _currentUserId
        });
    }

    public async Task CompleteAsync(CompleteOffsiteRecordRequest request)
    {
        AccessControlService.EnforcePermission("Offsite.Complete");
        OffsiteRecord? existing = await _offsiteRepository.GetByIdAsync(request.OffsiteRecordId);
        ValidateCompleteRequest(request, existing);
        if (existing is null)
        {
            throw new RecordNotFoundException("Record not found.");
        }

        string? previousProofPath = existing.ProofFilePath;
        string? finalProofPath = previousProofPath;
        bool proofChanged = !string.IsNullOrWhiteSpace(request.ProofFilePath)
            && !string.Equals(request.ProofFilePath, existing.ProofFilePath, StringComparison.OrdinalIgnoreCase);

        if (proofChanged && File.Exists(request.ProofFilePath))
        {
            finalProofPath = await UploadPathHelper.SaveOffsiteProofAsync(request.ProofFilePath);
        }

        CompleteOffsiteRecordRequest completion = new()
        {
            OffsiteRecordId = request.OffsiteRecordId,
            CompletedDate = request.CompletedDate.Date,
            WorkResult = request.WorkResult.Trim(),
            AmountPaid = request.AmountPaid,
            ProofFilePath = finalProofPath,
            FollowUpRequired = request.FollowUpRequired || RequiresFollowUp(request.WorkResult),
            FollowUpReason = NullIfWhiteSpace(request.FollowUpReason),
            SuggestedNextAction = null,
            CompletedByUserId = request.CompletedByUserId ?? _currentUserId
        };

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            if (existing.FleetScheduleId.HasValue)
            {
                FleetSchedule? schedule = await _fleetScheduleService.GetByIdAsync(existing.FleetScheduleId.Value);
                if (schedule != null)
                {
                    schedule.ScheduleType = FleetScheduleConstants.Type.Maintenance;
                    schedule.Status = FleetScheduleConstants.Status.Completed;
                    schedule.EndDate = completion.CompletedDate;
                    await _fleetScheduleService.UpdateFromInternalAsync(schedule, transaction);
                }
            }

            int affectedRows = await _offsiteRepository.CompleteAsync(completion, transaction);
            if (affectedRows == 0)
            {
                throw new RecordNotFoundException("Record not found or not in Ongoing status.");
            }

            await _activityLogService.LogAsync(
                "Completed",
                "OffsiteRecord",
                completion.OffsiteRecordId,
                BuildCompletionLogMessage(existing, completion),
                userId: _currentUserId,
                entityName: $"OFF-{completion.OffsiteRecordId:D4}",
                transaction: transaction);

            await _notificationService.NotifyAsync(
                "Maintenance Completed",
                $"Maintenance ({existing.OffsiteType}) completed for car #{existing.CarId}.",
                type: "Success",
                entityId: completion.OffsiteRecordId,
                module: "OffsiteRecord",
                transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            UploadPathHelper.DeleteNewOffsiteProofIfSaveFailed(finalProofPath, previousProofPath);
            RollbackQuietly(transaction);
            throw;
        }
    }

    public async Task CancelAsync(int recordId)
    {
        AccessControlService.EnforcePermission("Offsite.Cancel");
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
                    await _fleetScheduleService.UpdateFromInternalAsync(schedule, transaction);
                }
            }

            // 2. Cancel OffsiteRecord
            await _offsiteRepository.CancelAsync(recordId, transaction);

            await _activityLogService.LogAsync(
                "Cancelled",
                "OffsiteRecord",
                recordId,
                $"Cancelled offsite record for car #{existing.CarId}.",
                userId: _currentUserId,
                entityName: $"OFF-{recordId:D4}",
                transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    public async Task ArchiveAsync(int recordId)
    {
        AccessControlService.EnforcePermission("Offsite.ArchiveRestore");
        OffsiteRecord? existing = await _offsiteRepository.GetByIdAsync(recordId);
        if (existing == null) throw new RecordNotFoundException("Record not found.");
        if (existing.IsArchived) return;

        if (existing.Status == "Ongoing")
            throw new ValidationException([new ValidationFailure("Status", "Cannot archive an ongoing offsite record. Complete or cancel it first.")]);

        // Check for future linked schedule to prevent orphaning
        if (existing.FleetScheduleId.HasValue)
        {
            FleetSchedule? schedule = await _fleetScheduleService.GetByIdAsync(existing.FleetScheduleId.Value);
            if (schedule != null && !schedule.IsArchived && 
                schedule.Status != FleetScheduleConstants.Status.Completed && 
                schedule.Status != FleetScheduleConstants.Status.Cancelled &&
                schedule.StartDate.Date >= DateTime.Today)
            {
                throw new ValidationException([new ValidationFailure("Schedule", "Cannot archive an offsite record with an upcoming or active maintenance schedule. Cancel the schedule first.")]);
            }
        }

        int affectedRows = await _offsiteRepository.ArchiveAsync(recordId);
        if (affectedRows == 0) throw new RecordNotFoundException("Failed to archive record. It may have been deleted or modified.");

        await _activityLogService.LogAsync(
            "Archived",
            "OffsiteRecord",
            recordId,
            $"Archived offsite record #{recordId}.",
            userId: _currentUserId,
            entityName: $"OFF-{recordId:D4}");
    }

    private static void RollbackQuietly(SqlTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch
        {
            // Preserve original exception
        }
    }

    public async Task RestoreAsync(int recordId)
    {
        AccessControlService.EnforcePermission("Offsite.ArchiveRestore");
        OffsiteRecord? existing = await _offsiteRepository.GetByIdAsync(recordId);
        if (existing == null) throw new RecordNotFoundException("Record not found.");
        if (!existing.IsArchived) return;

        if (existing.Status == "Ongoing")
        {
            bool hasConflict = await _offsiteRepository.HasActiveOffsiteForCarAsync(existing.CarId);
            if (hasConflict)
                throw new ValidationException([new ValidationFailure("CarId", "Cannot restore because this car already has an ongoing offsite record.")]);
        }

        int affectedRows = await _offsiteRepository.RestoreAsync(recordId);
        if (affectedRows == 0) throw new RecordNotFoundException("Failed to restore record. It may have been deleted or modified.");

        await _activityLogService.LogAsync(
            "Restored",
            "OffsiteRecord",
            recordId,
            $"Restored offsite record #{recordId}.",
            userId: _currentUserId,
            entityName: $"OFF-{recordId:D4}");
    }

    private static void ValidateCompleteRequest(CompleteOffsiteRecordRequest request, OffsiteRecord? existing)
    {
        if (existing == null)
            throw new RecordNotFoundException("Record not found.");

        if (existing.IsArchived)
            throw new ValidationException([new ValidationFailure("IsArchived", "Cannot complete an archived offsite record.")]);

        if (existing.Status != "Ongoing")
            throw new ValidationException([new ValidationFailure("Status", "Only ongoing offsite records can be completed.")]);

        List<ValidationFailure> failures = [];

        if (request.CompletedDate.Date < existing.StartDate.Date)
            failures.Add(new ValidationFailure("CompletedDate", "Completed date cannot be before start date."));

        if (request.AmountPaid < 0)
            failures.Add(new ValidationFailure("AmountPaid", "Amount paid cannot be negative."));

        if (string.IsNullOrWhiteSpace(request.WorkResult))
            failures.Add(new ValidationFailure("WorkResult", "Work result is required."));
        else if (!ValidWorkResults.Contains(request.WorkResult))
            failures.Add(new ValidationFailure("WorkResult", "Work result is invalid."));

        bool requiresFollowUp = request.FollowUpRequired || RequiresFollowUp(request.WorkResult);

        if (requiresFollowUp && (string.IsNullOrWhiteSpace(request.FollowUpReason)
            || string.Equals(request.FollowUpReason, "Select a reason", StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add(new ValidationFailure("FollowUpReason", "Follow-up reason is required."));
        }

        string? proofPath = string.IsNullOrWhiteSpace(request.ProofFilePath) ? existing.ProofFilePath : request.ProofFilePath;
        if (request.AmountPaid > 0 && string.IsNullOrWhiteSpace(proofPath))
            failures.Add(new ValidationFailure("ProofFilePath", "Proof or receipt is required when amount paid is greater than zero."));

        if (!string.IsNullOrWhiteSpace(request.ProofFilePath))
        {
            string extension = Path.GetExtension(request.ProofFilePath);
            if (!AllowedProofExtensions.Contains(extension))
                failures.Add(new ValidationFailure("ProofFilePath", "Proof file must be a JPG, PNG, or PDF file."));
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);
    }

    private static string BuildCompletionLogMessage(OffsiteRecord record, CompleteOffsiteRecordRequest request)
    {
        string message = $"Completed offsite record for car #{record.CarId}. Result: {request.WorkResult}. Amount paid: ₱{request.AmountPaid:N2}.";

        if (request.FollowUpRequired && !string.IsNullOrWhiteSpace(request.FollowUpReason))
        {
            message += $" Follow-up required: {request.FollowUpReason.Trim()}";
        }

        return message;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool RequiresFollowUp(string? workResult)
    {
        return string.Equals(workResult, "Needs Follow-up", StringComparison.OrdinalIgnoreCase)
            || string.Equals(workResult, "Not Repaired", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ValidateCreateRequestAsync(CreateOffsiteRecordRequest request, IDbTransaction? transaction = null)
    {
        var result = await _validator.ValidateAsync(request);
        if (!result.IsValid) throw new ValidationException(result.Errors);

        if (request.ExpectedReturnDate.HasValue && request.ExpectedReturnDate.Value.Date < request.StartDate.Date)
            throw new ValidationException([new ValidationFailure("ExpectedReturnDate", "Expected return date cannot be before start date.")]);

        Car? car = await _carRepository.GetCarByIdAsync(request.CarId, DateTime.Today, transaction);
        if (car == null || car.IsArchived)
            throw new ValidationException([new ValidationFailure("CarId", "Selected car was not found or is archived.")]);

        bool hasActive = await _offsiteRepository.HasActiveOffsiteForCarAsync(request.CarId, null, transaction);
        if (hasActive)
            throw new ValidationException([new ValidationFailure("CarId", "This vehicle already has an active offsite maintenance record. Future maintenance planning should be scheduled through Fleet Schedule.")]);
    }

    private async Task ValidateUpdateRequestAsync(UpdateOffsiteRecordRequest request, OffsiteRecord existing)
    {
        var result = await _validator.ValidateAsync(new CreateOffsiteRecordRequest 
        { 
            CarId = existing.CarId,
            OffsiteType = request.OffsiteType,
            ContactNumber = request.ContactNumber
        });
        if (!result.IsValid) throw new ValidationException(result.Errors);

        if (request.ExpectedReturnDate.HasValue && request.ExpectedReturnDate.Value.Date < request.StartDate.Date)
            throw new ValidationException([new ValidationFailure("ExpectedReturnDate", "Expected return date cannot be before start date.")]);
    }
}
