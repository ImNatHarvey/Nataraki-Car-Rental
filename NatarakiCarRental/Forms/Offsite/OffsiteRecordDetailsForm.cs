using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using FluentValidation;
using System.Diagnostics;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.Forms.Offsite;

public sealed class OffsiteRecordDetailsForm : Form
{
    private enum FormMode { Add, Edit, View, Complete }

    private readonly OffsiteService _offsiteService;
    private readonly CarService _carService;
    private readonly FleetScheduleService _fleetScheduleService;
    private readonly FormMode _mode;
    private readonly int? _recordId;
    private readonly int _currentUserId;
    private OffsiteRecord? _record;
    private string? _selectedProofPath;

    // Tabs
    private readonly TabControl _addTabs = new();
    private readonly TabPage _scheduleTab = new() { Text = "Create from Schedule" };
    private readonly TabPage _manualTab = new() { Text = "Manual Entry" };

    // Components
    private readonly ComboBox _scheduleComboBox = new();
    private readonly Label _summaryCarLabel = new();
    private readonly Label _summaryDateLabel = new();
    private readonly Label _summaryStatusLabel = new();

    private readonly ComboBox _carComboBox = new();
    private readonly ComboBox _typeComboBox = new();
    private readonly TextBox _locationTextBox = ControlFactory.CreateTextBox(300);
    private readonly TextBox _contactPersonTextBox = ControlFactory.CreateTextBox(300);
    private readonly TextBox _contactNumberTextBox = ControlFactory.CreateTextBox(300);
    private readonly DateTimePicker _startDatePicker = new();
    private readonly DateTimePicker _expectedReturnPicker = new();
    private readonly NumericUpDown _estimatedCostInput = new();
    private readonly NumericUpDown _actualCostInput = new();
    private readonly DateTimePicker _completedDatePicker = new();
    
    private readonly TextBox _proofPathTextBox = ControlFactory.CreateTextBox(300);
    private readonly Button _browseProofButton = new();
    private readonly Button _openProofButton = new();

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

        _saveButton = ControlFactory.CreatePrimaryButton(GetSaveButtonText(), 180, 40);
        _cancelButton = CreateSecondaryButton(_mode == FormMode.View ? "Close" : "Cancel");

        InitializeComponent();
        SetupEvents();
    }

    private static Button CreateSecondaryButton(string text)
    {
        Button button = new()
        {
            Text = text,
            Size = new Size(120, 40),
            FlatStyle = FlatStyle.Flat,
            Font = FontHelper.SemiBold(),
            BackColor = Color.White,
            ForeColor = ThemeHelper.TextPrimary,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = ThemeHelper.Border;
        return button;
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
        string titleText = $"{_mode} Offsite Record";
        Text = titleText;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(920, 780);
        BackColor = ThemeHelper.Surface;
        Font = FontHelper.Regular();
        ShowInTaskbar = false;

        Label titleLabel = new()
        {
            Text = titleText,
            AutoSize = false,
            Location = new Point(32, 24),
            Size = new Size(400, 34),
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Label subtitleLabel = new()
        {
            Text = _mode switch
            {
                FormMode.Edit => "Update operational status and maintenance details.",
                FormMode.View => "Review recorded maintenance or operational offsite details.",
                FormMode.Complete => "Mark as finished and record final costs.",
                _ => "Record why a vehicle is temporarily unavailable (maintenance, repair, etc.)."
            },
            AutoSize = false,
            Location = new Point(34, 58),
            Size = new Size(620, 24),
            Font = FontHelper.Regular(9.5F),
            ForeColor = ThemeHelper.TextSecondary
        };

        _addTabs.Location = new Point(32, 100);
        _addTabs.Size = new Size(856, 600);
        _addTabs.Font = FontHelper.SemiBold(9F);

        if (_mode == FormMode.Add)
        {
            _addTabs.TabPages.Add(_scheduleTab);
            _addTabs.TabPages.Add(_manualTab);
            _scheduleTab.BackColor = ThemeHelper.Surface;
            _manualTab.BackColor = ThemeHelper.Surface;

            _scheduleTab.Controls.Add(CreateScheduleTabContent());
            _manualTab.Controls.Add(CreateManualTabContent());
        }
        else
        {
            Panel p = new() { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = ThemeHelper.Surface };
            p.Controls.Add(CreateManualTabContent());
            TabPage t = new() { Text = "Offsite Details", BackColor = ThemeHelper.Surface };
            t.Controls.Add(p);
            _addTabs.TabPages.Add(t);
        }

        _cancelButton.Location = new Point(ClientSize.Width - 120 - 32, 715);
        if (_mode != FormMode.View)
        {
            _saveButton.Location = new Point(_cancelButton.Location.X - 180 - 16, 715);
            Controls.Add(_saveButton);
        }
        else
        {
            _cancelButton.Location = new Point(ClientSize.Width - 120 - 32, 715);
        }

        Controls.Add(titleLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(_addTabs);
        Controls.Add(_cancelButton);
        
        Click += (_, _) => ActiveControl = null;
        ConfigureControls();
    }

    private Control CreateScheduleTabContent()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F)); // Source
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F)); // Summary
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F)); // Details
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // Payment

        // 1. Source Selection
        GroupBox sourceGroup = CreateGroupBox("Source", new Size(800, 80));
        _scheduleComboBox.Location = new Point(24, 34);
        _scheduleComboBox.Width = 750;
        _scheduleComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        sourceGroup.Controls.Add(_scheduleComboBox);

        // 2. Schedule Summary
        GroupBox summaryGroup = CreateGroupBox("Schedule Summary", new Size(800, 90));
        TableLayoutPanel summaryGrid = CreateFieldsGrid(2, 1);
        summaryGrid.Location = new Point(24, 30);
        summaryGrid.Size = new Size(750, 50);
        summaryGrid.Controls.Add(CreateDisplayRow("Vehicle:", _summaryCarLabel), 0, 0);
        summaryGrid.Controls.Add(CreateDisplayRow("Range:", _summaryDateLabel), 1, 0);
        summaryGroup.Controls.Add(summaryGrid);

        // 3. Offsite Details (same fields as manual but without start/end date)
        GroupBox detailsGroup = CreateGroupBox("Offsite Details", new Size(800, 200));
        TableLayoutPanel dGrid = CreateFieldsGrid(2, 2);
        dGrid.Location = new Point(24, 30);
        dGrid.Controls.Add(CreateInputPanel("Offsite Type *", _typeComboBox), 0, 0);
        dGrid.Controls.Add(CreateInputPanel("Location Name", _locationTextBox), 1, 0);
        dGrid.Controls.Add(CreateInputPanel("Contact Person", _contactPersonTextBox), 0, 1);
        dGrid.Controls.Add(CreateInputPanel("Contact Number", _contactNumberTextBox), 1, 1);
        detailsGroup.Controls.Add(dGrid);

        // 4. Payment
        GroupBox paymentGroup = CreateGroupBox("Payment Information", new Size(800, 140));
        paymentGroup.Controls.Add(CreatePaymentFieldsContent());

        layout.Controls.Add(sourceGroup, 0, 0);
        layout.Controls.Add(summaryGroup, 0, 1);
        layout.Controls.Add(detailsGroup, 0, 2);
        layout.Controls.Add(paymentGroup, 0, 3);
        return layout;
    }

    private Control CreateManualTabContent()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F)); // Vehicle
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F)); // Details
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // Payment

        // 1. Vehicle
        GroupBox vehicleGroup = CreateGroupBox("Vehicle Information", new Size(800, 80));
        TableLayoutPanel vGrid = CreateFieldsGrid(1, 1);
        vGrid.Location = new Point(24, 30);
        vGrid.Controls.Add(CreateInputPanel("Select Car *", _carComboBox), 0, 0);
        vehicleGroup.Controls.Add(vGrid);

        // 2. Offsite Details
        GroupBox detailsGroup = CreateGroupBox("Offsite Details", new Size(800, 210));
        TableLayoutPanel dGrid = CreateFieldsGrid(2, 3);
        dGrid.Location = new Point(24, 30);
        dGrid.Controls.Add(CreateInputPanel("Offsite Type *", _typeComboBox), 0, 0);
        dGrid.Controls.Add(CreateInputPanel("Location Name", _locationTextBox), 1, 0);
        dGrid.Controls.Add(CreateInputPanel("Contact Person", _contactPersonTextBox), 0, 1);
        dGrid.Controls.Add(CreateInputPanel("Contact Number", _contactNumberTextBox), 1, 1);
        dGrid.Controls.Add(CreateInputPanel("Start Date *", _startDatePicker), 0, 2);
        dGrid.Controls.Add(CreateInputPanel("Expected Return Date *", _expectedReturnPicker), 1, 2);
        detailsGroup.Controls.Add(dGrid);

        // 3. Payment Information
        GroupBox paymentGroup = CreateGroupBox("Payment Information", new Size(800, 180));
        paymentGroup.Controls.Add(CreatePaymentFieldsContent());

        layout.Controls.Add(vehicleGroup, 0, 0);
        layout.Controls.Add(detailsGroup, 0, 1);
        layout.Controls.Add(paymentGroup, 0, 2);
        return layout;
    }

    private Control CreatePaymentFieldsContent()
    {
        TableLayoutPanel pGrid = CreateFieldsGrid(2, 2);
        pGrid.Location = new Point(24, 30);
        pGrid.Size = new Size(750, 130);
        
        pGrid.Controls.Add(CreateInputPanel("Estimated Cost (₱)", _estimatedCostInput), 0, 0);
        
        // Proof upload row
        Panel proofPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 0) };
        Label proofLabel = ControlFactory.CreateInputLabel("Proof / Receipt");
        proofLabel.Location = new Point(0, 0);
        _proofPathTextBox.Location = new Point(0, 22);
        _proofPathTextBox.Width = 200;
        _proofPathTextBox.ReadOnly = true;
        
        _browseProofButton.Text = "Browse";
        _browseProofButton.Size = new Size(70, 30);
        _browseProofButton.Location = new Point(206, 21);
        _browseProofButton.FlatStyle = FlatStyle.Flat;
        _browseProofButton.FlatAppearance.BorderColor = ThemeHelper.Border;
        _browseProofButton.Font = FontHelper.SemiBold(8.5F);
        
        _openProofButton.Text = "Open";
        _openProofButton.Size = new Size(60, 30);
        _openProofButton.Location = new Point(282, 21);
        _openProofButton.FlatStyle = FlatStyle.Flat;
        _openProofButton.FlatAppearance.BorderColor = ThemeHelper.Border;
        _openProofButton.Font = FontHelper.SemiBold(8.5F);
        _openProofButton.Enabled = false;

        proofPanel.Controls.Add(proofLabel);
        proofPanel.Controls.Add(_proofPathTextBox);
        proofPanel.Controls.Add(_browseProofButton);
        proofPanel.Controls.Add(_openProofButton);
        pGrid.Controls.Add(proofPanel, 1, 0);

        if (_mode == FormMode.Complete || _mode == FormMode.View || (_record != null && _record.Status == "Completed"))
        {
            pGrid.Controls.Add(CreateInputPanel("Completed Date *", _completedDatePicker), 0, 1);
            pGrid.Controls.Add(CreateInputPanel("Actual Cost (₱) *", _actualCostInput), 1, 1);
        }

        return pGrid;
    }

    private static GroupBox CreateGroupBox(string title, Size size)
    {
        GroupBox gb = new()
        {
            Text = title,
            Size = size,
            Font = FontHelper.SemiBold(9.5F),
            ForeColor = ThemeHelper.TextPrimary,
            BackColor = ThemeHelper.Surface,
            Margin = new Padding(0, 0, 0, 12)
        };
        return gb;
    }

    private static TableLayoutPanel CreateFieldsGrid(int columns, int rows)
    {
        TableLayoutPanel grid = new() { ColumnCount = columns, RowCount = rows, AutoSize = true, BackColor = ThemeHelper.Surface };
        for (int i = 0; i < columns; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
        return grid;
    }

    private static Panel CreateDisplayRow(string label, Label valueLabel)
    {
        Panel p = new() { Width = 350, Height = 30 };
        Label l = new() { Text = label, Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextSecondary, Location = new Point(0, 5), AutoSize = true };
        valueLabel.Font = FontHelper.Regular(9.5F);
        valueLabel.ForeColor = ThemeHelper.TextPrimary;
        valueLabel.Location = new Point(100, 5);
        valueLabel.AutoSize = true;
        valueLabel.Text = "-";
        p.Controls.Add(l);
        p.Controls.Add(valueLabel);
        return p;
    }

    private static Panel CreateInputPanel(string labelText, Control input)
    {
        Panel p = new() { Width = 370, Height = 60, Padding = new Padding(0, 0, 20, 0) };
        Label l = new() { Text = labelText, Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextPrimary, Location = new Point(0, 0), AutoSize = true };
        input.Location = new Point(0, 22);
        input.Font = FontHelper.Regular(10F); // Consistent normal input font
        p.Controls.Add(l);
        p.Controls.Add(input);
        return p;
    }

    private void ConfigureControls()
    {
        _carComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _carComboBox.Width = 350;
        _typeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _typeComboBox.Width = 350;
        _typeComboBox.Items.AddRange(["Maintenance", "Repair", "Cleaning"]);

        _startDatePicker.Format = DateTimePickerFormat.Short;
        _startDatePicker.Width = 350;
        _expectedReturnPicker.Format = DateTimePickerFormat.Short;
        _expectedReturnPicker.Width = 350;
        _completedDatePicker.Format = DateTimePickerFormat.Short;
        _completedDatePicker.Width = 350;

        _estimatedCostInput.Minimum = 0;
        _estimatedCostInput.Maximum = 1000000;
        _estimatedCostInput.Increment = 1000;
        _estimatedCostInput.ThousandsSeparator = true;
        _estimatedCostInput.Width = 350;

        _actualCostInput.Minimum = 0;
        _actualCostInput.Maximum = 1000000;
        _actualCostInput.Increment = 1000;
        _actualCostInput.ThousandsSeparator = true;
        _actualCostInput.Width = 350;

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
                _estimatedCostInput.Enabled = false;
                _actualCostInput.Enabled = false;
                _browseProofButton.Visible = false;
            }
        }
    }

    private void SetupEvents()
    {
        Load += async (_, _) => await LoadDataAsync();
        _saveButton.Click += async (_, _) => await SaveAsync();
        _cancelButton.Click += (_, _) => Close();
        _startDatePicker.ValueChanged += (_, _) => 
        {
            _expectedReturnPicker.MinDate = _startDatePicker.Value;
            if (_expectedReturnPicker.Value < _startDatePicker.Value) _expectedReturnPicker.Value = _startDatePicker.Value;
        };
        
        _browseProofButton.Click += (_, _) => BrowseProof();
        _openProofButton.Click += (_, _) => OpenProof();
        _scheduleComboBox.SelectedIndexChanged += async (_, _) => await HandleScheduleSelectionChange();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var cars = await _carService.GetActiveCarsAsync();
            _carComboBox.Items.Clear();
            _carComboBox.Items.Add("Select a car");
            foreach (var car in cars) _carComboBox.Items.Add(new CarOption(car.CarId, car.CarName, car.PlateNumber));
            _carComboBox.SelectedIndex = 0;

            if (_mode == FormMode.Add)
            {
                var allSchedules = await _fleetScheduleService.GetSchedulesForMonthAsync(DateTime.Now.Year, DateTime.Now.Month);
                var eligible = allSchedules.Where(s => s.ScheduleType == FleetScheduleConstants.Type.Maintenance && 
                                                      (s.Status == FleetScheduleConstants.Status.Ongoing || s.Status == "Pending") && 
                                                      !s.IsArchived).ToList();
                
                _scheduleComboBox.Items.Clear();
                _scheduleComboBox.Items.Add("Select an eligible maintenance schedule");
                foreach(var s in eligible) _scheduleComboBox.Items.Add(new ScheduleOption(s.ScheduleId, s.Title, s.CarName, s.PlateNumber, s.StartDate, s.EndDate, s.Status, s.CarId));
                _scheduleComboBox.SelectedIndex = 0;
            }

            if (_recordId.HasValue)
            {
                _record = await _offsiteService.GetByIdAsync(_recordId.Value);
                if (_record != null)
                {
                    foreach (var item in _carComboBox.Items)
                    {
                        if (item is CarOption opt && opt.CarId == _record.CarId) { _carComboBox.SelectedItem = item; break; }
                    }

                    _typeComboBox.SelectedItem = _record.OffsiteType;
                    _locationTextBox.Text = _record.LocationName;
                    _contactPersonTextBox.Text = _record.ContactPerson;
                    _contactNumberTextBox.Text = _record.ContactNumber;
                    _startDatePicker.Value = _record.StartDate;
                    if (_record.ExpectedReturnDate.HasValue) _expectedReturnPicker.Value = _record.ExpectedReturnDate.Value;
                    _estimatedCostInput.Value = _record.EstimatedCost;
                    
                    if (!string.IsNullOrEmpty(_record.ProofFilePath))
                    {
                        _proofPathTextBox.Text = Path.GetFileName(_record.ProofFilePath);
                        _openProofButton.Enabled = true;
                    }

                    if (_record.CompletedDate.HasValue) _completedDatePicker.Value = _record.CompletedDate.Value;
                    _actualCostInput.Value = _record.ActualCost;
                }
            }
        }
        catch (Exception ex) { MessageBoxHelper.ShowError($"Failed to load data: {ex.Message}"); }
    }

    private async Task HandleScheduleSelectionChange()
    {
        if (_scheduleComboBox.SelectedItem is not ScheduleOption opt)
        {
            _summaryCarLabel.Text = "-"; _summaryDateLabel.Text = "-"; _summaryStatusLabel.Text = "-"; return;
        }
        _summaryCarLabel.Text = $"{opt.CarName} ({opt.PlateNumber})";
        _summaryDateLabel.Text = $"{opt.Start:MMM d, yyyy} to {opt.End:MMM d, yyyy}";
        _summaryStatusLabel.Text = opt.Status;
    }

    private void BrowseProof()
    {
        using OpenFileDialog dialog = new() { Filter = "Image/PDF Files|*.jpg;*.jpeg;*.png;*.pdf" };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _selectedProofPath = dialog.FileName;
            _proofPathTextBox.Text = Path.GetFileName(dialog.FileName);
            _openProofButton.Enabled = true;
        }
    }

    private void OpenProof()
    {
        string? path = _selectedProofPath;
        if (string.IsNullOrEmpty(path) && _record != null) path = UploadPathHelper.ResolveOffsiteProofPath(_record.ProofFilePath);
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) { MessageBoxHelper.ShowWarning("The proof file could not be found."); return; }
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBoxHelper.ShowError($"Could not open file: {ex.Message}"); }
    }

    private async Task SaveAsync()
    {
        try
        {
            if (_mode == FormMode.Add)
            {
                CreateOffsiteRecordRequest request;
                if (_addTabs.SelectedTab == _scheduleTab)
                {
                    if (_scheduleComboBox.SelectedItem is not ScheduleOption opt) throw new ValidationException("Please select a schedule.");
                    request = new CreateOffsiteRecordRequest { CarId = opt.CarId, FleetScheduleId = opt.ScheduleId, OffsiteType = _typeComboBox.SelectedItem?.ToString() ?? "Maintenance",
                        LocationName = _locationTextBox.Text, ContactPerson = _contactPersonTextBox.Text, ContactNumber = _contactNumberTextBox.Text,
                        StartDate = opt.Start, ExpectedReturnDate = opt.End, EstimatedCost = _estimatedCostInput.Value, ProofFilePath = _selectedProofPath };
                }
                else
                {
                    if (_carComboBox.SelectedItem is not CarOption car) throw new ValidationException("Please select a car.");
                    request = new CreateOffsiteRecordRequest { CarId = car.CarId, OffsiteType = _typeComboBox.SelectedItem?.ToString() ?? "",
                        LocationName = _locationTextBox.Text, ContactPerson = _contactPersonTextBox.Text, ContactNumber = _contactNumberTextBox.Text,
                        StartDate = _startDatePicker.Value, ExpectedReturnDate = _expectedReturnPicker.Value, EstimatedCost = _estimatedCostInput.Value, ProofFilePath = _selectedProofPath };
                }
                await _offsiteService.CreateAsync(request);
            }
            else if (_mode == FormMode.Edit)
            {
                var request = new UpdateOffsiteRecordRequest { OffsiteRecordId = _recordId!.Value, OffsiteType = _typeComboBox.SelectedItem?.ToString() ?? "",
                    LocationName = _locationTextBox.Text, ContactPerson = _contactPersonTextBox.Text, ContactNumber = _contactNumberTextBox.Text,
                    StartDate = _startDatePicker.Value, ExpectedReturnDate = _expectedReturnPicker.Value, EstimatedCost = _estimatedCostInput.Value,
                    ProofFilePath = _selectedProofPath ?? _record?.ProofFilePath };
                await _offsiteService.UpdateAsync(request);
            }
            else if (_mode == FormMode.Complete)
            {
                await _offsiteService.CompleteAsync(_recordId!.Value, _completedDatePicker.Value, _actualCostInput.Value, null);
            }

            DialogResult = DialogResult.OK; Close();
        }
        catch (ValidationException ex) { MessageBoxHelper.ShowWarning(ex.Errors.First().ErrorMessage); }
        catch (Exception ex) { MessageBoxHelper.ShowError($"Failed to save: {ex.Message}"); }
    }

    private sealed record CarOption(int CarId, string CarName, string PlateNumber) { public override string ToString() => $"{CarName} ({PlateNumber})"; }
    private sealed record ScheduleOption(int ScheduleId, string Title, string CarName, string PlateNumber, DateTime Start, DateTime End, string Status, int CarId)
    {
        public override string ToString() => $"{CarName} ({PlateNumber}) - {Start:MMM d}";
    }
}
