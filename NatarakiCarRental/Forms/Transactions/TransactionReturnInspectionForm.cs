using System.Diagnostics;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Forms.Transactions;

public sealed class TransactionReturnInspectionForm : Form
{
    private static readonly string[] BlacklistReasons =
    [
        "Unpaid balance",
        "Vehicle damage history",
        "Late return history",
        "Invalid or suspicious documents",
        "Rude or abusive behavior",
        "Policy violation",
        "Others"
    ];

    private readonly Transaction _transaction;
    private readonly ComboBox _conditionComboBox = CreateComboBox();
    private readonly NumericUpDown _daysLateInput = new() { Minimum = 1, Maximum = 365, Value = 1, Width = 120, Font = FontHelper.Regular(10F) };
    private readonly Label _daysLateLabel = ControlFactory.CreateInputLabel("Days Late *");
    
    private readonly NumericUpDown _additionalChargeInput = CreateMoneyInput();
    private readonly CheckBox _chargePaidCheckBox = new() { Text = "Charge Paid", AutoSize = true, Font = FontHelper.SemiBold(10F), ForeColor = ThemeHelper.Success };
    
    private readonly Button _browseProofButton = CreateSecondaryButton("Browse", 80, 28);
    private readonly Button _openProofButton = CreateSecondaryButton("Open File", 90, 28);
    private readonly Label _proofPathLabel = new() { Text = "No file selected", AutoSize = true, Font = FontHelper.Italic(9F), ForeColor = ThemeHelper.TextSecondary, Location = new Point(174, 30) };
    private string? _selectedProofPath;
    
    private readonly CheckBox _blacklistCustomerCheckBox = new() { Text = "Blacklist Customer", AutoSize = true, Font = FontHelper.SemiBold(10F), ForeColor = ThemeHelper.Danger };
    private readonly ComboBox _blacklistReasonComboBox = CreateComboBox();
    private readonly TextBox _customBlacklistReasonTextBox = ControlFactory.CreateTextBox();
    private readonly Label _validationLabel = new();

    public string ReturnCondition => _conditionComboBox.SelectedItem?.ToString() ?? "Good";
    public int? DaysLate => ReturnCondition == "Late Return" ? (int)_daysLateInput.Value : null;
    public decimal AdditionalCharge => _additionalChargeInput.Value;
    public bool ChargePaid => _chargePaidCheckBox.Checked;
    public string? ReceiptFilePath => _selectedProofPath;
    
    public bool BlacklistCustomer => _blacklistCustomerCheckBox.Checked;
    public string BlacklistReason
    {
        get
        {
            if (!_blacklistCustomerCheckBox.Checked) return string.Empty;
            string selected = _blacklistReasonComboBox.SelectedItem?.ToString() ?? string.Empty;
            return selected == "Others" ? _customBlacklistReasonTextBox.Text.Trim() : selected;
        }
    }

    public TransactionReturnInspectionForm(Transaction transaction)
    {
        _transaction = transaction;
        InitializeForm();
    }

    private void InitializeForm()
    {
        Text = "Return Inspection";
        Size = new Size(580, 780);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = ThemeHelper.Surface;
        ShowInTaskbar = false;

        Label titleLabel = new()
        {
            Text = "Return Inspection",
            AutoSize = true,
            Location = new Point(32, 24),
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Label subtitleLabel = new()
        {
            Text = $"Transaction: {_transaction.TransactionCode} | Daily Rate: ₱{_transaction.DailyRate:N2}",
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
            Size = new Size(500, 550),
            BackColor = ThemeHelper.Surface
        };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(CreateReturnInfoSection(), 0, 0);
        layout.Controls.Add(CreateChargeSection(), 0, 1);
        layout.Controls.Add(CreateBlacklistSection(), 0, 2);
        contentPanel.Controls.Add(layout);

        Button cancelButton = CreateSecondaryButton("Cancel", 110, 38);
        cancelButton.Location = new Point(220, 680);
        cancelButton.DialogResult = DialogResult.Cancel;

        Button completeButton = ControlFactory.CreatePrimaryButton("Complete Transaction", 190, 38);
        completeButton.Location = new Point(342, 680);
        completeButton.Click += CompleteButton_Click;

        Controls.Add(titleLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(_validationLabel);
        Controls.Add(contentPanel);
        Controls.Add(cancelButton);
        Controls.Add(completeButton);

        // Add blank area click handler to remove focus from inputs
        Click += (_, _) => ActiveControl = null;
        CancelButton = cancelButton;

        UpdateConditionState();
    }

    private GroupBox CreateReturnInfoSection()
    {
        GroupBox section = CreateSection("Return Information");
        _conditionComboBox.Items.AddRange(new[] { "Good", "With Damage", "Late Return" });
        _conditionComboBox.SelectedIndex = 0;
        _conditionComboBox.Width = 280;
        _conditionComboBox.SelectedIndexChanged += (_, _) => UpdateConditionState();

        _daysLateInput.ValueChanged += (_, _) => CalculateLatePenalty();

        Panel conditionPanel = new() { Width = 280, Height = 60, Location = new Point(16, 32) };
        Label condLabel = ControlFactory.CreateInputLabel("Condition *");
        condLabel.Location = new Point(0, 0);
        _conditionComboBox.Location = new Point(0, 24);
        conditionPanel.Controls.Add(condLabel);
        conditionPanel.Controls.Add(_conditionComboBox);

        Panel daysLatePanel = new() { Width = 120, Height = 60, Location = new Point(310, 32) };
        _daysLateLabel.Location = new Point(0, 0);
        _daysLateInput.Location = new Point(0, 24);
        daysLatePanel.Controls.Add(_daysLateLabel);
        daysLatePanel.Controls.Add(_daysLateInput);

        section.Controls.Add(conditionPanel);
        section.Controls.Add(daysLatePanel);
        return section;
    }

    private void UpdateConditionState()
    {
        string cond = ReturnCondition;
        bool isLate = cond == "Late Return";
        bool isGood = cond == "Good";

        _daysLateInput.Enabled = isLate;
        _daysLateInput.Visible = isLate;
        _daysLateLabel.Visible = isLate;
        if (!isLate) _daysLateInput.Value = 1;

        if (isGood)
        {
            _additionalChargeInput.Value = 0;
            _additionalChargeInput.Enabled = false;
            _chargePaidCheckBox.Checked = false;
            _chargePaidCheckBox.Enabled = false;
        }
        else if (isLate)
        {
            _additionalChargeInput.Enabled = false; // Penalty is auto-calculated
            CalculateLatePenalty();
        }
        else // With Damage
        {
            _additionalChargeInput.Enabled = true;
            _additionalChargeInput.Value = 0;
        }

        UpdateChargeState();
    }

    private void CalculateLatePenalty()
    {
        if (ReturnCondition == "Late Return")
        {
            _additionalChargeInput.Value = _transaction.DailyRate * _daysLateInput.Value;
        }
    }

    private GroupBox CreateChargeSection()
    {
        GroupBox section = CreateSection("Charges");
        
        _additionalChargeInput.Width = 200;
        _additionalChargeInput.ValueChanged += (_, _) => UpdateChargeState();

        Panel chargePanel = new() { Width = 200, Height = 60, Location = new Point(16, 32) };
        Label chargeLabel = ControlFactory.CreateInputLabel("Additional Charge (₱)");
        chargeLabel.Location = new Point(0, 0);
        _additionalChargeInput.Location = new Point(0, 24);
        chargePanel.Controls.Add(chargeLabel);
        chargePanel.Controls.Add(_additionalChargeInput);
        section.Controls.Add(chargePanel);

        _chargePaidCheckBox.Location = new Point(236, 56);
        _chargePaidCheckBox.Enabled = false;
        _chargePaidCheckBox.CheckedChanged += (_, _) => UpdateProofState();
        section.Controls.Add(_chargePaidCheckBox);

        Panel proofPanel = new() { Width = 460, Height = 70, Location = new Point(16, 110) };
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

        Label helperLabel = new()
        {
            Text = "Late fee: Daily rate × days late.",
            AutoSize = true,
            Location = new Point(18, 184),
            Font = FontHelper.Italic(8.5F),
            ForeColor = ThemeHelper.TextSecondary
        };
        section.Controls.Add(helperLabel);

        return section;
    }

    private void UpdateChargeState()
    {
        bool hasCharge = _additionalChargeInput.Value > 0;
        _chargePaidCheckBox.Enabled = hasCharge;
        if (!hasCharge)
        {
            _chargePaidCheckBox.Checked = false;
        }
        UpdateProofState();
    }

    private void UpdateProofState()
    {
        bool hasCharge = _additionalChargeInput.Value > 0;
        
        _browseProofButton.Enabled = hasCharge;
        
        bool hasFile = !string.IsNullOrWhiteSpace(_selectedProofPath);
        _openProofButton.Enabled = hasCharge && hasFile;
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

    private GroupBox CreateBlacklistSection()
    {
        GroupBox section = CreateSection("Blacklist Option");

        _blacklistCustomerCheckBox.Location = new Point(16, 28);
        _blacklistCustomerCheckBox.CheckedChanged += (_, _) => ToggleBlacklistFields();

        _blacklistReasonComboBox.Items.AddRange(BlacklistReasons.Cast<object>().ToArray());
        _blacklistReasonComboBox.Width = 460;
        _blacklistReasonComboBox.Enabled = false;
        _blacklistReasonComboBox.SelectedIndexChanged += (_, _) => ToggleCustomReason();

        _customBlacklistReasonTextBox.PlaceholderText = "Specify custom reason...";
        _customBlacklistReasonTextBox.Width = 460;
        _customBlacklistReasonTextBox.Enabled = false;
        _customBlacklistReasonTextBox.Visible = false;

        Panel reasonPanel = new() { Width = 460, Height = 60, Location = new Point(16, 60) };
        Label reasonLabel = ControlFactory.CreateInputLabel("Reason");
        reasonLabel.Location = new Point(0, 0);
        _blacklistReasonComboBox.Location = new Point(0, 24);
        reasonPanel.Controls.Add(reasonLabel);
        reasonPanel.Controls.Add(_blacklistReasonComboBox);

        Panel customReasonPanel = new() { Width = 460, Height = 60, Location = new Point(16, 126) };
        Label customLabel = ControlFactory.CreateInputLabel("Custom Reason *");
        customLabel.Location = new Point(0, 0);
        _customBlacklistReasonTextBox.Location = new Point(0, 24);
        customReasonPanel.Controls.Add(customLabel);
        customReasonPanel.Controls.Add(_customBlacklistReasonTextBox);

        section.Controls.Add(_blacklistCustomerCheckBox);
        section.Controls.Add(reasonPanel);
        section.Controls.Add(customReasonPanel);

        return section;
    }

    private void ToggleBlacklistFields()
    {
        bool enabled = _blacklistCustomerCheckBox.Checked;
        _blacklistReasonComboBox.Enabled = enabled;
        if (!enabled)
        {
            _blacklistReasonComboBox.SelectedIndex = -1;
            _customBlacklistReasonTextBox.Clear();
            _customBlacklistReasonTextBox.Visible = false;
        }
    }

    private void ToggleCustomReason()
    {
        bool isOthers = _blacklistReasonComboBox.SelectedItem?.ToString() == "Others";
        _customBlacklistReasonTextBox.Visible = isOthers;
        _customBlacklistReasonTextBox.Enabled = isOthers;
    }

    private void CompleteButton_Click(object? sender, EventArgs e)
    {
        _validationLabel.Visible = false;

        if (_additionalChargeInput.Value > 0 && !_chargePaidCheckBox.Checked)
        {
            ShowValidation("Additional charges must be settled before completing this transaction.");
            return;
        }

        if (_blacklistCustomerCheckBox.Checked)
        {
            if (_blacklistReasonComboBox.SelectedIndex < 0)
            {
                ShowValidation("Please select a reason for blacklisting.");
                return;
            }

            if (_blacklistReasonComboBox.SelectedItem?.ToString() == "Others" && string.IsNullOrWhiteSpace(_customBlacklistReasonTextBox.Text))
            {
                ShowValidation("Custom blacklist reason is required.");
                return;
            }
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
        return new ComboBox
        {
            Height = 30,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = FontHelper.Regular(10F)
        };
    }

    private static NumericUpDown CreateMoneyInput()
    {
        return new NumericUpDown
        {
            DecimalPlaces = 2,
            Maximum = 1000000,
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
