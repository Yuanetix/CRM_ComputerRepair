using System.Drawing;

namespace CRM.winforms
{
    /// <summary>
    /// Fixory design system — neutral base + subtle blue accent.
    /// HCI: reduces eye strain, emphasizes primary actions.
    /// </summary>
    public static class FixoryTheme
    {
        // ═══════════ BRAND ACCENT (used sparingly) ═══════════
        public static readonly Color Primary = Color.FromArgb(46, 102, 224);   // MutedBlue
        public static readonly Color PrimaryHover = Color.FromArgb(33, 89, 212);    // RoyalBlue
        public static readonly Color PrimaryLight = Color.FromArgb(59, 115, 237);   // CloudBlue
        public static readonly Color PrimarySoft = Color.FromArgb(233, 239, 253);  // MistBlue

        // ═══════════ NEUTRAL SIDEBAR ═══════════
        public static readonly Color SidebarBg = Color.FromArgb(30, 41, 59);     // Deep Slate
        public static readonly Color SidebarText = Color.FromArgb(226, 232, 240);  // Soft White
        public static readonly Color SidebarMuted = Color.FromArgb(148, 163, 184);  // Slate Light
        public static readonly Color SidebarHover = Color.FromArgb(51, 65, 85);     // Slate Hover
        public static readonly Color SidebarActive = Color.FromArgb(46, 102, 224);   // Primary
        public static readonly Color SidebarDivider = Color.FromArgb(51, 65, 85);     // Divider line

        // ═══════════ SURFACES ═══════════
        public static readonly Color Background = Color.FromArgb(248, 250, 252);  // Very Light Gray
        public static readonly Color Surface = Color.FromArgb(255, 255, 255);  // White
        public static readonly Color TopBarBg = Color.FromArgb(255, 255, 255);  // White
        public static readonly Color Border = Color.FromArgb(226, 232, 240);  // Light Gray
        public static readonly Color BorderFocus = Color.FromArgb(46, 102, 224);   // Primary
        public static readonly Color InputBg = Color.FromArgb(255, 255, 255);  // White

        // ═══════════ TEXT ═══════════
        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);     // Slate Dark
        public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);  // Slate Medium
        public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);  // Slate Light

        // ═══════════ STATUS ═══════════
        public static readonly Color Success = Color.FromArgb(16, 185, 129);   // Emerald
        public static readonly Color Warning = Color.FromArgb(245, 158, 11);   // Amber
        public static readonly Color Danger = Color.FromArgb(239, 68, 68);    // Red
        public static readonly Color Neutral = Color.FromArgb(241, 245, 249);  // Slate 100

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
        public const int InputHeight = 34;
        public const int ButtonHeight = 40;
        public const int GapSmall = 8;
        public const int GapMedium = 16;
        public const int GapLarge = 24;
    }
}