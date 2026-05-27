using System;
using System.Collections.Generic;
using System.Text;
using FlaxEditor.CustomEditors.Elements;
using FlaxEditor.GUI;
using FlaxEditor.GUI.Input;
using FlaxEditor.GUI.Tree;
using FlaxEngine;
using FlaxEngine.GUI;
using WsSourceControlEditor.Git;
using WsSourceControlEditor.UI;

namespace WsSourceControlEditor.VcsTabs
{
    /// <summary>
    /// The History tab shows the git commit log with search/filter
    /// and a detail panel for the selected commit.
    /// </summary>
    public class HistoryTab
    {
        private Tree _historyTree;
        private SearchBox _historySearch;
        private TextBox _historyDetailText;
        private Label _commitCountLabel;
        private const int DefaultLogCount = 100;

        /// <summary>
        /// Builds the History tab UI inside the given Tab control.
        /// </summary>
        public void Build(FlaxEditor.GUI.Tabs.Tab tab)
        {
            // Horizontal split: left = log list, right = commit detail
            var split = new SplitPanel(Orientation.Horizontal, ScrollBars.None, ScrollBars.None)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                SplitterValue = 0.4f,
                Parent = tab,
            };

            var leftContainer = new ContainerControl
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                Parent = split.Panel1,
            };

            // Header row with search and count
            var headerRow = new HorizontalPanel
            {
                Parent = leftContainer,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
                Height = 32,
                Margin = new Margin(4f),
                Spacing = 8f,
            };

            _historySearch = new SearchBox
            {
                Height = 20,
                Width = 180,
                Parent = headerRow,
                WatermarkText = "Search commits...",
            };
            _historySearch.TextChanged += () => PopulateHistoryTree();

            _commitCountLabel = new Label
            {
                Parent = headerRow,
                Text = "",
                TextColor = Style.Current.ForegroundGrey,
                AutoWidth = true,
                VerticalAlignment = TextAlignment.Center,
            };

            var treeScroll = new Panel(ScrollBars.Both)
            {
                Parent = leftContainer,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, 32, 0),
            };

            _historyTree = new Tree(false)
            {
                Parent = treeScroll,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
            };
            _historyTree.SelectedChanged += OnHistorySelectionChanged;

            var rightContainer = new ContainerControl
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                Parent = split.Panel2,
            };
            
            var rightHeader = new Label
            {
                Text = "Commit Detail",
                Parent = rightContainer,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
                Height = 24,
                HorizontalAlignment = TextAlignment.Near,
                Margin = new Margin(4, 0, 0, 0),
            };

            _historyDetailText = new TextBox(true, 0, 0, 0)
            {
                Parent = rightContainer,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, 24, 0),
                IsReadOnly = true,
                BackgroundColor = Style.Current.TextBoxBackground,
                BorderColor = Style.Current.BorderNormal,
                TextColor = Style.Current.Foreground,
            };
        }


        private void OnHistorySelectionChanged(List<TreeNode> before, List<TreeNode> after)
        {
            if (after == null || after.Count == 0 || after[0] is not CommitTreeNode node)
            {
                if (_historyDetailText != null)
                    _historyDetailText.Text = "Select a commit to view details.";
                return;
            }

            if (_historyDetailText == null) return;

            string detail = GitWrapper.GetCommitDetail(node.Entry.Hash);
            var files = GitWrapper.GetCommitChangedFiles(node.Entry.Hash);

            var sb = new StringBuilder();
            sb.AppendLine($"Commit:  {node.Entry.Hash}");
            sb.AppendLine($"Author:  {node.Entry.Author}");
            sb.AppendLine($"Date:    {node.Entry.Date}");
            sb.AppendLine();
            sb.AppendLine($"    {node.Entry.Message}");
            sb.AppendLine();

            if (files.Count > 0)
            {
                sb.AppendLine($"Changed Files ({files.Count}):");
                sb.AppendLine(new string('-', 40));
                foreach (var f in files)
                    sb.AppendLine($"  {f}");
                sb.AppendLine();
            }

            sb.Append(detail);
            _historyDetailText.Text = sb.ToString();
        }


        /// <summary>
        /// Refresh the commit log tree.
        /// </summary>
        public void RefreshData()
        {
            PopulateHistoryTree();
        }

        private void PopulateHistoryTree()
        {
            if (_historyTree == null) return;
            _historyTree.DisposeChildren();

            var log = GitWrapper.GetLog(DefaultLogCount);
            string filter = _historySearch?.Text?.Trim() ?? string.Empty;

            int displayed = 0;
            int total = log.Count;

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
                displayed++;
            }

            _historyTree.PerformLayout();

            if (_commitCountLabel != null)
            {
                if (string.IsNullOrEmpty(filter))
                    _commitCountLabel.Text = $"{total} commits";
                else
                    _commitCountLabel.Text = $"{displayed}/{total} commits";
            }
        }
    }
}
