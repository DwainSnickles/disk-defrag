using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace DiskDefrag;

internal enum DefragMode
{
    Analyze,
    Defragment,
    Optimize,
}

/// <summary>
/// One defrag.exe invocation. Windows owns the engine; this wraps it so the tool can stream the
/// output, report the exit code and stop the job cleanly.
/// </summary>
internal sealed class DefragJob : IDisposable
{
    private static readonly Encoding Oem;

    private readonly Process _process;

    static DefragJob()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Oem = ResolveOem();
    }

    private DefragJob(Process process)
    {
        _process = process;

        _process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) Line?.Invoke(e.Data);
        };

        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) Line?.Invoke(e.Data);
        };

        _process.Exited += (_, _) => Exited?.Invoke(_process.ExitCode);
    }

    public event Action<string>? Line;

    public event Action<int>? Exited;

    public static string ArgumentsFor(string letter, DefragMode mode) => mode switch
    {
        DefragMode.Analyze => $"\"{letter}:\" /A /V",
        DefragMode.Defragment => $"\"{letter}:\" /D /U /V",
        _ => $"\"{letter}:\" /O /U /V",
    };

    public static string VerbFor(DefragMode mode) => mode switch
    {
        DefragMode.Analyze => "Analysing",
        DefragMode.Defragment => "Defragmenting",
        _ => "Optimizing",
    };

    public static DefragJob Start(string letter, DefragMode mode)
    {
        var info = new ProcessStartInfo("defrag.exe", ArgumentsFor(letter, mode))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            StandardOutputEncoding = Oem,
            StandardErrorEncoding = Oem,
        };

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        if (!process.Start()) throw new InvalidOperationException("defrag.exe could not be started.");

        var job = new DefragJob(process);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return job;
    }

    public static bool IsRunning => Process.GetProcessesByName("defrag").Length > 0;

    public void Stop()
    {
        try
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // The process already finished; nothing to stop.
        }
    }

    public void Dispose()
    {
        try
        {
            _process.Dispose();
        }
        catch
        {
            // Disposal is best effort.
        }
    }

    private static Encoding ResolveOem()
    {
        try
        {
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }
}
