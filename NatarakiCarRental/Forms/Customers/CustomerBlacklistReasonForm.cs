using NatarakiCarRental.Helpers;

namespace NatarakiCarRental.Forms.Customers;

public sealed class CustomerBlacklistReasonForm : Form
{
    private static readonly string[] ReasonOptions =
    [
        "Unpaid balance",
        "Vehicle damage history",
        "Late return history",
        "Invalid or suspicious documents",
        "Rude or abusive behavior",
        "Policy violation",
        "Others"
    ];

    private readonly ComboBox _reasonComboBox = new();
    private readonly TextBox _customReasonTextBox = ControlFactory.CreateTextBox(330);
    private readonly Label _customReasonLabel = ControlFactory.CreateInputLabel("Custom Reason *");
    private readonly Label _validationLabel = new();
    private Button _confirmButton = null!;
    private Button _cancelButton = null!;

    public CustomerBlacklistReasonForm()
    {
        InitializeForm();
    }

    public string BlacklistReason { get; private set; } = string.Empty;

    private void InitializeForm()
    {
        Text = "Blacklist Customer";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(460, 280);
        BackColor = ThemeHelper.Surface;
        Font = FontHelper.Regular();
        ShowInTaskbar = false;

        Label titleLabel = new()
        {
            Text = "Blacklist Customer",
            AutoSize = false,
            Location = new Point(28, 22),
            Size = new Size(260, 30),
            Font = FontHelper.Title(16F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Label subtitleLabel = new()
        {
            Text = "Select a reason before flagging this customer.",
            AutoSize = false,
            Location = new Point(30, 56),
            Size = new Size(400, 24),
            Font = FontHelper.Regular(9.5F),
            ForeColor = ThemeHelper.TextSecondary
        };

        _validationLabel.AutoSize = false;
        _validationLabel.Location = new Point(30, 82);
        _validationLabel.Size = new Size(400, 22);
        _validationLabel.Font = FontHelper.SemiBold(9F);
        _validationLabel.ForeColor = ThemeHelper.Danger;
        _validationLabel.Visible = false;

        Label reasonLabel = ControlFactory.CreateInputLabel("Reason *");
        reasonLabel.Location = new Point(30, 112);

        _reasonComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _reasonComboBox.Font = FontHelper.Regular(10F);
        _reasonComboBox.ForeColor = ThemeHelper.TextPrimary;
        _reasonComboBox.Size = new Size(400, 30);
        _reasonComboBox.Location = new Point(30, 136);
        _reasonComboBox.Items.AddRange(ReasonOptions.Cast<object>().ToArray());
        _reasonComboBox.SelectedIndexChanged += (_, _) => UpdateCustomReasonState();

        _customReasonLabel.Location = new Point(30, 174);
        _customReasonLabel.Visible = false;

        _customReasonTextBox.Location = new Point(30, 198);
        _customReasonTextBox.Width = 400;
        _customReasonTextBox.Enabled = false;
        _customReasonTextBox.Visible = false;

        _confirmButton = ControlFactory.CreatePrimaryButton("Confirm", 110, 36);
        _confirmButton.BackColor = ThemeHelper.Danger;
        _confirmButton.FlatAppearance.MouseOverBackColor = ThemeHelper.Danger;
        _confirmButton.Location = new Point(320, 226);
        _confirmButton.Click += ConfirmButton_Click;

        _cancelButton = CreateSecondaryButton("Cancel", 110, 36);
        _cancelButton.Location = new Point(198, 226);
        _cancelButton.DialogResult = DialogResult.Cancel;

        Controls.Add(titleLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(_validationLabel);
        Controls.Add(reasonLabel);
        Controls.Add(_reasonComboBox);
        Controls.Add(_customReasonLabel);
        Controls.Add(_customReasonTextBox);
        Controls.Add(_cancelButton);
        Controls.Add(_confirmButton);

        AcceptButton = _confirmButton;
        CancelButton = _cancelButton;
    }

    private void UpdateCustomReasonState()
    {
        bool isOther = _reasonComboBox.SelectedItem?.ToString() == "Others";
        _customReasonTextBox.Enabled = isOther;
        _customReasonTextBox.Visible = isOther;
        _customReasonLabel.Visible = isOther;

        if (isOther)
        {
            ClientSize = new Size(460, 340);
            _cancelButton.Top = 286;
            _confirmButton.Top = 286;
        }
        else
        {
            ClientSize = new Size(460, 280);
            _cancelButton.Top = 226;
            _confirmButton.Top = 226;
            _customReasonTextBox.Clear();
        }
    }

    private void ConfirmButton_Click(object? sender, EventArgs e)
    {
        string? selectedReason = _reasonComboBox.SelectedItem?.ToString();

        if (string.IsNullOrWhiteSpace(selectedReason))
        {
            ShowValidation("Reason is required.");
            return;
        }

        if (selectedReason == "Others")
        {
            string customReason = _customReasonTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(customReason))
            {
                ShowValidation("Custom reason is required.");
                return;
            }

            BlacklistReason = customReason;
        }
        else
        {
            BlacklistReason = selectedReason;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void ShowValidation(string message)
    {
        _validationLabel.Text = message;
        _validationLabel.Visible = true;
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
