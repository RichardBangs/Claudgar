using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>Shows the saved paper doll directly in the Equipment tab.</summary>
internal sealed class EquipmentView : UserControl, ISectionView
{
    private readonly EquipmentCanvas canvas = new();

    public EquipmentView()
    {
        Dock = DockStyle.Fill; AutoScaleMode = AutoScaleMode.None;
        BackColor = BrowserTheme.Background;
        Controls.Add(canvas);
    }

    public void SetSection(JsonObject? section) => canvas.SetSheet(EquipmentPresentation.Create(section));
    public void SetCharacter(JsonObject? section) => canvas.SetCharacter(section);
}
