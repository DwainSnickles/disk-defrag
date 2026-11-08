using System.Globalization;
using System.Runtime.InteropServices;

namespace DiskDefrag;

internal sealed record VolumeInfo(string Letter, string Label, string FileSystem, string Type, ulong Total, ulong Free)
{
    public double UsedPercent => Total == 0 ? 0 : (Total - Free) * 100.0 / Total;

    public string Display => $"{Letter}:";
}

/// <summary>A downsamplable copy of the volume allocation bitmap, one bit per cluster.</summary>
internal sealed class ClusterMapData
{
    public ClusterMapData(byte[] bits, long clusters, int bytesPerCluster, string note, string letter)
    {
        Bits = bits;
        Clusters = clusters;
        BytesPerCluster = bytesPerCluster;
        Note = note;
        Letter = letter;
    }

    public byte[] Bits { get; }

    public long Clusters { get; }

    public int BytesPerCluster { get; }

    public string Note { get; }

    public string Letter { get; }
}

internal static class VolumeService
{
    private const uint GenericRead = 0x80000000u;
    private const uint ShareRead = 0x00000001u;
    private const uint ShareWrite = 0x00000002u;
    private const uint OpenExisting = 3u;
    private const uint FsctlGetNtfsVolumeData = 0x00090064u;
    private const uint FsctlGetVolumeBitmap = 0x0009006Fu;
    private const int ErrorMoreData = 234;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr security,
        uint creation,
        uint flags,
        IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        IntPtr device,
        uint controlCode,
        IntPtr input,
        int inputSize,
        IntPtr output,
        int outputSize,
        out int returned,
        IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    public static IReadOnlyList<VolumeInfo> Load()
    {
        var volumes = new List<VolumeInfo>();

        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                if (!drive.IsReady) continue;

                volumes.Add(new VolumeInfo(
                    drive.Name.TrimEnd('\\').TrimEnd(':'),
                    drive.VolumeLabel,
                    drive.DriveFormat,
                    drive.DriveType.ToString(),
                    (ulong)drive.TotalSize,
                    (ulong)drive.AvailableFreeSpace));
            }
            catch
            {
                // A drive that vanished between enumeration and inspection is simply skipped.
            }
        }

        return volumes.OrderBy(volume => volume.Letter, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Reads FSCTL_GET_VOLUME_BITMAP: one bit per cluster, set when the cluster is allocated.
    /// Needs an elevated session, so callers must handle the failure message.
    /// </summary>
    public static (ClusterMapData? Map, string Message) ReadBitmap(string letter)
    {
        IntPtr device = CreateFileW(
            $@"\\.\{letter}:",
            GenericRead,
            ShareRead | ShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (device == new IntPtr(-1))
        {
            return (null, DescribeFailure("the volume could not be opened"));
        }

        IntPtr input = IntPtr.Zero;
        IntPtr output = IntPtr.Zero;

        try
        {
            if (!TryNtfsLayout(device, out long clusters, out int bytesPerCluster, out string layoutNote))
            {
                return (null, layoutNote);
            }

            long bitmapBytes = (clusters + 7) / 8;
            if (bitmapBytes <= 0) return (null, "The volume reports no clusters to map.");
            if (bitmapBytes > 96L * 1024 * 1024) return (null, "The volume bitmap is too large to draw.");

            int capacity = checked((int)bitmapBytes);
            int attempts = 0;
            bool ok = false;
            int error = 0;

            while (attempts++ < 3)
            {
                input = Marshal.AllocHGlobal(16);
                Marshal.WriteInt64(input, 0, 0);
                Marshal.WriteInt64(input, 8, bitmapBytes);

                output = Marshal.AllocHGlobal(capacity + 16);

                ok = DeviceIoControl(device, FsctlGetVolumeBitmap, input, 16, output, capacity + 16, out _, IntPtr.Zero);
                if (ok) break;

                error = Marshal.GetLastWin32Error();

                Marshal.FreeHGlobal(input);
                Marshal.FreeHGlobal(output);
                input = IntPtr.Zero;
                output = IntPtr.Zero;

                if (error != ErrorMoreData) break;
                capacity = checked(capacity * 2);
                bitmapBytes = capacity;
            }

            if (!ok)
            {
                return (null, DescribeFailure("the cluster bitmap could not be read"));
            }

            long reported = Marshal.ReadInt64(output, 8);
            int length = (int)Math.Min(capacity, Math.Max(0, reported));
            var bits = new byte[length];
            Marshal.Copy(new IntPtr(output.ToInt64() + 16), bits, 0, length);

            return (new ClusterMapData(bits, clusters, bytesPerCluster, layoutNote, letter), string.Empty);
        }
        catch (Exception ex)
        {
            return (null, $"The cluster map failed: {ex.Message}");
        }
        finally
        {
            if (input != IntPtr.Zero) Marshal.FreeHGlobal(input);
            if (output != IntPtr.Zero) Marshal.FreeHGlobal(output);
            CloseHandle(device);
        }
    }

    private static bool TryNtfsLayout(IntPtr device, out long clusters, out int bytesPerCluster, out string note)
    {
        clusters = 0;
        bytesPerCluster = 4096;
        note = string.Empty;

        IntPtr buffer = Marshal.AllocHGlobal(1024);
        try
        {
            bool ok = DeviceIoControl(device, FsctlGetNtfsVolumeData, IntPtr.Zero, 0, buffer, 1024, out _, IntPtr.Zero);

            if (ok)
            {
                clusters = Marshal.ReadInt64(buffer, 16);
                bytesPerCluster = Marshal.ReadInt32(buffer, 40);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        if (clusters <= 0 || bytesPerCluster <= 0)
        {
            note = "Only NTFS volumes expose a cluster map.";
            return false;
        }

        note = $"{clusters.ToString("N0", CultureInfo.InvariantCulture)} clusters  \u2022  " +
               $"{bytesPerCluster.ToString("N0", CultureInfo.InvariantCulture)} bytes each";

        return true;
    }

    private static string DescribeFailure(string what)
    {
        int code = Marshal.GetLastWin32Error();

        if (code is 5 or 32)
        {
            return $"{what}: run Disk Defrag as administrator to read the volume bitmap.";
        }

        return $"{what} (error {code}).";
    }
}
