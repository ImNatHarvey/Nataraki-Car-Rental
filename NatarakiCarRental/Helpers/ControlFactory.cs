using FontAwesome.Sharp;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.Helpers;

public static class ControlFactory
{
    public static Button CreatePrimaryButton(string text, int width = 280, int height = 40)
    {
        Button button = new()
        {
            Text = text,
            Size = new Size(width, height),
            BackColor = ThemeHelper.Primary,
            ForeColor = Color.White,
            Font = FontHelper.SemiBold(),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = ThemeHelper.PrimaryHover;

        return button;
    }

    public static Button CreateSecondaryButton(string text, int width = 280, int height = 40)
    {
        Button button = new()
        {
            Text = text,
            Size = new Size(width, height),
            BackColor = ThemeHelper.Surface,
            ForeColor = ThemeHelper.TextPrimary,
            Font = FontHelper.SemiBold(),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderColor = ThemeHelper.Border;
        button.FlatAppearance.MouseOverBackColor = ThemeHelper.Background;

        return button;
    }

    public static IconButton CreateSidebarButton(string text, IconChar icon, bool isDanger = false)
    {
        IconButton button = new()
        {
            Text = text,
            IconChar = icon,
            IconColor = isDanger ? ThemeHelper.Danger : ThemeHelper.TextSecondary,
            IconSize = 18,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleLeft,
            Size = new Size(228, 42),
            Padding = new Padding(14, 0, 0, 0),
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent,
            ForeColor = isDanger ? ThemeHelper.Danger : ThemeHelper.TextPrimary,
            Font = FontHelper.Regular(9.5F),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseMnemonic = false
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = isDanger ? Color.FromArgb(254, 226, 226) : ThemeHelper.Secondary;

        return button;
    }

    public static TextBox CreateTextBox(int width = 280)
    {
        return new TextBox
        {
            Width = width,
            Height = 30,
            BorderStyle = BorderStyle.FixedSingle,
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextPrimary
        };
    }

    public static Label CreateInputLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Text = text,
            Font = FontHelper.SemiBold(9F),
            ForeColor = ThemeHelper.TextPrimary
        };
    }

    public static TextBox CreatePasswordTextBox(int width = 280)
    {
        TextBox textBox = CreateTextBox(width);
        textBox.UseSystemPasswordChar = true;

        return textBox;
    }

    public static BorderedPanel CreateCardPanel(Size size)
    {
        BorderedPanel panel = new()
        {
            Size = size,
            BackColor = ThemeHelper.Surface,
            Padding = new Padding(28),
            BorderColor = ThemeHelper.Border
        };

        return panel;
    }

    public static void ApplyRoundedPanel(Control panel)
    {
        if (panel is BorderedPanel borderedPanel)
        {
            borderedPanel.BorderColor = ThemeHelper.Border;
            borderedPanel.Invalidate();
        }
    }
}
