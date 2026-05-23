using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.Forms.ManageSystem;

public sealed class UserDetailsForm : Form
{
    private readonly UserService _userService = new();
    private readonly RoleService _roleService = new();
    private readonly int _currentUserId;
    private readonly int? _targetUserId;
    private readonly bool _isEdit;

    private readonly TextBox _firstNameInput = ControlFactory.CreateTextBox(360);
    private readonly TextBox _lastNameInput = ControlFactory.CreateTextBox(360);
    private readonly TextBox _usernameInput = ControlFactory.CreateTextBox(360);
    private readonly TextBox _passwordInput = ControlFactory.CreatePasswordTextBox(360);
    private readonly TextBox _confirmPasswordInput = ControlFactory.CreatePasswordTextBox(360);
    private readonly ComboBox _roleComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly CheckBox _isActiveCheckBox = new() { Text = "User is active and can log in", AutoSize = true, Checked = true };

    private readonly List<Role> _roles = [];

    public UserDetailsForm(int currentUserId, int? targetUserId = null)
    {
        _currentUserId = currentUserId;
        _targetUserId = targetUserId;
        _isEdit = targetUserId.HasValue;

        InitializeComponent();
        LoadRolesAndUserData();
    }

    private void InitializeComponent()
    {
        Text = _isEdit ? "Edit User Account" : "Add New User Account";
        ThemeHelper.ApplyCompactDialogFormSettings(this);
        ClientSize = new Size(420, _isEdit ? 480 : 620);

        int y = 24;
        AddInputControl("First Name *", _firstNameInput, ref y);
        AddInputControl("Last Name *", _lastNameInput, ref y);
        AddInputControl("Username *", _usernameInput, ref y);

        if (_isEdit)
        {
            _usernameInput.Enabled = false;
        }
        else
        {
            AddInputControl("Password *", _passwordInput, ref y);
            AddInputControl("Confirm Password *", _confirmPasswordInput, ref y);
        }

        AddLabel("Role *", ref y);
        _roleComboBox.Location = new Point(24, y);
        _roleComboBox.Font = FontHelper.Regular(10F);
        Controls.Add(_roleComboBox);
        y += 48;

        _isActiveCheckBox.Location = new Point(24, y);
        _isActiveCheckBox.Font = FontHelper.SemiBold(9F);
        Controls.Add(_isActiveCheckBox);
        y += 40;

        Button saveButton = ControlFactory.CreatePrimaryButton(_isEdit ? "Update User" : "Create User", 180, 40);
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
        AddLabel(label, ref y);
        input.Location = new Point(24, y);
        Controls.Add(input);
        y += 48;
    }

    private void AddLabel(string text, ref int y)
    {
        Label lbl = ControlFactory.CreateInputLabel(text);
        lbl.Location = new Point(24, y);
        Controls.Add(lbl);
        y += 24;
    }

    private async void LoadRolesAndUserData()
    {
        try
        {
            var roles = await _roleService.GetAllRolesAsync();
            _roles.AddRange(roles.Where(r => r.IsActive));
            _roleComboBox.Items.AddRange(_roles.Select(r => r.RoleName).ToArray());

            if (_isEdit && _targetUserId.HasValue)
            {
                User? user = await _userService.GetUserByIdAsync(_targetUserId.Value);
                if (user != null)
                {
                    _firstNameInput.Text = user.FirstName;
                    _lastNameInput.Text = user.LastName;
                    _usernameInput.Text = user.Username;
                    _isActiveCheckBox.Checked = user.IsActive;
                    
                    var role = _roles.FirstOrDefault(r => r.RoleId == user.RoleId);
                    if (role != null) _roleComboBox.SelectedItem = role.RoleName;

                    if (user.IsOwner)
                    {
                        _roleComboBox.Enabled = false;
                        _isActiveCheckBox.Enabled = false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBoxHelper.ShowError($"Failed to load data: {ex.Message}");
        }
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_roleComboBox.SelectedIndex < 0)
            {
                MessageBoxHelper.ShowWarning("Please select a role.");
                return;
            }

            int selectedRoleId = _roles[_roleComboBox.SelectedIndex].RoleId;

            if (_isEdit && _targetUserId.HasValue)
            {
                await _userService.UpdateUserAsync(new UpdateUserRequest
                {
                    UserId = _targetUserId.Value,
                    FirstName = _firstNameInput.Text,
                    LastName = _lastNameInput.Text,
                    RoleId = selectedRoleId,
                    IsActive = _isActiveCheckBox.Checked,
                    Email = null,
                    PhoneNumber = null
                }, _currentUserId);

                MessageBoxHelper.ShowSuccess("User updated successfully.");
            }
            else
            {
                if (_passwordInput.Text != _confirmPasswordInput.Text)
                {
                    MessageBoxHelper.ShowWarning("Passwords do not match.");
                    return;
                }

                await _userService.CreateUserAsync(new CreateUserRequest
                {
                    Username = _usernameInput.Text,
                    Password = _passwordInput.Text,
                    FirstName = _firstNameInput.Text,
                    LastName = _lastNameInput.Text,
                    RoleId = selectedRoleId,
                    IsActive = _isActiveCheckBox.Checked,
                    Email = null,
                    PhoneNumber = null
                }, _currentUserId);

                MessageBoxHelper.ShowSuccess("User created successfully.");
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBoxHelper.ShowWarning(ex.Message);
        }
    }
}
