using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>The main character view: choose an assistant, copy a question, or open the saved data.</summary>
internal sealed class AssistantHome : UserControl
{
    private readonly IAssistantLauncher launcher;
    private readonly Action<string> copyQuestion;
    private readonly AssistantCard chatGpt;
    private readonly AssistantCard claude;
    private readonly Label subtitle;
    private readonly LinkLabel details;
    private readonly List<Button> suggestions = [];
    private readonly System.Windows.Forms.Timer confirmationTimer = new() { Interval = 4500 };
    private readonly ToolTip statusHelp = new() { AutoPopDelay = 12000 };
    private readonly ControlScaleState scaleState = new();
    private readonly Font normalHeadingFont = new("Segoe UI", 26, FontStyle.Regular);
    private readonly Font compactHeadingFont = new("Segoe UI", 22, FontStyle.Regular);
    private JsonObject? snapshot;
    private bool statusWarning;
    private bool hasCharacter;

    public event EventHandler? DetailsRequested;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Button ChatGptButton => chatGpt.OpenButton;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Button ClaudeButton => claude.OpenButton;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<Button> SuggestionButtons => suggestions;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Label HeadingLabel { get; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Label StatusLabel { get; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Label FeedbackLabel { get; }

    public AssistantHome() : this(new WindowsAssistantLauncher()) { }

    internal AssistantHome(IAssistantLauncher launcher, Action<string>? copyQuestion = null)
    {
        this.launcher = launcher;
        this.copyQuestion = copyQuestion ?? Clipboard.SetText;
        AutoScaleMode = AutoScaleMode.None;
        AutoScroll = true;
        BackColor = BrowserTheme.Background;
        DoubleBuffered = true;
        HeadingLabel = new Label
        {
            Text = "Ask about your character", Font = normalHeadingFont,
            ForeColor = BrowserTheme.Ink, AutoSize = false, UseMnemonic = false,
            TextAlign = ContentAlignment.MiddleCenter
        };
        subtitle = new Label
        {
            Text = "Choose your assistant.", ForeColor = BrowserTheme.Muted,
            Font = new Font("Segoe UI", 11), AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter
        };
        chatGpt = new AssistantCard(AssistantKind.ChatGpt, (_, _) => OpenAssistant(AssistantKind.ChatGpt)) { TabIndex = 0 };
        claude = new AssistantCard(AssistantKind.Claude, (_, _) => OpenAssistant(AssistantKind.Claude)) { TabIndex = 1 };
        foreach (var question in AssistantHomePresentation.Suggestions)
        {
            var suggestion = new MinimalButton(question, (_, _) => CopySuggestion(question))
            {
                AutoSize = false, Font = new Font("Segoe UI", 10), TabIndex = suggestions.Count + 2,
                Padding = new Padding(15, 8, 15, 8), AccessibleDescription = "Copy this question for your assistant."
            };
            suggestions.Add(suggestion);
        }
        StatusLabel = new Label
        {
            AutoSize = false, ForeColor = BrowserTheme.Muted, Font = new Font("Segoe UI", 9),
            TextAlign = ContentAlignment.MiddleLeft
        };
        details = new LinkLabel
        {
            Text = "Details", AutoSize = false, LinkColor = BrowserTheme.Muted,
            ActiveLinkColor = BrowserTheme.Accent, VisitedLinkColor = BrowserTheme.Muted,
            LinkBehavior = LinkBehavior.HoverUnderline, Font = new Font("Segoe UI", 9),
            TextAlign = ContentAlignment.MiddleLeft, TabIndex = 5,
            AccessibleDescription = "View saved character data and collection status."
        };
        details.LinkClicked += (_, _) => DetailsRequested?.Invoke(this, EventArgs.Empty);
        FeedbackLabel = new Label
        {
            AutoSize = false, ForeColor = BrowserTheme.Success, Font = new Font("Segoe UI", 9),
            TextAlign = ContentAlignment.TopLeft, Visible = false, UseMnemonic = false
        };
        Controls.AddRange([HeadingLabel, subtitle, chatGpt, claude, StatusLabel, details, FeedbackLabel]);
        Controls.AddRange(suggestions.ToArray());
        confirmationTimer.Tick += (_, _) => { confirmationTimer.Stop(); FeedbackLabel.Visible = false; };
        SetCharacter(null);
    }

    public void SetCharacter(JsonObject? characterSnapshot)
    {
        snapshot = characterSnapshot;
        HeadingLabel.Text = AssistantHomePresentation.Heading(snapshot);
        var status = AssistantHomePresentation.Status(snapshot);
        StatusLabel.Text = status.Text;
        StatusLabel.AccessibleDescription = status.Text;
        statusHelp.SetToolTip(StatusLabel, "Reload or log out in-game to save your latest progress.");
        statusWarning = status.Warning;
        hasCharacter = status.HasCharacter;
        foreach (var suggestion in suggestions) suggestion.Enabled = status.HasCharacter;
        confirmationTimer.Stop();
        FeedbackLabel.Visible = false;
        PerformLayout();
        Invalidate();
    }

    public void SetExportStatus(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var status = AssistantHomePresentation.Status(snapshot);
        StatusLabel.Text = status.HasCharacter ? status.Text + " · Needs attention" :
            "Reload or log out in-game to save your first character.";
        StatusLabel.AccessibleDescription = text;
        statusHelp.SetToolTip(StatusLabel, text);
        statusWarning = true;
        PerformLayout();
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (HeadingLabel is null || chatGpt is null) return;
        var scale = scaleState.Factor(DeviceDpi);
        int D(float value) => (int)Math.Round(value * scale);
        var compact = Height < D(610);
        var pad = D(compact ? 24 : 36);
        var contentWidth = Math.Max(D(240), Width - pad * 2);
        var top = D(compact ? 16 : 38);
        HeadingLabel.Font = compact ? compactHeadingFont : normalHeadingFont;
        HeadingLabel.Bounds = new Rectangle(pad, top, contentWidth, D(compact ? 43 : 53));
        subtitle.Bounds = new Rectangle(pad + D(2), HeadingLabel.Bottom + D(2), contentWidth, D(compact ? 24 : 27));
        var cardsTop = subtitle.Bottom + D(compact ? 18 : 28);
        var gap = D(24);
        var cardWidth = Math.Min(D(420), (contentWidth - gap) / 2);
        var cardHeight = D(compact ? 190 : 250);
        var cardsLeft = pad + (contentWidth - cardWidth * 2 - gap) / 2;
        chatGpt.Bounds = new Rectangle(cardsLeft, cardsTop, cardWidth, cardHeight);
        claude.Bounds = new Rectangle(chatGpt.Right + gap, cardsTop, cardWidth, cardHeight);
        var suggestionTop = chatGpt.Bottom + D(compact ? 20 : 24);
        var chipGap = D(12);
        var chipWidth = (contentWidth - chipGap * 2) / 3;
        var chipHeight = D(62);
        for (var index = 0; index < suggestions.Count; index++)
        {
            var left = pad + index * (chipWidth + chipGap);
            var width = index == 2 ? pad + contentWidth - left : chipWidth;
            suggestions[index].Bounds = new Rectangle(left, suggestionTop, width, chipHeight);
        }
        var statusTop = suggestionTop + chipHeight + D(compact ? 20 : 32);
        var statusWidth = TextRenderer.MeasureText(StatusLabel.Text, StatusLabel.Font,
            new Size(Math.Max(1, contentWidth - D(84)), D(48)), TextFormatFlags.WordBreak).Width;
        statusWidth = Math.Min(statusWidth + D(8), Math.Max(1, contentWidth - D(84)));
        StatusLabel.Bounds = new Rectangle(pad + D(16), statusTop, statusWidth, D(24));
        details.Bounds = new Rectangle(StatusLabel.Right + D(12), statusTop, D(56), D(24));
        FeedbackLabel.Bounds = new Rectangle(pad + D(16), StatusLabel.Bottom + D(8), contentWidth - D(16), D(43));
        AutoScrollMinSize = new Size(0, FeedbackLabel.Bottom + D(14));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var scale = scaleState.Factor(DeviceDpi);
        var diameter = Math.Max(4, (int)Math.Round(6 * scale));
        using var dot = new SolidBrush(statusWarning ? BrowserTheme.Warning : hasCharacter ? BrowserTheme.Success : BrowserTheme.Muted);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.FillEllipse(dot, StatusLabel.Left - (int)Math.Round(14 * scale),
            StatusLabel.Top + (StatusLabel.Height - diameter) / 2, diameter, diameter);
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

    private void CopySuggestion(string question)
    {
        try
        {
            copyQuestion(AssistantHomePresentation.BuildQuestion(snapshot, question));
            FeedbackLabel.Text = "Copied. Paste into your assistant.";
            FeedbackLabel.ForeColor = BrowserTheme.Success;
        }
        catch (ExternalException)
        {
            FeedbackLabel.Text = "The clipboard is busy. Try again.";
            FeedbackLabel.ForeColor = BrowserTheme.Warning;
        }
        FeedbackLabel.Visible = true;
        confirmationTimer.Stop();
        confirmationTimer.Start();
    }

    private void OpenAssistant(AssistantKind kind)
    {
        var result = launcher.Open(kind);
        if (string.IsNullOrWhiteSpace(result.Message)) return;
        confirmationTimer.Stop();
        FeedbackLabel.Text = result.Message;
        FeedbackLabel.ForeColor = BrowserTheme.Warning;
        FeedbackLabel.Visible = true;
        if (result.BrowserFallback || !result.Opened)
            MessageBox.Show(this, result.Message, "Claudgar", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            confirmationTimer.Dispose();
            statusHelp.Dispose();
            normalHeadingFont.Dispose();
            compactHeadingFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
