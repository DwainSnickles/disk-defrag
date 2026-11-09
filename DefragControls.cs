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
    private const int LegendHeight = 24;

    // WCAG minimums: graphical objects 3:1, body text 4.5:1 against their own surface.
    private const double SwatchMinimum = 3.0;
    private const double LabelMinimum = 4.5;
    private const int AxisHeight = 20;
    private const int MaxMovers = 24;

    // Flat, high-contrast tones. Blending green through a ramp made neighbouring cells
    // differ by a few RGB points, which read as one blur rather than as squares.
    private static readonly Color FreeTone = Color.FromArgb(26, 54, 92);
    // Five discrete fill steps: enough to read how full a cell is, few enough that
    // neighbouring cells stay distinct squares instead of merging into a ramp.
    private static readonly Color[] UsedTones =
    {
        Color.FromArgb(21, 90, 61),
        Color.FromArgb(25, 115, 78),
        Color.FromArgb(31, 145, 96),
        Color.FromArgb(39, 184, 114),
        DefragTheme.Good,
    };
    private static readonly Color FragTone = Color.FromArgb(116, 22, 34);
    private static readonly Color FragSoftTone = Color.FromArgb(150, 44, 58);

    private readonly ToolTip _tips = new();
    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 40 };
    private readonly List<Mover> _movers = new();

    private ClusterMapData? _data;
    private string _message = "Select a volume to render its cluster map.";
    private double[]? _sample;
    private double[]? _previous;
    private sbyte[]? _delta;
    private double[]? _scatter;   // 0 = one long run, 1 = alternating used/free, per cell
    private int _startPercent;
    private bool _snapshotPending;
    private float[]? _pulse;
    private int _columns;
    private int _rows;
    private bool _active;
    private float _sweep;
    private int _hoverIndex = -1;
    private Rectangle _cells;      // the grid itself, snapped to whole pixels
    private string _analysis = string.Empty;   // defrag counters, drawn above the grid
    private int _cell = 5;         // integer pitch: fractional pitches render blurred

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
        _anim.Tick += (_, _) => Advance();
    }

    /// <summary>Analysis counters from defrag (fragmented files, percentage).</summary>
    public void SetAnalysis(string text)
    {
        if (_analysis == text) return;

        _analysis = text;
        Invalidate();
    }

    public string Note => _data?.Note ?? _message;

    public void ShowMessage(string message)
    {
        _data = null;
        _sample = null;
        _previous = null;
        _delta = null;
        _pulse = null;
        _movers.Clear();
        _message = message;
        Invalidate();
    }

    public void SetData(ClusterMapData data)
    {
        if (_data is null || _data.Letter != data.Letter)
        {
            _startPercent = 0;
            _scatter = null;
            _snapshotPending = false;
            _previous = null;
            _delta = null;
            _pulse = null;
            _movers.Clear();
        }

        _data = data;
        
        _sample = null;   // resample against the grid that is on screen now
        Invalidate();
    }

    /// <summary>defrag.exe started: keep frames coming so the sweep and movers advance.</summary>
    public void BeginActivity()
    {
        _active = true;
        _snapshotPending = true;   // the pre-run fragmentation is captured on the next paint
        _sweep = 0f;
        _anim.Start();
        Invalidate();
    }

    public void EndActivity()
    {
        _active = false;
        _movers.Clear();
        _anim.Stop();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 160 || Height < 110) return;

        Graphics g = e.Graphics;

        bool hasAnalysis = !string.IsNullOrEmpty(_analysis);
        int top = hasAnalysis ? 28 : 10;

        var area = new Rectangle(
            12,
            top,
            Math.Max(40, Width - 24),
            Math.Max(40, Height - top - LegendHeight - AxisHeight - 4));

        _cells = area;

        if (hasAnalysis)
        {
            TextRenderer.DrawText(
                g, _analysis, DefragTheme.Kicker,
                new Rectangle(12, 9, Math.Max(20, Width - 24), 16),
                DefragTheme.Text,
                TextFormatFlags.Right | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        if (_data is null)
        {
            TextRenderer.DrawText(
                g, _message, DefragTheme.MonoSmall, area, DefragTheme.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            return;
        }

        // Whole-pixel cells with a one-pixel gutter. Fractional pitch and size were what
        // made the grid read as a blur rather than as squares.
        int cell = Math.Clamp((int)Math.Round(area.Width / 150.0), 4, 10);
        int columns = Math.Max(8, area.Width / cell);
        int rows = Math.Max(6, area.Height / cell);

        if (_sample is null || _columns != columns || _rows != rows) Resample(columns, rows);
        UpdateDelta();
        
        if (_snapshotPending && _scatter is not null)
        {
            _startPercent = ScatterPercent(_scatter);
            _snapshotPending = false;
        }

        _cell = cell;
        _cells = new Rectangle(
            area.X + ((area.Width - (columns * cell)) / 2),
            area.Y + ((area.Height - (rows * cell)) / 2),
            columns * cell,
            rows * cell);

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

        Color free = FreeTone;
        int block = cell >= 4 ? cell - 1 : cell;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int index = (row * columns) + column;
                Color tone = ToneFor(index, _sample![index], free);

                using var brush = new SolidBrush(tone);
                g.FillRectangle(brush, _cells.X + (column * cell), _cells.Y + (row * cell), block, block);
            }
        }

        if (_active) PaintSweep(g, _cells);
        PaintMovers(g, _cells);
        PaintHover(g, _cells);
        PaintAxis(g, _cells);
        PaintLegend(g);
    }

    private void Resample(int columns, int rows)
    {
        var sample = new double[columns * rows];
        ClusterMapData data = _data!;
        long perCell = Math.Max(1, (data.Clusters + sample.Length - 1) / sample.Length);

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

        if (_columns != columns || _rows != rows || _previous is null || _previous.Length != sample.Length)
        {
            _previous = null;
            _pulse = new float[sample.Length];
        }

        _pulse ??= new float[sample.Length];
        _columns = columns;
        _rows = rows;
        _sample = sample;
        
        // Fragmentation pass: one scatter value per cell, from the real allocation bitmap.
        var scatter = new double[sample.Length];
        
        for (int cell = 0; cell < scatter.Length; cell++)
        {
            long first = cell * perCell;
            if (first >= data.Clusters) continue;
        
            long last = Math.Min(data.Clusters, first + perCell);
            scatter[cell] = ScatterAmount(first, last);
        }
        
        _scatter = scatter;
    }

    /// <summary>Difference from the previous frame: what defrag moved, and where.</summary>
    private void UpdateDelta()
    {
        if (_sample is null)
        {
            _delta = null;
            return;
        }

        if (_previous is null || _previous.Length != _sample.Length)
        {
            _delta = new sbyte[_sample.Length];
            _previous = (double[])_sample.Clone();
            return;
        }

        var delta = new sbyte[_sample.Length];
        var freed = new List<int>();
        var gained = new List<int>();
        _pulse ??= new float[_sample.Length];

        for (int index = 0; index < _sample.Length; index++)
        {
            double difference = _sample[index] - _previous[index];

            if (difference > 0.04)
            {
                delta[index] = 1;
                gained.Add(index);
                _pulse[index] = 1f;
            }
            else if (difference < -0.04)
            {
                delta[index] = -1;
                freed.Add(index);
                _pulse[index] = 1f;
            }
        }

        _delta = delta;
        _previous = (double[])_sample.Clone();

        if (_active && freed.Count > 0 && gained.Count > 0) SpawnMovers(freed, gained);
    }

    /// <summary>
    /// One block in flight per reallocated pair: a freed cell and a newly written cell are the
    /// same data moving, so the animation pairs them instead of inventing motion.
    /// </summary>
    private void SpawnMovers(List<int> freed, List<int> gained)
    {
        int pairs = Math.Min(MaxMovers, Math.Min(freed.Count, gained.Count));
        if (pairs <= 0) return;

        _movers.Clear();

        for (int i = 0; i < pairs; i++)
        {
            int from = freed[i * freed.Count / pairs];
            int to = gained[i * gained.Count / pairs];
            _movers.Add(new Mover(from, to));
        }

        if (!_anim.Enabled) _anim.Start();
    }

    private void Advance()
    {
        bool dirty = false;

        if (_pulse is not null)
        {
            for (int i = 0; i < _pulse.Length; i++)
            {
                if (_pulse[i] <= 0f) continue;
                _pulse[i] = Math.Max(0f, _pulse[i] - 0.045f);
                dirty = true;
            }
        }

        if (_active)
        {
            _sweep += 0.012f;
            if (_sweep > 1f) _sweep = 0f;
            dirty = true;
        }

        for (int i = _movers.Count - 1; i >= 0; i--)
        {
            _movers[i].Progress += 0.07f;
            if (_movers[i].Progress >= 1f) _movers.RemoveAt(i);
            dirty = true;
        }

        if (dirty) Invalidate();
        else if (!_active) _anim.Stop();
    }

    private Color ToneFor(int index, double used, Color free)
    {
        if (_pulse is not null && _pulse[index] > 0.01f)
        {
            Color hot = _delta is not null && _delta[index] < 0 ? DefragTheme.Violet : DefragTheme.Accent;
            return Blend(ToneForUse(used, free), hot, _pulse[index] * 0.85);
        }

        if (_delta is not null && _delta[index] != 0)
        {
            return _delta[index] > 0 ? DefragTheme.Accent : DefragTheme.Violet;
        }

        Color baseTone = ToneForUse(used, free);
        
        // Broken-up extents read red; as defrag consolidates them the tint drains away.
        if (_scatter is not null && index < _scatter.Length)
        {
            double amount = _scatter[index];

            // Two crisp steps instead of a wash: solid dark red where extents are chopped,
            // a darker half-tone where they are only mildly broken up.
            if (amount >= 0.5) return FragTone;

            if (amount >= 0.12) return FragSoftTone;
        }
        
        return baseTone;
    }

    private static Color ToneForUse(double used, Color free)
    {
        if (used <= 0.02) return free;

        // Buckets are 20% wide, so a cell lands on one of five fixed greens.
        int step = Math.Clamp((int)(used * UsedTones.Length), 0, UsedTones.Length - 1);

        return UsedTones[step];
    }

    private static Color Blend(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0.0, 1.0);

        return Color.FromArgb(
            (int)Math.Round(from.R + ((to.R - from.R) * amount)),
            (int)Math.Round(from.G + ((to.G - from.G) * amount)),
            (int)Math.Round(from.B + ((to.B - from.B) * amount)));
    }

    private void PaintSweep(Graphics g, Rectangle area)
    {
        const int Band = 10;
        int x = area.X + (int)(area.Width * _sweep);

        using (var band = new SolidBrush(Color.FromArgb(14, DefragTheme.Accent)))
        {
            g.FillRectangle(band, x - (Band / 2), area.Y, Band, area.Height);
        }

        using var pen = new Pen(DefragTheme.Alpha(DefragTheme.Accent, 140));
        g.DrawLine(pen, x, area.Y, x, area.Bottom);
    }

    private void PaintMovers(Graphics g, Rectangle grid)
    {
        if (_movers.Count == 0) return;

        int width = Math.Max(3, (int)(_cell * 1.6));

        foreach (Mover mover in _movers)
        {
            Point from = CellCenter(mover.From, grid);
            Point to = CellCenter(mover.To, grid);

            int x = (int)Math.Round(from.X + ((to.X - from.X) * mover.Progress));
            int y = (int)Math.Round(from.Y + ((to.Y - from.Y) * mover.Progress));

            using var brush = new SolidBrush(DefragTheme.Warn);
            g.FillRectangle(brush, x - (width / 2), y - (width / 2), width, width);
        }
    }

    private Point CellCenter(int index, Rectangle grid)
    {
        int column = index % Math.Max(1, _columns);
        int row = index / Math.Max(1, _columns);

        return new Point(grid.X + (column * _cell) + (_cell / 2), grid.Y + (row * _cell) + (_cell / 2));
    }

    private void PaintHover(Graphics g, Rectangle grid)
    {
        if (_hoverIndex < 0 || _columns <= 0 || _hoverIndex >= _columns * _rows) return;

        int column = _hoverIndex % _columns;
        int row = _hoverIndex / _columns;

        using var pen = new Pen(DefragTheme.Accent);
        g.DrawRectangle(pen, grid.X + (column * _cell), grid.Y + (row * _cell), Math.Max(1, _cell - 1), Math.Max(1, _cell - 1));
    }

    private void PaintAxis(Graphics g, Rectangle area)
    {
        ClusterMapData data = _data!;
        int lineY = Height - LegendHeight - AxisHeight + 4;

        using var pen = new Pen(DefragTheme.Border);
        g.DrawLine(pen, area.X, lineY, area.Right - 1, lineY);

        ulong total = (ulong)Math.Max(0L, data.Clusters) * (ulong)Math.Max(0, data.BytesPerCluster);

        for (int i = 0; i <= 4; i++)
        {
            int x = area.X + (int)Math.Round(area.Width * (i / 4.0));
            g.DrawLine(pen, x, lineY, x, lineY + 4);

            string label = DefragTheme.Bytes(total * (ulong)i / 4);
            int width = TextRenderer.MeasureText(label, DefragTheme.Kicker).Width;
            int left = Math.Clamp(x - (width / 2), area.X, Math.Max(area.X, area.Right - width));

            TextRenderer.DrawText(g, label, DefragTheme.Kicker, new Point(left, lineY + 4), DefragTheme.TextFaint, TextFormatFlags.NoPrefix);
        }
    }

    private void PaintLegend(Graphics g)
    {
        int y = Height - LegendHeight + 4;
        int x = 12;

        x = DrawKey(g, x, y, DefragTheme.Good, "USED");
        x = DrawKey(g, x, y, UsedTones[2], "PARTLY USED");
        x = DrawKey(g, x, y, FreeTone, "FREE");
        x = DrawKey(g, x, y, DefragTheme.Accent, "NEW DATA");
        x = DrawKey(g, x, y, DefragTheme.Violet, "FREED");
        x = DrawKey(g, x, y, DefragTheme.Warn, "MOVING");
        DrawKey(g, x, y, DefragTheme.Bad, "FRAGMENTED");

        string caption = _data?.Note ?? string.Empty;
        int now = ScatterPercent(_scatter);
        
        if (_active)
        {
            caption = "defrag running  \u2022  fragmented " + _startPercent + "% \u2192 " + now + "%";
        }
        else if (_startPercent > now && now > 0)
        {
            caption = "fragmented " + _startPercent + "% \u2192 " + now + "%  \u2022  " + caption;
        }

        TextRenderer.DrawText(
            g,
            DefragTheme.Truncate(caption, 60),
            DefragTheme.Kicker,
            new Rectangle(Math.Max(12, Width - 380), y, 368, 16),
            DefragTheme.TextDim,
            TextFormatFlags.Right | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }

    private static int DrawKey(Graphics g, int x, int y, Color tone, string label)
    {
        using (var brush = new SolidBrush(tone))
        {
            g.FillRectangle(brush, x, y + 3, 10, 10);
        }

        // A swatch that blends into the card on a bright panel keeps its exact grid tone,
        // so the legend never misreports a colour; it gains a lifted outline instead.
        if (ContrastRatio(tone, DefragTheme.Surface) < SwatchMinimum)
        {
            using var outline = new Pen(EnsureContrast(tone, DefragTheme.Surface, SwatchMinimum));
            g.DrawRectangle(outline, x, y + 3, 9, 9);
        }

        Color labelTone = EnsureContrast(DefragTheme.TextDim, DefragTheme.Surface, LabelMinimum);
        int width = TextRenderer.MeasureText(label, DefragTheme.Kicker).Width;
        TextRenderer.DrawText(g, label, DefragTheme.Kicker, new Point(x + 14, y), labelTone, TextFormatFlags.NoPrefix);

        return x + 14 + width + 16;
    }

    /// <summary>Lifts a tone away from the background until it clears the given contrast minimum.</summary>
    private static Color EnsureContrast(Color tone, Color background, double minimum)
    {
        if (ContrastRatio(tone, background) >= minimum) return tone;

        Color lifted = tone;

        for (int step = 0; step < 10; step++)
        {
            lifted = Blend(lifted, Color.White, 0.18);

            if (ContrastRatio(lifted, background) >= minimum) break;
        }

        return lifted;
    }

    /// <summary>WCAG 2.1 contrast ratio between two colours, 1.0 to 21.0.</summary>
    private static double ContrastRatio(Color first, Color second)
    {
        double a = RelativeLuminance(first);
        double b = RelativeLuminance(second);

        if (a < b) (a, b) = (b, a);

        return (a + 0.05) / (b + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        return (0.2126 * LinearChannel(color.R)) + (0.7152 * LinearChannel(color.G)) + (0.0722 * LinearChannel(color.B));
    }

    private static double LinearChannel(int value)
    {
        double channel = value / 255.0;

        return channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }

    /// <summary>1 when this span flips between used and free constantly, 0 when it forms long runs.</summary>
    private double ScatterAmount(long startCluster, long endCluster)
    {
        long span = endCluster - startCluster;
        if (span < 32) return 0;

        byte[] bits = _data!.Bits;
        long firstByte = Math.Max(0, startCluster / 8);
        long lastByte = Math.Min(bits.Length - 1, (endCluster - 1) / 8);
        if (lastByte < firstByte) return 0;

        int flips = 0;
        int previous = -1;

        for (long index = firstByte; index <= lastByte; index++)
        {
            int value = bits[index];
            flips += FlipsPerByte[value];

            if (previous >= 0 && ((previous >> 7) & 1) != (value & 1)) flips++;

            previous = value;
        }

        // Counted over the whole cell: on a real volume a cell spans tens of thousands of
        // clusters, so judging it from a leading window left almost everything green.
        double ratio = (double)flips / span;
        return Math.Clamp((ratio - 0.002) / 0.035, 0.0, 1.0);
    }

    private static int[] BuildFlips()
    {
        var table = new int[256];

        for (int value = 0; value < 256; value++)
        {
            int flips = 0;

            for (int bit = 0; bit < 7; bit++)
            {
                if (((value >> bit) & 1) != ((value >> (bit + 1)) & 1)) flips++;
            }

            table[value] = flips;
        }

        return table;
    }

    private static readonly int[] FlipsPerByte = BuildFlips();
    
        /// <summary>Share of the grid still broken into short extents, 0-100.</summary>
        private int ScatterPercent(double[]? scatter)
        {
            if (scatter is null || scatter.Length == 0) return 0;
    
            double total = 0;
            foreach (double value in scatter) total += value;
    
            return (int)Math.Round((total / scatter.Length) * 100);
        }
    
        private long CountAllocated(long startCluster, long endCluster)
    {
        byte[] bits = _data!.Bits;
        if (bits.Length == 0) return 0;

        long firstByte = Math.Max(0, startCluster / 8);
        long lastByte = Math.Min(bits.Length - 1, (endCluster - 1) / 8);
        long allocated = 0;

        for (long index = firstByte; index <= lastByte; index++)
        {
            byte value = bits[index];
            if (value != 0) allocated += BitOperations.PopCount(value);
        }

        return allocated;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        int index = HitTest(e.Location);
        if (index == _hoverIndex) return;

        _hoverIndex = index;

        if (index >= 0 && _data is not null && _sample is not null)
        {
            long perCell = Math.Max(1, (_data.Clusters + _sample.Length - 1) / _sample.Length);
            long first = index * perCell;
            long last = Math.Max(first, Math.Min(_data.Clusters, first + perCell) - 1);

            _tips.SetToolTip(this, $"clusters {first:N0}\u2013{last:N0}\n{_sample[index] * 100:0}% allocated");
        }
        else
        {
            _tips.SetToolTip(this, string.Empty);
        }

        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        _hoverIndex = -1;
        _tips.SetToolTip(this, string.Empty);
        Invalidate();
    }

    private int HitTest(Point point)
    {
        if (_data is null || _sample is null || _columns <= 0 || _rows <= 0) return -1;
        if (!_cells.Contains(point) || _cells.Width <= 0 || _cells.Height <= 0) return -1;

        if (_cell <= 0) return -1;

        int column = (point.X - _cells.X) / _cell;
        int row = (point.Y - _cells.Y) / _cell;

        if (column < 0 || column >= _columns || row < 0 || row >= _rows) return -1;
        return (row * _columns) + column;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _anim.Stop();
            _anim.Dispose();
            _tips.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class Mover
    {
        public Mover(int from, int to)
        {
            From = from;
            To = to;
        }

        public int From { get; }

        public int To { get; }

        public float Progress { get; set; }
    }
}
