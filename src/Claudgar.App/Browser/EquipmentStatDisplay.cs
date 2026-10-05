using System.Text.Json.Nodes;
using static Claudgar.App.Browser.EquipmentPresentation;

namespace Claudgar.App.Browser;

/// <summary>Displays exported character totals, or explicitly labelled recorded item bonuses.</summary>
internal static class EquipmentStatDisplay
{
    public static void Draw(Graphics g, RectangleF bounds, JsonObject? character, Sheet sheet)
    {
        var stats = character?["stats"] as JsonObject;
        if (stats is not { Count: > 0 }) { DrawItemBonuses(g, bounds, sheet); return; }
        var powerType = Stat(stats, "powerType");
        var powerLabel = powerType switch { 0 => "Mana", 1 => "Rage", 2 => "Focus", 3 => "Energy", 6 => "Runic power", _ => "Power" };
        var resource = powerType == 0 ? Stat(stats, "maxMana") ?? Stat(stats, "maxPower") : Stat(stats, "maxPower");
        var y = bounds.Y;
        Panel("General", [("Health", Format(Stat(stats, "maxHealth"))), (powerLabel, Format(resource)), ("Armor", Format(Stat(stats, "armor")))]);
        Panel("Attributes", [("Strength", Value("strength")), ("Agility", Value("agility")), ("Stamina", Value("stamina")), ("Intellect", Value("intellect")), ("Spirit", Value("spirit"))]);
        Panel("Melee", [("Damage", Range("meleeDamageMin", "meleeDamageMax")), ("Attack speed", Value("meleeAttackSpeed")), ("Attack power", Value("meleeAttackPower")), ("Crit chance", Format(Stat(stats, "meleeCritChance"), true))]);
        Panel("Ranged", [("Damage", Range("rangedDamageMin", "rangedDamageMax")), ("Attack speed", Value("rangedAttackSpeed")), ("Attack power", Value("rangedAttackPower")), ("Crit chance", Format(Stat(stats, "rangedCritChance"), true))]);
        Panel("Spell", [("Spell power", Value("spellPower")), ("Healing", Value("spellHealing")), ("Crit chance", Format(Stat(stats, "spellCritChance"), true))]);
        return;

        string Value(string key) => Format(Stat(stats, key));
        string Range(string min, string max) => Stat(stats, min) is double minimum && Stat(stats, max) is double maximum
            ? Format(Math.Max(1, Math.Floor(minimum))) + " – " + Format(Math.Max(1, Math.Ceiling(maximum))) : "—";
        void Panel(string title, (string Label, string Value)[] rows)
        {
            var height = 30 + rows.Length * 19;
            DrawPanel(g, new RectangleF(bounds.X, y, bounds.Width, height), title, rows);
            y += height + 5;
        }
    }

    private static void DrawItemBonuses(Graphics g, RectangleF bounds, Sheet sheet)
    {
        var bonuses = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var item in sheet.Slots.Where(slot => slot.State == SlotState.Equipped).Select(slot => slot.Item))
            if (item?["stats"] is JsonObject stats)
                foreach (var (key, _) in stats)
                    if (Stat(stats, key) is double value) bonuses[key] = bonuses.GetValueOrDefault(key) + value;
        EquipmentCanvas.DrawText(g, "Recorded item bonuses", new RectangleF(bounds.X + 10, bounds.Y + 7, bounds.Width - 20, 48), BrowserTheme.Accent, 19, true);
        var rows = bonuses.OrderBy(pair => StatLabel(pair.Key), StringComparer.Ordinal).ToArray();
        var y = bounds.Y + 66;
        foreach (var row in rows.Take(12))
        {
            EquipmentCanvas.DrawText(g, StatLabel(row.Key), new RectangleF(bounds.X + 10, y, bounds.Width - 72, 25), BrowserTheme.Ink, 14);
            EquipmentCanvas.DrawText(g, Format(row.Value), new RectangleF(bounds.Right - 62, y, 52, 25), BrowserTheme.Ink, 14);
            y += 30;
        }
        if (rows.Length == 0 || rows.Length > 12)
            EquipmentCanvas.DrawText(g, rows.Length == 0 ? "No item bonuses were saved." : $"{rows.Length - 12} more bonuses in item details",
                new RectangleF(bounds.X + 10, y + 4, bounds.Width - 20, 28), BrowserTheme.Muted, 13);
        EquipmentCanvas.DrawText(g, "Character stats will appear after an export with the updated addon.",
            new RectangleF(bounds.X + 10, bounds.Bottom - 76, bounds.Width - 20, 72), BrowserTheme.Muted, 13);
    }

    private static void DrawPanel(Graphics g, RectangleF bounds, string title, (string Label, string Value)[] rows)
    {
        using var surface = new SolidBrush(Color.FromArgb(17, 23, 27)); using var border = new Pen(Color.FromArgb(84, 79, 65));
        g.FillRectangle(surface, bounds); g.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        EquipmentCanvas.DrawText(g, title, new RectangleF(bounds.X + 10, bounds.Y + 3, bounds.Width - 20, 25), BrowserTheme.Accent, 18, true);
        for (var index = 0; index < rows.Length; index++)
        {
            var y = bounds.Y + 29 + index * 19;
            EquipmentCanvas.DrawText(g, rows[index].Label, new RectangleF(bounds.X + 10, y, bounds.Width - 90, 19), BrowserTheme.Accent, 14);
            EquipmentCanvas.DrawText(g, rows[index].Value, new RectangleF(bounds.Right - 85, y, 75, 19), BrowserTheme.Ink, 14);
        }
    }
}
