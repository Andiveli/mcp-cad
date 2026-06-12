using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace McpCad.Installer;

/// <summary>
/// WinForms GUI wizard for the installer (Batch 2 implementation).
/// Pure code (no .Designer.cs), self-contained.
/// 4 pages: Welcome, Selection (with Back button), Progress, Finish.
/// Functional Back navigation between pages.
/// Recommended set pre-checked by default.
/// Special handling for "CAD Skills" and "Backups" (updates State.BackupsEnabled).
/// Progress uses duplicated execution loop (per locked decision: no extraction, no touch to any TUI code in Program.cs).
/// Real reuse: calls agent.Run?.Invoke(state, agent) exactly as before.
/// Threading: Task for install work + Control.Invoke for UI updates.
/// Electric orange accents (#FF5F00).
/// State path: uses exact same "scripts/tui/state.json" (unchanged per lock).
/// </summary>
public class InstallerWizardForm : Form
{
    private readonly McpAgent[] _agents;
    private readonly State _state;
    private readonly string _serverPath;
    private readonly string _statePath = "scripts/tui/state.json"; // Exact same as original, unchanged per user lock

    private int _currentStep = 0; // 0=Welcome, 1=Selection, 2=Progress, 3=Finish
    private readonly Panel _welcomePanel = new();
    private readonly Panel _selectionPanel = new();
    private readonly Panel _progressPanel = new();
    private readonly Panel _finishPanel = new();

    private readonly List<CheckBox> _agentCheckBoxes = new();
    private readonly ListView _progressListView = new();
    private readonly ListView _finishListView = new();
    private readonly Button _backButton = new();
    private readonly Button _nextButton = new();
    private readonly Button _exitButton = new();
    private readonly Label _statusLabel = new();
    private readonly Label _finishSummaryLabel = new();
    private Button? _startInstallButton;
    private bool _installLaunched;

    private readonly List<(string Name, bool Success, string Message)> _results = new();

    // Recommended set (from original logic and spec)
    private static readonly string[] Recommended = { "Claude", "Cursor", "Grok", "OpenCode", "CAD Skills" };

    // Electric orange
    private static readonly Color ElectricOrange = Color.FromArgb(255, 95, 0);

    private const int NavBarHeight = 58;
    private const int ContentBottomPadding = 24;

    public InstallerWizardForm(McpAgent[] agents, State state, string serverPath)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _serverPath = serverPath ?? "";

        // Pre-check recommended + respect any prior Selected / auto-detect (per spec)
        foreach (var agent in _agents)
        {
            if (Recommended.Contains(agent.Name, StringComparer.OrdinalIgnoreCase))
            {
                agent.Selected = true;
            }
        }

        InitializeComponents();
        ShowStep(0);
    }

    private void InitializeComponents()
    {
        Text = "mcp-cad Installer";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(720, 560);
        MinimumSize = new Size(640, 520);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        // Common buttons
        _backButton.Text = "Back";
        _backButton.Size = new Size(80, 30);
        _backButton.Click += (_, __) => OnBack();

        _nextButton.Text = "Continue";
        _nextButton.Size = new Size(100, 30);
        _nextButton.Click += (_, __) => OnNext();

        _exitButton.Text = "Exit";
        _exitButton.Size = new Size(80, 30);
        _exitButton.Click += (_, __) => Close();

        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = ElectricOrange;

        // Build each page (simple panels toggled by visibility)
        BuildWelcomePanel();
        BuildSelectionPanel();
        BuildProgressPanel();
        BuildFinishPanel();

        // Add all panels (stacked, visibility controlled)
        Controls.Add(_welcomePanel);
        Controls.Add(_selectionPanel);
        Controls.Add(_progressPanel);
        Controls.Add(_finishPanel);

        // Bottom nav bar (extra height + padding so content does not hug the lower edge)
        var navPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = NavBarHeight,
            Padding = new Padding(16, 10, 16, 14),
            BackColor = Color.FromArgb(245, 245, 245)
        };
        navPanel.Controls.Add(_backButton);
        navPanel.Controls.Add(_nextButton);
        navPanel.Controls.Add(_exitButton);
        navPanel.Controls.Add(_statusLabel);

        _backButton.Location = new Point(16, 10);
        _nextButton.Location = new Point(116, 10);
        _exitButton.Location = new Point(Width - 126, 10);
        _statusLabel.Location = new Point(236, 14);

        Controls.Add(navPanel);

        // Ensure TUI is never reached from here (GUI path only)
    }

    private void BuildWelcomePanel()
    {
        _welcomePanel.Dock = DockStyle.Fill;
        _welcomePanel.Visible = false;

        var title = new Label
        {
            Text = "mcp-cad",
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            ForeColor = ElectricOrange,
            AutoSize = true,
            Location = new Point(40, 40)
        };

        var subtitle = new Label
        {
            Text = "AI + Autodesk Inventor",
            Font = new Font("Segoe UI", 14),
            ForeColor = Color.DimGray,
            AutoSize = true,
            Location = new Point(40, 90)
        };

        var explanation = new Label
        {
            Text = "Connect your AI tools (Claude, Cursor, Grok, etc.) to Autodesk Inventor.\nNo terminal or complex setup needed.\n\nDownload the portable package, double-click, and follow the simple wizard.\n\nThe mcp-cad server gives your AI direct parametric control over Inventor\n(sketch, extrude, patterns, assemblies, and more).",
            Font = new Font("Segoe UI", 11),
            AutoSize = true,
            MaximumSize = new Size(620, 200),
            Location = new Point(40, 140)
        };

        var nextBtn = new Button { Text = "Get Started", Size = new Size(140, 36), Location = new Point(40, 360) };
        nextBtn.Click += (_, __) => OnNext();

        var exitBtn = new Button { Text = "Exit", Size = new Size(80, 36), Location = new Point(200, 360) };
        exitBtn.Click += (_, __) => Close();

        if (string.IsNullOrEmpty(_serverPath))
        {
            var serverWarning = new Label
            {
                Text = "Server not found — place McpCad.Server.exe next to this installer before continuing.",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.DarkRed,
                AutoSize = true,
                MaximumSize = new Size(620, 60),
                Location = new Point(40, 340)
            };
            _welcomePanel.Controls.Add(serverWarning);
        }

        _welcomePanel.Controls.AddRange(new Control[] { title, subtitle, explanation, nextBtn, exitBtn });
    }

    private void BuildSelectionPanel()
    {
        _selectionPanel.Dock = DockStyle.Fill;
        _selectionPanel.Visible = false;

        var title = new Label
        {
            Text = "Select the AI tools you use",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = ElectricOrange,
            AutoSize = true,
            Location = new Point(30, 20)
        };

        var hint = new Label
        {
            Text = "Recommended agents are pre-selected. Toggle CAD Skills to deploy to all. Backups are enabled by default (quirúrgico: configs always, skills only if they exist).",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray,
            AutoSize = true,
            MaximumSize = new Size(650, 40),
            Location = new Point(30, 55)
        };

        var flow = new FlowLayoutPanel
        {
            Location = new Point(30, 100),
            Size = new Size(650, 248),
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 0, 0, ContentBottomPadding)
        };

        _agentCheckBoxes.Clear();

        foreach (var agent in _agents)
        {
            var container = new Panel { Size = new Size(620, 52), Margin = new Padding(0, 2, 0, 4) };

            var cb = new CheckBox
            {
                Text = agent.Name,
                Checked = agent.Selected,
                AutoSize = true,
                Location = new Point(0, 8),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            var desc = new Label
            {
                Text = agent.Description,
                AutoSize = true,
                Location = new Point(160, 8),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.DimGray,
                MaximumSize = new Size(455, 44)
            };

            cb.CheckedChanged += (s, e) =>
            {
                agent.Selected = cb.Checked;

                if (agent.Name == "Backups")
                {
                    _state.BackupsEnabled = cb.Checked;
                    _state.Save(_statePath);
                    desc.Text = _state.BackupsEnabled
                        ? "Enabled — uncheck to disable (recommended for safety)"
                        : "Disabled — check to enable (not recommended)";
                }

                if (_currentStep == 1)
                    _nextButton.Enabled = HasInstallableSelection();
            };

            if (agent.Name == "Backups")
            {
                cb.Checked = _state.BackupsEnabled;
                desc.Text = _state.BackupsEnabled
                    ? "Enabled — uncheck to disable (recommended for safety)"
                    : "Disabled — check to enable (not recommended)";
            }

            container.Controls.Add(cb);
            container.Controls.Add(desc);
            flow.Controls.Add(container);
            _agentCheckBoxes.Add(cb);
        }

        var helpers = new Panel { Location = new Point(30, 358), Size = new Size(650, 30) };
        var recBtn = new Button { Text = "Recommended", Size = new Size(110, 26), Location = new Point(0, 0) };
        recBtn.Click += (_, __) => SetRecommended();
        var allBtn = new Button { Text = "Select All", Size = new Size(90, 26), Location = new Point(120, 0) };
        allBtn.Click += (_, __) => SetAll(true);
        var noneBtn = new Button { Text = "None", Size = new Size(70, 26), Location = new Point(220, 0) };
        noneBtn.Click += (_, __) => SetAll(false);

        helpers.Controls.AddRange(new Control[] { recBtn, allBtn, noneBtn });

        _selectionPanel.Controls.Add(title);
        _selectionPanel.Controls.Add(hint);
        _selectionPanel.Controls.Add(flow);
        _selectionPanel.Controls.Add(helpers);

        // Navigation will be handled by common _backButton / _nextButton
    }

    private void SetRecommended()
    {
        foreach (var agent in _agents)
        {
            agent.Selected = Recommended.Contains(agent.Name, StringComparer.OrdinalIgnoreCase);
        }
        RefreshSelectionCheckboxes();
    }

    private void SetAll(bool value)
    {
        foreach (var agent in _agents)
        {
            if (agent.Name != "Backups") agent.Selected = value; // Backups is separate toggle
        }
        RefreshSelectionCheckboxes();
    }

    private void RefreshSelectionCheckboxes()
    {
        for (int i = 0; i < _agents.Length && i < _agentCheckBoxes.Count; i++)
        {
            _agentCheckBoxes[i].Checked = _agents[i].Selected;
        }
    }

    private void BuildProgressPanel()
    {
        _progressPanel.Dock = DockStyle.Fill;
        _progressPanel.Visible = false;

        var title = new Label
        {
            Text = "Installing...",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = ElectricOrange,
            AutoSize = true,
            Location = new Point(30, 20)
        };

        _progressListView.Location = new Point(30, 60);
        _progressListView.Size = new Size(650, 290);
        _progressListView.View = View.Details;
        _progressListView.FullRowSelect = true;
        _progressListView.Columns.Add("Agent", 140);
        _progressListView.Columns.Add("Status", 100);
        _progressListView.Columns.Add("Details", 400);

        _startInstallButton = new Button { Text = "Start Installation", Size = new Size(160, 32), Location = new Point(30, 368) };
        _startInstallButton.Click += async (_, __) => await RunInstallAsync(_startInstallButton);

        _progressPanel.Controls.Add(title);
        _progressPanel.Controls.Add(_progressListView);
        _progressPanel.Controls.Add(_startInstallButton);
    }

    private static bool HasInstallableSelection(McpAgent[] agents) =>
        agents.Any(a => a.Selected && a.Name != "Backups");

    private bool HasInstallableSelection() => HasInstallableSelection(_agents);

    private async Task RunInstallAsync(Button startBtn)
    {
        startBtn.Enabled = false;
        _backButton.Enabled = false;
        _nextButton.Enabled = false;

        _progressListView.Items.Clear();
        _results.Clear();

        // Collect selected (exclude Backups item itself from execution)
        var selected = _agents.Where(a => a.Selected && a.Name != "Backups").ToArray();

        if (selected.Length == 0)
        {
            MessageBox.Show("No agents selected for installation.", "mcp-cad Installer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            startBtn.Enabled = true;
            _backButton.Enabled = true;
            return;
        }

        var progressItems = selected
            .Select(agent =>
            {
                var item = new ListViewItem(new[] { agent.Name, "Pending...", "" });
                _progressListView.Items.Add(item);
                return (agent, item);
            })
            .ToArray();

        // DUPLICATED execution loop (per explicit user lock: inside the form, no extraction, no touch to TUI code)
        await Task.Run(() =>
        {
            foreach (var (agent, item) in progressItems)
            {

                UpdateProgressItem(item, agent.Name, "Running...", "");

                try
                {
                    var msg = agent.Run?.Invoke(_state, agent) ?? "unknown";
                    _results.Add((agent.Name, true, msg));
                    _state.LastAgent = agent.Name;

                    UpdateProgressItem(item, agent.Name, "OK", msg.Length > 80 ? msg[..80] + "..." : msg);
                }
                catch (Exception ex)
                {
                    _results.Add((agent.Name, false, ex.Message));
                    UpdateProgressItem(item, agent.Name, "FAIL", ex.Message.Length > 80 ? ex.Message[..80] + "..." : ex.Message);
                }
            }

            _state.Save(_statePath);
        });

        // Move to finish
        _currentStep = 3;
        ShowStep(3);
    }

    private void UpdateProgressItem(ListViewItem item, string name, string status, string details)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateProgressItem(item, name, status, details)));
            return;
        }

        item.SubItems[0].Text = name;
        item.SubItems[1].Text = status;
        item.SubItems[2].Text = details;

        if (status == "OK") item.BackColor = Color.FromArgb(220, 255, 220);
        else if (status == "FAIL") item.BackColor = Color.FromArgb(255, 220, 220);
        else if (status == "Running...") item.BackColor = Color.FromArgb(255, 250, 220);
    }

    private void BuildFinishPanel()
    {
        _finishPanel.Dock = DockStyle.Fill;
        _finishPanel.Visible = false;

        var title = new Label
        {
            Text = "Installation Complete",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = ElectricOrange,
            AutoSize = true,
            Location = new Point(30, 20)
        };

        _finishSummaryLabel.Text = "";
        _finishSummaryLabel.Font = new Font("Segoe UI", 11);
        _finishSummaryLabel.AutoSize = true;
        _finishSummaryLabel.Location = new Point(30, 55);

        _finishListView.Location = new Point(30, 90);
        _finishListView.Size = new Size(650, 200);
        _finishListView.View = View.Details;
        _finishListView.FullRowSelect = true;
        _finishListView.Columns.Add("Agent", 140);
        _finishListView.Columns.Add("Status", 80);
        _finishListView.Columns.Add("Details", 420);

        var nextSteps = new Label
        {
            Text = "Close & reopen your AI client(s). Keep Inventor running.",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = ElectricOrange,
            AutoSize = true,
            Location = new Point(30, 310)
        };

        var closeBtn = new Button { Text = "Close", Size = new Size(100, 36), Location = new Point(30, 350) };
        closeBtn.Click += (_, __) => Application.Exit();

        var backupsBtn = new Button { Text = "Open Backups Folder", Size = new Size(160, 36), Location = new Point(150, 350) };
        backupsBtn.Click += (_, __) =>
        {
            try
            {
                var root = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".mcp-cad", "backups");
                System.Diagnostics.Process.Start("explorer.exe", root);
            }
            catch { /* best effort */ }
        };

        _finishPanel.Controls.AddRange(new Control[] { title, _finishSummaryLabel, _finishListView, nextSteps, closeBtn, backupsBtn });

        // summary and list are populated in ShowStep(3)
    }

    private void ShowStep(int step)
    {
        _currentStep = step;

        _welcomePanel.Visible = step == 0;
        _selectionPanel.Visible = step == 1;
        _progressPanel.Visible = step == 2;
        _finishPanel.Visible = step == 3;

        _backButton.Visible = step > 0 && step < 3;
        _nextButton.Visible = step < 2;
        _exitButton.Visible = true;

        _backButton.Enabled = step > 0;
        _nextButton.Enabled = step < 2;

        if (step == 1)
        {
            _nextButton.Text = "Continue";
            _nextButton.Enabled = HasInstallableSelection();
        }
        else if (step == 2)
        {
            _nextButton.Text = "Next";
            _nextButton.Enabled = false; // enabled after progress or via finish
        }

        if (step == 3)
        {
            PopulateFinish();
            _backButton.Visible = false;
            _nextButton.Visible = false;
        }

        _statusLabel.Text = step switch
        {
            0 => "Welcome to the mcp-cad installer wizard",
            1 => "Select agents — recommended pre-checked",
            2 => "Running installation (reusing existing backend logic)",
            3 => "Done — follow the next steps below",
            _ => ""
        };
    }

    private void PopulateFinish()
    {
        _finishListView.Items.Clear();

        var successCount = _results.Count(r => r.Success);
        var failCount = _results.Count - successCount;

        _finishSummaryLabel.Text = failCount == 0
            ? $"Successfully configured {successCount} agent(s)."
            : $"Configured {successCount} agent(s); {failCount} had issues.";

        foreach (var (name, success, msg) in _results)
        {
            var status = success ? "OK" : "FAIL";
            var item = new ListViewItem(new[] { name, status, msg.Length > 90 ? msg[..90] + "..." : msg });
            if (!success) item.BackColor = Color.FromArgb(255, 220, 220);
            else item.BackColor = Color.FromArgb(220, 255, 220);
            _finishListView.Items.Add(item);
        }

        // If no results (e.g. only Backups or CAD Skills edge), show the agents that were selected
        if (_results.Count == 0)
        {
            var selected = _agents.Where(a => a.Selected).ToList();
            foreach (var a in selected)
            {
                _finishListView.Items.Add(new ListViewItem(new[] { a.Name, "See details in progress", "" }));
            }
        }
    }

    private void OnBack()
    {
        if (_currentStep > 0)
        {
            ShowStep(_currentStep - 1);
        }
    }

    private void OnNext()
    {
        if (_currentStep == 1)
        {
            if (_installLaunched)
                return;

            if (!HasInstallableSelection())
            {
                MessageBox.Show("Please select at least one agent (or CAD Skills).", "mcp-cad Installer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_serverPath))
            {
                MessageBox.Show(
                    "Could not locate McpCad.Server.exe next to the installer.\n\n" +
                    "Extract the full portable package so McpCad.Server.exe and McpCad.Installer.exe are in the same folder.",
                    "mcp-cad Installer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            _installLaunched = true;
            _nextButton.Enabled = false;
            ShowStep(2);
            if (_startInstallButton is not null)
                _ = RunInstallAsync(_startInstallButton);
        }
        else if (_currentStep < 3 && !_installLaunched)
        {
            ShowStep(_currentStep + 1);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        // Ensure clean exit for the GUI path (TUI path is never reached from here)
    }
}
