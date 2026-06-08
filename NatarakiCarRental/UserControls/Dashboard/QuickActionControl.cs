using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.UserControls.Dashboard;

public sealed class QuickActionControl : BorderedPanel
{
    private readonly IconButton _button = new();

    public QuickActionControl(IconChar icon, string title, Action onClick)
    {
        InitializeControl(icon, title, onClick);
    }

    private void InitializeControl(IconChar icon, string title, Action onClick)
    {
        BackColor = ThemeHelper.Surface;
        BorderColor = ThemeHelper.Border;
        Size = new Size(160, 90);
        Cursor = Cursors.Hand;
        Padding = new Padding(1);

        _button.Dock = DockStyle.Fill;
        _button.FlatStyle = FlatStyle.Flat;
        _button.FlatAppearance.BorderSize = 0;
        _button.BackColor = ThemeHelper.Surface;
        _button.IconChar = icon;
        _button.IconColor = ThemeHelper.Primary;
        _button.IconSize = 28;
        _button.Text = title;
        _button.ForeColor = ThemeHelper.TextPrimary;
        _button.Font = FontHelper.SemiBold(8.5F);
        _button.TextImageRelation = TextImageRelation.ImageAboveText;
        _button.ImageAlign = ContentAlignment.BottomCenter;
        _button.TextAlign = ContentAlignment.TopCenter;
        _button.Padding = new Padding(0, 12, 0, 10);
        _button.Cursor = Cursors.Hand;
        
        _button.FlatAppearance.MouseOverBackColor = ThemeHelper.Secondary;
        _button.FlatAppearance.MouseDownBackColor = Color.FromArgb(226, 232, 240);

        _button.Click += (s, e) => onClick();

        Controls.Add(_button);
    }
}
