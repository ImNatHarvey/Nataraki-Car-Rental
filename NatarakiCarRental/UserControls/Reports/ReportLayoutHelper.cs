using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.UserControls.Cards;

namespace NatarakiCarRental.UserControls.Reports;

public static class ReportLayoutHelper
{
    public static void ConfigureReportPage(Control page)
    {
        page.BackColor = ThemeHelper.ContentBackground;
        page.Padding = new Padding(14, 20, 14, 24);
    }

    public static FlowLayoutPanel CreateMetricPanel()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 8, 0, 0),
            Margin = new Padding(0, 0, 0, 10),
            BackColor = ThemeHelper.ContentBackground,
            Visible = true
        };
    }

    public static void LayoutMetricCards(FlowLayoutPanel panel, IReadOnlyList<Control> cards)
    {
        if (panel.IsDisposed || cards.Count == 0)
        {
            return;
        }

        int availableWidth = Math.Max(panel.ClientSize.Width, panel.Parent?.ClientSize.Width ?? 0);
        if (availableWidth <= 0)
        {
            availableWidth = 1000;
        }

        int columns = availableWidth < 1150 ? 3 : 4;
        int gap = 14;
        int cardHeight = 132;
        int horizontalPadding = panel.Padding.Left + panel.Padding.Right;
        int cardWidth = Math.Max(220, (availableWidth - horizontalPadding - (gap * columns)) / columns);
        int rows = (int)Math.Ceiling(cards.Count / (double)columns);
        int height = panel.Padding.Top + panel.Padding.Bottom + (rows * cardHeight) + ((rows - 1) * gap);

        panel.SuspendLayout();
        panel.Visible = true;
        panel.Height = height;
        foreach (Control card in cards)
        {
            card.Dock = DockStyle.None;
            card.Margin = new Padding(0, 0, gap, gap);
            card.Size = new Size(cardWidth, cardHeight);
        }
        panel.ResumeLayout(true);
    }

    public static void AddMetricCard(FlowLayoutPanel panel, MetricCardControl card, IconChar icon, string title, string value, string helperText, Color iconColor)
    {
        card.Dock = DockStyle.None;
        card.SetMetric(icon, title, value, helperText, iconColor);
        if (!panel.Controls.Contains(card))
        {
            panel.Controls.Add(card);
        }
    }

    public static DataGridView CreateSummaryGrid()
    {
        DataGridView grid = new()
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToResizeColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = ThemeHelper.Surface,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersHeight = 36,
            EnableHeadersVisualStyles = false,
            GridColor = ThemeHelper.TableGridLine,
            ReadOnly = true,
            RowHeadersVisible = false,
            RowTemplate = { Height = 34 },
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ScrollBars = ScrollBars.Both
        };

        grid.DefaultCellStyle.SelectionBackColor = ThemeHelper.Surface;
        grid.DefaultCellStyle.SelectionForeColor = ThemeHelper.TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.BackColor = ThemeHelper.Primary;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemeHelper.Primary;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = FontHelper.SemiBold(9F);
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        return grid;
    }

    public static Panel CreateGridCard(string title, DataGridView grid, int height = 0)
    {
        Panel card = ControlFactory.CreateCardPanel(new Size(0, height));
        card.Dock = height > 0 ? DockStyle.Top : DockStyle.Fill;
        card.Padding = new Padding(16);
        card.Margin = new Padding(0, 0, 0, 14);

        Label titleLabel = new()
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 28,
            Font = FontHelper.SemiBold(11F),
            ForeColor = ThemeHelper.TextPrimary
        };

        card.Controls.Add(grid);
        card.Controls.Add(titleLabel);
        return card;
    }

    public static void AddEmptyRow(DataGridView grid)
    {
        if (grid.Columns.Count == 0 || grid.Rows.Count > 0)
        {
            return;
        }

        object[] cells = Enumerable.Repeat<object>(string.Empty, grid.Columns.Count).ToArray();
        cells[0] = "No records found for this date range.";
        grid.Rows.Add(cells);
    }

    public static string FormatPeso(decimal amount) => $"₱{amount:N2}";

    public static string FormatPercent(decimal value) => $"{value:N1}%";

    public static string FormatDate(DateTime date) => $"{date:MMM d, yyyy}";
}
