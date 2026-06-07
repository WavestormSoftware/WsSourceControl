using System;
using FlaxEditor.GUI;
using FlaxEngine;
using FlaxEngine.GUI;
using WsSourceControl.Git;
using WsSourceControl.UI;

namespace WsSourceControl.VcsTabs
{
    public class SyncTab
    {
        private VerticalPanel _stashContainer;
        private VerticalPanel _conflictContainer;
        private Label _remoteSummaryLabel;
        private Label _remoteErrorLabel;
        private SectionHeader _stashHeader;
        private SectionHeader _conflictHeader;
        private GitAsyncWrapper _asyncWrapper;
        private string _lastRemoteError;

        public event Action DataChanged;

        public void Build(FlaxEditor.GUI.Tabs.Tab tab, GitAsyncWrapper asyncWrapper = null)
        {
            _asyncWrapper = asyncWrapper;

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
                Spacing = 8f,
                Margin = new Margin(6f),
                Parent = container,
            };

            var remoteHeader = new SectionHeader("Remote Sync")
            {
                Parent = content,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
            };
            remoteHeader.AddAction(UiActions.Button("Fetch", () => RunRemote(GitWrapper.Fetch, "Fetching..."), 58f, "Fetch from remote"));
            remoteHeader.AddAction(UiActions.Button("Pull", () => RunRemote(GitWrapper.Pull, "Pulling..."), 52f, "Pull current branch"));
            remoteHeader.AddAction(UiActions.Button("Push", () => RunRemote(GitWrapper.Push, "Pushing..."), 52f, "Push current branch"));

            _remoteSummaryLabel = new Label
            {
                Parent = content,
                Text = "",
                TextColor = SourceControlTheme.MutedText,
                Height = SourceControlTheme.CompactRowHeight,
                VerticalAlignment = TextAlignment.Center,
            };

            _remoteErrorLabel = new Label
            {
                Parent = content,
                Text = "",
                TextColor = SourceControlTheme.Error,
                Height = SourceControlTheme.CompactRowHeight,
                VerticalAlignment = TextAlignment.Center,
            };

            _stashHeader = new SectionHeader("Stash")
            {
                Parent = content,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
            };
            _stashHeader.AddAction(UiActions.Button("Stash", OnStash, 58f, "Stash current local changes"));
            _stashHeader.AddAction(UiActions.Button("Pop Latest", OnPopLatest, 82f, "Apply and remove the latest stash"));

            _stashContainer = new VerticalPanel
            {
                Parent = content,
                AutoSize = true,
                Pivot = Float2.Zero,
                Spacing = 2f,
            };

            _conflictHeader = new SectionHeader("Merge Conflicts")
            {
                Parent = content,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
            };

            _conflictContainer = new VerticalPanel
            {
                Parent = content,
                AutoSize = true,
                Pivot = Float2.Zero,
                Spacing = 2f,
            };
        }

        public void RefreshData()
        {
            PopulateRemoteSummary();
            PopulateStashList();
            PopulateConflictSection();
        }

        private void PopulateRemoteSummary()
        {
            var snapshot = GitWrapper.GetSnapshot();
            var remote = string.IsNullOrEmpty(snapshot.RemoteUrl) ? "No remote configured" : snapshot.RemoteUrl;
            var upstream = string.IsNullOrEmpty(snapshot.UpstreamName) ? "No upstream" : snapshot.UpstreamName;
            _remoteSummaryLabel.Text = $"{remote}  |  {upstream}  |  Up {snapshot.Ahead} Down {snapshot.Behind}";
            _remoteErrorLabel.Text = string.IsNullOrEmpty(_lastRemoteError) ? string.Empty : _lastRemoteError;
        }

        private void OnStash()
        {
            GitWrapper.Stash();
            RefreshData();
            DataChanged?.Invoke();
        }

        private void OnPopLatest()
        {
            if (!UiActions.ConfirmDanger("Pop the latest stash?\n\nThis applies the stash and removes it if successful."))
                return;
            GitWrapper.StashPop();
            RefreshData();
            DataChanged?.Invoke();
        }

        private void PopulateStashList()
        {
            if (_stashContainer == null) return;
            _stashContainer.DisposeChildren();

            var stashes = GitWrapper.GetStashList();
            _stashHeader?.SetCount(stashes.Count);

            if (stashes.Count == 0)
            {
                AddEmptyLabel(_stashContainer, "No stashes.");
                return;
            }

            foreach (var stash in stashes)
            {
                var row = new HorizontalPanel
                {
                    Parent = _stashContainer,
                    Height = SourceControlTheme.RowHeight,
                    Spacing = SourceControlTheme.Gap,
                };

                new Label
                {
                    Parent = row,
                    Text = $"stash@{{{stash.Index}}}: {stash.Message}",
                    TextColor = Style.Current.Foreground,
                    Width = 360f,
                    Height = SourceControlTheme.RowHeight,
                    VerticalAlignment = TextAlignment.Center,
                };

                var idx = stash.Index;
                UiActions.Button("Apply", () =>
                {
                    GitWrapper.StashApply(idx);
                    RefreshData();
                    DataChanged?.Invoke();
                }, 52f, $"Apply stash@{{{idx}}}").Parent = row;

                UiActions.Button("Pop", () =>
                {
                    if (!UiActions.ConfirmDanger($"Pop stash@{{{idx}}}?\n\nThis applies the stash and removes it if successful."))
                        return;
                    GitWrapper.StashPop(idx);
                    RefreshData();
                    DataChanged?.Invoke();
                }, 46f, $"Pop stash@{{{idx}}}").Parent = row;

                UiActions.DangerButton("Drop", () =>
                {
                    if (!UiActions.ConfirmDanger($"Drop stash@{{{idx}}}?\n\nThis cannot be undone from the plugin."))
                        return;
                    GitWrapper.StashDrop(idx);
                    RefreshData();
                    DataChanged?.Invoke();
                }, 50f, $"Drop stash@{{{idx}}}").Parent = row;
            }
        }

        private void PopulateConflictSection()
        {
            if (_conflictContainer == null) return;
            _conflictContainer.DisposeChildren();

            var conflicts = GitWrapper.GetConflictFiles();
            _conflictHeader?.SetCount(conflicts.Count);

            if (conflicts.Count == 0)
            {
                AddEmptyLabel(_conflictContainer, "No conflicts detected.", SourceControlTheme.Success);
                return;
            }

            AddEmptyLabel(_conflictContainer, "Resolve conflicts before committing.", SourceControlTheme.Error);
            foreach (var file in conflicts)
            {
                var row = new HorizontalPanel
                {
                    Parent = _conflictContainer,
                    Height = SourceControlTheme.RowHeight,
                    Spacing = SourceControlTheme.Gap,
                };

                new Label
                {
                    Parent = row,
                    Text = file.FilePath,
                    TextColor = SourceControlTheme.Warning,
                    Width = 420f,
                    Height = SourceControlTheme.RowHeight,
                    VerticalAlignment = TextAlignment.Center,
                };

                UiActions.Button("Open", () =>
                {
                    var fullPath = System.IO.Path.Combine(GitWrapper.ProjectPath, file.FilePath);
                    if (System.IO.File.Exists(fullPath))
                        FileSystem.ShowFileExplorer(fullPath);
                }, 52f, "Open file location").Parent = row;
            }
        }

        private static void AddEmptyLabel(ContainerControl parent, string text, Color? color = null)
        {
            new Label
            {
                Parent = parent,
                Text = text,
                TextColor = color ?? SourceControlTheme.MutedText,
                AutoWidth = true,
                Height = SourceControlTheme.CompactRowHeight,
                Margin = new Margin(4, 0, 2, 4),
                VerticalAlignment = TextAlignment.Center,
            };
        }

        private void RunRemote(Func<GitResult> operation, string statusText)
        {
            if (_asyncWrapper == null)
            {
                var result = operation();
                _lastRemoteError = result.Success ? string.Empty : result.Error;
                RefreshData();
                DataChanged?.Invoke();
                return;
            }

            _asyncWrapper.RunAsync(operation, result =>
            {
                _lastRemoteError = result.Success ? string.Empty : result.Error;
                RefreshData();
                DataChanged?.Invoke();
            }, statusText);
        }
    }
}
