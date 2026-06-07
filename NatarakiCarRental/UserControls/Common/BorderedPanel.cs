using NatarakiCarRental.Helpers;
using System.ComponentModel;

namespace NatarakiCarRental.UserControls.Common;

public class BorderedPanel : Panel
{
    public BorderedPanel()
    {
        BackColor = ThemeHelper.Surface;
        BorderStyle = BorderStyle.None;
        ResizeRedraw = true;
        DoubleBuffered = true;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = ThemeHelper.Border;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using Pen borderPen = new(BorderColor);
        Rectangle rectangle = new(0, 0, Width - 1, Height - 1);
        e.Graphics.DrawRectangle(borderPen, rectangle);
    }
}
