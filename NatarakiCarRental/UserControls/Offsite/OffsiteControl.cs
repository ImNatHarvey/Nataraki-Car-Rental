using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.UserControls.Offsite;

public sealed class OffsiteControl : UserControl
{
    private static readonly TimeSpan NormalRefreshInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DemoInterval = TimeSpan.FromSeconds(5);

    private readonly VehicleTrackingService _trackingService = new();
    private readonly VehicleTrackingSimulator _simulator = new();
    private readonly ComboBox _carComboBox = new();
    private readonly Button _refreshButton = ControlFactory.CreatePrimaryButton("Refresh", 90, 34);
    private readonly Button _startDemoButton = ControlFactory.CreatePrimaryButton("Start Tracking", 140, 34);
    private readonly Button _stopDemoButton = ControlFactory.CreatePrimaryButton("Stop Tracking", 140, 34);
    private readonly WebView2 _mapWebView = new();
    private readonly Label _selectedCarValueLabel = CreateValueLabel();
    private readonly Label _plateNumberValueLabel = CreateValueLabel();
    private readonly Label _latitudeValueLabel = CreateValueLabel();
    private readonly Label _longitudeValueLabel = CreateValueLabel();
    private readonly Label _lastUpdatedValueLabel = CreateValueLabel();
    private readonly Label _sourceValueLabel = CreateValueLabel();
    private readonly Label _statusLabel = new();
    private readonly Label _autoRefreshLabel = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly System.Windows.Forms.Timer _demoTimer = new();

    private bool _mapReady;
    private bool _isRefreshing;
    private bool _isDemoTickRunning;

    public OffsiteControl()
    {
        InitializeControl();
        Load += OffsiteControl_Load;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _demoTimer.Stop();
            _refreshTimer.Dispose();
            _demoTimer.Dispose();
            _mapWebView.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeControl()
    {
        BackColor = ThemeHelper.ContentBackground;
        Dock = DockStyle.Fill;
        Padding = new Padding(32);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126F));

        layout.Controls.Add(CreateHeaderPanel(), 0, 0);
        layout.Controls.Add(CreateToolbarPanel(), 0, 1);
        layout.Controls.Add(CreateMapCard(), 0, 2);
        layout.Controls.Add(CreateInfoPanel(), 0, 3);
        Controls.Add(layout);

        _refreshTimer.Interval = (int)NormalRefreshInterval.TotalMilliseconds;
        _refreshTimer.Tick += async (_, _) => await RefreshSelectedCarLocationAsync(showEmptyMessage: false);

        _demoTimer.Interval = (int)DemoInterval.TotalMilliseconds;
        _demoTimer.Tick += async (_, _) => await InsertDemoLocationAsync();
    }

    private static Panel CreateHeaderPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        panel.Controls.Add(new Label
        {
            Text = "Offsite Tracking",
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(360, 34),
            Font = FontHelper.Title(22F),
            ForeColor = ThemeHelper.TextPrimary
        });
        panel.Controls.Add(new Label
        {
            Text = "Monitor vehicle location and operational movement.",
            AutoSize = false,
            Location = new Point(2, 42),
            Size = new Size(620, 24),
            Font = FontHelper.Regular(10.5F),
            ForeColor = ThemeHelper.TextSecondary
        });
        return panel;
    }

    private Panel CreateToolbarPanel()
    {
        Panel panel = ControlFactory.CreateCardPanel(new Size(0, 58));
        panel.Dock = DockStyle.Fill;
        panel.Padding = new Padding(16, 12, 16, 12);

        FlowLayoutPanel flow = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false,
            BackColor = Color.Transparent
        };

        Label carLabel = new()
        {
            Text = "Car",
            AutoSize = true,
            Font = FontHelper.SemiBold(9F),
            ForeColor = ThemeHelper.TextSecondary,
            Margin = new Padding(0, 7, 8, 0)
        };

        _carComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _carComboBox.Font = FontHelper.Regular(10F);
        _carComboBox.Width = 240;
        _carComboBox.Margin = new Padding(0, 0, 12, 0);
        _carComboBox.SelectedIndexChanged += async (_, _) => await RefreshSelectedCarLocationAsync(showEmptyMessage: true);

        _refreshButton.Margin = new Padding(0, 0, 12, 0);
        _refreshButton.Click += async (_, _) => await RefreshSelectedCarLocationAsync(showEmptyMessage: true);

        _startDemoButton.Margin = new Padding(0, 0, 8, 0);
        _startDemoButton.Click += async (_, _) => await StartDemoTrackingAsync();

        _stopDemoButton.Enabled = false;
        _stopDemoButton.Margin = new Padding(0, 0, 12, 0);
        _stopDemoButton.Click += (_, _) => StopDemoTracking();

        _autoRefreshLabel.Text = "Refresh: 10m";
        _autoRefreshLabel.AutoSize = true;
        _autoRefreshLabel.Font = FontHelper.Regular(9F);
        _autoRefreshLabel.ForeColor = ThemeHelper.TextSecondary;
        _autoRefreshLabel.Margin = new Padding(0, 8, 0, 0);

        flow.Controls.Add(carLabel);
        flow.Controls.Add(_carComboBox);
        flow.Controls.Add(_refreshButton);
        flow.Controls.Add(_startDemoButton);
        flow.Controls.Add(_stopDemoButton);
        flow.Controls.Add(_autoRefreshLabel);

        panel.Controls.Add(flow);
        return panel;
    }

    private Panel CreateMapCard()
    {
        Panel card = ControlFactory.CreateCardPanel(new Size(0, 0));
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(12);
        card.Margin = new Padding(0, 14, 0, 14);
        _mapWebView.Dock = DockStyle.Fill;
        _mapWebView.NavigationCompleted += MapWebView_NavigationCompleted;
        card.Controls.Add(_mapWebView);
        return card;
    }

    private Panel CreateInfoPanel()
    {
        Panel card = ControlFactory.CreateCardPanel(new Size(0, 112));
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(18, 14, 18, 12);

        _statusLabel.Text = "Tracking data shown here is based on the latest coordinates recorded in the local database.";
        _statusLabel.AutoSize = false;
        _statusLabel.Dock = DockStyle.Bottom;
        _statusLabel.Height = 24;
        _statusLabel.Font = FontHelper.Regular(9F);
        _statusLabel.ForeColor = ThemeHelper.TextSecondary;

        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 2
        };

        for (int index = 0; index < 6; index++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66F));
        }

        AddInfoColumn(grid, "Selected Car", _selectedCarValueLabel, 0);
        AddInfoColumn(grid, "Plate Number", _plateNumberValueLabel, 1);
        AddInfoColumn(grid, "Last Latitude", _latitudeValueLabel, 2);
        AddInfoColumn(grid, "Last Longitude", _longitudeValueLabel, 3);
        AddInfoColumn(grid, "Last Updated", _lastUpdatedValueLabel, 4);
        AddInfoColumn(grid, "Source", _sourceValueLabel, 5);

        card.Controls.Add(grid);
        card.Controls.Add(_statusLabel);
        return card;
    }

    private static void AddInfoColumn(TableLayoutPanel grid, string title, Label valueLabel, int column)
    {
        Label titleLabel = new()
        {
            Text = title,
            Dock = DockStyle.Fill,
            Font = FontHelper.SemiBold(9F),
            ForeColor = ThemeHelper.TextSecondary
        };

        valueLabel.Dock = DockStyle.Fill;
        grid.Controls.Add(titleLabel, column, 0);
        grid.Controls.Add(valueLabel, column, 1);
    }

    private static Label CreateValueLabel()
    {
        return new Label
        {
            Text = "-",
            AutoSize = false,
            Font = FontHelper.SemiBold(10F),
            ForeColor = ThemeHelper.TextPrimary,
            AutoEllipsis = true
        };
    }

    private async void OffsiteControl_Load(object? sender, EventArgs e)
    {
        Load -= OffsiteControl_Load;
        await InitializeMapAsync();
        await LoadCarsAsync();
        _refreshTimer.Start();
    }

    private async Task InitializeMapAsync()
    {
        try
        {
            string mapPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Maps", "offsite-tracking-map.html");

            if (!File.Exists(mapPath))
            {
                _statusLabel.Text = "Unable to load map. The local map asset is missing.";
                return;
            }

            await _mapWebView.EnsureCoreWebView2Async();
            _mapWebView.Source = new Uri(mapPath);
        }
        catch (Exception exception) when (exception is WebView2RuntimeNotFoundException or InvalidOperationException or COMException)
        {
            _statusLabel.Text = "Unable to load map. Please make sure WebView2 Runtime is installed.";
            MessageBoxHelper.ShowWarning($"{_statusLabel.Text}\n\n{exception.Message}", "Offsite Tracking");
        }
        catch (Exception exception)
        {
            _statusLabel.Text = "Unable to load map.";
            MessageBoxHelper.ShowWarning($"Unable to load map.\n\n{exception.Message}", "Offsite Tracking");
        }
    }

    private async Task LoadCarsAsync()
    {
        try
        {
            IReadOnlyList<Car> cars = await _trackingService.GetTrackableCarsAsync();
            _carComboBox.BeginUpdate();
            _carComboBox.Items.Clear();
            _carComboBox.Items.Add("Select a car");
            foreach (Car car in cars)
            {
                _carComboBox.Items.Add(new CarOption(car.CarId, car.CarName, car.PlateNumber));
            }
            _carComboBox.SelectedIndex = 0;
            _carComboBox.EndUpdate();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning($"Unable to load cars for tracking.\n\n{exception.Message}", "Offsite Tracking");
        }
    }

    private async Task RefreshSelectedCarLocationAsync(bool showEmptyMessage)
    {
        if (_isRefreshing || _carComboBox.SelectedItem is not CarOption car)
        {
            return;
        }

        try
        {
            _isRefreshing = true;
            VehicleLocation? location = await _trackingService.GetLatestLocationAsync(car.CarId);
            if (location is null)
            {
                await ClearLocationDisplayAsync(car);
                if (showEmptyMessage)
                {
                    _statusLabel.Text = "No tracking data yet. Start tracking or add a location record.";
                }
                return;
            }

            UpdateLocationDisplay(car, location);
            await UpdateMapMarkerAsync(location, car.Label);
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning($"Unable to refresh tracking data.\n\n{exception.Message}", "Offsite Tracking");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task StartDemoTrackingAsync()
    {
        if (_carComboBox.SelectedItem is not CarOption)
        {
            MessageBoxHelper.ShowWarning("Select a car before starting tracking.", "Offsite Tracking");
            return;
        }

        _startDemoButton.Enabled = false;
        _stopDemoButton.Enabled = true;
        _autoRefreshLabel.Text = "Refresh: 5s";
        _demoTimer.Start();
        await InsertDemoLocationAsync();
    }

    private void StopDemoTracking()
    {
        _demoTimer.Stop();
        _startDemoButton.Enabled = true;
        _stopDemoButton.Enabled = false;
        _autoRefreshLabel.Text = "Refresh: 10m";
        _statusLabel.Text = "Tracking stopped. Latest saved location remains available.";
    }

    private async Task InsertDemoLocationAsync()
    {
        if (_isDemoTickRunning || _carComboBox.SelectedItem is not CarOption car)
        {
            return;
        }

        try
        {
            _isDemoTickRunning = true;
            await _simulator.InsertNextAsync(car.CarId);
            await RefreshSelectedCarLocationAsync(showEmptyMessage: false);
        }
        catch (Exception exception)
        {
            StopDemoTracking();
            MessageBoxHelper.ShowWarning($"Tracking stopped because a location could not be saved.\n\n{exception.Message}", "Offsite Tracking");
        }
        finally
        {
            _isDemoTickRunning = false;
        }
    }

    private async Task UpdateMapMarkerAsync(VehicleLocation location, string label)
    {
        if (!_mapReady || _mapWebView.CoreWebView2 is null)
        {
            return;
        }

        string safeLabel = JsonSerializer.Serialize(label);
        string script = FormattableString.Invariant(
            $"window.setVehicleLocation({location.Latitude}, {location.Longitude}, {safeLabel});");
        await _mapWebView.ExecuteScriptAsync(script);
    }

    private async Task ClearMapMarkerAsync()
    {
        if (_mapReady && _mapWebView.CoreWebView2 is not null)
        {
            await _mapWebView.ExecuteScriptAsync("window.clearVehicleMarker();");
        }
    }

    private async void MapWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _mapReady = e.IsSuccess;
        if (!e.IsSuccess)
        {
            _statusLabel.Text = "Unable to load map. Please make sure WebView2 Runtime is installed.";
            return;
        }

        await RefreshSelectedCarLocationAsync(showEmptyMessage: false);
    }

    private void UpdateLocationDisplay(CarOption car, VehicleLocation location)
    {
        _selectedCarValueLabel.Text = car.CarName;
        _plateNumberValueLabel.Text = car.PlateNumber;
        _latitudeValueLabel.Text = $"{location.Latitude:N7}";
        _longitudeValueLabel.Text = $"{location.Longitude:N7}";
        _lastUpdatedValueLabel.Text = $"{location.RecordedAt:MMM d, yyyy h:mm tt}";
        _sourceValueLabel.Text = location.Source;
        _statusLabel.Text = "Tracking data shown here is based on the latest coordinates recorded in the local database.";
    }

    private async Task ClearLocationDisplayAsync(CarOption car)
    {
        _selectedCarValueLabel.Text = car.CarName;
        _plateNumberValueLabel.Text = car.PlateNumber;
        _latitudeValueLabel.Text = "-";
        _longitudeValueLabel.Text = "-";
        _lastUpdatedValueLabel.Text = "-";
        _sourceValueLabel.Text = "-";
        await ClearMapMarkerAsync();
    }

    private sealed record CarOption(int CarId, string CarName, string PlateNumber)
    {
        public string Label => $"{CarName} ({PlateNumber})";

        public override string ToString() => Label;
    }
}
