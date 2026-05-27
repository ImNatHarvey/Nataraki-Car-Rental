using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Common;

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
    private readonly TextBox _securityQuestionInput = ControlFactory.CreateTextBox(InputWidth);
    private readonly TextBox _securityAnswerInput = ControlFactory.CreateTextBox(InputWidth);
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
    private bool _loadedUserIsOwner;

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
        ClientSize = new Size(760, _isViewOnly ? 430 : 600);

        Panel root = new()
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.ContentBackground,
            Padding = new Padding(24, 20, 24, 0)
        };

        Panel header = CreateHeader();
        header.Dock = DockStyle.Top;
        header.Height = 52;

        Panel content = new()
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.ContentBackground,
            AutoScroll = true,
            Padding = new Padding(0, 14, 0, 14)
        };

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

        if (!_isViewOnly)
        {
            GroupBox securityGroup = CreateGroupBox("Security", 280);
            securityGroup.Location = new Point(0, accountGroup.Bottom + 14);
            securityGroup.Width = 706;
            if (_isEdit)
            {
                AddLabeledPasswordControl(securityGroup, "New Password", _passwordInput, 24, 34);
                AddLabeledPasswordControl(securityGroup, "Confirm Password", _confirmPasswordInput, 372, 34);
                Label helper = new()
                {
                    Text = "Leave blank to keep the current password. Minimum 8 characters.",
                    AutoSize = false,
                    Location = new Point(24, 102),
                    Size = new Size(540, 24),
                    Font = FontHelper.Regular(9F),
                    ForeColor = ThemeHelper.TextSecondary
                };
                securityGroup.Controls.Add(helper);

                AddLabeledControl(securityGroup, "Security Question *", _securityQuestionInput, 24, 140);
                AddLabeledControl(securityGroup, "Security Answer *", _securityAnswerInput, 372, 140);
                Label questionHelper = new()
                {
                    Text = "Used for account recovery via Forgot Password.",
                    AutoSize = false,
                    Location = new Point(24, 208),
                    Size = new Size(540, 24),
                    Font = FontHelper.Regular(9F),
                    ForeColor = ThemeHelper.TextSecondary
                };
                securityGroup.Controls.Add(questionHelper);
            }
            else
            {
                AddLabeledPasswordControl(securityGroup, "Password *", _passwordInput, 24, 34);
                AddLabeledPasswordControl(securityGroup, "Confirm Password *", _confirmPasswordInput, 372, 34);
                
                AddLabeledControl(securityGroup, "Security Question *", _securityQuestionInput, 24, 102);
                AddLabeledControl(securityGroup, "Security Answer *", _securityAnswerInput, 372, 102);
                
                Label helper = new()
                {
                    Text = "Minimum 8 characters. Security fields are required for recovery.",
                    AutoSize = false,
                    Location = new Point(24, 170),
                    Size = new Size(540, 24),
                    Font = FontHelper.Regular(9F),
                    ForeColor = ThemeHelper.TextSecondary
                };
                securityGroup.Controls.Add(helper);
            }
            content.Controls.Add(securityGroup);
        }

        Panel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 72,
            BackColor = ThemeHelper.ContentBackground,
            Padding = new Padding(0, 14, 0, 18)
        };
        
        ClientSize = new Size(760, _isViewOnly ? 430 : 700);

        Button cancelButton = ControlFactory.CreateSecondaryButton(_isViewOnly ? "Close" : "Cancel", 110, 38);
        cancelButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        footer.Controls.Add(cancelButton);
        CancelButton = cancelButton;

        if (!_isViewOnly)
        {
            _saveButton = ControlFactory.CreatePrimaryButton(_isEdit ? "Save User" : "Add User", 142, 38);
            _saveButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _saveButton.Click += SaveButton_Click;
            footer.Controls.Add(_saveButton);
            AcceptButton = _saveButton;
        }
        else
        {
            AcceptButton = cancelButton;
        }

        footer.Resize += (_, _) => LayoutFooterButtons(footer, cancelButton, _saveButton);
        LayoutFooterButtons(footer, cancelButton, _saveButton);

        root.Controls.Add(content);
        root.Controls.Add(footer);
        root.Controls.Add(header);
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

    private static void AddLabeledPasswordControl(Control parent, string labelText, TextBox input, int x, int y)
    {
        Label label = ControlFactory.CreateInputLabel(labelText);
        label.Location = new Point(x, y);
        BorderedPanel fieldPanel = CreatePasswordFieldPanel(input, InputWidth);
        fieldPanel.Location = new Point(x, y + 23);
        parent.Controls.Add(label);
        parent.Controls.Add(fieldPanel);
    }

    private static BorderedPanel CreatePasswordFieldPanel(TextBox input, int width)
    {
        IconButton previewButton = new();
        BorderedPanel panel = new()
        {
            Size = new Size(width, InputHeight),
            BackColor = ThemeHelper.Surface,
            BorderColor = ThemeHelper.Border,
            Cursor = Cursors.IBeam
        };
        panel.Click += (_, _) => input.Focus();

        input.BorderStyle = BorderStyle.None;
        input.BackColor = ThemeHelper.Surface;
        input.Location = new Point(8, 6);
        input.Size = new Size(width - 48, InputHeight - 4);
        input.Font = FontHelper.Regular(10F);
        input.Cursor = Cursors.IBeam;

        previewButton.Size = new Size(34, InputHeight - 2);
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

    private static ComboBox CreateComboBox(int width) => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = width,
        Height = InputHeight,
        Font = FontHelper.Regular(10F),
        ForeColor = ThemeHelper.TextPrimary
    };

    private static void LayoutFooterButtons(Panel footer, Button cancelButton, Button? saveButton)
    {
        int y = 14;
        int right = footer.ClientSize.Width;
        if (saveButton is not null)
        {
            saveButton.Location = new Point(Math.Max(0, right - saveButton.Width), y);
            cancelButton.Location = new Point(Math.Max(0, saveButton.Left - 12 - cancelButton.Width), y);
            return;
        }

        cancelButton.Location = new Point(Math.Max(0, right - cancelButton.Width), y);
    }

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
                    _securityQuestionInput.Text = user.SecurityQuestion;
                    _securityAnswerInput.Text = user.SecurityAnswer;
                    _isActiveCheckBox.Checked = user.IsActive;
                    _loadedUserIsOwner = user.IsOwner;

                    Role? role = _roles.FirstOrDefault(role => role.RoleId == user.RoleId);
                    if (role != null) _roleComboBox.SelectedItem = role.RoleName;

                    if (user.IsOwner)
                    {
                        // Allow Owner to edit their own username in Edit mode
                        _usernameInput.Enabled = true;
                        _roleComboBox.Enabled = false;
                        _isActiveCheckBox.Enabled = false;
                        _protectedNote!.Visible = true;
                    }
                }
            }

            if (!_isEdit && _roleComboBox.Items.Count > 0) _roleComboBox.SelectedIndex = 0;
            if (_isViewOnly) SetReadOnly();
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
        _securityQuestionInput.ReadOnly = true;
        _securityAnswerInput.ReadOnly = true;
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
                    Username = _usernameInput.Text,
                    FirstName = _firstNameInput.Text,
                    LastName = _lastNameInput.Text,
                    RoleId = selectedRoleId,
                    IsActive = _isActiveCheckBox.Checked,
                    SecurityQuestion = _securityQuestionInput.Text,
                    SecurityAnswer = _securityAnswerInput.Text,
                    Email = null,
                    PhoneNumber = null
                }, _currentUserId);

                await SaveOptionalPasswordAsync(_targetUserId.Value);
                MessageBoxHelper.ShowSuccess(_loadedUserIsOwner
                    ? "Owner account updated successfully."
                    : "User updated successfully.");
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
                    SecurityQuestion = _securityQuestionInput.Text,
                    SecurityAnswer = _securityAnswerInput.Text,
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

    private async Task SaveOptionalPasswordAsync(int userId)
    {
        bool hasNewPassword = !string.IsNullOrWhiteSpace(_passwordInput.Text);
        bool hasConfirmPassword = !string.IsNullOrWhiteSpace(_confirmPasswordInput.Text);
        if (!hasNewPassword && !hasConfirmPassword) return;

        if (!hasNewPassword || !hasConfirmPassword)
        {
            throw new InvalidOperationException("Enter both password fields or leave both blank.");
        }

        if (_passwordInput.Text != _confirmPasswordInput.Text)
        {
            throw new InvalidOperationException("Passwords do not match.");
        }

        if (_passwordInput.Text.Length < 8)
        {
            throw new InvalidOperationException("Password must be at least 8 characters.");
        }

        await _userService.ChangePasswordAsync(new ChangePasswordRequest
        {
            UserId = userId,
            NewPassword = _passwordInput.Text
        }, _currentUserId);
    }
}
