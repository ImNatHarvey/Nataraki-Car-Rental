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

        // Normalize status for mapping
        return status.Trim() switch
        {
            // Primary - Blue (Reserved, Scheduled, Confirmed)
            "Reserved" or "Scheduled" or "Reservation" or "Confirmed" => ThemeHelper.Primary,

            // Success - Green (Rented, Active, Available, Paid)
            "Rented" or "Active" or "Available" or "Paid" or "High" => ThemeHelper.Success,

            // Warning - Orange (Maintenance, Pending, Ongoing, Medium)
            "Pending" or "Maintenance" or "Ongoing" or "Medium" or "Extend" or "Partial" => ThemeHelper.Warning,

            // Danger - Red (Cancelled, Blacklisted, Unpaid, Overdue)
            "Cancelled" or "Blacklisted" or "Unpaid" or "Overdue" or "OVERDUE" or "Danger" or "Critical" => ThemeHelper.Danger,

            // Secondary - Gray (Completed, Archived)
            "Completed" or "Archived" or "Low" or "None" or "Not Applicable" => ThemeHelper.StatusGray,

            // Default
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
