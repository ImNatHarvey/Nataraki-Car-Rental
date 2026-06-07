using System.Text.RegularExpressions;
using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.UserControls.ActivityLogs;

public sealed class ActivityLogControl : UserControl
{
    private sealed record LookupOption(string Value, string Display)
    {
        public override string ToString() => Display;
    }

    private const int MaxQueryRows = 500;
    private readonly ActivityLogService _activityLogService = new();
    private readonly MetricCardControl _totalLogsCard = new();
    private readonly MetricCardControl _todaysLogsCard = new();
    private readonly MetricCardControl _carActionsCard = new();
    private readonly MetricCardControl _customerActionsCard = new();
    private readonly TextBox _searchTextBox = new();
    private readonly ComboBox _actionComboBox = new();
    private readonly ComboBox _entityTypeComboBox = new();
    private readonly DateTimePicker _dateFromPicker = CreateDatePicker();
    private readonly DateTimePicker _dateToPicker = CreateDatePicker();
    private readonly FlowLayoutPanel _timelinePanel = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        WrapContents = false,
        FlowDirection = FlowDirection.TopDown,
        BackColor = ThemeHelper.ContentBackground,
        Padding = new Padding(0)
    };
    private readonly Label _emptyStateLabel = new();
    private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 350 };
    private readonly System.Windows.Forms.Timer _resizeTimer = new() { Interval = 300 };
    private bool _isInitializingFilters;

    private int _currentPage = 1;
    private int _pageSize = 13;
    private int _lastHeight;
    private readonly Label _paginationLabel = new();
    private readonly Button _prevPageButton = CreatePaginationButton("Previous");
    private readonly Button _nextPageButton = CreatePaginationButton("Next");

    public ActivityLogControl()
    {
        InitializeControl();
        _resizeTimer.Tick += ResizeTimer_Tick;
        Load += ActivityLogControl_Load;
        Disposed += (s, e) => 
        {
            Load -= ActivityLogControl_Load;
            Resize -= ActivityLogControl_Resize;
            _searchTimer.Dispose();
            _resizeTimer.Dispose();
        };
    }

    private void ActivityLogControl_Resize(object? sender, EventArgs e)
    {
        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    private async void ResizeTimer_Tick(object? sender, EventArgs e)
    {
        _resizeTimer.Stop();
        
        ResizeTimelineItems();

        if (Math.Abs(Height - _lastHeight) > 50)
        {
            _lastHeight = Height;
            _currentPage = 1;
            await LoadLogsAsync();
        }
    }

    private void ResizeTimelineItems()
    {
        if (_timelinePanel.Controls.Count == 0) return;

        _timelinePanel.SuspendLayout();
        
        int targetWidth = Math.Max(100, _timelinePanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24);

        foreach (Control control in _timelinePanel.Controls)
        {
            if (control is BorderedPanel panel)
            {
                panel.Width = targetWidth;
            }
            else if (control is Label label && label.TextAlign == ContentAlignment.BottomLeft)
            {
                label.Width = targetWidth;
            }
        }
        
        _timelinePanel.ResumeLayout(true);
    }

    private static DateTimePicker CreateDatePicker()
    {
        return new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Width = 110,
            Font = FontHelper.Regular(10F),
            CalendarForeColor = ThemeHelper.TextPrimary,
            CalendarMonthBackground = ThemeHelper.Surface
        };
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

    private void InitializeControl()
    {
        BackColor = ThemeHelper.ContentBackground;
        Dock = DockStyle.Fill;
        Padding = new Padding(32, 8, 32, 32);

        TableLayoutPanel mainLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5
        };

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

        mainLayout.Controls.Add(CreateMetricGrid(), 0, 0);
        mainLayout.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground }, 0, 1);
        mainLayout.Controls.Add(CreateSearchPanel(), 0, 2);
        mainLayout.Controls.Add(CreateTablePanel(), 0, 3);
        mainLayout.Controls.Add(CreatePaginationPanel(), 0, 4);

        Controls.Add(mainLayout);
    }

    private Panel CreatePaginationPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        _prevPageButton.Location = new Point(0, 8);
        _prevPageButton.Click += async (_, _) =>
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                await LoadLogsAsync();
            }
        };
        _nextPageButton.Location = new Point(90, 8);
        _nextPageButton.Click += async (_, _) =>
        {
            _currentPage++;
            await LoadLogsAsync();
        };

        _paginationLabel.AutoSize = false;
        _paginationLabel.Location = new Point(180, 8);
        _paginationLabel.Size = new Size(300, 32);
        _paginationLabel.TextAlign = ContentAlignment.MiddleLeft;
        _paginationLabel.Font = FontHelper.Regular(9.5F);
        _paginationLabel.ForeColor = ThemeHelper.TextSecondary;

        panel.Controls.Add(_prevPageButton);
        panel.Controls.Add(_nextPageButton);
        panel.Controls.Add(_paginationLabel);
        return panel;
    }

    private static Panel CreateHeaderPanel()
    {
        Panel panel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.ContentBackground
        };

        Label titleLabel = new()
        {
            Text = "Activity Log",
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(260, 34),
            Font = FontHelper.Title(22F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Label subtitleLabel = new()
        {
            Text = "Monitor recent system actions and record changes.",
            AutoSize = false,
            Location = new Point(2, 42),
            Size = new Size(560, 24),
            Font = FontHelper.Regular(10.5F),
            ForeColor = ThemeHelper.TextSecondary
        };

        panel.Controls.Add(titleLabel);
        panel.Controls.Add(subtitleLabel);
        return panel;
    }

    private TableLayoutPanel CreateMetricGrid()
    {
        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            Padding = new Padding(0, 12, 0, 8)
        };

        for (int i = 0; i < 4; i++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        AddMetricCard(grid, _totalLogsCard, IconChar.ClipboardList, "Total Logs", 0, "All recorded actions", ThemeHelper.Primary);
        AddMetricCard(grid, _todaysLogsCard, IconChar.CalendarDay, "Today's Logs", 1, "Recorded today", ThemeHelper.Success);
        AddMetricCard(grid, _carActionsCard, IconChar.Car, "Car Actions", 2, "Vehicle record changes", ThemeHelper.Warning);
        AddMetricCard(grid, _customerActionsCard, IconChar.Users, "Customer Actions", 3, "Customer record changes", ThemeHelper.Purple);
        return grid;
    }

    private static void AddMetricCard(TableLayoutPanel grid, MetricCardControl card, IconChar icon, string title, int column, string helperText, Color iconColor)
    {
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, column == 3 ? 0 : 14, 0);
        card.SetMetric(icon, title, "0", helperText, iconColor);
        grid.Controls.Add(card, column, 0);
    }

    private Panel CreateSearchPanel()
    {
        Panel panel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.ContentBackground
        };

        // Compact widths to prevent cropping in minimized view
        int searchWidth = 220;
        int comboWidth = 154;
        int dateWidth = 104;

        BorderedPanel searchContainer = new()
        {
            Size = new Size(searchWidth, 32),
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

        _searchTextBox.BorderStyle = BorderStyle.None;
        _searchTextBox.PlaceholderText = "Search logs...";
        _searchTextBox.BackColor = ThemeHelper.Surface;
        _searchTextBox.Font = FontHelper.Regular(10F);
        _searchTextBox.ForeColor = ThemeHelper.TextPrimary;
        _searchTextBox.Location = new Point(34, 7);
        _searchTextBox.Width = searchWidth - 44;
        _searchTextBox.TextChanged += (_, _) =>
        {
            _currentPage = 1;
            _searchTimer.Stop();
            _searchTimer.Start();
        };

        searchContainer.Controls.Add(searchIcon);
        searchContainer.Controls.Add(_searchTextBox);
        searchContainer.Click += (_, _) => _searchTextBox.Focus();

        // Swap order: Module first, then Action
        ConfigureFilterComboBox(_entityTypeComboBox, new Point(searchWidth + 12, 8));
        _entityTypeComboBox.Width = comboWidth;

        ConfigureFilterComboBox(_actionComboBox, new Point(searchWidth + comboWidth + 24, 8));
        _actionComboBox.Width = comboWidth;

        int nextX = searchWidth + (comboWidth * 2) + 36;

        Label fromLabel = new() { Text = "From:", AutoSize = true, Location = new Point(nextX, 14), Font = FontHelper.Regular(9F), ForeColor = ThemeHelper.TextSecondary };
        _dateFromPicker.Location = new Point(nextX + 42, 10);
        _dateFromPicker.Width = dateWidth;
        _dateFromPicker.Value = DateTime.Today.AddDays(-7);

        Label toLabel = new() { Text = "To:", AutoSize = true, Location = new Point(nextX + 42 + dateWidth + 12, 14), Font = FontHelper.Regular(9F), ForeColor = ThemeHelper.TextSecondary };
        _dateToPicker.Location = new Point(nextX + 42 + dateWidth + 12 + 28, 10);
        _dateToPicker.Width = dateWidth;
        _dateToPicker.Value = DateTime.Today;

        _entityTypeComboBox.SelectedIndexChanged += async (_, _) =>
        {
            if (!_isInitializingFilters)
            {
                await HandleModuleFilterChangedAsync();
            }
        };

        _actionComboBox.SelectedIndexChanged += async (_, _) =>
        {
            if (!_isInitializingFilters)
            {
                _currentPage = 1;
                await LoadLogsAsync();
            }
        };

        _dateFromPicker.ValueChanged += async (_, _) =>
        {
            _currentPage = 1;
            await LoadLogsAsync();
        };
        _dateToPicker.ValueChanged += async (_, _) =>
        {
            _currentPage = 1;
            await LoadLogsAsync();
        };
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await LoadLogsAsync();
        };

        panel.Controls.Add(searchContainer);
        panel.Controls.Add(_entityTypeComboBox);
        panel.Controls.Add(_actionComboBox);
        panel.Controls.Add(fromLabel);
        panel.Controls.Add(_dateFromPicker);
        panel.Controls.Add(toLabel);
        panel.Controls.Add(_dateToPicker);
        return panel;
    }

    private async Task HandleModuleFilterChangedAsync()
    {
        try
        {
            _isInitializingFilters = true;
            _currentPage = 1;

            string? selectedModule = GetSelectedFilter(_entityTypeComboBox);
            IReadOnlyList<string> actions;

            if (selectedModule == null)
            {
                actions = await _activityLogService.GetActionTypesAsync();
            }
            else
            {
                actions = await _activityLogService.GetActionTypesByEntityAsync(selectedModule);
            }

            var actionOptions = actions.Select(a => new LookupOption(a, a));
            SetFilterItems(_actionComboBox, "All Actions", actionOptions);
            _actionComboBox.SelectedIndex = 0;
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning($"Unable to update actions filter.\n\n{exception.Message}", "Activity Log");
        }
        finally
        {
            _isInitializingFilters = false;
        }

        await LoadLogsAsync();
    }

    private static void ConfigureFilterComboBox(ComboBox comboBox, Point location)
    {
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.Font = FontHelper.Regular(10F);
        comboBox.ForeColor = ThemeHelper.TextPrimary;
        comboBox.Size = new Size(176, 30);
        if (location != Point.Empty) comboBox.Location = location;
    }

    private Panel CreateTablePanel()
    {
        Panel panel = ControlFactory.CreateCardPanel(new Size(0, 0));
        panel.Dock = DockStyle.Fill;
        panel.Padding = new Padding(18);

        typeof(FlowLayoutPanel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(_timelinePanel, true, null);

        _emptyStateLabel.Text = "No activity logs yet.";
        _emptyStateLabel.Dock = DockStyle.Bottom;
        _emptyStateLabel.Height = 42;
        _emptyStateLabel.Font = FontHelper.Regular(10F);
        _emptyStateLabel.ForeColor = ThemeHelper.TextSecondary;
        _emptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        _emptyStateLabel.Visible = false;

        panel.Controls.Add(_timelinePanel);
        panel.Controls.Add(_emptyStateLabel);
        return panel;
    }

    private async void ActivityLogControl_Load(object? sender, EventArgs e)
    {
        if (!AccessControlService.HasPermission("ActivityLog.View"))
        {
            ShowPermissionDenied();
            return;
        }

        _lastHeight = Height;
        Resize += (s, ev) => 
        {
            _resizeTimer.Stop();
            _resizeTimer.Start();
        };

        await InitializeFiltersAsync();
        await LoadLogsAsync();
    }

    private static void ShowPermissionDenied()
    {
        MessageBoxHelper.ShowWarning("You do not have permission to access this feature.", "Permission Denied");
    }

    private async Task InitializeFiltersAsync()
    {
        try
        {
            _isInitializingFilters = true;
            IReadOnlyList<string> actionTypes = await _activityLogService.GetActionTypesAsync();
            IReadOnlyList<string> entityNames = await _activityLogService.GetEntityNamesAsync();

            var actionOptions = actionTypes.Select(name => new LookupOption(name, name));
            var moduleOptions = entityNames.Select(name => new LookupOption(name, FormatModuleName(name)));

            SetFilterItems(_actionComboBox, "All Actions", actionOptions);
            SetFilterItems(_entityTypeComboBox, "All Modules", moduleOptions);
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning($"Unable to load activity log filters.\n\n{exception.Message}", "Activity Log");
            SetFilterItems(_actionComboBox, "All Actions", []);
            SetFilterItems(_entityTypeComboBox, "All Modules", []);
        }
        finally
        {
            _isInitializingFilters = false;
        }
    }

    private static string FormatModuleName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        // Insert spaces before capital letters (PascalCase -> Spaced Case)
        return Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
    }

    private async Task LoadLogsAsync()
    {
        try
        {
            _pageSize = Height > 700 ? 13 : 4;

            ActivityLogMetrics metrics = await _activityLogService.GetMetricsAsync();
            UpdateMetricCards(metrics);

            DateTime dateFrom = _dateFromPicker.Value.Date;
            DateTime dateTo = _dateToPicker.Value.Date.AddDays(1).AddSeconds(-1);

            int totalItems = await _activityLogService.CountAsync(
                _searchTextBox.Text,
                GetSelectedFilter(_actionComboBox),
                GetSelectedFilter(_entityTypeComboBox),
                dateFrom,
                dateTo);

            int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / _pageSize));
            if (_currentPage > totalPages) _currentPage = totalPages;

            IReadOnlyList<ActivityLog> logs = await _activityLogService.SearchLogsAsync(
                _searchTextBox.Text,
                GetSelectedFilter(_actionComboBox),
                GetSelectedFilter(_entityTypeComboBox),
                dateFrom,
                dateTo,
                _currentPage,
                _pageSize);

            PopulateGrid(logs, totalItems, totalPages);
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to load activity logs.\n\n{exception.Message}", "Activity Log");
        }
    }

    private void UpdateMetricCards(ActivityLogMetrics metrics)
    {
        _totalLogsCard.SetMetric(IconChar.ClipboardList, "Total Logs", metrics.TotalLogs.ToString(), "All recorded actions", ThemeHelper.Primary);
        _todaysLogsCard.SetMetric(IconChar.CalendarDay, "Today's Logs", metrics.TodaysLogs.ToString(), "Recorded today", ThemeHelper.Success);
        _carActionsCard.SetMetric(IconChar.Car, "Car Actions", metrics.CarActions.ToString(), "Vehicle record changes", ThemeHelper.Warning);
        _customerActionsCard.SetMetric(IconChar.Users, "Customer Actions", metrics.CustomerActions.ToString(), "Customer record changes", ThemeHelper.Purple);
    }

    private void PopulateGrid(IReadOnlyList<ActivityLog> pagedLogs, int totalItems, int totalPages)
    {
        _timelinePanel.SuspendLayout();

        // Properly dispose old controls to prevent GDI leaks
        while (_timelinePanel.Controls.Count > 0)
        {
            var control = _timelinePanel.Controls[0];
            _timelinePanel.Controls.RemoveAt(0);
            control.Dispose();
        }

        _paginationLabel.Text = $"Page {_currentPage} of {totalPages} ({totalItems} records)";
        _prevPageButton.Enabled = _currentPage > 1;
        _nextPageButton.Enabled = _currentPage < totalPages;

        DateTime? lastDate = null;
        foreach (ActivityLog log in pagedLogs)
        {
            DateTime logDate = log.CreatedAt.Date;
            if (lastDate != logDate)
            {
                _timelinePanel.Controls.Add(CreateDateHeader(logDate));
                lastDate = logDate;
            }
            _timelinePanel.Controls.Add(CreateTimelineItem(log));
        }

        _emptyStateLabel.Visible = totalItems == 0;
        _timelinePanel.ResumeLayout();
    }

    private Control CreateDateHeader(DateTime date)
    {
        string text = date == DateTime.Today ? "TODAY" : date == DateTime.Today.AddDays(-1) ? "YESTERDAY" : date.ToString("MMM dd, yyyy").ToUpperInvariant();
        return new Label
        {
            Text = text,
            Font = FontHelper.SemiBold(9F),
            ForeColor = ThemeHelper.TextSecondary,
            AutoSize = false,
            Size = new Size(Math.Max(100, _timelinePanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24), 24),
            Margin = new Padding(12, 16, 0, 8),
            TextAlign = ContentAlignment.BottomLeft
        };
    }

    private Control CreateTimelineItem(ActivityLog log)
    {
        var changes = DiffHelper.ParseChanges(log.OldValue, log.NewValue);
        bool hasChanges = changes.Count > 0;

        BorderedPanel row = new BorderedPanel
        {
            Width = Math.Max(100, _timelinePanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24),
            Height = hasChanges ? 84 + (changes.Count * 42) : 64,
            Margin = new Padding(16, 0, 0, 8),
            BackColor = ThemeHelper.Surface
        };

        ControlFactory.ApplyRoundedPanel(row);

        Color actionColor = GetActionColor(log.Action);
        Panel colorBar = new Panel { Width = 4, Dock = DockStyle.Left, BackColor = actionColor };
        row.Controls.Add(colorBar);

        Label timeLabel = new Label
        {
            Text = log.CreatedAt.ToString("hh:mm tt"),
            Font = FontHelper.Regular(9F),
            ForeColor = ThemeHelper.TextSecondary,
            AutoSize = true,
            Location = new Point(16, 24)
        };
        row.Controls.Add(timeLabel);

        string[] nameParts = log.UserFullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string firstName = nameParts.Length > 0 ? nameParts[0] : log.UserFullName;
        string lastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "";
        User fakeUser = new User { FirstName = firstName, LastName = lastName };

        PictureBox avatar = new PictureBox
        {
            Size = new Size(32, 32),
            Location = new Point(86, 16),
            Image = UserAvatarHelper.CreateAvatar(fakeUser, 32),
            SizeMode = PictureBoxSizeMode.CenterImage
        };
        row.Controls.Add(avatar);

        string titleText = BuildActivityTitle(log);

        Label titleLabel = new Label
        {
            Text = titleText,
            Font = FontHelper.SemiBold(10F),
            ForeColor = ThemeHelper.TextPrimary,
            AutoSize = true,
            Location = new Point(130, 12)
        };
        row.Controls.Add(titleLabel);

        Label descLabel = new Label
        {
            Text = log.Description,
            Font = FontHelper.Regular(9F),
            ForeColor = ThemeHelper.TextSecondary,
            AutoSize = true,
            Location = new Point(130, 34)
        };
        row.Controls.Add(descLabel);

        if (hasChanges)
        {
            Label changesHeader = new Label
            {
                Text = "Changes:",
                Font = FontHelper.SemiBold(8.5F),
                ForeColor = ThemeHelper.TextPrimary,
                AutoSize = true,
                Location = new Point(130, 60)
            };
            row.Controls.Add(changesHeader);

            int currentY = 82;
            foreach (var change in changes)
            {
                Label fieldLabel = new Label
                {
                    Text = $"• {change.Field}",
                    Font = FontHelper.SemiBold(8.5F),
                    ForeColor = ThemeHelper.TextSecondary,
                    AutoSize = true,
                    Location = new Point(140, currentY)
                };
                row.Controls.Add(fieldLabel);

                Label valuesLabel = new Label
                {
                    Text = $"From: {change.From}   To: {change.To}",
                    Font = FontHelper.Regular(8.5F),
                    ForeColor = ThemeHelper.TextSecondary,
                    AutoSize = true,
                    Location = new Point(152, currentY + 18)
                };
                row.Controls.Add(valuesLabel);

                currentY += 42;
            }
        }

        return row;
    }

    private static string BuildActivityTitle(ActivityLog log)
    {
        string user = log.UserFullName;
        string action = log.Action;
        string module = log.Module;
        string entity = log.EntityName ?? string.Empty;

        // Friendly Module Names
        string displayModule = module switch
        {
            "FleetSchedule" => "Schedule",
            "OffsiteRecord" => "Offsite Record",
            "ManageSystem" => "Manage System",
            "SystemSettings" => "System Settings",
            "BrandingTheme" => "Branding Theme",
            _ => FormatModuleName(module)
        };

        // Standardize case for logic
        string actionLower = action.ToLowerInvariant();

        // 1. Handle Auth/System Special Cases
        if (actionLower is "login" or "logout")
        {
            return $"{user} {actionLower}";
        }

        if (actionLower == "system start")
        {
            return "System Started";
        }

        // 2. Handle Password Reset
        if (actionLower == "reset password" || (actionLower == "updated" && entity.Contains("Password")))
        {
            return $"{user} reset password for {displayModule}";
        }

        // 3. Prevent Duplication
        // If entity already contains displayModule, or vice versa, don't repeat them
        string title;
        if (string.IsNullOrWhiteSpace(entity) || entity.Equals(displayModule, StringComparison.OrdinalIgnoreCase))
        {
            title = $"{user} {actionLower} {displayModule}";
        }
        else if (entity.Contains(displayModule, StringComparison.OrdinalIgnoreCase))
        {
            title = $"{user} {actionLower} {entity}";
        }
        else
        {
            // Standard: User + Action + Module + Entity
            // e.g. Harvey updated Customer Juan Dela Cruz
            title = $"{user} {actionLower} {displayModule} {entity}";
        }

        return title.Trim();
    }

    private static Color GetActionColor(string action)
    {
        string lowerText = action.ToLowerInvariant();

        // GREEN
        if (lowerText is "added" or "created" or "restored" or "completed" or "approved" or "removed from blacklist")
            return ThemeHelper.Success;

        // BLUE
        if (lowerText is "updated" or "edited" or "changed" or "modified" or "saved")
            return ThemeHelper.Primary;

        // ORANGE
        if (lowerText is "cancelled" or "extended" or "warning" or "expiring" or "reset password")
            return ThemeHelper.Warning;

        // RED
        if (lowerText is "archived" or "deleted" or "blacklisted" or "deactivated" or "removed")
            return ThemeHelper.Danger;

        // GRAY
        return ThemeHelper.GrayIcon;
    }

    private static void SetFilterItems(ComboBox comboBox, string placeholder, IEnumerable<LookupOption> values)
    {
        comboBox.BeginUpdate();
        comboBox.Items.Clear();
        comboBox.Items.Add(new LookupOption(string.Empty, placeholder));
        comboBox.Items.AddRange(values.Cast<object>().ToArray());
        comboBox.SelectedIndex = 0;
        comboBox.EndUpdate();
    }

    private static string? GetSelectedFilter(ComboBox comboBox)
    {
        if (comboBox.SelectedIndex <= 0) return null;
        if (comboBox.SelectedItem is LookupOption option)
        {
            return string.IsNullOrWhiteSpace(option.Value) ? null : option.Value;
        }
        return comboBox.SelectedItem?.ToString();
    }
}
