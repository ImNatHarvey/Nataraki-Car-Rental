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
    private bool _movingForward = true;
    private readonly Random _random = new();

    private static readonly (decimal Latitude, decimal Longitude)[] Waypoints =
    [
        (14.8171m, 120.8953m), // Wawa, Balagtas
        (14.8176m, 120.8936m),
        (14.8180m, 120.8920m),
        (14.8188m, 120.8893m),
        (14.8211m, 120.8821m),
        (14.8236m, 120.8732m),
        (14.8256m, 120.8677m),
        (14.8277m, 120.8636m), // Tabang, Guiguinto
        (14.8315m, 120.8585m),
        (14.8344m, 120.8546m),
        (14.8368m, 120.8524m), // Guiguinto crossing
        (14.8406m, 120.8466m),
        (14.8436m, 120.8385m), // Tikay, Malolos
        (14.8443m, 120.8335m),
        (14.8450m, 120.8288m),
        (14.8456m, 120.8250m),
        (14.8462m, 120.8222m),
        (14.8475m, 120.8164m)  // Malolos Bayan
    ];

    public static IReadOnlyList<(decimal Lat, decimal Lng)> GetRoutePoints() => Waypoints;

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
            _movingForward = true;
            _initialized = true;
        }

        decimal prevLat = _currentLat;
        decimal prevLng = _currentLng;

        // Move 20-80 meters per tick (approx 15-60 kph)
        double stepDistanceMeters = 20.0 + (_random.NextDouble() * 60.0);

        decimal targetLat = Waypoints[_targetIndex].Latitude;
        decimal targetLng = Waypoints[_targetIndex].Longitude;

        double distanceToTarget = CalculateDistance(_currentLat, _currentLng, targetLat, targetLng);

        if (distanceToTarget < stepDistanceMeters)
        {
            _currentLat = targetLat;
            _currentLng = targetLng;

            if (_movingForward)
            {
                if (_targetIndex + 1 >= Waypoints.Length)
                {
                    _movingForward = false;
                    _targetIndex = Waypoints.Length - 2;
                }
                else
                {
                    _targetIndex++;
                }
            }
            else
            {
                if (_targetIndex - 1 < 0)
                {
                    _movingForward = true;
                    _targetIndex = 1;
                }
                else
                {
                    _targetIndex--;
                }
            }
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
            Source = "Road Simulation",
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
