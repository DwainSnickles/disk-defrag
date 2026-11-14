using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DiskDefrag;

/// <summary>The window's colours, so a control drawn by hand and a control styled by the designer agree.</summary>
internal static class Palette
{
    public static readonly Color Background = Color.Black;
    public static readonly Color Surface = Color.FromArgb(20, 29, 41);
    public static readonly Color SurfaceRaised = Color.FromArgb(15, 21, 30);
    public static readonly Color Border = Color.FromArgb(35, 55, 73);
    public static readonly Color Text = Color.FromArgb(220, 233, 245);
    public static readonly Color TextDim = Color.FromArgb(160, 180, 200);
    public static readonly Color Accent = Color.FromArgb(29, 206, 235);
    public static readonly Color Selection = Color.FromArgb(12, 74, 92);
}

/// <summary>
/// Paints the window frame dark. Windows 10 1809+ and Windows 11 honour the immersive-dark-mode
/// attribute; Windows 11 also accepts explicit caption/border/text colours, so the frame matches
/// the black client area instead of sitting there in light grey. Older builds simply stay light.
/// </summary>
internal static class DarkWindow
{
    private const int UseImmersiveDarkMode = 20;
    private const int UseImmersiveDarkModeLegacy = 19;   // Windows 10 1809-1909 spell it 19
    private const int CaptionColor = 35;
    private const int BorderColor = 34;
    private const int TextColor = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    public static void Apply(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;

        int dark = 1;
        if (DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref dark, sizeof(int)) != 0 &&
            DwmSetWindowAttribute(handle, UseImmersiveDarkModeLegacy, ref dark, sizeof(int)) != 0)
        {
            return;
        }

        int caption = ToColorRef(Palette.Background);
        int border = ToColorRef(Palette.Border);
        int text = ToColorRef(Palette.Text);
        DwmSetWindowAttribute(handle, CaptionColor, ref caption, sizeof(int));
        DwmSetWindowAttribute(handle, BorderColor, ref border, sizeof(int));
        DwmSetWindowAttribute(handle, TextColor, ref text, sizeof(int));
    }

    /// <summary>DWM wants a COLORREF: 0x00BBGGRR, not the ARGB an int would carry.</summary>
    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);
}

/// <summary>
/// The drive picker. Windows draws a ComboBox with the system colours, which left a white box in the
/// middle of the black toolbar, so the field, the arrow and every list row are drawn here instead.
/// The drop-down is a separate top-level window that .NET subclasses for itself, so it is shaped with
/// a window region and its frame is drawn from the last row rather than by a second subclass.
/// </summary>
internal sealed class DarkComboBox : ComboBox
{
    private const int WmPaint = 0x000F;
    private const int WmPrint = 0x0317;
    private const int WmPrintClient = 0x0318;
    private const int ArrowWidth = 24;
    private const int GwlStyle = -16;
    private const int WsBorder = 0x00800000;
    private const uint SwpFrameOnly = 0x0001 | 0x0002 | 0x0004 | 0x0020; // NOSIZE|NOMOVE|NOZORDER|FRAMECHANGED

    private Region? _shape;
    private IntPtr _listHandle;
    private Size _listSize;

    public DarkComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        BackColor = Palette.Surface;
        ForeColor = Palette.Text;
        ItemHeight = 22;
        Cursor = Cursors.Hand;
    }

    private Color FaceTop => NeonButton.Blend(BackColor, Color.White, 0.12);
    private Color FaceBottom => NeonButton.Blend(BackColor, Color.Black, 0.30);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DropFieldBorder();
        ApplyShape();
    }

    /// <summary>
    /// The field's 1px window border is drawn by the system in its own light colour, outside our
    /// client area. Removing it leaves the whole control to the ramp and the rounded border drawn here.
    /// </summary>
    private void DropFieldBorder()
    {
        int style = GetWindowLong(Handle, GwlStyle);
        if ((style & WsBorder) == 0) return;

        SetWindowLong(Handle, GwlStyle, style & ~WsBorder);
        SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, SwpFrameOnly);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyShape();
        Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyShape();
        Invalidate();
    }

    /// <summary>Clips the native field to the same rounded pill the toolbar buttons use.</summary>
    private void ApplyShape()
    {
        if (!IsHandleCreated) return;

        var bounds = new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height));
        int radius = NeonButton.CornerRadius(DeviceDpi, bounds.Width, bounds.Height);
        using GraphicsPath path = NeonButton.RoundedPath(bounds, radius);

        var updated = new Region(path);
        Region? previous = _shape;
        _shape = updated;
        Region = updated;
        previous?.Dispose();
    }

    protected override void OnDropDown(EventArgs e)
    {
        base.OnDropDown(e);

        // Only once the list exists: the handle changes when the list is recreated.
        IntPtr list = ComboBoxInfo.ListHandle(Handle);
        if (list == IntPtr.Zero) return;

        if (list != _listHandle)
        {
            _listHandle = list;
            DropSystemBorder(list);
        }

        ApplyListShape(list);
    }

    /// <summary>
    /// The list window carries a 1px light grey frame outside its client area, which would draw a
    /// square around the rounded shape. Removing the border style hands that ring to our own painting.
    /// </summary>
    private static void DropSystemBorder(IntPtr list)
    {
        int style = GetWindowLong(list, GwlStyle);
        if ((style & WsBorder) == 0) return;

        SetWindowLong(list, GwlStyle, style & ~WsBorder);
        SetWindowPos(list, IntPtr.Zero, 0, 0, 0, 0, SwpFrameOnly);
    }

    /// <summary>Rounds the popup to the field's radius, so the two read as one control.</summary>
    private void ApplyListShape(IntPtr list)
    {
        if (!GetClientRect(list, out NativeRect rect)) return;

        var size = new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
        if (size.Width <= 0 || size.Height <= 0) return;

        _listSize = size;
        int radius = NeonButton.CornerRadius(DeviceDpi, size.Width, size.Height);
        IntPtr region = CreateRoundRectRgn(0, 0, size.Width + 1, size.Height + 1, radius * 2, radius * 2);
        if (region != IntPtr.Zero) SetWindowRgn(list, region, true);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool closedField = (e.State & DrawItemState.ComboBoxEdit) != 0;
        bool highlighted = !closedField && (e.State & DrawItemState.Selected) != 0;

        if (closedField)
        {
            // Same vertical ramp as the buttons, so the field sits in the same set.
            using var face = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, Width), Math.Max(2, Height) + 1),
                FaceTop,
                FaceBottom,
                LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(face, e.Bounds);
        }
        else
        {
            // The first row owns the list's whole surface, so any strip the rows do not cover is dark.
            if (e.Index == 0 && !_listSize.IsEmpty)
            {
                using var surface = new SolidBrush(Palette.SurfaceRaised);
                e.Graphics.FillRectangle(surface, new Rectangle(Point.Empty, _listSize));
            }

            using var row = new SolidBrush(highlighted ? Palette.Selection : Palette.SurfaceRaised);
            e.Graphics.FillRectangle(row, e.Bounds);
        }

        if (e.Index < 0 || e.Index >= Items.Count) return;

        Rectangle textBounds = new(e.Bounds.Left + 7, e.Bounds.Top, e.Bounds.Width - 12, e.Bounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            GetItemText(Items[e.Index]),
            Font,
            textBounds,
            ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        // After the last row, so the border is not covered by the rows that follow it.
        if (!closedField && e.Index == Items.Count - 1) DrawListFrame(e.Graphics, e.Bounds.Bottom);
    }

    /// <summary>Paints the popup's own frame: the rounded border and anything below the last row.</summary>
    private void DrawListFrame(Graphics graphics, int rowsBottom)
    {
        if (_listSize.IsEmpty) return;

        int below = _listSize.Height - rowsBottom;
        if (below > 0)
        {
            using var surface = new SolidBrush(Palette.SurfaceRaised);
            graphics.FillRectangle(surface, 0, rowsBottom, _listSize.Width, below);
        }

        int radius = NeonButton.CornerRadius(DeviceDpi, _listSize.Width, _listSize.Height);
        using GraphicsPath shape = NeonButton.RoundedPath(
            new Rectangle(0, 0, _listSize.Width - 1, _listSize.Height - 1), radius);

        SmoothingMode previous = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var border = new Pen(Palette.Border))
        {
            graphics.DrawPath(border, shape);
        }

        graphics.SmoothingMode = previous;
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        // WM_PRINT renders the field through the system's own painting - a light border and arrow
        // button - so the overlay has to run on that DC too, or anything that prints the control
        // (the WinForms designer, DrawToBitmap) shows a white frame around a dark field.
        if (m.Msg is WmPrint or WmPrintClient)
        {
            using Graphics printed = Graphics.FromHdc(m.WParam);
            DrawFieldOverlay(printed, paintFace: true);
            return;
        }

        if (m.Msg != WmPaint) return;

        using Graphics graphics = Graphics.FromHwnd(Handle);
        DrawFieldOverlay(graphics);
    }

    /// <summary>Repaints everything the system draws light: the arrow well and the rounded border.</summary>
    private void DrawFieldOverlay(Graphics graphics, bool paintFace = false)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = NeonButton.CornerRadius(DeviceDpi, bounds.Width, bounds.Height);
        using GraphicsPath shape = NeonButton.RoundedPath(bounds, radius);

        if (paintFace)
        {
            // WM_PRINT never sends WM_DRAWITEM, so the system leaves a white text pane behind the
            // label. Printing gets the same ramp and label the live field shows.
            using (var face = new LinearGradientBrush(
                       new Rectangle(0, 0, Math.Max(1, Width), Math.Max(2, Height) + 1),
                       FaceTop,
                       FaceBottom,
                       LinearGradientMode.Vertical))
            {
                graphics.FillRectangle(face, bounds);
            }

            if (SelectedIndex >= 0)
            {
                TextRenderer.DrawText(
                    graphics,
                    GetItemText(Items[SelectedIndex]),
                    Font,
                    new Rectangle(7, 0, Math.Max(1, Width - ArrowWidth - 14), Height),
                    ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        // The system still draws its own arrow button, including a light underline; repaint that strip
        // with the field's ramp, then put the rounded border on top of it.
        var well = new Rectangle(Width - ArrowWidth - 1, 0, ArrowWidth, Height);
        using (var fill = new LinearGradientBrush(
                   new Rectangle(0, 0, Math.Max(1, Width), Height + 1),
                   FaceTop,
                   FaceBottom,
                   LinearGradientMode.Vertical))
        {
            graphics.FillRectangle(fill, well);
        }

        using (var border = new Pen(Palette.Border))
        {
            graphics.DrawPath(border, shape);
        }

        int centerX = well.Left + well.Width / 2;
        int centerY = well.Top + well.Height / 2;
        using var chevron = new Pen(Palette.TextDim, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.DrawLines(chevron, new[]
        {
            new Point(centerX - 4, centerY - 2),
            new Point(centerX, centerY + 2),
            new Point(centerX + 4, centerY - 2),
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _shape?.Dispose();
            _shape = null;
        }

        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ComboBoxInfo
    {
        public int Size;
        public int ItemLeft, ItemTop, ItemRight, ItemBottom;
        public int ButtonLeft, ButtonTop, ButtonRight, ButtonBottom;
        public int ButtonState;
        public IntPtr ComboHandle;
        public IntPtr EditHandle;
        public IntPtr ListHandleRaw;

        public static IntPtr ListHandle(IntPtr combo)
        {
            var info = new ComboBoxInfo { Size = Marshal.SizeOf<ComboBoxInfo>() };
            return GetComboBoxInfo(combo, ref info) ? info.ListHandleRaw : IntPtr.Zero;
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetComboBoxInfo")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetComboBoxInfo(IntPtr combo, ref ComboBoxInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr window, int index, int value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr window, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(
        int left, int top, int right, int bottom, int width, int height);
}
