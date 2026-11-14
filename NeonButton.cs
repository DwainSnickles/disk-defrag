using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DiskDefrag;

/// <summary>
/// The toolbar buttons: rounded corners, and a vertical gradient with a light top edge and a dark
/// bottom edge, so they sit up off the black window the same way the progress bar does. BackColor is
/// the base of the gradient and ForeColor the label, so the code that recolours STOP and the close
/// button keeps working.
/// </summary>
internal sealed class NeonButton : Button
{
    /// <summary>The radius the pills should have at 96 DPI; every other scaling is derived from it.</summary>
    private const int RadiusAt96Dpi = 12;


    public NeonButton()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);

        BackColor = Palette.Surface;
        ForeColor = Palette.Text;
        Cursor = Cursors.Hand;
    }

    /// <summary>
    /// Read from the live pointer rather than tracked with events: there is no state to leave set when
    /// the mouse leaves during a press, the control is disabled mid-click, or the form loses focus.
    /// </summary>
    private bool Hovered => Enabled && ClientRectangle.Contains(PointToClient(Cursor.Position));

    /// <summary>ButtonBase takes mouse capture for the press, so capture plus hover is the press.</summary>
    private bool Pressed => (Hovered && Capture) || KeyboardPressed;

    /// <summary>
    /// Space on a focused button is ButtonBase's keyboard press, and it holds no capture, so the key
    /// state is read from Windows instead of being tracked here.
    /// </summary>
    private bool KeyboardPressed =>
        (Focused && Held(VkSpace)) || (FindForm()?.AcceptButton == this && Held(VkReturn));

    private static bool Held(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    private const int VkSpace = 0x20;
    private const int VkReturn = 0x0D;

    // ButtonBase repaints itself on these; a hand-painted button has to ask.
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        Invalidate();
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    // SetButtons recolours STOP and swaps its label mid-run, so those changes have to repaint.
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        Invalidate();
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;

        // Own every pixel of the client rect, not just the pill: a host that paints the control without
        // running its background pass (the WinForms designer does) would otherwise show its own colour,
        // typically white, in the four corners.
        g.Clear(Parent?.BackColor ?? Palette.Background);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = CornerRadius(DeviceDpi, bounds.Width, bounds.Height);
        using GraphicsPath shape = RoundedPath(bounds, radius);

        bool hovered = Hovered;
        bool pressed = Pressed;

        // The focused button wears the accent, the way the primary action does in the console, but it
        // keeps the same lit-top / shaded-bottom ramp so it still reads as a raised pill.
        bool active = Enabled && Focused;
        Color face = !Enabled ? Blend(BackColor, Palette.Background, 0.55)
            : active ? Palette.Accent
            : BackColor;

        // Vertical ramp: lit at the top, shaded at the bottom - reversed and darkened while pressed.
        Color top = pressed
            ? Blend(face, Color.Black, active ? 0.30 : 0.40)
            : Blend(face, Color.White, hovered ? 0.34 : active ? 0.20 : 0.24);
        Color bottom = pressed
            ? Blend(face, Color.White, active ? 0.18 : 0.16)
            : Blend(face, Color.Black, hovered ? 0.32 : active ? 0.32 : 0.46);

        using (var fill = new LinearGradientBrush(
                   new Rectangle(0, 0, Width, Height + 1), top, bottom, LinearGradientMode.Vertical))
        {
            g.FillPath(fill, shape);
        }

        // Bevel: a bright line under the top edge and a dark one above the bottom edge.
        using (var highlight = new Pen(Color.FromArgb(Enabled ? 115 : 45, 255, 255, 255)))
        using (var shade = new Pen(Color.FromArgb(Enabled ? 120 : 60, 0, 0, 0)))
        {
            g.DrawLine(highlight, radius, 1, bounds.Width - radius, 1);
            g.DrawLine(shade, radius, bounds.Height - 1, bounds.Width - radius, bounds.Height - 1);
        }

        Color border = !Enabled ? Color.FromArgb(28, 36, 48)
            : active ? Blend(Palette.Accent, Color.Black, 0.32)
            : hovered ? Palette.Accent
            : Palette.Border;
        using (var outline = new Pen(border))
        {
            g.DrawPath(outline, shape);
        }

        // The accent face is itself the focus cue, so the inner ring would only mud it up.
        if (!active && Focused && ShowFocusCues)
        {
            int inset = Math.Max(2, ScaleToDpi(DeviceDpi, 3));
            using GraphicsPath inner = RoundedPath(Rectangle.Inflate(bounds, -inset, -inset), Math.Max(2, radius - inset));
            using var focus = new Pen(Color.FromArgb(120, Palette.Accent));
            g.DrawPath(focus, inner);
        }

        Color textColor = !Enabled ? Blend(ForeColor, Palette.Background, 0.55)
            : active ? Color.FromArgb(4, 18, 24)
            : ForeColor;
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            new Rectangle(4, pressed ? 1 : 0, Width - 8, Height),
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    /// <summary>
    /// The pill radius for the control's own monitor DPI, so a 125% or 150% display keeps the same
    /// shape instead of a squarer one, and never past a half-rounded end.
    /// </summary>
    internal static int CornerRadius(int dpi, int width, int height)
    {
        int scaled = ScaleToDpi(dpi, RadiusAt96Dpi);
        return Math.Max(2, Math.Min(scaled, Math.Min(width, height) / 2));
    }

    private static int ScaleToDpi(int dpi, int value) =>
        (int)Math.Round(value * Math.Max(96, dpi) / 96.0, MidpointRounding.AwayFromZero);

    /// <summary>Shared with the drive picker, so both controls draw the same shape.</summary>
    internal static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        if (diameter >= bounds.Width || diameter >= bounds.Height)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static Color Blend(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            (int)Math.Round(from.R + (to.R - from.R) * amount),
            (int)Math.Round(from.G + (to.G - from.G) * amount),
            (int)Math.Round(from.B + (to.B - from.B) * amount));
    }
}
