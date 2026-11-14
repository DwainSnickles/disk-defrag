using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace aProgressBar;

[ToolboxItem(true)]
[DefaultProperty(nameof(Value))]
public class aProgressBar : Control
{
    private readonly System.Windows.Forms.Timer _animationTimer = new() { Interval = 30 };
    private const double SweepStep = 0.015;   // about two seconds for one pass
    private double _sweep;
    private Color _gradientColor1 = Color.Silver;
    private Color _gradientColor2 = Color.Black;
    private Color _gradientColor3 = Color.LightGray;
    private LinearGradientMode _gradientMode = LinearGradientMode.Horizontal;
    private Color _backGradientColor1 = Color.Black;
    private Color _backGradientColor2 = Color.Black;
    private Color _backGradientColor3 = Color.Black;
    private Color _borderColor = Color.FromArgb(61, 89, 112);
    private int _borderWidth = 2;
    private Color _progressBackColor = Color.Black;
    private Color _textColor = Color.White;
    private ContentAlignment _textAlignment = ContentAlignment.MiddleCenter;
    private Point _textMargin = new(1, 1);
    private Font _textFont = new("Consolas", 10f, FontStyle.Bold);
    private string _text = "READY";
    private bool _indeterminate;
    private int _minimum;
    private int _maximum = 100;
    private int _value;

    public aProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Black;
        Size = new Size(310, 30);
        _animationTimer.Tick += (_, _) =>
        {
            _sweep = (_sweep + SweepStep) % 1.0;
            Invalidate();
        };
    }

    [Browsable(true)]
    [Category("Behavior")]
    [DefaultValue(0)]
    public int Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < _minimum) _maximum = _minimum;
            _value = Math.Clamp(_value, _minimum, _maximum);
            Invalidate();
        }
    }

    [Browsable(true)]
    [Category("Behavior")]
    [DefaultValue(100)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(_minimum, value);
            _value = Math.Clamp(_value, _minimum, _maximum);
            Invalidate();
        }
    }

    [Browsable(true)]
    [Category("Behavior")]
    [DefaultValue(0)]
    public int Value
    {
        get => _value;
        set { int adjusted = Math.Clamp(value, Minimum, Maximum); if (_value != adjusted) { _value = adjusted; Invalidate(); } }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Indeterminate
    {
        get => _indeterminate;
        set
        {
            if (_indeterminate == value) return;
            _indeterminate = value;
            _sweep = 0;
            if (value) _animationTimer.Start(); else _animationTimer.Stop();
            Invalidate();
        }
    }

    [Browsable(true)]
    [Category("Appearance")]
    [DefaultValue(ContentAlignment.MiddleCenter)]
    public ContentAlignment TextAlignment
    {
        get => _textAlignment;
        set { if (_textAlignment != value) { _textAlignment = value; Invalidate(); } }
    }

    [Browsable(true)]
    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Font TextFont
    {
        get => _textFont;
        set { if (_textFont != value) { _textFont = value ?? SystemFonts.DefaultFont; Invalidate(); } }
    }

    [Browsable(true)]
    [Category("Appearance")]
    [DefaultValue(typeof(Color), "White")]
    public Color TextColor
    {
        get => _textColor;
        set { if (_textColor != value) { _textColor = value; Invalidate(); } }
    }

    [Browsable(true)]
    [Category("Layout")]
    [DefaultValue(typeof(Point), "1, 1")]
    public Point TextMargin
    {
        get => _textMargin;
        set { if (_textMargin != value) { _textMargin = value; Invalidate(); } }
    }

    [Browsable(true)]
    [Category("Appearance")]
    [DefaultValue("READY")]
    [AllowNull]
    public override string Text
    {
        get => _text;
        set { string adjusted = value ?? string.Empty; if (_text != adjusted) { _text = adjusted; Invalidate(); } }
    }

    [Browsable(true)]
    [Category("Progress Gradient")]
    [DefaultValue(typeof(Color), "14, 119, 166")]
    public Color GradientColor1 { get => _gradientColor1; set { if (_gradientColor1 != value) { _gradientColor1 = value; Invalidate(); } } }

    [Browsable(true)]
    [Category("Progress Gradient")]
    [DefaultValue(typeof(Color), "29, 206, 235")]
    public Color GradientColor2 { get => _gradientColor2; set { if (_gradientColor2 != value) { _gradientColor2 = value; Invalidate(); } } }

    [Browsable(true)]
    [Category("Progress Gradient")]
    [DefaultValue(typeof(Color), "115, 245, 255")]
    public Color GradientColor3 { get => _gradientColor3; set { if (_gradientColor3 != value) { _gradientColor3 = value; Invalidate(); } } }

    [Browsable(true)]
    [Category("Background Gradient")]
    [DefaultValue(typeof(Color), "12, 21, 31")]
    public Color BackGradientColor1 { get => _backGradientColor1; set { if (_backGradientColor1 != value) { _backGradientColor1 = value; Invalidate(); } } }

    [Browsable(true)]
    [Category("Background Gradient")]
    [DefaultValue(typeof(Color), "22, 35, 49")]
    public Color BackGradientColor2 { get => _backGradientColor2; set { if (_backGradientColor2 != value) { _backGradientColor2 = value; Invalidate(); } } }

    [Browsable(true)]
    [Category("Background Gradient")]
    [DefaultValue(typeof(Color), "8, 15, 24")]
    public Color BackGradientColor3 { get => _backGradientColor3; set { if (_backGradientColor3 != value) { _backGradientColor3 = value; Invalidate(); } } }

    [Browsable(true)]
    [Category("Appearance")]
    [DefaultValue(LinearGradientMode.Horizontal)]
    public LinearGradientMode GradientMode { get => _gradientMode; set { if (_gradientMode != value) { _gradientMode = value; Invalidate(); } } }

    [Browsable(true)]
    [Category("Appearance")]
    [DefaultValue(typeof(Color), "61, 89, 112")]
    public Color BorderColor { get => _borderColor; set { if (_borderColor != value) { _borderColor = value; Invalidate(); } } }

    [Browsable(true)]
    [Category("Appearance")]
    [DefaultValue(2)]
    public int BorderWidth { get => _borderWidth; set { int adjusted = Math.Max(0, value); if (_borderWidth != adjusted) { _borderWidth = adjusted; Invalidate(); } } }

    [Browsable(true)]
    [Category("Background Gradient")]
    [DefaultValue(typeof(Color), "12, 21, 31")]
    public Color ProgressBackColor
    {
        get => _progressBackColor;
        set
        {
            if (_progressBackColor == value) return;
            _progressBackColor = value;
            _backGradientColor1 = value;
            _backGradientColor2 = value;
            _backGradientColor3 = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Rectangle rect = ClientRectangle;
        if (rect.Width <= 2 || rect.Height <= 2) return;
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        Rectangle inner = Rectangle.Inflate(rect, -_borderWidth, -_borderWidth);
        if (inner.Width <= 0 || inner.Height <= 0) return;
        using (var background = CreateGradient(inner, BackGradientColor1, BackGradientColor2, BackGradientColor3))
            g.FillRectangle(background, inner);

        int fillRight = inner.Left;
        if (_indeterminate)
        {
            DrawSweep(g, inner);
        }
        else if (_maximum > _minimum && _value > _minimum)
        {
            float fraction = Math.Clamp((float)(_value - _minimum) / (_maximum - _minimum), 0f, 1f);
            int width = (int)Math.Round(inner.Width * fraction);
            if (width > 0)
            {
                Rectangle fill = new(inner.Left, inner.Top, Math.Clamp(width, 1, inner.Width), inner.Height);
                using var progress = CreateGradient(fill, GradientColor1, GradientColor2, GradientColor3);
                g.FillRectangle(progress, fill);
                fillRight = fill.Right;
                DrawBevel(g, fill);
                using var sheen = new LinearGradientBrush(
                    new Rectangle(fill.Left, fill.Top, fill.Width, Math.Max(2, fill.Height / 2)),
                    Color.FromArgb(100, Color.White), Color.FromArgb(0, Color.White), LinearGradientMode.Vertical);
                g.FillRectangle(sheen, fill.Left + 1, fill.Top + 1, Math.Max(0, fill.Width - 2), Math.Max(1, fill.Height / 2));
            }
        }

        // The trough is framed whether or not there is a fill, so the bar does not change outline
        // as soon as the first percent arrives.
        DrawFrame(g, rect);
        if (!string.IsNullOrEmpty(_text)) DrawLabel(g, rect, fillRight);
    }

    /// <summary>
    /// A soft band that travels left to right while the bar is indeterminate, so waiting for the
    /// first percentage reads as activity rather than as a stuck zero.
    /// </summary>
    private void DrawSweep(Graphics graphics, Rectangle inner)
    {
        int band = Math.Max(24, inner.Width / 4);
        int travel = inner.Width + band;
        int left = inner.Left - band + (int)(travel * _sweep);
        Rectangle rect = Rectangle.Intersect(new Rectangle(left, inner.Top, band, inner.Height), inner);
        if (rect.Width <= 0) return;

        using var brush = new LinearGradientBrush(
            new Rectangle(left, inner.Top, band, inner.Height),
            Color.Transparent,
            Color.Transparent,
            LinearGradientMode.Horizontal);
        brush.InterpolationColors = new ColorBlend
        {
            Colors = new[] { Color.Transparent, _gradientColor2, Color.Transparent },
            Positions = new[] { 0f, 0.5f, 1f }
        };
        graphics.FillRectangle(brush, rect);
    }

    private void DrawBevel(Graphics graphics, Rectangle rectangle)
    {
        if (rectangle.Width < 2 || rectangle.Height < 2) return;
        using var top = new Pen(Color.FromArgb(175, 230, 250, 255));
        using var bottom = new Pen(Color.FromArgb(140, 0, 20, 36));
        graphics.DrawLine(top, rectangle.Left, rectangle.Top, rectangle.Right - 1, rectangle.Top);
        graphics.DrawLine(top, rectangle.Left, rectangle.Top, rectangle.Left, rectangle.Bottom - 1);
        graphics.DrawLine(bottom, rectangle.Left, rectangle.Bottom - 1, rectangle.Right - 1, rectangle.Bottom - 1);
        graphics.DrawLine(bottom, rectangle.Right - 1, rectangle.Top, rectangle.Right - 1, rectangle.Bottom - 1);
    }

    private void DrawFrame(Graphics graphics, Rectangle rectangle)
    {
        using var dark = new Pen(Color.FromArgb(230, 2, 7, 12), 1f);
        graphics.DrawRectangle(dark, rectangle.Left, rectangle.Top, rectangle.Width - 1, rectangle.Height - 1);
        if (_borderWidth > 1)
        {
            using var edge = new Pen(_borderColor, 1f);
            graphics.DrawRectangle(edge, rectangle.Left + 1, rectangle.Top + 1,
                Math.Max(1, rectangle.Width - 3), Math.Max(1, rectangle.Height - 3));
        }
    }

    /// <summary>
    /// Draws the caption twice against complementary backgrounds: dark ink where the fill has
    /// reached, light text over the empty trough, so the number stays legible wherever the fill
    /// edge lands under it.
    /// </summary>
    private void DrawLabel(Graphics graphics, Rectangle rectangle, int fillRight)
    {
        using var format = new StringFormat
        {
            Alignment = ToHorizontalAlignment(_textAlignment),
            LineAlignment = ToVerticalAlignment(_textAlignment),
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        var bounds = new RectangleF(rectangle.X + 5, rectangle.Y + 1, Math.Max(0, rectangle.Width - 10), Math.Max(0, rectangle.Height - 2));

        if (fillRight > rectangle.Left)
        {
            int clipRight = Math.Min(fillRight, rectangle.Right);
            GraphicsState state = graphics.Save();
            graphics.SetClip(
                Rectangle.FromLTRB(rectangle.Left, rectangle.Top, clipRight, rectangle.Bottom),
                CombineMode.Intersect);
            using (var ink = new SolidBrush(Color.FromArgb(4, 18, 24)))
            {
                graphics.DrawString(_text, _textFont, ink, bounds, format);
            }

            graphics.Restore(state);
        }

        if (fillRight >= rectangle.Right) return;

        GraphicsState rest = graphics.Save();
        graphics.SetClip(
            Rectangle.FromLTRB(Math.Max(fillRight, rectangle.Left), rectangle.Top, rectangle.Right, rectangle.Bottom),
            CombineMode.Intersect);
        using (var shadow = new SolidBrush(Color.FromArgb(190, 0, 7, 12)))
        using (var text = new SolidBrush(_textColor))
        {
            graphics.DrawString(_text, _textFont, shadow, new RectangleF(bounds.X + 1, bounds.Y + 1, bounds.Width, bounds.Height), format);
            graphics.DrawString(_text, _textFont, text, bounds, format);
        }

        graphics.Restore(rest);
    }

    private LinearGradientBrush CreateGradient(Rectangle rectangle, Color first, Color middle, Color last)
    {
        var brush = new LinearGradientBrush(rectangle, first, last, _gradientMode);
        brush.InterpolationColors = new ColorBlend
        {
            Colors = new[] { first, middle, last },
            Positions = new[] { 0f, 0.5f, 1f }
        };
        return brush;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Stop();
            _animationTimer.Dispose();
            if (_textFont != SystemFonts.DefaultFont) _textFont.Dispose();
        }
        base.Dispose(disposing);
    }

    private static StringAlignment ToHorizontalAlignment(ContentAlignment alignment) => alignment switch
    {
        ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => StringAlignment.Center,
        ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => StringAlignment.Far,
        _ => StringAlignment.Near
    };

    private static StringAlignment ToVerticalAlignment(ContentAlignment alignment) => alignment switch
    {
        ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight => StringAlignment.Center,
        ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => StringAlignment.Far,
        _ => StringAlignment.Near
    };
}
