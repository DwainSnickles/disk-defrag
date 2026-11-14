#nullable enable

namespace DiskDefrag;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;
    private TableLayoutPanel _root = null!;
    private Panel _topPanel = null!;
    private FlowLayoutPanel _toolbar = null!;
    private Panel _selectionPanel = null!;
    private TableLayoutPanel _selectionRow = null!;
    private Panel _infoPanel = null!;
    private TableLayoutPanel _reductionPanel = null!;
    private NeonPanel _optionsPanel = null!;
    private ComboBox _drivePicker = null!;
    private Button _refresh = null!;
    private Button _analyze = null!;
    private Button _defragment = null!;
    private Button _optimize = null!;
    private Button _stop = null!;
    private Label _driveInfo = null!;
    private Label _fragmentCount = null!;
    private Label _operationStatus = null!;
    private aProgressBar.aProgressBar _progress = null!;
    private Label _fragmentReductionLabel = null!;
    private CheckBox _keepAwake = null!;
    private CheckBox _shutdownAfter = null!;
    private ClusterMapControl _map = null!;
    private TextBox _output = null!;
    private System.Windows.Forms.Timer _refreshTimer = null!;

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _root = new TableLayoutPanel();
        _topPanel = new Panel();
        _toolbar = new FlowLayoutPanel();
        _refresh = new NeonButton();
        _analyze = new NeonButton();
        _defragment = new NeonButton();
        _optimize = new NeonButton();
        _stop = new NeonButton();
        _drivePicker = new DarkComboBox();
        _selectionPanel = new Panel();
        _selectionRow = new TableLayoutPanel();
        _driveInfo = new Label();
        _operationStatus = new Label();
        _progress = new aProgressBar.aProgressBar();
        _infoPanel = new Panel();
        _fragmentCount = new Label();
        _map = new ClusterMapControl();
        _reductionPanel = new TableLayoutPanel();
        _optionsPanel = new NeonPanel();
        _keepAwake = new CheckBox();
        _shutdownAfter = new CheckBox();
        _fragmentReductionLabel = new Label();
        _output = new TextBox();
        _refreshTimer = new System.Windows.Forms.Timer(components);
        _root.SuspendLayout();
        _topPanel.SuspendLayout();
        _toolbar.SuspendLayout();
        _selectionPanel.SuspendLayout();
        _selectionRow.SuspendLayout();
        _infoPanel.SuspendLayout();
        _reductionPanel.SuspendLayout();
        _optionsPanel.SuspendLayout();
        SuspendLayout();
        // 
        // _root
        // 
        _root.BackColor = Color.Black;
        _root.ColumnCount = 1;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
        _root.Controls.Add(_topPanel, 0, 0);
        _root.Controls.Add(_selectionPanel, 0, 1);
        _root.Controls.Add(_infoPanel, 0, 2);
        _root.Controls.Add(_map, 0, 3);
        _root.Controls.Add(_reductionPanel, 0, 4);
        _root.Controls.Add(_output, 0, 5);
        _root.Dock = DockStyle.Fill;
        _root.Location = new Point(0, 0);
        _root.Name = "_root";
        _root.Padding = new Padding(16);
        _root.RowCount = 6;
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        // The map and the log share what is left, weighted so extra height goes to the map: at the
        // 900x650 minimum that is ~200px of map and ~130px of log instead of a 29px map.
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104F));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
        _root.Size = new Size(1160, 800);
        _root.TabIndex = 0;
        // 
        // _topPanel
        // 
        _topPanel.BackColor = Color.Black;
        _topPanel.Controls.Add(_toolbar);
        _topPanel.Dock = DockStyle.Fill;
        _topPanel.Location = new Point(19, 19);
        _topPanel.Name = "_topPanel";
        _topPanel.Size = new Size(1122, 44);
        _topPanel.TabIndex = 0;
        // 
        // _toolbar
        // 
        _toolbar.BackColor = Color.Black;
        _toolbar.Controls.Add(_refresh);
        _toolbar.Controls.Add(_analyze);
        _toolbar.Controls.Add(_defragment);
        _toolbar.Controls.Add(_optimize);
        _toolbar.Controls.Add(_stop);
        _toolbar.Controls.Add(_drivePicker);
        _toolbar.Dock = DockStyle.Fill;
        _toolbar.Location = new Point(0, 0);
        _toolbar.Name = "_toolbar";
        _toolbar.Padding = new Padding(0, 6, 0, 0);
        _toolbar.Size = new Size(1122, 44);
        _toolbar.TabIndex = 0;
        _toolbar.WrapContents = false;
        // 
        // _refresh
        // 
        _refresh.BackColor = Color.FromArgb(20, 29, 41);
        _refresh.Cursor = Cursors.Hand;
        _refresh.ForeColor = Color.FromArgb(220, 233, 245);
        _refresh.Location = new Point(0, 6);
        _refresh.Margin = new Padding(0, 0, 7, 0);
        _refresh.Name = "_refresh";
        _refresh.Size = new Size(92, 34);
        _refresh.TabIndex = 0;
        _refresh.Text = "↻  REFRESH";
        // 
        // _analyze
        // 
        _analyze.BackColor = Color.FromArgb(20, 29, 41);
        _analyze.Cursor = Cursors.Hand;
        _analyze.ForeColor = Color.FromArgb(220, 233, 245);
        _analyze.Location = new Point(99, 6);
        _analyze.Margin = new Padding(0, 0, 7, 0);
        _analyze.Name = "_analyze";
        _analyze.Size = new Size(100, 34);
        _analyze.TabIndex = 1;
        _analyze.Text = "⌕  ANALYZE";
        // 
        // _defragment
        // 
        _defragment.BackColor = Color.FromArgb(20, 29, 41);
        _defragment.Cursor = Cursors.Hand;
        _defragment.ForeColor = Color.FromArgb(220, 233, 245);
        _defragment.Location = new Point(206, 6);
        _defragment.Margin = new Padding(0, 0, 7, 0);
        _defragment.Name = "_defragment";
        _defragment.Size = new Size(124, 34);
        _defragment.TabIndex = 2;
        _defragment.Text = "▦  DEFRAGMENT";
        // 
        // _optimize
        // 
        _optimize.BackColor = Color.FromArgb(20, 29, 41);
        _optimize.Cursor = Cursors.Hand;
        _optimize.ForeColor = Color.FromArgb(220, 233, 245);
        _optimize.Location = new Point(337, 6);
        _optimize.Margin = new Padding(0, 0, 7, 0);
        _optimize.Name = "_optimize";
        _optimize.Size = new Size(105, 34);
        _optimize.TabIndex = 3;
        _optimize.Text = "◆  OPTIMIZE";
        // 
        // _stop
        // 
        _stop.BackColor = Color.FromArgb(20, 29, 41);
        _stop.Cursor = Cursors.Hand;
        _stop.ForeColor = Color.FromArgb(220, 233, 245);
        _stop.Location = new Point(449, 6);
        _stop.Margin = new Padding(0, 0, 7, 0);
        _stop.Name = "_stop";
        _stop.Size = new Size(78, 34);
        _stop.TabIndex = 4;
        _stop.Text = "✕  CLOSE";
        // 
        // _drivePicker
        // 
        _drivePicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _drivePicker.Location = new Point(534, 6);
        _drivePicker.Margin = new Padding(0, 3, 8, 0);
        _drivePicker.Name = "_drivePicker";
        _drivePicker.Size = new Size(180, 23);
        _drivePicker.TabIndex = 6;
        // 
        // _selectionPanel
        // 
        _selectionPanel.BackColor = Color.Black;
        _selectionPanel.Controls.Add(_selectionRow);
        _selectionPanel.Dock = DockStyle.Fill;
        _selectionPanel.Location = new Point(19, 69);
        _selectionPanel.Name = "_selectionPanel";
        _selectionPanel.Size = new Size(1122, 54);
        _selectionPanel.TabIndex = 1;
        // 
        // _selectionRow
        // 
        _selectionRow.BackColor = Color.Black;
        _selectionRow.ColumnCount = 3;
        _selectionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46F));
        _selectionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        _selectionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        _selectionRow.Controls.Add(_driveInfo, 0, 0);
        _selectionRow.Controls.Add(_operationStatus, 1, 0);
        _selectionRow.Controls.Add(_progress, 2, 0);
        _selectionRow.Dock = DockStyle.Fill;
        _selectionRow.Location = new Point(0, 0);
        _selectionRow.Margin = new Padding(0);
        _selectionRow.Name = "_selectionRow";
        _selectionRow.RowCount = 1;
        _selectionRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _selectionRow.Size = new Size(1122, 54);
        _selectionRow.TabIndex = 0;
        // 
        // _driveInfo
        // 
        _driveInfo.AutoEllipsis = true;
        _driveInfo.Dock = DockStyle.Fill;
        _driveInfo.ForeColor = Color.FromArgb(160, 180, 200);
        _driveInfo.Location = new Point(3, 0);
        _driveInfo.Name = "_driveInfo";
        _driveInfo.Size = new Size(510, 54);
        _driveInfo.TabIndex = 0;
        _driveInfo.Text = "No volume selected.";
        _driveInfo.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _operationStatus
        // 
        _operationStatus.Dock = DockStyle.Fill;
        _operationStatus.ForeColor = Color.FromArgb(160, 180, 200);
        _operationStatus.Location = new Point(519, 0);
        _operationStatus.Name = "_operationStatus";
        _operationStatus.Size = new Size(218, 54);
        _operationStatus.TabIndex = 1;
        _operationStatus.TextAlign = ContentAlignment.MiddleLeft;
        _operationStatus.Text = "IDLE";
        // 
        // _progress
        // 
        _progress.BackColor = Color.Black;
        _progress.BorderColor = Palette.Border;
        _progress.Dock = DockStyle.Bottom;
        _progress.GradientColor1 = Color.FromArgb(8, 52, 68);
        _progress.GradientColor2 = Color.FromArgb(12, 96, 120);
        _progress.GradientColor3 = Color.FromArgb(20, 150, 180);
        _progress.GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Vertical;
        _progress.Location = new Point(743, 23);
        _progress.Name = "_progress";
        _progress.Padding = new Padding(0, 20, 0, 20);
        _progress.ProgressBackColor = Palette.SurfaceRaised;
        _progress.Size = new Size(376, 28);
        _progress.TabIndex = 2;
        _progress.TextFont = new Font("Consolas", 9F, FontStyle.Bold);
        // 
        // _infoPanel
        // 
        _infoPanel.BackColor = Color.Black;
        _infoPanel.Controls.Add(_fragmentCount);
        _infoPanel.Dock = DockStyle.Fill;
        _infoPanel.Location = new Point(19, 129);
        _infoPanel.Name = "_infoPanel";
        _infoPanel.Size = new Size(1122, 30);
        _infoPanel.TabIndex = 2;
        // 
        // _fragmentCount
        // 
        _fragmentCount.AutoEllipsis = true;
        _fragmentCount.Dock = DockStyle.Fill;
        _fragmentCount.Font = new Font("Consolas", 12F, FontStyle.Bold);
        _fragmentCount.ForeColor = Color.FromArgb(29, 206, 235);
        _fragmentCount.Location = new Point(0, 0);
        _fragmentCount.Name = "_fragmentCount";
        _fragmentCount.Size = new Size(1122, 30);
        _fragmentCount.TabIndex = 0;
        _fragmentCount.Text = "ANALYSIS: run Analyze to get Windows' fragmented-file count.";
        _fragmentCount.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _map
        // 
        _map.BackColor = Color.FromArgb(10, 15, 23);
        _map.Dock = DockStyle.Fill;
        _map.ForeColor = Color.White;
        _map.Location = new Point(16, 167);
        _map.Margin = new Padding(0, 5, 0, 5);
        _map.Name = "_map";
        _map.Size = new Size(1128, 208);
        _map.TabIndex = 3;
        // 
        // _reductionPanel
        // 
        _reductionPanel.BackColor = Color.Black;
        _reductionPanel.ColumnCount = 1;
        _reductionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
        _reductionPanel.Controls.Add(_optionsPanel, 0, 0);
        _reductionPanel.Controls.Add(_fragmentReductionLabel, 0, 1);
        _reductionPanel.Dock = DockStyle.Fill;
        _reductionPanel.Location = new Point(19, 383);
        _reductionPanel.Name = "_reductionPanel";
        _reductionPanel.RowCount = 2;
        _reductionPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        _reductionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _reductionPanel.Size = new Size(1122, 98);
        _reductionPanel.TabIndex = 4;
        // 
        // _optionsPanel
        // 
        _optionsPanel.BackColor = Color.Black;
        _optionsPanel.Controls.Add(_keepAwake);
        _optionsPanel.Controls.Add(_shutdownAfter);
        _optionsPanel.Dock = DockStyle.Fill;
        _optionsPanel.Location = new Point(3, 3);
        _optionsPanel.Name = "_optionsPanel";
        _optionsPanel.Padding = new Padding(6, 0, 0, 0);
        _optionsPanel.Size = new Size(1116, 24);
        _optionsPanel.TabIndex = 0;
        _optionsPanel.WrapContents = false;
        // 
        // _keepAwake
        // 
        _keepAwake.AutoSize = true;
        _keepAwake.BackColor = Color.Transparent;
        _keepAwake.ForeColor = Color.FromArgb(185, 201, 218);
        _keepAwake.Location = new Point(2, 5);
        _keepAwake.Margin = new Padding(2, 5, 24, 0);
        _keepAwake.Name = "_keepAwake";
        _keepAwake.Size = new Size(165, 19);
        _keepAwake.TabIndex = 0;
        _keepAwake.Text = "KEEP AWAKE UNTIL DONE";
        // 
        // _shutdownAfter
        // 
        _shutdownAfter.AutoSize = true;
        _shutdownAfter.BackColor = Color.Transparent;
        _shutdownAfter.ForeColor = Color.FromArgb(185, 201, 218);
        _shutdownAfter.Location = new Point(193, 5);
        _shutdownAfter.Margin = new Padding(2, 5, 3, 0);
        _shutdownAfter.Name = "_shutdownAfter";
        _shutdownAfter.Size = new Size(204, 19);
        _shutdownAfter.TabIndex = 1;
        _shutdownAfter.Text = "SHUT DOWN AFTER COMPLETED";
        // 
        // _fragmentReductionLabel
        // 
        _fragmentReductionLabel.Dock = DockStyle.Fill;
        _fragmentReductionLabel.ForeColor = Color.FromArgb(160, 180, 200);
        _fragmentReductionLabel.Location = new Point(3, 30);
        _fragmentReductionLabel.Name = "_fragmentReductionLabel";
        _fragmentReductionLabel.Size = new Size(1116, 68);
        _fragmentReductionLabel.TabIndex = 1;
        _fragmentReductionLabel.Text = "FRAGMENT REDUCTION: run Defragment or Optimize after Analyze";
        _fragmentReductionLabel.TextAlign = ContentAlignment.MiddleLeft;
        _fragmentReductionLabel.Visible = false;
        // 
        // _output
        // 
        _output.BackColor = Color.FromArgb(15, 21, 30);
        _output.Dock = DockStyle.Fill;
        _output.Font = new Font("Consolas", 9F);
        _output.ForeColor = Color.FromArgb(185, 201, 218);
        _output.Location = new Point(19, 487);
        _output.Multiline = true;
        _output.Name = "_output";
        _output.ReadOnly = true;
        _output.ScrollBars = ScrollBars.Vertical;
        _output.Size = new Size(1122, 294);
        _output.TabIndex = 5;
        _output.WordWrap = false;
        // 
        // _refreshTimer
        // 
        _refreshTimer.Interval = 1500;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.Black;
        ClientSize = new Size(1160, 800);
        Controls.Add(_root);
        ForeColor = Color.White;
        MinimumSize = new Size(900, 650);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Windows Disk Defrag Monitor";
        _root.ResumeLayout(false);
        _root.PerformLayout();
        _topPanel.ResumeLayout(false);
        _toolbar.ResumeLayout(false);
        _selectionPanel.ResumeLayout(false);
        _selectionRow.ResumeLayout(false);
        _infoPanel.ResumeLayout(false);
        _reductionPanel.ResumeLayout(false);
        _optionsPanel.ResumeLayout(false);
        _optionsPanel.PerformLayout();
        ResumeLayout(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }
}
