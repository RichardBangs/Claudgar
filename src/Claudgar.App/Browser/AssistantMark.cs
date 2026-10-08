using System.Drawing.Drawing2D;

namespace Claudgar.App.Browser;

/// <summary>Resolution-independent assistant marks for the two desktop launch cards.</summary>
internal sealed class AssistantMark : Control
{
    private readonly AssistantKind kind;
    private readonly GraphicsPath? chatGptPath;

    public AssistantMark(AssistantKind kind)
    {
        this.kind = kind;
        if (kind == AssistantKind.ChatGpt) chatGptPath = CreateChatGptPath();
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        TabStop = false;
        AccessibleName = kind == AssistantKind.ChatGpt ? "ChatGPT" : "Claude";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TranslateTransform(Width / 2f, Height / 2f);
        var scale = Math.Min(Width, Height) / 84f;
        graphics.ScaleTransform(scale, scale);
        if (kind == AssistantKind.Claude) DrawClaude(graphics);
        else if (chatGptPath is not null) DrawChatGpt(graphics, chatGptPath);
        graphics.Restore(state);
    }

    private static void DrawClaude(Graphics graphics)
    {
        using var ink = new SolidBrush(BrowserTheme.Ink);
        var lengths = new[] { 35f, 38f, 31f, 40f, 35f, 39f, 31f, 37f, 40f, 34f, 38f, 32f, 39f, 35f };
        for (var index = 0; index < lengths.Length; index++)
        {
            var angle = (index * 360f / lengths.Length - 92) * MathF.PI / 180;
            var along = new PointF(MathF.Cos(angle), MathF.Sin(angle));
            var across = new PointF(-along.Y, along.X);
            var length = lengths[index];
            graphics.FillPolygon(ink,
            [
                new PointF(along.X * 3 + across.X * 3.5f, along.Y * 3 + across.Y * 3.5f),
                new PointF(along.X * length + across.X * 2.3f, along.Y * length + across.Y * 2.3f),
                new PointF(along.X * (length + 1) - across.X * 1.6f, along.Y * (length + 1) - across.Y * 1.6f),
                new PointF(along.X * 3 - across.X * 3.5f, along.Y * 3 - across.Y * 3.5f)
            ]);
        }
        graphics.FillEllipse(ink, -7, -7, 14, 14);
    }

    private static void DrawChatGpt(Graphics graphics, GraphicsPath path)
    {
        using var ink = new SolidBrush(BrowserTheme.Ink);
        graphics.FillPath(ink, path);
    }

    private static GraphicsPath CreateChatGptPath()
    {
        // OpenAI's ChatGPT Blossom, converted directly from its official SVG compound path.
        // Source (pinned): https://github.com/openai/openai-cookbook/blob/4a85c3018d20ceef48bf7549450c567896501bf9/examples/voice_solutions/realtime_translation_guide/livekit-translation-demo/public/brand/chatgpt-blossom-black.svg
        // The ChatGPT mark belongs to OpenAI: https://openai.com/brand/
        var path = new GraphicsPath(FillMode.Winding);
        var current = PointF.Empty;
        void MoveTo(float x, float y) { path.StartFigure(); current = new PointF(x, y); }
        void LineTo(float x, float y)
        {
            var next = new PointF(x, y);
            path.AddLine(current, next);
            current = next;
        }
        void CurveTo(float x1, float y1, float x2, float y2, float x, float y)
        {
            var next = new PointF(x, y);
            path.AddBezier(current, new PointF(x1, y1), new PointF(x2, y2), next);
            current = next;
        }

        MoveTo(508.749f, 317.399f);
        CurveTo(516.777f, 287.314f, 508.991f, 253.884f, 485.389f, 230.282f);
        CurveTo(461.788f, 206.681f, 428.36f, 198.895f, 398.273f, 206.923f);
        CurveTo(376.231f, 184.928f, 343.39f, 174.956f, 311.148f, 183.596f);
        CurveTo(278.906f, 192.234f, 255.45f, 217.292f, 247.36f, 247.361f);
        CurveTo(217.291f, 255.451f, 192.233f, 278.91f, 183.595f, 311.149f);
        CurveTo(174.957f, 343.391f, 184.927f, 376.232f, 206.924f, 398.274f);
        CurveTo(198.896f, 428.359f, 206.683f, 461.789f, 230.284f, 485.391f);
        CurveTo(253.885f, 508.992f, 287.313f, 516.779f, 317.401f, 508.75f);
        CurveTo(339.442f, 530.745f, 372.286f, 540.717f, 404.525f, 532.079f);
        CurveTo(436.767f, 523.441f, 460.223f, 498.384f, 468.313f, 468.315f);
        CurveTo(498.383f, 460.224f, 523.44f, 436.766f, 532.078f, 404.526f);
        CurveTo(540.716f, 372.285f, 530.747f, 339.443f, 508.749f, 317.402f);
        LineTo(current.X, 317.399f);
        path.CloseFigure();

        MoveTo(470.899f, 244.776f);
        CurveTo(486.892f, 260.77f, 493.488f, 282.601f, 490.687f, 303.412f);
        LineTo(415.577f, 260.046f);
        CurveTo(412.411f, 258.218f, 408.509f, 258.218f, 405.345f, 260.046f);
        LineTo(317.401f, 310.82f);
        LineTo(current.X, 277.526f);
        CurveTo(317.401f, 275.191f, 318.652f, 273.005f, 320.676f, 271.837f);
        LineTo(387.644f, 233.174f);
        CurveTo(414.178f, 218.353f, 448.346f, 222.223f, 470.901f, 244.776f);
        LineTo(470.899f, current.Y);
        path.CloseFigure();

        MoveTo(357.837f, 311.144f);
        LineTo(398.275f, 334.491f);
        LineTo(current.X, 381.185f);
        LineTo(357.837f, 404.532f);
        LineTo(317.398f, 381.185f);
        LineTo(current.X, 334.491f);
        LineTo(357.837f, 311.144f);
        path.CloseFigure();

        MoveTo(264.776f, 269.693f);
        CurveTo(265.207f, 239.305f, 285.644f, 211.649f, 316.453f, 203.393f);
        CurveTo(338.3f, 197.54f, 360.505f, 202.744f, 377.127f, 215.573f);
        LineTo(302.014f, 258.937f);
        CurveTo(298.848f, 260.764f, 296.898f, 264.144f, 296.898f, 267.798f);
        LineTo(current.X, 369.346f);
        LineTo(268.065f, 352.699f);
        CurveTo(266.043f, 351.531f, 264.776f, 349.353f, 264.776f, 347.017f);
        LineTo(current.X, 269.691f);
        LineTo(current.X, 269.693f);
        path.CloseFigure();

        MoveTo(203.391f, 316.454f);
        CurveTo(209.244f, 294.608f, 224.854f, 277.978f, 244.276f, 269.999f);
        LineTo(current.X, 356.73f);
        CurveTo(244.276f, 360.384f, 246.226f, 363.763f, 249.392f, 365.591f);
        LineTo(337.337f, 416.365f);
        LineTo(308.503f, 433.013f);
        CurveTo(306.481f, 434.181f, 303.961f, 434.188f, 301.939f, 433.02f);
        LineTo(234.971f, 394.357f);
        CurveTo(208.868f, 378.789f, 195.138f, 347.261f, 203.391f, 316.454f);
        path.CloseFigure();

        MoveTo(244.775f, 470.9f);
        CurveTo(228.781f, 454.906f, 222.186f, 433.075f, 224.986f, 412.264f);
        LineTo(300.096f, 455.63f);
        CurveTo(303.263f, 457.457f, 307.164f, 457.457f, 310.328f, 455.63f);
        LineTo(398.273f, 404.856f);
        LineTo(current.X, 438.149f);
        CurveTo(398.273f, 440.485f, 397.022f, 442.671f, 394.997f, 443.839f);
        LineTo(328.029f, 482.502f);
        CurveTo(301.495f, 497.322f, 267.327f, 493.452f, 244.772f, 470.9f);
        LineTo(244.775f, current.Y);
        path.CloseFigure();

        MoveTo(450.897f, 445.982f);
        CurveTo(450.466f, 476.371f, 430.029f, 504.027f, 399.22f, 512.283f);
        CurveTo(377.373f, 518.136f, 355.168f, 512.932f, 338.547f, 500.102f);
        LineTo(413.659f, 456.738f);
        CurveTo(416.826f, 454.911f, 418.775f, 451.532f, 418.775f, 447.877f);
        LineTo(current.X, 346.329f);
        LineTo(447.609f, 362.977f);
        CurveTo(449.631f, 364.145f, 450.897f, 366.323f, 450.897f, 368.659f);
        LineTo(current.X, 445.985f);
        LineTo(current.X, 445.982f);
        path.CloseFigure();

        MoveTo(512.282f, 399.221f);
        CurveTo(506.429f, 421.068f, 490.819f, 437.697f, 471.397f, 445.676f);
        LineTo(current.X, 358.946f);
        CurveTo(471.397f, 355.292f, 469.448f, 351.912f, 466.281f, 350.085f);
        LineTo(378.336f, 299.311f);
        LineTo(407.17f, 282.663f);
        CurveTo(409.192f, 281.495f, 411.712f, 281.487f, 413.734f, 282.655f);
        LineTo(480.702f, 321.318f);
        CurveTo(506.805f, 336.887f, 520.536f, 368.415f, 512.282f, 399.221f);
        path.CloseFigure();

        var bounds = path.GetBounds();
        var scale = 76f / Math.Max(bounds.Width, bounds.Height);
        using var transform = new Matrix();
        transform.Translate(-bounds.X - bounds.Width / 2, -bounds.Y - bounds.Height / 2);
        transform.Scale(scale, scale, MatrixOrder.Append);
        path.Transform(transform);
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) chatGptPath?.Dispose();
        base.Dispose(disposing);
    }
}
