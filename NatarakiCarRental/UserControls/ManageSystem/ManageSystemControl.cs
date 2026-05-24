using FontAwesome.Sharp;
using NatarakiCarRental.Forms.ManageSystem;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NatarakiCarRental.UserControls.ManageSystem;

public sealed class ManageSystemControl : UserControl
{
    private const int WidePageSize = 13;
    private const int NarrowPageSize = 4;

    private readonly int _currentUserId;
    private readonly SystemSettingsService _service = new();
    private readonly UserService _userService = new();
    private readonly RoleService _roleService = new();

    private readonly FlowLayoutPanel _tabPanel = new();
    private readonly Panel _contentPanel = new();
    private readonly List<(string Key, string Text, IconChar Icon, Func<Control> Factory)> _availableTabs = [];
    private string _activeTabKey = string.Empty;

    private readonly TextBox _businessNameInput = ControlFactory.CreateTextBox(360);
    private readonly TextBox _contactNumberInput = ControlFactory.CreateTextBox(280);
    private readonly TextBox _emailInput = ControlFactory.CreateTextBox(360);
    private readonly TextBox _addressInput = ControlFactory.CreateTextBox(360);

    private readonly PictureBox _iconPreview = new() { SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(88, 88), BackColor = ThemeHelper.Background };
    private readonly Label _iconPathLabel = CreatePathLabel();
    private string _currentIconPath = string.Empty;

    private readonly PictureBox _posterPreview = new() { SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(150, 210), BackColor = ThemeHelper.Background };
    private readonly Label _posterPathLabel = CreatePathLabel();
    private string _currentPosterPath = string.Empty;
    private readonly CheckBox _useCustomPosterToggle = new() { Text = "Use custom login poster", AutoSize = true, Font = FontHelper.Regular(10F), ForeColor = ThemeHelper.TextPrimary };

    private readonly Dictionary<string, string> _themeColors = new()
    {
        { "Blue", "#2563EB" },
        { "Purple", "#7C3AED" },
        { "Green", "#16A34A" },
        { "Red", "#DC2626" },
        { "Orange", "#EA580C" },
        { "Dark", "#111827" }
    };
    private string _selectedThemeName = "Blue";

    private readonly DataGridView _usersGrid = CreateGrid();
    private readonly TextBox _userSearchInput = ControlFactory.CreateTextBox(260);
    private readonly ComboBox _roleFilter = CreateComboBox(160);
    private readonly ComboBox _userStatusFilter = CreateComboBox(140);
    private readonly Label _usersPaginationLabel = CreatePagingLabel();
    private readonly Button _usersPrevButton = CreatePagingButton("Previous");
    private readonly Button _usersNextButton = CreatePagingButton("Next");
    private readonly Label _usersEmptyLabel = CreateEmptyLabel("No users found.");
    private readonly List<Role> _roles = [];
    private List<UserListItem> _filteredUsers = [];
    private int _usersPage = 1;

    private readonly DataGridView _rolesGrid = CreateGrid();
    private readonly TextBox _roleSearchInput = ControlFactory.CreateTextBox(260);
    private readonly ComboBox _roleStatusFilter = CreateComboBox(140);
    private readonly Label _rolesPaginationLabel = CreatePagingLabel();
    private readonly Button _rolesPrevButton = CreatePagingButton("Previous");
    private readonly Button _rolesNextButton = CreatePagingButton("Next");
    private readonly Label _rolesEmptyLabel = CreateEmptyLabel("No roles found.");
    private List<Role> _filteredRoles = [];
    private int _rolesPage = 1;

    public ManageSystemControl(int currentUserId)
    {
        _currentUserId = currentUserId;
        Dock = DockStyle.Fill;
        BackColor = ThemeHelper.ContentBackground;
        Padding = new Padding(32);

        InitializeLayout();
        LoadSettings();
        _ = LoadReferenceDataAsync();
    }

    private void InitializeLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = ThemeHelper.ContentBackground
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        root.Controls.Add(CreateHeaderPanel(), 0, 0);

        _tabPanel.Dock = DockStyle.Fill;
        _tabPanel.FlowDirection = FlowDirection.LeftToRight;
        _tabPanel.WrapContents = false;
        _tabPanel.BackColor = ThemeHelper.ContentBackground;
        root.Controls.Add(_tabPanel, 0, 1);

        _contentPanel.Dock = DockStyle.Fill;
        _contentPanel.BackColor = ThemeHelper.ContentBackground;
        root.Controls.Add(_contentPanel, 0, 2);

        Controls.Add(root);

        BuildAvailableTabs();
        RenderTabs();
        Resize += async (_, _) =>
        {
            if (_activeTabKey == "Users") await LoadUsersAsync();
            if (_activeTabKey == "Roles") await LoadRolesAsync();
        };
    }

    private Panel CreateHeaderPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        panel.Controls.Add(new Label
        {
            Text = "Manage System",
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(360, 34),
            Font = FontHelper.Title(22F),
            ForeColor = ThemeHelper.TextPrimary
        });
        panel.Controls.Add(new Label
        {
            Text = "Configure system settings, branding, users, roles, and permissions.",
            AutoSize = false,
            Location = new Point(2, 42),
            Size = new Size(720, 24),
            Font = FontHelper.Regular(10.5F),
            ForeColor = ThemeHelper.TextSecondary
        });
        return panel;
    }

    private void BuildAvailableTabs()
    {
        _availableTabs.Clear();
        if (AccessControlService.HasPermission("ManageSystem.Settings"))
            _availableTabs.Add(("Settings", "System Settings", IconChar.Gear, CreateSystemSettingsPanel));
        if (AccessControlService.HasPermission("ManageSystem.Branding"))
            _availableTabs.Add(("Branding", "Branding & Theme", IconChar.Palette, CreateBrandingPanel));
        if (AccessControlService.HasPermission("ManageSystem.Users"))
            _availableTabs.Add(("Users", "Users", IconChar.Users, CreateUsersPanel));
        if (AccessControlService.HasPermission("ManageSystem.Roles"))
            _availableTabs.Add(("Roles", "Roles & Permissions", IconChar.UserShield, CreateRolesPanel));
    }

    private void RenderTabs()
    {
        _tabPanel.Controls.Clear();

        if (_availableTabs.Count == 0)
        {
            _contentPanel.Controls.Clear();
            _contentPanel.Controls.Add(CreateAccessRestrictedPanel());
            return;
        }

        _activeTabKey = string.IsNullOrWhiteSpace(_activeTabKey) ? _availableTabs[0].Key : _activeTabKey;
        foreach (var tab in _availableTabs)
        {
            IconButton button = new()
            {
                Text = tab.Text,
                IconChar = tab.Icon,
                IconSize = 16,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Size = new Size(tab.Key == "Roles" ? 180 : 158, 34),
                Margin = new Padding(0, 10, 8, 0),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = FontHelper.SemiBold(9.5F)
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (_, _) => ShowTab(tab.Key);
            ApplyTabStyle(button, tab.Key == _activeTabKey);
            _tabPanel.Controls.Add(button);
        }

        ShowTab(_activeTabKey);
    }

    private void ShowTab(string key)
    {
        var tab = _availableTabs.FirstOrDefault(t => t.Key == key);
        if (tab.Factory == null) return;

        _activeTabKey = key;
        foreach (IconButton button in _tabPanel.Controls.OfType<IconButton>())
            ApplyTabStyle(button, string.Equals(button.Text, tab.Text, StringComparison.OrdinalIgnoreCase));

        _contentPanel.Controls.Clear();
        _contentPanel.Controls.Add(tab.Factory());
    }

    private static void ApplyTabStyle(IconButton button, bool active)
    {
        button.BackColor = active ? ThemeHelper.Primary : ThemeHelper.Surface;
        button.ForeColor = active ? Color.White : ThemeHelper.TextPrimary;
        button.IconColor = active ? Color.White : ThemeHelper.TextSecondary;
    }

    private Control CreateAccessRestrictedPanel()
    {
        Panel wrapper = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        Panel card = ControlFactory.CreateCardPanel(new Size(520, 170));
        card.Anchor = AnchorStyles.None;
        card.Location = new Point((wrapper.Width - card.Width) / 2, (wrapper.Height - card.Height) / 2);
        card.Controls.Add(new Label
        {
            Text = "Access Restricted",
            AutoSize = false,
            Location = new Point(28, 28),
            Size = new Size(420, 30),
            Font = FontHelper.Title(16F),
            ForeColor = ThemeHelper.TextPrimary
        });
        card.Controls.Add(new Label
        {
            Text = "You do not have permission to manage system settings, branding, users, or roles.",
            AutoSize = false,
            Location = new Point(28, 70),
            Size = new Size(450, 48),
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextSecondary
        });
        wrapper.Resize += (_, _) => card.Location = new Point((wrapper.Width - card.Width) / 2, (wrapper.Height - card.Height) / 2);
        wrapper.Controls.Add(card);
        return wrapper;
    }

    private Control CreateSystemSettingsPanel()
    {
        Panel card = ControlFactory.CreateCardPanel(new Size(0, 0));
        card.Dock = DockStyle.Top;
        card.Height = 315;
        card.Padding = new Padding(24);
        card.Controls.Add(CreateSectionTitle("Business Information", new Point(24, 22), 320));

        AddLabeledInput(card, "Business Name *", _businessNameInput, new Point(24, 70), 360);
        AddLabeledInput(card, "Contact Number", _contactNumberInput, new Point(420, 70), 280);
        AddLabeledInput(card, "Email Address", _emailInput, new Point(24, 138), 360);
        AddLabeledInput(card, "Business Address", _addressInput, new Point(420, 138), 420);

        Button resetButton = ControlFactory.CreateSecondaryButton("Reset to Defaults", 150, 36);
        Button saveButton = ControlFactory.CreatePrimaryButton("Save Settings", 140, 36);
        card.Resize += (_, _) =>
        {
            saveButton.Location = new Point(card.Width - 24 - saveButton.Width, 250);
            resetButton.Location = new Point(saveButton.Left - 14 - resetButton.Width, 250);
        };
        resetButton.Click += async (_, _) => await ResetDefaultsAsync();
        saveButton.Click += async (_, _) => await SaveSystemSettingsAsync();
        card.Controls.Add(resetButton);
        card.Controls.Add(saveButton);

        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground, Padding = new Padding(0, 12, 0, 0) };
        panel.Controls.Add(card);
        return panel;
    }

    private Control CreateBrandingPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = ThemeHelper.ContentBackground, Padding = new Padding(0, 12, 0, 0) };

        Panel iconCard = CreateBrandingCard("System Icon / Logo", 150);
        _iconPreview.Location = new Point(24, 48);
        iconCard.Controls.Add(_iconPreview);
        AddBrandingButtons(iconCard, new Point(140, 56), BrowseIconBtn_Click, OpenIcon, ClearIcon, _iconPathLabel, "Browse", "Open File", "Use Default");

        Panel themeCard = CreateBrandingCard("Main Theme Color", 145);
        int x = 24;
        foreach (string name in _themeColors.Keys)
        {
            Button themeButton = CreateThemeButton(name);
            themeButton.Location = new Point(x, 58);
            themeButton.Click += (_, _) => SelectTheme(name);
            themeCard.Controls.Add(themeButton);
            x += 104;
        }
        Button saveThemeButton = ControlFactory.CreatePrimaryButton("Save Theme", 130, 34);
        saveThemeButton.Location = new Point(24, 100);
        saveThemeButton.Click += async (_, _) => await SaveBrandingAsync();
        themeCard.Controls.Add(saveThemeButton);

        Panel posterCard = CreateBrandingCard("Login Poster", 295);
        _posterPreview.Location = new Point(24, 52);
        posterCard.Controls.Add(_posterPreview);
        AddBrandingButtons(posterCard, new Point(200, 64), BrowsePosterBtn_Click, OpenPoster, ClearPoster, _posterPathLabel, "Browse Poster", "Open File", "Remove Custom");
        _useCustomPosterToggle.Location = new Point(200, 150);
        posterCard.Controls.Add(_useCustomPosterToggle);
        Label posterHint = new()
        {
            Text = "Default login branding will be used when no custom poster is selected.",
            AutoSize = false,
            Location = new Point(200, 184),
            Size = new Size(420, 24),
            Font = FontHelper.Regular(9F),
            ForeColor = ThemeHelper.TextSecondary
        };
        posterCard.Controls.Add(posterHint);
        Button saveBrandingButton = ControlFactory.CreatePrimaryButton("Save Branding & Theme", 210, 36);
        saveBrandingButton.Location = new Point(200, 226);
        saveBrandingButton.Click += async (_, _) => await SaveBrandingAsync();
        posterCard.Controls.Add(saveBrandingButton);

        posterCard.Dock = DockStyle.Top;
        themeCard.Dock = DockStyle.Top;
        iconCard.Dock = DockStyle.Top;
        panel.Controls.Add(posterCard);
        panel.Controls.Add(CreateSpacer());
        panel.Controls.Add(themeCard);
        panel.Controls.Add(CreateSpacer());
        panel.Controls.Add(iconCard);
        return panel;
    }

    private Panel CreateUsersPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground, Padding = new Padding(0, 12, 0, 0) };
        Panel toolbar = new() { Dock = DockStyle.Top, Height = 52, BackColor = ThemeHelper.ContentBackground };
        _userSearchInput.PlaceholderText = "Search users...";
        _userSearchInput.Location = new Point(0, 8);
        _userSearchInput.TextChanged -= UserFilterChanged;
        _userSearchInput.TextChanged += UserFilterChanged;

        _roleFilter.Location = new Point(274, 8);
        _roleFilter.SelectedIndexChanged -= UserFilterChanged;
        _roleFilter.SelectedIndexChanged += UserFilterChanged;

        _userStatusFilter.Items.Clear();
        _userStatusFilter.Items.AddRange(["All Status", "Active", "Inactive"]);
        if (_userStatusFilter.SelectedIndex < 0) _userStatusFilter.SelectedIndex = 0;
        _userStatusFilter.Location = new Point(446, 8);
        _userStatusFilter.SelectedIndexChanged -= UserFilterChanged;
        _userStatusFilter.SelectedIndexChanged += UserFilterChanged;

        Button addButton = ControlFactory.CreatePrimaryButton("Add User", 120, 34);
        addButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        addButton.Location = new Point(0, 6);
        addButton.Click += async (_, _) => await OpenUserFormAsync(null);
        toolbar.Resize += (_, _) => addButton.Left = Math.Max(0, toolbar.Width - addButton.Width);
        toolbar.Controls.Add(_userSearchInput);
        toolbar.Controls.Add(_roleFilter);
        toolbar.Controls.Add(_userStatusFilter);
        toolbar.Controls.Add(addButton);

        Panel card = ControlFactory.CreateCardPanel(new Size(0, 0));
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(18);
        SetupUsersGrid();
        card.Controls.Add(_usersGrid);
        card.Controls.Add(_usersEmptyLabel);

        Panel pager = CreatePager(_usersPrevButton, _usersNextButton, _usersPaginationLabel);
        _usersPrevButton.Click -= UsersPrevButton_Click;
        _usersPrevButton.Click += UsersPrevButton_Click;
        _usersNextButton.Click -= UsersNextButton_Click;
        _usersNextButton.Click += UsersNextButton_Click;

        panel.Controls.Add(card);
        panel.Controls.Add(pager);
        panel.Controls.Add(toolbar);
        _ = LoadUsersAsync();
        return panel;
    }

    private Panel CreateRolesPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground, Padding = new Padding(0, 12, 0, 0) };
        Panel toolbar = new() { Dock = DockStyle.Top, Height = 52, BackColor = ThemeHelper.ContentBackground };
        _roleSearchInput.PlaceholderText = "Search roles...";
        _roleSearchInput.Location = new Point(0, 8);
        _roleSearchInput.TextChanged -= RoleFilterChanged;
        _roleSearchInput.TextChanged += RoleFilterChanged;

        _roleStatusFilter.Items.Clear();
        _roleStatusFilter.Items.AddRange(["All Status", "Active", "Inactive"]);
        if (_roleStatusFilter.SelectedIndex < 0) _roleStatusFilter.SelectedIndex = 0;
        _roleStatusFilter.Location = new Point(274, 8);
        _roleStatusFilter.SelectedIndexChanged -= RoleFilterChanged;
        _roleStatusFilter.SelectedIndexChanged += RoleFilterChanged;

        Button addButton = ControlFactory.CreatePrimaryButton("Add Role", 120, 34);
        addButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        addButton.Location = new Point(0, 6);
        addButton.Click += async (_, _) => await OpenRoleFormAsync(null);
        toolbar.Resize += (_, _) => addButton.Left = Math.Max(0, toolbar.Width - addButton.Width);
        toolbar.Controls.Add(_roleSearchInput);
        toolbar.Controls.Add(_roleStatusFilter);
        toolbar.Controls.Add(addButton);

        Panel card = ControlFactory.CreateCardPanel(new Size(0, 0));
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(18);
        SetupRolesGrid();
        card.Controls.Add(_rolesGrid);
        card.Controls.Add(_rolesEmptyLabel);

        Panel pager = CreatePager(_rolesPrevButton, _rolesNextButton, _rolesPaginationLabel);
        _rolesPrevButton.Click -= RolesPrevButton_Click;
        _rolesPrevButton.Click += RolesPrevButton_Click;
        _rolesNextButton.Click -= RolesNextButton_Click;
        _rolesNextButton.Click += RolesNextButton_Click;

        panel.Controls.Add(card);
        panel.Controls.Add(pager);
        panel.Controls.Add(toolbar);
        _ = LoadRolesAsync();
        return panel;
    }

    private async Task LoadReferenceDataAsync()
    {
        try
        {
            _roles.Clear();
            _roles.AddRange(await _roleService.GetAllRolesAsync(includeArchived: true));
            PopulateRoleFilter();
            await LoadUsersAsync();
            await LoadRolesAsync();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to load Manage System data.\n\n{exception.Message}", "Manage System");
        }
    }

    private void PopulateRoleFilter()
    {
        string? selected = _roleFilter.SelectedItem?.ToString();
        _roleFilter.Items.Clear();
        _roleFilter.Items.Add("All Roles");
        foreach (Role role in _roles.Where(role => role.IsActive && !role.IsArchived))
            _roleFilter.Items.Add(role.RoleName);
        _roleFilter.SelectedItem = _roleFilter.Items.Contains(selected) ? selected : "All Roles";
    }

    private async Task LoadUsersAsync()
    {
        if (_usersGrid.Columns.Count == 0) return;

        try
        {
            int? roleId = null;
            if (_roleFilter.SelectedIndex > 0)
            {
                Role? role = _roles.FirstOrDefault(r => r.RoleName == _roleFilter.SelectedItem?.ToString());
                roleId = role?.RoleId;
            }
            bool? isActive = _userStatusFilter.SelectedIndex == 1 ? true : _userStatusFilter.SelectedIndex == 2 ? false : null;
            _filteredUsers = (await _userService.SearchUsersAsync(_userSearchInput.Text, roleId, isActive, includeArchived: true)).ToList();
            int pageSize = GetPageSize();
            int totalPages = Math.Max(1, (int)Math.Ceiling(_filteredUsers.Count / (double)pageSize));
            if (_usersPage > totalPages) _usersPage = totalPages;

            _usersGrid.Rows.Clear();
            foreach (UserListItem user in _filteredUsers.Skip((_usersPage - 1) * pageSize).Take(pageSize))
            {
                string status = user.IsArchived ? "Archived" : user.IsActive ? "Active" : "Inactive";
                int row = _usersGrid.Rows.Add(
                    user.FullName,
                    user.Username,
                    user.RoleName,
                    status,
                    user.LastLoginAt?.ToString("MMM d, yyyy h:mm tt") ?? "-",
                    user.CreatedAt.ToString("MMM d, yyyy"),
                    GetUserActions(user));
                _usersGrid.Rows[row].Tag = user;
            }
            _usersEmptyLabel.Visible = _filteredUsers.Count == 0;
            UpdatePager(_usersPrevButton, _usersNextButton, _usersPaginationLabel, _usersPage, totalPages, _filteredUsers.Count, "users");
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to load users.\n\n{exception.Message}", "Manage System");
        }
    }

    private async Task LoadRolesAsync()
    {
        if (_rolesGrid.Columns.Count == 0) return;

        try
        {
            IReadOnlyList<Role> roles = await _roleService.GetAllRolesAsync(includeArchived: true);
            string search = _roleSearchInput.Text.Trim();
            bool? isActive = _roleStatusFilter.SelectedIndex == 1 ? true : _roleStatusFilter.SelectedIndex == 2 ? false : null;
            _filteredRoles = roles
                .Where(role => string.IsNullOrWhiteSpace(search)
                    || role.RoleName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (role.Description ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase))
                .Where(role => !isActive.HasValue || role.IsActive == isActive.Value)
                .ToList();

            int pageSize = GetPageSize();
            int totalPages = Math.Max(1, (int)Math.Ceiling(_filteredRoles.Count / (double)pageSize));
            if (_rolesPage > totalPages) _rolesPage = totalPages;

            List<UserListItem> users = (await _userService.SearchUsersAsync(includeArchived: true)).ToList();
            _rolesGrid.Rows.Clear();
            foreach (Role role in _filteredRoles.Skip((_rolesPage - 1) * pageSize).Take(pageSize))
            {
                RoleWithPermissions? permissions = await _roleService.GetRoleWithPermissionsAsync(role.RoleId);
                int row = _rolesGrid.Rows.Add(
                    role.RoleName,
                    role.Description ?? "-",
                    role.IsArchived ? "Archived" : role.IsActive ? "Active" : "Inactive",
                    users.Count(user => user.RoleName == role.RoleName && !user.IsArchived).ToString(),
                    (permissions?.PermissionKeys.Count ?? 0).ToString(),
                    role.IsSystemRole ? "System" : "Custom",
                    GetRoleActions(role));
                _rolesGrid.Rows[row].Tag = role;
            }
            _rolesEmptyLabel.Visible = _filteredRoles.Count == 0;
            UpdatePager(_rolesPrevButton, _rolesNextButton, _rolesPaginationLabel, _rolesPage, totalPages, _filteredRoles.Count, "roles");
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to load roles.\n\n{exception.Message}", "Manage System");
        }
    }

    private void SetupUsersGrid()
    {
        _usersGrid.Columns.Clear();
        _usersGrid.Columns.Add("FullName", "Full Name");
        _usersGrid.Columns.Add("Username", "Username");
        _usersGrid.Columns.Add("Role", "Role");
        _usersGrid.Columns.Add("Status", "Status");
        _usersGrid.Columns.Add("LastLogin", "Last Login");
        _usersGrid.Columns.Add("CreatedAt", "Created At");
        _usersGrid.Columns.Add("Actions", "Actions");
        _usersGrid.CellDoubleClick -= UsersGrid_CellDoubleClick;
        _usersGrid.CellDoubleClick += UsersGrid_CellDoubleClick;
        _usersGrid.CellContentClick -= UsersGrid_CellContentClick;
        _usersGrid.CellContentClick += UsersGrid_CellContentClick;
    }

    private void SetupRolesGrid()
    {
        _rolesGrid.Columns.Clear();
        _rolesGrid.Columns.Add("RoleName", "Role Name");
        _rolesGrid.Columns.Add("Description", "Description");
        _rolesGrid.Columns.Add("Status", "Status");
        _rolesGrid.Columns.Add("UsersCount", "Users Count");
        _rolesGrid.Columns.Add("PermissionsCount", "Permissions Count");
        _rolesGrid.Columns.Add("Type", "Type");
        _rolesGrid.Columns.Add("Actions", "Actions");
        _rolesGrid.CellDoubleClick -= RolesGrid_CellDoubleClick;
        _rolesGrid.CellDoubleClick += RolesGrid_CellDoubleClick;
        _rolesGrid.CellContentClick -= RolesGrid_CellContentClick;
        _rolesGrid.CellContentClick += RolesGrid_CellContentClick;
    }

    private static DataGridView CreateGrid()
    {
        DataGridView grid = new()
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            BackgroundColor = ThemeHelper.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            CellBorderStyle = DataGridViewCellBorderStyle.Single,
            ColumnHeadersHeight = 38,
            EnableHeadersVisualStyles = false,
            GridColor = ThemeHelper.TableGridLine,
            ReadOnly = true,
            RowHeadersVisible = false,
            RowTemplate = { Height = 38 },
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = ThemeHelper.Primary;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemeHelper.Primary;
        grid.ColumnHeadersDefaultCellStyle.Font = FontHelper.SemiBold(9F);
        grid.DefaultCellStyle.Font = FontHelper.Regular(9F);
        grid.DefaultCellStyle.SelectionBackColor = ThemeHelper.Surface;
        grid.DefaultCellStyle.SelectionForeColor = ThemeHelper.TextPrimary;
        return grid;
    }

    private static Panel CreatePager(Button previous, Button next, Label label)
    {
        Panel panel = new() { Dock = DockStyle.Bottom, Height = 48, BackColor = ThemeHelper.ContentBackground };
        previous.Location = new Point(0, 8);
        next.Location = new Point(90, 8);
        label.Location = new Point(180, 8);
        panel.Controls.Add(previous);
        panel.Controls.Add(next);
        panel.Controls.Add(label);
        return panel;
    }

    private static Button CreatePagingButton(string text) => ControlFactory.CreateSecondaryButton(text, 80, 32);

    private static Label CreatePagingLabel() => new()
    {
        AutoSize = false,
        Size = new Size(280, 32),
        TextAlign = ContentAlignment.MiddleLeft,
        Font = FontHelper.Regular(9.5F),
        ForeColor = ThemeHelper.TextSecondary
    };

    private static Label CreateEmptyLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Bottom,
        Height = 42,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = FontHelper.Regular(10F),
        ForeColor = ThemeHelper.TextSecondary,
        Visible = false
    };

    private int GetPageSize() => Width >= 1200 ? WidePageSize : NarrowPageSize;

    private static void UpdatePager(Button previous, Button next, Label label, int page, int totalPages, int count, string noun)
    {
        label.Text = $"Page {page} of {totalPages} ({count} {noun})";
        previous.Enabled = page > 1;
        next.Enabled = page < totalPages;
    }

    private async void UserFilterChanged(object? sender, EventArgs e)
    {
        _usersPage = 1;
        await LoadUsersAsync();
    }

    private async void RoleFilterChanged(object? sender, EventArgs e)
    {
        _rolesPage = 1;
        await LoadRolesAsync();
    }

    private async void UsersPrevButton_Click(object? sender, EventArgs e)
    {
        if (_usersPage <= 1) return;
        _usersPage--;
        await LoadUsersAsync();
    }

    private async void UsersNextButton_Click(object? sender, EventArgs e)
    {
        int totalPages = Math.Max(1, (int)Math.Ceiling(_filteredUsers.Count / (double)GetPageSize()));
        if (_usersPage >= totalPages) return;
        _usersPage++;
        await LoadUsersAsync();
    }

    private async void RolesPrevButton_Click(object? sender, EventArgs e)
    {
        if (_rolesPage <= 1) return;
        _rolesPage--;
        await LoadRolesAsync();
    }

    private async void RolesNextButton_Click(object? sender, EventArgs e)
    {
        int totalPages = Math.Max(1, (int)Math.Ceiling(_filteredRoles.Count / (double)GetPageSize()));
        if (_rolesPage >= totalPages) return;
        _rolesPage++;
        await LoadRolesAsync();
    }

    private async void UsersGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _usersGrid.Rows[e.RowIndex].Tag is not UserListItem user) return;
        await OpenUserFormAsync(user.UserId);
    }

    private async void UsersGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _usersGrid.Columns[e.ColumnIndex].Name != "Actions") return;
        if (_usersGrid.Rows[e.RowIndex].Tag is not UserListItem user) return;
        await OpenUserActionsAsync(user);
    }

    private async void RolesGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _rolesGrid.Rows[e.RowIndex].Tag is not Role role) return;
        await OpenRoleFormAsync(role.RoleId);
    }

    private async void RolesGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _rolesGrid.Columns[e.ColumnIndex].Name != "Actions") return;
        if (_rolesGrid.Rows[e.RowIndex].Tag is not Role role) return;
        await OpenRoleActionsAsync(role);
    }

    private async Task OpenUserActionsAsync(UserListItem user)
    {
        ContextMenuStrip menu = new();
        menu.Items.Add("View", null, async (_, _) => await OpenUserFormAsync(user.UserId, isViewOnly: true));
        if (!user.IsOwner)
        {
            menu.Items.Add("Edit", null, async (_, _) => await OpenUserFormAsync(user.UserId));
            menu.Items.Add("Change Password", null, async (_, _) => await OpenPasswordFormAsync(user));
            menu.Items.Add(user.IsArchived ? "Restore" : "Archive", null, async (_, _) => await ToggleUserArchiveAsync(user));
        }
        menu.Show(Cursor.Position);
        await Task.CompletedTask;
    }

    private async Task OpenRoleActionsAsync(Role role)
    {
        ContextMenuStrip menu = new();
        menu.Items.Add("Edit / Permissions", null, async (_, _) => await OpenRoleFormAsync(role.RoleId));
        if (!role.IsSystemRole)
            menu.Items.Add("Archive", null, async (_, _) => await ArchiveRoleAsync(role));
        menu.Show(Cursor.Position);
        await Task.CompletedTask;
    }

    private async Task OpenUserFormAsync(int? userId, bool isViewOnly = false)
    {
        if (!AccessControlService.HasPermission("ManageSystem.Users"))
        {
            ShowPermissionDenied();
            return;
        }

        using var form = new UserDetailsForm(_currentUserId, userId, isViewOnly);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _usersPage = 1;
            await LoadUsersAsync();
        }
    }

    private async Task OpenPasswordFormAsync(UserListItem user)
    {
        if (user.IsOwner)
        {
            MessageBoxHelper.ShowWarning("The system owner account is protected.");
            return;
        }

        using var form = new UserPasswordForm(_currentUserId, user.UserId, user.Username);
        if (form.ShowDialog() == DialogResult.OK) await LoadUsersAsync();
    }

    private async Task ToggleUserArchiveAsync(UserListItem user)
    {
        try
        {
            if (user.IsOwner)
            {
                MessageBoxHelper.ShowWarning("The system owner account is protected.");
                return;
            }

            if (user.IsArchived)
            {
                await _userService.RestoreUserAsync(user.UserId, _currentUserId);
                MessageBoxHelper.ShowSuccess("User restored successfully.");
            }
            else
            {
                if (!MessageBoxHelper.Confirm($"Archive user {user.Username}?")) return;
                await _userService.ArchiveUserAsync(user.UserId, _currentUserId);
                MessageBoxHelper.ShowSuccess("User archived successfully.");
            }
            await LoadUsersAsync();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning(exception.Message, "Manage System");
        }
    }

    private async Task OpenRoleFormAsync(int? roleId)
    {
        if (!AccessControlService.HasPermission("ManageSystem.Roles"))
        {
            ShowPermissionDenied();
            return;
        }

        using var form = new RoleDetailsForm(_currentUserId, roleId);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _rolesPage = 1;
            await LoadRolesAsync();
            await LoadReferenceDataAsync();
        }
    }

    private async Task ArchiveRoleAsync(Role role)
    {
        try
        {
            if (role.IsSystemRole)
            {
                MessageBoxHelper.ShowWarning("System roles are protected.");
                return;
            }

            if (!MessageBoxHelper.Confirm($"Archive role {role.RoleName}?")) return;
            await _roleService.ArchiveRoleAsync(role.RoleId, _currentUserId);
            MessageBoxHelper.ShowSuccess("Role archived successfully.");
            await LoadRolesAsync();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning(exception.Message, "Manage System");
        }
    }

    private static string GetUserActions(UserListItem user)
    {
        if (user.IsOwner) return "View";
        return user.IsArchived ? "View | Edit | Password | Restore" : "View | Edit | Password | Archive";
    }

    private static string GetRoleActions(Role role)
    {
        return role.IsSystemRole ? "View | Permissions" : "Edit | Permissions | Archive";
    }

    private async Task SaveSystemSettingsAsync()
    {
        if (string.IsNullOrWhiteSpace(_businessNameInput.Text))
        {
            MessageBoxHelper.ShowWarning("Business name is required.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_emailInput.Text)
            && !Regex.IsMatch(_emailInput.Text.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            MessageBoxHelper.ShowWarning("Please enter a valid email address.");
            return;
        }

        try
        {
            SystemSettingsModel model = AppBrandingManager.CurrentSettings;
            model.BusinessName = _businessNameInput.Text.Trim();
            model.ContactNumber = _contactNumberInput.Text.Trim();
            model.EmailAddress = _emailInput.Text.Trim();
            model.BusinessAddress = _addressInput.Text.Trim();
            await _service.SaveSystemSettingsAsync(model, _currentUserId);
            await AppBrandingManager.LoadSettingsAsync();
            MessageBoxHelper.ShowSuccess("System settings saved successfully.");
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to save system settings.\n\n{exception.Message}", "Manage System");
        }
    }

    private async Task SaveBrandingAsync()
    {
        try
        {
            if (!_themeColors.TryGetValue(_selectedThemeName, out string? colorHex)) colorHex = "#2563EB";
            SystemSettingsModel model = AppBrandingManager.CurrentSettings;
            model.ThemeColor = colorHex;
            model.SystemIconPath = _currentIconPath;
            model.LoginPosterPath = _currentPosterPath;
            model.UseCustomLoginPoster = _useCustomPosterToggle.Checked;
            await _service.SaveBrandingSettingsAsync(model, _currentUserId);
            await AppBrandingManager.LoadSettingsAsync();
            ThemeHelper.SetPrimaryColor(ColorTranslator.FromHtml(colorHex));
            MessageBoxHelper.ShowSuccess($"Updated theme color: {_selectedThemeName}.");
            RenderTabs();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to save branding settings.\n\n{exception.Message}", "Manage System");
        }
    }

    private async Task ResetDefaultsAsync()
    {
        if (!MessageBoxHelper.Confirm("Reset system settings to defaults?")) return;
        await _service.ResetDefaultsAsync(_currentUserId);
        await AppBrandingManager.LoadSettingsAsync();
        LoadSettings();
        MessageBoxHelper.ShowSuccess("Settings reset to defaults.");
    }

    private void LoadSettings()
    {
        SystemSettingsModel settings = AppBrandingManager.CurrentSettings;
        _businessNameInput.Text = settings.BusinessName;
        _contactNumberInput.Text = settings.ContactNumber;
        _emailInput.Text = settings.EmailAddress;
        _addressInput.Text = settings.BusinessAddress;
        _selectedThemeName = _themeColors.FirstOrDefault(x => x.Value.Equals(settings.ThemeColor, StringComparison.OrdinalIgnoreCase)).Key ?? "Blue";

        _currentIconPath = settings.SystemIconPath ?? string.Empty;
        _iconPathLabel.Text = GetDisplayPath(_currentIconPath);
        _iconPreview.ImageLocation = File.Exists(_currentIconPath) ? _currentIconPath : null;

        _currentPosterPath = settings.LoginPosterPath ?? string.Empty;
        _posterPathLabel.Text = GetDisplayPath(_currentPosterPath);
        _posterPreview.ImageLocation = File.Exists(_currentPosterPath) ? _currentPosterPath : null;
        _useCustomPosterToggle.Checked = settings.UseCustomLoginPoster;
    }

    private void BrowseIconBtn_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new() { Filter = "Image Files|*.ico;*.png;*.jpg;*.jpeg", Title = "Select System Icon" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        string newPath = UploadPathHelper.GetBrandingUploadPath(Path.GetFileName(dialog.FileName));
        File.Copy(dialog.FileName, newPath, true);
        _currentIconPath = newPath;
        _iconPreview.ImageLocation = _currentIconPath;
        _iconPathLabel.Text = Path.GetFileName(_currentIconPath);
    }

    private void BrowsePosterBtn_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new() { Filter = "Image Files|*.png;*.jpg;*.jpeg", Title = "Select Login Poster" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        string newPath = UploadPathHelper.GetBrandingUploadPath(Path.GetFileName(dialog.FileName));
        File.Copy(dialog.FileName, newPath, true);
        _currentPosterPath = newPath;
        _posterPreview.ImageLocation = _currentPosterPath;
        _posterPathLabel.Text = Path.GetFileName(_currentPosterPath);
        _useCustomPosterToggle.Checked = true;
    }

    private void OpenIcon() => OpenPath(_currentIconPath);
    private void OpenPoster() => OpenPath(_currentPosterPath);

    private void ClearIcon()
    {
        _currentIconPath = string.Empty;
        _iconPreview.ImageLocation = null;
        _iconPathLabel.Text = "No file selected";
    }

    private void ClearPoster()
    {
        _currentPosterPath = string.Empty;
        _posterPreview.ImageLocation = null;
        _posterPathLabel.Text = "No file selected";
        _useCustomPosterToggle.Checked = false;
    }

    private static void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBoxHelper.ShowInfo("No file selected.");
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void SelectTheme(string themeName)
    {
        _selectedThemeName = themeName;
        foreach (Button button in _contentPanel.Controls.Find("ThemeButton", true).OfType<Button>())
        {
            bool selected = button.Text == themeName;
            button.BackColor = selected ? ThemeHelper.Primary : ThemeHelper.Surface;
            button.ForeColor = selected ? Color.White : ThemeHelper.TextPrimary;
        }
    }

    private Button CreateThemeButton(string name)
    {
        Button button = ControlFactory.CreateSecondaryButton(name, 94, 34);
        button.Name = "ThemeButton";
        bool selected = name == _selectedThemeName;
        button.BackColor = selected ? ThemeHelper.Primary : ThemeHelper.Surface;
        button.ForeColor = selected ? Color.White : ThemeHelper.TextPrimary;
        return button;
    }

    private static Panel CreateBrandingCard(string title, int height)
    {
        Panel card = ControlFactory.CreateCardPanel(new Size(0, height));
        card.Height = height;
        card.Padding = new Padding(24);
        card.Controls.Add(CreateSectionTitle(title, new Point(24, 20), 320));
        return card;
    }

    private static void AddBrandingButtons(Panel parent, Point location, EventHandler browseHandler, Action openAction, Action clearAction, Label pathLabel, string browseText, string openText, string clearText)
    {
        Button browse = ControlFactory.CreateSecondaryButton(browseText, 120, 32);
        Button open = ControlFactory.CreateSecondaryButton(openText, 100, 32);
        Button clear = ControlFactory.CreateSecondaryButton(clearText, 120, 32);
        browse.Location = location;
        open.Location = new Point(location.X + 132, location.Y);
        clear.Location = new Point(location.X, location.Y + 42);
        pathLabel.Location = new Point(location.X + 132, location.Y + 48);
        pathLabel.Size = new Size(360, 22);
        browse.Click += browseHandler;
        open.Click += (_, _) => openAction();
        clear.Click += (_, _) => clearAction();
        parent.Controls.Add(browse);
        parent.Controls.Add(open);
        parent.Controls.Add(clear);
        parent.Controls.Add(pathLabel);
    }

    private static Label CreateSectionTitle(string text, Point location, int width) => new()
    {
        Text = text,
        AutoSize = false,
        Location = location,
        Size = new Size(width, 28),
        Font = FontHelper.SemiBold(12F),
        ForeColor = ThemeHelper.TextPrimary
    };

    private static void AddLabeledInput(Control parent, string labelText, TextBox input, Point labelLocation, int width)
    {
        Label label = ControlFactory.CreateInputLabel(labelText);
        label.Location = labelLocation;
        input.Location = new Point(labelLocation.X, labelLocation.Y + 24);
        input.Size = new Size(width, 30);
        input.Font = FontHelper.Regular(10F);
        parent.Controls.Add(label);
        parent.Controls.Add(input);
    }

    private static ComboBox CreateComboBox(int width)
    {
        return new ComboBox
        {
            Width = width,
            Height = 30,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextPrimary
        };
    }

    private static Label CreatePathLabel() => new()
    {
        Text = "No file selected",
        AutoSize = false,
        Font = FontHelper.Regular(9F),
        ForeColor = ThemeHelper.TextSecondary,
        AutoEllipsis = true
    };

    private static string GetDisplayPath(string path) => string.IsNullOrWhiteSpace(path) ? "No file selected" : Path.GetFileName(path);

    private static Panel CreateSpacer() => new() { Dock = DockStyle.Top, Height = 16, BackColor = ThemeHelper.ContentBackground };

    private static void ShowPermissionDenied()
    {
        MessageBoxHelper.ShowWarning("You do not have permission to perform this action.", "Permission Denied");
    }
}
