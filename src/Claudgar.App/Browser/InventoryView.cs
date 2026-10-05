using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>A read-only bag layout built entirely from the saved inventory export.</summary>
internal sealed class InventoryView : UserControl, ISectionView
{
    private readonly FlowLayoutPanel bags = new()
    {
        Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight,
        BackColor = BrowserTheme.Background, Padding = new Padding(10), Margin = Padding.Empty
    };
    private readonly TextBox search = new()
    {
        Width = 230, PlaceholderText = "Find an item or item ID", Margin = new Padding(6, 8, 8, 6)
    };
    private readonly Label summary = new()
    {
        AutoSize = true, ForeColor = BrowserTheme.Muted, BackColor = BrowserTheme.Surface,
        Margin = new Padding(8, 8, 6, 6)
    };
    private readonly RichTextBox details = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, BackColor = BrowserTheme.Surface, ForeColor = BrowserTheme.Ink,
        BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10), WordWrap = true
    };
    private readonly ToolTip tooltip = new() { AutoPopDelay = 15000, InitialDelay = 250, ReshowDelay = 100, ShowAlways = true };
    private readonly List<InventorySlotButton> slots = [];
    private InventorySlotButton? selected;
    private string? fingerprint;
    private string inventorySummary = "";

    public InventoryView()
    {
        Dock = DockStyle.Fill; AutoScaleMode = AutoScaleMode.None;
        BackColor = BrowserTheme.Background; ForeColor = BrowserTheme.Ink;
        BrowserTheme.StyleInput(search);
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true,
            BackColor = BrowserTheme.Surface, Padding = new Padding(8, 2, 8, 2), Margin = Padding.Empty
        };
        toolbar.Controls.Add(new Label
        {
            Text = "Carried bags", AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = BrowserTheme.Accent, Margin = new Padding(6, 8, 12, 6)
        });
        toolbar.Controls.Add(search);
        toolbar.Controls.Add(summary);
        var detailsPanel = new Panel { Dock = DockStyle.Fill, BackColor = BrowserTheme.Surface, Padding = new Padding(14) };
        detailsPanel.Controls.Add(details);
        var split = new SplitContainer
        {
            Size = new Size(900, 540), Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
            SplitterDistance = 655, Panel1MinSize = 315, Panel2MinSize = 190, SplitterWidth = 5, FixedPanel = FixedPanel.Panel2,
            BackColor = BrowserTheme.Border, Margin = Padding.Empty
        };
        split.Panel1.Controls.Add(bags); split.Panel2.Controls.Add(detailsPanel);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(toolbar, 0, 0); layout.Controls.Add(split, 0, 1);
        Controls.Add(layout);
        search.TextChanged += (_, _) => FilterItems();
        SetSection(null);
    }

    public void SetSection(JsonObject? section)
    {
        var hash = section?.ToJsonString() ?? "none";
        if (hash == fingerprint) return;
        fingerprint = hash;
        var previousBag = selected?.BagKey; var previousSlot = selected?.Slot;
        selected = null; slots.Clear(); tooltip.RemoveAll();
        bags.SuspendLayout();
        foreach (Control control in bags.Controls.Cast<Control>().ToArray()) control.Dispose();
        bags.Controls.Clear();
        if (section?["data"] is not JsonObject data || data["bags"] is not JsonArray savedBags)
        {
            inventorySummary = "No carried bag data is available in this export.";
            AddMessage("Reload or log out with Claudgar enabled to save your carried bags.");
        }
        else
        {
            var complete = InventoryPresentation.Text(section, "status") == "complete";
            var occupied = 0; var total = 0; var bagIndex = 0;
            foreach (var bag in savedBags.OfType<JsonObject>())
            {
                var panel = new InventoryBagPanel(bag, bagIndex++, complete, tooltip, SelectSlot);
                slots.AddRange(panel.Slots); bags.Controls.Add(panel);
                occupied += panel.Slots.Count(slot => slot.Item is not null);
                total += panel.Slots.Count(slot => slot.Slot is not null);
            }
            inventorySummary = $"{bagIndex} bags · {occupied} stacks · {total} slots";
            if (!complete) inventorySummary += " · ? unrecorded";
            tooltip.SetToolTip(summary, "? marks an unrecorded slot; it may be empty or unread. Item tiles show initials. Select a stack for saved details.");
            if (bagIndex == 0) AddMessage(complete ? "No carried bags were recorded." : "Bag data is incomplete; no bags were recorded.");
        }
        details.Text = "Select an item stack to see its name, quality, count, stats, and saved item link.";
        bags.ResumeLayout(true);
        FilterItems();
        if (previousBag is not null && previousSlot is int position && slots.FirstOrDefault(slot => slot.BagKey == previousBag && slot.Slot == position && slot.Item is not null) is { } restored)
            SelectSlot(restored);
    }

    private void AddMessage(string text) => bags.Controls.Add(new Label
    {
        Text = text, AutoSize = true, MaximumSize = new Size(520, 0), ForeColor = BrowserTheme.Muted,
        Padding = new Padding(12), Margin = Padding.Empty
    });

    private void FilterItems()
    {
        var filter = search.Text.Trim(); var matches = 0;
        foreach (var slot in slots)
        {
            slot.MatchesSearch = filter.Length == 0 || slot.Item is not null &&
                (InventoryPresentation.ItemName(slot.Item).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                 InventoryPresentation.Number(slot.Item, "itemId")?.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase) == true);
            if (slot.Item is not null && slot.MatchesSearch) matches++;
            slot.Invalidate();
        }
        summary.Text = inventorySummary + (filter.Length > 0 ? $" · {matches} matches" : "");
    }

    private void SelectSlot(InventorySlotButton slot)
    {
        if (selected is not null) { selected.Selected = false; selected.Invalidate(); }
        selected = slot; selected.Selected = true; selected.Invalidate();
        details.Text = slot.Item is not null ? InventoryPresentation.Details(slot.Item, slot.BagName, slot.Slot)
            : $"{slot.BagName} · Slot {slot.Slot}\n\n{(slot.KnownEmpty ? "This slot was empty in the saved export." : "This slot was not recorded. It may be empty, or its item data may have been unreadable.")}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) tooltip.Dispose();
        base.Dispose(disposing);
    }
}
