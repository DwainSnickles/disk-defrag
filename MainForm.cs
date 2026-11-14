using System.Diagnostics;
using System.Text.Json;
using aProgressBar;

namespace DiskDefrag;

public partial class MainForm : Form
{
    private sealed record AnalysisCacheEntry(bool IsAnalyzed, AnalysisStats Stats, DateTime AnalyzedUtc);
    private DefragEngine? _engine;
    private DriveRow? _selectedDrive;
    private bool _working;
    private bool _closing;
    private int _lastProgress = -1;
    private AnalysisStats _stats = new(null, null, null);
    private CancellationTokenSource? _samplingCts;
    private int? _initialFragmentCount;
    private int? _remainingFragmentCount;
    private int _pickerWidth = 180;
    private DiskOperation? _currentOperation;
    private string _currentDrive = string.Empty;
    private const string NoDefragNeeded = "You do not need to defragment this volume.";

    private bool _engineReportedComplete;
    private bool _noDefragNeeded;
    private KeepAwakeLease? _keepAwakeLease;
    private readonly Dictionary<string, AnalysisCacheEntry> _analysisCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string AnalysisCacheFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiskDefrag", "analysis-cache.json");

    public MainForm()
    {
        InitializeComponent();
        ApplyIcon();
        LoadAnalysisCache();
        _refresh.Click += async (_, _) => await RefreshDrivesAsync();
        _drivePicker.SelectedIndexChanged += async (_, _) => await SelectDriveAsync();
        _analyze.Click += async (_, _) => await StartOperationAsync(DiskOperation.Analyze);
        _defragment.Click += async (_, _) => await StartOperationAsync(DiskOperation.Defragment);
        _optimize.Click += async (_, _) => await StartOperationAsync(DiskOperation.Optimize);
        _stop.Click += (_, _) =>
        {
            if (_working) StopOperation();
            else Close();
        };
        _refreshTimer.Tick += async (_, _) => await RefreshMapAsync();
        _toolbar.Resize += (_, _) => ApplyPickerWidth();
        Shown += async (_, _) =>
        {
            await RefreshDrivesAsync();
            _refreshTimer.Start();
        };
    }

    private async Task RefreshDrivesAsync()
    {
        string? keep = _selectedDrive?.Letter;
        IReadOnlyList<DriveRow> drives = await Task.Run(VolumeMapReader.GetDrives);
        if (IsDisposed) return;
        _drivePicker.BeginUpdate();
        _drivePicker.Items.Clear();
        foreach (DriveRow drive in drives) _drivePicker.Items.Add(drive);
        _drivePicker.DisplayMember = nameof(DriveRow.Display);
        int widest = 150;
        foreach (DriveRow drive in drives)
            widest = Math.Max(widest, TextRenderer.MeasureText(drive.Display, _drivePicker.Font).Width + SystemInformation.VerticalScrollBarWidth + 12);
        _pickerWidth = widest;
        ApplyPickerWidth();
        _drivePicker.EndUpdate();
        int selected = drives.ToList().FindIndex(d => d.Letter.Equals(keep, StringComparison.OrdinalIgnoreCase));
        _drivePicker.SelectedIndex = drives.Count == 0 ? -1 : selected >= 0 ? selected : 0;
        if (drives.Count == 0) Append("No fixed or removable volumes are ready.");
    }

    /// <summary>
    /// The picker shares its row with the operation buttons and the close cell, so it is clamped to
    /// whatever room is left - on every resize, not only when the drive list is rebuilt.
    /// </summary>
    private void ApplyPickerWidth()
    {
        int room = Math.Max(150, _toolbar.ClientSize.Width - _drivePicker.Left - 10);
        _drivePicker.Width = Math.Clamp(_pickerWidth, 150, room);
        _drivePicker.DropDownWidth = _pickerWidth;
    }

    private static string AnalysisCacheKey(DriveRow drive) =>
        $"{drive.Letter}|{drive.Label}|{drive.FileSystem}";

    private AnalysisCacheEntry? GetCachedAnalysis(DriveRow? drive) =>
        drive is not null && _analysisCache.TryGetValue(AnalysisCacheKey(drive), out AnalysisCacheEntry? entry) && entry.IsAnalyzed
            ? entry
            : null;

    private void LoadAnalysisCache()
    {
        try
        {
            if (!File.Exists(AnalysisCacheFile)) return;
            var loaded = JsonSerializer.Deserialize<Dictionary<string, AnalysisCacheEntry>>(File.ReadAllText(AnalysisCacheFile));
            if (loaded is null) return;
            foreach (var item in loaded)
                if (item.Value.IsAnalyzed) _analysisCache[item.Key] = item.Value;
        }
        catch (Exception ex) { Append("Saved analysis cache could not be loaded: " + ex.Message); }
    }

    private void SaveAnalysisCache()
    {
        try
        {
            string? directory = Path.GetDirectoryName(AnalysisCacheFile);
            if (directory is not null) Directory.CreateDirectory(directory);
            string temporary = AnalysisCacheFile + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_analysisCache));
            File.Move(temporary, AnalysisCacheFile, true);
        }
        catch (Exception ex) { Append("Analysis cache could not be saved: " + ex.Message); }
    }

    private async Task SelectDriveAsync()
    {
        if (_working) return;
        _selectedDrive = _drivePicker.SelectedItem as DriveRow;
        if (_selectedDrive is null)
        {
            _driveInfo.Text = "No volume selected.";
            _map.SetMap(null, _driveInfo.Text, false);
            return;
        }
        _driveInfo.Text = $"SELECTED: {_selectedDrive.Letter}:  |  {_selectedDrive.Label}  |  {_selectedDrive.FileSystem}  |  {_selectedDrive.Kind}";
        AnalysisCacheEntry? cached = GetCachedAnalysis(_selectedDrive);
        _stats = cached?.Stats ?? new AnalysisStats(null, null, null);
        _initialFragmentCount = cached?.Stats.FragmentedFiles;
        _remainingFragmentCount = cached?.Stats.FragmentedFiles;
        _fragmentCount.Text = cached is not null
            ? $"ANALYZED {cached.AnalyzedUtc.ToLocalTime():MM/dd H:mm} · {cached.Stats.Summary}"
            : "ANALYSIS: run Analyze to get Windows' fragmented file count.";
        UpdateReductionProgress();
        await RefreshMapAsync();
    }

    private async Task RefreshMapAsync()
    {
        DriveRow? drive = _selectedDrive;
        if (drive is null || _closing || IsDisposed) return;
        (byte[]? bits, string message) = await Task.Run(() => VolumeMapReader.Read(drive.Letter));
        if (IsDisposed || _closing || _selectedDrive?.Letter != drive.Letter) return;
        _map.SetMap(bits, message, _working && !_engineReportedComplete);
    }

    private async Task StartOperationAsync(DiskOperation operation)
    {
        if (_working) return;
        if (_selectedDrive is null)
        {
            Append("Choose a volume first.");
            return;
        }
        DriveRow selected = _selectedDrive;
        if (operation != DiskOperation.Analyze)
        {
            DialogResult answer = MessageBox.Show(this,
                $"Run Windows {OperationName(operation)} on {selected.Letter}: ({selected.Label})?\n\n" +
                "Windows chooses the appropriate operation for the drive type. This may move files or issue SSD TRIM commands.",
                "Confirm disk operation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;
        }

        _working = true;
        _engineReportedComplete = false;
        _noDefragNeeded = false;
        if (_keepAwake.Checked)
        {
            _keepAwakeLease = KeepAwakeLease.TryAcquire();
            if (_keepAwakeLease is null) Append("Windows keep-awake request could not be set; check the operation and power policy.");
        }
        _stats = new AnalysisStats(null, null, null);
        _lastProgress = -1;
        _initialFragmentCount = null;
        _remainingFragmentCount = null;
        _samplingCts?.Cancel();
        _samplingCts?.Dispose();
        _samplingCts = new CancellationTokenSource();
        _currentOperation = operation;
        _currentDrive = selected.Letter + ":\\";
        _fragmentCount.Text = operation == DiskOperation.Analyze
            ? "ANALYSIS: Windows is counting fragmented files…"
            : "INITIAL FRAGMENT COUNT: collecting a read-only baseline…";
        _fragmentReductionLabel.Text = "FRAGMENT REDUCTION: baseline analysis in progress";
        _fragmentReductionLabel.Visible = false;
        // No count yet: the bar sweeps with no number at all, because a parked 0% would claim a
        // progress Windows has not reported. The first real percentage replaces the ellipsis.
        _progress.Indeterminate = true;
        _progress.Value = 0;
        _progress.Text = ProgressLabel(null);
        _operationStatus.Text = operation == DiskOperation.Analyze ? "ANALYZING" : "RUNNING";
        SetButtons();
        try
        {
            if (operation != DiskOperation.Analyze)
            {
                AnalysisCacheEntry? cached = GetCachedAnalysis(selected);
                if (cached?.IsAnalyzed == true && cached.Stats.FragmentedFiles is { } cachedCount)
                {
                    _initialFragmentCount = cachedCount;
                    _remainingFragmentCount = cachedCount;
                    _fragmentCount.Text = $"CACHED ANALYSIS {cached.AnalyzedUtc.ToLocalTime():g}: {cachedCount:N0} fragmented files";
                    UpdateReductionProgress();
                }
                else
                {
                    _operationStatus.Text = "BASELINE ANALYSIS";
                    AnalysisStats? initial = await AnalysisProbe.RunAsync(
                        selected.Letter,
                        _samplingCts.Token,
                        line => OnUi(() => Append(line)));
                    if (initial?.FragmentedFiles is { } initialCount)
                    {
                        _initialFragmentCount = initialCount;
                        _remainingFragmentCount = initialCount;
                        _analysisCache[AnalysisCacheKey(selected)] = new AnalysisCacheEntry(true, initial, DateTime.UtcNow);
                        SaveAnalysisCache();
                        _fragmentCount.Text = $"INITIAL FRAGMENTED FILES: {initialCount:N0}  |  LATEST REMAINING: {initialCount:N0}";
                        UpdateReductionProgress();
                    }
                    else if (initial is null)
                    {
                        _fragmentCount.Text = "BASELINE ANALYSIS UNAVAILABLE — use Analyze explicitly; no count was cached.";
                        Append("Baseline analysis did not return a fragmented-file count. Defragment may continue, but reduction progress will remain unavailable.");
                    }
                    if (!_working || IsDisposed) return;
                }
            }

            DefragEngine current = DefragEngine.Create(selected.Letter, operation);
            _engine = current;
            current.Output += line => OnUi(() => HandleEngineOutput(line));
            current.Progress += (phase, percent) => OnUi(() => SetProgress(phase, percent));
            current.Analysis += stats => OnUi(() => SetAnalysis(stats));
            current.Exited += code => OnUi(() => _ = FinishOperationAsync(current, selected, operation, code));
            current.StartProcess();
            await RefreshMapAsync();
        }
        catch (Exception ex)
        {
            _engine?.Dispose();
            _engine = null;
            _samplingCts?.Cancel();
            _samplingCts?.Dispose();
            _samplingCts = null;
            _working = false;
            _currentOperation = null;
            ReleaseKeepAwake();
            _progress.Indeterminate = false;
            _progress.Value = 0;
            _progress.Text = "FAILED TO START";
            _operationStatus.Text = "FAILED";
            Append("Could not start Windows defrag: " + ex.Message);
            SetButtons();
        }
    }

    private void HandleEngineOutput(string line)
    {
        Append(line);
        if (IsNoDefragVerdict(line)) _noDefragNeeded = true;
        if (_engine is null || _engineReportedComplete) return;
        if (line.Contains("operation completed successfully", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("operation completed", StringComparison.OrdinalIgnoreCase) && line.Contains("success", StringComparison.OrdinalIgnoreCase))
        {
            _engineReportedComplete = true;
            ShowCompletion();
            ReleaseKeepAwake();
            _ = RefreshMapAsync();
        }
    }

    /// <summary>
    /// The bar's wording: an analysis names the volume it is reading, the operations that move files
    /// talk about Windows doing the work. No percentage until Windows reports one.
    /// </summary>
    private string ProgressLabel(int? percent) => _currentOperation == DiskOperation.Analyze
        ? percent is int value ? $"ANALYZING DISK {_currentDrive} {value}%" : $"ANALYZING DISK {_currentDrive}"
        : percent is int reported ? $"WAITING ON WINDOWS {reported}%" : "WAITING ON WINDOWS…";

    /// <summary>Windows' own "nothing to do" verdict, which ends an optimize at 100% without any work.</summary>
    internal static bool IsNoDefragVerdict(string line) =>
        line.Contains("do not need to defragment", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("does not need to be defragmented", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A volume Windows leaves alone has not made progress, so the bar drops back to zero and says
    /// why instead of sitting at 100%.
    /// </summary>
    private void ShowCompletion()
    {
        _progress.Indeterminate = false;
        if (_noDefragNeeded)
        {
            _progress.Value = 0;
            _progress.Text = NoDefragNeeded;
            return;
        }

        _progress.Value = 100;
        _progress.Text = "100%";
    }

    private void SetProgress(string phase, int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        _lastProgress = percent;
        _progress.Value = percent;
        _progress.Indeterminate = false;
        _progress.Text = ProgressLabel(percent);

        // The bar carries the wording and the number, so the phase Windows names goes on the status line.
        if (phase.Length > 0) _operationStatus.Text = phase.ToUpperInvariant();
    }

    private void SetAnalysis(AnalysisStats stats)
    {
        _stats = stats;
        _fragmentCount.Text = stats.Summary;
        if (!_working && stats.FragmentedFiles is { } count)
        {
            _initialFragmentCount = count;
            _remainingFragmentCount = count;
            if (_selectedDrive is not null)
            {
                _analysisCache[AnalysisCacheKey(_selectedDrive)] = new AnalysisCacheEntry(true, stats, DateTime.UtcNow);
                SaveAnalysisCache();
            }
            _fragmentCount.Text = stats.Summary;
        }
        else if (_working && _initialFragmentCount is { } initial && stats.FragmentedFiles is { } current)
        {
            _remainingFragmentCount = current;
            _fragmentCount.Text = $"INITIAL FRAGMENTED FILES: {initial:N0}  |  LATEST REMAINING: {current:N0}";
        }
        UpdateReductionProgress();
    }

    private void UpdateReductionProgress(bool operationSucceeded = false)
    {
        if (_working && _currentOperation is DiskOperation.Defragment or DiskOperation.Optimize)
        {
            _fragmentReductionLabel.Visible = false;
            _fragmentReductionLabel.Text = "FRAGMENT REDUCTION\r\nThe count is rechecked after the Windows operation completes.";
            return;
        }

        if (_initialFragmentCount is not { } initial || _remainingFragmentCount is not { } remaining)
        {
            _fragmentReductionLabel.Visible = false;
            _fragmentReductionLabel.Text = "FRAGMENT REDUCTION\r\nWaiting for a valid baseline and current analysis.";
            return;
        }
        if (initial == 0)
        {
            _fragmentReductionLabel.Visible = true;
            _fragmentReductionLabel.Text = operationSucceeded
                ? "FRAGMENT REDUCTION: 100%   ·   INITIAL: 0   ·   REMAINING: 0\r\nNothing was fragmented at baseline."
                : "FRAGMENT REDUCTION: no fragmented files at baseline\r\nNo reduction is needed.";
            return;
        }
        if (initial < 0)
        {
            _fragmentReductionLabel.Visible = true;
            _fragmentReductionLabel.Text = "FRAGMENT REDUCTION: invalid analysis count";
            return;
        }

        int resolved = Math.Max(0, initial - remaining);
        double ratio = (double)resolved / initial;
        int percent = (int)Math.Floor(ratio * 100);
        if (_working || (!operationSucceeded && remaining == 0)) percent = Math.Min(percent, 99);
        else if (remaining > 0) percent = Math.Min(percent, 99);
        else percent = 100;
        string status = remaining > initial ? "Count increased; the estimate is held at 0%." :
            !_working && remaining > 0 ? "Some fragmented files remain." :
            !_working && remaining == 0 ? "Analysis reports nothing left to move." :
            "Read-only estimate; Windows may make multiple passes.";
        _fragmentReductionLabel.Visible = true;
        _fragmentReductionLabel.Text =
            $"FRAGMENT REDUCTION: {percent}%   ·   INITIAL: {initial:N0}   ·   REMAINING: {remaining:N0}   ·   RESOLVED: {resolved:N0}\r\n{status}";
    }

    private async Task FinishOperationAsync(DefragEngine engine, DriveRow drive, DiskOperation operation, int exitCode)
    {
        if (!ReferenceEquals(_engine, engine) || IsDisposed) return;
        _engine = null;
        engine.Dispose();
        _samplingCts?.Cancel();
        _samplingCts?.Dispose();
        _samplingCts = null;
        _currentOperation = null;
        ReleaseKeepAwake();
        bool finalAnalysisSucceeded = false;
        try
        {
            if (operation != DiskOperation.Analyze && exitCode == 0)
            {
                AnalysisStats? final = await AnalysisProbe.RunAsync(drive.Letter, CancellationToken.None);

                if (final?.FragmentedFiles is { } remaining)
                {
                    finalAnalysisSucceeded = true;
                    _analysisCache[AnalysisCacheKey(drive)] = new AnalysisCacheEntry(true, final, DateTime.UtcNow);
                    SaveAnalysisCache();
                    _remainingFragmentCount = remaining;
                    _fragmentCount.Text = _initialFragmentCount is { } initial
                        ? $"FRAGMENTED FILE COUNT: {initial:N0} initial  |  {remaining:N0} after operation"
                        : final.Summary;
                }
                else
                {
                    _fragmentCount.Text = "POST-OPERATION ANALYSIS UNAVAILABLE — saved analysis remains marked stale.";
                    _analysisCache.Remove(AnalysisCacheKey(drive));
                    SaveAnalysisCache();
                }
            }
            else if (operation == DiskOperation.Analyze && _stats.FragmentedFiles is { } analyzed)
            {
                _analysisCache[AnalysisCacheKey(drive)] = new AnalysisCacheEntry(true, _stats, DateTime.UtcNow);
                SaveAnalysisCache();
                _initialFragmentCount = analyzed;
                _remainingFragmentCount = analyzed;
                _fragmentCount.Text = _stats.Summary;
            }
        }
        finally
        {
            // Verifying the result is part of the operation, so IsWorking stays true until it ends.
            _working = false;
        }
        _progress.Indeterminate = false;
        if (exitCode == 0 && _engineReportedComplete)
        {
            ShowCompletion();
        }
        else if (_lastProgress >= 0)
        {
            _progress.Value = _lastProgress;
            _progress.Text = $"{_lastProgress}%";
        }
        else
        {
            _progress.Text = exitCode == 0 ? string.Empty : "NO PROGRESS";
        }
        if (exitCode != 0) Append($"Windows defrag exited with code {exitCode} on {drive.Letter}:");
        _operationStatus.Text = exitCode == 0 ? "IDLE" : "IDLE · LAST RUN FAILED";
        UpdateReductionProgress(operation == DiskOperation.Analyze ? false : finalAnalysisSucceeded);
        if (exitCode == 0 && operation != DiskOperation.Analyze && _shutdownAfter.Checked)
            ScheduleShutdown();
        SetButtons();
        _ = RefreshDrivesAsync();
        _ = RefreshMapAsync();
    }

    /// <summary>True from the first click to the end of the post-operation verification.</summary>
    internal bool IsWorking => _working;

    internal void Embed()
    {
        TopLevel = false;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        _root.Dock = DockStyle.Fill;
    }

    private void ReleaseKeepAwake()
    {
        _keepAwakeLease?.Dispose();
        _keepAwakeLease = null;
    }

    private void ScheduleShutdown()
    {
        try
        {
            Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 60 /c \"Disk Defrag completed\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            Append("Shutdown scheduled in 60 seconds. Run 'shutdown /a' from an administrator command prompt to cancel.");
        }
        catch (Exception ex) { Append("Could not schedule shutdown: " + ex.Message); }
    }

    private void StopOperation()
    {
        if (_engine is null) return;
        _progress.Indeterminate = false;
        _progress.Text = "STOPPING WINDOWS OPERATION…";
        _operationStatus.Text = "STOPPING";
        _engine.Stop();
    }

    private void SetButtons()
    {
        _drivePicker.Enabled = !_working;
        _refresh.Enabled = !_working;
        _analyze.Enabled = !_working;
        _defragment.Enabled = !_working;
        _optimize.Enabled = !_working;
        _stop.Enabled = true;
        _stop.Text = _working ? "■  STOP" : "✕  CLOSE";
        _stop.BackColor = _working ? Color.FromArgb(20, 29, 41) : Color.FromArgb(35, 20, 27);
        _stop.ForeColor = _working ? Color.FromArgb(220, 233, 245) : Color.FromArgb(255, 145, 155);
    }

    private void Append(string line)
    {
        if (IsDisposed || string.IsNullOrWhiteSpace(line)) return;
        _output.AppendText(line + Environment.NewLine);
        if (_output.TextLength > 60000)
            _output.Text = _output.Text[^40000..];
    }

    private void OnUi(Action action)
    {
        if (IsDisposed || _closing || !IsHandleCreated) return;
        try { BeginInvoke(action); } catch (InvalidOperationException) { }
    }

    private static string OperationName(DiskOperation operation) => operation switch
    {
        DiskOperation.Analyze => "analysis",
        DiskOperation.Defragment => "defragmentation",
        _ => "optimization",
    };


    /// <summary>The .ico the executable carries, set explicitly so a host process cannot supply its own.</summary>
    private void ApplyIcon()
    {
        using Stream? stream = typeof(MainForm).Assembly.GetManifestResourceStream("app.ico");
        if (stream is not null) Icon = new Icon(stream);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkWindow.Apply(Handle);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _closing = true;
        _refreshTimer.Stop();
        ReleaseKeepAwake();
        _samplingCts?.Cancel();
        _samplingCts?.Dispose();
        _samplingCts = null;
        _engine?.Stop();
        base.OnFormClosing(e);
    }
}
