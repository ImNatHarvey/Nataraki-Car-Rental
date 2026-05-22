using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.Helpers;

public sealed class VehicleTrackingSimulator
{
    private readonly VehicleTrackingService _trackingService;
    private int _routeIndex;

    private static readonly (decimal Latitude, decimal Longitude, string Name)[] DemoRoute =
    [
        (14.6760m, 121.0437m, "Quezon City"),
        (14.7566m, 121.0450m, "Caloocan"),
        (14.7987m, 120.9266m, "Bocaue"),
        (14.8175m, 120.8661m, "Balagtas"),
        (14.8527m, 120.8160m, "Malolos"),
        (14.8139m, 121.0453m, "San Jose del Monte")
    ];

    public VehicleTrackingSimulator()
        : this(new VehicleTrackingService())
    {
    }

    public VehicleTrackingSimulator(VehicleTrackingService trackingService)
    {
        _trackingService = trackingService;
    }

    public async Task<VehicleLocation> InsertNextAsync(int carId)
    {
        (decimal latitude, decimal longitude, string _) = DemoRoute[_routeIndex];
        decimal? heading = _routeIndex == 0 ? null : CalculateHeading(_routeIndex);
        _routeIndex = (_routeIndex + 1) % DemoRoute.Length;

        VehicleLocation location = new()
        {
            CarId = carId,
            Latitude = latitude,
            Longitude = longitude,
            SpeedKph = 38,
            Heading = heading,
            Source = "Simulator",
            RecordedAt = DateTime.Now
        };

        location.VehicleLocationId = await _trackingService.AddLocationAsync(location);
        return location;
    }

    private static decimal CalculateHeading(int routeIndex)
    {
        (decimal currentLat, decimal currentLng, string _) = DemoRoute[routeIndex - 1];
        (decimal nextLat, decimal nextLng, string _) = DemoRoute[routeIndex];
        double y = Math.Sin((double)(nextLng - currentLng)) * Math.Cos((double)nextLat);
        double x = (Math.Cos((double)currentLat) * Math.Sin((double)nextLat))
                 - (Math.Sin((double)currentLat) * Math.Cos((double)nextLat) * Math.Cos((double)(nextLng - currentLng)));
        double heading = (Math.Atan2(y, x) * 180D / Math.PI + 360D) % 360D;
        return (decimal)Math.Round(heading, 2);
    }

    // Future sources such as Bluetooth GPS, phone GPS, or a lightweight API sender
    // should write validated records into VehicleLocations. Offsite remains the viewer.
}
