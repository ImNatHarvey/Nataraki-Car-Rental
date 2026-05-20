using FontAwesome.Sharp;
using System.Drawing.Drawing2D;
using NatarakiCarRental.Forms.Customers;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.UserControls.Customers;

public sealed class CustomerControl : UserControl
{
    private readonly CustomerService _customerService;
    private readonly MetricCardControl _totalCustomersCard = new();
    private readonly MetricCardControl _activeCustomersCard = new();
    private readonly MetricCardControl _blacklistedCustomersCard = new();
    private readonly MetricCardControl _archivedCustomersCard = new();
    private readonly TextBox _searchTextBox = new();
    private readonly IconButton _activeButton = new();
    private readonly IconButton _blacklistedButton = new();
    private readonly IconButton _archivedButton = new();
    private readonly DataGridView _customersGrid = new();
    private readonly Label _emptyStateLabel = new();
    private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 350 };
    private int _currentPage = 1;
    private int _pageSize = 15;
    private readonly Label _paginationLabel = new();
    private readonly Button _prevPageButton = CreatePaginationButton("Previous");
    private readonly Button _nextPageButton = CreatePaginationButton("Next");

    private CustomerListFilter _filter = CustomerListFilter.Active;

    private readonly int _currentUserId;

    public CustomerControl(int currentUserId)
    {
        _currentUserId = currentUserId;
        _customerService = new CustomerService(currentUserId);
        InitializeControl();
        Load += CustomerControl_Load;
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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

        mainLayout.Controls.Add(CreateHeaderPanel(), 0, 0);
        mainLayout.Controls.Add(CreateMetricGrid(), 0, 1);
        mainLayout.Controls.Add(CreateActionBarPanel(), 0, 2);
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
                await LoadCustomersAsync();
            }
        };
        _nextPageButton.Location = new Point(90, 8);
        _nextPageButton.Click += async (_, _) =>
        {
            _currentPage++;
            await LoadCustomersAsync();
        };

        _paginationLabel.AutoSize = false;
        _paginationLabel.Location = new Point(180, 8);
        _paginationLabel.Size = new Size(200, 32);
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
            Text = "Customers",
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(260, 34),
            Font = FontHelper.Title(22F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Label subtitleLabel = new()
        {
            Text = "Manage client records, documents, blacklist status, and archived customers.",
            AutoSize = false,
            Location = new Point(2, 42),
            Size = new Size(680, 24),
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

        AddMetricCard(grid, _totalCustomersCard, IconChar.Users, "Total Customers", 0, "All active records", ThemeHelper.Primary);
        AddMetricCard(grid, _activeCustomersCard, IconChar.CircleCheck, "Active Customers", 1, "Ready for booking", ThemeHelper.Success);
        AddMetricCard(grid, _blacklistedCustomersCard, IconChar.UserSlash, "Blacklisted", 2, "Flagged clients", ThemeHelper.Danger);
        AddMetricCard(grid, _archivedCustomersCard, IconChar.BoxArchive, "Archived Customers", 3, "Hidden records", ThemeHelper.GrayIcon);

        return grid;
    }

    private static void AddMetricCard(TableLayoutPanel grid, MetricCardControl card, IconChar icon, string title, int column, string helperText, Color iconColor)
    {
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, column == 3 ? 0 : 14, 0);
        card.SetMetric(icon, title, "0", helperText, iconColor);
        grid.Controls.Add(card, column, 0);
    }

    private Panel CreateActionBarPanel()
    {
        Panel panel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.ContentBackground
        };

        ConfigureTabButton(_activeButton, "Customers", IconChar.Users, new Point(0, 10));
        ConfigureTabButton(_archivedButton, "Archived", IconChar.BoxArchive, new Point(132, 10));
        ConfigureTabButton(_blacklistedButton, "Blacklisted", IconChar.UserSlash, new Point(256, 10));

        IconButton addCustomerButton = new()
        {
            Text = "Add Customer",
            IconChar = IconChar.Plus,
            IconColor = Color.White,
            IconSize = 14,
            Size = new Size(142, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 142, 10),
            BackColor = ThemeHelper.Primary,
            ForeColor = Color.White,
            Font = FontHelper.SemiBold(9F),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        addCustomerButton.FlatAppearance.BorderSize = 0;
        addCustomerButton.TextImageRelation = TextImageRelation.ImageBeforeText;
        addCustomerButton.Click += AddCustomerButton_Click;

        panel.Resize += (_, _) => addCustomerButton.Left = panel.Width - addCustomerButton.Width;
        _activeButton.Click += async (_, _) => await SwitchFilterAsync(CustomerListFilter.Active);
        _blacklistedButton.Click += async (_, _) => await SwitchFilterAsync(CustomerListFilter.Blacklisted);
        _archivedButton.Click += async (_, _) => await SwitchFilterAsync(CustomerListFilter.Archived);

        panel.Controls.Add(_activeButton);
        panel.Controls.Add(_blacklistedButton);
        panel.Controls.Add(_archivedButton);
        panel.Controls.Add(addCustomerButton);
        return panel;
    }

    private Panel CreateSearchPanel()
    {
        Panel panel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.ContentBackground
        };

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

        _searchTextBox.BorderStyle = BorderStyle.None;
        _searchTextBox.PlaceholderText = "Search customers...";
        _searchTextBox.BackColor = ThemeHelper.Surface;
        _searchTextBox.Font = FontHelper.Regular(10F);
        _searchTextBox.ForeColor = ThemeHelper.TextPrimary;
        _searchTextBox.Location = new Point(34, 7);
        _searchTextBox.Width = 196;
        _searchTextBox.TextChanged += (_, _) =>
        {
            _currentPage = 1;
            _searchTimer.Stop();
            _searchTimer.Start();
        };

        searchContainer.Controls.Add(searchIcon);
        searchContainer.Controls.Add(_searchTextBox);
        searchContainer.Click += (_, _) => _searchTextBox.Focus();
        panel.Controls.Add(searchContainer);
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await LoadCustomersAsync();
        };
        return panel;
    }

    private static void ConfigureTabButton(IconButton button, string text, IconChar icon, Point location)
    {
        button.Text = text;
        button.IconChar = icon;
        button.IconSize = 16;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.Location = location;
        button.Size = new Size(text == "Customers" ? 124 : text == "Blacklisted" ? 124 : 116, 34);
        button.FlatStyle = FlatStyle.Flat;
        button.Cursor = Cursors.Hand;
        button.Font = FontHelper.SemiBold(9F);
        button.FlatAppearance.BorderSize = 0;
    }

    private Panel CreateTablePanel()
    {
        Panel panel = ControlFactory.CreateCardPanel(new Size(0, 0));
        panel.Dock = DockStyle.Fill;
        panel.Padding = new Padding(18);

        _customersGrid.Dock = DockStyle.Fill;
        _customersGrid.AllowUserToAddRows = false;
        _customersGrid.AllowUserToDeleteRows = false;
        _customersGrid.AllowUserToResizeRows = false;
        _customersGrid.AllowUserToResizeColumns = false;
        _customersGrid.ScrollBars = ScrollBars.Vertical;
        _customersGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _customersGrid.BackgroundColor = ThemeHelper.Surface;
        _customersGrid.BorderStyle = BorderStyle.FixedSingle;
        _customersGrid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        _customersGrid.ColumnHeadersHeight = 38;
        _customersGrid.EnableHeadersVisualStyles = false;
        _customersGrid.GridColor = ThemeHelper.TableGridLine;
        _customersGrid.ReadOnly = true;
        _customersGrid.RowHeadersVisible = false;
        _customersGrid.RowTemplate.Height = 38;
        _customersGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _customersGrid.DefaultCellStyle.SelectionBackColor = ThemeHelper.Surface;
        _customersGrid.DefaultCellStyle.SelectionForeColor = ThemeHelper.TextPrimary;
        _customersGrid.ColumnHeadersDefaultCellStyle.BackColor = ThemeHelper.Primary;
        _customersGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _customersGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemeHelper.Primary;
        _customersGrid.ColumnHeadersDefaultCellStyle.Font = FontHelper.SemiBold(9F);
        _customersGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _customersGrid.CellContentClick += CustomersGrid_CellContentClick;
        _customersGrid.CellMouseMove += CustomersGrid_CellMouseMove;
        _customersGrid.CellMouseLeave += (_, _) => _customersGrid.Cursor = Cursors.Default;
        _customersGrid.CellPainting += CustomersGrid_CellPainting;

        _emptyStateLabel.Text = "No customer records found.";
        _emptyStateLabel.Dock = DockStyle.Bottom;
        _emptyStateLabel.Height = 42;
        _emptyStateLabel.Font = FontHelper.Regular(10F);
        _emptyStateLabel.ForeColor = ThemeHelper.TextSecondary;
        _emptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        _emptyStateLabel.Visible = false;

        panel.Controls.Add(_customersGrid);
        panel.Controls.Add(_emptyStateLabel);
        return panel;
    }

    private void AddGridColumns()
    {
        _customersGrid.Columns.Clear();
        _customersGrid.Columns.Add("CustomerId", "Customer ID");
        _customersGrid.Columns.Add("FullName", "Full Name");
        _customersGrid.Columns.Add("PhoneNumber", "Phone Number");
        _customersGrid.Columns.Add("Email", "Email");
        _customersGrid.Columns.Add("Address", "Address");
        _customersGrid.Columns.Add("Status", "Status");
        _customersGrid.Columns.Add("BlacklistReason", "Blacklist Reason");

        AddActionColumn("ViewAction", "Actions", "View");

        if (_filter == CustomerListFilter.Active)
        {
            AddActionColumn("EditAction", "", "Edit");
            AddActionColumn("BlacklistAction", "", "Blacklist");
            AddActionColumn("ArchiveAction", "", "Archive");
        }
        else if (_filter == CustomerListFilter.Blacklisted)
        {
            AddActionColumn("EditAction", "", "Edit");
            AddActionColumn("RemoveBlacklistAction", "", "Remove");
            AddActionColumn("ArchiveAction", "", "Archive");
        }
        else
        {
            AddActionColumn("RestoreAction", "", "Restore");
        }

        if (_customersGrid.Columns["CustomerId"] is DataGridViewColumn idColumn)
        {
            idColumn.Visible = false;
        }

        if (_customersGrid.Columns["BlacklistReason"] is DataGridViewColumn reasonColumn)
        {
            reasonColumn.Visible = _filter == CustomerListFilter.Blacklisted;
        }

        SetFillWeight("FullName", 95);
        SetFillWeight("PhoneNumber", 80);
        SetFillWeight("Email", 95);
        SetFillWeight("Address", 120);
        SetFillWeight("Status", 75);
        SetFillWeight("BlacklistReason", 110);
        SetFillWeight("ViewAction", 58);
        SetFillWeight("EditAction", 58);
        SetFillWeight("BlacklistAction", 74);
        SetFillWeight("RemoveBlacklistAction", 74);
        SetFillWeight("ArchiveAction", 62);
        SetFillWeight("RestoreAction", 68);
    }

    private void SetFillWeight(string columnName, float weight)
    {
        if (_customersGrid.Columns[columnName] is DataGridViewColumn column)
        {
            column.FillWeight = weight;
        }
    }

    private void AddActionColumn(string name, string headerText, string buttonText)
    {
        DataGridViewButtonColumn column = new()
        {
            Name = name,
            HeaderText = headerText,
            Text = buttonText,
            UseColumnTextForButtonValue = true,
            FlatStyle = FlatStyle.Flat
        };
        column.DefaultCellStyle.BackColor = ThemeHelper.Surface;
        column.DefaultCellStyle.SelectionBackColor = ThemeHelper.Surface;
        _customersGrid.Columns.Add(column);
    }

    private async Task SwitchFilterAsync(CustomerListFilter filter)
    {
        _currentPage = 1;
        _filter = filter;
        await LoadCustomersAsync();
    }

    private int _lastHeight;

    private async Task LoadCustomersAsync()
    {
        try
        {
            _pageSize = Height > 700 ? 15 : 5;
            UpdateTabStyles();
            CustomerCounts counts = await _customerService.GetCustomerCountsAsync();
            UpdateMetricCards(counts);
            IReadOnlyList<Customer> allCustomers = await _customerService.SearchCustomersAsync(_searchTextBox.Text, _filter);
            PopulateGrid(allCustomers);
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to load customer records.\n\n{exception.Message}");
        }
    }

    private async void CustomerControl_Load(object? sender, EventArgs e)
    {
        Load -= CustomerControl_Load;
        _lastHeight = Height;
        Resize += async (_, _) =>
        {
            if (Math.Abs(Height - _lastHeight) > 50)
            {
                _lastHeight = Height;
                _currentPage = 1;
                await LoadCustomersAsync();
            }
        };
        await LoadCustomersAsync();
    }

    private void UpdateMetricCards(CustomerCounts counts)
    {
        _totalCustomersCard.SetMetric(IconChar.Users, "Total Customers", counts.TotalCustomers.ToString(), "All active records", ThemeHelper.Primary);
        _activeCustomersCard.SetMetric(IconChar.CircleCheck, "Active Customers", counts.ActiveCustomers.ToString(), "Ready for booking", ThemeHelper.Success);
        _blacklistedCustomersCard.SetMetric(IconChar.UserSlash, "Blacklisted", counts.BlacklistedCustomers.ToString(), "Flagged clients", ThemeHelper.Danger);
        _archivedCustomersCard.SetMetric(IconChar.BoxArchive, "Archived Customers", counts.ArchivedCustomers.ToString(), "Hidden records", ThemeHelper.GrayIcon);
    }

    private void PopulateGrid(IReadOnlyList<Customer> allCustomers)
    {
        AddGridColumns();
        _customersGrid.Rows.Clear();

        int totalItems = allCustomers.Count;
        int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / _pageSize));
        if (_currentPage > totalPages) _currentPage = totalPages;
        
        _paginationLabel.Text = $"Page {_currentPage} of {totalPages} ({totalItems} records)";
        _prevPageButton.Enabled = _currentPage > 1;
        _nextPageButton.Enabled = _currentPage < totalPages;

        var pagedCustomers = allCustomers.Skip((_currentPage - 1) * _pageSize).Take(_pageSize);

        foreach (Customer customer in pagedCustomers)
        {
            _customersGrid.Rows.Add(
                customer.CustomerId,
                $"{customer.FirstName} {customer.LastName}".Trim(),
                customer.PhoneNumber,
                string.IsNullOrWhiteSpace(customer.Email) ? "-" : customer.Email,
                FormatAddress(customer),
                GetStatusText(customer),
                customer.BlacklistReason ?? "-");
        }

        _emptyStateLabel.Visible = totalItems == 0;
    }

    private static string GetStatusText(Customer customer)
    {
        if (customer.IsArchived)
        {
            return "Archived";
        }

        return customer.IsBlacklisted ? "Blacklisted" : "Active";
    }

    private static string FormatAddress(Customer customer)
    {
        string[] parts =
        [
            customer.StreetAddress ?? string.Empty,
            customer.Barangay ?? string.Empty,
            customer.City ?? string.Empty,
            customer.Province ?? string.Empty,
            customer.Region ?? string.Empty
        ];

        string address = string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(address) ? "-" : address;
    }

    private void UpdateTabStyles()
    {
        ApplyTabStyle(_activeButton, _filter == CustomerListFilter.Active);
        ApplyTabStyle(_blacklistedButton, _filter == CustomerListFilter.Blacklisted);
        ApplyTabStyle(_archivedButton, _filter == CustomerListFilter.Archived);
    }

    private static void ApplyTabStyle(IconButton button, bool isActive)
    {
        button.BackColor = isActive ? ThemeHelper.Primary : ThemeHelper.Surface;
        button.ForeColor = isActive ? Color.White : ThemeHelper.TextPrimary;
        button.IconColor = isActive ? Color.White : ThemeHelper.TextSecondary;
    }

    private void CustomersGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        string columnName = _customersGrid.Columns[e.ColumnIndex].Name;
        bool isStatus = columnName == "Status";
        bool isAction = columnName.EndsWith("Action", StringComparison.Ordinal);

        if (!isStatus && !isAction)
        {
            return;
        }

        e.PaintBackground(e.CellBounds, true);
        string text = isAction ? e.FormattedValue?.ToString() ?? string.Empty : e.Value?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (e.Graphics is null)
        {
            return;
        }

        Color backColor = GetPillBackColor(columnName, text);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Font font = e.CellStyle?.Font ?? FontHelper.SemiBold(9F);
        SizeF textSize = e.Graphics.MeasureString(text, font);
        float pillHeight = 26;
        float pillWidth = isAction ? e.CellBounds.Width - 10 : textSize.Width + 24;

        if (pillWidth > e.CellBounds.Width - 4)
        {
            pillWidth = e.CellBounds.Width - 4;
        }

        float x = isStatus
            ? e.CellBounds.X + 8
            : e.CellBounds.X + (e.CellBounds.Width - pillWidth) / 2;
        float y = e.CellBounds.Y + (e.CellBounds.Height - pillHeight) / 2;
        RectangleF rect = new(x, y, pillWidth, pillHeight);

        using GraphicsPath path = GetRoundedRect(rect, pillHeight / 2);
        using SolidBrush backBrush = new(backColor);
        using SolidBrush foreBrush = new(Color.White);
        e.Graphics.FillPath(backBrush, path);

        using StringFormat format = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };
        e.Graphics.DrawString(text, font, foreBrush, rect, format);
        e.Handled = true;
    }

    private static Color GetPillBackColor(string columnName, string text)
    {
        if (columnName == "Status")
        {
            return text switch
            {
                "Active" => ThemeHelper.Success,
                "Blacklisted" => ThemeHelper.Danger,
                "Archived" => ThemeHelper.GrayIcon,
                _ => ThemeHelper.GrayIcon
            };
        }

        return columnName switch
        {
            "ViewAction" => ThemeHelper.Primary,
            "EditAction" => ThemeHelper.Success,
            "BlacklistAction" => ThemeHelper.Danger,
            "RemoveBlacklistAction" => ThemeHelper.Warning,
            "ArchiveAction" => ThemeHelper.Danger,
            "RestoreAction" => ThemeHelper.Warning,
            _ => ThemeHelper.Primary
        };
    }

    private static GraphicsPath GetRoundedRect(RectangleF rect, float radius)
    {
        GraphicsPath path = new();
        float diameter = radius * 2;
        Size size = new((int)diameter, (int)diameter);
        RectangleF arc = new(rect.Location, size);

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

    private async void CustomersGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        string columnName = _customersGrid.Columns[e.ColumnIndex].Name;

        if (!columnName.EndsWith("Action", StringComparison.Ordinal))
        {
            return;
        }

        int customerId = Convert.ToInt32(_customersGrid.Rows[e.RowIndex].Cells["CustomerId"].Value);

        switch (columnName)
        {
            case "ViewAction":
                await ViewCustomerAsync(customerId);
                break;
            case "EditAction":
                await EditCustomerAsync(customerId);
                break;
            case "BlacklistAction":
                await BlacklistCustomerAsync(customerId);
                break;
            case "RemoveBlacklistAction":
                await RemoveBlacklistAsync(customerId);
                break;
            case "ArchiveAction":
                await ArchiveCustomerAsync(customerId);
                break;
            case "RestoreAction":
                await RestoreCustomerAsync(customerId);
                break;
        }
    }

    private void CustomersGrid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
    {
        _customersGrid.Cursor = IsActionColumn(e.ColumnIndex) ? Cursors.Hand : Cursors.Default;
    }

    private bool IsActionColumn(int columnIndex)
    {
        return columnIndex >= 0
            && _customersGrid.Columns[columnIndex].Name.EndsWith("Action", StringComparison.Ordinal);
    }

    private async void AddCustomerButton_Click(object? sender, EventArgs e)
    {
        using CustomerDetailsForm form = new(CustomerFormMode.Add, currentUserId: _currentUserId);

        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _filter = CustomerListFilter.Active;
            await LoadCustomersAsync();
        }
    }

    private async Task ViewCustomerAsync(int customerId)
    {
        Customer? customer = await GetCustomerOrRefreshAsync(customerId);
        if (customer is null)
        {
            return;
        }

        using CustomerDetailsForm form = new(CustomerFormMode.View, customer, _currentUserId);
        form.ShowDialog(this);
    }

    private async Task EditCustomerAsync(int customerId)
    {
        Customer? customer = await GetCustomerOrRefreshAsync(customerId);
        if (customer is null)
        {
            return;
        }

        using CustomerDetailsForm form = new(CustomerFormMode.Edit, customer, _currentUserId);

        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadCustomersAsync();
        }
    }

    private async Task BlacklistCustomerAsync(int customerId)
    {
        Customer? customer = await GetCustomerOrRefreshAsync(customerId);
        if (customer is null)
        {
            return;
        }

        using CustomerBlacklistReasonForm form = new();

        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await _customerService.ToggleBlacklistAsync(customerId, isBlacklisted: true, form.BlacklistReason);
        await LoadCustomersAsync();
    }

    private async Task RemoveBlacklistAsync(int customerId)
    {
        Customer? customer = await GetCustomerOrRefreshAsync(customerId);
        if (customer is null)
        {
            return;
        }

        bool confirmed = MessageBoxHelper.ShowConfirmWarning(
            $"Remove blacklist flag from {customer.FirstName} {customer.LastName}?",
            "Remove Blacklist");

        if (!confirmed)
        {
            return;
        }

        await _customerService.ToggleBlacklistAsync(customerId, isBlacklisted: false);
        await LoadCustomersAsync();
    }

    private async Task ArchiveCustomerAsync(int customerId)
    {
        Customer? customer = await GetCustomerOrRefreshAsync(customerId);
        if (customer is null)
        {
            return;
        }

        bool confirmed = MessageBoxHelper.ShowConfirmDanger(
            $"Archive {customer.FirstName} {customer.LastName}? This will hide the customer from active lists.",
            "Archive Customer");

        if (!confirmed)
        {
            return;
        }

        try
        {
            await _customerService.ArchiveCustomerAsync(customerId);
            await LoadCustomersAsync();
        }
        catch (FluentValidation.ValidationException exception)
        {
            MessageBoxHelper.ShowWarning(exception.Errors.FirstOrDefault()?.ErrorMessage ?? exception.Message, "Archive Customer");
        }
    }

    private async Task RestoreCustomerAsync(int customerId)
    {
        Customer? customer = await GetCustomerOrRefreshAsync(customerId);
        if (customer is null)
        {
            return;
        }

        bool confirmed = MessageBoxHelper.ShowConfirmWarning(
            $"Restore {customer.FirstName} {customer.LastName} to customer lists?",
            "Restore Customer");

        if (!confirmed)
        {
            return;
        }

        await _customerService.RestoreCustomerAsync(customerId);
        await LoadCustomersAsync();
    }

    private async Task<Customer?> GetCustomerOrRefreshAsync(int customerId)
    {
        Customer? customer = await _customerService.GetCustomerByIdAsync(customerId);

        if (customer is null)
        {
            MessageBoxHelper.ShowWarning("The selected customer record no longer exists.");
            await LoadCustomersAsync();
        }

        return customer;
    }
}
