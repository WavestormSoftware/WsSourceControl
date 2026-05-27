using System;
using System.Collections.Generic;
using FlaxEditor.CustomEditors.Elements;
using FlaxEditor.GUI;
using FlaxEditor.GUI.ContextMenu;
using FlaxEditor.GUI.Tree;
using FlaxEngine;
using FlaxEngine.GUI;
using WsSourceControlEditor.Git;
using WsSourceControlEditor.UI;

namespace WsSourceControlEditor.VcsTabs
{
    /// <summary>
    /// The Changes tab uses a 2-column horizontal split:
    ///   Left:  Staged + Unstaged file trees in a single scrollable panel
    ///   Right: Diff viewer + Commit form in a single scrollable panel
    /// This avoids the 4-quadrant problem of too many scrollable containers.
    /// All buttons use GroupElement.Button() for proper DropPanel layout.
    /// </summary>
    public class ChangesTab
    {
        private Tree _stagedTree;
        private Tree _unstagedTree;
        private TextBox _commitMessage;
        private CheckBox _amendCheck;
        private TextBox _diffTextBox;
        private Label _diffFileLabel;
        private Label _stagedHeader;
        private Label _unstagedHeader;
        private Button _discardAllBtn;
        private bool _discardConfirmPending;
        private float _discardConfirmTimer;

        /// <summary>
        /// Called when data should be refreshed (e.g., after a commit).
        /// </summary>
        public event Action DataChanged;

        /// <summary>
        /// Builds the Changes tab UI inside the given Tab control.
        /// </summary>
        public void Build(FlaxEditor.GUI.Tabs.Tab tab)
        {
            // Horizontal split: left = file trees, right = diff + commit
            var mainSplit = new SplitPanel(Orientation.Horizontal, ScrollBars.None, ScrollBars.None)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                SplitterValue = 0.45f,
                Parent = tab,
            };

            BuildFileListPanel(mainSplit.Panel1);
            BuildDetailPanel(mainSplit.Panel2);
        }


        private void BuildFileListPanel(ContainerControl parent)
        {
            var verticalSplit = new SplitPanel(Orientation.Vertical, ScrollBars.None, ScrollBars.None)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                SplitterValue = 0.5f,
                Parent = parent,
            };

            var stagedContainer = new ContainerControl { AnchorPreset = AnchorPresets.StretchAll, Offsets = Margin.Zero, Parent = verticalSplit.Panel1 };
            
            _stagedHeader = new Label
            {
                Text = "Staged Changes (0)",
                Parent = stagedContainer,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
                Height = 24,
                HorizontalAlignment = TextAlignment.Near,
                Margin = new Margin(4, 0, 0, 0),
            };

            var unstageAllBtn = new Button
            {
                Text = "Unstage All",
                Parent = stagedContainer,
                AnchorPreset = AnchorPresets.TopRight,
                Offsets = new Margin(-86, 84, 2, 20),
            };
            unstageAllBtn.Clicked += OnUnstageAll;

            var stagedScroll = new Panel(ScrollBars.Both)
            {
                Parent = stagedContainer,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, 24, 0),
            };

            _stagedTree = new Tree(false)
            {
                Parent = stagedScroll,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
            };
            _stagedTree.SelectedChanged += (before, after) => OnFileSelected(after, isStaged: true);
            _stagedTree.RightClick += (node, loc) => OnFileRightClick(node, loc, isStaged: true);

            var unstagedContainer = new ContainerControl { AnchorPreset = AnchorPresets.StretchAll, Offsets = Margin.Zero, Parent = verticalSplit.Panel2 };
            
            _unstagedHeader = new Label
            {
                Text = "Unstaged Changes (0)",
                Parent = unstagedContainer,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
                Height = 24,
                HorizontalAlignment = TextAlignment.Near,
                Margin = new Margin(4, 0, 0, 0),
            };

            var discardAllBtn = new Button
            {
                Text = "Discard All",
                Parent = unstagedContainer,
                AnchorPreset = AnchorPresets.TopRight,
                Offsets = new Margin(-76, 74, 2, 20),
                BackgroundColor = new Color(0.5f, 0.15f, 0.15f),
                BackgroundColorHighlighted = new Color(0.7f, 0.2f, 0.2f),
            };
            _discardAllBtn = discardAllBtn;
            _discardAllBtn.Clicked += OnDiscardAll;

            var stageAllBtn = new Button
            {
                Text = "Stage All",
                Parent = unstagedContainer,
                AnchorPreset = AnchorPresets.TopRight,
                Offsets = new Margin(-154, 74, 2, 20),
                BackgroundColor = Style.Current.BackgroundSelected,
                BackgroundColorHighlighted = Style.Current.BackgroundSelected.RGBMultiplied(1.2f),
            };
            stageAllBtn.Clicked += OnStageAll;

            var unstagedScroll = new Panel(ScrollBars.Both)
            {
                Parent = unstagedContainer,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, 24, 0),
            };

            _unstagedTree = new Tree(false)
            {
                Parent = unstagedScroll,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
            };
            _unstagedTree.SelectedChanged += (before, after) => OnFileSelected(after, isStaged: false);
            _unstagedTree.RightClick += (node, loc) => OnFileRightClick(node, loc, isStaged: false);
        }


        private void BuildDetailPanel(ContainerControl parent)
        {
            var verticalSplit = new SplitPanel(Orientation.Vertical, ScrollBars.None, ScrollBars.None)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                SplitterValue = 0.7f, // Diff takes 70%, Commit takes 30%
                Parent = parent,
            };

            var diffContainer = new ContainerControl { AnchorPreset = AnchorPresets.StretchAll, Offsets = Margin.Zero, Parent = verticalSplit.Panel1 };

            var diffHeader = new Label
            {
                Text = "Diff",
                Parent = diffContainer,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
                Height = 24,
                HorizontalAlignment = TextAlignment.Near,
                Margin = new Margin(4, 0, 0, 0),
            };

            _diffFileLabel = new Label
            {
                Parent = diffContainer,
                Text = "(select a file to view diff)",
                TextColor = Style.Current.ForegroundGrey,
                AnchorPreset = AnchorPresets.TopRight,
                Offsets = new Margin(-304, 300, 2, 20),
                HorizontalAlignment = TextAlignment.Far,
            };

            _diffTextBox = new TextBox(true, 0, 0, 0)
            {
                Parent = diffContainer,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, 24, 0),
                IsReadOnly = true,
                BackgroundColor = Style.Current.TextBoxBackground,
                BorderColor = Style.Current.BorderNormal,
                TextColor = Style.Current.Foreground,
            };

            var commitContainer = new ContainerControl { AnchorPreset = AnchorPresets.StretchAll, Offsets = Margin.Zero, Parent = verticalSplit.Panel2 };

            var commitHeader = new Label
            {
                Text = "Commit",
                Parent = commitContainer,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
                Height = 24,
                HorizontalAlignment = TextAlignment.Near,
                Margin = new Margin(4, 0, 0, 0),
            };

            var commitBottomArea = new ContainerControl
            {
                Parent = commitContainer,
                AnchorPreset = AnchorPresets.HorizontalStretchBottom,
                Offsets = new Margin(0, 0, -60, 60), // Y=-60, Height=60
            };

            var bottomRow = new HorizontalPanel
            {
                Parent = commitBottomArea,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(4, 4, 0, 24),
                Spacing = 4f,
            };

            var amendCheck = new CheckBox
            {
                Parent = bottomRow,
                Size = new Float2(16, 16),
            };
            _amendCheck = amendCheck;
            
            new Label
            {
                Parent = bottomRow,
                Text = "Amend previous commit",
                AutoWidth = true,
            };

            var commitBtn = new Button
            {
                Text = "Commit Staged Changes",
                Parent = commitBottomArea,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(4, 4, 30, 28),
                BackgroundColor = Style.Current.BackgroundSelected,
                BackgroundColorHighlighted = Style.Current.BackgroundSelected.RGBMultiplied(1.2f),
            };
            commitBtn.Clicked += OnCommit;

            _commitMessage = new TextBox(true, 0, 0, 0)
            {
                Parent = commitContainer,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(4, 4, 24, 60), // Fits perfectly between header and bottom area
                WatermarkText = "Commit message...",
            };
        }


        private void OnFileSelected(List<TreeNode> selection, bool isStaged)
        {
            if (selection == null || selection.Count == 0 || selection[0] is not ChangeTreeNode node)
            {
                if (_diffFileLabel != null)
                    _diffFileLabel.Text = "(select a file to view diff)";
                if (_diffTextBox != null)
                    _diffTextBox.Text = "";
                return;
            }

            _diffFileLabel.Text = node.FilePath;
            string diff = isStaged
                ? GitWrapper.GetDiffStaged(node.FilePath)
                : GitWrapper.GetDiff(node.FilePath);
            _diffTextBox.Text = string.IsNullOrEmpty(diff) ? "(No diff available)" : diff;
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
                    _diffFileLabel.Text = cn.FilePath;
                    _diffTextBox.Text = GitWrapper.GetDiffStaged(cn.FilePath) ?? "(No diff)";
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
                    GitWrapper.Reset(cn.FilePath);
                    RefreshData();
                });
                menu.AddButton("View Diff", () =>
                {
                    _diffFileLabel.Text = cn.FilePath;
                    _diffTextBox.Text = GitWrapper.GetDiff(cn.FilePath) ?? "(No diff)";
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
            if (paths.Count > 0)
            {
                GitWrapper.Unstage(paths.ToArray());
                RefreshData();
            }
        }

        private void OnDiscardAll()
        {
            if (!_discardConfirmPending)
            {
                // First click: enter confirmation state
                _discardConfirmPending = true;
                _discardConfirmTimer = 3.0f; // 3 seconds to confirm
                _discardAllBtn.Text = "Confirm Discard?";
                _discardAllBtn.BackgroundColor = new Color(0.8f, 0.2f, 0.1f);
                _discardAllBtn.BackgroundColorHighlighted = new Color(0.9f, 0.3f, 0.15f);
            }
            else
            {
                // Second click: actually discard
                ResetDiscardButton();
                GitWrapper.ResetHard();
                RefreshData();
            }
        }

        /// <summary>
        /// Called each frame to auto-reset the discard confirmation if the timer expires.
        /// </summary>
        public void Update(float dt)
        {
            if (_discardConfirmPending)
            {
                _discardConfirmTimer -= dt;
                if (_discardConfirmTimer <= 0f)
                    ResetDiscardButton();
            }
        }

        private void ResetDiscardButton()
        {
            _discardConfirmPending = false;
            if (_discardAllBtn != null)
            {
                _discardAllBtn.Text = "Discard All";
                _discardAllBtn.BackgroundColor = new Color(0.5f, 0.15f, 0.15f);
                _discardAllBtn.BackgroundColorHighlighted = new Color(0.7f, 0.2f, 0.2f);
            }
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
                GitWrapper.CommitAmend(msg);
            else
                GitWrapper.Commit(msg);

            _commitMessage.Text = string.Empty;
            _amendCheck.Checked = false;
            RefreshData();
            DataChanged?.Invoke();
        }


        /// <summary>
        /// Refresh staged/unstaged trees and update group headers with counts.
        /// Groups files by directory using folder TreeNodes.
        /// </summary>
        public void RefreshData()
        {
            ResetDiscardButton();
            PopulateChangesTrees();
        }

        private void PopulateChangesTrees()
        {
            if (_stagedTree == null || _unstagedTree == null) return;
            _stagedTree.DisposeChildren();
            _unstagedTree.DisposeChildren();

            var changes = GitWrapper.GetStatus();
            int stagedCount = 0;
            int unstagedCount = 0;

            // Separate staged and unstaged
            var staged = new List<GitChange>();
            var unstaged = new List<GitChange>();
            foreach (var change in changes)
            {
                if (change.Staged)
                    staged.Add(change);
                else
                    unstaged.Add(change);
            }

            PopulateTreeWithFolders(_stagedTree, staged);
            PopulateTreeWithFolders(_unstagedTree, unstaged);
            stagedCount = staged.Count;
            unstagedCount = unstaged.Count;

            _stagedTree.PerformLayout();
            _unstagedTree.PerformLayout();

            if (_stagedHeader != null)
                _stagedHeader.Text = $"Staged Changes ({stagedCount})";
            if (_unstagedHeader != null)
                _unstagedHeader.Text = $"Unstaged Changes ({unstagedCount})";
        }

        /// <summary>
        /// Groups files by their parent directory and creates folder TreeNodes.
        /// Files at the root level are added directly to the tree.
        /// Files in subdirectories are grouped under a folder node.
        /// </summary>
        private void PopulateTreeWithFolders(Tree tree, List<GitChange> changes)
        {
            var rootFiles = new List<GitChange>();
            var folderGroups = new Dictionary<string, List<GitChange>>();

            foreach (var change in changes)
            {
                string path = change.FilePath;
                int lastSlash = path.IndexOf('/');
                if (lastSlash < 0)
                {
                    // Root-level file
                    rootFiles.Add(change);
                }
                else
                {
                    // File in a subdirectory — group by top-level folder
                    string folder = path.Substring(0, lastSlash);
                    if (!folderGroups.ContainsKey(folder))
                        folderGroups[folder] = new List<GitChange>();
                    folderGroups[folder].Add(change);
                }
            }

            // Add root-level files first
            foreach (var change in rootFiles)
            {
                new ChangeTreeNode(change).Parent = tree;
            }

            // Add folder groups
            foreach (var kvp in folderGroups)
            {
                var folderNode = new TreeNode
                {
                    Text = $"{kvp.Key}/ ({kvp.Value.Count})",
                    TextColor = Style.Current.ForegroundGrey,
                };
                folderNode.Parent = tree;

                foreach (var change in kvp.Value)
                {
                    new ChangeTreeNode(change).Parent = folderNode;
                }

                folderNode.Expand();
            }
        }
    }
}
