using System.Drawing.Drawing2D;

namespace Claudgar.App.Browser;

/// <summary>Shared geometry for surfaces whose corner radius follows the display scale.</summary>
internal static class RoundedDrawing
{
    public static GraphicsPath CreatePath(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        radius = Math.Clamp(radius, 0, Math.Min(bounds.Width, bounds.Height) / 2);
        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
