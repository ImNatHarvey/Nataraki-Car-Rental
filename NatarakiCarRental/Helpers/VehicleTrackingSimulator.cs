using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.Helpers;

public sealed class VehicleTrackingSimulator
{
    private readonly VehicleTrackingService _trackingService;
    private int _lastCarId = -1;
    private decimal _currentLat;
    private decimal _currentLng;
    private int _targetIndex;
    private bool _initialized;
    private bool _movingForward = true;
    private readonly Random _random = new();

    private static readonly (decimal Latitude, decimal Longitude)[][] AllRoutes =
    [
        // Route 1: Balagtas to Malolos (MacArthur Highway)
        [
            (14.8171m, 120.8953m), (14.8188m, 120.8893m), (14.8211m, 120.8821m), (14.8236m, 120.8732m),
            (14.8256m, 120.8677m), (14.8277m, 120.8636m), (14.8315m, 120.8585m), (14.8344m, 120.8546m),
            (14.8368m, 120.8524m), (14.8406m, 120.8466m), (14.8436m, 120.8385m), (14.8475m, 120.8164m)
        ],
        // Route 2: Marilao to Bocaue (MacArthur Highway South)
        [
            (14.7578m, 120.9575m), (14.7645m, 120.9520m), (14.7733m, 120.9412m), (14.7812m, 120.9345m),
            (14.7885m, 120.9298m), (14.7944m, 120.9255m), (14.8021m, 120.9150m), (14.8111m, 120.9022m)
        ],
        // Route 3: Malolos to Plaridel (Governor Padilla Road)
        [
            (14.8475m, 120.8164m), (14.8485m, 120.8250m), (14.8512m, 120.8380m), (14.8540m, 120.8450m),
            (14.8561m, 120.8519m), (14.8620m, 120.8550m), (14.8750m, 120.8570m), (14.8878m, 120.8583m)
        ],
        // Route 4: San Jose Del Monte (SJDM-Marilao Road)
        [
            (14.8118m, 121.0504m), (14.8080m, 121.0450m), (14.8042m, 121.0380m), (14.7995m, 121.0310m),
            (14.7940m, 121.0250m), (14.7880m, 121.0180m), (14.7820m, 121.0100m), (14.7760m, 120.9950m)
        ],
        // Route 5: Meycauayan Northward
        [
            (14.7350m, 120.9578m), (14.7400m, 120.9590m), (14.7480m, 120.9610m), (14.7550m, 120.9580m),
            (14.7620m, 120.9540m), (14.7690m, 120.9480m), (14.7750m, 120.9420m), (14.7820m, 120.9350m)
        ]
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
        var routeIndex = Math.Abs(carId) % AllRoutes.Length;
        var waypoints = AllRoutes[routeIndex];

        if (!_initialized || _lastCarId != carId)
        {
            _currentLat = waypoints[0].Latitude;
            _currentLng = waypoints[0].Longitude;
            _targetIndex = 1;
            _movingForward = true;
            _initialized = true;
            _lastCarId = carId;
        }

        decimal prevLat = _currentLat;
        decimal prevLng = _currentLng;

        // Move 20-80 meters per tick (approx 15-60 kph)
        double stepDistanceMeters = 20.0 + (_random.NextDouble() * 60.0);

        decimal targetLat = waypoints[_targetIndex].Latitude;
        decimal targetLng = waypoints[_targetIndex].Longitude;

        double distanceToTarget = CalculateDistance(_currentLat, _currentLng, targetLat, targetLng);

        if (distanceToTarget < stepDistanceMeters)
        {
            _currentLat = targetLat;
            _currentLng = targetLng;

            if (_movingForward)
            {
                if (_targetIndex + 1 >= waypoints.Length)
                {
                    _movingForward = false;
                    _targetIndex = waypoints.Length - 2;
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
    }    private static double CalculateDistance(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
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
