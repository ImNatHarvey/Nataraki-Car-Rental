using FluentValidation;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using System.Diagnostics;
using FleetScheduleModel = NatarakiCarRental.Models.FleetSchedule;

namespace NatarakiCarRental.Forms.Offsite;

public sealed class OffsiteRecordDetailsForm : Form
{
    private const int FormWidth = 1060;
    private const int AddFormHeight = 760;
    private const int DetailFormHeight = 740;
    private const int ViewFormHeight = 890;
    private const int InputWidth = 360;
    private const int WideInputWidth = 900;
    private const int InputHeight = 28;

    private enum FormMode { Add, Edit, View, Complete }

    private readonly OffsiteService _offsiteService;
    private readonly CarService _carService;
    private readonly FleetScheduleService _fleetScheduleService;
    private readonly FormMode _mode;
    private readonly int? _recordId;
    private readonly int _currentUserId;
    private OffsiteRecord? _record;
    private string? _selectedProofPath;

    private readonly TabControl _addTabs = new();
    private readonly TabPage _scheduleTab = new() { Text = "Create from Schedule" };
    private readonly TabPage _manualTab = new() { Text = "Manual Entry" };

    private readonly ComboBox _scheduleComboBox = CreateComboBox(WideInputWidth);
    private readonly Label _summaryCarLabel = CreateValueLabel();
    private readonly Label _summaryDateLabel = CreateValueLabel();
    private readonly Label _summaryStatusLabel = CreateValueLabel();
    private readonly ComboBox _scheduleTypeComboBox = CreateComboBox();
    private readonly TextBox _scheduleLocationTextBox = ControlFactory.CreateTextBox(InputWidth);
    private readonly TextBox _scheduleContactPersonTextBox = ControlFactory.CreateTextBox(InputWidth);
    private readonly TextBox _scheduleContactNumberTextBox = ControlFactory.CreateTextBox(InputWidth);
    private readonly NumericUpDown _scheduleAmountPaidInput = CreateMoneyInput();
    private readonly Label _scheduleProofPathLabel = CreatePathLabel();
    private readonly Button _scheduleBrowseProofButton = CreateSecondaryButton("Browse", 90, InputHeight);
    private readonly Button _scheduleOpenProofButton = CreateSecondaryButton("Open File", 90, InputHeight);

    private readonly ComboBox _carComboBox = CreateComboBox(WideInputWidth);
    private readonly ComboBox _typeComboBox = CreateComboBox();
    private readonly TextBox _locationTextBox = ControlFactory.CreateTextBox(InputWidth);
    private readonly TextBox _contactPersonTextBox = ControlFactory.CreateTextBox(InputWidth);
    private readonly TextBox _contactNumberTextBox = ControlFactory.CreateTextBox(InputWidth);
    private readonly DateTimePicker _startDatePicker = CreateDatePicker();
    private readonly DateTimePicker _expectedReturnPicker = CreateDatePicker();
    private readonly NumericUpDown _amountPaidInput = CreateMoneyInput();
    private readonly NumericUpDown _actualCostInput = CreateMoneyInput();
    private readonly DateTimePicker _completedDatePicker = CreateDatePicker();
    private readonly Label _proofPathLabel = CreatePathLabel();
    private readonly Button _browseProofButton = CreateSecondaryButton("Browse", 90, InputHeight);
    private readonly Button _openProofButton = CreateSecondaryButton("Open File", 90, InputHeight);
    private readonly Label _workResultLabel = CreateValueLabel();
    private readonly Label _followUpRequiredLabel = CreateValueLabel();
    private readonly Label _followUpReasonLabel = CreateValueLabel();
    private readonly Label _auditCompletedDateLabel = CreateValueLabel();
    private readonly Label _auditAmountPaidLabel = CreateValueLabel();
    private readonly Label _auditProofLabel = CreateValueLabel();
    private readonly Label _completedByLabel = CreateValueLabel();
    private readonly Button _auditBrowseProofButton = CreateSecondaryButton("Browse", 90, InputHeight);
    private readonly Button _auditOpenProofButton = CreateSecondaryButton("Open File", 90, InputHeight);

    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    public OffsiteRecordDetailsForm(int currentUserId, int? recordId = null, bool isViewOnly = false, bool isCompletion = false)
    {
        _currentUserId = currentUserId;
        _offsiteService = new OffsiteService(currentUserId);
        _carService = new CarService(currentUserId);
        _fleetScheduleService = new FleetScheduleService(currentUserId);
        _recordId = recordId;

        if (isCompletion) _mode = FormMode.Complete;
        else if (isViewOnly) _mode = FormMode.View;
        else if (recordId.HasValue) _mode = FormMode.Edit;
        else _mode = FormMode.Add;

        _saveButton = ControlFactory.CreatePrimaryButton(GetSaveButtonText(), 180, 38);
        _cancelButton = CreateSecondaryButton(_mode == FormMode.View ? "Close" : "Cancel", 118, 38);

        InitializeComponent();
        SetupEvents();
    }

    private string GetSaveButtonText() => _mode switch
    {
        FormMode.Add => "Add Offsite Record",
        FormMode.Edit => "Save Changes",
        FormMode.Complete => "Complete Offsite",
        _ => "Close"
    };

    private void InitializeComponent()
    {
        Text = _mode switch
        {
            FormMode.Add => "Add Offsite Record",
            FormMode.Edit => "Edit Offsite Record",
            FormMode.View => "View Offsite Record",
            FormMode.Complete => "Complete Offsite Record",
            _ => "Offsite Record"
        };
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(FormWidth, _mode == FormMode.Add ? AddFormHeight : (_mode == FormMode.View ? ViewFormHeight : DetailFormHeight));
        BackColor = ThemeHelper.Surface;
        Font = FontHelper.Regular();
        ShowInTaskbar = false;

        Controls.Add(new Label
        {
            Text = Text,
            AutoSize = false,
            Location = new Point(32, 24),
            Size = new Size(360, 34),
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary
        });

        Controls.Add(new Label
        {
            Text = _mode switch
            {
                FormMode.Edit => "Update operational status and maintenance details.",
                FormMode.View => "Review recorded maintenance or operational offsite details.",
                FormMode.Complete => "Mark as finished and record final costs.",
                _ => "Record why a vehicle is temporarily unavailable."
            },
            AutoSize = false,
            Location = new Point(34, 58),
            Size = new Size(720, 24),
            Font = FontHelper.Regular(9.5F),
            ForeColor = ThemeHelper.TextSecondary
        });

        _addTabs.Location = new Point(32, 96);
        _addTabs.Size = new Size(996, _mode == FormMode.Add ? 584 : (_mode == FormMode.View ? 710 : 560));
        _addTabs.Font = FontHelper.SemiBold(9F);

        if (_mode == FormMode.Add)
        {
            _scheduleTab.BackColor = ThemeHelper.Surface;
            _manualTab.BackColor = ThemeHelper.Surface;
            _scheduleTab.Controls.Add(CreateScheduleTabContent());
            _manualTab.Controls.Add(CreateManualTabContent());
            _addTabs.TabPages.Add(_scheduleTab);
            _addTabs.TabPages.Add(_manualTab);
        }
        else
        {
            TabPage detailsTab = new() { Text = "Offsite Details", BackColor = ThemeHelper.Surface };
            detailsTab.Controls.Add(CreateManualTabContent());
            _addTabs.TabPages.Add(detailsTab);
        }

        if (_mode == FormMode.View)
        {
            _cancelButton.Location = new Point(ClientSize.Width - 32 - _cancelButton.Width, ClientSize.Height - 60);
        }
        else
        {
            _cancelButton.Location = new Point(ClientSize.Width - 32 - _saveButton.Width - 14 - _cancelButton.Width, ClientSize.Height - 60);
            _saveButton.Location = new Point(ClientSize.Width - 32 - _saveButton.Width, ClientSize.Height - 60);
            Controls.Add(_saveButton);
            AcceptButton = _saveButton;
        }

        Controls.Add(_addTabs);
        Controls.Add(_cancelButton);
        CancelButton = _cancelButton;
        Click += (_, _) => ActiveControl = null;

        ConfigureControls();
    }

    private Control CreateScheduleTabContent()
    {
        Panel layout = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.Surface };
        const int left = 14;
        const int width = 948;

        GroupBox source = CreateSingleInputSection("Source", "Eligible Maintenance Schedule *", _scheduleComboBox, new Point(left, 14), new Size(width, 96), wideInput: true);
        GroupBox summary = CreateScheduleSummarySection(new Point(left, 124), new Size(width, 92));
        GroupBox details = CreateDetailsSection(
            "Offsite Details",
            new Point(left, 230),
            new Size(width, 205),
            (_scheduleTypeComboBox, _scheduleContactPersonTextBox, null),
            (_scheduleLocationTextBox, _scheduleContactNumberTextBox, null));
        GroupBox payment = CreatePaymentSection(
            new Point(left, 449),
            new Size(width, 128),
            _scheduleAmountPaidInput,
            _scheduleProofPathLabel,
            _scheduleBrowseProofButton,
            _scheduleOpenProofButton,
            includeCompletionFields: false);

        layout.Controls.Add(source);
        layout.Controls.Add(summary);
        layout.Controls.Add(details);
        layout.Controls.Add(payment);
        return layout;
    }

    private Control CreateManualTabContent()
    {
        bool includeCompletionFields = _mode is FormMode.Complete or FormMode.View || (_record?.Status == "Completed");
        Panel layout = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.Surface };
        const int left = 14;
        const int width = 948;

        GroupBox vehicle = CreateSingleInputSection("Vehicle Information", "Select Car *", _carComboBox, new Point(left, 14), new Size(width, 96), wideInput: false);
        GroupBox details = CreateDetailsSection(
            "Offsite Details",
            new Point(left, 124),
            new Size(width, 205),
            (_typeComboBox, _contactPersonTextBox, _startDatePicker),
            (_locationTextBox, _contactNumberTextBox, _expectedReturnPicker));
        GroupBox payment = CreatePaymentSection(
            new Point(left, 343),
            new Size(width, includeCompletionFields ? 160 : 128),
            _amountPaidInput,
            _proofPathLabel,
            _browseProofButton,
            _openProofButton,
            includeCompletionFields);

        layout.Controls.Add(vehicle);
        layout.Controls.Add(details);
        layout.Controls.Add(payment);

        if (_mode == FormMode.View)
        {
            layout.Controls.Add(CreateAuditSection(new Point(left, 517), new Size(width, 176)));
        }

        return layout;
    }

    private Control CreatePaymentLayout(NumericUpDown costInput, Label pathLabel, Button browseButton, Button openButton, bool includeCompletionFields)
    {
        TableLayoutPanel layout = CreateGrid(2, includeCompletionFields ? 2 : 1, includeCompletionFields ? 62 : 70);
        layout.Controls.Add(CreateInputPanel("Amount Paid (₱)", costInput), 0, 0);
        layout.Controls.Add(CreateProofPickerPanel("Proof / Receipt", pathLabel, browseButton, openButton), 1, 0);

        if (includeCompletionFields)
        {
            layout.Controls.Add(CreateInputPanel("Completed Date *", _completedDatePicker), 0, 1);
            layout.Controls.Add(CreateInputPanel("Amount Paid (₱) *", _actualCostInput), 1, 1);
        }

        return layout;
    }

    private static GroupBox CreateBaseSection(string title, Point location, Size size)
    {
        return new GroupBox
        {
            Text = title,
            Location = location,
            Size = size,
            Font = FontHelper.SemiBold(9.5F),
            ForeColor = ThemeHelper.TextPrimary,
            BackColor = ThemeHelper.Surface
        };
    }

    private static GroupBox CreateSingleInputSection(string title, string labelText, Control input, Point location, Size size, bool wideInput)
    {
        GroupBox group = CreateBaseSection(title, location, size);
        AddPositionedInput(group, labelText, input, new Point(24, 30), wideInput ? 860 : InputWidth);
        return group;
    }

    private GroupBox CreateScheduleSummarySection(Point location, Size size)
    {
        GroupBox group = CreateBaseSection("Schedule Summary", location, size);
        AddPositionedDisplay(group, "Vehicle", _summaryCarLabel, new Point(24, 30), 280);
        AddPositionedDisplay(group, "Date Range", _summaryDateLabel, new Point(330, 30), 280);
        AddPositionedDisplay(group, "Status", _summaryStatusLabel, new Point(636, 30), 220);
        return group;
    }

    private static GroupBox CreateDetailsSection(
        string title,
        Point location,
        Size size,
        (Control row1, Control row2, Control? row3) column1,
        (Control row1, Control row2, Control? row3) column2)
    {
        GroupBox group = CreateBaseSection(title, location, size);
        AddPositionedInput(group, "Offsite Type *", column1.row1, new Point(24, 30), InputWidth);
        AddPositionedInput(group, "Location Name", column2.row1, new Point(486, 30), InputWidth);
        AddPositionedInput(group, "Contact Person", column1.row2, new Point(24, 88), InputWidth);
        AddPositionedInput(group, "Contact Number", column2.row2, new Point(486, 88), InputWidth);

        if (column1.row3 is not null)
        {
            AddPositionedInput(group, "Start Date *", column1.row3, new Point(24, 146), InputWidth);
        }

        if (column2.row3 is not null)
        {
            AddPositionedInput(group, "Expected Return Date *", column2.row3, new Point(486, 146), InputWidth);
        }

        return group;
    }

    private GroupBox CreatePaymentSection(
        Point location,
        Size size,
        NumericUpDown costInput,
        Label pathLabel,
        Button browseButton,
        Button openButton,
        bool includeCompletionFields)
    {
        GroupBox group = CreateBaseSection("Payment Information", location, size);
        AddPositionedInput(group, "Amount Paid (₱)", costInput, new Point(24, 30), InputWidth);
        AddPositionedProof(group, "Proof / Receipt", pathLabel, browseButton, openButton, new Point(486, 30));

        if (includeCompletionFields)
        {
            AddPositionedInput(group, "Completed Date *", _completedDatePicker, new Point(24, 88), InputWidth);
            AddPositionedInput(group, "Amount Paid (₱) *", _actualCostInput, new Point(486, 88), InputWidth);
        }

        return group;
    }

    private GroupBox CreateAuditSection(Point location, Size size)
    {
        GroupBox group = CreateBaseSection("Completion Audit", location, size);
        AddPositionedDisplay(group, "Completed Date", _auditCompletedDateLabel, new Point(24, 30), 170);
        AddPositionedDisplay(group, "Work Result", _workResultLabel, new Point(250, 30), 170);
        AddPositionedDisplay(group, "Amount Paid", _auditAmountPaidLabel, new Point(476, 30), 170);
        AddPositionedDisplay(group, "Completed By", _completedByLabel, new Point(702, 30), 180);
        AddPositionedDisplay(group, "Follow-up Required", _followUpRequiredLabel, new Point(24, 84), 170);
        AddPositionedDisplay(group, "Follow-up Reason", _followUpReasonLabel, new Point(250, 84), 210);
        AddAuditProof(group, new Point(476, 84));
        return group;
    }

    private void AddAuditProof(Control parent, Point labelLocation)
    {
        Label label = new()
        {
            Text = "Proof / Receipt",
            Location = labelLocation,
            Size = new Size(180, 18),
            Font = FontHelper.SemiBold(9F),
            ForeColor = ThemeHelper.TextSecondary
        };
        _auditBrowseProofButton.Location = new Point(labelLocation.X, labelLocation.Y + 22);
        _auditBrowseProofButton.Enabled = false;
        _auditOpenProofButton.Location = new Point(labelLocation.X + 98, labelLocation.Y + 22);
        _auditProofLabel.Location = new Point(labelLocation.X + 200, labelLocation.Y + 26);
        _auditProofLabel.Size = new Size(250, 22);
        _auditOpenProofButton.Click -= AuditOpenProofButton_Click;
        _auditOpenProofButton.Click += AuditOpenProofButton_Click;
        parent.Controls.Add(label);
        parent.Controls.Add(_auditBrowseProofButton);
        parent.Controls.Add(_auditOpenProofButton);
        parent.Controls.Add(_auditProofLabel);
    }

    private static void AddPositionedInput(Control parent, string labelText, Control input, Point labelLocation, int width)
    {
        Label label = ControlFactory.CreateInputLabel(labelText);
        label.Location = labelLocation;
        input.Location = new Point(labelLocation.X, labelLocation.Y + 22);
        input.Size = new Size(width, InputHeight);
        input.Font = FontHelper.Regular(10F);
        parent.Controls.Add(label);
        parent.Controls.Add(input);
    }

    private static void AddPositionedDisplay(Control parent, string labelText, Label valueLabel, Point labelLocation, int width)
    {
        Label label = new()
        {
            Text = labelText,
            Location = labelLocation,
            Size = new Size(width, 18),
            Font = FontHelper.SemiBold(9F),
            ForeColor = ThemeHelper.TextSecondary
        };
        valueLabel.Location = new Point(labelLocation.X, labelLocation.Y + 22);
        valueLabel.Size = new Size(width, 24);
        parent.Controls.Add(label);
        parent.Controls.Add(valueLabel);
    }

    private static void AddPositionedProof(Control parent, string labelText, Label pathLabel, Button browseButton, Button openButton, Point labelLocation)
    {
        Label label = ControlFactory.CreateInputLabel(labelText);
        label.Location = labelLocation;
        browseButton.Location = new Point(labelLocation.X, labelLocation.Y + 22);
        openButton.Location = new Point(labelLocation.X + 98, labelLocation.Y + 22);
        pathLabel.Location = new Point(labelLocation.X + 200, labelLocation.Y + 26);
        pathLabel.Size = new Size(220, 20);
        parent.Controls.Add(label);
        parent.Controls.Add(browseButton);
        parent.Controls.Add(openButton);
        parent.Controls.Add(pathLabel);
    }

    private void ConfigureControls()
    {
        _typeComboBox.Items.AddRange(["Maintenance", "Repair", "Cleaning"]);
        _scheduleTypeComboBox.Items.AddRange(["Maintenance", "Repair", "Cleaning"]);

        foreach (Control input in GetInputControls())
        {
            input.Font = FontHelper.Regular(10F);
            input.ForeColor = ThemeHelper.TextPrimary;
            input.Height = InputHeight;
        }

        _scheduleTypeComboBox.Width = InputWidth;
        _typeComboBox.Width = InputWidth;
        _locationTextBox.Width = InputWidth;
        _contactPersonTextBox.Width = InputWidth;
        _contactNumberTextBox.Width = InputWidth;
        _scheduleLocationTextBox.Width = InputWidth;
        _scheduleContactPersonTextBox.Width = InputWidth;
        _scheduleContactNumberTextBox.Width = InputWidth;

        ConfigureMoneyInput(_amountPaidInput);
        ConfigureMoneyInput(_scheduleAmountPaidInput);
        ConfigureMoneyInput(_actualCostInput);

        _scheduleOpenProofButton.Enabled = false;
        _openProofButton.Enabled = false;

        if (_mode != FormMode.Add)
        {
            _carComboBox.Enabled = false;
            _startDatePicker.Enabled = false;
            if (_mode == FormMode.View)
            {
                _typeComboBox.Enabled = false;
                _locationTextBox.ReadOnly = true;
                _contactPersonTextBox.ReadOnly = true;
                _contactNumberTextBox.ReadOnly = true;
                _expectedReturnPicker.Enabled = false;
                _completedDatePicker.Enabled = false;
                _amountPaidInput.Enabled = false;
                _actualCostInput.Enabled = false;
                _browseProofButton.Visible = false;
            }
        }
    }

    private static void ConfigureMoneyInput(NumericUpDown input)
    {
        input.Minimum = 0;
        input.Maximum = 1000000;
        input.DecimalPlaces = 2;
        input.Increment = 1000;
        input.ThousandsSeparator = true;
        input.Width = InputWidth;
        input.Height = InputHeight;
        input.Font = FontHelper.Regular(10F);
    }

    private IEnumerable<Control> GetInputControls()
    {
        yield return _scheduleComboBox;
        yield return _scheduleTypeComboBox;
        yield return _scheduleLocationTextBox;
        yield return _scheduleContactPersonTextBox;
        yield return _scheduleContactNumberTextBox;
        yield return _carComboBox;
        yield return _typeComboBox;
        yield return _locationTextBox;
        yield return _contactPersonTextBox;
        yield return _contactNumberTextBox;
        yield return _startDatePicker;
        yield return _expectedReturnPicker;
        yield return _completedDatePicker;
    }

    private void SetupEvents()
    {
        Load += async (_, _) => await LoadDataAsync();
        _saveButton.Click += async (_, _) => await SaveAsync();
        _cancelButton.Click += (_, _) => Close();
        _startDatePicker.ValueChanged += (_, _) => UpdateExpectedReturnMinimum();
        _browseProofButton.Click += (_, _) => BrowseProof();
        _scheduleBrowseProofButton.Click += (_, _) => BrowseProof();
        _openProofButton.Click += (_, _) => OpenProof();
        _scheduleOpenProofButton.Click += (_, _) => OpenProof();
        _scheduleComboBox.SelectedIndexChanged += (_, _) => UpdateScheduleSummary();
    }

    private void AuditOpenProofButton_Click(object? sender, EventArgs e)
    {
        OpenProof();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            IReadOnlyList<Car> cars = await _carService.GetActiveCarsAsync();
            _carComboBox.Items.Clear();
            _carComboBox.Items.Add("Select a car");
            foreach (Car car in cars)
            {
                _carComboBox.Items.Add(new CarOption(car.CarId, car.CarName, car.PlateNumber));
            }
            _carComboBox.SelectedIndex = 0;

            if (_mode == FormMode.Add)
            {
                IReadOnlyList<FleetScheduleModel> eligible = await _fleetScheduleService.GetMaintenanceSchedulesAsync();

                _scheduleComboBox.Items.Clear();
                _scheduleComboBox.Items.Add("Select an eligible maintenance schedule");
                foreach (FleetScheduleModel schedule in eligible)
                {
                    _scheduleComboBox.Items.Add(new ScheduleOption(
                        schedule.ScheduleId,
                        schedule.Title,
                        schedule.CarName,
                        schedule.PlateNumber,
                        schedule.StartDate,
                        schedule.EndDate,
                        schedule.Status,
                        schedule.CarId));
                }
                _scheduleComboBox.SelectedIndex = 0;
            }

            if (_recordId.HasValue)
            {
                _record = await _offsiteService.GetByIdAsync(_recordId.Value);
                if (_record is not null)
                {
                    LoadRecord(_record);
                }
            }

            UpdateExpectedReturnMinimum();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Failed to load data: {exception.Message}");
        }
    }

    private void LoadRecord(OffsiteRecord record)
    {
        foreach (object item in _carComboBox.Items)
        {
            if (item is CarOption option && option.CarId == record.CarId)
            {
                _carComboBox.SelectedItem = item;
                break;
            }
        }

        _typeComboBox.SelectedItem = record.OffsiteType;
        _locationTextBox.Text = record.LocationName;
        _contactPersonTextBox.Text = record.ContactPerson;
        _contactNumberTextBox.Text = record.ContactNumber;
        _startDatePicker.Value = record.StartDate;
        _expectedReturnPicker.Value = record.ExpectedReturnDate ?? record.StartDate;
        _amountPaidInput.Value = record.ActualCost;

        if (!string.IsNullOrWhiteSpace(record.ProofFilePath))
        {
            _proofPathLabel.Text = Path.GetFileName(record.ProofFilePath);
            _proofPathLabel.ForeColor = ThemeHelper.Primary;
            _openProofButton.Enabled = true;
            _auditProofLabel.Text = Path.GetFileName(record.ProofFilePath);
            _auditProofLabel.ForeColor = ThemeHelper.Primary;
            _auditOpenProofButton.Enabled = true;
        }
        else
        {
            _auditProofLabel.Text = "No file selected";
            _auditOpenProofButton.Enabled = false;
        }

        if (record.CompletedDate.HasValue)
        {
            _completedDatePicker.Value = record.CompletedDate.Value;
        }
        _actualCostInput.Value = record.ActualCost;
        _auditCompletedDateLabel.Text = record.CompletedDate?.ToString("MMM d, yyyy") ?? "-";
        _auditAmountPaidLabel.Text = $"₱{record.ActualCost:N2}";
        _completedByLabel.Text = record.CompletedByUserId.HasValue ? $"User #{record.CompletedByUserId.Value}" : "-";
        _workResultLabel.Text = string.IsNullOrWhiteSpace(record.WorkResult) ? "-" : record.WorkResult;
        _followUpRequiredLabel.Text = record.FollowUpRequired ? "Yes" : "No";
        _followUpReasonLabel.Text = string.IsNullOrWhiteSpace(record.FollowUpReason) ? "-" : record.FollowUpReason;
    }

    private void UpdateScheduleSummary()
    {
        if (_scheduleComboBox.SelectedItem is not ScheduleOption option)
        {
            _summaryCarLabel.Text = "-";
            _summaryDateLabel.Text = "-";
            _summaryStatusLabel.Text = "-";
            return;
        }

        _summaryCarLabel.Text = $"{option.CarName} ({option.PlateNumber})";
        _summaryDateLabel.Text = $"{option.Start:MMM d, yyyy} - {option.End:MMM d, yyyy}";
        _summaryStatusLabel.Text = option.Status;
        if (_scheduleTypeComboBox.SelectedIndex < 0)
        {
            _scheduleTypeComboBox.SelectedItem = "Maintenance";
        }
    }

    private void UpdateExpectedReturnMinimum()
    {
        DateTime startDate = _startDatePicker.Value.Date;
        _expectedReturnPicker.MinDate = DateTimePicker.MinimumDateTime;
        if (_expectedReturnPicker.Value.Date < startDate)
        {
            _expectedReturnPicker.Value = startDate;
        }
        _expectedReturnPicker.MinDate = startDate;
    }

    private void BrowseProof()
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "Image/PDF Files|*.jpg;*.jpeg;*.png;*.pdf",
            Title = "Select proof or receipt",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _selectedProofPath = dialog.FileName;
        string fileName = Path.GetFileName(dialog.FileName);
        SetProofLabel(_proofPathLabel, fileName);
        SetProofLabel(_scheduleProofPathLabel, fileName);
        _openProofButton.Enabled = true;
        _scheduleOpenProofButton.Enabled = true;
    }

    private static void SetProofLabel(Label label, string text)
    {
        label.Text = text;
        label.ForeColor = ThemeHelper.Primary;
    }

    private void OpenProof()
    {
        string? path = !string.IsNullOrWhiteSpace(_selectedProofPath) && File.Exists(_selectedProofPath)
            ? _selectedProofPath
            : UploadPathHelper.ResolveOffsiteProofPath(_record?.ProofFilePath);

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBoxHelper.ShowWarning("The proof file could not be found.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Could not open file: {exception.Message}");
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            if (_mode == FormMode.Add)
            {
                await _offsiteService.CreateAsync(BuildCreateRequest());
            }
            else if (_mode == FormMode.Edit)
            {
                await _offsiteService.UpdateAsync(new UpdateOffsiteRecordRequest
                {
                    OffsiteRecordId = _recordId!.Value,
                    OffsiteType = _typeComboBox.SelectedItem?.ToString() ?? string.Empty,
                    LocationName = _locationTextBox.Text,
                    ContactPerson = _contactPersonTextBox.Text,
                    ContactNumber = _contactNumberTextBox.Text,
                    StartDate = _startDatePicker.Value.Date,
                    ExpectedReturnDate = _expectedReturnPicker.Value.Date,
                    AmountPaid = _amountPaidInput.Value,
                    ProofFilePath = _selectedProofPath ?? _record?.ProofFilePath
                });
            }
            else if (_mode == FormMode.Complete)
            {
                await _offsiteService.CompleteAsync(new CompleteOffsiteRecordRequest
                {
                    OffsiteRecordId = _recordId!.Value,
                    CompletedDate = _completedDatePicker.Value.Date,
                    WorkResult = "Completed",
                    AmountPaid = _actualCostInput.Value,
                    ProofFilePath = _selectedProofPath ?? _record?.ProofFilePath,
                    CompletedByUserId = _currentUserId
                });
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException exception)
        {
            string message = exception.Errors.FirstOrDefault()?.ErrorMessage ?? exception.Message;
            MessageBoxHelper.ShowWarning(message);
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Failed to save: {exception.Message}");
        }
    }

    private CreateOffsiteRecordRequest BuildCreateRequest()
    {
        if (_addTabs.SelectedTab == _scheduleTab)
        {
            if (_scheduleComboBox.SelectedItem is not ScheduleOption schedule)
            {
                throw new ValidationException("Please select a schedule.");
            }

            return new CreateOffsiteRecordRequest
            {
                CarId = schedule.CarId,
                FleetScheduleId = schedule.ScheduleId,
                OffsiteType = _scheduleTypeComboBox.SelectedItem?.ToString() ?? "Maintenance",
                LocationName = _scheduleLocationTextBox.Text,
                ContactPerson = _scheduleContactPersonTextBox.Text,
                ContactNumber = _scheduleContactNumberTextBox.Text,
                StartDate = schedule.Start.Date,
                ExpectedReturnDate = schedule.End.Date,
                AmountPaid = _scheduleAmountPaidInput.Value,
                ProofFilePath = _selectedProofPath
            };
        }

        if (_carComboBox.SelectedItem is not CarOption car)
        {
            throw new ValidationException("Please select a car.");
        }

        return new CreateOffsiteRecordRequest
        {
            CarId = car.CarId,
            OffsiteType = _typeComboBox.SelectedItem?.ToString() ?? string.Empty,
            LocationName = _locationTextBox.Text,
            ContactPerson = _contactPersonTextBox.Text,
            ContactNumber = _contactNumberTextBox.Text,
            StartDate = _startDatePicker.Value.Date,
            ExpectedReturnDate = _expectedReturnPicker.Value.Date,
            AmountPaid = _amountPaidInput.Value,
            ProofFilePath = _selectedProofPath
        };
    }

    private static GroupBox CreateSection(string title, Control content)
    {
        GroupBox section = new()
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 30, 16, 14),
            Font = FontHelper.SemiBold(9.5F),
            ForeColor = ThemeHelper.TextPrimary,
            BackColor = ThemeHelper.Surface
        };
        content.Dock = DockStyle.Fill;
        section.Controls.Add(content);
        return section;
    }

    private static TableLayoutPanel CreateGrid(int columns, int rows, int rowHeight)
    {
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = columns,
            RowCount = rows,
            BackColor = ThemeHelper.Surface
        };

        for (int column = 0; column < columns; column++)
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
        }

        for (int row = 0; row < rows; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
        }

        return layout;
    }

    private static TableLayoutPanel CreateSingleInputLayout(string labelText, Control input)
    {
        TableLayoutPanel layout = CreateGrid(1, 1, 62);
        layout.Controls.Add(CreateInputPanel(labelText, input), 0, 0);
        return layout;
    }

    private static Panel CreateInputPanel(string labelText, Control input)
    {
        Panel panel = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 20, 0), BackColor = ThemeHelper.Surface };
        Label label = ControlFactory.CreateInputLabel(labelText);
        label.Location = new Point(0, 0);
        input.Location = new Point(0, 24);
        input.Font = FontHelper.Regular(10F);
        panel.Controls.Add(label);
        panel.Controls.Add(input);
        return panel;
    }

    private static Panel CreateDisplayCell(string labelText, Label valueLabel)
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.Surface };
        Label label = new()
        {
            Text = labelText,
            Location = new Point(0, 0),
            Size = new Size(280, 18),
            Font = FontHelper.SemiBold(9F),
            ForeColor = ThemeHelper.TextSecondary
        };
        valueLabel.Location = new Point(0, 20);
        valueLabel.Size = new Size(280, 24);
        panel.Controls.Add(label);
        panel.Controls.Add(valueLabel);
        return panel;
    }

    private static Panel CreateProofPickerPanel(string labelText, Label pathLabel, Button browseButton, Button openButton)
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.Surface };
        Label label = ControlFactory.CreateInputLabel(labelText);
        label.Location = new Point(0, 0);
        browseButton.Location = new Point(0, 26);
        openButton.Location = new Point(102, 26);
        pathLabel.Location = new Point(204, 31);
        pathLabel.Size = new Size(250, 20);
        panel.Resize += (_, _) => pathLabel.Width = Math.Max(panel.Width - 214, 180);
        panel.Controls.Add(label);
        panel.Controls.Add(browseButton);
        panel.Controls.Add(openButton);
        panel.Controls.Add(pathLabel);
        return panel;
    }

    private static ComboBox CreateComboBox(int width = InputWidth)
    {
        return new ComboBox
        {
            Width = width,
            Height = InputHeight,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextPrimary
        };
    }

    private static DateTimePicker CreateDatePicker()
    {
        return new DateTimePicker
        {
            Width = InputWidth,
            Height = InputHeight,
            Format = DateTimePickerFormat.Short,
            Font = FontHelper.Regular(10F)
        };
    }

    private static NumericUpDown CreateMoneyInput()
    {
        return new NumericUpDown
        {
            Width = InputWidth,
            Height = InputHeight,
            DecimalPlaces = 2,
            Maximum = 1000000,
            Increment = 1000,
            ThousandsSeparator = true,
            Font = FontHelper.Regular(10F)
        };
    }

    private static Label CreateValueLabel()
    {
        return new Label
        {
            Text = "-",
            AutoSize = false,
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextPrimary,
            AutoEllipsis = true
        };
    }

    private static Label CreatePathLabel()
    {
        return new Label
        {
            Text = "No file selected",
            AutoSize = false,
            Font = FontHelper.Regular(9F),
            ForeColor = ThemeHelper.TextSecondary,
            AutoEllipsis = true
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
            Font = FontHelper.SemiBold(9F),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = ThemeHelper.Border;
        return button;
    }

    private sealed record CarOption(int CarId, string CarName, string PlateNumber)
    {
        public override string ToString() => $"{CarName} ({PlateNumber})";
    }

    private sealed record ScheduleOption(int ScheduleId, string Title, string CarName, string PlateNumber, DateTime Start, DateTime End, string Status, int CarId)
    {
        public override string ToString() => $"{CarName} ({PlateNumber}) - {Start:MMM d}";
    }
}
