using System.Drawing.Drawing2D;

namespace Claudgar.App.Browser;

/// <summary>Displays the shared portrait crisply at the current display scale.</summary>
internal sealed class PixelPortrait(Image portrait) : Control
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.DrawImage(portrait, ClientRectangle);
    }
}
