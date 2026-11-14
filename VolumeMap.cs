using System.Runtime.InteropServices;

namespace DiskDefrag;

internal sealed record DriveRow(string Letter, string Label, string FileSystem, string Kind, long Total, long Free)
{
    public string Display => $"{Letter}:  {Label}  ({FileSystem}, {Kind})  {FormatBytes(Free)} free / {FormatBytes(Total)}";
    private static string FormatBytes(long value)
    {
        double amount = value;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int unit = 0;
        while (amount >= 1024 && unit < units.Length - 1) { amount /= 1024; unit++; }
        return $"{amount:0.##} {units[unit]}";
    }
}

internal static class VolumeMapReader
{
    private const uint GenericRead = 0x80000000;
    private const uint ShareRead = 1;
    private const uint ShareWrite = 2;
    private const uint OpenExisting = 3;
    private const uint FsctlGetNtfsVolumeData = 0x00090064;
    private const uint FsctlGetVolumeBitmap = 0x0009006F;
    private const int ErrorMoreData = 234;
    private const int MaxBitmapBytes = 96 * 1024 * 1024;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(IntPtr device, uint code, IntPtr input, int inputSize, IntPtr output, int outputSize, out int returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    public static IReadOnlyList<DriveRow> GetDrives()
    {
        var rows = new List<DriveRow>();
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                rows.Add(new DriveRow(drive.Name.TrimEnd('\\', ':'), drive.VolumeLabel, drive.DriveFormat,
                    drive.DriveType.ToString(), drive.TotalSize, drive.AvailableFreeSpace));
            }
            catch { }
        }
        return rows.OrderBy(row => row.Letter, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static (byte[]? Bits, string Message) Read(string letter)
    {
        IntPtr device = CreateFile($@"\\.\{letter.TrimEnd(':')}:", GenericRead, ShareRead | ShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (device == new IntPtr(-1)) return (null, ErrorText(Marshal.GetLastWin32Error()));
        IntPtr output = IntPtr.Zero;
        try
        {
            IntPtr geometry = Marshal.AllocHGlobal(1024);
            long clusters;
            try
            {
                if (!DeviceIoControl(device, FsctlGetNtfsVolumeData, IntPtr.Zero, 0, geometry, 1024, out _, IntPtr.Zero))
                    return (null, "Live cluster maps require an NTFS volume and administrator rights.");
                clusters = Marshal.ReadInt64(geometry, 16);
            }
            finally { Marshal.FreeHGlobal(geometry); }
            if (clusters <= 0) return (null, "The volume reports no clusters to map.");
            long bytes = (clusters + 7) / 8;
            if (bytes > MaxBitmapBytes) return (null, "This volume bitmap is too large to display safely.");
            int capacity = checked((int)bytes);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                IntPtr input = Marshal.AllocHGlobal(16);
                try
                {
                    Marshal.WriteInt64(input, 0, 0);
                    output = Marshal.AllocHGlobal(checked(capacity + 16));
                    if (DeviceIoControl(device, FsctlGetVolumeBitmap, input, 8, output, capacity + 16, out _, IntPtr.Zero))
                    {
                        int length = (int)Math.Min(capacity, Math.Max(0, Marshal.ReadInt64(output, 8) + 7) / 8);
                        var bits = new byte[length];
                        Marshal.Copy(IntPtr.Add(output, 16), bits, 0, length);
                        return (bits, string.Empty);
                    }
                    int error = Marshal.GetLastWin32Error();
                    Marshal.FreeHGlobal(output);
                    output = IntPtr.Zero;
                    if (error != ErrorMoreData) return (null, ErrorText(error));
                    capacity = Math.Min(MaxBitmapBytes, checked(capacity * 2));
                    if (capacity >= MaxBitmapBytes) return (null, "Volume bitmap exceeds the safe display limit.");
                }
                finally { Marshal.FreeHGlobal(input); }
            }
            return (null, "The volume bitmap changed repeatedly; try refreshing again.");
        }
        catch (Exception ex) { return (null, "Could not read volume bitmap: " + ex.Message); }
        finally
        {
            if (output != IntPtr.Zero) Marshal.FreeHGlobal(output);
            CloseHandle(device);
        }
    }

    private static string ErrorText(int code) => code switch
    {
        5 => "Access denied. Run as administrator to display the live map.",
        32 => "The volume is busy. Try again shortly.",
        _ => $"Windows could not read this volume bitmap (error {code})."
    };
}

internal sealed class ClusterMapControl : Control
{
    private byte[]? _bits;
    private string _message = "Select an NTFS volume to read its allocation map.";
    private const int SweepMilliseconds = 12000;   // one slow pass of the read head
    private int _mapPosition;
    private long _scanStarted;
    private bool _busy;

    public ClusterMapControl()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(10, 15, 23);
        ForeColor = Color.White;
        ResizeRedraw = true;
    }

    public void SetMap(byte[]? bits, string message, bool busy)
    {
        _bits = bits;
        _message = message;
        _busy = busy;
        _mapPosition = 0;
        _scanStarted = Environment.TickCount64;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);
        if (_bits is null || _bits.Length == 0)
        {
            TextRenderer.DrawText(g, _message, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        const int gap = 1;
        int cell = Math.Clamp(Width / 140, 3, 8);
        int columns = Math.Max(1, (Width - 24) / cell);
        int rows = Math.Max(1, (Height - 62) / cell);
        long cells = (long)columns * rows;
        long totalBits = (long)_bits.Length * 8;
        int left = (Width - columns * cell) / 2;
        int top = 22;
        using var free = new SolidBrush(Color.FromArgb(28, 65, 104));
        using var used = new SolidBrush(Color.FromArgb(36, 170, 112));
        using var moving = new SolidBrush(Color.FromArgb(255, 205, 60));
        for (long i = 0; i < cells; i++)
        {
            long bit = i * totalBits / cells;
            bool allocated = (_bits[bit >> 3] & (1 << (int)(bit & 7))) != 0;
            int x = left + (int)(i % columns) * cell;
            int y = top + (int)(i / columns) * cell;
            Brush brush = _busy && i >= _mapPosition && i < _mapPosition + 3 ? moving : allocated ? used : free;
            g.FillRectangle(brush, x, y, cell - gap, cell - gap);
        }
        if (_busy)
        {
            // The old defragmenter's read head: a thin white line crossing the map left to right,
            // timed rather than counted in frames, so its pace does not depend on how fast we repaint.
            int gridWidth = columns * cell;
            int gridHeight = rows * cell;
            long elapsed = Environment.TickCount64 - _scanStarted;
            int scanX = left + (int)(elapsed % SweepMilliseconds * Math.Max(1, gridWidth - 1) / SweepMilliseconds);
            using (var sweep = new Pen(Color.FromArgb(215, 255, 255, 255)))
            {
                g.DrawLine(sweep, scanX, top, scanX, top + gridHeight - 1);
            }
            _mapPosition = (_mapPosition + 3) % Math.Max(1, (int)Math.Min(cells, int.MaxValue));
            Invalidate();
        }
        TextRenderer.DrawText(g, "ALLOCATED", Font, new Point(12, Height - 28), Color.FromArgb(36, 170, 112), TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, "FREE", Font, new Point(120, Height - 28), Color.FromArgb(110, 158, 205), TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, _busy ? "YELLOW = ACTIVE MAP SCAN / RECENT REFRESH" : "LIVE NTFS VOLUME ALLOCATION MAP", Font,
            new Rectangle(220, Height - 28, Math.Max(0, Width - 232), 22), _busy ? Color.FromArgb(255, 205, 60) : Color.FromArgb(29, 206, 235),
            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }
}
