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

public sealed class TransactionService
{
    private const int MaxTransactionCodeCreateAttempts = 3;
    private readonly TransactionRepository _transactionRepository;
    private readonly TransactionPaymentRepository _transactionPaymentRepository;
    private readonly FleetScheduleRepository _scheduleRepository;
    private readonly FleetScheduleService _scheduleService;
    private readonly CarRepository _carRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly ActivityLogService _activityLogService;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly int? _currentUserId;

    public TransactionService(int? currentUserId = null)
        : this(new DbConnectionFactory(), currentUserId)
    {
    }

    private TransactionService(DbConnectionFactory connectionFactory, int? currentUserId)
        : this(
            new TransactionRepository(connectionFactory),
            new TransactionPaymentRepository(connectionFactory),
            new FleetScheduleRepository(connectionFactory),
            new FleetScheduleService(
                new FleetScheduleRepository(connectionFactory),
                new CarRepository(connectionFactory),
                new CustomerRepository(connectionFactory),
                new TransactionRepository(connectionFactory),
                new ActivityLogService(connectionFactory),
                connectionFactory,
                currentUserId),
            new CarRepository(connectionFactory),
            new CustomerRepository(connectionFactory),
            new ActivityLogService(connectionFactory),
            connectionFactory,
            currentUserId)
    {
    }

    public TransactionService(
        TransactionRepository transactionRepository,
        TransactionPaymentRepository transactionPaymentRepository,
        FleetScheduleRepository scheduleRepository,
        FleetScheduleService scheduleService,
        CarRepository carRepository,
        CustomerRepository customerRepository,
        ActivityLogService activityLogService,
        DbConnectionFactory connectionFactory,
        int? currentUserId = null)
    {
        _transactionRepository = transactionRepository;
        _transactionPaymentRepository = transactionPaymentRepository;
        _scheduleRepository = scheduleRepository;
        _scheduleService = scheduleService;
        _carRepository = carRepository;
        _customerRepository = customerRepository;
        _activityLogService = activityLogService;
        _connectionFactory = connectionFactory;
        _currentUserId = currentUserId;
    }

    public Task<Transaction?> GetByIdAsync(int transactionId)
    {
        return _transactionRepository.GetByIdAsync(transactionId);
    }

    public Task<IReadOnlyList<TransactionListItem>> SearchTransactionsAsync(
        string searchText,
        string? transactionStatus = null,
        string? paymentStatus = null,
        bool includeArchived = false,
        int maxRows = 100)
    {
        return _transactionRepository.SearchAsync(searchText, transactionStatus, paymentStatus, includeArchived, maxRows);
    }

    public Task<TransactionMetrics> GetMetricsAsync(DateTime referenceDate)
    {
        return _transactionRepository.GetMetricsAsync(referenceDate);
    }

    public Task<IReadOnlyList<TransactionListItem>> GetRecentTransactionsAsync(int take)
    {
        return _transactionRepository.GetRecentAsync(take);
    }

    public async Task<int> CreateFromReservationAsync(CreateTransactionFromReservationRequest request)
    {
        FleetSchedule? reservation = await _scheduleRepository.GetByIdAsync(request.FleetScheduleId);

        if (reservation is null || reservation.IsArchived)
        {
            throw Validation(nameof(CreateTransactionFromReservationRequest.FleetScheduleId), "Reservation schedule was not found or is archived.");
        }

        if (reservation.ScheduleType != FleetScheduleConstants.Type.Reservation
            || reservation.Status is not FleetScheduleConstants.Status.Pending and not FleetScheduleConstants.Status.Reserved)
        {
            throw Validation(nameof(CreateTransactionFromReservationRequest.FleetScheduleId), "Only pending or reserved reservation schedules can be converted into transactions.");
        }

        if (!reservation.CustomerId.HasValue)
        {
            throw Validation(nameof(FleetSchedule.CustomerId), "A customer is required before a reservation can become a rental transaction.");
        }

        Customer customer = await GetEligibleCustomerAsync(reservation.CustomerId.Value);
        Car car = await GetEligibleCarAsync(reservation.CarId);
        DateTime startDate = request.StartDate?.Date ?? reservation.StartDate.Date;
        DateTime endDate = request.EndDate?.Date ?? reservation.EndDate.Date;
        decimal dailyRate = request.DailyRate ?? car.RatePerDay;
        ValidateCommercialInputs(startDate, endDate, dailyRate, request.AmountPaid, request.ModeOfPayment);

        if (await _transactionRepository.HasActiveForFleetScheduleAsync(reservation.ScheduleId))
        {
            throw Validation(nameof(CreateTransactionFromReservationRequest.FleetScheduleId), "This reservation already has a transaction.");
        }

        FleetSchedule reservedSchedule = CreateReservationSchedule(
            reservation.ScheduleId,
            car.CarId,
            customer.CustomerId,
            startDate,
            endDate,
            reservation.Notes,
            GetReservationStatusForPaidAmount(request.AmountPaid));
        await _scheduleService.PrepareForSaveAsync(reservedSchedule, reservation.ScheduleId);

        Transaction transaction = BuildTransaction(
            reservation.ScheduleId,
            customer.CustomerId,
            car.CarId,
            startDate,
            endDate,
            dailyRate,
            request.ModeOfPayment,
            request.AmountPaid,
            request.Notes,
            GetReservationTransactionStatusForPaidAmount(request.AmountPaid));

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction dbTransaction = connection.BeginTransaction();

        try
        {
            int affectedRows = await _scheduleRepository.UpdateAsync(reservedSchedule, dbTransaction);

            if (affectedRows == 0)
            {
                throw new RecordNotFoundException($"Reservation schedule #{reservation.ScheduleId} was not found or is archived.");
            }

            int transactionId = await CreateWithUniqueCodeAsync(transaction, dbTransaction);

            if (request.AmountPaid > 0)
            {
                await _transactionPaymentRepository.AddAsync(
                    new TransactionPayment
                    {
                        TransactionId = transactionId,
                        PaymentDate = DateTime.Now,
                        Amount = request.AmountPaid,
                        ModeOfPayment = request.ModeOfPayment.Trim(),
                        ReceiptFilePath = request.ReceiptFilePath,
                        Notes = "Initial payment",
                        CreatedByUserId = _currentUserId
                    },
                    dbTransaction);
                await RecalculatePaymentSummaryAsync(transactionId, dbTransaction);
            }

            await _activityLogService.LogAsync(
                "Create transaction",
                "Transaction",
                transactionId,
                $"Created transaction {transaction.TransactionCode} from reservation schedule #{reservation.ScheduleId}.",
                _currentUserId,
                dbTransaction);
            await _activityLogService.LogAsync(
                "Reserve transaction",
                "FleetSchedule",
                reservation.ScheduleId,
                $"Linked reservation schedule #{reservation.ScheduleId} to reserved transaction {transaction.TransactionCode}.",
                _currentUserId,
                dbTransaction);
            dbTransaction.Commit();
            return transactionId;
        }
        catch
        {
            RollbackQuietly(dbTransaction);
            throw;
        }
    }

    public async Task<int> CreateWalkInTransactionAsync(CreateWalkInTransactionRequest request)
    {
        string transactionType = string.IsNullOrWhiteSpace(request.TransactionType)
            ? FleetScheduleConstants.Type.Rental
            : request.TransactionType.Trim();

        if (transactionType is not FleetScheduleConstants.Type.Reservation and not FleetScheduleConstants.Type.Rental)
        {
            throw Validation(nameof(CreateWalkInTransactionRequest.TransactionType), "Transaction type is invalid.");
        }

        Customer customer = request.CustomerId.HasValue
            ? await GetEligibleCustomerAsync(request.CustomerId.Value)
            : await GetWalkInCustomerAsync(request.WalkInFirstName, request.WalkInLastName);
        Car car = await GetEligibleCarAsync(request.CarId);
        decimal dailyRate = request.DailyRate ?? car.RatePerDay;
        ValidateCommercialInputs(request.StartDate.Date, request.EndDate.Date, dailyRate, request.AmountPaid, request.ModeOfPayment);

        decimal totalAmount = CalculateTotalAmount(request.StartDate.Date, request.EndDate.Date, dailyRate);

        if (transactionType == FleetScheduleConstants.Type.Rental && request.AmountPaid < totalAmount)
        {
            throw Validation(nameof(CreateWalkInTransactionRequest.AmountPaid), "Direct rental requires full payment before the car can be released.");
        }

        FleetSchedule schedule = transactionType == FleetScheduleConstants.Type.Reservation
            ? CreateReservationSchedule(
                scheduleId: 0,
                car.CarId,
                customer.CustomerId,
                request.StartDate.Date,
                request.EndDate.Date,
                request.Notes,
                GetReservationStatusForPaidAmount(request.AmountPaid))
            : CreateRentalSchedule(
                scheduleId: 0,
                car.CarId,
                customer.CustomerId,
                request.StartDate.Date,
                request.EndDate.Date,
                request.Notes);
        await _scheduleService.PrepareForSaveAsync(schedule);

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction dbTransaction = connection.BeginTransaction();

        try
        {
            schedule.CreatedByUserId = _currentUserId;
            int scheduleId = await _scheduleRepository.CreateAsync(schedule, dbTransaction);
            Transaction transaction = BuildTransaction(
                scheduleId,
                customer.CustomerId,
                car.CarId,
                request.StartDate.Date,
                request.EndDate.Date,
                dailyRate,
                request.ModeOfPayment,
                request.AmountPaid,
                request.Notes,
                transactionType == FleetScheduleConstants.Type.Reservation
                    ? GetReservationTransactionStatusForPaidAmount(request.AmountPaid)
                    : TransactionConstants.Status.Active);
            int transactionId = await CreateWithUniqueCodeAsync(transaction, dbTransaction);

            if (request.AmountPaid > 0)
            {
                await _transactionPaymentRepository.AddAsync(
                    new TransactionPayment
                    {
                        TransactionId = transactionId,
                        PaymentDate = DateTime.Now,
                        Amount = request.AmountPaid,
                        ModeOfPayment = request.ModeOfPayment.Trim(),
                        ReceiptFilePath = request.ReceiptFilePath,
                        Notes = "Initial payment",
                        CreatedByUserId = _currentUserId
                    },
                    dbTransaction);
                await RecalculatePaymentSummaryAsync(transactionId, dbTransaction);
            }

            await _activityLogService.LogAsync(
                "Create walk-in transaction",
                "Transaction",
                transactionId,
                $"Created walk-in {transactionType.ToLowerInvariant()} transaction {transaction.TransactionCode} with schedule #{scheduleId}.",
                _currentUserId,
                dbTransaction);
            dbTransaction.Commit();
            return transactionId;
        }
        catch
        {
            RollbackQuietly(dbTransaction);
            throw;
        }
    }

    public Task CompleteTransactionAsync(int transactionId, int currentUserId)
    {
        return CompletePaidTransactionAsync(transactionId, currentUserId);
    }

    public async Task StartRentalAsync(int transactionId, int currentUserId)
    {
        Transaction transaction = await GetActiveTransactionAsync(transactionId);

        if (transaction.TransactionStatus != TransactionConstants.Status.Reserved)
        {
            throw Validation(nameof(Transaction.TransactionStatus), "Only reserved transactions can be started.");
        }

        if (transaction.PaymentStatus != TransactionConstants.PaymentStatus.Paid)
        {
            throw Validation(nameof(Transaction.PaymentStatus), "This rental cannot start until the payment is fully paid.");
        }

        await ChangeStatusAsync(
            transactionId,
            currentUserId,
            TransactionConstants.Status.Active,
            FleetScheduleConstants.Type.Rental,
            FleetScheduleConstants.Status.Rented,
            "Start rental");
    }

    public async Task CancelTransactionAsync(int transactionId, int currentUserId, string? reason = null)
    {
        string? trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        Transaction transaction = await GetActiveTransactionAsync(transactionId);
        string scheduleType = transaction.TransactionStatus is TransactionConstants.Status.Pending or TransactionConstants.Status.Reserved
            ? FleetScheduleConstants.Type.Reservation
            : FleetScheduleConstants.Type.Rental;

        await ChangeStatusAsync(transactionId, currentUserId, TransactionConstants.Status.Cancelled, scheduleType, FleetScheduleConstants.Status.Cancelled, "Cancel transaction", trimmedReason);
    }

    public async Task ArchiveTransactionAsync(int transactionId, int currentUserId)
    {
        Transaction transaction = await GetActiveTransactionAsync(transactionId);

        if (transaction.TransactionStatus is not (TransactionConstants.Status.Completed or TransactionConstants.Status.Cancelled))
        {
            throw Validation(nameof(Transaction.TransactionStatus), "Only completed or cancelled transactions can be archived.");
        }

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction dbTransaction = connection.BeginTransaction();

        try
        {
            int affectedRows = await _transactionRepository.ArchiveAsync(transactionId, dbTransaction);

            if (affectedRows == 0)
            {
                throw new RecordNotFoundException($"Transaction record #{transactionId} was not found or is already archived.");
            }

            await _activityLogService.LogAsync(
                "Archive transaction",
                "Transaction",
                transactionId,
                $"Archived transaction {transaction.TransactionCode}.",
                currentUserId,
                dbTransaction);
            dbTransaction.Commit();
        }
        catch
        {
            RollbackQuietly(dbTransaction);
            throw;
        }
    }

    public async Task RestoreTransactionAsync(int transactionId, int currentUserId)
    {
        Transaction? transaction = await _transactionRepository.GetByIdAsync(transactionId);

        if (transaction is null || !transaction.IsArchived)
        {
            throw new RecordNotFoundException($"Archived transaction record #{transactionId} was not found.");
        }

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction dbTransaction = connection.BeginTransaction();

        try
        {
            int affectedRows = await _transactionRepository.RestoreAsync(transactionId, dbTransaction);

            if (affectedRows == 0)
            {
                throw new RecordNotFoundException($"Archived transaction record #{transactionId} was not found.");
            }

            await _activityLogService.LogAsync(
                "Restore transaction",
                "Transaction",
                transactionId,
                $"Restored transaction {transaction.TransactionCode}.",
                currentUserId,
                dbTransaction);
            dbTransaction.Commit();
        }
        catch
        {
            RollbackQuietly(dbTransaction);
            throw;
        }
    }

    public async Task AddPaymentAsync(AddTransactionPaymentRequest request, int currentUserId)
    {
        Transaction transaction = await GetActiveTransactionAsync(request.TransactionId);

        if (transaction.TransactionStatus == TransactionConstants.Status.Cancelled)
        {
            throw Validation(nameof(Transaction.TransactionStatus), "Cannot add payments to a cancelled transaction.");
        }

        if (transaction.TransactionStatus == TransactionConstants.Status.Completed)
        {
            throw Validation(nameof(Transaction.TransactionStatus), "Cannot add payments to a completed transaction.");
        }

        if (request.Amount <= 0)
        {
            throw Validation(nameof(AddTransactionPaymentRequest.Amount), "Payment amount must be greater than 0.");
        }

        decimal currentTotalPaid = await _transactionPaymentRepository.GetTotalPaidAsync(request.TransactionId);

        if (currentTotalPaid + request.Amount > transaction.TotalAmount)
        {
            throw Validation(nameof(AddTransactionPaymentRequest.Amount), $"Payment amount exceeds the remaining balance. Remaining: ₱{transaction.TotalAmount - currentTotalPaid:N2}");
        }

        if (!TransactionConstants.ModeOfPayment.All.Contains(request.ModeOfPayment))
        {
            throw Validation(nameof(AddTransactionPaymentRequest.ModeOfPayment), "Mode of payment is invalid.");
        }

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction dbTransaction = connection.BeginTransaction();

        try
        {
            TransactionPayment payment = new()
            {
                TransactionId = request.TransactionId,
                PaymentDate = DateTime.Now,
                Amount = request.Amount,
                ModeOfPayment = request.ModeOfPayment.Trim(),
                ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim(),
                ReceiptFilePath = request.ReceiptFilePath,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                CreatedByUserId = currentUserId
            };

            await _transactionPaymentRepository.AddAsync(payment, dbTransaction);
            await RecalculatePaymentSummaryAsync(request.TransactionId, dbTransaction);
            await SyncReservationTransactionPaymentStatusAsync(transaction, dbTransaction);

            await _activityLogService.LogAsync(
                "Add payment",
                "Transaction",
                request.TransactionId,
                $"Added payment of ₱{request.Amount:N2} for {transaction.TransactionCode} via {request.ModeOfPayment}.",
                currentUserId,
                dbTransaction);

            dbTransaction.Commit();
        }
        catch
        {
            RollbackQuietly(dbTransaction);
            throw;
        }
    }

    public Task<IReadOnlyList<TransactionPaymentListItem>> GetPaymentsAsync(int transactionId)
    {
        return _transactionPaymentRepository.GetByTransactionIdAsync(transactionId);
    }

    public async Task RecalculatePaymentSummaryAsync(int transactionId, IDbTransaction? dbTransaction = null)
    {
        Transaction? transaction = await _transactionRepository.GetByIdAsync(transactionId, dbTransaction);

        if (transaction is null)
        {
            return;
        }

        decimal totalPaid = await _transactionPaymentRepository.GetTotalPaidAsync(transactionId, dbTransaction);
        string paymentStatus = GetPaymentStatus(totalPaid, transaction.TotalAmount);

        await _transactionRepository.UpdatePaymentSummaryAsync(
            transactionId,
            totalPaid,
            transaction.TotalAmount - totalPaid,
            paymentStatus,
            dbTransaction);
    }

    private async Task CompletePaidTransactionAsync(int transactionId, int currentUserId)
    {
        Transaction transaction = await GetActiveTransactionAsync(transactionId);

        if (transaction.TransactionStatus != TransactionConstants.Status.Active)
        {
            throw Validation(nameof(Transaction.TransactionStatus), "Only active transactions can be completed.");
        }

        if (transaction.PaymentStatus != TransactionConstants.PaymentStatus.Paid)
        {
            throw Validation(nameof(Transaction.PaymentStatus), "This transaction cannot be completed until the payment is fully paid.");
        }
        await ChangeStatusAsync(
            transactionId,
            currentUserId,
            TransactionConstants.Status.Completed,
            FleetScheduleConstants.Type.Rental,
            FleetScheduleConstants.Status.Completed,
            "Complete transaction");
    }

    private async Task ChangeStatusAsync(
        int transactionId,
        int currentUserId,
        string transactionStatus,
        string scheduleType,
        string scheduleStatus,
        string actionType,
        string? reason = null)
    {
        Transaction transaction = await GetActiveTransactionAsync(transactionId);

        if (transaction.TransactionStatus == TransactionConstants.Status.Cancelled)
        {
            throw Validation(nameof(Transaction.TransactionStatus), "Cancelled transactions cannot be changed.");
        }

        if (transaction.TransactionStatus == TransactionConstants.Status.Completed)
        {
            throw Validation(nameof(Transaction.TransactionStatus), "Completed transactions cannot be changed.");
        }

        FleetSchedule? schedule = await _scheduleRepository.GetByIdAsync(transaction.FleetScheduleId);
        if (schedule is null || schedule.IsArchived)
        {
            throw Validation(nameof(Transaction.FleetScheduleId), "Linked fleet schedule was not found or is archived.");
        }

        schedule.ScheduleType = scheduleType;
        schedule.Status = scheduleStatus;
        await _scheduleService.PrepareForSaveAsync(schedule, schedule.ScheduleId);

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction dbTransaction = connection.BeginTransaction();

        try
        {
            await _transactionRepository.UpdateStatusAsync(transactionId, transactionStatus, dbTransaction);
            await _scheduleRepository.UpdateAsync(schedule, dbTransaction);
            string description = reason is null
                ? $"{actionType} for {transaction.TransactionCode}."
                : $"{actionType} for {transaction.TransactionCode}. Reason: {reason}";
            await _activityLogService.LogAsync(actionType, "Transaction", transactionId, description, currentUserId, dbTransaction);
            dbTransaction.Commit();
        }
        catch
        {
            RollbackQuietly(dbTransaction);
            throw;
        }
    }

    private async Task<Transaction> GetActiveTransactionAsync(int transactionId)
    {
        Transaction? transaction = await _transactionRepository.GetByIdAsync(transactionId);

        if (transaction is null || transaction.IsArchived)
        {
            throw new RecordNotFoundException($"Transaction record #{transactionId} was not found or is archived.");
        }

        return transaction;
    }

    private async Task<Car> GetEligibleCarAsync(int carId)
    {
        Car? car = await _carRepository.GetCarByIdAsync(carId);

        if (car is null || car.IsArchived)
        {
            throw Validation(nameof(Transaction.CarId), "Selected car was not found or is archived.");
        }

        return car;
    }

    private async Task<Customer> GetEligibleCustomerAsync(int customerId)
    {
        Customer? customer = await _customerRepository.GetCustomerByIdAsync(customerId);

        if (customer is null || customer.IsArchived)
        {
            throw Validation(nameof(Transaction.CustomerId), "Selected customer was not found or is archived.");
        }

        if (customer.IsBlacklisted)
        {
            throw Validation(nameof(Transaction.CustomerId), "This customer is blacklisted and cannot be assigned to a new transaction.");
        }

        return customer;
    }

    private async Task<Customer> GetWalkInCustomerAsync(string? firstName, string? lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
        {
            return await _customerRepository.GetOrCreateWalkInCustomerAsync();
        }

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            throw Validation(nameof(CreateWalkInTransactionRequest.WalkInFirstName), "First name and last name are required for a named walk-in customer.");
        }

        string phoneNumber;
        do
        {
            phoneNumber = $"09{Random.Shared.Next(0, 1_000_000_000):000000000}";
        }
        while (await _customerRepository.PhoneNumberExistsAsync(phoneNumber));

        Customer customer = new()
        {
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            PhoneNumber = phoneNumber
        };
        int customerId = await new CustomerService(_currentUserId).AddCustomerAsync(customer);
        return await _customerRepository.GetCustomerByIdAsync(customerId)
            ?? throw new InvalidOperationException("Named walk-in customer could not be loaded after creation.");
    }

    private static FleetSchedule CreateRentalSchedule(
        int scheduleId,
        int carId,
        int customerId,
        DateTime startDate,
        DateTime endDate,
        string? notes)
    {
        return new FleetSchedule
        {
            ScheduleId = scheduleId,
            CarId = carId,
            CustomerId = customerId,
            ScheduleType = FleetScheduleConstants.Type.Rental,
            Status = FleetScheduleConstants.Status.Rented,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            Notes = notes
        };
    }

    private static FleetSchedule CreateReservationSchedule(
        int scheduleId,
        int carId,
        int customerId,
        DateTime startDate,
        DateTime endDate,
        string? notes,
        string status)
    {
        return new FleetSchedule
        {
            ScheduleId = scheduleId,
            CarId = carId,
            CustomerId = customerId,
            ScheduleType = FleetScheduleConstants.Type.Reservation,
            Status = status,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            Notes = notes
        };
    }

    private Transaction BuildTransaction(
        int scheduleId,
        int customerId,
        int carId,
        DateTime startDate,
        DateTime endDate,
        decimal dailyRate,
        string modeOfPayment,
        decimal amountPaid,
        string? notes,
        string transactionStatus)
    {
        int totalDays = (endDate.Date - startDate.Date).Days + 1;
        decimal totalAmount = CalculateTotalAmount(startDate, endDate, dailyRate);
        return new Transaction
        {
            FleetScheduleId = scheduleId,
            CustomerId = customerId,
            CarId = carId,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            DailyRate = dailyRate,
            TotalDays = totalDays,
            TotalAmount = totalAmount,
            AmountPaid = amountPaid,
            BalanceAmount = totalAmount - amountPaid,
            ModeOfPayment = modeOfPayment.Trim(),
            PaymentStatus = GetPaymentStatus(amountPaid, totalAmount),
            TransactionStatus = transactionStatus,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedByUserId = _currentUserId
        };
    }

    private static void ValidateCommercialInputs(
        DateTime startDate,
        DateTime endDate,
        decimal dailyRate,
        decimal amountPaid,
        string modeOfPayment)
    {
        List<ValidationFailure> failures = [];

        if (startDate.Date > endDate.Date)
        {
            failures.Add(new ValidationFailure(nameof(Transaction.EndDate), "End date must be on or after start date."));
        }

        if (dailyRate <= 0)
        {
            failures.Add(new ValidationFailure(nameof(Transaction.DailyRate), "Daily rate must be greater than 0."));
        }

        ValidatePaymentInputs(CalculateTotalAmount(startDate, endDate, dailyRate), amountPaid, modeOfPayment, failures);

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }

    private static void ValidatePaymentInputs(decimal totalAmount, decimal amountPaid, string modeOfPayment, List<ValidationFailure>? failures = null)
    {
        failures ??= [];
        if (amountPaid < 0)
        {
            failures.Add(new ValidationFailure(nameof(Transaction.AmountPaid), "Amount paid cannot be negative."));
        }
        if (amountPaid > totalAmount)
        {
            failures.Add(new ValidationFailure(nameof(Transaction.AmountPaid), "Amount paid cannot exceed total amount."));
        }
        if (!TransactionConstants.ModeOfPayment.All.Contains(modeOfPayment))
        {
            failures.Add(new ValidationFailure(nameof(Transaction.ModeOfPayment), "Mode of payment is invalid."));
        }
        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }

    private static string GetPaymentStatus(decimal amountPaid, decimal totalAmount)
    {
        return amountPaid <= 0
            ? TransactionConstants.PaymentStatus.Unpaid
            : amountPaid < totalAmount
                ? TransactionConstants.PaymentStatus.Partial
                : TransactionConstants.PaymentStatus.Paid;
    }

    private static decimal CalculateTotalAmount(DateTime startDate, DateTime endDate, decimal dailyRate)
    {
        int totalDays = (endDate.Date - startDate.Date).Days + 1;
        return dailyRate * totalDays;
    }

    private static string GetReservationStatusForPaidAmount(decimal amountPaid)
    {
        return amountPaid > 0
            ? FleetScheduleConstants.Status.Reserved
            : FleetScheduleConstants.Status.Pending;
    }

    private static string GetReservationTransactionStatusForPaidAmount(decimal amountPaid)
    {
        return amountPaid > 0
            ? TransactionConstants.Status.Reserved
            : TransactionConstants.Status.Pending;
    }

    private async Task SyncReservationTransactionPaymentStatusAsync(Transaction transaction, SqlTransaction dbTransaction)
    {
        if (transaction.TransactionStatus is not (TransactionConstants.Status.Pending or TransactionConstants.Status.Reserved))
        {
            return;
        }

        FleetSchedule? schedule = await _scheduleRepository.GetByIdAsync(transaction.FleetScheduleId);
        if (schedule is null || schedule.IsArchived || schedule.ScheduleType != FleetScheduleConstants.Type.Reservation)
        {
            return;
        }

        decimal totalPaid = await _transactionPaymentRepository.GetTotalPaidAsync(transaction.TransactionId, dbTransaction);
        string scheduleStatus = GetReservationStatusForPaidAmount(totalPaid);
        string transactionStatus = GetReservationTransactionStatusForPaidAmount(totalPaid);

        if (transaction.TransactionStatus != transactionStatus)
        {
            await _transactionRepository.UpdateStatusAsync(transaction.TransactionId, transactionStatus, dbTransaction);
        }

        if (schedule.Status == scheduleStatus)
        {
            return;
        }

        schedule.Status = scheduleStatus;
        await _scheduleService.PrepareForSaveAsync(schedule, schedule.ScheduleId);
        await _scheduleRepository.UpdateAsync(schedule, dbTransaction);
    }

    private async Task<string> GenerateTransactionCodeAsync(SqlTransaction dbTransaction)
    {
        int year = DateTime.Today.Year;
        int sequence = await _transactionRepository.GetNextSequenceForYearAsync(year, dbTransaction);
        return $"TXN-{year}-{sequence:000000}";
    }

    private async Task<int> CreateWithUniqueCodeAsync(Transaction transaction, SqlTransaction dbTransaction)
    {
        for (int attempt = 1; attempt <= MaxTransactionCodeCreateAttempts; attempt++)
        {
            transaction.TransactionCode = await GenerateTransactionCodeAsync(dbTransaction);

            try
            {
                return await _transactionRepository.CreateAsync(transaction, dbTransaction);
            }
            catch (SqlException exception) when (IsUniqueConstraintViolation(exception) && attempt < MaxTransactionCodeCreateAttempts)
            {
                // Retry after the database unique constraint rejects an unexpected collision.
            }
        }

        throw Validation(nameof(Transaction.TransactionCode), "A unique transaction code could not be generated. Please try again.");
    }

    private static bool IsUniqueConstraintViolation(SqlException exception)
    {
        return exception.Number is 2601 or 2627;
    }

    private static ValidationException Validation(string propertyName, string message)
    {
        return new ValidationException([new ValidationFailure(propertyName, message)]);
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
