using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.ActivityLogs;
using NatarakiCarRental.UserControls.Cars;
using NatarakiCarRental.UserControls.Customers;
using NatarakiCarRental.UserControls.Dashboard;
using NatarakiCarRental.UserControls.FleetSchedule;
using NatarakiCarRental.UserControls.ManageSystem;
using NatarakiCarRental.UserControls.Offsite;
using NatarakiCarRental.UserControls.Reports;
using NatarakiCarRental.UserControls.Transactions;

namespace NatarakiCarRental.Forms.Main;

public sealed class MainForm : Form
{
    private readonly Panel _contentPanel = new();
    private readonly List<IconButton> _navigationButtons = [];
    private readonly Label _brandLabel = new();
    private readonly Panel _brandIconHost = new();
    private readonly Panel _identityHost = new();
    private readonly Panel _identityPanel = new();
    private readonly PictureBox _identityAvatar = new();
    private readonly Label _identityNameLabel = new();

    public event EventHandler? LoggedOut;

    public MainForm(User currentUser)
    {
        CurrentUser = currentUser;
        InitializeMainForm();
        ShowOverview();
        FormClosed += (_, _) => _identityAvatar.Image?.Dispose();
        
        AppBrandingManager.SettingsUpdated += (_, _) => UpdateBranding();
    }

    private User CurrentUser { get; }

    private void UpdateBranding()
    {
        Text = AppBrandingManager.CurrentSettings.BusinessName;
        _brandLabel.Text = AppBrandingManager.CurrentSettings.BusinessName;
        ApplyWindowIcon();
        RenderBrandIcon();
        SetActiveNavigation(_navigationButtons.FirstOrDefault(b => b.BackColor != Color.Transparent)?.Text ?? "Overview");
    }

    private void InitializeMainForm()
    {
        Text = AppBrandingManager.CurrentSettings.BusinessName;
        ThemeHelper.ApplyStandardMainFormSettings(this);
        ApplyWindowIcon();

        Panel sidebarPanel = new()
        {
            BackColor = ThemeHelper.Surface,
            Dock = DockStyle.Left,
            Width = 280,
            Padding = new Padding(16, 22, 16, 16)
        };

        Panel brandPanel = new()
        {
            Dock = DockStyle.Top,
            Height = 64
        };

        _brandIconHost.Location = new Point(0, 8);
        _brandIconHost.Size = new Size(34, 34);
        _brandIconHost.BackColor = ThemeHelper.Surface;
        RenderBrandIcon();

        _brandLabel.Text = AppBrandingManager.CurrentSettings.BusinessName;
        _brandLabel.AutoSize = false;
        _brandLabel.Location = new Point(42, 4);
        _brandLabel.Size = new Size(220, 44);
        _brandLabel.Font = FontHelper.Title(12F);
        _brandLabel.ForeColor = ThemeHelper.TextPrimary;
        _brandLabel.TextAlign = ContentAlignment.MiddleLeft;

        ConfigureIdentityPanel();

        FlowLayoutPanel menuPanel = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 28, 0, 0)
        };

        NavigationItem[] menuItems =
        [
            new("Overview", IconChar.ChartLine, AccessControlService.HasPermission("Overview.View")),
            new("Fleet Schedule", IconChar.CalendarDays, AccessControlService.HasPermission("FleetSchedule.View")),
            new("Transactions", IconChar.Receipt, AccessControlService.HasPermission("Transactions.View")),
            new("Offsite", IconChar.LocationDot, AccessControlService.HasPermission("Offsite.View")),
            new("Customers", IconChar.Users, AccessControlService.HasPermission("Customers.View")),
            new("Car Garage", IconChar.Car, AccessControlService.HasPermission("Cars.View")),
            new("Reports & Analytics", IconChar.ChartColumn, AccessControlService.HasPermission("Reports.View")),
            new("Activity Log", IconChar.ClipboardList, AccessControlService.HasPermission("ActivityLog.View")),
            new("Manage System", IconChar.Gear, AccessControlService.HasPermission("ManageSystem.View"))
        ];

        string? firstAvailablePage = null;

        foreach (NavigationItem menuItem in menuItems)
        {
            if (!menuItem.IsImplemented) continue;

            firstAvailablePage ??= menuItem.Text;

            IconButton button = ControlFactory.CreateSidebarButton(menuItem.Text, menuItem.Icon);
            button.Click += (_, _) => Navigate(menuItem.Text);
            
            _navigationButtons.Add(button);
            menuPanel.Controls.Add(button);
        }

        if (firstAvailablePage != null)
        {
            Navigate(firstAvailablePage);
        }
        else
        {
            ShowPlaceholder("No Access");
        }

        Panel logoutHost = new()
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = ThemeHelper.Surface
        };

        IconButton logoutButton = ControlFactory.CreateSidebarButton("Log Out", IconChar.RightFromBracket, isDanger: true);
        logoutButton.Location = new Point(0, 0);
        logoutButton.Click += LogoutButton_Click;
        logoutHost.Controls.Add(logoutButton);

        brandPanel.Controls.Add(_brandIconHost);
        brandPanel.Controls.Add(_brandLabel);

        sidebarPanel.Controls.Add(menuPanel);
        sidebarPanel.Controls.Add(_identityHost);
        sidebarPanel.Controls.Add(brandPanel);
        sidebarPanel.Controls.Add(logoutHost);

        _contentPanel.Dock = DockStyle.Fill;
        _contentPanel.BackColor = ThemeHelper.ContentBackground;

        Controls.Add(_contentPanel);
        Controls.Add(sidebarPanel);
    }

    private void RenderBrandIcon()
    {
        foreach (Control control in _brandIconHost.Controls)
        {
            control.Dispose();
        }

        _brandIconHost.Controls.Clear();
        Image? logoImage = BrandingHelper.LoadCurrentLogoImage();
        if (logoImage is not null)
        {
            _brandIconHost.Controls.Add(new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = logoImage,
                BackColor = ThemeHelper.Surface
            });
            return;
        }

        _brandIconHost.Controls.Add(new IconPictureBox
        {
            Dock = DockStyle.Fill,
            IconChar = BrandingHelper.ResolveCurrentBuiltInLogoIcon(),
            IconColor = ThemeHelper.Primary,
            IconSize = 30,
            BackColor = ThemeHelper.Surface
        });
    }

    private void ConfigureIdentityPanel()
    {
        _identityHost.Dock = DockStyle.Top;
        _identityHost.Height = 50;
        _identityHost.BackColor = ThemeHelper.Surface;

        _identityPanel.Size = new Size(228, 42);
        _identityPanel.Location = new Point(0, 0);
        _identityPanel.Padding = new Padding(14, 0, 0, 0);
        _identityPanel.BackColor = Color.Transparent;
        _identityPanel.Cursor = Cursors.Hand;
        _identityPanel.Click += (_, _) => ShowManageSystem("Profile");
        _identityPanel.MouseEnter += IdentitySurface_MouseEnter;
        _identityPanel.MouseLeave += IdentitySurface_MouseLeave;

        _identityAvatar.Location = new Point(14, 6);
        _identityAvatar.Size = new Size(30, 30);
        _identityAvatar.SizeMode = PictureBoxSizeMode.Zoom;
        _identityAvatar.BackColor = Color.Transparent;
        _identityAvatar.Cursor = Cursors.Hand;
        _identityAvatar.Click += (_, _) => ShowManageSystem("Profile");
        _identityAvatar.MouseEnter += IdentitySurface_MouseEnter;
        _identityAvatar.MouseLeave += IdentitySurface_MouseLeave;

        _identityNameLabel.AutoSize = false;
        _identityNameLabel.Location = new Point(54, 1);
        _identityNameLabel.Size = new Size(158, 40);
        _identityNameLabel.Font = FontHelper.SemiBold(9.5F);
        _identityNameLabel.ForeColor = ThemeHelper.TextPrimary;
        _identityNameLabel.TextAlign = ContentAlignment.MiddleLeft;
        _identityNameLabel.AutoEllipsis = true;
        _identityNameLabel.BackColor = Color.Transparent;
        _identityNameLabel.Cursor = Cursors.Hand;
        _identityNameLabel.Click += (_, _) => ShowManageSystem("Profile");
        _identityNameLabel.MouseEnter += IdentitySurface_MouseEnter;
        _identityNameLabel.MouseLeave += IdentitySurface_MouseLeave;

        _identityPanel.Controls.Add(_identityAvatar);
        _identityPanel.Controls.Add(_identityNameLabel);
        _identityHost.Controls.Add(_identityPanel);
        RenderIdentity();
    }

    private void IdentitySurface_MouseEnter(object? sender, EventArgs e)
    {
        _identityPanel.BackColor = ThemeHelper.Secondary;
    }

    private void IdentitySurface_MouseLeave(object? sender, EventArgs e)
    {
        Point cursorLocation = _identityPanel.PointToClient(Cursor.Position);
        if (!_identityPanel.ClientRectangle.Contains(cursorLocation))
        {
            _identityPanel.BackColor = Color.Transparent;
        }
    }

    private void RenderIdentity()
    {
        _identityNameLabel.Text = string.IsNullOrWhiteSpace(CurrentUser.FullName)
            ? CurrentUser.Username
            : CurrentUser.FullName;

        Image? previousImage = _identityAvatar.Image;
        _identityAvatar.Image = UserAvatarHelper.CreateAvatar(CurrentUser, 30);
        previousImage?.Dispose();
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

    private void Navigate(string pageName)
    {
        if (pageName == "Overview")
        {
            ShowOverview();
            return;
        }

        if (pageName == "Car Garage")
        {
            ShowCarGarage();
            return;
        }

        if (pageName == "Customers")
        {
            ShowCustomers();
            return;
        }

        if (pageName == "Fleet Schedule")
        {
            ShowFleetSchedule();
            return;
        }

        if (pageName == "Activity Log")
        {
            ShowActivityLog();
            return;
        }

        if (pageName == "Transactions")
        {
            ShowTransactions();
            return;
        }

        if (pageName == "Offsite")
        {
            ShowOffsite();
            return;
        }

        if (pageName == "Reports & Analytics")
        {
            ShowReports();
            return;
        }

        if (pageName == "Manage System")
        {
            ShowManageSystem();
            return;
        }

        ShowPlaceholder(pageName);
    }

    private void ShowOverview()
    {
        LoadContent(new OverviewControl());
        SetActiveNavigation("Overview");
    }

    private void ShowManageSystem()
    {
        ShowManageSystem(null);
    }

    private void ShowManageSystem(string? initialTabKey)
    {
        ManageSystemControl control = new(CurrentUser.UserId, initialTabKey);
        control.ProfileUpdated += (_, updatedUser) =>
        {
            CurrentUser.Username = updatedUser.Username;
            CurrentUser.FirstName = updatedUser.FirstName;
            CurrentUser.LastName = updatedUser.LastName;
            CurrentUser.ProfileImagePath = updatedUser.ProfileImagePath;
            RenderIdentity();
        };
        LoadContent(control);
        SetActiveNavigation("Manage System");
    }

    private void ShowCarGarage()
    {
        LoadContent(new CarGarageControl(CurrentUser.UserId));
        SetActiveNavigation("Car Garage");
    }

    private void ShowCustomers()
    {
        LoadContent(new CustomerControl(CurrentUser.UserId));
        SetActiveNavigation("Customers");
    }

    private void ShowFleetSchedule()
    {
        LoadContent(new FleetScheduleControl(CurrentUser.UserId));
        SetActiveNavigation("Fleet Schedule");
    }

    private void ShowActivityLog()
    {
        LoadContent(new ActivityLogControl());
        SetActiveNavigation("Activity Log");
    }

    private void ShowTransactions()
    {
        LoadContent(new TransactionControl(CurrentUser.UserId));
        SetActiveNavigation("Transactions");
    }

    private void ShowReports()
    {
        LoadContent(new ReportsControl());
        SetActiveNavigation("Reports & Analytics");
    }

    private void ShowOffsite()
    {
        LoadContent(new OffsiteControl(CurrentUser.UserId));
        SetActiveNavigation("Offsite");
    }

    private void ShowPlaceholder(string pageName)
    {
        UserControl placeholderControl = CreatePlaceholderControl(pageName);
        LoadContent(placeholderControl);
        SetActiveNavigation(pageName);
    }

    private void LoadContent(Control control)
    {
        foreach (Control c in _contentPanel.Controls)
        {
            c.Dispose();
        }
        _contentPanel.Controls.Clear();
        control.Dock = DockStyle.Fill;
        _contentPanel.Controls.Add(control);
    }

    private static UserControl CreatePlaceholderControl(string pageName)
    {
        UserControl control = new()
        {
            BackColor = ThemeHelper.ContentBackground,
            Padding = new Padding(32)
        };

        Label titleLabel = new()
        {
            Text = pageName,
            Dock = DockStyle.Top,
            Height = 48,
            Font = FontHelper.Title(20F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Panel placeholderCard = ControlFactory.CreateCardPanel(new Size(0, 160));
        placeholderCard.Dock = DockStyle.Top;
        placeholderCard.Padding = new Padding(28);

        Label placeholderLabel = new()
        {
            Text = $"{pageName} module placeholder. This section will be built in a later step.",
            Dock = DockStyle.Fill,
            Font = FontHelper.Regular(12F),
            ForeColor = ThemeHelper.TextSecondary,
            TextAlign = ContentAlignment.MiddleLeft
        };

        placeholderCard.Controls.Add(placeholderLabel);
        control.Controls.Add(placeholderCard);
        control.Controls.Add(titleLabel);

        return control;
    }

    private void SetActiveNavigation(string pageName)
    {
        foreach (IconButton button in _navigationButtons)
        {
            bool isActive = button.Text == pageName;
            button.BackColor = isActive ? ThemeHelper.Secondary : Color.Transparent;
            button.IconColor = isActive ? ThemeHelper.Primary : ThemeHelper.TextSecondary;
            button.ForeColor = isActive ? ThemeHelper.Primary : ThemeHelper.TextPrimary;
        }
    }

    private async void LogoutButton_Click(object? sender, EventArgs e)
    {
        if (!MessageBoxHelper.Confirm("Are you sure you want to log out?"))
        {
            return;
        }

        AccessControlService.Logout();
        await AppBrandingManager.LoadSettingsAsync();
        LoggedOut?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private sealed record NavigationItem(string Text, IconChar Icon, bool IsImplemented);
}
