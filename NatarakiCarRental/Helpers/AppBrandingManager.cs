using System.Drawing;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.Helpers;

public static class AppBrandingManager
{
    public static SystemSettingsModel CurrentSettings { get; private set; } = new SystemSettingsModel();
    public static event EventHandler? SettingsUpdated;

    public static async Task LoadSettingsAsync()
    {
        try
        {
            var service = new SystemSettingsService();
            CurrentSettings = await service.GetSettingsAsync();
            ApplyTheme();
            NotifySettingsUpdated();
        }
        catch
        {
            // Fallback to defaults on error
            CurrentSettings = new SystemSettingsModel();
            ApplyTheme();
        }
    }

    public static void NotifySettingsUpdated()
    {
        ApplyTheme();
        SettingsUpdated?.Invoke(null, EventArgs.Empty);
    }

    public static void ApplyTheme()
    {
        try
        {
            Color color = ColorTranslator.FromHtml(CurrentSettings.ThemeColor);
            ThemeHelper.SetPrimaryColor(color);
        }
        catch
        {
            ThemeHelper.SetPrimaryColor(Color.FromArgb(37, 99, 235)); // Default Blue
        }
    }
}
