using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Claudgar.App.Browser;

/// <summary>A rounded action retaining native button input, accessibility, and keyboard navigation.</summary>
internal sealed class MinimalButton : Button
{
    private bool primary;
    private float cornerRadius = 12;
    private bool hovered;
    private bool pressed;
    private readonly ControlScaleState scaleState = new();

    public MinimalButton(string text, EventHandler? clicked = null, bool primary = false)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor, true);
        Text = text;
        this.primary = primary;
        BackColor = Color.Transparent;
        ForeColor = BrowserTheme.Ink;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        UseMnemonic = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(0, 48);
        Padding = new Padding(16, 10, 16, 10);
        Margin = Padding.Empty;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
        if (clicked is not null) Click += clicked;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Primary
    {
        get => primary;
        set { primary = value; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float CornerRadius
    {
        get => cornerRadius;
        set { cornerRadius = Math.Max(0, value); Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 1 || Height <= 1) return;
        var scale = scaleState.Factor(DeviceDpi);
        var fillColor = Primary ? BrowserTheme.Accent : BackColor.A == 0 ? BrowserTheme.Raised : BackColor;
        var textColor = Primary ? BrowserTheme.Background : ForeColor;
        if (!Enabled)
        {
            fillColor = BrowserTheme.Surface;
            textColor = BrowserTheme.Muted;
        }
        else if (pressed) fillColor = Mix(fillColor, BrowserTheme.Background, .2f);
        else if (hovered) fillColor = Mix(fillColor, BrowserTheme.Ink, .09f);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = RoundedDrawing.CreatePath(new RectangleF(.5f, .5f, Width - 1, Height - 1), CornerRadius * scale);
        using var fill = new SolidBrush(fillColor);
        e.Graphics.FillPath(fill, shape);

        var textBounds = new Rectangle(Padding.Left, Padding.Top,
            Math.Max(0, Width - Padding.Horizontal), Math.Max(0, Height - Padding.Vertical));
        var flags = TextFlags();
        if (textBounds.Width > 0 && textBounds.Height > 0)
        {
            var measured = TextRenderer.MeasureText(e.Graphics, Text, Font,
                new Size(textBounds.Width, int.MaxValue), flags);
            var spareHeight = Math.Max(0, textBounds.Height - measured.Height);
            textBounds.Y += TextAlign switch
            {
                ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => 0,
                ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => spareHeight,
                _ => spareHeight / 2
            };
            textBounds.Height -= textBounds.Y - Padding.Top;
            TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, textColor, flags);
        }

        if (Focused && ShowFocusCues)
        {
            var inset = 3 * scale;
            using var focusShape = RoundedDrawing.CreatePath(new RectangleF(inset, inset,
                Math.Max(1, Width - inset * 2), Math.Max(1, Height - inset * 2)), Math.Max(0, CornerRadius * scale - inset));
            using var focus = new Pen(Primary ? BrowserTheme.Background : BrowserTheme.Accent, Math.Max(1, 1.5f * scale));
            e.Graphics.DrawPath(focus, focusShape);
        }
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; pressed = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } }
    protected override void OnKeyUp(KeyEventArgs e) { base.OnKeyUp(e); pressed = false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); pressed = false; Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (!Enabled) pressed = hovered = false; Invalidate(); }
    protected override void OnChangeUICues(UICuesEventArgs e) { base.OnChangeUICues(e); Invalidate(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); scaleState.DpiChanged(DeviceDpi); Invalidate(); }

    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        scaleState.Apply(factor, specified, DeviceDpi);
        base.ScaleControl(factor, specified);
        Invalidate();
    }

    private TextFormatFlags TextFlags()
    {
        var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        flags |= TextAlign switch
        {
            ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => TextFormatFlags.Left,
            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => TextFormatFlags.Right,
            _ => TextFormatFlags.HorizontalCenter
        };
        return flags;
    }

    private static Color Mix(Color start, Color end, float amount) => Color.FromArgb(
        (int)(start.R + (end.R - start.R) * amount),
        (int)(start.G + (end.G - start.G) * amount),
        (int)(start.B + (end.B - start.B) * amount));
}
