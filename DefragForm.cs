using System.Diagnostics;

namespace DiskDefrag;

internal sealed class DefragForm : Form
{
    private readonly Label _admin = new();
    private readonly Label _status = new();
    private readonly ToolTip _tips = new();
    private readonly VolumeGrid _grid = new();
    private readonly ClusterMapView _map = new();
    private readonly ProgressStrip _progress = new() { Dock = DockStyle.Bottom, Height = 42 };
    private readonly TextBox _log = new();
    private readonly DarkButton _elevate = new() { Text = "RUN AS ADMIN", Size = new Size(140, 28) };
    private readonly DarkButton _analyze = new() { Text = "ANALYZE", Primary = true, Size = new Size(120, 32) };
    private readonly DarkButton _defragment = new() { Text = "DEFRAGMENT", Size = new Size(130, 32) };
    private readonly DarkButton _optimize = new() { Text = "OPTIMIZE", Size = new Size(120, 32) };
    private readonly DarkButton _stop = new() { Text = "STOP", Tone = DefragTheme.Bad, Size = new Size(90, 32) };
    private readonly DarkButton _exit = new() { Text = "EXIT", Size = new Size(90, 32) };

    private string? _selected;
    private DefragJob? _job;
    private bool _busy;

    public DefragForm()
    {
        Text = "DISK DEFRAG \u2014 PC MEDIC TOOL";
        ClientSize = new Size(940, 720);
        MinimumSize = new Size(880, 660);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = DefragTheme.Bg;
        ForeColor = DefragTheme.Text;
        Font = DefragTheme.Ui;
        DoubleBuffered = true;

        var header = BuildHeader();
        var toolbar = BuildToolbar();
        var volumes = BuildVolumes();
        var map = BuildMap();
        var log = BuildLog();
        var status = BuildStatus();

        Controls.Add(map);
        Controls.Add(log);
        Controls.Add(status);
        Controls.Add(volumes);
        Controls.Add(toolbar);
        Controls.Add(header);

        foreach (DarkButton button in new[] { _analyze, _defragment, _optimize, _stop, _exit }) toolbar.Controls.Add(button);
        toolbar.Controls.Add(_elevate);

        _analyze.Click += async (_, _) => await RunAsync(DefragMode.Analyze);
        _defragment.Click += async (_, _) => await RunAsync(DefragMode.Defragment);
        _optimize.Click += async (_, _) => await RunAsync(DefragMode.Optimize);
        _stop.Click += (_, _) => StopJob();
        _exit.Click += (_, _) => Close();
        _elevate.Click += (_, _) => RelaunchElevated();
        _grid.SelectionChanged += OnVolumeSelected;

        _tips.SetToolTip(_analyze, "Runs defrag /A: reports fragmentation without touching the volume.");
        _tips.SetToolTip(_defragment, "Runs defrag /D: consolidates fragmented files. Needs an elevated session.");
        _tips.SetToolTip(_optimize, "Runs defrag /O: consolidates free space and TRIMs solid-state volumes.");
        _tips.SetToolTip(_stop, "Terminates the running defrag.exe.");
        _tips.SetToolTip(_map, "Read from the volume allocation bitmap; needs administrator rights.");

        SetBusy(false, string.Empty);
        LoadVolumes();
    }

    private Panel BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = DefragTheme.Bg };
        header.Paint += (_, e) =>
        {
            using var pen = new Pen(DefragTheme.BorderSoft);
            e.Graphics.DrawLine(pen, 22, header.Height - 1, header.Width - 22, header.Height - 1);
        };

        var title = new Label
        {
            Text = "DISK DEFRAG",
            Font = DefragTheme.H1,
            ForeColor = DefragTheme.Text,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(22, 12),
        };

        var subtitle = new Label
        {
            Text = "Analyse, defragment and optimize the volumes on this PC \u2014 the Windows engine, with a live cluster map.",
            Font = DefragTheme.UiSmall,
            ForeColor = DefragTheme.TextDim,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(24, 44),
        };

        _admin.Text = Native.IsElevated ? "ELEVATED" : "STANDARD USER";
        _admin.Font = DefragTheme.Kicker;
        _admin.ForeColor = Native.IsElevated ? DefragTheme.Good : DefragTheme.Warn;
        _admin.BackColor = Color.Transparent;
        _admin.AutoSize = true;

        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(_admin);

        header.Resize += (_, _) => _admin.Location = new Point(header.ClientSize.Width - _admin.Width - 24, 20);

        return header;
    }

    private Panel BuildToolbar()
    {
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = DefragTheme.Bg };

        void Layout()
        {
            // Keep the outer buttons off the form edge, aligned with the card edges below.
            const int Inset = 14;

            int x = Inset;
            foreach (DarkButton button in new[] { _analyze, _defragment, _optimize, _stop })
            {
                button.SetBounds(x, 4, button.Width, 32);
                x += button.Width + 8;
            }

            _exit.SetBounds(toolbar.ClientSize.Width - _exit.Width - Inset, 4, _exit.Width, 32);
            _elevate.SetBounds(_exit.Left - _elevate.Width - 8, 6, _elevate.Width, 28);
        }

        toolbar.Resize += (_, _) => Layout();
        toolbar.HandleCreated += (_, _) => Layout();

        return toolbar;
    }

    private CardPanel BuildVolumes()
    {
        var card = new CardPanel("VOLUMES", "SELECT A VOLUME")
        {
            Dock = DockStyle.Top,
            Height = 250,
            Margin = new Padding(0),
        };

        _grid.Dock = DockStyle.Fill;
        card.Controls.Add(_grid);
        return card;
    }

    private CardPanel BuildMap()
    {
        var card = new CardPanel("CLUSTER MAP", "VOLUME LAYOUT")
        {
            Dock = DockStyle.Fill,
        };

        _map.Dock = DockStyle.Fill;
        card.Controls.Add(_map);

        // Docked after the map, so the strip takes the card's last rows only while a job is running.
        card.Controls.Add(_progress);

        return card;
    }

    private CardPanel BuildLog()
    {
        var card = new CardPanel("ACTIVITY", "DEFRAG OUTPUT")
        {
            Dock = DockStyle.Bottom,
            Height = 150,
        };

        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.BorderStyle = BorderStyle.None;
        _log.BackColor = DefragTheme.Surface;
        _log.ForeColor = DefragTheme.TextDim;
        _log.Font = DefragTheme.MonoSmall;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.WordWrap = false;
        _log.Dock = DockStyle.Fill;

        card.Controls.Add(_log);
        return card;
    }

    private Panel BuildStatus()
    {
        var panel = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = DefragTheme.Bg };

        _status.AutoSize = false;
        _status.AutoEllipsis = true;
        _status.Font = DefragTheme.MonoSmall;
        _status.ForeColor = DefragTheme.TextDim;
        _status.BackColor = Color.Transparent;
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;

        panel.Controls.Add(_status);
        return panel;
    }

    private void LoadVolumes()
    {
        IReadOnlyList<VolumeInfo> volumes = VolumeService.Load();
        _grid.SetVolumes(volumes);

        if (_selected is null && volumes.Count > 0) OnVolumeSelected(volumes[0].Letter);
        if (volumes.Count == 0) SetStatus("No fixed volumes were found.", DefragTheme.Warn);
    }

    private void OnVolumeSelected(string letter)
    {
        _selected = letter;

        VolumeInfo? volume = VolumeService.Load().FirstOrDefault(candidate => candidate.Letter == letter);
        SetStatus(
            volume is null
                ? $"Selected {letter}:"
                : $"Selected {letter}:  \u2022  {volume.FileSystem}  \u2022  {volume.UsedPercent:0}% used  \u2022  {DefragTheme.Bytes(volume.Free)} free");

        LoadMap(letter);
    }

    private async void LoadMap(string letter)
    {
        _map.ShowMessage("Reading the volume bitmap\u2026");
        using IDisposable busy = Busy.Enter(this);

        (ClusterMapData? data, string message) = await Task.Run(() => VolumeService.ReadBitmap(letter));

        if (IsDisposed) return;

        if (data is null)
        {
            _map.ShowMessage(message);
            _tips.SetToolTip(_map, message);
            return;
        }

        _map.SetData(data);
        _tips.SetToolTip(_map, data.Note);
    }

    private async Task RunAsync(DefragMode mode)
    {
        if (_busy) return;

        if (_selected is null)
        {
            SetStatus("Select a volume first.", DefragTheme.Warn);
            return;
        }

        string letter = _selected;
        DefragJob job;

        try
        {
            job = DefragJob.Start(letter, mode);
        }
        catch (Exception ex)
        {
            SetStatus($"{DefragJob.VerbFor(mode)} {letter}: failed \u2014 {ex.Message}", DefragTheme.Bad);
            return;
        }

        _job = job;
        _progress.Begin($"{DefragJob.VerbFor(mode).ToUpperInvariant()} {letter}:");
        SetBusy(true, $"{DefragJob.VerbFor(mode)} {letter}: \u2014 defrag.exe is running.");

        Append(string.Empty);
        Append($"> defrag {DefragJob.ArgumentsFor(letter, mode)}");
        Append($"Session: {(Native.IsElevated ? "elevated" : "standard \u2014 defrag may be refused")}  \u2022  volume {letter}:");

        bool refused = false;

        job.Line += line =>
        {
            if (line.Contains("insufficient privileges", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("cannot start", StringComparison.OrdinalIgnoreCase))
            {
                refused = true;
            }

            if (!IsDisposed)
            {
                BeginInvoke(() =>
                {
                    Append(line);

                    if (DefragOutput.TryReadProgress(line, out string label, out int percent))
                    {
                        _progress.Report(label, percent);
                    }
                });
            }
        };

        job.Exited += code =>
        {
            if (IsDisposed) return;

            BeginInvoke(() =>
            {
                _job = null;
                _busy = false;
                _progress.Stop();
                SetBusy(false, string.Empty);

                if (refused)
                {
                    // defrag.exe prints the privilege failure and still exits 0, so trust the output.
                    SetStatus(
                        $"{DefragJob.VerbFor(mode)} {letter}: refused \u2014 the storage optimizer needs an elevated session.",
                        DefragTheme.Warn);
                }
                else if (code == 0)
                {
                    SetStatus($"{DefragJob.VerbFor(mode)} {letter}: finished.", DefragTheme.Good);
                }
                else
                {
                    SetStatus(
                        $"{DefragJob.VerbFor(mode)} {letter}: defrag.exe exited with code {code}.",
                        DefragTheme.Warn);
                }

                LoadVolumes();
                LoadMap(letter);
            });
        };

        await Task.CompletedTask;
    }

    private void StopJob()
    {
        if (_job is null)
        {
            SetStatus("Nothing is running.", DefragTheme.TextDim);
            return;
        }

        _job.Stop();
        Append("Stopped by the operator.");
        if (_selected is not null) _progress.Relabel($"STOPPING {_selected}:");
        SetStatus("Stopping defrag.exe\u2026", DefragTheme.Warn);
    }

    private void SetBusy(bool busy, string status)
    {
        _busy = busy;
        _analyze.Enabled = !busy;
        _defragment.Enabled = !busy;
        _optimize.Enabled = !busy;
        _stop.Enabled = busy;
        _grid.Enabled = !busy;

        foreach (DarkButton button in new[] { _analyze, _defragment, _optimize, _stop }) button.Invalidate();

        if (!string.IsNullOrEmpty(status)) SetStatus(status);
        else if (!busy) SetStatus(_selected is null ? "Ready." : $"Ready \u2014 {_selected}: selected");
    }

    private void SetStatus(string text, Color? tone = null)
    {
        _status.Text = text;
        _status.ForeColor = tone ?? DefragTheme.TextDim;
        _tips.SetToolTip(_status, text);
    }

    private void Append(string text) => _log.AppendText(text + Environment.NewLine);

    private void RelaunchElevated()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true, Verb = "runas" });
            Close();
        }
        catch (Exception ex)
        {
            SetStatus($"Elevation was cancelled or failed: {ex.Message}", DefragTheme.Bad);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _job?.Stop();
        _job?.Dispose();
        base.OnFormClosing(e);
    }
}
