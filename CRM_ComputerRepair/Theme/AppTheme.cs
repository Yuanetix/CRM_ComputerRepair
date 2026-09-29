using System.Drawing;

namespace CRM.winforms
{
    /// <summary>
    /// Fixory design system — modern, tonal, soft-UI dashboard style.
    /// Spacing follows a strict 4/8pt scale. Color states favor soft tinted
    /// backgrounds over solid fills (the "Linear/Notion" pattern) — it reads
    /// as calmer and more current than flat blocks of brand color.
    /// </summary>
    public static class AppTheme
    {
        // ═══════════ BRAND ACCENT ═══════════
        public static readonly Color Primary         = Color.FromArgb(99, 102, 241);   // indigo-500
        public static readonly Color PrimaryHover     = Color.FromArgb(79, 82, 221);
        public static readonly Color PrimaryActive    = Color.FromArgb(67, 70, 200);
        public static readonly Color PrimarySoft      = Color.FromArgb(238, 239, 255);
        public static readonly Color PrimaryGradientA = Color.FromArgb(129, 131, 255);  // lighter stop
        public static readonly Color PrimaryGradientB = Color.FromArgb(88, 80, 236);    // darker stop

        // ═══════════ SURFACES ═══════════
        public static readonly Color Background      = Color.FromArgb(248, 248, 251);
        public static readonly Color Surface          = Color.FromArgb(255, 255, 255);
        public static readonly Color SurfaceHover      = Color.FromArgb(247, 247, 250);
        public static readonly Color InputBg           = Color.FromArgb(255, 255, 255);

        // ═══════════ SIDEBAR ═══════════
        public static readonly Color SidebarBg        = Color.FromArgb(255, 255, 255);
        public static readonly Color SidebarText        = Color.FromArgb(113, 118, 130);
        public static readonly Color SidebarTextHov     = Color.FromArgb(31, 41, 55);
        // Modern nav pattern: active items get a soft tinted background +
        // colored text/icon, not a solid brand-color block. Solid fills read
        // as an older "Bootstrap admin template" style.
        public static readonly Color SidebarActiveBg   = PrimarySoft;
        public static readonly Color SidebarActiveTx    = Primary;
        public static readonly Color SidebarAccentBar   = Primary;

        // ═══════════ BORDERS ═══════════
        public static readonly Color Border           = Color.FromArgb(235, 236, 242);
        public static readonly Color BorderStrong      = Color.FromArgb(218, 220, 228);
        public static readonly Color BorderFocus       = Primary;
        public static readonly Color Divider           = Color.FromArgb(240, 241, 246);

        // ═══════════ TEXT ═══════════
        public static readonly Color TextPrimary      = Color.FromArgb(17, 20, 32);
        public static readonly Color TextSecondary      = Color.FromArgb(105, 111, 126);
        public static readonly Color TextMuted           = Color.FromArgb(156, 162, 176);

        // ═══════════ STATUS (base + soft tint pairs, used for pills/badges) ═══════════
        public static readonly Color Success          = Color.FromArgb(22, 163, 74);
        public static readonly Color SuccessSoft       = Color.FromArgb(230, 250, 238);
        public static readonly Color Warning          = Color.FromArgb(217, 119, 6);
        public static readonly Color WarningSoft        = Color.FromArgb(255, 246, 230);
        public static readonly Color Danger           = Color.FromArgb(220, 38, 38);
        public static readonly Color DangerSoft         = Color.FromArgb(254, 242, 242);
        public static readonly Color Neutral            = Color.FromArgb(245, 246, 249);
        public static readonly Color Info             = Color.FromArgb(37, 99, 235);
        public static readonly Color InfoSoft         = Color.FromArgb(239, 246, 255);

        // ═══════════ SAAS / HCI TOKENS ═══════════
        // Single source for focus, overlays and non-blocking feedback.
        // HCI rationale (Nielsen): focus must always be visible (H1), feedback
        // must not block flow (H1/H9) — toasts over MessageBox for success/info,
        // dialogs only for destructive / irreversible actions.
        public static readonly Color FocusRing        = Color.FromArgb(165, 180, 252); // indigo-300, 2px outer ring
        public static readonly Color OverlayDim       = Color.FromArgb(110, 17, 24, 39); // scrim behind modals/palette
        public static readonly Color ToastText        = Color.FromArgb(17, 20, 32);
        public static readonly Color ToastBorder      = Color.FromArgb(235, 236, 242);
        public static readonly Color SearchHintBg     = Color.FromArgb(247, 247, 250);
        public static readonly Color KbdBg            = Color.FromArgb(245, 246, 249);
        public static readonly Color KbdBorder        = Color.FromArgb(218, 220, 228);

        // ═══════════ STAT TILE COLORS (soft pastel bg + matched-weight fg) ═══════════
        public static readonly Color TilePinkBg        = Color.FromArgb(255, 240, 245);
        public static readonly Color TilePinkFg          = Color.FromArgb(219, 39, 119);
        public static readonly Color TileOrangeBg        = Color.FromArgb(255, 245, 232);
        public static readonly Color TileOrangeFg          = Color.FromArgb(217, 119, 6);
        public static readonly Color TileGreenBg          = Color.FromArgb(235, 252, 242);
        public static readonly Color TileGreenFg            = Color.FromArgb(22, 163, 74);
        public static readonly Color TilePurpleBg           = Color.FromArgb(245, 240, 255);
        public static readonly Color TilePurpleFg             = Color.FromArgb(126, 34, 206);

        // ═══════════ FONTS ═══════════
        // One family (Inter via AppFonts) everywhere; hierarchy is carried
        // by size + weight. Strong == old "Segoe UI Semibold" slots.
        public static readonly Font FontDisplay        = AppFonts.Strong(22F);
        public static readonly Font FontTitle           = AppFonts.Strong(17F);
        public static readonly Font FontPageTitle       = AppFonts.Strong(15F);
        public static readonly Font FontPageSubtitle    = AppFonts.Regular(9.5F);
        public static readonly Font FontBreadcrumb      = AppFonts.Regular(8F);
        public static readonly Font FontKbd             = AppFonts.Strong(7.5F);
        public static readonly Font FontToastTitle      = AppFonts.Strong(9F);
        public static readonly Font FontToastBody       = AppFonts.Regular(8.5F);
        public static readonly Font FontSection          = AppFonts.Strong(11.5F);
        public static readonly Font FontSubtitle          = AppFonts.Regular(9.5F);
        public static readonly Font FontLabel               = AppFonts.Regular(8F);
        public static readonly Font FontInput                = AppFonts.Regular(9.5F);
        public static readonly Font FontButton                 = AppFonts.Strong(9F);
        public static readonly Font FontBody                    = AppFonts.Regular(9F);
        public static readonly Font FontMedium                  = AppFonts.Strong(10.5F);
        public static readonly Font FontSmall                   = AppFonts.Regular(8.5F);
        public static readonly Font FontSmallStrong             = AppFonts.Strong(8.5F);
        public static readonly Font FontMicro                   = AppFonts.Regular(7.5F);
        public static readonly Font FontMicroStrong             = AppFonts.Strong(7.5F);
        public static readonly Font FontStatus                   = AppFonts.Regular(8F);
        public static readonly Font FontSidebar                   = AppFonts.Regular(9.5F);
        public static readonly Font FontSidebarActive               = AppFonts.Strong(9.5F);
        public static readonly Font FontBrand                        = AppFonts.Strong(13.5F);
        public static readonly Font FontStatNumber                     = AppFonts.Strong(24F);
        public static readonly Font FontStatLabel                        = AppFonts.Regular(8.5F);
        public static readonly Font FontStatDelta                          = AppFonts.Strong(8F);
        public static readonly Font FontError                                = AppFonts.Regular(7.5F);

        // ═══════════ RADIUS ═══════════
        // Larger, softer corners across the board — one of the clearest
        // "this looks current" signals.
        public const int Radius        = 14;
        public const int RadiusSmall   = 10;
        public const int RadiusPill    = 999; // request a full stadium shape
        public const int ButtonRadius  = 10;

        // ═══════════ LAYOUT ═══════════
        public const int SidebarWidth  = 232;
        public const int TopBarHeight  = 64;

        public const int CardPadding   = 20;
        public const int InputHeight   = 36;
        public const int ButtonHeight  = 38;

        // ═══════════ SPACING SCALE (4pt base unit) ═══════════
        public const int Space2  = 2;
        public const int Space4  = 4;
        public const int Space8  = 8;
        public const int Space12 = 12;
        public const int Space16 = 16;
        public const int Space20 = 20;
        public const int Space24 = 24;
        public const int Space32 = 32;
        public const int Space40 = 40;
        public const int Space48 = 48;

        public const int GapTiny       = Space8;
        public const int GapSmall      = Space12;
        public const int GapMedium     = Space16;
        public const int GapLarge      = Space24;
        public const int GapXLarge     = Space32;

        // ═══════════ SIDEBAR-SPECIFIC RHYTHM ═══════════
        public const int SidebarItemHeight = 42;
        public const int SidebarItemGap    = Space8; // a touch more air between rows than a dense admin panel
        public const int SidebarSidePad    = Space16;

        // ═══════════ ELEVATION (tinted, not pure black — modern shadows carry a hint of the surface's hue) ═══════════
        public static readonly Color ShadowTint = Color.FromArgb(20, 30, 41, 59); // slate, low alpha

        // ═══════════ MODAL ═══════════
        // Solid, opaque frame behind a modal card's shadow margin. Fully
        // opaque on purpose — see ModalForm for why.
        public static readonly Color ModalFrame = Color.FromArgb(23, 25, 32);
    }
}