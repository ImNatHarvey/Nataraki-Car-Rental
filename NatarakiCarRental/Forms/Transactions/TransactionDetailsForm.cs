using FluentValidation;
using FluentValidation.Results;
using System.Diagnostics;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using FleetScheduleModel = NatarakiCarRental.Models.FleetSchedule;

namespace NatarakiCarRental.Forms.Transactions;

public enum TransactionFormMode
{
    Add,
    Edit,
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
    private readonly NumericUpDown _amountPaidInput = CreateMoneyInput();
    private readonly Label _balanceLabel = CreateSummaryLabel();
    private readonly CheckBox _useWalkInCustomerCheckBox = new() { Text = "Use Walk-In Customer", AutoSize = true };
    private readonly TextBox _walkInFirstNameTextBox = ControlFactory.CreateTextBox();
    private readonly TextBox _walkInLastNameTextBox = ControlFactory.CreateTextBox();
    private readonly ComboBox _modeOfPaymentComboBox = CreateComboBox();
    private readonly TextBox _notesTextBox = new()
    {
        Width = 812,
        Height = 86,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Font = FontHelper.Regular(10F)
    };
    private readonly TableLayoutPanel _viewLayout = new();
    private readonly DataGridView _paymentsGrid = new();
    private readonly Panel _addPaymentPanel = new();
    private readonly NumericUpDown _newPaymentAmountInput = CreateMoneyInput();
    private readonly ComboBox _newPaymentModeComboBox = CreateComboBox();
    private readonly TextBox _newPaymentNotesTextBox = new()
    {
        Width = 250,
        Height = 54,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Font = FontHelper.Regular(10F)
    };
    private readonly Label _initialProofPathLabel = CreatePathLabel();
    private readonly Label _paymentProofPathLabel = CreatePathLabel();
    private readonly Button _initialProofBrowseButton = CreateSecondaryButton("Browse", 90, 30);
    private readonly Button _initialProofOpenButton = CreateSecondaryButton("Open File", 90, 30);
    private readonly Button _paymentProofBrowseButton = CreateSecondaryButton("Browse", 90, 30);
    private readonly Button _paymentProofOpenButton = CreateSecondaryButton("Open File", 90, 30);
    private readonly Button _submitPaymentButton = ControlFactory.CreatePrimaryButton("Submit Payment", 148, 38);
    private string? _selectedReceiptSourcePath;
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

    public TransactionDetailsForm(Transaction transaction, int currentUserId)
    {
        _mode = TransactionFormMode.Edit;
        _transaction = transaction;
        _currentUserId = currentUserId;
        _transactionService = new TransactionService(currentUserId);
        _scheduleService = new FleetScheduleService(currentUserId);
        InitializeForm();
        LoadEditTransaction(transaction);
    }

    private void InitializeForm()
    {
        Text = _mode switch
        {
            TransactionFormMode.View => "View Transaction",
            TransactionFormMode.Edit => "Edit Transaction",
            _ => "Add Transaction"
        };
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(1060, 880);
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
        _validationLabel.Size = new Size(996, 24);
        _validationLabel.Font = FontHelper.SemiBold(9F);
        _validationLabel.ForeColor = ThemeHelper.Danger;
        _validationLabel.Visible = false;
        Controls.Add(_validationLabel);

        if (_mode == TransactionFormMode.View)
        {
            CreateViewLayout();
            return;
        }

        if (_mode == TransactionFormMode.Edit)
        {
            CreateEditLayout();
            return;
        }

        CreateAddLayout();
    }

    private void CreateAddLayout()
    {
        _flowTabs.Dock = DockStyle.Fill;
        _flowTabs.Font = FontHelper.SemiBold(9F);
        _flowTabs.TabPages.Add(CreateReservationTab());
        _flowTabs.TabPages.Add(CreateWalkInTab());
        GroupBox rentalSection = CreateSection("Rental Information", _flowTabs);
        rentalSection.Location = new Point(32, 98);
        rentalSection.Size = new Size(996, 400);
        rentalSection.Dock = DockStyle.None;

        _modeOfPaymentComboBox.Items.AddRange(TransactionConstants.ModeOfPayment.All.Cast<object>().ToArray());
        _modeOfPaymentComboBox.SelectedItem = TransactionConstants.ModeOfPayment.Cash;

        ConfigureProofPicker(_initialProofBrowseButton, _initialProofOpenButton, _initialProofPathLabel);
        _amountPaidInput.ValueChanged += (_, _) => UpdateBalanceLabel();

        TableLayoutPanel footerLayout = new()
        {
            Location = new Point(32, 514),
            Size = new Size(996, 250),
            ColumnCount = 2,
            RowCount = 2
        };
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        footerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118F));
        footerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132F));

        footerLayout.Controls.Add(CreateSection("Payment Information", CreateInitialPaymentLayout()), 0, 0);
        footerLayout.SetColumnSpan(footerLayout.GetControlFromPosition(0, 0)!, 2);
        footerLayout.Controls.Add(CreateSection("Notes", CreateNotesLayout()), 0, 1);
        footerLayout.SetColumnSpan(footerLayout.GetControlFromPosition(0, 1)!, 2);

        Button cancelButton = CreateSecondaryButton("Cancel", 110, 38);
        cancelButton.Location = new Point(756, 800);
        cancelButton.DialogResult = DialogResult.Cancel;

        Button saveButton = ControlFactory.CreatePrimaryButton("Create Transaction", 164, 38);
        saveButton.Location = new Point(888, 800);
        saveButton.Click += SaveButton_Click;

        Controls.Add(rentalSection);
        Controls.Add(footerLayout);
        Controls.Add(cancelButton);
        Controls.Add(saveButton);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private TableLayoutPanel CreateInitialPaymentLayout()
    {
        TableLayoutPanel layout = CreateGrid(2, 1);
        TableLayoutPanel paymentFields = CreateGrid(2, 1);
        paymentFields.Controls.Add(CreateInputPanel("Mode of Payment *", _modeOfPaymentComboBox), 0, 0);
        paymentFields.Controls.Add(CreateInputPanel("Amount Paid *", _amountPaidInput), 1, 0);
        layout.Controls.Add(paymentFields, 0, 0);
        layout.Controls.Add(CreateProofPickerPanel("Payment Proof", _initialProofPathLabel, _initialProofBrowseButton, _initialProofOpenButton), 1, 0);
        return layout;
    }

    private Panel CreateNotesLayout()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.Surface };
        _notesTextBox.Width = 930;
        _notesTextBox.Height = 78;
        _notesTextBox.Location = new Point(0, 0);
        panel.Controls.Add(_notesTextBox);
        return panel;
    }

    private TabPage CreateReservationTab()
    {
        TabPage tab = new("Create from Reservation") { BackColor = ThemeHelper.Surface };
        _reservationComboBox.Width = 920;
        tab.Controls.Add(CreateInputPanel("Eligible Reservation *", _reservationComboBox, new Point(18, 18)));
        _reservationSummaryLabel.Location = new Point(18, 94);
        _reservationSummaryLabel.Size = new Size(920, 230);
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
            Size = new Size(920, 330),
            ColumnCount = 2,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));

        layout.Controls.Add(CreateInputPanel("Customer", _walkInCustomerComboBox), 0, 0);
        layout.Controls.Add(CreateInputPanel("Car *", _walkInCarComboBox), 1, 0);
        layout.Controls.Add(CreateInputPanel("Start Date *", _walkInStartDatePicker), 0, 1);
        layout.Controls.Add(CreateInputPanel("End Date *", _walkInEndDatePicker), 1, 1);
        layout.Controls.Add(CreateInputPanel("Daily Rate", _walkInDailyRateInput), 0, 2);
        _walkInDailyRateInput.Enabled = false;
        _walkInTotalLabel.BorderStyle = BorderStyle.None;
        _walkInTotalLabel.BackColor = ThemeHelper.Surface;
        _walkInTotalLabel.Padding = new Padding(0, 2, 0, 0);
        layout.Controls.Add(CreateInputPanel("Rental Summary", _walkInTotalLabel), 1, 2);
        Panel checkboxPanel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.Surface };
        _useWalkInCustomerCheckBox.Location = new Point(0, 6);
        checkboxPanel.Controls.Add(_useWalkInCustomerCheckBox);
        layout.Controls.Add(checkboxPanel, 0, 3);
        layout.SetColumnSpan(checkboxPanel, 2);

        _walkInFirstNameTextBox.Width = 280;
        _walkInLastNameTextBox.Width = 280;
        _walkInFirstNameTextBox.PlaceholderText = "First name";
        _walkInLastNameTextBox.PlaceholderText = "Last name";
        layout.Controls.Add(CreateInputPanel("First Name", _walkInFirstNameTextBox), 0, 4);
        layout.Controls.Add(CreateInputPanel("Last Name", _walkInLastNameTextBox), 1, 4);
        tab.Controls.Add(layout);

        _walkInCarComboBox.SelectedIndexChanged += (_, _) => ApplySelectedCarRate();
        _walkInStartDatePicker.ValueChanged += (_, _) => UpdateWalkInTotal();
        _walkInEndDatePicker.ValueChanged += (_, _) => UpdateWalkInTotal();
        _walkInDailyRateInput.ValueChanged += (_, _) => UpdateWalkInTotal();
        _useWalkInCustomerCheckBox.CheckedChanged += (_, _) => UpdateWalkInInputs();
        UpdateWalkInInputs();
        return tab;
    }

    private void CreateEditLayout()
    {
        CreateCommonDetailsLayout();
        CreatePaymentsGrid();

        if (_transaction?.TransactionStatus == TransactionConstants.Status.Active)
        {
            CreateAddPaymentSection();
            _submitPaymentButton.Location = new Point(880, 800);
            _submitPaymentButton.Click += AddPaymentButton_Click;
            Controls.Add(_submitPaymentButton);
        }

        Button closeButton = CreateSecondaryButton("Close", 110, 38);
        closeButton.Location = _transaction?.TransactionStatus == TransactionConstants.Status.Active
            ? new Point(758, 800)
            : new Point(918, 800);
        closeButton.Click += (_, _) => Close();
        Controls.Add(closeButton);
        CancelButton = closeButton;
    }

    private void CreateViewLayout()
    {
        CreateCommonDetailsLayout();
        CreatePaymentsGrid();

        Button closeButton = CreateSecondaryButton("Close", 110, 38);
        closeButton.Location = new Point(918, 800);
        closeButton.DialogResult = DialogResult.Cancel;
        Controls.Add(closeButton);
        CancelButton = closeButton;
    }

    private void CreateCommonDetailsLayout()
    {
        _viewLayout.Dock = DockStyle.Fill;
        _viewLayout.ColumnCount = 2;
        _viewLayout.RowCount = 6;
        _viewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _viewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        for (int row = 0; row < 6; row++)
        {
            _viewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        }
        GroupBox detailsSection = CreateSection("Transaction Information", _viewLayout);
        detailsSection.Location = new Point(32, 90);
        detailsSection.Size = new Size(996, 292);
        detailsSection.Dock = DockStyle.None;
        Controls.Add(detailsSection);
    }

    private void CreatePaymentsGrid()
    {
        Panel paymentPanel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.Surface
        };

        _paymentsGrid.Dock = DockStyle.Fill;
        _paymentsGrid.AllowUserToAddRows = false;
        _paymentsGrid.AllowUserToDeleteRows = false;
        _paymentsGrid.AllowUserToResizeRows = false;
        _paymentsGrid.ReadOnly = true;
        _paymentsGrid.RowHeadersVisible = false;
        _paymentsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _paymentsGrid.BackgroundColor = ThemeHelper.Surface;
        _paymentsGrid.BorderStyle = BorderStyle.FixedSingle;
        _paymentsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _paymentsGrid.ColumnHeadersHeight = 30;
        _paymentsGrid.RowTemplate.Height = 30;
        _paymentsGrid.Font = FontHelper.Regular(9F);
        _paymentsGrid.CellContentClick += PaymentsGrid_CellContentClick;

        _paymentsGrid.Columns.Add("Date", "Date");
        _paymentsGrid.Columns.Add("Amount", "Amount");
        _paymentsGrid.Columns.Add("Mode", "Mode");
        _paymentsGrid.Columns.Add("Notes", "Notes");
        _paymentsGrid.Columns.Add("ReceiptFilePath", "ReceiptFilePath");
        _paymentsGrid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "ProofAction",
            HeaderText = "Proof",
            Text = "View",
            UseColumnTextForButtonValue = true,
            FlatStyle = FlatStyle.Flat,
            Width = 60
        });

        _paymentsGrid.Columns["Date"]!.FillWeight = 90;
        _paymentsGrid.Columns["Amount"]!.FillWeight = 80;
        _paymentsGrid.Columns["Mode"]!.FillWeight = 80;
        _paymentsGrid.Columns["Notes"]!.FillWeight = 120;
        _paymentsGrid.Columns["ReceiptFilePath"]!.Visible = false;
        _paymentsGrid.Columns["ProofAction"]!.FillWeight = 60;

        paymentPanel.Controls.Add(_paymentsGrid);
        GroupBox section = CreateSection("Payment History", paymentPanel);
        section.Location = new Point(32, 404);
        section.Size = new Size(996, 156);
        section.Dock = DockStyle.None;
        Controls.Add(section);
    }

    private void CreateAddPaymentSection()
    {
        _addPaymentPanel.Dock = DockStyle.Fill;
        _addPaymentPanel.BackColor = ThemeHelper.Surface;

        _newPaymentModeComboBox.Width = 150;
        _newPaymentModeComboBox.Items.AddRange(TransactionConstants.ModeOfPayment.All.Cast<object>().ToArray());
        _newPaymentModeComboBox.SelectedItem = TransactionConstants.ModeOfPayment.Cash;

        _newPaymentAmountInput.Width = 140;
        _newPaymentNotesTextBox.Width = 930;
        _newPaymentNotesTextBox.Height = 54;

        ConfigureProofPicker(_paymentProofBrowseButton, _paymentProofOpenButton, _paymentProofPathLabel);

        _addPaymentPanel.Controls.Add(CreateInputPanel("Amount *", _newPaymentAmountInput, new Point(0, 0)));
        _addPaymentPanel.Controls.Add(CreateInputPanel("Mode *", _newPaymentModeComboBox, new Point(170, 0)));
        Panel proofPanel = CreateProofPickerPanel("Payment Proof", _paymentProofPathLabel, _paymentProofBrowseButton, _paymentProofOpenButton);
        proofPanel.Location = new Point(360, 0);
        proofPanel.Size = new Size(570, 70);
        _addPaymentPanel.Controls.Add(proofPanel);
        _addPaymentPanel.Controls.Add(CreateInputPanel("Notes", _newPaymentNotesTextBox, new Point(0, 80)));

        GroupBox section = CreateSection("Add New Payment", _addPaymentPanel);
        section.Location = new Point(32, 578);
        section.Size = new Size(996, 190);
        section.Dock = DockStyle.None;
        Controls.Add(section);
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

            if (_transaction is not null)
            {
                await LoadPaymentsAsync();
            }
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to load transaction form data.\n\n{exception.Message}", "Transactions");
            Close();
        }
    }

    private async Task LoadPaymentsAsync()
    {
        if (_transaction is null) return;
        try
        {
            IReadOnlyList<TransactionPaymentListItem> payments = await _transactionService.GetPaymentsAsync(_transaction.TransactionId);
            _paymentsGrid.Rows.Clear();
            foreach (TransactionPaymentListItem payment in payments)
            {
                _paymentsGrid.Rows.Add(
                    payment.PaymentDate.ToString("MMM d, yyyy h:mm tt"),
                    FormatPeso(payment.Amount),
                    payment.ModeOfPayment,
                    payment.Notes ?? "-",
                    payment.ReceiptFilePath ?? string.Empty,
                    "Open File");
            }
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to load payment history.\n\n{exception.Message}", "Transactions");
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
        _walkInCustomerComboBox.Items.AddRange(_customers
            .Select(customer => new LookupOption(customer.CustomerId, $"{customer.FirstName} {customer.LastName}".Trim()))
            .Cast<object>()
            .ToArray());
        if (_walkInCustomerComboBox.Items.Count > 0) _walkInCustomerComboBox.SelectedIndex = 0;

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
            _reservationSummaryLabel.Text = "Select a car and date range to calculate total.";
            _amountPaidInput.Maximum = 0;
            UpdateBalanceLabel();
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
            $"Daily Rate: ₱{rate:N2}{Environment.NewLine}" +
            $"Total Days: {totalDays}{Environment.NewLine}" +
            $"Total Amount: ₱{total:N2}";
        _amountPaidInput.Maximum = total;
        UpdateBalanceLabel();
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
        if (GetSelectedCar() is null)
        {
            _walkInTotalLabel.Text = "Select a car and date range to calculate total.";
        }
        else
        {
            _walkInTotalLabel.Text =
                $"Daily Rate: ₱{_walkInDailyRateInput.Value:N2}{Environment.NewLine}" +
                $"Total Days: {days}{Environment.NewLine}" +
                $"Total Amount: ₱{total:N2}";
        }
        _amountPaidInput.Maximum = Math.Max(total, 0);
        UpdateBalanceLabel();
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button saveButton)
        {
            return;
        }

        string? newReceiptPath = null;
        try
        {
            saveButton.Enabled = false;
            ClearValidationState();

            newReceiptPath = UploadPathHelper.SavePaymentReceiptIfSelected(_selectedReceiptSourcePath, null);

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
                        AmountPaid = _amountPaidInput.Value,
                        ReceiptFilePath = newReceiptPath,
                        Notes = _notesTextBox.Text
                    });
            }
            else
            {
                await _transactionService.CreateWalkInTransactionAsync(
                    new CreateWalkInTransactionRequest
                    {
                        CustomerId = _useWalkInCustomerCheckBox.Checked ? null : GetSelectedLookupId(_walkInCustomerComboBox),
                        CarId = GetSelectedLookupId(_walkInCarComboBox) ?? 0,
                        StartDate = _walkInStartDatePicker.Value.Date,
                        EndDate = _walkInEndDatePicker.Value.Date,
                        DailyRate = _walkInDailyRateInput.Value,
                        AmountPaid = _amountPaidInput.Value,
                        ModeOfPayment = GetSelectedText(_modeOfPaymentComboBox),
                        ReceiptFilePath = newReceiptPath,
                        WalkInFirstName = _useWalkInCustomerCheckBox.Checked ? _walkInFirstNameTextBox.Text : null,
                        WalkInLastName = _useWalkInCustomerCheckBox.Checked ? _walkInLastNameTextBox.Text : null,
                        Notes = _notesTextBox.Text
                    });
            }

            MessageBoxHelper.ShowSuccess("Transaction created successfully.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException exception)
        {
            UploadPathHelper.DeleteNewPaymentReceiptIfSaveFailed(newReceiptPath, null);
            ShowValidationErrors(exception.Errors.ToList(), exception.Message);
        }
        catch (Exception exception)
        {
            UploadPathHelper.DeleteNewPaymentReceiptIfSaveFailed(newReceiptPath, null);
            MessageBoxHelper.ShowError($"Unable to save transaction.\n\n{exception.Message}", "Transactions");
        }
        finally
        {
            saveButton.Enabled = true;
        }
    }

    private void LoadViewTransaction(Transaction transaction)
    {
        _viewLayout.Controls.Clear();
        AddViewRow(0, "Transaction Code", transaction.TransactionCode, "Customer", transaction.CustomerName);
        AddViewRow(1, "Car / Plate", $"{transaction.CarName} ({transaction.PlateNumber})", "Schedule Reference", $"#{transaction.FleetScheduleId}");
        AddViewRow(2, "Date Range", $"{transaction.StartDate:MMM d, yyyy} - {transaction.EndDate:MMM d, yyyy}", "Created", transaction.CreatedAt.ToString("MMM d, yyyy h:mm tt"));
        AddViewRow(3, "Total Amount", FormatPeso(transaction.TotalAmount), "Mode of Payment", transaction.ModeOfPayment);
        AddViewRow(4, "Amount Paid", FormatPeso(transaction.AmountPaid), "Balance", FormatPeso(transaction.BalanceAmount));
        AddViewRow(5, "Payment Status", transaction.PaymentStatus, "Transaction Status", transaction.TransactionStatus);
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
                nameof(Transaction.AmountPaid) => _amountPaidInput,
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

    private void LoadEditTransaction(Transaction transaction)
    {
        _modeOfPaymentComboBox.SelectedItem = transaction.ModeOfPayment;
        _amountPaidInput.Maximum = transaction.TotalAmount;
        _amountPaidInput.Value = transaction.AmountPaid;
        _notesTextBox.Text = transaction.Notes ?? string.Empty;
        _submitPaymentButton.Enabled = transaction.BalanceAmount > 0;
        UpdateBalanceLabel();
        LoadViewTransaction(transaction);
    }

    private void ConfigureProofPicker(Button browseButton, Button openButton, Label pathLabel)
    {
        openButton.Enabled = false;
        browseButton.Click += (_, _) => BrowseProofFile(pathLabel, openButton);
        openButton.Click += (_, _) => OpenProofFile(_selectedReceiptSourcePath, null);
    }

    private void BrowseProofFile(Label pathLabel, Button openButton)
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.webp|PDF Files|*.pdf|All Files|*.*",
            Title = "Select payment proof"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _selectedReceiptSourcePath = dialog.FileName;
            pathLabel.Text = Path.GetFileName(dialog.FileName);
            pathLabel.ForeColor = ThemeHelper.Primary;
            openButton.Enabled = true;
        }
    }

    private static void OpenProofFile(string? selectedSourcePath, string? storedPath)
    {
        string? path = !string.IsNullOrWhiteSpace(selectedSourcePath) && File.Exists(selectedSourcePath)
            ? selectedSourcePath
            : UploadPathHelper.ResolvePaymentReceiptPath(storedPath);

        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBoxHelper.ShowWarning("The payment proof file could not be found.", "Attachment Missing");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to open payment proof.\n\n{exception.Message}", "Open File");
        }
    }

    private async void AddPaymentButton_Click(object? sender, EventArgs e)
    {
        if (_transaction is null || sender is not Button addButton) return;

        string? newReceiptPath = null;
        try
        {
            addButton.Enabled = false;
            ClearValidationState();

            newReceiptPath = UploadPathHelper.SavePaymentReceiptIfSelected(_selectedReceiptSourcePath, null);

            await _transactionService.AddPaymentAsync(new AddTransactionPaymentRequest
            {
                TransactionId = _transaction.TransactionId,
                Amount = _newPaymentAmountInput.Value,
                ModeOfPayment = GetSelectedText(_newPaymentModeComboBox),
                ReferenceNumber = null,
                ReceiptFilePath = newReceiptPath,
                Notes = _newPaymentNotesTextBox.Text
            }, _currentUserId);

            MessageBoxHelper.ShowSuccess("Payment added successfully.");
            
            // Reset input
            _newPaymentAmountInput.Value = 0;
            _newPaymentNotesTextBox.Clear();
            _selectedReceiptSourcePath = null;
            _paymentProofPathLabel.Text = "No file selected";
            _paymentProofPathLabel.ForeColor = ThemeHelper.TextSecondary;
            _paymentProofOpenButton.Enabled = false;

            // Refresh data
            Transaction? updated = await _transactionService.GetByIdAsync(_transaction.TransactionId);
            if (updated is not null)
            {
                LoadViewTransaction(updated);
                _submitPaymentButton.Enabled = updated.BalanceAmount > 0;
            }
            await LoadPaymentsAsync();
        }
        catch (ValidationException exception)
        {
            UploadPathHelper.DeleteNewPaymentReceiptIfSaveFailed(newReceiptPath, null);
            ShowValidationErrors(exception.Errors.ToList(), exception.Message);
        }
        catch (Exception exception)
        {
            UploadPathHelper.DeleteNewPaymentReceiptIfSaveFailed(newReceiptPath, null);
            MessageBoxHelper.ShowError($"Unable to save payment.\n\n{exception.Message}", "Transactions");
        }
        finally
        {
            addButton.Enabled = true;
        }
    }

    private void PaymentsGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        if (_paymentsGrid.Columns[e.ColumnIndex].Name == "ProofAction")
        {
            string? receiptPath = _paymentsGrid.Rows[e.RowIndex].Cells["ReceiptFilePath"].Value?.ToString();
            if (string.IsNullOrWhiteSpace(receiptPath))
            {
                MessageBoxHelper.ShowInfo("No proof receipt uploaded for this payment.");
                return;
            }

            OpenProofFile(null, receiptPath);
        }
    }

    private void UpdateWalkInInputs()
    {
        _walkInCustomerComboBox.Enabled = !_useWalkInCustomerCheckBox.Checked;
        _walkInFirstNameTextBox.Enabled = _useWalkInCustomerCheckBox.Checked;
        _walkInLastNameTextBox.Enabled = _useWalkInCustomerCheckBox.Checked;
    }

    private void UpdateBalanceLabel()
    {
        decimal total = _flowTabs.SelectedIndex == 0
            ? GetSelectedReservationTotal()
            : Math.Max((_walkInEndDatePicker.Value.Date - _walkInStartDatePicker.Value.Date).Days + 1, 0) * _walkInDailyRateInput.Value;
        if (_transaction is not null) total = _transaction.TotalAmount;
        _balanceLabel.Text = FormatPeso(Math.Max(total - _amountPaidInput.Value, 0));
    }

    private decimal GetSelectedReservationTotal()
    {
        FleetScheduleModel? schedule = GetSelectedReservation();
        Car? car = schedule is null ? null : _cars.FirstOrDefault(item => item.CarId == schedule.CarId);
        return schedule is null || car is null ? 0 : ((schedule.EndDate.Date - schedule.StartDate.Date).Days + 1) * car.RatePerDay;
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

    private static GroupBox CreateSection(string title, Control content)
    {
        GroupBox section = new()
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 28, 16, 12),
            Font = FontHelper.SemiBold(9.5F),
            ForeColor = ThemeHelper.TextPrimary,
            BackColor = ThemeHelper.Surface
        };
        content.Dock = DockStyle.Fill;
        section.Controls.Add(content);
        return section;
    }

    private static TableLayoutPanel CreateGrid(int columns, int rows)
    {
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = columns,
            RowCount = rows,
            Margin = new Padding(0)
        };

        for (int column = 0; column < columns; column++)
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
        }

        for (int row = 0; row < rows; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));
        }

        return layout;
    }

    private static Panel CreateProofPickerPanel(string labelText, Label pathLabel, Button browseButton, Button openButton)
    {
        Panel panel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.Surface,
            Margin = new Padding(0)
        };

        Label titleLabel = ControlFactory.CreateInputLabel(labelText);
        titleLabel.Location = new Point(0, 0);

        browseButton.Location = new Point(0, 24);
        openButton.Location = new Point(96, 24);

        pathLabel.Location = new Point(204, 29);
        pathLabel.Size = new Size(Math.Max(panel.Width - 210, 260), 20);
        panel.Resize += (_, _) => pathLabel.Width = Math.Max(panel.Width - 210, 220);

        panel.Controls.Add(titleLabel);
        panel.Controls.Add(browseButton);
        panel.Controls.Add(openButton);
        panel.Controls.Add(pathLabel);
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
            Size = new Size(400, 18),
            Font = FontHelper.SemiBold(9F),
            ForeColor = ThemeHelper.TextSecondary
        });
        panel.Controls.Add(new Label
        {
            Text = value,
            AutoSize = false,
            Location = new Point(0, 19),
            Size = new Size(400, 26),
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

    private static Label CreatePathLabel()
    {
        return new Label
        {
            AutoSize = false,
            Text = "No file selected",
            Font = FontHelper.Regular(9F),
            ForeColor = ThemeHelper.TextSecondary,
            AutoEllipsis = true
        };
    }

    private static string FormatPeso(decimal amount) => $"₱{amount:N2}";

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
