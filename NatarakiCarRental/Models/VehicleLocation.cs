namespace NatarakiCarRental.Models;

public sealed class VehicleLocation
{
    public int VehicleLocationId { get; set; }
    public int CarId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? SpeedKph { get; set; }
    public decimal? Heading { get; set; }
    public string Source { get; set; } = "Simulator";
    public DateTime RecordedAt { get; set; }
    public bool IsArchived { get; set; }
    public string CarName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
}
