using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.Forms.ManageSystem;

public sealed class UserDetailsForm : Form
{
    private const int InputWidth = 300;
    private const int InputHeight = 30;

    private readonly UserService _userService = new();
    private readonly RoleService _roleService = new();
    private readonly int _currentUserId;
    private readonly int? _targetUserId;
    private readonly bool _isEdit;
    private readonly bool _isViewOnly;

    private readonly TextBox _firstNameInput = ControlFactory.CreateTextBox(InputWidth);
    private readonly TextBox _lastNameInput = ControlFactory.CreateTextBox(InputWidth);
    private readonly TextBox _usernameInput = ControlFactory.CreateTextBox(InputWidth);
    private readonly TextBox _passwordInput = ControlFactory.CreatePasswordTextBox(InputWidth);
    private readonly TextBox _confirmPasswordInput = ControlFactory.CreatePasswordTextBox(InputWidth);
    private readonly ComboBox _roleComboBox = CreateComboBox(InputWidth);
    private readonly CheckBox _isActiveCheckBox = new()
    {
        Text = "User is active and can log in",
        AutoSize = true,
        Checked = true,
        Font = FontHelper.Regular(9.5F),
        ForeColor = ThemeHelper.TextPrimary
    };

    private readonly List<Role> _roles = [];
    private Button? _saveButton;
    private Label? _protectedNote;

    public UserDetailsForm(int currentUserId, int? targetUserId = null, bool isViewOnly = false)
    {
        _currentUserId = currentUserId;
        _targetUserId = targetUserId;
        _isEdit = targetUserId.HasValue;
        _isViewOnly = isViewOnly;

        InitializeComponent();
        LoadRolesAndUserData();
    }

    private void InitializeComponent()
    {
        Text = _isViewOnly ? "View User" : _isEdit ? "Edit User" : "Add User";
        ThemeHelper.ApplyCompactDialogFormSettings(this);
        ClientSize = new Size(760, _isEdit || _isViewOnly ? 430 : 535);

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = ThemeHelper.ContentBackground,
            Padding = new Padding(24, 20, 24, 18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));

        root.Controls.Add(CreateHeader(), 0, 0);

        Panel content = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        GroupBox accountGroup = CreateGroupBox("Account Information", _isEdit || _isViewOnly ? 220 : 220);
        accountGroup.Location = new Point(0, 0);
        accountGroup.Width = 706;
        AddLabeledControl(accountGroup, "First Name *", _firstNameInput, 24, 34);
        AddLabeledControl(accountGroup, "Last Name *", _lastNameInput, 372, 34);
        AddLabeledControl(accountGroup, "Username *", _usernameInput, 24, 102);
        AddLabeledControl(accountGroup, "Role *", _roleComboBox, 372, 102);
        _isActiveCheckBox.Location = new Point(24, 174);
        accountGroup.Controls.Add(_isActiveCheckBox);

        _protectedNote = new Label
        {
            Text = "This protected owner account cannot be reassigned or deactivated.",
            AutoSize = false,
            Location = new Point(372, 168),
            Size = new Size(300, 40),
            Font = FontHelper.Regular(9F),
            ForeColor = ThemeHelper.TextSecondary,
            Visible = false
        };
        accountGroup.Controls.Add(_protectedNote);
        content.Controls.Add(accountGroup);

        GroupBox securityGroup = CreateGroupBox("Security", _isEdit || _isViewOnly ? 86 : 156);
        securityGroup.Location = new Point(0, accountGroup.Bottom + 14);
        securityGroup.Width = 706;
        if (_isEdit || _isViewOnly)
        {
            Label note = new()
            {
                Text = "Use the Change Password action to update this account password.",
                AutoSize = false,
                Location = new Point(24, 36),
                Size = new Size(620, 28),
                Font = FontHelper.Regular(9.5F),
                ForeColor = ThemeHelper.TextSecondary
            };
            securityGroup.Controls.Add(note);
        }
        else
        {
            AddLabeledControl(securityGroup, "Password *", _passwordInput, 24, 34);
            AddLabeledControl(securityGroup, "Confirm Password *", _confirmPasswordInput, 372, 34);
            Label helper = new()
            {
                Text = "Minimum 8 characters.",
                AutoSize = false,
                Location = new Point(24, 104),
                Size = new Size(300, 22),
                Font = FontHelper.Regular(9F),
                ForeColor = ThemeHelper.TextSecondary
            };
            securityGroup.Controls.Add(helper);
        }
        content.Controls.Add(securityGroup);
        root.Controls.Add(content, 0, 1);

        Panel footer = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        Button cancelButton = ControlFactory.CreateSecondaryButton(_isViewOnly ? "Close" : "Cancel", 110, 38);
        cancelButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        cancelButton.Location = new Point(_isViewOnly ? 572 : 440, 14);
        cancelButton.Click += (_, _) => Close();
        footer.Controls.Add(cancelButton);

        if (!_isViewOnly)
        {
            _saveButton = ControlFactory.CreatePrimaryButton(_isEdit ? "Save User" : "Save User", 132, 38);
            _saveButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            _saveButton.Location = new Point(566, 14);
            _saveButton.Click += SaveButton_Click;
            footer.Controls.Add(_saveButton);
        }
        root.Controls.Add(footer, 0, 2);

        Controls.Add(root);
    }

    private Panel CreateHeader()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        panel.Controls.Add(new Label
        {
            Text = _isViewOnly ? "View User" : _isEdit ? "Edit User" : "Add User",
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(360, 30),
            Font = FontHelper.Title(16F),
            ForeColor = ThemeHelper.TextPrimary
        });
        panel.Controls.Add(new Label
        {
            Text = "Manage account details, role assignment, and login status.",
            AutoSize = false,
            Location = new Point(1, 30),
            Size = new Size(560, 20),
            Font = FontHelper.Regular(9.5F),
            ForeColor = ThemeHelper.TextSecondary
        });
        return panel;
    }

    private static GroupBox CreateGroupBox(string text, int height) => new()
    {
        Text = text,
        Height = height,
        Font = FontHelper.SemiBold(10F),
        ForeColor = ThemeHelper.TextPrimary,
        BackColor = ThemeHelper.Surface
    };

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

    private static ComboBox CreateComboBox(int width) => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = width,
        Height = InputHeight,
        Font = FontHelper.Regular(10F),
        ForeColor = ThemeHelper.TextPrimary
    };

    private async void LoadRolesAndUserData()
    {
        try
        {
            IReadOnlyList<Role> roles = await _roleService.GetAllRolesAsync();
            _roles.Clear();
            _roles.AddRange(roles.Where(role => role.IsActive && !role.IsArchived));
            _roleComboBox.Items.Clear();
            _roleComboBox.Items.AddRange(_roles.Select(role => role.RoleName).ToArray());

            if (_isEdit && _targetUserId.HasValue)
            {
                User? user = await _userService.GetUserByIdAsync(_targetUserId.Value);
                if (user != null)
                {
                    _firstNameInput.Text = user.FirstName;
                    _lastNameInput.Text = user.LastName;
                    _usernameInput.Text = user.Username;
                    _isActiveCheckBox.Checked = user.IsActive;

                    Role? role = _roles.FirstOrDefault(role => role.RoleId == user.RoleId);
                    if (role != null) _roleComboBox.SelectedItem = role.RoleName;

                    if (user.IsOwner)
                    {
                        _roleComboBox.Enabled = false;
                        _isActiveCheckBox.Enabled = false;
                        _protectedNote!.Visible = true;
                    }
                }
            }

            if (!_isEdit && _roleComboBox.Items.Count > 0) _roleComboBox.SelectedIndex = 0;
            if (_isViewOnly) SetReadOnly();
            if (_isEdit) _usernameInput.Enabled = false;
        }
        catch (Exception ex)
        {
            MessageBoxHelper.ShowError($"Failed to load user details.\n\n{ex.Message}", "Manage System");
        }
    }

    private void SetReadOnly()
    {
        _firstNameInput.ReadOnly = true;
        _lastNameInput.ReadOnly = true;
        _usernameInput.ReadOnly = true;
        _passwordInput.ReadOnly = true;
        _confirmPasswordInput.ReadOnly = true;
        _roleComboBox.Enabled = false;
        _isActiveCheckBox.Enabled = false;
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
            MessageBoxHelper.ShowWarning(ex.Message, "Manage System");
        }
    }
}
