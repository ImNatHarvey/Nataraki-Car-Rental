using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.Forms.ManageSystem;

public sealed class RoleDetailsForm : Form
{
    private const int InputWidth = 320;
    private const int InputHeight = 30;

    private readonly RoleService _roleService = new();
    private readonly int _currentUserId;
    private readonly int? _targetRoleId;
    private readonly bool _isEdit;

    private readonly TextBox _roleNameInput = ControlFactory.CreateTextBox(InputWidth);
    private readonly TextBox _descriptionInput = ControlFactory.CreateTextBox(680);
    private readonly CheckBox _isActiveCheckBox = new()
    {
        Text = "Role is active",
        AutoSize = true,
        Checked = true,
        Font = FontHelper.Regular(9.5F),
        ForeColor = ThemeHelper.TextPrimary
    };

    private readonly Panel _permissionsPanel = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = ThemeHelper.Surface };
    private readonly Dictionary<string, List<CheckBox>> _moduleCheckBoxes = [];
    private Label? _protectedNote;
    private bool _isProtectedRole;

    public RoleDetailsForm(int currentUserId, int? targetRoleId = null)
    {
        _currentUserId = currentUserId;
        _targetRoleId = targetRoleId;
        _isEdit = targetRoleId.HasValue;

        InitializeComponent();
        LoadPermissionsAndRoleData();
    }

    private void InitializeComponent()
    {
        Text = _isEdit ? "Edit Role / Permissions" : "Add Role";
        ThemeHelper.ApplyCompactDialogFormSettings(this);
        ClientSize = new Size(880, 735);

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = ThemeHelper.ContentBackground,
            Padding = new Padding(24, 20, 24, 18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 182F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));

        root.Controls.Add(CreateHeader(), 0, 0);
        root.Controls.Add(CreateRoleInformationGroup(), 0, 1);
        root.Controls.Add(CreatePermissionsGroup(), 0, 2);
        root.Controls.Add(CreateFooter(), 0, 3);
        Controls.Add(root);
    }

    private Panel CreateHeader()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        panel.Controls.Add(new Label
        {
            Text = _isEdit ? "Edit Role / Permissions" : "Add Role",
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(360, 30),
            Font = FontHelper.Title(16F),
            ForeColor = ThemeHelper.TextPrimary
        });
        panel.Controls.Add(new Label
        {
            Text = "Manage role identity and module permissions.",
            AutoSize = false,
            Location = new Point(1, 30),
            Size = new Size(500, 22),
            Font = FontHelper.Regular(9.5F),
            ForeColor = ThemeHelper.TextSecondary
        });
        return panel;
    }

    private GroupBox CreateRoleInformationGroup()
    {
        GroupBox group = CreateGroupBox("Role Information");
        AddLabeledControl(group, "Role Name *", _roleNameInput, 24, 34, InputWidth);
        AddLabeledControl(group, "Description", _descriptionInput, 24, 102, 680);
        _isActiveCheckBox.Location = new Point(380, 58);
        group.Controls.Add(_isActiveCheckBox);

        _protectedNote = new Label
        {
            Text = "This is a protected system role.",
            AutoSize = false,
            Location = new Point(380, 92),
            Size = new Size(360, 24),
            Font = FontHelper.SemiBold(9F),
            ForeColor = ThemeHelper.TextSecondary,
            Visible = false
        };
        group.Controls.Add(_protectedNote);
        return group;
    }

    private GroupBox CreatePermissionsGroup()
    {
        GroupBox group = CreateGroupBox("Permissions");
        group.Padding = new Padding(18, 46, 18, 18);

        Button selectAllButton = ControlFactory.CreateSecondaryButton("Select All", 104, 30);
        selectAllButton.Location = new Point(24, 24);
        selectAllButton.Click += (_, _) => SetAllPermissions(true);
        Button clearAllButton = ControlFactory.CreateSecondaryButton("Clear All", 104, 30);
        clearAllButton.Location = new Point(138, 24);
        clearAllButton.Click += (_, _) => SetAllPermissions(false);
        group.Controls.Add(selectAllButton);
        group.Controls.Add(clearAllButton);

        _permissionsPanel.Location = new Point(18, 62);
        _permissionsPanel.Size = new Size(792, 330);
        _permissionsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        group.Controls.Add(_permissionsPanel);
        return group;
    }

    private Panel CreateFooter()
    {
        Panel footer = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        Button cancelButton = ControlFactory.CreateSecondaryButton("Cancel", 110, 38);
        cancelButton.Location = new Point(586, 14);
        cancelButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        cancelButton.Click += (_, _) => Close();
        Button saveButton = ControlFactory.CreatePrimaryButton("Save Role", 132, 38);
        saveButton.Location = new Point(710, 14);
        saveButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        saveButton.Click += SaveButton_Click;
        footer.Controls.Add(cancelButton);
        footer.Controls.Add(saveButton);
        return footer;
    }

    private static GroupBox CreateGroupBox(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Font = FontHelper.SemiBold(10F),
        ForeColor = ThemeHelper.TextPrimary,
        BackColor = ThemeHelper.Surface
    };

    private static void AddLabeledControl(Control parent, string labelText, Control input, int x, int y, int width)
    {
        Label label = ControlFactory.CreateInputLabel(labelText);
        label.Location = new Point(x, y);
        input.Location = new Point(x, y + 23);
        input.Size = new Size(width, InputHeight);
        input.Font = FontHelper.Regular(10F);
        parent.Controls.Add(label);
        parent.Controls.Add(input);
    }

    private async void LoadPermissionsAndRoleData()
    {
        try
        {
            IReadOnlyList<Permission> allPermissions = await _roleService.GetAllPermissionsAsync();
            RenderPermissions(allPermissions);

            if (_isEdit && _targetRoleId.HasValue)
            {
                RoleWithPermissions? roleWithPerms = await _roleService.GetRoleWithPermissionsAsync(_targetRoleId.Value);
                if (roleWithPerms != null)
                {
                    _roleNameInput.Text = roleWithPerms.RoleName;
                    _descriptionInput.Text = roleWithPerms.Description;
                    _isActiveCheckBox.Checked = roleWithPerms.IsActive;
                    _isProtectedRole = roleWithPerms.RoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase);

                    foreach (CheckBox checkBox in _moduleCheckBoxes.Values.SelectMany(items => items))
                    {
                        checkBox.Checked = roleWithPerms.PermissionKeys.Contains(checkBox.Tag?.ToString() ?? "");
                    }

                    if (_isProtectedRole)
                    {
                        _roleNameInput.Enabled = false;
                        _isActiveCheckBox.Enabled = false;
                        _permissionsPanel.Enabled = false;
                        _protectedNote!.Visible = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBoxHelper.ShowError($"Failed to load role details.\n\n{ex.Message}", "Manage System");
        }
    }

    private void RenderPermissions(IReadOnlyList<Permission> allPermissions)
    {
        _permissionsPanel.Controls.Clear();
        _moduleCheckBoxes.Clear();

        int y = 8;
        foreach (IGrouping<string, Permission> group in allPermissions.GroupBy(permission => permission.ModuleName).OrderBy(group => group.Key))
        {
            Label moduleLabel = new()
            {
                Text = group.Key,
                Location = new Point(10, y),
                Size = new Size(720, 24),
                Font = FontHelper.SemiBold(10F),
                ForeColor = ThemeHelper.Primary
            };
            _permissionsPanel.Controls.Add(moduleLabel);
            y += 28;

            List<CheckBox> checkBoxes = [];
            int index = 0;
            foreach (Permission permission in group.OrderBy(permission => permission.PermissionName))
            {
                int column = index % 2;
                int row = index / 2;
                CheckBox checkBox = new()
                {
                    Text = permission.PermissionName,
                    Tag = permission.PermissionKey,
                    AutoSize = false,
                    Location = new Point(18 + (column * 360), y + (row * 30)),
                    Size = new Size(330, 24),
                    Font = FontHelper.Regular(9F),
                    ForeColor = ThemeHelper.TextPrimary
                };
                if (!string.IsNullOrWhiteSpace(permission.Description))
                    checkBox.ToolTipText(permission.Description);
                checkBoxes.Add(checkBox);
                _permissionsPanel.Controls.Add(checkBox);
                index++;
            }

            _moduleCheckBoxes[group.Key] = checkBoxes;
            y += Math.Max(1, (int)Math.Ceiling(index / 2D)) * 30 + 14;
        }
    }

    private void SetAllPermissions(bool isChecked)
    {
        if (_isProtectedRole) return;
        foreach (CheckBox checkBox in _moduleCheckBoxes.Values.SelectMany(items => items))
            checkBox.Checked = isChecked;
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            List<string> selectedKeys = _moduleCheckBoxes.Values
                .SelectMany(items => items)
                .Where(checkBox => checkBox.Checked)
                .Select(checkBox => checkBox.Tag?.ToString() ?? string.Empty)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToList();

            RoleWithPermissions request = new()
            {
                RoleId = _targetRoleId ?? 0,
                RoleName = _roleNameInput.Text,
                Description = _descriptionInput.Text,
                IsActive = _isActiveCheckBox.Checked,
                PermissionKeys = selectedKeys
            };

            if (_isEdit)
            {
                await _roleService.UpdateRoleAsync(request, _currentUserId);
                MessageBoxHelper.ShowSuccess("Role updated successfully.");
            }
            else
            {
                await _roleService.CreateRoleAsync(request, _currentUserId);
                MessageBoxHelper.ShowSuccess("Role created successfully.");
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

internal static class CheckBoxToolTipExtensions
{
    private static readonly ToolTip ToolTip = new();

    public static void ToolTipText(this CheckBox checkBox, string text)
    {
        ToolTip.SetToolTip(checkBox, text);
    }
}
