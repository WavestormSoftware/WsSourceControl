using System;
using FlaxEngine;
using FlaxEngine.GUI;

namespace WsSourceControl.UI
{
    public class EmptyState : ContainerControl
    {
        private readonly Label _titleLabel;
        private readonly Label _messageLabel;
        private readonly Button _primaryButton;
        private readonly Label _noteLabel;

        public EmptyState(string title, string message, string primaryAction = null, Action primaryClicked = null, string note = null)
        {
            Height = primaryAction == null ? 58f : 102f;
            BackgroundColor = Style.Current.Background;

            _titleLabel = new Label
            {
                Parent = this,
                Text = title,
                TextColor = Style.Current.Foreground,
                HorizontalAlignment = TextAlignment.Center,
                VerticalAlignment = TextAlignment.Center,
                Height = 24f,
            };

            _messageLabel = new Label
            {
                Parent = this,
                Text = message,
                TextColor = SourceControlTheme.MutedText,
                HorizontalAlignment = TextAlignment.Center,
                VerticalAlignment = TextAlignment.Center,
                Height = 22f,
            };

            if (!string.IsNullOrEmpty(primaryAction))
            {
                _primaryButton = UiActions.PrimaryButton(primaryAction, primaryClicked, 170f);
                _primaryButton.Parent = this;
            }

            if (!string.IsNullOrEmpty(note))
            {
                _noteLabel = new Label
                {
                    Parent = this,
                    Text = note,
                    TextColor = SourceControlTheme.MutedText,
                    HorizontalAlignment = TextAlignment.Center,
                    VerticalAlignment = TextAlignment.Center,
                    Height = 18f,
                };
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            var y = SourceControlTheme.Padding;
            _titleLabel.Bounds = new Rectangle(0, y, Width, 24f);
            y += 24f;
            _messageLabel.Bounds = new Rectangle(0, y, Width, 22f);
            y += 28f;

            if (_primaryButton != null)
            {
                _primaryButton.Bounds = new Rectangle((Width - _primaryButton.Width) * 0.5f, y, _primaryButton.Width, SourceControlTheme.ButtonHeight);
                y += SourceControlTheme.ButtonHeight + 4f;
            }

            if (_noteLabel != null)
                _noteLabel.Bounds = new Rectangle(0, y, Width, 18f);
        }
    }
}
