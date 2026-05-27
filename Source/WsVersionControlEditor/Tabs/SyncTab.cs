using System;
using System.Collections.Generic;
using FlaxEditor.CustomEditors.Elements;
using FlaxEditor.GUI;
using FlaxEngine;
using FlaxEngine.GUI;
using WsVersionControlEditor.Git;

namespace WsVersionControlEditor.VcsTabs
{
    /// <summary>
    /// The Sync tab provides remote operations (fetch/pull/push),
    /// stash management, and conflict detection in a clear layout.
    /// </summary>
    public class SyncTab
    {
        private ContainerControl _stashContainer;
        private ContainerControl _conflictContainer;

        /// <summary>
        /// Called when sync operations change the working tree state.
        /// </summary>
        public event Action DataChanged;

        /// <summary>
        /// Builds the Sync tab UI inside the given Tab control.
        /// </summary>
        public void Build(FlaxEditor.GUI.Tabs.Tab tab)
        {
            var container = new Panel(ScrollBars.Vertical)
            {
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                Parent = tab,
            };

            var content = new VerticalPanel
            {
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = Margin.Zero,
                AutoSize = true,
                Pivot = Float2.Zero,
                Spacing = 6f,
                Margin = new Margin(6f),
                Parent = container,
            };

            var syncHeader = new Label
            {
                Text = "Remote Sync",
                Parent = content,
                Font = new FontReference(Style.Current.FontTitle),
                HorizontalAlignment = TextAlignment.Near,
                Height = 24,
            };

            var syncActionsRow = new HorizontalPanel
            {
                Parent = content,
                Height = 26,
                Spacing = 4f,
            };

            var fetchBtn = new Button { Text = "Fetch", Parent = syncActionsRow, Width = 80 };
            fetchBtn.Clicked += () =>
            {
                GitWrapper.Fetch();
                RefreshData();
                DataChanged?.Invoke();
            };

            var pullBtn = new Button { Text = "Pull", Parent = syncActionsRow, Width = 80, BackgroundColor = new Color(0.2f, 0.55f, 0.2f), BackgroundColorHighlighted = new Color(0.25f, 0.65f, 0.25f) };
            pullBtn.Clicked += () =>
            {
                GitWrapper.Pull();
                RefreshData();
                DataChanged?.Invoke();
            };

            var pushBtn = new Button { Text = "Push", Parent = syncActionsRow, Width = 80, BackgroundColor = new Color(0.55f, 0.3f, 0.15f), BackgroundColorHighlighted = new Color(0.65f, 0.35f, 0.18f) };
            pushBtn.Clicked += () =>
            {
                GitWrapper.Push();
                RefreshData();
                DataChanged?.Invoke();
            };

            var stashHeader = new Label
            {
                Text = "Stash",
                Parent = content,
                Font = new FontReference(Style.Current.FontTitle),
                HorizontalAlignment = TextAlignment.Near,
                Height = 24,
                Margin = new Margin(0, 0, 12, 0),
            };

            _stashContainer = new VerticalPanel
            {
                Parent = content,
                AutoSize = true,
                Pivot = Float2.Zero,
                Spacing = 2f,
            };

            var stashBtnRow = new HorizontalPanel
            {
                Parent = _stashContainer,
                Height = 26,
                Spacing = 4f,
            };

            var stashBtn = new Button
            {
                Text = "Stash",
                Parent = stashBtnRow,
                Width = 60,
                Height = 22,
                BackgroundColor = Style.Current.BackgroundNormal,
                BackgroundColorHighlighted = Style.Current.BackgroundHighlighted,
            };
            stashBtn.Clicked += () =>
            {
                GitWrapper.Stash();
                RefreshData();
                DataChanged?.Invoke();
            };

            var popBtn = new Button
            {
                Text = "Pop Latest",
                Parent = stashBtnRow,
                Width = 80,
                Height = 22,
                BackgroundColor = new Color(0.2f, 0.55f, 0.2f),
                BackgroundColorHighlighted = new Color(0.25f, 0.65f, 0.25f),
            };
            popBtn.Clicked += () =>
            {
                GitWrapper.StashPop();
                RefreshData();
                DataChanged?.Invoke();
            };

            var conflictHeader = new Label
            {
                Text = "Merge Conflicts",
                Parent = content,
                Font = new FontReference(Style.Current.FontTitle),
                HorizontalAlignment = TextAlignment.Near,
                Height = 24,
                Margin = new Margin(0, 0, 12, 0),
            };

            _conflictContainer = new VerticalPanel
            {
                Parent = content,
                AutoSize = true,
                Pivot = Float2.Zero,
                Spacing = 2f,
            };

            new Label
            {
                Parent = _conflictContainer,
                Text = "Conflicts are detected automatically when they exist.",
                TextColor = Style.Current.ForegroundGrey,
                AutoWidth = true,
                Margin = new Margin(4, 0, 2, 4),
            };
        }


        /// <summary>
        /// Refresh stash list and conflict status.
        /// </summary>
        public void RefreshData()
        {
            PopulateStashList();
            PopulateConflictSection();
        }

        private void PopulateStashList()
        {
            if (_stashContainer == null) return;

            // Remove dynamically added stash entries (keep template buttons/labels)
            for (int i = _stashContainer.ChildrenCount - 1; i >= 0; i--)
            {
                var child = _stashContainer.Children[i];
                if (child.Tag as string == "stashEntry" || child.Tag as string == "stashEmpty")
                {
                    child.Dispose();
                }
            }

            var stashes = GitWrapper.GetStashList();
            if (stashes.Count == 0)
            {
                new Label
                {
                    Text = "No stashes.",
                    Parent = _stashContainer,
                    TextColor = Style.Current.ForegroundGrey,
                    AutoWidth = true,
                    Margin = new Margin(4, 0, 2, 4),
                    Tag = "stashEmpty",
                };
                return;
            }

            foreach (var stash in stashes)
            {
                var row = new ContainerControl
                {
                    Parent = _stashContainer,
                    Height = 26,
                    AnchorPreset = AnchorPresets.HorizontalStretchTop,
                    Offsets = new Margin(2, 2, 0, 26), // 26 height
                    Tag = "stashEntry",
                };

                var dropBtn = new Button
                {
                    Text = "Drop",
                    Parent = row,
                    AnchorPreset = AnchorPresets.TopRight,
                    Offsets = new Margin(-48, 44, 2, 22),
                    BackgroundColor = new Color(0.5f, 0.15f, 0.15f),
                    BackgroundColorHighlighted = new Color(0.7f, 0.2f, 0.2f),
                };
                int idx = stash.Index;
                dropBtn.Clicked += () =>
                {
                    GitWrapper.StashDrop(idx);
                    RefreshData();
                    DataChanged?.Invoke();
                };

                var applyBtn = new Button
                {
                    Text = "Apply",
                    Parent = row,
                    AnchorPreset = AnchorPresets.TopRight,
                    Offsets = new Margin(-100, 48, 2, 22),
                    BackgroundColor = Style.Current.BackgroundNormal,
                    BackgroundColorHighlighted = Style.Current.BackgroundHighlighted,
                };
                applyBtn.Clicked += () =>
                {
                    GitWrapper.StashApply(idx);
                    RefreshData();
                    DataChanged?.Invoke();
                };

                var popBtn = new Button
                {
                    Text = "Pop",
                    Parent = row,
                    AnchorPreset = AnchorPresets.TopRight,
                    Offsets = new Margin(-148, 44, 2, 22),
                    BackgroundColor = Style.Current.BackgroundNormal,
                    BackgroundColorHighlighted = Style.Current.BackgroundHighlighted,
                };
                popBtn.Clicked += () =>
                {
                    GitWrapper.StashPop();
                    RefreshData();
                    DataChanged?.Invoke();
                };

                new Label
                {
                    Text = $"stash@{{{stash.Index}}}: {stash.Message}",
                    Parent = row,
                    AnchorPreset = AnchorPresets.HorizontalStretchTop,
                    Offsets = new Margin(0, 154, 2, 22), // Leaves 154px on the right for buttons
                    HorizontalAlignment = TextAlignment.Near,
                    TextColor = Style.Current.Foreground,
                };

                row.PerformLayout();
            }
        }

        private void PopulateConflictSection()
        {
            if (_conflictContainer == null) return;

            // Remove dynamically added conflict entries
            for (int i = _conflictContainer.ChildrenCount - 1; i >= 0; i--)
            {
                var child = _conflictContainer.Children[i];
                if (child.Tag as string == "conflictEntry")
                {
                    child.Dispose();
                }
            }

            if (!GitWrapper.HasConflicts())
            {
                new Label
                {
                    Text = "No conflicts detected.",
                    Parent = _conflictContainer,
                    TextColor = new Color(0.35f, 0.85f, 0.35f),
                    AutoWidth = true,
                    Margin = new Margin(4, 0, 2, 4),
                    Tag = "conflictEntry",
                };
                return;
            }

            var warningLabel = new Label
            {
                Text = "Merge conflicts detected! Resolve before committing.",
                Parent = _conflictContainer,
                TextColor = new Color(0.9f, 0.3f, 0.3f),
                AutoWidth = true,
                Margin = new Margin(4, 0, 2, 4),
                Tag = "conflictEntry",
            };

            foreach (var file in GitWrapper.GetConflictFiles())
            {
                var row = new ContainerControl
                {
                    Parent = _conflictContainer,
                    Height = 22,
                    AnchorPreset = AnchorPresets.HorizontalStretchTop,
                    Offsets = new Margin(4, 4, 0, 22),
                    Tag = "conflictEntry",
                };

                var openBtn = new Button
                {
                    Text = "Open",
                    Parent = row,
                    AnchorPreset = AnchorPresets.TopRight,
                    Offsets = new Margin(-54, 50, 2, 18),
                    BackgroundColor = Style.Current.BackgroundNormal,
                    BackgroundColorHighlighted = Style.Current.BackgroundHighlighted,
                };
                openBtn.Clicked += () =>
                {
                    var fullPath = System.IO.Path.Combine(GitWrapper.ProjectPath, file.FilePath);
                    if (System.IO.File.Exists(fullPath))
                        FileSystem.ShowFileExplorer(fullPath);
                };

                new Label
                {
                    Text = file.FilePath,
                    Parent = row,
                    AnchorPreset = AnchorPresets.HorizontalStretchTop,
                    Offsets = new Margin(0, 60, 2, 18), // Leaves 60px on right for the open button
                    HorizontalAlignment = TextAlignment.Near,
                    TextColor = new Color(0.9f, 0.5f, 0.2f),
                };

                row.PerformLayout();
            }
        }
    }
}
