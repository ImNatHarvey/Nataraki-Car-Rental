using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.UserControls.Cards;

namespace NatarakiCarRental.UserControls.Reports;

public static class ReportLayoutHelper
{
    public static void ConfigureReportPage(Control page)
    {
        page.BackColor = ThemeHelper.ContentBackground;
        page.Padding = new Padding(16, 20, 16, 24);
    }

    public static FlowLayoutPanel CreateMetricPanel()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 8, 0, 0),
            Margin = new Padding(0, 0, 0, 16),
            BackColor = ThemeHelper.ContentBackground
        };
    }

    public static void LayoutMetricCards(FlowLayoutPanel panel, IReadOnlyList<Control> cards)
    {
        if (panel.Width == 0) return;
        
        int availableWidth = panel.Width - panel.Padding.Horizontal - 24;
        int columns = availableWidth < 1200 ? 3 : 4;
        if (availableWidth < 800) columns = 2;

        int gap = 16;
        int cardWidth = (availableWidth - (gap * (columns - 1))) / columns;

        panel.SuspendLayout();
        foreach (Control card in cards)
        {
            card.Size = new Size(cardWidth, 132);
            card.Margin = new Padding(0, 0, gap, gap);
        }
        panel.ResumeLayout();
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
            AllowUserToResizeColumns = true,
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
        card.Margin = new Padding(0, 0, 14, 18);

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
        if (grid.Columns.Count == 0 || grid.Rows.Count > 0) return;
        object[] cells = Enumerable.Repeat<object>(string.Empty, grid.Columns.Count).ToArray();
        cells[0] = "No records found.";
        grid.Rows.Add(cells);
    }

    public static Panel CreateSectionHeader(string title)
    {
        Panel panel = new()
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(0, 16, 0, 0)
        };

        Label label = new()
        {
            Text = title.ToUpperInvariant(),
            Dock = DockStyle.Fill,
            Font = FontHelper.SemiBold(10F),
            ForeColor = ThemeHelper.Primary,
            TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(4, 0, 0, 4)
        };

        panel.Controls.Add(label);

        Panel underline = new()
        {
            Height = 2,
            BackColor = Color.FromArgb(40, ThemeHelper.Primary),
            Dock = DockStyle.Bottom
        };
        panel.Controls.Add(underline);

        return panel;
    }

    public static string FormatPeso(decimal amount) => $"₱{amount:N2}";
    public static string FormatPercent(decimal value) => $"{value:N1}%";
    public static string FormatDate(DateTime date) => $"{date:MMM d, yyyy}";
}
