using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Claudgar.App.Browser;

/// <summary>A quiet rounded surface. Its corners are painted without allocating a window region.</summary>
internal sealed class RoundedPanel : Panel
{
    private float cornerRadius = 18;
    private Color borderColor = Color.Transparent;
    private readonly ControlScaleState scaleState = new();

    public RoundedPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = BrowserTheme.Surface;
        Padding = new Padding(24);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float CornerRadius
    {
        get => cornerRadius;
        set { cornerRadius = Math.Max(0, value); Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor
    {
        get => borderColor;
        set { borderColor = value; Invalidate(); }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? BrowserTheme.Background);
        if (Width <= 1 || Height <= 1) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = RoundedDrawing.CreatePath(new RectangleF(0, 0, Width - 1, Height - 1), CornerRadius * scaleState.Factor(DeviceDpi));
        using var fill = new SolidBrush(BackColor);
        e.Graphics.FillPath(fill, shape);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (BorderColor.A == 0 || Width <= 1 || Height <= 1) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = scaleState.Factor(DeviceDpi);
        using var shape = RoundedDrawing.CreatePath(new RectangleF(.5f, .5f, Width - 2, Height - 2), CornerRadius * scale);
        using var outline = new Pen(BorderColor, Math.Max(1, scale));
        e.Graphics.DrawPath(outline, shape);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        scaleState.DpiChanged(DeviceDpi);
        Invalidate();
    }

    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        scaleState.Apply(factor, specified, DeviceDpi);
        base.ScaleControl(factor, specified);
        Invalidate();
    }
}
