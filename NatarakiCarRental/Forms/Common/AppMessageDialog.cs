using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;

namespace NatarakiCarRental.Forms.Common;

public sealed class AppMessageDialog : Form
{
    public AppMessageDialog(string title, string message, IconChar icon, Color accentColor)
    {
        InitializeDialog(title, message, icon, accentColor, isConfirmation: false);
    }

    public AppMessageDialog(string title, string message, IconChar icon, Color accentColor, bool isConfirmation)
    {
        InitializeDialog(title, message, icon, accentColor, isConfirmation);
    }

    private void InitializeDialog(string title, string message, IconChar icon, Color accentColor, bool isConfirmation)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Size measuredMessageSize = TextRenderer.MeasureText(
            message,
            FontHelper.Regular(9.5F),
            new Size(310, 0),
            TextFormatFlags.WordBreak);
        int messageHeight = Math.Clamp(measuredMessageSize.Height + 8, 66, 132);
        int dialogHeight = 124 + messageHeight;

        ClientSize = new Size(430, dialogHeight);
        BackColor = ThemeHelper.Surface;
        Font = FontHelper.Regular();
        ShowInTaskbar = false;

        Panel accentPanel = new()
        {
            Dock = DockStyle.Bottom,
            Height = 6,
            BackColor = accentColor
        };

        Control logoControl = CreateBrandingLogoControl(accentColor);

        Label titleLabel = new()
        {
            Text = title,
            AutoSize = false,
            Location = new Point(84, 28),
            Size = new Size(310, 28),
            Font = FontHelper.Title(12F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Label messageLabel = new()
        {
            Text = message,
            AutoSize = false,
            Location = new Point(86, 62),
            Size = new Size(310, messageHeight),
            Font = FontHelper.Regular(9.5F),
            ForeColor = ThemeHelper.TextSecondary
        };

        // Primary Button ("Yes" or "OK") is now always on the right
        Button primaryButton = ControlFactory.CreatePrimaryButton(isConfirmation ? "Yes" : "OK", 92, 34);
        primaryButton.BackColor = accentColor;
        primaryButton.FlatAppearance.MouseOverBackColor = accentColor;
        primaryButton.Location = new Point(306, dialogHeight - 52);
        primaryButton.DialogResult = isConfirmation ? DialogResult.Yes : DialogResult.OK;

        Button? secondaryButton = null;

        if (isConfirmation)
        {
            // Secondary Button ("No") is now on the left
            secondaryButton = new Button
            {
                Text = "No",
                Size = new Size(92, 34),
                Location = new Point(204, dialogHeight - 52),
                BackColor = ThemeHelper.Surface,
                ForeColor = ThemeHelper.TextPrimary,
                Font = FontHelper.SemiBold(),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.No
            };
            secondaryButton.FlatAppearance.BorderColor = ThemeHelper.Border;
        }

        Controls.Add(accentPanel);
        Controls.Add(logoControl);
        Controls.Add(titleLabel);
        Controls.Add(messageLabel);
        Controls.Add(primaryButton);

        if (secondaryButton is not null)
        {
            Controls.Add(secondaryButton);
            CancelButton = secondaryButton;
        }

        AcceptButton = primaryButton;
    }

    private static Control CreateBrandingLogoControl(Color accentColor)
    {
        Image? logoImage = BrandingHelper.LoadCurrentLogoImage();
        if (logoImage is not null)
        {
            return new PictureBox
            {
                Image = logoImage,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = ThemeHelper.Surface,
                Location = new Point(28, 30),
                Size = new Size(40, 40)
            };
        }

        return new IconPictureBox
        {
            IconChar = BrandingHelper.ResolveCurrentBuiltInLogoIcon(),
            IconColor = string.Equals(AppBrandingManager.CurrentSettings.SystemLogoMode, "BuiltIn", StringComparison.OrdinalIgnoreCase)
                ? ThemeHelper.Primary
                : accentColor,
            IconSize = 34,
            BackColor = ThemeHelper.Surface,
            Location = new Point(28, 30),
            Size = new Size(40, 40)
        };
    }
}
