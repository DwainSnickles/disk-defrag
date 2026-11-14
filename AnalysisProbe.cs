using System.Diagnostics;
using System.Text;

namespace DiskDefrag;

/// <summary>Runs a read-only Windows analysis to sample the current fragmented-file count.</summary>
internal static class AnalysisProbe
{
    public static async Task<AnalysisStats?> RunAsync(
        string letter,
        CancellationToken cancellationToken,
        Action<string>? log = null,
        Action<int>? progress = null)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding encoding;
        try { encoding = Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
        catch { encoding = Encoding.UTF8; }

        var start = new ProcessStartInfo("defrag.exe", $"\"{letter}:\" /A /V")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = encoding,
            StandardErrorEncoding = encoding,
        };
        using var process = new Process { StartInfo = start };
        var output = new List<string>();
        void HandleOutput(string? line)
        {
            if (line is null) return;

            lock (output) output.Add(line);

            log?.Invoke("[AnalysisProbe] " + line);
        }

        process.OutputDataReceived += (_, e) => HandleOutput(e.Data);
        process.ErrorDataReceived += (_, e) => HandleOutput(e.Data);
        if (!process.Start()) return null;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            return null;
        }

        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

        string[] lines;
        lock (output) lines = output.ToArray();

        log?.Invoke($"[AnalysisProbe] Drive {letter}: exit code {process.ExitCode}");

        if (process.ExitCode != 0)
            return null;

        var stats = new AnalysisParser.StatsReader();
        foreach (string line in lines) stats.Read(line);

        return stats.HasAny ? stats.ToStats() : null;
    }
}
