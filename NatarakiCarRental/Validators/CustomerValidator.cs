using FluentValidation;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Validators;

public sealed class CustomerValidator : AbstractValidator<Customer>
{
    public CustomerValidator()
    {
        RuleFor(customer => customer.FirstName)
            .NotEmpty()
            .When(customer => customer.CustomerType == "Rental")
            .WithMessage("First name is required for rental customers.");

        RuleFor(customer => customer.LastName)
            .NotEmpty()
            .When(customer => customer.CustomerType == "Rental")
            .WithMessage("Last name is required for rental customers.");

        RuleFor(customer => customer.CompanyName)
            .NotEmpty()
            .When(customer => customer.CustomerType == "Maintenance")
            .WithMessage("Company name or full client name is required for offsite clients.");

        ApplyPhoneNumberRules(RuleFor(customer => customer.PhoneNumber));

        RuleFor(customer => customer.Email)
            .EmailAddress()
            .When(customer => !string.IsNullOrWhiteSpace(customer.Email))
            .WithMessage("Email address is not valid.");

        When(HasAnyAddressValue, () =>
        {
            RuleFor(customer => customer.Region)
                .NotEmpty()
                .WithMessage("Region is required when entering an address.");

            RuleFor(customer => customer.Province)
                .NotEmpty()
                .WithMessage("Province is required when entering an address.");

            RuleFor(customer => customer.City)
                .NotEmpty()
                .WithMessage("City or municipality is required when entering an address.");

            RuleFor(customer => customer.Barangay)
                .NotEmpty()
                .WithMessage("Barangay is required when entering an address.");
        });
    }

    public static void ApplyPhoneNumberRules<T>(IRuleBuilder<T, string?> ruleBuilder)
    {
        ruleBuilder
            .NotEmpty()
            .WithMessage("Phone number is required.")
            .Matches(@"^09\d{9}$")
            .WithMessage("Phone number must be exactly 11 digits and start with 09 (e.g., 09123456789).");
    }

    private static bool HasAnyAddressValue(Customer customer)
    {
        return !string.IsNullOrWhiteSpace(customer.Region)
            || !string.IsNullOrWhiteSpace(customer.Province)
            || !string.IsNullOrWhiteSpace(customer.City)
            || !string.IsNullOrWhiteSpace(customer.Barangay)
            || !string.IsNullOrWhiteSpace(customer.StreetAddress);
    }
}

