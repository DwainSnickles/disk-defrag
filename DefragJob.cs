using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

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

/// <summary>
/// Reads the percentage out of defrag.exe's streamed output. A progress line is a short label, the
/// number and a percent sign, optionally followed by one word or a run of dots ("Analysis: 41%
/// complete", "Defragmentation: 17 % done", "  20% ....."). Report rows put "=" between label and
/// value and are rejected by the shape test, so "Percent file fragmentation = 49 %" cannot move the bar.
/// </summary>
internal static class DefragOutput
{
    private static readonly Regex ProgressLine = new(
        @"^\s*(?<label>[\p{L}][\p{L}\s:.\-]{0,23}?)?\s*(?<percent>\d{1,3})\s*%(?<tail>[\p{L}\s.]*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>True when the line carries a percentage; the label may be empty.</summary>
    public static bool TryReadProgress(string line, out string label, out int percent)
    {
        label = string.Empty;
        percent = 0;

        if (string.IsNullOrWhiteSpace(line)) return false;

        Match match = ProgressLine.Match(line);
        if (!match.Success) return false;
        if (!int.TryParse(match.Groups["percent"].Value, out int value) || value is < 0 or > 100) return false;

        label = match.Groups["label"].Value.Trim().TrimEnd(':', '-', '.').Trim();
        percent = value;

        return true;
    }

    private static readonly Regex FragmentedFiles = new(
        @"^\s*(?:Total\s+)?fragmented files\s*[:=]?\s*(?<value>[\d,]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FilePercent = new(
        @"^\s*(?:Percent\s+)?file fragmentation\s*[:=]?\s*(?<value>[\d,]+)\s*%?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ExcessFragments = new(
        @"^\s*(?:Total\s+)?excess fragments\s*[:=]?\s*(?<value>[\d,]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Reads the counters defrag prints during /A (and at the end of a pass). Each counter
    /// arrives on its own line, so they are matched independently. English output only: a
    /// localized defrag leaves the report empty rather than reporting a wrong figure.
    /// </summary>
    public static bool TryReadAnalysis(string line, AnalysisReport report)
    {
        if (string.IsNullOrWhiteSpace(line) || report is null) return false;

        if (FragmentedFiles.Match(line) is { Success: true } files &&
            int.TryParse(files.Groups["value"].Value.Replace(",", string.Empty), out int count))
        {
            report.FragmentedFiles = count;
            return true;
        }

        if (FilePercent.Match(line) is { Success: true } percent &&
            int.TryParse(percent.Groups["value"].Value.Replace(",", string.Empty), out int value))
        {
            report.FilePercent = value;
            return true;
        }

        if (ExcessFragments.Match(line) is { Success: true } excess &&
            int.TryParse(excess.Groups["value"].Value.Replace(",", string.Empty), out int fragments))
        {
            report.ExcessFragments = fragments;
            return true;
        }

        return false;
    }
}

/// <summary>Analysis counters from the last defrag run, shown beside the cluster map.</summary>
internal sealed class AnalysisReport
{
    public int? FragmentedFiles { get; set; }

    public int? FilePercent { get; set; }

    public int? ExcessFragments { get; set; }

    public bool HasAny => FragmentedFiles.HasValue || FilePercent.HasValue;

    public void Clear()
    {
        FragmentedFiles = null;
        FilePercent = null;
        ExcessFragments = null;
    }

    public string Describe()
    {
        if (!HasAny) return string.Empty;

        var parts = new List<string>(3);

        if (FragmentedFiles is { } files) parts.Add($"{files:N0} fragmented files");
        if (FilePercent is { } percent) parts.Add($"{percent}% file fragmentation");
        if (ExcessFragments is { } excess) parts.Add($"{excess:N0} excess fragments");

        return string.Join("  •  ", parts);
    }
}
