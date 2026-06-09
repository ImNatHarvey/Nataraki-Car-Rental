using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Forms.Offsite;

public sealed class MaintenanceExtendForm : Form
{
    private readonly Transaction _transaction;
    private readonly DateTimePicker _newEndDatePicker = CreateDatePicker();
    private readonly Label _extraDaysLabel = CreateSummaryValueLabel();
    private readonly Label _validationLabel = new();

    public DateTime NewEndDate => _newEndDatePicker.Value.Date;

    public MaintenanceExtendForm(Transaction transaction)
    {
        _transaction = transaction;
        InitializeForm();
    }

    private void InitializeForm()
    {
        Text = "Extend Maintenance";
        Size = new Size(500, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = ThemeHelper.Surface;
        ShowInTaskbar = false;

        Label titleLabel = new()
        {
            Text = "Extend Maintenance",
            AutoSize = true,
            Location = new Point(32, 24),
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Label subtitleLabel = new()
        {
            Text = $"Maintenance: {_transaction.TransactionCode} | Current End: {_transaction.EndDate:MMM d, yyyy}",
            AutoSize = true,
            Location = new Point(34, 60),
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextSecondary
        };

        _validationLabel.AutoSize = false;
        _validationLabel.Location = new Point(34, 88);
        _validationLabel.Size = new Size(432, 24);
        _validationLabel.Font = FontHelper.SemiBold(9F);
        _validationLabel.ForeColor = ThemeHelper.Danger;
        _validationLabel.Visible = false;

        GroupBox section = CreateSection("Extension Details");
        section.Location = new Point(32, 120);
        section.Size = new Size(420, 130);
        
        _newEndDatePicker.Width = 180;
        _newEndDatePicker.MinDate = _transaction.EndDate.AddDays(1);
        _newEndDatePicker.ValueChanged += (_, _) => UpdateExtensionSummary();

        Panel datePanel = new() { Width = 180, Height = 60, Location = new Point(20, 40) };
        Label dateLabel = ControlFactory.CreateInputLabel("New End Date *");
        dateLabel.Location = new Point(0, 0);
        _newEndDatePicker.Location = new Point(0, 24);
        datePanel.Controls.Add(dateLabel);
        datePanel.Controls.Add(_newEndDatePicker);
        section.Controls.Add(datePanel);

        Panel summaryPanel = new() { Width = 180, Height = 60, Location = new Point(220, 40) };
        Label summaryLabel = ControlFactory.CreateInputLabel("Extension Summary");
        summaryLabel.Location = new Point(0, 0);
        _extraDaysLabel.Location = new Point(0, 24);
        _extraDaysLabel.Width = 180;
        summaryPanel.Controls.Add(summaryLabel);
        summaryPanel.Controls.Add(_extraDaysLabel);
        section.Controls.Add(summaryPanel);

        Button cancelButton = CreateSecondaryButton("Cancel", 110, 38);
        cancelButton.Location = new Point(202, 310);
        cancelButton.DialogResult = DialogResult.Cancel;

        Button extendButton = ControlFactory.CreatePrimaryButton("Extend", 130, 38);
        extendButton.Location = new Point(322, 310);
        extendButton.Click += ExtendButton_Click;

        Controls.Add(titleLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(_validationLabel);
        Controls.Add(section);
        Controls.Add(cancelButton);
        Controls.Add(extendButton);

        Click += (_, _) => ActiveControl = null;
        CancelButton = cancelButton;

        _newEndDatePicker.Value = _transaction.EndDate.AddDays(1);
        UpdateExtensionSummary();
    }

    private void UpdateExtensionSummary()
    {
        int extraDays = (NewEndDate - _transaction.EndDate.Date).Days;
        _extraDaysLabel.Text = $"{extraDays} extra day(s)";
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
            Padding = new Padding(8, 20, 8, 8),
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

    private static DateTimePicker CreateDatePicker()
    {
        return new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
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
