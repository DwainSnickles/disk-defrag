using System.Diagnostics;
using System.Text;

namespace DiskDefrag;

internal enum DiskOperation { Analyze, Defragment, Optimize }

internal sealed class DefragEngine : IDisposable
{
    private readonly Process _process;
    private readonly AnalysisParser.StatsReader _stats = new();

    private DefragEngine(Process process) => _process = process;

    public event Action<string>? Output;
    public event Action<string, int>? Progress;
    public event Action<AnalysisStats>? Analysis;
    public event Action<int>? Exited;

    public static DefragEngine Create(string letter, DiskOperation operation)
    {
        string args = operation switch
        {
            DiskOperation.Analyze => $"\"{letter}:\" /A /V /U",
            DiskOperation.Defragment => $"\"{letter}:\" /D /V /U",
            _ => $"\"{letter}:\" /O /V /U",
        };

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding encoding;
        try { encoding = Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
        catch { encoding = Encoding.UTF8; }

        var start = new ProcessStartInfo("defrag.exe", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = encoding,
            StandardErrorEncoding = encoding,
        };
        var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        var engine = new DefragEngine(process);
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) engine.ReadLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) engine.ReadLine(e.Data); };
        process.Exited += (_, _) =>
        {
            try { process.WaitForExit(); } catch { }
            engine.Exited?.Invoke(process.ExitCode);
        };
        return engine;
    }

    public void StartProcess()
    {
        if (!_process.Start()) throw new InvalidOperationException("Windows defrag engine did not start.");
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    private void ReadLine(string line)
    {
        Output?.Invoke(line);

        if (AnalysisParser.TryReadProgress(line, out string phase, out int percent))
        {
            Progress?.Invoke(phase, percent);
        }

        if (_stats.Read(line))
        {
            Analysis?.Invoke(_stats.ToStats());
        }
    }

    public void Stop()
    {
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); } catch { }
    }

    public void Dispose() => _process.Dispose();
}
