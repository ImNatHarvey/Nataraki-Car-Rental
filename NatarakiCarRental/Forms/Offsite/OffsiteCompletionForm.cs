using FluentValidation;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using System.Diagnostics;

namespace NatarakiCarRental.Forms.Offsite;

public sealed class OffsiteCompletionForm : Form
{
    private const int FormWidth = 760;
    private const int FormHeight = 720;
    private const int InputHeight = 28;
    private const int InputWidth = 280;

    private readonly int _currentUserId;
    private readonly int _recordId;
    private readonly OffsiteService _offsiteService;
    private readonly CarService _carService;

    private OffsiteRecord? _record;
    private string? _selectedProofPath;

    private readonly Label _carLabel = CreateValueLabel();
    private readonly Label _typeLabel = CreateValueLabel();
    private readonly Label _startDateLabel = CreateValueLabel();
    private readonly Label _expectedReturnLabel = CreateValueLabel();
    private readonly Label _locationLabel = CreateValueLabel();
    private readonly Label _statusLabel = CreateValueLabel();

    private readonly DateTimePicker _completedDatePicker = CreateDatePicker();
    private readonly ComboBox _workResultComboBox = CreateComboBox();
    private readonly CheckBox _followUpCheckBox = new();
    private readonly NumericUpDown _amountPaidInput = CreateMoneyInput();
    private readonly Label _proofPathLabel = CreatePathLabel();
    private readonly Button _browseProofButton = CreateSecondaryButton("Browse", 90, InputHeight);
    private readonly Button _openProofButton = CreateSecondaryButton("Open File", 90, InputHeight);
    private readonly GroupBox _followUpGroup = CreateSection("Follow-up Details", new Point(32, 486), new Size(696, 146));
    private readonly ComboBox _followUpReasonComboBox = CreateFollowUpReasonComboBox();
    private readonly Label _customFollowUpReasonLabel = ControlFactory.CreateInputLabel("Custom Reason");
    private readonly TextBox _customFollowUpReasonTextBox = ControlFactory.CreateTextBox(620);
    private readonly Button _completeButton = ControlFactory.CreatePrimaryButton("Complete Offsite Record", 190, 38);
    private readonly Button _cancelButton = CreateSecondaryButton("Cancel", 118, 38);

    public OffsiteCompletionForm(int currentUserId, int recordId)
    {
        _currentUserId = currentUserId;
        _recordId = recordId;
        _offsiteService = new OffsiteService(currentUserId);
        _carService = new CarService(currentUserId);

        InitializeComponent();
        SetupEvents();
    }

    private void InitializeComponent()
    {
        Text = "Complete Offsite Record";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(FormWidth, FormHeight);
        BackColor = ThemeHelper.Surface;
        Font = FontHelper.Regular();
        ShowInTaskbar = false;

        Controls.Add(new Label
        {
            Text = Text,
            AutoSize = false,
            Location = new Point(32, 22),
            Size = new Size(420, 34),
            Font = FontHelper.Title(18F),
            ForeColor = ThemeHelper.TextPrimary
        });

        Controls.Add(new Label
        {
            Text = "Record return details, final result, amount paid, and proof for audit.",
            AutoSize = false,
            Location = new Point(34, 56),
            Size = new Size(620, 24),
            Font = FontHelper.Regular(9.5F),
            ForeColor = ThemeHelper.TextSecondary
        });

        Controls.Add(CreateSummarySection());
        Controls.Add(CreateCompletionSection());
        Controls.Add(CreatePaymentSection());
        Controls.Add(CreateFollowUpSection());

        _cancelButton.Location = new Point(ClientSize.Width - 32 - _completeButton.Width - 16 - _cancelButton.Width, ClientSize.Height - 58);
        _completeButton.Location = new Point(ClientSize.Width - 32 - _completeButton.Width, ClientSize.Height - 58);
        Controls.Add(_cancelButton);
        Controls.Add(_completeButton);
        CancelButton = _cancelButton;

        _openProofButton.Enabled = false;
        Click += (_, _) => ActiveControl = null;
        UpdateFollowUpVisibility();
    }

    private GroupBox CreateSummarySection()
    {
        GroupBox group = CreateSection("Offsite Summary", new Point(32, 94), new Size(696, 130));
        AddDisplay(group, "Car / Plate", _carLabel, new Point(24, 30), 270);
        AddDisplay(group, "Offsite Type", _typeLabel, new Point(300, 30), 160);
        AddDisplay(group, "Start Date", _startDateLabel, new Point(550, 30), 140);
        AddDisplay(group, "Expected Return", _expectedReturnLabel, new Point(24, 80), 170);
        AddDisplay(group, "Location", _locationLabel, new Point(300, 80), 160);
        AddDisplay(group, "Status", _statusLabel, new Point(550, 80), 140);
        return group;
    }

    private GroupBox CreateCompletionSection()
    {
        GroupBox group = CreateSection("Completion Details", new Point(32, 238), new Size(696, 126));
        AddInput(group, "Completed Date *", _completedDatePicker, new Point(20, 32), InputWidth);
        AddInput(group, "Work Result *", _workResultComboBox, new Point(360, 32), InputWidth);

        _followUpCheckBox.Text = "Follow-up Required";
        _followUpCheckBox.AutoSize = true;
        _followUpCheckBox.Font = FontHelper.SemiBold(9.5F);
        _followUpCheckBox.ForeColor = ThemeHelper.TextPrimary;
        _followUpCheckBox.Location = new Point(360, 88);
        group.Controls.Add(_followUpCheckBox);

        return group;
    }

    private GroupBox CreatePaymentSection()
    {
        GroupBox group = CreateSection("Payment Information", new Point(32, 378), new Size(696, 94));
        AddInput(group, "Amount Paid (\u20B1)", _amountPaidInput, new Point(20, 32), InputWidth);
        AddProofPicker(group, "Proof / Receipt", new Point(360, 32));
        return group;
    }

    private GroupBox CreateFollowUpSection()
    {
        AddInput(_followUpGroup, "Follow-up Reason", _followUpReasonComboBox, new Point(20, 42), InputWidth);
        _customFollowUpReasonLabel.Location = new Point(360, 42);
        _customFollowUpReasonTextBox.Location = new Point(360, 64);
        _customFollowUpReasonTextBox.Size = new Size(InputWidth, InputHeight);
        _customFollowUpReasonTextBox.Font = FontHelper.Regular(10F);
        _followUpGroup.Controls.Add(_customFollowUpReasonLabel);
        _followUpGroup.Controls.Add(_customFollowUpReasonTextBox);
        return _followUpGroup;
    }

    private void SetupEvents()
    {
        Load += async (_, _) => await LoadDataAsync();
        _cancelButton.Click += (_, _) => Close();
        _completeButton.Click += async (_, _) => await CompleteAsync();
        _browseProofButton.Click += (_, _) => BrowseProof();
        _openProofButton.Click += (_, _) => OpenProof();
        _workResultComboBox.SelectedIndexChanged += (_, _) => WorkResultChanged();
        _followUpCheckBox.CheckedChanged += (_, _) => UpdateFollowUpVisibility();
        _followUpReasonComboBox.SelectedIndexChanged += (_, _) => UpdateFollowUpVisibility();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _record = await _offsiteService.GetByIdAsync(_recordId);
            if (_record is null)
            {
                MessageBoxHelper.ShowWarning("The selected offsite record no longer exists.");
                Close();
                return;
            }

            Car? car = await _carService.GetCarByIdAsync(_record.CarId);
            _carLabel.Text = car is null ? $"Car #{_record.CarId}" : $"{car.CarName} ({car.PlateNumber})";
            _typeLabel.Text = _record.OffsiteType;
            _startDateLabel.Text = _record.StartDate.ToString("MMM d, yyyy");
            _expectedReturnLabel.Text = _record.ExpectedReturnDate?.ToString("MMM d, yyyy") ?? "-";
            _locationLabel.Text = string.IsNullOrWhiteSpace(_record.LocationName) ? "-" : _record.LocationName;
            _statusLabel.Text = _record.Status;

            _completedDatePicker.MinDate = _record.StartDate.Date;
            if (_completedDatePicker.Value.Date < _record.StartDate.Date)
            {
                _completedDatePicker.Value = _record.StartDate.Date;
            }

            _amountPaidInput.Value = _record.ActualCost;
            if (!string.IsNullOrWhiteSpace(_record.ProofFilePath))
            {
                _proofPathLabel.Text = Path.GetFileName(_record.ProofFilePath);
                _proofPathLabel.ForeColor = ThemeHelper.Primary;
                _openProofButton.Enabled = true;
            }
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to load completion form.\n\n{exception.Message}", "Offsite Completion");
            Close();
        }
    }

    private async Task CompleteAsync()
    {
        try
        {
            await _offsiteService.CompleteAsync(new CompleteOffsiteRecordRequest
            {
                OffsiteRecordId = _recordId,
                CompletedDate = _completedDatePicker.Value.Date,
                WorkResult = _workResultComboBox.SelectedItem?.ToString() ?? string.Empty,
                AmountPaid = _amountPaidInput.Value,
                ProofFilePath = _selectedProofPath ?? _record?.ProofFilePath,
                FollowUpRequired = _followUpCheckBox.Checked,
                FollowUpReason = GetSelectedFollowUpReason(),
                SuggestedNextAction = null,
                CompletedByUserId = _currentUserId
            });

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException exception)
        {
            MessageBoxHelper.ShowWarning(exception.Errors.FirstOrDefault()?.ErrorMessage ?? exception.Message, "Offsite Completion");
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to complete offsite record.\n\n{exception.Message}", "Offsite Completion");
        }
    }

    private void BrowseProof()
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "Image/PDF Files|*.jpg;*.jpeg;*.png;*.pdf",
            Title = "Select proof or receipt",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _selectedProofPath = dialog.FileName;
        _proofPathLabel.Text = Path.GetFileName(dialog.FileName);
        _proofPathLabel.ForeColor = ThemeHelper.Primary;
        _openProofButton.Enabled = true;
    }

    private void OpenProof()
    {
        string? path = !string.IsNullOrWhiteSpace(_selectedProofPath) && File.Exists(_selectedProofPath)
            ? _selectedProofPath
            : UploadPathHelper.ResolveOffsiteProofPath(_record?.ProofFilePath);

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBoxHelper.ShowWarning("The proof file could not be found.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Could not open file: {exception.Message}");
        }
    }

    private void UpdateFollowUpVisibility()
    {
        string? workResult = _workResultComboBox.SelectedItem?.ToString();
        bool resultRequiresFollowUp = IsFollowUpWorkResult(workResult);

        if (resultRequiresFollowUp)
        {
            _followUpCheckBox.Checked = true;
        }

        bool enabled = resultRequiresFollowUp || _followUpCheckBox.Checked;
        bool isOther = string.Equals(_followUpReasonComboBox.SelectedItem?.ToString(), "Other", StringComparison.OrdinalIgnoreCase);

        _followUpGroup.Enabled = true;
        _followUpGroup.Visible = true;
        _followUpReasonComboBox.Enabled = enabled;
        _customFollowUpReasonLabel.Enabled = enabled && isOther;
        _customFollowUpReasonTextBox.Enabled = enabled && isOther;

        if (!enabled)
        {
            if (_followUpReasonComboBox.SelectedIndex != 0)
            {
                _followUpReasonComboBox.SelectedIndex = 0;
            }

            _customFollowUpReasonTextBox.Clear();
        }
    }

    private string? GetSelectedFollowUpReason()
    {
        if (!_followUpReasonComboBox.Enabled)
        {
            return null;
        }

        string reason = _followUpReasonComboBox.SelectedItem?.ToString() ?? string.Empty;
        if (string.Equals(reason, "Other", StringComparison.OrdinalIgnoreCase))
        {
            return _customFollowUpReasonTextBox.Text;
        }

        return string.Equals(reason, "Select a reason", StringComparison.OrdinalIgnoreCase) ? null : reason;
    }

    private void WorkResultChanged()
    {
        if (!IsFollowUpWorkResult(_workResultComboBox.SelectedItem?.ToString()))
        {
            _followUpCheckBox.Checked = false;
        }

        UpdateFollowUpVisibility();
    }

    private static bool IsFollowUpWorkResult(string? workResult)
    {
        return string.Equals(workResult, "Needs Follow-up", StringComparison.OrdinalIgnoreCase)
            || string.Equals(workResult, "Not Repaired", StringComparison.OrdinalIgnoreCase);
    }

    private static GroupBox CreateSection(string title, Point location, Size size)
    {
        return new GroupBox
        {
            Text = title,
            Location = location,
            Size = size,
            Font = FontHelper.SemiBold(9.5F),
            ForeColor = ThemeHelper.TextPrimary,
            BackColor = ThemeHelper.Surface
        };
    }

    private static void AddInput(Control parent, string labelText, Control input, Point labelLocation, int width)
    {
        Label label = ControlFactory.CreateInputLabel(labelText);
        label.Location = labelLocation;
        input.Location = new Point(labelLocation.X, labelLocation.Y + 22);
        input.Size = new Size(width, InputHeight);
        input.Font = FontHelper.Regular(10F);
        parent.Controls.Add(label);
        parent.Controls.Add(input);
    }

    private static void AddDisplay(Control parent, string labelText, Label valueLabel, Point labelLocation, int width)
    {
        Label label = new()
        {
            Text = labelText,
            Location = labelLocation,
            Size = new Size(width, 18),
            Font = FontHelper.SemiBold(9F),
            ForeColor = ThemeHelper.TextSecondary
        };
        valueLabel.Location = new Point(labelLocation.X, labelLocation.Y + 22);
        valueLabel.Size = new Size(width, 24);
        parent.Controls.Add(label);
        parent.Controls.Add(valueLabel);
    }

    private void AddProofPicker(Control parent, string labelText, Point labelLocation)
    {
        Label label = ControlFactory.CreateInputLabel(labelText);
        label.Location = labelLocation;
        _browseProofButton.Location = new Point(labelLocation.X, labelLocation.Y + 22);
        _openProofButton.Location = new Point(labelLocation.X + 98, labelLocation.Y + 22);
        _proofPathLabel.Location = new Point(labelLocation.X + 200, labelLocation.Y + 26);
        _proofPathLabel.Size = new Size(130, 20);
        parent.Controls.Add(label);
        parent.Controls.Add(_browseProofButton);
        parent.Controls.Add(_openProofButton);
        parent.Controls.Add(_proofPathLabel);
    }

    private static ComboBox CreateComboBox()
    {
        ComboBox comboBox = new()
        {
            Width = InputWidth,
            Height = InputHeight,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextPrimary
        };
        comboBox.Items.AddRange(["Completed", "Needs Follow-up", "Not Repaired"]);
        comboBox.SelectedIndex = 0;
        return comboBox;
    }

    private static ComboBox CreateFollowUpReasonComboBox()
    {
        ComboBox comboBox = new()
        {
            Width = InputWidth,
            Height = InputHeight,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextPrimary
        };
        comboBox.Items.AddRange([
            "Select a reason",
            "Additional repair needed",
            "Parts unavailable",
            "Issue not fully resolved",
            "Needs further inspection",
            "Customer approval required",
            "Other"
        ]);
        comboBox.SelectedIndex = 0;
        return comboBox;
    }

    private static DateTimePicker CreateDatePicker()
    {
        return new DateTimePicker
        {
            Width = InputWidth,
            Height = InputHeight,
            Format = DateTimePickerFormat.Short,
            Font = FontHelper.Regular(10F)
        };
    }

    private static NumericUpDown CreateMoneyInput()
    {
        return new NumericUpDown
        {
            Width = InputWidth,
            Height = InputHeight,
            DecimalPlaces = 2,
            Maximum = 1000000,
            Increment = 1000,
            ThousandsSeparator = true,
            Font = FontHelper.Regular(10F)
        };
    }

    private static Label CreateValueLabel()
    {
        return new Label
        {
            Text = "-",
            AutoSize = false,
            Font = FontHelper.Regular(10F),
            ForeColor = ThemeHelper.TextPrimary,
            AutoEllipsis = true
        };
    }

    private static Label CreatePathLabel()
    {
        return new Label
        {
            Text = "No file selected",
            AutoSize = false,
            Font = FontHelper.Regular(9F),
            ForeColor = ThemeHelper.TextSecondary,
            AutoEllipsis = true
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
            Font = FontHelper.SemiBold(9F),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = ThemeHelper.Border;
        return button;
    }
}
