using System.Text.Json;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.Forms.Offsite;
using NatarakiCarRental.UserControls.Cards;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.UserControls.Offsite;

public sealed class OffsiteControl : UserControl
{
    private const float ActionPillHeight = 26F;
    private const int NarrowRecordsGridWidth = 1200;
    private static readonly TimeSpan NormalRefreshInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DemoInterval = TimeSpan.FromSeconds(5);

    private readonly VehicleTrackingService _trackingService = new();
    private readonly VehicleTrackingSimulator _simulator = new();
    private readonly OffsiteService _offsiteService;
    private readonly SecurityVerificationService _verificationService = new();
    private readonly int _currentUserId;
    
    // Main Navigation Buttons (Module-style)
    private readonly IconButton _mapTrackingTabButton = new();
    private readonly IconButton _offsiteRecordsTabButton = new();
    private readonly Panel _mainContentPanel = new();

    // Map Tracking Components
    private readonly ComboBox _carComboBox = new();
    private readonly IconButton _refreshButton = CreateToolbarIconButton(IconChar.Rotate, "Refresh", 95);
    private readonly IconButton _startTrackingButton = CreateToolbarIconButton(IconChar.Play, "Start Tracking", 145);
    private readonly IconButton _stopTrackingButton = CreateToolbarIconButton(IconChar.Stop, "Stop Tracking", 145);
    private readonly WebView2 _mapWebView = new();
    
    private readonly Label _selectedCarValueLabel = CreateValueLabel();
    private readonly Label _plateNumberValueLabel = CreateValueLabel();
    private readonly Label _latitudeValueLabel = CreateValueLabel();
    private readonly Label _longitudeValueLabel = CreateValueLabel();
    private readonly Label _lastUpdatedValueLabel = CreateValueLabel();
    private readonly Label _sourceValueLabel = CreateValueLabel();
    private readonly Label _statusLabel = new();
    private readonly Label _autoRefreshLabel = new();
    
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly System.Windows.Forms.Timer _demoTimer = new();
    private readonly System.Windows.Forms.Timer _recordsSearchTimer = new() { Interval = 350 };

    // Records Components
    private readonly DataGridView _recordsGrid = new();
    private readonly IconButton _recordsSubTabButton = new();
    private readonly IconButton _archivedSubTabButton = new();
    private readonly TextBox _searchBox = new();
    private readonly ComboBox _typeFilter = new();
    private readonly ComboBox _statusFilter = new();
    private readonly Button _addRecordButton = CreateAddButton();
    private readonly Label _emptyStateLabel = new();
    private readonly Label _paginationLabel = new();
    private readonly Button _prevPageButton = CreatePaginationButton("Previous");
    private readonly Button _nextPageButton = CreatePaginationButton("Next");
    
    private readonly MetricCardControl _currentlyOffsiteCard = new();
    private readonly MetricCardControl _maintenanceCard = new();
    private readonly MetricCardControl _repairsCard = new();
    private readonly MetricCardControl _completedMonthCard = new();

    private bool _mapReady;
    private bool _isRefreshing;
    private bool _isDemoTickRunning;
    private bool _isInitializingRecordFilters;
    private bool _isLoadingRecords;
    private bool _pendingRecordsReload;
    private int _currentPage = 1;
    private int _pageSize = 4;
    private int _totalItems;
    private bool _isMapTabActive = true;
    private bool _showArchivedRecords;

    private static Button CreateAddButton()
    {
        IconButton button = new()
        {
            Text = "Add Record",
            IconChar = IconChar.Plus,
            IconColor = Color.White,
            IconSize = 14,
            Size = new Size(130, 36),
            BackColor = ThemeHelper.Primary,
            ForeColor = Color.White,
            Font = FontHelper.SemiBold(9F),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TextImageRelation = TextImageRelation.ImageBeforeText
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private static Button CreatePaginationButton(string text)
    {
        Button button = new()
        {
            Text = text,
            Size = new Size(80, 32),
            BackColor = ThemeHelper.Surface,
            ForeColor = ThemeHelper.TextPrimary,
            Font = FontHelper.SemiBold(9F),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = ThemeHelper.Border;
        return button;
    }

    public OffsiteControl(int currentUserId)
    {
        _currentUserId = currentUserId;
        _offsiteService = new OffsiteService(currentUserId);
        InitializeControl();
        Load += OffsiteControl_Load;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _demoTimer.Stop();
            _recordsSearchTimer.Stop();
            _refreshTimer.Dispose();
            _demoTimer.Dispose();
            _recordsSearchTimer.Dispose();
            _mapWebView.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeControl()
    {
        BackColor = ThemeHelper.ContentBackground;
        Dock = DockStyle.Fill;
        Padding = new Padding(32);

        TableLayoutPanel mainLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        
        mainLayout.Controls.Add(CreateHeaderPanel(), 0, 0);
        mainLayout.Controls.Add(CreateMainTabSwitcher(), 0, 1);
        
        _mainContentPanel.Dock = DockStyle.Fill;
        _mainContentPanel.BackColor = ThemeHelper.ContentBackground;
        mainLayout.Controls.Add(_mainContentPanel, 0, 2);
        
        Controls.Add(mainLayout);
        Resize += OffsiteControl_Resize;

        _refreshTimer.Interval = (int)NormalRefreshInterval.TotalMilliseconds;
        _refreshTimer.Tick += async (_, _) => await RefreshSelectedCarLocationAsync(showEmptyMessage: false);

        _demoTimer.Interval = (int)DemoInterval.TotalMilliseconds;
        _demoTimer.Tick += async (_, _) => await InsertDemoLocationAsync();

        _recordsSearchTimer.Tick += RecordsSearchTimer_Tick;
    }

    private Panel CreateHeaderPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        panel.Controls.Add(new Label
        {
            Text = "Offsite & Tracking",
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(360, 34),
            Font = FontHelper.Title(22F),
            ForeColor = ThemeHelper.TextPrimary
        });
        panel.Controls.Add(new Label
        {
            Text = "Monitor vehicle location and manage off-operational records.",
            AutoSize = false,
            Location = new Point(2, 42),
            Size = new Size(620, 24),
            Font = FontHelper.Regular(10.5F),
            ForeColor = ThemeHelper.TextSecondary
        });
        return panel;
    }

    private Panel CreateMainTabSwitcher()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        
        ConfigureTabButton(_mapTrackingTabButton, "Map Tracking", IconChar.MapLocationDot, new Point(0, 10), 160);
        ConfigureTabButton(_offsiteRecordsTabButton, "Offsite Records", IconChar.ClipboardList, new Point(168, 10), 160);

        _mapTrackingTabButton.Click += (_, _) => ShowMapTrackingView();
        _offsiteRecordsTabButton.Click += async (_, _) => await ShowRecordsViewAsync();

        panel.Controls.Add(_mapTrackingTabButton);
        panel.Controls.Add(_offsiteRecordsTabButton);
        return panel;
    }

    private void ShowMapTrackingView()
    {
        _isMapTabActive = true;
        UpdateMainTabStyles();
        _mainContentPanel.Controls.Clear();
        _mainContentPanel.Controls.Add(CreateMapTrackingLayout());
    }

    private async Task ShowRecordsViewAsync()
    {
        _isMapTabActive = false;
        UpdateMainTabStyles();
        _mainContentPanel.Controls.Clear();
        _mainContentPanel.Controls.Add(CreateRecordsLayout());
        await LoadRecordsAsync();
    }

    private void UpdateMainTabStyles()
    {
        ApplyTabStyle(_mapTrackingTabButton, _isMapTabActive);
        ApplyTabStyle(_offsiteRecordsTabButton, !_isMapTabActive);
    }

    private async void OffsiteControl_Resize(object? sender, EventArgs e)
    {
        if (_isMapTabActive || _recordsGrid.Columns.Count == 0) return;
        UpdateRecordsGridColumnLayout();
        int newPageSize = GetRecordsPageSize();
        if (newPageSize == _pageSize) return;
        _pageSize = newPageSize;
        _currentPage = 1;
        await LoadRecordsAsync();
    }

    private Control CreateMapTrackingLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(0, 12, 0, 0) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F)); // Controls
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Map
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116F)); // Compact Info

        layout.Controls.Add(CreateTrackingControlsRow(), 0, 0);
        layout.Controls.Add(CreateMapContainer(), 0, 1);
        layout.Controls.Add(CreateSelectedCarInfoCard(), 0, 2);
        
        Panel scrollPanel = new() { Dock = DockStyle.Fill, AutoScroll = true };
        scrollPanel.Controls.Add(layout);
        return scrollPanel;
    }

    private Control CreateTrackingControlsRow()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        FlowLayoutPanel flow = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };

        Label label = new() { Text = "Tracking Car", AutoSize = true, Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextSecondary, Margin = new Padding(0, 9, 8, 0) };
        _carComboBox.Width = 220;
        _carComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _carComboBox.Font = FontHelper.Regular(10F);
        _carComboBox.Margin = new Padding(0, 2, 12, 0);
        _carComboBox.SelectedIndexChanged += async (_, _) => await RefreshSelectedCarLocationAsync(showEmptyMessage: true);

        _refreshButton.Margin = new Padding(0, 0, 10, 0);
        _refreshButton.Click += async (_, _) => await RefreshSelectedCarLocationAsync(showEmptyMessage: true);

        _startTrackingButton.Margin = new Padding(0, 0, 8, 0);
        _startTrackingButton.Click += async (_, _) => await StartDemoTrackingAsync();

        _stopTrackingButton.Margin = new Padding(0, 0, 12, 0);
        _stopTrackingButton.Click += (_, _) => StopDemoTracking();

        _autoRefreshLabel.Text = "Refresh: 10m";
        _autoRefreshLabel.AutoSize = true;
        _autoRefreshLabel.Font = FontHelper.SemiBold(9F);
        _autoRefreshLabel.ForeColor = ThemeHelper.TextSecondary;
        _autoRefreshLabel.Margin = new Padding(0, 10, 0, 0);

        flow.Controls.Add(label);
        flow.Controls.Add(_carComboBox);
        flow.Controls.Add(_refreshButton);
        flow.Controls.Add(_startTrackingButton);
        flow.Controls.Add(_stopTrackingButton);
        flow.Controls.Add(_autoRefreshLabel);

        panel.Controls.Add(flow);
        return panel;
    }

    private Control CreateMapContainer()
    {
        Panel card = ControlFactory.CreateCardPanel(new Size(0, 0));
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(2);
        card.Margin = new Padding(0, 0, 0, 16);
        _mapWebView.Dock = DockStyle.Fill;
        _mapWebView.NavigationCompleted += MapWebView_NavigationCompleted;
        card.Controls.Add(_mapWebView);
        return card;
    }

    private Control CreateSelectedCarInfoCard()
    {
        Panel card = ControlFactory.CreateCardPanel(new Size(0, 100));
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(22, 16, 22, 16);

        TableLayoutPanel grid = new() { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 2 };
        for (int i = 0; i < 6; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66F));
        
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));

        AddInfoColumn(grid, "Selected Car", _selectedCarValueLabel, 0);
        AddInfoColumn(grid, "Plate Number", _plateNumberValueLabel, 1);
        AddInfoColumn(grid, "Last Latitude", _latitudeValueLabel, 2);
        AddInfoColumn(grid, "Last Longitude", _longitudeValueLabel, 3);
        AddInfoColumn(grid, "Last Updated", _lastUpdatedValueLabel, 4);
        AddInfoColumn(grid, "Source", _sourceValueLabel, 5);

        card.Controls.Add(grid);
        return card;
    }

    private Control CreateRecordsLayout()
    {
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(0, 0, 0, 0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

        // Metrics
        TableLayoutPanel metricsGrid = new() { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Padding = new Padding(0, 12, 0, 8) };
        for (int i = 0; i < 4; i++) metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        
        _currentlyOffsiteCard.SetMetric(IconChar.LocationDot, "Currently Offsite", "0", "Cars", ThemeHelper.Primary);
        _maintenanceCard.SetMetric(IconChar.Gears, "Maintenance", "0", "Active", ThemeHelper.Warning);
        _repairsCard.SetMetric(IconChar.Wrench, "Repairs", "0", "Active", ThemeHelper.Danger);
        _completedMonthCard.SetMetric(IconChar.CheckCircle, "Completed (Month)", "0", "Records", ThemeHelper.Success);

        AddMetricCard(metricsGrid, _currentlyOffsiteCard, 0);
        AddMetricCard(metricsGrid, _maintenanceCard, 1);
        AddMetricCard(metricsGrid, _repairsCard, 2);
        AddMetricCard(metricsGrid, _completedMonthCard, 3);
        layout.Controls.Add(metricsGrid, 0, 0);

        // Search Row (no border)
        Panel searchRow = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        
        BorderedPanel searchContainer = new()
        {
            Size = new Size(260, 32),
            Location = new Point(0, 8),
            BackColor = ThemeHelper.Surface,
            BorderColor = ThemeHelper.Border,
            Cursor = Cursors.IBeam
        };

        IconPictureBox searchIcon = new()
        {
            IconChar = IconChar.MagnifyingGlass,
            IconColor = ThemeHelper.TextSecondary,
            IconSize = 18,
            BackColor = ThemeHelper.Surface,
            Location = new Point(8, 7),
            Size = new Size(20, 20)
        };

        _searchBox.BorderStyle = BorderStyle.None;
        _searchBox.PlaceholderText = "Search car, plate, location...";
        _searchBox.BackColor = ThemeHelper.Surface;
        _searchBox.Font = FontHelper.Regular(10F);
        _searchBox.ForeColor = ThemeHelper.TextPrimary;
        _searchBox.Location = new Point(34, 7);
        _searchBox.Width = 210;
        _searchBox.TextChanged -= SearchBox_TextChanged;
        _searchBox.TextChanged += SearchBox_TextChanged;

        searchContainer.Controls.Add(searchIcon);
        searchContainer.Controls.Add(_searchBox);
        searchContainer.Click += (_, _) => _searchBox.Focus();

        ConfigureRecordFilters();

        _addRecordButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _addRecordButton.Location = new Point(0, 6);
        _addRecordButton.Click -= AddRecordButton_Click;
        _addRecordButton.Click += AddRecordButton_Click;
        searchRow.Resize += (_, _) => _addRecordButton.Left = Math.Max(0, searchRow.Width - _addRecordButton.Width);

        searchRow.Controls.Add(searchContainer);
        searchRow.Controls.Add(_typeFilter);
        searchRow.Controls.Add(_statusFilter);
        searchRow.Controls.Add(_addRecordButton);
        layout.Controls.Add(searchRow, 0, 1);

        // Sub Tabs (Module-style)
        Panel subTabRow = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        ConfigureTabButton(_recordsSubTabButton, "Records", IconChar.ListUl, new Point(0, 10), 120);
        ConfigureTabButton(_archivedSubTabButton, "Archived", IconChar.BoxArchive, new Point(128, 10), 120);

        _recordsSubTabButton.Click -= RecordsSubTabButton_Click;
        _recordsSubTabButton.Click += RecordsSubTabButton_Click;
        _archivedSubTabButton.Click -= ArchivedSubTabButton_Click;
        _archivedSubTabButton.Click += ArchivedSubTabButton_Click;
        UpdateSubTabStyles();

        subTabRow.Controls.Add(_recordsSubTabButton);
        subTabRow.Controls.Add(_archivedSubTabButton);
        layout.Controls.Add(subTabRow, 0, 2);

        // Table
        Panel tableCard = ControlFactory.CreateCardPanel(new Size(0, 0));
        tableCard.Dock = DockStyle.Fill;
        tableCard.Padding = new Padding(18);

        SetupRecordsGrid();
        tableCard.Controls.Add(_recordsGrid);
        tableCard.Controls.Add(_emptyStateLabel);
        layout.Controls.Add(tableCard, 0, 3);
        layout.Controls.Add(CreatePaginationPanel(), 0, 4);

        return layout;
    }

    private Panel CreatePaginationPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };

        _prevPageButton.Location = new Point(0, 8);
        _prevPageButton.Click -= PrevPageButton_Click;
        _prevPageButton.Click += PrevPageButton_Click;

        _nextPageButton.Location = new Point(90, 8);
        _nextPageButton.Click -= NextPageButton_Click;
        _nextPageButton.Click += NextPageButton_Click;

        _paginationLabel.AutoSize = false;
        _paginationLabel.Location = new Point(180, 8);
        _paginationLabel.Size = new Size(260, 32);
        _paginationLabel.TextAlign = ContentAlignment.MiddleLeft;
        _paginationLabel.Font = FontHelper.Regular(9.5F);
        _paginationLabel.ForeColor = ThemeHelper.TextSecondary;

        panel.Controls.Add(_prevPageButton);
        panel.Controls.Add(_nextPageButton);
        panel.Controls.Add(_paginationLabel);
        return panel;
    }

    private void ConfigureRecordFilters()
    {
        string? selectedType = _typeFilter.SelectedItem?.ToString();
        string? selectedStatus = _statusFilter.SelectedItem?.ToString();

        _isInitializingRecordFilters = true;

        _typeFilter.SelectedIndexChanged -= TypeFilter_SelectedIndexChanged;
        _typeFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _typeFilter.Font = FontHelper.Regular(10F);
        _typeFilter.Items.Clear();
        _typeFilter.Items.AddRange(["All Types", "Maintenance", "Repair", "Cleaning"]);
        _typeFilter.SelectedItem = IsComboValueAvailable(_typeFilter, selectedType) ? selectedType : "All Types";
        _typeFilter.Size = new Size(180, 30);
        _typeFilter.Location = new Point(272, 8);

        _statusFilter.SelectedIndexChanged -= StatusFilter_SelectedIndexChanged;
        _statusFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _statusFilter.Font = FontHelper.Regular(10F);
        _statusFilter.Items.Clear();
        _statusFilter.Items.AddRange(["All Status", "Ongoing", "Completed", "Cancelled"]);
        _statusFilter.SelectedItem = IsComboValueAvailable(_statusFilter, selectedStatus) ? selectedStatus : "All Status";
        _statusFilter.Size = new Size(160, 30);
        _statusFilter.Location = new Point(464, 8);

        _isInitializingRecordFilters = false;

        _typeFilter.SelectedIndexChanged += TypeFilter_SelectedIndexChanged;
        _statusFilter.SelectedIndexChanged += StatusFilter_SelectedIndexChanged;
    }

    private static bool IsComboValueAvailable(ComboBox comboBox, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        foreach (object? item in comboBox.Items)
        {
            if (string.Equals(item?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void SearchBox_TextChanged(object? sender, EventArgs e)
    {
        if (_isInitializingRecordFilters) return;

        _currentPage = 1;
        _recordsSearchTimer.Stop();
        _recordsSearchTimer.Start();
    }

    private async void RecordsSearchTimer_Tick(object? sender, EventArgs e)
    {
        _recordsSearchTimer.Stop();
        await LoadRecordsAsync();
    }

    private async void TypeFilter_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isInitializingRecordFilters) return;

        _currentPage = 1;
        await LoadRecordsAsync();
    }

    private async void StatusFilter_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isInitializingRecordFilters) return;

        _currentPage = 1;
        await LoadRecordsAsync();
    }

    private async void AddRecordButton_Click(object? sender, EventArgs e)
    {
        await AddRecordAsync();
    }

    private async void PrevPageButton_Click(object? sender, EventArgs e)
    {
        if (_currentPage <= 1) return;
        _currentPage--;
        await LoadRecordsAsync();
    }

    private async void NextPageButton_Click(object? sender, EventArgs e)
    {
        int totalPages = Math.Max(1, (int)Math.Ceiling(_totalItems / (double)_pageSize));
        if (_currentPage >= totalPages) return;
        _currentPage++;
        await LoadRecordsAsync();
    }

    private async void RecordsSubTabButton_Click(object? sender, EventArgs e)
    {
        _showArchivedRecords = false;
        _currentPage = 1;
        UpdateSubTabStyles();
        await LoadRecordsAsync();
    }

    private async void ArchivedSubTabButton_Click(object? sender, EventArgs e)
    {
        _showArchivedRecords = true;
        _currentPage = 1;
        UpdateSubTabStyles();
        await LoadRecordsAsync();
    }

    private void UpdateSubTabStyles()
    {
        ApplyTabStyle(_recordsSubTabButton, !_showArchivedRecords);
        ApplyTabStyle(_archivedSubTabButton, _showArchivedRecords);
    }

    private void SetupRecordsGrid()
    {
        _recordsGrid.Dock = DockStyle.Fill;
        _recordsGrid.AllowUserToAddRows = false;
        _recordsGrid.AllowUserToDeleteRows = false;
        _recordsGrid.AllowUserToResizeRows = false;
        _recordsGrid.AllowUserToResizeColumns = false;
        _recordsGrid.ScrollBars = ScrollBars.Vertical;
        _recordsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _recordsGrid.BackgroundColor = ThemeHelper.Surface;
        _recordsGrid.BorderStyle = BorderStyle.FixedSingle;
        _recordsGrid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        _recordsGrid.ColumnHeadersHeight = 38;
        _recordsGrid.EnableHeadersVisualStyles = false;
        _recordsGrid.GridColor = ThemeHelper.TableGridLine;
        _recordsGrid.ReadOnly = true;
        _recordsGrid.RowHeadersVisible = false;
        _recordsGrid.RowTemplate.Height = 38;
        _recordsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        _recordsGrid.DefaultCellStyle.SelectionBackColor = ThemeHelper.Surface;
        _recordsGrid.DefaultCellStyle.SelectionForeColor = ThemeHelper.TextPrimary;

        _recordsGrid.ColumnHeadersDefaultCellStyle.BackColor = ThemeHelper.Primary;
        _recordsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _recordsGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemeHelper.Primary;
        _recordsGrid.ColumnHeadersDefaultCellStyle.Font = FontHelper.SemiBold(9F);
        _recordsGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

        _recordsGrid.CellMouseClick -= RecordsGrid_CellMouseClick;
        _recordsGrid.CellMouseClick += RecordsGrid_CellMouseClick;
        _recordsGrid.CellMouseMove -= RecordsGrid_CellMouseMove;
        _recordsGrid.CellMouseMove += RecordsGrid_CellMouseMove;
        _recordsGrid.CellMouseLeave -= RecordsGrid_CellMouseLeave;
        _recordsGrid.CellMouseLeave += RecordsGrid_CellMouseLeave;
        _recordsGrid.CellPainting -= RecordsGrid_CellPainting;
        _recordsGrid.CellPainting += RecordsGrid_CellPainting;
        _recordsGrid.Resize -= RecordsGrid_Resize;
        _recordsGrid.Resize += RecordsGrid_Resize;

        _emptyStateLabel.Text = "No offsite records found.";
        _emptyStateLabel.Dock = DockStyle.Bottom;
        _emptyStateLabel.Height = 42;
        _emptyStateLabel.Font = FontHelper.Regular(10F);
        _emptyStateLabel.ForeColor = ThemeHelper.TextSecondary;
        _emptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        _emptyStateLabel.Visible = false;

        _recordsGrid.Columns.Clear();
        _recordsGrid.Columns.Add("Car", "Car / Plate");
        _recordsGrid.Columns.Add("Type", "Type");
        _recordsGrid.Columns.Add("Status", "Status");
        _recordsGrid.Columns.Add("Dates", "Date Range");
        _recordsGrid.Columns.Add("Location", "Location");
        _recordsGrid.Columns.Add("ContactPerson", "Contact Person");
        _recordsGrid.Columns.Add("ContactNumber", "Contact Number");
        _recordsGrid.Columns.Add("AmountPaid", "Amount Paid");
        _recordsGrid.Columns.Add("Actions", "Actions");
        UpdateRecordsGridColumnLayout();
    }

    private void SetColumnSizing(string name, float fillWeight, int minWidth)
    {
        if (_recordsGrid.Columns[name] is DataGridViewColumn col)
        {
            col.FillWeight = fillWeight;
            col.MinimumWidth = minWidth;
            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }
    }

    private void SetColumnWidth(string name, int width)
    {
        if (_recordsGrid.Columns[name] is DataGridViewColumn col)
        {
            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            col.Width = width;
            col.MinimumWidth = width;
        }
    }

    private void UpdateRecordsGridColumnLayout()
    {
        if (_recordsGrid.Columns.Count == 0)
        {
            return;
        }

        int gridWidth = _recordsGrid.ClientSize.Width;
        if (gridWidth > 0 && gridWidth < NarrowRecordsGridWidth)
        {
            _recordsGrid.ScrollBars = ScrollBars.Both;
            _recordsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            SetColumnWidth("Car", 150);
            SetColumnWidth("Type", 100);
            SetColumnWidth("Status", 110);
            SetColumnWidth("Dates", 135);
            SetColumnWidth("Location", 170);
            SetColumnWidth("ContactPerson", 150);
            SetColumnWidth("ContactNumber", 135);
            SetColumnWidth("AmountPaid", 120);
            SetColumnWidth("Actions", 320);
            return;
        }

        _recordsGrid.ScrollBars = ScrollBars.Vertical;
        _recordsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        SetColumnSizing("Car", 18F, 150);
        SetColumnSizing("Type", 12F, 85);
        SetColumnSizing("Status", 12F, 85);
        SetColumnSizing("Dates", 15F, 100);
        SetColumnSizing("Location", 25F, 120);
        SetColumnSizing("ContactPerson", 20F, 100);
        SetColumnSizing("ContactNumber", 15F, 100);
        SetColumnSizing("AmountPaid", 12F, 85);
        SetColumnWidth("Actions", 350);
    }

    private void RecordsGrid_Resize(object? sender, EventArgs e)
    {
        UpdateRecordsGridColumnLayout();
    }

    private void RecordsGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics == null) return;
        if (e.RowIndex >= _recordsGrid.Rows.Count || e.ColumnIndex >= _recordsGrid.Columns.Count) return;

        string columnName = _recordsGrid.Columns[e.ColumnIndex].Name;
        bool isStatus = columnName == "Status";
        bool isAction = columnName == "Actions";

        if (!isStatus && !isAction) return;

        if (_recordsGrid.Rows[e.RowIndex].Tag is not OffsiteRecordListItem item) return;

        e.PaintBackground(e.CellBounds, true);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Font font = FontHelper.SemiBold(8.5F);

        if (isAction)
        {
            var actions = GetRowActions(item);
            var layout = GetOffsiteActionButtonBounds(e.CellBounds, actions);

            using Pen linePen = new(ThemeHelper.TableGridLine);

            for (int i = 0; i < layout.Count; i++)
            {
                var entry = layout[i];
                Color backColor = GetActionColor(entry.Action);
                using GraphicsPath path = GetRoundedRect(entry.Bounds, entry.Bounds.Height / 2);
                using SolidBrush brush = new(backColor);
                using SolidBrush foreBrush = new(Color.White);
                e.Graphics.FillPath(brush, path);
                
                using StringFormat format = new()
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                e.Graphics.DrawString(entry.Action, font, foreBrush, entry.Bounds, format);

                if (i < layout.Count - 1)
                {
                    float lineX = entry.Bounds.Right + 3F;
                    e.Graphics.DrawLine(linePen, lineX, e.CellBounds.Top, lineX, e.CellBounds.Bottom);
                }
            }
        }
        else
        {
            Color backColor = item.Status switch { "Ongoing" => ThemeHelper.Warning, "Completed" => ThemeHelper.Success, "Cancelled" => ThemeHelper.Danger, _ => ThemeHelper.GrayIcon };
            float height = ActionPillHeight;
            float width = 90F;
            float x = e.CellBounds.X + (e.CellBounds.Width - width) / 2;
            float y = e.CellBounds.Y + (e.CellBounds.Height - height) / 2;
            RectangleF rect = new(x, y, width, height);

            using var path = GetRoundedRect(rect, height / 2);
            using var brush = new SolidBrush(backColor);
            using var foreBrush = new SolidBrush(Color.White);
            e.Graphics.FillPath(brush, path);

            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            };
            e.Graphics.DrawString(item.Status, font, foreBrush, rect, format);
        }

        e.Handled = true;
    }

    private static Color GetActionColor(string action) => action switch
    {
        "View" => ThemeHelper.Primary,
        "Edit" => ThemeHelper.Primary,
        "Complete" => ThemeHelper.Success,
        "Cancel" => ThemeHelper.Danger,
        "Archive" => ThemeHelper.GrayIcon,
        "Restore" => ThemeHelper.Success,
        _ => ThemeHelper.Primary
    };

    private void RecordsGrid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Button != MouseButtons.Left) return;
        if (_recordsGrid.Columns[e.ColumnIndex].Name != "Actions") return;

        if (_recordsGrid.Rows[e.RowIndex].Tag is not OffsiteRecordListItem item) return;

        string? clickedAction = GetActionAt(e.RowIndex, e.ColumnIndex, e.X, e.Y);
        if (clickedAction == null) return;

        switch (clickedAction)
        {
            case "View": ShowDetails(item.OffsiteRecordId, true); break;
            case "Edit": ShowDetails(item.OffsiteRecordId, false); break;
            case "Complete": _ = CompleteRecord(item.OffsiteRecordId); break;
            case "Cancel": _ = CancelRecord(item.OffsiteRecordId); break;
            case "Archive": _ = ArchiveRecord(item.OffsiteRecordId); break;
            case "Restore": _ = RestoreRecord(item.OffsiteRecordId); break;
        }
    }

    private void RecordsGrid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.RowIndex >= _recordsGrid.Rows.Count || e.ColumnIndex >= _recordsGrid.Columns.Count)
        {
            _recordsGrid.Cursor = Cursors.Default;
            return;
        }

        _recordsGrid.Cursor = GetActionAt(e.RowIndex, e.ColumnIndex, e.X, e.Y) is null ? Cursors.Default : Cursors.Hand;
    }

    private void RecordsGrid_CellMouseLeave(object? sender, DataGridViewCellEventArgs e)
    {
        _recordsGrid.Cursor = Cursors.Default;
    }

    private List<string> GetRowActions(OffsiteRecordListItem item)
    {
        if (_showArchivedRecords)
            return new List<string> { "View", "Restore" };

        if (string.Equals(item.Status, "Ongoing", StringComparison.OrdinalIgnoreCase))
            return new List<string> { "View", "Edit", "Complete", "Cancel" };

        return new List<string> { "View", "Archive" };
    }

    private string? GetActionAt(int rowIndex, int colIndex, int x, int y)
    {
        if (rowIndex < 0 || colIndex < 0 || rowIndex >= _recordsGrid.Rows.Count || colIndex >= _recordsGrid.Columns.Count)
            return null;

        if (_recordsGrid.Columns[colIndex].Name != "Actions")
            return null;

        if (_recordsGrid.Rows[rowIndex].Tag is not OffsiteRecordListItem item)
            return null;

        var actions = GetRowActions(item);
        Rectangle cellBounds = _recordsGrid.GetCellDisplayRectangle(colIndex, rowIndex, false);
        var layout = GetOffsiteActionButtonBounds(cellBounds, actions);

        foreach (var entry in layout)
        {
            RectangleF cellRelativeBounds = new(entry.Bounds.X - cellBounds.X, entry.Bounds.Y - cellBounds.Y, entry.Bounds.Width, entry.Bounds.Height);
            if (cellRelativeBounds.Contains(x, y)) return entry.Action;
        }
        return null;
    }

    private List<(string Action, RectangleF Bounds)> GetOffsiteActionButtonBounds(Rectangle cellBounds, IReadOnlyList<string> actions)
    {
        List<(string Action, RectangleF Bounds)> result = [];
        if (actions.Count == 0) return result;

        float totalGap = (actions.Count - 1) * 6F;
        float availableWidth = cellBounds.Width - 12; // 6px padding each side
        float buttonWidth = (availableWidth - totalGap) / actions.Count;
        float height = ActionPillHeight;
        float y = cellBounds.Y + (cellBounds.Height - height) / 2;
        float currentX = cellBounds.X + 6;

        foreach (string action in actions)
        {
            result.Add((action, new RectangleF(currentX, y, buttonWidth, height)));
            currentX += buttonWidth + 6;
        }

        return result;
    }

    private static float GetActionPillWidth(Graphics graphics, Font font, string action)
    {
        return graphics.MeasureString(action, font).Width + 22F;
    }

    private static System.Drawing.Drawing2D.GraphicsPath GetRoundedRect(RectangleF rect, float radius)
    {
        System.Drawing.Drawing2D.GraphicsPath path = new();
        float diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void ConfigureTabButton(IconButton button, string text, IconChar icon, Point location, int width = 120)
    {
        button.Text = text; button.IconChar = icon; button.IconSize = 16; button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.Location = location; button.Size = new Size(width, 34); button.FlatStyle = FlatStyle.Flat; button.Cursor = Cursors.Hand;
        button.Font = FontHelper.SemiBold(9.5F); button.FlatAppearance.BorderSize = 0;
    }

    private static void ApplyTabStyle(IconButton button, bool isActive)
    {
        button.BackColor = isActive ? ThemeHelper.Primary : ThemeHelper.Surface;
        button.ForeColor = isActive ? Color.White : ThemeHelper.TextPrimary;
        button.IconColor = isActive ? Color.White : ThemeHelper.TextSecondary;
    }

    private static void AddMetricCard(TableLayoutPanel grid, MetricCardControl card, int column)
    {
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, column == 3 ? 0 : 14, 0);
        grid.Controls.Add(card, column, 0);
    }

    private static void AddInfoColumn(TableLayoutPanel grid, string title, Label valueLabel, int column)
    {
        Label titleLabel = new() { Text = title, Dock = DockStyle.Fill, Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextSecondary };
        valueLabel.Dock = DockStyle.Fill;
        grid.Controls.Add(titleLabel, column, 0);
        grid.Controls.Add(valueLabel, column, 1);
    }

    private static Label CreateValueLabel() => new() { Text = "-", AutoSize = false, Font = FontHelper.SemiBold(9.5F), ForeColor = ThemeHelper.TextPrimary, AutoEllipsis = true };

    private async void OffsiteControl_Load(object? sender, EventArgs e)
    {
        Load -= OffsiteControl_Load;
        await InitializeMapAsync();
        await LoadCarsAsync();
        ShowMapTrackingView();
        _refreshTimer.Start();
    }

    private async Task LoadRecordsAsync()
    {
        if (_isLoadingRecords)
        {
            _pendingRecordsReload = true;
            return;
        }

        _isLoadingRecords = true;
        try
        {
            do
            {
                _pendingRecordsReload = false;
                await LoadRecordsCoreAsync();
            }
            while (_pendingRecordsReload);
        }
        finally
        {
            _isLoadingRecords = false;
        }
    }

    private async Task LoadRecordsCoreAsync()
    {
        try
        {
            _pageSize = GetRecordsPageSize();
            string? status = _statusFilter.SelectedIndex > 0 ? _statusFilter.SelectedItem?.ToString() : null;
            string? type = _typeFilter.SelectedIndex > 0 ? _typeFilter.SelectedItem?.ToString() : null;
            _totalItems = await _offsiteService.CountAsync(_searchBox.Text, status, type, _showArchivedRecords);
            int totalPages = Math.Max(1, (int)Math.Ceiling(_totalItems / (double)_pageSize));
            if (_currentPage > totalPages) _currentPage = totalPages;
            var items = await _offsiteService.GetListAsync(_searchBox.Text, status, type, _showArchivedRecords, _currentPage, _pageSize);

            _recordsGrid.Rows.Clear();
            foreach (var item in items)
            {
                string dates = item.Status == "Ongoing" 
                    ? $"{item.StartDate:MMM d} - {item.ExpectedReturnDate?.ToString("MMM d") ?? "???"}"
                    : $"{item.StartDate:MMM d} - {item.CompletedDate?.ToString("MMM d") ?? "Cancelled"}";

                string location = string.IsNullOrWhiteSpace(item.LocationName) ? "-" : item.LocationName;
                string contactPerson = string.IsNullOrWhiteSpace(item.ContactPerson) ? "-" : item.ContactPerson.Trim();
                string contactNumber = string.IsNullOrWhiteSpace(item.ContactNumber) ? "-" : item.ContactNumber.Trim();

                int rowIndex = _recordsGrid.Rows.Add(
                    $"{item.CarName} ({item.PlateNumber})",
                    item.OffsiteType,
                    item.Status,
                    dates,
                    location,
                    contactPerson,
                    contactNumber,
                    $"₱{item.ActualCost:N2}",
                    string.Empty // Actions column is custom painted
                );
                
                _recordsGrid.Rows[rowIndex].Tag = item;
            }
            _emptyStateLabel.Visible = !items.Any();
            UpdateRecordsGridColumnLayout();
            UpdatePagination(totalPages);
            UpdateMetrics();
        }
        catch (Exception ex) { MessageBoxHelper.ShowError($"Failed to load records: {ex.Message}"); }
    }

    private int GetRecordsPageSize()
    {
        return Height > 700 ? 13 : 4;
    }

    private void UpdatePagination(int totalPages)
    {
        _paginationLabel.Text = $"Page {_currentPage} of {totalPages} ({_totalItems} records)";
        _prevPageButton.Enabled = _currentPage > 1;
        _nextPageButton.Enabled = _currentPage < totalPages;
    }

    private void UpdateMetrics() => _currentlyOffsiteCard.SetMetric(IconChar.LocationDot, "Currently Offsite", _totalItems.ToString(), "Cars", ThemeHelper.Primary);

    private static IconButton CreateToolbarIconButton(IconChar icon, string text, int width)
    {
        IconButton button = new() { Text = text, IconChar = icon, IconColor = Color.White, IconSize = 16, TextImageRelation = TextImageRelation.ImageBeforeText,
            Size = new Size(width, 34), BackColor = ThemeHelper.Primary, ForeColor = Color.White, Font = FontHelper.SemiBold(9F), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        button.FlatAppearance.BorderSize = 0; return button;
    }

    private void ShowDetails(int recordId, bool viewOnly) 
    { 
        if (!viewOnly && !AccessControlService.HasPermission("Offsite.Edit"))
        {
            MessageBoxHelper.ShowWarning("You do not have permission to perform this action.");
            return;
        }
        using var form = new OffsiteRecordDetailsForm(_currentUserId, recordId, viewOnly); 
        if (form.ShowDialog() == DialogResult.OK) { _currentPage = 1; _ = LoadRecordsAsync(); } 
    }
    private async Task CompleteRecord(int recordId)
    {
        if (!AccessControlService.HasPermission("Offsite.Complete"))
        {
            MessageBoxHelper.ShowWarning("You do not have permission to perform this action.");
            return;
        }

        if (!await _verificationService.RequireOwnerVerificationIfNeededAsync(_currentUserId, "Complete offsite record"))
        {
            return;
        }

        using var form = new OffsiteCompletionForm(_currentUserId, recordId);
        if (form.ShowDialog() == DialogResult.OK)
        {
            MessageBoxHelper.ShowSuccess("Offsite record completed successfully.");
            await LoadRecordsAsync();
        }
    }
    private async Task CancelRecord(int recordId) 
    { 
        if (!AccessControlService.HasPermission("Offsite.Cancel"))
        {
            MessageBoxHelper.ShowWarning("You do not have permission to perform this action.");
            return;
        }

        if (!await _verificationService.RequireOwnerVerificationIfNeededAsync(_currentUserId, "Cancel offsite activity"))
        {
            return;
        }

        if (MessageBoxHelper.Confirm("Are you sure you want to cancel this offsite activity?", "Cancel Offsite")) { await _offsiteService.CancelAsync(recordId); _currentPage = 1; await LoadRecordsAsync(); } 
    }
    private async Task ArchiveRecord(int recordId) 
    { 
        if (!AccessControlService.HasPermission("Offsite.ArchiveRestore"))
        {
            MessageBoxHelper.ShowWarning("You do not have permission to perform this action.");
            return;
        }

        if (!await _verificationService.RequireOwnerVerificationIfNeededAsync(_currentUserId, "Archive offsite record"))
        {
            return;
        }

        await _offsiteService.ArchiveAsync(recordId); _currentPage = 1; await LoadRecordsAsync(); 
    }
    private async Task RestoreRecord(int recordId) 
    { 
        if (!AccessControlService.HasPermission("Offsite.ArchiveRestore"))
        {
            MessageBoxHelper.ShowWarning("You do not have permission to perform this action.");
            return;
        }

        if (!await _verificationService.RequireOwnerVerificationIfNeededAsync(_currentUserId, "Restore offsite record"))
        {
            return;
        }

        await _offsiteService.RestoreAsync(recordId); _currentPage = 1; await LoadRecordsAsync(); 
    }
    private async Task AddRecordAsync() 
    { 
        if (!AccessControlService.HasPermission("Offsite.Create"))
        {
            MessageBoxHelper.ShowWarning("You do not have permission to perform this action.");
            return;
        }
        using var form = new OffsiteRecordDetailsForm(_currentUserId); 
        if (form.ShowDialog() == DialogResult.OK) { _currentPage = 1; await LoadRecordsAsync(); } 
    }

    private async Task InitializeMapAsync()
    {
        try {
            string mapPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Maps", "offsite-tracking-map.html");
            if (!File.Exists(mapPath)) { _statusLabel.Text = "Unable to load map. Asset missing."; return; }
            await _mapWebView.EnsureCoreWebView2Async();
            _mapWebView.Source = new Uri(mapPath);
        } catch { _statusLabel.Text = "Unable to load map."; }
    }

    private async Task LoadCarsAsync()
    {
        try {
            IReadOnlyList<Car> cars = await _trackingService.GetTrackableCarsAsync();
            _carComboBox.BeginUpdate();
            _carComboBox.Items.Clear();
            _carComboBox.Items.Add("Select a car");
            foreach (Car car in cars) _carComboBox.Items.Add(new CarOption(car.CarId, car.CarName, car.PlateNumber));
            _carComboBox.SelectedIndex = 0;
            _carComboBox.EndUpdate();
        } catch { }
    }

    private async Task RefreshSelectedCarLocationAsync(bool showEmptyMessage)
    {
        if (_isRefreshing || _carComboBox.SelectedItem is not CarOption car) return;
        try {
            _isRefreshing = true;
            VehicleLocation? location = await _trackingService.GetLatestLocationAsync(car.CarId);
            if (location is null) { await ClearLocationDisplayAsync(car); if (showEmptyMessage) _statusLabel.Text = "No tracking data yet."; return; }
            UpdateLocationDisplay(car, location);
            await UpdateMapMarkerAsync(location, car.Label);
        } finally { _isRefreshing = false; }
    }

    private bool _isStartingDemo;

    private async Task StartDemoTrackingAsync()
    {
        if (!AccessControlService.HasPermission("Offsite.MapTracking"))
        {
            MessageBoxHelper.ShowWarning("You do not have permission to perform this action.");
            return;
        }

        if (_isStartingDemo) return;
        _isStartingDemo = true;
        try
        {
            if (_carComboBox.SelectedItem is not CarOption) { MessageBoxHelper.ShowWarning("Select a car before starting tracking."); return; }
            _startTrackingButton.Enabled = false; _stopTrackingButton.Enabled = true;
            _autoRefreshLabel.Text = "Refresh: 5s";
            _demoTimer.Start();
            await InsertDemoLocationAsync();
        }
        finally
        {
            _isStartingDemo = false;
        }
    }

    private void StopDemoTracking() { _demoTimer.Stop(); _startTrackingButton.Enabled = true; _stopTrackingButton.Enabled = false; _autoRefreshLabel.Text = "Refresh: 10m"; }

    private async Task InsertDemoLocationAsync()
    {
        if (_isDemoTickRunning || _carComboBox.SelectedItem is not CarOption car) return;
        try { _isDemoTickRunning = true; await _simulator.InsertNextAsync(car.CarId); await RefreshSelectedCarLocationAsync(showEmptyMessage: false); }
        catch { StopDemoTracking(); } finally { _isDemoTickRunning = false; }
    }

    private async Task UpdateMapMarkerAsync(VehicleLocation location, string label)
    {
        if (!_mapReady || _mapWebView.CoreWebView2 is null) return;
        string script = FormattableString.Invariant($"window.setVehicleLocation({location.Latitude}, {location.Longitude}, {JsonSerializer.Serialize(label)});");
        await _mapWebView.ExecuteScriptAsync(script);
    }

    private async Task ClearMapMarkerAsync() { if (_mapReady && _mapWebView.CoreWebView2 is not null) await _mapWebView.ExecuteScriptAsync("window.clearVehicleMarker();"); }

    private async void MapWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) { _mapReady = e.IsSuccess; await RefreshSelectedCarLocationAsync(showEmptyMessage: false); }

    private void UpdateLocationDisplay(CarOption car, VehicleLocation location)
    {
        _selectedCarValueLabel.Text = car.CarName; _plateNumberValueLabel.Text = car.PlateNumber;
        _latitudeValueLabel.Text = $"{location.Latitude:N7}"; _longitudeValueLabel.Text = $"{location.Longitude:N7}";
        _lastUpdatedValueLabel.Text = $"{location.RecordedAt:MMM d, yyyy h:mm tt}"; _sourceValueLabel.Text = location.Source;
    }

    private async Task ClearLocationDisplayAsync(CarOption car)
    {
        _selectedCarValueLabel.Text = car.CarName; _plateNumberValueLabel.Text = car.PlateNumber;
        _latitudeValueLabel.Text = "-"; _longitudeValueLabel.Text = "-"; _lastUpdatedValueLabel.Text = "-"; _sourceValueLabel.Text = "-";
        await ClearMapMarkerAsync();
    }

    private sealed record CarOption(int CarId, string CarName, string PlateNumber) { public string Label => $"{CarName} ({PlateNumber})"; public override string ToString() => Label; }
}
