using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.Forms.ManageSystem;

public sealed class OwnerPasswordConfirmationForm : Form
{
    private const int ContentLeft = 36;
    private const int PasswordWidth = 380;
    private const int ButtonWidth = 110;
    private const int ButtonGap = 12;

    private readonly SecurityVerificationService _verificationService = new();
    private readonly string _actionName;
    private readonly TextBox _passwordInput = ControlFactory.CreatePasswordTextBox(PasswordWidth);
    private readonly IconButton _passwordPreviewButton = new();

    public OwnerPasswordConfirmationForm(string actionName)
    {
        _actionName = actionName;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Owner Verification Required";
        ThemeHelper.ApplyCompactDialogFormSettings(this);
        ClientSize = new Size(460, 292);

        Label title = new()
        {
            Text = "Owner Verification Required",
            AutoSize = false,
            Location = new Point(ContentLeft, 22),
            Size = new Size(PasswordWidth, 30),
            Font = FontHelper.Title(15F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Label message = new()
        {
            Text = $"Enter the owner password to continue:\n{_actionName}",
            AutoSize = false,
            Location = new Point(ContentLeft, 62),
            Size = new Size(PasswordWidth, 52),
            Font = FontHelper.Regular(9.5F),
            ForeColor = ThemeHelper.TextSecondary
        };

        Label passwordLabel = ControlFactory.CreateInputLabel("Owner Password *");
        passwordLabel.Location = new Point(ContentLeft, 132);
        BorderedPanel passwordPanel = CreatePasswordFieldPanel(_passwordInput, _passwordPreviewButton, PasswordWidth);
        passwordPanel.Location = new Point(ContentLeft, 156);

        int passwordRight = passwordPanel.Right;
        Button cancelButton = ControlFactory.CreateSecondaryButton("Cancel", ButtonWidth, 38);
        cancelButton.Location = new Point(passwordRight - (ButtonWidth * 2) - ButtonGap, 226);
        cancelButton.Click += (_, _) => Close();
        cancelButton.DialogResult = DialogResult.Cancel;

        Button verifyButton = ControlFactory.CreatePrimaryButton("Verify", ButtonWidth, 38);
        verifyButton.Location = new Point(passwordRight - ButtonWidth, 226);
        verifyButton.Click += VerifyButton_Click;

        Controls.Add(title);
        Controls.Add(message);
        Controls.Add(passwordLabel);
        Controls.Add(passwordPanel);
        Controls.Add(cancelButton);
        Controls.Add(verifyButton);
        AcceptButton = verifyButton;
        CancelButton = cancelButton;
    }

    private static BorderedPanel CreatePasswordFieldPanel(TextBox input, IconButton previewButton, int width)
    {
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
        input.Width = width - 48;
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

    private static void TogglePasswordPreview(TextBox input, IconButton previewButton)
    {
        bool showPassword = input.UseSystemPasswordChar;
        input.UseSystemPasswordChar = !showPassword;
        previewButton.IconChar = showPassword ? IconChar.EyeSlash : IconChar.Eye;
        input.Focus();
    }

    private async void VerifyButton_Click(object? sender, EventArgs e)
    {
        try
        {
            bool verified = await _verificationService.VerifyOwnerPasswordAsync(_passwordInput.Text);
            if (!verified)
            {
                MessageBoxHelper.ShowWarning("Owner verification failed. Please check the owner password.");
                _passwordInput.Clear();
                _passwordInput.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning($"Unable to verify owner password.\n\n{exception.Message}", "Owner Verification");
        }
    }
}
