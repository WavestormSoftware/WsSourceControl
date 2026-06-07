using FlaxEngine;
using FlaxEngine.GUI;

namespace WsSourceControl.UI
{
    public static class SourceControlTheme
    {
        public const float HeaderHeight = 52f;
        public const float TabsHeight = 28f;
        public const float SectionHeaderHeight = 26f;
        public const float RowHeight = 24f;
        public const float CompactRowHeight = 22f;
        public const float ButtonHeight = 22f;
        public const float Padding = 6f;
        public const float Gap = 4f;

        public static Color Success => new Color(0.35f, 0.85f, 0.35f);
        public static Color Warning => new Color(0.9f, 0.7f, 0.2f);
        public static Color Error => new Color(0.9f, 0.3f, 0.3f);
        public static Color Info => new Color(0.4f, 0.65f, 0.95f);
        public static Color Danger => new Color(0.5f, 0.15f, 0.15f);
        public static Color DangerHover => new Color(0.7f, 0.2f, 0.2f);

        public static Color MutedText => Style.Current.ForegroundGrey;
        public static Color RowBackground => Style.Current.Background;
        public static Color SectionBackground => Style.Current.BackgroundNormal;
        public static Color Accent => Style.Current.BackgroundSelected;
    }
}
