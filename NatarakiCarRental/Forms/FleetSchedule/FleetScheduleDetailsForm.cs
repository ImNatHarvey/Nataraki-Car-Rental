using FluentValidation;
using FluentValidation.Results;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using FleetScheduleModel = NatarakiCarRental.Models.FleetSchedule;

namespace NatarakiCarRental.Forms.FleetSchedule;

public enum FleetScheduleFormMode
{
    Add,
    Edit,
    View
}

public sealed class FleetScheduleDetailsForm : Form
{
    private const int InputWidth = 368;

    private readonly int _currentUserId;
    private readonly FleetScheduleService _scheduleService;
    private readonly SecurityVerificationService _verificationService = new();
    private readonly CarService _carService = new();
    private readonly CustomerService _customerService = new();
    private readonly FleetScheduleFormMode _mode;
    private readonly FleetScheduleModel? _sourceSchedule;
    private readonly int? _prefilledCarId;
    private readonly DateTime? _prefilledDate;
    private readonly string? _viewNote;
    private readonly ErrorProvider _errorProvider = new();

    private readonly ComboBox _carComboBox = CreateComboBox();
    private readonly ComboBox _customerComboBox = CreateComboBox();
    private readonly ComboBox _scheduleTypeComboBox = CreateComboBox();
    private readonly Label _statusLabel = CreateStatusLabel();
    private readonly DateTimePicker _startDatePicker = CreateDatePicker();
    private readonly DateTimePicker _endDatePicker = CreateDatePicker();
    private readonly Label _validationLabel = new();
    private readonly Label _codingDayLabel = new();
    private readonly Label _codingDayWarningLabel = new();
    private readonly OffsiteService _offsiteService;
    private Label? _titleLabel;
    private Label? _transactionManagedNoteLabel;
    private Button? _cancelButton;
    private Button? _saveButton;
    private Button? _archiveButton;
    
    // Maintenance execution buttons
    private Button? _viewRecordButton;

    // Transaction action button
    private Button? _viewTransactionButton;

    private bool _isViewOnly;

    private IReadOnlyList<Car> _cars = [];
    private IReadOnlyList<Customer> _customers = [];
    private IReadOnlyList<Customer> _offsiteClients = [];

    public FleetScheduleDetailsForm(
        FleetScheduleFormMode mode,
        int currentUserId,
        FleetScheduleModel? schedule = null,
        int? prefilledCarId = null,
        DateTime? prefilledDate = null,
        string? viewNote = null)
    {
        _currentUserId = currentUserId;
        _scheduleService = new FleetScheduleService(currentUserId);
        _offsiteService = new OffsiteService(currentUserId);
        _mode = mode;
        _sourceSchedule = schedule;
        _prefilledCarId = prefilledCarId;
        _prefilledDate = prefilledDate;
        _viewNote = viewNote;
        _isViewOnly = mode == FleetScheduleFormMode.View;

        InitializeForm();
        Load += FleetScheduleDetailsForm_Load;
    }

    private void InitializeForm()
    {
        if (_mode == FleetScheduleFormMode.View)
        {
            Text = "View Schedule";
        }
        else
        {
            Text = _mode == FleetScheduleFormMode.Edit ? "Edit Schedule" : "Add Schedule";
        }

        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(920, 650);
        BackColor = ThemeHelper.Surface;
        Font = FontHelper.Regular();
        ShowInTaskbar = false;

        _errorProvider.ContainerControl = this;
        _errorProvider.BlinkStyle = ErrorBlinkStyle.NeverBlink;

        _titleLabel = new Label
        {
            Text = Text,
            AutoSize = false,
            Location = new Point(32, 20),
            Size = new Size(260, 34),
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary
        };

        _validationLabel.AutoSize = false;
        _validationLabel.Size = new Size(856, 24);
        _validationLabel.Font = FontHelper.SemiBold(9F);
        _validationLabel.ForeColor = ThemeHelper.Danger;
        _validationLabel.Visible = false;

        if (_mode == FleetScheduleFormMode.Add)
        {
            Label helperLabel = new()
            {
                Text = "Rental schedules are created from the Transaction module.",
                AutoSize = true,
                Location = new Point(32, 58),
                Font = FontHelper.Regular(8.5F),
                ForeColor = ThemeHelper.TextSecondary
            };
            Controls.Add(helperLabel);
            _validationLabel.Location = new Point(34, 76);
        }
        else if (_mode == FleetScheduleFormMode.View && !string.IsNullOrWhiteSpace(_viewNote))
        {
            _transactionManagedNoteLabel = new Label
            {
                Text = _viewNote,
                AutoSize = true,
                Location = new Point(32, 58),
                Font = FontHelper.Regular(8.5F),
                ForeColor = ThemeHelper.TextSecondary
            };
            Controls.Add(_transactionManagedNoteLabel);
            _validationLabel.Location = new Point(34, 76);
        }
        else
        {
            _validationLabel.Location = new Point(34, 58);
        }

        if (_mode == FleetScheduleFormMode.View)
        {
            _carComboBox.Enabled = false;
            _customerComboBox.Enabled = false;
            _scheduleTypeComboBox.Enabled = false;
            _startDatePicker.Enabled = false;
            _endDatePicker.Enabled = false;
        }

        Panel contentPanel = new()
        {
            Location = new Point(32, (_mode == FleetScheduleFormMode.Add || (_mode == FleetScheduleFormMode.View && !string.IsNullOrWhiteSpace(_viewNote))) ? 104 : 88),
            Size = new Size(856, 470),
            BackColor = ThemeHelper.Surface
        };

        TableLayoutPanel contentLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180F));

        contentLayout.Controls.Add(CreateSection("Car / Customer Information", CreateCarCustomerLayout()), 0, 0);
        contentLayout.Controls.Add(CreateSection("Schedule Information", CreateScheduleInfoLayout()), 0, 1);
        contentLayout.Controls.Add(CreateSection("Date Range", CreateDateRangeLayout()), 0, 2);
        contentPanel.Controls.Add(contentLayout);

        _cancelButton = CreateSecondaryButton(_mode == FleetScheduleFormMode.View ? "Close" : "Cancel", 110, 38);
        _cancelButton.Location = new Point(_mode == FleetScheduleFormMode.View ? 778 : 622, 590);
        _cancelButton.DialogResult = DialogResult.Cancel;

        _saveButton = ControlFactory.CreatePrimaryButton(_mode == FleetScheduleFormMode.Edit ? "Save Schedule" : "Add Schedule", 134, 38);
        _saveButton.Location = new Point(754, 590);
        _saveButton.Click += SaveButton_Click;
        _saveButton.Visible = _mode != FleetScheduleFormMode.View;

        if (_mode == FleetScheduleFormMode.Edit)
        {
            _archiveButton = CreateDangerButton("Archive", 110, 38);
            _archiveButton.Location = new Point(32, 590);
            _archiveButton.Click += ArchiveButton_Click;

            _viewRecordButton = CreateSecondaryButton("View Offsite Record", 160, 38);
            _viewRecordButton.Location = new Point(32, 590); // Aligned to left
            _viewRecordButton.Visible = false;
            _viewRecordButton.Click += ViewRecordButton_Click;

            // Transaction action
            _viewTransactionButton = CreateSecondaryButton("View Transaction", 160, 38);
            _viewTransactionButton.Location = new Point(32, 590); // Aligned to left
            _viewTransactionButton.Visible = false;
            _viewTransactionButton.Click += ViewTransactionButton_Click;
        }

        Controls.Add(_titleLabel);
        Controls.Add(_validationLabel);
        Controls.Add(contentPanel);
        Controls.Add(_cancelButton);
        Controls.Add(_saveButton);
        
        if (_archiveButton is not null) Controls.Add(_archiveButton);
        if (_viewRecordButton is not null) Controls.Add(_viewRecordButton);
        if (_viewTransactionButton is not null) Controls.Add(_viewTransactionButton);

        // Add blank area click handler to remove focus from inputs
        Click += (_, _) => ActiveControl = null;
        CancelButton = _cancelButton;
    }

    private async Task UpdateContextualActionsAsync()
    {
        if (_sourceSchedule is null || _mode != FleetScheduleFormMode.Edit)
        {
            return;
        }

        if (_sourceSchedule.ScheduleType == FleetScheduleConstants.Type.Maintenance)
        {
            bool isPending = _sourceSchedule.Status == FleetScheduleConstants.Status.Pending;
            bool hasOperationalExecution = _sourceSchedule.Status == FleetScheduleConstants.Status.Maintenance ||
                                           _sourceSchedule.Status == FleetScheduleConstants.Status.Completed ||
                                           _sourceSchedule.Status == FleetScheduleConstants.Status.Cancelled;

            if (hasOperationalExecution && _viewRecordButton != null)
            {
                OffsiteRecord? record = await _offsiteService.GetByFleetScheduleIdAsync(_sourceSchedule.ScheduleId);
                _viewRecordButton.Visible = record is not null;
            }

            // Hide normal save/archive if operational execution started (execution record handles it)
            if (hasOperationalExecution)
            {
                if (_saveButton != null) _saveButton.Visible = false;
                if (_archiveButton != null) _archiveButton.Visible = false;
            }
        }
        else if (_sourceSchedule.ScheduleType == FleetScheduleConstants.Type.Rental)
        {
            if (_viewTransactionButton != null)
            {
                var transactionService = new TransactionService(_currentUserId);
                Transaction? linkedTransaction = await transactionService.GetByFleetScheduleIdAsync(_sourceSchedule.ScheduleId);
                _viewTransactionButton.Visible = linkedTransaction is not null;
            }
        }
    }

    private async void ViewRecordButton_Click(object? sender, EventArgs e)
    {
        if (_sourceSchedule is null) return;
        
        OffsiteRecord? record = await _offsiteService.GetByFleetScheduleIdAsync(_sourceSchedule.ScheduleId);
        if (record is null)
        {
            MessageBoxHelper.ShowWarning("No execution record found for this schedule.");
            return;
        }

        using NatarakiCarRental.Forms.Offsite.OffsiteRecordDetailsForm form = new(_currentUserId, record.OffsiteRecordId, isViewOnly: true);
        form.ShowDialog(this);
    }

    private async void ViewTransactionButton_Click(object? sender, EventArgs e)
    {
        if (_sourceSchedule is null) return;

        var transactionService = new TransactionService(_currentUserId);
        Transaction? linkedTransaction = await transactionService.GetByFleetScheduleIdAsync(_sourceSchedule.ScheduleId);
        
        if (linkedTransaction is null)
        {
            MessageBoxHelper.ShowWarning("No transaction found for this schedule.");
            return;
        }

        using NatarakiCarRental.Forms.Transactions.TransactionDetailsForm form = new(linkedTransaction);
        form.ShowDialog(this);
    }

    private async void FleetScheduleDetailsForm_Load(object? sender, EventArgs e)
    {
        Load -= FleetScheduleDetailsForm_Load;

        try
        {
            _cars = await _carService.GetActiveCarsAsync();
            _customers = await _customerService.SearchCustomersAsync(string.Empty, CustomerListFilter.Active);
            _offsiteClients = await _customerService.SearchCustomersAsync(string.Empty, CustomerListFilter.OffsiteClients);

            if (_sourceSchedule?.CustomerId is int sourceCustomerId
                && !_customers.Any(customer => customer.CustomerId == sourceCustomerId))
            {
                Customer? sourceCustomer = await _customerService.GetCustomerByIdAsync(sourceCustomerId);

                if (sourceCustomer is not null && !sourceCustomer.IsArchived)
                {
                    _customers = [.. _customers, sourceCustomer];
                }
            }

            PopulateLookups();

            if (_sourceSchedule is not null)
            {
                await CheckTransactionLinkAndLoadAsync();
            }
            else
            {
                await ApplyDefaultsAsync();
            }
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to load schedule form data.\n\n{exception.Message}", "Fleet Schedule");
            Close();
        }
    }

    private void PopulateLookups()
    {
        _carComboBox.Items.Clear();
        _carComboBox.Items.AddRange(_cars.Select(car => new LookupOption(car.CarId, $"{car.CarName} ({car.PlateNumber})")).Cast<object>().ToArray());

        UpdateCustomerList();

        _scheduleTypeComboBox.Items.Clear();
        
        if (_sourceSchedule?.ScheduleType == FleetScheduleConstants.Type.Rental)
        {
            // View-only mode for Rentals
            _scheduleTypeComboBox.Items.Add(FleetScheduleConstants.Type.Rental);
        }
        else
        {
            // Normal Add/Edit mode
            _scheduleTypeComboBox.Items.AddRange(new[]
            {
                FleetScheduleConstants.Type.Reservation,
                FleetScheduleConstants.Type.Maintenance
            });
        }

        if (_sourceSchedule is not null && _scheduleTypeComboBox.Items.Contains(_sourceSchedule.ScheduleType))
        {
            _scheduleTypeComboBox.SelectedItem = _sourceSchedule.ScheduleType;
        }
        else if (_scheduleTypeComboBox.Items.Count > 0)
        {
            _scheduleTypeComboBox.SelectedIndex = 0;
        }

        _scheduleTypeComboBox.SelectedIndexChanged += async (_, _) => 
        {
            await UpdateStatusTextAsync();
            UpdateCustomerList();
        };
        _carComboBox.SelectedIndexChanged += (_, _) => UpdateCodingDayIndicator();
        _startDatePicker.ValueChanged += (_, _) => UpdateCodingDayIndicator();
        _endDatePicker.ValueChanged += (_, _) => UpdateCodingDayIndicator();
    }

    private async Task CheckTransactionLinkAndLoadAsync()
    {
        if (_sourceSchedule is null) return;

        // If we are already in View mode from constructor, just load data and return
        if (_mode == FleetScheduleFormMode.View)
        {
            await LoadScheduleAsync(_sourceSchedule);
            return;
        }

        bool isLinkedToTransaction = await _scheduleService.IsLinkedToActiveTransactionAsync(_sourceSchedule.ScheduleId);
        bool isRental = _sourceSchedule.ScheduleType == FleetScheduleConstants.Type.Rental;
        
        bool isCompletedOrCancelled = _sourceSchedule.Status == FleetScheduleConstants.Status.Completed || 
                                      _sourceSchedule.Status == FleetScheduleConstants.Status.Cancelled;
        
        bool isActiveMaintenance = _sourceSchedule.ScheduleType == FleetScheduleConstants.Type.Maintenance && 
                                   _sourceSchedule.Status != FleetScheduleConstants.Status.Pending;

        if (isLinkedToTransaction || isRental)
        {
            await SetToViewModeAsync("This schedule is managed through the Transactions module.");
        }
        else if (isActiveMaintenance)
        {
            await SetToViewModeAsync("This maintenance schedule is managed through the Offsite module.");
        }
        else if (isCompletedOrCancelled)
        {
            await SetToViewModeAsync("This historical schedule is view-only.");
        }
        else
        {
            await LoadScheduleAsync(_sourceSchedule);
        }

        await UpdateContextualActionsAsync();
    }

    private async Task SetToViewModeAsync(string noteText)
    {
        _isViewOnly = true;
        Text = "View Schedule";
        if (_titleLabel is not null)
        {
            _titleLabel.Text = Text;
        }

        _carComboBox.Enabled = false;
        _customerComboBox.Enabled = false;
        _scheduleTypeComboBox.Enabled = false;
        _startDatePicker.Enabled = false;
        _endDatePicker.Enabled = false;

        if (_transactionManagedNoteLabel == null)
        {
            _transactionManagedNoteLabel = new Label
            {
                Text = noteText,
                AutoSize = true,
                Location = new Point(32, 58),
                Font = FontHelper.Regular(8.5F),
                ForeColor = ThemeHelper.TextSecondary
            };
            Controls.Add(_transactionManagedNoteLabel);
            _validationLabel.Location = new Point(34, 76);
        }
        else
        {
            _transactionManagedNoteLabel.Text = noteText;
        }

        if (_sourceSchedule is not null)
        {
            await LoadScheduleAsync(_sourceSchedule);
            if (_saveButton is not null) _saveButton.Visible = false;
            if (_archiveButton is not null) _archiveButton.Visible = false;
            if (_cancelButton is not null)
            {
                _cancelButton.Text = "Close";
                _cancelButton.Location = new Point(778, 590);
            }
        }
    }

    private async Task ApplyDefaultsAsync()
    {
        SelectLookup(_carComboBox, _prefilledCarId);
        _scheduleTypeComboBox.SelectedItem = FleetScheduleConstants.Type.Reservation;
        await UpdateStatusTextAsync();
        DateTime date = _prefilledDate?.Date ?? DateTime.Today;
        _startDatePicker.Value = date;
        _endDatePicker.Value = date;
        UpdateCodingDayIndicator();
    }

    private async Task LoadScheduleAsync(FleetScheduleModel schedule)
    {
        SelectLookup(_carComboBox, schedule.CarId);
        SelectLookup(_customerComboBox, schedule.CustomerId);
        _scheduleTypeComboBox.SelectedItem = schedule.ScheduleType;
        SetStatusText(schedule.Status);
        _startDatePicker.Value = schedule.StartDate;
        _endDatePicker.Value = schedule.EndDate;
        UpdateCodingDayIndicator();
        await UpdateContextualActionsAsync();
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
            if (_isViewOnly)
            {
                Close();
                return;
            }

            FleetScheduleModel schedule = BuildSchedule();

            if (_mode == FleetScheduleFormMode.Edit)
            {
                await _scheduleService.UpdateAsync(schedule);
                MessageBoxHelper.ShowSuccess("Schedule updated successfully.");
            }
            else
            {
                await _scheduleService.CreateAsync(schedule);
                MessageBoxHelper.ShowSuccess("Schedule created successfully.");
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException exception)
        {
            ShowValidationErrors(exception.Errors.ToList(), exception.Message);
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to save schedule.\n\n{exception.Message}", "Fleet Schedule");
        }
        finally
        {
            saveButton.Enabled = true;
        }
    }

    private async void ArchiveButton_Click(object? sender, EventArgs e)
    {
        if (_sourceSchedule is null)
        {
            return;
        }

        if (!await _verificationService.RequireOwnerVerificationIfNeededAsync(_currentUserId, $"Archive schedule: {_sourceSchedule.Title}"))
        {
            return;
        }

        bool confirmed = MessageBoxHelper.ShowConfirmWarning(
            $"Archive schedule '{_sourceSchedule.Title}'?",
            "Archive Schedule");

        if (!confirmed)
        {
            return;
        }

        try
        {
            await _scheduleService.ArchiveAsync(_sourceSchedule.ScheduleId);
            MessageBoxHelper.ShowSuccess("Schedule archived successfully.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to archive schedule.\n\n{exception.Message}", "Fleet Schedule");
        }
    }

    private FleetScheduleModel BuildSchedule()
    {
        return new FleetScheduleModel
        {
            ScheduleId = _sourceSchedule?.ScheduleId ?? 0,
            CarId = GetSelectedLookupId(_carComboBox) ?? 0,
            CustomerId = GetSelectedLookupId(_customerComboBox),
            ScheduleType = _scheduleTypeComboBox.SelectedItem?.ToString() ?? string.Empty,
            Status = GetSelectedStatusValue(),
            StartDate = _startDatePicker.Value.Date,
            EndDate = _endDatePicker.Value.Date,
            Notes = _sourceSchedule?.Notes ?? string.Empty
        };
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
            Control? control = GetControlForProperty(error.PropertyName);
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

    private Control? GetControlForProperty(string propertyName)
    {
        return propertyName switch
        {
            nameof(FleetScheduleModel.CarId) => _carComboBox,
            nameof(FleetScheduleModel.CustomerId) => _customerComboBox,
            nameof(FleetScheduleModel.ScheduleType) => _scheduleTypeComboBox,
            nameof(FleetScheduleModel.Status) => _statusLabel,
            nameof(FleetScheduleModel.StartDate) => _startDatePicker,
            nameof(FleetScheduleModel.EndDate) => _endDatePicker,
            _ => null
        };
    }

    private TableLayoutPanel CreateCarCustomerLayout()
    {
        TableLayoutPanel layout = CreateGrid(2, 1);
        layout.Padding = new Padding(0, 0, 0, 12);
        layout.RowStyles.Clear();
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 128F));
        layout.Controls.Add(CreateCarInputPanel(), 0, 0);
        layout.Controls.Add(CreateInputPanel("Customer", _customerComboBox), 1, 0);
        return layout;
    }

    private Panel CreateCarInputPanel()
    {
        Panel panel = CreateInputPanel("Car *", _carComboBox);

        _codingDayLabel.AutoSize = false;
        _codingDayLabel.Location = new Point(0, 60);
        _codingDayLabel.Size = new Size(InputWidth, 24);
        _codingDayLabel.Font = FontHelper.Regular(8.5F);
        _codingDayLabel.ForeColor = ThemeHelper.TextSecondary;

        _codingDayWarningLabel.AutoSize = false;
        _codingDayWarningLabel.Location = new Point(0, 86);
        _codingDayWarningLabel.Size = new Size(InputWidth, 34);
        _codingDayWarningLabel.Font = FontHelper.Regular(8.5F);
        _codingDayWarningLabel.ForeColor = ThemeHelper.Warning;
        _codingDayWarningLabel.Visible = false;

        panel.Controls.Add(_codingDayLabel);
        panel.Controls.Add(_codingDayWarningLabel);
        return panel;
    }

    private TableLayoutPanel CreateScheduleInfoLayout()
    {
        TableLayoutPanel layout = CreateGrid(2, 1);
        layout.Controls.Add(CreateInputPanel("Schedule Type *", _scheduleTypeComboBox), 0, 0);
        layout.Controls.Add(CreateInputPanel("Status", _statusLabel), 1, 0);
        return layout;
    }

    private TableLayoutPanel CreateDateRangeLayout()
    {
        TableLayoutPanel layout = CreateGrid(2, 1);
        
        _startDatePicker.ValueChanged += (_, _) =>
        {
            if (_endDatePicker.Value.Date < _startDatePicker.Value.Date)
            {
                _endDatePicker.Value = _startDatePicker.Value.Date;
            }
            _endDatePicker.MinDate = _startDatePicker.Value.Date;
        };

        layout.Controls.Add(CreateInputPanel("Start Date *", _startDatePicker), 0, 0);
        layout.Controls.Add(CreateInputPanel("End Date *", _endDatePicker), 1, 0);

        Label historicalHelperLabel = new()
        {
            Text = "Past dates are allowed for missed/historical records.",
            AutoSize = true,
            Location = new Point(16, 60),
            Font = FontHelper.Italic(8.5F),
            ForeColor = ThemeHelper.TextSecondary
        };
        
        TableLayoutPanel outerLayout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        outerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));
        outerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        outerLayout.Controls.Add(layout, 0, 0);
        outerLayout.Controls.Add(historicalHelperLabel, 0, 1);

        return outerLayout;
    }

    private static GroupBox CreateSection(string title, Control content)
    {
        GroupBox section = new()
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 24, 16, 14),
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
            RowCount = rows
        };

        if (columns == 2)
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        }
        else
        {
            for (int column = 0; column < columns; column++)
            {
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
            }
        }

        for (int row = 0; row < rows; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        }

        return layout;
    }

    private static Panel CreateInputPanel(string labelText, Control inputControl)
    {
        Panel panel = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 20, 0), BackColor = ThemeHelper.Surface };
        Label label = ControlFactory.CreateInputLabel(labelText);
        label.Location = new Point(0, 0);
        inputControl.Location = new Point(0, 26);
        panel.Controls.Add(label);
        panel.Controls.Add(inputControl);
        return panel;
    }

    private static ComboBox CreateComboBox()
    {
        return new ComboBox
        {
            Width = InputWidth,
            Height = 30,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = FontHelper.Regular(10F)
        };
    }

    private static Label CreateStatusLabel()
    {
        return new Label
        {
            Width = InputWidth,
            Height = 30,
            AutoSize = false,
            BackColor = ThemeHelper.Surface,
            ForeColor = ThemeHelper.TextPrimary,
            Font = FontHelper.Regular(10F),
            Padding = Padding.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static DateTimePicker CreateDatePicker()
    {
        return new DateTimePicker
        {
            Width = InputWidth,
            Height = 30,
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

    private static Button CreateDangerButton(string text, int width, int height)
    {
        Button button = ControlFactory.CreatePrimaryButton(text, width, height);
        button.BackColor = ThemeHelper.Danger;
        button.FlatAppearance.MouseOverBackColor = ThemeHelper.Danger;
        return button;
    }

    private static int? GetSelectedLookupId(ComboBox comboBox)
    {
        return comboBox.SelectedItem is LookupOption option ? option.Id : null;
    }

    private static void SelectLookup(ComboBox comboBox, int? id)
    {
        LookupOption? option = comboBox.Items.OfType<LookupOption>().FirstOrDefault(item => item.Id == id);
        if (option is not null)
        {
            comboBox.SelectedItem = option;
        }
    }

    private async Task UpdateStatusTextAsync()
    {
        string scheduleType = _scheduleTypeComboBox.SelectedItem?.ToString() ?? string.Empty;
        string status = _mode == FleetScheduleFormMode.Edit
            && _sourceSchedule is not null
            && _sourceSchedule.ScheduleType == scheduleType
                ? _sourceSchedule.Status
                : FleetScheduleVisualHelper.GetDefaultStatusForType(scheduleType);
        SetStatusText(status);
        await UpdateContextualActionsAsync();
    }

    private void SetStatusText(string status)
    {
        _statusLabel.Text = status;
        _statusLabel.ForeColor = ThemeHelper.TextPrimary;
    }

    private void UpdateCustomerList()
    {
        bool isMaintenance = _scheduleTypeComboBox.SelectedItem?.ToString() == FleetScheduleConstants.Type.Maintenance;
        var customersToUse = isMaintenance ? _offsiteClients : _customers;
        
        _customerComboBox.Items.Clear();
        _customerComboBox.Items.Add(new LookupOption(null, "No customer"));
        _customerComboBox.Items.AddRange(customersToUse.Select(customer => new LookupOption(customer.CustomerId, (customer.CompanyName ?? $"{customer.FirstName} {customer.LastName}").Trim())).Cast<object>().ToArray());
        _customerComboBox.SelectedIndex = 0;
    }

    private void UpdateCodingDayIndicator()
    {
        Car? selectedCar = GetSelectedCar();
        if (selectedCar is null)
        {
            _codingDayLabel.Text = "Coding Day: -";
            _codingDayWarningLabel.Visible = false;
            return;
        }

        string codingDay = string.IsNullOrWhiteSpace(selectedCar.CodingDay)
            ? CarConstants.CodingDay.NotApplicable
            : selectedCar.CodingDay;

        _codingDayLabel.Text = $"Coding Day: {FormatCodingDayDisplay(codingDay)}";
        bool hasConflict = CodingDayValidationHelper.DateRangeContainsCodingDay(
            _startDatePicker.Value,
            _endDatePicker.Value,
            selectedCar.CodingDay);
        _codingDayWarningLabel.Text = "Selected dates include this vehicle's coding restriction day.";
        _codingDayWarningLabel.Visible = hasConflict;
    }

    private Car? GetSelectedCar()
    {
        int? carId = GetSelectedLookupId(_carComboBox);
        return carId.HasValue
            ? _cars.FirstOrDefault(car => car.CarId == carId.Value)
            : null;
    }

    private static string FormatCodingDayDisplay(string codingDay)
    {
        return codingDay switch
        {
            CarConstants.CodingDay.Monday => "Monday (1 & 2)",
            CarConstants.CodingDay.Tuesday => "Tuesday (3 & 4)",
            CarConstants.CodingDay.Wednesday => "Wednesday (5 & 6)",
            CarConstants.CodingDay.Thursday => "Thursday (7 & 8)",
            CarConstants.CodingDay.Friday => "Friday (9 & 0)",
            _ => CarConstants.CodingDay.NotApplicable
        };
    }

    private string GetSelectedStatusValue()
    {
        return _statusLabel.Text;
    }

    private sealed record LookupOption(int? Id, string Name)
    {
        public override string ToString() => Name;
    }
}
