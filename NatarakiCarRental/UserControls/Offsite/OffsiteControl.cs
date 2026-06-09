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
using NatarakiCarRental.Forms.Transactions;
using NatarakiCarRental.UserControls.Cards;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.UserControls.Offsite;

public sealed class OffsiteControl : UserControl
{
    private const float ActionPillHeight = 26F;
    private static readonly TimeSpan NormalRefreshInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DemoInterval = TimeSpan.FromSeconds(5);

    private readonly VehicleTrackingService _trackingService = new();
    private readonly VehicleTrackingSimulator _simulator = new();
    private readonly TransactionService _transactionService;
    private readonly SecurityVerificationService _verificationService = new();
    private readonly int _currentUserId;
    
    private readonly IconButton _mapTrackingTabButton = new();
    private readonly IconButton _maintenanceTabButton = new();
    private readonly Panel _mainContentPanel = new();

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

    private readonly DataGridView _maintenanceGrid = new();
    private readonly IconButton _recordsSubTabButton = new();
    private readonly IconButton _archivedSubTabButton = new();
    private readonly TextBox _searchBox = new();
    private readonly ComboBox _statusFilter = new();
    private readonly Button _createTransactionButton = CreateAddButton();
    private readonly Label _emptyStateLabel = new();
    private readonly Label _paginationLabel = new();
    private readonly Button _prevPageButton = CreatePaginationButton("Previous");
    private readonly Button _nextPageButton = CreatePaginationButton("Next");
    
    private readonly MetricCardControl _maintenanceRecordsCard = new();
    private readonly MetricCardControl _activeMaintenanceCard = new();
    private readonly MetricCardControl _upcomingMaintenanceCard = new();
    private readonly MetricCardControl _completedMaintenanceCard = new();

    private bool _mapReady;
    private bool _isRefreshing;
    private bool _isDemoTickRunning;
    private bool _isInitializingFilters;
    private bool _isLoadingRecords;
    private bool _pendingRecordsReload;
    private int _currentPage = 1;
    private int _pageSize = 13;
    private int _totalItems;
    private bool _isMapTabActive = false;
    private bool _showArchived;

    public OffsiteControl(int currentUserId)
    {
        _currentUserId = currentUserId;
        _transactionService = new TransactionService(currentUserId);
        InitializeControl();
        Load += OffsiteControl_Load;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop(); _demoTimer.Stop(); _recordsSearchTimer.Stop();
            _refreshTimer.Dispose(); _demoTimer.Dispose(); _recordsSearchTimer.Dispose();
            _mapWebView.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeControl()
    {
        BackColor = ThemeHelper.ContentBackground; Dock = DockStyle.Fill; Padding = new Padding(32, 8, 32, 32);
        TableLayoutPanel mainLayout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.Controls.Add(CreateMainTabSwitcher(), 0, 0);
        _mainContentPanel.Dock = DockStyle.Fill; _mainContentPanel.BackColor = ThemeHelper.ContentBackground;
        mainLayout.Controls.Add(_mainContentPanel, 0, 1);
        Controls.Add(mainLayout);
        Resize += OffsiteControl_Resize;
        _refreshTimer.Interval = (int)NormalRefreshInterval.TotalMilliseconds;
        _refreshTimer.Tick += async (_, _) => await RefreshSelectedCarLocationAsync(false);
        _demoTimer.Interval = (int)DemoInterval.TotalMilliseconds;
        _demoTimer.Tick += async (_, _) => await InsertDemoLocationAsync();
        _recordsSearchTimer.Tick += RecordsSearchTimer_Tick;

        // Wire up events once
        _mapTrackingTabButton.Click += (_, _) => ShowMapTrackingView();
        _maintenanceTabButton.Click += async (_, _) => await ShowMaintenanceViewAsync();
        _carComboBox.SelectedIndexChanged += async (_, _) => await RefreshSelectedCarLocationAsync(true);
        _refreshButton.Click += async (_, _) => await RefreshSelectedCarLocationAsync(true);
        _startTrackingButton.Click += async (_, _) => await StartDemoTrackingAsync();
        _stopTrackingButton.Click += (_, _) => StopDemoTracking();
        _createTransactionButton.Click += CreateTransactionButton_Click;
        _recordsSubTabButton.Click += RecordsSubTabButton_Click;
        _archivedSubTabButton.Click += ArchivedSubTabButton_Click;
        _prevPageButton.Click += PrevPageButton_Click;
        _nextPageButton.Click += NextPageButton_Click;
        _statusFilter.SelectedIndexChanged += StatusFilter_SelectedIndexChanged;
        _searchBox.TextChanged += SearchBox_TextChanged;
    }

    private Panel CreateMainTabSwitcher()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        ConfigureTabButton(_maintenanceTabButton, "Maintenance Records", IconChar.ClipboardList, new Point(0, 10), 220);
        ConfigureTabButton(_mapTrackingTabButton, "Live Map Tracking", IconChar.MapLocationDot, new Point(228, 10), 200);
        panel.Controls.Add(_maintenanceTabButton); panel.Controls.Add(_mapTrackingTabButton);
        return panel;
    }

    private async void ShowMapTrackingView()
    {
        if (!_mapReady) await InitializeMapAsync();
        _isMapTabActive = true; UpdateMainTabStyles();
        _mainContentPanel.Controls.Clear(); _mainContentPanel.Controls.Add(CreateMapTrackingLayout());
    }

    private async Task ShowMaintenanceViewAsync()
    {
        _isMapTabActive = false; UpdateMainTabStyles();
        _mainContentPanel.Controls.Clear(); _mainContentPanel.Controls.Add(CreateMaintenanceLayout());
        await LoadRecordsAsync();
    }

    private void UpdateMainTabStyles() { ApplyTabStyle(_mapTrackingTabButton, _isMapTabActive); ApplyTabStyle(_maintenanceTabButton, !_isMapTabActive); }

    private async void OffsiteControl_Resize(object? sender, EventArgs e)
    {
        if (_isMapTabActive || _maintenanceGrid.Columns.Count == 0) return;
        UpdateGridColumnLayout();
        int newPageSize = GetRecordsPageSize();
        if (newPageSize == _pageSize) return;
        _pageSize = newPageSize; _currentPage = 1; await LoadRecordsAsync();
    }

    private Control CreateMapTrackingLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(0, 12, 0, 0) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116F));
        layout.Controls.Add(CreateTrackingControlsRow(), 0, 0); layout.Controls.Add(CreateMapContainer(), 0, 1); layout.Controls.Add(CreateSelectedCarInfoCard(), 0, 2);
        return layout;
    }

    private Control CreateTrackingControlsRow()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        FlowLayoutPanel flow = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        Label label = new() { Text = "Tracking Vehicle", AutoSize = true, Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextSecondary, Margin = new Padding(0, 9, 8, 0) };
        _carComboBox.Width = 220; _carComboBox.DropDownStyle = ComboBoxStyle.DropDownList; _carComboBox.Font = FontHelper.Regular(10F); _carComboBox.Margin = new Padding(0, 2, 12, 0);
        _autoRefreshLabel.Text = "Refresh: 10m"; _autoRefreshLabel.AutoSize = true; _autoRefreshLabel.Font = FontHelper.SemiBold(9F); _autoRefreshLabel.ForeColor = ThemeHelper.TextSecondary; _autoRefreshLabel.Margin = new Padding(0, 10, 0, 0);
        flow.Controls.AddRange([label, _carComboBox, _refreshButton, _startTrackingButton, _stopTrackingButton, _autoRefreshLabel]);
        panel.Controls.Add(flow); return panel;
    }

    private Control CreateMapContainer() { Panel card = ControlFactory.CreateCardPanel(new Size(0, 0)); card.Dock = DockStyle.Fill; card.Padding = new Padding(2); _mapWebView.Dock = DockStyle.Fill; card.Controls.Add(_mapWebView); return card; }

    private Control CreateSelectedCarInfoCard()
    {
        Panel card = ControlFactory.CreateCardPanel(new Size(0, 100)); card.Dock = DockStyle.Fill; card.Padding = new Padding(22, 16, 22, 16);
        TableLayoutPanel grid = new() { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 2 };
        for (int i = 0; i < 6; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        AddInfoColumn(grid, "Selected Car", _selectedCarValueLabel, 0); AddInfoColumn(grid, "Plate Number", _plateNumberValueLabel, 1); AddInfoColumn(grid, "Last Latitude", _latitudeValueLabel, 2); AddInfoColumn(grid, "Last Longitude", _longitudeValueLabel, 3); AddInfoColumn(grid, "Last Updated", _lastUpdatedValueLabel, 4); AddInfoColumn(grid, "Source", _sourceValueLabel, 5);
        card.Controls.Add(grid); return card;
    }

    private Control CreateMaintenanceLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148F)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        
        TableLayoutPanel metricsGrid = new() { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Padding = new Padding(0, 12, 0, 8) };
        for (int i = 0; i < 4; i++) metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        AddMetricCard(metricsGrid, _maintenanceRecordsCard, 0); AddMetricCard(metricsGrid, _activeMaintenanceCard, 1); AddMetricCard(metricsGrid, _upcomingMaintenanceCard, 2); AddMetricCard(metricsGrid, _completedMaintenanceCard, 3);
        layout.Controls.Add(metricsGrid, 0, 0);

        Panel searchRow = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        BorderedPanel searchContainer = new() { Size = new Size(280, 32), Location = new Point(0, 8), BackColor = ThemeHelper.Surface, BorderColor = ThemeHelper.Border, Cursor = Cursors.IBeam };
        IconPictureBox searchIcon = new() { IconChar = IconChar.MagnifyingGlass, IconColor = ThemeHelper.TextSecondary, IconSize = 18, BackColor = ThemeHelper.Surface, Location = new Point(8, 7), Size = new Size(20, 20) };
        _searchBox.BorderStyle = BorderStyle.None; _searchBox.PlaceholderText = "Search code, car, client..."; _searchBox.BackColor = ThemeHelper.Surface; _searchBox.Font = FontHelper.Regular(10F); _searchBox.ForeColor = ThemeHelper.TextPrimary; _searchBox.Location = new Point(34, 7); _searchBox.Width = 230;
        searchContainer.Controls.Add(searchIcon); searchContainer.Controls.Add(_searchBox); ConfigureFilters();
        _createTransactionButton.Location = new Point(0, 6);
        searchRow.Resize += (_, _) => _createTransactionButton.Left = Math.Max(0, searchRow.Width - _createTransactionButton.Width);
        searchRow.Controls.AddRange([searchContainer, _statusFilter, _createTransactionButton]);
        layout.Controls.Add(searchRow, 0, 1);

        Panel subTabRow = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        ConfigureTabButton(_recordsSubTabButton, "Active Records", IconChar.ListUl, new Point(0, 10), 160); ConfigureTabButton(_archivedSubTabButton, "Archived", IconChar.BoxArchive, new Point(168, 10), 120);
        UpdateSubTabStyles(); subTabRow.Controls.AddRange([_recordsSubTabButton, _archivedSubTabButton]);
        layout.Controls.Add(subTabRow, 0, 2);

        Panel tableCard = ControlFactory.CreateCardPanel(new Size(0, 0)); tableCard.Dock = DockStyle.Fill; tableCard.Padding = new Padding(18);
        SetupGrid(); tableCard.Controls.AddRange([_maintenanceGrid, _emptyStateLabel]);
        layout.Controls.Add(tableCard, 0, 3); layout.Controls.Add(CreatePaginationPanel(), 0, 4);
        return layout;
    }

    private Panel CreatePaginationPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        _prevPageButton.Location = new Point(0, 8);
        _nextPageButton.Location = new Point(90, 8);
        _paginationLabel.AutoSize = false; _paginationLabel.Location = new Point(180, 8); _paginationLabel.Size = new Size(300, 32); _paginationLabel.TextAlign = ContentAlignment.MiddleLeft; _paginationLabel.Font = FontHelper.Regular(9.5F); _paginationLabel.ForeColor = ThemeHelper.TextSecondary;
        panel.Controls.AddRange([_prevPageButton, _nextPageButton, _paginationLabel]); return panel;
    }

    private void ConfigureFilters()
    {
        _isInitializingFilters = true; _statusFilter.DropDownStyle = ComboBoxStyle.DropDownList; _statusFilter.Font = FontHelper.Regular(10F); _statusFilter.Items.Clear(); _statusFilter.Items.AddRange(["All Status", "Scheduled", "Maintenance", "Completed", "Cancelled"]); _statusFilter.SelectedIndex = 0; _statusFilter.Size = new Size(180, 30); _statusFilter.Location = new Point(292, 8); _isInitializingFilters = false;
    }

    private async Task LoadRecordsAsync()
    {
        if (_isLoadingRecords) { _pendingRecordsReload = true; return; }
        _isLoadingRecords = true; try { do { _pendingRecordsReload = false; await LoadRecordsCoreAsync(); } while (_pendingRecordsReload); }
        finally { _isLoadingRecords = false; }
    }

    private async Task LoadRecordsCoreAsync()
    {
        try {
            _pageSize = GetRecordsPageSize(); string? status = _statusFilter.SelectedIndex > 0 ? _statusFilter.SelectedItem?.ToString() : null;
            var allItems = await _transactionService.SearchTransactionsAsync(_searchBox.Text, status, null, _showArchived, "Maintenance", 500);
            _totalItems = allItems.Count; int totalPages = Math.Max(1, (int)Math.Ceiling(_totalItems / (double)_pageSize));
            if (_currentPage > totalPages) _currentPage = totalPages;
            var pagedItems = allItems.Skip((_currentPage - 1) * _pageSize).Take(_pageSize).ToList();
            _maintenanceGrid.Rows.Clear();
            foreach (var item in pagedItems) {
   int rowIndex = _maintenanceGrid.Rows.Add(item.TransactionCode, $"{item.CarName} ({item.PlateNumber})", item.CustomerName, item.TransactionStatus, $"{item.StartDate:MMM d} - {item.EndDate:MMM d}", $"₱{item.TotalAmount:N2}", $"₱{item.AmountPaid:N2}", $"₱{item.BalanceAmount:N2}", string.Join("|", GetRowActions(item)));
                _maintenanceGrid.Rows[rowIndex].Tag = item;
            }
            _emptyStateLabel.Visible = !pagedItems.Any(); UpdatePagination(totalPages); await UpdateMetrics();
        } catch (Exception ex) { MessageBoxHelper.ShowError($"Failed to load maintenance: {ex.Message}"); }
    }

    private List<string> GetRowActions(TransactionListItem item) 
    { 
        if (_showArchived) return ["View", "Restore"]; 
        if (item.TransactionStatus == TransactionConstants.Status.Scheduled)
        {
            var actions = new List<string> { "View" };
            if (item.StartDate.Date <= DateTime.Today) actions.Add("Start");
            actions.Add("Cancel");
            return actions;
        }
        if (item.TransactionStatus == TransactionConstants.Status.Maintenance) return ["View", "Extend", "Complete", "Cancel"];
        return ["View", "Archive"];
        }

        private void SetupGrid()
        {
        DataGridViewHelper.ApplyStandardStyle(_maintenanceGrid); _maintenanceGrid.Dock = DockStyle.Fill;
        _maintenanceGrid.CellMouseClick += Grid_CellMouseClick; _maintenanceGrid.CellMouseMove += Grid_CellMouseMove; _maintenanceGrid.CellMouseLeave += (_, _) => _maintenanceGrid.Cursor = Cursors.Default;
        DataGridViewHelper.SetupStatusPills(_maintenanceGrid, "Status"); DataGridViewHelper.SetupActionButtons(_maintenanceGrid);
        _emptyStateLabel.Text = "No maintenance transactions found."; _emptyStateLabel.Dock = DockStyle.Bottom; _emptyStateLabel.Height = 42; _emptyStateLabel.Font = FontHelper.Regular(10F); _emptyStateLabel.ForeColor = ThemeHelper.TextSecondary; _emptyStateLabel.TextAlign = ContentAlignment.MiddleCenter; _emptyStateLabel.Visible = false;
        _maintenanceGrid.Columns.Clear(); _maintenanceGrid.Columns.Add("Code", "Code"); _maintenanceGrid.Columns.Add("Vehicle", "Vehicle / Plate"); _maintenanceGrid.Columns.Add("Client", "Client / Partner"); _maintenanceGrid.Columns.Add("Status", "Status"); _maintenanceGrid.Columns.Add("Dates", "Duration"); _maintenanceGrid.Columns.Add("Cost", "Cost"); _maintenanceGrid.Columns.Add("Paid", "Paid"); _maintenanceGrid.Columns.Add("Balance", "Balance"); _maintenanceGrid.Columns.Add("Actions", "Actions");
        UpdateGridColumnLayout();
        }

        private void UpdateGridColumnLayout()
        {
        if (_maintenanceGrid.Columns.Count == 0) return;
        SetColumnSizing("Code", 10F, 100); SetColumnSizing("Vehicle", 10F, 110); SetColumnSizing("Client", 15F, 140); SetColumnSizing("Status", 10F, 90); SetColumnSizing("Dates", 12F, 120); SetColumnSizing("Cost", 10F, 100); SetColumnSizing("Paid", 10F, 100); SetColumnSizing("Balance", 10F, 100);
        if (_maintenanceGrid.Columns["Actions"] is DataGridViewColumn col) { col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None; col.Width = 350; col.MinimumWidth = 350; }
        }

    private void SetColumnSizing(string name, float weight, int min) { if (_maintenanceGrid.Columns[name] is DataGridViewColumn c) { c.FillWeight = weight; c.MinimumWidth = min; c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; } }

    private async void Grid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Button != MouseButtons.Left || _maintenanceGrid.Columns[e.ColumnIndex].Name != "Actions" || _maintenanceGrid.Rows[e.RowIndex].Tag is not TransactionListItem item) return;
        string? action = DataGridViewHelper.GetClickedAction(_maintenanceGrid, e.RowIndex, e.ColumnIndex, e.X, e.Y);
        if (action == null) return;
        switch (action) {
            case "View": await ViewTransactionAsync(item.TransactionId); break;
            case "Start": await StartMaintenanceAsync(item.TransactionId); break;
            case "Extend": await ExtendTransactionAsync(item.TransactionId); break;
            case "Complete": await CompleteTransactionAsync(item.TransactionId); break;
            case "Cancel": await CancelTransactionAsync(item.TransactionId); break;
            case "Archive": await ArchiveTransactionAsync(item.TransactionId); break;
            case "Restore": await RestoreTransactionAsync(item.TransactionId); break;
        }
    }

    private void Grid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e) { if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _maintenanceGrid.Rows[e.RowIndex].Tag is TransactionListItem) _maintenanceGrid.Cursor = DataGridViewHelper.GetClickedAction(_maintenanceGrid, e.RowIndex, e.ColumnIndex, e.X, e.Y) != null ? Cursors.Hand : Cursors.Default; }

    private static Button CreateAddButton()
    {
        IconButton button = new() { Text = "Create Maintenance", IconChar = IconChar.Plus, IconColor = Color.White, IconSize = 14, Size = new Size(180, 36), BackColor = ThemeHelper.Primary, ForeColor = Color.White, Font = FontHelper.SemiBold(9F), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, TextImageRelation = TextImageRelation.ImageBeforeText };
        button.FlatAppearance.BorderSize = 0; return button;
    }

    private static Button CreatePaginationButton(string text)
    {
        Button button = new() { Text = text, Size = new Size(80, 32), BackColor = ThemeHelper.Surface, ForeColor = ThemeHelper.TextPrimary, Font = FontHelper.SemiBold(9F), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        button.FlatAppearance.BorderColor = ThemeHelper.Border; return button;
    }

    private async Task ViewTransactionAsync(int id) { Transaction? txn = await _transactionService.GetByIdAsync(id); if (txn != null) new MaintenanceTransactionDetailsForm(_currentUserId, txn, true).ShowDialog(); }
    private async Task StartMaintenanceAsync(int id) 
    { 
        try 
        { 
            Transaction? txn = await _transactionService.GetByIdAsync(id);
            if (txn != null && txn.StartDate.Date > DateTime.Today)
            {
                MessageBoxHelper.ShowError("Cannot start maintenance before its scheduled start date.");
                return;
            }
            await _transactionService.StartMaintenanceTransactionAsync(id, _currentUserId); 
            MessageBoxHelper.ShowSuccess("Maintenance started successfully."); 
            await LoadRecordsAsync(); 
        } 
        catch (Exception ex) 
        { 
            MessageBoxHelper.ShowError($"Failed to start maintenance: {ex.Message}"); 
        } 
    }
    private async Task ExtendTransactionAsync(int id)
    {
        Transaction? txn = await _transactionService.GetByIdAsync(id);
        if (txn == null) return;
        
        using var form = new MaintenanceExtendForm(txn);
        if (form.ShowDialog() == DialogResult.OK)
        {
            try
            {
                await _transactionService.ExtendRentalAsync(id, form.NewEndDate, TransactionConstants.ModeOfPayment.Cash, 0, null, _currentUserId);
                MessageBoxHelper.ShowSuccess("Maintenance extended successfully.");
                await LoadRecordsAsync();
            }
            catch (Exception ex)
            {
                MessageBoxHelper.ShowError($"Failed to extend maintenance: {ex.Message}");
            }
        }
    }

    private async Task CompleteTransactionAsync(int id)
    {
        Transaction? txn = await _transactionService.GetByIdAsync(id);
        if (txn == null) return;

        using var form = new MaintenanceCompleteForm(txn);
        if (form.ShowDialog() == DialogResult.OK)
        {
            try
            {
                await _transactionService.CompleteTransactionAsync(new CompleteTransactionRequest
                {
                    TransactionId = id,
                    ReturnCondition = form.ReturnCondition,
                    AdditionalCharge = form.MaintenanceFee,
                    ChargePaid = true,
                    ModeOfPayment = form.ModeOfPayment,
                    ReceiptFilePath = form.InvoiceFilePath
                }, _currentUserId);
                MessageBoxHelper.ShowSuccess("Maintenance completed successfully.");
                await LoadRecordsAsync();
            }
            catch (Exception ex)
            {
                MessageBoxHelper.ShowError($"Failed to complete maintenance: {ex.Message}");
            }
        }
    }
    private async Task CancelTransactionAsync(int id) { if (await _verificationService.RequireOwnerVerificationIfNeededAsync(_currentUserId, "Cancel maintenance") && MessageBoxHelper.ShowConfirmWarning("Cancel maintenance transaction?", "Cancel")) { await _transactionService.CancelTransactionAsync(id, _currentUserId); await LoadRecordsAsync(); } }
    private async Task ArchiveTransactionAsync(int id) { await _transactionService.ArchiveTransactionAsync(id, _currentUserId); await LoadRecordsAsync(); }
    private async Task RestoreTransactionAsync(int id) { await _transactionService.RestoreTransactionAsync(id, _currentUserId); await LoadRecordsAsync(); }

    private void SearchBox_TextChanged(object? sender, EventArgs e) { if (!_isInitializingFilters) { _currentPage = 1; _recordsSearchTimer.Stop(); _recordsSearchTimer.Start(); } }
    private async void RecordsSearchTimer_Tick(object? sender, EventArgs e) { _recordsSearchTimer.Stop(); await LoadRecordsAsync(); }
    private async void StatusFilter_SelectedIndexChanged(object? sender, EventArgs e) { if (!_isInitializingFilters) { _currentPage = 1; await LoadRecordsAsync(); } }
    private async void CreateTransactionButton_Click(object? sender, EventArgs e) { if (AccessControlService.HasPermission("Offsite.Create") && new CreateMaintenanceForm(_currentUserId).ShowDialog() == DialogResult.OK) await LoadRecordsAsync(); }
    private async void PrevPageButton_Click(object? sender, EventArgs e) { if (_currentPage > 1) { _currentPage--; await LoadRecordsAsync(); } }
    private async void NextPageButton_Click(object? sender, EventArgs e) { if (_currentPage < Math.Ceiling(_totalItems / (double)_pageSize)) { _currentPage++; await LoadRecordsAsync(); } }
    private async void RecordsSubTabButton_Click(object? sender, EventArgs e) { _showArchived = false; _currentPage = 1; UpdateSubTabStyles(); await LoadRecordsAsync(); }
    private async void ArchivedSubTabButton_Click(object? sender, EventArgs e) { _showArchived = true; _currentPage = 1; UpdateSubTabStyles(); await LoadRecordsAsync(); }
    private void UpdateSubTabStyles() { ApplyTabStyle(_recordsSubTabButton, !_showArchived); ApplyTabStyle(_archivedSubTabButton, _showArchived); }
    private int GetRecordsPageSize() => Height > 700 ? 13 : 4;
    private void UpdatePagination(int totalPages) { _paginationLabel.Text = $"Page {_currentPage} of {totalPages} ({_totalItems} records)"; _prevPageButton.Enabled = _currentPage > 1; _nextPageButton.Enabled = _currentPage < totalPages; }
    private async Task UpdateMetrics() 
    { 
        try { 
            var m = await _transactionService.GetMetricsAsync(DateTime.Today); 
            _maintenanceRecordsCard.SetMetric(IconChar.ClipboardList, "Total Maintenance", m.TotalMaintenance.ToString(), "All time", ThemeHelper.Primary); 
            _activeMaintenanceCard.SetMetric(IconChar.Tools, "Active Maintenance", m.ActiveMaintenance.ToString(), "Currently ongoing", ThemeHelper.Warning); 
            _upcomingMaintenanceCard.SetMetric(IconChar.Calendar, "Upcoming", m.UpcomingMaintenance.ToString(), "Scheduled", ThemeHelper.Info); 
            _completedMaintenanceCard.SetMetric(IconChar.CheckCircle, "Completed", m.CompletedMaintenance.ToString(), "Total finalized", ThemeHelper.Success); 
        } catch { } 
    }

    private static IconButton CreateToolbarIconButton(IconChar icon, string text, int width) { var b = new IconButton { Text = text, IconChar = icon, IconColor = Color.White, IconSize = 16, TextImageRelation = TextImageRelation.ImageBeforeText, Size = new Size(width, 34), BackColor = ThemeHelper.Primary, ForeColor = Color.White, Font = FontHelper.SemiBold(9F), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand }; b.FlatAppearance.BorderSize = 0; return b; }
    private async Task InitializeMapAsync() { try { string p = Path.Combine(AppContext.BaseDirectory, "Assets", "Maps", "offsite-tracking-map.html"); if (File.Exists(p)) { await _mapWebView.EnsureCoreWebView2Async(); _mapWebView.Source = new Uri(p); _mapReady = true; } } catch { } }
    private async Task LoadCarsAsync() { try { var cars = await _trackingService.GetTrackableCarsAsync(); _carComboBox.Items.Clear(); _carComboBox.Items.Add("Select a vehicle"); foreach (var c in cars) _carComboBox.Items.Add(new CarOption(c.CarId, c.CarName, c.PlateNumber)); _carComboBox.SelectedIndex = 0; } catch { } }
    private async Task RefreshSelectedCarLocationAsync(bool msg) 
    { 
        if (_isRefreshing || _carComboBox.SelectedItem is not CarOption car) return; 
        try 
        { 
            _isRefreshing = true; 
            var loc = await _trackingService.GetLatestLocationAsync(car.CarId); 
            if (loc == null) { await ClearLocationDisplayAsync(car); return; } 
            UpdateLocationDisplay(car, loc); 
            await UpdateMapMarkerAsync(loc, car.Label); 
        } 
        catch (Exception ex)
        {
            if (msg) MessageBoxHelper.ShowError($"Failed to refresh location: {ex.Message}");
        }
        finally 
        { 
            _isRefreshing = false; 
        } 
    }
    private async Task StartDemoTrackingAsync() { if (!AccessControlService.HasPermission("Offsite.MapTracking") || _carComboBox.SelectedItem is not CarOption) return; _startTrackingButton.Enabled = false; _stopTrackingButton.Enabled = true; _autoRefreshLabel.Text = "Refresh: 5s"; _demoTimer.Start(); await InsertDemoLocationAsync(); }
    private void StopDemoTracking() { _demoTimer.Stop(); _startTrackingButton.Enabled = true; _stopTrackingButton.Enabled = false; _autoRefreshLabel.Text = "Refresh: 10m"; }
    private async Task InsertDemoLocationAsync() { if (_isDemoTickRunning || _carComboBox.SelectedItem is not CarOption car) return; try { _isDemoTickRunning = true; await _simulator.InsertNextAsync(car.CarId); await RefreshSelectedCarLocationAsync(false); } catch { StopDemoTracking(); } finally { _isDemoTickRunning = false; } }
    private async Task UpdateMapMarkerAsync(VehicleLocation loc, string lbl) { if (_mapReady && _mapWebView.CoreWebView2 != null) await _mapWebView.ExecuteScriptAsync(FormattableString.Invariant($"window.setVehicleLocation({loc.Latitude}, {loc.Longitude}, {JsonSerializer.Serialize(lbl)});")); }
    private async Task ClearMapMarkerAsync() { if (_mapReady && _mapWebView.CoreWebView2 != null) await _mapWebView.ExecuteScriptAsync("window.clearVehicleMarker();"); }
    private async void MapWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) { _mapReady = e.IsSuccess; await RefreshSelectedCarLocationAsync(false); }
    private void UpdateLocationDisplay(CarOption c, VehicleLocation l) { _selectedCarValueLabel.Text = c.CarName; _plateNumberValueLabel.Text = c.PlateNumber; _latitudeValueLabel.Text = $"{l.Latitude:N7}"; _longitudeValueLabel.Text = $"{l.Longitude:N7}"; _lastUpdatedValueLabel.Text = $"{l.RecordedAt:MMM d, yyyy h:mm tt}"; _sourceValueLabel.Text = l.Source; }
    private async Task ClearLocationDisplayAsync(CarOption c) { _selectedCarValueLabel.Text = c.CarName; _plateNumberValueLabel.Text = c.PlateNumber; _latitudeValueLabel.Text = "-"; _longitudeValueLabel.Text = "-"; _lastUpdatedValueLabel.Text = "-"; _sourceValueLabel.Text = "-"; await ClearMapMarkerAsync(); }

    private static void ConfigureTabButton(IconButton b, string t, IconChar i, Point l, int w) { b.Text = t; b.IconChar = i; b.IconSize = 16; b.TextImageRelation = TextImageRelation.ImageBeforeText; b.Location = l; b.Size = new Size(w, 34); b.FlatStyle = FlatStyle.Flat; b.Cursor = Cursors.Hand; b.Font = FontHelper.SemiBold(9.5F); b.FlatAppearance.BorderSize = 0; }
    private static void ApplyTabStyle(IconButton b, bool a) { b.BackColor = a ? ThemeHelper.Primary : ThemeHelper.Surface; b.ForeColor = a ? Color.White : ThemeHelper.TextPrimary; b.IconColor = a ? Color.White : ThemeHelper.TextSecondary; }
    private static void AddMetricCard(TableLayoutPanel g, MetricCardControl c, int col) { c.Dock = DockStyle.Fill; c.Margin = new Padding(0, 0, col == 3 ? 0 : 14, 0); g.Controls.Add(c, col, 0); }
    private static void AddInfoColumn(TableLayoutPanel g, string t, Label v, int col) { g.Controls.Add(new Label { Text = t, Dock = DockStyle.Fill, Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextSecondary }, col, 0); v.Dock = DockStyle.Fill; g.Controls.Add(v, col, 1); }
    private static Label CreateValueLabel() => new() { Text = "-", AutoSize = false, Font = FontHelper.SemiBold(9.5F), ForeColor = ThemeHelper.TextPrimary, AutoEllipsis = true };
    private static bool IsComboValueAvailable(ComboBox cb, string? v) { if (string.IsNullOrWhiteSpace(v)) return false; foreach (object? item in cb.Items) if (string.Equals(item?.ToString(), v, StringComparison.OrdinalIgnoreCase)) return true; return false; }
    private async void OffsiteControl_Load(object? sender, EventArgs e) { Load -= OffsiteControl_Load; await LoadCarsAsync(); await ShowMaintenanceViewAsync(); _refreshTimer.Start(); }

    private sealed record CarOption(int CarId, string CarName, string PlateNumber) { public string Label => $"{CarName} ({PlateNumber})"; public override string ToString() => Label; }
    private sealed record ClientOption(int CustomerId, string Name) { public override string ToString() => Name; }
}
