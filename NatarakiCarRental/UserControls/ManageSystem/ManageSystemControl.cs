using FontAwesome.Sharp;
using NatarakiCarRental.Forms.ManageSystem;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Common;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;

namespace NatarakiCarRental.UserControls.ManageSystem;

public sealed class ManageSystemControl : UserControl
{
    private const int WidePageSize = 13;
    private const int NarrowPageSize = 4;
    private const int WideManageGridThreshold = 1180;
    private const string NotApplicableProvinceCode = "__NA__";
    private const string NotApplicableProvinceName = "N/A";

    private readonly int _currentUserId;
    private readonly SystemSettingsService _service = new();
    private readonly UserService _userService = new();
    private readonly RoleService _roleService = new();
    private readonly LocalAddressService _addressService = new();
    private readonly SecurityVerificationService _verificationService = new();

    private readonly FlowLayoutPanel _tabPanel = new();
    private readonly Panel _contentPanel = new();
    private readonly List<(string Key, string Text, IconChar Icon, Func<Control> Factory)> _availableTabs = [];
    private string _activeTabKey = string.Empty;

    private readonly TextBox _businessNameInput = ControlFactory.CreateTextBox(360);
    private readonly TextBox _contactNumberInput = ControlFactory.CreateTextBox(360);
    private readonly TextBox _emailInput = ControlFactory.CreateTextBox(360);
    private readonly TextBox _addressInput = ControlFactory.CreateTextBox(360);
    private readonly TextBox _loginDescriptionInput = ControlFactory.CreateTextBox(360);
    private readonly ComboBox _businessRegionComboBox = CreateComboBox(360);
    private readonly ComboBox _businessProvinceComboBox = CreateComboBox(360);
    private readonly ComboBox _businessCityComboBox = CreateComboBox(360);
    private readonly ComboBox _businessBarangayComboBox = CreateComboBox(360);
    private readonly TextBox _businessStreetInput = ControlFactory.CreateTextBox(360);
    private bool _isInitializingAddress;

    private readonly Panel _iconPreview = new() { Size = new Size(88, 88), BackColor = ThemeHelper.Background, BorderStyle = BorderStyle.FixedSingle };
    private readonly Label _iconPathLabel = CreatePathLabel();
    private string _currentIconPath = string.Empty;
    private string _currentLogoMode = "BuiltIn";
    private string _currentLogoIconKey = "Car";

    private readonly PictureBox _posterPreview = new() { SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(150, 210), BackColor = ThemeHelper.Background };
    private readonly Label _posterPathLabel = CreatePathLabel();
    private string _currentPosterPath = string.Empty;
    private readonly CheckBox _useCustomPosterToggle = new() { Text = "Use custom login poster", AutoSize = true, Font = FontHelper.Regular(10F), ForeColor = ThemeHelper.TextPrimary };
    private readonly ComboBox _themePresetComboBox = CreateComboBox(280);
    private readonly TextBox _themeHexInput = ControlFactory.CreateTextBox(110);
    private readonly Panel _themeColorPreview = new() { Size = new Size(34, 30), BackColor = ThemeHelper.Primary };

    private readonly Dictionary<string, string> _themeColors = new()
    {
        { "Blue", "#2563EB" },
        { "Navy", "#1E3A8A" },
        { "Indigo", "#4F46E5" },
        { "Slate", "#334155" },
        { "Emerald", "#059669" },
        { "Teal", "#0F766E" },
        { "Violet", "#7C3AED" },
        { "Red", "#DC2626" },
        { "Orange", "#EA580C" },
        { "Dark", "#111827" }
    };
    private string _selectedThemeName = "Blue";
    private string _selectedThemeHex = "#2563EB";

    private readonly DataGridView _usersGrid = CreateGrid();
    private readonly IconButton _usersTabButton = CreateSubTabButton("Users", IconChar.Users);
    private readonly IconButton _archivedUsersTabButton = CreateSubTabButton("Archived", IconChar.Archive);
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
    private bool _showArchivedUsers;

    private readonly DataGridView _rolesGrid = CreateGrid();
    private readonly IconButton _rolesTabButton = CreateSubTabButton("Roles", IconChar.UserShield);
    private readonly IconButton _archivedRolesTabButton = CreateSubTabButton("Archived", IconChar.Archive);
    private readonly TextBox _roleSearchInput = ControlFactory.CreateTextBox(260);
    private readonly ComboBox _roleStatusFilter = CreateComboBox(140);
    private readonly Label _rolesPaginationLabel = CreatePagingLabel();
    private readonly Button _rolesPrevButton = CreatePagingButton("Previous");
    private readonly Button _rolesNextButton = CreatePagingButton("Next");
    private readonly Label _rolesEmptyLabel = CreateEmptyLabel("No roles found.");
    private List<RoleListItem> _filteredRoles = [];
    private int _rolesPage = 1;
    private int _rolesLoadVersion;
    private bool _showArchivedRoles;
    
    private readonly System.Windows.Forms.Timer _resizeTimer = new() { Interval = 300 };

    public ManageSystemControl(int currentUserId)
    {
        _currentUserId = currentUserId;
        Dock = DockStyle.Fill;
        BackColor = ThemeHelper.ContentBackground;
        Padding = new Padding(32);

        InitializeLayout();
        _resizeTimer.Tick += ResizeTimer_Tick;
        Load += ManageSystemControl_Load;
        Disposed += (s, e) => _resizeTimer.Dispose();
    }

    private async void ResizeTimer_Tick(object? sender, EventArgs e)
    {
        _resizeTimer.Stop();
        if (_activeTabKey == "Users") await LoadUsersAsync();
        if (_activeTabKey == "Roles") await LoadRolesAsync();
    }

    private async void ManageSystemControl_Load(object? sender, EventArgs e)
    {
        LoadSettings();
        await LoadReferenceDataAsync();
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
        Resize += (s, ev) =>
        {
            _resizeTimer.Stop();
            _resizeTimer.Start();
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
            _availableTabs.Add(("Branding", "Branding && Theme", IconChar.Palette, CreateBrandingPanel));
        if (AccessControlService.HasPermission("ManageSystem.Users"))
            _availableTabs.Add(("Users", "Users", IconChar.Users, CreateUsersPanel));
        if (AccessControlService.HasPermission("ManageSystem.Roles"))
            _availableTabs.Add(("Roles", "Roles && Permissions", IconChar.UserShield, CreateRolesPanel));
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

        _activeTabKey = string.IsNullOrWhiteSpace(_activeTabKey) ? _availableTabs[0].Key : _availableTabs[0].Key;
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
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = active ? ThemeHelper.PrimaryHover : Color.Empty;
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
        Panel viewport = new()
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = ThemeHelper.ContentBackground,
            Padding = new Padding(0, 12, 0, 0)
        };

        Panel card = ControlFactory.CreateCardPanel(new Size(0, 330));
        card.Dock = DockStyle.Top;
        card.Height = 330;
        card.Padding = new Padding(24);
        card.Controls.Add(CreateSectionTitle("Business Information", new Point(24, 22), 320));

        AddLabeledInput(card, "Business Name *", _businessNameInput, new Point(24, 70), 360);
        AddLabeledInput(card, "Contact Number", _contactNumberInput, new Point(420, 70), 360);
        AddLabeledInput(card, "Email Address", _emailInput, new Point(816, 70), 360);

        card.Controls.Add(CreateSectionTitle("Business Address", new Point(24, 138), 320));
        AddLabeledCombo(card, "Region", _businessRegionComboBox, new Point(24, 186), 360);
        AddLabeledCombo(card, "Province", _businessProvinceComboBox, new Point(420, 186), 360);
        AddLabeledCombo(card, "City / Municipality", _businessCityComboBox, new Point(816, 186), 360);
        AddLabeledCombo(card, "Barangay", _businessBarangayComboBox, new Point(24, 254), 360);
        AddLabeledInput(card, "Street / House / Block", _businessStreetInput, new Point(420, 254), 360);

        Button resetButton = ControlFactory.CreateSecondaryButton("Reset to Defaults", 150, 36);
        Button saveButton = ControlFactory.CreatePrimaryButton("Save Settings", 140, 36);
        resetButton.Click += async (_, _) => await ResetDefaultsAsync();
        saveButton.Click += async (_, _) => await SaveSystemSettingsAsync();
        WireAddressSelectors();
        LayoutSystemSettingsCard(card);
        card.Resize += (_, _) => LayoutSystemSettingsCard(card);

        Panel footer = new() { Dock = DockStyle.Top, Height = 54, BackColor = ThemeHelper.ContentBackground };
        footer.Controls.Add(resetButton);
        footer.Controls.Add(saveButton);
        footer.Resize += (_, _) => LayoutSystemSettingsFooter(footer, resetButton, saveButton);
        LayoutSystemSettingsFooter(footer, resetButton, saveButton);

        viewport.Controls.Add(footer);
        viewport.Controls.Add(CreateSpacer());
        viewport.Controls.Add(card);
        _ = InitializeBusinessAddressSelectorsAsync();
        return viewport;
    }

    private Control CreateBrandingPanel()
    {
        Panel viewport = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = ThemeHelper.ContentBackground, Padding = new Padding(0, 12, 0, 0) };

        Panel themeCard = CreateBrandingCard("Main Theme Color", 154);
        AddLabeledCombo(themeCard, "Preset Theme", _themePresetComboBox, new Point(24, 56), 280);
        PopulateThemePresetComboBox();
        AddLabeledInput(themeCard, "Hex Color", _themeHexInput, new Point(330, 56), 110);
        _themeColorPreview.Location = new Point(462, 80);
        _themeColorPreview.Paint -= ThemeColorPreview_Paint;
        _themeColorPreview.Paint += ThemeColorPreview_Paint;
        Button customColorButton = ControlFactory.CreateSecondaryButton("Pick Custom Color", 158, 32);
        customColorButton.Location = new Point(514, 78);
        customColorButton.Click += PickCustomColorButton_Click;
        themeCard.Controls.Add(_themeColorPreview);
        themeCard.Controls.Add(customColorButton);

        Panel logoCard = CreateBrandingCard("System Icon / Logo", 210);
        _iconPreview.Location = new Point(24, 58);
        logoCard.Controls.Add(_iconPreview);
        AddBrandingButtons(logoCard, new Point(130, 64), BrowseIconBtn_Click, OpenIcon, ClearIcon, _iconPathLabel, "Browse", "Open File", "Use Default");
        Button builtInIconButton = ControlFactory.CreateSecondaryButton("Choose Built-in Icon", 178, 32);
        builtInIconButton.Location = new Point(130, 148);
        builtInIconButton.Click += ChooseBuiltInIconButton_Click;
        logoCard.Controls.Add(builtInIconButton);

        Panel posterCard = CreateBrandingCard("Login Poster", 300);
        _posterPreview.Location = new Point(24, 58);
        posterCard.Controls.Add(_posterPreview);
        AddBrandingButtons(posterCard, new Point(196, 64), BrowsePosterBtn_Click, OpenPoster, ClearPoster, _posterPathLabel, "Browse", "Open File", "Remove");
        _useCustomPosterToggle.Location = new Point(196, 156);
        _useCustomPosterToggle.CheckedChanged -= UseCustomPosterToggle_CheckedChanged;
        _useCustomPosterToggle.CheckedChanged += UseCustomPosterToggle_CheckedChanged;
        posterCard.Controls.Add(_useCustomPosterToggle);
        AddLabeledInput(posterCard, "Login Description", _loginDescriptionInput, new Point(196, 196), 420);
        Label posterHint = new()
        {
            Text = "Used only when no custom poster is shown.",
            AutoSize = false,
            Location = new Point(196, 250),
            Size = new Size(420, 22),
            Font = FontHelper.Regular(9F),
            ForeColor = ThemeHelper.TextSecondary
        };
        posterCard.Controls.Add(posterHint);

        Panel footer = new() { Dock = DockStyle.Top, Height = 54, BackColor = ThemeHelper.ContentBackground };

        Button resetBrandingButton = ControlFactory.CreateSecondaryButton("Reset Branding", 140, 36);
        Button saveBrandingButton = ControlFactory.CreatePrimaryButton("Save Branding & Theme", 210, 36);
        resetBrandingButton.Click += (_, _) => ResetBrandingFields();
        saveBrandingButton.Click += async (_, _) => await SaveBrandingAsync();
        footer.Controls.Add(resetBrandingButton);
        footer.Controls.Add(saveBrandingButton);
        footer.Resize += (_, _) => AlignFooterButtons(footer, resetBrandingButton, saveBrandingButton);
        AlignFooterButtons(footer, resetBrandingButton, saveBrandingButton);

        viewport.Controls.Add(footer);
        viewport.Controls.Add(CreateSpacer());
        viewport.Controls.Add(posterCard);
        viewport.Controls.Add(CreateSpacer());
        viewport.Controls.Add(logoCard);
        viewport.Controls.Add(CreateSpacer());
        viewport.Controls.Add(themeCard);
        return viewport;
    }

    private Panel CreateUsersPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground, Padding = new Padding(0, 12, 0, 0) };
        Panel tabRow = new() { Dock = DockStyle.Top, Height = 42, BackColor = ThemeHelper.ContentBackground };
        _usersTabButton.Location = new Point(0, 4);
        _archivedUsersTabButton.Location = new Point(106, 4);
        _usersTabButton.Click -= UsersTabButton_Click;
        _usersTabButton.Click += UsersTabButton_Click;
        _archivedUsersTabButton.Click -= ArchivedUsersTabButton_Click;
        _archivedUsersTabButton.Click += ArchivedUsersTabButton_Click;
        tabRow.Controls.Add(_usersTabButton);
        tabRow.Controls.Add(_archivedUsersTabButton);
        ApplyUserArchiveTabStyles();

        Panel toolbar = new() { Dock = DockStyle.Top, Height = 52, BackColor = ThemeHelper.ContentBackground };
        
        BorderedPanel searchContainer = CreateSearchInput(_userSearchInput, "Search users...", 260);
        searchContainer.Location = new Point(0, 8);
        _userSearchInput.TextChanged -= UserFilterChanged;
        _userSearchInput.TextChanged += UserFilterChanged;

        _roleFilter.Location = new Point(274, 8);
        _roleFilter.SelectedIndexChanged -= UserFilterChanged;
        _roleFilter.SelectedIndexChanged += UserFilterChanged;

        IconButton addButton = CreateToolbarIconButton("Add User", IconChar.UserPlus, 128);
        addButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        addButton.Location = new Point(0, 6);
        addButton.Click += async (_, _) => await OpenUserFormAsync(null);
        toolbar.Resize += (_, _) => addButton.Left = Math.Max(0, toolbar.Width - addButton.Width);
        toolbar.Controls.Add(searchContainer);
        toolbar.Controls.Add(_roleFilter);
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
        panel.Controls.Add(tabRow);
        _ = LoadUsersAsync();
        return panel;
    }

    private Panel CreateRolesPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground, Padding = new Padding(0, 12, 0, 0) };
        Panel tabRow = new() { Dock = DockStyle.Top, Height = 42, BackColor = ThemeHelper.ContentBackground };
        _rolesTabButton.Location = new Point(0, 4);
        _archivedRolesTabButton.Location = new Point(106, 4);
        _rolesTabButton.Click -= RolesTabButton_Click;
        _rolesTabButton.Click += RolesTabButton_Click;
        _archivedRolesTabButton.Click -= ArchivedRolesTabButton_Click;
        _archivedRolesTabButton.Click += ArchivedRolesTabButton_Click;
        tabRow.Controls.Add(_rolesTabButton);
        tabRow.Controls.Add(_archivedRolesTabButton);
        ApplyRoleArchiveTabStyles();

        Panel toolbar = new() { Dock = DockStyle.Top, Height = 52, BackColor = ThemeHelper.ContentBackground };
        
        BorderedPanel searchContainer = CreateSearchInput(_roleSearchInput, "Search roles...", 260);
        searchContainer.Location = new Point(0, 8);
        _roleSearchInput.TextChanged -= RoleFilterChanged;
        _roleSearchInput.TextChanged += RoleFilterChanged;

        IconButton addButton = CreateToolbarIconButton("Add Role", IconChar.UserShield, 128);
        addButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        addButton.Location = new Point(0, 6);
        addButton.Click += async (_, _) => await OpenRoleFormAsync(null);
        toolbar.Resize += (_, _) => addButton.Left = Math.Max(0, toolbar.Width - addButton.Width);
        toolbar.Controls.Add(searchContainer);
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
        panel.Controls.Add(tabRow);
        _ = LoadRolesAsync();
        return panel;
    }

    private static BorderedPanel CreateSearchInput(TextBox textBox, string placeholder, int width)
    {
        BorderedPanel container = new()
        {
            Size = new Size(width, 32),
            BackColor = ThemeHelper.Surface,
            BorderColor = ThemeHelper.Border,
            Cursor = Cursors.IBeam
        };

        IconPictureBox icon = new()
        {
            IconChar = IconChar.MagnifyingGlass,
            IconColor = ThemeHelper.TextSecondary,
            IconSize = 18,
            BackColor = ThemeHelper.Surface,
            Location = new Point(8, 7),
            Size = new Size(20, 20)
        };

        textBox.BorderStyle = BorderStyle.None;
        textBox.PlaceholderText = placeholder;
        textBox.BackColor = ThemeHelper.Surface;
        textBox.Font = FontHelper.Regular(10F);
        textBox.ForeColor = ThemeHelper.TextPrimary;
        textBox.Location = new Point(34, 6);
        textBox.Width = width - 42;

        container.Click += (_, _) => textBox.Focus();
        container.Controls.Add(icon);
        container.Controls.Add(textBox);
        return container;
    }

    private async Task LoadReferenceDataAsync()
    {
        try
        {
            await _roleService.NormalizeDuplicateOwnerRolesAsync();
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
            _filteredUsers = (await _userService.SearchUsersAsync(_userSearchInput.Text, roleId, null, includeArchived: true))
                .Where(user => user.IsArchived == _showArchivedUsers)
                .ToList();
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
        if (_rolesGrid.Columns.Count == 0 || !AccessControlService.HasPermission("ManageSystem.Roles")) return;

        int loadVersion = Interlocked.Increment(ref _rolesLoadVersion);

        try
        {
            await _roleService.NormalizeDuplicateOwnerRolesAsync();
            var items = await _roleService.GetRoleListItemsAsync(includeArchived: true);
            if (loadVersion != _rolesLoadVersion) return;

            string search = _roleSearchInput.Text.Trim();
            _filteredRoles = items
                .Where(role => role.IsArchived == _showArchivedRoles)
                .Where(role => string.IsNullOrWhiteSpace(search)
                    || role.RoleName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

            int pageSize = GetPageSize();
            int totalPages = Math.Max(1, (int)Math.Ceiling(_filteredRoles.Count / (double)pageSize));
            if (_rolesPage > totalPages) _rolesPage = totalPages;

            _rolesGrid.Rows.Clear();
            foreach (var role in _filteredRoles.Skip((_rolesPage - 1) * pageSize).Take(pageSize))
            {
                int row = _rolesGrid.Rows.Add(
                    role.RoleName,
                    role.IsArchived ? "Archived" : role.IsActive ? "Active" : "Inactive",
                    role.UsersCount.ToString(),
                    $"{role.ModuleAccessCount} / 9",
                    role.RoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase) ? "Protected" : role.IsSystemRole ? "System" : "Custom",
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
        _usersGrid.CellMouseClick -= UsersGrid_CellMouseClick;
        _usersGrid.CellMouseClick += UsersGrid_CellMouseClick;
        _usersGrid.CellMouseMove -= UsersGrid_CellMouseMove;
        _usersGrid.CellMouseMove += UsersGrid_CellMouseMove;
        _usersGrid.CellMouseLeave -= UsersGrid_CellMouseLeave;
        _usersGrid.CellMouseLeave += UsersGrid_CellMouseLeave;
        _usersGrid.Resize -= UsersGrid_Resize;
        _usersGrid.Resize += UsersGrid_Resize;
        _usersGrid.CellFormatting -= UsersGrid_CellFormatting;
        _usersGrid.CellFormatting += UsersGrid_CellFormatting;
        _usersGrid.CellPainting -= ManageGrid_CellPainting;
        _usersGrid.CellPainting += ManageGrid_CellPainting;
        ApplyUsersGridColumnLayout();
    }

    private void SetupRolesGrid()
    {
        _rolesGrid.Columns.Clear();
        _rolesGrid.Columns.Add("RoleName", "Role Name");
        _rolesGrid.Columns.Add("Status", "Status");
        _rolesGrid.Columns.Add("UsersCount", "Users Count");
        _rolesGrid.Columns.Add("ModuleAccess", "Module Access");
        _rolesGrid.Columns.Add("Type", "Type");
        _rolesGrid.Columns.Add("Actions", "Actions");
        _rolesGrid.CellDoubleClick -= RolesGrid_CellDoubleClick;
        _rolesGrid.CellDoubleClick += RolesGrid_CellDoubleClick;
        _rolesGrid.CellContentClick -= RolesGrid_CellContentClick;
        _rolesGrid.CellMouseClick -= RolesGrid_CellMouseClick;
        _rolesGrid.CellMouseClick += RolesGrid_CellMouseClick;
        _rolesGrid.CellMouseMove -= RolesGrid_CellMouseMove;
        _rolesGrid.CellMouseMove += RolesGrid_CellMouseMove;
        _rolesGrid.Resize -= RolesGrid_Resize;
        _rolesGrid.Resize += RolesGrid_Resize;
        _rolesGrid.CellFormatting -= RolesGrid_CellFormatting;
        _rolesGrid.CellFormatting += RolesGrid_CellFormatting;
        _rolesGrid.CellPainting -= ManageGrid_CellPainting;
        _rolesGrid.CellPainting += ManageGrid_CellPainting;
        ApplyRolesGridColumnLayout();
    }

    private static DataGridView CreateGrid()
    {
        DataGridView grid = new()
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeColumns = false,
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
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ScrollBars = ScrollBars.Both
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

    private void ApplyUsersGridColumnLayout()
    {
        _usersGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        SetColumnFill(_usersGrid, "FullName", 24F, 220);
        SetColumnFill(_usersGrid, "Username", 18F, 160);
        SetColumnFill(_usersGrid, "Role", 13F, 130);
        SetColumnFill(_usersGrid, "Status", 10F, 110);
        SetColumnFill(_usersGrid, "LastLogin", 18F, 170);
        SetColumnFill(_usersGrid, "CreatedAt", 17F, 150);
        
        SetColumnFixed(_usersGrid, "Actions", 285);
        if (_usersGrid.Columns["Actions"] is DataGridViewColumn actionsCol)
        {
            actionsCol.MinimumWidth = 270;
            actionsCol.Resizable = DataGridViewTriState.False;
        }

        _usersGrid.ScrollBars = ScrollBars.Both;
    }

    private void ApplyRolesGridColumnLayout()
    {
        _rolesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        SetColumnFill(_rolesGrid, "RoleName", 35F, 220);
        SetColumnFill(_rolesGrid, "Status", 14F, 120);
        SetColumnFill(_rolesGrid, "UsersCount", 14F, 110);
        SetColumnFill(_rolesGrid, "ModuleAccess", 14F, 120);
        SetColumnFill(_rolesGrid, "Type", 14F, 120);
        
        SetColumnFixed(_rolesGrid, "Actions", 285);
        if (_rolesGrid.Columns["Actions"] is DataGridViewColumn actionsCol)
        {
            actionsCol.MinimumWidth = 270;
            actionsCol.Resizable = DataGridViewTriState.False;
        }

        _rolesGrid.ScrollBars = ScrollBars.Both;
    }

    private static void SetColumnFill(DataGridView grid, string columnName, float fillWeight, int minimumWidth)
    {
        DataGridViewColumn? column = grid.Columns[columnName];
        if (column is not null)
        {
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.FillWeight = fillWeight;
            column.MinimumWidth = minimumWidth;
        }
    }

    private static void SetColumnFixed(DataGridView grid, string columnName, int width)
    {
        DataGridViewColumn? column = grid.Columns[columnName];
        if (column is not null)
        {
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Width = width;
            column.MinimumWidth = width;
        }
    }

    private void UsersGrid_Resize(object? sender, EventArgs e)
    {
        ApplyUsersGridColumnLayout();
    }

    private void RolesGrid_Resize(object? sender, EventArgs e)
    {
        ApplyRolesGridColumnLayout();
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

    private static IconButton CreateToolbarIconButton(string text, IconChar icon, int width)
    {
        IconButton button = new()
        {
            Text = text,
            IconChar = icon,
            IconSize = 15,
            IconColor = Color.White,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            Size = new Size(width, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeHelper.Primary,
            ForeColor = Color.White,
            Font = FontHelper.SemiBold(9F),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = ThemeHelper.PrimaryHover;
        return button;
    }

    private static IconButton CreateSubTabButton(string text, IconChar icon)
    {
        IconButton button = new()
        {
            Text = text,
            IconChar = icon,
            IconSize = 14,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            Size = new Size(98, 32),
            FlatStyle = FlatStyle.Flat,
            Font = FontHelper.SemiBold(9F),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.Empty;
        return button;
    }

    private void ApplyUserArchiveTabStyles()
    {
        ApplySubTabStyle(_usersTabButton, !_showArchivedUsers);
        ApplySubTabStyle(_archivedUsersTabButton, _showArchivedUsers);
    }

    private void ApplyRoleArchiveTabStyles()
    {
        ApplySubTabStyle(_rolesTabButton, !_showArchivedRoles);
        ApplySubTabStyle(_archivedRolesTabButton, _showArchivedRoles);
    }

    private static void ApplySubTabStyle(IconButton button, bool active)
    {
        button.BackColor = active ? ThemeHelper.Primary : ThemeHelper.Surface;
        button.ForeColor = active ? Color.White : ThemeHelper.TextPrimary;
        button.IconColor = active ? Color.White : ThemeHelper.TextSecondary;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.BorderColor = button.BackColor;
        button.FlatAppearance.MouseOverBackColor = active ? ThemeHelper.PrimaryHover : Color.Empty;
    }

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

    private int GetPageSize()
    {
        // Dynamically calculate page size based on available grid height
        // Both grids use RowTemplate.Height = 38 and ColumnHeadersHeight = 38
        DataGridView activeGrid = _activeTabKey == "Roles" ? _rolesGrid : _usersGrid;
        int availableHeight = activeGrid.Height - activeGrid.ColumnHeadersHeight;
        
        // Return at least 1, but otherwise fill the space
        return Math.Max(1, availableHeight / 38);
    }

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

    private async void UsersTabButton_Click(object? sender, EventArgs e)
    {
        _showArchivedUsers = false;
        _usersPage = 1;
        ApplyUserArchiveTabStyles();
        await LoadUsersAsync();
    }

    private async void ArchivedUsersTabButton_Click(object? sender, EventArgs e)
    {
        _showArchivedUsers = true;
        _usersPage = 1;
        ApplyUserArchiveTabStyles();
        await LoadUsersAsync();
    }

    private async void RolesTabButton_Click(object? sender, EventArgs e)
    {
        _showArchivedRoles = false;
        _rolesPage = 1;
        ApplyRoleArchiveTabStyles();
        await LoadRolesAsync();
    }

    private async void ArchivedRolesTabButton_Click(object? sender, EventArgs e)
    {
        _showArchivedRoles = true;
        _rolesPage = 1;
        ApplyRoleArchiveTabStyles();
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

    private void UsersGrid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
    {
        _usersGrid.Cursor = GetActionAt(_usersGrid, e.RowIndex, e.ColumnIndex, e.Location) is null
            ? Cursors.Default
            : Cursors.Hand;
    }

    private void UsersGrid_CellMouseLeave(object? sender, EventArgs e)
    {
        _usersGrid.Cursor = Cursors.Default;
    }

    private void RolesGrid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
    {
        _rolesGrid.Cursor = GetActionAt(_rolesGrid, e.RowIndex, e.ColumnIndex, e.Location) is null
            ? Cursors.Default
            : Cursors.Hand;
    }

    private void RolesGrid_CellMouseLeave(object? sender, EventArgs e)
    {
        _rolesGrid.Cursor = Cursors.Default;
    }

    private async void UsersGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _usersGrid.Rows[e.RowIndex].Tag is not UserListItem user) return;
        await OpenUserFormAsync(user.UserId);
    }

    private async void UsersGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        await Task.CompletedTask;
    }

    private async void UsersGrid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _usersGrid.Columns[e.ColumnIndex].Name != "Actions") return;
        if (_usersGrid.Rows[e.RowIndex].Tag is not UserListItem user) return;
        string? action = GetActionAt(_usersGrid, e.RowIndex, e.ColumnIndex, e.Location);
        if (string.IsNullOrWhiteSpace(action)) return;
        await PerformUserActionAsync(user, action);
    }

    private async void RolesGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _rolesGrid.Rows[e.RowIndex].Tag is not RoleListItem role) return;
        await OpenRoleFormAsync(role.RoleId);
    }

    private async void RolesGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        await Task.CompletedTask;
    }

    private async void RolesGrid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _rolesGrid.Columns[e.ColumnIndex].Name != "Actions") return;
        if (_rolesGrid.Rows[e.RowIndex].Tag is not RoleListItem role) return;
        string? action = GetActionAt(_rolesGrid, e.RowIndex, e.ColumnIndex, e.Location);
        if (string.IsNullOrWhiteSpace(action)) return;
        await PerformRoleActionAsync(role, action);
    }

    private void UsersGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        string columnName = _usersGrid.Columns[e.ColumnIndex].Name;
        if (columnName == "Status")
        {
            ApplyBadgeStyle(e.CellStyle, e.Value?.ToString());
        }
        else if (columnName is "Role" or "Actions")
        {
            e.CellStyle.Font = FontHelper.SemiBold(8.8F);
            e.CellStyle.ForeColor = columnName == "Actions" ? ThemeHelper.Primary : ThemeHelper.TextPrimary;
        }
    }

    private void RolesGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        string columnName = _rolesGrid.Columns[e.ColumnIndex].Name;
        if (columnName == "Status")
        {
            ApplyBadgeStyle(e.CellStyle, e.Value?.ToString());
        }
        else if (columnName is "Type" or "ModuleAccess" or "Actions")
        {
            e.CellStyle.Font = FontHelper.SemiBold(8.8F);
            e.CellStyle.ForeColor = columnName == "Actions" ? ThemeHelper.Primary : ThemeHelper.TextPrimary;
        }
    }

    private static void ApplyBadgeStyle(DataGridViewCellStyle style, string? value)
    {
        style.Font = FontHelper.SemiBold(8.8F);
        style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        style.SelectionForeColor = ThemeHelper.TextPrimary;
        style.SelectionBackColor = ThemeHelper.Surface;
        style.ForeColor = value switch
        {
            "Active" => ThemeHelper.Success,
            "Inactive" => ThemeHelper.Warning,
            "Archived" => ThemeHelper.Danger,
            _ => ThemeHelper.TextSecondary
        };
    }

    private void ManageGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0) return;
        string columnName = grid.Columns[e.ColumnIndex].Name;
        bool isAction = columnName == "Actions";
        bool isBadge = columnName is "Status" or "Role" or "Type";
        if (!isAction && !isBadge) return;

        e.PaintBackground(e.CellBounds, true);
        string text = e.FormattedValue?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            e.Handled = true;
            return;
        }

        if (e.Graphics is null)
        {
            e.Handled = true;
            return;
        }

        if (isAction)
        {
            DrawActionPills(e.Graphics, e.CellBounds, text);
        }
        else
        {
            DrawSinglePill(e.Graphics, e.CellBounds, text, GetManagePillBackColor(columnName, text), Color.White, minWidth: columnName == "Role" ? 86 : 78);
        }

        e.Handled = true;
    }

    private List<(string Action, Rectangle Bounds)> GetUserActionButtonBounds(Rectangle cellBounds, IReadOnlyList<string> actions)
    {
        using Graphics g = CreateGraphics();
        return CalculateActionButtonBounds(g, cellBounds, actions);
    }

    private List<(string Action, Rectangle Bounds)> GetRoleActionButtonBounds(Rectangle cellBounds, IReadOnlyList<string> actions)
    {
        using Graphics g = CreateGraphics();
        return CalculateActionButtonBounds(g, cellBounds, actions);
    }

    private static List<(string Action, Rectangle Bounds)> CalculateActionButtonBounds(Graphics g, Rectangle cellBounds, IReadOnlyList<string> actions)
    {
        List<(string Action, Rectangle Bounds)> results = [];
        if (actions.Count == 0) return results;

        Font font = FontHelper.SemiBold(8.4F);
        int x = cellBounds.Left + 12;
        int y = cellBounds.Top + (cellBounds.Height - 24) / 2;
        const int gap = 8;

        foreach (string action in actions)
        {
            // Apply precise pill dimensions for consistency with other modules
            int width = action switch
            {
                "View" or "Edit" => 72,
                "Archive" or "Restore" => 92,
                _ => Math.Max(54, (int)Math.Ceiling(g.MeasureString(action, font).Width) + 20)
            };

            results.Add((action, new Rectangle(x, y, width, 24)));
            x += width + gap;
        }

        return results;
    }

    private static void DrawActionPills(Graphics graphics, Rectangle cellBounds, string actionsText)
    {
        string[] actions = actionsText.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var boundsList = CalculateActionButtonBounds(graphics, cellBounds, actions);
        Font font = FontHelper.SemiBold(8.4F);
        using Pen linePen = new(ThemeHelper.TableGridLine);

        for (int i = 0; i < boundsList.Count; i++)
        {
            var item = boundsList[i];
            DrawRoundedPill(graphics, item.Bounds, item.Action, font, GetActionPillBackColor(item.Action), Color.White);

            if (i < boundsList.Count - 1)
            {
                var nextItem = boundsList[i + 1];
                float lineX = (item.Bounds.Right + nextItem.Bounds.Left) / 2F;
                graphics.DrawLine(linePen, lineX, cellBounds.Top, lineX, cellBounds.Bottom);
            }
        }
    }

    private static void DrawSinglePill(Graphics graphics, Rectangle cellBounds, string text, Color backColor, Color foreColor, int minWidth)
    {
        Font font = FontHelper.SemiBold(8.6F);
        int width = Math.Min(cellBounds.Width - 12, Math.Max(minWidth, (int)Math.Ceiling(graphics.MeasureString(text, font).Width) + 22));
        Rectangle rect = new(cellBounds.Left + 8, cellBounds.Top + (cellBounds.Height - 24) / 2, width, 24);
        DrawRoundedPill(graphics, rect, text, font, backColor, foreColor);
    }

    private static void DrawRoundedPill(Graphics graphics, Rectangle rect, string text, Font font, Color backColor, Color foreColor)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath path = GetRoundedRect(rect, rect.Height / 2F);
        using SolidBrush backBrush = new(backColor);
        using SolidBrush foreBrush = new(foreColor);
        graphics.FillPath(backBrush, path);
        using StringFormat format = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };
        graphics.DrawString(text, font, foreBrush, rect, format);
    }

    private static Color GetManagePillBackColor(string columnName, string text)
    {
        if (columnName == "Status")
        {
            return text switch
            {
                "Active" => ThemeHelper.Success,
                "Inactive" => ThemeHelper.Warning,
                "Archived" => ThemeHelper.Danger,
                _ => ThemeHelper.GrayIcon
            };
        }

        if (columnName == "Type")
        {
            return text switch
            {
                "Protected" => ThemeHelper.Danger,
                "System" => ThemeHelper.Primary,
                _ => ThemeHelper.GrayIcon
            };
        }

        return ThemeHelper.GrayIcon;
    }

    private static Color GetActionPillBackColor(string action)
    {
        return action switch
        {
            "Edit" => ThemeHelper.Success,
            "Activate" or "Restore" => ThemeHelper.Success,
            "Deactivate" => ThemeHelper.Warning,
            "Archive" => ThemeHelper.Danger,
            _ => ThemeHelper.Primary
        };
    }

    private static GraphicsPath GetRoundedRect(RectangleF rect, float radius)
    {
        GraphicsPath path = new();
        float diameter = radius * 2;
        SizeF size = new(diameter, diameter);
        RectangleF arc = new(rect.Location, size);
        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static string? GetActionAt(DataGridView grid, int rowIndex, int columnIndex, Point cellLocation)
    {
        if (rowIndex < 0 || columnIndex < 0) return null;
        if (rowIndex >= grid.Rows.Count || columnIndex >= grid.Columns.Count) return null;
        if (grid.Columns[columnIndex].Name != "Actions") return null;

        string actionsText = grid.Rows[rowIndex].Cells[columnIndex].Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actionsText)) return null;

        string[] actions = actionsText.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Rectangle cellBounds = grid.GetCellDisplayRectangle(columnIndex, rowIndex, false);
        
        // Convert cell-relative location to grid-relative to match CalculateActionButtonBounds behavior
        Point gridLocation = new Point(cellBounds.Left + cellLocation.X, cellBounds.Top + cellLocation.Y);

        using Graphics graphics = grid.CreateGraphics();
        var boundsList = CalculateActionButtonBounds(graphics, cellBounds, actions);

        foreach (var item in boundsList)
        {
            if (item.Bounds.Contains(gridLocation)) return item.Action;
        }

        return null;
    }

    private async Task PerformUserActionAsync(UserListItem user, string action)
    {
        switch (action)
        {
            case "View":
                await OpenUserFormAsync(user.UserId, isViewOnly: true);
                break;
            case "Edit":
                await OpenUserFormAsync(user.UserId);
                break;
            case "Password":
                break;
            case "Activate":
            case "Deactivate":
                await ToggleUserActiveAsync(user);
                break;
            case "Archive":
            case "Restore":
                await ToggleUserArchiveAsync(user);
                break;
        }
    }

    private async Task PerformRoleActionAsync(RoleListItem role, string action)
    {
        switch (action)
        {
            case "View":
                await OpenRoleFormAsync(role.RoleId, isViewOnly: true);
                break;
            case "Edit":
                await OpenRoleFormAsync(role.RoleId);
                break;
            case "Activate":
            case "Deactivate":
                await ToggleRoleActiveAsync(role);
                break;
            case "Archive":
                await ArchiveRoleAsync(role);
                break;
            case "Restore":
                await RestoreRoleAsync(role);
                break;
        }
    }

    private async Task OpenUserFormAsync(int? userId, bool isViewOnly = false)
    {
        if (!AccessControlService.HasPermission("ManageSystem.Users"))
        {
            ShowAccessDenied();
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

        if (!await ConfirmOwnerForSensitiveActionAsync($"Change password for user: {user.Username}"))
        {
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
                if (!MessageBoxHelper.Confirm($"Are you sure you want to restore user: {user.Username}?", "Restore User")) return;
                if (!await ConfirmOwnerForSensitiveActionAsync($"Restore user: {user.Username}")) return;
                await _userService.RestoreUserAsync(user.UserId, _currentUserId);
                MessageBoxHelper.ShowSuccess("User restored successfully.");
                _showArchivedUsers = false;
                ApplyUserArchiveTabStyles();
            }
            else
            {
                if (!MessageBoxHelper.Confirm($"Archive user {user.Username}?")) return;
                if (!await ConfirmOwnerForSensitiveActionAsync($"Archive user: {user.Username}")) return;
                await _userService.ArchiveUserAsync(user.UserId, _currentUserId);
                MessageBoxHelper.ShowSuccess("User archived successfully.");
                _showArchivedUsers = true;
                ApplyUserArchiveTabStyles();
            }
            _usersPage = 1;
            await LoadUsersAsync();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning(exception.Message, "Manage System");
        }
    }

    private async Task ToggleUserActiveAsync(UserListItem user)
    {
        try
        {
            if (user.IsOwner)
            {
                MessageBoxHelper.ShowWarning("The system owner account is protected.");
                return;
            }

            string action = user.IsActive ? "Deactivate" : "Activate";
            if (!MessageBoxHelper.Confirm($"{action} user {user.Username}?")) return;
            if (!await ConfirmOwnerForSensitiveActionAsync($"{action} user: {user.Username}")) return;

            User? details = await _userService.GetUserByIdAsync(user.UserId);
            Role? role = _roles.FirstOrDefault(item => item.RoleName == user.RoleName);
            if (details is null || role is null)
            {
                MessageBoxHelper.ShowWarning("Unable to load the selected user.");
                return;
            }

            await _userService.UpdateUserAsync(new UpdateUserRequest
            {
                UserId = user.UserId,
                Username = details.Username,
                FirstName = details.FirstName,
                LastName = details.LastName,
                RoleId = role.RoleId,
                IsActive = !user.IsActive,
                Email = details.Email,
                PhoneNumber = details.PhoneNumber
            }, _currentUserId);
            MessageBoxHelper.ShowSuccess($"User {action.ToLowerInvariant()}d successfully.");
            await LoadUsersAsync();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning(exception.Message, "Manage System");
        }
    }

    private async Task OpenRoleFormAsync(int? roleId, bool isViewOnly = false)
    {
        if (!AccessControlService.HasPermission("ManageSystem.Roles"))
        {
            ShowAccessDenied();
            return;
        }

        if (!isViewOnly && !await ConfirmOwnerForSensitiveActionAsync(roleId.HasValue ? "Edit role permissions" : "Create role"))
        {
            return;
        }

        using var form = new RoleDetailsForm(_currentUserId, roleId, isViewOnly);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _rolesPage = 1;
            await LoadRolesAsync();
            await LoadReferenceDataAsync();
        }
    }

    private async Task ArchiveRoleAsync(RoleListItem role)
    {
        try
        {
            if (role.IsSystemRole)
            {
                MessageBoxHelper.ShowWarning("System roles are protected.");
                return;
            }

            if (!MessageBoxHelper.Confirm($"Archive role {role.RoleName}?")) return;
            if (!await ConfirmOwnerForSensitiveActionAsync($"Archive role: {role.RoleName}")) return;
            await _roleService.ArchiveRoleAsync(role.RoleId, _currentUserId);
            MessageBoxHelper.ShowSuccess("Role archived successfully.");
            _showArchivedRoles = true;
            _rolesPage = 1;
            ApplyRoleArchiveTabStyles();
            await LoadRolesAsync();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning(exception.Message, "Manage System");
        }
    }

    private async Task RestoreRoleAsync(RoleListItem role)
    {
        try
        {
            if (role.RoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase))
            {
                MessageBoxHelper.ShowWarning("Owner role is protected.");
                return;
            }

            if (!MessageBoxHelper.Confirm($"Are you sure you want to restore role: {role.RoleName}?", "Restore Role")) return;
            if (!await ConfirmOwnerForSensitiveActionAsync($"Restore role: {role.RoleName}")) return;
            await _roleService.RestoreRoleAsync(role.RoleId, _currentUserId);
            MessageBoxHelper.ShowSuccess("Role restored successfully.");
            _showArchivedRoles = false;
            _rolesPage = 1;
            ApplyRoleArchiveTabStyles();
            await LoadRolesAsync();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning(exception.Message, "Manage System");
        }
    }

    private async Task ToggleRoleActiveAsync(RoleListItem role)
    {
        try
        {
            if (role.IsSystemRole)
            {
                MessageBoxHelper.ShowWarning("System roles are protected.");
                return;
            }

            string action = role.IsActive ? "Deactivate" : "Activate";
            if (!MessageBoxHelper.Confirm($"{action} role {role.RoleName}?")) return;
            if (!await ConfirmOwnerForSensitiveActionAsync($"{action} role: {role.RoleName}")) return;

            RoleWithPermissions? details = await _roleService.GetRoleWithPermissionsAsync(role.RoleId);
            if (details is null)
            {
                MessageBoxHelper.ShowWarning("Unable to load the selected role.");
                return;
            }

            details.IsActive = !role.IsActive;
            await _roleService.UpdateRoleAsync(details, _currentUserId);
            MessageBoxHelper.ShowSuccess($"Role {action.ToLowerInvariant()}d successfully.");
            await LoadRolesAsync();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning(exception.Message, "Manage System");
        }
    }

    private static string GetUserActions(UserListItem user)
    {
        if (user.IsOwner) return "View | Edit";
        if (user.IsArchived) return "View | Restore";
        return "View | Edit | Archive";
    }

    private static string GetRoleActions(RoleListItem role)
    {
        if (role.RoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase)) return "View";
        if (role.IsArchived) return "View | Restore";
        return "View | Edit | Archive";
    }

    private async Task<bool> ConfirmOwnerForSensitiveActionAsync(string actionName)
    {
        if (AccessControlService.CurrentUser?.IsOwner == true) return true;
        User? currentUser = await _userService.GetUserByIdAsync(_currentUserId);
        if (currentUser?.IsOwner == true) return true;
        using OwnerPasswordConfirmationForm form = new(actionName);
        return form.ShowDialog() == DialogResult.OK;
    }

    private async Task SaveSystemSettingsAsync()
    {
        if (!AccessControlService.HasPermission("ManageSystem.Settings"))
        {
            ShowAccessDenied();
            return;
        }

        if (string.IsNullOrWhiteSpace(_businessNameInput.Text))
        {
            MessageBoxHelper.ShowWarning("Business Name is required.");
            return;
        }

        if (!await _verificationService.RequireOwnerVerificationIfNeededAsync(_currentUserId, "Update system settings"))
        {
            return;
        }

        try
        {
            SystemSettingsModel model = AppBrandingManager.CurrentSettings;
            model.BusinessName = _businessNameInput.Text.Trim();
            model.ContactNumber = _contactNumberInput.Text.Trim();
            model.EmailAddress = _emailInput.Text.Trim();
            model.LoginDescription = string.IsNullOrWhiteSpace(_loginDescriptionInput.Text)
                ? "Internal scheduling and record management system"
                : _loginDescriptionInput.Text.Trim();

            AddressOption? region = _businessRegionComboBox.SelectedItem as AddressOption;
            AddressOption? province = _businessProvinceComboBox.SelectedItem as AddressOption;
            AddressOption? city = _businessCityComboBox.SelectedItem as AddressOption;
            AddressOption? barangay = _businessBarangayComboBox.SelectedItem as AddressOption;
            model.BusinessRegionCode = region?.Code ?? string.Empty;
            model.BusinessRegionName = region?.Name ?? string.Empty;
            model.BusinessProvinceCode = province is null || IsNotApplicableProvince(province) ? string.Empty : province.Code;
            model.BusinessProvinceName = province is null || IsNotApplicableProvince(province) ? string.Empty : province.Name;
            model.BusinessCityCode = city?.Code ?? string.Empty;
            model.BusinessCityName = city?.Name ?? string.Empty;
            model.BusinessBarangayCode = barangay?.Code ?? string.Empty;
            model.BusinessBarangayName = barangay?.Name ?? string.Empty;
            model.BusinessStreetAddress = _businessStreetInput.Text.Trim();
            model.BusinessAddress = BuildFullBusinessAddress(model);
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
            string colorHex = _themeHexInput.Text.Trim();
            if (!Regex.IsMatch(colorHex, "^#[0-9A-Fa-f]{6}$"))
            {
                MessageBoxHelper.ShowWarning("Please enter a valid hex color, such as #2563EB.");
                return;
            }

            SystemSettingsModel model = AppBrandingManager.CurrentSettings;
            model.ThemeColor = colorHex;
            model.SystemIconPath = _currentIconPath;
            model.SystemLogoMode = _currentLogoMode;
            model.SystemLogoIconKey = _currentLogoIconKey;
            model.LoginPosterPath = _currentPosterPath;
            model.UseCustomLoginPoster = _useCustomPosterToggle.Checked;
            model.LoginDescription = string.IsNullOrWhiteSpace(_loginDescriptionInput.Text)
                ? "Internal scheduling and record management system"
                : _loginDescriptionInput.Text.Trim();
            await _service.SaveBrandingSettingsAsync(model, _currentUserId);
            await AppBrandingManager.LoadSettingsAsync();
            ThemeHelper.SetPrimaryColor(ColorTranslator.FromHtml(colorHex));
            MessageBoxHelper.ShowSuccess("Branding and theme settings saved successfully.");
            ApplyThemeToCurrentManageSystemUi();
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
        _loginDescriptionInput.Text = settings.LoginDescription;
        _businessStreetInput.Text = settings.BusinessStreetAddress;
        _selectedThemeName = _themeColors.FirstOrDefault(x => x.Value.Equals(settings.ThemeColor, StringComparison.OrdinalIgnoreCase)).Key ?? "Blue";
        _selectedThemeHex = settings.ThemeColor;
        SetSelectedThemeHex(_selectedThemeHex);

        _currentIconPath = settings.SystemIconPath ?? string.Empty;
        _currentLogoMode = string.IsNullOrWhiteSpace(settings.SystemLogoMode) ? "BuiltIn" : settings.SystemLogoMode;
        _currentLogoIconKey = string.IsNullOrWhiteSpace(settings.SystemLogoIconKey) ? "Car" : settings.SystemLogoIconKey;
        _iconPathLabel.Text = GetDisplayPath(_currentIconPath);
        RefreshLogoPreview();

        _currentPosterPath = settings.LoginPosterPath ?? string.Empty;
        _posterPathLabel.Text = GetDisplayPath(_currentPosterPath);
        _useCustomPosterToggle.Checked = settings.UseCustomLoginPoster;
        RefreshPosterPreview();
    }

    private void ResetBrandingFields()
    {
        _currentIconPath = string.Empty;
        _currentLogoMode = "BuiltIn";
        _currentLogoIconKey = "Car";
        _iconPathLabel.Text = "No file selected";
        RefreshLogoPreview();
        _currentPosterPath = string.Empty;
        _posterPathLabel.Text = "No file selected";
        _useCustomPosterToggle.Checked = false;
        RefreshPosterPreview();
        _loginDescriptionInput.Text = "Internal scheduling and record management system";
        _selectedThemeName = "Blue";
        SetSelectedThemeHex("#2563EB");
        PopulateThemePresetComboBox();
    }

    private static string BuildFullBusinessAddress(SystemSettingsModel model)
    {
        return string.Join(", ", new[]
            {
                model.BusinessStreetAddress,
                model.BusinessBarangayName,
                model.BusinessCityName,
                model.BusinessProvinceName,
                model.BusinessRegionName
            }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private void BrowseIconBtn_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new() { Filter = "Image Files|*.ico;*.png;*.jpg;*.jpeg", Title = "Select System Icon" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        string newPath = UploadPathHelper.GetBrandingUploadPath(Path.GetFileName(dialog.FileName));
        File.Copy(dialog.FileName, newPath, true);
        _currentIconPath = newPath;
        _currentLogoMode = "File";
        _iconPathLabel.Text = Path.GetFileName(_currentIconPath);
        RefreshLogoPreview();
    }

    private void BrowsePosterBtn_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new() { Filter = "Image Files|*.png;*.jpg;*.jpeg", Title = "Select Login Poster" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        string newPath = UploadPathHelper.GetBrandingUploadPath(Path.GetFileName(dialog.FileName));
        File.Copy(dialog.FileName, newPath, true);
        _currentPosterPath = newPath;
        _posterPathLabel.Text = Path.GetFileName(_currentPosterPath);
        _useCustomPosterToggle.Checked = true;
        RefreshPosterPreview();
    }

    private void OpenIcon() => OpenPath(_currentIconPath);
    private void OpenPoster() => OpenPath(_currentPosterPath);

    private void ClearIcon()
    {
        _currentIconPath = string.Empty;
        _currentLogoMode = "BuiltIn";
        _currentLogoIconKey = "Car";
        _iconPathLabel.Text = "No file selected";
        RefreshLogoPreview();
    }

    private void ClearPoster()
    {
        _currentPosterPath = string.Empty;
        _posterPathLabel.Text = "No file selected";
        _useCustomPosterToggle.Checked = false;
        RefreshPosterPreview();
    }

    private void UseCustomPosterToggle_CheckedChanged(object? sender, EventArgs e) => RefreshPosterPreview();

    private void ChooseBuiltInIconButton_Click(object? sender, EventArgs e)
    {
        ContextMenuStrip menu = new();
        foreach ((string key, string label) in GetBuiltInLogoChoices())
        {
            menu.Items.Add(label, null, (_, _) =>
            {
                _currentLogoMode = "BuiltIn";
                _currentLogoIconKey = key;
                _currentIconPath = string.Empty;
                _iconPathLabel.Text = $"Built-in: {label}";
                RefreshLogoPreview();
            });
        }

        if (sender is Control control)
        {
            menu.Show(control, new Point(0, control.Height + 2));
        }
        else
        {
            menu.Show(Cursor.Position);
        }
    }

    private static IReadOnlyList<(string Key, string Label)> GetBuiltInLogoChoices() =>
    [
        ("Car", "Car"),
        ("CarSide", "Car Side"),
        ("Taxi", "Taxi"),
        ("Truck", "Truck"),
        ("Road", "Road"),
        ("Warehouse", "Garage / Warehouse"),
        ("Key", "Key")
    ];

    private void RefreshLogoPreview()
    {
        foreach (Control control in _iconPreview.Controls)
        {
            control.Dispose();
        }
        _iconPreview.Controls.Clear();

        if (string.Equals(_currentLogoMode, "File", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_currentIconPath)
            && File.Exists(_currentIconPath))
        {
            _iconPreview.Controls.Add(new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                ImageLocation = _currentIconPath,
                BackColor = ThemeHelper.Background
            });
            return;
        }

        string label = GetBuiltInLogoChoices().FirstOrDefault(choice => choice.Key == _currentLogoIconKey).Label;
        label = string.IsNullOrWhiteSpace(label) ? "Default Logo" : label;
        IconPictureBox icon = new()
        {
            IconChar = ResolveBuiltInLogoIcon(_currentLogoIconKey),
            IconColor = ThemeHelper.Primary,
            IconSize = 38,
            BackColor = ThemeHelper.Background,
            Location = new Point((_iconPreview.Width - 42) / 2, 14),
            Size = new Size(42, 42)
        };
        Label text = new()
        {
            Text = _currentLogoIconKey == "Car" ? "Default Logo" : label,
            AutoSize = false,
            Location = new Point(4, 58),
            Size = new Size(_iconPreview.Width - 8, 20),
            Font = FontHelper.SemiBold(7.8F),
            ForeColor = ThemeHelper.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _iconPreview.Controls.Add(icon);
        _iconPreview.Controls.Add(text);
    }

    private static IconChar ResolveBuiltInLogoIcon(string? key)
    {
        return key switch
        {
            "CarSide" => IconChar.CarSide,
            "Taxi" => IconChar.Taxi,
            "Truck" => IconChar.Truck,
            "Road" => IconChar.Road,
            "Warehouse" => IconChar.Warehouse,
            "Key" => IconChar.Key,
            _ => IconChar.Car
        };
    }

    private void RefreshPosterPreview()
    {
        _posterPreview.ImageLocation = null;
        Image? oldImage = _posterPreview.Image;

        if (_useCustomPosterToggle.Checked
            && !string.IsNullOrWhiteSpace(_currentPosterPath)
            && File.Exists(_currentPosterPath))
        {
            _posterPreview.Image = null;
            _posterPreview.ImageLocation = _currentPosterPath;
        }
        else
        {
            _posterPreview.Image = CreatePreviewPlaceholder("Default Login", "Branding", _posterPreview.Size);
        }

        oldImage?.Dispose();
    }

    private static Bitmap CreatePreviewPlaceholder(string title, string subtitle, Size size)
    {
        Bitmap bitmap = new(Math.Max(1, size.Width), Math.Max(1, size.Height));
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(ThemeHelper.Background);
        using Pen borderPen = new(ThemeHelper.Border);
        graphics.DrawRectangle(borderPen, 0, 0, bitmap.Width - 1, bitmap.Height - 1);
        Font titleFont = FontHelper.SemiBold(size.Width > 120 ? 10F : 8.5F);
        Font subtitleFont = FontHelper.Regular(size.Width > 120 ? 8.5F : 7.5F);
        using SolidBrush titleBrush = new(ThemeHelper.TextPrimary);
        using SolidBrush subtitleBrush = new(ThemeHelper.TextSecondary);
        using StringFormat format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        Rectangle titleRect = new(8, (bitmap.Height / 2) - 24, bitmap.Width - 16, 24);
        Rectangle subtitleRect = new(8, (bitmap.Height / 2), bitmap.Width - 16, 24);
        graphics.DrawString(title, titleFont, titleBrush, titleRect, format);
        graphics.DrawString(subtitle, subtitleFont, subtitleBrush, subtitleRect, format);
        return bitmap;
    }

    private void ApplyThemeToCurrentManageSystemUi()
    {
        foreach (IconButton button in _tabPanel.Controls.OfType<IconButton>())
        {
            bool active = _availableTabs.FirstOrDefault(tab => tab.Key == _activeTabKey).Text == button.Text;
            ApplyTabStyle(button, active);
        }

        ApplyUserArchiveTabStyles();
        ApplyRoleArchiveTabStyles();

        _usersGrid.ColumnHeadersDefaultCellStyle.BackColor = ThemeHelper.Primary;
        _usersGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemeHelper.Primary;
        _rolesGrid.ColumnHeadersDefaultCellStyle.BackColor = ThemeHelper.Primary;
        _rolesGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemeHelper.Primary;
        _themeColorPreview.BackColor = ColorTranslator.FromHtml(_selectedThemeHex);
        RefreshLogoPreview();
        RefreshPosterPreview();
        InvalidateManageSystemControls(this);
    }

    private static void InvalidateManageSystemControls(Control parent)
    {
        parent.Invalidate();
        foreach (Control child in parent.Controls)
        {
            if (child is Button button && button.ForeColor == Color.White)
            {
                button.BackColor = ThemeHelper.Primary;
                button.FlatAppearance.MouseOverBackColor = ThemeHelper.PrimaryHover;
            }
            InvalidateManageSystemControls(child);
        }
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
        if (_themeColors.TryGetValue(themeName, out string? hex))
            SetSelectedThemeHex(hex);
    }

    private void PopulateThemePresetComboBox()
    {
        _themePresetComboBox.SelectedIndexChanged -= ThemePresetComboBox_SelectedIndexChanged;
        _themePresetComboBox.Items.Clear();
        foreach (KeyValuePair<string, string> pair in _themeColors)
            _themePresetComboBox.Items.Add($"{pair.Key} - {pair.Value}");
        string selected = $"{_selectedThemeName} - {_selectedThemeHex}";
        _themePresetComboBox.SelectedItem = _themePresetComboBox.Items.Contains(selected) ? selected : null;
        _themePresetComboBox.SelectedIndexChanged += ThemePresetComboBox_SelectedIndexChanged;
    }

    private void ThemePresetComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        string? selected = _themePresetComboBox.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(selected)) return;
        string themeName = selected.Split(" - ")[0];
        SelectTheme(themeName);
    }

    private void PickCustomColorButton_Click(object? sender, EventArgs e)
    {
        using ColorDialog dialog = new() { Color = _themeColorPreview.BackColor, FullOpen = true };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        SetSelectedThemeHex(ColorTranslator.ToHtml(dialog.Color));
        _selectedThemeName = _themeColors.FirstOrDefault(pair => pair.Value.Equals(_selectedThemeHex, StringComparison.OrdinalIgnoreCase)).Key ?? "Custom";
        PopulateThemePresetComboBox();
    }

    private void ThemeColorPreview_Paint(object? sender, PaintEventArgs e)
    {
        using Pen pen = new(ThemeHelper.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, _themeColorPreview.Width - 1, _themeColorPreview.Height - 1);
    }

    private void SetSelectedThemeHex(string hex)
    {
        _selectedThemeHex = hex.ToUpperInvariant();
        _themeHexInput.Text = _selectedThemeHex;
        try
        {
            _themeColorPreview.BackColor = ColorTranslator.FromHtml(_selectedThemeHex);
        }
        catch
        {
            _themeColorPreview.BackColor = ThemeHelper.Primary;
        }
    }

    private void LayoutSystemSettingsCard(Panel card)
    {
        const int contentPadding = 24;
        const int columnGap = 24;
        int availableWidth = Math.Max(1, card.ClientSize.Width - (contentPadding * 2));
        int columnWidth = Math.Max(1, (availableWidth - (columnGap * 2)) / 3);
        int leftX = contentPadding;
        int rightX = contentPadding + columnWidth + columnGap;
        int thirdX = contentPadding + ((columnWidth + columnGap) * 2);

        MoveLabeledControl(card, "Business Name *", _businessNameInput, leftX, 70, columnWidth);
        MoveLabeledControl(card, "Contact Number", _contactNumberInput, rightX, 70, columnWidth);
        MoveLabeledControl(card, "Email Address", _emailInput, thirdX, 70, columnWidth);

        MoveLabeledControl(card, "Region", _businessRegionComboBox, leftX, 186, columnWidth);
        MoveLabeledControl(card, "Province", _businessProvinceComboBox, rightX, 186, columnWidth);
        MoveLabeledControl(card, "City / Municipality", _businessCityComboBox, thirdX, 186, columnWidth);
        MoveLabeledCombo(card, "Barangay", _businessBarangayComboBox, new Point(leftX, 254), columnWidth);
        MoveLabeledControl(card, "Street / House / Block", _businessStreetInput, rightX, 254, columnWidth);
    }

    private static void LayoutSystemSettingsFooter(Panel footer, Button resetButton, Button saveButton)
    {
        saveButton.Location = new Point(Math.Max(24, footer.ClientSize.Width - 24 - saveButton.Width), 8);
        resetButton.Location = new Point(Math.Max(24, saveButton.Left - 14 - resetButton.Width), 8);
    }

    private static void MoveLabeledControl(Control parent, string labelText, Control input, int x, int y, int width)
    {
        Label? label = parent.Controls.OfType<Label>().FirstOrDefault(item => item.Text == labelText);
        if (label is not null)
        {
            label.Location = new Point(x, y);
            label.Width = width;
        }

        input.Location = new Point(x, y + 24);
        input.Size = new Size(width, input.Height);
    }

    private static void MoveLabeledCombo(Control parent, string labelText, ComboBox input, Point labelLocation, int width)
    {
        Label? label = parent.Controls.OfType<Label>().FirstOrDefault(item => item.Text == labelText);
        if (label is not null)
        {
            label.Location = labelLocation;
            label.Width = width;
        }

        input.Location = new Point(labelLocation.X, labelLocation.Y + 24);
        input.Size = new Size(width, 30);
    }

    private static void AlignFooterButtons(Panel footer, Button resetButton, Button saveButton)
    {
        saveButton.Location = new Point(Math.Max(0, footer.ClientSize.Width - saveButton.Width), 8);
        resetButton.Location = new Point(Math.Max(0, saveButton.Left - 14 - resetButton.Width), 8);
    }

    private static Panel CreateBrandingCard(string title, int height)
    {
        Panel card = ControlFactory.CreateCardPanel(new Size(0, height));
        card.Dock = DockStyle.Top;
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

    private static void AddLabeledCombo(Control parent, string labelText, ComboBox input, Point labelLocation, int width)
    {
        Label label = ControlFactory.CreateInputLabel(labelText);
        label.Location = labelLocation;
        input.Location = new Point(labelLocation.X, labelLocation.Y + 24);
        input.Size = new Size(width, 30);
        input.Font = FontHelper.Regular(10F);
        parent.Controls.Add(label);
        parent.Controls.Add(input);
    }

    private void WireAddressSelectors()
    {
        _businessRegionComboBox.SelectedIndexChanged -= BusinessRegionComboBox_SelectedIndexChanged;
        _businessProvinceComboBox.SelectedIndexChanged -= BusinessProvinceComboBox_SelectedIndexChanged;
        _businessCityComboBox.SelectedIndexChanged -= BusinessCityComboBox_SelectedIndexChanged;
        _businessRegionComboBox.SelectedIndexChanged += BusinessRegionComboBox_SelectedIndexChanged;
        _businessProvinceComboBox.SelectedIndexChanged += BusinessProvinceComboBox_SelectedIndexChanged;
        _businessCityComboBox.SelectedIndexChanged += BusinessCityComboBox_SelectedIndexChanged;
    }

    private async Task InitializeBusinessAddressSelectorsAsync()
    {
        try
        {
            _isInitializingAddress = true;
            IReadOnlyList<PsgcRegionDto> regions = await _addressService.GetRegionsAsync();
            SetAddressItems(
                _businessRegionComboBox,
                regions.Select(region => new AddressOption(region.Code, region.Name)),
                "Select a region",
                true);

            SystemSettingsModel settings = AppBrandingManager.CurrentSettings;
            if (SelectByName(_businessRegionComboBox, settings.BusinessRegionName)
                && _businessRegionComboBox.SelectedItem is AddressOption region)
            {
                await LoadBusinessProvincesAsync(region.Code, settings.BusinessProvinceName);
                if (_businessProvinceComboBox.SelectedItem is AddressOption province)
                {
                    if (IsNotApplicableProvince(province))
                    {
                        await LoadBusinessCitiesByRegionAsync(region.Code, settings.BusinessCityName);
                    }
                    else
                    {
                        await LoadBusinessCitiesAsync(province.Code, settings.BusinessCityName);
                    }

                    if (_businessCityComboBox.SelectedItem is AddressOption city)
                    {
                        await LoadBusinessBarangaysAsync(city.Code, settings.BusinessBarangayName);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            ResetComboBox(_businessRegionComboBox, "Unable to load regions", false);
            ResetComboBox(_businessProvinceComboBox, "Address lookup unavailable", false);
            ResetComboBox(_businessCityComboBox, "Address lookup unavailable", false);
            ResetComboBox(_businessBarangayComboBox, "Address lookup unavailable", false);
            MessageBoxHelper.ShowWarning(exception.Message, "Address Lookup");
        }
        finally
        {
            _isInitializingAddress = false;
        }
    }

    private async void BusinessRegionComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isInitializingAddress) return;
        if (_businessRegionComboBox.SelectedItem is not AddressOption region)
        {
            ResetComboBox(_businessProvinceComboBox, "Select a region first", false);
            ResetComboBox(_businessCityComboBox, "Select a province first", false);
            ResetComboBox(_businessBarangayComboBox, "Select a city first", false);
            return;
        }

        await LoadBusinessProvincesAsync(region.Code);
    }

    private async void BusinessProvinceComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isInitializingAddress) return;
        if (_businessProvinceComboBox.SelectedItem is not AddressOption province)
        {
            ResetComboBox(_businessCityComboBox, "Select a province first", false);
            ResetComboBox(_businessBarangayComboBox, "Select a city first", false);
            return;
        }

        if (IsNotApplicableProvince(province))
        {
            if (_businessRegionComboBox.SelectedItem is AddressOption region)
                await LoadBusinessCitiesByRegionAsync(region.Code);
            return;
        }

        await LoadBusinessCitiesAsync(province.Code);
    }

    private async void BusinessCityComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isInitializingAddress) return;
        if (_businessCityComboBox.SelectedItem is not AddressOption city)
        {
            ResetComboBox(_businessBarangayComboBox, "Select a city first", false);
            return;
        }

        await LoadBusinessBarangaysAsync(city.Code);
    }

    private async Task LoadBusinessProvincesAsync(string regionCode, string? selectedName = null)
    {
        ResetComboBox(_businessProvinceComboBox, "Loading provinces...", false);
        ResetComboBox(_businessCityComboBox, "Select a province first", false);
        ResetComboBox(_businessBarangayComboBox, "Select a city first", false);

        IReadOnlyList<PsgcProvinceDto> provinces = await _addressService.GetProvincesByRegionAsync(regionCode);
        if (provinces.Count == 0)
        {
            SetNotApplicableProvince();
            await LoadBusinessCitiesByRegionAsync(regionCode, selectedName);
            return;
        }

        SetAddressItems(_businessProvinceComboBox, provinces.Select(province => new AddressOption(province.Code, province.Name)), "Select a province", true);
        SelectByName(_businessProvinceComboBox, selectedName);
    }

    private async Task LoadBusinessCitiesAsync(string provinceCode, string? selectedName = null)
    {
        ResetComboBox(_businessCityComboBox, "Loading cities...", false);
        ResetComboBox(_businessBarangayComboBox, "Select a city first", false);
        IReadOnlyList<PsgcCityMunicipalityDto> cities = await _addressService.GetCitiesByProvinceAsync(provinceCode);
        SetAddressItems(_businessCityComboBox, cities.Select(city => new AddressOption(city.Code, city.Name)), "Select a city / municipality", true);
        SelectByName(_businessCityComboBox, selectedName);
    }

    private async Task LoadBusinessCitiesByRegionAsync(string regionCode, string? selectedName = null)
    {
        ResetComboBox(_businessCityComboBox, "Loading cities...", false);
        ResetComboBox(_businessBarangayComboBox, "Select a city first", false);
        IReadOnlyList<PsgcCityMunicipalityDto> cities = await _addressService.GetCitiesByRegionAsync(regionCode);
        SetAddressItems(_businessCityComboBox, cities.Select(city => new AddressOption(city.Code, city.Name)), "Select a city / municipality", true);
        SelectByName(_businessCityComboBox, selectedName);
    }

    private async Task LoadBusinessBarangaysAsync(string cityCode, string? selectedName = null)
    {
        ResetComboBox(_businessBarangayComboBox, "Loading barangays...", false);
        IReadOnlyList<PsgcBarangayDto> barangays = await _addressService.GetBarangaysByCityAsync(cityCode);
        SetAddressItems(_businessBarangayComboBox, barangays.Select(barangay => new AddressOption(barangay.Code, barangay.Name)), "Select a barangay", true);
        SelectByName(_businessBarangayComboBox, selectedName);
    }

    private void SetNotApplicableProvince()
    {
        _businessProvinceComboBox.BeginUpdate();
        _businessProvinceComboBox.Items.Clear();
        _businessProvinceComboBox.Items.Add(new AddressOption(NotApplicableProvinceCode, NotApplicableProvinceName));
        _businessProvinceComboBox.SelectedIndex = 0;
        _businessProvinceComboBox.Enabled = false;
        _businessProvinceComboBox.EndUpdate();
    }

    private static void ResetComboBox(ComboBox comboBox, string text, bool enabled)
    {
        comboBox.BeginUpdate();
        comboBox.Items.Clear();
        comboBox.Items.Add(text);
        comboBox.SelectedIndex = 0;
        comboBox.Enabled = enabled;
        comboBox.EndUpdate();
    }

    private static void SetAddressItems(ComboBox comboBox, IEnumerable<AddressOption> options, string placeholder, bool enabled)
    {
        comboBox.BeginUpdate();
        comboBox.Items.Clear();
        comboBox.Items.Add(placeholder);
        comboBox.Items.AddRange(options.OrderBy(option => option.Name).Cast<object>().ToArray());
        comboBox.SelectedIndex = 0;
        comboBox.Enabled = enabled;
        comboBox.EndUpdate();
    }

    private static bool SelectByName(ComboBox comboBox, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        AddressOption? option = comboBox.Items
            .OfType<AddressOption>()
            .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (option is null) return false;
        comboBox.SelectedItem = option;
        return true;
    }

    private static bool IsNotApplicableProvince(AddressOption option)
        => string.Equals(option.Code, NotApplicableProvinceCode, StringComparison.Ordinal);

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

    private static void PositionBoundedCard(Panel viewport, Control card)
    {
        int left = Math.Max(0, (viewport.ClientSize.Width - card.Width - SystemInformation.VerticalScrollBarWidth) / 2);
        card.Location = new Point(left, 0);
    }

    private static void ShowAccessDenied()
    {
        MessageBoxHelper.ShowWarning("You do not have permission to perform this action.", "Permission Denied");
    }

    private sealed record AddressOption(string Code, string Name)
    {
        public override string ToString() => Name;
    }
}
