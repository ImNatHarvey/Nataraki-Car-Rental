using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.UserControls.Cards;

public sealed class MetricCardControl : BorderedPanel
{
    private readonly IconPictureBox _iconBox = new();
    private readonly Label _titleLabel = new();
    private readonly Label _valueLabel = new();
    private readonly Label _helperTextLabel = new();

    public MetricCardControl()
    {
        InitializeControl();
    }

    public void SetMetric(IconChar icon, string title, string value, string helperText)
    {
        SetMetric(icon, title, value, helperText, ThemeHelper.Primary);
    }

    public void SetMetric(IconChar icon, string title, string value, string helperText, Color iconColor)
    {
        _iconBox.IconChar = icon;
        _iconBox.IconColor = iconColor;
        _titleLabel.Text = title;
        _valueLabel.Text = value;
        _helperTextLabel.Text = helperText;
    }

    private void InitializeControl()
    {
        BackColor = ThemeHelper.Surface;
        BorderColor = ThemeHelper.Border;
        Margin = new Padding(0);
        Padding = new Padding(18);
        MinimumSize = new Size(180, 110);

        _iconBox.IconColor = ThemeHelper.Primary;
        _iconBox.IconSize = 24;
        _iconBox.BackColor = ThemeHelper.Surface;
        _iconBox.Location = new Point(18, 18);
        _iconBox.Size = new Size(30, 30);

        _titleLabel.AutoSize = false;
        _titleLabel.Location = new Point(54, 20);
        _titleLabel.Size = new Size(Width - 72, 24);
        _titleLabel.Font = FontHelper.SemiBold(9F);
        _titleLabel.ForeColor = ThemeHelper.TextSecondary;
        _titleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        _valueLabel.AutoSize = false;
        _valueLabel.Location = new Point(18, 56);
        _valueLabel.Size = new Size(Width - 36, 32);
        _valueLabel.Font = FontHelper.Title(18F);
        _valueLabel.ForeColor = ThemeHelper.TextPrimary;
        _valueLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        _helperTextLabel.AutoSize = false;
        _helperTextLabel.Location = new Point(18, 90);
        _helperTextLabel.Size = new Size(Width - 36, 26);
        _helperTextLabel.Font = FontHelper.Regular(8.5F);
        _helperTextLabel.ForeColor = ThemeHelper.TextSecondary;
        _helperTextLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        Controls.Add(_iconBox);
        Controls.Add(_titleLabel);
        Controls.Add(_valueLabel);
        Controls.Add(_helperTextLabel);
    }
}
