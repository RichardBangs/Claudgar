using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>A keyboard-accessible bag slot with a local mnemonic in place of a game icon.</summary>
internal sealed class InventorySlotButton : Button
{
    internal const int LogicalSize = 60;
    internal const int LogicalMargin = 2;
    internal static int PitchForDpi(int dpi) => (int)Math.Round(LogicalSize * dpi / 96f) + 2 * (int)Math.Round(LogicalMargin * dpi / 96f);
    public JsonObject? Item { get; }
    public string BagName { get; }
    public string BagKey { get; }
    public int? Slot { get; }
    public bool KnownEmpty { get; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Selected { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool MatchesSearch { get; set; } = true;

    public InventorySlotButton(JsonObject? item, string bagName, string bagKey, int? slot, bool knownEmpty)
    {
        Item = item; BagName = bagName; BagKey = bagKey; Slot = slot; KnownEmpty = knownEmpty;
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        BackColor = BrowserTheme.Raised; ForeColor = BrowserTheme.Ink;
        UseVisualStyleBackColor = false; UseMnemonic = false;
        Cursor = item is null ? Cursors.Default : Cursors.Hand;
        TabStop = item is not null;
        AccessibleName = item is null ? $"{bagName}, slot {slot}, {(knownEmpty ? "empty" : "not recorded")}" : InventoryPresentation.Tooltip(item, bagName, slot);
        AccessibleRole = AccessibleRole.PushButton;
        ScaleSlot();
    }

    private void ScaleSlot()
    {
        var side = (int)Math.Round(LogicalSize * DeviceDpi / 96f);
        Size = new Size(side, side);
        Margin = new Padding((int)Math.Round(LogicalMargin * DeviceDpi / 96f));
    }

    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); ScaleSlot(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var scale = DeviceDpi / 96f;
        var padding = Math.Max(3, (int)Math.Round(5 * scale));
        var background = Selected ? Color.FromArgb(60, 65, 62) : Item is null ? BrowserTheme.Background : BrowserTheme.Raised;
        if (!MatchesSearch) background = BrowserTheme.Background;
        e.Graphics.Clear(background);
        var quality = Item is null ? BrowserTheme.Border : InventoryPresentation.QualityColor(InventoryPresentation.Number(Item, "quality"));
        var border = Selected ? BrowserTheme.Accent : MatchesSearch ? quality : BrowserTheme.Border;
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle, border, ButtonBorderStyle.Solid);
        var textColor = MatchesSearch ? BrowserTheme.Ink : BrowserTheme.Border;
        if (Item is null)
        {
            TextRenderer.DrawText(e.Graphics, KnownEmpty ? "·" : "?", Font, ClientRectangle, BrowserTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        else
        {
            using var monogramFont = new Font(Font.FontFamily, Math.Max(12, Font.SizeInPoints + 5), FontStyle.Bold);
            var mnemonic = new Rectangle(padding, padding, Width - 2 * padding, Math.Max(1, Height / 2));
            TextRenderer.DrawText(e.Graphics, InventoryPresentation.Monogram(Item), monogramFont, mnemonic,
                MatchesSearch ? quality : BrowserTheme.Border, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            var name = new Rectangle(padding, Height / 2 - (int)Math.Round(2 * scale), Width - 2 * padding, Math.Max(1, Height / 3));
            TextRenderer.DrawText(e.Graphics, InventoryPresentation.ItemName(Item), Font, name, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            if (InventoryPresentation.Number(Item, "count") is int count && count > 1)
            {
                using var countFont = new Font(Font, FontStyle.Bold);
                var countRect = new Rectangle(padding, Height - Font.Height - padding, Width - 2 * padding, Font.Height + 1);
                TextRenderer.DrawText(e.Graphics, count.ToString("N0"), countFont, countRect, textColor,
                    TextFormatFlags.Right | TextFormatFlags.Bottom | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }
        }
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -padding, -padding), BrowserTheme.Accent, background);
    }
}
