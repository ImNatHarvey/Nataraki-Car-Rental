using System.Runtime.InteropServices;
using FontAwesome.Sharp;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Helpers;

public static class BrandingHelper
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

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
        SystemSettingsModel settings = AppBrandingManager.CurrentSettings;

        // 1. Try custom logo mode
        if (string.Equals(settings.SystemLogoMode, "File", StringComparison.OrdinalIgnoreCase))
        {
            // First try to load as a direct .ico if configured
            string iconPath = settings.SystemIconPath;
            if (!string.IsNullOrWhiteSpace(iconPath)
                && File.Exists(iconPath)
                && string.Equals(Path.GetExtension(iconPath), ".ico", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return new System.Drawing.Icon(iconPath);
                }
                catch
                {
                    // Fallback to dynamic conversion
                }
            }

            // Fallback: Convert the current logo image to an icon dynamically
            using Image? logoImage = LoadCurrentLogoImage();
            if (logoImage is not null)
            {
                return ConvertImageToIcon(logoImage);
            }
        }

        // 2. Built-in logo mode or fallback: Generate from FontAwesome
        try
        {
            IconChar iconChar = ResolveCurrentBuiltInLogoIcon();
            
            // Create a 32x32 bitmap for the icon rendering
            using Bitmap bitmap = new(32, 32);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                // Use FontAwesome's ToBitmap method or manual rendering
                // ToBitmap is generally more reliable for sizing
                using Bitmap faBitmap = iconChar.ToBitmap(ThemeHelper.Primary, 32);
                graphics.DrawImage(faBitmap, 0, 0, 32, 32);
            }

            return ConvertImageToIcon(bitmap);
        }
        catch
        {
            return null;
        }
    }

    public static System.Drawing.Icon? ConvertImageToIcon(Image? sourceImage)
    {
        if (sourceImage is null)
        {
            return null;
        }

        try
        {
            // Create a 32x32 bitmap for the icon
            using Bitmap bitmap = new(32, 32);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(sourceImage, 0, 0, 32, 32);
            }

            IntPtr hIcon = bitmap.GetHicon();
            try
            {
                // Create a managed icon from the unmanaged handle and clone it to ensure it's managed-owned
                using System.Drawing.Icon tempIcon = System.Drawing.Icon.FromHandle(hIcon);
                return (System.Drawing.Icon)tempIcon.Clone();
            }
            finally
            {
                // Safely destroy the unmanaged handle to prevent GDI leaks
                DestroyIcon(hIcon);
            }
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
