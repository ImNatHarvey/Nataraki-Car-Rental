using System.Drawing.Drawing2D;
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
    private readonly int _currentUserId;
    private readonly TransactionService _transactionService;
    private readonly MetricCardControl _totalTransactionsCard = new();
    private readonly MetricCardControl _activeRentalsCard = new();
    private readonly MetricCardControl _unpaidTransactionsCard = new();
    private readonly MetricCardControl _completedThisMonthCard = new();
    private readonly TextBox _searchTextBox = new();
    private readonly ComboBox _statusFilterComboBox = new();
    private readonly ComboBox _paymentFilterComboBox = new();
    private readonly IconButton _transactionsTabButton = new();
    private readonly IconButton _archivedTabButton = new();
    private readonly Button _addTransactionButton;
    private readonly DataGridView _transactionsGrid = new();
    private readonly Label _emptyStateLabel = new();
    private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 350 };
    private int _currentPage = 1;
    private int _pageSize = 15;
    private readonly Label _paginationLabel = new();
    private readonly Button _prevPageButton = CreatePaginationButton("Previous");
    private readonly Button _nextPageButton = CreatePaginationButton("Next");
    private bool _showArchived;

    public TransactionControl(int currentUserId)
    {
        _currentUserId = currentUserId;
        _transactionService = new TransactionService(currentUserId);
        _addTransactionButton = CreatePrimaryIconButton("Add Transaction", IconChar.Plus, 158, 36);
        InitializeControl();
        Load += TransactionControl_Load;
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
                await LoadTransactionsAsync();
            }
        };
        _nextPageButton.Location = new Point(90, 8);
        _nextPageButton.Click += async (_, _) =>
        {
            _currentPage++;
            await LoadTransactionsAsync();
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
        ConfigureTabButton(_transactionsTabButton, "Transactions", IconChar.Receipt, new Point(0, 10));
        ConfigureTabButton(_archivedTabButton, "Archived", IconChar.BoxArchive, new Point(144, 10));

        _addTransactionButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _addTransactionButton.Location = new Point(0, 10);
        _addTransactionButton.Click += AddButton_Click;
        panel.Resize += (_, _) => _addTransactionButton.Left = Math.Max(0, panel.Width - _addTransactionButton.Width);

        _transactionsTabButton.Click += async (_, _) => await SwitchArchiveViewAsync(false);
        _archivedTabButton.Click += async (_, _) => await SwitchArchiveViewAsync(true);

        panel.Controls.Add(_transactionsTabButton);
        panel.Controls.Add(_archivedTabButton);
        panel.Controls.Add(_addTransactionButton);
        return panel;
    }

    private static void ConfigureTabButton(IconButton button, string text, IconChar icon, Point location)
    {
        button.Text = text;
        button.IconChar = icon;
        button.IconSize = 16;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.Location = location;
        button.Size = new Size(text == "Transactions" ? 136 : 112, 34);
        button.FlatStyle = FlatStyle.Flat;
        button.Cursor = Cursors.Hand;
        button.Font = FontHelper.SemiBold(9F);
        button.FlatAppearance.BorderSize = 0;
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
            _currentPage = 1;
            _searchTimer.Stop();
            _searchTimer.Start();
        };
        searchContainer.Controls.Add(_searchTextBox);
        searchContainer.Click += (_, _) => _searchTextBox.Focus();

        ConfigureFilter(_statusFilterComboBox, new Point(356, 8), ["All Status", TransactionConstants.Status.Active, TransactionConstants.Status.Completed, TransactionConstants.Status.Cancelled]);
        ConfigureFilter(_paymentFilterComboBox, new Point(536, 8), ["All Payment", .. TransactionConstants.PaymentStatus.All]);
        _statusFilterComboBox.SelectedIndexChanged += async (_, _) => { _currentPage = 1; await LoadTransactionsAsync(); };
        _paymentFilterComboBox.SelectedIndexChanged += async (_, _) => { _currentPage = 1; await LoadTransactionsAsync(); };
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
        _transactionsGrid.ScrollBars = ScrollBars.Both;
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
        _transactionsGrid.CellMouseClick += TransactionsGrid_CellMouseClick;
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
        _transactionsGrid.Columns.Add("AmountPaid", "Amount Paid");
        _transactionsGrid.Columns.Add("Balance", "Balance");
        _transactionsGrid.Columns.Add("Payment", "Payment");
        _transactionsGrid.Columns.Add("Status", "Status");
        
        DataGridViewTextBoxColumn actionsColumn = new()
        {
            Name = "Actions",
            HeaderText = "Actions",
            ReadOnly = true
        };
        _transactionsGrid.Columns.Add(actionsColumn);

        _transactionsGrid.Columns["TransactionId"]!.Visible = false;
        SetColumnSizing("TransactionCode", 145, 105);
        SetColumnSizing("Customer", 150, 110);
        SetColumnSizing("CarPlate", 165, 120);
        SetColumnSizing("Dates", 118, 95);
        SetColumnSizing("TotalAmount", 106, 86);
        SetColumnSizing("AmountPaid", 96, 78);
        SetColumnSizing("Balance", 96, 78);
        SetColumnSizing("Payment", 104, 86);
        SetColumnSizing("Status", 100, 82);
        SetColumnSizing("Actions", 286, 230);
    }

    private void SetColumnSizing(string columnName, float fillWeight, int minimumWidth)
    {
        if (_transactionsGrid.Columns[columnName] is DataGridViewColumn column)
        {
            column.FillWeight = fillWeight;
            column.MinimumWidth = minimumWidth;
        }
    }

    private int _lastHeight;

    private async void TransactionControl_Load(object? sender, EventArgs e)
    {
        Load -= TransactionControl_Load;
        _lastHeight = Height;
        Resize += async (_, _) =>
        {
            if (Math.Abs(Height - _lastHeight) > 50)
            {
                _lastHeight = Height;
                _currentPage = 1;
                await LoadTransactionsAsync();
            }
        };
        await LoadTransactionsAsync();
    }

    private async Task LoadTransactionsAsync()
    {
        try
        {
            _pageSize = Height > 700 ? 15 : 5;
            UpdateTabStyles();

            TransactionMetrics metrics = await _transactionService.GetMetricsAsync(DateTime.Today);
            UpdateMetricCards(metrics);
            string? status = _statusFilterComboBox.SelectedIndex <= 0 ? null : _statusFilterComboBox.SelectedItem?.ToString();
            string? payment = _paymentFilterComboBox.SelectedIndex <= 0 ? null : _paymentFilterComboBox.SelectedItem?.ToString();
            IReadOnlyList<TransactionListItem> transactions = await _transactionService.SearchTransactionsAsync(_searchTextBox.Text, status, payment, _showArchived);
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

    private void PopulateGrid(IReadOnlyList<TransactionListItem> allTransactions)
    {
        AddGridColumns();
        _transactionsGrid.Rows.Clear();
        
        int totalItems = allTransactions.Count;
        int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / _pageSize));
        if (_currentPage > totalPages) _currentPage = totalPages;
        
        _paginationLabel.Text = $"Page {_currentPage} of {totalPages} ({totalItems} records)";
        _prevPageButton.Enabled = _currentPage > 1;
        _nextPageButton.Enabled = _currentPage < totalPages;

        var pagedTransactions = allTransactions.Skip((_currentPage - 1) * _pageSize).Take(_pageSize);

        foreach (TransactionListItem transaction in pagedTransactions)
        {
            string actions = "View";
            if (_showArchived)
            {
                actions += "|Restore";
            }
            else if (transaction.TransactionStatus == TransactionConstants.Status.Active)
            {
                actions += "|Payment|Complete|Cancel";
            }
            else if (transaction.TransactionStatus is TransactionConstants.Status.Completed or TransactionConstants.Status.Cancelled)
            {
                actions += "|Archive";
            }

            _transactionsGrid.Rows.Add(
                transaction.TransactionId,
                transaction.TransactionCode,
                transaction.CustomerName,
                $"{transaction.CarName} ({transaction.PlateNumber})",
                $"{transaction.StartDate:MMM d} - {transaction.EndDate:MMM d}",
                FormatPeso(transaction.TotalAmount),
                FormatPeso(transaction.AmountPaid),
                FormatPeso(transaction.BalanceAmount),
                transaction.PaymentStatus,
                transaction.TransactionStatus,
                actions);
        }
        _emptyStateLabel.Visible = totalItems == 0;
    }

    private static string FormatPeso(decimal amount) => $"₱{amount:N2}";

    private static IconButton CreatePrimaryIconButton(string text, IconChar icon, int width, int height)
    {
        IconButton button = new()
        {
            Text = text,
            IconChar = icon,
            IconColor = Color.White,
            IconSize = 14,
            Size = new Size(width, height),
            BackColor = ThemeHelper.Primary,
            ForeColor = Color.White,
            Font = FontHelper.SemiBold(9F),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TextImageRelation = TextImageRelation.ImageBeforeText
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = ThemeHelper.PrimaryHover;
        return button;
    }

    private void TransactionsGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        string columnName = _transactionsGrid.Columns[e.ColumnIndex].Name;
        bool isBadge = columnName is "Payment" or "Status";
        bool isAction = columnName == "Actions";
        if (!isBadge && !isAction)
        {
            return;
        }

        e.PaintBackground(e.CellBounds, true);
        string text = e.Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || e.Graphics is null)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Font font = e.CellStyle?.Font ?? FontHelper.SemiBold(9F);

        if (isAction)
        {
            string[] actions = text.Split('|');
            float currentX = e.CellBounds.X + 4;
            float height = 26;
            float y = e.CellBounds.Y + (e.CellBounds.Height - height) / 2;

            foreach (string action in actions)
            {
                SizeF textSize = e.Graphics.MeasureString(action, font);
                float width = textSize.Width + 22;
                RectangleF rect = new(currentX, y, width, height);

                Color color = GetPillColor(action);
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
                e.Graphics.DrawString(action, font, foreground, rect, format);

                currentX += width + 6;
                if (currentX > e.CellBounds.Right - 4)
                {
                    break;
                }
            }
        }
        else
        {
            Color color = GetPillColor(text);
            SizeF textSize = e.Graphics.MeasureString(text, font);
            float height = 26;
            float width = Math.Min(textSize.Width + 24, e.CellBounds.Width - 4);
            float x = e.CellBounds.X + 8;
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
        }

        e.Handled = true;
    }

    private static Color GetPillColor(string text)
    {
        return text switch
        {
            TransactionConstants.PaymentStatus.Paid => ThemeHelper.Success,
            TransactionConstants.PaymentStatus.Partial => ThemeHelper.Warning,
            TransactionConstants.PaymentStatus.Unpaid => ThemeHelper.Danger,
            TransactionConstants.Status.Active => ThemeHelper.Success,
            TransactionConstants.Status.Completed => ThemeHelper.GrayIcon,
            TransactionConstants.Status.Cancelled => ThemeHelper.Danger,
            "View" => ThemeHelper.Primary,
            "Payment" => ThemeHelper.Warning,
            "Complete" => ThemeHelper.Success,
            "Cancel" => ThemeHelper.Danger,
            "Archive" => ThemeHelper.GrayIcon,
            "Restore" => ThemeHelper.Warning,
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

    private async void TransactionsGrid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Button != MouseButtons.Left)
        {
            return;
        }
        string columnName = _transactionsGrid.Columns[e.ColumnIndex].Name;
        if (columnName != "Actions")
        {
            return;
        }

        string text = _transactionsGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        int transactionId = Convert.ToInt32(_transactionsGrid.Rows[e.RowIndex].Cells["TransactionId"].Value);

        using Graphics g = _transactionsGrid.CreateGraphics();
        Font font = _transactionsGrid.DefaultCellStyle.Font ?? FontHelper.SemiBold(9F);
        string[] actions = text.Split('|');
        float currentX = 4;
        float height = 26;
        float y = (_transactionsGrid.Rows[e.RowIndex].Height - height) / 2F;
        
        foreach (string action in actions)
        {
            SizeF textSize = g.MeasureString(action, font);
            float width = textSize.Width + 22;
            RectangleF rect = new(currentX, y, width, height);
            
            if (rect.Contains(e.X, e.Y))
            {
                switch (action)
                {
                    case "View":
                        await ViewTransactionAsync(transactionId);
                        break;
                    case "Payment":
                        await EditTransactionAsync(transactionId);
                        break;
                    case "Complete":
                        await CompleteTransactionAsync(transactionId);
                        break;
                    case "Cancel":
                        await CancelTransactionAsync(transactionId);
                        break;
                    case "Archive":
                        await ArchiveTransactionAsync(transactionId);
                        break;
                    case "Restore":
                        await RestoreTransactionAsync(transactionId);
                        break;
                }
                return;
            }
            currentX += width + 6;
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

    private async Task RestoreTransactionAsync(int transactionId)
    {
        Transaction? transaction = await GetTransactionOrRefreshAsync(transactionId);
        if (transaction is null)
        {
            return;
        }
        if (!MessageBoxHelper.ShowConfirmWarning($"Restore transaction {transaction.TransactionCode} to the active list?", "Restore Transaction"))
        {
            return;
        }
        await _transactionService.RestoreTransactionAsync(transactionId, _currentUserId);
        MessageBoxHelper.ShowSuccess("Transaction restored successfully.");
        await LoadTransactionsAsync();
    }

    private async Task SwitchArchiveViewAsync(bool showArchived)
    {
        _showArchived = showArchived;
        _currentPage = 1;
        await LoadTransactionsAsync();
    }

    private void UpdateTabStyles()
    {
        ApplyTabStyle(_transactionsTabButton, !_showArchived);
        ApplyTabStyle(_archivedTabButton, _showArchived);
        _addTransactionButton.Visible = !_showArchived;
    }

    private static void ApplyTabStyle(IconButton button, bool isActive)
    {
        button.BackColor = isActive ? ThemeHelper.Primary : ThemeHelper.Surface;
        button.ForeColor = isActive ? Color.White : ThemeHelper.TextPrimary;
        button.IconColor = isActive ? Color.White : ThemeHelper.TextSecondary;
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
