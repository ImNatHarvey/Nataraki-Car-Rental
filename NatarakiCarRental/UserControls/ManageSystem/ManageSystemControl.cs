using FontAwesome.Sharp;
using NatarakiCarRental.Forms.ManageSystem;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using System.Data;

namespace NatarakiCarRental.UserControls.ManageSystem;

public sealed class ManageSystemControl : UserControl
{
    private readonly int _currentUserId;
    private readonly SystemSettingsService _service = new();
    private readonly UserService _userService = new();
    private readonly RoleService _roleService = new();
    private readonly TabControl _tabs = new();

    private readonly TextBox _businessNameInput = new();
    private readonly TextBox _contactNumberInput = new();
    private readonly TextBox _emailInput = new();
    private readonly TextBox _addressInput = new();

    private readonly PictureBox _iconPreview = new() { SizeMode = PictureBoxSizeMode.Zoom, Width = 64, Height = 64 };
    private string _currentIconPath = "";

    private readonly PictureBox _posterPreview = new() { SizeMode = PictureBoxSizeMode.Zoom, Width = 120, Height = 180 };
    private string _currentPosterPath = "";
    private readonly CheckBox _useCustomPosterToggle = new() { Text = "Use custom login poster", AutoSize = true };

    private readonly ComboBox _themeDropdown = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Dictionary<string, string> _themeColors = new()
    {
        { "Blue (Default)", "#2563EB" },
        { "Purple", "#7C3AED" },
        { "Green", "#16A34A" },
        { "Red", "#DC2626" },
        { "Orange", "#EA580C" },
        { "Dark", "#111827" }
    };

    // Users Tab Controls
    private readonly DataGridView _usersGrid = CreateGrid();
    private readonly TextBox _userSearchInput = ControlFactory.CreateTextBox(200);
    private readonly ComboBox _roleFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly ComboBox _statusFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private int _userPageSize = 13;

    // Roles Tab Controls
    private readonly DataGridView _rolesGrid = CreateGrid();

    public ManageSystemControl(int currentUserId)
    {
        _currentUserId = currentUserId;
        Dock = DockStyle.Fill;
        BackColor = ThemeHelper.ContentBackground;
        Padding = new Padding(32);
        InitializeLayout();
        LoadSettings();
        
        _ = LoadUsersAsync();
        _ = LoadRolesAsync();
    }

    private void InitializeLayout()
    {
        Label titleLabel = new()
        {
            Text = "Manage System",
            Dock = DockStyle.Top,
            Height = 48,
            Font = FontHelper.Title(20F),
            ForeColor = ThemeHelper.TextPrimary
        };

        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = FontHelper.SemiBold(10F);
        
        TabPage settingsTab = new() { Text = "System Settings", BackColor = ThemeHelper.ContentBackground, Padding = new Padding(16) };
        if (AccessControlService.HasPermission("ManageSystem.Settings"))
            settingsTab.Controls.Add(CreateSystemSettingsPanel());
        else
            settingsTab.Controls.Add(CreatePlaceholder("You do not have permission to edit system settings."));

        TabPage brandingTab = new() { Text = "Branding & Theme", BackColor = ThemeHelper.ContentBackground, Padding = new Padding(16) };
        if (AccessControlService.HasPermission("ManageSystem.Branding"))
            brandingTab.Controls.Add(CreateBrandingPanel());
        else
            brandingTab.Controls.Add(CreatePlaceholder("You do not have permission to edit branding settings."));

        TabPage usersTab = new() { Text = "Users", BackColor = ThemeHelper.ContentBackground, Padding = new Padding(16) };
        if (AccessControlService.HasPermission("ManageSystem.Users"))
            usersTab.Controls.Add(CreateUsersPanel());
        else
            usersTab.Controls.Add(CreatePlaceholder("You do not have permission to manage users."));

        TabPage rolesTab = new() { Text = "Roles & Permissions", BackColor = ThemeHelper.ContentBackground, Padding = new Padding(16) };
        if (AccessControlService.HasPermission("ManageSystem.Roles"))
            rolesTab.Controls.Add(CreateRolesPanel());
        else
            rolesTab.Controls.Add(CreatePlaceholder("You do not have permission to manage roles."));

        _tabs.TabPages.Add(settingsTab);
        _tabs.TabPages.Add(brandingTab);
        _tabs.TabPages.Add(usersTab);
        _tabs.TabPages.Add(rolesTab);

        Controls.Add(_tabs);
        Controls.Add(titleLabel);

        Resize += (_, _) => { _userPageSize = Height > 700 ? 13 : 4; _ = LoadUsersAsync(); };
    }

    private Panel CreateUsersPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill };
        
        Panel toolbar = new() { Dock = DockStyle.Top, Height = 50, Padding = new Padding(0, 0, 0, 10) };
        
        _userSearchInput.PlaceholderText = "Search users...";
        _userSearchInput.Location = new Point(0, 5);
        _userSearchInput.TextChanged += async (_, _) => await LoadUsersAsync();

        _roleFilter.Items.Add("All Roles");
        _roleFilter.SelectedIndex = 0;
        _roleFilter.Location = new Point(210, 5);
        _roleFilter.SelectedIndexChanged += async (_, _) => await LoadUsersAsync();

        _statusFilter.Items.AddRange(["All Status", "Active", "Inactive"]);
        _statusFilter.SelectedIndex = 0;
        _statusFilter.Location = new Point(370, 5);
        _statusFilter.SelectedIndexChanged += async (_, _) => await LoadUsersAsync();

        Button addUserBtn = ControlFactory.CreatePrimaryButton("Add User", 120, 32);
        addUserBtn.Location = new Point(500, 4);
        addUserBtn.Click += async (_, _) => {
            using var form = new UserDetailsForm(_currentUserId);
            if (form.ShowDialog() == DialogResult.OK) await LoadUsersAsync();
        };

        toolbar.Controls.Add(_userSearchInput);
        toolbar.Controls.Add(_roleFilter);
        toolbar.Controls.Add(_statusFilter);
        toolbar.Controls.Add(addUserBtn);

        _usersGrid.CellContentClick += UsersGrid_CellContentClick;
        panel.Controls.Add(_usersGrid);
        panel.Controls.Add(toolbar);

        return panel;
    }

    private Panel CreateRolesPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill };
        Panel toolbar = new() { Dock = DockStyle.Top, Height = 50, Padding = new Padding(0, 0, 0, 10) };

        Button addRoleBtn = ControlFactory.CreatePrimaryButton("Add Role", 120, 32);
        addRoleBtn.Location = new Point(0, 4);
        addRoleBtn.Click += async (_, _) => {
            using var form = new RoleDetailsForm(_currentUserId);
            if (form.ShowDialog() == DialogResult.OK) await LoadRolesAsync();
        };

        toolbar.Controls.Add(addRoleBtn);
        _rolesGrid.CellContentClick += RolesGrid_CellContentClick;

        panel.Controls.Add(_rolesGrid);
        panel.Controls.Add(toolbar);
        return panel;
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            bool? isActive = _statusFilter.SelectedIndex == 1 ? true : (_statusFilter.SelectedIndex == 2 ? false : null);
            var users = await _userService.SearchUsersAsync(_userSearchInput.Text, null, isActive);
            
            _usersGrid.Rows.Clear();
            if (_usersGrid.Columns.Count == 0)
            {
                _usersGrid.Columns.Add("FullName", "Full Name");
                _usersGrid.Columns.Add("Username", "Username");
                _usersGrid.Columns.Add("Role", "Role");
                _usersGrid.Columns.Add("Status", "Status");
                _usersGrid.Columns.Add("Actions", "Actions");
            }

            foreach (var user in users.Take(_userPageSize))
            {
                _usersGrid.Rows.Add(
                    user.FullName ?? string.Empty, 
                    user.Username ?? string.Empty, 
                    user.RoleName ?? string.Empty, 
                    user.IsActive ? "Active" : "Inactive", 
                    "Edit Account");
                _usersGrid.Rows[_usersGrid.Rows.Count - 1].Tag = user;
            }
        }
        catch (Exception ex) { MessageBoxHelper.ShowError(ex.Message); }
    }

    private async Task LoadRolesAsync()
    {
        try
        {
            var roles = await _roleService.GetAllRolesAsync();
            
            _roleFilter.Items.Clear();
            _roleFilter.Items.Add("All Roles");
            foreach (var r in roles.Where(r => r.IsActive)) _roleFilter.Items.Add(r.RoleName);
            _roleFilter.SelectedIndex = 0;

            _rolesGrid.Rows.Clear();
            if (_rolesGrid.Columns.Count == 0)
            {
                _rolesGrid.Columns.Add("RoleName", "Role Name");
                _rolesGrid.Columns.Add("Description", "Description");
                _rolesGrid.Columns.Add("Status", "Status");
                _rolesGrid.Columns.Add("Actions", "Actions");
            }

            foreach (var role in roles)
            {
                _rolesGrid.Rows.Add(role.RoleName, role.Description, role.IsActive ? "Active" : "Inactive", "Edit Permissions");
                _rolesGrid.Rows[_rolesGrid.Rows.Count - 1].Tag = role;
            }
        }
        catch (Exception ex) { MessageBoxHelper.ShowError(ex.Message); }
    }

    private async void UsersGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _usersGrid.Rows[e.RowIndex].Tag is not UserListItem user) return;

        if (_usersGrid.Columns[e.ColumnIndex].Name == "Actions")
        {
            using var form = new UserDetailsForm(_currentUserId, user.UserId);
            if (form.ShowDialog() == DialogResult.OK) await LoadUsersAsync();
        }
    }

    private async void RolesGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _rolesGrid.Rows[e.RowIndex].Tag is not Role role) return;

        if (_rolesGrid.Columns[e.ColumnIndex].Name == "Actions")
        {
            using var form = new RoleDetailsForm(_currentUserId, role.RoleId);
            if (form.ShowDialog() == DialogResult.OK) await LoadRolesAsync();
        }
    }

    private static DataGridView CreateGrid()
    {
        DataGridView grid = new()
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        grid.ColumnHeadersDefaultCellStyle.Font = FontHelper.SemiBold(9F);
        grid.DefaultCellStyle.Font = FontHelper.Regular(9F);
        return grid;
    }

    private Panel CreateSystemSettingsPanel()
    {
        Panel panel = ControlFactory.CreateCardPanel(new Size(600, 380));
        panel.Dock = DockStyle.Top;
        panel.Padding = new Padding(24);

        int y = 24;
        AddInput(panel, "Business Name *", _businessNameInput, ref y);
        AddInput(panel, "Contact Number", _contactNumberInput, ref y);
        AddInput(panel, "Email Address", _emailInput, ref y);
        AddInput(panel, "Business Address", _addressInput, ref y);

        Button saveButton = ControlFactory.CreatePrimaryButton("Save Settings", 140, 36);
        saveButton.Location = new Point(24, y + 16);
        saveButton.Click += async (_, _) => await SaveSystemSettingsAsync();

        Button resetButton = ControlFactory.CreateSecondaryButton("Reset Defaults", 140, 36);
        resetButton.Location = new Point(176, y + 16);
        resetButton.Click += async (_, _) => await ResetDefaultsAsync();

        panel.Controls.Add(saveButton);
        panel.Controls.Add(resetButton);

        return panel;
    }

    private Panel CreateBrandingPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, AutoScroll = true };

        // Theme
        Panel themeCard = ControlFactory.CreateCardPanel(new Size(600, 140));
        themeCard.Dock = DockStyle.Top;
        themeCard.Padding = new Padding(24);
        themeCard.Margin = new Padding(0, 0, 0, 16);
        
        Label themeLabel = ControlFactory.CreateInputLabel("Main Theme Color");
        themeLabel.Location = new Point(24, 24);
        
        _themeDropdown.Items.AddRange(_themeColors.Keys.ToArray());
        _themeDropdown.Location = new Point(24, 48);
        _themeDropdown.Size = new Size(240, 30);
        _themeDropdown.Font = FontHelper.Regular(10F);
        
        Button saveThemeButton = ControlFactory.CreatePrimaryButton("Save Theme", 120, 30);
        saveThemeButton.Location = new Point(280, 47);
        saveThemeButton.Click += async (_, _) => await SaveBrandingAsync();

        themeCard.Controls.Add(themeLabel);
        themeCard.Controls.Add(_themeDropdown);
        themeCard.Controls.Add(saveThemeButton);

        // Icon
        Panel iconCard = ControlFactory.CreateCardPanel(new Size(600, 160));
        iconCard.Dock = DockStyle.Top;
        iconCard.Padding = new Padding(24);
        iconCard.Margin = new Padding(0, 0, 0, 16);

        Label iconLabel = ControlFactory.CreateInputLabel("System Icon / Logo");
        iconLabel.Location = new Point(24, 24);
        
        _iconPreview.Location = new Point(24, 48);
        _iconPreview.BackColor = ThemeHelper.Background;
        
        Button browseIconBtn = ControlFactory.CreateSecondaryButton("Browse Icon", 100, 30);
        browseIconBtn.Location = new Point(104, 48);
        browseIconBtn.Click += BrowseIconBtn_Click;
        
        Button clearIconBtn = ControlFactory.CreateSecondaryButton("Clear", 80, 30);
        clearIconBtn.Location = new Point(104, 86);
        clearIconBtn.Click += (_, _) => { _currentIconPath = ""; _iconPreview.ImageLocation = null; };

        iconCard.Controls.Add(iconLabel);
        iconCard.Controls.Add(_iconPreview);
        iconCard.Controls.Add(browseIconBtn);
        iconCard.Controls.Add(clearIconBtn);

        // Poster
        Panel posterCard = ControlFactory.CreateCardPanel(new Size(600, 260));
        posterCard.Dock = DockStyle.Top;
        posterCard.Padding = new Padding(24);

        Label posterLabel = ControlFactory.CreateInputLabel("Login Poster Image");
        posterLabel.Location = new Point(24, 24);
        
        _posterPreview.Location = new Point(24, 48);
        _posterPreview.BackColor = ThemeHelper.Background;

        Button browsePosterBtn = ControlFactory.CreateSecondaryButton("Browse Image", 120, 30);
        browsePosterBtn.Location = new Point(160, 48);
        browsePosterBtn.Click += BrowsePosterBtn_Click;

        _useCustomPosterToggle.Location = new Point(160, 86);
        _useCustomPosterToggle.Font = FontHelper.Regular(10F);

        Button clearPosterBtn = ControlFactory.CreateSecondaryButton("Remove Custom", 140, 30);
        clearPosterBtn.Location = new Point(160, 116);
        clearPosterBtn.Click += (_, _) => { _currentPosterPath = ""; _posterPreview.ImageLocation = null; };
        
        Button saveBrandingBtn = ControlFactory.CreatePrimaryButton("Save Branding & Theme", 220, 36);
        saveBrandingBtn.Location = new Point(24, 210);
        saveBrandingBtn.Click += async (_, _) => await SaveBrandingAsync();

        posterCard.Controls.Add(posterLabel);
        posterCard.Controls.Add(_posterPreview);
        posterCard.Controls.Add(browsePosterBtn);
        posterCard.Controls.Add(_useCustomPosterToggle);
        posterCard.Controls.Add(clearPosterBtn);
        posterCard.Controls.Add(saveBrandingBtn);

        panel.Controls.Add(posterCard);
        panel.Controls.Add(new Panel { Height = 16, Dock = DockStyle.Top });
        panel.Controls.Add(iconCard);
        panel.Controls.Add(new Panel { Height = 16, Dock = DockStyle.Top });
        panel.Controls.Add(themeCard);

        return panel;
    }

    private void AddInput(Control parent, string labelText, TextBox input, ref int y)
    {
        Label label = ControlFactory.CreateInputLabel(labelText);
        label.Location = new Point(24, y);
        
        input.Location = new Point(24, y + 24);
        input.Size = new Size(400, 30);
        input.Font = FontHelper.Regular(11F);
        
        parent.Controls.Add(label);
        parent.Controls.Add(input);
        
        y += 68;
    }

    private Control CreatePlaceholder(string message)
    {
        Panel panel = ControlFactory.CreateCardPanel(new Size(0, 160));
        panel.Dock = DockStyle.Top;
        panel.Padding = new Padding(28);

        Label label = new()
        {
            Text = message,
            Dock = DockStyle.Fill,
            Font = FontHelper.Regular(12F),
            ForeColor = ThemeHelper.TextSecondary,
            TextAlign = ContentAlignment.MiddleLeft
        };

        panel.Controls.Add(label);
        return panel;
    }

    private void LoadSettings()
    {
        var settings = AppBrandingManager.CurrentSettings;
        _businessNameInput.Text = settings.BusinessName;
        _contactNumberInput.Text = settings.ContactNumber;
        _emailInput.Text = settings.EmailAddress;
        _addressInput.Text = settings.BusinessAddress;

        string themeName = _themeColors.FirstOrDefault(x => x.Value.Equals(settings.ThemeColor, StringComparison.OrdinalIgnoreCase)).Key;
        _themeDropdown.SelectedItem = string.IsNullOrEmpty(themeName) ? "Blue (Default)" : themeName;

        _currentIconPath = settings.SystemIconPath;
        if (!string.IsNullOrEmpty(_currentIconPath) && File.Exists(_currentIconPath))
            _iconPreview.ImageLocation = _currentIconPath;

        _currentPosterPath = settings.LoginPosterPath;
        if (!string.IsNullOrEmpty(_currentPosterPath) && File.Exists(_currentPosterPath))
            _posterPreview.ImageLocation = _currentPosterPath;

        _useCustomPosterToggle.Checked = settings.UseCustomLoginPoster;
    }

    private async Task SaveSystemSettingsAsync()
    {
        if (string.IsNullOrWhiteSpace(_businessNameInput.Text))
        {
            MessageBoxHelper.ShowWarning("Business Name is required.");
            return;
        }

        var model = AppBrandingManager.CurrentSettings;
        model.BusinessName = _businessNameInput.Text;
        model.ContactNumber = _contactNumberInput.Text;
        model.EmailAddress = _emailInput.Text;
        model.BusinessAddress = _addressInput.Text;

        await _service.SaveSystemSettingsAsync(model, _currentUserId);
        await AppBrandingManager.LoadSettingsAsync();
        MessageBoxHelper.ShowSuccess("System settings saved successfully.");
    }

    private async Task SaveBrandingAsync()
    {
        if (_themeDropdown.SelectedItem is string selectedTheme && _themeColors.TryGetValue(selectedTheme, out string? colorHex))
        {
            var model = AppBrandingManager.CurrentSettings;
            model.ThemeColor = colorHex;
            model.SystemIconPath = _currentIconPath;
            model.LoginPosterPath = _currentPosterPath;
            model.UseCustomLoginPoster = _useCustomPosterToggle.Checked;

            await _service.SaveBrandingSettingsAsync(model, _currentUserId);
            await AppBrandingManager.LoadSettingsAsync();
            MessageBoxHelper.ShowSuccess("Branding and theme saved. Some changes may require re-opening the application.");
        }
    }

    private async Task ResetDefaultsAsync()
    {
        if (MessageBoxHelper.Confirm("Are you sure you want to reset system settings to defaults?"))
        {
            await _service.ResetDefaultsAsync(_currentUserId);
            await AppBrandingManager.LoadSettingsAsync();
            LoadSettings();
            MessageBoxHelper.ShowSuccess("Settings reset to defaults.");
        }
    }

    private void BrowseIconBtn_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new() { Filter = "Image Files|*.ico;*.png;*.jpg;*.jpeg", Title = "Select System Icon" };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            string newPath = UploadPathHelper.GetBrandingUploadPath(Path.GetFileName(dialog.FileName));
            File.Copy(dialog.FileName, newPath, true);
            _currentIconPath = newPath;
            _iconPreview.ImageLocation = _currentIconPath;
        }
    }

    private void BrowsePosterBtn_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new() { Filter = "Image Files|*.png;*.jpg;*.jpeg", Title = "Select Login Poster" };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            string newPath = UploadPathHelper.GetBrandingUploadPath(Path.GetFileName(dialog.FileName));
            File.Copy(dialog.FileName, newPath, true);
            _currentPosterPath = newPath;
            _posterPreview.ImageLocation = _currentPosterPath;
            _useCustomPosterToggle.Checked = true;
        }
    }
}
