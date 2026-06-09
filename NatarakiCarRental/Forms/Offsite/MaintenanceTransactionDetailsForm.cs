using System.Drawing.Drawing2D;
using FluentValidation;
using FluentValidation.Results;
using System.Diagnostics;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.Forms.Offsite;

public sealed class MaintenanceTransactionDetailsForm : Form
{
    private readonly int _currentUserId;
    private readonly TransactionService _transactionService;
    private readonly CarService _carService;
    private readonly CustomerService _customerService;
    private readonly ErrorProvider _errorProvider = new();
    private readonly Transaction? _existingTransaction;
    private readonly bool _viewOnly;

    private readonly ComboBox _carComboBox = CreateComboBox();
    private readonly ComboBox _clientComboBox = CreateComboBox();
    private readonly ComboBox _maintenanceTypeComboBox = CreateComboBox();
    private readonly DateTimePicker _startDatePicker = CreateDatePicker();
    private readonly DateTimePicker _endDatePicker = CreateDatePicker();
    private readonly NumericUpDown _estimatedCostInput = CreateMoneyInput();
    private readonly NumericUpDown _amountPaidInput = CreateMoneyInput();
    private readonly ComboBox _modeOfPaymentComboBox = CreateComboBox();
    private readonly TextBox _notesTextBox = new()
    {
        Width = 812,
        Height = 86,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Font = FontHelper.Regular(10F)
    };
    private readonly Label _validationLabel = new();
    private readonly Button _saveButton = ControlFactory.CreatePrimaryButton("Create Transaction", 180, 38);

    private IReadOnlyList<Car> _cars = [];
    private IReadOnlyList<Customer> _clients = [];

    public MaintenanceTransactionDetailsForm(int currentUserId, Transaction? transaction = null, bool viewOnly = false)
    {
        _currentUserId = currentUserId;
        _transactionService = new TransactionService(currentUserId);
        _carService = new CarService(currentUserId);
        _customerService = new CustomerService(currentUserId);
        _existingTransaction = transaction;
        _viewOnly = viewOnly;
        InitializeForm();
        ConfigureInputs();
        Load += MaintenanceTransactionDetailsForm_Load;
        
        if (transaction is not null)
        {
            LoadTransaction(transaction);
        }
    }

    private void InitializeForm()
    {
        Text = _existingTransaction is null ? "Create Maintenance Transaction" : (_viewOnly ? "View Maintenance Transaction" : "Edit Maintenance Transaction");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(1060, 680);
        BackColor = ThemeHelper.Surface;
        Font = FontHelper.Regular();

        Label titleLabel = new()
        {
            Text = Text,
            AutoSize = true,
            Location = new Point(32, 24),
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary
        };

        _validationLabel.AutoSize = false;
        _validationLabel.Location = new Point(34, 66);
        _validationLabel.Size = new Size(996, 24);
        _validationLabel.Font = FontHelper.SemiBold(9F);
        _validationLabel.ForeColor = ThemeHelper.Danger;
        _validationLabel.Visible = false;

        Panel contentPanel = ControlFactory.CreateCardPanel(new Size(996, 480));
        contentPanel.Location = new Point(32, 96);
        contentPanel.Padding = new Padding(24);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(CreateInputPanel("Vehicle *", _carComboBox), 0, 0);
        layout.Controls.Add(CreateInputPanel("Offsite Client / Partner *", _clientComboBox), 1, 0);
        layout.Controls.Add(CreateInputPanel("Maintenance Type *", _maintenanceTypeComboBox), 0, 1);
        layout.Controls.Add(CreateInputPanel("Estimated Cost (₱) *", _estimatedCostInput), 1, 1);
        layout.Controls.Add(CreateInputPanel("Start Date *", _startDatePicker), 0, 2);
        layout.Controls.Add(CreateInputPanel("End Date *", _endDatePicker), 1, 2);
        layout.Controls.Add(CreateInputPanel("Amount Paid (₱)", _amountPaidInput), 0, 3);
        layout.Controls.Add(CreateInputPanel("Mode of Payment", _modeOfPaymentComboBox), 1, 3);
        layout.Controls.Add(CreateInputPanel("Internal Notes", _notesTextBox), 0, 4);
        layout.SetColumnSpan(_notesTextBox.Parent!, 2);

        contentPanel.Controls.Add(layout);

        Button cancelButton = CreateSecondaryButton(_viewOnly ? "Close" : "Cancel", 120, 38);
        cancelButton.Location = new Point(_viewOnly ? 908 : 716, 608);
        cancelButton.DialogResult = DialogResult.Cancel;

        _saveButton.Location = new Point(848, 608);
        _saveButton.Click += SaveButton_Click;

        Controls.Add(titleLabel);
        Controls.Add(_validationLabel);
        Controls.Add(contentPanel);
        Controls.Add(cancelButton);
        if (!_viewOnly) Controls.Add(_saveButton);

        AcceptButton = _viewOnly ? cancelButton : _saveButton;
        CancelButton = cancelButton;
    }

    private void ConfigureInputs()
    {
        _maintenanceTypeComboBox.Items.AddRange(["Maintenance", "Repair", "Cleaning", "Inspection", "Body Work", "Engine Work", "Other"]);
        _maintenanceTypeComboBox.SelectedIndex = 0;

        _modeOfPaymentComboBox.Items.AddRange(TransactionConstants.ModeOfPayment.All);
        _modeOfPaymentComboBox.SelectedIndex = 0;

        _startDatePicker.Value = DateTime.Today;
        _endDatePicker.Value = DateTime.Today.AddDays(1);
        
        _startDatePicker.ValueChanged += (_, _) => {
            if (_endDatePicker.Value < _startDatePicker.Value) _endDatePicker.Value = _startDatePicker.Value;
            _endDatePicker.MinDate = _startDatePicker.Value;
        };

        if (_viewOnly)
        {
            _carComboBox.Enabled = false;
            _clientComboBox.Enabled = false;
            _maintenanceTypeComboBox.Enabled = false;
            _startDatePicker.Enabled = false;
            _endDatePicker.Enabled = false;
            _estimatedCostInput.Enabled = false;
            _amountPaidInput.Enabled = false;
            _modeOfPaymentComboBox.Enabled = false;
            _notesTextBox.ReadOnly = true;
        }
    }

    private async void MaintenanceTransactionDetailsForm_Load(object? sender, EventArgs e)
    {
        try {
            _cars = await _carService.GetAllCarsAsync();
            _clients = await _customerService.SearchCustomersAsync("", CustomerListFilter.OffsiteClients);

            _carComboBox.BeginUpdate();
            _carComboBox.Items.Clear();
            _carComboBox.Items.Add("Select a vehicle");
            foreach (var car in _cars.OrderBy(c => c.CarName)) 
                _carComboBox.Items.Add(new CarOption(car.CarId, car.CarName, car.PlateNumber));
            _carComboBox.SelectedIndex = 0;
            _carComboBox.EndUpdate();

            _clientComboBox.BeginUpdate();
            _clientComboBox.Items.Clear();
            _clientComboBox.Items.Add("Select a client");
            foreach (var client in _clients.OrderBy(c => c.CompanyName ?? c.FirstName))
                _clientComboBox.Items.Add(new ClientOption(client.CustomerId, client.CompanyName ?? $"{client.FirstName} {client.LastName}"));
            _clientComboBox.SelectedIndex = 0;
            _clientComboBox.EndUpdate();

            if (_existingTransaction is not null)
            {
                SelectCar(_existingTransaction.CarId);
                SelectClient(_existingTransaction.CustomerId);
            }
        } catch (Exception ex) { MessageBoxHelper.ShowError($"Failed to load data: {ex.Message}"); }
    }

    private void SelectCar(int carId)
    {
        for (int i = 1; i < _carComboBox.Items.Count; i++)
        {
            if (_carComboBox.Items[i] is CarOption option && option.CarId == carId) { _carComboBox.SelectedIndex = i; break; }
        }
    }

    private void SelectClient(int customerId)
    {
        for (int i = 1; i < _clientComboBox.Items.Count; i++)
        {
            if (_clientComboBox.Items[i] is ClientOption option && option.CustomerId == customerId) { _clientComboBox.SelectedIndex = i; break; }
        }
    }

    private void LoadTransaction(Transaction transaction)
    {
        _maintenanceTypeComboBox.SelectedItem = transaction.MaintenanceType;
        _startDatePicker.Value = transaction.StartDate;
        _endDatePicker.Value = transaction.EndDate;
        _estimatedCostInput.Value = transaction.TotalAmount;
        _amountPaidInput.Value = transaction.AmountPaid;
        _modeOfPaymentComboBox.SelectedItem = transaction.ModeOfPayment;
        _notesTextBox.Text = transaction.Notes;
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        if (_viewOnly) return;
        ClearValidation();

        if (_carComboBox.SelectedIndex <= 0) { SetError(_carComboBox, "Please select a vehicle."); return; }
        if (_clientComboBox.SelectedIndex <= 0) { SetError(_clientComboBox, "Please select a client."); return; }

        var request = new CreateMaintenanceTransactionRequest
        {
            CarId = ((CarOption)_carComboBox.SelectedItem!).CarId,
            CustomerId = ((ClientOption)_clientComboBox.SelectedItem!).CustomerId,
            MaintenanceType = _maintenanceTypeComboBox.SelectedItem?.ToString() ?? "Maintenance",
            StartDate = _startDatePicker.Value,
            EndDate = _endDatePicker.Value,
            EstimatedCost = _estimatedCostInput.Value,
            AmountPaid = _amountPaidInput.Value,
            ModeOfPayment = _modeOfPaymentComboBox.SelectedItem?.ToString() ?? "Cash",
            Notes = _notesTextBox.Text
        };

        try {
            _saveButton.Enabled = false;
            await _transactionService.CreateMaintenanceTransactionAsync(request);
            MessageBoxHelper.ShowSuccess("Maintenance transaction created successfully.");
            DialogResult = DialogResult.OK;
            Close();
        } catch (ValidationException ex) { ShowValidationErrors(ex.Errors); }
        catch (Exception ex) { MessageBoxHelper.ShowError($"Failed to create transaction: {ex.Message}"); }
        finally { _saveButton.Enabled = true; }
    }

    private void ShowValidationErrors(IEnumerable<ValidationFailure> errors)
    {
        _validationLabel.Text = errors.First().ErrorMessage;
        _validationLabel.Visible = true;
        foreach (var error in errors)
        {
            Control? target = error.PropertyName switch {
                "CarId" => _carComboBox,
                "CustomerId" => _clientComboBox,
                "EndDate" => _endDatePicker,
                _ => null
            };
            if (target != null) _errorProvider.SetError(target, error.ErrorMessage);
        }
    }

    private void ClearValidation() { _validationLabel.Visible = false; _errorProvider.Clear(); }
    private void SetError(Control c, string msg) { _errorProvider.SetError(c, msg); _validationLabel.Text = msg; _validationLabel.Visible = true; }

    private static ComboBox CreateComboBox(int width = 380) => new() { Width = width, DropDownStyle = ComboBoxStyle.DropDownList, Font = FontHelper.Regular(10F) };
    private static DateTimePicker CreateDatePicker() => new() { Width = 380, Format = DateTimePickerFormat.Short, Font = FontHelper.Regular(10F) };
    private static NumericUpDown CreateMoneyInput() { var n = new NumericUpDown { Width = 380, Minimum = 0, Maximum = 1000000, DecimalPlaces = 2, Font = FontHelper.Regular(10F) }; n.ThousandsSeparator = true; return n; }
    private static Panel CreateInputPanel(string labelText, Control input)
    {
        Panel p = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 10), BackColor = ThemeHelper.Surface };
        Label l = ControlFactory.CreateInputLabel(labelText); l.Location = new Point(0, 0); input.Location = new Point(0, 22);
        p.Controls.Add(l); p.Controls.Add(input); return p;
    }
    private static Button CreateSecondaryButton(string text, int w, int h) { var b = new Button { Text = text, Size = new Size(w, h), BackColor = ThemeHelper.Surface, ForeColor = ThemeHelper.TextPrimary, Font = FontHelper.SemiBold(), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = ThemeHelper.Border; return b; }

    private sealed record CarOption(int CarId, string CarName, string PlateNumber) { public override string ToString() => $"{CarName} ({PlateNumber})"; }
    private sealed record ClientOption(int CustomerId, string Name) { public override string ToString() => Name; }
}
