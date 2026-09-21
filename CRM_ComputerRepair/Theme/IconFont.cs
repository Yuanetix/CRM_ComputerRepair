using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Segoe Fluent Icons — the native Windows 11 icon font.
    /// Each glyph is a Unicode character; rendered with FontFamily = "Segoe Fluent Icons".
    /// </summary>
    public static class IconFont
    {
        // ═══════════ NAVIGATION ═══════════

        public const string Dashboard = "\uE80F";   // Home
        public const string Customers = "\uE716";   // People
        public const string Repairs = "\uE90F";   // Repair (wrench)
        public const string Devices = "\uE7F4";   // Devices (pc)
        public const string Parts = "\uE74C";   // Parts (toolbox)
        public const string Suppliers = "\uE731";   // Suppliers (shop/building)
        public const string Reports = "\uE9D9";   // Reports (chart)
        public const string Loyalty = "\uE8C7";   // Loyalty (favorite/star)

        // ═══════════ PROFILE MENU ═══════════

        public const string Profile = "\uE77B";   // Contact / person
        public const string Settings = "\uE713";   // Gear
        public const string SignOut = "\uF3B1";   // Sign-out (power)

        // ═══════════ MISC ═══════════

        public const string Search = "\uE721";   // Magnifier
        public const string Close = "\uE711";   // X
        public const string ChevronUp = "\uE70E";   // ^
        public const string ChevronDown = "\uE70D";   // v
        public const string Bell = "\uEA8F";   // Bell
        public const string Brand = "\uE90F";   // Wrench (Fixory)

        // ═══════════ FACTORY ═══════════

        /// <summary>
        /// Returns a Font using Segoe Fluent Icons at the given size.
        /// Falls back to "Segoe MDL2 Assets" (Windows 10) if Fluent not present.
        /// Falls back to "Segoe UI Symbol" as last resort.
        /// </summary>
        public static Font Create(float size, System.Drawing.FontStyle style = System.Drawing.FontStyle.Regular)
        {
            // Try Segoe Fluent Icons (Windows 11)
            if (IsFontInstalled("Segoe Fluent Icons"))
                return new Font("Segoe Fluent Icons", size, style);

            // Fallback: Segoe MDL2 Assets (Windows 10 — same Unicode range for most glyphs)
            if (IsFontInstalled("Segoe MDL2 Assets"))
                return new Font("Segoe MDL2 Assets", size, style);

            // Final fallback
            return new Font("Segoe UI Symbol", size, style);
        }

        private static bool IsFontInstalled(string familyName)
        {
            try
            {
                using var font = new Font(familyName, 10F);
                return string.Equals(font.Name, familyName, System.StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}