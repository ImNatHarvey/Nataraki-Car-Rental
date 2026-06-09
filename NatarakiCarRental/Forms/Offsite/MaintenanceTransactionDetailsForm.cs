using System.Drawing.Drawing2D;
using FluentValidation;
using FluentValidation.Results;
using System.Diagnostics;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.Forms.Offsite;

public sealed class MaintenanceTransactionDetailsForm : Form
{
    private readonly Transaction _transaction;
    private readonly int _currentUserId;
    private readonly TransactionService _transactionService;
    private readonly ErrorProvider _errorProvider = new();
    private readonly Label _validationLabel = new();
    private readonly TableLayoutPanel _viewLayout = new();
    private readonly DataGridView _paymentsGrid = new();

    public MaintenanceTransactionDetailsForm(int currentUserId, Transaction transaction, bool viewOnly = false)
    {
        _transaction = transaction;
        _currentUserId = currentUserId;
        _transactionService = new TransactionService(currentUserId);
        InitializeForm();
        LoadMaintenanceData(transaction);
        if (viewOnly) DisableActions();
        Load += MaintenanceTransactionDetailsForm_Load;
    }

    private void InitializeForm()
    {
        Text = "Maintenance Details";
        ClientSize = new Size(1060, 680);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = ThemeHelper.Surface;
        Font = FontHelper.Regular();
        ShowInTaskbar = false;

        Controls.Add(new Label { Text = "Maintenance Details", AutoSize = false, Location = new Point(32, 24), Size = new Size(400, 34), Font = FontHelper.Title(18F), ForeColor = ThemeHelper.TextPrimary });

        _validationLabel.AutoSize = false; _validationLabel.Location = new Point(34, 66); _validationLabel.Size = new Size(996, 24); _validationLabel.Font = FontHelper.SemiBold(9F); _validationLabel.ForeColor = ThemeHelper.Danger; _validationLabel.Visible = false;
        Controls.Add(_validationLabel);

        CreateViewLayout();
        CreatePaymentsGrid(new Point(32, 376), new Size(996, 222));

        Button closeButton = CreateSecondaryButton("Close", 110, 38);
        closeButton.Location = new Point(918, 620);
        closeButton.Click += (_, _) => Close();
        Controls.Add(closeButton);

        Click += (_, _) => ActiveControl = null;
    }

    private void CreateViewLayout()
    {
        _viewLayout.Dock = DockStyle.Fill; _viewLayout.ColumnCount = 3; _viewLayout.RowCount = 4;
        _viewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F)); _viewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F)); _viewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        for (int i = 0; i < 4; i++) _viewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 53F));
        GroupBox section = CreateSection("Maintenance Information", _viewLayout);
        section.Location = new Point(32, 90); section.Size = new Size(996, 268);
        Controls.Add(section);
    }

    private void CreatePaymentsGrid(Point location, Size size)
    {
        Panel p = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.Surface };
        _paymentsGrid.Dock = DockStyle.Fill; _paymentsGrid.AllowUserToAddRows = false; _paymentsGrid.AllowUserToDeleteRows = false; _paymentsGrid.AllowUserToResizeRows = false; _paymentsGrid.ReadOnly = true; _paymentsGrid.RowHeadersVisible = false; _paymentsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _paymentsGrid.BackgroundColor = ThemeHelper.Surface; _paymentsGrid.BorderStyle = BorderStyle.FixedSingle; _paymentsGrid.CellBorderStyle = DataGridViewCellBorderStyle.Single; _paymentsGrid.GridColor = ThemeHelper.TableGridLine; _paymentsGrid.EnableHeadersVisualStyles = false; _paymentsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _paymentsGrid.ColumnHeadersHeight = 38; _paymentsGrid.RowTemplate.Height = 38;
        _paymentsGrid.ColumnHeadersDefaultCellStyle.BackColor = ThemeHelper.Primary; _paymentsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White; _paymentsGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemeHelper.Primary; _paymentsGrid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White; _paymentsGrid.ColumnHeadersDefaultCellStyle.Font = FontHelper.SemiBold(9F); _paymentsGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _paymentsGrid.DefaultCellStyle.BackColor = ThemeHelper.Surface; _paymentsGrid.DefaultCellStyle.ForeColor = ThemeHelper.TextPrimary; _paymentsGrid.DefaultCellStyle.SelectionBackColor = ThemeHelper.Surface; _paymentsGrid.DefaultCellStyle.SelectionForeColor = ThemeHelper.TextPrimary;
        _paymentsGrid.Font = FontHelper.Regular(9F);
        _paymentsGrid.Columns.Add("Date", "Date"); _paymentsGrid.Columns.Add("Amount", "Amount Paid"); _paymentsGrid.Columns.Add("Type", "Type"); _paymentsGrid.Columns.Add("Proof", "Proof"); _paymentsGrid.Columns.Add("Path", "Path"); _paymentsGrid.Columns["Path"]!.Visible = false;
        _paymentsGrid.CellPainting += PaymentsGrid_CellPainting; _paymentsGrid.CellMouseClick += PaymentsGrid_CellMouseClick;
        p.Controls.Add(_paymentsGrid); GroupBox section = CreateSection("Payment Ledger", p); section.Location = location; section.Size = size; Controls.Add(section);
    }

    private void LoadMaintenanceData(Transaction t)
    {
        _viewLayout.Controls.Clear();
        AddViewCell(0, 0, "Code", t.TransactionCode); AddViewCell(1, 0, "Vehicle", $"{t.CarName} ({t.PlateNumber})"); AddViewCell(2, 0, "Duration", $"{t.StartDate:MMM d, yyyy} - {t.EndDate:MMM d, yyyy}"); AddViewCell(3, 0, "Total Cost", FormatPeso(t.TotalAmount));
        AddViewCell(0, 1, "Client / Partner", t.CustomerName); AddViewCell(1, 1, "Ref #", $"#{t.FleetScheduleId}"); AddViewCell(2, 1, "Recorded", t.CreatedAt.ToString("MMM d, yyyy h:mm tt")); AddViewCell(3, 1, "Mode of Payment", t.ModeOfPayment);
        AddViewCell(0, 2, "Paid Amount", FormatPeso(t.AmountPaid)); AddViewCell(1, 2, "Balance", FormatPeso(t.BalanceAmount)); AddViewCell(2, 2, "Pay Status", t.PaymentStatus); AddViewCell(3, 2, "Overall Status", t.TransactionStatus);
    }

    private async void MaintenanceTransactionDetailsForm_Load(object? sender, EventArgs e) => await LoadPaymentsAsync();

    private async Task LoadPaymentsAsync()
    {
        try {
            var payments = await _transactionService.GetPaymentsAsync(_transaction.TransactionId);
            _paymentsGrid.Rows.Clear();
            foreach (var p in payments) _paymentsGrid.Rows.Add(p.PaymentDate.ToString("yyyy-MM-dd HH:mm"), FormatPeso(p.Amount), p.ModeOfPayment, string.IsNullOrWhiteSpace(p.ReceiptFilePath) ? "-" : "View", p.ReceiptFilePath ?? string.Empty);
        } catch { }
    }

    private void DisableActions() { }

    private void AddViewCell(int r, int c, string l, string v) => _viewLayout.Controls.Add(CreateReadOnlyValue(l, v), c, r);
    private static void OpenProof(string? s, string? t) { string? p = !string.IsNullOrWhiteSpace(s) && File.Exists(s) ? s : UploadPathHelper.ResolvePaymentReceiptPath(t); if (!string.IsNullOrWhiteSpace(p)) Process.Start(new ProcessStartInfo(p) { UseShellExecute = true }); }
    private void PaymentsGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e) 
    { 
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics == null) return;
        
        if (_paymentsGrid.Columns[e.ColumnIndex].Name == "Proof" && e.Value?.ToString() == "View") 
        { 
            e.PaintBackground(e.CellBounds, true); 
            float h = 22, w = 50;
            float x = e.CellBounds.X + (e.CellBounds.Width - w) / 2;
            float y = e.CellBounds.Y + (e.CellBounds.Height - h) / 2; 
            
            using GraphicsPath path = CreateRoundedRect(new RectangleF(x, y, w, h), h / 2); 
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; 
            
            using SolidBrush b = new(ThemeHelper.Primary); 
            e.Graphics.FillPath(b, path); 
            e.Graphics.DrawString("View", FontHelper.SemiBold(8.5F), Brushes.White, new RectangleF(x, y, w, h), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); 
            e.Handled = true; 
        } 
    }

    private void PaymentsGrid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e) 
    { 
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || sender is not DataGridView grid) return;
        
        if (e.ColumnIndex >= grid.Columns.Count) return;
        
        var column = grid.Columns[e.ColumnIndex];
        var row = grid.Rows[e.RowIndex];
        
        if (column.Name == "Proof" && row.Cells[e.ColumnIndex].Value?.ToString() == "View") 
        { 
            var pathValue = row.Cells["Path"].Value?.ToString();
            OpenProof(null, pathValue); 
        } 
    }
    private static Panel CreateReadOnlyValue(string l, string v) { Panel p = new() { Dock = DockStyle.Fill }; p.Controls.Add(new Label { Text = l, AutoSize = false, Location = new Point(0, 0), Size = new Size(292, 18), Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextSecondary }); p.Controls.Add(new Label { Text = v, AutoSize = false, Location = new Point(0, 19), Size = new Size(292, 26), Font = FontHelper.Regular(10F), ForeColor = ThemeHelper.TextPrimary, AutoEllipsis = true }); return p; }
    private static Panel CreateInputPanel(string l, Control i, Point p) { Panel pan = new() { Location = p, Size = new Size(i.Width, i.Height + 24) }; Label lbl = ControlFactory.CreateInputLabel(l); lbl.Location = new Point(0, 0); i.Location = new Point(0, 22); pan.Controls.Add(lbl); pan.Controls.Add(i); return pan; }
    private static Panel CreateProofPickerPanel(string l, Label pl, Button b, Button o) { Panel p = new(); Label tl = ControlFactory.CreateInputLabel(l); tl.Location = new Point(0, 0); b.Location = new Point(0, 24); o.Location = new Point(96, 24); pl.Location = new Point(204, 29); p.Controls.AddRange([tl, b, o, pl]); return p; }
    private static GroupBox CreateSection(string t, Control c) { GroupBox s = new() { Text = t, Padding = new Padding(16, 28, 16, 12), Font = FontHelper.SemiBold(9.5F), ForeColor = ThemeHelper.TextPrimary }; c.Dock = DockStyle.Fill; s.Controls.Add(c); return s; }
    private static ComboBox CreateComboBox(int w = 280) => new() { Width = w, Height = 30, DropDownStyle = ComboBoxStyle.DropDownList, Font = FontHelper.Regular(10F) };
    private static NumericUpDown CreateMoneyInput() => new() { Width = 280, Height = 30, DecimalPlaces = 2, Maximum = 1000000, Increment = 1000, ThousandsSeparator = true, Font = FontHelper.Regular(10F) };
    private static Label CreatePathLabel() => new() { AutoSize = false, Text = "No file selected", Font = FontHelper.Regular(9F), ForeColor = ThemeHelper.TextSecondary, AutoEllipsis = true };
    private static string FormatPeso(decimal a) => $"₱{a:N2}";
    private static Button CreateSecondaryButton(string t, int w, int h) { Button b = new() { Text = t, Size = new Size(w, h), BackColor = ThemeHelper.Surface, ForeColor = ThemeHelper.TextPrimary, Font = FontHelper.SemiBold(), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = ThemeHelper.Border; return b; }
    private static GraphicsPath CreateRoundedRect(RectangleF r, float rd) { GraphicsPath p = new(); float d = rd * 2; RectangleF a = new(r.Location, new SizeF(d, d)); p.AddArc(a, 180, 90); a.X = r.Right - d; p.AddArc(a, 270, 90); a.Y = r.Bottom - d; p.AddArc(a, 0, 90); a.X = r.Left; p.AddArc(a, 90, 90); p.CloseFigure(); return p; }
}
