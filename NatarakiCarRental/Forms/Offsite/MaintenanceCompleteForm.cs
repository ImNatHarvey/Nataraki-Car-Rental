using System.Diagnostics;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Forms.Offsite;

public sealed class MaintenanceCompleteForm : Form
{
    private readonly Transaction _transaction;
    private readonly ComboBox _conditionComboBox = CreateComboBox();
    
    private readonly NumericUpDown _maintenanceFeeInput = CreateMoneyInput();
    
    private readonly Button _browseInvoiceButton = CreateSecondaryButton("Browse", 80, 28);
    private readonly Button _openInvoiceButton = CreateSecondaryButton("Open File", 90, 28);
    private readonly Label _invoicePathLabel = new() { Text = "No file selected", AutoSize = true, Font = FontHelper.Italic(9F), ForeColor = ThemeHelper.TextSecondary, Location = new Point(174, 30) };
    private string? _selectedInvoicePath;
    
    private readonly ComboBox _modeOfPaymentComboBox = CreateComboBox();
    private readonly NumericUpDown _amountPaidInput = CreateMoneyInput();
    private readonly Label _validationLabel = new();

    public string ReturnCondition => _conditionComboBox.SelectedItem?.ToString() ?? "Good";
    public decimal MaintenanceFee => _maintenanceFeeInput.Value;
    public string? InvoiceFilePath => _selectedInvoicePath;
    
    public string ModeOfPayment => _modeOfPaymentComboBox.SelectedItem?.ToString() ?? "Cash";
    public decimal AmountPaid => _amountPaidInput.Value;

    public MaintenanceCompleteForm(Transaction transaction)
    {
        _transaction = transaction;
        InitializeForm();
    }

    private void InitializeForm()
    {
        Text = "Complete Maintenance";
        Size = new Size(580, 680);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = ThemeHelper.Surface;
        ShowInTaskbar = false;

        Label titleLabel = new()
        {
            Text = "Complete Maintenance",
            AutoSize = true,
            Location = new Point(32, 24),
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Label subtitleLabel = new()
        {
            Text = $"Maintenance: {_transaction.TransactionCode}",
            AutoSize = true,
            Location = new Point(34, 60),
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextSecondary
        };

        _validationLabel.AutoSize = false;
        _validationLabel.Location = new Point(34, 88);
        _validationLabel.Size = new Size(490, 24);
        _validationLabel.Font = FontHelper.SemiBold(9F);
        _validationLabel.ForeColor = ThemeHelper.Danger;
        _validationLabel.Visible = false;

        Panel contentPanel = new()
        {
            Location = new Point(32, 116),
            Size = new Size(500, 450),
            BackColor = ThemeHelper.Surface
        };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(CreateCompletionInfoSection(), 0, 0);
        layout.Controls.Add(CreateFeeSection(), 0, 1);
        layout.Controls.Add(CreatePaymentSection(), 0, 2);
        contentPanel.Controls.Add(layout);

        Button cancelButton = CreateSecondaryButton("Cancel", 110, 38);
        cancelButton.Location = new Point(220, 580);
        cancelButton.DialogResult = DialogResult.Cancel;

        Button completeButton = ControlFactory.CreatePrimaryButton("Complete", 190, 38);
        completeButton.Location = new Point(342, 580);
        completeButton.Click += CompleteButton_Click;

        Controls.Add(titleLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(_validationLabel);
        Controls.Add(contentPanel);
        Controls.Add(cancelButton);
        Controls.Add(completeButton);

        Click += (_, _) => ActiveControl = null;
        CancelButton = cancelButton;

        UpdateFeeState();
    }

    private GroupBox CreateCompletionInfoSection()
    {
        GroupBox section = CreateSection("Completion Information");
        _conditionComboBox.Items.AddRange(new[] { "Good", "Needs Follow-up", "Unresolved" });
        _conditionComboBox.SelectedIndex = 0;
        _conditionComboBox.Width = 460;

        Panel conditionPanel = new() { Width = 460, Height = 60, Location = new Point(16, 32) };
        Label condLabel = ControlFactory.CreateInputLabel("Condition *");
        condLabel.Location = new Point(0, 0);
        _conditionComboBox.Location = new Point(0, 24);
        conditionPanel.Controls.Add(condLabel);
        conditionPanel.Controls.Add(_conditionComboBox);

        section.Controls.Add(conditionPanel);
        return section;
    }

    private GroupBox CreateFeeSection()
    {
        GroupBox section = CreateSection("Maintenance Fee");
        
        _maintenanceFeeInput.Width = 200;
        _maintenanceFeeInput.ValueChanged += (_, _) => {
            UpdateFeeState();
            _amountPaidInput.Maximum = _maintenanceFeeInput.Value;
        };

        Panel chargePanel = new() { Width = 200, Height = 60, Location = new Point(16, 32) };
        Label chargeLabel = ControlFactory.CreateInputLabel("Total Cost (₱)");
        chargeLabel.Location = new Point(0, 0);
        _maintenanceFeeInput.Location = new Point(0, 24);
        chargePanel.Controls.Add(chargeLabel);
        chargePanel.Controls.Add(_maintenanceFeeInput);
        section.Controls.Add(chargePanel);

        return section;
    }

    private GroupBox CreatePaymentSection()
    {
        GroupBox section = CreateSection("Payment Information");

        _modeOfPaymentComboBox.Items.AddRange(TransactionConstants.ModeOfPayment.All.Cast<object>().ToArray());
        _modeOfPaymentComboBox.SelectedItem = TransactionConstants.ModeOfPayment.Cash;
        _modeOfPaymentComboBox.Width = 200;

        Panel modePanel = new() { Width = 200, Height = 60, Location = new Point(16, 32) };
        Label modeLabel = ControlFactory.CreateInputLabel("Mode of Payment");
        modeLabel.Location = new Point(0, 0);
        _modeOfPaymentComboBox.Location = new Point(0, 24);
        modePanel.Controls.Add(modeLabel);
        modePanel.Controls.Add(_modeOfPaymentComboBox);
        section.Controls.Add(modePanel);

        _amountPaidInput.Width = 210;
        _amountPaidInput.Maximum = _maintenanceFeeInput.Value;

        Panel amountPanel = new() { Width = 210, Height = 60, Location = new Point(236, 32) };
        Label amountLabel = ControlFactory.CreateInputLabel("Amount Paid (₱)");
        amountLabel.Location = new Point(0, 0);
        _amountPaidInput.Location = new Point(0, 24);
        amountPanel.Controls.Add(amountLabel);
        amountPanel.Controls.Add(_amountPaidInput);
        section.Controls.Add(amountPanel);

        Panel proofPanel = new() { Width = 460, Height = 70, Location = new Point(16, 96) };
        Label proofTitleLabel = ControlFactory.CreateInputLabel("Payment Receipt / Invoice");
        proofTitleLabel.Location = new Point(0, 0);

        _browseInvoiceButton.Location = new Point(0, 24);
        _browseInvoiceButton.Click -= BrowseInvoiceButton_Click;
        _browseInvoiceButton.Click += BrowseInvoiceButton_Click;
        
        _openInvoiceButton.Location = new Point(86, 24);
        _openInvoiceButton.Click -= OpenInvoiceButton_Click;
        _openInvoiceButton.Click += OpenInvoiceButton_Click;

        proofPanel.Controls.Add(proofTitleLabel);
        proofPanel.Controls.Add(_browseInvoiceButton);
        proofPanel.Controls.Add(_openInvoiceButton);
        proofPanel.Controls.Add(_invoicePathLabel);
        
        section.Controls.Add(proofPanel);

        return section;
    }

    private void UpdateFeeState()
    {
        bool hasFee = _maintenanceFeeInput.Value > 0;
        _browseInvoiceButton.Enabled = true;
        _openInvoiceButton.Enabled = !string.IsNullOrWhiteSpace(_selectedInvoicePath);
    }

    private void BrowseInvoiceButton_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "Image Files (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
            Title = "Select Fee Invoice"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _selectedInvoicePath = dialog.FileName;
            _invoicePathLabel.Text = Path.GetFileName(dialog.FileName);
            UpdateFeeState();
        }
    }

    private void OpenInvoiceButton_Click(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_selectedInvoicePath))
        {
            try { Process.Start(new ProcessStartInfo(_selectedInvoicePath) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBoxHelper.ShowError($"Unable to open file: {ex.Message}"); }
        }
    }

    private void CompleteButton_Click(object? sender, EventArgs e)
    {
        _validationLabel.Visible = false;
        
        if (_maintenanceFeeInput.Value > 0 && _amountPaidInput.Value < _maintenanceFeeInput.Value)
        {
            ShowValidation("Maintenance fee must be fully paid before completion.");
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void ShowValidation(string message)
    {
        _validationLabel.Text = message;
        _validationLabel.Visible = true;
    }

    private static GroupBox CreateSection(string title)
    {
        return new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 20, 8, 8),
            Margin = new Padding(0, 0, 0, 10),
            Font = FontHelper.SemiBold(9.5F),
            ForeColor = ThemeHelper.TextPrimary,
            BackColor = ThemeHelper.Surface
        };
    }

    private static ComboBox CreateComboBox()
    {
        return new ComboBox { Height = 30, DropDownStyle = ComboBoxStyle.DropDownList, Font = FontHelper.Regular(10F) };
    }

    private static NumericUpDown CreateMoneyInput()
    {
        return new NumericUpDown { DecimalPlaces = 2, Maximum = 1000000, Increment = 1000, ThousandsSeparator = true, Font = FontHelper.Regular(10F) };
    }

    private static Button CreateSecondaryButton(string text, int width, int height)
    {
        Button button = new() { Text = text, Size = new Size(width, height), BackColor = ThemeHelper.Surface, ForeColor = ThemeHelper.TextPrimary, Font = FontHelper.SemiBold(), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        button.FlatAppearance.BorderColor = ThemeHelper.Border;
        return button;
    }
}
