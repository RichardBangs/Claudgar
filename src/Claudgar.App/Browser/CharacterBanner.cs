using System.Drawing.Drawing2D;
using System.Reflection;
using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>Embedded artwork with a summary drawn from the selected saved export.</summary>
internal sealed class CharacterBanner : Control
{
    private readonly Image artwork;
    private string name = "Your next adventure awaits";
    private string detail = "Select a saved character to explore your journey.";
    private (string Label, string Value)[] facts = [("REALM", "No character selected"), ("ALLEGIANCE", "—"), ("LOCATION", "—")];

    public CharacterBanner()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Artwork/stormwind-dusk.png")
            ?? throw new InvalidOperationException("The character banner artwork is missing.");
        using var source = Image.FromStream(stream);
        artwork = new Bitmap(source);
        Height = 246;
        BackColor = BrowserTheme.Surface;
    }

    public void SetCharacter(JsonObject? snapshot)
    {
        var identity = snapshot?["sections"]?["character"]?["data"] as JsonObject ?? snapshot?["character"] as JsonObject;
        name = identity?["name"]?.ToString() ?? "Your next adventure awaits";
        var level = identity?["level"]?.ToString() ?? "—";
        detail = identity is null ? "Select a saved character to explore your journey." :
            $"Level {level} · {identity["raceName"]} {identity["className"]}";
        var zone = identity?["zone"]?.ToString();
        var subZone = identity?["subZone"]?.ToString();
        facts = [
            ("REALM", identity?["realm"]?.ToString() ?? "No character selected"),
            ("ALLEGIANCE", identity?["faction"]?.ToString() ?? "Not recorded"),
            ("LOCATION", string.IsNullOrEmpty(zone) ? "Not recorded" : zone + (string.IsNullOrEmpty(subZone) ? "" : " · " + subZone))
        ];
        AccessibleName = name;
        AccessibleDescription = detail + ". " + string.Join(". ", facts.Select(f => f.Label + ": " + f.Value));
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var scale = DeviceDpi / 96f;
        int D(float value) => (int)Math.Round(value * scale);
        var compact = Height < D(230);
        var factsHeight = D(compact ? 60 : 72);
        var hero = new Rectangle(0, 0, Width, Math.Max(1, Height - factsHeight));
        var ratio = Math.Max((float)hero.Width / artwork.Width, (float)hero.Height / artwork.Height);
        var drawnWidth = artwork.Width * ratio;
        var drawnHeight = artwork.Height * ratio;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(artwork, new RectangleF((Width - drawnWidth) / 2, (hero.Height - drawnHeight) * .35f, drawnWidth, drawnHeight));
        using var shade = new LinearGradientBrush(hero, Color.FromArgb(125, 11, 18, 25), Color.FromArgb(12, 11, 18, 25), LinearGradientMode.Horizontal);
        g.FillRectangle(shade, hero);
        using var surface = new SolidBrush(BrowserTheme.Surface);
        g.FillRectangle(surface, 0, hero.Bottom, Width, factsHeight);
        using var rule = new Pen(BrowserTheme.Border);
        using var gold = new Pen(BrowserTheme.Accent);
        g.DrawLine(gold, 0, 0, Width, 0);
        g.DrawLine(rule, 0, hero.Bottom, Width, hero.Bottom);
        using var eyebrow = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var title = new Font("Cambria", compact ? 26 : 30, FontStyle.Bold);
        using var subtitle = new Font("Segoe UI", 11);
        using var valueFont = new Font("Segoe UI", 10);
        Draw(g, "CHARACTER CHRONICLE", eyebrow, new Rectangle(D(24), D(compact ? 12 : 22), Width - D(48), D(22)), BrowserTheme.Accent);
        Draw(g, name, title, new Rectangle(D(22), D(compact ? 33 : 47), Width - D(125), D(compact ? 47 : 59)), BrowserTheme.Ink);
        Draw(g, detail, subtitle, new Rectangle(D(24), D(compact ? 82 : 115), Width - D(48), D(28)), BrowserTheme.Ink);
        var widths = new[] { .34f, .22f, .44f };
        var left = 0;
        for (var i = 0; i < facts.Length; i++)
        {
            var width = (int)(Width * widths[i]);
            if (i > 0) g.DrawLine(rule, left, hero.Bottom + D(17), left, Height - D(17));
            Draw(g, facts[i].Label, eyebrow, new Rectangle(left + D(24), hero.Bottom + D(compact ? 8 : 13), width - D(36), D(18)), BrowserTheme.Muted);
            Draw(g, facts[i].Value, valueFont, new Rectangle(left + D(24), hero.Bottom + D(compact ? 28 : 35), width - D(36), D(26)), BrowserTheme.Ink);
            left += width;
        }
    }

    private static void Draw(Graphics graphics, string text, Font font, Rectangle bounds, Color color) =>
        TextRenderer.DrawText(graphics, text, font, bounds, color, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

    protected override void Dispose(bool disposing)
    {
        if (disposing) artwork.Dispose();
        base.Dispose(disposing);
    }
}
