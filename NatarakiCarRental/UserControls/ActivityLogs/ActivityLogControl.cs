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
    private readonly ComboBox _actionTypeComboBox = new();
    private readonly ComboBox _entityTypeComboBox = new();
    private readonly DateTimePicker _dateFromPicker = CreateDatePicker();
    private readonly DateTimePicker _dateToPicker = CreateDatePicker();
    private readonly DataGridView _logsGrid = new();
    private readonly Label _emptyStateLabel = new();
    private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 350 };
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
        Load += ActivityLogControl_Load;
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
        Padding = new Padding(32);

        TableLayoutPanel mainLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6
        };

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

        mainLayout.Controls.Add(CreateHeaderPanel(), 0, 0);
        mainLayout.Controls.Add(CreateMetricGrid(), 0, 1);
        mainLayout.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground }, 0, 2);
        mainLayout.Controls.Add(CreateSearchPanel(), 0, 3);
        mainLayout.Controls.Add(CreateTablePanel(), 0, 4);
        mainLayout.Controls.Add(CreatePaginationPanel(), 0, 5);

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

        ConfigureFilterComboBox(_actionTypeComboBox, new Point(searchWidth + comboWidth + 24, 8));
        _actionTypeComboBox.Width = comboWidth;

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

        _actionTypeComboBox.SelectedIndexChanged += async (_, _) =>
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
        panel.Controls.Add(_actionTypeComboBox);
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
            SetFilterItems(_actionTypeComboBox, "All Actions", actionOptions);
            _actionTypeComboBox.SelectedIndex = 0;
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning($"Unable to update actions filter.\\n\\n{exception.Message}", "Activity Log");
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

        _logsGrid.Dock = DockStyle.Fill;
        _logsGrid.AllowUserToAddRows = false;
        _logsGrid.AllowUserToDeleteRows = false;
        _logsGrid.AllowUserToResizeRows = false;
        _logsGrid.AllowUserToResizeColumns = false;
        _logsGrid.ScrollBars = ScrollBars.Both;
        _logsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _logsGrid.BackgroundColor = ThemeHelper.Surface;
        _logsGrid.BorderStyle = BorderStyle.FixedSingle;
        _logsGrid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        _logsGrid.ColumnHeadersHeight = 38;
        _logsGrid.EnableHeadersVisualStyles = false;
        _logsGrid.GridColor = ThemeHelper.TableGridLine;
        _logsGrid.ReadOnly = true;
        _logsGrid.RowHeadersVisible = false;
        _logsGrid.RowTemplate.Height = 38;
        _logsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _logsGrid.DefaultCellStyle.SelectionBackColor = ThemeHelper.Surface;
        _logsGrid.DefaultCellStyle.SelectionForeColor = ThemeHelper.TextPrimary;
        _logsGrid.ColumnHeadersDefaultCellStyle.BackColor = ThemeHelper.Primary;
        _logsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _logsGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemeHelper.Primary;
        _logsGrid.ColumnHeadersDefaultCellStyle.Font = FontHelper.SemiBold(9F);
        _logsGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _logsGrid.CellPainting += LogsGrid_CellPainting;

        _emptyStateLabel.Text = "No activity logs yet.";
        _emptyStateLabel.Dock = DockStyle.Bottom;
        _emptyStateLabel.Height = 42;
        _emptyStateLabel.Font = FontHelper.Regular(10F);
        _emptyStateLabel.ForeColor = ThemeHelper.TextSecondary;
        _emptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        _emptyStateLabel.Visible = false;

        panel.Controls.Add(_logsGrid);
        panel.Controls.Add(_emptyStateLabel);
        return panel;
    }

    private async void ActivityLogControl_Load(object? sender, EventArgs e)
    {
        Load -= ActivityLogControl_Load;

        if (!AccessControlService.HasPermission("ActivityLog.View"))
        {
            ShowPermissionDenied();
            return;
        }

        _lastHeight = Height;
        Resize += async (_, _) =>
        {
            UpdateColumnLayout();
            if (Math.Abs(Height - _lastHeight) > 50)
            {
                _lastHeight = Height;
                _currentPage = 1;
                await LoadLogsAsync();
            }
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

            var moduleOptions = entityNames.Select(name => new LookupOption(name, FormatModuleName(name)));
            var actionOptions = actionTypes.Select(name => new LookupOption(name, name));

            SetFilterItems(_entityTypeComboBox, "All Modules", moduleOptions);
            SetFilterItems(_actionTypeComboBox, "All Actions", actionOptions);
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning($"Unable to load activity log filters.\n\n{exception.Message}", "Activity Log");
            SetFilterItems(_actionTypeComboBox, "All Actions", []);
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

            IReadOnlyList<ActivityLog> logs = await _activityLogService.SearchLogsAsync(
                _searchTextBox.Text,
                GetSelectedFilter(_actionTypeComboBox),
                GetSelectedFilter(_entityTypeComboBox),
                dateFrom,
                dateTo,
                MaxQueryRows);
            PopulateGrid(logs);
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

    private void PopulateGrid(IReadOnlyList<ActivityLog> allLogs)
    {
        AddGridColumns();
        UpdateColumnLayout();
        _logsGrid.Rows.Clear();

        int totalItems = allLogs.Count;
        int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / _pageSize));
        if (_currentPage > totalPages) _currentPage = totalPages;

        _paginationLabel.Text = $"Page {_currentPage} of {totalPages} ({totalItems} records)";
        _prevPageButton.Enabled = _currentPage > 1;
        _nextPageButton.Enabled = _currentPage < totalPages;

        var pagedLogs = allLogs.Skip((_currentPage - 1) * _pageSize).Take(_pageSize);

        foreach (ActivityLog log in pagedLogs)
        {
            _logsGrid.Rows.Add(
                log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                log.UserDisplayName,
                log.EntityName ?? "System",
                log.ActionType,
                log.Description);
        }

        _emptyStateLabel.Visible = totalItems == 0;
    }

    private void AddGridColumns()
    {
        _logsGrid.Columns.Clear();
        _logsGrid.Columns.Add("CreatedAt", "Date/Time");
        _logsGrid.Columns.Add("User", "User");
        _logsGrid.Columns.Add("Module", "Module");
        _logsGrid.Columns.Add("Action", "Action");
        _logsGrid.Columns.Add("Description", "Description");
    }

    private void UpdateColumnLayout()
    {
        if (_logsGrid.Columns.Count == 0) return;

        int gridWidth = _logsGrid.ClientSize.Width;
        int threshold = 1120; // 150 + 150 + 130 + 170 + 520

        if (gridWidth < threshold && gridWidth > 0)
        {
            _logsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            if (_logsGrid.Columns["CreatedAt"] is DataGridViewColumn c1) c1.Width = 150;
            if (_logsGrid.Columns["User"] is DataGridViewColumn c2) c2.Width = 150;
            if (_logsGrid.Columns["Module"] is DataGridViewColumn c3) c3.Width = 130;
            if (_logsGrid.Columns["Action"] is DataGridViewColumn c4) c4.Width = 170;
            if (_logsGrid.Columns["Description"] is DataGridViewColumn c5) c5.Width = 600;
        }
        else
        {
            _logsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            SetFillWeight("CreatedAt", 84);
            SetFillWeight("User", 84);
            SetFillWeight("Module", 70);
            SetFillWeight("Action", 92);
            SetFillWeight("Description", 180);
        }
    }

    private void SetFillWeight(string columnName, float weight)
    {
        if (_logsGrid.Columns[columnName] is DataGridViewColumn column)
        {
            column.FillWeight = weight;
        }
    }

    private void LogsGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        string columnName = _logsGrid.Columns[e.ColumnIndex].Name;
        bool isAction = columnName == "Action";
        bool isModule = columnName == "Module";

        if (isAction || isModule)
        {
            e.PaintBackground(e.CellBounds, true);

            string rawText = e.Value?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(rawText)) return;

            string displayText = rawText;
            if (isAction && displayText.Equals("Remove customer blacklist", StringComparison.OrdinalIgnoreCase))
            {
                displayText = "Remove Blacklist";
            }
            else if (isAction && displayText.Equals("Reserve transaction", StringComparison.OrdinalIgnoreCase))
            {
                displayText = "Reserve";
            }

            Color backColor = ThemeHelper.GrayIcon;
            Color foreColor = Color.White;

            string lowerText = rawText.ToLowerInvariant();

            if (isAction)
            {
                if (lowerText.Contains("remove blacklist") || lowerText.Contains("remove customer blacklist")) backColor = ThemeHelper.Primary;
                else if (lowerText.Contains("blacklist")) backColor = ThemeHelper.Danger;
                else if (lowerText.Contains("archive") || lowerText.Contains("system") || lowerText.Contains("login") || lowerText.Contains("logout")) backColor = ThemeHelper.GrayIcon;
                else if (lowerText.Contains("cancel") || lowerText.Contains("delete")) backColor = ThemeHelper.Danger;
                else if (lowerText.Contains("create") || lowerText.Contains("add") || lowerText.Contains("complete") || lowerText.Contains("finish") || lowerText.Contains("payment")) backColor = ThemeHelper.Success;
                else if (lowerText.Contains("update") || lowerText.Contains("edit") || lowerText.Contains("restore") || lowerText.Contains("rental") || lowerText.Contains("reserve") || lowerText.Contains("reservation")) backColor = ThemeHelper.Primary;
            }
            else if (isModule)
            {
                if (lowerText.Contains("car")) backColor = ThemeHelper.Primary;
                else if (lowerText.Contains("customer")) backColor = ThemeHelper.Purple;
                else if (lowerText.Contains("transaction")) backColor = ThemeHelper.Success;
                else if (lowerText.Contains("fleet") || lowerText.Contains("schedule")) backColor = ThemeHelper.Warning;
                else backColor = ThemeHelper.GrayIcon;
            }

            if (e.Graphics is null) return;

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Font font = e.CellStyle?.Font ?? FontHelper.SemiBold(8.5F);
            SizeF textSize = e.Graphics.MeasureString(displayText, font);
            float pillHeight = 24;
            float pillWidth = textSize.Width + 20;

            if (pillWidth > e.CellBounds.Width - 12) pillWidth = e.CellBounds.Width - 12;

            float x = e.CellBounds.X + 8;
            float y = e.CellBounds.Y + (e.CellBounds.Height - pillHeight) / 2;

            RectangleF rect = new RectangleF(x, y, pillWidth, pillHeight);

            using var path = GetRoundedRect(rect, pillHeight / 2);
            using SolidBrush backBrush = new(backColor);
            using SolidBrush foreBrush = new(foreColor);

            e.Graphics.FillPath(backBrush, path);

            using StringFormat format = new()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            };
            e.Graphics.DrawString(displayText, font, foreBrush, rect, format);

            e.Handled = true;
        }
    }

    private static System.Drawing.Drawing2D.GraphicsPath GetRoundedRect(RectangleF rect, float radius)
    {
        System.Drawing.Drawing2D.GraphicsPath path = new();
        float diameter = radius * 2;
        Size size = new Size((int)diameter, (int)diameter);
        RectangleF arc = new RectangleF(rect.Location, size);

        if (radius == 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
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
