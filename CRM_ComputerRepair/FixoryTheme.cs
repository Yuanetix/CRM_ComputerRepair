using System.Drawing;

namespace CRM.winforms
{
    /// <summary>
    /// Fixory design system — clean, mostly-white, minimal color.
    /// HCI: high contrast, visible inputs, blue only for primary action.
    /// </summary>
    public static class FixoryTheme
    {
        // ═══════════ ACCENT (used sparingly) ═══════════
        public static readonly Color Primary = Color.FromArgb(46, 102, 224);   // MutedBlue
        public static readonly Color PrimaryHover = Color.FromArgb(33, 89, 212);    // RoyalBlue
        public static readonly Color PrimaryLight = Color.FromArgb(59, 115, 237);   // CloudBlue
        public static readonly Color PrimarySoft = Color.FromArgb(239, 246, 255);  // Pale Blue (active bg)
        public static readonly Color PrimaryTint = Color.FromArgb(219, 234, 254);  // Light tint

        // ═══════════ SURFACES (all white) ═══════════
        public static readonly Color Background = Color.FromArgb(247, 248, 250);  // Very Light Gray (page)
        public static readonly Color Surface = Color.FromArgb(255, 255, 255);  // White (cards, sidebar, topbar)
        public static readonly Color SurfaceHover = Color.FromArgb(243, 244, 246);  // Light gray hover
        public static readonly Color InputBg = Color.FromArgb(255, 255, 255);  // White input bg

        // ═══════════ BORDERS ═══════════
        public static readonly Color Border = Color.FromArgb(229, 231, 235);  // Light Gray
        public static readonly Color BorderStrong = Color.FromArgb(209, 213, 219);  // Medium Gray (input border)
        public static readonly Color BorderFocus = Color.FromArgb(46, 102, 224);   // Blue focus

        // ═══════════ SIDEBAR (white) ═══════════
        public static readonly Color SidebarBg = Color.FromArgb(255, 255, 255);  // White
        public static readonly Color SidebarText = Color.FromArgb(31, 41, 55);     // Slate Dark
        public static readonly Color SidebarMuted = Color.FromArgb(156, 163, 175);  // Slate Light (brand)
        public static readonly Color SidebarHover = Color.FromArgb(243, 244, 246);  // Light gray hover
        public static readonly Color SidebarActiveBg = Color.FromArgb(239, 246, 255);  // Pale Blue
        public static readonly Color SidebarActiveTx = Color.FromArgb(46, 102, 224);   // Blue text

        // ═══════════ TEXT ═══════════
        public static readonly Color TextPrimary = Color.FromArgb(17, 24, 39);     // Slate Dark (near black)
        public static readonly Color TextSecondary = Color.FromArgb(107, 114, 128);  // Slate Medium
        public static readonly Color TextMuted = Color.FromArgb(156, 163, 175);  // Slate Light

        // ═══════════ STATUS ═══════════
        public static readonly Color Success = Color.FromArgb(16, 185, 129);   // Emerald
        public static readonly Color Warning = Color.FromArgb(245, 158, 11);   // Amber
        public static readonly Color Danger = Color.FromArgb(239, 68, 68);    // Red
        public static readonly Color DangerSoft = Color.FromArgb(254, 242, 242);  // Light red (error bg)
        public static readonly Color Neutral = Color.FromArgb(243, 244, 246);  // Slate 100

        // ═══════════ FONTS ═══════════
        public static readonly Font FontTitle = new Font("Segoe UI", 16F, FontStyle.Bold);
        public static readonly Font FontSubtitle = new Font("Segoe UI", 10F, FontStyle.Regular);
        public static readonly Font FontLabel = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        public static readonly Font FontInput = new Font("Segoe UI", 10F, FontStyle.Regular);
        public static readonly Font FontButton = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        public static readonly Font FontBody = new Font("Segoe UI", 9F, FontStyle.Regular);
        public static readonly Font FontStatus = new Font("Segoe UI", 8.5F, FontStyle.Regular);
        public static readonly Font FontSidebar = new Font("Segoe UI", 10F, FontStyle.Regular);
        public static readonly Font FontSidebarBrand = new Font("Segoe UI", 8F, FontStyle.Bold);
        public static readonly Font FontError = new Font("Segoe UI", 8F, FontStyle.Regular);

        // ═══════════ SIZES ═══════════
        public const int Radius = 8;
        public const int CardPadding = 20;
        public const int InputHeight = 36;
        public const int ButtonHeight = 40;
        public const int GapSmall = 8;
        public const int GapMedium = 16;
        public const int GapLarge = 24;
    }
}