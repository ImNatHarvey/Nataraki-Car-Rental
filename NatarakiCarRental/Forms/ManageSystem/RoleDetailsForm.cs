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
    private readonly Panel _permissionsPanel = new() { BackColor = ThemeHelper.Surface };
    private readonly Dictionary<string, List<CheckBox>> _moduleCheckBoxes = [];
    private readonly Dictionary<string, List<Permission>> _permissionsByModule = [];
    private Label? _protectedNote;
    private Button? _saveButton;
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
        ClientSize = new Size(740, _isViewOnly ? 500 : 520);

        Panel root = new()
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.ContentBackground,
            Padding = new Padding(24, 20, 24, 0)
        };

        Panel header = CreateHeader();
        header.Dock = DockStyle.Top;
        header.Height = 54;

        Panel content = new()
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.ContentBackground,
            Padding = new Padding(0, 14, 0, 14)
        };

        GroupBox roleGroup = CreateGroupBox("Role Information");
        roleGroup.Location = new Point(0, 0);
        roleGroup.Size = new Size(686, 108);
        AddLabeledControl(roleGroup, "Role Name *", _roleNameInput, 24, 34, InputWidth);

        _protectedNote = new Label
        {
            Text = "This is a protected system role.",
            AutoSize = false,
            Location = new Point(372, 57),
            Size = new Size(270, 24),
            Font = FontHelper.SemiBold(9F),
            ForeColor = ThemeHelper.TextSecondary,
            Visible = false
        };
        roleGroup.Controls.Add(_protectedNote);
        content.Controls.Add(roleGroup);

        GroupBox permissionsGroup = CreatePermissionsGroup();
        permissionsGroup.Location = new Point(0, roleGroup.Bottom + 14);
        permissionsGroup.Size = new Size(686, 244);
        content.Controls.Add(permissionsGroup);

        Panel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 72,
            BackColor = ThemeHelper.ContentBackground,
            Padding = new Padding(0, 14, 0, 18)
        };

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
            _saveButton = ControlFactory.CreatePrimaryButton(_isEdit ? "Save Role" : "Add Role", 132, 38);
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
            Text = _isViewOnly ? "View Role" : _isEdit ? "Edit Role / Permissions" : "Add Role",
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(360, 30),
            Font = FontHelper.Title(16F),
            ForeColor = ThemeHelper.TextPrimary
        });
        panel.Controls.Add(new Label
        {
            Text = "Manage role identity and module access.",
            AutoSize = false,
            Location = new Point(1, 30),
            Size = new Size(500, 22),
            Font = FontHelper.Regular(9.5F),
            ForeColor = ThemeHelper.TextSecondary
        });
        return panel;
    }

    private GroupBox CreatePermissionsGroup()
    {
        GroupBox group = CreateGroupBox("Module Access");
        _permissionsPanel.Location = new Point(22, 32);
        _permissionsPanel.Size = new Size(640, 190);
        group.Controls.Add(_permissionsPanel);
        return group;
    }

    private static GroupBox CreateGroupBox(string text) => new()
    {
        Text = text,
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
                    _isProtectedRole = roleWithPerms.RoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase);

                    foreach (KeyValuePair<string, List<CheckBox>> pair in _moduleCheckBoxes)
                    {
                        bool moduleGranted = _permissionsByModule.TryGetValue(pair.Key, out List<Permission>? permissions)
                            && permissions.Any(permission => roleWithPerms.PermissionKeys.Contains(permission.PermissionKey));
                        foreach (CheckBox checkBox in pair.Value)
                        {
                            checkBox.Checked = string.Equals(pair.Key, "Overview", StringComparison.OrdinalIgnoreCase)
                                || _isProtectedRole
                                || moduleGranted;
                        }
                    }

                    if (_isProtectedRole)
                    {
                        _roleNameInput.Enabled = false;
                        _permissionsPanel.Enabled = false;
                        _protectedNote!.Visible = true;
                    }
                }
            }

            if (_isViewOnly)
            {
                _roleNameInput.ReadOnly = true;
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
        string[] leftColumn = ["Overview", "Transactions", "Car Garage", "Activity Log", "Manage System"];
        string[] rightColumn = ["Fleet Schedule", "Customers", "Offsite", "Reports & Analytics"];
        foreach (ModulePermissionMap module in ModulePermissionMaps)
        {
            List<Permission> permissions = module.PermissionKeys
                .Where(permissionByKey.ContainsKey)
                .Select(key => permissionByKey[key])
                .ToList();
            _permissionsByModule[module.DisplayName] = permissions;
        }

        AddModuleCheckBoxes(leftColumn, 10, 8);
        AddModuleCheckBoxes(rightColumn, 330, 8);
    }

    private void AddModuleCheckBoxes(IReadOnlyList<string> moduleNames, int x, int startY)
    {
        for (int index = 0; index < moduleNames.Count; index++)
        {
            string moduleName = moduleNames[index];
            CheckBox moduleCheckBox = new()
            {
                Text = moduleName == "Overview" ? "Overview (default)" : moduleName,
                Location = new Point(x, startY + (index * 34)),
                Size = new Size(280, 28),
                Font = FontHelper.SemiBold(10F),
                ForeColor = ThemeHelper.TextPrimary,
                Tag = moduleName,
                Checked = moduleName == "Overview",
                Enabled = moduleName != "Overview"
            };
            _moduleCheckBoxes[moduleName] = [moduleCheckBox];
            _permissionsPanel.Controls.Add(moduleCheckBox);
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
                selectedKeys.Add("Overview.View");

            RoleWithPermissions request = new()
            {
                RoleId = _targetRoleId ?? 0,
                RoleName = _roleNameInput.Text,
                Description = null,
                IsActive = true,
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
