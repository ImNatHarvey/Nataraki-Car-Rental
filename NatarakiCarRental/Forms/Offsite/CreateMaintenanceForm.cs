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
    private readonly TransactionService _transactionService;
    private readonly FleetScheduleService _scheduleService;
    private readonly CarService _carService = new();
    private readonly CustomerService _customerService = new();
    private readonly ErrorProvider _errorProvider = new();
    private readonly Label _validationLabel = new();
    private readonly TabControl _flowTabs = new();
    
    // Tab 1: Maintenance from Schedule
    private readonly ComboBox _reservationComboBox = CreateComboBox(900);
    private readonly Label _reservationSummaryLabel = CreateSummaryLabel();
    
    // Tab 2: Direct Maintenance
    private readonly ComboBox _maintenanceCarComboBox = CreateComboBox();
    private readonly ComboBox _maintenanceClientComboBox = CreateComboBox();
    private readonly DateTimePicker _maintenanceStartDatePicker = CreateDatePicker();
    private readonly DateTimePicker _maintenanceEndDatePicker = CreateDatePicker();
    private readonly NumericUpDown _estimatedCostInput = CreateMoneyInput();

    private IReadOnlyList<FleetScheduleModel> _eligibleMaintenance = [];
    private IReadOnlyList<Car> _cars = [];
    private IReadOnlyList<Customer> _offsiteClients = [];

    public CreateMaintenanceForm(int currentUserId)
    {
        _currentUserId = currentUserId;
        _transactionService = new TransactionService(currentUserId);
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
        ClientSize = new Size(1000, 560);
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
        _flowTabs.Size = new Size(936, 360);
        _flowTabs.Font = FontHelper.SemiBold(9F);
        _flowTabs.TabPages.Add(CreateScheduleTab());
        _flowTabs.TabPages.Add(CreateDirectTab());
        Controls.Add(_flowTabs);

        Button cancelButton = ControlFactory.CreateSecondaryButton("Cancel", 110, 38);
        cancelButton.Location = new Point(666, 484);
        cancelButton.DialogResult = DialogResult.Cancel;

        Button saveButton = ControlFactory.CreatePrimaryButton("Create Maintenance", 180, 38);
        saveButton.Location = new Point(788, 484);
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
        
        _reservationSummaryLabel.Location = new Point(18, 82);
        _reservationSummaryLabel.Size = new Size(900, 200);
        _reservationSummaryLabel.Text = "Select a pending maintenance schedule to view its details.";
        tab.Controls.Add(_reservationSummaryLabel);
        
        _reservationComboBox.SelectedIndexChanged += (_, _) => UpdateSummary();
        return tab;
    }

    private TabPage CreateDirectTab()
    {
        TabPage tab = new("Direct Maintenance") { BackColor = ThemeHelper.Surface };
        TableLayoutPanel layout = new()
        {
            Location = new Point(18, 10),
            Size = new Size(900, 300),
            ColumnCount = 2,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        
        layout.Controls.Add(CreateInputPanel("Car *", _maintenanceCarComboBox), 0, 0);
        layout.Controls.Add(CreateInputPanel("Offsite Client *", _maintenanceClientComboBox), 1, 0);
        layout.Controls.Add(CreateInputPanel("Start Date *", _maintenanceStartDatePicker), 0, 1);
        layout.Controls.Add(CreateInputPanel("End Date *", _maintenanceEndDatePicker), 1, 1);
        layout.Controls.Add(CreateInputPanel("Estimated Cost (₱)", _estimatedCostInput), 0, 2);
        
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
            _offsiteClients = await _customerService.SearchCustomersAsync("", CustomerListFilter.OffsiteClients);
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

        _maintenanceClientComboBox.Items.Clear();
        _maintenanceClientComboBox.Items.Add(new LookupOption(null, "Select an offsite client"));
        _maintenanceClientComboBox.Items.AddRange(_offsiteClients
            .Select(c => new LookupOption(c.CustomerId, !string.IsNullOrWhiteSpace(c.CompanyName) ? c.CompanyName : $"{c.FirstName} {c.LastName}".Trim()))
            .Cast<object>()
            .ToArray());
        _maintenanceClientComboBox.SelectedIndex = 0;
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

        string clientName = schedule.CustomerName ?? "-";
        string email = "N/A";
        string address = "N/A";

        if (schedule.CustomerId.HasValue)
        {
            try
            {
                var customer = await _customerService.GetCustomerByIdAsync(schedule.CustomerId.Value);
                if (customer != null)
                {
                    clientName = !string.IsNullOrWhiteSpace(customer.CompanyName) ? customer.CompanyName : $"{customer.FirstName} {customer.LastName}".Trim();
                    email = customer.Email ?? "N/A";
                    address = $"{customer.StreetAddress}, {customer.Barangay}, {customer.City}, {customer.Province}".Trim(',', ' ');
                }
            }
            catch { }
        }

        _reservationSummaryLabel.Text =
            $"Offsite Client: {clientName}{Environment.NewLine}" +
            $"Email: {email}{Environment.NewLine}" +
            $"Address: {address}{Environment.NewLine}{Environment.NewLine}" +
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
                
                var schedule = _eligibleMaintenance.First(s => s.ScheduleId == scheduleId);
                if (!schedule.CustomerId.HasValue) throw Validation(nameof(_reservationComboBox), "Selected schedule has no client assigned.");

                await _transactionService.CreateMaintenanceTransactionAsync(new CreateMaintenanceTransactionRequest
                {
                    FleetScheduleId = schedule.ScheduleId,
                    CarId = schedule.CarId,
                    CustomerId = schedule.CustomerId.Value,
                    MaintenanceType = "Maintenance",
                    StartDate = schedule.StartDate,
                    EndDate = schedule.EndDate,
                    EstimatedCost = 0
                });
            }
            else
            {
                int? carId = GetSelectedLookupId(_maintenanceCarComboBox);
                int? clientId = GetSelectedLookupId(_maintenanceClientComboBox);
                
                if (carId is null) throw Validation(nameof(_maintenanceCarComboBox), "Please select a car.");
                if (clientId is null) throw Validation(nameof(_maintenanceClientComboBox), "Please select an offsite client.");
                
                await _transactionService.CreateMaintenanceTransactionAsync(new CreateMaintenanceTransactionRequest
                {
                    CarId = carId.Value,
                    CustomerId = clientId.Value,
                    MaintenanceType = "Maintenance",
                    StartDate = _maintenanceStartDatePicker.Value.Date,
                    EndDate = _maintenanceEndDatePicker.Value.Date,
                    EstimatedCost = _estimatedCostInput.Value
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

    private static NumericUpDown CreateMoneyInput()
    {
        return new NumericUpDown { DecimalPlaces = 2, Maximum = 1000000, Increment = 1000, ThousandsSeparator = true, Font = FontHelper.Regular(10F), Width = 420 };
    }

    private static Label CreateSummaryLabel()
    {
        return new Label { AutoSize = false, Font = FontHelper.Regular(10F), ForeColor = ThemeHelper.TextPrimary, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(12), BackColor = Color.FromArgb(248, 250, 252) };
    }

    private static int? GetSelectedLookupId(ComboBox comboBox) => comboBox.SelectedItem is LookupOption option ? option.Id : null;
    private static ValidationException Validation(string propertyName, string message) => new([new ValidationFailure(propertyName, message)]);
    private sealed record LookupOption(int? Id, string Name) { public override string ToString() => Name; }
}
