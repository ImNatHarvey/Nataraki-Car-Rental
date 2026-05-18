#if DEBUG
using System.Diagnostics;
using Dapper;
using FluentValidation;
using Microsoft.Data.SqlClient;
using NatarakiCarRental.Data;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Repositories;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.Tools;

internal static class TransactionWorkflowDebugRunner
{
    private const string HarnessPrefix = "__TXN_DEBUG__";

    public static bool ShouldRun =>
        string.Equals(
            Environment.GetEnvironmentVariable("NATARAKI_RUN_TRANSACTION_DEBUG_HARNESS"),
            "1",
            StringComparison.Ordinal);

    public static bool ShouldExitAfterRun =>
        string.Equals(
            Environment.GetEnvironmentVariable("NATARAKI_EXIT_AFTER_TRANSACTION_DEBUG_HARNESS"),
            "1",
            StringComparison.Ordinal);

    public static async Task RunAsync()
    {
        string token = $"{HarnessPrefix}{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        DbConnectionFactory connectionFactory = new();
        TransactionService transactionService = new(currentUserId: null);
        FleetScheduleService scheduleService = new(currentUserId: null);
        FleetScheduleRepository scheduleRepository = new(connectionFactory);
        TransactionRepository transactionRepository = new(connectionFactory);
        CustomerRepository customerRepository = new(connectionFactory);
        List<int> transactionIds = [];
        List<int> scheduleIds = [];
        List<int> customerIds = [];
        List<int> carIds = [];

        try
        {
            DatabaseInitializer.Initialize();
            await VerifyStartupStateAsync(connectionFactory);
            int harnessUserId = await GetHarnessUserIdAsync(connectionFactory);

            int baseDayOffset = 40;
            int activeCustomerId = await CreateCustomerAsync(connectionFactory, token, "Active", customerIds);
            int blacklistedCustomerId = await CreateCustomerAsync(connectionFactory, token, "Blacklisted", customerIds, isBlacklisted: true);
            int archivedCustomerId = await CreateCustomerAsync(connectionFactory, token, "Archived", customerIds, isArchived: true);
            int activeCarId = await CreateCarAsync(connectionFactory, token, "ACTIVE", carIds);
            int archivedCarId = await CreateCarAsync(connectionFactory, token, "ARCH", carIds, isArchived: true);

            int reservationScheduleId = await CreateReservationAsync(
                scheduleService,
                activeCarId,
                activeCustomerId,
                DateTime.Today.AddDays(baseDayOffset),
                DateTime.Today.AddDays(baseDayOffset + 2),
                token);
            scheduleIds.Add(reservationScheduleId);

            int reservationTransactionId = await transactionService.CreateFromReservationAsync(
                new CreateTransactionFromReservationRequest
                {
                    FleetScheduleId = reservationScheduleId,
                    ModeOfPayment = TransactionConstants.ModeOfPayment.Cash,
                    PaymentStatus = TransactionConstants.PaymentStatus.Unpaid,
                    Notes = token
                });
            transactionIds.Add(reservationTransactionId);
            await VerifyReservationConversionAsync(
                connectionFactory,
                transactionRepository,
                reservationScheduleId,
                reservationTransactionId);

            int walkInTransactionId = await transactionService.CreateWalkInTransactionAsync(
                new CreateWalkInTransactionRequest
                {
                    CarId = activeCarId,
                    StartDate = DateTime.Today.AddDays(baseDayOffset + 5),
                    EndDate = DateTime.Today.AddDays(baseDayOffset + 6),
                    ModeOfPayment = TransactionConstants.ModeOfPayment.GCash,
                    PaymentStatus = TransactionConstants.PaymentStatus.Partial,
                    Notes = token
                });
            transactionIds.Add(walkInTransactionId);
            Transaction walkInTransaction = await RequireTransactionAsync(transactionRepository, walkInTransactionId);
            scheduleIds.Add(walkInTransaction.FleetScheduleId);
            await VerifyWalkInTransactionAsync(connectionFactory, walkInTransaction);

            await VerifyValidationFailuresAsync(
                connectionFactory,
                transactionService,
                scheduleService,
                activeCarId,
                archivedCarId,
                activeCustomerId,
                archivedCustomerId,
                blacklistedCustomerId,
                token,
                scheduleIds,
                baseDayOffset + 10);

            await transactionService.CompleteTransactionAsync(reservationTransactionId, harnessUserId);
            await VerifyStatusAsync(
                transactionRepository,
                scheduleRepository,
                reservationTransactionId,
                TransactionConstants.Status.Completed,
                FleetScheduleConstants.Status.Completed);

            await transactionService.CancelTransactionAsync(walkInTransactionId, harnessUserId, "Debug harness cancellation");
            await VerifyStatusAsync(
                transactionRepository,
                scheduleRepository,
                walkInTransactionId,
                TransactionConstants.Status.Cancelled,
                FleetScheduleConstants.Status.Cancelled);
            bool cancelledScheduleBlocks = await scheduleRepository.HasConflictAsync(
                activeCarId,
                walkInTransaction.StartDate,
                walkInTransaction.EndDate);
            Require(!cancelledScheduleBlocks, "Cancelled schedules must not block future schedules.");

            await transactionService.ArchiveTransactionAsync(walkInTransactionId, harnessUserId);
            Transaction archivedTransaction = await RequireTransactionAsync(transactionRepository, walkInTransactionId);
            Require(archivedTransaction.IsArchived, "Archived transaction must be soft-deleted.");
            FleetSchedule? preservedSchedule = await scheduleRepository.GetByIdAsync(walkInTransaction.FleetScheduleId);
            Require(preservedSchedule is not null && !preservedSchedule.IsArchived, "Archiving a transaction must preserve the linked fleet schedule.");

            await VerifySequentialCodesAsync(
                transactionService,
                transactionRepository,
                activeCarId,
                token,
                transactionIds,
                scheduleIds,
                baseDayOffset + 20);
            await VerifyExpectedLogsAsync(connectionFactory, transactionIds, scheduleIds);

            Debug.WriteLine("Transaction debug harness passed.");
        }
        finally
        {
            await CleanupAsync(connectionFactory, transactionIds, scheduleIds, customerIds, carIds);
        }
    }

    private static async Task VerifyStartupStateAsync(DbConnectionFactory connectionFactory)
    {
        DatabaseInitializer.Initialize();
        using SqlConnection connection = await connectionFactory.CreateOpenConnectionAsync();
        bool transactionsTableExists = await connection.ExecuteScalarAsync<bool>(
            "SELECT CAST(CASE WHEN OBJECT_ID(N'dbo.Transactions', N'U') IS NULL THEN 0 ELSE 1 END AS bit);");
        int walkInCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.Customers WHERE PhoneNumber = N'00000000000';");
        Require(transactionsTableExists, "dbo.Transactions table must exist.");
        Require(walkInCount == 1, "Walk-In Customer must exist exactly once after repeated startup initialization.");
    }

    private static async Task VerifyReservationConversionAsync(
        DbConnectionFactory connectionFactory,
        TransactionRepository transactionRepository,
        int scheduleId,
        int transactionId)
    {
        Transaction transaction = await RequireTransactionAsync(transactionRepository, transactionId);
        Require(transaction.FleetScheduleId == scheduleId, "Transaction must stay linked to the original reservation schedule.");
        Require(IsValidTransactionCode(transaction.TransactionCode), "Transaction code format is invalid.");

        using SqlConnection connection = await connectionFactory.CreateOpenConnectionAsync();
        int scheduleCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.FleetSchedules WHERE ScheduleId = @ScheduleId;",
            new { ScheduleId = scheduleId });
        var schedule = await connection.QuerySingleAsync<(string ScheduleType, string Status)>(
            "SELECT ScheduleType, Status FROM dbo.FleetSchedules WHERE ScheduleId = @ScheduleId;",
            new { ScheduleId = scheduleId });
        Require(scheduleCount == 1, "Reservation conversion must not duplicate the fleet schedule row.");
        Require(
            schedule.ScheduleType == FleetScheduleConstants.Type.Rental
            && schedule.Status == FleetScheduleConstants.Status.Rented,
            "Reservation schedule must convert to Rental / Rented.");
    }

    private static async Task VerifyWalkInTransactionAsync(DbConnectionFactory connectionFactory, Transaction transaction)
    {
        using SqlConnection connection = await connectionFactory.CreateOpenConnectionAsync();
        string? phoneNumber = await connection.ExecuteScalarAsync<string?>(
            "SELECT PhoneNumber FROM dbo.Customers WHERE CustomerId = @CustomerId;",
            new { transaction.CustomerId });
        var schedule = await connection.QuerySingleAsync<(string ScheduleType, string Status)>(
            "SELECT ScheduleType, Status FROM dbo.FleetSchedules WHERE ScheduleId = @ScheduleId;",
            new { ScheduleId = transaction.FleetScheduleId });
        Require(phoneNumber == "00000000000", "Walk-in transaction must use the default Walk-In Customer.");
        Require(
            schedule.ScheduleType == FleetScheduleConstants.Type.Rental
            && schedule.Status == FleetScheduleConstants.Status.Rented,
            "Walk-in transaction must create one Rental / Rented fleet schedule.");
    }

    private static async Task VerifyValidationFailuresAsync(
        DbConnectionFactory connectionFactory,
        TransactionService transactionService,
        FleetScheduleService scheduleService,
        int activeCarId,
        int archivedCarId,
        int activeCustomerId,
        int archivedCustomerId,
        int blacklistedCustomerId,
        string token,
        List<int> scheduleIds,
        int dayOffset)
    {
        await ExpectWalkInFailureWithoutOrphansAsync(
            connectionFactory,
            transactionService,
            new CreateWalkInTransactionRequest
            {
                CustomerId = archivedCustomerId,
                CarId = activeCarId,
                StartDate = DateTime.Today.AddDays(dayOffset),
                EndDate = DateTime.Today.AddDays(dayOffset),
                ModeOfPayment = TransactionConstants.ModeOfPayment.Cash,
                PaymentStatus = TransactionConstants.PaymentStatus.Unpaid,
                Notes = token
            },
            "Archived customers must be blocked.");

        await ExpectWalkInFailureWithoutOrphansAsync(
            connectionFactory,
            transactionService,
            new CreateWalkInTransactionRequest
            {
                CustomerId = blacklistedCustomerId,
                CarId = activeCarId,
                StartDate = DateTime.Today.AddDays(dayOffset + 1),
                EndDate = DateTime.Today.AddDays(dayOffset + 1),
                ModeOfPayment = TransactionConstants.ModeOfPayment.Cash,
                PaymentStatus = TransactionConstants.PaymentStatus.Unpaid,
                Notes = token
            },
            "Blacklisted customers must be blocked.");

        await ExpectWalkInFailureWithoutOrphansAsync(
            connectionFactory,
            transactionService,
            new CreateWalkInTransactionRequest
            {
                CustomerId = activeCustomerId,
                CarId = archivedCarId,
                StartDate = DateTime.Today.AddDays(dayOffset + 2),
                EndDate = DateTime.Today.AddDays(dayOffset + 2),
                ModeOfPayment = TransactionConstants.ModeOfPayment.Cash,
                PaymentStatus = TransactionConstants.PaymentStatus.Unpaid,
                Notes = token
            },
            "Archived cars must be blocked.");

        await ExpectWalkInFailureWithoutOrphansAsync(
            connectionFactory,
            transactionService,
            new CreateWalkInTransactionRequest
            {
                CustomerId = activeCustomerId,
                CarId = activeCarId,
                StartDate = DateTime.Today.AddDays(dayOffset + 4),
                EndDate = DateTime.Today.AddDays(dayOffset + 3),
                ModeOfPayment = TransactionConstants.ModeOfPayment.Cash,
                PaymentStatus = TransactionConstants.PaymentStatus.Unpaid,
                Notes = token
            },
            "End date before start date must be blocked.");

        await ExpectWalkInFailureWithoutOrphansAsync(
            connectionFactory,
            transactionService,
            new CreateWalkInTransactionRequest
            {
                CustomerId = activeCustomerId,
                CarId = activeCarId,
                StartDate = DateTime.Today.AddDays(dayOffset + 5),
                EndDate = DateTime.Today.AddDays(dayOffset + 5),
                DailyRate = 0,
                ModeOfPayment = TransactionConstants.ModeOfPayment.Cash,
                PaymentStatus = TransactionConstants.PaymentStatus.Unpaid,
                Notes = token
            },
            "Daily rate <= 0 must be blocked.");

        await ExpectWalkInFailureWithoutOrphansAsync(
            connectionFactory,
            transactionService,
            new CreateWalkInTransactionRequest
            {
                CustomerId = activeCustomerId,
                CarId = activeCarId,
                StartDate = DateTime.Today.AddDays(dayOffset + 6),
                EndDate = DateTime.Today.AddDays(dayOffset + 6),
                ModeOfPayment = "Cheque",
                PaymentStatus = TransactionConstants.PaymentStatus.Unpaid,
                Notes = token
            },
            "Invalid payment mode must be blocked.");

        await ExpectWalkInFailureWithoutOrphansAsync(
            connectionFactory,
            transactionService,
            new CreateWalkInTransactionRequest
            {
                CustomerId = activeCustomerId,
                CarId = activeCarId,
                StartDate = DateTime.Today.AddDays(dayOffset + 7),
                EndDate = DateTime.Today.AddDays(dayOffset + 7),
                ModeOfPayment = TransactionConstants.ModeOfPayment.Cash,
                PaymentStatus = "Settled",
                Notes = token
            },
            "Invalid payment status must be blocked.");

        int blockingScheduleId = await CreateReservationAsync(
            scheduleService,
            activeCarId,
            activeCustomerId,
            DateTime.Today.AddDays(dayOffset + 8),
            DateTime.Today.AddDays(dayOffset + 9),
            token);
        scheduleIds.Add(blockingScheduleId);
        await ExpectWalkInFailureWithoutOrphansAsync(
            connectionFactory,
            transactionService,
            new CreateWalkInTransactionRequest
            {
                CustomerId = activeCustomerId,
                CarId = activeCarId,
                StartDate = DateTime.Today.AddDays(dayOffset + 8),
                EndDate = DateTime.Today.AddDays(dayOffset + 8),
                ModeOfPayment = TransactionConstants.ModeOfPayment.Cash,
                PaymentStatus = TransactionConstants.PaymentStatus.Unpaid,
                Notes = token
            },
            "Overlapping operational schedules must be blocked.");

        int reservationForRollbackId = await CreateReservationAsync(
            scheduleService,
            activeCarId,
            activeCustomerId,
            DateTime.Today.AddDays(dayOffset + 11),
            DateTime.Today.AddDays(dayOffset + 11),
            token);
        scheduleIds.Add(reservationForRollbackId);
        await ExpectValidationFailureAsync(
            () => transactionService.CreateFromReservationAsync(
                new CreateTransactionFromReservationRequest
                {
                    FleetScheduleId = reservationForRollbackId,
                    ModeOfPayment = "Cheque",
                    PaymentStatus = TransactionConstants.PaymentStatus.Unpaid,
                    Notes = token
                }),
            "Invalid reservation conversion must fail.");
        using SqlConnection connection = await connectionFactory.CreateOpenConnectionAsync();
        var reservation = await connection.QuerySingleAsync<(string ScheduleType, string Status)>(
            "SELECT ScheduleType, Status FROM dbo.FleetSchedules WHERE ScheduleId = @ScheduleId;",
            new { ScheduleId = reservationForRollbackId });
        Require(
            reservation.ScheduleType == FleetScheduleConstants.Type.Reservation
            && reservation.Status == FleetScheduleConstants.Status.Reserved,
            "Failed reservation conversion must leave the schedule unchanged.");
    }

    private static async Task VerifyStatusAsync(
        TransactionRepository transactionRepository,
        FleetScheduleRepository scheduleRepository,
        int transactionId,
        string expectedTransactionStatus,
        string expectedScheduleStatus)
    {
        Transaction transaction = await RequireTransactionAsync(transactionRepository, transactionId);
        FleetSchedule? schedule = await scheduleRepository.GetByIdAsync(transaction.FleetScheduleId);
        Require(transaction.TransactionStatus == expectedTransactionStatus, $"Transaction status should be {expectedTransactionStatus}.");
        Require(schedule?.Status == expectedScheduleStatus, $"Linked schedule status should be {expectedScheduleStatus}.");
        Require(schedule?.ScheduleType == FleetScheduleConstants.Type.Rental, "Linked schedule must stay Rental.");
    }

    private static async Task VerifySequentialCodesAsync(
        TransactionService transactionService,
        TransactionRepository transactionRepository,
        int activeCarId,
        string token,
        List<int> transactionIds,
        List<int> scheduleIds,
        int dayOffset)
    {
        List<string> codes = [];

        for (int index = 0; index < 3; index++)
        {
            int transactionId = await transactionService.CreateWalkInTransactionAsync(
                new CreateWalkInTransactionRequest
                {
                    CarId = activeCarId,
                    StartDate = DateTime.Today.AddDays(dayOffset + index),
                    EndDate = DateTime.Today.AddDays(dayOffset + index),
                    ModeOfPayment = TransactionConstants.ModeOfPayment.Other,
                    PaymentStatus = TransactionConstants.PaymentStatus.Paid,
                    Notes = token
                });
            transactionIds.Add(transactionId);
            Transaction transaction = await RequireTransactionAsync(transactionRepository, transactionId);
            scheduleIds.Add(transaction.FleetScheduleId);
            codes.Add(transaction.TransactionCode);
        }

        Require(codes.Distinct(StringComparer.Ordinal).Count() == 3, "Transaction codes must remain unique.");
        Require(codes.All(IsValidTransactionCode), "Generated transaction codes must follow TXN-YYYY-000001 format.");
        int[] sequences = codes.Select(ParseSequence).ToArray();
        Require(
            sequences[1] == sequences[0] + 1 && sequences[2] == sequences[1] + 1,
            "Transaction codes created in one run must increase sequentially.");
    }

    private static async Task VerifyExpectedLogsAsync(
        DbConnectionFactory connectionFactory,
        IReadOnlyCollection<int> transactionIds,
        IReadOnlyCollection<int> scheduleIds)
    {
        using SqlConnection connection = await connectionFactory.CreateOpenConnectionAsync();
        int transactionLogCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1)
            FROM dbo.ActivityLogs
            WHERE EntityName = N'Transaction'
              AND EntityId IN @TransactionIds;
            """,
            new { TransactionIds = transactionIds });
        int conversionLogCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1)
            FROM dbo.ActivityLogs
            WHERE ActionType = N'Convert reservation to rental'
              AND EntityName = N'FleetSchedule'
              AND EntityId IN @ScheduleIds;
            """,
            new { ScheduleIds = scheduleIds });
        Require(transactionLogCount >= transactionIds.Count, "Expected transaction activity logs were not written.");
        Require(conversionLogCount >= 1, "Reservation conversion activity log was not written.");
    }

    private static async Task<int> GetHarnessUserIdAsync(DbConnectionFactory connectionFactory)
    {
        using SqlConnection connection = await connectionFactory.CreateOpenConnectionAsync();
        int? userId = await connection.ExecuteScalarAsync<int?>(
            "SELECT TOP (1) UserId FROM dbo.Users ORDER BY UserId;");
        return userId ?? throw new InvalidOperationException("At least one user is required before running the transaction debug harness.");
    }

    private static async Task ExpectWalkInFailureWithoutOrphansAsync(
        DbConnectionFactory connectionFactory,
        TransactionService transactionService,
        CreateWalkInTransactionRequest request,
        string failureMessage)
    {
        (int scheduleCountBefore, int transactionCountBefore, int logCountBefore) = await SnapshotCountsAsync(connectionFactory);
        await ExpectValidationFailureAsync(() => transactionService.CreateWalkInTransactionAsync(request), failureMessage);
        (int scheduleCountAfter, int transactionCountAfter, int logCountAfter) = await SnapshotCountsAsync(connectionFactory);
        Require(scheduleCountAfter == scheduleCountBefore, $"{failureMessage} No fleet schedule should be created.");
        Require(transactionCountAfter == transactionCountBefore, $"{failureMessage} No transaction should be created.");
        Require(logCountAfter == logCountBefore, $"{failureMessage} No success activity log should be written.");
    }

    private static async Task<(int Schedules, int Transactions, int Logs)> SnapshotCountsAsync(DbConnectionFactory connectionFactory)
    {
        using SqlConnection connection = await connectionFactory.CreateOpenConnectionAsync();
        return await connection.QuerySingleAsync<(int Schedules, int Transactions, int Logs)>(
            """
            SELECT
                Schedules = COUNT(1),
                Transactions = (SELECT COUNT(1) FROM dbo.Transactions),
                Logs = (SELECT COUNT(1) FROM dbo.ActivityLogs)
            FROM dbo.FleetSchedules;
            """);
    }

    private static async Task<int> CreateReservationAsync(
        FleetScheduleService scheduleService,
        int carId,
        int customerId,
        DateTime startDate,
        DateTime endDate,
        string token)
    {
        return await scheduleService.CreateAsync(
            new FleetSchedule
            {
                CarId = carId,
                CustomerId = customerId,
                ScheduleType = FleetScheduleConstants.Type.Reservation,
                Status = FleetScheduleConstants.Status.Reserved,
                StartDate = startDate,
                EndDate = endDate,
                Notes = token
            });
    }

    private static async Task<int> CreateCustomerAsync(
        DbConnectionFactory connectionFactory,
        string token,
        string suffix,
        List<int> customerIds,
        bool isBlacklisted = false,
        bool isArchived = false)
    {
        using SqlConnection connection = await connectionFactory.CreateOpenConnectionAsync();
        int customerId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO dbo.Customers
            (
                FirstName,
                LastName,
                PhoneNumber,
                IsBlacklisted,
                BlacklistReason,
                IsArchived,
                ArchivedAt
            )
            OUTPUT INSERTED.CustomerId
            VALUES
            (
                @FirstName,
                @LastName,
                @PhoneNumber,
                @IsBlacklisted,
                @BlacklistReason,
                @IsArchived,
                CASE WHEN @IsArchived = 1 THEN sysdatetime() ELSE NULL END
            );
            """,
            new
            {
                FirstName = token,
                LastName = suffix,
                PhoneNumber = BuildPhoneNumber(customerIds.Count),
                IsBlacklisted = isBlacklisted,
                BlacklistReason = isBlacklisted ? "Debug harness" : null,
                IsArchived = isArchived
            });
        customerIds.Add(customerId);
        return customerId;
    }

    private static async Task<int> CreateCarAsync(
        DbConnectionFactory connectionFactory,
        string token,
        string suffix,
        List<int> carIds,
        bool isArchived = false)
    {
        using SqlConnection connection = await connectionFactory.CreateOpenConnectionAsync();
        int carId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO dbo.Cars
            (
                CarName,
                Brand,
                Model,
                PlateNumber,
                RatePerDay,
                Status,
                IsArchived,
                ArchivedAt
            )
            OUTPUT INSERTED.CarId
            VALUES
            (
                @CarName,
                N'Debug',
                N'Harness',
                @PlateNumber,
                1500,
                @Status,
                @IsArchived,
                CASE WHEN @IsArchived = 1 THEN sysdatetime() ELSE NULL END
            );
            """,
            new
            {
                CarName = $"{token} {suffix}",
                PlateNumber = BuildPlateNumber(carIds.Count),
                Status = CarConstants.Status.Available,
                IsArchived = isArchived
            });
        carIds.Add(carId);
        return carId;
    }

    private static async Task CleanupAsync(
        DbConnectionFactory connectionFactory,
        IReadOnlyCollection<int> transactionIds,
        IReadOnlyCollection<int> scheduleIds,
        IReadOnlyCollection<int> customerIds,
        IReadOnlyCollection<int> carIds)
    {
        using SqlConnection connection = await connectionFactory.CreateOpenConnectionAsync();

        await connection.ExecuteAsync(
            """
            DELETE FROM dbo.ActivityLogs
            WHERE (EntityName = N'Transaction' AND EntityId IN @TransactionIds)
               OR (EntityName = N'FleetSchedule' AND EntityId IN @ScheduleIds);
            """,
            new { TransactionIds = transactionIds, ScheduleIds = scheduleIds });
        await connection.ExecuteAsync("DELETE FROM dbo.Transactions WHERE TransactionId IN @TransactionIds;", new { TransactionIds = transactionIds });
        await connection.ExecuteAsync("DELETE FROM dbo.FleetSchedules WHERE ScheduleId IN @ScheduleIds;", new { ScheduleIds = scheduleIds });
        await connection.ExecuteAsync("DELETE FROM dbo.Customers WHERE CustomerId IN @CustomerIds;", new { CustomerIds = customerIds });
        await connection.ExecuteAsync("DELETE FROM dbo.Cars WHERE CarId IN @CarIds;", new { CarIds = carIds });
    }

    private static async Task ExpectValidationFailureAsync(Func<Task> action, string failureMessage)
    {
        try
        {
            await action();
        }
        catch (ValidationException)
        {
            return;
        }

        throw new InvalidOperationException(failureMessage);
    }

    private static async Task<Transaction> RequireTransactionAsync(TransactionRepository repository, int transactionId)
    {
        Transaction? transaction = await repository.GetByIdAsync(transactionId);
        return transaction ?? throw new InvalidOperationException($"Transaction #{transactionId} was not found.");
    }

    private static bool IsValidTransactionCode(string code)
    {
        return code.Length == 15
            && code.StartsWith($"TXN-{DateTime.Today.Year}-", StringComparison.Ordinal)
            && int.TryParse(code[^6..], out _);
    }

    private static int ParseSequence(string code)
    {
        return int.Parse(code[^6..]);
    }

    private static string BuildPhoneNumber(int index)
    {
        return $"0999000{DateTime.UtcNow:HHmm}{index:00}";
    }

    private static string BuildPlateNumber(int index)
    {
        return $"DBG{DateTime.UtcNow:mmss}{index:00}";
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
