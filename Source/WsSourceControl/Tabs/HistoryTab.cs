using System;
using System.Collections.Generic;
using System.Text;
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
    public class HistoryTab
    {
        private Tree _historyTree;
        private SearchBox _historySearch;
        private TextBox _historyDetailText;
        private SectionHeader _historyHeader;
        private Label _historyEmptyLabel;
        private const int DefaultLogCount = 100;

        public void Build(FlaxEditor.GUI.Tabs.Tab tab)
        {
            var split = new SplitPanel(Orientation.Horizontal, ScrollBars.None, ScrollBars.None)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                SplitterValue = 0.42f,
                Parent = tab,
            };

            var leftContainer = new ContainerControl
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                Parent = split.Panel1,
            };

            var searchRow = new ContainerControl
            {
                Parent = leftContainer,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(0, 0, 0, 30),
            };

            _historySearch = new SearchBox
            {
                Parent = searchRow,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(6, 6, 5, 23),
                WatermarkText = "Search commits...",
            };
            _historySearch.TextChanged += PopulateHistoryTree;

            _historyHeader = new SectionHeader("History")
            {
                Parent = leftContainer,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(0, 0, 30, SourceControlTheme.SectionHeaderHeight),
            };
            _historyHeader.AddAction(UiActions.Button("Refresh", RefreshData, 66f, "Refresh commit history"));

            var treeScroll = new Panel(ScrollBars.Both)
            {
                Parent = leftContainer,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, 30 + SourceControlTheme.SectionHeaderHeight, 0),
            };

            _historyTree = new Tree(false)
            {
                Parent = treeScroll,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
            };
            _historyTree.SelectedChanged += OnHistorySelectionChanged;
            _historyTree.RightClick += OnHistoryRightClick;

            _historyEmptyLabel = new Label
            {
                Parent = treeScroll,
                Text = "No commits.",
                TextColor = SourceControlTheme.MutedText,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(8, 8, 8, 22),
            };

            var rightContainer = new ContainerControl
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                Parent = split.Panel2,
            };

            var detailHeader = new SectionHeader("Commit Detail")
            {
                Parent = rightContainer,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
            };

            _historyDetailText = new TextBox(true, 0, 0, 0)
            {
                Parent = rightContainer,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, SourceControlTheme.SectionHeaderHeight, 0),
                IsReadOnly = true,
                BackgroundColor = Style.Current.TextBoxBackground,
                BorderColor = Style.Current.BorderNormal,
                TextColor = Style.Current.Foreground,
                Text = "Select a commit to view details.",
            };
        }

        private void OnHistoryRightClick(TreeNode node, Float2 location)
        {
            if (node is not CommitTreeNode commitNode)
                return;

            var menu = new ContextMenu();
            menu.AddButton("Copy Hash", () => Clipboard.Text = commitNode.Entry.Hash);
            menu.AddButton("Create Branch From Commit", () =>
            {
                var branchName = $"branch-{commitNode.Entry.Hash.Substring(0, Math.Min(8, commitNode.Entry.Hash.Length))}";
                if (GitWrapper.CreateBranch(branchName, commitNode.Entry.Hash))
                    RefreshData();
            });
            menu.Show(_historyTree, location);
        }

        private void OnHistorySelectionChanged(List<TreeNode> before, List<TreeNode> after)
        {
            if (after == null || after.Count == 0 || after[0] is not CommitTreeNode node)
            {
                if (_historyDetailText != null)
                    _historyDetailText.Text = "Select a commit to view details.";
                return;
            }

            var detail = GitWrapper.GetCommitDetail(node.Entry.Hash);
            var files = GitWrapper.GetCommitChangedFiles(node.Entry.Hash);

            var sb = new StringBuilder();
            sb.AppendLine($"Commit: {node.Entry.Hash}");
            sb.AppendLine($"Author: {node.Entry.Author}");
            sb.AppendLine($"Date:   {node.Entry.Date}");
            sb.AppendLine();
            sb.AppendLine(node.Entry.Message);
            sb.AppendLine();
            sb.AppendLine($"Changed Files ({files.Count})");
            sb.AppendLine(new string('-', 40));
            foreach (var file in files)
                sb.AppendLine(file);
            sb.AppendLine();
            sb.Append(detail);

            _historyDetailText.Text = sb.ToString();
        }

        public void RefreshData()
        {
            PopulateHistoryTree();
        }

        private void PopulateHistoryTree()
        {
            if (_historyTree == null) return;
            _historyTree.DisposeChildren();

            var log = GitWrapper.GetLog(DefaultLogCount);
            var filter = _historySearch?.Text?.Trim() ?? string.Empty;
            var displayed = 0;

            foreach (var entry in log)
            {
                if (!MatchesFilter(entry, filter))
                    continue;

                new CommitTreeNode(entry).Parent = _historyTree;
                displayed++;
            }

            _historyTree.PerformLayout();
            _historyHeader?.SetSubtitle(string.IsNullOrEmpty(filter) ? $"{log.Count} commits" : $"{displayed}/{log.Count} commits");

            if (_historyEmptyLabel != null)
            {
                _historyEmptyLabel.Visible = displayed == 0;
                _historyEmptyLabel.Text = log.Count == 0 ? "No commits." : "No matching commits.";
            }
        }

        private static bool MatchesFilter(GitLogEntry entry, string filter)
        {
            if (string.IsNullOrEmpty(filter))
                return true;

            return (entry.Hash != null && entry.Hash.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (entry.Author != null && entry.Author.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (entry.Message != null && entry.Message.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
