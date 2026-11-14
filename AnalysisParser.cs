using System.Text.RegularExpressions;

namespace DiskDefrag;

/// <summary>What Windows defrag reports about a volume's fragmentation.</summary>
internal sealed record AnalysisStats(int? FragmentedFiles, int? FragmentationPercent, int? ExcessFragments)
{
    /// <summary>One line that still fits the strip at the window's minimum width.</summary>
    public string Summary =>
        $"FRAGMENTED FILES: {FragmentedFiles?.ToString("N0") ?? "—"} · " +
        $"FRAGMENTATION: {(FragmentationPercent is { } p ? $"{p}%" : "—")} · " +
        $"EXCESS FRAGMENTS: {ExcessFragments?.ToString("N0") ?? "—"}";
}

/// <summary>
/// The one place that knows what defrag.exe prints. The live engine reads its output line by line and
/// the read-only probe reads a finished run, so both go through this rather than keeping two copies of
/// the same patterns.
/// </summary>
internal static class AnalysisParser
{
    // The label carries digits in Windows' own output ("Phase 1: 42% complete"), so the phase
    // group has to allow them.
    private static readonly Regex PhasePercent = new(
        @"^\s*(?<phase>[\p{L}][\p{L}\d\s-]{0,32}):\s*(?<pct>\d{1,3})\s*%\s*complete",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FragmentedFilesPattern = new(
        @"^\s*(?:Total\s+)?fragmented files\s*[:=]?\s*(?<value>[\d,]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FragmentationPattern = new(
        @"^\s*(?:Percent\s+)?file fragmentation\s*[:=]?\s*(?<value>\d{1,3})\s*%",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ExcessPattern = new(
        @"^\s*(?:Total\s+)?excess fragments\s*[:=]?\s*(?<value>[\d,]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>"Phase 2: 42% complete" gives ("Phase 2", 42); any other line is not progress.</summary>
    public static bool TryReadProgress(string line, out string phase, out int percent)
    {
        Match match = PhasePercent.Match(line);
        if (match.Success && int.TryParse(match.Groups["pct"].Value, out percent) && percent <= 100)
        {
            phase = match.Groups["phase"].Value.Trim();
            return true;
        }

        phase = string.Empty;
        percent = 0;
        return false;
    }

    /// <summary>
    /// Collects the figures as lines go past. The engine keeps one for the life of a run; the probe
    /// makes one, feeds it the whole output and asks for the result.
    /// </summary>
    public sealed class StatsReader
    {
        public int? FragmentedFiles { get; private set; }
        public int? FragmentationPercent { get; private set; }
        public int? ExcessFragments { get; private set; }

        public bool HasAny =>
            FragmentedFiles.HasValue || FragmentationPercent.HasValue || ExcessFragments.HasValue;

        public AnalysisStats ToStats() => new(FragmentedFiles, FragmentationPercent, ExcessFragments);

        /// <summary>Reads one line; true when it carried a figure and changed the result.</summary>
        public bool Read(string line)
        {
            bool changed = false;

            Match match = FragmentedFilesPattern.Match(line);
            if (match.Success && int.TryParse(Number(match), out int files))
            {
                FragmentedFiles = files;
                changed = true;
            }

            match = FragmentationPattern.Match(line);
            if (match.Success && int.TryParse(match.Groups["value"].Value, out int percent))
            {
                FragmentationPercent = percent;
                changed = true;
            }

            match = ExcessPattern.Match(line);
            if (match.Success && int.TryParse(Number(match), out int excess))
            {
                ExcessFragments = excess;
                changed = true;
            }

            return changed;
        }

        /// <summary>Windows groups thousands with commas; the numbers are read without them.</summary>
        private static string Number(Match match) =>
            match.Groups["value"].Value.Replace(",", string.Empty);
    }
}
