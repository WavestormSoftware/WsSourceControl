using System;
using FlaxEditor.GUI.Docking;
using FlaxEditor.GUI.Input;
using FlaxEditor.GUI.Tabs;
using FlaxEditor.Windows;
using FlaxEngine;
using FlaxEngine.GUI;
using WsSourceControl.Git;
using WsSourceControl.UI;
using WsSourceControl.VcsTabs;

namespace WsSourceControl
{
    /// <summary>
    /// Source Control editor window with a repository header, tabbed content,
    /// and async Git operations.
    /// </summary>
    public class WsSourceControlWindow : EditorWindow
    {
        private SourceControlHeader _header;
        private Tabs _tabs;
        private GitAsyncWrapper _asyncWrapper;

        // Tab content handlers
        private ChangesTab _changesTab;
        private HistoryTab _historyTab;
        private BranchesTab _branchesTab;
        private SyncTab _syncTab;
        private EmptyState _noRepoState;

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
                statusText => _header?.UpdateStatus(statusText, statusText != "Ready"),
                error => 
                {
                    Debug.LogError($"Git error: {error}");
                    FlaxEngine.MessageBox.Show(error, "Git Error", FlaxEngine.MessageBoxButtons.OK, FlaxEngine.MessageBoxIcon.Error);
                });

            _header = new SourceControlHeader
            {
                Parent = this,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(0, 0, 0, SourceControlTheme.HeaderHeight),
            };
            _header.AddCommand("Refresh", RefreshAllData, "Refresh source control data (F5)");
            _header.AddCommand("Fetch", () => _asyncWrapper.RunAsync(GitWrapper.Fetch, res => { if (res.Success) RefreshAllData(); }, "Fetching..."), "Fetch from remote");
            _header.AddCommand("Pull", () => _asyncWrapper.RunAsync(GitWrapper.Pull, res => { if (res.Success) RefreshAllData(); }, "Pulling..."), "Pull current branch");
            _header.AddCommand("Push", () => _asyncWrapper.RunAsync(GitWrapper.Push, res => { if (res.Success) RefreshAllData(); }, "Pushing..."), "Push current branch");
            _header.RefreshFromGit();

            _tabs = new Tabs
            {
                Orientation = Orientation.Horizontal,
                UseScroll = true,
                TabsSize = new Float2(120, SourceControlTheme.TabsHeight),
                Parent = this,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, SourceControlTheme.HeaderHeight, 0),
            };

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
            _syncTab.Build(syncTabRef, _asyncWrapper);
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

            _header = null;
            _tabs = null;
            _asyncWrapper?.Dispose();
            _asyncWrapper = null;
            _changesTab = null;
            _historyTab = null;
            _branchesTab = null;
            _syncTab = null;
            _changesTabRef = null;
            _noRepoState = null;

            BuildUI();
        }


        /// <summary>
        /// When changes tab modifies data (commit, stage, discard), refresh everything.
        /// </summary>
        private void OnChangesDataChanged()
        {
            _historyTab?.RefreshData();
            _header?.RefreshFromGit();
            UpdateChangesBadge();
        }

        /// <summary>
        /// When branches tab changes branch, refresh everything.
        /// </summary>
        private void OnBranchesDataChanged()
        {
            _changesTab?.RefreshData();
            _historyTab?.RefreshData();
            _header?.RefreshFromGit();
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
            _header?.RefreshFromGit();
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
            _header?.RefreshFromGit();
            _header?.UpdateStatus("Ready", false);
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
            if (_noRepoState != null)
                _noRepoState.Bounds = new Rectangle((Width - _noRepoState.Width) * 0.5f, (Height - _noRepoState.Height) * 0.5f, _noRepoState.Width, _noRepoState.Height);
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

            _noRepoState = new EmptyState(
                "Source Control",
                "Project is not inside a Git repository.",
                "Initialize Repository",
                () =>
                {
                    GitWrapper.InitRepo();
                    RebuildUI();
                },
                "Creates Flax .gitignore defaults.")
            {
                Parent = container,
                Width = 360f,
                Height = 110f,
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
