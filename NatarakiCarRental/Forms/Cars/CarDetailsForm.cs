using FluentValidation;
using FluentValidation.Results;
using System.Diagnostics;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.Forms.Cars;

public enum CarFormMode
{
    Add,
    Edit,
    View
}

public sealed class CarDetailsForm : Form
{
    // Removed blank "" options
    private static readonly string[] StatusOptions = CarConstants.Status.ManualSelectable;
    private static readonly string[] TransmissionOptions = ["Automatic", "Manual", "CVT"];
    private static readonly string[] FuelTypeOptions = ["Gasoline", "Diesel", "Hybrid", "Electric"];
    private static readonly string[] CodingDayOptions = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "None / Not Applicable"];

    private readonly CarService _carService;
    private readonly CarFormMode _mode;
    private readonly Car? _sourceCar;
    private readonly ErrorProvider _errorProvider = new();

    private readonly TextBox _carNameTextBox = ControlFactory.CreateTextBox(220);
    private readonly TextBox _modelTextBox = ControlFactory.CreateTextBox(220);
    private readonly TextBox _plateNumberTextBox = ControlFactory.CreateTextBox(220);
    private readonly TextBox _ratePerDayTextBox = ControlFactory.CreateTextBox(220);
    private readonly ComboBox _statusComboBox = CreateComboBox(220, StatusOptions);
    private readonly TextBox _brandTextBox = ControlFactory.CreateTextBox(220);
    private readonly TextBox _yearTextBox = ControlFactory.CreateTextBox(220);
    private readonly TextBox _colorTextBox = ControlFactory.CreateTextBox(220);
    private readonly TextBox _seatingCapacityTextBox = ControlFactory.CreateTextBox(220);
    private readonly ComboBox _transmissionComboBox = CreateComboBox(220, TransmissionOptions);
    private readonly ComboBox _fuelTypeComboBox = CreateComboBox(220, FuelTypeOptions);
    private readonly ComboBox _codingDayComboBox = CreateComboBox(220, CodingDayOptions);
    private readonly TextBox _mileageTextBox = ControlFactory.CreateTextBox(220); // Added Mileage
    private readonly DateTimePicker _registrationExpirationPicker = CreateNullableDatePicker(220);
    private readonly DateTimePicker _insuranceExpirationPicker = CreateNullableDatePicker(220);

    private readonly Label _imagePathLabel = new();
    private readonly Label _orCrPathLabel = new();

    private readonly Button _imageBrowseButton = CreateSecondaryButton("Browse", 90, 30);
    private readonly Button _orCrBrowseButton = CreateSecondaryButton("Browse", 90, 30);
    private readonly Button _imageOpenButton = CreateSecondaryButton("Open File", 90, 30);
    private readonly Button _orCrOpenButton = CreateSecondaryButton("Open File", 90, 30);
    private readonly Label _validationLabel = new();

    private string? _selectedImageSourcePath;
    private string? _selectedOrCrSourcePath;

    public CarDetailsForm(CarFormMode mode, Car? car = null, int? currentUserId = null)
    {
        _carService = new CarService(currentUserId);
        _mode = mode;
        _sourceCar = car;
        InitializeForm();
        ConfigureInputs();

        if (car is not null)
        {
            LoadCar(car);
        }
        else
        {
            // Preselect defaults for Add mode
            _statusComboBox.SelectedItem = "Available";
            _transmissionComboBox.SelectedIndex = 0; // Default to Automatic
            _fuelTypeComboBox.SelectedIndex = 0; // Default to Gasoline
            _codingDayComboBox.SelectedItem = "None / Not Applicable"; // Default to None
        }

        if (_mode == CarFormMode.View)
        {
            ApplyViewMode();
        }
    }

    private bool IsViewMode => _mode == CarFormMode.View;

    private void InitializeForm()
    {
        string title = _mode switch
        {
            CarFormMode.Edit => "Edit Car",
            CarFormMode.View => "View Car",
            _ => "Add Car"
        };

        Text = title;
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

        Label titleLabel = new()
        {
            Text = title,
            AutoSize = false,
            Location = new Point(32, 24),
            Size = new Size(260, 34),
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Label subtitleLabel = new()
        {
            Text = _mode switch
            {
                CarFormMode.Edit => "Update vehicle details, compliance dates, and attached files.",
                CarFormMode.View => "Review this vehicle record and its saved details.",
                _ => "Create a vehicle record with rental, compliance, and file details."
            },
            AutoSize = false,
            Location = new Point(34, 58),
            Size = new Size(620, 24),
            Font = FontHelper.Regular(9.5F),
            ForeColor = ThemeHelper.TextSecondary
        };

        _validationLabel.AutoSize = false;
        _validationLabel.Location = new Point(34, 86);
        _validationLabel.Size = new Size(996, 24);
        _validationLabel.Font = FontHelper.SemiBold(9F);
        _validationLabel.ForeColor = ThemeHelper.Danger;
        _validationLabel.Visible = false;

        Panel contentPanel = new()
        {
            Location = new Point(32, 116),
            Size = new Size(996, 660),
            BackColor = ThemeHelper.Surface
        };

        TableLayoutPanel contentLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };

        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 125F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 155F));

        contentLayout.Controls.Add(CreateSection("General Information", CreateRequiredFieldsLayout()), 0, 0);
        contentLayout.Controls.Add(CreateSection("Specifications", CreateVehicleDetailsLayout()), 0, 1);
        contentLayout.Controls.Add(CreateSection("Compliance & Dates", CreateComplianceLayout()), 0, 2);
        contentLayout.Controls.Add(CreateSection("Attachments", CreateFilesLayout()), 0, 3);

        contentPanel.Controls.Add(contentLayout);

        Button cancelButton = CreateSecondaryButton(IsViewMode ? "Close" : "Cancel", 118, 38);
        cancelButton.Location = new Point(IsViewMode ? 910 : 756, 800);
        cancelButton.DialogResult = DialogResult.Cancel;

        Button? saveButton = null;
        if (!IsViewMode)
        {
            saveButton = ControlFactory.CreatePrimaryButton(_mode == CarFormMode.Edit ? "Save Changes" : "Add Car", 140, 38);
            saveButton.Location = new Point(888, 800);
            saveButton.Click += SaveButton_Click;
            AcceptButton = saveButton;
        }

        Controls.Add(titleLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(_validationLabel);
        Controls.Add(contentPanel);
        Controls.Add(cancelButton);

        if (saveButton is not null)
        {
            Controls.Add(saveButton);
        }

        CancelButton = cancelButton;
    }

    private TableLayoutPanel CreateRequiredFieldsLayout()
    {
        TableLayoutPanel layout = CreateGrid(4, 2);
        layout.Controls.Add(CreateInputPanel("Car Name *", _carNameTextBox), 0, 0);
        layout.Controls.Add(CreateInputPanel("Model *", _modelTextBox), 1, 0);
        layout.Controls.Add(CreateInputPanel("Plate Number *", _plateNumberTextBox), 2, 0);

        layout.Controls.Add(CreateInputPanel("Rate Per Day (PHP) *", _ratePerDayTextBox), 3, 0);

        layout.Controls.Add(CreateInputPanel("Status *", _statusComboBox), 0, 1);

        return layout;
    }

    private TableLayoutPanel CreateVehicleDetailsLayout()
    {
        TableLayoutPanel layout = CreateGrid(4, 2);
        layout.Controls.Add(CreateInputPanel("Brand", _brandTextBox), 0, 0);
        layout.Controls.Add(CreateInputPanel("Year", _yearTextBox), 1, 0);
        layout.Controls.Add(CreateInputPanel("Color", _colorTextBox), 2, 0);
        layout.Controls.Add(CreateInputPanel("Seating Capacity", _seatingCapacityTextBox), 3, 0);
        layout.Controls.Add(CreateInputPanel("Transmission", _transmissionComboBox), 0, 1);
        layout.Controls.Add(CreateInputPanel("Fuel Type", _fuelTypeComboBox), 1, 1);
        layout.Controls.Add(CreateInputPanel("Car Coding Day", _codingDayComboBox), 2, 1);

        // Placed Mileage in the empty 4th column slot
        layout.Controls.Add(CreateInputPanel("Mileage (km)", _mileageTextBox), 3, 1);

        return layout;
    }

    private TableLayoutPanel CreateComplianceLayout()
    {
        TableLayoutPanel layout = CreateGrid(4, 1);
        layout.Controls.Add(CreateInputPanel("Registration Expiration Date", _registrationExpirationPicker), 0, 0);
        layout.Controls.Add(CreateInputPanel("Insurance Expiration Date", _insuranceExpirationPicker), 1, 0);

        return layout;
    }

    private TableLayoutPanel CreateFilesLayout()
    {
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(CreateFilePickerPanel("Car Image", _imagePathLabel, _imageBrowseButton, _imageOpenButton), 0, 0);
        layout.Controls.Add(CreateFilePickerPanel("OR/CR Document", _orCrPathLabel, _orCrBrowseButton, _orCrOpenButton), 1, 0);

        return layout;
    }

    private static GroupBox CreateSection(string title, Control content)
    {
        GroupBox section = new()
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 32, 16, 16),
            Font = FontHelper.SemiBold(9.5F),
            ForeColor = ThemeHelper.TextPrimary
        };
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

    private static Panel CreateInputPanel(string labelText, Control inputControl)
    {
        Panel panel = new()
        {
            Dock = DockStyle.Fill,
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

    private static Panel CreateFilePickerPanel(string labelText, Label pathLabel, Button browseButton, Button openButton)
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
        openButton.Location = new Point(102, 24);

        pathLabel.AutoSize = false;
        pathLabel.Location = new Point(204, 29);
        pathLabel.Size = new Size(248, 20);
        pathLabel.Font = FontHelper.Regular(9F);
        pathLabel.ForeColor = ThemeHelper.TextSecondary;
        pathLabel.Text = "No file selected";
        pathLabel.AutoEllipsis = true;

        panel.Controls.Add(titleLabel);
        panel.Controls.Add(browseButton);
        panel.Controls.Add(openButton);
        panel.Controls.Add(pathLabel);

        return panel;
    }

    private void ConfigureInputs()
    {
        _plateNumberTextBox.CharacterCasing = CharacterCasing.Upper;
        _yearTextBox.MaxLength = 4;
        _yearTextBox.KeyPress += AllowDigitsOnly;
        _seatingCapacityTextBox.KeyPress += AllowDigitsOnly;
        _ratePerDayTextBox.KeyPress += AllowDecimalOnly;
        _mileageTextBox.KeyPress += AllowDigitsOnly; // Enforce numbers for mileage

        // Add Placeholder Texts
        _carNameTextBox.PlaceholderText = "e.g. Vios Elite";
        _modelTextBox.PlaceholderText = "e.g. 1.5 G CVT";
        _plateNumberTextBox.PlaceholderText = "e.g. ABC 1234";
        _ratePerDayTextBox.PlaceholderText = "e.g. 2500.00";
        _brandTextBox.PlaceholderText = "e.g. Toyota";
        _yearTextBox.PlaceholderText = "e.g. 2024";
        _colorTextBox.PlaceholderText = "e.g. Pearl White";
        _seatingCapacityTextBox.PlaceholderText = "e.g. 5";
        _mileageTextBox.PlaceholderText = "e.g. 15000";

        _imageBrowseButton.Click += (_, _) => BrowseFile(
            "Select car image",
            "Image files|*.jpg;*.jpeg;*.png;*.webp",
            path =>
            {
                _selectedImageSourcePath = path;
                _imagePathLabel.Text = Path.GetFileName(path);
                _imagePathLabel.ForeColor = ThemeHelper.Primary;
                _imageOpenButton.Enabled = true;
            });

        _orCrBrowseButton.Click += (_, _) => BrowseFile(
            "Select OR/CR document",
            "Supported files|*.jpg;*.jpeg;*.png;*.webp;*.pdf",
            path =>
            {
                _selectedOrCrSourcePath = path;
                _orCrPathLabel.Text = Path.GetFileName(path);
                _orCrPathLabel.ForeColor = ThemeHelper.Primary;
                _orCrOpenButton.Enabled = true;
            });

        _imageOpenButton.Enabled = false;
        _orCrOpenButton.Enabled = false;
        _imageOpenButton.Click += (_, _) => OpenAttachment(
            _selectedImageSourcePath,
            _sourceCar?.ImagePath,
            UploadPathHelper.ResolveCarFilePath,
            "car image");
        _orCrOpenButton.Click += (_, _) => OpenAttachment(
            _selectedOrCrSourcePath,
            _sourceCar?.OrCrPath,
            UploadPathHelper.ResolveCarFilePath,
            "OR/CR document");
    }

    private void LoadCar(Car car)
    {
        _carNameTextBox.Text = car.CarName;
        _modelTextBox.Text = car.Model;
        _plateNumberTextBox.Text = car.PlateNumber;
        _ratePerDayTextBox.Text = car.RatePerDay.ToString("0.##");
        bool hasLegacyStatus = !StatusOptions.Contains(car.Status);
        if (hasLegacyStatus && !_statusComboBox.Items.Contains(car.Status))
        {
            _statusComboBox.Items.Add(car.Status);
        }

        _statusComboBox.SelectedItem = _statusComboBox.Items.Contains(car.Status) ? car.Status : CarConstants.Status.Available;
        if (_mode == CarFormMode.Edit && hasLegacyStatus)
        {
            _statusComboBox.Enabled = false;
        }
        _brandTextBox.Text = car.Brand;
        _yearTextBox.Text = car.Year?.ToString() ?? string.Empty;
        _colorTextBox.Text = car.Color ?? string.Empty;
        _seatingCapacityTextBox.Text = car.SeatingCapacity?.ToString() ?? string.Empty;
        _transmissionComboBox.SelectedItem = car.Transmission ?? "Automatic";
        _fuelTypeComboBox.SelectedItem = car.FuelType ?? "Gasoline";
        _codingDayComboBox.SelectedItem = car.CodingDay ?? "None / Not Applicable";
        _mileageTextBox.Text = car.Mileage?.ToString() ?? string.Empty;
        SetNullableDate(_registrationExpirationPicker, car.RegistrationExpirationDate);
        SetNullableDate(_insuranceExpirationPicker, car.InsuranceExpirationDate);

        _imagePathLabel.Text = string.IsNullOrWhiteSpace(car.ImagePath) ? "No file attached" : Path.GetFileName(car.ImagePath);
        _orCrPathLabel.Text = string.IsNullOrWhiteSpace(car.OrCrPath) ? "No file attached" : Path.GetFileName(car.OrCrPath);
        _imageOpenButton.Enabled = !string.IsNullOrWhiteSpace(car.ImagePath);
        _orCrOpenButton.Enabled = !string.IsNullOrWhiteSpace(car.OrCrPath);
    }

    private void ApplyViewMode()
    {
        foreach (Control control in GetAllControls(this))
        {
            switch (control)
            {
                case TextBox textBox:
                    textBox.ReadOnly = true;
                    textBox.BackColor = Color.FromArgb(248, 250, 252);
                    break;
                case ComboBox comboBox:
                    comboBox.Enabled = false;
                    break;
                case DateTimePicker picker:
                    picker.Enabled = false;
                    break;
            }
        }

        _imageBrowseButton.Enabled = false;
        _orCrBrowseButton.Enabled = false;
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button saveButton)
        {
            return;
        }

        string? newImagePath = null;
        string? newOrCrPath = null;

        try
        {
            saveButton.Enabled = false;
            ClearValidationState();

            EnsureRatePerDayIsValid();

            Car car = BuildCarFromInputs();
            newImagePath = UploadPathHelper.SaveCarFileIfSelected(_selectedImageSourcePath, _sourceCar?.ImagePath, allowPdf: false);
            newOrCrPath = UploadPathHelper.SaveCarFileIfSelected(_selectedOrCrSourcePath, _sourceCar?.OrCrPath, allowPdf: true);
            car.ImagePath = newImagePath;
            car.OrCrPath = newOrCrPath;

            if (_mode == CarFormMode.Edit)
            {
                await _carService.UpdateCarAsync(car);
                MessageBoxHelper.ShowSuccess("Car record updated successfully.");
            }
            else
            {
                await _carService.AddCarAsync(car);
                MessageBoxHelper.ShowSuccess("Car record added successfully.");
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException exception)
        {
            UploadPathHelper.DeleteNewCarUploadIfSaveFailed(newImagePath, _sourceCar?.ImagePath);
            UploadPathHelper.DeleteNewCarUploadIfSaveFailed(newOrCrPath, _sourceCar?.OrCrPath);
            ShowValidationErrors(exception.Errors.ToList(), exception.Message);
        }
        catch (Exception exception)
        {
            UploadPathHelper.DeleteNewCarUploadIfSaveFailed(newImagePath, _sourceCar?.ImagePath);
            UploadPathHelper.DeleteNewCarUploadIfSaveFailed(newOrCrPath, _sourceCar?.OrCrPath);
            MessageBoxHelper.ShowError($"Unable to save car record.\n\n{exception.Message}");
        }
        finally
        {
            saveButton.Enabled = true;
        }
    }

    private Car BuildCarFromInputs()
    {
        return new Car
        {
            CarId = _sourceCar?.CarId ?? 0,
            CarName = _carNameTextBox.Text,
            Brand = _brandTextBox.Text,
            Model = _modelTextBox.Text,
            PlateNumber = _plateNumberTextBox.Text.Trim().ToUpperInvariant(),
            Year = TryParseInt(_yearTextBox.Text),
            Color = NullIfWhiteSpace(_colorTextBox.Text),
            Transmission = NullIfWhiteSpace(_transmissionComboBox.SelectedItem?.ToString()),
            FuelType = NullIfWhiteSpace(_fuelTypeComboBox.SelectedItem?.ToString()),
            SeatingCapacity = TryParseInt(_seatingCapacityTextBox.Text),
            RatePerDay = TryParseDecimal(_ratePerDayTextBox.Text),
            Status = _statusComboBox.SelectedItem?.ToString() ?? "Available",
            CodingDay = NullIfWhiteSpace(_codingDayComboBox.SelectedItem?.ToString()),
            Mileage = TryParseInt(_mileageTextBox.Text), // Included Mileage
            RegistrationExpirationDate = GetNullableDate(_registrationExpirationPicker),
            InsuranceExpirationDate = GetNullableDate(_insuranceExpirationPicker),
            ImagePath = _sourceCar?.ImagePath,
            OrCrPath = _sourceCar?.OrCrPath,
            IsArchived = _sourceCar?.IsArchived ?? false
        };
    }

    private static void BrowseFile(string title, string filter, Action<string> selected)
    {
        using OpenFileDialog dialog = new()
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            selected(dialog.FileName);
        }
    }

    private static void OpenAttachment(
        string? selectedSourcePath,
        string? storedPath,
        Func<string?, string?> resolver,
        string attachmentName)
    {
        string? path = !string.IsNullOrWhiteSpace(selectedSourcePath) && File.Exists(selectedSourcePath)
            ? selectedSourcePath
            : resolver(storedPath);

        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBoxHelper.ShowWarning($"The saved {attachmentName} file could not be found.", "Attachment Missing");
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
            MessageBoxHelper.ShowError($"Unable to open the {attachmentName} file.\n\n{exception.Message}", "Open File");
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
            Control? control = GetControlForProperty(error.PropertyName);
            if (control is not null)
            {
                _errorProvider.SetError(control, error.ErrorMessage);
            }
        }

        if (message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            _errorProvider.SetError(_plateNumberTextBox, message);
            MessageBoxHelper.ShowWarning(message);
            return;
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
            nameof(Car.CarName) => _carNameTextBox,
            nameof(Car.Model) => _modelTextBox,
            nameof(Car.PlateNumber) => _plateNumberTextBox,
            nameof(Car.RatePerDay) => _ratePerDayTextBox,
            nameof(Car.Status) => _statusComboBox,
            nameof(Car.Year) => _yearTextBox,
            nameof(Car.SeatingCapacity) => _seatingCapacityTextBox,
            nameof(Car.Transmission) => _transmissionComboBox,
            nameof(Car.FuelType) => _fuelTypeComboBox,
            nameof(Car.CodingDay) => _codingDayComboBox,
            nameof(Car.Mileage) => _mileageTextBox,
            _ => null
        };
    }

    private static ComboBox CreateComboBox(int width, IEnumerable<string> items)
    {
        ComboBox comboBox = new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextPrimary,
            Width = width,
            Height = 30
        };
        comboBox.Items.AddRange(items.Cast<object>().ToArray());
        comboBox.SelectedIndex = 0;

        return comboBox;
    }

    private static DateTimePicker CreateNullableDatePicker(int width)
    {
        return new DateTimePicker
        {
            Width = width,
            Height = 30,
            Format = DateTimePickerFormat.Short,
            ShowCheckBox = true,
            Checked = false,
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

    private static void SetNullableDate(DateTimePicker picker, DateTime? value)
    {
        picker.Checked = value.HasValue;

        if (value.HasValue)
        {
            picker.Value = value.Value;
        }
    }

    private static DateTime? GetNullableDate(DateTimePicker picker)
    {
        return picker.Checked ? picker.Value.Date : null;
    }

    private static int? TryParseInt(string value)
    {
        return int.TryParse(value.Trim(), out int result) ? result : null;
    }

    private static decimal TryParseDecimal(string value)
    {
        return decimal.TryParse(value.Trim(), out decimal result) ? result : 0M;
    }

    private void EnsureRatePerDayIsValid()
    {
        if (decimal.TryParse(_ratePerDayTextBox.Text.Trim(), out _))
        {
            return;
        }

        throw new ValidationException(
            [new ValidationFailure(nameof(Car.RatePerDay), "Rate per day must be a valid number.")]);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IEnumerable<Control> GetAllControls(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            yield return control;

            foreach (Control child in GetAllControls(control))
            {
                yield return child;
            }
        }
    }

    private static void AllowDigitsOnly(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
        {
            e.Handled = true;
        }
    }

    private static void AllowDecimalOnly(object? sender, KeyPressEventArgs e)
    {
        TextBox? textBox = sender as TextBox;
        bool isDecimalPoint = e.KeyChar == '.';

        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && !isDecimalPoint)
        {
            e.Handled = true;
            return;
        }

        if (isDecimalPoint && textBox is not null && textBox.Text.Contains('.'))
        {
            e.Handled = true;
        }
    }
}
