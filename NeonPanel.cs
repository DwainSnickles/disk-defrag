using System.Drawing.Drawing2D;

namespace DiskDefrag;

/// <summary>
/// A container with the progress bar's material: a vertical ramp with a light top edge and a dark
/// bottom one, inside a two-tone frame, so the option strip sits in the same panel as the bar.
/// </summary>
internal sealed class NeonPanel : FlowLayoutPanel
{
    public NeonPanel()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);
        BackColor = Palette.Background;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        if (bounds.Width <= 4 || bounds.Height <= 4) return;

        // Own every pixel, so nothing of the host shows through at the edges.
        g.Clear(Parent?.BackColor ?? Palette.Background);

        var inner = Rectangle.Inflate(bounds, -2, -2);
        Color top = NeonButton.Blend(Palette.SurfaceRaised, Color.White, 0.10);
        Color bottom = NeonButton.Blend(Palette.SurfaceRaised, Color.Black, 0.45);
        using (var ramp = new LinearGradientBrush(
                   new Rectangle(inner.X, inner.Y, inner.Width, inner.Height + 1),
                   top,
                   bottom,
                   LinearGradientMode.Vertical))
        {
            g.FillRectangle(ramp, inner);
        }

        // Sheen over the top half, like the progress bar's fill, to lift the strip off the window.
        var sheenArea = new Rectangle(inner.X, inner.Y, inner.Width, Math.Max(2, inner.Height / 2));
        using (var sheen = new LinearGradientBrush(
                   sheenArea, Color.FromArgb(30, Color.White), Color.FromArgb(0, Color.White), LinearGradientMode.Vertical))
        {
            g.FillRectangle(sheen, sheenArea);
        }

        // Bevel: a lit line under the top edge and a shadow above the bottom edge, as on the bar.
        using (var light = new Pen(Color.FromArgb(60, 255, 255, 255)))
        using (var shade = new Pen(Color.FromArgb(90, 0, 0, 0)))
        {
            g.DrawLine(light, inner.Left, inner.Top, inner.Right - 1, inner.Top);
            g.DrawLine(shade, inner.Left, inner.Bottom - 1, inner.Right - 1, inner.Bottom - 1);
        }

        // Frame: near-black outside, the toolbar's border colour inside, matching the progress bar.
        using (var outer = new Pen(Color.FromArgb(230, 2, 7, 12)))
        {
            g.DrawRectangle(outer, 0, 0, bounds.Width, bounds.Height);
        }

        using (var edge = new Pen(Palette.Border))
        {
            g.DrawRectangle(edge, 1, 1, bounds.Width - 2, bounds.Height - 2);
        }
    }
}
