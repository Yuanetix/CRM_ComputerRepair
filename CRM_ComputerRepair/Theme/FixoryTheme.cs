using System.Drawing;

namespace CRM.winforms
{
    /// <summary>
    /// Legacy shim — kept only so older screens compile.
    /// New code must use <see cref="AppTheme"/> + <see cref="UiKit"/>.
    /// SaaS consolidation: one token source, no competing neutrals.
    /// </summary>
    public static class FixoryTheme
    {
        public static Color Primary => AppTheme.Primary;
        public static Color PrimaryHover => AppTheme.PrimaryHover;
        public static Color PrimaryLight => AppTheme.PrimaryGradientA;
        public static Color PrimarySoft => AppTheme.PrimarySoft;
        public static Color PrimaryTint => AppTheme.PrimarySoft;

        public static Color Background => AppTheme.Background;
        public static Color Surface => AppTheme.Surface;
        public static Color SurfaceHover => AppTheme.SurfaceHover;
        public static Color InputBg => AppTheme.InputBg;

        public static Color Border => AppTheme.Border;
        public static Color BorderStrong => AppTheme.BorderStrong;
        public static Color BorderFocus => AppTheme.BorderFocus;

        public static Color SidebarBg => AppTheme.SidebarBg;
        public static Color SidebarText => AppTheme.SidebarText;
        public static Color SidebarMuted => AppTheme.TextMuted;
        public static Color SidebarHover => AppTheme.SurfaceHover;
        public static Color SidebarActiveBg => AppTheme.SidebarActiveBg;
        public static Color SidebarActiveTx => AppTheme.SidebarActiveTx;

        public static Color TextPrimary => AppTheme.TextPrimary;
        public static Color TextSecondary => AppTheme.TextSecondary;
        public static Color TextMuted => AppTheme.TextMuted;

        public static Color Success => AppTheme.Success;
        public static Color Warning => AppTheme.Warning;
        public static Color Danger => AppTheme.Danger;
        public static Color DangerSoft => AppTheme.DangerSoft;
        public static Color Neutral => AppTheme.Neutral;

        public static Font FontTitle => AppTheme.FontTitle;
        public static Font FontSubtitle => AppTheme.FontSubtitle;
        public static Font FontLabel => AppTheme.FontLabel;
        public static Font FontInput => AppTheme.FontInput;
        public static Font FontButton => AppTheme.FontButton;
        public static Font FontBody => AppTheme.FontBody;
        public static Font FontStatus => AppTheme.FontStatus;
        public static Font FontSidebar => AppTheme.FontSidebar;
        public static Font FontSidebarBrand => AppTheme.FontBrand;
        public static Font FontError => AppTheme.FontError;

        public const int Radius = AppTheme.RadiusSmall;
        public const int CardPadding = AppTheme.CardPadding;
        public const int InputHeight = AppTheme.InputHeight;
        public const int ButtonHeight = AppTheme.ButtonHeight;
        public const int GapSmall = AppTheme.GapTiny;
        public const int GapMedium = AppTheme.GapMedium;
        public const int GapLarge = AppTheme.GapLarge;
    }
}
