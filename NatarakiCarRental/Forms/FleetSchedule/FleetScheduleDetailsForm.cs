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
    Edit
}

public sealed class FleetScheduleDetailsForm : Form
{
    private const int InputWidth = 368;

    private readonly FleetScheduleService _scheduleService;
    private readonly CarService _carService = new();
    private readonly CustomerService _customerService = new();
    private readonly FleetScheduleFormMode _mode;
    private readonly FleetScheduleModel? _sourceSchedule;
    private readonly int? _prefilledCarId;
    private readonly DateTime? _prefilledDate;
    private readonly ErrorProvider _errorProvider = new();

    private readonly ComboBox _carComboBox = CreateComboBox();
    private readonly ComboBox _customerComboBox = CreateComboBox();
    private readonly ComboBox _scheduleTypeComboBox = CreateComboBox();
    private readonly ComboBox _statusComboBox = CreateComboBox();
    private readonly DateTimePicker _startDatePicker = CreateDatePicker();
    private readonly DateTimePicker _endDatePicker = CreateDatePicker();
    private readonly Label _validationLabel = new();

    private IReadOnlyList<Car> _cars = [];
    private IReadOnlyList<Customer> _customers = [];

    public FleetScheduleDetailsForm(
        FleetScheduleFormMode mode,
        int? currentUserId,
        FleetScheduleModel? schedule = null,
        int? prefilledCarId = null,
        DateTime? prefilledDate = null)
    {
        _scheduleService = new FleetScheduleService(currentUserId);
        _mode = mode;
        _sourceSchedule = schedule;
        _prefilledCarId = prefilledCarId;
        _prefilledDate = prefilledDate;
        InitializeForm();
        Load += FleetScheduleDetailsForm_Load;
    }

    private void InitializeForm()
    {
        Text = _mode == FleetScheduleFormMode.Edit ? "Edit Schedule" : "Add Schedule";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(920, 520);
        BackColor = ThemeHelper.Surface;
        Font = FontHelper.Regular();
        ShowInTaskbar = false;

        _errorProvider.ContainerControl = this;
        _errorProvider.BlinkStyle = ErrorBlinkStyle.NeverBlink;

        Label titleLabel = new()
        {
            Text = Text,
            AutoSize = false,
            Location = new Point(32, 20),
            Size = new Size(260, 34),
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary
        };

        _validationLabel.AutoSize = false;
        _validationLabel.Location = new Point(34, 58);
        _validationLabel.Size = new Size(856, 24);
        _validationLabel.Font = FontHelper.SemiBold(9F);
        _validationLabel.ForeColor = ThemeHelper.Danger;
        _validationLabel.Visible = false;

        Panel contentPanel = new()
        {
            Location = new Point(32, 88),
            Size = new Size(856, 360),
            BackColor = ThemeHelper.Surface
        };

        TableLayoutPanel contentLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));

        contentLayout.Controls.Add(CreateSection("Car / Customer Information", CreateCarCustomerLayout()), 0, 0);
        contentLayout.Controls.Add(CreateSection("Schedule Information", CreateScheduleInfoLayout()), 0, 1);
        contentLayout.Controls.Add(CreateSection("Date Range", CreateDateRangeLayout()), 0, 2);
        contentPanel.Controls.Add(contentLayout);

        Button cancelButton = CreateSecondaryButton("Cancel", 110, 38);
        cancelButton.Location = new Point(622, 462);
        cancelButton.DialogResult = DialogResult.Cancel;

        Button saveButton = ControlFactory.CreatePrimaryButton(_mode == FleetScheduleFormMode.Edit ? "Save Schedule" : "Add Schedule", 134, 38);
        saveButton.Location = new Point(754, 462);
        saveButton.Click += SaveButton_Click;

        Button? archiveButton = null;
        if (_mode == FleetScheduleFormMode.Edit)
        {
            archiveButton = CreateDangerButton("Archive", 110, 38);
            archiveButton.Location = new Point(32, 462);
            archiveButton.Click += ArchiveButton_Click;
        }

        Controls.Add(titleLabel);
        Controls.Add(_validationLabel);
        Controls.Add(contentPanel);
        Controls.Add(cancelButton);
        Controls.Add(saveButton);
        if (archiveButton is not null)
        {
            Controls.Add(archiveButton);
        }
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private async void FleetScheduleDetailsForm_Load(object? sender, EventArgs e)
    {
        Load -= FleetScheduleDetailsForm_Load;

        try
        {
            _cars = await _carService.GetActiveCarsAsync();
            _customers = await _customerService.SearchCustomersAsync(string.Empty, CustomerListFilter.Active);

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
                LoadSchedule(_sourceSchedule);
            }
            else
            {
                ApplyDefaults();
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

        _customerComboBox.Items.Clear();
        _customerComboBox.Items.Add(new LookupOption(null, "No customer"));
        _customerComboBox.Items.AddRange(_customers.Select(customer => new LookupOption(customer.CustomerId, $"{customer.FirstName} {customer.LastName}".Trim())).Cast<object>().ToArray());
        _customerComboBox.SelectedIndex = 0;

        _scheduleTypeComboBox.Items.AddRange(FleetScheduleConstants.Type.All.Cast<object>().ToArray());
        _scheduleTypeComboBox.SelectedIndexChanged += (_, _) => RefreshStatusOptions();
    }

    private void ApplyDefaults()
    {
        SelectLookup(_carComboBox, _prefilledCarId);
        _scheduleTypeComboBox.SelectedItem = FleetScheduleConstants.Type.Reservation;
        DateTime date = _prefilledDate?.Date ?? DateTime.Today;
        _startDatePicker.Value = date;
        _endDatePicker.Value = date;
    }

    private void LoadSchedule(FleetScheduleModel schedule)
    {
        SelectLookup(_carComboBox, schedule.CarId);
        SelectLookup(_customerComboBox, schedule.CustomerId);
        _scheduleTypeComboBox.SelectedItem = schedule.ScheduleType;
        SelectStatus(schedule.Status);
        _startDatePicker.Value = schedule.StartDate;
        _endDatePicker.Value = schedule.EndDate;
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
            nameof(FleetScheduleModel.Status) => _statusComboBox,
            nameof(FleetScheduleModel.StartDate) => _startDatePicker,
            nameof(FleetScheduleModel.EndDate) => _endDatePicker,
            _ => null
        };
    }

    private TableLayoutPanel CreateCarCustomerLayout()
    {
        TableLayoutPanel layout = CreateGrid(2, 1);
        layout.Controls.Add(CreateInputPanel("Car *", _carComboBox), 0, 0);
        layout.Controls.Add(CreateInputPanel("Customer", _customerComboBox), 1, 0);
        return layout;
    }

    private TableLayoutPanel CreateScheduleInfoLayout()
    {
        TableLayoutPanel layout = CreateGrid(2, 1);
        layout.Controls.Add(CreateInputPanel("Schedule Type *", _scheduleTypeComboBox), 0, 0);
        layout.Controls.Add(CreateInputPanel("Status *", _statusComboBox), 1, 0);
        return layout;
    }

    private TableLayoutPanel CreateDateRangeLayout()
    {
        TableLayoutPanel layout = CreateGrid(2, 1);
        layout.Controls.Add(CreateInputPanel("Start Date *", _startDatePicker), 0, 0);
        layout.Controls.Add(CreateInputPanel("End Date *", _endDatePicker), 1, 0);
        return layout;
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

    private void SelectStatus(string status)
    {
        FleetScheduleVisualHelper.StatusDisplayOption? option = _statusComboBox.Items
            .OfType<FleetScheduleVisualHelper.StatusDisplayOption>()
            .FirstOrDefault(item => item.Value == status);

        if (option is not null)
        {
            _statusComboBox.SelectedItem = option;
        }
    }

    private void RefreshStatusOptions()
    {
        string scheduleType = _scheduleTypeComboBox.SelectedItem?.ToString() ?? string.Empty;
        string currentStatus = GetSelectedStatusValue();
        IReadOnlyList<FleetScheduleVisualHelper.StatusDisplayOption> options =
            FleetScheduleVisualHelper.GetStatusOptionsForType(scheduleType);

        _statusComboBox.Items.Clear();
        _statusComboBox.Items.AddRange(options.Cast<object>().ToArray());

        FleetScheduleVisualHelper.StatusDisplayOption? selectedOption = options
            .FirstOrDefault(option => option.Value == currentStatus);

        _statusComboBox.SelectedItem = selectedOption
            ?? options.FirstOrDefault(option => option.Value == FleetScheduleVisualHelper.GetDefaultStatusForType(scheduleType));
    }

    private string GetSelectedStatusValue()
    {
        return _statusComboBox.SelectedItem is FleetScheduleVisualHelper.StatusDisplayOption option
            ? option.Value
            : string.Empty;
    }

    private sealed record LookupOption(int? Id, string Name)
    {
        public override string ToString() => Name;
    }
}
