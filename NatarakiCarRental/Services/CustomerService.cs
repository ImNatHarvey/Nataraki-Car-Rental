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

public sealed class CustomerService
{
    private readonly CustomerRepository _customerRepository;
    private readonly FleetScheduleRepository _fleetScheduleRepository;
    private readonly ActivityLogService _activityLogService;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly int? _currentUserId;

    public CustomerService()
        : this(currentUserId: null)
    {
    }

    public CustomerService(int? currentUserId)
        : this(new DbConnectionFactory(), currentUserId)
    {
    }

    private CustomerService(DbConnectionFactory connectionFactory, int? currentUserId)
        : this(
            new CustomerRepository(connectionFactory),
            new FleetScheduleRepository(connectionFactory),
            new ActivityLogService(connectionFactory),
            connectionFactory,
            currentUserId)
    {
    }

    public CustomerService(CustomerRepository customerRepository, ActivityLogService activityLogService)
        : this(customerRepository, new FleetScheduleRepository(), activityLogService, new DbConnectionFactory(), currentUserId: null)
    {
    }

    public CustomerService(
        CustomerRepository customerRepository,
        FleetScheduleRepository fleetScheduleRepository,
        ActivityLogService activityLogService,
        DbConnectionFactory connectionFactory,
        int? currentUserId = null)
    {
        _customerRepository = customerRepository;
        _fleetScheduleRepository = fleetScheduleRepository;
        _activityLogService = activityLogService;
        _connectionFactory = connectionFactory;
        _currentUserId = currentUserId;
    }

    public Task<Customer?> GetCustomerByIdAsync(int customerId)
    {
        return _customerRepository.GetCustomerByIdAsync(customerId);
    }

    public Task<IReadOnlyList<Customer>> SearchCustomersAsync(string searchText, CustomerListFilter filter)
    {
        return _customerRepository.SearchCustomersAsync(searchText, filter);
    }

    public Task<CustomerCounts> GetCustomerCountsAsync()
    {
        return _customerRepository.GetCustomerCountsAsync();
    }

    public Task<IReadOnlyList<Customer>> GetRecentCustomersAsync(int take)
    {
        return _customerRepository.GetRecentCustomersAsync(take);
    }

    public Task<bool> PhoneNumberExistsAsync(string phoneNumber, int? excludingCustomerId = null)
    {
        return _customerRepository.PhoneNumberExistsAsync(phoneNumber, excludingCustomerId);
    }

    public Task<Customer?> GetCustomerByPhoneNumberAsync(string phoneNumber)
    {
        return _customerRepository.GetCustomerByPhoneNumberAsync(phoneNumber);
    }

    public Task<Customer> GetOrCreateWalkInCustomerAsync()
    {
        return _customerRepository.GetOrCreateWalkInCustomerAsync();
    }

    public async Task<int> AddCustomerAsync(Customer customer)
    {
        AccessControlService.EnforcePermission("Customers.Create");
        ValidateCustomer(customer);

        bool phoneExists = await _customerRepository.PhoneNumberExistsAsync(customer.PhoneNumber);

        if (phoneExists)
        {
            throw CreateDuplicatePhoneValidationException();
        }

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            int customerId = await _customerRepository.AddCustomerAsync(customer, transaction);
            await _activityLogService.LogAsync(
                "Added",
                "Customer",
                customerId,
                $"Added customer {customer.FirstName} {customer.LastName} ({customer.PhoneNumber}).",
                userId: _currentUserId,
                entityName: $"{customer.FirstName} {customer.LastName}",
                transaction: transaction);

            transaction.Commit();
            return customerId;
        }
        catch (SqlException exception) when (IsUniqueConstraintViolation(exception))
        {
            RollbackQuietly(transaction);
            throw CreateDuplicatePhoneValidationException();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    public async Task UpdateCustomerAsync(Customer customer)
    {
        AccessControlService.EnforcePermission("Customers.Edit");
        ValidateCustomer(customer);

        Customer? existing = await _customerRepository.GetCustomerByIdAsync(customer.CustomerId);
        if (existing?.IsWalkIn == true)
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(Customer.CustomerId), "Walk-In Customer is a protected system customer and cannot be edited.")]);
        }

        bool phoneExists = await _customerRepository.PhoneNumberExistsAsync(customer.PhoneNumber, customer.CustomerId);

        if (phoneExists)
        {
            throw CreateDuplicatePhoneValidationException();
        }

        Customer? oldCustomer = await _customerRepository.GetCustomerByIdAsync(customer.CustomerId);
        var (oldValue, newValue) = DiffHelper.GetJsonDiff(oldCustomer, customer);

        if (oldValue == null) return; // Only log and update if ACTUAL changes occurred

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            int affectedRows = await _customerRepository.UpdateCustomerAsync(customer, transaction);

            if (affectedRows == 0)
            {
                throw new RecordNotFoundException($"Customer record #{customer.CustomerId} was not found.");
            }

            await _activityLogService.LogAsync(
                "Updated",
                "Customer",
                customer.CustomerId,
                $"Updated customer {customer.FirstName} {customer.LastName} ({customer.PhoneNumber}).",
                userId: _currentUserId,
                entityName: $"{customer.FirstName} {customer.LastName}",
                oldValue: oldValue,
                newValue: newValue,
                transaction: transaction);

            transaction.Commit();
        }
        catch (SqlException exception) when (IsUniqueConstraintViolation(exception))
        {
            RollbackQuietly(transaction);
            throw CreateDuplicatePhoneValidationException();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    public async Task ArchiveCustomerAsync(int customerId)
    {
        AccessControlService.EnforcePermission("Customers.ArchiveRestore");
        Customer? customer = await _customerRepository.GetCustomerByIdAsync(customerId);
        if (customer?.IsWalkIn == true)
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(Customer.CustomerId), "Walk-In Customer is protected and cannot be archived.")]);
        }

        if (customer?.IsBlacklisted == true)
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(Customer.CustomerId), "Blacklisted customers cannot be archived. Remove the blacklist flag first.")]);
        }

        FleetSchedule? blockingSchedule = await _fleetScheduleRepository.GetActiveOrUpcomingOperationalScheduleForCustomerAsync(customerId, DateTime.Today);

        if (blockingSchedule is not null)
        {
            throw new ValidationException(
                [new ValidationFailure(
                    nameof(Customer.CustomerId),
                    $"This customer cannot be archived because they have an active or upcoming schedule: '{blockingSchedule.Title}' from {blockingSchedule.StartDate:MMM d, yyyy} to {blockingSchedule.EndDate:MMM d, yyyy}.")]);
        }

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            int affectedRows = await _customerRepository.ArchiveCustomerAsync(customerId, transaction);

            if (affectedRows == 0)
            {
                throw new RecordNotFoundException($"Customer record #{customerId} was not found or is already archived.");
            }

            await _activityLogService.LogAsync(
                "Archived",
                "Customer",
                customerId,
                $"Archived customer {DescribeCustomer(customer, customerId)}.",
                userId: _currentUserId,
                entityName: customer != null ? $"{customer.FirstName} {customer.LastName}" : $"#{customerId}",
                transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    public async Task RestoreCustomerAsync(int customerId)
    {
        AccessControlService.EnforcePermission("Customers.ArchiveRestore");
        Customer? customer = await _customerRepository.GetCustomerByIdAsync(customerId);
        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            int affectedRows = await _customerRepository.RestoreCustomerAsync(customerId, transaction);

            if (affectedRows == 0)
            {
                throw new RecordNotFoundException($"Customer record #{customerId} was not found or is not archived.");
            }

            await _activityLogService.LogAsync(
                "Restored",
                "Customer",
                customerId,
                $"Restored customer {DescribeCustomer(customer, customerId)}.",
                userId: _currentUserId,
                entityName: customer != null ? $"{customer.FirstName} {customer.LastName}" : $"#{customerId}",
                transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    public async Task ToggleBlacklistAsync(int customerId, bool isBlacklisted, string? reason = null)
    {
        AccessControlService.EnforcePermission("Customers.Blacklist");
        reason = NullIfWhiteSpace(reason);

        Customer? customer = await _customerRepository.GetCustomerByIdAsync(customerId);
        if (customer?.IsWalkIn == true)
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(Customer.CustomerId), "Walk-In Customer is protected and cannot be blacklisted.")]);
        }

        if (isBlacklisted && string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(Customer.BlacklistReason), "Blacklist reason is required.")]);
        }

        await using SqlConnection connection = await _connectionFactory.CreateOpenConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            int affectedRows = await _customerRepository.ToggleBlacklistAsync(customerId, isBlacklisted, reason, transaction);

            if (affectedRows == 0)
            {
                throw new RecordNotFoundException($"Customer record #{customerId} was not found or is archived.");
            }

            string description = isBlacklisted
                ? $"Blacklisted customer {DescribeCustomer(customer, customerId)}. Reason: {reason}"
                : $"Removed blacklist flag from customer {DescribeCustomer(customer, customerId)}.";

            await _activityLogService.LogAsync(
                "Updated",
                "Customer",
                customerId,
                description,
                userId: _currentUserId,
                entityName: customer != null ? $"{customer.FirstName} {customer.LastName}" : $"#{customerId}",
                transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    private static void NormalizeCustomer(Customer customer)
    {
        customer.FirstName = customer.FirstName?.Trim() ?? string.Empty;
        customer.LastName = customer.LastName?.Trim() ?? string.Empty;
        customer.Email = NullIfWhiteSpace(customer.Email);
        customer.PhoneNumber = customer.PhoneNumber?.Trim() ?? string.Empty;
        customer.Region = NullIfWhiteSpace(customer.Region);
        customer.Province = NullIfWhiteSpace(customer.Province);
        customer.City = NullIfWhiteSpace(customer.City);
        customer.Barangay = NullIfWhiteSpace(customer.Barangay);
        customer.StreetAddress = NullIfWhiteSpace(customer.StreetAddress);
        customer.BlacklistReason = customer.IsBlacklisted ? NullIfWhiteSpace(customer.BlacklistReason) : null;
        customer.DriverLicensePath = NullIfWhiteSpace(customer.DriverLicensePath);
        customer.ProofOfBillingPath = NullIfWhiteSpace(customer.ProofOfBillingPath);
        customer.ValidIdFilePath = NullIfWhiteSpace(customer.ValidIdFilePath);
        customer.SelfieWithValidIdFilePath = NullIfWhiteSpace(customer.SelfieWithValidIdFilePath);
    }

    private static void ValidateCustomer(Customer customer)
    {
        CustomerValidator validator = new();
        validator.ValidateAndThrow(customer);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string DescribeCustomer(Customer? customer, int customerId)
    {
        return customer is null
            ? $"#{customerId}"
            : $"{customer.FirstName} {customer.LastName} ({customer.PhoneNumber})";
    }

    private static bool IsUniqueConstraintViolation(SqlException exception)
    {
        return exception.Number is 2601 or 2627;
    }

    private static ValidationException CreateDuplicatePhoneValidationException()
    {
        return new ValidationException(
            [new ValidationFailure(nameof(Customer.PhoneNumber), "Phone number already exists.")]);
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
