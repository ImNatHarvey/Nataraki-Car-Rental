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
    private readonly bool _isViewOnly;

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
    private readonly Dictionary<string, List<Permission>> _permissionsByModule = [];
    private Label? _protectedNote;
    private bool _isProtectedRole;

    private static readonly IReadOnlyList<ModulePermissionMap> ModulePermissionMaps =
    [
        new("Overview", ["Overview.View"]),
        new("Fleet Schedule", ["FleetSchedule.View", "FleetSchedule.Create", "FleetSchedule.Edit", "FleetSchedule.Cancel"]),
        new("Transactions", ["Transactions.View", "Transactions.Create", "Transactions.Edit", "Transactions.StartRental", "Transactions.AddPayment", "Transactions.Complete", "Transactions.Cancel", "Transactions.ArchiveRestore"]),
        new("Customers", ["Customers.View", "Customers.Create", "Customers.Edit", "Customers.Blacklist", "Customers.ArchiveRestore"]),
        new("Car Garage", ["Cars.View", "Cars.Create", "Cars.Edit", "Cars.ArchiveRestore"]),
        new("Offsite", ["Offsite.View", "Offsite.Create", "Offsite.Edit", "Offsite.Complete", "Offsite.Cancel", "Offsite.ArchiveRestore", "Offsite.MapTracking"]),
        new("Activity Log", ["ActivityLog.View"]),
        new("Reports & Analytics", ["Reports.View", "Reports.Export"]),
        new("Manage System", ["ManageSystem.View", "ManageSystem.Settings", "ManageSystem.Branding", "ManageSystem.Users", "ManageSystem.Roles"])
    ];

    public RoleDetailsForm(int currentUserId, int? targetRoleId = null, bool isViewOnly = false)
    {
        _currentUserId = currentUserId;
        _targetRoleId = targetRoleId;
        _isEdit = targetRoleId.HasValue;
        _isViewOnly = isViewOnly;

        InitializeComponent();
        LoadPermissionsAndRoleData();
    }

    private void InitializeComponent()
    {
        Text = _isViewOnly ? "View Role" : _isEdit ? "Edit Role / Permissions" : "Add Role";
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
            Text = _isViewOnly ? "View Role" : _isEdit ? "Edit Role / Permissions" : "Add Role",
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
        GroupBox group = CreateGroupBox("Module Access");
        group.Padding = new Padding(18, 46, 18, 18);

        Button selectAllButton = ControlFactory.CreateSecondaryButton("Select All", 104, 30);
        selectAllButton.Location = new Point(24, 24);
        selectAllButton.Click += (_, _) => SetAllPermissions(true);
        Button clearAllButton = ControlFactory.CreateSecondaryButton("Clear All", 104, 30);
        clearAllButton.Location = new Point(138, 24);
        clearAllButton.Click += (_, _) => SetAllPermissions(false);
        group.Controls.Add(selectAllButton);
        group.Controls.Add(clearAllButton);
        Label helper = new()
        {
            Text = "Selecting a module grants the standard actions for that module.",
            AutoSize = false,
            Location = new Point(260, 28),
            Size = new Size(420, 22),
            Font = FontHelper.Regular(9F),
            ForeColor = ThemeHelper.TextSecondary
        };
        group.Controls.Add(helper);

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
        footer.Controls.Add(cancelButton);
        if (!_isViewOnly)
        {
            Button saveButton = ControlFactory.CreatePrimaryButton("Save Role", 132, 38);
            saveButton.Location = new Point(710, 14);
            saveButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            saveButton.Click += SaveButton_Click;
            footer.Controls.Add(saveButton);
        }
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

                    foreach (KeyValuePair<string, List<CheckBox>> pair in _moduleCheckBoxes)
                    {
                        bool moduleGranted = _permissionsByModule.TryGetValue(pair.Key, out List<Permission>? permissions)
                            && permissions.Any(permission => roleWithPerms.PermissionKeys.Contains(permission.PermissionKey));
                        foreach (CheckBox checkBox in pair.Value)
                        {
                            checkBox.Checked = string.Equals(pair.Key, "Overview", StringComparison.OrdinalIgnoreCase) || moduleGranted;
                        }
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

            if (_isViewOnly)
            {
                _roleNameInput.ReadOnly = true;
                _descriptionInput.ReadOnly = true;
                _isActiveCheckBox.Enabled = false;
                _permissionsPanel.Enabled = false;
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
        _permissionsByModule.Clear();

        Dictionary<string, Permission> permissionByKey = allPermissions.ToDictionary(permission => permission.PermissionKey, StringComparer.OrdinalIgnoreCase);
        int y = 8;
        for (int index = 0; index < ModulePermissionMaps.Count; index++)
        {
            ModulePermissionMap module = ModulePermissionMaps[index];
            List<Permission> permissions = module.PermissionKeys
                .Where(permissionByKey.ContainsKey)
                .Select(key => permissionByKey[key])
                .ToList();
            _permissionsByModule[module.DisplayName] = permissions;
            int column = index % 2;
            int row = index / 2;
            CheckBox moduleCheckBox = new()
            {
                Text = module.DisplayName,
                Location = new Point(10 + (column * 370), y + (row * 38)),
                Size = new Size(330, 28),
                Font = FontHelper.SemiBold(10F),
                ForeColor = ThemeHelper.TextPrimary,
                Tag = module.DisplayName,
                Checked = module.DisplayName == "Overview",
                Enabled = module.DisplayName != "Overview"
            };
            if (module.DisplayName == "Overview")
            {
                moduleCheckBox.Text = "Overview (default)";
            }
            _moduleCheckBoxes[module.DisplayName] = [moduleCheckBox];
            _permissionsPanel.Controls.Add(moduleCheckBox);
        }
    }

    private void SetAllPermissions(bool isChecked)
    {
        if (_isProtectedRole) return;
        foreach (KeyValuePair<string, List<CheckBox>> pair in _moduleCheckBoxes)
        {
            foreach (CheckBox checkBox in pair.Value)
                checkBox.Checked = pair.Key == "Overview" || isChecked;
        }
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            List<string> selectedKeys = _moduleCheckBoxes
                .Where(pair => pair.Value.Any(checkBox => checkBox.Checked))
                .SelectMany(pair => _permissionsByModule.TryGetValue(pair.Key, out List<Permission>? permissions)
                    ? permissions.Select(permission => permission.PermissionKey)
                    : [])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!selectedKeys.Contains("Overview.View", StringComparer.OrdinalIgnoreCase))
            {
                selectedKeys.Add("Overview.View");
            }

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

    private sealed record ModulePermissionMap(string DisplayName, string[] PermissionKeys);
}

internal static class CheckBoxToolTipExtensions
{
    private static readonly ToolTip ToolTip = new();

    public static void ToolTipText(this CheckBox checkBox, string text)
    {
        ToolTip.SetToolTip(checkBox, text);
    }
}
