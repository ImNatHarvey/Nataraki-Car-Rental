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

    public event EventHandler? LoggedOut;

    public MainForm(User currentUser)
    {
        CurrentUser = currentUser;
        InitializeMainForm();
        ShowOverview();
        
        AppBrandingManager.SettingsUpdated += (_, _) => UpdateBranding();
    }

    private User CurrentUser { get; }

    private void UpdateBranding()
    {
        _brandLabel.Text = AppBrandingManager.CurrentSettings.BusinessName;
        SetActiveNavigation(_navigationButtons.FirstOrDefault(b => b.BackColor != Color.Transparent)?.Text ?? "Overview");
    }

    private void InitializeMainForm()
    {
        Text = string.Empty;
        ThemeHelper.ApplyStandardMainFormSettings(this);

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

        IconPictureBox brandIcon = new()
        {
            IconChar = IconChar.Car,
            IconColor = ThemeHelper.Primary,
            IconSize = 30,
            BackColor = ThemeHelper.Surface,
            Location = new Point(0, 8),
            Size = new Size(34, 34)
        };

        _brandLabel.Text = AppBrandingManager.CurrentSettings.BusinessName;
        _brandLabel.AutoSize = false;
        _brandLabel.Location = new Point(42, 4);
        _brandLabel.Size = new Size(220, 44);
        _brandLabel.Font = FontHelper.Title(12F);
        _brandLabel.ForeColor = ThemeHelper.TextPrimary;
        _brandLabel.TextAlign = ContentAlignment.MiddleLeft;

        Label userLabel = new()
        {
            Text = $"Signed in: {CurrentUser.FirstName}",
            AutoSize = false,
            Height = 34,
            Dock = DockStyle.Top,
            ForeColor = ThemeHelper.TextSecondary,
            TextAlign = ContentAlignment.MiddleLeft
        };

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
            new("Customers", IconChar.Users, AccessControlService.HasPermission("Customers.View")),
            new("Car Garage", IconChar.Car, AccessControlService.HasPermission("Cars.View")),
            new("Offsite", IconChar.LocationDot, AccessControlService.HasPermission("Offsite.View")),
            new("Activity Log", IconChar.ClipboardList, AccessControlService.HasPermission("ActivityLog.View")),
            new("Reports & Analytics", IconChar.ChartColumn, AccessControlService.HasPermission("Reports.View")),
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

        IconButton logoutButton = ControlFactory.CreateSidebarButton("Log Out", IconChar.RightFromBracket, isDanger: true);
        logoutButton.Dock = DockStyle.Bottom;
        logoutButton.Width = 228;
        logoutButton.Click += LogoutButton_Click;

        brandPanel.Controls.Add(brandIcon);
        brandPanel.Controls.Add(_brandLabel);

        sidebarPanel.Controls.Add(menuPanel);
        sidebarPanel.Controls.Add(userLabel);
        sidebarPanel.Controls.Add(brandPanel);
        sidebarPanel.Controls.Add(logoutButton);

        _contentPanel.Dock = DockStyle.Fill;
        _contentPanel.BackColor = ThemeHelper.ContentBackground;

        Controls.Add(_contentPanel);
        Controls.Add(sidebarPanel);
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
        LoadContent(new ManageSystemControl(CurrentUser.UserId));
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

    private void LogoutButton_Click(object? sender, EventArgs e)
    {
        if (!MessageBoxHelper.Confirm("Are you sure you want to log out?"))
        {
            return;
        }

        AccessControlService.Logout();
        LoggedOut?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private sealed record NavigationItem(string Text, IconChar Icon, bool IsImplemented);
}
