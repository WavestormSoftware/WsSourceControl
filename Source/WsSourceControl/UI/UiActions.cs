using System;
using FlaxEngine;
using FlaxEngine.GUI;

namespace WsSourceControl.UI
{
    public static class UiActions
    {
        public static Button Button(string text, Action clicked, float width = 72f, string tooltip = null)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = SourceControlTheme.ButtonHeight,
                BackgroundColor = Style.Current.BackgroundNormal,
                BackgroundColorHighlighted = Style.Current.BackgroundHighlighted,
                TooltipText = tooltip ?? text,
            };
            if (clicked != null)
                button.Clicked += clicked;
            return button;
        }

        public static Button PrimaryButton(string text, Action clicked, float width = 110f, string tooltip = null)
        {
            var button = Button(text, clicked, width, tooltip);
            button.BackgroundColor = Style.Current.BackgroundSelected;
            button.BackgroundColorHighlighted = Style.Current.BackgroundSelected.RGBMultiplied(1.2f);
            return button;
        }

        public static Button DangerButton(string text, Action clicked, float width = 86f, string tooltip = null)
        {
            var button = Button(text, clicked, width, tooltip);
            button.BackgroundColor = SourceControlTheme.Danger;
            button.BackgroundColorHighlighted = SourceControlTheme.DangerHover;
            return button;
        }

        public static Label Badge(string text, Color color)
        {
            return new Label
            {
                Text = text,
                TextColor = color,
                AutoWidth = true,
                Height = SourceControlTheme.CompactRowHeight,
                VerticalAlignment = TextAlignment.Center,
            };
        }

        public static bool ConfirmDanger(string message)
        {
            return MessageBox.Show(message, "Confirm Git Operation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }
    }
}
