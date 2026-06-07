using System;
using FlaxEditor.GUI;
using FlaxEditor.GUI.ContextMenu;
using FlaxEditor.GUI.Tree;
using FlaxEngine;
using FlaxEngine.GUI;
using WsSourceControl.Git;
using WsSourceControl.UI;

namespace WsSourceControl.VcsTabs
{
    public class BranchesTab
    {
        private Tree _branchTree;
        private TextBox _newBranchBox;
        private Label _remoteLabel;
        private Label _validationLabel;
        private Button _createButton;
        private SectionHeader _branchHeader;

        public event Action DataChanged;

        public void Build(FlaxEditor.GUI.Tabs.Tab tab)
        {
            var container = new ContainerControl
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                Parent = tab,
            };

            var remoteHeader = new SectionHeader("Remote")
            {
                Parent = container,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
            };

            _remoteLabel = new Label
            {
                Parent = container,
                Text = "(loading...)",
                TextColor = SourceControlTheme.MutedText,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(8, 8, SourceControlTheme.SectionHeaderHeight, SourceControlTheme.CompactRowHeight),
                HorizontalAlignment = TextAlignment.Near,
                VerticalAlignment = TextAlignment.Center,
            };

            _branchHeader = new SectionHeader("Branches")
            {
                Parent = container,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(0, 0, SourceControlTheme.SectionHeaderHeight + SourceControlTheme.CompactRowHeight, SourceControlTheme.SectionHeaderHeight),
            };

            var branchScroll = new Panel(ScrollBars.Both)
            {
                Parent = container,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, SourceControlTheme.SectionHeaderHeight * 2f + SourceControlTheme.CompactRowHeight, 42),
            };

            _branchTree = new Tree(false)
            {
                Parent = branchScroll,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
            };
            _branchTree.RightClick += OnBranchRightClick;

            var createRow = new PinnedBottomPanel
            {
                Parent = container,
                PinHeight = 42f,
            };
            createRow.LayoutChildren = LayoutCreateRow;

            _validationLabel = new Label
            {
                Parent = createRow,
                Text = string.Empty,
                TextColor = SourceControlTheme.Warning,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(8, 150, 0, 16),
                HorizontalAlignment = TextAlignment.Near,
            };

            _newBranchBox = new TextBox(false, 0, 0, 0)
            {
                Parent = createRow,
                AnchorPreset = AnchorPresets.HorizontalStretchBottom,
                Offsets = new Margin(8, 146, 17, 22),
                WatermarkText = "New branch name...",
            };

            _createButton = UiActions.PrimaryButton("Create & Checkout", OnCreateBranch, 132f, "Create branch and check it out");
            _createButton.Parent = createRow;
        }

        private void LayoutCreateRow(PinnedBottomPanel row)
        {
            var pad = SourceControlTheme.Padding;
            var buttonWidth = _createButton?.Width ?? 132f;
            if (_validationLabel != null)
                _validationLabel.Bounds = new Rectangle(pad, 0f, Math.Max(80f, row.Width - buttonWidth - pad * 3f), 16f);
            if (_newBranchBox != null)
                _newBranchBox.Bounds = new Rectangle(pad, 17f, Math.Max(80f, row.Width - buttonWidth - pad * 3f), SourceControlTheme.ButtonHeight);
            if (_createButton != null)
                _createButton.Bounds = new Rectangle(row.Width - buttonWidth - pad, 17f, buttonWidth, SourceControlTheme.ButtonHeight);
        }

        private void OnBranchRightClick(TreeNode node, Float2 location)
        {
            if (node is not BranchTreeNode branchNode) return;
            var menu = new ContextMenu();

            if (!branchNode.IsRemote && !branchNode.IsCurrent)
            {
                menu.AddButton("Checkout", () =>
                {
                    if (GitWrapper.GetStatus().Count > 0 && !UiActions.ConfirmDanger("Checkout another branch with local changes present?\n\nCommit, stash, or discard changes first if you want a clean checkout."))
                        return;
                    GitWrapper.CheckoutBranch(branchNode.BranchName);
                    RefreshData();
                    DataChanged?.Invoke();
                });

                menu.AddButton("Delete", () =>
                {
                    if (!UiActions.ConfirmDanger($"Delete local branch '{branchNode.BranchName}'?\n\nThis cannot be undone from the plugin."))
                        return;
                    GitWrapper.DeleteBranch(branchNode.BranchName);
                    RefreshData();
                    DataChanged?.Invoke();
                });
            }
            else if (branchNode.IsCurrent)
            {
                menu.AddButton("Create From Here...", () =>
                {
                    _newBranchBox.Text = string.Empty;
                    _newBranchBox.Focus();
                });
            }

            if (branchNode.IsRemote)
            {
                menu.AddButton("Checkout as Local", () =>
                {
                    if (GitWrapper.GetStatus().Count > 0 && !UiActions.ConfirmDanger("Checkout this remote branch with local changes present?\n\nCommit, stash, or discard changes first if you want a clean checkout."))
                        return;
                    GitWrapper.CheckoutBranch(branchNode.BranchName);
                    RefreshData();
                    DataChanged?.Invoke();
                });
            }

            menu.Show(_branchTree, location);
        }

        private void OnCreateBranch()
        {
            var name = _newBranchBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                _validationLabel.Text = "Branch name is required.";
                return;
            }

            _validationLabel.Text = string.Empty;
            if (GitWrapper.CreateBranch(name))
            {
                _newBranchBox.Text = string.Empty;
                RefreshData();
                DataChanged?.Invoke();
            }
        }

        public void RefreshData()
        {
            PopulateBranchTree();

            if (_remoteLabel != null)
            {
                var snapshot = GitWrapper.GetSnapshot();
                var remote = string.IsNullOrEmpty(snapshot.RemoteUrl) ? "No remote configured" : snapshot.RemoteUrl;
                var upstream = string.IsNullOrEmpty(snapshot.UpstreamName) ? "No upstream" : snapshot.UpstreamName;
                _remoteLabel.Text = $"{remote}  |  {upstream}  |  Up {snapshot.Ahead} Down {snapshot.Behind}";
            }
        }

        private void PopulateBranchTree()
        {
            if (_branchTree == null) return;
            _branchTree.DisposeChildren();

            var branches = GitWrapper.GetBranchInfos();
            var localCount = 0;
            var remoteCount = 0;

            foreach (var branch in branches)
            {
                if (branch.IsRemote)
                    continue;

                new BranchTreeNode(branch.FriendlyName, branch.IsCurrent, false, branch.UpstreamName, branch.Ahead, branch.Behind).Parent = _branchTree;
                localCount++;
            }

            var remoteNode = new TreeNode
            {
                Text = "Remotes",
                TextColor = SourceControlTheme.MutedText,
            };
            remoteNode.Parent = _branchTree;

            foreach (var branch in branches)
            {
                if (!branch.IsRemote)
                    continue;

                new BranchTreeNode(branch.FriendlyName, false, true, branch.UpstreamName, branch.Ahead, branch.Behind).Parent = remoteNode;
                remoteCount++;
            }

            if (remoteCount > 0)
                remoteNode.Expand();

            _branchTree.PerformLayout();
            _branchHeader?.SetSubtitle($"{localCount} local, {remoteCount} remote");
        }
    }
}
