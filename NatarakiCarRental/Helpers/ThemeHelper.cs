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
    public static readonly Color Primary = Color.FromArgb(37, 99, 235);
    public static readonly Color PrimaryHover = Color.FromArgb(29, 78, 216);
    public static readonly Color Secondary = Color.FromArgb(219, 234, 254);
    public static readonly Color Success = Color.FromArgb(22, 163, 74);
    public static readonly Color Warning = Color.FromArgb(217, 119, 6);
    public static readonly Color Purple = Color.FromArgb(124, 58, 237);
    public static readonly Color GrayIcon = Color.FromArgb(100, 116, 139);
    public static readonly Color TextPrimary = Color.FromArgb(30, 41, 59);
    public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);
    public static readonly Color Border = Color.FromArgb(203, 213, 225);
    public static readonly Color TableGridLine = Color.FromArgb(148, 163, 184);
    public static readonly Color TableGridLineStrong = Color.FromArgb(71, 85, 105);
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);
    public static readonly Color Error = Danger;

    public static readonly Color AppBackground = Background;
    public static readonly Color SidebarBackground = Surface;
    public static readonly Color Accent = Secondary;
    public static readonly Color HeaderText = TextPrimary;
    public static readonly Color MutedText = TextSecondary;

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
