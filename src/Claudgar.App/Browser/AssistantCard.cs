using System.ComponentModel;

namespace Claudgar.App.Browser;

/// <summary>A quiet launch card with an assistant mark, name, and one primary action.</summary>
internal sealed class AssistantCard : UserControl
{
    private readonly RoundedPanel surface;
    private readonly AssistantMark mark;
    private readonly Label name;
    private readonly ControlScaleState scaleState = new();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Button OpenButton { get; }

    public AssistantCard(AssistantKind kind, EventHandler clicked)
    {
        AutoScaleMode = AutoScaleMode.None;
        BackColor = BrowserTheme.Background;
        var assistantName = kind == AssistantKind.ChatGpt ? "ChatGPT" : "Claude";
        surface = new RoundedPanel { Dock = DockStyle.Fill, BackColor = BrowserTheme.Surface, CornerRadius = 18 };
        mark = new AssistantMark(kind);
        name = new Label
        {
            Text = assistantName, ForeColor = BrowserTheme.Ink, BackColor = BrowserTheme.Surface,
            Font = new Font("Segoe UI", 20, FontStyle.Regular), TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false, UseMnemonic = false
        };
        OpenButton = new MinimalButton("Open " + assistantName, clicked, primary: true)
        {
            AutoSize = false, Font = new Font("Segoe UI", 10.5f), TabIndex = 0,
            AccessibleName = "Open " + assistantName,
            AccessibleDescription = "Open the installed desktop assistant, or its website if unavailable."
        };
        surface.Controls.Add(mark);
        surface.Controls.Add(name);
        surface.Controls.Add(OpenButton);
        Controls.Add(surface);
        AccessibleName = assistantName;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (surface is null) return;
        var scale = scaleState.Factor(DeviceDpi);
        int D(float value) => (int)Math.Round(value * scale);
        var compact = Height < D(230);
        var iconSize = D(compact ? 49 : 62);
        var iconTop = D(compact ? 20 : 32);
        mark.Bounds = new Rectangle((Width - iconSize) / 2, iconTop, iconSize, iconSize);
        name.Bounds = new Rectangle(D(18), mark.Bottom + D(compact ? 9 : 16), Width - D(36), D(38));
        var buttonWidth = Math.Min(D(258), Math.Max(D(100), Width - D(48)));
        OpenButton.Bounds = new Rectangle((Width - buttonWidth) / 2, Height - D(compact ? 66 : 72), buttonWidth, D(48));
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        scaleState.DpiChanged(DeviceDpi);
        PerformLayout();
    }

    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        scaleState.Apply(factor, specified, DeviceDpi);
        base.ScaleControl(factor, specified);
        PerformLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) name.Font.Dispose();
        base.Dispose(disposing);
    }
}
