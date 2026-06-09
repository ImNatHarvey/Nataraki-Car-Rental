using System.Drawing;

namespace NatarakiCarRental.Helpers;

/// <summary>
/// Centralized helper for status-to-color mapping across the system.
/// Ensures consistent branding and UI feedback for all modules.
/// </summary>
public static class StatusColorHelper
{
    public static Color GetStatusColor(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return ThemeHelper.GrayIcon;
        }

        return status.Trim() switch
        {
            // Pending - Brown
            "Pending" => ThemeHelper.WarningDark,

            // Scheduled - Blue
            "Scheduled" => ThemeHelper.Primary,

            // Rented, Active, Available - Green
            "Rented" or "Active" or "Available" => ThemeHelper.Success,

            // Maintenance, Ongoing - Orange
            "Maintenance" or "Ongoing" => ThemeHelper.Warning,

            // Completed, Archived - Gray
            "Completed" or "Archived" => ThemeHelper.StatusGray,

            // Cancelled, Overdue, Unpaid - Red
            "Cancelled" or "Overdue" or "Unpaid" => ThemeHelper.Danger,

            // Partial - Brown
            "Partial" => ThemeHelper.WarningDark,

            // Paid - Green
            "Paid" => ThemeHelper.Success,

            _ => ThemeHelper.GrayIcon
        };
    }

    /// <summary>
    /// Gets the recommended text color (contrast) for a given status background.
    /// </summary>
    public static Color GetStatusForeColor(string? status)
    {
        return ThemeHelper.GetContrastTextColor(GetStatusColor(status));
    }

    /// <summary>
    /// Gets a color suitable for icons or text representing the status.
    /// Returns a darker/more visible version than the background pill color if needed.
    /// </summary>
    public static Color GetStatusIconColor(string? status)
    {
        Color color = GetStatusColor(status);
        
        // If it's the secondary (light gray), use the darker GrayIcon for better visibility as an icon/text
        if (color == ThemeHelper.StatusGray)
        {
            return ThemeHelper.GrayIcon;
        }

        return color;
    }
}
