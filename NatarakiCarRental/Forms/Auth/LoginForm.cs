using System.Threading;
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
    private readonly Panel _brandingPanel = new();
    private readonly Panel _formContainer = new();
    private readonly Panel _loginPanel = new();
    private readonly Panel _resetPanel = new();
    private readonly UserService _userService = new();

    // Reset view controls
    private TextBox txtResetUsername = null!;
    private TextBox txtResetLastName = null!;
    private TextBox txtNewPassword = null!;
    private TextBox txtConfirmPassword = null!;
    private Label lblUserStatus = null!;
    private Button btnResetAction = null!;
    private Button btnBackToLogin = null!;
    private CancellationTokenSource? _resetSearchCts;

    private bool _isReturningFromLogout;

    public LoginForm()
    {
        InitializeLoginForm();
        WireEvents();
    }

    private void WireEvents()
    {
        txtResetUsername.TextChanged += txtResetUsername_TextChanged;
        btnBackToLogin.Click += (s, e) => ShowLoginView();
        btnResetAction.Click += btnResetAction_Click;
    }

    private void InitializeLoginForm()
    {
        Text = AppBrandingManager.CurrentSettings.BusinessName;
        ThemeHelper.ApplyCompactDialogFormSettings(this);
        ApplyWindowIcon();

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

        _formContainer.Dock = DockStyle.Fill;
        _formContainer.BackColor = ThemeHelper.Surface;

        InitializeLoginPanel();
        InitializeResetPanelControls();

        _formContainer.Controls.Add(_loginPanel);
        _formContainer.Controls.Add(_resetPanel);
        _resetPanel.Visible = false;

        rootLayout.Controls.Add(_brandingPanel, 0, 0);
        rootLayout.Controls.Add(_formContainer, 1, 0);

        Controls.Add(rootLayout);
    }

    private void InitializeLoginPanel()
    {
        _loginPanel.Dock = DockStyle.Fill;
        _loginPanel.BackColor = ThemeHelper.Surface;

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

        BorderedPanel passwordFieldPanel = ControlFactory.CreatePasswordFieldPanel(_passwordTextBox, 320);
        passwordFieldPanel.Location = new Point(66, 310);

        Button loginButton = ControlFactory.CreatePrimaryButton("Log In", 320, 40);
        loginButton.Location = new Point(66, 374);
        loginButton.Click += LoginButton_Click;

        LinkLabel forgotPasswordLink = new()
        {
            Text = "Forgot Password?",
            Font = FontHelper.Regular(9F),
            LinkColor = ThemeHelper.Primary,
            ActiveLinkColor = ThemeHelper.Secondary,
            VisitedLinkColor = ThemeHelper.Primary,
            LinkBehavior = LinkBehavior.HoverUnderline,
            Location = new Point(66, 424),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        forgotPasswordLink.Click += (_, _) => ShowResetView();

        _loginPanel.Controls.Add(loginHeadingLabel);
        _loginPanel.Controls.Add(subtextLabel);
        _loginPanel.Controls.Add(usernameLabel);
        _loginPanel.Controls.Add(_usernameTextBox);
        _loginPanel.Controls.Add(passwordLabel);
        _loginPanel.Controls.Add(passwordFieldPanel);
        _loginPanel.Controls.Add(loginButton);
        _loginPanel.Controls.Add(forgotPasswordLink);
        
        AcceptButton = loginButton;
    }

    private void InitializeResetPanelControls()
    {
        _resetPanel.Dock = DockStyle.Fill;
        _resetPanel.BackColor = ThemeHelper.Surface;
        _resetPanel.Padding = new Padding(20);

        Label resetHeadingLabel = new()
        {
            AutoSize = false,
            Text = "Reset Password",
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary,
            Location = new Point(64, 60),
            Size = new Size(320, 32)
        };

        Label subtextLabel = new()
        {
            AutoSize = false,
            Text = "Verify identity to set a new password",
            Font = FontHelper.Regular(9.5F),
            ForeColor = ThemeHelper.TextSecondary,
            Location = new Point(66, 94),
            Size = new Size(320, 24)
        };

        // --- Username Group ---
        Label usernameLabel = ControlFactory.CreateInputLabel("Username");
        usernameLabel.Location = new Point(66, 134);

        txtResetUsername = ControlFactory.CreateTextBox();
        txtResetUsername.Location = new Point(66, 158);
        txtResetUsername.Width = 320;

        lblUserStatus = new Label
        {
            AutoSize = false,
            Text = "Enter username to begin verification",
            Font = FontHelper.SemiBold(8.5F),
            ForeColor = ThemeHelper.TextSecondary,
            Location = new Point(66, 190),
            Size = new Size(320, 18)
        };

        // --- Last Name Group ---
        Label lastNameLabel = ControlFactory.CreateInputLabel("Enter your Last Name to verify identity");
        lastNameLabel.Location = new Point(66, 218);

        txtResetLastName = ControlFactory.CreateTextBox();
        txtResetLastName.Location = new Point(66, 242);
        txtResetLastName.Width = 320;

        // --- New Password Group ---
        Label newPasswordLabel = ControlFactory.CreateInputLabel("New Password");
        newPasswordLabel.Location = new Point(66, 286);

        txtNewPassword = ControlFactory.CreatePasswordTextBox();
        BorderedPanel newPasswordWrapper = ControlFactory.CreatePasswordFieldPanel(txtNewPassword, 320);
        newPasswordWrapper.Location = new Point(66, 310);

        // --- Confirm Password Group ---
        Label confirmPasswordLabel = ControlFactory.CreateInputLabel("Confirm Password");
        confirmPasswordLabel.Location = new Point(66, 354);

        txtConfirmPassword = ControlFactory.CreatePasswordTextBox();
        BorderedPanel confirmPasswordWrapper = ControlFactory.CreatePasswordFieldPanel(txtConfirmPassword, 320);
        confirmPasswordWrapper.Location = new Point(66, 378);

        // --- Action Buttons ---
        // btnResetAction is positioned at the bottom right of the 320px input group (66 + 320 - 160 = 226)
        btnResetAction = ControlFactory.CreatePrimaryButton("Reset Password", 160, 40);
        btnResetAction.Location = new Point(226, 428);

        // btnBackToLogin is positioned at the bottom left, styled as a clickable link
        btnBackToLogin = new Button
        {
            Text = "Back to Login",
            Font = FontHelper.SemiBold(9F),
            ForeColor = ThemeHelper.Primary,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(66, 433), // Vertically offset for alignment with the action button text
            Size = new Size(120, 30),
            Cursor = Cursors.Hand
        };
        btnBackToLogin.FlatAppearance.BorderSize = 0;
        btnBackToLogin.FlatAppearance.MouseOverBackColor = Color.Transparent;
        btnBackToLogin.FlatAppearance.MouseDownBackColor = Color.Transparent;

        _resetPanel.Controls.Add(resetHeadingLabel);
        _resetPanel.Controls.Add(subtextLabel);
        _resetPanel.Controls.Add(usernameLabel);
        _resetPanel.Controls.Add(txtResetUsername);
        _resetPanel.Controls.Add(lblUserStatus);
        _resetPanel.Controls.Add(lastNameLabel);
        _resetPanel.Controls.Add(txtResetLastName);
        _resetPanel.Controls.Add(newPasswordLabel);
        _resetPanel.Controls.Add(newPasswordWrapper);
        _resetPanel.Controls.Add(confirmPasswordLabel);
        _resetPanel.Controls.Add(confirmPasswordWrapper);
        _resetPanel.Controls.Add(btnResetAction);
        _resetPanel.Controls.Add(btnBackToLogin);
    }

    private void ShowResetView()
    {
        _loginPanel.Visible = false;
        _resetPanel.Visible = true;
        _resetPanel.BringToFront();

        txtResetUsername.Text = _usernameTextBox.Text;
        
        this.AcceptButton = btnResetAction;
        this.Invalidate();
        this.Refresh();
        
        txtResetUsername.Focus();
    }

    private void ShowLoginView()
    {
        _resetPanel.Visible = false;
        _loginPanel.Visible = true;
        _loginPanel.BringToFront();

        _usernameTextBox.Text = txtResetUsername.Text;
        
        this.AcceptButton = _loginPanel.Controls.OfType<Button>().FirstOrDefault(b => b.Text == "Log In");
        this.Invalidate();
        this.Refresh();
        
        _usernameTextBox.Focus();
    }

    private async void txtResetUsername_TextChanged(object? sender, EventArgs e)
    {
        _resetSearchCts?.Cancel();
        _resetSearchCts = new CancellationTokenSource();
        var token = _resetSearchCts.Token;

        try
        {
            await Task.Delay(500, token);
            
            string username = txtResetUsername.Text.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                lblUserStatus.Text = "Enter username to begin verification";
                lblUserStatus.ForeColor = ThemeHelper.TextSecondary;
                return;
            }

            bool exists = await _userService.CheckUserExistsAsync(username);
            
            if (token.IsCancellationRequested) return;

            if (exists)
            {
                lblUserStatus.Text = "User found. Please verify your last name.";
                lblUserStatus.ForeColor = ThemeHelper.Primary;
            }
            else
            {
                lblUserStatus.Text = "User not found";
                lblUserStatus.ForeColor = ThemeHelper.Error;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (!token.IsCancellationRequested)
            {
                lblUserStatus.Text = "Error verifying user";
                lblUserStatus.ForeColor = ThemeHelper.Error;
            }
        }
    }

    private async void btnResetAction_Click(object? sender, EventArgs e)
    {
        try
        {
            string username = txtResetUsername.Text.Trim();
            string lastName = txtResetLastName.Text.Trim();
            string newPassword = txtNewPassword.Text;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(newPassword))
            {
                MessageBoxHelper.ShowWarning("Please fill in all fields.");
                return;
            }

            if (txtNewPassword.Text != txtConfirmPassword.Text)
            {
                MessageBoxHelper.ShowWarning("Passwords do not match.");
                return;
            }

            await _userService.ResetPasswordAsync(username, lastName, newPassword);
            MessageBoxHelper.ShowInfo("Password has been reset successfully. You can now log in.");
            
            _passwordTextBox.Clear();
            txtResetLastName.Clear();
            txtNewPassword.Clear();
            txtConfirmPassword.Clear();
            ShowLoginView();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Reset failed: {exception.Message}");
        }
    }

    private void RefreshBrandingPanel()
    {
        DisposeBrandingPanelControls();
        Text = AppBrandingManager.CurrentSettings.BusinessName;
        ApplyWindowIcon();

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

    private void ApplyWindowIcon()
    {
        System.Drawing.Icon? icon = BrandingHelper.LoadCurrentWindowIcon();
        if (icon is null)
        {
            return;
        }

        Icon = icon;
    }

    private async void LoginButton_Click(object? sender, EventArgs e)
    {
        try
        {
            string username = _usernameTextBox.Text.Trim();
            string password = _passwordTextBox.Text;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBoxHelper.ShowWarning("Please enter both username and password.");
                return;
            }

            User? user = await _authService.LoginAsync(username, password);

            if (user is null)
            {
                MessageBoxHelper.ShowError("Invalid username or password.");
                _passwordTextBox.Clear();
                _passwordTextBox.Focus();
                return;
            }

            await new ActivityLogService().LogAsync(
                action: "Logged In",
                module: "Authentication",
                entityId: user.UserId,
                description: $"User {user.Username} ({user.FullName}) logged into the system.",
                userId: user.UserId,
                entityName: user.FullName);

            OpenMainForm(user);
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Login failed.\n\n{exception.Message}");
        }
    }

    private void OpenMainForm(User user)
    {
        Hide();

        MainForm mainForm = new(user);
        
        void OnLoggedOut(object? s, EventArgs e)
        {
            mainForm.LoggedOut -= OnLoggedOut;
            RefreshBrandingPanel();
            _isReturningFromLogout = true;
            _passwordTextBox.Clear();
            Show();
            _passwordTextBox.Focus();
        }

        mainForm.LoggedOut += OnLoggedOut;
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
