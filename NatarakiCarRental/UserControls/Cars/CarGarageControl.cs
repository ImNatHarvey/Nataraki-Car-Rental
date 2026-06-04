using FontAwesome.Sharp;
using System.Drawing.Drawing2D;
using System.Linq;
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

    public CarGarageControl(int currentUserId)
    {
        _currentUserId = currentUserId;
        _carService = new CarService(currentUserId);
        InitializeControl();
        Load += CarGarageControl_Load;
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

    private static Panel CreateHeaderPanel()
    {
        Panel panel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeHelper.ContentBackground
        };

        Label titleLabel = new()
        {
            Text = "Car Garage",
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(260, 34),
            Font = FontHelper.Title(22F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Label subtitleLabel = new()
        {
            Text = "Manage vehicle records, availability, and archived cars.",
            AutoSize = false,
            Location = new Point(2, 42),
            Size = new Size(520, 24),
            Font = FontHelper.Regular(10.5F),
            ForeColor = ThemeHelper.TextSecondary
        };
        panel.Controls.Add(titleLabel);
        panel.Controls.Add(subtitleLabel);

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

        _carsGrid.Dock = DockStyle.Fill;
        _carsGrid.AllowUserToAddRows = false;
        _carsGrid.AllowUserToDeleteRows = false;
        _carsGrid.AllowUserToResizeRows = false;
        _carsGrid.AllowUserToResizeColumns = false;
        _carsGrid.ScrollBars = ScrollBars.Vertical;
        _carsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _carsGrid.BackgroundColor = ThemeHelper.Surface;
        _carsGrid.BorderStyle = BorderStyle.FixedSingle;
        _carsGrid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        _carsGrid.ColumnHeadersHeight = 38;
        _carsGrid.EnableHeadersVisualStyles = false;
        _carsGrid.GridColor = ThemeHelper.TableGridLine;
        _carsGrid.ReadOnly = true;
        _carsGrid.RowHeadersVisible = false;
        _carsGrid.RowTemplate.Height = 38;
        _carsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        _carsGrid.DefaultCellStyle.SelectionBackColor = ThemeHelper.Surface;
        _carsGrid.DefaultCellStyle.SelectionForeColor = ThemeHelper.TextPrimary;

        _carsGrid.ColumnHeadersDefaultCellStyle.BackColor = ThemeHelper.Primary;
        _carsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _carsGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemeHelper.Primary;
        _carsGrid.ColumnHeadersDefaultCellStyle.Font = FontHelper.SemiBold(9F);
        _carsGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

        _carsGrid.CellContentClick += CarsGrid_CellContentClick;
        _carsGrid.CellMouseMove += CarsGrid_CellMouseMove;
        _carsGrid.CellMouseLeave += (_, _) => _carsGrid.Cursor = Cursors.Default;
        _carsGrid.CellPainting += CarsGrid_CellPainting;

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

        AddActionColumn("ViewAction", "Actions", "View");

        if (_showArchived)
        {
            AddActionColumn("RestoreAction", "", "Restore");
        }
        else
        {
            AddActionColumn("EditAction", "", "Edit");
            AddActionColumn("ArchiveAction", "", "Archive");
        }

        if (_carsGrid.Columns["CarId"] is DataGridViewColumn carIdColumn)
        {
            carIdColumn.Visible = false;
        }

        if (_carsGrid.Columns["RatePerDay"] is DataGridViewColumn rateColumn)
        {
            rateColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        }

        // REDUCED weights on the left side to give more room to the buttons on the right
        SetFillWeight("CarName", 85);
        SetFillWeight("Model", 80);
        SetFillWeight("PlateNumber", 80);
        SetFillWeight("RatePerDay", 70);
        SetFillWeight("CodingDay", 65);
        SetFillWeight("Status", 95);

        // INCREASED weights for the action buttons so they are all equally breathable
        if (_carsGrid.Columns["ViewAction"] is DataGridViewColumn viewColumn)
        {
            viewColumn.FillWeight = 60;
        }

        if (_carsGrid.Columns["EditAction"] is DataGridViewColumn editColumn)
        {
            editColumn.FillWeight = 60;
        }

        if (_carsGrid.Columns["ArchiveAction"] is DataGridViewColumn archiveColumn)
        {
            archiveColumn.FillWeight = 60;
        }

        if (_carsGrid.Columns["RestoreAction"] is DataGridViewColumn restoreColumn)
        {
            restoreColumn.FillWeight = 60;
        }
    }

    private void AddActionColumn(string name, string headerText, string buttonText)
    {
        DataGridViewButtonColumn column = new()
        {
            Name = name,
            HeaderText = headerText,
            Text = buttonText,
            UseColumnTextForButtonValue = true,
            FlatStyle = FlatStyle.Flat
        };

        column.DefaultCellStyle.BackColor = ThemeHelper.Surface;
        column.DefaultCellStyle.SelectionBackColor = ThemeHelper.Surface;

        _carsGrid.Columns.Add(column);
    }

    private void SetFillWeight(string columnName, float weight)
    {
        if (_carsGrid.Columns[columnName] is DataGridViewColumn column)
        {
            column.FillWeight = weight;
        }
    }

    private int _lastHeight;

    private async void CarGarageControl_Load(object? sender, EventArgs e)
    {
        Load -= CarGarageControl_Load;
        _lastHeight = Height;
        Resize += async (_, _) =>
        {
            if (Math.Abs(Height - _lastHeight) > 50)
            {
                _lastHeight = Height;
                _currentPage = 1;
                await LoadCarsAsync();
            }
        };
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

            IReadOnlyList<Car> cars = await _carService.SearchCarsAsync(_searchTextBox.Text, _showArchived);

            if (_filterComboBox.SelectedIndex > 0)
            {
                string selectedStatus = _filterComboBox.SelectedItem?.ToString() ?? "";
                cars = cars.Where(c => c.Status == selectedStatus).ToList();
            }

            PopulateGrid(cars);
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

    private void PopulateGrid(IReadOnlyList<Car> allCars)
    {
        AddGridColumns();
        _carsGrid.Rows.Clear();

        int totalItems = allCars.Count;
        int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / _pageSize));
        if (_currentPage > totalPages) _currentPage = totalPages;
        
        _paginationLabel.Text = $"Page {_currentPage} of {totalPages} ({totalItems} records)";
        _prevPageButton.Enabled = _currentPage > 1;
        _nextPageButton.Enabled = _currentPage < totalPages;

        var pagedCars = allCars.Skip((_currentPage - 1) * _pageSize).Take(_pageSize);

        foreach (Car car in pagedCars)
        {
            string codingDayDisplay = string.IsNullOrWhiteSpace(car.CodingDay) ? "-" :
                (car.CodingDay.StartsWith("None", StringComparison.OrdinalIgnoreCase) ? "None" : car.CodingDay);

            _carsGrid.Rows.Add(
                car.CarId,
                car.CarName,
                car.Model,
                car.PlateNumber,
                FormatPeso(car.RatePerDay),
                codingDayDisplay,
                car.Status);
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

    private void CarsGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        string columnName = _carsGrid.Columns[e.ColumnIndex].Name;
        bool isStatus = columnName == "Status";
        bool isAction = columnName is "ViewAction" or "EditAction" or "ArchiveAction" or "RestoreAction";

        if (isStatus || isAction)
        {
            e.PaintBackground(e.CellBounds, true);

            string text = isAction ? (e.FormattedValue?.ToString() ?? string.Empty) : (e.Value?.ToString() ?? string.Empty);
            if (string.IsNullOrEmpty(text)) return;

            Color backColor = ThemeHelper.Surface;
            Color foreColor = Color.White;

            if (isStatus)
            {
                switch (text)
                {
                    case "Available":
                        backColor = ThemeHelper.Success;
                        break;
                    case "Rented":
                        backColor = ThemeHelper.Success;
                        break;
                    case "Maintenance":
                        backColor = ThemeHelper.Danger;
                        break;
                    case "Reserved":
                        backColor = ThemeHelper.Primary;
                        break;
                    default:
                        backColor = ThemeHelper.GrayIcon;
                        break;
                }
            }
            else if (isAction)
            {
                switch (columnName)
                {
                    case "ViewAction":
                        backColor = ThemeHelper.Primary;
                        break;
                    case "EditAction":
                        backColor = ThemeHelper.Success;
                        break;
                    case "ArchiveAction":
                        backColor = ThemeHelper.Danger;
                        break;
                    case "RestoreAction":
                        backColor = ThemeHelper.Warning;
                        break;
                }
            }

            if (e.Graphics is null)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Font font = e.CellStyle?.Font ?? FontHelper.SemiBold(9F);
            SizeF textSize = e.Graphics.MeasureString(text, font);
            float pillHeight = 26;
            float pillWidth;

            if (isAction)
            {
                pillWidth = e.CellBounds.Width - 12;
            }
            else
            {
                pillWidth = textSize.Width + 24;
            }

            if (pillWidth > e.CellBounds.Width - 4) pillWidth = e.CellBounds.Width - 4;

            float x = isStatus
                ? e.CellBounds.X + 8
                : e.CellBounds.X + (e.CellBounds.Width - pillWidth) / 2;

            float y = e.CellBounds.Y + (e.CellBounds.Height - pillHeight) / 2;

            RectangleF rect = new RectangleF(x, y, pillWidth, pillHeight);

            using GraphicsPath path = GetRoundedRect(rect, pillHeight / 2);
            using SolidBrush backBrush = new(backColor);
            using SolidBrush foreBrush = new(foreColor);

            e.Graphics.FillPath(backBrush, path);

            using StringFormat format = new()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            };
            e.Graphics.DrawString(text, font, foreBrush, rect, format);

            e.Handled = true;
        }
    }

    private static GraphicsPath GetRoundedRect(RectangleF rect, float radius)
    {
        GraphicsPath path = new();
        float diameter = radius * 2;
        Size size = new Size((int)diameter, (int)diameter);
        RectangleF arc = new RectangleF(rect.Location, size);

        if (radius == 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private async void CarsGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        string columnName = _carsGrid.Columns[e.ColumnIndex].Name;

        if (columnName is not "ViewAction" and not "EditAction" and not "ArchiveAction" and not "RestoreAction")
        {
            return;
        }

        int carId = Convert.ToInt32(_carsGrid.Rows[e.RowIndex].Cells["CarId"].Value);

        if (columnName == "ViewAction")
        {
            await ViewCarAsync(carId);
            return;
        }

        if (columnName == "EditAction")
        {
            await EditCarAsync(carId);
            return;
        }

        if (columnName == "ArchiveAction")
        {
            await ArchiveCarAsync(carId);
            return;
        }

        if (columnName == "RestoreAction")
        {
            await RestoreCarAsync(carId);
        }
    }

    private void CarsGrid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
    {
        _carsGrid.Cursor = IsActionColumn(e.ColumnIndex) ? Cursors.Hand : Cursors.Default;
    }

    private bool IsActionColumn(int columnIndex)
    {
        return columnIndex >= 0
            && _carsGrid.Columns[columnIndex].Name is "ViewAction" or "EditAction" or "ArchiveAction" or "RestoreAction";
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
