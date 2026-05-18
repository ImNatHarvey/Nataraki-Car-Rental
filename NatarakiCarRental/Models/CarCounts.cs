namespace NatarakiCarRental.Models;

public sealed class CarCounts
{
    public int TotalCars { get; set; }
    public int AvailableCars { get; set; }
    public int MaintenanceCars { get; set; }
    public int RentedCars { get; set; }
    public int ArchivedCars { get; set; }
}
