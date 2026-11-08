using System.Drawing.Drawing2D;
using System.Numerics;

namespace DiskDefrag;

/// <summary>Angular instrument button, same language as the PC Medic shell.</summary>
internal sealed class DarkButton : Control
{
    private bool _hover;
    private bool _down;

    public DarkButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Font = DefragTheme.UiBold;
        ForeColor = DefragTheme.Text;
        Cursor = Cursors.Hand;
        Size = new Size(130, 32);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Primary { get; set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color Tone { get; set; } = DefragTheme.Accent;

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _down = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _down = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _down = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 6 || Height < 6) return;

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using GraphicsPath path = DefragTheme.Round(rect, 7f);

        if (!Enabled)
        {
            using var disabledFill = new SolidBrush(DefragTheme.Surface);
            using var disabledEdge = new Pen(DefragTheme.BorderSoft);
            g.FillPath(disabledFill, path);
            g.DrawPath(disabledEdge, path);
            TextRenderer.DrawText(
                g, Text, Font, ClientRectangle, DefragTheme.TextFaint,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            return;
        }

        if (Primary)
        {
            Color top = _down ? ControlPaint.Dark(Tone, 0.12f) : _hover ? ControlPaint.Light(Tone, 0.16f) : Tone;

            using (var fill = new LinearGradientBrush(new RectangleF(0f, 0f, Width, Height), top, ControlPaint.Dark(top, 0.22f), 90f))
            {
                g.FillPath(fill, path);
            }

            TextRenderer.DrawText(
                g, Text, Font, ClientRectangle, Color.FromArgb(4, 18, 24),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            return;
        }

        using var body = new SolidBrush(_hover ? DefragTheme.Surface2 : Color.FromArgb(19, 24, 33));
        using var edge = new Pen(_hover ? DefragTheme.Alpha(Tone, 175) : DefragTheme.Border, 1f);
        g.FillPath(body, path);
        g.DrawPath(edge, path);
        TextRenderer.DrawText(
            g, Text, Font, ClientRectangle, _hover ? Tone : DefragTheme.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

/// <summary>
/// Operation progress strip: the running verb on the left, the engine's own phase and percentage on
/// the right, and a filling bar underneath. When defrag.exe reports no number the bar sweeps, so the
/// strip never invents a percentage it cannot back up.
/// </summary>
internal sealed class ProgressStrip : Control
{
    private readonly System.Windows.Forms.Timer _sweepTimer = new() { Interval = 40 };

    private string _caption = string.Empty;
    private string _phase = string.Empty;
    private int _percent = -1;
    private int _sweep;

    public ProgressStrip()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = DefragTheme.Surface;
        Visible = false;

        _sweepTimer.Tick += OnSweep;
    }

    /// <summary>Shows the strip for a new job: caption on the left, sweeping bar until a number arrives.</summary>
    public void Begin(string caption)
    {
        _caption = caption;
        _phase = string.Empty;
        _percent = -1;
        _sweep = 0;
        Visible = true;
        _sweepTimer.Start();
        Invalidate();
    }

    /// <summary>Changes the caption and falls back to the sweep, used when an operator stops the job.</summary>
    public void Relabel(string caption)
    {
        if (!Visible) return;

        _caption = caption;
        _phase = string.Empty;
        _percent = -1;
        _sweepTimer.Start();
        Invalidate();
    }

    /// <summary>Records one parsed progress line; an empty label keeps the phase already on screen.</summary>
    public void Report(string label, int percent)
    {
        if (!Visible) return;

        string cleaned = DefragTheme.Truncate(label.Trim().ToUpperInvariant(), 22);
        if (cleaned.Length > 0) _phase = cleaned;

        _percent = Math.Clamp(percent, 0, 100);

        // A real number beats the sweep: stop guessing once the engine says where it is.
        _sweepTimer.Stop();
        Invalidate();
    }

    public void Stop()
    {
        _sweepTimer.Stop();
        _phase = string.Empty;
        _percent = -1;
        Visible = false;
    }

    private void OnSweep(object? sender, EventArgs e)
    {
        _sweep = (_sweep + 5) % 100;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 120 || Height < 24) return;

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        const int BarHeight = 8;

        string right = _percent < 0
            ? (_phase.Length == 0 ? "WORKING" : $"{_phase}  \u00b7  WORKING")
            : (_phase.Length == 0 ? $"{_percent}%" : $"{_phase}  \u00b7  {_percent}%");

        TextRenderer.DrawText(
            g, _caption, DefragTheme.Kicker,
            new Rectangle(0, 1, Math.Max(40, Width - 220), 17),
            DefragTheme.TextDim,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        TextRenderer.DrawText(
            g, right, DefragTheme.MonoSmall,
            new Rectangle(Math.Max(0, Width - 218), 0, Math.Min(218, Width - 8), 18),
            _percent < 0 ? DefragTheme.TextDim : DefragTheme.Accent,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        var bar = new RectangleF(0f, Math.Max(20, Height - BarHeight - 6), Math.Max(40, Width), BarHeight);

        using (GraphicsPath track = DefragTheme.Round(bar, BarHeight / 2f))
        {
            using var fill = new SolidBrush(DefragTheme.Surface2);
            using var edge = new Pen(DefragTheme.Border);
            g.FillPath(fill, track);
            g.DrawPath(edge, track);
        }

        if (_percent > 0)
        {
            var filled = new RectangleF(bar.X, bar.Y, Math.Max(BarHeight, bar.Width * (_percent / 100f)), bar.Height);

            using GraphicsPath path = DefragTheme.Round(filled, BarHeight / 2f);
            using var brush = new LinearGradientBrush(
                filled,
                DefragTheme.Alpha(DefragTheme.Good, 110),
                DefragTheme.Good,
                LinearGradientMode.Horizontal);
            g.FillPath(brush, path);
        }
        else if (_percent < 0)
        {
            float segment = Math.Max(60f, bar.Width * 0.22f);
            float travel = bar.Width + segment;
            float x = bar.X - segment + (travel * (_sweep / 100f));

            RectangleF lit = RectangleF.Intersect(new RectangleF(x, bar.Y, segment, bar.Height), bar);

            if (lit.Width > 1f && lit.Height > 1f)
            {
                using GraphicsPath path = DefragTheme.Round(lit, BarHeight / 2f);
                using var brush = new LinearGradientBrush(
                    lit,
                    DefragTheme.Alpha(DefragTheme.Accent, 30),
                    DefragTheme.Accent,
                    LinearGradientMode.Horizontal);
                g.FillPath(brush, path);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _sweepTimer.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Volume table: one selectable row per mounted volume.</summary>
internal sealed class VolumeGrid : Control
{
    private const int HeaderHeight = 26;
    private const int RowHeight = 34;

    private IReadOnlyList<VolumeInfo> _volumes = Array.Empty<VolumeInfo>();
    private string? _selected;
    private int _hover = -1;

    public VolumeGrid()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Font = DefragTheme.MonoSmall;
    }

    public event Action<string>? SelectionChanged;

    public string? Selected => _selected;

    public void SetVolumes(IReadOnlyList<VolumeInfo> volumes)
    {
        _volumes = volumes;

        if (_selected is not null && volumes.All(volume => volume.Letter != _selected)) _selected = null;

        if (_selected is null && volumes.Count > 0)
        {
            _selected = volumes[0].Letter;
            SelectionChanged?.Invoke(_selected);
        }

        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int index = RowAt(e.Y);

        if (index != _hover)
        {
            _hover = index;
            Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = -1;
        Cursor = Cursors.Default;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int index = RowAt(e.Y);

        if (index >= 0)
        {
            _selected = _volumes[index].Letter;
            SelectionChanged?.Invoke(_selected);
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    private int RowAt(int y)
    {
        if (y < HeaderHeight) return -1;
        int index = (y - HeaderHeight) / RowHeight;
        return index >= 0 && index < _volumes.Count ? index : -1;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 120 || Height < 40) return;

        Graphics g = e.Graphics;

        string[] headers = { "VOLUME", "FILE SYSTEM", "TYPE", "TOTAL", "FREE", "USED", "STATE" };
        int[] columns = Columns();

        for (int i = 0; i < headers.Length; i++)
        {
            TextRenderer.DrawText(
                g, headers[i], DefragTheme.Kicker,
                new Rectangle(columns[i], 4, 120, HeaderHeight - 8),
                DefragTheme.TextDim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        using (var rule = new Pen(DefragTheme.BorderSoft))
        {
            g.DrawLine(rule, 0, HeaderHeight - 1, Width, HeaderHeight - 1);
        }

        for (int index = 0; index < _volumes.Count; index++)
        {
            VolumeInfo volume = _volumes[index];
            int y = HeaderHeight + (index * RowHeight);
            bool selected = volume.Letter == _selected;
            bool hovered = index == _hover;

            if (selected || hovered)
            {
                using var fill = new SolidBrush(selected ? DefragTheme.Alpha(DefragTheme.Accent, 22) : DefragTheme.Alpha(DefragTheme.Text, 8));
                g.FillRectangle(fill, 0, y, Width, RowHeight - 1);
            }

            var marker = new Rectangle(4, y + (RowHeight / 2) - 7, 14, 14);
            using (GraphicsPath path = DefragTheme.Round(marker, 3f))
            using (var border = new Pen(selected ? DefragTheme.Accent : DefragTheme.Border))
            {
                g.DrawPath(border, path);
            }

            if (selected)
            {
                using var fill = new SolidBrush(DefragTheme.Accent);
                g.FillRectangle(fill, marker.X + 4, marker.Y + 4, 6, 6);
            }

            Color tone = selected ? DefragTheme.Text : DefragTheme.TextDim;

            DrawCell(g, columns[0] + 18, y, $"{volume.Letter}:  {DefragTheme.Truncate(volume.Label, 18)}", tone);
            DrawCell(g, columns[1], y, volume.FileSystem, tone);
            DrawCell(g, columns[2], y, volume.Type, DefragTheme.TextDim);
            DrawCell(g, columns[3], y, DefragTheme.Bytes(volume.Total), tone);
            DrawCell(g, columns[4], y, DefragTheme.Bytes(volume.Free), tone);
            DrawCell(g, columns[5], y, $"{volume.UsedPercent:0}%", volume.UsedPercent > 90 ? DefragTheme.Bad : tone);
            DrawCell(g, columns[6], y, selected ? "SELECTED" : string.Empty, DefragTheme.Accent);

            using var separator = new Pen(DefragTheme.BorderSoft);
            g.DrawLine(separator, 0, y + RowHeight - 1, Width, y + RowHeight - 1);
        }
    }

    private int[] Columns()
    {
        // Column origins follow the available width, so the table stays readable when resized.
        int unit = Math.Max(60, (Width - 210) / 6);
        return new[] { 24, 24 + unit, 24 + (unit * 2), 24 + (unit * 3), 24 + (unit * 4), 24 + (unit * 5), 24 + (unit * 6) - 6 };
    }

    private static void DrawCell(Graphics g, int x, int y, string text, Color tone)
    {
        TextRenderer.DrawText(
            g, text, DefragTheme.MonoSmall,
            new Rectangle(x, y, Math.Max(40, 150), RowHeight - 1),
            tone,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>Cluster map: one sampled cell per slice of the volume allocation bitmap.</summary>
internal sealed class ClusterMapView : Control
{
    private ClusterMapData? _data;
    private string _message = "Select a volume to render its cluster map.";
    private double[]? _sample;
    private double[]? _previousSample;
    private sbyte[]? _delta;
    private int _columns;
    private int _rows;

    public ClusterMapView()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
    }

    public string Note => _data?.Note ?? _message;

    public void ShowMessage(string message)
    {
        _data = null;
        _sample = null;
        _previousSample = null;
        _delta = null;
        _message = message;
        Invalidate();
    }

    public void SetData(ClusterMapData data)
    {
        if (_data is null || _data.Letter != data.Letter)
        {
            _previousSample = null;
            _delta = null;
        }

        _data = data;
        _sample = null;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 80 || Height < 60) return;

        Graphics g = e.Graphics;
        var area = new Rectangle(12, 10, Math.Max(20, Width - 24), Math.Max(20, Height - 44));

        if (_data is null)
        {
            TextRenderer.DrawText(
                g, _message, DefragTheme.MonoSmall, area, DefragTheme.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            return;
        }

        int columns = Math.Max(16, area.Width / 6);
        int rows = Math.Max(8, area.Height / 6);
        EnsureSample(columns, rows);
        UpdateDelta();

        float size = Math.Min(area.Width / (float)columns, area.Height / (float)rows);
        float gap = size > 3.2f ? 1f : 0f;

        Color free = Color.FromArgb(22, 30, 40);
        Color partial = DefragTheme.Alpha(DefragTheme.Good, 110);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int index = (row * columns) + column;
                double used = _sample![index];
                Color tone;

                if (_delta is not null && _delta[index] != 0)
                {
                    tone = _delta[index] > 0 ? DefragTheme.Accent : DefragTheme.Violet;
                }
                else
                {
                    tone = used <= 0.001 ? free : used >= 0.99 ? DefragTheme.Good : partial;
                }

                using var brush = new SolidBrush(tone);
                g.FillRectangle(
                    brush,
                    area.X + (column * size),
                    area.Y + (row * size),
                    Math.Max(1f, size - gap),
                    Math.Max(1f, size - gap));
            }
        }

        PaintLegend(g);
    }

    private void EnsureSample(int columns, int rows)
    {
        if (_sample is not null && _columns == columns && _rows == rows) return;

        _columns = columns;
        _rows = rows;

        var sample = new double[columns * rows];
        ClusterMapData data = _data!;
        long perCell = Math.Max(8, data.Clusters / sample.Length);

        for (int cell = 0; cell < sample.Length; cell++)
        {
            long start = cell * perCell;
            if (start >= data.Clusters)
            {
                sample[cell] = 0;
                continue;
            }

            long end = Math.Min(data.Clusters, start + perCell);
            sample[cell] = (double)CountAllocated(start, end) / (end - start);
        }

        _sample = sample;
    }

    private void UpdateDelta()
    {
        if (_sample is null)
        {
            _delta = null;
            return;
        }

        if (_previousSample is null || _previousSample.Length != _sample.Length)
        {
            _delta = new sbyte[_sample.Length];
            _previousSample = (double[])_sample.Clone();
            return;
        }

        var delta = new sbyte[_sample.Length];

        for (int index = 0; index < _sample.Length; index++)
        {
            double difference = _sample[index] - _previousSample[index];
            delta[index] = difference > 0.04 ? (sbyte)1 : difference < -0.04 ? (sbyte)-1 : (sbyte)0;
        }

        _delta = delta;
        _previousSample = (double[])_sample.Clone();
    }

    private long CountAllocated(long startCluster, long endCluster)
    {
        byte[] bits = _data!.Bits;
        long firstByte = startCluster / 8;
        long lastByte = Math.Min(bits.Length - 1, (endCluster - 1) / 8);
        long allocated = 0;

        for (long index = firstByte; index <= lastByte; index++)
        {
            byte value = bits[index];
            if (value != 0) allocated += BitOperations.PopCount(value);
        }

        return allocated;
    }

    private void PaintLegend(Graphics g)
    {
        int y = Height - 26;
        int x = 12;

        x = DrawKey(g, x, y, DefragTheme.Good, "USED");
        x = DrawKey(g, x, y, DefragTheme.Alpha(DefragTheme.Good, 110), "PARTLY USED");
        x = DrawKey(g, x, y, Color.FromArgb(22, 30, 40), "FREE");
        x = DrawKey(g, x, y, DefragTheme.Accent, "NEW DATA");
        DrawKey(g, x, y, DefragTheme.Violet, "FREED");

        TextRenderer.DrawText(
            g,
            DefragTheme.Truncate(_data?.Note ?? string.Empty, 52),
            DefragTheme.MonoSmall,
            new Rectangle(Math.Max(12, Width - 340), y, 328, 16),
            DefragTheme.TextDim,
            TextFormatFlags.Right | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }

    private static int DrawKey(Graphics g, int x, int y, Color tone, string label)
    {
        using (var brush = new SolidBrush(tone))
        {
            g.FillRectangle(brush, x, y + 3, 10, 10);
        }

        int width = TextRenderer.MeasureText(label, DefragTheme.Kicker).Width;
        TextRenderer.DrawText(g, label, DefragTheme.Kicker, new Point(x + 14, y), DefragTheme.TextDim, TextFormatFlags.NoPrefix);

        return x + 14 + width + 16;
    }
}
