using FluentValidation;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Validators;

public sealed class OffsiteRecordValidator : AbstractValidator<CreateOffsiteRecordRequest>
{
    public OffsiteRecordValidator()
    {
        RuleFor(request => request.CarId)
            .GreaterThan(0)
            .WithMessage("Vehicle selection is required.");

        RuleFor(request => request.OffsiteType)
            .NotEmpty()
            .WithMessage("Offsite type is required.");

        RuleFor(request => request.ContactNumber)
            .Matches(@"^09\d{9}$")
            .When(request => !string.IsNullOrWhiteSpace(request.ContactNumber))
            .WithMessage("Contact number must be exactly 11 digits and start with 09 (e.g., 09123456789).");
    }
}
