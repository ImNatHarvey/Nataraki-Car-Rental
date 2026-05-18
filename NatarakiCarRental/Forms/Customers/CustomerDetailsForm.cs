using FluentValidation;
using FluentValidation.Results;
using System.Diagnostics;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.Forms.Customers;

public enum CustomerFormMode
{
    Add,
    Edit,
    View
}

public sealed class CustomerDetailsForm : Form
{
    private const string NotApplicableProvinceCode = "__NA__";
    private const string NotApplicableProvinceName = "N/A";
    private readonly CustomerService _customerService;
    private readonly LocalAddressService _addressService = new();
    private readonly CustomerFormMode _mode;
    private readonly Customer? _sourceCustomer;
    private readonly ErrorProvider _errorProvider = new();

    private readonly TextBox _firstNameTextBox = ControlFactory.CreateTextBox(280);
    private readonly TextBox _lastNameTextBox = ControlFactory.CreateTextBox(280);
    private readonly TextBox _phoneNumberTextBox = ControlFactory.CreateTextBox(280);
    private readonly TextBox _emailTextBox = ControlFactory.CreateTextBox(280);
    private readonly ComboBox _regionComboBox = CreateAddressComboBox();
    private readonly ComboBox _provinceComboBox = CreateAddressComboBox();
    private readonly ComboBox _cityComboBox = CreateAddressComboBox();
    private readonly ComboBox _barangayComboBox = CreateAddressComboBox();
    private readonly TextBox _streetAddressTextBox = ControlFactory.CreateTextBox(280);
    private readonly Label _driverLicensePathLabel = new();
    private readonly Label _proofOfBillingPathLabel = new();
    private readonly Button _driverLicenseBrowseButton = CreateSecondaryButton("Browse", 90, 30);
    private readonly Button _proofOfBillingBrowseButton = CreateSecondaryButton("Browse", 90, 30);
    private readonly Button _driverLicenseOpenButton = CreateSecondaryButton("Open File", 90, 30);
    private readonly Button _proofOfBillingOpenButton = CreateSecondaryButton("Open File", 90, 30);
    private readonly Label _validationLabel = new();

    private string? _selectedDriverLicenseSourcePath;
    private string? _selectedProofOfBillingSourcePath;
    private bool _isInitializingAddress;

    public CustomerDetailsForm(CustomerFormMode mode, Customer? customer = null, int? currentUserId = null)
    {
        _customerService = new CustomerService(currentUserId);
        _mode = mode;
        _sourceCustomer = customer;
        InitializeForm();
        ConfigureInputs();
        Shown += async (_, _) => await InitializeAddressSelectorsAsync();

        if (customer is not null)
        {
            LoadCustomer(customer);
        }

        if (_mode == CustomerFormMode.View)
        {
            ApplyViewMode();
        }
    }

    private bool IsViewMode => _mode == CustomerFormMode.View;

    private void InitializeForm()
    {
        string title = _mode switch
        {
            CustomerFormMode.Edit => "Edit Customer",
            CustomerFormMode.View => "View Customer",
            _ => "Add Customer"
        };

        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(1060, 760);
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
            Size = new Size(280, 34),
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Label subtitleLabel = new()
        {
            Text = _mode switch
            {
                CustomerFormMode.Edit => "Update customer contact details and attached documents.",
                CustomerFormMode.View => "Review this customer record and saved documents.",
                _ => "Create a customer record for rentals and document tracking."
            },
            AutoSize = false,
            Location = new Point(34, 58),
            Size = new Size(650, 24),
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
            Size = new Size(996, 540),
            BackColor = ThemeHelper.Surface
        };

        TableLayoutPanel contentLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };

        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 125F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 260F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 125F));

        contentLayout.Controls.Add(CreateSection("Required Information", CreateRequiredLayout()), 0, 0);
        contentLayout.Controls.Add(CreateSection("Contact & Address", CreateContactLayout()), 0, 1);
        contentLayout.Controls.Add(CreateSection("Documents", CreateDocumentsLayout()), 0, 2);
        contentPanel.Controls.Add(contentLayout);

        Button cancelButton = CreateSecondaryButton(IsViewMode ? "Close" : "Cancel", 118, 38);
        cancelButton.Location = new Point(IsViewMode ? 910 : 756, 680);
        cancelButton.DialogResult = DialogResult.Cancel;

        Button? saveButton = null;
        if (!IsViewMode)
        {
            saveButton = ControlFactory.CreatePrimaryButton(_mode == CustomerFormMode.Edit ? "Save Changes" : "Add Customer", 140, 38);
            saveButton.Location = new Point(888, 680);
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

    private TableLayoutPanel CreateRequiredLayout()
    {
        TableLayoutPanel layout = CreateGrid(3, 1);
        layout.Controls.Add(CreateInputPanel("First Name *", _firstNameTextBox), 0, 0);
        layout.Controls.Add(CreateInputPanel("Last Name *", _lastNameTextBox), 1, 0);
        layout.Controls.Add(CreateInputPanel("Phone Number *", _phoneNumberTextBox), 2, 0);
        return layout;
    }

    private TableLayoutPanel CreateContactLayout()
    {
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));

        layout.Controls.Add(CreateInputPanel("Email", _emailTextBox), 0, 0);
        layout.Controls.Add(CreateInputPanel("Street / House / Block No.", _streetAddressTextBox), 1, 0);
        layout.Controls.Add(CreateInputPanel("Region", _regionComboBox), 0, 1);
        layout.Controls.Add(CreateInputPanel("Province", _provinceComboBox), 1, 1);
        layout.Controls.Add(CreateInputPanel("City / Municipality", _cityComboBox), 2, 1);
        layout.Controls.Add(CreateInputPanel("Barangay", _barangayComboBox), 0, 2);

        return layout;
    }

    private TableLayoutPanel CreateDocumentsLayout()
    {
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        layout.Controls.Add(CreateFilePickerPanel("Driver's License Document", _driverLicensePathLabel, _driverLicenseBrowseButton, _driverLicenseOpenButton), 0, 0);
        layout.Controls.Add(CreateFilePickerPanel("Proof of Billing Document", _proofOfBillingPathLabel, _proofOfBillingBrowseButton, _proofOfBillingOpenButton), 1, 0);
        return layout;
    }

    private static GroupBox CreateSection(string title, Control content)
    {
        GroupBox section = new()
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 30, 16, 12),
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
            BackColor = ThemeHelper.Surface
        };

        Label label = ControlFactory.CreateInputLabel(labelText);
        label.Location = new Point(0, 0);
        browseButton.Location = new Point(0, 28);
        openButton.Location = new Point(102, 28);

        pathLabel.AutoSize = false;
        pathLabel.Location = new Point(204, 33);
        pathLabel.Size = new Size(168, 20);
        pathLabel.Font = FontHelper.Regular(9F);
        pathLabel.ForeColor = ThemeHelper.TextSecondary;
        pathLabel.Text = "No file selected";
        pathLabel.AutoEllipsis = true;

        panel.Controls.Add(label);
        panel.Controls.Add(browseButton);
        panel.Controls.Add(openButton);
        panel.Controls.Add(pathLabel);
        return panel;
    }

    private void ConfigureInputs()
    {
        _firstNameTextBox.PlaceholderText = "e.g. Juan";
        _lastNameTextBox.PlaceholderText = "e.g. Dela Cruz";
        _phoneNumberTextBox.PlaceholderText = "e.g. 09171234567";
        _emailTextBox.PlaceholderText = "e.g. customer@email.com";
        _streetAddressTextBox.PlaceholderText = "e.g. Block 3 Lot 8, Mabini Street";

        ResetComboBox(_regionComboBox, "Loading regions...", false);
        ResetComboBox(_provinceComboBox, "Select a region first", false);
        ResetComboBox(_cityComboBox, "Select a province first", false);
        ResetComboBox(_barangayComboBox, "Select a city first", false);

        _regionComboBox.SelectedIndexChanged += RegionComboBox_SelectedIndexChanged;
        _provinceComboBox.SelectedIndexChanged += ProvinceComboBox_SelectedIndexChanged;
        _cityComboBox.SelectedIndexChanged += CityComboBox_SelectedIndexChanged;

        _driverLicenseBrowseButton.Click += (_, _) => BrowseFile(
            "Select driver's license document",
            "Supported files|*.jpg;*.jpeg;*.png;*.webp;*.pdf",
            path =>
            {
                _selectedDriverLicenseSourcePath = path;
                _driverLicensePathLabel.Text = Path.GetFileName(path);
                _driverLicensePathLabel.ForeColor = ThemeHelper.Primary;
                _driverLicenseOpenButton.Enabled = true;
            });

        _proofOfBillingBrowseButton.Click += (_, _) => BrowseFile(
            "Select proof of billing document",
            "Supported files|*.jpg;*.jpeg;*.png;*.webp;*.pdf",
            path =>
            {
                _selectedProofOfBillingSourcePath = path;
                _proofOfBillingPathLabel.Text = Path.GetFileName(path);
                _proofOfBillingPathLabel.ForeColor = ThemeHelper.Primary;
                _proofOfBillingOpenButton.Enabled = true;
            });

        _driverLicenseOpenButton.Enabled = false;
        _proofOfBillingOpenButton.Enabled = false;
        _driverLicenseOpenButton.Click += (_, _) => OpenAttachment(
            _selectedDriverLicenseSourcePath,
            _sourceCustomer?.DriverLicensePath,
            "driver's license document");
        _proofOfBillingOpenButton.Click += (_, _) => OpenAttachment(
            _selectedProofOfBillingSourcePath,
            _sourceCustomer?.ProofOfBillingPath,
            "proof of billing document");
    }

    private void LoadCustomer(Customer customer)
    {
        _firstNameTextBox.Text = customer.FirstName;
        _lastNameTextBox.Text = customer.LastName;
        _phoneNumberTextBox.Text = customer.PhoneNumber;
        _emailTextBox.Text = customer.Email ?? string.Empty;
        _streetAddressTextBox.Text = customer.StreetAddress ?? string.Empty;
        _driverLicensePathLabel.Text = string.IsNullOrWhiteSpace(customer.DriverLicensePath)
            ? "No file attached"
            : Path.GetFileName(customer.DriverLicensePath);
        _proofOfBillingPathLabel.Text = string.IsNullOrWhiteSpace(customer.ProofOfBillingPath)
            ? "No file attached"
            : Path.GetFileName(customer.ProofOfBillingPath);
        _driverLicenseOpenButton.Enabled = !string.IsNullOrWhiteSpace(customer.DriverLicensePath);
        _proofOfBillingOpenButton.Enabled = !string.IsNullOrWhiteSpace(customer.ProofOfBillingPath);
    }

    private void ApplyViewMode()
    {
        foreach (Control control in GetAllControls(this))
        {
            if (control is TextBox textBox)
            {
                textBox.ReadOnly = true;
                textBox.BackColor = Color.FromArgb(248, 250, 252);
            }
        }

        _driverLicenseBrowseButton.Enabled = false;
        _proofOfBillingBrowseButton.Enabled = false;
        _regionComboBox.Enabled = false;
        _provinceComboBox.Enabled = false;
        _cityComboBox.Enabled = false;
        _barangayComboBox.Enabled = false;
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button saveButton)
        {
            return;
        }

        string? newDriverLicensePath = null;
        string? newProofOfBillingPath = null;

        try
        {
            saveButton.Enabled = false;
            ClearValidationState();

            Customer customer = BuildCustomerFromInputs();
            newDriverLicensePath = UploadPathHelper.SaveCustomerFileIfSelected(_selectedDriverLicenseSourcePath, _sourceCustomer?.DriverLicensePath);
            newProofOfBillingPath = UploadPathHelper.SaveCustomerFileIfSelected(_selectedProofOfBillingSourcePath, _sourceCustomer?.ProofOfBillingPath);
            customer.DriverLicensePath = newDriverLicensePath;
            customer.ProofOfBillingPath = newProofOfBillingPath;

            if (_mode == CustomerFormMode.Edit)
            {
                await _customerService.UpdateCustomerAsync(customer);
                MessageBoxHelper.ShowSuccess("Customer record updated successfully.");
            }
            else
            {
                await _customerService.AddCustomerAsync(customer);
                MessageBoxHelper.ShowSuccess("Customer record added successfully.");
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException exception)
        {
            UploadPathHelper.DeleteNewCustomerUploadIfSaveFailed(newDriverLicensePath, _sourceCustomer?.DriverLicensePath);
            UploadPathHelper.DeleteNewCustomerUploadIfSaveFailed(newProofOfBillingPath, _sourceCustomer?.ProofOfBillingPath);
            ShowValidationErrors(exception.Errors.ToList(), exception.Message);
        }
        catch (Exception exception)
        {
            UploadPathHelper.DeleteNewCustomerUploadIfSaveFailed(newDriverLicensePath, _sourceCustomer?.DriverLicensePath);
            UploadPathHelper.DeleteNewCustomerUploadIfSaveFailed(newProofOfBillingPath, _sourceCustomer?.ProofOfBillingPath);
            MessageBoxHelper.ShowError($"Unable to save customer record.\n\n{exception.Message}");
        }
        finally
        {
            saveButton.Enabled = true;
        }
    }

    private Customer BuildCustomerFromInputs()
    {
        return new Customer
        {
            CustomerId = _sourceCustomer?.CustomerId ?? 0,
            FirstName = _firstNameTextBox.Text,
            LastName = _lastNameTextBox.Text,
            PhoneNumber = _phoneNumberTextBox.Text,
            Email = NullIfWhiteSpace(_emailTextBox.Text),
            Region = GetSelectedAddressName(_regionComboBox),
            Province = GetSelectedAddressName(_provinceComboBox),
            City = GetSelectedAddressName(_cityComboBox),
            Barangay = GetSelectedAddressName(_barangayComboBox),
            StreetAddress = NullIfWhiteSpace(_streetAddressTextBox.Text),
            IsBlacklisted = _sourceCustomer?.IsBlacklisted ?? false,
            BlacklistReason = _sourceCustomer?.BlacklistReason,
            DriverLicensePath = _sourceCustomer?.DriverLicensePath,
            ProofOfBillingPath = _sourceCustomer?.ProofOfBillingPath,
            IsArchived = _sourceCustomer?.IsArchived ?? false
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

    private static void OpenAttachment(string? selectedSourcePath, string? storedPath, string attachmentName)
    {
        string? path = !string.IsNullOrWhiteSpace(selectedSourcePath) && File.Exists(selectedSourcePath)
            ? selectedSourcePath
            : UploadPathHelper.ResolveCustomerFilePath(storedPath);

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
            _errorProvider.SetError(_phoneNumberTextBox, message);
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
            nameof(Customer.FirstName) => _firstNameTextBox,
            nameof(Customer.LastName) => _lastNameTextBox,
            nameof(Customer.PhoneNumber) => _phoneNumberTextBox,
            nameof(Customer.Email) => _emailTextBox,
            nameof(Customer.Region) => _regionComboBox,
            nameof(Customer.Province) => _provinceComboBox,
            nameof(Customer.City) => _cityComboBox,
            nameof(Customer.Barangay) => _barangayComboBox,
            _ => null
        };
    }

    private async Task InitializeAddressSelectorsAsync()
    {
        try
        {
            _isInitializingAddress = true;
            IReadOnlyList<PsgcRegionDto> regions = await _addressService.GetRegionsAsync();
            SetAddressItems(
                _regionComboBox,
                regions.Select(region => new AddressOption(region.Code, region.Name)),
                "Select a region",
                !IsViewMode);

            if (_sourceCustomer is not null)
            {
                await RestoreSavedAddressAsync(_sourceCustomer);
            }
        }
        catch (Exception exception)
        {
            ResetComboBox(_regionComboBox, "Unable to load regions", false);
            ResetComboBox(_provinceComboBox, "Address lookup unavailable", false);
            ResetComboBox(_cityComboBox, "Address lookup unavailable", false);
            ResetComboBox(_barangayComboBox, "Address lookup unavailable", false);
            MessageBoxHelper.ShowWarning(exception.Message, "Address Lookup");
        }
        finally
        {
            _isInitializingAddress = false;
        }
    }

    private async Task RestoreSavedAddressAsync(Customer customer)
    {
        if (!SelectByName(_regionComboBox, customer.Region))
        {
            return;
        }

        AddressOption region = (AddressOption)_regionComboBox.SelectedItem!;
        await LoadProvincesAsync(region.Code, customer.Province);

        if (_provinceComboBox.SelectedItem is not AddressOption province)
        {
            return;
        }

        if (IsNotApplicableProvince(province))
        {
            await LoadCitiesByRegionAsync(region.Code, customer.City);
        }
        else
        {
            await LoadCitiesAsync(province.Code, customer.City);
        }

        if (_cityComboBox.SelectedItem is not AddressOption city)
        {
            return;
        }

        await LoadBarangaysAsync(city.Code, customer.Barangay);
    }

    private async void RegionComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isInitializingAddress)
        {
            return;
        }

        if (_regionComboBox.SelectedItem is not AddressOption region)
        {
            ResetComboBox(_provinceComboBox, "Select a region first", false);
            ResetComboBox(_cityComboBox, "Select a province first", false);
            ResetComboBox(_barangayComboBox, "Select a city first", false);
            return;
        }

        await LoadProvincesAsync(region.Code);
    }

    private async void ProvinceComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isInitializingAddress)
        {
            return;
        }

        if (_provinceComboBox.SelectedItem is not AddressOption province)
        {
            ResetComboBox(_cityComboBox, "Select a province first", false);
            ResetComboBox(_barangayComboBox, "Select a city first", false);
            return;
        }

        if (IsNotApplicableProvince(province))
        {
            return;
        }

        await LoadCitiesAsync(province.Code);
    }

    private async void CityComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isInitializingAddress)
        {
            return;
        }

        if (_cityComboBox.SelectedItem is not AddressOption city)
        {
            ResetComboBox(_barangayComboBox, "Select a city first", false);
            return;
        }

        await LoadBarangaysAsync(city.Code);
    }

    private async Task LoadProvincesAsync(string regionCode, string? selectedName = null)
    {
        ResetComboBox(_provinceComboBox, "Loading provinces...", false);
        ResetComboBox(_cityComboBox, "Select a province first", false);
        ResetComboBox(_barangayComboBox, "Select a city first", false);

        try
        {
            IReadOnlyList<PsgcProvinceDto> provinces = await _addressService.GetProvincesByRegionAsync(regionCode);

            if (provinces.Count == 0)
            {
                SetNotApplicableProvince();
                await LoadCitiesByRegionAsync(regionCode);
                return;
            }

            SetAddressItems(
                _provinceComboBox,
                provinces.Select(province => new AddressOption(province.Code, province.Name)),
                "Select a province",
                !IsViewMode);
            SelectByName(_provinceComboBox, selectedName);
        }
        catch (Exception exception)
        {
            ResetComboBox(_provinceComboBox, "Unable to load provinces", false);
            MessageBoxHelper.ShowWarning(exception.Message, "Address Lookup");
        }
    }

    private async Task LoadCitiesByRegionAsync(string regionCode, string? selectedName = null)
    {
        ResetComboBox(_cityComboBox, "Loading cities...", false);
        ResetComboBox(_barangayComboBox, "Select a city first", false);

        try
        {
            IReadOnlyList<PsgcCityMunicipalityDto> cities = await _addressService.GetCitiesByRegionAsync(regionCode);
            SetAddressItems(
                _cityComboBox,
                cities.Select(city => new AddressOption(city.Code, city.Name)),
                "Select a city / municipality",
                !IsViewMode);
            SelectByName(_cityComboBox, selectedName);
        }
        catch (Exception exception)
        {
            ResetComboBox(_cityComboBox, "Unable to load cities", false);
            MessageBoxHelper.ShowWarning(exception.Message, "Address Lookup");
        }
    }

    private async Task LoadCitiesAsync(string provinceCode, string? selectedName = null)
    {
        ResetComboBox(_cityComboBox, "Loading cities...", false);
        ResetComboBox(_barangayComboBox, "Select a city first", false);

        try
        {
            IReadOnlyList<PsgcCityMunicipalityDto> cities = await _addressService.GetCitiesByProvinceAsync(provinceCode);
            SetAddressItems(
                _cityComboBox,
                cities.Select(city => new AddressOption(city.Code, city.Name)),
                "Select a city / municipality",
                !IsViewMode);
            SelectByName(_cityComboBox, selectedName);
        }
        catch (Exception exception)
        {
            ResetComboBox(_cityComboBox, "Unable to load cities", false);
            MessageBoxHelper.ShowWarning(exception.Message, "Address Lookup");
        }
    }

    private async Task LoadBarangaysAsync(string cityCode, string? selectedName = null)
    {
        ResetComboBox(_barangayComboBox, "Loading barangays...", false);

        try
        {
            IReadOnlyList<PsgcBarangayDto> barangays = await _addressService.GetBarangaysByCityAsync(cityCode);
            SetAddressItems(
                _barangayComboBox,
                barangays.Select(barangay => new AddressOption(barangay.Code, barangay.Name)),
                "Select a barangay",
                !IsViewMode);
            SelectByName(_barangayComboBox, selectedName);
        }
        catch (Exception exception)
        {
            ResetComboBox(_barangayComboBox, "Unable to load barangays", false);
            MessageBoxHelper.ShowWarning(exception.Message, "Address Lookup");
        }
    }

    private static ComboBox CreateAddressComboBox()
    {
        return new ComboBox
        {
            Width = 280,
            Height = 30,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = FontHelper.Regular(10F)
        };
    }

    private static void ResetComboBox(ComboBox comboBox, string text, bool enabled)
    {
        comboBox.BeginUpdate();
        comboBox.Items.Clear();
        comboBox.Items.Add(text);
        comboBox.SelectedIndex = 0;
        comboBox.Enabled = enabled;
        comboBox.EndUpdate();
    }

    private static void SetAddressItems(
        ComboBox comboBox,
        IEnumerable<AddressOption> options,
        string placeholder,
        bool enabled)
    {
        comboBox.BeginUpdate();
        comboBox.Items.Clear();
        comboBox.Items.Add(placeholder);
        comboBox.Items.AddRange(options.OrderBy(option => option.Name).Cast<object>().ToArray());
        comboBox.SelectedIndex = 0;
        comboBox.Enabled = enabled;
        comboBox.EndUpdate();
    }

    private void SetNotApplicableProvince()
    {
        _provinceComboBox.BeginUpdate();
        _provinceComboBox.Items.Clear();
        _provinceComboBox.Items.Add(new AddressOption(NotApplicableProvinceCode, NotApplicableProvinceName));
        _provinceComboBox.SelectedIndex = 0;
        _provinceComboBox.Enabled = false;
        _provinceComboBox.EndUpdate();
    }

    private static bool SelectByName(ComboBox comboBox, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        AddressOption? option = comboBox.Items
            .OfType<AddressOption>()
            .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

        if (option is null)
        {
            return false;
        }

        comboBox.SelectedItem = option;
        return true;
    }

    private static string? GetSelectedAddressName(ComboBox comboBox)
    {
        return comboBox.SelectedItem is AddressOption option ? option.Name : null;
    }

    private static bool IsNotApplicableProvince(AddressOption option)
    {
        return string.Equals(option.Code, NotApplicableProvinceCode, StringComparison.Ordinal);
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

    private sealed record AddressOption(string Code, string Name)
    {
        public override string ToString() => Name;
    }
}
