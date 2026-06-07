using System;
using System.Collections.Generic;
using FlaxEditor.GUI;
using FlaxEditor.GUI.ContextMenu;
using FlaxEditor.GUI.Input;
using FlaxEditor.GUI.Tree;
using FlaxEngine;
using FlaxEngine.GUI;
using WsSourceControl.Git;
using WsSourceControl.UI;

namespace WsSourceControl.VcsTabs
{
    public class ChangesTab
    {
        private Tree _stagedTree;
        private Tree _unstagedTree;
        private TextBox _commitMessage;
        private CheckBox _amendCheck;
        private TextBox _diffTextBox;
        private Label _diffFileLabel;
        private SectionHeader _stagedHeader;
        private SectionHeader _unstagedHeader;
        private SectionHeader _commitHeader;
        private SearchBox _filterBox;
        private Button _commitButton;
        private Label _stagedEmptyLabel;
        private Label _unstagedEmptyLabel;
        private int _stagedCount;

        public event Action DataChanged;

        public void Build(FlaxEditor.GUI.Tabs.Tab tab)
        {
            var mainSplit = new SplitPanel(Orientation.Horizontal, ScrollBars.None, ScrollBars.None)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                SplitterValue = 0.4f,
                Parent = tab,
            };

            BuildFileListPanel(mainSplit.Panel1);
            BuildDetailPanel(mainSplit.Panel2);
        }

        private void BuildFileListPanel(ContainerControl parent)
        {
            var filterRow = new ContainerControl
            {
                Parent = parent,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(0, 0, 0, 36),
            };

            _filterBox = new SearchBox
            {
                Parent = filterRow,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(6, 6, 6, 24),
                WatermarkText = "Filter changed files...",
            };
            _filterBox.TextChanged += PopulateChangesTrees;

            var verticalSplit = new SplitPanel(Orientation.Vertical, ScrollBars.None, ScrollBars.None)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, 36, 0),
                SplitterValue = 0.5f,
                Parent = parent,
            };

            var stagedContainer = new ContainerControl { AnchorPreset = AnchorPresets.StretchAll, Offsets = Margin.Zero, Parent = verticalSplit.Panel1 };
            _stagedHeader = new SectionHeader("Staged")
            {
                Parent = stagedContainer,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(0, 0, 0, SourceControlTheme.SectionHeaderHeight),
                Height = SourceControlTheme.SectionHeaderHeight,
            };
            _stagedHeader.AddAction(UiActions.Button("Unstage All", OnUnstageAll, 92f, "Unstage all staged files"));

            var stagedScroll = new Panel(ScrollBars.Both)
            {
                Parent = stagedContainer,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, SourceControlTheme.SectionHeaderHeight, 0),
            };

            _stagedTree = new Tree(false)
            {
                Parent = stagedScroll,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
            };
            _stagedTree.SelectedChanged += (before, after) => OnFileSelected(after, true);
            _stagedTree.RightClick += (node, loc) => OnFileRightClick(node, loc, true);
            _stagedEmptyLabel = BuildEmptyListLabel(stagedScroll, "No staged changes.");

            var unstagedContainer = new ContainerControl { AnchorPreset = AnchorPresets.StretchAll, Offsets = Margin.Zero, Parent = verticalSplit.Panel2 };
            _unstagedHeader = new SectionHeader("Unstaged")
            {
                Parent = unstagedContainer,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(0, 0, 0, SourceControlTheme.SectionHeaderHeight),
                Height = SourceControlTheme.SectionHeaderHeight,
            };
            _unstagedHeader.AddAction(UiActions.PrimaryButton("Stage All", OnStageAll, 76f, "Stage all unstaged files"));
            _unstagedHeader.AddAction(UiActions.DangerButton("Discard", OnDiscardAll, 70f, "Discard all local changes"));

            var unstagedScroll = new Panel(ScrollBars.Both)
            {
                Parent = unstagedContainer,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, SourceControlTheme.SectionHeaderHeight, 0),
            };

            _unstagedTree = new Tree(false)
            {
                Parent = unstagedScroll,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
            };
            _unstagedTree.SelectedChanged += (before, after) => OnFileSelected(after, false);
            _unstagedTree.RightClick += (node, loc) => OnFileRightClick(node, loc, false);
            _unstagedEmptyLabel = BuildEmptyListLabel(unstagedScroll, "No unstaged changes.");
        }

        private void BuildDetailPanel(ContainerControl parent)
        {
            var verticalSplit = new SplitPanel(Orientation.Vertical, ScrollBars.None, ScrollBars.None)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                SplitterValue = 0.7f,
                Parent = parent,
            };

            var diffContainer = new ContainerControl { AnchorPreset = AnchorPresets.StretchAll, Offsets = Margin.Zero, Parent = verticalSplit.Panel1 };
            var diffHeader = new SectionHeader("Diff")
            {
                Parent = diffContainer,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
            };

            _diffFileLabel = new Label
            {
                Parent = diffHeader,
                Text = "(select a file to view diff)",
                TextColor = SourceControlTheme.MutedText,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(52, 6, 0, SourceControlTheme.SectionHeaderHeight),
                HorizontalAlignment = TextAlignment.Near,
                VerticalAlignment = TextAlignment.Center,
            };

            _diffTextBox = new TextBox(true, 0, 0, 0)
            {
                Parent = diffContainer,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, SourceControlTheme.SectionHeaderHeight, 0),
                IsReadOnly = true,
                BackgroundColor = Style.Current.TextBoxBackground,
                BorderColor = Style.Current.BorderNormal,
                TextColor = Style.Current.Foreground,
            };

            var commitContainer = new ContainerControl { AnchorPreset = AnchorPresets.StretchAll, Offsets = Margin.Zero, Parent = verticalSplit.Panel2 };
            _commitHeader = new SectionHeader("Commit")
            {
                Parent = commitContainer,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
            };

            var commitBottomArea = new PinnedBottomPanel
            {
                Parent = commitContainer,
                PinHeight = 60f,
            };

            var bottomRow = new HorizontalPanel
            {
                Parent = commitBottomArea,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(4, 4, 0, 24),
                Spacing = 4f,
            };

            _amendCheck = new CheckBox
            {
                Parent = bottomRow,
                Size = new Float2(16, 16),
            };

            new Label
            {
                Parent = bottomRow,
                Text = "Amend previous commit",
                AutoWidth = true,
            };

            _commitButton = UiActions.PrimaryButton("Commit Staged Changes", OnCommit, 180f, "Commit staged files");
            _commitButton.Parent = commitBottomArea;
            _commitButton.AnchorPreset = AnchorPresets.HorizontalStretchTop;
            _commitButton.Offsets = new Margin(4, 4, 30, 28);

            _commitMessage = new TextBox(true, 0, 0, 0)
            {
                Parent = commitContainer,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(4, 4, SourceControlTheme.SectionHeaderHeight + 2f, 60),
                WatermarkText = "Commit message...",
            };
            _commitMessage.TextChanged += UpdateCommitButtonState;
        }

        private static Label BuildEmptyListLabel(ContainerControl parent, string text)
        {
            return new Label
            {
                Parent = parent,
                Text = text,
                TextColor = SourceControlTheme.MutedText,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(8, 8, 8, 22),
                HorizontalAlignment = TextAlignment.Near,
                VerticalAlignment = TextAlignment.Center,
            };
        }

        private void OnFileSelected(List<TreeNode> selection, bool isStaged)
        {
            if (selection == null || selection.Count == 0 || selection[0] is not ChangeTreeNode node)
            {
                if (_diffFileLabel != null)
                    _diffFileLabel.Text = "(select a file to view diff)";
                if (_diffTextBox != null)
                    _diffTextBox.Text = string.Empty;
                return;
            }

            var state = isStaged ? "staged" : "unstaged";
            _diffFileLabel.Text = $"{node.FilePath}  ({state}{GetChangeBadges(node.Change)})";
            var diff = isStaged ? GitWrapper.GetDiffStaged(node.FilePath) : GitWrapper.GetDiff(node.FilePath);
            _diffTextBox.Text = string.IsNullOrEmpty(diff) ? GetNoDiffMessage(node.Change) : diff;
        }

        private void OnFileRightClick(TreeNode node, Float2 location, bool isStaged)
        {
            if (node is not ChangeTreeNode cn) return;
            var menu = new ContextMenu();

            if (isStaged)
            {
                menu.AddButton("Unstage", () =>
                {
                    GitWrapper.Unstage(new[] { cn.FilePath });
                    RefreshData();
                });
                menu.AddButton("View Diff (Staged)", () =>
                {
                    _diffFileLabel.Text = $"{cn.FilePath}  (staged{GetChangeBadges(cn.Change)})";
                    _diffTextBox.Text = GitWrapper.GetDiffStaged(cn.FilePath) ?? GetNoDiffMessage(cn.Change);
                });
            }
            else
            {
                menu.AddButton("Stage", () =>
                {
                    GitWrapper.Add(new[] { cn.FilePath });
                    RefreshData();
                });
                menu.AddButton("Discard Changes", () =>
                {
                    if (!UiActions.ConfirmDanger($"Discard changes in:\n\n{cn.FilePath}\n\nThis cannot be undone."))
                        return;
                    GitWrapper.Reset(cn.FilePath);
                    RefreshData();
                });
                menu.AddButton("View Diff", () =>
                {
                    _diffFileLabel.Text = $"{cn.FilePath}  (unstaged{GetChangeBadges(cn.Change)})";
                    _diffTextBox.Text = GitWrapper.GetDiff(cn.FilePath) ?? GetNoDiffMessage(cn.Change);
                });
            }

            menu.AddSeparator();
            menu.AddButton("Open in Explorer", () =>
            {
                var fullPath = System.IO.Path.Combine(GitWrapper.ProjectPath, cn.FilePath);
                if (System.IO.File.Exists(fullPath))
                    FileSystem.ShowFileExplorer(fullPath);
            });

            menu.Show(isStaged ? _stagedTree : _unstagedTree, location);
        }

        private void OnStageAll()
        {
            GitWrapper.AddAll();
            RefreshData();
        }

        private void OnUnstageAll()
        {
            var changes = GitWrapper.GetStatus();
            var paths = new List<string>();
            foreach (var c in changes)
                if (c.Staged) paths.Add(c.FilePath);
            if (paths.Count <= 0)
                return;

            GitWrapper.Unstage(paths.ToArray());
            RefreshData();
        }

        private void OnDiscardAll()
        {
            var changes = GitWrapper.GetStatus();
            if (changes.Count == 0)
                return;

            var preview = string.Join("\n", changes.ConvertAll(x => x.FilePath));
            if (preview.Length > 1200)
                preview = preview.Substring(0, 1200) + "\n...";

            if (!UiActions.ConfirmDanger($"Discard all local changes and remove untracked files?\n\n{preview}\n\nThis cannot be undone."))
                return;

            GitWrapper.ResetHard();
            RefreshData();
        }

        public void Update(float dt)
        {
        }

        private void OnCommit()
        {
            var msg = _commitMessage?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(msg))
            {
                Debug.LogWarning("Please enter a commit message.");
                return;
            }

            if (_stagedCount == 0)
            {
                Debug.LogWarning("Stage at least one change before committing.");
                return;
            }

            var amend = _amendCheck?.Checked ?? false;
            if (amend && !UiActions.ConfirmDanger("Amend the previous commit?\n\nThis rewrites the latest commit."))
                return;

            if (amend)
                GitWrapper.CommitAmend(msg);
            else
                GitWrapper.Commit(msg);

            _commitMessage.Text = string.Empty;
            _amendCheck.Checked = false;
            RefreshData();
            DataChanged?.Invoke();
        }

        public void RefreshData()
        {
            PopulateChangesTrees();
            UpdateCommitButtonState();
        }

        private void PopulateChangesTrees()
        {
            if (_stagedTree == null || _unstagedTree == null) return;
            _stagedTree.DisposeChildren();
            _unstagedTree.DisposeChildren();

            var changes = GitWrapper.GetStatus();
            var filter = _filterBox?.Text?.Trim() ?? string.Empty;
            var staged = new List<GitChange>();
            var unstaged = new List<GitChange>();

            foreach (var change in changes)
            {
                if (!string.IsNullOrEmpty(filter) && change.FilePath.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (change.Staged)
                    staged.Add(change);
                else
                    unstaged.Add(change);
            }

            PopulateTreeWithFolders(_stagedTree, staged);
            PopulateTreeWithFolders(_unstagedTree, unstaged);
            _stagedCount = staged.Count;

            _stagedTree.PerformLayout();
            _unstagedTree.PerformLayout();
            _stagedHeader?.SetCount(staged.Count);
            _unstagedHeader?.SetCount(unstaged.Count);

            if (_stagedEmptyLabel != null)
                _stagedEmptyLabel.Visible = staged.Count == 0;
            if (_unstagedEmptyLabel != null)
                _unstagedEmptyLabel.Visible = unstaged.Count == 0;
        }

        private void PopulateTreeWithFolders(Tree tree, List<GitChange> changes)
        {
            var rootFiles = new List<GitChange>();
            var folderGroups = new Dictionary<string, List<GitChange>>();

            foreach (var change in changes)
            {
                var path = change.FilePath;
                var lastSlash = path.IndexOf('/');
                if (lastSlash < 0)
                {
                    rootFiles.Add(change);
                    continue;
                }

                var folder = path.Substring(0, lastSlash);
                if (!folderGroups.ContainsKey(folder))
                    folderGroups[folder] = new List<GitChange>();
                folderGroups[folder].Add(change);
            }

            foreach (var change in rootFiles)
                new ChangeTreeNode(change).Parent = tree;

            foreach (var kvp in folderGroups)
            {
                var folderNode = new TreeNode
                {
                    Text = $"{kvp.Key}/ ({kvp.Value.Count})",
                    TextColor = SourceControlTheme.MutedText,
                };
                folderNode.Parent = tree;

                foreach (var change in kvp.Value)
                    new ChangeTreeNode(change).Parent = folderNode;

                folderNode.Expand();
            }
        }

        private void UpdateCommitButtonState()
        {
            if (_commitButton == null)
                return;

            var hasMessage = !string.IsNullOrWhiteSpace(_commitMessage?.Text);
            _commitButton.Enabled = _stagedCount > 0 && hasMessage;
            _commitHeader?.SetSubtitle(_stagedCount > 0 ? $"{_stagedCount} staged" : "No staged changes");
        }

        private static string GetChangeBadges(GitChange change)
        {
            var badges = string.Empty;
            if (change.IsBinary)
                badges += ", binary";
            if (change.IsLfsPointer)
                badges += ", LFS";
            if (change.IsConflict)
                badges += ", conflict";
            return badges;
        }

        private static string GetNoDiffMessage(GitChange change)
        {
            if (change.IsBinary)
                return "(Binary file. No text diff available.)";
            if (change.IsLfsPointer)
                return "(Git LFS pointer. No file diff available.)";
            if (change.IsConflict)
                return "(Conflict file. Resolve conflict markers before committing.)";
            return "(No diff available.)";
        }
    }
}
