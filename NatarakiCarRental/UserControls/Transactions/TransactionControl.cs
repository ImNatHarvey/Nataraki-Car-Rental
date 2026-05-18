using System.Drawing.Drawing2D;
using System.Globalization;
using FontAwesome.Sharp;
using FluentValidation;
using NatarakiCarRental.Forms.Transactions;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.UserControls.Transactions;

public sealed class TransactionControl : UserControl
{
    private static readonly CultureInfo PhilippineCulture = new("en-PH");
    private readonly int _currentUserId;
    private readonly TransactionService _transactionService;
    private readonly MetricCardControl _totalTransactionsCard = new();
    private readonly MetricCardControl _activeRentalsCard = new();
    private readonly MetricCardControl _unpaidTransactionsCard = new();
    private readonly MetricCardControl _completedThisMonthCard = new();
    private readonly TextBox _searchTextBox = new();
    private readonly ComboBox _statusFilterComboBox = new();
    private readonly ComboBox _paymentFilterComboBox = new();
    private readonly DataGridView _transactionsGrid = new();
    private readonly Label _emptyStateLabel = new();
    private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 350 };

    public TransactionControl(int currentUserId)
    {
        _currentUserId = currentUserId;
        _transactionService = new TransactionService(currentUserId);
        InitializeControl();
        Load += TransactionControl_Load;
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
            RowCount = 5
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.Controls.Add(CreateHeaderPanel(), 0, 0);
        mainLayout.Controls.Add(CreateMetricGrid(), 0, 1);
        mainLayout.Controls.Add(CreateActionBarPanel(), 0, 2);
        mainLayout.Controls.Add(CreateSearchPanel(), 0, 3);
        mainLayout.Controls.Add(CreateTablePanel(), 0, 4);
        Controls.Add(mainLayout);
    }

    private static Panel CreateHeaderPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        panel.Controls.Add(new Label
        {
            Text = "Transactions",
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(260, 34),
            Font = FontHelper.Title(22F),
            ForeColor = ThemeHelper.TextPrimary
        });
        panel.Controls.Add(new Label
        {
            Text = "Manage rental transaction records and operational completion.",
            AutoSize = false,
            Location = new Point(2, 42),
            Size = new Size(620, 24),
            Font = FontHelper.Regular(10.5F),
            ForeColor = ThemeHelper.TextSecondary
        });
        return panel;
    }

    private TableLayoutPanel CreateMetricGrid()
    {
        TableLayoutPanel grid = new() { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0, 12, 0, 8) };
        for (int i = 0; i < 4; i++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }
        AddMetricCard(grid, _totalTransactionsCard, IconChar.Receipt, "Total Transactions", 0, "Active records", ThemeHelper.Primary);
        AddMetricCard(grid, _activeRentalsCard, IconChar.Key, "Active Rentals", 1, "Currently open", ThemeHelper.Success);
        AddMetricCard(grid, _unpaidTransactionsCard, IconChar.Wallet, "Unpaid Transactions", 2, "Needs settlement", ThemeHelper.Warning);
        AddMetricCard(grid, _completedThisMonthCard, IconChar.CircleCheck, "Completed This Month", 3, "Closed rentals", ThemeHelper.GrayIcon);
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
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        Button addButton = ControlFactory.CreatePrimaryButton("Add Transaction", 146, 36);
        addButton.Location = new Point(0, 10);
        addButton.Click += AddButton_Click;
        panel.Controls.Add(addButton);
        return panel;
    }

    private Panel CreateSearchPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        BorderedPanel searchContainer = new()
        {
            Size = new Size(340, 32),
            Location = new Point(0, 8),
            BackColor = ThemeHelper.Surface,
            BorderColor = ThemeHelper.Border,
            Cursor = Cursors.IBeam
        };
        searchContainer.Controls.Add(new IconPictureBox
        {
            IconChar = IconChar.MagnifyingGlass,
            IconColor = ThemeHelper.TextSecondary,
            IconSize = 18,
            BackColor = ThemeHelper.Surface,
            Location = new Point(8, 7),
            Size = new Size(20, 20)
        });
        _searchTextBox.BorderStyle = BorderStyle.None;
        _searchTextBox.PlaceholderText = "Search transaction code, customer, car, or plate number";
        _searchTextBox.BackColor = ThemeHelper.Surface;
        _searchTextBox.Font = FontHelper.Regular(10F);
        _searchTextBox.ForeColor = ThemeHelper.TextPrimary;
        _searchTextBox.Location = new Point(34, 7);
        _searchTextBox.Width = 296;
        _searchTextBox.TextChanged += (_, _) =>
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        };
        searchContainer.Controls.Add(_searchTextBox);
        searchContainer.Click += (_, _) => _searchTextBox.Focus();

        ConfigureFilter(_statusFilterComboBox, new Point(356, 8), ["All Status", TransactionConstants.Status.Active, TransactionConstants.Status.Completed, TransactionConstants.Status.Cancelled]);
        ConfigureFilter(_paymentFilterComboBox, new Point(536, 8), ["All Payment", .. TransactionConstants.PaymentStatus.All]);
        _statusFilterComboBox.SelectedIndexChanged += async (_, _) => await LoadTransactionsAsync();
        _paymentFilterComboBox.SelectedIndexChanged += async (_, _) => await LoadTransactionsAsync();
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await LoadTransactionsAsync();
        };
        panel.Controls.Add(searchContainer);
        panel.Controls.Add(_statusFilterComboBox);
        panel.Controls.Add(_paymentFilterComboBox);
        return panel;
    }

    private static void ConfigureFilter(ComboBox comboBox, Point location, object[] items)
    {
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.Font = FontHelper.Regular(10F);
        comboBox.ForeColor = ThemeHelper.TextPrimary;
        comboBox.Size = new Size(164, 30);
        comboBox.Location = location;
        comboBox.Items.AddRange(items);
        comboBox.SelectedIndex = 0;
    }

    private Panel CreateTablePanel()
    {
        Panel panel = ControlFactory.CreateCardPanel(new Size(0, 0));
        panel.Dock = DockStyle.Fill;
        panel.Padding = new Padding(18);

        _transactionsGrid.Dock = DockStyle.Fill;
        _transactionsGrid.AllowUserToAddRows = false;
        _transactionsGrid.AllowUserToDeleteRows = false;
        _transactionsGrid.AllowUserToResizeRows = false;
        _transactionsGrid.AllowUserToResizeColumns = false;
        _transactionsGrid.ScrollBars = ScrollBars.Vertical;
        _transactionsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _transactionsGrid.BackgroundColor = ThemeHelper.Surface;
        _transactionsGrid.BorderStyle = BorderStyle.None;
        _transactionsGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _transactionsGrid.ColumnHeadersHeight = 38;
        _transactionsGrid.EnableHeadersVisualStyles = false;
        _transactionsGrid.GridColor = ThemeHelper.Border;
        _transactionsGrid.ReadOnly = true;
        _transactionsGrid.RowHeadersVisible = false;
        _transactionsGrid.RowTemplate.Height = 38;
        _transactionsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _transactionsGrid.DefaultCellStyle.SelectionBackColor = ThemeHelper.Surface;
        _transactionsGrid.DefaultCellStyle.SelectionForeColor = ThemeHelper.TextPrimary;
        _transactionsGrid.ColumnHeadersDefaultCellStyle.BackColor = ThemeHelper.Primary;
        _transactionsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _transactionsGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemeHelper.Primary;
        _transactionsGrid.ColumnHeadersDefaultCellStyle.Font = FontHelper.SemiBold(9F);
        _transactionsGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _transactionsGrid.CellContentClick += TransactionsGrid_CellContentClick;
        _transactionsGrid.CellPainting += TransactionsGrid_CellPainting;

        _emptyStateLabel.Text = "No transaction records found.";
        _emptyStateLabel.Dock = DockStyle.Bottom;
        _emptyStateLabel.Height = 42;
        _emptyStateLabel.Font = FontHelper.Regular(10F);
        _emptyStateLabel.ForeColor = ThemeHelper.TextSecondary;
        _emptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        _emptyStateLabel.Visible = false;
        panel.Controls.Add(_transactionsGrid);
        panel.Controls.Add(_emptyStateLabel);
        return panel;
    }

    private void AddGridColumns()
    {
        _transactionsGrid.Columns.Clear();
        _transactionsGrid.Columns.Add("TransactionId", "Transaction ID");
        _transactionsGrid.Columns.Add("TransactionCode", "Transaction Code");
        _transactionsGrid.Columns.Add("Customer", "Customer");
        _transactionsGrid.Columns.Add("CarPlate", "Car / Plate");
        _transactionsGrid.Columns.Add("Dates", "Dates");
        _transactionsGrid.Columns.Add("TotalAmount", "Total Amount");
        _transactionsGrid.Columns.Add("Balance", "Balance");
        _transactionsGrid.Columns.Add("Payment", "Payment");
        _transactionsGrid.Columns.Add("Status", "Status");
        AddActionColumn("ViewAction", "Actions", "View");
        AddActionColumn("EditAction", "", "Edit");
        AddActionColumn("CompleteAction", "", "Complete");
        AddActionColumn("CancelAction", "", "Cancel");
        AddActionColumn("ArchiveAction", "", "Archive");
        _transactionsGrid.Columns["TransactionId"]!.Visible = false;
        SetFillWeight("TransactionCode", 90);
        SetFillWeight("Customer", 100);
        SetFillWeight("CarPlate", 100);
        SetFillWeight("Dates", 95);
        SetFillWeight("TotalAmount", 80);
        SetFillWeight("Balance", 76);
        SetFillWeight("Payment", 74);
        SetFillWeight("Status", 74);
        SetFillWeight("ViewAction", 58);
        SetFillWeight("EditAction", 58);
        SetFillWeight("CompleteAction", 72);
        SetFillWeight("CancelAction", 62);
        SetFillWeight("ArchiveAction", 62);
    }

    private void AddActionColumn(string name, string headerText, string buttonText)
    {
        DataGridViewButtonColumn column = new()
        {
            Name = name,
            HeaderText = headerText,
            Text = buttonText,
            UseColumnTextForButtonValue = false,
            FlatStyle = FlatStyle.Flat
        };
        column.DefaultCellStyle.BackColor = ThemeHelper.Surface;
        column.DefaultCellStyle.SelectionBackColor = ThemeHelper.Surface;
        _transactionsGrid.Columns.Add(column);
    }

    private void SetFillWeight(string columnName, float weight)
    {
        if (_transactionsGrid.Columns[columnName] is DataGridViewColumn column)
        {
            column.FillWeight = weight;
        }
    }

    private async void TransactionControl_Load(object? sender, EventArgs e)
    {
        Load -= TransactionControl_Load;
        await LoadTransactionsAsync();
    }

    private async Task LoadTransactionsAsync()
    {
        try
        {
            TransactionMetrics metrics = await _transactionService.GetMetricsAsync(DateTime.Today);
            UpdateMetricCards(metrics);
            string? status = _statusFilterComboBox.SelectedIndex <= 0 ? null : _statusFilterComboBox.SelectedItem?.ToString();
            string? payment = _paymentFilterComboBox.SelectedIndex <= 0 ? null : _paymentFilterComboBox.SelectedItem?.ToString();
            IReadOnlyList<TransactionListItem> transactions = await _transactionService.SearchTransactionsAsync(_searchTextBox.Text, status, payment);
            PopulateGrid(transactions);
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to load transaction records.\n\n{exception.Message}", "Transactions");
        }
    }

    private void UpdateMetricCards(TransactionMetrics metrics)
    {
        _totalTransactionsCard.SetMetric(IconChar.Receipt, "Total Transactions", metrics.TotalTransactions.ToString(), "Active records", ThemeHelper.Primary);
        _activeRentalsCard.SetMetric(IconChar.Key, "Active Rentals", metrics.ActiveTransactions.ToString(), "Currently open", ThemeHelper.Success);
        _unpaidTransactionsCard.SetMetric(IconChar.Wallet, "Unpaid Transactions", metrics.UnpaidTransactions.ToString(), "Needs settlement", ThemeHelper.Warning);
        _completedThisMonthCard.SetMetric(IconChar.CircleCheck, "Completed This Month", metrics.CompletedTransactions.ToString(), "Closed rentals", ThemeHelper.GrayIcon);
    }

    private void PopulateGrid(IReadOnlyList<TransactionListItem> transactions)
    {
        AddGridColumns();
        _transactionsGrid.Rows.Clear();
        foreach (TransactionListItem transaction in transactions)
        {
            _transactionsGrid.Rows.Add(
                transaction.TransactionId,
                transaction.TransactionCode,
                transaction.CustomerName,
                $"{transaction.CarName} ({transaction.PlateNumber})",
                $"{transaction.StartDate:MMM d} - {transaction.EndDate:MMM d}",
                transaction.TotalAmount.ToString("C", PhilippineCulture),
                transaction.BalanceAmount.ToString("C", PhilippineCulture),
                transaction.PaymentStatus,
                transaction.TransactionStatus,
                "View",
                transaction.TransactionStatus == TransactionConstants.Status.Active ? "Edit" : string.Empty,
                transaction.TransactionStatus == TransactionConstants.Status.Active ? "Complete" : string.Empty,
                transaction.TransactionStatus == TransactionConstants.Status.Active ? "Cancel" : string.Empty,
                transaction.TransactionStatus is TransactionConstants.Status.Completed or TransactionConstants.Status.Cancelled ? "Archive" : string.Empty);
        }
        _emptyStateLabel.Visible = transactions.Count == 0;
    }

    private void TransactionsGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        string columnName = _transactionsGrid.Columns[e.ColumnIndex].Name;
        bool isBadge = columnName is "Payment" or "Status";
        bool isAction = columnName.EndsWith("Action", StringComparison.Ordinal);
        if (!isBadge && !isAction)
        {
            return;
        }

        e.PaintBackground(e.CellBounds, true);
        string text = isAction ? e.FormattedValue?.ToString() ?? string.Empty : e.Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || e.Graphics is null)
        {
            return;
        }

        Color color = GetPillColor(columnName, text);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Font font = e.CellStyle?.Font ?? FontHelper.SemiBold(9F);
        SizeF textSize = e.Graphics.MeasureString(text, font);
        float height = 26;
        float width = isAction ? e.CellBounds.Width - 10 : Math.Min(textSize.Width + 24, e.CellBounds.Width - 4);
        float x = isAction ? e.CellBounds.X + (e.CellBounds.Width - width) / 2 : e.CellBounds.X + 8;
        float y = e.CellBounds.Y + (e.CellBounds.Height - height) / 2;
        RectangleF rect = new(x, y, width, height);
        using GraphicsPath path = CreateRoundedRect(rect, height / 2);
        using SolidBrush background = new(color);
        using SolidBrush foreground = new(Color.White);
        e.Graphics.FillPath(background, path);
        using StringFormat format = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };
        e.Graphics.DrawString(text, font, foreground, rect, format);
        e.Handled = true;
    }

    private static Color GetPillColor(string columnName, string text)
    {
        return columnName switch
        {
            "Payment" => text switch
            {
                TransactionConstants.PaymentStatus.Paid => ThemeHelper.Success,
                TransactionConstants.PaymentStatus.Partial => Color.FromArgb(234, 88, 12),
                _ => ThemeHelper.Warning
            },
            "Status" => text switch
            {
                TransactionConstants.Status.Active => ThemeHelper.Success,
                TransactionConstants.Status.Completed => ThemeHelper.GrayIcon,
                TransactionConstants.Status.Cancelled => ThemeHelper.Danger,
                _ => ThemeHelper.Warning
            },
            "ViewAction" => ThemeHelper.Primary,
            "EditAction" => ThemeHelper.Primary,
            "CompleteAction" => ThemeHelper.Success,
            "CancelAction" => ThemeHelper.Danger,
            "ArchiveAction" => ThemeHelper.GrayIcon,
            _ => ThemeHelper.Primary
        };
    }

    private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
    {
        GraphicsPath path = new();
        float diameter = radius * 2;
        RectangleF arc = new(rect.Location, new SizeF(diameter, diameter));
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

    private async void AddButton_Click(object? sender, EventArgs e)
    {
        using TransactionDetailsForm form = new(_currentUserId);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadTransactionsAsync();
        }
    }

    private async void TransactionsGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }
        string columnName = _transactionsGrid.Columns[e.ColumnIndex].Name;
        if (!columnName.EndsWith("Action", StringComparison.Ordinal))
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(_transactionsGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString()))
        {
            return;
        }
        int transactionId = Convert.ToInt32(_transactionsGrid.Rows[e.RowIndex].Cells["TransactionId"].Value);
        switch (columnName)
        {
            case "ViewAction":
                await ViewTransactionAsync(transactionId);
                break;
            case "EditAction":
                await EditTransactionAsync(transactionId);
                break;
            case "CompleteAction":
                await CompleteTransactionAsync(transactionId);
                break;
            case "CancelAction":
                await CancelTransactionAsync(transactionId);
                break;
            case "ArchiveAction":
                await ArchiveTransactionAsync(transactionId);
                break;
        }
    }

    private async Task ViewTransactionAsync(int transactionId)
    {
        Transaction? transaction = await GetTransactionOrRefreshAsync(transactionId);
        if (transaction is null)
        {
            return;
        }
        using TransactionDetailsForm form = new(transaction);
        form.ShowDialog(this);
    }

    private async Task CompleteTransactionAsync(int transactionId)
    {
        Transaction? transaction = await GetTransactionOrRefreshAsync(transactionId);
        if (transaction is null)
        {
            return;
        }
        if (!MessageBoxHelper.ShowConfirmWarning($"Complete transaction {transaction.TransactionCode}?", "Complete Transaction"))
        {
            return;
        }
        try
        {
            await _transactionService.CompleteTransactionAsync(transactionId, _currentUserId);
            MessageBoxHelper.ShowSuccess("Transaction completed successfully.");
            await LoadTransactionsAsync();
        }
        catch (ValidationException exception)
        {
            MessageBoxHelper.ShowWarning(exception.Errors.FirstOrDefault()?.ErrorMessage ?? exception.Message, "Complete Transaction");
        }
    }

    private async Task EditTransactionAsync(int transactionId)
    {
        Transaction? transaction = await GetTransactionOrRefreshAsync(transactionId);
        if (transaction is null || transaction.TransactionStatus != TransactionConstants.Status.Active)
        {
            return;
        }
        using TransactionDetailsForm form = new(transaction, _currentUserId);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadTransactionsAsync();
        }
    }

    private async Task CancelTransactionAsync(int transactionId)
    {
        Transaction? transaction = await GetTransactionOrRefreshAsync(transactionId);
        if (transaction is null)
        {
            return;
        }
        if (!MessageBoxHelper.ShowConfirmDanger($"Cancel transaction {transaction.TransactionCode}?", "Cancel Transaction"))
        {
            return;
        }
        try
        {
            await _transactionService.CancelTransactionAsync(transactionId, _currentUserId);
            MessageBoxHelper.ShowSuccess("Transaction cancelled successfully.");
            await LoadTransactionsAsync();
        }
        catch (ValidationException exception)
        {
            MessageBoxHelper.ShowWarning(exception.Errors.FirstOrDefault()?.ErrorMessage ?? exception.Message, "Cancel Transaction");
        }
    }

    private async Task ArchiveTransactionAsync(int transactionId)
    {
        Transaction? transaction = await GetTransactionOrRefreshAsync(transactionId);
        if (transaction is null)
        {
            return;
        }
        if (!MessageBoxHelper.ShowConfirmDanger($"Archive transaction {transaction.TransactionCode}? This keeps the linked schedule history.", "Archive Transaction"))
        {
            return;
        }
        await _transactionService.ArchiveTransactionAsync(transactionId, _currentUserId);
        MessageBoxHelper.ShowSuccess("Transaction archived successfully.");
        await LoadTransactionsAsync();
    }

    private async Task<Transaction?> GetTransactionOrRefreshAsync(int transactionId)
    {
        Transaction? transaction = await _transactionService.GetByIdAsync(transactionId);
        if (transaction is null)
        {
            MessageBoxHelper.ShowWarning("The selected transaction record no longer exists.");
            await LoadTransactionsAsync();
        }
        return transaction;
    }
}
