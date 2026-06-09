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
        Padding = new Padding(32, 8, 32, 32);

        TableLayoutPanel mainLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        mainLayout.Controls.Add(CreateToolbarPanel(), 0, 0);
        mainLayout.Controls.Add(CreateTimelineHost(), 0, 1);
        Controls.Add(mainLayout);
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

        // Reservation Category
        Label resLabel = new() { Text = "Reservation:", Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextSecondary, AutoSize = true, Margin = new Padding(0, 4, 8, 0) };
        panel.Controls.Add(resLabel);
        AddLegendItem(panel, "Pending", FleetScheduleVisualHelper.GetColor(FleetScheduleConstants.Status.Pending));
        AddLegendItem(panel, "Reserved", FleetScheduleVisualHelper.GetColor(FleetScheduleConstants.Status.Reserved));
        AddLegendItem(panel, "Rented", FleetScheduleVisualHelper.GetColor(FleetScheduleConstants.Status.Rented));
        AddLegendItem(panel, "Completed", FleetScheduleVisualHelper.GetColor(FleetScheduleConstants.Status.Completed));
        AddLegendItem(panel, "Cancelled", FleetScheduleVisualHelper.GetColor(FleetScheduleConstants.Status.Cancelled));

        // Separator
        Label sep = new() { Text = " | ", Font = FontHelper.Regular(9F), ForeColor = ThemeHelper.Border, AutoSize = true, Margin = new Padding(8, 4, 8, 0) };
        panel.Controls.Add(sep);

        // Maintenance Category
        Label maintLabel = new() { Text = "Maintenance:", Font = FontHelper.SemiBold(9F), ForeColor = ThemeHelper.TextSecondary, AutoSize = true, Margin = new Padding(0, 4, 8, 0) };
        panel.Controls.Add(maintLabel);
        AddLegendItem(panel, "Pending", FleetScheduleVisualHelper.GetColor(FleetScheduleConstants.Status.Pending));
        AddLegendItem(panel, "Reserved", FleetScheduleVisualHelper.GetColor(FleetScheduleConstants.Status.Reserved));
        AddLegendItem(panel, "Maintenance", FleetScheduleVisualHelper.GetColor(FleetScheduleConstants.Status.Maintenance));
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
        FleetScheduleFormMode mode = FleetScheduleFormMode.Edit;
        string? viewNote = null;

        if (schedule.ScheduleType == FleetScheduleConstants.Type.Rental)
        {
            mode = FleetScheduleFormMode.View;
            viewNote = "This schedule is managed through the Transactions module.";
        }
        else if (schedule.Status == FleetScheduleConstants.Status.Completed || 
                 schedule.Status == FleetScheduleConstants.Status.Cancelled)
        {
            mode = FleetScheduleFormMode.View;
            viewNote = "This historical schedule is view-only.";
        }
        else if (schedule.ScheduleType == FleetScheduleConstants.Type.Maintenance && 
                 schedule.Status != FleetScheduleConstants.Status.Pending)
        {
            mode = FleetScheduleFormMode.View;
            viewNote = "This maintenance schedule is managed through the Offsite module.";
        }
        else
        {
            bool isLinked = await _scheduleService.IsLinkedToActiveTransactionAsync(schedule.ScheduleId);
            if (isLinked)
            {
                mode = FleetScheduleFormMode.View;
                viewNote = "This schedule is managed through the Transactions module.";
            }
        }

        using FleetScheduleDetailsForm form = new(mode, _currentUserId, schedule, viewNote: viewNote);
        if (form.ShowDialog() == DialogResult.OK)
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
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
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

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            if (se.ScrollOrientation == ScrollOrientation.HorizontalScroll)
            {
                // Invalidate the entire area to ensure the sticky column is redrawn correctly
                // and to avoid bit-blit artifacts from AutoScroll.
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            
            // Use high quality
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int days = DateTime.DaysInMonth(_owner.SelectedMonth.Year, _owner.SelectedMonth.Month);
            int totalWidth = CarColumnWidth + days * DayWidth;
            int totalHeight = AutoScrollMinSize.Height;

            // Fill background - visible area only
            graphics.FillRectangle(Brushes.White, e.ClipRectangle);

            // Apply scroll transformation to EVERYTHING
            graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

            // Determine visible range
            Rectangle visibleRect = new(-AutoScrollPosition.X, -AutoScrollPosition.Y, ClientSize.Width, ClientSize.Height);

            using Pen gridPen = new(ThemeHelper.TableGridLine);
            using Pen majorGridPen = new(ThemeHelper.TableGridLineStrong);
            using SolidBrush headerBrush = new(ThemeHelper.ContentBackground);
            using SolidBrush surfaceBrush = new(ThemeHelper.Surface);
            using SolidBrush textBrush = new(ThemeHelper.TextPrimary);
            using SolidBrush mutedBrush = new(ThemeHelper.TextSecondary);
            using SolidBrush weekendBrush = new(Color.FromArgb(248, 250, 252));
            using SolidBrush todayBrush = new(Color.FromArgb(239, 246, 255));

            // 1. Draw Day Backgrounds (Weekend/Today)
            for (int day = 0; day < days; day++)
            {
                int x = CarColumnWidth + day * DayWidth;
                if (x + DayWidth < visibleRect.Left || x > visibleRect.Right) continue;

                DateTime date = _owner.SelectedMonth.AddDays(day);
                
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    graphics.FillRectangle(weekendBrush, x, HeaderHeight, DayWidth, totalHeight - HeaderHeight);
                }

                if (date.Date == DateTime.Today)
                {
                    graphics.FillRectangle(todayBrush, x, 0, DayWidth, totalHeight);
                }
            }

            // 2. Draw Row Lines and Car Names
            int drawableRows = Math.Min(_owner._cars.Count, _rowLayouts.Count);
            for (int row = 0; row < drawableRows; row++)
            {
                RowLayout rowLayout = _rowLayouts[row];
                if (rowLayout.Top + rowLayout.Height < visibleRect.Top || rowLayout.Top > visibleRect.Bottom) continue;

                float nameY = rowLayout.Top + Math.Max((rowLayout.Height - 34) / 2F, 8);
                
                graphics.DrawLine(gridPen, 0, rowLayout.Top, totalWidth, rowLayout.Top);
                
                // Draw name and plate (Sticky column effect is handled by redrawing everything on scroll, 
                // but we draw background for the sticky column later)
            }

            // 3. Draw Grid Vertical Lines
            for (int day = 0; day <= days; day++)
            {
                int x = CarColumnWidth + day * DayWidth;
                if (x < visibleRect.Left || x > visibleRect.Right) continue;

                DateTime date = day < days ? _owner.SelectedMonth.AddDays(day) : _owner.SelectedMonth.AddDays(days - 1).AddDays(1);
                graphics.DrawLine(date.Day == 1 || day == days ? majorGridPen : gridPen, x, 0, x, totalHeight);
            }

            // 4. Draw Schedules
            _scheduleBounds.Clear();
            foreach (ScheduleLayout layout in _rowLayouts.SelectMany(rowLayout => rowLayout.Schedules))
            {
                Rectangle rect = layout.Bounds;
                _scheduleBounds[layout.Schedule.ScheduleId] = rect;

                // Only draw if visible
                if (rect.Right < visibleRect.Left || rect.Left > visibleRect.Right || 
                    rect.Bottom < visibleRect.Top || rect.Top > visibleRect.Bottom) continue;

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

            // 5. Sticky Headers and Columns (Drawn last to be on top)
            // Restore transformation for sticky elements
            graphics.ResetTransform();

            // Sticky Left Column (Cars)
            Rectangle carColHeaderRect = new(0, 0, CarColumnWidth, HeaderHeight);
            graphics.FillRectangle(headerBrush, carColHeaderRect);
            graphics.DrawString("Car", FontHelper.SemiBold(9F), textBrush, new PointF(14, 15));
            graphics.DrawLine(majorGridPen, CarColumnWidth, 0, CarColumnWidth, Height);
            graphics.DrawLine(majorGridPen, 0, HeaderHeight, Width, HeaderHeight);

            // Sticky Rows for visible range
            for (int row = 0; row < drawableRows; row++)
            {
                RowLayout rowLayout = _rowLayouts[row];
                int screenTop = rowLayout.Top + AutoScrollPosition.Y;
                if (screenTop + rowLayout.Height < HeaderHeight || screenTop > Height) continue;

                Rectangle carNameRect = new(0, screenTop, CarColumnWidth, rowLayout.Height);
                graphics.FillRectangle(headerBrush, carNameRect);
                graphics.DrawLine(gridPen, 0, screenTop, CarColumnWidth, screenTop);
                
                float nameY = screenTop + Math.Max((rowLayout.Height - 34) / 2F, 8);
                graphics.DrawString(rowLayout.Car.CarName, FontHelper.SemiBold(9F), textBrush, new PointF(14, nameY));
                graphics.DrawString(rowLayout.Car.PlateNumber, FontHelper.Regular(8.5F), mutedBrush, new PointF(14, nameY + 18));
            }

            // Sticky Top Header (Days)
            graphics.SetClip(new Rectangle(CarColumnWidth + 1, 0, Width - CarColumnWidth - 1, HeaderHeight));
            graphics.TranslateTransform(AutoScrollPosition.X, 0); // Only X scroll for day headers
            for (int day = 0; day < days; day++)
            {
                int x = CarColumnWidth + day * DayWidth;
                if (x + DayWidth < visibleRect.Left || x > visibleRect.Right) continue;

                DateTime date = _owner.SelectedMonth.AddDays(day);
                graphics.DrawString(date.Day.ToString(), FontHelper.SemiBold(9F), textBrush, new PointF(x + 13, 8));
                graphics.DrawString(date.ToString("ddd"), FontHelper.Regular(8F), mutedBrush, new PointF(x + 10, 24));
                
                graphics.DrawLine(date.Day == 1 ? majorGridPen : gridPen, x, 0, x, HeaderHeight);
            }
            graphics.ResetTransform();
            graphics.ResetClip();

            // 6. Today Indicator (Must be scrolled)
            graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            DrawTodayIndicator(graphics, days);
            graphics.ResetTransform();

            if (_owner._cars.Count == 0)
            {
                const string message = "No active cars available. Add cars in Car Garage first.";
                SizeF messageSize = graphics.MeasureString(message, FontHelper.Regular(11F));
                float x = (Width - messageSize.Width) / 2;
                float y = (Height - messageSize.Height) / 2;
                graphics.DrawString(message, FontHelper.Regular(11F), mutedBrush, new PointF(x, y));
            }
        }

        private void DrawTodayIndicator(Graphics graphics, int daysInMonth)
        {
            if (_owner.SelectedMonth.Year != DateTime.Today.Year || _owner.SelectedMonth.Month != DateTime.Today.Month)
            {
                return;
            }

            int today = DateTime.Today.Day;
            int x = CarColumnWidth + (today - 1) * DayWidth;
            int totalHeight = AutoScrollMinSize.Height;
            
            // Draw Line
            using Pen todayPen = new(ThemeHelper.Primary, 2);
            graphics.DrawLine(todayPen, x, 0, x, totalHeight);

            // Draw Label
            using SolidBrush bgBrush = new(ThemeHelper.Primary);
            Rectangle labelRect = new(x - 22, 2, 44, 14);
            using GraphicsPath path = FleetScheduleVisualHelper.CreateRoundedRectanglePath(labelRect, 4);
            graphics.FillPath(bgBrush, path);

            using StringFormat format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString("TODAY", FontHelper.SemiBold(7F), Brushes.White, labelRect, format);
        }

        private Point TranslatePoint(Point point)
        {
            return new Point(point.X - AutoScrollPosition.X, point.Y - AutoScrollPosition.Y);
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
