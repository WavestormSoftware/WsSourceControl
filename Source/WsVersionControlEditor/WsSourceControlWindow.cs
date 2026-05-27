using System;
using FlaxEditor.GUI;
using FlaxEditor.GUI.ContextMenu;
using FlaxEditor.GUI.Docking;
using FlaxEditor.GUI.Input;
using FlaxEditor.GUI.Tabs;
using FlaxEditor.Windows;
using FlaxEngine;
using FlaxEngine.GUI;
using WsVersionControlEditor.Git;
using WsVersionControlEditor.VcsTabs;
using WsVersionControlEditor.UI;

namespace WsVersionControlEditor
{
    /// <summary>
    /// Redesigned Source Control window for Flax Editor.
    /// 
    /// Architecture:
    ///   - StatusBar at top: branch, sync status, remote, operation state
    ///   - Tabs: Changes (4-quadrant split), History, Branches, Sync
    ///   - Tab content delegated to separate classes for modularity
    ///   - GitAsyncWrapper integration for non-blocking operations
    ///   - Cross-tab refresh via DataChanged events
    /// 
    /// Layout:
    ///   ┌─ StatusBar ─────────────────────────────────────────────┐
    ///   │ main  Up 2 Down 1  │  origin: github.com/...  │ Ready  │
    ///   └─────────────────────────────────────────────────────────┘
    ///   ┌─ ToolStrip ─────────────────────────────────────────────┐
    ///   │ [Refresh]                                               │
    ///   └─────────────────────────────────────────────────────────┘
    ///   ┌─ Tabs ──────────────────────────────────────────────────┐
    ///   │ [Changes (3)]  [History]  [Branches]  [Sync]          │
    ///   │                                                         │
    ///   │  (Tab Content Area)                                    │
    ///   │                                                         │
    ///   └─────────────────────────────────────────────────────────┘
    /// </summary>
    public class WsSourceControlWindow : EditorWindow
    {
        private VcsStatusBar _statusBar;
        private Tabs _tabs;
        private ToolStrip _toolStrip;
        private GitAsyncWrapper _asyncWrapper;

        // Tab content handlers
        private ChangesTab _changesTab;
        private HistoryTab _historyTab;
        private BranchesTab _branchesTab;
        private SyncTab _syncTab;

        // Tab references for badge updates
        private Tab _changesTabRef;

        public WsSourceControlWindow() : base(FlaxEditor.Editor.Instance, false, ScrollBars.None)
        {
            Title = "Source Control";
            BuildUI();
        }

        private void BuildUI()
        {
            if (!GitWrapper.IsGitRepo())
            {
                BuildNoRepoLayout();
                return;
            }

            // Initialize async wrapper for non-blocking git ops
            _asyncWrapper = new GitAsyncWrapper(
                statusText => _statusBar?.UpdateStatus(statusText, true),
                error => 
                {
                    Debug.LogError($"Git error: {error}");
                    FlaxEngine.MessageBox.Show(error, "Git Error", FlaxEngine.MessageBoxButtons.OK, FlaxEngine.MessageBoxIcon.Error);
                });

            _toolStrip = new ToolStrip
            {
                Parent = this,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(0, 0, 0, 28),
            };

            var refreshBtn = _toolStrip.AddButton("Refresh");
            refreshBtn.Clicked += RefreshAllData;

            _toolStrip.AddSeparator();

            var fetchBtn = _toolStrip.AddButton("Fetch");
            fetchBtn.Clicked += () => _asyncWrapper.RunAsync(GitWrapper.Fetch, res => { if (res.Success) RefreshAllData(); }, "Fetching...");

            var pullBtn = _toolStrip.AddButton("Pull");
            pullBtn.Clicked += () => _asyncWrapper.RunAsync(GitWrapper.Pull, res => { if (res.Success) RefreshAllData(); }, "Pulling...");

            var pushBtn = _toolStrip.AddButton("Push");
            pushBtn.Clicked += () => _asyncWrapper.RunAsync(GitWrapper.Push, res => { if (res.Success) RefreshAllData(); }, "Pushing...");

            _tabs = new Tabs
            {
                Orientation = Orientation.Horizontal,
                UseScroll = true,
                TabsSize = new Float2(120, 28),
                Parent = this,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, 28, 28), // Starts below ToolStrip (28), leaves 28 for StatusBar
            };

            _statusBar = new VcsStatusBar
            {
                Parent = this,
                AnchorPreset = AnchorPresets.HorizontalStretchBottom,
                Offsets = new Margin(0, 0, -28, 28), // Bottom docked, 28px tall
            };
            _statusBar.RefreshFromGit();

            // Changes tab (primary)
            _changesTabRef = _tabs.AddTab(new Tab("Changes"));
            _changesTab = new ChangesTab();
            _changesTab.Build(_changesTabRef);
            _changesTab.DataChanged += OnChangesDataChanged;

            // History tab
            var historyTabRef = _tabs.AddTab(new Tab("History"));
            _historyTab = new HistoryTab();
            _historyTab.Build(historyTabRef);

            // Branches tab
            var branchesTabRef = _tabs.AddTab(new Tab("Branches"));
            _branchesTab = new BranchesTab();
            _branchesTab.Build(branchesTabRef);
            _branchesTab.DataChanged += OnBranchesDataChanged;

            // Sync tab
            var syncTabRef = _tabs.AddTab(new Tab("Sync"));
            _syncTab = new SyncTab();
            _syncTab.Build(syncTabRef);
            _syncTab.DataChanged += OnSyncDataChanged;

            // Initial data load
            RefreshAllData();
        }

        /// <summary>
        /// Rebuild the entire UI (e.g., after git init).
        /// </summary>
        private void RebuildUI()
        {
            for (int i = ChildrenCount - 1; i >= 0; i--)
                Children[i].Dispose();

            _statusBar = null;
            _tabs = null;
            _toolStrip = null;
            _asyncWrapper?.Dispose();
            _asyncWrapper = null;
            _changesTab = null;
            _historyTab = null;
            _branchesTab = null;
            _syncTab = null;
            _changesTabRef = null;

            BuildUI();
        }


        /// <summary>
        /// When changes tab modifies data (commit, stage, discard), refresh everything.
        /// </summary>
        private void OnChangesDataChanged()
        {
            _historyTab?.RefreshData();
            _statusBar?.RefreshFromGit();
            UpdateChangesBadge();
        }

        /// <summary>
        /// When branches tab changes branch, refresh everything.
        /// </summary>
        private void OnBranchesDataChanged()
        {
            _changesTab?.RefreshData();
            _historyTab?.RefreshData();
            _statusBar?.RefreshFromGit();
            UpdateChangesBadge();
        }

        /// <summary>
        /// When sync tab performs remote ops, refresh everything.
        /// </summary>
        private void OnSyncDataChanged()
        {
            _changesTab?.RefreshData();
            _historyTab?.RefreshData();
            _branchesTab?.RefreshData();
            _statusBar?.RefreshFromGit();
            UpdateChangesBadge();
        }

        /// <summary>
        /// Refresh all tab data and the status bar.
        /// </summary>
        public void RefreshAllData()
        {
            _changesTab?.RefreshData();
            _historyTab?.RefreshData();
            _branchesTab?.RefreshData();
            _syncTab?.RefreshData();
            _statusBar?.RefreshFromGit();
            _statusBar?.UpdateStatus("Ready", false);
            UpdateChangesBadge();
        }

        /// <summary>
        /// Update the Changes tab badge to show the number of unstaged changes.
        /// </summary>
        private void UpdateChangesBadge()
        {
            if (_changesTabRef == null) return;
            var changes = GitWrapper.GetStatus();
            int total = 0;
            foreach (var c in changes)
                if (!c.Staged) total++;
            _changesTabRef.Text = total > 0 ? $"Changes ({total})" : "Changes";
        }


        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            _changesTab?.Update(deltaTime);
        }

        public override bool OnKeyDown(KeyboardKeys key)
        {
            // F5 = Refresh
            if (key == KeyboardKeys.F5)
            {
                RefreshAllData();
                return true;
            }

            return base.OnKeyDown(key);
        }


        private void BuildNoRepoLayout()
        {
            var container = new ContainerControl
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                Parent = this,
            };

            var content = new VerticalPanel
            {
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
                AutoSize = true,
                Spacing = 8f,
                Margin = new Margin(16f),
                Parent = container,
            };

            // Center the content visually
            var title = new Label
            {
                Parent = content,
                Text = "Source Control",
                TextColor = Style.Current.Foreground,
                HorizontalAlignment = TextAlignment.Center,
                AutoWidth = true,
                Height = 32,
                Margin = new Margin(0, 0, 16, 8),
            };

            var infoLabel = new Label
            {
                Parent = content,
                Text = "This project is not inside a Git repository.\nInitialize a repository to start using source control.",
                TextColor = Style.Current.ForegroundGrey,
                HorizontalAlignment = TextAlignment.Center,
                AutoWidth = true,
                Margin = new Margin(0, 0, 4, 4),
            };

            var initBtn = new Button
            {
                Parent = content,
                Text = "Initialize Git Repository",
                Width = 200,
                Height = 32,
                X = 50, // slight offset for centering
                BackgroundColor = Style.Current.BackgroundSelected,
                BackgroundColorHighlighted = Style.Current.BackgroundSelected.RGBMultiplied(1.2f),
            };
            initBtn.Clicked += () =>
            {
                GitWrapper.InitRepo();
                RebuildUI();
            };
        }


        public override void OnDestroy()
        {
            _asyncWrapper?.Dispose();
            _asyncWrapper = null;
            base.OnDestroy();
        }
    }
}
