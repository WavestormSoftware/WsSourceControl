using System.Collections.Generic;
using FlaxEngine;
using FlaxEngine.GUI;

namespace WsSourceControl.UI
{
    public class SectionHeader : ContainerControl
    {
        private readonly Label _titleLabel;
        private readonly Label _countLabel;
        private readonly List<Button> _actions = new List<Button>();

        public SectionHeader(string title)
        {
            Height = SourceControlTheme.SectionHeaderHeight;
            BackgroundColor = SourceControlTheme.SectionBackground;

            _titleLabel = new Label
            {
                Parent = this,
                Text = title,
                TextColor = Style.Current.Foreground,
                VerticalAlignment = TextAlignment.Center,
                HorizontalAlignment = TextAlignment.Near,
            };

            _countLabel = new Label
            {
                Parent = this,
                Text = string.Empty,
                TextColor = SourceControlTheme.MutedText,
                VerticalAlignment = TextAlignment.Center,
                HorizontalAlignment = TextAlignment.Near,
                AutoWidth = true,
            };

        }

        public void SetCount(int count)
        {
            _countLabel.Text = count.ToString();
        }

        public void SetSubtitle(string text)
        {
            _countLabel.Text = text ?? string.Empty;
        }

        public void AddAction(Button button)
        {
            button.Parent = this;
            _actions.Add(button);
            LayoutActions();
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            Height = SourceControlTheme.SectionHeaderHeight;
            if (Parent != null && Width <= 0f)
                Width = Parent.Width;
            LayoutActions();
        }

        private void LayoutActions()
        {
            if (Width <= 0f)
                return;

            var pad = SourceControlTheme.Padding;
            var actionWidth = 0f;
            for (int i = 0; i < _actions.Count; i++)
            {
                actionWidth += _actions[i].Width;
                if (i > 0)
                    actionWidth += SourceControlTheme.Gap;
            }

            var x = System.Math.Max(pad, Width - actionWidth - pad);
            for (int i = 0; i < _actions.Count; i++)
            {
                var button = _actions[i];
                button.Bounds = new Rectangle(x, 2f, button.Width, SourceControlTheme.ButtonHeight);
                x += button.Width + SourceControlTheme.Gap;
            }

            var labelRight = System.Math.Max(80f, Width - actionWidth - pad * 3f);
            _titleLabel.Bounds = new Rectangle(pad, 0f, System.Math.Min(120f, labelRight * 0.55f), SourceControlTheme.SectionHeaderHeight);
            _countLabel.Bounds = new Rectangle(_titleLabel.Right + 8f, 0f, System.Math.Max(0f, labelRight - _titleLabel.Right - 8f), SourceControlTheme.SectionHeaderHeight);
        }

        public override void Draw()
        {
            base.Draw();
            Render2D.FillRectangle(new Rectangle(0, Height - 1f, Width, 1f), Style.Current.BorderNormal);
        }
    }
}
