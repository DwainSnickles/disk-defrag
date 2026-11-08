using System.Drawing.Drawing2D;

namespace DiskDefrag;

/// <summary>The PC Medic instrument-panel palette, so the companion tool reads as part of the suite.</summary>
internal static class DefragTheme
{
    public static readonly Color Bg = Color.FromArgb(7, 10, 15);
    public static readonly Color Surface = Color.FromArgb(13, 18, 26);
    public static readonly Color Surface2 = Color.FromArgb(18, 25, 35);
    public static readonly Color Border = Color.FromArgb(30, 42, 56);
    public static readonly Color BorderSoft = Color.FromArgb(22, 31, 42);

    public static readonly Color Accent = Color.FromArgb(34, 211, 238);
    public static readonly Color Good = Color.FromArgb(52, 211, 153);
    public static readonly Color Warn = Color.FromArgb(250, 191, 76);
    public static readonly Color Bad = Color.FromArgb(248, 113, 113);
    public static readonly Color Violet = Color.FromArgb(167, 139, 250);

    public static readonly Color Text = Color.FromArgb(230, 239, 248);
    public static readonly Color TextDim = Color.FromArgb(130, 149, 168);
    public static readonly Color TextFaint = Color.FromArgb(82, 99, 116);

    public static readonly Font Ui;
    public static readonly Font UiBold;
    public static readonly Font UiSmall;
    public static readonly Font Mono;
    public static readonly Font MonoSmall;
    public static readonly Font Numeric;
    public static readonly Font Kicker;
    public static readonly Font H1;
    public static readonly Font H2;

    static DefragTheme()
    {
        string ui = FirstAvailable("Segoe UI Variable Text", "Segoe UI", "Tahoma");
        string mono = FirstAvailable("Cascadia Mono", "Consolas", "Lucida Console");

        Ui = new Font(ui, 9.75f);
        UiBold = new Font(ui, 9.75f, FontStyle.Bold);
        UiSmall = new Font(ui, 8.5f);
        Mono = new Font(mono, 9f);
        MonoSmall = new Font(mono, 8.25f);
        Numeric = new Font(mono, 15f, FontStyle.Bold);
        Kicker = new Font(mono, 7.5f, FontStyle.Bold);
        H1 = new Font(ui, 19f, FontStyle.Bold);
        H2 = new Font(ui, 12.5f, FontStyle.Bold);
    }

    private static string FirstAvailable(params string[] names)
    {
        FontFamily[] families = FontFamily.Families;

        foreach (string name in names)
        {
            foreach (FontFamily family in families)
            {
                if (string.Equals(family.Name, name, StringComparison.OrdinalIgnoreCase)) return family.Name;
            }
        }

        return "Microsoft Sans Serif";
    }

    public static Color Alpha(Color color, int alpha) => Color.FromArgb(Math.Clamp(alpha, 0, 255), color);

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));

        if (d <= 1f || r.Width <= 0 || r.Height <= 0)
        {
            path.AddRectangle(r);
            return path;
        }

        path.AddArc(r.X, r.Y, d, d, 180f, 90f);
        path.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
        path.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    public static void PaintSurface(Graphics g, RectangleF rect, float radius, Color fill, Color border, Color? tick = null)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath path = Round(rect, radius);

        using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
        using (var pen = new Pen(border, 1f)) g.DrawPath(pen, path);

        if (tick is { } tickColor)
        {
            using var tickPen = new Pen(Alpha(tickColor, 170), 2f);
            g.DrawLine(tickPen, rect.X + 16f, rect.Y + 0.5f, rect.X + 58f, rect.Y + 0.5f);
        }
    }

    public static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? string.Empty;
        return string.Concat(text.AsSpan(0, Math.Max(0, max - 1)), "\u2026");
    }

    public static string Bytes(ulong bytes)
    {
        double value = bytes;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit >= 3 ? $"{value:0.00} {units[unit]}" : $"{value:0} {units[unit]}";
    }
}

/// <summary>Panel with a small-caps kicker and header, matching PC Medic's cards.</summary>
internal sealed class CardPanel : Panel
{
    public CardPanel(string header, string kicker)
    {
        DoubleBuffered = true;
        BackColor = DefragTheme.Bg;
        SetStyle(ControlStyles.ResizeRedraw, true);
        Header = header;
        Kicker = kicker;
        Padding = new Padding(18, 50, 18, 14);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Header { get; set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Kicker { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 8 || Height < 8) return;

        DefragTheme.PaintSurface(
            e.Graphics,
            new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f),
            10f,
            DefragTheme.Surface,
            DefragTheme.Border,
            DefragTheme.Accent);

        TextRenderer.DrawText(
            e.Graphics,
            Kicker.ToUpperInvariant(),
            DefragTheme.Kicker,
            new Point(18, 15),
            DefragTheme.TextDim,
            TextFormatFlags.NoPrefix);

        TextRenderer.DrawText(
            e.Graphics,
            Header,
            DefragTheme.H2,
            new Point(18, 27),
            DefragTheme.Text,
            TextFormatFlags.NoPrefix);

        base.OnPaint(e);
    }
}
