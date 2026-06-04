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

    public static BorderedPanel CreatePasswordFieldPanel(TextBox input, int width)
    {
        IconButton previewButton = new();
        BorderedPanel panel = new()
        {
            Size = new Size(width, 30),
            BackColor = ThemeHelper.Surface,
            BorderColor = ThemeHelper.Border,
            Cursor = Cursors.IBeam
        };
        panel.Click += (_, _) => input.Focus();

        input.BorderStyle = BorderStyle.None;
        input.BackColor = ThemeHelper.Surface;
        input.Location = new Point(8, 6);
        input.Size = new Size(width - 48, 26);
        input.Cursor = Cursors.IBeam;

        previewButton.Size = new Size(34, 28);
        previewButton.Location = new Point(width - 35, 1);
        previewButton.IconChar = IconChar.Eye;
        previewButton.IconColor = ThemeHelper.TextSecondary;
        previewButton.IconSize = 16;
        previewButton.BackColor = ThemeHelper.Surface;
        previewButton.FlatStyle = FlatStyle.Flat;
        previewButton.Cursor = Cursors.Hand;
        previewButton.TabStop = false;
        previewButton.Text = string.Empty;
        previewButton.FlatAppearance.BorderSize = 0;
        previewButton.FlatAppearance.MouseOverBackColor = ThemeHelper.ContentBackground;
        previewButton.FlatAppearance.MouseDownBackColor = ThemeHelper.Secondary;
        previewButton.Click += (_, _) => TogglePasswordPreview(input, previewButton);

        panel.Controls.Add(input);
        panel.Controls.Add(previewButton);
        return panel;
    }

    public static void TogglePasswordPreview(TextBox input, IconButton previewButton)
    {
        bool showPassword = input.UseSystemPasswordChar;
        input.UseSystemPasswordChar = !showPassword;
        previewButton.IconChar = showPassword ? IconChar.EyeSlash : IconChar.Eye;
        input.Focus();
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
