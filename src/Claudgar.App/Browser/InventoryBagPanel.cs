using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>Keeps each exported bag's slot positions, including uncertain and empty slots.</summary>
internal sealed class InventoryBagPanel : TableLayoutPanel
{
    private const int Columns = 4;
    public IReadOnlyList<InventorySlotButton> Slots => slots;
    private readonly List<InventorySlotButton> slots = [];
    private readonly FlowLayoutPanel grid;

    public InventoryBagPanel(JsonObject bag, int index, bool complete, ToolTip tooltip, Action<InventorySlotButton> select)
    {
        var bagName = InventoryPresentation.BagName(bag, index);
        var bagKey = InventoryPresentation.Number(bag, "bagId") is int bagId ? $"bag:{bagId}" : $"index:{index}";
        var declaredCount = InventoryPresentation.Number(bag, "slotCount");
        var items = (bag["items"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        var positions = new Dictionary<int, JsonObject>();
        var unpositioned = new List<JsonObject>();
        var count = declaredCount is >= 0 and <= 1000 ? declaredCount.Value : 0;
        foreach (var item in items)
        {
            var position = InventoryPresentation.Number(item, "slot");
            if (position is > 0 and <= 1000 && (declaredCount is null || position <= count) && positions.TryAdd(position.Value, item))
            {
                if (declaredCount is null) count = Math.Max(count, position.Value);
            }
            else unpositioned.Add(item);
        }
        BackColor = BrowserTheme.Surface; ForeColor = BrowserTheme.Ink;
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = new Padding(0, 0, 12, 12); Padding = new Padding(8);
        ColumnCount = 1; RowCount = 3;
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        RowStyles.Add(new RowStyle(SizeType.AutoSize)); RowStyles.Add(new RowStyle(SizeType.AutoSize)); RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(new Label { Text = bagName, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = BrowserTheme.Accent, AutoSize = true, Margin = new Padding(2, 0, 2, 3) }, 0, 0);
        var metadata = declaredCount is >= 0 and <= 1000 ? $"{declaredCount} slots" : "Slot count unavailable";
        if (InventoryPresentation.Number(bag, "freeSlots") is int free) metadata += $" · {free} reported free";
        Controls.Add(new Label { Text = metadata, ForeColor = BrowserTheme.Muted, AutoSize = true, Margin = new Padding(2, 0, 2, 6) }, 0, 1);
        grid = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight, Margin = Padding.Empty, Padding = Padding.Empty,
            BackColor = BrowserTheme.Surface
        };
        Controls.Add(grid, 0, 2);
        for (var slot = 1; slot <= count; slot++)
        {
            var item = positions.GetValueOrDefault(slot);
            AddSlot(item, slot, complete && declaredCount is not null && bag["items"] is JsonArray);
        }
        if (count == 0)
        {
            grid.Controls.Add(new Label
            {
                Text = declaredCount == 0 && complete ? "No bag slots" : "No slots recorded",
                AutoSize = true, ForeColor = BrowserTheme.Muted, Margin = new Padding(3, 5, 3, 8)
            });
        }
        if (unpositioned.Count > 0)
        {
            var label = new Label { Text = "Items with unrecorded slot positions", AutoSize = true, ForeColor = BrowserTheme.Warning, Margin = new Padding(3, 12, 3, 8) };
            grid.SetFlowBreak(grid.Controls[grid.Controls.Count - 1], true);
            grid.Controls.Add(label); grid.SetFlowBreak(label, true);
            foreach (var item in unpositioned) AddSlot(item, null, false);
        }
        ScaleGrid();

        void AddSlot(JsonObject? item, int? slot, bool knownEmpty)
        {
            var button = new InventorySlotButton(item, bagName, bagKey, slot, knownEmpty) { Font = new Font("Segoe UI", 8) };
            button.Click += (_, _) => select(button);
            tooltip.SetToolTip(button, item is null ? $"{bagName} · Slot {slot}\n{(knownEmpty ? "Empty in the saved export" : "Not recorded; may be empty or unread")}" : InventoryPresentation.Tooltip(item, bagName, slot));
            slots.Add(button); grid.Controls.Add(button);
        }
    }

    private void ScaleGrid()
    {
        var width = InventorySlotButton.PitchForDpi(DeviceDpi) * Columns;
        grid.MinimumSize = new Size(width, 0); grid.MaximumSize = new Size(width, 0); grid.Width = width;
        Controls[0].MaximumSize = new Size(width, 0); Controls[1].MaximumSize = new Size(width, 0);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); ScaleGrid(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle, BrowserTheme.Border, ButtonBorderStyle.Solid);
    }
}
