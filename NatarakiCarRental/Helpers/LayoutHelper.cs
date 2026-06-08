using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.UserControls.Cards;

namespace NatarakiCarRental.Helpers;

public static class LayoutHelper
{
    public static void LayoutMetricCards(Control container, IReadOnlyList<Control> cards, int? fixedColumns = null)
    {
        if (container.IsDisposed || cards.Count == 0) return;

        bool isFlowLayout = container is FlowLayoutPanel;
        int availableWidth = container.Width;
        if (availableWidth <= 0 && container.Parent != null)
            availableWidth = container.Parent.ClientSize.Width - container.Parent.Padding.Horizontal;
        
        if (availableWidth <= 0) availableWidth = 1000;

        // KPI card specific logic
        int columns = fixedColumns ?? (availableWidth < 1150 ? 3 : 4);
        if (!fixedColumns.HasValue && availableWidth < 800) columns = 2;

        int gap = 14;
        int cardHeight = 132;
        int horizontalPadding = container.Padding.Left + container.Padding.Right;
        
        int cardWidth = (availableWidth - horizontalPadding - (gap * (columns - 1))) / columns;
        int rows = (int)Math.Ceiling(cards.Count / (double)columns);
        int totalHeight = container.Padding.Top + container.Padding.Bottom + (rows * cardHeight) + ((rows - 1) * gap) + 4;

        container.SuspendLayout();
        
        // Only set height if not a FlowLayoutPanel (which manages its own flow usually, but we are manual here)
        if (!isFlowLayout) container.Height = totalHeight;

        for (int i = 0; i < cards.Count; i++)
        {
            Control card = cards[i];
            int col = i % columns;
            int row = i / columns;

            card.Size = new Size(cardWidth, cardHeight);
            
            if (isFlowLayout)
            {
                // For FlowLayoutPanel, we use margins instead of manual Location
                card.Margin = new Padding(0, 0, col == columns - 1 ? 0 : gap, gap);
            }
            else
            {
                card.Location = new Point(
                    container.Padding.Left + (col * (cardWidth + gap)),
                    container.Padding.Top + (row * (cardHeight + gap))
                );
            }
        }
        
        container.ResumeLayout(true);
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
