using FluentValidation;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Validators;

public sealed class CarValidator : AbstractValidator<Car>
{
    public CarValidator()
    {
        RuleFor(car => car.CarName)
            .NotEmpty()
            .WithMessage("Car name is required.");

        RuleFor(car => car.Model)
            .NotEmpty()
            .WithMessage("Model is required.");

        RuleFor(car => car.PlateNumber)
            .NotEmpty()
            .WithMessage("Plate number is required.");

        RuleFor(car => car.PlateNumber)
            .Must(PlateNumberHelper.IsValidPhilippinePlate)
            .WithMessage("Plate number must follow the format ABC 123 or ABC 1234.");

        RuleFor(car => car.RatePerDay)
            .GreaterThan(0)
            .WithMessage("Rate per day must be greater than 0.");

        RuleFor(car => car.Year)
            .InclusiveBetween(1000, 9999)
            .When(car => car.Year.HasValue)
            .WithMessage("Year must be 4 digits.");

        RuleFor(car => car.SeatingCapacity)
            .GreaterThan(0)
            .When(car => car.SeatingCapacity.HasValue)
            .WithMessage("Seating capacity must be greater than 0.");

        RuleFor(car => car.Mileage)
            .GreaterThanOrEqualTo(0)
            .When(car => car.Mileage.HasValue)
            .WithMessage("Mileage cannot be a negative number.");

        RuleFor(car => car.Status)
            .NotEmpty()
            .WithMessage("Status is required.");

        RuleFor(car => car.Status)
            .Must(status => CarConstants.Status.All.Contains(status))
            .WithMessage("Status must be Available, Rented, or Maintenance.");

        RuleFor(car => car.Transmission)
            .Must(value => string.IsNullOrWhiteSpace(value) || CarConstants.Transmission.All.Contains(value))
            .WithMessage("Transmission must be Automatic, Manual, or CVT.");

        RuleFor(car => car.FuelType)
            .Must(value => string.IsNullOrWhiteSpace(value) || CarConstants.FuelType.All.Contains(value))
            .WithMessage("Fuel type must be Gasoline, Diesel, Hybrid, or Electric.");

        RuleFor(car => car.CodingDay)
            .Must(value => string.IsNullOrWhiteSpace(value) || CarConstants.CodingDay.All.Contains(value))
            .WithMessage("Car coding day must be Monday, Tuesday, Wednesday, Thursday, Friday, or None / Not Applicable.");
    }
}
