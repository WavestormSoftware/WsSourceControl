using System;
using FlaxEngine;
using FlaxEngine.GUI;
using WsVersionControlEditor.Git;

namespace WsVersionControlEditor.UI
{
    /// <summary>
    /// A horizontal status bar showing current branch, sync status, remote, and operation state.
    /// Sits at the top of the Source Control window.
    /// </summary>
    public class VcsStatusBar : ContainerControl
    {
        private string _branchName = "";
        private int _ahead;
        private int _behind;
        private string _remote = "";
        private string _statusText = "Ready";
        private bool _isBusy;

        // Child controls for interactivity
        private Label _branchLabel;
        private Label _syncLabel;
        private Label _remoteLabel;
        private Label _statusLabel;
        
        // Separator lines
        private const float BarHeight = 28f;
        private const float SeparatorMargin = 6f;

        public VcsStatusBar()
        {
            Height = BarHeight;
            AnchorPreset = AnchorPresets.HorizontalStretchTop;
            Offsets = new Margin(0, 0, 0, BarHeight);
            BackgroundColor = Style.Current.Background;
            
            BuildUI();
        }

        private void BuildUI()
        {
            float x = 8f;
            float labelY = 0;
            float labelH = BarHeight;
            float gap = 16f;

            // Branch icon + label
            _branchLabel = new Label
            {
                Parent = this,
                Text = "No Branch",
                TextColor = Style.Current.Foreground,
                Offsets = new Margin(x, 200, labelY, labelH),
                HorizontalAlignment = TextAlignment.Near,
                VerticalAlignment = TextAlignment.Center,
            };
            x += 200 + gap;

            // Sync info (ahead/behind)
            _syncLabel = new Label
            {
                Parent = this,
                Text = "",
                TextColor = Style.Current.ForegroundGrey,
                Offsets = new Margin(x, 120, labelY, labelH),
                HorizontalAlignment = TextAlignment.Near,
                VerticalAlignment = TextAlignment.Center,
            };
            x += 120 + gap;

            // Remote
            _remoteLabel = new Label
            {
                Parent = this,
                Text = "",
                TextColor = Style.Current.ForegroundGrey,
                Offsets = new Margin(x, 300, labelY, labelH),
                HorizontalAlignment = TextAlignment.Near,
                VerticalAlignment = TextAlignment.Center,
            };

            // Status (right-aligned)
            _statusLabel = new Label
            {
                Parent = this,
                Text = "Ready",
                TextColor = new Color(0.35f, 0.85f, 0.35f), // green
                Offsets = new Margin(0, 120, labelY, labelH),
                AnchorPreset = AnchorPresets.TopRight,
                HorizontalAlignment = TextAlignment.Far,
                VerticalAlignment = TextAlignment.Center,
            };
        }

        public void UpdateBranch(string branchName)
        {
            _branchName = branchName ?? "";
            _branchLabel.Text = string.IsNullOrEmpty(_branchName) ? "No Branch" : _branchName;
            _branchLabel.TextColor = string.IsNullOrEmpty(_branchName) 
                ? Style.Current.ForegroundGrey 
                : Style.Current.Foreground;
        }

        public void UpdateSyncStatus(int ahead, int behind)
        {
            _ahead = ahead;
            _behind = behind;
            if (ahead == 0 && behind == 0)
            {
                _syncLabel.Text = "Up to date";
                _syncLabel.TextColor = new Color(0.35f, 0.85f, 0.35f);
            }
            else
            {
                _syncLabel.Text = $"Up {ahead}  Down {behind}";
                _syncLabel.TextColor = (ahead > 0 && behind > 0)
                    ? new Color(0.9f, 0.7f, 0.2f) // yellow for diverged
                    : new Color(0.4f, 0.6f, 0.9f); // blue for one-way
            }
        }

        public void UpdateRemote(string remote)
        {
            _remote = remote ?? "";
            _remoteLabel.Text = string.IsNullOrEmpty(_remote) ? "No remote" : _remote;
        }

        public void UpdateStatus(string statusText, bool isBusy)
        {
            _statusText = statusText ?? "Ready";
            _isBusy = isBusy;
            _statusLabel.Text = _isBusy ? $"{_statusText}..." : _statusText;
            _statusLabel.TextColor = _isBusy 
                ? new Color(0.9f, 0.7f, 0.2f) // yellow when busy
                : new Color(0.35f, 0.85f, 0.35f); // green when ready
        }

        public override void Draw()
        {
            base.Draw();
            
            // Draw bottom border line
            Render2D.FillRectangle(
                new Rectangle(0, Height - 1, Width, 1),
                Style.Current.BorderNormal);
        }

        public void RefreshFromGit()
        {
            string branch = GitWrapper.GetCurrentBranch();
            UpdateBranch(branch);

            GitWrapper.GetAheadBehind(out int ahead, out int behind);
            UpdateSyncStatus(ahead, behind);

            string remote = GitWrapper.GetRemoteUrl();
            UpdateRemote(remote);
        }
    }
}
