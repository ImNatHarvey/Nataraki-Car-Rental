using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.UserControls.ManageSystem;

public sealed class ManageSystemControl : UserControl
{
    private readonly int _currentUserId;
    private readonly SystemSettingsService _service = new();
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

    public ManageSystemControl(int currentUserId)
    {
        _currentUserId = currentUserId;
        Dock = DockStyle.Fill;
        BackColor = ThemeHelper.ContentBackground;
        Padding = new Padding(32);
        InitializeLayout();
        LoadSettings();
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
        settingsTab.Controls.Add(CreateSystemSettingsPanel());

        TabPage brandingTab = new() { Text = "Branding & Theme", BackColor = ThemeHelper.ContentBackground, Padding = new Padding(16) };
        brandingTab.Controls.Add(CreateBrandingPanel());

        TabPage usersTab = new() { Text = "Users", BackColor = ThemeHelper.ContentBackground, Padding = new Padding(16) };
        usersTab.Controls.Add(CreatePlaceholder("User Management will be implemented in Manage System Phase 2."));

        TabPage rolesTab = new() { Text = "Roles & Permissions", BackColor = ThemeHelper.ContentBackground, Padding = new Padding(16) };
        rolesTab.Controls.Add(CreatePlaceholder("Role-Based Access Control will be implemented in Manage System Phase 2."));

        _tabs.TabPages.Add(settingsTab);
        _tabs.TabPages.Add(brandingTab);
        _tabs.TabPages.Add(usersTab);
        _tabs.TabPages.Add(rolesTab);

        Controls.Add(_tabs);
        Controls.Add(titleLabel);
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
        MessageBoxHelper.ShowInfo("System settings saved successfully.");
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
            MessageBoxHelper.ShowInfo("Branding and theme saved. Some changes may require re-opening the application.");
            
            // Reload the form to apply theme
            if (ParentForm is Forms.Main.MainForm main)
            {
                // Trigger a refresh logic if needed
            }
        }
    }

    private async Task ResetDefaultsAsync()
    {
        if (MessageBoxHelper.Confirm("Are you sure you want to reset system settings to defaults?"))
        {
            await _service.ResetDefaultsAsync(_currentUserId);
            await AppBrandingManager.LoadSettingsAsync();
            LoadSettings();
            MessageBoxHelper.ShowInfo("Settings reset to defaults.");
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
