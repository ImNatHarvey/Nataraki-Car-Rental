using System.Drawing.Drawing2D;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.Helpers;

public static class DataGridViewHelper
{
    private const float StatusPillHeight = 26F;

    public static void ApplyStandardStyle(DataGridView grid)
    {
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AllowUserToResizeColumns = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.BackgroundColor = ThemeHelper.Surface;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        grid.ColumnHeadersHeight = 38;
        grid.EnableHeadersVisualStyles = false;
        grid.GridColor = ThemeHelper.TableGridLine;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.RowTemplate.Height = 38;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.DefaultCellStyle.SelectionBackColor = ThemeHelper.Surface;
        grid.DefaultCellStyle.SelectionForeColor = ThemeHelper.TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.BackColor = ThemeHelper.Primary;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemeHelper.Primary;
        grid.ColumnHeadersDefaultCellStyle.Font = FontHelper.SemiBold(9F);
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
    }

    public static void SetupStatusPills(DataGridView grid, params string[] columnNames)
    {
        SetupStatusPills(grid, ContentAlignment.MiddleLeft, columnNames);
    }

    public static void SetupStatusPills(DataGridView grid, ContentAlignment alignment, params string[] columnNames)
    {
        if (columnNames.Length == 0) return;

        var configuredColumns = grid.Tag as HashSet<string> ?? [];
        bool alreadyHasHandler = configuredColumns.Count > 0;

        foreach (var col in columnNames) configuredColumns.Add(col);
        grid.Tag = configuredColumns;

        if (alreadyHasHandler) return;

        grid.CellPainting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Tag is not HashSet<string> columns) return;
            
            string columnName = grid.Columns[e.ColumnIndex].Name;
            if (!columns.Contains(columnName)) return;

            e.PaintBackground(e.CellBounds, true);
            string text = e.Value?.ToString() ?? string.Empty;
            
            if (string.IsNullOrWhiteSpace(text) || e.Graphics is null) return;

            RenderStatusPill(e, text, alignment);
            e.Handled = true;
        };
    }

    public static void SetupActionButtons(DataGridView grid, string columnName = "Actions")
    {
        grid.CellPainting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (grid.Columns[e.ColumnIndex].Name != columnName) return;

            e.PaintBackground(e.CellBounds, true);
            string text = e.Value?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text) || e.Graphics is null) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Font font = e.CellStyle?.Font ?? FontHelper.SemiBold(9F);

            string[] actions = text.Split('|');
            var layout = GetActionButtonsLayout(e.Graphics, e.CellBounds, font, actions);

            foreach (var entry in layout)
            {
                Color color = GetActionColor(entry.Action);
                using GraphicsPath path = CreateRoundedRectanglePath(entry.Bounds, entry.Bounds.Height / 2);
                using SolidBrush background = new(color);
                using SolidBrush foreground = new(ThemeHelper.GetContrastTextColor(color));

                e.Graphics.FillPath(background, path);

                using StringFormat format = new()
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                e.Graphics.DrawString(entry.Action, font, foreground, entry.Bounds, format);
            }

            e.Handled = true;
        };
    }

    public static string? GetClickedAction(DataGridView grid, int rowIndex, int columnIndex, int x, int y, string columnName = "Actions")
    {
        if (rowIndex < 0 || columnIndex < 0 || grid.Columns[columnIndex].Name != columnName) return null;

        string text = grid.Rows[rowIndex].Cells[columnIndex].Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return null;

        using Graphics graphics = grid.CreateGraphics();
        Font font = grid.DefaultCellStyle.Font ?? FontHelper.SemiBold(9F);
        
        string[] actions = text.Split('|');
        float currentX = 4F;
        float yOffset = (grid.Rows[rowIndex].Height - StatusPillHeight) / 2F;

        foreach (string action in actions)
        {
            float width = graphics.MeasureString(action, font).Width + 22F;
            RectangleF rect = new(currentX, yOffset, width, StatusPillHeight);
            if (rect.Contains(x, y)) return action;
            currentX += width + 6F;
        }

        return null;
    }

    private static List<(string Action, RectangleF Bounds)> GetActionButtonsLayout(Graphics g, Rectangle cellBounds, Font font, string[] actions)
    {
        List<(string Action, RectangleF Bounds)> result = [];
        float currentX = cellBounds.X + 4;
        float height = StatusPillHeight;
        float y = cellBounds.Y + (cellBounds.Height - height) / 2;

        foreach (string action in actions)
        {
            float width = g.MeasureString(action, font).Width + 22F;
            result.Add((action, new RectangleF(currentX, y, width, height)));
            currentX += width + 6;
        }
        return result;
    }

    private static Color GetActionColor(string action)
    {
        return action switch
        {
            "View" or "Details" => ThemeHelper.Primary,
            "Edit" or "Payment" or "Start Rental" or "Remove Blacklist" or "Restore" or "Complete" => ThemeHelper.Success,
            "Cancel" or "Archive" or "Blacklist" => ThemeHelper.Danger,
            "Extend" => ThemeHelper.Warning,
            "Mark as Read" => ThemeHelper.StatusGray,
            _ => ThemeHelper.GrayIcon
        };
    }

    public static Color GetStatusColor(string status)
    {
        return StatusColorHelper.GetStatusColor(status);
    }

    private static void RenderStatusPill(DataGridViewCellPaintingEventArgs e, string text, ContentAlignment alignment)
    {
        if (e.Graphics is null) return;

        Color backColor = StatusColorHelper.GetStatusColor(text);
        Color foreColor = StatusColorHelper.GetStatusForeColor(text);
        Font font = e.CellStyle?.Font ?? FontHelper.SemiBold(8.5F);

        SizeF textSize = e.Graphics.MeasureString(text, font);
        float width = Math.Min(textSize.Width + 24, e.CellBounds.Width - 8);
        float height = StatusPillHeight;

        float x = alignment switch
        {
            ContentAlignment.MiddleRight => e.CellBounds.Right - width - 8,
            ContentAlignment.MiddleCenter => e.CellBounds.X + (e.CellBounds.Width - width) / 2,
            _ => e.CellBounds.X + 8
        };
        float y = e.CellBounds.Y + (e.CellBounds.Height - height) / 2;

        RectangleF pillRect = new(x, y, width, height);
        using GraphicsPath path = CreateRoundedRectanglePath(pillRect, height / 2);
        using SolidBrush backBrush = new(backColor);
        using SolidBrush foreBrush = new(foreColor);

        e.Graphics.FillPath(backBrush, path);

        using StringFormat format = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        e.Graphics.DrawString(text, font, foreBrush, pillRect, format);
    }

    public static GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float radius)
    {
        GraphicsPath path = new();
        float diameter = radius * 2;
        if (diameter > rect.Width) diameter = rect.Width;
        if (diameter > rect.Height) diameter = rect.Height;
        
        RectangleF arc = new(rect.Location, new SizeF(diameter, diameter));

        if (radius <= 0)
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

    public static Panel CreateBorderedContainer(Control control)
    {
        BorderedPanel container = new()
        {
            Padding = new Padding(1),
            BorderColor = ThemeHelper.Border,
            BackColor = ThemeHelper.Border
        };
        control.Dock = DockStyle.Fill;
        container.Controls.Add(control);
        return container;
    }
}
