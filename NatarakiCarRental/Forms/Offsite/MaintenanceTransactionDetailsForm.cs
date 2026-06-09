using System.Drawing.Drawing2D;
using FluentValidation;
using FluentValidation.Results;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using FleetScheduleModel = NatarakiCarRental.Models.FleetSchedule;

namespace NatarakiCarRental.Forms.Offsite;

public sealed class MaintenanceTransactionDetailsForm : Form
{
    private readonly int _currentUserId;
    private readonly TransactionService _transactionService;
    private readonly CarService _carService;
    private readonly CustomerService _customerService;
    private readonly FleetScheduleService _scheduleService;
    private readonly ErrorProvider _errorProvider = new();
    private readonly Transaction? _existingTransaction;
    private readonly bool _viewOnly;
    
    private readonly ComboBox _reservationComboBox = CreateComboBox(750);
    private readonly Label _reservationSummaryLabel = CreateSummaryLabel();

    private readonly ComboBox _directCarComboBox = CreateComboBox();
    private readonly ComboBox _directClientComboBox = CreateComboBox();
    private readonly DateTimePicker _startDatePicker = CreateDatePicker();
    private readonly DateTimePicker _endDatePicker = CreateDatePicker();
    
    private readonly Label _validationLabel = new();
    private readonly TabControl _flowTabs = new();
    private readonly Button _saveButton = ControlFactory.CreatePrimaryButton("Create Maintenance", 140, 38);

    private IReadOnlyList<Car> _cars = [];
    private IReadOnlyList<Customer> _clients = [];
    private IReadOnlyList<FleetScheduleModel> _schedules = [];

    public MaintenanceTransactionDetailsForm(int currentUserId, Transaction? transaction = null, bool viewOnly = false)
    {
        _currentUserId = currentUserId;
        _transactionService = new TransactionService(currentUserId);
        _carService = new CarService(currentUserId);
        _customerService = new CustomerService(currentUserId);
        _scheduleService = new FleetScheduleService(currentUserId);
        _existingTransaction = transaction;
        _viewOnly = viewOnly;
        InitializeForm();
        Load += MaintenanceTransactionDetailsForm_Load;
        if (transaction is not null) LoadTransaction(transaction);
    }

    private void InitializeForm()
    {
        Text = "Create Maintenance Transaction";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(1060, 520);
        BackColor = ThemeHelper.Surface;
        Font = FontHelper.Regular();

        Controls.Add(new Label
        {
            Text = "Create Maintenance Transaction",
            AutoSize = false,
            Location = new Point(32, 24),
            Size = new Size(400, 34),
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary
        });

        _validationLabel.AutoSize = false;
        _validationLabel.Location = new Point(34, 66);
        _validationLabel.Size = new Size(996, 24);
        _validationLabel.Font = FontHelper.SemiBold(9F);
        _validationLabel.ForeColor = ThemeHelper.Danger;
        _validationLabel.Visible = false;
        Controls.Add(_validationLabel);

        _flowTabs.Dock = DockStyle.None;
        _flowTabs.Location = new Point(32, 100);
        _flowTabs.Size = new Size(996, 320);
        _flowTabs.Font = FontHelper.SemiBold(9F);
        _flowTabs.TabPages.Add(CreateScheduleTab());
        _flowTabs.TabPages.Add(CreateDirectTab());
        Controls.Add(_flowTabs);

        Button cancelButton = ControlFactory.CreateSecondaryButton("Cancel", 118, 38);
        cancelButton.Location = new Point(756, 450);
        cancelButton.DialogResult = DialogResult.Cancel;

        _saveButton.Location = new Point(888, 450);
        _saveButton.Click += SaveButton_Click;

        Controls.Add(cancelButton);
        Controls.Add(_saveButton);
        AcceptButton = _saveButton;
        CancelButton = cancelButton;
    }

    private TabPage CreateScheduleTab()
    {
        TabPage tab = new("Create from Maintenance Schedule") { BackColor = ThemeHelper.Surface };
        _reservationComboBox.Width = 960;
        tab.Controls.Add(CreateInputPanel("Eligible Maintenance Schedule *", _reservationComboBox, new Point(18, 16)));
        
        _reservationSummaryLabel.Location = new Point(18, 82);
        _reservationSummaryLabel.Size = new Size(960, 190);
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
            Size = new Size(960, 200),
            ColumnCount = 2,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        
        layout.Controls.Add(CreateInputPanel("Car *", _directCarComboBox), 0, 0);
        layout.Controls.Add(CreateInputPanel("Offsite Client *", _directClientComboBox), 1, 0);
        layout.Controls.Add(CreateInputPanel("Start Date *", _startDatePicker), 0, 1);
        layout.Controls.Add(CreateInputPanel("End Date *", _endDatePicker), 1, 1);
        
        tab.Controls.Add(layout);
        return tab;
    }

    private async void MaintenanceTransactionDetailsForm_Load(object? sender, EventArgs e)
    {
        try {
            _schedules = await _scheduleService.GetMaintenanceSchedulesAsync();
            _cars = await _carService.GetActiveCarsAsync();
            _clients = await _customerService.SearchCustomersAsync("", CustomerListFilter.OffsiteClients);
            PopulateLookups();
        } catch (Exception ex) { MessageBoxHelper.ShowError($"Failed to load data: {ex.Message}"); }
    }

    private void PopulateLookups()
    {
        _reservationComboBox.Items.Clear();
        _reservationComboBox.Items.Add(new LookupOption(null, "Select a maintenance schedule"));
        _reservationComboBox.Items.AddRange(_schedules
            .Select(s => new LookupOption(s.ScheduleId, $"{s.Title} - {s.CarName} ({s.PlateNumber})"))
            .Cast<object>()
            .ToArray());
        _reservationComboBox.SelectedIndex = 0;

        _directCarComboBox.Items.Clear();
        _directCarComboBox.Items.Add(new LookupOption(null, "Select a car"));
        _directCarComboBox.Items.AddRange(_cars
            .Select(c => new LookupOption(c.CarId, $"{c.CarName} ({c.PlateNumber})"))
            .Cast<object>()
            .ToArray());
        _directCarComboBox.SelectedIndex = 0;

        _directClientComboBox.Items.Clear();
        _directClientComboBox.Items.Add(new LookupOption(null, "Select a client"));
        _directClientComboBox.Items.AddRange(_clients
            .Select(c => new LookupOption(c.CustomerId, $"{c.CompanyName ?? c.FirstName + " " + c.LastName}"))
            .Cast<object>()
            .ToArray());
        _directClientComboBox.SelectedIndex = 0;
    }

    private async void UpdateSummary()
    {
        int? id = GetSelectedLookupId(_reservationComboBox);
        var schedule = _schedules.FirstOrDefault(s => s.ScheduleId == id);
        if (schedule is null)
        {
            _reservationSummaryLabel.Text = "Select a pending maintenance schedule to view its details.";
            return;
        }

        string customerInfo = "N/A";
        if (schedule.CustomerId.HasValue)
        {
            var customer = await _customerService.GetCustomerByIdAsync(schedule.CustomerId.Value);
            if (customer != null)
            {
                string address = $"{customer.StreetAddress}, {customer.Barangay}, {customer.City}, {customer.Province}".Trim(',', ' ');
                customerInfo = $"Offsite Client: {customer.FirstName} {customer.LastName}{Environment.NewLine}" +
                               $"Email: {customer.Email ?? "N/A"}{Environment.NewLine}" +
                               $"Address: {address}";
            }
        }
        else customerInfo = "Offsite Client: Walk-In / No customer";

        _reservationSummaryLabel.Text =
            customerInfo + $"{Environment.NewLine}" +
            $"Car: {schedule.CarName} ({schedule.PlateNumber}){Environment.NewLine}" +
            $"Dates: {schedule.StartDate:MMM d, yyyy} - {schedule.EndDate:MMM d, yyyy}";
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        _validationLabel.Visible = false;
        _errorProvider.Clear();

        CreateMaintenanceTransactionRequest request;
        if (_flowTabs.SelectedIndex == 0)
        {
            int? id = GetSelectedLookupId(_reservationComboBox);
            if (id is null) { SetError(_reservationComboBox, "Please select a schedule."); return; }
            var schedule = _schedules.First(s => s.ScheduleId == id);
            request = new CreateMaintenanceTransactionRequest { CarId = schedule.CarId, CustomerId = schedule.CustomerId ?? 0, StartDate = schedule.StartDate, EndDate = schedule.EndDate };
        }
        else
        {
            int? carId = GetSelectedLookupId(_directCarComboBox);
            if (carId is null) { SetError(_directCarComboBox, "Please select a car."); return; }
            int? clientId = GetSelectedLookupId(_directClientComboBox);
            if (clientId is null) { SetError(_directClientComboBox, "Please select a client."); return; }
            request = new CreateMaintenanceTransactionRequest { CarId = carId.Value, CustomerId = clientId.Value, StartDate = _startDatePicker.Value.Date, EndDate = _endDatePicker.Value.Date };
        }

        try {
            _saveButton.Enabled = false;
            await _transactionService.CreateMaintenanceTransactionAsync(request);
            MessageBoxHelper.ShowSuccess("Maintenance transaction created successfully.");
            DialogResult = DialogResult.OK;
            Close();
        } catch (Exception ex) { MessageBoxHelper.ShowError($"Failed to create transaction: {ex.Message}"); }
        finally { _saveButton.Enabled = true; }
    }

    private void SetError(Control c, string msg) { _errorProvider.SetError(c, msg); _validationLabel.Text = msg; _validationLabel.Visible = true; }
    private static ComboBox CreateComboBox(int width = 420) => new() { Width = width, Height = 30, DropDownStyle = ComboBoxStyle.DropDownList, Font = FontHelper.Regular(10F) };
    private static DateTimePicker CreateDatePicker() => new() { Width = 420, Height = 30, Format = DateTimePickerFormat.Short, Font = FontHelper.Regular(10F) };
    private static Label CreateSummaryLabel() => new() { AutoSize = false, Font = FontHelper.Regular(10F), ForeColor = ThemeHelper.TextPrimary, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(12), BackColor = Color.FromArgb(248, 250, 252) };
    private static Panel CreateInputPanel(string label, Control input, Point? loc = null) {
        Panel p = new() { Dock = loc is null ? DockStyle.Fill : DockStyle.None, Location = loc ?? Point.Empty, Size = new Size(input.Width, input.Height + 24), Padding = new Padding(0, 0, 12, 0), BackColor = ThemeHelper.Surface };
        Label l = ControlFactory.CreateInputLabel(label); l.Location = new Point(0, 0); input.Location = new Point(0, 22);
        p.Controls.Add(l); p.Controls.Add(input); return p;
    }
    private static int? GetSelectedLookupId(ComboBox c) => c.SelectedItem is LookupOption o ? o.Id : null;
    private void LoadTransaction(Transaction transaction) { _saveButton.Visible = !_viewOnly; }
    private sealed record LookupOption(int? Id, string Name) { public override string ToString() => Name; }
}
