using System.Drawing.Drawing2D;
using FluentValidation;
using FluentValidation.Results;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using FleetScheduleModel = NatarakiCarRental.Models.FleetSchedule;

namespace NatarakiCarRental.Forms.Offsite;

public sealed class CreateMaintenanceForm : Form
{
    private readonly int _currentUserId;
    private readonly OffsiteService _offsiteService;
    private readonly FleetScheduleService _scheduleService;
    private readonly CarService _carService = new();
    private readonly CustomerService _customerService = new();
    private readonly ErrorProvider _errorProvider = new();
    private readonly Label _validationLabel = new();
    private readonly TabControl _flowTabs = new();
    
    // Tab 1: Maintenance from Schedule
    private readonly ComboBox _reservationComboBox = CreateComboBox(880);
    private readonly Label _reservationSummaryLabel = CreateSummaryLabel();
    
    // Tab 2: Direct Maintenance
    private readonly ComboBox _maintenanceCarComboBox = CreateComboBox();
    private readonly DateTimePicker _maintenanceStartDatePicker = CreateDatePicker();
    private readonly DateTimePicker _maintenanceEndDatePicker = CreateDatePicker();

    private IReadOnlyList<FleetScheduleModel> _eligibleMaintenance = [];
    private IReadOnlyList<Car> _cars = [];

    public CreateMaintenanceForm(int currentUserId)
    {
        _currentUserId = currentUserId;
        _offsiteService = new OffsiteService(currentUserId);
        _scheduleService = new FleetScheduleService(currentUserId);
        InitializeForm();
        Load += CreateMaintenanceForm_Load;
    }

    private void InitializeForm()
    {
        Text = "Create Maintenance Record";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(1000, 520);
        BackColor = ThemeHelper.Surface;
        Font = FontHelper.Regular();
        ShowInTaskbar = false;

        _errorProvider.ContainerControl = this;
        _errorProvider.BlinkStyle = ErrorBlinkStyle.NeverBlink;

        Controls.Add(new Label
        {
            Text = "Create Maintenance Record",
            AutoSize = false,
            Location = new Point(32, 24),
            Size = new Size(400, 34),
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary
        });

        _validationLabel.AutoSize = false;
        _validationLabel.Location = new Point(34, 66);
        _validationLabel.Size = new Size(932, 24);
        _validationLabel.Font = FontHelper.SemiBold(9F);
        _validationLabel.ForeColor = ThemeHelper.Danger;
        _validationLabel.Visible = false;
        Controls.Add(_validationLabel);

        _flowTabs.Dock = DockStyle.None;
        _flowTabs.Location = new Point(32, 100);
        _flowTabs.Size = new Size(936, 320);
        _flowTabs.Font = FontHelper.SemiBold(9F);
        _flowTabs.TabPages.Add(CreateScheduleTab());
        _flowTabs.TabPages.Add(CreateDirectTab());
        Controls.Add(_flowTabs);

        Button cancelButton = ControlFactory.CreateSecondaryButton("Cancel", 110, 38);
        cancelButton.Location = new Point(560, 450);
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Margin = new Padding(0, 0, 10, 0);

        Button saveButton = ControlFactory.CreatePrimaryButton("Create Maintenance", 180, 38);
        saveButton.Location = new Point(680, 450);
        saveButton.Click += SaveButton_Click;

        Controls.Add(cancelButton);
        Controls.Add(saveButton);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private TabPage CreateScheduleTab()
    {
        TabPage tab = new("Create from Maintenance Schedule") { BackColor = ThemeHelper.Surface };
        tab.Controls.Add(CreateInputPanel("Eligible Maintenance Schedule *", _reservationComboBox, new Point(18, 16)));
        
        // Bordered details preview box
        GroupBox detailsBox = new()
        {
            Text = "Maintenance Details",
            Location = new Point(18, 82),
            Size = new Size(900, 190),
            Font = FontHelper.SemiBold(9.5F),
            ForeColor = ThemeHelper.TextPrimary,
            BackColor = ThemeHelper.Surface
        };
        
        _reservationSummaryLabel.Location = new Point(16, 26);
        _reservationSummaryLabel.Size = new Size(868, 150);
        _reservationSummaryLabel.Text = "Select a pending maintenance schedule to view its details.";
        detailsBox.Controls.Add(_reservationSummaryLabel);
        
        tab.Controls.Add(detailsBox);
        _reservationComboBox.SelectedIndexChanged += (_, _) => UpdateSummary();
        return tab;
    }

    private TabPage CreateDirectTab()
    {
        TabPage tab = new("Direct Maintenance") { BackColor = ThemeHelper.Surface };
        TableLayoutPanel layout = new()
        {
            Location = new Point(18, 10),
            Size = new Size(900, 200),
            ColumnCount = 2,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        
        layout.Controls.Add(CreateInputPanel("Car *", _maintenanceCarComboBox), 0, 0);
        layout.Controls.Add(CreateInputPanel("Start Date *", _maintenanceStartDatePicker), 0, 1);
        layout.Controls.Add(CreateInputPanel("End Date *", _maintenanceEndDatePicker), 1, 1);
        
        tab.Controls.Add(layout);
        return tab;
    }

    private async void CreateMaintenanceForm_Load(object? sender, EventArgs e)
    {
        Load -= CreateMaintenanceForm_Load;
        try
        {
            _eligibleMaintenance = await _scheduleService.GetMaintenanceSchedulesAsync();
            _cars = await _carService.GetActiveCarsAsync();
            PopulateLookups();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to load maintenance form data.\n\n{exception.Message}", "Maintenance");
            Close();
        }
    }

    private void PopulateLookups()
    {
        _reservationComboBox.Items.Clear();
        _reservationComboBox.Items.Add(new LookupOption(null, "Select a maintenance schedule"));
        _reservationComboBox.Items.AddRange(_eligibleMaintenance
            .Select(schedule => new LookupOption(schedule.ScheduleId, $"{schedule.Title} - {schedule.CarName} ({schedule.PlateNumber})"))
            .Cast<object>()
            .ToArray());
        _reservationComboBox.SelectedIndex = 0;

        _maintenanceCarComboBox.Items.Clear();
        _maintenanceCarComboBox.Items.Add(new LookupOption(null, "Select a car"));
        _maintenanceCarComboBox.Items.AddRange(_cars
            .Select(car => new LookupOption(car.CarId, $"{car.CarName} ({car.PlateNumber})"))
            .Cast<object>()
            .ToArray());
        _maintenanceCarComboBox.SelectedIndex = 0;
    }

    private async void UpdateSummary()
    {
        int? id = GetSelectedLookupId(_reservationComboBox);
        var schedule = _eligibleMaintenance.FirstOrDefault(s => s.ScheduleId == id);
        if (schedule is null)
        {
            _reservationSummaryLabel.Text = "Select a pending maintenance schedule to view its details.";
            return;
        }

        string customerInfo = "N/A";
        if (schedule.CustomerId.HasValue)
        {
            try
            {
                var customer = await _customerService.GetCustomerByIdAsync(schedule.CustomerId.Value);
                if (customer != null)
                {
                    string fullName = $"{customer.FirstName} {customer.LastName}".Trim();
                    string address = $"{customer.StreetAddress}, {customer.Barangay}, {customer.City}, {customer.Province}".Trim(',', ' ');
                    customerInfo = $"Offsite Client: {fullName}{Environment.NewLine}" +
                                   $"Email: {customer.Email ?? "N/A"}{Environment.NewLine}" +
                                   $"Address: {address}";
                }
            }
            catch
            {
                customerInfo = "Error loading customer details.";
            }
        }
        else
        {
            customerInfo = "Offsite Client: Walk-In / No customer";
        }

        _reservationSummaryLabel.Text =
            customerInfo + $"{Environment.NewLine}" +
            $"Car: {schedule.CarName} ({schedule.PlateNumber}){Environment.NewLine}" +
            $"Dates: {schedule.StartDate:MMM d, yyyy} - {schedule.EndDate:MMM d, yyyy}";
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button saveButton) return;

        try
        {
            saveButton.Enabled = false;
            _validationLabel.Visible = false;
            _errorProvider.Clear();

            if (_flowTabs.SelectedIndex == 0)
            {
                int? scheduleId = GetSelectedLookupId(_reservationComboBox);
                if (scheduleId is null) throw Validation(nameof(_reservationComboBox), "Please select a maintenance schedule.");
                
                await _offsiteService.CreateAsync(new CreateOffsiteRecordRequest
                {
                    CarId = _eligibleMaintenance.First(s => s.ScheduleId == scheduleId).CarId,
                    OffsiteType = "Maintenance",
                    FleetScheduleId = scheduleId.Value,
                    StartDate = DateTime.Today // Placeholder, service uses schedule
                });
            }
            else
            {
                int? carId = GetSelectedLookupId(_maintenanceCarComboBox);
                if (carId is null) throw Validation(nameof(_maintenanceCarComboBox), "Please select a car.");
                
                await _offsiteService.CreateAsync(new CreateOffsiteRecordRequest
                {
                    CarId = carId.Value,
                    OffsiteType = "Maintenance",
                    StartDate = _maintenanceStartDatePicker.Value.Date,
                    ExpectedReturnDate = _maintenanceEndDatePicker.Value.Date
                });
            }

            MessageBoxHelper.ShowSuccess("Maintenance record created successfully.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException ex)
        {
            _validationLabel.Text = ex.Errors.FirstOrDefault()?.ErrorMessage ?? ex.Message;
            _validationLabel.Visible = true;
        }
        catch (Exception ex)
        {
            MessageBoxHelper.ShowError($"Unable to save maintenance record.\n\n{ex.Message}", "Maintenance");
        }
        finally
        {
            saveButton.Enabled = true;
        }
    }

    private static Panel CreateInputPanel(string labelText, Control inputControl, Point? location = null)
    {
        Panel panel = new()
        {
            Dock = location is null ? DockStyle.Fill : DockStyle.None,
            Location = location ?? Point.Empty,
            Size = new Size(inputControl.Width, inputControl.Height + 24),
            Padding = new Padding(0, 0, 12, 0),
            BackColor = ThemeHelper.Surface
        };
        Label label = ControlFactory.CreateInputLabel(labelText);
        label.Location = new Point(0, 0);
        inputControl.Location = new Point(0, 22);
        panel.Controls.Add(label);
        panel.Controls.Add(inputControl);
        return panel;
    }

    private static ComboBox CreateComboBox(int width = 420)
    {
        return new ComboBox { Width = width, Height = 30, DropDownStyle = ComboBoxStyle.DropDownList, Font = FontHelper.Regular(10F) };
    }

    private static DateTimePicker CreateDatePicker()
    {
        return new DateTimePicker { Width = 420, Height = 30, Format = DateTimePickerFormat.Short, Font = FontHelper.Regular(10F) };
    }

    private static Label CreateSummaryLabel()
    {
        return new Label { AutoSize = false, Font = FontHelper.Regular(10F), ForeColor = ThemeHelper.TextPrimary, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(12), BackColor = Color.FromArgb(248, 250, 252) };
    }

    private static int? GetSelectedLookupId(ComboBox comboBox) => comboBox.SelectedItem is LookupOption option ? option.Id : null;
    private static ValidationException Validation(string propertyName, string message) => new([new ValidationFailure(propertyName, message)]);
    private sealed record LookupOption(int? Id, string Name) { public override string ToString() => Name; }
}
