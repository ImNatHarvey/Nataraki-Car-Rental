using System.Drawing.Drawing2D;
using FontAwesome.Sharp;
using NatarakiCarRental.Forms.FleetSchedule;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;

namespace NatarakiCarRental.UserControls.FleetSchedule;

public sealed class FleetScheduleControl : UserControl
{
    private const int CarColumnWidth = 210;
    private const int HeaderHeight = 46;
    private const int MinimumRowHeight = 52;
    private const int DayWidth = 42;
    private readonly int _currentUserId;
    private readonly CarService _carService;
    private readonly FleetScheduleService _scheduleService;
    private readonly Label _monthLabel = new();
    private readonly TimelineCanvas _timelineCanvas;
    private readonly ToolTip _toolTip = new();
    private int? _hoveredScheduleId;
    private DateTime _selectedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private IReadOnlyList<Car> _cars = [];
    private IReadOnlyList<Models.FleetSchedule> _schedules = [];
    private DateTime SelectedMonth => _selectedMonth;

    public FleetScheduleControl(int currentUserId)
    {
        _currentUserId = currentUserId;
        _carService = new CarService(currentUserId);
        _scheduleService = new FleetScheduleService(currentUserId);
        _timelineCanvas = new TimelineCanvas(this);
        InitializeControl();
        Load += FleetScheduleControl_Load;
    }

    private void InitializeControl()
    {
        BackColor = ThemeHelper.ContentBackground;
        Dock = DockStyle.Fill;
        Padding = new Padding(32);

        TableLayoutPanel mainLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        mainLayout.Controls.Add(CreateHeaderPanel(), 0, 0);
        mainLayout.Controls.Add(CreateToolbarPanel(), 0, 1);
        mainLayout.Controls.Add(CreateTimelineHost(), 0, 2);
        Controls.Add(mainLayout);
    }

    private static Panel CreateHeaderPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        panel.Controls.Add(new Label
        {
            Text = "Fleet Schedule",
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(280, 34),
            Font = FontHelper.Title(22F),
            ForeColor = ThemeHelper.TextPrimary
        });
        panel.Controls.Add(new Label
        {
            Text = "Visual monthly planning board for reservations, rentals, and maintenance.",
            AutoSize = false,
            Location = new Point(2, 42),
            Size = new Size(680, 24),
            Font = FontHelper.Regular(10.5F),
            ForeColor = ThemeHelper.TextSecondary
        });
        return panel;
    }

    private Panel CreateToolbarPanel()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = ThemeHelper.ContentBackground };
        Button previousButton = CreateSecondaryButton("<", 38, 34);
        previousButton.Location = new Point(0, 10);
        previousButton.Click += async (_, _) => await ChangeMonthAsync(-1);

        _monthLabel.Location = new Point(48, 10);
        _monthLabel.Size = new Size(180, 34);
        _monthLabel.Font = FontHelper.Title(12F);
        _monthLabel.ForeColor = ThemeHelper.TextPrimary;
        _monthLabel.TextAlign = ContentAlignment.MiddleCenter;

        Button nextButton = CreateSecondaryButton(">", 38, 34);
        nextButton.Location = new Point(238, 10);
        nextButton.Click += async (_, _) => await ChangeMonthAsync(1);

        Button todayButton = CreateSecondaryButton("Today", 84, 34);
        todayButton.Location = new Point(288, 10);
        todayButton.Click += async (_, _) =>
        {
            _selectedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            await LoadBoardAsync();
        };

        IconButton addButton = CreatePrimaryIconButton("Add Schedule", IconChar.CalendarPlus, 142, 36);
        addButton.Location = new Point(390, 9);
        addButton.Click += async (_, _) => await OpenAddFormAsync(null, null);
        addButton.Visible = AccessControlService.HasPermission("FleetSchedule.Create");

        FlowLayoutPanel legendPanel = CreateLegendPanel();
        panel.Resize += (_, _) =>
        {
            legendPanel.Width = Math.Max(panel.Width - legendPanel.Left, 280);
            legendPanel.Height = panel.Height - legendPanel.Top;
        };

        panel.Controls.Add(previousButton);
        panel.Controls.Add(_monthLabel);
        panel.Controls.Add(nextButton);
        panel.Controls.Add(todayButton);
        panel.Controls.Add(addButton);
        panel.Controls.Add(legendPanel);
        return panel;
    }

    private FlowLayoutPanel CreateLegendPanel()
    {
        FlowLayoutPanel panel = new()
        {
            Location = new Point(544, 8),
            Size = new Size(510, 76),
            BackColor = ThemeHelper.ContentBackground,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 7, 0, 0)
        };

        AddLegendItem(panel, "Pending", FleetScheduleVisualHelper.GetColor(FleetScheduleConstants.Status.Pending));
        AddLegendItem(panel, "Reserved", FleetScheduleVisualHelper.GetColor(FleetScheduleConstants.Status.Reserved));
        AddLegendItem(panel, "Rented", FleetScheduleVisualHelper.GetColor(FleetScheduleConstants.Status.Rented));
        AddLegendItem(panel, "Maintenance", FleetScheduleVisualHelper.GetColor(FleetScheduleConstants.Status.Ongoing, FleetScheduleConstants.Type.Maintenance));
        AddLegendItem(panel, "Completed", FleetScheduleVisualHelper.GetColor(FleetScheduleConstants.Status.Completed));
        AddLegendItem(panel, "Cancelled", FleetScheduleVisualHelper.GetColor(FleetScheduleConstants.Status.Cancelled));
        return panel;
    }

    private static void AddLegendItem(FlowLayoutPanel panel, string text, Color color)
    {
        Size labelSize = TextRenderer.MeasureText(text, FontHelper.Regular(9F));
        Panel itemPanel = new()
        {
            Size = new Size(28 + 8 + labelSize.Width + 4, 24),
            Margin = new Padding(0, 0, 10, 0),
            BackColor = ThemeHelper.ContentBackground
        };
        RoundedLegendMarker swatch = new(color)
        {
            Location = new Point(0, 5),
            Size = new Size(28, 14)
        };
        Label label = new()
        {
            Text = text,
            Location = new Point(36, 1),
            Size = new Size(itemPanel.Width - 36, 22),
            Font = FontHelper.Regular(9F),
            ForeColor = ThemeHelper.TextSecondary
        };
        itemPanel.Controls.Add(swatch);
        itemPanel.Controls.Add(label);
        panel.Controls.Add(itemPanel);
    }

    private Control CreateTimelineHost()
    {
        Panel card = ControlFactory.CreateCardPanel(new Size(0, 0));
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(12);

        _timelineCanvas.Dock = DockStyle.Fill;
        _timelineCanvas.AutoScroll = true;
        _timelineCanvas.BackColor = ThemeHelper.Surface;
        _timelineCanvas.MouseMove += TimelineCanvas_MouseMove;
        _timelineCanvas.MouseClick += TimelineCanvas_MouseClick;
        card.Controls.Add(_timelineCanvas);
        return card;
    }

    private async void FleetScheduleControl_Load(object? sender, EventArgs e)
    {
        Load -= FleetScheduleControl_Load;
        await LoadBoardAsync();
    }

    private async Task ChangeMonthAsync(int months)
    {
        _selectedMonth = _selectedMonth.AddMonths(months);
        await LoadBoardAsync();
    }

    private async Task LoadBoardAsync()
    {
        try
        {
            _monthLabel.Text = _selectedMonth.ToString("MMMM yyyy");
            _cars = await _carService.GetActiveCarsAsync();
            _schedules = await _scheduleService.GetSchedulesForMonthAsync(_selectedMonth.Year, _selectedMonth.Month);
            _timelineCanvas.UpdateVirtualSize();
            _timelineCanvas.Invalidate();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to load fleet schedule.\n\n{exception.Message}", "Fleet Schedule");
        }
    }

    private async Task OpenAddFormAsync(int? carId, DateTime? date)
    {
        using FleetScheduleDetailsForm form = new(FleetScheduleFormMode.Add, _currentUserId, prefilledCarId: carId, prefilledDate: date);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadBoardAsync();
        }
    }

    private async Task OpenEditFormAsync(Models.FleetSchedule schedule)
    {
        using FleetScheduleDetailsForm form = new(FleetScheduleFormMode.Edit, _currentUserId, schedule);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await LoadBoardAsync();
        }
    }

    private void TimelineCanvas_MouseMove(object? sender, MouseEventArgs e)
    {
        Models.FleetSchedule? schedule = _timelineCanvas.GetScheduleAt(e.Location);
        var cellInfo = _timelineCanvas.GetCellAt(e.Location);
        bool isEmptyCell = schedule is null && cellInfo.Car is not null && cellInfo.Date.HasValue;
        
        _timelineCanvas.Cursor = schedule is not null || isEmptyCell ? Cursors.Hand : Cursors.Default;
        int? nextScheduleId = schedule?.ScheduleId;

        if (_hoveredScheduleId != nextScheduleId)
        {
            _hoveredScheduleId = nextScheduleId;
            _toolTip.SetToolTip(
                _timelineCanvas,
                schedule is null
                    ? null
                    : BuildToolTipText(schedule));
        }
    }

    private static string BuildToolTipText(Models.FleetSchedule schedule)
    {
        List<string> lines =
        [
            $"{schedule.CarName} ({schedule.PlateNumber})",
            schedule.Title,
            $"{schedule.StartDate:MMM d, yyyy} - {schedule.EndDate:MMM d, yyyy}",
            FleetScheduleVisualHelper.GetDisplayStatus(schedule.Status, schedule.ScheduleType)
        ];

        if (!string.IsNullOrWhiteSpace(schedule.CustomerName))
        {
            lines.Add($"Customer: {schedule.CustomerName}");
        }

        if (!string.IsNullOrWhiteSpace(schedule.Notes))
        {
            lines.Add($"Notes: {schedule.Notes}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private async void TimelineCanvas_MouseClick(object? sender, MouseEventArgs e)
    {
        Models.FleetSchedule? schedule = _timelineCanvas.GetScheduleAt(e.Location);
        if (schedule is not null)
        {
            if (!AccessControlService.HasPermission("FleetSchedule.Edit") && !AccessControlService.HasPermission("FleetSchedule.View"))
            {
                MessageBoxHelper.ShowWarning("You do not have permission to view or edit schedules.");
                return;
            }
            await OpenEditFormAsync(schedule);
            return;
        }

        var cellInfo = _timelineCanvas.GetCellAt(e.Location);
        if (cellInfo.Car is not null && cellInfo.Date.HasValue)
        {
            if (!AccessControlService.HasPermission("FleetSchedule.Create"))
            {
                MessageBoxHelper.ShowWarning("You do not have permission to create schedules.");
                return;
            }
            await OpenAddFormAsync(cellInfo.Car.CarId, cellInfo.Date.Value);
        }
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

    private static IconButton CreatePrimaryIconButton(string text, IconChar icon, int width, int height)
    {
        IconButton button = new()
        {
            Text = text,
            IconChar = icon,
            IconColor = Color.White,
            IconSize = 14,
            Size = new Size(width, height),
            BackColor = ThemeHelper.Primary,
            ForeColor = Color.White,
            Font = FontHelper.SemiBold(9F),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TextImageRelation = TextImageRelation.ImageBeforeText
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = ThemeHelper.PrimaryHover;
        return button;
    }

    private sealed class TimelineCanvas : Panel
    {
        private const int PillHeight = 30;
        private const int RowTopPadding = 10;
        private const int RowBottomPadding = 12;
        private const int LaneSpacing = 8;
        private readonly FleetScheduleControl _owner;
        private readonly Dictionary<int, Rectangle> _scheduleBounds = [];
        private IReadOnlyList<RowLayout> _rowLayouts = [];

        public TimelineCanvas(FleetScheduleControl owner)
        {
            _owner = owner;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            if (se.ScrollOrientation == ScrollOrientation.HorizontalScroll)
            {
                Invalidate();
            }
        }

        public void UpdateVirtualSize()
        {
            RebuildLayouts();
            int days = DateTime.DaysInMonth(_owner.SelectedMonth.Year, _owner.SelectedMonth.Month);
            int width = CarColumnWidth + days * DayWidth;
            int height = HeaderHeight + (_rowLayouts.Count == 0 ? MinimumRowHeight : _rowLayouts.Sum(layout => layout.Height));
            AutoScrollMinSize = new Size(width, height);
        }

        public Models.FleetSchedule? GetScheduleAt(Point point)
        {
            if (point.X < CarColumnWidth)
            {
                return null;
            }

            Point translated = TranslatePoint(point);
            int? scheduleId = _scheduleBounds.FirstOrDefault(pair => pair.Value.Contains(translated)).Key;
            return scheduleId is null or 0
                ? null
                : _owner._schedules.FirstOrDefault(schedule => schedule.ScheduleId == scheduleId);
        }

        public (Car? Car, DateTime? Date) GetCellAt(Point point)
        {
            if (point.X < CarColumnWidth)
            {
                return (null, null);
            }

            Point translated = TranslatePoint(point);
            if (translated.X < CarColumnWidth || translated.Y < HeaderHeight)
            {
                return (null, null);
            }

            RowLayout? rowLayout = _rowLayouts.FirstOrDefault(layout =>
                translated.Y >= layout.Top
                && translated.Y < layout.Top + layout.Height);
            int dayIndex = (translated.X - CarColumnWidth) / DayWidth;
            if (rowLayout is null)
            {
                return (null, null);
            }

            int days = DateTime.DaysInMonth(_owner.SelectedMonth.Year, _owner.SelectedMonth.Month);
            if (dayIndex < 0 || dayIndex >= days)
            {
                return (null, null);
            }

            return (rowLayout.Car, _owner.SelectedMonth.AddDays(dayIndex));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            _scheduleBounds.Clear();

            int days = DateTime.DaysInMonth(_owner.SelectedMonth.Year, _owner.SelectedMonth.Month);
            using Pen gridPen = new(ThemeHelper.TableGridLine);
            using Pen majorGridPen = new(ThemeHelper.TableGridLineStrong);
            using SolidBrush headerBrush = new(ThemeHelper.ContentBackground);
            using SolidBrush surfaceBrush = new(ThemeHelper.Surface);
            using SolidBrush textBrush = new(ThemeHelper.TextPrimary);
            using SolidBrush mutedBrush = new(ThemeHelper.TextSecondary);
            using SolidBrush weekendBrush = new(Color.FromArgb(248, 250, 252));
            using SolidBrush todayBrush = new(Color.FromArgb(239, 246, 255));

            graphics.FillRectangle(surfaceBrush, 0, 0, AutoScrollMinSize.Width, AutoScrollMinSize.Height);
            graphics.FillRectangle(headerBrush, 0, 0, AutoScrollMinSize.Width, HeaderHeight);
            graphics.FillRectangle(headerBrush, 0, 0, CarColumnWidth, AutoScrollMinSize.Height);

            graphics.DrawString("Car", FontHelper.SemiBold(9F), textBrush, new PointF(14, 15));
            for (int day = 0; day < days; day++)
            {
                int x = CarColumnWidth + day * DayWidth;
                DateTime date = _owner.SelectedMonth.AddDays(day);
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    graphics.FillRectangle(weekendBrush, x, HeaderHeight, DayWidth, AutoScrollMinSize.Height - HeaderHeight);
                }

                if (date.Date == DateTime.Today)
                {
                    graphics.FillRectangle(todayBrush, x, 0, DayWidth, AutoScrollMinSize.Height);
                }

                graphics.DrawLine(date.Day == 1 ? majorGridPen : gridPen, x, 0, x, AutoScrollMinSize.Height);
                graphics.DrawString(date.Day.ToString(), FontHelper.SemiBold(9F), textBrush, new PointF(x + 13, 8));
                graphics.DrawString(date.ToString("ddd"), FontHelper.Regular(8F), mutedBrush, new PointF(x + 10, 24));
            }

            graphics.DrawLine(majorGridPen, 0, HeaderHeight, AutoScrollMinSize.Width, HeaderHeight);
            if (_owner._cars.Count == 0)
            {
                const string message = "No active cars available. Add cars in Car Garage first.";
                SizeF messageSize = graphics.MeasureString(message, FontHelper.Regular(11F));
                float x = Math.Max((ClientSize.Width - messageSize.Width) / 2 - AutoScrollPosition.X, 24);
                float y = HeaderHeight + Math.Max((ClientSize.Height - HeaderHeight - messageSize.Height) / 2 - AutoScrollPosition.Y, 28);
                graphics.DrawString(message, FontHelper.Regular(11F), mutedBrush, new PointF(x, y));
                return;
            }

            foreach (RowLayout rowLayout in _rowLayouts)
            {
                graphics.DrawLine(gridPen, 0, rowLayout.Top, AutoScrollMinSize.Width, rowLayout.Top);
            }

            foreach (ScheduleLayout layout in _rowLayouts.SelectMany(rowLayout => rowLayout.Schedules))
            {
                Rectangle rect = layout.Bounds;
                _scheduleBounds[layout.Schedule.ScheduleId] = rect;

                using GraphicsPath path = FleetScheduleVisualHelper.CreateRoundedRectanglePath(rect, 12);
                using SolidBrush fillBrush = new(FleetScheduleVisualHelper.GetColor(layout.Schedule.Status, layout.Schedule.ScheduleType));
                graphics.FillPath(fillBrush, path);

                string displayStatus = FleetScheduleVisualHelper.GetDisplayStatus(layout.Schedule.Status, layout.Schedule.ScheduleType);
                using StringFormat format = new()
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                graphics.DrawString(displayStatus, FontHelper.SemiBold(8.5F), Brushes.White, new RectangleF(rect.X + 8, rect.Y, rect.Width - 12, rect.Height), format);
            }

            DrawStickyCarColumn(graphics, gridPen, majorGridPen, headerBrush, textBrush, mutedBrush);
        }

        private Point TranslatePoint(Point point)
        {
            return new Point(point.X - AutoScrollPosition.X, point.Y - AutoScrollPosition.Y);
        }

        private void DrawStickyCarColumn(
            Graphics graphics,
            Pen gridPen,
            Pen majorGridPen,
            Brush headerBrush,
            Brush textBrush,
            Brush mutedBrush)
        {
            int stickyX = -AutoScrollPosition.X;
            graphics.FillRectangle(headerBrush, stickyX, 0, CarColumnWidth, AutoScrollMinSize.Height);
            graphics.DrawLine(majorGridPen, stickyX + CarColumnWidth, 0, stickyX + CarColumnWidth, AutoScrollMinSize.Height);
            graphics.DrawString("Car", FontHelper.SemiBold(9F), textBrush, new PointF(stickyX + 14, 15));

            int drawableRows = Math.Min(_owner._cars.Count, _rowLayouts.Count);
            for (int row = 0; row < drawableRows; row++)
            {
                RowLayout rowLayout = _rowLayouts[row];
                float nameY = rowLayout.Top + Math.Max((rowLayout.Height - 34) / 2F, 8);
                graphics.DrawLine(gridPen, stickyX, rowLayout.Top, stickyX + CarColumnWidth, rowLayout.Top);
                graphics.DrawString(rowLayout.Car.CarName, FontHelper.SemiBold(9F), textBrush, new PointF(stickyX + 14, nameY));
                graphics.DrawString(rowLayout.Car.PlateNumber, FontHelper.Regular(8.5F), mutedBrush, new PointF(stickyX + 14, nameY + 18));
            }
        }

        private void RebuildLayouts()
        {
            DateTime monthStart = _owner.SelectedMonth;
            DateTime monthEnd = monthStart.AddMonths(1).AddDays(-1);
            List<RowLayout> rows = [];
            int currentTop = HeaderHeight;

            foreach (Car car in _owner._cars)
            {
                List<ScheduleSpan> spans = _owner._schedules
                    .Where(schedule => schedule.CarId == car.CarId)
                    .Select(schedule =>
                    {
                        DateTime visibleStart = schedule.StartDate < monthStart ? monthStart : schedule.StartDate;
                        DateTime visibleEnd = schedule.EndDate > monthEnd ? monthEnd : schedule.EndDate;
                        return new ScheduleSpan(schedule, visibleStart, visibleEnd);
                    })
                    .Where(span => span.VisibleEnd >= span.VisibleStart)
                    .OrderBy(span => span.StartDay)
                    .ThenBy(span => span.EndDay)
                    .ThenBy(span => span.Schedule.ScheduleId)
                    .ToList();

                List<int> laneEndDays = [];
                List<ScheduleLayout> scheduleLayouts = [];
                foreach (ScheduleSpan span in spans)
                {
                    int lane = FindAvailableLane(laneEndDays, span.StartDay);
                    if (lane == laneEndDays.Count)
                    {
                        laneEndDays.Add(span.EndDay);
                    }
                    else
                    {
                        laneEndDays[lane] = span.EndDay;
                    }

                    Rectangle bounds = CreateScheduleBounds(span, currentTop, lane);
                    scheduleLayouts.Add(new ScheduleLayout(span.Schedule, bounds));
                }

                int laneCount = Math.Max(laneEndDays.Count, 1);
                int rowHeight = Math.Max(
                    MinimumRowHeight,
                    RowTopPadding + laneCount * PillHeight + Math.Max(laneCount - 1, 0) * LaneSpacing + RowBottomPadding);
                rows.Add(new RowLayout(car, currentTop, rowHeight, scheduleLayouts));
                currentTop += rowHeight;
            }

            _rowLayouts = rows;
        }

        private static int FindAvailableLane(IReadOnlyList<int> laneEndDays, int startDay)
        {
            for (int lane = 0; lane < laneEndDays.Count; lane++)
            {
                if (laneEndDays[lane] < startDay)
                {
                    return lane;
                }
            }

            return laneEndDays.Count;
        }

        private static Rectangle CreateScheduleBounds(ScheduleSpan span, int rowTop, int lane)
        {
            int durationDays = span.EndDay - span.StartDay + 1;
            return new Rectangle(
                CarColumnWidth + span.StartDay * DayWidth + 4,
                rowTop + RowTopPadding + lane * (PillHeight + LaneSpacing),
                Math.Max(durationDays * DayWidth - 8, 20),
                PillHeight);
        }

        private sealed record RowLayout(Car Car, int Top, int Height, IReadOnlyList<ScheduleLayout> Schedules);

        private sealed record ScheduleLayout(Models.FleetSchedule Schedule, Rectangle Bounds);

        private sealed record ScheduleSpan(Models.FleetSchedule Schedule, DateTime VisibleStart, DateTime VisibleEnd)
        {
            public int StartDay => VisibleStart.Day - 1;
            public int EndDay => VisibleEnd.Day - 1;
        }
    }

    private sealed class RoundedLegendMarker : Control
    {
        private readonly Color _fillColor;

        public RoundedLegendMarker(Color fillColor)
        {
            _fillColor = fillColor;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new(0, 0, Width - 1, Height - 1);
            using GraphicsPath path = FleetScheduleVisualHelper.CreateRoundedRectanglePath(bounds, Height / 2);
            using SolidBrush brush = new(_fillColor);
            e.Graphics.FillPath(brush, path);
        }
    }
}
