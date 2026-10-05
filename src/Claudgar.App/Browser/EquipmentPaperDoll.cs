using System.Drawing.Drawing2D;

namespace Claudgar.App.Browser;

/// <summary>A neutral silhouette; saved equipment does not contain a rendered player model.</summary>
internal static class EquipmentPaperDoll
{
    public static void Draw(Graphics g, RectangleF bounds)
    {
        using var shade = new LinearGradientBrush(bounds, Color.FromArgb(23, 29, 32), Color.FromArgb(18, 24, 28), LinearGradientMode.Vertical);
        g.FillRectangle(shade, bounds);
        var state = g.Save(); g.TranslateTransform(bounds.X + bounds.Width / 2, bounds.Y + 16);
        using var outline = new Pen(Color.FromArgb(115, 102, 75), 1.6f);
        using var body = new SolidBrush(Color.FromArgb(46, 49, 45));
        using var shadow = new SolidBrush(Color.FromArgb(12, 17, 21));
        g.FillEllipse(shadow, -110, 391, 220, 25);
        using var shape = new GraphicsPath();
        shape.AddPolygon([new(-28, 80), new(-78, 91), new(-110, 129), new(-122, 218), new(-102, 251),
            new(-83, 241), new(-77, 173), new(-58, 139), new(-57, 213), new(-72, 344), new(-85, 379),
            new(-70, 395), new(-21, 391), new(-10, 312), new(10, 312), new(21, 391), new(70, 395),
            new(85, 379), new(72, 344), new(57, 213), new(58, 139), new(77, 173), new(83, 241),
            new(102, 251), new(122, 218), new(110, 129), new(78, 91), new(28, 80)]);
        g.FillPath(body, shape); g.DrawPath(outline, shape);
        g.FillEllipse(body, -34, 5, 68, 76); g.DrawEllipse(outline, -34, 5, 68, 76);
        g.DrawLine(outline, -28, 81, -15, 108); g.DrawLine(outline, 28, 81, 15, 108);
        g.DrawLine(outline, -15, 108, 15, 108);
        g.DrawArc(outline, -57, 125, 114, 50, 0, 180);
        g.DrawLine(outline, -56, 209, 56, 209);
        g.DrawLine(outline, 0, 226, 0, 305);
        g.DrawLine(outline, -50, 226, -58, 346); g.DrawLine(outline, 50, 226, 58, 346);
        g.Restore(state);
    }
}
