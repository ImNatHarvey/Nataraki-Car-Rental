using FluentValidation;
using FluentValidation.Results;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using FleetScheduleModel = NatarakiCarRental.Models.FleetSchedule;

namespace NatarakiCarRental.Forms.Transactions;

public enum TransactionFormMode
{
    Add,
    View
}

public sealed class TransactionDetailsForm : Form
{
    private readonly TransactionFormMode _mode;
    private readonly Transaction? _transaction;
    private readonly int _currentUserId;
    private readonly TransactionService _transactionService;
    private readonly FleetScheduleService _scheduleService;
    private readonly CarService _carService = new();
    private readonly CustomerService _customerService = new();
    private readonly ErrorProvider _errorProvider = new();
    private readonly Label _validationLabel = new();
    private readonly TabControl _flowTabs = new();
    private readonly ComboBox _reservationComboBox = CreateComboBox(610);
    private readonly Label _reservationSummaryLabel = CreateSummaryLabel();
    private readonly ComboBox _walkInCustomerComboBox = CreateComboBox();
    private readonly ComboBox _walkInCarComboBox = CreateComboBox();
    private readonly DateTimePicker _walkInStartDatePicker = CreateDatePicker();
    private readonly DateTimePicker _walkInEndDatePicker = CreateDatePicker();
    private readonly NumericUpDown _walkInDailyRateInput = CreateMoneyInput();
    private readonly Label _walkInTotalLabel = CreateSummaryLabel();
    private readonly ComboBox _modeOfPaymentComboBox = CreateComboBox();
    private readonly ComboBox _paymentStatusComboBox = CreateComboBox();
    private readonly TextBox _notesTextBox = new()
    {
        Width = 610,
        Height = 72,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Font = FontHelper.Regular(10F)
    };
    private readonly TableLayoutPanel _viewLayout = new();
    private IReadOnlyList<FleetScheduleModel> _eligibleReservations = [];
    private IReadOnlyList<Customer> _customers = [];
    private IReadOnlyList<Car> _cars = [];

    public TransactionDetailsForm(int currentUserId)
    {
        _mode = TransactionFormMode.Add;
        _currentUserId = currentUserId;
        _transactionService = new TransactionService(currentUserId);
        _scheduleService = new FleetScheduleService(currentUserId);
        InitializeForm();
        Load += TransactionDetailsForm_Load;
    }

    public TransactionDetailsForm(Transaction transaction)
    {
        _mode = TransactionFormMode.View;
        _transaction = transaction;
        _transactionService = new TransactionService();
        _scheduleService = new FleetScheduleService(currentUserId: null);
        InitializeForm();
        LoadViewTransaction(transaction);
    }

    private void InitializeForm()
    {
        Text = _mode == TransactionFormMode.View ? "View Transaction" : "Add Transaction";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = _mode == TransactionFormMode.View ? new Size(760, 520) : new Size(760, 640);
        BackColor = ThemeHelper.Surface;
        Font = FontHelper.Regular();
        ShowInTaskbar = false;

        _errorProvider.ContainerControl = this;
        _errorProvider.BlinkStyle = ErrorBlinkStyle.NeverBlink;

        Controls.Add(new Label
        {
            Text = Text,
            AutoSize = false,
            Location = new Point(32, 24),
            Size = new Size(300, 34),
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary
        });

        _validationLabel.AutoSize = false;
        _validationLabel.Location = new Point(34, 66);
        _validationLabel.Size = new Size(690, 24);
        _validationLabel.Font = FontHelper.SemiBold(9F);
        _validationLabel.ForeColor = ThemeHelper.Danger;
        _validationLabel.Visible = false;
        Controls.Add(_validationLabel);

        if (_mode == TransactionFormMode.View)
        {
            CreateViewLayout();
            return;
        }

        CreateAddLayout();
    }

    private void CreateAddLayout()
    {
        _flowTabs.Location = new Point(32, 98);
        _flowTabs.Size = new Size(696, 350);
        _flowTabs.Font = FontHelper.SemiBold(9F);
        _flowTabs.TabPages.Add(CreateReservationTab());
        _flowTabs.TabPages.Add(CreateWalkInTab());

        _modeOfPaymentComboBox.Items.AddRange(TransactionConstants.ModeOfPayment.All.Cast<object>().ToArray());
        _modeOfPaymentComboBox.SelectedItem = TransactionConstants.ModeOfPayment.Cash;
        _paymentStatusComboBox.Items.AddRange(TransactionConstants.PaymentStatus.All.Cast<object>().ToArray());
        _paymentStatusComboBox.SelectedItem = TransactionConstants.PaymentStatus.Unpaid;

        TableLayoutPanel footerLayout = new()
        {
            Location = new Point(32, 464),
            Size = new Size(696, 112),
            ColumnCount = 2,
            RowCount = 2
        };
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        footerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        footerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        footerLayout.Controls.Add(CreateInputPanel("Mode of Payment *", _modeOfPaymentComboBox), 0, 0);
        footerLayout.Controls.Add(CreateInputPanel("Payment Status *", _paymentStatusComboBox), 1, 0);
        footerLayout.Controls.Add(CreateInputPanel("Notes", _notesTextBox), 0, 1);
        footerLayout.SetColumnSpan(footerLayout.GetControlFromPosition(0, 1)!, 2);

        Button cancelButton = CreateSecondaryButton("Cancel", 110, 38);
        cancelButton.Location = new Point(484, 590);
        cancelButton.DialogResult = DialogResult.Cancel;

        Button saveButton = ControlFactory.CreatePrimaryButton("Create Transaction", 134, 38);
        saveButton.Location = new Point(594, 590);
        saveButton.Click += SaveButton_Click;

        Controls.Add(_flowTabs);
        Controls.Add(footerLayout);
        Controls.Add(cancelButton);
        Controls.Add(saveButton);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private TabPage CreateReservationTab()
    {
        TabPage tab = new("Create from Reservation") { BackColor = ThemeHelper.Surface };
        tab.Controls.Add(CreateInputPanel("Eligible Reservation *", _reservationComboBox, new Point(18, 18)));
        _reservationSummaryLabel.Location = new Point(18, 94);
        _reservationSummaryLabel.Size = new Size(642, 160);
        _reservationSummaryLabel.Text = "Select a pending or reserved reservation to view its details.";
        tab.Controls.Add(_reservationSummaryLabel);
        _reservationComboBox.SelectedIndexChanged += (_, _) => UpdateReservationSummary();
        return tab;
    }

    private TabPage CreateWalkInTab()
    {
        TabPage tab = new("Direct Walk-In") { BackColor = ThemeHelper.Surface };
        TableLayoutPanel layout = new()
        {
            Location = new Point(18, 18),
            Size = new Size(642, 250),
            ColumnCount = 2,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        for (int row = 0; row < 4; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        }

        layout.Controls.Add(CreateInputPanel("Customer", _walkInCustomerComboBox), 0, 0);
        layout.Controls.Add(CreateInputPanel("Car *", _walkInCarComboBox), 1, 0);
        layout.Controls.Add(CreateInputPanel("Start Date *", _walkInStartDatePicker), 0, 1);
        layout.Controls.Add(CreateInputPanel("End Date *", _walkInEndDatePicker), 1, 1);
        layout.Controls.Add(CreateInputPanel("Daily Rate *", _walkInDailyRateInput), 0, 2);
        layout.Controls.Add(CreateInputPanel("Calculated Total", _walkInTotalLabel), 1, 2);
        tab.Controls.Add(layout);

        _walkInCarComboBox.SelectedIndexChanged += (_, _) => ApplySelectedCarRate();
        _walkInStartDatePicker.ValueChanged += (_, _) => UpdateWalkInTotal();
        _walkInEndDatePicker.ValueChanged += (_, _) => UpdateWalkInTotal();
        _walkInDailyRateInput.ValueChanged += (_, _) => UpdateWalkInTotal();
        return tab;
    }

    private void CreateViewLayout()
    {
        _viewLayout.Location = new Point(32, 96);
        _viewLayout.Size = new Size(696, 340);
        _viewLayout.ColumnCount = 2;
        _viewLayout.RowCount = 7;
        _viewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _viewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        for (int row = 0; row < 7; row++)
        {
            _viewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        }

        Button closeButton = CreateSecondaryButton("Close", 110, 38);
        closeButton.Location = new Point(618, 462);
        closeButton.DialogResult = DialogResult.Cancel;
        Controls.Add(_viewLayout);
        Controls.Add(closeButton);
        CancelButton = closeButton;
    }

    private async void TransactionDetailsForm_Load(object? sender, EventArgs e)
    {
        Load -= TransactionDetailsForm_Load;

        try
        {
            _eligibleReservations = await _scheduleService.GetEligibleReservationsAsync(DateTime.Today);
            _customers = await _customerService.SearchCustomersAsync(string.Empty, CustomerListFilter.Active);
            _cars = await _carService.GetActiveCarsAsync();
            PopulateLookups();
            UpdateReservationSummary();
            UpdateWalkInTotal();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to load transaction form data.\n\n{exception.Message}", "Transactions");
            Close();
        }
    }

    private void PopulateLookups()
    {
        _reservationComboBox.Items.Clear();
        _reservationComboBox.Items.Add(new LookupOption(null, "Select a reservation"));
        _reservationComboBox.Items.AddRange(_eligibleReservations
            .Select(schedule => new LookupOption(schedule.ScheduleId, $"{schedule.Title} - {schedule.CarName} ({schedule.PlateNumber})"))
            .Cast<object>()
            .ToArray());
        _reservationComboBox.SelectedIndex = 0;

        _walkInCustomerComboBox.Items.Clear();
        _walkInCustomerComboBox.Items.Add(new LookupOption(null, "Use Walk-In Customer"));
        _walkInCustomerComboBox.Items.AddRange(_customers
            .Select(customer => new LookupOption(customer.CustomerId, $"{customer.FirstName} {customer.LastName}".Trim()))
            .Cast<object>()
            .ToArray());
        _walkInCustomerComboBox.SelectedIndex = 0;

        _walkInCarComboBox.Items.Clear();
        _walkInCarComboBox.Items.Add(new LookupOption(null, "Select a car"));
        _walkInCarComboBox.Items.AddRange(_cars
            .Select(car => new LookupOption(car.CarId, $"{car.CarName} ({car.PlateNumber})"))
            .Cast<object>()
            .ToArray());
        _walkInCarComboBox.SelectedIndex = 0;
    }

    private void UpdateReservationSummary()
    {
        FleetScheduleModel? schedule = GetSelectedReservation();
        if (schedule is null)
        {
            _reservationSummaryLabel.Text = "Select a pending or reserved reservation to view its details.";
            return;
        }

        Car? car = _cars.FirstOrDefault(item => item.CarId == schedule.CarId);
        int totalDays = (schedule.EndDate.Date - schedule.StartDate.Date).Days + 1;
        decimal rate = car?.RatePerDay ?? 0;
        decimal total = rate * totalDays;
        _reservationSummaryLabel.Text =
            $"Customer: {schedule.CustomerName ?? "-"}{Environment.NewLine}" +
            $"Car: {schedule.CarName} ({schedule.PlateNumber}){Environment.NewLine}" +
            $"Dates: {schedule.StartDate:MMM d, yyyy} - {schedule.EndDate:MMM d, yyyy}{Environment.NewLine}" +
            $"Daily Rate: {rate:C}{Environment.NewLine}" +
            $"Total Days: {totalDays}{Environment.NewLine}" +
            $"Total Amount: {total:C}";
    }

    private void ApplySelectedCarRate()
    {
        Car? car = GetSelectedCar();
        if (car is not null)
        {
            _walkInDailyRateInput.Value = Math.Min(_walkInDailyRateInput.Maximum, car.RatePerDay);
        }
        UpdateWalkInTotal();
    }

    private void UpdateWalkInTotal()
    {
        int days = Math.Max((_walkInEndDatePicker.Value.Date - _walkInStartDatePicker.Value.Date).Days + 1, 0);
        decimal total = days * _walkInDailyRateInput.Value;
        _walkInTotalLabel.Text = days == 0 ? "-" : $"{days} day(s) / {total:C}";
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button saveButton)
        {
            return;
        }

        try
        {
            saveButton.Enabled = false;
            ClearValidationState();

            if (_flowTabs.SelectedIndex == 0)
            {
                FleetScheduleModel? reservation = GetSelectedReservation();
                if (reservation is null)
                {
                    throw Validation(nameof(CreateTransactionFromReservationRequest.FleetScheduleId), "Select a reservation before saving.");
                }

                await _transactionService.CreateFromReservationAsync(
                    new CreateTransactionFromReservationRequest
                    {
                        FleetScheduleId = reservation.ScheduleId,
                        ModeOfPayment = GetSelectedText(_modeOfPaymentComboBox),
                        PaymentStatus = GetSelectedText(_paymentStatusComboBox),
                        Notes = _notesTextBox.Text
                    });
            }
            else
            {
                await _transactionService.CreateWalkInTransactionAsync(
                    new CreateWalkInTransactionRequest
                    {
                        CustomerId = GetSelectedLookupId(_walkInCustomerComboBox),
                        CarId = GetSelectedLookupId(_walkInCarComboBox) ?? 0,
                        StartDate = _walkInStartDatePicker.Value.Date,
                        EndDate = _walkInEndDatePicker.Value.Date,
                        DailyRate = _walkInDailyRateInput.Value,
                        ModeOfPayment = GetSelectedText(_modeOfPaymentComboBox),
                        PaymentStatus = GetSelectedText(_paymentStatusComboBox),
                        Notes = _notesTextBox.Text
                    });
            }

            MessageBoxHelper.ShowSuccess("Transaction created successfully.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException exception)
        {
            ShowValidationErrors(exception.Errors.ToList(), exception.Message);
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to save transaction.\n\n{exception.Message}", "Transactions");
        }
        finally
        {
            saveButton.Enabled = true;
        }
    }

    private void LoadViewTransaction(Transaction transaction)
    {
        AddViewRow(0, "Transaction Code", transaction.TransactionCode, "Customer", transaction.CustomerName);
        AddViewRow(1, "Car / Plate", $"{transaction.CarName} ({transaction.PlateNumber})", "Schedule Reference", $"#{transaction.FleetScheduleId}");
        AddViewRow(2, "Date Range", $"{transaction.StartDate:MMM d, yyyy} - {transaction.EndDate:MMM d, yyyy}", "Created", transaction.CreatedAt.ToString("MMM d, yyyy h:mm tt"));
        AddViewRow(3, "Daily Rate", transaction.DailyRate.ToString("C"), "Total Days", transaction.TotalDays.ToString());
        AddViewRow(4, "Total Amount", transaction.TotalAmount.ToString("C"), "Mode of Payment", transaction.ModeOfPayment);
        AddViewRow(5, "Payment Status", transaction.PaymentStatus, "Transaction Status", transaction.TransactionStatus);
        AddViewRow(6, "Notes", string.IsNullOrWhiteSpace(transaction.Notes) ? "-" : transaction.Notes, string.Empty, string.Empty);
    }

    private void AddViewRow(int row, string leftLabel, string leftValue, string rightLabel, string rightValue)
    {
        _viewLayout.Controls.Add(CreateReadOnlyValue(leftLabel, leftValue), 0, row);
        if (!string.IsNullOrWhiteSpace(rightLabel))
        {
            _viewLayout.Controls.Add(CreateReadOnlyValue(rightLabel, rightValue), 1, row);
        }
    }

    private void ShowValidationErrors(IReadOnlyList<ValidationFailure> errors, string fallbackMessage)
    {
        string message = string.Join(Environment.NewLine, errors.Select(error => error.ErrorMessage));
        if (string.IsNullOrWhiteSpace(message))
        {
            message = fallbackMessage;
        }

        _validationLabel.Text = message.Split(Environment.NewLine).FirstOrDefault() ?? message;
        _validationLabel.Visible = true;
        foreach (ValidationFailure error in errors)
        {
            Control? control = error.PropertyName switch
            {
                nameof(CreateTransactionFromReservationRequest.FleetScheduleId) => _reservationComboBox,
                nameof(Transaction.CustomerId) => _walkInCustomerComboBox,
                nameof(Transaction.CarId) => _walkInCarComboBox,
                nameof(Transaction.StartDate) => _walkInStartDatePicker,
                nameof(Transaction.EndDate) => _walkInEndDatePicker,
                nameof(Transaction.DailyRate) => _walkInDailyRateInput,
                nameof(Transaction.ModeOfPayment) => _modeOfPaymentComboBox,
                nameof(Transaction.PaymentStatus) => _paymentStatusComboBox,
                _ => null
            };
            if (control is not null)
            {
                _errorProvider.SetError(control, error.ErrorMessage);
            }
        }
        MessageBoxHelper.ShowError(message, "Validation Error");
    }

    private void ClearValidationState()
    {
        _validationLabel.Visible = false;
        _errorProvider.Clear();
    }

    private FleetScheduleModel? GetSelectedReservation()
    {
        int? scheduleId = GetSelectedLookupId(_reservationComboBox);
        return scheduleId.HasValue
            ? _eligibleReservations.FirstOrDefault(schedule => schedule.ScheduleId == scheduleId.Value)
            : null;
    }

    private Car? GetSelectedCar()
    {
        int? carId = GetSelectedLookupId(_walkInCarComboBox);
        return carId.HasValue ? _cars.FirstOrDefault(car => car.CarId == carId.Value) : null;
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

    private static Panel CreateReadOnlyValue(string labelText, string value)
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.Surface };
        panel.Controls.Add(new Label
        {
            Text = labelText,
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(320, 18),
            Font = FontHelper.SemiBold(9F),
            ForeColor = ThemeHelper.TextSecondary
        });
        panel.Controls.Add(new Label
        {
            Text = value,
            AutoSize = false,
            Location = new Point(0, 19),
            Size = new Size(320, 22),
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextPrimary,
            AutoEllipsis = true
        });
        return panel;
    }

    private static ComboBox CreateComboBox(int width = 280)
    {
        return new ComboBox
        {
            Width = width,
            Height = 30,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = FontHelper.Regular(10F)
        };
    }

    private static DateTimePicker CreateDatePicker()
    {
        return new DateTimePicker
        {
            Width = 280,
            Height = 30,
            Format = DateTimePickerFormat.Short,
            Font = FontHelper.Regular(10F)
        };
    }

    private static NumericUpDown CreateMoneyInput()
    {
        return new NumericUpDown
        {
            Width = 280,
            Height = 30,
            DecimalPlaces = 2,
            Maximum = 1000000,
            ThousandsSeparator = true,
            Font = FontHelper.Regular(10F)
        };
    }

    private static Label CreateSummaryLabel()
    {
        return new Label
        {
            AutoSize = false,
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(248, 250, 252)
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

    private static int? GetSelectedLookupId(ComboBox comboBox)
    {
        return comboBox.SelectedItem is LookupOption option ? option.Id : null;
    }

    private static string GetSelectedText(ComboBox comboBox)
    {
        return comboBox.SelectedItem?.ToString() ?? string.Empty;
    }

    private static ValidationException Validation(string propertyName, string message)
    {
        return new ValidationException([new ValidationFailure(propertyName, message)]);
    }

    private sealed record LookupOption(int? Id, string Name)
    {
        public override string ToString() => Name;
    }
}
