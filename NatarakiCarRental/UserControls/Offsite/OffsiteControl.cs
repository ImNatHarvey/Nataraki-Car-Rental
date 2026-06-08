using System.Text.Json;
using System.Runtime.InteropServices;
using FontAwesome.Sharp;
using Microsoft.Web.WebView2.Core;
using NatarakiCarRental.Forms.Offsite;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.UserControls.Offsite;

public sealed class OffsiteControl : UserControl
{
    private readonly OffsiteService _offsiteService;
    private readonly VehicleTrackingService _trackingService = new();
    private readonly SecurityVerificationService _verificationService = new();
    private readonly int _currentUserId;

    private readonly IconButton _mapTrackingTabButton = new();
    private readonly IconButton _offsiteRecordsTabButton = new();
    private readonly Panel _mainContentPanel = new();
    private bool _isMapTabActive = true;

    // Map View Controls
    private Microsoft.Web.WebView2.WinForms.WebView2 _mapWebView = new();
    private readonly ComboBox _carSelector = new();
    private readonly MetricCardControl _lastSeenCard = new();
    private readonly MetricCardControl _statusCard = new();
    private readonly MetricCardControl _speedCard = new();
    private readonly Label _selectedCarNameLabel = new();
    private readonly Label _selectedCarPlateLabel = new();
    private readonly Label _selectedCarLocationLabel = new();
    private bool _mapReady;

    // Records View Controls
    private readonly DataGridView _recordsGrid = new();
    private readonly TextBox _searchBox = new();
    private readonly ComboBox _typeFilter = new();
    private readonly ComboBox _statusFilter = new();
    private readonly IconButton _recordsSubTabButton = new();
    private readonly IconButton _archivedSubTabButton = new();
    private readonly Button _addRecordButton;
    private readonly Label _emptyStateLabel = new();
    private readonly MetricCardControl _offsiteRecordsCard = new();
    private readonly MetricCardControl _maintenanceCarsCard = new();
    private readonly MetricCardControl _upcomingMaintenanceCard = new();
    private readonly MetricCardControl _completedRecordsCard = new();

    private int _currentPage = 1;
    private int _pageSize = 10;
    private int _totalItems;
    private readonly Label _paginationLabel = new();
    private readonly Button _prevPageButton = CreatePaginationButton("Previous");
    private readonly Button _nextPageButton = CreatePaginationButton("Next");
    private bool _showArchivedRecords;
    private bool _isInitializingRecordFilters;

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
        _addRecordButton = ControlFactory.CreatePrimaryButton("Add Offsite Record", 168, 36);
        InitializeControl();
    }

    private void InitializeControl()
    {
        BackColor = ThemeHelper.ContentBackground;
        Dock = DockStyle.Fill;
        Padding = new Padding(24, 8, 24, 24);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(CreateHeaderTabs(), 0, 0);
        _mainContentPanel.Dock = DockStyle.Fill;
        layout.Controls.Add(_mainContentPanel, 0, 1);

        Controls.Add(layout);
        _ = ShowMapTrackingViewAsync();

        Resize += OffsiteControl_Resize;
    }

    private Panel CreateHeaderTabs()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };

        ConfigureTabButton(_mapTrackingTabButton, "Map Tracking", IconChar.MapLocationDot, new Point(0, 10), 160);
        ConfigureTabButton(_offsiteRecordsTabButton, "Maintenance Records", IconChar.ClipboardList, new Point(168, 10), 200);

        _mapTrackingTabButton.Click += async (_, _) => await ShowMapTrackingViewAsync();
        _offsiteRecordsTabButton.Click += async (_, _) => await ShowRecordsViewAsync();

        panel.Controls.Add(_offsiteRecordsTabButton);
        panel.Controls.Add(_mapTrackingTabButton);
        return panel;
    }

    private bool _isMapInitialized;

    private async Task ShowMapTrackingViewAsync()
    {
        if (!_isMapInitialized)
        {
            await InitializeMapAsync();
            _isMapInitialized = true;
        }

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

    private void OffsiteControl_Resize(object? sender, EventArgs e)
    {
        if (_isMapTabActive || _recordsGrid.Columns.Count == 0) return;
        UpdateRecordsGridColumnLayout();
        
        int newPageSize = GetRecordsPageSize();
        if (newPageSize == _pageSize) return;
        _pageSize = newPageSize;
        _currentPage = 1;
        _ = LoadRecordsAsync(); 
    }

    private static void ConfigureTabButton(IconButton button, string text, IconChar icon, Point location, int width)
    {
        button.Text = text;
        button.IconChar = icon;
        button.IconSize = 18;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.Location = location;
        button.Size = new Size(width, 36);
        button.FlatStyle = FlatStyle.Flat;
        button.Cursor = Cursors.Hand;
        button.Font = FontHelper.SemiBold(10F);
        button.FlatAppearance.BorderSize = 0;
    }

    private static void ApplyTabStyle(IconButton button, bool isActive)
    {
        button.BackColor = isActive ? ThemeHelper.Primary : ThemeHelper.Surface;
        button.ForeColor = isActive ? Color.White : ThemeHelper.TextPrimary;
        button.IconColor = isActive ? Color.White : ThemeHelper.TextSecondary;
    }

    // MAP VIEW LOGIC
    private async Task InitializeMapAsync()
    {
        try
        {
            await _mapWebView.EnsureCoreWebView2Async();
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Maps", "offsite-tracking-map.html");
            if (File.Exists(htmlPath))
            {
                _mapWebView.CoreWebView2.Navigate($"file:///{htmlPath.Replace("\\", "/")}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 Error: {ex.Message}");
        }
    }

    private Control CreateMapTrackingLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F)); // Selector
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Map
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160F)); // Compact Info

        layout.Controls.Add(CreateTrackingControlsRow(), 0, 0);
        layout.Controls.Add(CreateMapContainer(), 0, 1);
        layout.Controls.Add(CreateSelectedCarInfoCard(), 0, 2);

        _ = PopulateCarSelector();
        return layout;
    }

    private Panel CreateTrackingControlsRow()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        Label label = new() { Text = "Track Vehicle:", Font = FontHelper.SemiBold(10F), AutoSize = true, Location = new Point(0, 18), ForeColor = ThemeHelper.TextPrimary };
        _carSelector.Location = new Point(110, 14);
        _carSelector.Size = new Size(300, 30);
        _carSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _carSelector.Font = FontHelper.Regular(10F);
        _carSelector.SelectedIndexChanged += async (_, _) => await HandleCarSelectionChangedAsync();

        IconButton refreshButton = new()
        {
            IconChar = IconChar.Rotate,
            IconSize = 18,
            Size = new Size(36, 30),
            Location = new Point(416, 14),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            BackColor = ThemeHelper.Surface
        };
        refreshButton.FlatAppearance.BorderColor = ThemeHelper.Border;
        refreshButton.Click += async (_, _) => await RefreshSelectedCarLocationAsync();

        panel.Controls.Add(label);
        panel.Controls.Add(_carSelector);
        panel.Controls.Add(refreshButton);
        return panel;
    }

    private Control CreateMapContainer()
    {
        BorderedPanel container = new() { Dock = DockStyle.Fill, Padding = new Padding(1), BorderColor = ThemeHelper.Border };
        _mapWebView.Dock = DockStyle.Fill;
        _mapWebView.NavigationCompleted += MapWebView_NavigationCompleted;
        container.Controls.Add(_mapWebView);
        return container;
    }

    private Control CreateSelectedCarInfoCard()
    {
        Panel panel = ControlFactory.CreateCardPanel(new Size(0, 0));
        panel.Dock = DockStyle.Fill;
        panel.Margin = new Padding(0, 16, 0, 0);
        panel.Padding = new Padding(24);

        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));

        // Left Column: Basic Car Info
        Panel carInfoPanel = new() { Dock = DockStyle.Fill };
        _selectedCarNameLabel.Text = "No car selected";
        _selectedCarNameLabel.Font = FontHelper.SemiBold(14F);
        _selectedCarNameLabel.ForeColor = ThemeHelper.Primary;
        _selectedCarNameLabel.AutoSize = true;
        _selectedCarNameLabel.Location = new Point(0, 0);

        _selectedCarPlateLabel.Text = "Select a vehicle to track";
        _selectedCarPlateLabel.Font = FontHelper.Regular(10F);
        _selectedCarPlateLabel.ForeColor = ThemeHelper.TextSecondary;
        _selectedCarPlateLabel.AutoSize = true;
        _selectedCarPlateLabel.Location = new Point(0, 28);

        Label locHeader = new() { Text = "Current Location:", Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextPrimary, AutoSize = true, Location = new Point(0, 60) };
        _selectedCarLocationLabel.Text = "-";
        _selectedCarLocationLabel.Font = FontHelper.Regular(9.5F);
        _selectedCarLocationLabel.ForeColor = ThemeHelper.TextPrimary;
        _selectedCarLocationLabel.AutoSize = false;
        _selectedCarLocationLabel.Size = new Size(320, 44);
        _selectedCarLocationLabel.Location = new Point(0, 80);

        carInfoPanel.Controls.Add(_selectedCarNameLabel);
        carInfoPanel.Controls.Add(_selectedCarPlateLabel);
        carInfoPanel.Controls.Add(locHeader);
        carInfoPanel.Controls.Add(_selectedCarLocationLabel);

        // Right Column: Live Metrics
        TableLayoutPanel metricsGrid = new() { Dock = DockStyle.Fill, ColumnCount = 3 };
        metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
        metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
        metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4F));

        _lastSeenCard.SetMetric(IconChar.Clock, "Last Seen", "N/A", "Last update time", ThemeHelper.GrayIcon);
        _statusCard.SetMetric(IconChar.CircleDot, "Engine Status", "Unknown", "Real-time state", ThemeHelper.GrayIcon);
        _speedCard.SetMetric(IconChar.GaugeHigh, "Current Speed", "0 km/h", "Movement velocity", ThemeHelper.GrayIcon);

        metricsGrid.Controls.Add(_lastSeenCard, 0, 0);
        metricsGrid.Controls.Add(_statusCard, 1, 0);
        metricsGrid.Controls.Add(_speedCard, 2, 0);

        foreach (Control c in metricsGrid.Controls) { c.Dock = DockStyle.Fill; c.Margin = new Padding(8, 0, 8, 0); }

        layout.Controls.Add(carInfoPanel, 0, 0);
        layout.Controls.Add(metricsGrid, 1, 0);

        panel.Controls.Add(layout);
        return panel;
    }

    // RECORDS VIEW LOGIC
    private Control CreateRecordsLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148F)); // Metrics
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F)); // Search
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F)); // Sub Tabs
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Table
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F)); // Pagination

        // Metrics Row
        TableLayoutPanel metricsGrid = new() { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0, 12, 0, 8) };
        for (int i = 0; i < 4; i++) metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

        _offsiteRecordsCard.SetMetric(IconChar.ClipboardList, "Active Records", "0", "Total", ThemeHelper.Primary);
        _maintenanceCarsCard.SetMetric(IconChar.ScrewdriverWrench, "Maintenance", "0", "Cars", ThemeHelper.Warning);
        _upcomingMaintenanceCard.SetMetric(IconChar.CalendarDays, "Upcoming", "0", "Next 7 days", ThemeHelper.GrayIcon);
        _completedRecordsCard.SetMetric(IconChar.CheckCircle, "Completed Records", "0", "Total", ThemeHelper.Success);

        AddMetricCard(metricsGrid, _offsiteRecordsCard, 0);
        AddMetricCard(metricsGrid, _maintenanceCarsCard, 1);
        AddMetricCard(metricsGrid, _upcomingMaintenanceCard, 2);
        AddMetricCard(metricsGrid, _completedRecordsCard, 3);
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
        DataGridViewHelper.ApplyStandardStyle(_recordsGrid);
        _recordsGrid.CellMouseClick -= RecordsGrid_CellMouseClick;
        _recordsGrid.CellMouseClick += RecordsGrid_CellMouseClick;
        _recordsGrid.CellMouseMove -= RecordsGrid_CellMouseMove;
        _recordsGrid.CellMouseMove += RecordsGrid_CellMouseMove;
        _recordsGrid.CellMouseLeave -= RecordsGrid_CellMouseLeave;
        _recordsGrid.CellMouseLeave += RecordsGrid_CellMouseLeave;
        _recordsGrid.Resize -= RecordsGrid_Resize;
        _recordsGrid.Resize += RecordsGrid_Resize;

        DataGridViewHelper.SetupStatusPills(_recordsGrid, "Status");
        DataGridViewHelper.SetupActionButtons(_recordsGrid);

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
        _recordsGrid.Columns.Add("Location", "Location");
        _recordsGrid.Columns.Add("Dates", "Dates");
        _recordsGrid.Columns.Add("Cost", "Cost");
        _recordsGrid.Columns.Add("Status", "Status");

        var actionsCol = new DataGridViewTextBoxColumn
        {
            Name = "Actions",
            HeaderText = "Actions",
            ReadOnly = true
        };
        _recordsGrid.Columns.Add(actionsCol);

        UpdateRecordsGridColumnLayout();
    }

    private async Task LoadRecordsAsync()
    {
        try
        {
            _pageSize = GetRecordsPageSize();
            var metrics = await _offsiteService.GetMetricsAsync(DateTime.Today);
            UpdateMetricCards(metrics);

            string? status = _statusFilter.SelectedIndex > 0 ? _statusFilter.SelectedItem?.ToString() : null;
            string? type = _typeFilter.SelectedIndex > 0 ? _typeFilter.SelectedItem?.ToString() : null;

            var items = await _offsiteService.GetListAsync(
                _searchBox.Text, 
                status,
                type,
                _showArchivedRecords,
                _currentPage,
                _pageSize);

            _totalItems = await _offsiteService.CountAsync(_searchBox.Text, status, type, _showArchivedRecords);
            PopulateRecordsGrid(items);
        }
        catch (Exception ex)
        {
            MessageBoxHelper.ShowError($"Failed to load records: {ex.Message}");
        }
    }

    private void PopulateRecordsGrid(IReadOnlyList<OffsiteRecordListItem> allItems)
    {
        _recordsGrid.Rows.Clear();
        int totalPages = Math.Max(1, (int)Math.Ceiling(_totalItems / (double)_pageSize));
        if (_currentPage > totalPages) _currentPage = totalPages;

        _paginationLabel.Text = $"Page {_currentPage} of {totalPages} ({_totalItems} records)";
        _prevPageButton.Enabled = _currentPage > 1;
        _nextPageButton.Enabled = _currentPage < totalPages;

        foreach (var item in allItems)
        {
            string actions = string.Join("|", GetRowActions(item));
            
            int rowIndex = _recordsGrid.Rows.Add(
                $"{item.CarName} ({item.PlateNumber})",
                item.OffsiteType,
                item.LocationName ?? "-",
                $"{item.StartDate:MMM d} - {item.ExpectedReturnDate:MMM d}",
                LayoutHelper.FormatPeso(item.EstimatedCost),
                item.Status,
                actions);
            
            _recordsGrid.Rows[rowIndex].Tag = item;
        }

        _emptyStateLabel.Visible = _totalItems == 0;
    }

    private void UpdateMetricCards(OffsiteMetrics metrics)
    {
        _offsiteRecordsCard.SetMetric(IconChar.ClipboardList, "Active Records", metrics.TotalOffsiteRecords.ToString(), "Total", ThemeHelper.Primary);
        _maintenanceCarsCard.SetMetric(IconChar.ScrewdriverWrench, "Maintenance", metrics.MaintenanceCars.ToString(), "Cars", ThemeHelper.Warning);
        _upcomingMaintenanceCard.SetMetric(IconChar.CalendarDays, "Upcoming", metrics.UpcomingMaintenance.ToString(), "Next 7 days", ThemeHelper.GrayIcon);
        _completedRecordsCard.SetMetric(IconChar.CheckCircle, "Completed Records", metrics.CompletedRecords.ToString(), "Total", ThemeHelper.Success);
    }

    private void RecordsGrid_Resize(object? sender, EventArgs e) => UpdateRecordsGridColumnLayout();

    private int GetRecordsPageSize() => Math.Max(1, (_recordsGrid.Height - _recordsGrid.ColumnHeadersHeight) / _recordsGrid.RowTemplate.Height);

    private async void SearchBox_TextChanged(object? sender, EventArgs e)
    {
        _currentPage = 1;
        await LoadRecordsAsync();
    }

    private void AddMetricCard(TableLayoutPanel grid, MetricCardControl card, int column)
    {
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(column == 0 ? 0 : 12, 0, column == 3 ? 0 : 12, 0);
        grid.Controls.Add(card, column, 0);
    }

    private List<string> GetRowActions(OffsiteRecordListItem item)
    {
        if (_showArchivedRecords)
            return new List<string> { "View", "Restore" };

        if (string.Equals(item.Status, "Ongoing", StringComparison.OrdinalIgnoreCase))
            return new List<string> { "View", "Edit", "Complete", "Cancel" };

        return new List<string> { "View", "Archive" };
    }

    private async void RecordsGrid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Button != MouseButtons.Left) return;
        if (_recordsGrid.Columns[e.ColumnIndex].Name != "Actions") return;

        if (_recordsGrid.Rows[e.RowIndex].Tag is not OffsiteRecordListItem item) return;

        string? clickedAction = DataGridViewHelper.GetClickedAction(_recordsGrid, e.RowIndex, e.ColumnIndex, e.X, e.Y);
        if (clickedAction == null) return;

        switch (clickedAction)
        {
            case "View": ShowDetails(item.OffsiteRecordId, true); break;
            case "Edit": ShowDetails(item.OffsiteRecordId, false); break;
            case "Complete": await CompleteRecord(item.OffsiteRecordId); break;
            case "Cancel": await CancelRecord(item.OffsiteRecordId); break;
            case "Archive": await ArchiveRecord(item.OffsiteRecordId); break;
            case "Restore": await RestoreRecord(item.OffsiteRecordId); break;
        }
    }

    private void RecordsGrid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
    {
        _recordsGrid.Cursor = DataGridViewHelper.GetClickedAction(_recordsGrid, e.RowIndex, e.ColumnIndex, e.X, e.Y) is null ? Cursors.Default : Cursors.Hand;
    }

    private void RecordsGrid_CellMouseLeave(object? sender, DataGridViewCellEventArgs e)
    {
        _recordsGrid.Cursor = Cursors.Default;
    }

    private void ShowDetails(int recordId, bool isReadOnly)
    {
        using OffsiteRecordDetailsForm form = new(_currentUserId, recordId, isReadOnly);
        if (form.ShowDialog(this) == DialogResult.OK) _ = LoadRecordsAsync();
    }

    private async Task AddRecordAsync()
    {
        if (!AccessControlService.HasPermission("Offsite.Create"))
        {
            MessageBoxHelper.ShowWarning("Permission denied.");
            return;
        }

        using OffsiteRecordDetailsForm form = new(_currentUserId);
        if (form.ShowDialog(this) == DialogResult.OK) await LoadRecordsAsync();
    }

    private async Task CompleteRecord(int recordId)
    {
        if (!AccessControlService.HasPermission("Offsite.Complete"))
        {
            MessageBoxHelper.ShowWarning("Permission denied.");
            return;
        }

        if (!await _verificationService.RequireOwnerVerificationIfNeededAsync(_currentUserId, "Complete offsite record"))
        {
            return;
        }

        using OffsiteRecordDetailsForm form = new(_currentUserId, recordId, false, true);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            MessageBoxHelper.ShowSuccess("Offsite record completed successfully.");
            await LoadRecordsAsync();
        }
    }

    private async Task CancelRecord(int recordId)
    {
        if (!AccessControlService.HasPermission("Offsite.Cancel"))
        {
            MessageBoxHelper.ShowWarning("Permission denied.");
            return;
        }

        if (!await _verificationService.RequireOwnerVerificationIfNeededAsync(_currentUserId, "Cancel offsite record"))
        {
            return;
        }

        if (MessageBoxHelper.ShowConfirmDanger("Cancel this offsite record?", "Cancel Record"))
        {
            await _offsiteService.CancelAsync(recordId);
            await LoadRecordsAsync();
        }
    }

    private async Task ArchiveRecord(int recordId)
    {
        if (!AccessControlService.HasPermission("Offsite.ArchiveRestore"))
        {
            MessageBoxHelper.ShowWarning("Permission denied.");
            return;
        }

        if (MessageBoxHelper.ShowConfirmDanger("Archive this offsite record?", "Archive Record"))
        {
            await _offsiteService.ArchiveAsync(recordId);
            await LoadRecordsAsync();
        }
    }

    private async Task RestoreRecord(int recordId)
    {
        if (!AccessControlService.HasPermission("Offsite.ArchiveRestore"))
        {
            MessageBoxHelper.ShowWarning("Permission denied.");
            return;
        }

        if (MessageBoxHelper.ShowConfirmWarning("Restore this offsite record?", "Restore Record"))
        {
            await _offsiteService.RestoreAsync(recordId);
            await LoadRecordsAsync();
        }
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

    private void UpdateRecordsGridColumnLayout()
    {
        if (_recordsGrid.Columns.Count == 0) return;

        bool isNarrow = _recordsGrid.Width < 800;
        _recordsGrid.Columns["Car"]!.FillWeight = 120;
        _recordsGrid.Columns["Type"]!.FillWeight = 100;
        _recordsGrid.Columns["Location"]!.FillWeight = 120;
        _recordsGrid.Columns["Dates"]!.FillWeight = 130;
        _recordsGrid.Columns["Cost"]!.FillWeight = 80;
        _recordsGrid.Columns["Status"]!.FillWeight = 90;
        _recordsGrid.Columns["Actions"]!.FillWeight = 150;
        
        if (!isNarrow)
        {
            _recordsGrid.Columns["Actions"]!.MinimumWidth = 300;
            _recordsGrid.Columns["Actions"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            _recordsGrid.Columns["Actions"]!.Width = 300;
        }
    }

    // WEBVIEW MAP COMMUNICATIONS
    private async void MapWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) { _mapReady = e.IsSuccess; await RefreshSelectedCarLocationAsync(showEmptyMessage: false); }

    private async Task HandleCarSelectionChangedAsync()
    {
        await RefreshSelectedCarLocationAsync();
    }

    private async Task RefreshSelectedCarLocationAsync(bool showEmptyMessage = true)
    {
        if (!_mapReady || _carSelector.SelectedItem is not CarOption car) return;

        try
        {
            var location = await _trackingService.GetLatestLocationAsync(car.CarId);
            if (location != null)
            {
                UpdateCarInfoDisplay(car, location);
                await UpdateMapMarkerAsync(location, car);
            }
            else
            {
                ResetCarInfoDisplay(car);
                if (showEmptyMessage) MessageBoxHelper.ShowInfo($"No tracking data available for {car.CarName}.", "Vehicle Tracking");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tracking Error: {ex.Message}");
        }
    }

    private async Task PopulateCarSelector()
    {
        try
        {
            var cars = await _trackingService.GetTrackableCarsAsync();
            _carSelector.Items.Clear();
            foreach (var car in cars)
            {
                _carSelector.Items.Add(new CarOption(car.CarId, car.CarName, car.PlateNumber));
            }

            if (_carSelector.Items.Count > 0) _carSelector.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Car Selector Error: {ex.Message}");
        }
    }

    private void UpdateCarInfoDisplay(CarOption car, VehicleLocation loc)
    {
        _selectedCarNameLabel.Text = car.CarName;
        _selectedCarPlateLabel.Text = car.PlateNumber;
        _selectedCarLocationLabel.Text = $"{loc.Latitude:F6}, {loc.Longitude:F6}";
        _lastSeenCard.SetMetric(IconChar.Clock, "Last Seen", loc.RecordedAt.ToString("h:mm tt"), "Last update time", ThemeHelper.Primary);
        _statusCard.SetMetric(IconChar.CircleDot, "Engine Status", "Active", "Real-time state", ThemeHelper.Success);
        _speedCard.SetMetric(IconChar.GaugeHigh, "Current Speed", $"{loc.SpeedKph:F1} km/h", "Movement velocity", (loc.SpeedKph ?? 0) > 0 ? ThemeHelper.Success : ThemeHelper.GrayIcon);
    }

    private void ResetCarInfoDisplay(CarOption car)
    {
        _selectedCarNameLabel.Text = car.CarName;
        _selectedCarPlateLabel.Text = car.PlateNumber;
        _selectedCarLocationLabel.Text = "Location unknown or tracking disabled.";
        _lastSeenCard.SetMetric(IconChar.Clock, "Last Seen", "N/A", "Last update time", ThemeHelper.GrayIcon);
        _statusCard.SetMetric(IconChar.CircleDot, "Engine Status", "Unknown", "Real-time state", ThemeHelper.GrayIcon);
        _speedCard.SetMetric(IconChar.GaugeHigh, "Current Speed", "0 km/h", "Movement velocity", ThemeHelper.GrayIcon);
    }

    private async Task UpdateMapMarkerAsync(VehicleLocation loc, CarOption car)
    {
        if (!_mapReady) return;

        var data = new { lat = loc.Latitude, lng = loc.Longitude, title = car.CarName, plate = car.PlateNumber };
        string json = JsonSerializer.Serialize(data);
        await _mapWebView.CoreWebView2.ExecuteScriptAsync($"updateMarker({json})");
    }

    private async Task ClearMapMarkerAsync()
    {
        if (!_mapReady) return;
        await _mapWebView.CoreWebView2.ExecuteScriptAsync("clearMarker()");
    }

    private sealed record CarOption(int CarId, string CarName, string PlateNumber) { public string Label => $"{CarName} ({PlateNumber})"; public override string ToString() => Label; }
}
