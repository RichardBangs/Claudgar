using System.Drawing.Drawing2D;
using System.Text.Json.Nodes;
using static Claudgar.App.Browser.EquipmentPresentation;

namespace Claudgar.App.Browser;

/// <summary>Renders and fits the read-only equipment sheet; every hit uses the same transform.</summary>
internal sealed class EquipmentCanvas : Control
{
    private sealed record PlacedSlot(Slot Slot, RectangleF Bounds);
    private sealed record PlacedExtra(UnplacedItem Item, RectangleF Bounds);
    private readonly ToolTip tooltip = new() { InitialDelay = 250, ReshowDelay = 100, AutoPopDelay = 20000 };
    private Sheet sheet = Create(null);
    private IReadOnlyList<PlacedSlot> placements = [];
    private IReadOnlyList<PlacedExtra> extras = [];
    private JsonObject? character;
    private Slot? selected;
    private object? hovered;
    private float fittedScale = 1;
    private SizeF sheetSize = new(1100, 570);
    private const float IconSize = 52;

    public EquipmentCanvas()
    {
        Dock = DockStyle.Fill; BackColor = BrowserTheme.Background; ForeColor = BrowserTheme.Ink; TabStop = true;
        AccessibleName = "Saved equipment";
        AccessibleDescription = "Equipment arranged around a paper doll. Hover or select slots for saved item details. Arrow keys move between slots.";
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        SetSheet(sheet);
    }

    public void SetSheet(Sheet value)
    {
        sheet = value; selected = null; hovered = null; tooltip.SetToolTip(this, null);
        placements = sheet.Slots.Select(slot => new PlacedSlot(slot, SlotBounds(slot.Definition))).ToArray();
        extras = sheet.Unplaced.Select((item, index) => new PlacedExtra(item,
            new RectangleF(22 + index % 16 * 66, 610 + index / 16 * 76, IconSize, IconSize))).ToArray();
        sheetSize = new SizeF(1100, extras.Count == 0 ? 570 : 618 + (float)Math.Ceiling(extras.Count / 16f) * 76);
        UpdateFit(); Invalidate();
    }

    public void SetCharacter(JsonObject? section) { character = section?["data"] as JsonObject; Invalidate(); }
    private static RectangleF SlotBounds(SlotDefinition slot) => slot.Region switch
    {
        SlotRegion.Left => new(20, 18 + slot.Position * 60, IconSize, IconSize),
        SlotRegion.Right => new(606, 18 + slot.Position * 60, IconSize, IconSize),
        SlotRegion.Weapon => new(277 + slot.Position * 92, 502, IconSize, IconSize),
        _ => new(730, 502, IconSize, IconSize)
    };
    private PointF Origin => new((ClientSize.Width - sheetSize.Width * fittedScale) / 2, (ClientSize.Height - sheetSize.Height * fittedScale) / 2);
    private void UpdateFit()
    {
        fittedScale = ClientSize.Width > 24 && ClientSize.Height > 24
            ? Math.Min((ClientSize.Width - 16) / sheetSize.Width, (ClientSize.Height - 16) / sheetSize.Height) : 1;
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); UpdateFit(); Invalidate(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); UpdateFit(); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        var origin = Origin; g.TranslateTransform(origin.X, origin.Y); g.ScaleTransform(fittedScale, fittedScale);
        using var background = new LinearGradientBrush(new RectangleF(0, 0, sheetSize.Width, sheetSize.Height),
            Color.FromArgb(32, 35, 38), Color.FromArgb(16, 22, 28), LinearGradientMode.Vertical);
        g.FillRectangle(background, new RectangleF(0, 0, sheetSize.Width, sheetSize.Height));
        using var border = new Pen(Color.FromArgb(83, 75, 60));
        g.DrawRectangle(border, 0, 0, sheetSize.Width, sheetSize.Height);
        EquipmentPaperDoll.Draw(g, new RectangleF(256, 20, 330, 466));
        EquipmentStatDisplay.Draw(g, new RectangleF(850, 18, 230, 536), character, sheet);
        foreach (var placed in placements) DrawSlot(g, placed);
        if (extras.Count > 0)
        {
            DrawText(g, "Other saved equipment", new RectangleF(22, 580, 1050, 26), BrowserTheme.Warning, 16, true);
            foreach (var extra in extras)
            {
                DrawItem(g, extra.Item.Item, extra.Bounds, false);
                DrawText(g, "?", new RectangleF(extra.Bounds.Right - 12, extra.Bounds.Bottom - 17, 14, 18), BrowserTheme.Warning, 13, true);
            }
        }
    }

    private void DrawSlot(Graphics g, PlacedSlot placed)
    {
        var slot = placed.Slot; var frame = placed.Bounds; var hasItem = slot.State == SlotState.Equipped && slot.Item is not null;
        if (hasItem) DrawItem(g, slot.Item!, frame, ReferenceEquals(slot, selected));
        else
        {
            using var surface = new SolidBrush(Color.FromArgb(25, 28, 29)); g.FillRectangle(surface, frame);
            DrawText(g, slot.Definition.Label[..Math.Min(2, slot.Definition.Label.Length)], frame, BrowserTheme.Muted, 16, true, true);
            using var edge = new Pen(ReferenceEquals(slot, selected) ? BrowserTheme.Accent : BrowserTheme.Border, ReferenceEquals(slot, selected) ? 2 : 1);
            g.DrawRectangle(edge, frame.X, frame.Y, frame.Width, frame.Height);
            if (slot.State is SlotState.Unknown or SlotState.Conflict)
                DrawText(g, slot.State == SlotState.Conflict ? "!" : "?", new RectangleF(frame.Right - 16, frame.Bottom - 20, 16, 20), BrowserTheme.Warning, 17, true, true);
        }
        if (slot.Definition.Region is SlotRegion.Left or SlotRegion.Right)
        {
            var label = new RectangleF(frame.Right + 12, frame.Top + 3, slot.Definition.Region == SlotRegion.Left ? 166 : 172, 18);
            DrawText(g, slot.Definition.Label, label, BrowserTheme.Muted, 12);
            label.Y += 19; label.Height = 30;
            var text = hasItem ? InventoryPresentation.ItemName(slot.Item!) : slot.State switch
            { SlotState.Empty => "Empty", SlotState.Conflict => "Conflicting records", _ => "Not recorded" };
            DrawText(g, text, label, hasItem ? InventoryPresentation.QualityColor(Quality(slot.Item!)) : BrowserTheme.Muted, 14, hasItem);
        }
        else DrawText(g, slot.Definition.Label, new RectangleF(frame.X - 18, frame.Bottom + 3, frame.Width + 36, 16), BrowserTheme.Muted, 11, false, true);
    }

    private void DrawItem(Graphics g, JsonObject item, RectangleF frame, bool isSelected)
    {
        var color = InventoryPresentation.QualityColor(Quality(item));
        using var surface = new SolidBrush(BrowserTheme.Raised); g.FillRectangle(surface, frame);
        DrawText(g, InventoryPresentation.Monogram(item), frame, color, 15, true, true);
        using var edge = new Pen(color, 2); g.DrawRectangle(edge, frame.X, frame.Y, frame.Width, frame.Height);
        if (isSelected)
        {
            using var highlight = new Pen(BrowserTheme.Accent, 2);
            g.DrawRectangle(highlight, frame.X - 4, frame.Y - 4, frame.Width + 8, frame.Height + 8);
        }
        if (InventoryPresentation.Number(item, "maxDurability") is > 0 and var max && InventoryPresentation.Number(item, "durability") is int current)
        {
            var fraction = Math.Clamp((float)current / max, 0, 1);
            using var track = new SolidBrush(Color.FromArgb(12, 17, 21));
            using var health = new SolidBrush(fraction <= 0.2f ? Color.IndianRed : fraction <= 0.5f ? BrowserTheme.Warning : BrowserTheme.Success);
            g.FillRectangle(track, frame.X + 2, frame.Bottom - 5, frame.Width - 4, 3);
            g.FillRectangle(health, frame.X + 2, frame.Bottom - 5, (frame.Width - 4) * fraction, 3);
        }
        if (InventoryPresentation.Number(item, "count") is > 1 and var count)
            DrawText(g, count.ToString(), new RectangleF(frame.X + 2, frame.Bottom - 21, frame.Width - 4, 17), Color.White, 12, true);
    }

    internal static void DrawText(Graphics g, string text, RectangleF bounds, Color color, float size, bool bold = false, bool centered = false)
    {
        using var font = new Font(bold ? "Cambria" : "Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = centered ? StringAlignment.Center : StringAlignment.Near,
            LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        g.DrawString(text, font, brush, bounds, format);
    }

    private PointF ToSheet(Point location) => new((location.X - Origin.X) / fittedScale, (location.Y - Origin.Y) / fittedScale);
    private object? Hit(Point location)
    {
        var point = ToSheet(location);
        return (object?)placements.FirstOrDefault(slot => slot.Bounds.Contains(point)) ?? extras.FirstOrDefault(extra => extra.Bounds.Contains(point));
    }
    private static string Describe(Slot slot) => slot.State == SlotState.Equipped && slot.Item is not null
        ? Details(slot.Item, slot.Definition.Label) : slot.Definition.Label + Environment.NewLine + (slot.State switch
        {
            SlotState.Empty => "Empty in the saved export.", SlotState.Conflict => "Multiple saved records claim this slot. They are shown below the sheet.",
            _ => "Equipment for this slot was not recorded."
        });
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); var hit = Hit(e.Location);
        if (ReferenceEquals(hit, hovered)) return;
        hovered = hit; Cursor = hit is null ? Cursors.Default : Cursors.Hand;
        tooltip.SetToolTip(this, hit switch { PlacedSlot slot => Describe(slot.Slot), PlacedExtra extra => Details(extra.Item.Item, extra.Item.Reason), _ => null });
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = null; tooltip.SetToolTip(this, null); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return; Focus();
        var hit = Hit(e.Location); selected = (hit as PlacedSlot)?.Slot; Invalidate();
        var details = hit switch { PlacedSlot slot => Describe(slot.Slot), PlacedExtra extra => Details(extra.Item.Item, extra.Item.Reason), _ => null };
        if (details is not null) tooltip.Show(details, this, Math.Min(e.X + 20, Math.Max(0, Width - 300)), Math.Max(0, e.Y - 20), 20000);
    }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) { selected = null; tooltip.Hide(this); Invalidate(); e.Handled = true; return; }
        var index = placements.ToList().FindIndex(slot => ReferenceEquals(slot.Slot, selected));
        if (e.KeyCode is Keys.Right or Keys.Down) index = (index + 1) % placements.Count;
        else if (e.KeyCode is Keys.Left or Keys.Up) index = index <= 0 ? placements.Count - 1 : index - 1;
        else if (e.KeyCode == Keys.Home) index = 0;
        else if (e.KeyCode == Keys.End) index = placements.Count - 1;
        else if (e.KeyCode is not (Keys.Enter or Keys.Space)) return;
        if (index < 0) index = 0;
        selected = placements[index].Slot; Invalidate(); e.Handled = true;
        var bounds = placements[index].Bounds;
        tooltip.Show(Describe(selected), this, (int)(Origin.X + (bounds.Right + 8) * fittedScale), (int)(Origin.Y + bounds.Top * fittedScale), 20000);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) tooltip.Dispose();
        base.Dispose(disposing);
    }
}
