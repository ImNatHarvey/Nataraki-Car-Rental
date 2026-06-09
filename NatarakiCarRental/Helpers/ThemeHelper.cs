using System.Drawing;
using System.Windows.Forms;

namespace NatarakiCarRental.Helpers;

public static class ThemeHelper
{
    public static readonly Size StandardMainFormSize = new(1280, 720);
    public static readonly Size CompactDialogFormSize = new(860, 500);

    public static readonly Color Background = Color.FromArgb(242, 244, 255);
    public static readonly Color ContentBackground = Color.FromArgb(250, 250, 250);
    public static readonly Color Surface = Color.White;
    public static Color Primary { get; private set; } = Color.FromArgb(37, 99, 235);
    public static Color PrimaryHover { get; private set; } = Color.FromArgb(29, 78, 216);

    public static Color Success { get; private set; } = Color.FromArgb(22, 163, 74);
    public static Color Warning { get; private set; } = Color.FromArgb(217, 119, 6);
    public static Color Danger { get; private set; } = Color.FromArgb(220, 38, 38);
    public static Color Error => Danger;

    public static void SetPrimaryColor(Color color)
    {
        Primary = color;
        PrimaryHover = GetHoverColor(color);

        // Harmonize semantic colors with the branding theme
        Success = HarmonizeColor(Color.FromArgb(22, 163, 74), color);
        Warning = HarmonizeColor(Color.FromArgb(217, 119, 6), color);
        Danger = HarmonizeColor(Color.FromArgb(220, 38, 38), color);
    }

    public static Color GetHoverColor(Color color)
    {
        return Color.FromArgb(
            Math.Max(0, color.R - 30),
            Math.Max(0, color.G - 30),
            Math.Max(0, color.B - 30)
        );
    }

    public static Color GetContrastTextColor(Color bgColor)
    {
        // Calculate relative luminance
        double luminance = (0.299 * bgColor.R + 0.587 * bgColor.G + 0.114 * bgColor.B) / 255;
        return luminance > 0.6 ? TextPrimary : Color.White;
    }

    private static Color HarmonizeColor(Color semantic, Color primary)
    {
        // Blend semantic with primary to harmonize (85% semantic, 15% primary)
        // This ensures the semantic meaning is clear but it "belongs" to the same palette
        return Color.FromArgb(
            (int)(semantic.R * 0.85 + primary.R * 0.15),
            (int)(semantic.G * 0.85 + primary.G * 0.15),
            (int)(semantic.B * 0.85 + primary.B * 0.15)
        );
    }

    // Centralized Dialog Color Helpers
    public static Color GetDialogAccentColor(string type) => type.ToLower() switch
    {
        "success" => Success,
        "warning" => Warning,
        "error" or "danger" => Danger,
        "confirm" or "info" => Primary,
        _ => Primary
    };

    public static Color GetDialogButtonColor(string type) => GetDialogAccentColor(type);
    public static Color GetDialogIconColor(string type) => GetDialogAccentColor(type);

    public static readonly Color Secondary = Color.FromArgb(219, 234, 254); // Light Blue (Original Navigation Accent)
    public static readonly Color StatusGray = Color.FromArgb(71, 85, 105); // Dark Gray (Slate-600) for Completed/Archived
    public static readonly Color Purple = Color.FromArgb(124, 58, 237);
    public static readonly Color GrayIcon = Color.FromArgb(100, 116, 139);
    public static readonly Color TextPrimary = Color.FromArgb(30, 41, 59);
    public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);
    public static readonly Color Border = Color.FromArgb(203, 213, 225);
    public static readonly Color TableGridLine = Color.FromArgb(148, 163, 184);
    public static readonly Color TableGridLineStrong = Color.FromArgb(71, 85, 105);

    public static void ApplyFormDefaults(Form form)
    {
        form.BackColor = ContentBackground;
        form.Font = FontHelper.Regular();
    }

    public static void ApplyStandardMainFormSettings(Form form)
    {
        ApplyFormDefaults(form);

        form.StartPosition = FormStartPosition.CenterScreen;
        form.MinimumSize = StandardMainFormSize;
        form.Size = StandardMainFormSize;
        form.FormBorderStyle = FormBorderStyle.FixedSingle;
        form.MaximizeBox = true;
        form.MinimizeBox = true;
    }

    public static void ApplyCompactDialogFormSettings(Form form)
    {
        form.BackColor = Surface;
        form.Font = FontHelper.Regular();
        form.StartPosition = FormStartPosition.CenterScreen;
        form.ClientSize = CompactDialogFormSize;
        form.FormBorderStyle = FormBorderStyle.FixedSingle;
        form.MaximizeBox = false;
        form.MinimizeBox = true;
    }
}
