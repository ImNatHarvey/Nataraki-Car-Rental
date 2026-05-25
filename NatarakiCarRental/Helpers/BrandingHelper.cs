using FontAwesome.Sharp;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Helpers;

public static class BrandingHelper
{
    public static Image? LoadCurrentLogoImage()
    {
        SystemSettingsModel settings = AppBrandingManager.CurrentSettings;

        if (!string.Equals(settings.SystemLogoMode, "File", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(settings.SystemIconPath)
            || !File.Exists(settings.SystemIconPath))
        {
            return null;
        }

        try
        {
            if (string.Equals(Path.GetExtension(settings.SystemIconPath), ".ico", StringComparison.OrdinalIgnoreCase))
            {
                using System.Drawing.Icon icon = new(settings.SystemIconPath);
                using Bitmap bitmap = icon.ToBitmap();
                return new Bitmap(bitmap);
            }

            using FileStream stream = new(settings.SystemIconPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using Image image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch
        {
            return null;
        }
    }

    public static System.Drawing.Icon? LoadCurrentWindowIcon()
    {
        string iconPath = AppBrandingManager.CurrentSettings.SystemIconPath;
        if (string.IsNullOrWhiteSpace(iconPath)
            || !File.Exists(iconPath)
            || !string.Equals(Path.GetExtension(iconPath), ".ico", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            return new System.Drawing.Icon(iconPath);
        }
        catch
        {
            return null;
        }
    }

    public static IconChar ResolveCurrentBuiltInLogoIcon()
    {
        return ResolveBuiltInLogoIcon(AppBrandingManager.CurrentSettings.SystemLogoIconKey);
    }

    public static IconChar ResolveBuiltInLogoIcon(string? key)
    {
        return key switch
        {
            "CarSide" => IconChar.CarSide,
            "Taxi" => IconChar.Taxi,
            "Truck" => IconChar.Truck,
            "Road" => IconChar.Road,
            "Warehouse" => IconChar.Warehouse,
            "Key" => IconChar.Key,
            _ => IconChar.Car
        };
    }
}
