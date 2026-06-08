using System.Drawing.Drawing2D;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.UserControls.Common;

namespace NatarakiCarRental.Helpers;

public static class DataGridViewHelper
{
    private const float StatusPillHeight = 26F;

    public static void SetupStatusPills(DataGridView grid, params string[] columnNames)
    {
        SetupStatusPills(grid, ContentAlignment.MiddleLeft, columnNames);
    }

    public static void SetupStatusPills(DataGridView grid, ContentAlignment alignment, params string[] columnNames)
    {
        grid.CellPainting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            
            string columnName = grid.Columns[e.ColumnIndex].Name;
            if (!columnNames.Contains(columnName)) return;

            e.PaintBackground(e.CellBounds, true);
            string text = e.Value?.ToString() ?? string.Empty;
            
            if (string.IsNullOrWhiteSpace(text) || e.Graphics is null) return;

            RenderStatusPill(e, text, alignment);
            e.Handled = true;
        };
    }

    public static void RenderStatusPill(DataGridViewCellPaintingEventArgs e, string text, ContentAlignment alignment = ContentAlignment.MiddleCenter)
    {
        if (e.Graphics == null) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Font font = e.CellStyle?.Font ?? FontHelper.SemiBold(9F);
        Color color = GetStatusColor(text);

        float height = StatusPillHeight;
        float width = Math.Min(100F, e.CellBounds.Width - 16); // Slightly smaller standard pill width
        
        float x;
        if (alignment == ContentAlignment.MiddleLeft)
            x = e.CellBounds.X + 8; // Align with typical grid text padding
        else if (alignment == ContentAlignment.MiddleRight)
            x = e.CellBounds.Right - width - 8;
        else
            x = e.CellBounds.X + (e.CellBounds.Width - width) / 2F;

        float y = e.CellBounds.Y + (e.CellBounds.Height - height) / 2F;

        RectangleF rect = new(x, y, width, height);
        
        using GraphicsPath path = CreateRoundedRect(rect, height / 2);
        using SolidBrush background = new(color);
        using SolidBrush foreground = new(Color.White);

        e.Graphics.FillPath(background, path);

        using StringFormat format = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };
        
        e.Graphics.DrawString(text, font, foreground, rect, format);
    }

    public static Color GetStatusColor(string status)
    {
        return status.ToUpperInvariant() switch
        {
            "PAID" or "ACTIVE" or "RENTED" or "SUCCESS" or "ONGOING" or "AVAILABLE" => ThemeHelper.Success,
            "PENDING" or "PARTIAL" or "DUE" or "RESERVED" or "MAINTENANCE" => ThemeHelper.Warning,
            "UNPAID" or "CANCELLED" or "OVERDUE" or "BLACKLISTED" or "DANGER" or "HIGH" => ThemeHelper.Danger,
            "COMPLETED" or "ARCHIVED" or "CLOSED" or "N/A" or "LOW" or "MEDIUM" => ThemeHelper.GrayIcon,
            _ => ThemeHelper.Primary
        };
    }

    public static Panel CreateBorderedContainer(Control control, int padding = 16)
    {
        BorderedPanel container = new()
        {
            Dock = control.Dock,
            Height = control.Height,
            Padding = new Padding(padding),
            BackColor = ThemeHelper.Surface,
            BorderColor = ThemeHelper.Border
        };
        
        control.Dock = DockStyle.Fill;
        container.Controls.Add(control);
        return container;
    }

    private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
    {
        GraphicsPath path = new();
        float diameter = radius * 2;
        RectangleF arc = new(rect.Location, new SizeF(diameter, diameter));
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
}
