using System;
using System.Collections.Generic;
using FlaxEditor.CustomEditors.Elements;
using FlaxEditor.GUI;
using FlaxEditor.GUI.ContextMenu;
using FlaxEditor.GUI.Tree;
using FlaxEngine;
using FlaxEngine.GUI;
using WsSourceControl.Git;
using WsSourceControl.UI;

namespace WsSourceControl.VcsTabs
{
    /// <summary>
    /// The Branches tab shows local and remote branches with
    /// checkout, create, and delete operations.
    /// </summary>
    public class BranchesTab
    {
        private Tree _branchTree;
        private TextBox _newBranchBox;
        private Label _remoteLabel;
        private Label _localHeader;

        /// <summary>
        /// Called when branch operations change the working tree state.
        /// </summary>
        public event Action DataChanged;

        /// <summary>
        /// Builds the Branches tab UI inside the given Tab control.
        /// </summary>
        public void Build(FlaxEditor.GUI.Tabs.Tab tab)
        {
            var container = new ContainerControl
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                Parent = tab,
            };

            _localHeader = new Label
            {
                Text = "Local Branches",
                Parent = container,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
                Height = 24,
                HorizontalAlignment = TextAlignment.Near,
                Margin = new Margin(4, 0, 0, 0),
            };

            var branchScroll = new Panel(ScrollBars.Both)
            {
                Parent = container,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, 24, 120), // Leave 120px at bottom for Remote and Create sections
            };

            _branchTree = new Tree(false)
            {
                Parent = branchScroll,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
            };
            _branchTree.RightClick += OnBranchRightClick;

            // Bottom area
            var bottomArea = new ContainerControl
            {
                Parent = container,
                AnchorPreset = AnchorPresets.HorizontalStretchBottom,
                Offsets = new Margin(0, 0, -120, 120),
            };

            var remoteHeader = new Label
            {
                Text = "Remote",
                Parent = bottomArea,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
                Height = 24,
                HorizontalAlignment = TextAlignment.Near,
                Margin = new Margin(4, 0, 0, 0),
            };

            _remoteLabel = new Label
            {
                Parent = bottomArea,
                Text = "(loading...)",
                TextColor = Style.Current.ForegroundGrey,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(0, 0, 24, 24),
                HorizontalAlignment = TextAlignment.Near,
                Margin = new Margin(8, 0, 0, 0),
            };

            var createHeader = new Label
            {
                Text = "Create Branch",
                Parent = bottomArea,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(0, 0, 52, 24),
                HorizontalAlignment = TextAlignment.Near,
                Margin = new Margin(4, 0, 0, 0),
            };

            _newBranchBox = new TextBox(false, 0, 0, 0)
            {
                Parent = bottomArea,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(8, 150, 76, 20),
                WatermarkText = "New branch name...",
            };

            var createBtn = new Button
            {
                Text = "Create & Checkout",
                Parent = bottomArea,
                AnchorPreset = AnchorPresets.TopRight,
                Offsets = new Margin(-140, 136, 76, 20),
                BackgroundColor = Style.Current.BackgroundSelected,
                BackgroundColorHighlighted = Style.Current.BackgroundSelected.RGBMultiplied(1.2f),
            };
            createBtn.Clicked += OnCreateBranch;
        }


        private void OnBranchRightClick(TreeNode node, Float2 location)
        {
            if (node is not BranchTreeNode branchNode) return;
            var menu = new ContextMenu();

            if (!branchNode.IsRemote && !branchNode.IsCurrent)
            {
                menu.AddButton("Checkout", () =>
                {
                    if (GitWrapper.GetStatus().Count > 0 && !ConfirmRiskyAction("Checkout another branch with local changes present?\n\nCommit, stash, or discard changes first if you want a clean checkout."))
                        return;
                    GitWrapper.CheckoutBranch(branchNode.BranchName);
                    RefreshData();
                    DataChanged?.Invoke();
                });

                menu.AddButton("Delete", () =>
                {
                    if (!ConfirmRiskyAction($"Delete local branch '{branchNode.BranchName}'?\n\nThis cannot be undone from the plugin."))
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
                    if (_newBranchBox != null)
                    {
                        _newBranchBox.Text = string.Empty;
                        _newBranchBox.Focus();
                    }
                });
            }

            if (branchNode.IsRemote)
            {
                menu.AddButton("Checkout as Local", () =>
                {
                    if (GitWrapper.GetStatus().Count > 0 && !ConfirmRiskyAction("Checkout this remote branch with local changes present?\n\nCommit, stash, or discard changes first if you want a clean checkout."))
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
            string name = _newBranchBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                Debug.LogWarning("Branch name cannot be empty.");
                return;
            }

            if (GitWrapper.CreateBranch(name))
            {
                _newBranchBox.Text = string.Empty;
                RefreshData();
                DataChanged?.Invoke();
            }
        }


        /// <summary>
        /// Refresh branch list and remote info.
        /// </summary>
        public void RefreshData()
        {
            PopulateBranchTree();

            if (_remoteLabel != null)
            {
                string remote = GitWrapper.GetRemoteUrl();
                _remoteLabel.Text = string.IsNullOrEmpty(remote) ? "(No remote configured)" : remote;
            }
        }

        private void PopulateBranchTree()
        {
            if (_branchTree == null) return;
            _branchTree.DisposeChildren();

            var branches = GitWrapper.GetBranchInfos();
            var localBranches = branches.FindAll(x => !x.IsRemote);

            int localCount = 0;
            foreach (var branch in localBranches)
            {
                var node = new BranchTreeNode(branch.FriendlyName, branch.IsCurrent, false);
                var sync = branch.Ahead != 0 || branch.Behind != 0 ? $"  Up {branch.Ahead} Down {branch.Behind}" : string.Empty;
                var upstream = string.IsNullOrEmpty(branch.UpstreamName) ? string.Empty : $"  -> {branch.UpstreamName}";
                node.Text = $"{node.Text}{upstream}{sync}";
                node.Parent = _branchTree;
                localCount++;
            }

            var remoteBranches = branches.FindAll(x => x.IsRemote);
            if (remoteBranches.Count > 0)
            {
                var remoteNode = new TreeNode { Text = $"Remotes ({remoteBranches.Count})" };
                remoteNode.Parent = _branchTree;
                remoteNode.Expand();

                foreach (var branch in remoteBranches)
                    new BranchTreeNode(branch.FriendlyName, false, true).Parent = remoteNode;
            }

            _branchTree.PerformLayout();

            if (_localHeader != null)
                _localHeader.Text = $"Local Branches ({localCount})";
        }

        private bool ConfirmRiskyAction(string message)
        {
            return MessageBox.Show(message, "Confirm Git Operation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }
    }
}
