using NatarakiCarRental.Helpers;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.UserControls.Reports;

public sealed class ReportsControl : UserControl
{
    private readonly DateTimePicker _fromDatePicker = CreateDatePicker();
    private readonly DateTimePicker _toDatePicker = CreateDatePicker();
    private readonly Button _applyFilterButton = ControlFactory.CreatePrimaryButton("Apply Filter", 120, 32);
    private readonly TabControl _reportTabs = new();
    private readonly Dictionary<TabPage, IReportTab> _tabs = [];
    private readonly HashSet<IReportTab> _loadedTabs = [];

    public ReportsControl()
    {
        InitializeControl();
        Load += ReportsControl_Load;
    }

    private void InitializeControl()
    {
        BackColor = ThemeHelper.ContentBackground;
        Dock = DockStyle.Fill;
        Padding = new Padding(32, 8, 32, 32);

        TableLayoutPanel mainLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.Controls.Add(CreateFilterPanel(), 0, 0);
        mainLayout.Controls.Add(CreateTabControl(), 0, 1);
        Controls.Add(mainLayout);
    }

    private Panel CreateFilterPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        Label fromLabel = new() { Text = "From:", AutoSize = true, Location = new Point(0, 14), Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextSecondary };
        _fromDatePicker.Location = new Point(45, 10);
        _fromDatePicker.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        Label toLabel = new() { Text = "To:", AutoSize = true, Location = new Point(170, 14), Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextSecondary };
        _toDatePicker.Location = new Point(200, 10);
        _toDatePicker.Value = DateTime.Today;

        _applyFilterButton.Location = new Point(330, 9);
        _applyFilterButton.Click += async (_, _) =>
        {
            _loadedTabs.Clear();
            await RefreshSelectedTabAsync(forceReload: true);
        };

        panel.Controls.Add(fromLabel);
        panel.Controls.Add(_fromDatePicker);
        panel.Controls.Add(toLabel);
        panel.Controls.Add(_toDatePicker);
        panel.Controls.Add(_applyFilterButton);
        return panel;
    }

    private TabControl CreateTabControl()
    {
        _reportTabs.Dock = DockStyle.Fill;
        _reportTabs.Font = FontHelper.SemiBold(10F);
        _reportTabs.SelectedIndexChanged += async (_, _) => await RefreshSelectedTabAsync(forceReload: false);

        AddReportTab("Overview", new ReportsOverviewTab());
        AddReportTab("Financial", new ReportsFinancialTab());
        AddReportTab("Fleet Performance", new ReportsFleetPerformanceTab());
        AddReportTab("Operations", new ReportsOperationsTab());
        AddReportTab("Customers", new ReportsCustomersTab());
        
        if (AccessControlService.HasPermission("Reports.Export"))
        {
            AddReportTab("Exports", new ReportsExportsTab());
        }

        return _reportTabs;
    }

    private void AddReportTab(string title, UserControl control)
    {
        if (control is not IReportTab reportTab)
        {
            throw new InvalidOperationException($"{control.GetType().Name} must implement IReportTab.");
        }

        TabPage page = new(title)
        {
            BackColor = ThemeHelper.ContentBackground
        };

        control.Dock = DockStyle.Fill;
        page.Controls.Add(control);
        _reportTabs.TabPages.Add(page);
        _tabs[page] = reportTab;
    }

    private async void ReportsControl_Load(object? sender, EventArgs e)
    {
        Load -= ReportsControl_Load;

        if (!AccessControlService.HasPermission("Reports.View"))
        {
            ShowPermissionDenied();
            return;
        }

        await RefreshSelectedTabAsync(forceReload: true);
    }

    private static void ShowPermissionDenied()
    {
        MessageBoxHelper.ShowWarning("You do not have permission to access this feature.", "Permission Denied");
    }

    private async Task RefreshSelectedTabAsync(bool forceReload)
    {
        if (_reportTabs.SelectedTab is null || !_tabs.TryGetValue(_reportTabs.SelectedTab, out IReportTab? tab))
        {
            return;
        }

        if (!forceReload && _loadedTabs.Contains(tab))
        {
            return;
        }

        DateTime from = _fromDatePicker.Value.Date;
        DateTime to = _toDatePicker.Value.Date.AddDays(1).AddSeconds(-1);
        if (to < from)
        {
            MessageBoxHelper.ShowWarning("The report end date must be after the start date.", "Reports");
            return;
        }

        try
        {
            _applyFilterButton.Enabled = false;
            Cursor = Cursors.WaitCursor;
            await tab.LoadAsync(from, to);
            _loadedTabs.Add(tab);
        }
        finally
        {
            Cursor = Cursors.Default;
            _applyFilterButton.Enabled = true;
        }
    }

    private static DateTimePicker CreateDatePicker()
    {
        return new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Width = 110,
            Font = FontHelper.Regular(10F)
        };
    }
}
