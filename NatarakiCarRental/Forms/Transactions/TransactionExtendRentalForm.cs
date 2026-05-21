using System.Diagnostics;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Forms.Transactions;

public sealed class TransactionExtendRentalForm : Form
{
    private readonly Transaction _transaction;
    private readonly DateTimePicker _newEndDatePicker = CreateDatePicker();
    private readonly Label _extraDaysLabel = CreateSummaryValueLabel();
    private readonly Label _extensionChargeLabel = CreateSummaryValueLabel();
    private readonly ComboBox _modeOfPaymentComboBox = CreateComboBox();
    private readonly NumericUpDown _amountPaidInput = CreateMoneyInput();
    private readonly Button _browseProofButton = CreateSecondaryButton("Browse", 80, 28);
    private readonly Button _openProofButton = CreateSecondaryButton("Open File", 90, 28);
    private readonly Label _proofPathLabel = new() { Text = "No file selected", AutoSize = true, Font = FontHelper.Italic(9F), ForeColor = ThemeHelper.TextSecondary, Location = new Point(174, 30) };
    private string? _selectedProofPath;
    private readonly Label _validationLabel = new();

    public DateTime NewEndDate => _newEndDatePicker.Value.Date;
    public string ModeOfPayment => _modeOfPaymentComboBox.SelectedItem?.ToString() ?? "Cash";
    public decimal AmountPaid => _amountPaidInput.Value;
    public string? ReceiptFilePath => _selectedProofPath;

    public TransactionExtendRentalForm(Transaction transaction)
    {
        _transaction = transaction;
        InitializeForm();
    }

    private void InitializeForm()
    {
        Text = "Extend Rental";
        Size = new Size(540, 680);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = ThemeHelper.Surface;
        ShowInTaskbar = false;

        Label titleLabel = new()
        {
            Text = "Extend Rental",
            AutoSize = true,
            Location = new Point(32, 24),
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Label subtitleLabel = new()
        {
            Text = $"Transaction: {_transaction.TransactionCode} | Current End: {_transaction.EndDate:MMM d, yyyy}",
            AutoSize = true,
            Location = new Point(34, 60),
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextSecondary
        };

        _validationLabel.AutoSize = false;
        _validationLabel.Location = new Point(34, 88);
        _validationLabel.Size = new Size(472, 24);
        _validationLabel.Font = FontHelper.SemiBold(9F);
        _validationLabel.ForeColor = ThemeHelper.Danger;
        _validationLabel.Visible = false;

        Panel contentPanel = new()
        {
            Location = new Point(32, 116),
            Size = new Size(472, 450),
            BackColor = ThemeHelper.Surface
        };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(CreateExtensionInfoSection(), 0, 0);
        layout.Controls.Add(CreatePaymentSection(), 0, 1);
        contentPanel.Controls.Add(layout);

        Button cancelButton = CreateSecondaryButton("Cancel", 110, 38);
        cancelButton.Location = new Point(234, 590);
        cancelButton.DialogResult = DialogResult.Cancel;

        Button extendButton = ControlFactory.CreatePrimaryButton("Extend Rental", 140, 38);
        extendButton.Location = new Point(356, 590);
        extendButton.Click += ExtendButton_Click;

        Controls.Add(titleLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(_validationLabel);
        Controls.Add(contentPanel);
        Controls.Add(cancelButton);
        Controls.Add(extendButton);

        // Add blank area click handler to remove focus from inputs
        Click += (_, _) => ActiveControl = null;
        CancelButton = cancelButton;

        _newEndDatePicker.Value = _transaction.EndDate.AddDays(1);
        UpdateExtensionSummary();
    }

    private GroupBox CreateExtensionInfoSection()
    {
        GroupBox section = CreateSection("Extension Details");
        
        _newEndDatePicker.Width = 200;
        _newEndDatePicker.MinDate = _transaction.EndDate.AddDays(1);
        _newEndDatePicker.ValueChanged += (_, _) => UpdateExtensionSummary();

        Panel datePanel = new() { Width = 200, Height = 60, Location = new Point(16, 32) };
        Label dateLabel = ControlFactory.CreateInputLabel("New End Date *");
        dateLabel.Location = new Point(0, 0);
        _newEndDatePicker.Location = new Point(0, 24);
        datePanel.Controls.Add(dateLabel);
        datePanel.Controls.Add(_newEndDatePicker);
        section.Controls.Add(datePanel);

        Panel summaryPanel = new() { Width = 210, Height = 60, Location = new Point(236, 32) };
        Label summaryLabel = ControlFactory.CreateInputLabel("Extension Summary");
        summaryLabel.Location = new Point(0, 0);
        _extraDaysLabel.Location = new Point(0, 24);
        _extraDaysLabel.Width = 210;
        summaryPanel.Controls.Add(summaryLabel);
        summaryPanel.Controls.Add(_extraDaysLabel);
        section.Controls.Add(summaryPanel);

        Label dailyRateLabel = new()
        {
            Text = $"Daily Rate: ₱{_transaction.DailyRate:N2}",
            AutoSize = true,
            Location = new Point(16, 104),
            Font = FontHelper.SemiBold(9.5F),
            ForeColor = ThemeHelper.TextPrimary
        };
        section.Controls.Add(dailyRateLabel);

        _extensionChargeLabel.Location = new Point(16, 130);
        _extensionChargeLabel.Width = 430;
        _extensionChargeLabel.Font = FontHelper.SemiBold(11F);
        _extensionChargeLabel.ForeColor = ThemeHelper.Primary;
        section.Controls.Add(_extensionChargeLabel);

        return section;
    }

    private GroupBox CreatePaymentSection()
    {
        GroupBox section = CreateSection("Payment (Optional)");

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
        _amountPaidInput.ValueChanged += (_, _) => UpdateProofState();

        Panel amountPanel = new() { Width = 210, Height = 60, Location = new Point(236, 32) };
        Label amountLabel = ControlFactory.CreateInputLabel("Amount Paid (₱)");
        amountLabel.Location = new Point(0, 0);
        _amountPaidInput.Location = new Point(0, 24);
        amountPanel.Controls.Add(amountLabel);
        amountPanel.Controls.Add(_amountPaidInput);
        section.Controls.Add(amountPanel);

        Panel proofPanel = new() { Width = 460, Height = 70, Location = new Point(16, 104) };
        Label proofTitleLabel = ControlFactory.CreateInputLabel("Payment Proof");
        proofTitleLabel.Location = new Point(0, 0);

        _browseProofButton.Location = new Point(0, 24);
        _browseProofButton.Click += BrowseProofButton_Click;
        
        _openProofButton.Location = new Point(86, 24);
        _openProofButton.Click += OpenProofButton_Click;

        proofPanel.Controls.Add(proofTitleLabel);
        proofPanel.Controls.Add(_browseProofButton);
        proofPanel.Controls.Add(_openProofButton);
        proofPanel.Controls.Add(_proofPathLabel);
        section.Controls.Add(proofPanel);

        UpdateProofState();
        return section;
    }

    private void UpdateExtensionSummary()
    {
        int extraDays = (NewEndDate - _transaction.EndDate.Date).Days;
        decimal charge = extraDays * _transaction.DailyRate;
        _extraDaysLabel.Text = $"{extraDays} extra day(s)";
        _extensionChargeLabel.Text = $"Total Extension Charge: ₱{charge:N2}";
        _amountPaidInput.Maximum = charge;
    }

    private void UpdateProofState()
    {
        _browseProofButton.Enabled = true;
        _openProofButton.Enabled = _selectedProofPath is not null;
    }

    private void BrowseProofButton_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "Image Files (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
            Title = "Select Payment Proof"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _selectedProofPath = dialog.FileName;
            _proofPathLabel.Text = Path.GetFileName(dialog.FileName);
            UpdateProofState();
        }
    }

    private void OpenProofButton_Click(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_selectedProofPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo(_selectedProofPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBoxHelper.ShowError($"Unable to open file: {ex.Message}");
            }
        }
    }

    private void ExtendButton_Click(object? sender, EventArgs e)
    {
        _validationLabel.Visible = false;

        if (NewEndDate <= _transaction.EndDate.Date)
        {
            ShowValidation("New end date must be later than the current end date.");
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

    private static Label CreateSummaryValueLabel()
    {
        return new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextPrimary,
            Height = 30
        };
    }

    private static ComboBox CreateComboBox()
    {
        return new ComboBox
        {
            Height = 30,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = FontHelper.Regular(10F)
        };
    }

    private static DateTimePicker CreateDatePicker()
    {
        return new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Font = FontHelper.Regular(10F)
        };
    }

    private static NumericUpDown CreateMoneyInput()
    {
        return new NumericUpDown
        {
            DecimalPlaces = 2,
            Maximum = 1000000,
            Increment = 1000,
            ThousandsSeparator = true,
            Font = FontHelper.Regular(10F)
        };
    }

    private static Button CreateSecondaryButton(string text, int width, int height)
    {
        Button button = new()
        {
            Text = text,
            Size = new Size(width, height),
            BackColor = ThemeHelper.Surface,
            ForeColor = ThemeHelper.TextPrimary,
            Font = FontHelper.SemiBold(),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = ThemeHelper.Border;
        return button;
    }
}
