using NatarakiCarRental.Helpers;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.UserControls.Reports;

public sealed class ReportsControl : UserControl
{
    private readonly DateTimePicker _fromDatePicker = CreateDatePicker();
    private readonly DateTimePicker _toDatePicker = CreateDatePicker();
    private readonly Button _applyFilterButton = ControlFactory.CreatePrimaryButton("Apply Filters");
    private readonly TabControl _reportTabs = new();
    private readonly Dictionary<TabPage, IReportTab> _tabs = [];
    private readonly HashSet<TabPage> _loadedTabs = [];
    
    private readonly List<Button> _presetButtons = [];
    private bool _isInternalDateChange = false;

    public ReportsControl()
    {
        Dock = DockStyle.Fill;
        BackColor = ThemeHelper.ContentBackground;
        InitializeLayout();
        Load += ReportsControl_Load;
        
        _fromDatePicker.ValueChanged += DatePicker_ValueChanged;
        _toDatePicker.ValueChanged += DatePicker_ValueChanged;
    }

    private void DatePicker_ValueChanged(object? sender, EventArgs e)
    {
        if (!_isInternalDateChange)
        {
            UpdatePresetVisuals(null);
        }
    }

    private void InitializeLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        root.Controls.Add(CreateFilterPanel(), 0, 0);
        root.Controls.Add(CreateTabControl(), 0, 1);

        Controls.Add(root);
    }

    private Panel CreateFilterPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground, Padding = new Padding(16, 12, 16, 12) };
        
        FlowLayoutPanel filterLayout = new() 
        { 
            Dock = DockStyle.Fill, 
            FlowDirection = FlowDirection.LeftToRight, 
            WrapContents = false,
            BackColor = ThemeHelper.ContentBackground
        };

        Label fromLabel = new() { Text = "From:", AutoSize = true, Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextSecondary, Margin = new Padding(0, 8, 4, 0) };
        _fromDatePicker.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _fromDatePicker.Margin = new Padding(0, 4, 16, 0);

        Label toLabel = new() { Text = "To:", AutoSize = true, Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextSecondary, Margin = new Padding(0, 8, 4, 0) };
        _toDatePicker.Value = DateTime.Today;
        _toDatePicker.Margin = new Padding(0, 4, 16, 0);

        _applyFilterButton.Width = 120;
        _applyFilterButton.Height = 32;
        _applyFilterButton.Margin = new Padding(0, 1, 24, 0);
        _applyFilterButton.Click += async (_, _) =>
        {
            _loadedTabs.Clear();
            await RefreshSelectedTabAsync(forceReload: true);
        };

        filterLayout.Controls.Add(fromLabel);
        filterLayout.Controls.Add(_fromDatePicker);
        filterLayout.Controls.Add(toLabel);
        filterLayout.Controls.Add(_toDatePicker);
        filterLayout.Controls.Add(_applyFilterButton);

        // Separator
        filterLayout.Controls.Add(new Panel { Width = 1, Height = 24, BackColor = ThemeHelper.Border, Margin = new Padding(0, 4, 24, 0) });

        // Presets
        var todayBtn = CreatePresetButton("Today", () => SetPreset(DateTime.Today, DateTime.Today));
        var weekBtn = CreatePresetButton("This Week", () => SetPreset(DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek), DateTime.Today));
        var monthBtn = CreatePresetButton("This Month", () => SetPreset(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today));
        var yearBtn = CreatePresetButton("This Year", () => SetPreset(new DateTime(DateTime.Today.Year, 1, 1), DateTime.Today));

        filterLayout.Controls.Add(todayBtn);
        filterLayout.Controls.Add(weekBtn);
        filterLayout.Controls.Add(monthBtn);
        filterLayout.Controls.Add(yearBtn);
        
        _presetButtons.AddRange([todayBtn, weekBtn, monthBtn, yearBtn]);
        
        // Initial active state matches "This Month"
        UpdatePresetVisuals(monthBtn);

        panel.Controls.Add(filterLayout);
        return panel;
    }

    private Button CreatePresetButton(string text, Action onClick)
    {
        Button btn = new()
        {
            Text = text,
            Width = 90,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            Font = FontHelper.SemiBold(8.5F),
            ForeColor = ThemeHelper.Primary,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 2, 8, 0)
        };
        btn.FlatAppearance.BorderColor = ThemeHelper.Primary;
        btn.FlatAppearance.BorderSize = 1;
        btn.Click += async (s, _) => 
        { 
            UpdatePresetVisuals((Button)s!);
            onClick(); 
            _loadedTabs.Clear(); 
            await RefreshSelectedTabAsync(forceReload: true); 
        };
        return btn;
    }

    private void UpdatePresetVisuals(Button? activeBtn)
    {
        foreach (var btn in _presetButtons)
        {
            bool isActive = btn == activeBtn;
            btn.BackColor = isActive ? ThemeHelper.Primary : Color.Transparent;
            btn.ForeColor = isActive ? Color.White : ThemeHelper.Primary;
            
            // Ensure hover looks good
            btn.FlatAppearance.MouseOverBackColor = isActive ? ThemeHelper.Primary : Color.FromArgb(20, ThemeHelper.Primary);
        }
    }

    private void SetPreset(DateTime from, DateTime to)
    {
        _isInternalDateChange = true;
        try
        {
            _fromDatePicker.Value = from;
            _toDatePicker.Value = to;
        }
        finally
        {
            _isInternalDateChange = false;
        }
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
        
        if (AccessControlService.HasPermission("ManageSystem.ActivityLogs"))
        {
            AddReportTab("Audit Log", new ReportsActivityLogTab());
        }

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

    private async Task RefreshSelectedTabAsync(bool forceReload)
    {
        if (_reportTabs.SelectedTab == null || !_tabs.TryGetValue(_reportTabs.SelectedTab, out IReportTab? tab))
        {
            return;
        }

        if (forceReload || !_loadedTabs.Contains(_reportTabs.SelectedTab))
        {
            DateTime from = _fromDatePicker.Value.Date;
            DateTime to = _toDatePicker.Value.Date.AddDays(1).AddSeconds(-1);
            
            await tab.LoadAsync(from, to);
            _loadedTabs.Add(_reportTabs.SelectedTab);
        }
    }

    private void ShowPermissionDenied()
    {
        Controls.Clear();
        Label label = new()
        {
            Text = "You do not have permission to view reports.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = FontHelper.SemiBold(12F),
            ForeColor = ThemeHelper.TextSecondary
        };
        Controls.Add(label);
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