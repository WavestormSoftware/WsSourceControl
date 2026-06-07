using System;
using FlaxEngine;
using FlaxEngine.GUI;
using WsSourceControl.Git;

namespace WsSourceControl.UI
{
    public class SourceControlHeader : ContainerControl
    {
        private readonly Label _branchLabel;
        private readonly Label _repoLabel;
        private readonly Label _syncLabel;
        private readonly Label _remoteLabel;
        private readonly Label _statusLabel;
        private readonly HorizontalPanel _actions;
        private string _statusText = "Ready";
        private bool _busy;

        public SourceControlHeader()
        {
            Height = SourceControlTheme.HeaderHeight;
            BackgroundColor = Style.Current.Background;

            _branchLabel = new Label { Parent = this, Text = "No Branch", TextColor = Style.Current.Foreground, VerticalAlignment = TextAlignment.Center };
            _repoLabel = new Label { Parent = this, Text = "Source Control", TextColor = SourceControlTheme.MutedText, VerticalAlignment = TextAlignment.Center };
            _syncLabel = new Label { Parent = this, Text = "Up to date", TextColor = SourceControlTheme.Success, VerticalAlignment = TextAlignment.Center };
            _remoteLabel = new Label { Parent = this, Text = "No remote", TextColor = SourceControlTheme.MutedText, VerticalAlignment = TextAlignment.Center };
            _statusLabel = new Label { Parent = this, Text = "Ready", TextColor = SourceControlTheme.Success, HorizontalAlignment = TextAlignment.Far, VerticalAlignment = TextAlignment.Center };
            _actions = new HorizontalPanel { Parent = this, Spacing = SourceControlTheme.Gap, Height = SourceControlTheme.ButtonHeight };
        }

        public void AddCommand(string text, Action clicked, string tooltip)
        {
            UiActions.Button(text, clicked, 58f, tooltip).Parent = _actions;
            LayoutHeader();
        }

        public void UpdateStatus(string text, bool busy)
        {
            _statusText = string.IsNullOrEmpty(text) ? "Ready" : text;
            _busy = busy;
            _statusLabel.Text = _busy ? _statusText : _statusText;
            _statusLabel.TextColor = _busy ? SourceControlTheme.Warning : SourceControlTheme.Success;
        }

        public void RefreshFromGit()
        {
            var snapshot = GitWrapper.GetSnapshot();
            if (!snapshot.IsRepository)
            {
                _branchLabel.Text = "No Repository";
                _repoLabel.Text = "Project is not inside a Git repository";
                _syncLabel.Text = string.Empty;
                _remoteLabel.Text = string.Empty;
                return;
            }

            _branchLabel.Text = string.IsNullOrEmpty(snapshot.BranchName) ? "Detached HEAD" : snapshot.BranchName;
            _repoLabel.Text = string.IsNullOrEmpty(snapshot.UpstreamName) ? "Local branch" : snapshot.UpstreamName;

            if (snapshot.Ahead == 0 && snapshot.Behind == 0)
            {
                _syncLabel.Text = "Up to date";
                _syncLabel.TextColor = SourceControlTheme.Success;
            }
            else
            {
                _syncLabel.Text = $"Up {snapshot.Ahead}  Down {snapshot.Behind}";
                _syncLabel.TextColor = snapshot.Ahead > 0 && snapshot.Behind > 0 ? SourceControlTheme.Warning : SourceControlTheme.Info;
            }

            var remote = string.IsNullOrEmpty(snapshot.RemoteUrl) ? "No remote" : snapshot.RemoteUrl;
            if (snapshot.HasConflicts)
            {
                remote += "  |  Conflicts";
                _remoteLabel.TextColor = SourceControlTheme.Error;
            }
            else
            {
                if (snapshot.HasLfsConfigured)
                    remote += "  |  LFS";
                _remoteLabel.TextColor = SourceControlTheme.MutedText;
            }
            _remoteLabel.Text = remote;
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            LayoutHeader();
        }

        private void LayoutHeader()
        {
            var pad = SourceControlTheme.Padding;
            var actionWidth = 0f;
            for (int i = 0; i < _actions.ChildrenCount; i++)
            {
                actionWidth += _actions.Children[i].Width;
                if (i > 0)
                    actionWidth += _actions.Spacing;
            }

            _actions.Bounds = new Rectangle(Width - actionWidth - pad, 6f, actionWidth, SourceControlTheme.ButtonHeight);
            _statusLabel.Bounds = new Rectangle(Width - actionWidth - 150f - pad * 2f, 28f, 150f, 18f);

            var contentRight = System.Math.Max(180f, Width - actionWidth - 170f - pad * 3f);
            _branchLabel.Bounds = new Rectangle(pad, 4f, 190f, 22f);
            _repoLabel.Bounds = new Rectangle(pad, 27f, 190f, 18f);
            _syncLabel.Bounds = new Rectangle(210f, 4f, 130f, 22f);
            _remoteLabel.Bounds = new Rectangle(210f, 27f, contentRight - 210f, 18f);
        }

        public override void Draw()
        {
            base.Draw();
            Render2D.FillRectangle(new Rectangle(0, Height - 1f, Width, 1f), Style.Current.BorderNormal);
        }
    }
}
