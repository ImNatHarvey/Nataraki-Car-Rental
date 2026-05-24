using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.Forms.ManageSystem;

public sealed class UserPasswordForm : Form
{
    private const int InputWidth = 360;
    private const int InputHeight = 30;

    private readonly UserService _userService = new();
    private readonly int _currentUserId;
    private readonly int _targetUserId;
    private readonly string _targetUsername;

    private readonly TextBox _passwordInput = ControlFactory.CreatePasswordTextBox(InputWidth);
    private readonly TextBox _confirmPasswordInput = ControlFactory.CreatePasswordTextBox(InputWidth);

    public UserPasswordForm(int currentUserId, int targetUserId, string targetUsername)
    {
        _currentUserId = currentUserId;
        _targetUserId = targetUserId;
        _targetUsername = targetUsername;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = $"Change Password - {_targetUsername}";
        ThemeHelper.ApplyCompactDialogFormSettings(this);
        ClientSize = new Size(520, 360);

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = ThemeHelper.ContentBackground,
            Padding = new Padding(24, 20, 24, 18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));

        Panel header = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        header.Controls.Add(new Label
        {
            Text = "Change Password",
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(280, 30),
            Font = FontHelper.Title(16F),
            ForeColor = ThemeHelper.TextPrimary
        });
        header.Controls.Add(new Label
        {
            Text = $"Update the password for {_targetUsername}.",
            AutoSize = false,
            Location = new Point(1, 30),
            Size = new Size(420, 22),
            Font = FontHelper.Regular(9.5F),
            ForeColor = ThemeHelper.TextSecondary
        });
        root.Controls.Add(header, 0, 0);

        GroupBox securityGroup = new()
        {
            Text = "Security",
            Dock = DockStyle.Top,
            Height = 182,
            Font = FontHelper.SemiBold(10F),
            ForeColor = ThemeHelper.TextPrimary,
            BackColor = ThemeHelper.Surface
        };
        AddLabeledControl(securityGroup, "New Password *", _passwordInput, 24, 34);
        AddLabeledControl(securityGroup, "Confirm Password *", _confirmPasswordInput, 24, 102);
        Label helper = new()
        {
            Text = "Minimum 8 characters.",
            AutoSize = false,
            Location = new Point(24, 154),
            Size = new Size(360, 20),
            Font = FontHelper.Regular(9F),
            ForeColor = ThemeHelper.TextSecondary
        };
        securityGroup.Controls.Add(helper);

        Panel content = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        content.Controls.Add(securityGroup);
        root.Controls.Add(content, 0, 1);

        Panel footer = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        Button cancelButton = ControlFactory.CreateSecondaryButton("Cancel", 110, 38);
        cancelButton.Location = new Point(244, 12);
        cancelButton.Click += (_, _) => Close();
        Button saveButton = ControlFactory.CreatePrimaryButton("Change Password", 148, 38);
        saveButton.Location = new Point(368, 12);
        saveButton.Click += SaveButton_Click;
        footer.Controls.Add(cancelButton);
        footer.Controls.Add(saveButton);
        root.Controls.Add(footer, 0, 2);

        Controls.Add(root);
    }

    private static void AddLabeledControl(Control parent, string labelText, Control input, int x, int y)
    {
        Label label = ControlFactory.CreateInputLabel(labelText);
        label.Location = new Point(x, y);
        input.Location = new Point(x, y + 23);
        input.Size = new Size(InputWidth, InputHeight);
        input.Font = FontHelper.Regular(10F);
        parent.Controls.Add(label);
        parent.Controls.Add(input);
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_passwordInput.Text != _confirmPasswordInput.Text)
            {
                MessageBoxHelper.ShowWarning("Passwords do not match.");
                return;
            }

            await _userService.ChangePasswordAsync(new ChangePasswordRequest
            {
                UserId = _targetUserId,
                NewPassword = _passwordInput.Text
            }, _currentUserId);

            MessageBoxHelper.ShowSuccess("Password updated successfully.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBoxHelper.ShowWarning(ex.Message, "Manage System");
        }
    }
}
