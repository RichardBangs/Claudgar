using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>Scenic character context, with saved details available through a single quiet action.</summary>
internal sealed class CharacterBanner : Control
{
    private readonly Image artwork;
    private readonly MinimalButton detailsButton;
    private readonly Font detailsFont = new("Segoe UI", 10);
    private readonly ControlScaleState scaleState = new();
    private string name = "Your next adventure awaits";
    private string detail = "Select a saved character to get started.";

    public CharacterBanner()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Artwork/stormwind-dusk.png")
            ?? throw new InvalidOperationException("The character banner artwork is missing.");
        using var source = Image.FromStream(stream);
        artwork = new Bitmap(source);
        Height = 140;
        BackColor = BrowserTheme.Background;
        detailsButton = new MinimalButton("View character data", (_, _) => DetailsRequested?.Invoke(this, EventArgs.Empty))
        {
            AutoSize = false,
            Enabled = false,
            MinimumSize = Size.Empty,
            AccessibleDescription = "Open saved character, quest, talent, inventory, and equipment details.",
            Font = detailsFont
        };
        Controls.Add(detailsButton);
    }

    public event EventHandler? DetailsRequested;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Button DetailsButton => detailsButton;

    public void SetCharacter(JsonObject? snapshot)
    {
        var identity = snapshot?["sections"]?["character"]?["data"] as JsonObject ?? snapshot?["character"] as JsonObject;
        name = identity?["name"]?.ToString() ?? "Your next adventure awaits";
        if (identity is null) detail = "Select a saved character to get started.";
        else
        {
            var parts = new List<string>();
            if (identity["level"] is { } level) parts.Add($"Level {level}");
            var raceAndClass = string.Join(" ", new[] { identity["raceName"]?.ToString(), identity["className"]?.ToString() }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (raceAndClass.Length > 0) parts.Add(raceAndClass);
            if (identity["realm"] is { } realm && !string.IsNullOrWhiteSpace(realm.ToString())) parts.Add(realm.ToString());
            detail = string.Join(" · ", parts);
        }
        detailsButton.Enabled = identity is not null;
        AccessibleName = name;
        AccessibleDescription = detail;
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (detailsButton is null) return;
        var scale = scaleState.Factor(DeviceDpi);
        int D(float value) => (int)Math.Round(value * scale);
        var stacked = Width < D(600);
        var buttonWidth = Math.Min(D(196), Math.Max(D(150), Width - D(48)));
        detailsButton.Bounds = new Rectangle(stacked ? D(24) : Math.Max(D(24), Width - buttonWidth - D(24)),
            stacked ? Math.Max(D(60), Height - D(52)) : (Height - D(44)) / 2, buttonWidth, D(44));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width <= 1 || Height <= 1) return;
        var g = e.Graphics;
        var scale = scaleState.Factor(DeviceDpi);
        int D(float value) => (int)Math.Round(value * scale);
        var compact = Height < D(125);
        var stacked = Width < D(600);
        var bounds = new Rectangle(0, 0, Width, Height);
        using var shape = RoundedDrawing.CreatePath(new RectangleF(0, 0, Width - 1, Height - 1), D(14));
        var state = g.Save();
        g.SetClip(shape);
        var ratio = Math.Max((float)Width / artwork.Width, (float)Height / artwork.Height);
        var drawnWidth = artwork.Width * ratio;
        var drawnHeight = artwork.Height * ratio;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(artwork, new RectangleF((Width - drawnWidth) / 2, (Height - drawnHeight) * .35f, drawnWidth, drawnHeight));
        using var shade = new LinearGradientBrush(bounds, Color.FromArgb(223, 13, 21, 29),
            Color.FromArgb(66, 13, 21, 29), LinearGradientMode.Horizontal);
        g.FillRectangle(shade, bounds);
        using var title = new Font("Cambria", stacked ? 21 : compact ? 25 : 28, FontStyle.Bold);
        using var subtitle = new Font("Segoe UI", stacked ? 9.5f : 10.5f);
        var textWidth = stacked ? Width - D(48) : Math.Max(0, detailsButton.Left - D(48));
        var titleTop = stacked ? D(9) : (Height - D(compact ? 62 : 72)) / 2;
        Draw(g, name, title, new Rectangle(D(24), titleTop, textWidth, D(stacked ? 32 : compact ? 40 : 46)), BrowserTheme.Ink);
        Draw(g, detail, subtitle, new Rectangle(D(25), titleTop + D(stacked ? 32 : compact ? 42 : 48), textWidth, D(24)), BrowserTheme.Ink);
        g.Restore(state);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        scaleState.DpiChanged(DeviceDpi);
        PerformLayout();
        Invalidate();
    }

    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        scaleState.Apply(factor, specified, DeviceDpi);
        base.ScaleControl(factor, specified);
        PerformLayout();
        Invalidate();
    }

    private static void Draw(Graphics graphics, string text, Font font, Rectangle bounds, Color color) =>
        TextRenderer.DrawText(graphics, text, font, bounds, color,
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            artwork.Dispose();
            detailsFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
