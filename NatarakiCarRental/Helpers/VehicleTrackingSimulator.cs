using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.Helpers;

public sealed class VehicleTrackingSimulator
{
    private readonly VehicleTrackingService _trackingService;
    private decimal _currentLat;
    private decimal _currentLng;
    private int _targetIndex;
    private bool _initialized;
    private readonly Random _random = new();

    private static readonly (decimal Latitude, decimal Longitude)[] Waypoints =
    [
        (14.8118m, 120.8631m), // STI Balagtas
        (14.8050m, 120.8800m), // Towards Bocaue
        (14.7944m, 120.9255m), // Bocaue
        (14.8100m, 120.9400m), // Towards Santa Maria
        (14.8191m, 120.9616m), // Santa Maria
        (14.8327m, 120.8794m), // Guiguinto
        (14.8441m, 120.8116m)  // Malolos
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
        if (!_initialized)
        {
            _currentLat = Waypoints[0].Latitude;
            _currentLng = Waypoints[0].Longitude;
            _targetIndex = 1;
            _initialized = true;
        }

        decimal prevLat = _currentLat;
        decimal prevLng = _currentLng;

        // Move 40-70 meters per 5-second tick (approx 28-50 kph)
        double stepDistanceMeters = 40.0 + (_random.NextDouble() * 30.0);
        
        decimal targetLat = Waypoints[_targetIndex].Latitude;
        decimal targetLng = Waypoints[_targetIndex].Longitude;
        
        double distanceToTarget = CalculateDistance(_currentLat, _currentLng, targetLat, targetLng);
        
        if (distanceToTarget < stepDistanceMeters)
        {
            _currentLat = targetLat;
            _currentLng = targetLng;
            _targetIndex = (_targetIndex + 1) % Waypoints.Length;
        }
        else
        {
            double ratio = stepDistanceMeters / distanceToTarget;
            _currentLat += (decimal)((double)(targetLat - _currentLat) * ratio);
            _currentLng += (decimal)((double)(targetLng - _currentLng) * ratio);
        }

        decimal? heading = CalculateHeading(prevLat, prevLng, _currentLat, _currentLng);
        decimal speedKph = (decimal)(stepDistanceMeters / 5.0 * 3.6); // m/s to kph

        VehicleLocation location = new()
        {
            CarId = carId,
            Latitude = _currentLat,
            Longitude = _currentLng,
            SpeedKph = Math.Round(speedKph, 1),
            Heading = heading,
            Source = "Simulator",
            RecordedAt = DateTime.Now
        };

        location.VehicleLocationId = await _trackingService.AddLocationAsync(location);
        return location;
    }

    private static double CalculateDistance(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        const double EarthRadius = 6371000;
        double dLat = DegreeToRadian((double)(lat2 - lat1));
        double dLng = DegreeToRadian((double)(lng2 - lng1));
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(DegreeToRadian((double)lat1)) * Math.Cos(DegreeToRadian((double)lat2)) *
                   Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return EarthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static decimal? CalculateHeading(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        if (lat1 == lat2 && lng1 == lng2) return null;

        double y = Math.Sin(DegreeToRadian((double)(lng2 - lng1))) * Math.Cos(DegreeToRadian((double)lat2));
        double x = (Math.Cos(DegreeToRadian((double)lat1)) * Math.Sin(DegreeToRadian((double)lat2)))
                 - (Math.Sin(DegreeToRadian((double)lat1)) * Math.Cos(DegreeToRadian((double)lat2)) * Math.Cos(DegreeToRadian((double)(lng2 - lng1))));
        double heading = (RadianToDegree(Math.Atan2(y, x)) + 360.0) % 360.0;
        return (decimal)Math.Round(heading, 2);
    }

    private static double DegreeToRadian(double degree) => degree * Math.PI / 180.0;
    private static double RadianToDegree(double radian) => radian * 180.0 / Math.PI;
}
