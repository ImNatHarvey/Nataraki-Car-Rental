using FontAwesome.Sharp;
using NatarakiCarRental.Forms.Main;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.Forms.Auth;

public sealed class LoginForm : Form
{
    private readonly AuthService _authService = new();
    private readonly TextBox _usernameTextBox = ControlFactory.CreateTextBox();
    private readonly TextBox _passwordTextBox = ControlFactory.CreatePasswordTextBox();
    private readonly BorderedPanel _passwordFieldPanel = new();
    private readonly IconButton _passwordPreviewButton = new();
    private readonly Panel _brandingPanel = new();
    private bool _isReturningFromLogout;

    public LoginForm()
    {
        InitializeLoginForm();
    }

    private void InitializeLoginForm()
    {
        Text = AppBrandingManager.CurrentSettings.BusinessName;
        ThemeHelper.ApplyCompactDialogFormSettings(this);
        ApplyWindowIcon(this);

        TableLayoutPanel rootLayout = new()
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.Surface,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0)
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 410F));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _brandingPanel.Dock = DockStyle.Fill;
        _brandingPanel.BackColor = ThemeHelper.ContentBackground;
        RefreshBrandingPanel();

        Panel formPanel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.Surface
        };

        Label loginHeadingLabel = new()
        {
            AutoSize = false,
            Text = "Log In",
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary,
            Location = new Point(64, 116),
            Size = new Size(320, 32)
        };

        Label subtextLabel = new()
        {
            AutoSize = false,
            Text = "Sign in with your system account",
            Font = FontHelper.Regular(9.5F),
            ForeColor = ThemeHelper.TextSecondary,
            Location = new Point(66, 154),
            Size = new Size(320, 24)
        };

        Label usernameLabel = ControlFactory.CreateInputLabel("Username");
        usernameLabel.Location = new Point(66, 206);
        _usernameTextBox.Location = new Point(66, 230);
        _usernameTextBox.Width = 320;

        Label passwordLabel = ControlFactory.CreateInputLabel("Password");
        passwordLabel.Location = new Point(66, 286);

        ConfigurePasswordFieldPanel();
        _passwordFieldPanel.Location = new Point(66, 310);
        ConfigurePasswordPreviewButton();

        Button loginButton = ControlFactory.CreatePrimaryButton("Log In", 320, 40);
        loginButton.Location = new Point(66, 374);
        loginButton.Click += LoginButton_Click;

        formPanel.Controls.Add(loginHeadingLabel);
        formPanel.Controls.Add(subtextLabel);
        formPanel.Controls.Add(usernameLabel);
        formPanel.Controls.Add(_usernameTextBox);
        formPanel.Controls.Add(passwordLabel);
        formPanel.Controls.Add(_passwordFieldPanel);
        formPanel.Controls.Add(loginButton);

        rootLayout.Controls.Add(_brandingPanel, 0, 0);
        rootLayout.Controls.Add(formPanel, 1, 0);

        Controls.Add(rootLayout);
        AcceptButton = loginButton;
    }

    private void RefreshBrandingPanel()
    {
        DisposeBrandingPanelControls();
        Text = AppBrandingManager.CurrentSettings.BusinessName;
        ApplyWindowIcon(this);

        if (AppBrandingManager.CurrentSettings.UseCustomLoginPoster &&
            !string.IsNullOrWhiteSpace(AppBrandingManager.CurrentSettings.LoginPosterPath) &&
            File.Exists(AppBrandingManager.CurrentSettings.LoginPosterPath))
        {
            PictureBox posterBox = new()
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                ImageLocation = AppBrandingManager.CurrentSettings.LoginPosterPath,
                BackColor = ThemeHelper.ContentBackground
            };
            _brandingPanel.Controls.Add(posterBox);
            return;
        }

        Control brandIcon = CreateBrandIcon(new Point(56, 140), new Size(52, 52), ThemeHelper.ContentBackground);

        Label titleLabel = new()
        {
            AutoSize = false,
            Text = AppBrandingManager.CurrentSettings.BusinessName,
            Font = FontHelper.Title(20F),
            ForeColor = ThemeHelper.Primary,
            Location = new Point(56, 204),
            Size = new Size(330, 34),
            TextAlign = ContentAlignment.MiddleLeft
        };

        Label descriptionLabel = new()
        {
            AutoSize = false,
            Text = AppBrandingManager.CurrentSettings.LoginDescription,
            Font = FontHelper.Regular(10.5F),
            ForeColor = ThemeHelper.TextSecondary,
            Location = new Point(58, 248),
            Size = new Size(300, 50),
            TextAlign = ContentAlignment.MiddleLeft
        };

        Panel accentLine = new()
        {
            BackColor = ThemeHelper.Primary,
            Location = new Point(58, 318),
            Size = new Size(72, 3)
        };

        _brandingPanel.Controls.Add(brandIcon);
        _brandingPanel.Controls.Add(titleLabel);
        _brandingPanel.Controls.Add(descriptionLabel);
        _brandingPanel.Controls.Add(accentLine);
    }

    private void DisposeBrandingPanelControls()
    {
        foreach (Control control in _brandingPanel.Controls)
        {
            control.Dispose();
        }

        _brandingPanel.Controls.Clear();
    }

    private static Control CreateBrandIcon(Point location, Size size, Color backColor)
    {
        Image? logoImage = BrandingHelper.LoadCurrentLogoImage();
        if (logoImage is not null)
        {
            return new PictureBox
            {
                Location = location,
                Size = size,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = logoImage,
                BackColor = backColor
            };
        }

        return new IconPictureBox
        {
            IconChar = BrandingHelper.ResolveCurrentBuiltInLogoIcon(),
            IconColor = ThemeHelper.Primary,
            IconSize = 46,
            BackColor = backColor,
            Location = location,
            Size = size
        };
    }

    private static void ApplyWindowIcon(Form form)
    {
        System.Drawing.Icon? icon = BrandingHelper.LoadCurrentWindowIcon();
        if (icon is null)
        {
            return;
        }

        form.Icon = icon;
    }

    private async void LoginButton_Click(object? sender, EventArgs e)
    {
        try
        {
            User? user = await _authService.LoginAsync(_usernameTextBox.Text, _passwordTextBox.Text);

            if (user is null)
            {
                MessageBoxHelper.ShowError("Invalid username or password.");
                _passwordTextBox.Clear();
                _passwordTextBox.Focus();
                return;
            }

            OpenMainForm(user);
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Login failed.\n\n{exception.Message}");
        }
    }

    private void ConfigurePasswordPreviewButton()
    {
        _passwordPreviewButton.Size = new Size(34, 28);
        _passwordPreviewButton.Location = new Point(285, 1);
        _passwordPreviewButton.IconChar = IconChar.Eye;
        _passwordPreviewButton.IconColor = ThemeHelper.TextSecondary;
        _passwordPreviewButton.IconSize = 16;
        _passwordPreviewButton.BackColor = ThemeHelper.Surface;
        _passwordPreviewButton.ForeColor = ThemeHelper.TextPrimary;
        _passwordPreviewButton.FlatStyle = FlatStyle.Flat;
        _passwordPreviewButton.Cursor = Cursors.Hand;
        _passwordPreviewButton.TabStop = false;
        _passwordPreviewButton.Text = string.Empty;
        _passwordPreviewButton.FlatAppearance.BorderSize = 0;
        _passwordPreviewButton.FlatAppearance.MouseOverBackColor = ThemeHelper.ContentBackground;
        _passwordPreviewButton.FlatAppearance.MouseDownBackColor = ThemeHelper.Secondary;
        _passwordPreviewButton.Click += (_, _) => TogglePasswordPreview();
    }

    private void ConfigurePasswordFieldPanel()
    {
        _passwordFieldPanel.Size = new Size(320, 30);
        _passwordFieldPanel.BackColor = ThemeHelper.Surface;
        _passwordFieldPanel.BorderColor = ThemeHelper.Border;
        _passwordFieldPanel.Cursor = Cursors.IBeam;
        _passwordFieldPanel.Click += (_, _) => _passwordTextBox.Focus();

        _passwordTextBox.BorderStyle = BorderStyle.None;
        _passwordTextBox.BackColor = ThemeHelper.Surface;
        _passwordTextBox.Location = new Point(8, 6);
        _passwordTextBox.Width = 272;
        _passwordTextBox.Cursor = Cursors.IBeam;

        _passwordFieldPanel.Controls.Add(_passwordTextBox);
        _passwordFieldPanel.Controls.Add(_passwordPreviewButton);
    }

    private void TogglePasswordPreview()
    {
        bool showPassword = _passwordTextBox.UseSystemPasswordChar;
        _passwordTextBox.UseSystemPasswordChar = !showPassword;
        _passwordPreviewButton.IconChar = showPassword ? IconChar.EyeSlash : IconChar.Eye;
        _passwordTextBox.Focus();
    }

    private void OpenMainForm(User user)
    {
        Hide();

        MainForm mainForm = new(user);
        mainForm.LoggedOut += (_, _) =>
        {
            RefreshBrandingPanel();
            _isReturningFromLogout = true;
            _passwordTextBox.Clear();
            Show();
            _passwordTextBox.Focus();
        };
        mainForm.FormClosed += (_, _) =>
        {
            if (_isReturningFromLogout)
            {
                _isReturningFromLogout = false;
                return;
            }

            if (!Visible)
            {
                Close();
            }
        };

        mainForm.Show();
    }
}
