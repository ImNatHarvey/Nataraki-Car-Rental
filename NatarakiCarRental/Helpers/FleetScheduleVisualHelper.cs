using System.Drawing;
using System.Drawing.Drawing2D;

namespace NatarakiCarRental.Helpers;

public static class FleetScheduleVisualHelper
{
    public static IReadOnlyList<StatusDisplayOption> StatusOptions { get; } =
        FleetScheduleConstants.Status.All.Select(status => new StatusDisplayOption(status, status)).ToList();

    public static string GetDisplayStatus(string status, string? scheduleType = null)
    {
        return StatusOptions.FirstOrDefault(option => option.Value == status)?.Label ?? status;
    }

    public static IReadOnlyList<StatusDisplayOption> GetStatusOptionsForType(string scheduleType)
    {
        string[] statuses = scheduleType switch
        {
            FleetScheduleConstants.Type.Reservation => FleetScheduleConstants.Status.ReservationOptions,
            FleetScheduleConstants.Type.Rental => FleetScheduleConstants.Status.RentalOptions,
            FleetScheduleConstants.Type.Maintenance => FleetScheduleConstants.Status.MaintenanceOptions,
            _ => []
        };

        return statuses.Select(status => new StatusDisplayOption(status, status)).ToList();
    }

    public static string GetDefaultStatusForType(string scheduleType)
    {
        return scheduleType switch
        {
            FleetScheduleConstants.Type.Reservation => FleetScheduleConstants.Status.Pending,
            FleetScheduleConstants.Type.Rental => FleetScheduleConstants.Status.Rented,
            FleetScheduleConstants.Type.Maintenance => FleetScheduleConstants.Status.Pending,
            _ => string.Empty
        };
    }

    public static Color GetColor(string status, string? scheduleType = null)
    {
        return StatusColorHelper.GetStatusColor(status);
    }

    public static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        GraphicsPath path = new();
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return path;
        }

        int diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        if (diameter <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        Rectangle arc = new(rect.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    public sealed record StatusDisplayOption(string Value, string Label)
    {
        public override string ToString() => Label;
    }
}
