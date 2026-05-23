using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.Forms.ManageSystem;

public sealed class UserPasswordForm : Form
{
    private readonly UserService _userService = new();
    private readonly int _currentUserId;
    private readonly int _targetUserId;
    private readonly string _targetUsername;

    private readonly TextBox _passwordInput = ControlFactory.CreatePasswordTextBox(360);
    private readonly TextBox _confirmPasswordInput = ControlFactory.CreatePasswordTextBox(360);

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
        ClientSize = new Size(420, 320);

        int y = 24;
        AddInputControl("New Password *", _passwordInput, ref y);
        AddInputControl("Confirm Password *", _confirmPasswordInput, ref y);

        Button saveButton = ControlFactory.CreatePrimaryButton("Update Password", 180, 40);
        saveButton.Location = new Point(24, y);
        saveButton.Click += SaveButton_Click;

        Button cancelButton = ControlFactory.CreateSecondaryButton("Cancel", 100, 40);
        cancelButton.Location = new Point(214, y);
        cancelButton.Click += (_, _) => Close();

        Controls.Add(saveButton);
        Controls.Add(cancelButton);
    }

    private void AddInputControl(string label, Control input, ref int y)
    {
        Label lbl = ControlFactory.CreateInputLabel(label);
        lbl.Location = new Point(24, y);
        Controls.Add(lbl);
        y += 24;

        input.Location = new Point(24, y);
        Controls.Add(input);
        y += 48;
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
            MessageBoxHelper.ShowWarning(ex.Message);
        }
    }
}
