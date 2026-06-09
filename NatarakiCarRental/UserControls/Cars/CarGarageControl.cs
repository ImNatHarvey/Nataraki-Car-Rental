using FontAwesome.Sharp;
using System.Drawing.Drawing2D;
using NatarakiCarRental.Forms.Cars;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.UserControls.Cars;

public sealed class CarGarageControl : UserControl
{
    private readonly CarService _carService;
    private readonly SecurityVerificationService _verificationService = new();
    private readonly MetricCardControl _totalCarsCard = new();
    private readonly MetricCardControl _availableCarsCard = new();
    private readonly MetricCardControl _rentedCarsCard = new();
    private readonly MetricCardControl _maintenanceCarsCard = new();

    private readonly TextBox _searchTextBox = new();
    private readonly ComboBox _filterComboBox = new();
    private readonly IconButton _activeCarsButton = new();
    private readonly IconButton _archivedCarsButton = new();
    private readonly DataGridView _carsGrid = new();
    private readonly Label _emptyStateLabel = new();
    private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 350 };
    private int _currentPage = 1;
    private int _pageSize = 13;
    private readonly Label _paginationLabel = new();
    private readonly Button _prevPageButton = CreatePaginationButton("Previous");
    private readonly Button _nextPageButton = CreatePaginationButton("Next");

    private bool _showArchived;

    private readonly int _currentUserId;
    private int _lastWidth;
    private int _lastHeight;
    private readonly System.Windows.Forms.Timer _layoutThrottleTimer = new() { Interval = 100 };

    public CarGarageControl(int currentUserId)
    {
        _currentUserId = currentUserId;
        _carService = new CarService(currentUserId);
        InitializeControl();
        Load += CarGarageControl_Load;
        _layoutThrottleTimer.Tick += async (s, e) => {
            _layoutThrottleTimer.Stop();
            await PerformDeferredLayoutAsync();
        };
    }

    private static Button CreatePaginationButton(string text)
    {
        Button button = new()
        {
            Text = text,
            Size = new Size(80, 32),
            BackColor = ThemeHelper.Surface,
            ForeColor = ThemeHelper.TextPrimary,
            Font = FontHelper.SemiBold(9F),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = ThemeHelper.Border;
        return button;
    }

    private void InitializeControl()
    {
        BackColor = ThemeHelper.ContentBackground;
        Dock = DockStyle.Fill;
        Padding = new Padding(32, 8, 32, 32);

        TableLayoutPanel mainLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5
        };

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

        mainLayout.Controls.Add(CreateMetricGrid(), 0, 0);
        mainLayout.Controls.Add(CreateActionBarPanel(), 0, 1);
        mainLayout.Controls.Add(CreateSearchPanel(), 0, 2);
        mainLayout.Controls.Add(CreateTablePanel(), 0, 3);
        mainLayout.Controls.Add(CreatePaginationPanel(), 0, 4);

        Controls.Add(mainLayout);

        Resize += (_, _) => {
            if (Width == _lastWidth) return;
            _layoutThrottleTimer.Stop();
            _layoutThrottleTimer.Start();
        };
    }

    private async Task PerformDeferredLayoutAsync()
    {
        if (IsDisposed) return;
        if (Math.Abs(Height - _lastHeight) > 50)
        {
            _lastHeight = Height;
            _currentPage = 1;
            await LoadCarsAsync();
        }
        _lastWidth = Width;
    }

    private Panel CreatePaginationPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        _prevPageButton.Location = new Point(0, 8);
        _prevPageButton.Click += async (_, _) =>
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                await LoadCarsAsync();
            }
        };
        _nextPageButton.Location = new Point(90, 8);
        _nextPageButton.Click += async (_, _) =>
        {
            _currentPage++;
            await LoadCarsAsync();
        };

        _paginationLabel.AutoSize = false;
        _paginationLabel.Location = new Point(180, 8);
        _paginationLabel.Size = new Size(200, 32);
        _paginationLabel.TextAlign = ContentAlignment.MiddleLeft;
        _paginationLabel.Font = FontHelper.Regular(9.5F);
        _paginationLabel.ForeColor = ThemeHelper.TextSecondary;

        panel.Controls.Add(_prevPageButton);
        panel.Controls.Add(_nextPageButton);
        panel.Controls.Add(_paginationLabel);
        return panel;
    }

    private TableLayoutPanel CreateMetricGrid()
    {
        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            Padding = new Padding(0, 12, 0, 8)
        };

        for (int i = 0; i < 4; i++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        AddMetricCard(grid, _totalCarsCard, IconChar.Car, "Total Cars", 0, "All active vehicles", ThemeHelper.Primary);
        AddMetricCard(grid, _availableCarsCard, IconChar.CircleCheck, "Available Cars", 1, "Ready for rental", ThemeHelper.Success);
        AddMetricCard(grid, _rentedCarsCard, IconChar.Key, "Rented Cars", 2, "Currently rented", ThemeHelper.Warning);
        AddMetricCard(grid, _maintenanceCarsCard, IconChar.ScrewdriverWrench, "Maintenance Cars", 3, "Under maintenance today", ThemeHelper.Danger);

        return grid;
    }

    private static void AddMetricCard(TableLayoutPanel grid, MetricCardControl card, IconChar icon, string title, int column, string helperText, Color iconColor)
    {
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, column == 3 ? 0 : 14, 0);
        card.SetMetric(icon, title, "0", helperText, iconColor);
        grid.Controls.Add(card, column, 0);
    }

    private Panel CreateActionBarPanel()
    {
        Panel panel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.ContentBackground
        };

        ConfigureTabButton(_activeCarsButton, "Cars", IconChar.Car, new Point(0, 10));
        ConfigureTabButton(_archivedCarsButton, "Archived", IconChar.BoxArchive, new Point(128, 10));

        IconButton addCarButton = new()
        {
            Text = "Add Car",
            IconChar = IconChar.Plus,
            IconColor = Color.White,
            IconSize = 14,
            Size = new Size(116, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 116, 10),
            BackColor = ThemeHelper.Primary,
            ForeColor = Color.White,
            Font = FontHelper.SemiBold(9F),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        addCarButton.FlatAppearance.BorderSize = 0;
        addCarButton.TextImageRelation = TextImageRelation.ImageBeforeText;
        addCarButton.Click += AddCarButton_Click;

        panel.Resize += (_, _) => addCarButton.Left = panel.Width - addCarButton.Width;
        _activeCarsButton.Click += async (_, _) =>
        {
            _showArchived = false;
            _currentPage = 1;
            await LoadCarsAsync();
        };

        _archivedCarsButton.Click += async (_, _) =>
        {
            _showArchived = true;
            _currentPage = 1;
            await LoadCarsAsync();
        };

        panel.Controls.Add(_activeCarsButton);
        panel.Controls.Add(_archivedCarsButton);
        panel.Controls.Add(addCarButton);

        return panel;
    }

    private Panel CreateSearchPanel()
    {
        Panel panel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.ContentBackground
        };

        BorderedPanel searchContainer = new()
        {
            Size = new Size(240, 32),
            Location = new Point(0, 8),
            BackColor = ThemeHelper.Surface,
            BorderColor = ThemeHelper.Border,
            Cursor = Cursors.IBeam
        };

        IconPictureBox searchIcon = new()
        {
            IconChar = IconChar.MagnifyingGlass,
            IconColor = ThemeHelper.TextSecondary,
            IconSize = 18,
            BackColor = ThemeHelper.Surface,
            Location = new Point(8, 7),
            Size = new Size(20, 20)
        };

        _searchTextBox.BorderStyle = BorderStyle.None;
        _searchTextBox.PlaceholderText = "Search cars...";
        _searchTextBox.BackColor = ThemeHelper.Surface;
        _searchTextBox.Font = FontHelper.Regular(10F);
        _searchTextBox.ForeColor = ThemeHelper.TextPrimary;
        _searchTextBox.Location = new Point(34, 7);
        _searchTextBox.Width = 196;
        _searchTextBox.TextChanged += (_, _) =>
        {
            _currentPage = 1;
            _searchTimer.Stop();
            _searchTimer.Start();
        };

        searchContainer.Controls.Add(searchIcon);
        searchContainer.Controls.Add(_searchTextBox);
        searchContainer.Click += (_, _) => _searchTextBox.Focus();

        _filterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _filterComboBox.Font = FontHelper.Regular(10F);
        _filterComboBox.ForeColor = ThemeHelper.TextPrimary;
        _filterComboBox.Size = new Size(180, 30);
        _filterComboBox.Location = new Point(256, 8);
        _filterComboBox.Items.AddRange(["All Status", "Available", "Maintenance"]);
        _filterComboBox.SelectedIndex = 0;
        _filterComboBox.SelectedIndexChanged += async (_, _) =>
        {
            _currentPage = 1;
            await LoadCarsAsync();
        };
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await LoadCarsAsync();
        };

        panel.Controls.Add(searchContainer);
        panel.Controls.Add(_filterComboBox);

        return panel;
    }

    private static void ConfigureTabButton(IconButton button, string text, IconChar icon, Point location)
    {
        button.Text = text;
        button.IconChar = icon;
        button.IconSize = 16;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.Location = location;
        button.Size = new Size(120, 34);
        button.FlatStyle = FlatStyle.Flat;
        button.Cursor = Cursors.Hand;
        button.Font = FontHelper.SemiBold(9F);
        button.FlatAppearance.BorderSize = 0;
    }

    private Panel CreateTablePanel()
    {
        Panel panel = ControlFactory.CreateCardPanel(new Size(0, 0));
        panel.Dock = DockStyle.Fill;
        panel.Padding = new Padding(18);

        DataGridViewHelper.ApplyStandardStyle(_carsGrid);
        _carsGrid.Dock = DockStyle.Fill;
        _carsGrid.CellMouseClick += CarsGrid_CellMouseClick;
        _carsGrid.CellMouseMove += CarsGrid_CellMouseMove;
        _carsGrid.CellMouseLeave += (_, _) => _carsGrid.Cursor = Cursors.Default;
        DataGridViewHelper.SetupStatusPills(_carsGrid, "Status");
        DataGridViewHelper.SetupActionButtons(_carsGrid);

        _emptyStateLabel.Text = "No car records found.";
        _emptyStateLabel.Dock = DockStyle.Bottom;
        _emptyStateLabel.Height = 42;
        _emptyStateLabel.Font = FontHelper.Regular(10F);
        _emptyStateLabel.ForeColor = ThemeHelper.TextSecondary;
        _emptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        _emptyStateLabel.Visible = false;

        panel.Controls.Add(_carsGrid);
        panel.Controls.Add(_emptyStateLabel);

        return panel;
    }

    private void AddGridColumns()
    {
        _carsGrid.Columns.Clear();
        _carsGrid.Columns.Add("CarId", "Car ID");
        _carsGrid.Columns.Add("CarName", "Car Name");
        _carsGrid.Columns.Add("Model", "Model");
        _carsGrid.Columns.Add("PlateNumber", "Plate Number");
        _carsGrid.Columns.Add("RatePerDay", "Rate/Day");
        _carsGrid.Columns.Add("CodingDay", "Coding");
        _carsGrid.Columns.Add("Status", "Status");

        DataGridViewTextBoxColumn actionsColumn = new()
        {
            Name = "Actions",
            HeaderText = "Actions",
            ReadOnly = true
        };
        _carsGrid.Columns.Add(actionsColumn);

        if (_carsGrid.Columns["CarId"] is DataGridViewColumn carIdColumn)
        {
            carIdColumn.Visible = false;
        }

        SetFillWeight("CarName", 120);
        SetFillWeight("Model", 100);
        SetFillWeight("PlateNumber", 90);
        SetFillWeight("RatePerDay", 80);
        SetFillWeight("CodingDay", 80);
        SetFillWeight("Status", 100);
        
        if (_carsGrid.Columns["Actions"] is DataGridViewColumn actionsCol)
        {
            actionsCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            actionsCol.Width = 240;
            actionsCol.MinimumWidth = 240;
        }
    }

    private void SetFillWeight(string columnName, float weight)
    {
        if (_carsGrid.Columns[columnName] is DataGridViewColumn column)
        {
            column.FillWeight = weight;
        }
    }

    private async void CarGarageControl_Load(object? sender, EventArgs e)
    {
        Load -= CarGarageControl_Load;
        _lastHeight = Height;
        _lastWidth = Width;
        await LoadCarsAsync();
    }

    private async Task LoadCarsAsync()
    {
        try
        {
            _pageSize = Height > 700 ? 13 : 4;
            UpdateTabStyles();

            CarCounts counts = await _carService.GetCarCountsAsync();
            UpdateMetricCards(counts);

            string? statusFilter = _filterComboBox.SelectedIndex > 0 ? _filterComboBox.SelectedItem?.ToString() : null;

            int totalItems = await _carService.CountCarsAsync(_searchTextBox.Text, _showArchived, statusFilter);
            int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / _pageSize));
            if (_currentPage > totalPages) _currentPage = totalPages;

            IReadOnlyList<Car> cars = await _carService.SearchCarsAsync(_searchTextBox.Text, _showArchived, statusFilter, _currentPage, _pageSize);

            PopulateGrid(cars, totalItems, totalPages);
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to load car records.\n\n{exception.Message}");
        }
    }

    private void UpdateMetricCards(CarCounts counts)
    {
        _totalCarsCard.SetMetric(IconChar.Car, "Total Cars", counts.TotalCars.ToString(), "All active vehicles", ThemeHelper.Primary);
        _availableCarsCard.SetMetric(IconChar.CircleCheck, "Available Cars", counts.AvailableCars.ToString(), "Ready for rental", ThemeHelper.Success);
        _rentedCarsCard.SetMetric(IconChar.Key, "Rented Cars", counts.RentedCars.ToString(), "Currently rented", ThemeHelper.Warning);
        _maintenanceCarsCard.SetMetric(IconChar.ScrewdriverWrench, "Maintenance Cars", counts.MaintenanceCars.ToString(), "Under maintenance today", ThemeHelper.Danger);
    }

    private void PopulateGrid(IReadOnlyList<Car> pagedCars, int totalItems, int totalPages)
    {
        AddGridColumns();
        _carsGrid.Rows.Clear();

        _paginationLabel.Text = $"Page {_currentPage} of {totalPages} ({totalItems} records)";
        _prevPageButton.Enabled = _currentPage > 1;
        _nextPageButton.Enabled = _currentPage < totalPages;

        foreach (Car car in pagedCars)
        {
            string codingDayDisplay = string.IsNullOrWhiteSpace(car.CodingDay) ? "-" :
                (car.CodingDay.StartsWith("None", StringComparison.OrdinalIgnoreCase) ? "None" : car.CodingDay);

            string actions = "View";
            if (!_showArchived)
            {
                actions += "|Edit|Archive";
            }
            else
            {
                actions += "|Restore";
            }

            _carsGrid.Rows.Add(
                car.CarId,
                car.CarName,
                car.Model,
                car.PlateNumber,
                FormatPeso(car.RatePerDay),
                codingDayDisplay,
                car.Status,
                actions);
        }

        _emptyStateLabel.Text = _showArchived ? "No archived car records found." : "No active car records found.";
        _emptyStateLabel.Visible = totalItems == 0;
    }

    private void UpdateTabStyles()
    {
        ApplyTabStyle(_activeCarsButton, !_showArchived);
        ApplyTabStyle(_archivedCarsButton, _showArchived);
    }

    private static string FormatPeso(decimal amount) => $"₱{amount:N2}";

    private static void ApplyTabStyle(IconButton button, bool isActive)
    {
        button.BackColor = isActive ? ThemeHelper.Primary : ThemeHelper.Surface;
        button.ForeColor = isActive ? Color.White : ThemeHelper.TextPrimary;
        button.IconColor = isActive ? Color.White : ThemeHelper.TextSecondary;
    }

    private async void CarsGrid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Button != MouseButtons.Left) return;

        string columnName = _carsGrid.Columns[e.ColumnIndex].Name;
        if (columnName != "Actions") return;

        int carId = Convert.ToInt32(_carsGrid.Rows[e.RowIndex].Cells["CarId"].Value);
        string? clickedAction = DataGridViewHelper.GetClickedAction(_carsGrid, e.RowIndex, e.ColumnIndex, e.X, e.Y);

        if (clickedAction is null) return;

        switch (clickedAction)
        {
            case "View":
                await ViewCarAsync(carId);
                break;
            case "Edit":
                await EditCarAsync(carId);
                break;
            case "Archive":
                await ArchiveCarAsync(carId);
                break;
            case "Restore":
                await RestoreCarAsync(carId);
                break;
        }
    }

    private void CarsGrid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
    {
        _carsGrid.Cursor = DataGridViewHelper.GetClickedAction(_carsGrid, e.RowIndex, e.ColumnIndex, e.X, e.Y) is not null
            ? Cursors.Hand
            : Cursors.Default;
    }

    private async void AddCarButton_Click(object? sender, EventArgs e)
    {
        if (!AccessControlService.HasPermission("Cars.Create"))
        {
            MessageBoxHelper.ShowWarning("You do not have permission to perform this action.");
            return;
        }

        using CarDetailsForm addCarForm = new(CarFormMode.Add, currentUserId: _currentUserId);

        if (addCarForm.ShowDialog(this) == DialogResult.OK)
        {
            _showArchived = false;
            await LoadCarsAsync();
        }
    }

    private async Task ViewCarAsync(int carId)
    {
        Car? car = await _carService.GetCarByIdAsync(carId);

        if (car is null)
        {
            MessageBoxHelper.ShowWarning("The selected car record no longer exists.");
            await LoadCarsAsync();
            return;
        }

        using CarDetailsForm form = new(CarFormMode.View, car, _currentUserId);
        form.ShowDialog(this);
    }

    private async Task EditCarAsync(int carId)
    {
        if (!AccessControlService.HasPermission("Cars.Edit"))
        {
            MessageBoxHelper.ShowWarning("You do not have permission to perform this action.");
            return;
        }

        Car? car = await _carService.GetCarByIdAsync(carId);

        if (car is null)
        {
            MessageBoxHelper.ShowWarning("The selected car record no longer exists.");
            await LoadCarsAsync();
            return;
        }

        if (!await _verificationService.RequireOwnerVerificationIfNeededAsync(_currentUserId, $"Edit car: {car.CarName} ({car.PlateNumber})"))
        {
            return;
        }

        using CarDetailsForm form = new(CarFormMode.Edit, car, _currentUserId);

        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadCarsAsync();
        }
    }

    private async Task ArchiveCarAsync(int carId)
    {
        if (!AccessControlService.HasPermission("Cars.ArchiveRestore"))
        {
            MessageBoxHelper.ShowWarning("You do not have permission to perform this action.");
            return;
        }

        Car? car = await _carService.GetCarByIdAsync(carId);

        if (car is null)
        {
            MessageBoxHelper.ShowWarning("The selected car record no longer exists.");
            await LoadCarsAsync();
            return;
        }

        if (!await _verificationService.RequireOwnerVerificationIfNeededAsync(_currentUserId, $"Archive car: {car.CarName}"))
        {
            return;
        }

        bool confirmed = MessageBoxHelper.ShowConfirmDanger(
            $"Archive {car.CarName} ({car.PlateNumber})? This will hide it from active car lists.",
            "Archive Car");

        if (!confirmed)
        {
            return;
        }

        try
        {
            await _carService.ArchiveCarAsync(carId);
            await LoadCarsAsync();
        }
        catch (FluentValidation.ValidationException exception)
        {
            MessageBoxHelper.ShowWarning(exception.Errors.FirstOrDefault()?.ErrorMessage ?? exception.Message, "Archive Car");
        }
    }

    private async Task RestoreCarAsync(int carId)
    {
        if (!AccessControlService.HasPermission("Cars.ArchiveRestore"))
        {
            MessageBoxHelper.ShowWarning("You do not have permission to perform this action.");
            return;
        }

        Car? car = await _carService.GetCarByIdAsync(carId);

        if (car is null)
        {
            MessageBoxHelper.ShowWarning("The selected car record no longer exists.");
            await LoadCarsAsync();
            return;
        }

        if (!await _verificationService.RequireOwnerVerificationIfNeededAsync(_currentUserId, $"Restore car: {car.CarName}"))
        {
            return;
        }

        bool confirmed = MessageBoxHelper.ShowConfirmWarning(
            $"Restore {car.CarName} ({car.PlateNumber}) to the active car list?",
            "Restore Car");

        if (!confirmed)
        {
            return;
        }

        await _carService.RestoreCarAsync(carId);
        await LoadCarsAsync();
    }
}
