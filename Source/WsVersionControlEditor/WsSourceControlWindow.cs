using System;
using System.Collections.Generic;
using System.Text;
using FlaxEditor.CustomEditors.Elements;
using FlaxEditor.GUI;
using FlaxEditor.GUI.ContextMenu;
using FlaxEditor.GUI.Input;
using FlaxEditor.GUI.Tabs;
using FlaxEditor.GUI.Tree;
using FlaxEditor.Windows;
using FlaxEngine;
using FlaxEngine.GUI;
using WsVersionControlEditor.Git;

namespace WsVersionControlEditor
{
    public class WsSourceControlWindow : EditorWindow
    {
        private Tabs _tabs;
        private Tree _stagedTree;
        private Tree _unstagedTree;
        private TextBox _commitMessage;
        private CheckBox _amendCheck;
        private TextBox _diffTextBox;
        private Label _diffFileLabel;
        private Tree _historyTree;
        private SearchBox _historySearch;
        private TextBox _historyDetailText;
        private Tree _branchTree;
        private TextBox _newBranchBox;
        private Label _remoteLabel;
        private ContainerControl _stashContainer;
        private ContainerControl _conflictContainer;

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

            _tabs = new Tabs
            {
                Orientation = Orientation.Horizontal,
                TabsSize = new Float2(100, 28),
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                Parent = this,
            };

            BuildChangesTab(_tabs.AddTab(new Tab("Changes")));
            BuildHistoryTab(_tabs.AddTab(new Tab("History")));
            BuildBranchesTab(_tabs.AddTab(new Tab("Branches")));
            BuildSyncTab(_tabs.AddTab(new Tab("Sync")));

            RefreshData();
        }

        private VerticalPanel CreateScrollContent(Tab tab)
        {
            var scroll = new Panel(ScrollBars.Vertical)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                Parent = tab,
            };
            var content = new VerticalPanel
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                IsScrollable = true,
                AutoSize = true,
                Spacing = 3f,
                Margin = new Margin(3f),
                Parent = scroll,
            };
            return content;
        }

        private void RebuildUI()
        {
            for (int i = ChildrenCount - 1; i >= 0; i--)
                Children[i].Dispose();

            _tabs = null;
            _stagedTree = null;
            _unstagedTree = null;
            _commitMessage = null;
            _amendCheck = null;
            _diffTextBox = null;
            _diffFileLabel = null;
            _historyTree = null;
            _historySearch = null;
            _historyDetailText = null;
            _branchTree = null;
            _newBranchBox = null;
            _remoteLabel = null;
            _stashContainer = null;
            _conflictContainer = null;

            BuildUI();
        }

        private void RefreshData()
        {
            PopulateChangesTrees();
            PopulateHistoryTree();
            PopulateBranchTree();
            PopulateStashList();
            PopulateConflictSection();

            if (_remoteLabel != null)
            {
                string remote = GitWrapper.GetRemoteUrl();
                _remoteLabel.Text = string.IsNullOrEmpty(remote) ? "(No remote configured)" : remote;
            }
        }

        #region No Repo

        private void BuildNoRepoLayout()
        {
            var scroll = new Panel(ScrollBars.Vertical)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                Parent = this,
            };
            var content = new VerticalPanel
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                IsScrollable = true,
                AutoSize = true,
                Spacing = 3f,
                Margin = new Margin(3f),
                Parent = scroll,
            };

            var group = new GroupElement();
            group.Panel.HeaderText = "Git Repository";
            group.Panel.Parent = content;
            group.Label("This project is not inside a Git repository.").Label.HorizontalAlignment = TextAlignment.Center;
            var initBtn = group.Button("Initialize Git Repository");
            initBtn.Button.Clicked += () =>
            {
                GitWrapper.InitRepo();
                RebuildUI();
            };
        }

        #endregion

        #region Changes Tab

        private void BuildChangesTab(Tab tab)
        {
            var content = CreateScrollContent(tab);

            string branch = GitWrapper.GetCurrentBranch();
            GitWrapper.GetAheadBehind(out int ahead, out int behind);
            string syncInfo = branch;
            if (ahead > 0 || behind > 0)
                syncInfo += $"  (↑{ahead} ↓{behind})";

            var branchGroup = new GroupElement();
            branchGroup.Panel.HeaderText = $"Branch: {syncInfo}";
            branchGroup.Panel.Parent = content;
            var refreshBtn = branchGroup.Button("Refresh");
            refreshBtn.Button.Clicked += () => RefreshData();

            var stagedGroup = new GroupElement();
            stagedGroup.Panel.HeaderText = "Staged Changes";
            stagedGroup.Panel.Parent = content;
            var unstageAllBtn = stagedGroup.Button("Unstage All");
            unstageAllBtn.Button.Clicked += () =>
            {
                var changes = GitWrapper.GetStatus();
                var paths = new List<string>();
                foreach (var c in changes)
                    if (c.Staged) paths.Add(c.FilePath);
                if (paths.Count > 0)
                {
                    GitWrapper.Unstage(paths.ToArray());
                    RefreshData();
                }
            };
            _stagedTree = new Tree(false) { IsScrollable = true, Parent = stagedGroup.Panel, Height = 120 };
            _stagedTree.SelectedChanged += (b, a) => OnFileSelected(a, true);
            _stagedTree.RightClick += (n, l) => OnFileRightClick(n, l, true, _stagedTree);

            var unstagedGroup = new GroupElement();
            unstagedGroup.Panel.HeaderText = "Unstaged Changes";
            unstagedGroup.Panel.Parent = content;
            var stageAllBtn = unstagedGroup.Button("Stage All");
            stageAllBtn.Button.Clicked += () =>
            {
                GitWrapper.AddAll();
                RefreshData();
            };
            _unstagedTree = new Tree(false) { IsScrollable = true, Parent = unstagedGroup.Panel, Height = 120 };
            _unstagedTree.SelectedChanged += (b, a) => OnFileSelected(a, false);
            _unstagedTree.RightClick += (n, l) => OnFileRightClick(n, l, false, _unstagedTree);

            var diffGroup = new GroupElement();
            diffGroup.Panel.HeaderText = "Diff";
            diffGroup.Panel.Parent = content;
            _diffFileLabel = new Label
            {
                Parent = diffGroup.Panel,
                Text = "(select a file)",
                TextColor = FlaxEngine.GUI.Style.Current.ForegroundGrey,
                AutoWidth = true,
                IsScrollable = true,
                Margin = new Margin(4, 0, 2, 2),
            };
            _diffTextBox = new TextBox(true, 0, 0, 0)
            {
                Parent = diffGroup.Panel,
                IsReadOnly = true,
                IsScrollable = true,
                Height = 100,
                BackgroundColor = FlaxEngine.GUI.Style.Current.TextBoxBackground,
                BorderColor = FlaxEngine.GUI.Style.Current.BorderNormal,
                TextColor = FlaxEngine.GUI.Style.Current.Foreground,
            };

            var commitGroup = new GroupElement();
            commitGroup.Panel.HeaderText = "Commit";
            commitGroup.Panel.Parent = content;
            _commitMessage = new TextBox(true, 0, 0, 0)
            {
                Parent = commitGroup.Panel,
                IsMultiline = true,
                IsScrollable = true,
                Height = 48,
                WatermarkText = "Enter commit message...",
            };

            _amendCheck = commitGroup.Checkbox("Amend").CheckBox;
            var commitBtn = commitGroup.Button("Commit Staged");
            commitBtn.Button.BackgroundColor = FlaxEngine.GUI.Style.Current.BackgroundSelected;
            commitBtn.Button.BackgroundColorHighlighted = FlaxEngine.GUI.Style.Current.BackgroundSelected.RGBMultiplied(1.2f);
            commitBtn.Button.Clicked += OnCommit;
        }

        private void OnFileSelected(List<TreeNode> selection, bool staged)
        {
            if (selection == null || selection.Count == 0 || selection[0] is not ChangeTreeNode node) return;
            _diffFileLabel.Text = node.Change.FilePath;
            string diff = staged ? GitWrapper.GetDiffStaged(node.Change.FilePath) : GitWrapper.GetDiff(node.Change.FilePath);
            _diffTextBox.Text = string.IsNullOrEmpty(diff) ? "(No diff available)" : diff;
        }

        private void OnFileRightClick(TreeNode node, Float2 location, bool isStaged, Tree tree)
        {
            if (node is not ChangeTreeNode cn) return;
            var menu = new ContextMenu();

            if (isStaged)
            {
                menu.AddButton("Unstage", () => { GitWrapper.Unstage(new[] { cn.Change.FilePath }); RefreshData(); });
                menu.AddButton("View Diff (Staged)", () =>
                {
                    _diffFileLabel.Text = cn.Change.FilePath;
                    _diffTextBox.Text = GitWrapper.GetDiffStaged(cn.Change.FilePath) ?? "(No diff)";
                });
            }
            else
            {
                menu.AddButton("Stage", () => { GitWrapper.Add(new[] { cn.Change.FilePath }); RefreshData(); });
                menu.AddButton("Discard Changes", () => { GitWrapper.Reset(cn.Change.FilePath); RefreshData(); });
                menu.AddButton("View Diff", () =>
                {
                    _diffFileLabel.Text = cn.Change.FilePath;
                    _diffTextBox.Text = GitWrapper.GetDiff(cn.Change.FilePath) ?? "(No diff)";
                });
            }

            menu.AddSeparator();
            menu.AddButton("Open in Explorer", () =>
            {
                var fullPath = System.IO.Path.Combine(GitWrapper.ProjectPath, cn.Change.FilePath);
                if (System.IO.File.Exists(fullPath))
                    FileSystem.ShowFileExplorer(fullPath);
            });

            menu.Show(tree, location);
        }

        private void OnCommit()
        {
            string msg = _commitMessage?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(msg))
            {
                Debug.LogWarning("Please enter a commit message.");
                return;
            }

            bool amend = _amendCheck?.Checked ?? false;
            if (amend)
            {
                GitWrapper.CommitAmend(msg);
                _commitMessage.Text = string.Empty;
            }
            else
            {
                GitWrapper.Commit(msg);
                _commitMessage.Text = string.Empty;
            }
            RefreshData();
        }

        private void PopulateChangesTrees()
        {
            if (_stagedTree == null || _unstagedTree == null) return;
            _stagedTree.DisposeChildren();
            _unstagedTree.DisposeChildren();

            var changes = GitWrapper.GetStatus();
            foreach (var change in changes)
            {
                var node = new ChangeTreeNode(change);
                node.Parent = change.Staged ? _stagedTree : _unstagedTree;
            }
            _stagedTree.PerformLayout();
            _unstagedTree.PerformLayout();
        }

        #endregion

        #region History Tab

        private void BuildHistoryTab(Tab tab)
        {
            var split = new SplitPanel(Orientation.Horizontal, ScrollBars.Vertical, ScrollBars.Vertical)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                SplitterValue = 0.4f,
                Parent = tab,
            };

            var leftContent = new VerticalPanel
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                IsScrollable = true,
                AutoSize = true,
                Spacing = 3f,
                Margin = new Margin(3f),
                Parent = split.Panel1,
            };

            _historySearch = new SearchBox
            {
                IsScrollable = true,
                Height = 20,
                Parent = leftContent,
            };
            _historySearch.TextChanged += () => PopulateHistoryTree();

            _historyTree = new Tree(false)
            {
                IsScrollable = true,
                Parent = split.Panel1,
                Height = 300,
            };
            _historyTree.SelectedChanged += OnHistorySelectionChanged;

            _historyDetailText = new TextBox(true, 0, 0, 0)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(4, 4, 4, 4),
                IsReadOnly = true,
                Parent = split.Panel2,
                BackgroundColor = FlaxEngine.GUI.Style.Current.TextBoxBackground,
                BorderColor = FlaxEngine.GUI.Style.Current.BorderNormal,
                TextColor = FlaxEngine.GUI.Style.Current.Foreground,
            };
        }

        private void PopulateHistoryTree()
        {
            if (_historyTree == null) return;
            _historyTree.DisposeChildren();

            var log = GitWrapper.GetLog(50);
            string filter = _historySearch?.Text?.Trim() ?? string.Empty;

            foreach (var entry in log)
            {
                if (!string.IsNullOrEmpty(filter))
                {
                    bool match = entry.Hash.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                 entry.Author.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                 entry.Message.Contains(filter, StringComparison.OrdinalIgnoreCase);
                    if (!match) continue;
                }
                new CommitTreeNode(entry).Parent = _historyTree;
            }
            _historyTree.PerformLayout();
        }

        private void OnHistorySelectionChanged(List<TreeNode> before, List<TreeNode> after)
        {
            if (after == null || after.Count == 0 || after[0] is not CommitTreeNode node) return;
            if (_historyDetailText == null) return;

            string detail = GitWrapper.GetCommitDetail(node.Entry.Hash);
            var files = GitWrapper.GetCommitChangedFiles(node.Entry.Hash);

            var sb = new StringBuilder();
            sb.AppendLine($"Commit:  {node.Entry.Hash}");
            sb.AppendLine($"Author:  {node.Entry.Author}");
            sb.AppendLine($"Date:    {node.Entry.Date}");
            sb.AppendLine($"Message: {node.Entry.Message}");
            sb.AppendLine();
            if (files.Count > 0)
            {
                sb.AppendLine("Changed Files:");
                foreach (var f in files)
                    sb.AppendLine($"  {f}");
                sb.AppendLine();
            }
            sb.Append(detail);
            _historyDetailText.Text = sb.ToString();
        }

        #endregion

        #region Branches Tab

        private void BuildBranchesTab(Tab tab)
        {
            var content = CreateScrollContent(tab);

            var localGroup = new GroupElement();
            localGroup.Panel.HeaderText = "Local Branches";
            localGroup.Panel.Parent = content;
            _branchTree = new Tree(false) { IsScrollable = true, Parent = localGroup.Panel, Height = 200 };
            _branchTree.RightClick += OnBranchRightClick;

            var remoteGroup = new GroupElement();
            remoteGroup.Panel.HeaderText = "Remote";
            remoteGroup.Panel.Parent = content;
            _remoteLabel = new Label
            {
                Parent = remoteGroup.Panel,
                Text = GitWrapper.GetRemoteUrl() ?? "(none)",
                TextColor = FlaxEngine.GUI.Style.Current.ForegroundGrey,
                AutoWidth = true,
                IsScrollable = true,
                Margin = new Margin(4, 0, 2, 4),
            };

            var createGroup = new GroupElement();
            createGroup.Panel.HeaderText = "Create Branch";
            createGroup.Panel.Parent = content;
            _newBranchBox = createGroup.TextBox().TextBox;
            _newBranchBox.WatermarkText = "Branch name...";
            var createBtn = createGroup.Button("Create & Checkout");
            createBtn.Button.BackgroundColor = FlaxEngine.GUI.Style.Current.BackgroundSelected;
            createBtn.Button.BackgroundColorHighlighted = FlaxEngine.GUI.Style.Current.BackgroundSelected.RGBMultiplied(1.2f);
            createBtn.Button.Clicked += OnCreateBranch;
        }

        private void PopulateBranchTree()
        {
            if (_branchTree == null) return;
            _branchTree.DisposeChildren();

            var localBranches = GitWrapper.GetBranches();
            string currentBranch = GitWrapper.GetCurrentBranch();

            foreach (var branch in localBranches)
            {
                bool isCurrent = branch == currentBranch && !GitWrapper.IsDetachedHead();
                new BranchTreeNode(branch, isCurrent, false).Parent = _branchTree;
            }

            var remoteBranches = GitWrapper.GetRemoteBranches();
            if (remoteBranches.Count > 0)
            {
                var remoteNode = new TreeNode { Text = "Remote" };
                remoteNode.Parent = _branchTree;
                remoteNode.Expand();

                foreach (var branch in remoteBranches)
                    new BranchTreeNode(branch, false, true).Parent = remoteNode;
            }

            _branchTree.PerformLayout();
        }

        private void OnBranchRightClick(TreeNode node, Float2 location)
        {
            if (node is not BranchTreeNode branchNode) return;
            var menu = new ContextMenu();

            if (!branchNode.IsRemote && !branchNode.IsCurrent)
            {
                menu.AddButton("Checkout", () => { GitWrapper.CheckoutBranch(branchNode.BranchName); RefreshData(); });
                menu.AddButton("Delete", () => { GitWrapper.DeleteBranch(branchNode.BranchName); RefreshData(); });
            }
            else if (branchNode.IsCurrent)
            {
                menu.AddButton("Create From Here...", () =>
                {
                    if (_newBranchBox != null)
                    {
                        _newBranchBox.Text = string.Empty;
                        _newBranchBox.Focus();
                        if (_tabs != null) _tabs.SelectedTabIndex = 2;
                    }
                });
            }

            menu.Show(_branchTree, location);
        }

        private void OnCreateBranch()
        {
            string name = _newBranchBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                Debug.LogWarning("Branch name cannot be empty.");
                return;
            }
            GitWrapper.CreateBranch(name);
            _newBranchBox.Text = string.Empty;
            RefreshData();
        }

        #endregion

        #region Sync Tab

        private void BuildSyncTab(Tab tab)
        {
            var content = CreateScrollContent(tab);

            var syncGroup = new GroupElement();
            syncGroup.Panel.HeaderText = "Sync";
            syncGroup.Panel.Parent = content;

            var fetchBtn = syncGroup.Button("Fetch");
            fetchBtn.Button.Clicked += () => { GitWrapper.Fetch(); RefreshData(); };

            var pullBtn = syncGroup.Button("Pull");
            pullBtn.Button.BackgroundColor = new Color(0.2f, 0.55f, 0.2f);
            pullBtn.Button.BackgroundColorHighlighted = new Color(0.25f, 0.65f, 0.25f);
            pullBtn.Button.Clicked += () => { GitWrapper.Pull(); RefreshData(); };

            var pushBtn = syncGroup.Button("Push");
            pushBtn.Button.BackgroundColor = new Color(0.55f, 0.3f, 0.15f);
            pushBtn.Button.BackgroundColorHighlighted = new Color(0.65f, 0.35f, 0.18f);
            pushBtn.Button.Clicked += () => { GitWrapper.Push(); RefreshData(); };

            var stashBtn = syncGroup.Button("Stash");
            stashBtn.Button.Clicked += () => { GitWrapper.Stash(); RefreshData(); };

            var popBtn = syncGroup.Button("Stash Pop");
            popBtn.Button.Clicked += () => { GitWrapper.StashPop(); RefreshData(); };

            var stashGroup = new GroupElement();
            stashGroup.Panel.HeaderText = "Stashes";
            stashGroup.Panel.Parent = content;
            _stashContainer = stashGroup.Panel;

            var conflictGroup = new GroupElement();
            conflictGroup.Panel.HeaderText = "Conflicts";
            conflictGroup.Panel.Parent = content;
            _conflictContainer = conflictGroup.Panel;
        }

        private void PopulateStashList()
        {
            if (_stashContainer == null) return;

            for (int i = _stashContainer.ChildrenCount - 1; i >= 0; i--)
            {
                var child = _stashContainer.Children[i];
                if (child is not DropPanel)
                    child.Dispose();
            }

            var stashes = GitWrapper.GetStashList();
            if (stashes.Count == 0)
            {
                new Label
                {
                    Text = "No stashes.",
                    Parent = _stashContainer,
                    TextColor = FlaxEngine.GUI.Style.Current.ForegroundGrey,
                    AutoWidth = true,
                    IsScrollable = true,
                    Margin = new Margin(4, 0, 2, 4),
                };
                return;
            }

            foreach (var stash in stashes)
            {
                var row = new HorizontalPanel
                {
                    Parent = _stashContainer,
                    Height = 26,
                    IsScrollable = true,
                    Offsets = new Margin(2, 2, 1, 1),
                };

                new Label
                {
                    Text = $"stash@{{{stash.Index}}}: {stash.Message}",
                    Parent = row,
                    AutoWidth = true,
                    IsScrollable = true,
                    TextColor = FlaxEngine.GUI.Style.Current.Foreground,
                };

                var popBtn = new Button
                {
                    Text = "Pop",
                    Parent = row,
                    Width = 44,
                    Height = 20,
                    IsScrollable = true,
                    BackgroundColor = FlaxEngine.GUI.Style.Current.BackgroundNormal,
                    BackgroundColorHighlighted = FlaxEngine.GUI.Style.Current.BackgroundHighlighted,
                };
                popBtn.Clicked += () => { GitWrapper.StashPop(); RefreshData(); };

                var applyBtn = new Button
                {
                    Text = "Apply",
                    Parent = row,
                    Width = 48,
                    Height = 20,
                    IsScrollable = true,
                    BackgroundColor = FlaxEngine.GUI.Style.Current.BackgroundNormal,
                    BackgroundColorHighlighted = FlaxEngine.GUI.Style.Current.BackgroundHighlighted,
                };
                int idx = stash.Index;
                applyBtn.Clicked += () => { GitWrapper.StashApply(idx); RefreshData(); };

                var dropBtn = new Button
                {
                    Text = "Drop",
                    Parent = row,
                    Width = 44,
                    Height = 20,
                    IsScrollable = true,
                    BackgroundColor = new Color(0.5f, 0.15f, 0.15f),
                    BackgroundColorHighlighted = new Color(0.7f, 0.2f, 0.2f),
                };
                dropBtn.Clicked += () => { GitWrapper.StashDrop(idx); RefreshData(); };

                row.PerformLayout();
            }
        }

        private void PopulateConflictSection()
        {
            if (_conflictContainer == null) return;

            for (int i = _conflictContainer.ChildrenCount - 1; i >= 0; i--)
            {
                var child = _conflictContainer.Children[i];
                if (child is not DropPanel)
                    child.Dispose();
            }

            if (!GitWrapper.HasConflicts())
            {
                new Label
                {
                    Text = "No conflicts detected.",
                    Parent = _conflictContainer,
                    TextColor = new Color(0.35f, 0.85f, 0.35f),
                    AutoWidth = true,
                    IsScrollable = true,
                    Margin = new Margin(4, 0, 2, 4),
                };
                return;
            }

            new Label
            {
                Text = "Merge conflicts detected! Resolve before committing.",
                Parent = _conflictContainer,
                TextColor = new Color(0.9f, 0.3f, 0.3f),
                AutoWidth = true,
                IsScrollable = true,
                Margin = new Margin(4, 0, 2, 4),
            };

            foreach (var file in GitWrapper.GetConflictFiles())
            {
                new Label
                {
                    Text = $"  {file.FilePath}",
                    Parent = _conflictContainer,
                    TextColor = new Color(0.9f, 0.5f, 0.2f),
                    AutoWidth = true,
                    IsScrollable = true,
                    Margin = new Margin(8, 0, 0, 2),
                };
            }
        }

        #endregion

        #region Custom Tree Nodes

        private class ChangeTreeNode : TreeNode
        {
            public readonly GitChange Change;

            public ChangeTreeNode(GitChange change)
            {
                Change = change;
                Text = $"{GitWrapper.GetChangeTypePrefix(change.Type)}  {change.FilePath}";
                TextColor = GitWrapper.GetChangeColor(change.Type);
            }
        }

        private class CommitTreeNode : TreeNode
        {
            public readonly GitLogEntry Entry;

            public CommitTreeNode(GitLogEntry entry)
            {
                Entry = entry;
                Text = $"{entry.Hash}  {entry.Date}  {entry.Author}";
            }
        }

        private class BranchTreeNode : TreeNode
        {
            public readonly string BranchName;
            public readonly bool IsCurrent;
            public readonly bool IsRemote;

            public BranchTreeNode(string branchName, bool isCurrent, bool isRemote)
            {
                BranchName = branchName;
                IsCurrent = isCurrent;
                IsRemote = isRemote;
                Text = isCurrent ? $"● {branchName}" : $"  {branchName}";
                TextColor = isCurrent ? FlaxEngine.GUI.Style.Current.BorderSelected : FlaxEngine.GUI.Style.Current.Foreground;
            }
        }

        #endregion
    }
}
