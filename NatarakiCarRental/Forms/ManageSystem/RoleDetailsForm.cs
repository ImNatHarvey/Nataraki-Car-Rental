using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.Forms.ManageSystem;

public sealed class RoleDetailsForm : Form
{
    private readonly RoleService _roleService = new();
    private readonly int _currentUserId;
    private readonly int? _targetRoleId;
    private readonly bool _isEdit;

    private readonly TextBox _roleNameInput = ControlFactory.CreateTextBox(400);
    private readonly TextBox _descriptionInput = ControlFactory.CreateTextBox(400);
    private readonly CheckBox _isActiveCheckBox = new() { Text = "Role is active", AutoSize = true, Checked = true };
    
    private readonly FlowLayoutPanel _permissionsPanel = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private readonly Dictionary<string, List<CheckBox>> _moduleCheckBoxes = [];

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
        Text = _isEdit ? "Edit Role & Permissions" : "Add New Role";
        ThemeHelper.ApplyCompactDialogFormSettings(this);
        ClientSize = new Size(800, 700);

        Panel topPanel = new() { Dock = DockStyle.Top, Height = 180, Padding = new Padding(24) };
        
        int y = 24;
        AddLabel(topPanel, "Role Name *", ref y);
        _roleNameInput.Location = new Point(24, y);
        topPanel.Controls.Add(_roleNameInput);
        y += 48;

        AddLabel(topPanel, "Description", ref y);
        _descriptionInput.Location = new Point(24, y);
        topPanel.Controls.Add(_descriptionInput);
        y += 48;

        _isActiveCheckBox.Location = new Point(24, y);
        _isActiveCheckBox.Font = FontHelper.SemiBold(9F);
        topPanel.Controls.Add(_isActiveCheckBox);

        Panel centerPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(24, 0, 24, 24) };
        Label pLabel = ControlFactory.CreateInputLabel("Permissions");
        pLabel.Dock = DockStyle.Top;
        pLabel.Height = 30;
        centerPanel.Controls.Add(_permissionsPanel);
        centerPanel.Controls.Add(pLabel);

        Panel bottomPanel = new() { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(24) };
        Button saveButton = ControlFactory.CreatePrimaryButton(_isEdit ? "Update Role" : "Create Role", 180, 40);
        saveButton.Location = new Point(24, 20);
        saveButton.Click += SaveButton_Click;

        Button cancelButton = ControlFactory.CreateSecondaryButton("Cancel", 100, 40);
        cancelButton.Location = new Point(214, 20);
        cancelButton.Click += (_, _) => Close();

        bottomPanel.Controls.Add(saveButton);
        bottomPanel.Controls.Add(cancelButton);

        Controls.Add(centerPanel);
        Controls.Add(topPanel);
        Controls.Add(bottomPanel);
    }

    private void AddLabel(Control parent, string text, ref int y)
    {
        Label lbl = ControlFactory.CreateInputLabel(text);
        lbl.Location = new Point(24, y);
        parent.Controls.Add(lbl);
        y += 24;
    }

    private async void LoadPermissionsAndRoleData()
    {
        try
        {
            var allPermissions = await _roleService.GetAllPermissionsAsync();
            var grouped = allPermissions.GroupBy(p => p.ModuleName);

            foreach (var group in grouped)
            {
                Label modLabel = new() { Text = group.Key, Font = FontHelper.SemiBold(10.5F), ForeColor = ThemeHelper.Primary, AutoSize = true, Margin = new Padding(0, 16, 0, 8) };
                _permissionsPanel.Controls.Add(modLabel);

                FlowLayoutPanel inner = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Width = 720, Padding = new Padding(12, 0, 0, 0) };
                List<CheckBox> cbs = [];

                foreach (var p in group)
                {
                    CheckBox cb = new() { Text = p.PermissionName, Tag = p.PermissionKey, AutoSize = true, Font = FontHelper.Regular(9F), Margin = new Padding(0, 0, 24, 8) };
                    cbs.Add(cb);
                    inner.Controls.Add(cb);
                }

                _moduleCheckBoxes[group.Key] = cbs;
                _permissionsPanel.Controls.Add(inner);
            }

            if (_isEdit && _targetRoleId.HasValue)
            {
                var roleWithPerms = await _roleService.GetRoleWithPermissionsAsync(_targetRoleId.Value);
                if (roleWithPerms != null)
                {
                    _roleNameInput.Text = roleWithPerms.RoleName;
                    _descriptionInput.Text = roleWithPerms.Description;
                    _isActiveCheckBox.Checked = roleWithPerms.IsActive;

                    if (roleWithPerms.RoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase))
                    {
                        _roleNameInput.Enabled = false;
                        _isActiveCheckBox.Enabled = false;
                        _permissionsPanel.Enabled = false;
                    }

                    foreach (var kvp in _moduleCheckBoxes)
                    {
                        foreach (var cb in kvp.Value)
                        {
                            cb.Checked = roleWithPerms.PermissionKeys.Contains(cb.Tag?.ToString() ?? "");
                        }
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
            List<string> selectedKeys = [];
            foreach (var kvp in _moduleCheckBoxes)
            {
                selectedKeys.AddRange(kvp.Value.Where(cb => cb.Checked).Select(cb => cb.Tag?.ToString() ?? ""));
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
            MessageBoxHelper.ShowWarning(ex.Message);
        }
    }
}
