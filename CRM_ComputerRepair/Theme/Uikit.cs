using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Single source of truth for the shell's neutrals, type scale, spacing and painting.
    /// Brand hues still come from AppTheme so the rest of the app stays in step.
    /// </summary>
    public static class UiKit
    {
        // ═══════════ COLOR ═══════════

        public static readonly Color Canvas = Color.FromArgb(0xFA, 0xFA, 0xFB);
        public static readonly Color Surface = Color.White;
        public static readonly Color Line = Color.FromArgb(0xE6, 0xE8, 0xEC);
        public static readonly Color LineSoft = Color.FromArgb(0xF1, 0xF3, 0xF6);
        public static readonly Color Ink = Color.FromArgb(0x1A, 0x1D, 0x23);
        public static readonly Color InkMuted = Color.FromArgb(0x6B, 0x72, 0x80);
        public static readonly Color InkFaint = Color.FromArgb(0x9A, 0xA1, 0xAD);
        public static readonly Color Hover = Color.FromArgb(0xF8, 0xF9, 0xFB);

        public static Color Accent => AppTheme.Primary;
        public static Color AccentHover => AppTheme.PrimaryHover;
        public static Color AccentActive => AppTheme.PrimaryActive;
        public static Color Danger => AppTheme.Danger;

        // ═══════════ SPACE (4pt scale) ═══════════

        public const int S1 = 4, S2 = 8, S3 = 12, S4 = 16, S5 = 24, S6 = 32;

        // ═══════════ GEOMETRY ═══════════

        public const int SidebarWidth = 248;
        public const int SidebarRail = 72;
        public const int TopBarHeight = 64;
        public const int NavHeight = 40;
        public const int NavGap = 2;
        public const int Radius = 10;
        public const int RadiusSm = 8;

        // ═══════════ TYPE (one family, hierarchy by size + weight) ═══════════

        public static readonly Font Brand = new Font("Segoe UI Semibold", 13.5F);
        public static readonly Font Title = new Font("Segoe UI Semibold", 13F);
        public static readonly Font Body = new Font("Segoe UI", 9.5F);
        public static readonly Font BodyStrong = new Font("Segoe UI Semibold", 9.5F);
        public static readonly Font Small = new Font("Segoe UI", 8.75F);
        public static readonly Font SmallStrong = new Font("Segoe UI Semibold", 8.5F);
        public static readonly Font Micro = new Font("Segoe UI", 8F);

        // ═══════════ PAINT ═══════════

        public static void Quality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public static Color Mix(Color a, Color b, double t) => Color.FromArgb(
            (int)Math.Round(a.R + (b.R - a.R) * t),
            (int)Math.Round(a.G + (b.G - a.G) * t),
            (int)Math.Round(a.B + (b.B - a.B) * t));

        /// <summary>Light wash of a semantic colour — used for selected states and pills.</summary>
        public static Color Wash(Color c) => Mix(c, Color.White, 0.88);

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d <= 0) { path.AddRectangle(r); return path; }

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Rectangle r, int radius, Color fill)
        {
            using var p = Rounded(r, radius);
            using var b = new SolidBrush(fill);
            g.FillPath(b, p);
        }

        public static void StrokeRounded(Graphics g, Rectangle r, int radius, Color stroke, float width = 1f)
        {
            using var p = Rounded(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), radius);
            using var pen = new Pen(stroke, width);
            g.DrawPath(pen, p);
        }

        /// <summary>Flat white card with a 1px hairline. No shadow.</summary>
        public static void Card(Graphics g, Rectangle r, int radius, Color fill, Color border)
        {
            r = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
            using var p = Rounded(r, radius);
            using var b = new SolidBrush(fill);
            using var pen = new Pen(border, 1);
            g.FillPath(b, p);
            g.DrawPath(pen, p);
        }

        public static void Dot(Graphics g, float cx, float cy, float size, Color c)
        {
            using var b = new SolidBrush(c);
            g.FillEllipse(b, cx - size / 2f, cy - size / 2f, size, size);
        }

        public static void HLine(Graphics g, int x1, int x2, int y, Color c)
        {
            using var pen = new Pen(c, 1);
            g.DrawLine(pen, x1, y, x2, y);
        }

        public static void VLine(Graphics g, int x, int y1, int y2, Color c)
        {
            using var pen = new Pen(c, 1);
            g.DrawLine(pen, x, y1, x, y2);
        }

        public static void Text(Graphics g, string text, Font font, Color color,
                                Rectangle bounds, TextFormatFlags flags)
            => TextRenderer.DrawText(g, text, font, bounds, color, flags);

        public static Size Measure(string text, Font font)
            => TextRenderer.MeasureText(text, font);

        public const TextFormatFlags Left =
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;

        public const TextFormatFlags Center =
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;

        /// <summary>Glyph font, falling back to Segoe UI Symbol where MDL2 is missing.</summary>
        public static Font GlyphFont(float size)
        {
            try { return IconFont.Create(size); }
            catch { return new Font("Segoe UI Symbol", size); }
        }

        public static void Initials(Graphics g, Rectangle avatar, string name, Color bg, Font font)
        {
            FillRounded(g, avatar, avatar.Height / 2, bg);

            var parts = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string initials = parts.Length >= 2
                ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
                : parts.Length == 1 ? parts[0][..1].ToUpperInvariant() : "?";

            Text(g, initials, font, Color.White, avatar, Center);
        }

        // ═══════════════════════════════════════════════════════════════
        //  MODULE TOKENS
        //  Shared by every list module (Interactions, Customers,
        //  Follow-Ups, Repair Requests) so they render identically.
        // ═══════════════════════════════════════════════════════════════

        public static class T
        {
            // Neutrals
            public static readonly Color Surface = Color.White;
            public static readonly Color Line = Color.FromArgb(0xE6, 0xE8, 0xEC);
            public static readonly Color LineSoft = Color.FromArgb(0xF1, 0xF3, 0xF6);
            public static readonly Color Ink = Color.FromArgb(0x1A, 0x1D, 0x23);
            public static readonly Color InkMuted = Color.FromArgb(0x6B, 0x72, 0x80);
            public static readonly Color InkFaint = Color.FromArgb(0x9A, 0xA1, 0xAD);
            public static readonly Color RowHover = Color.FromArgb(0xF8, 0xF9, 0xFB);

            // Spacing — 4pt scale
            public const int S1 = 4, S2 = 8, S3 = 12, S4 = 16, S5 = 24, S6 = 32;

            // Geometry
            public const int Radius = 10;
            public const int PillRadius = 9;
            public const int StripHeight = 86;
            public const int InputHeight = 34;
            public const int ButtonHeight = 36;
            public const int RowHeight = 46;
            public const int HeaderHeight = 38;

            // Type — one family, hierarchy carried by size and weight
            public static readonly Font Title = new Font("Segoe UI Semibold", 16.5F);
            public static readonly Font Subtitle = new Font("Segoe UI", 9.5F);
            public static readonly Font Section = new Font("Segoe UI Semibold", 11F);
            public static readonly Font Body = new Font("Segoe UI", 9.5F);
            public static readonly Font BodyStrong = new Font("Segoe UI Semibold", 9.5F);
            public static readonly Font Small = new Font("Segoe UI", 8.75F);
            public static readonly Font SmallStrong = new Font("Segoe UI Semibold", 8.5F);
            public static readonly Font Metric = new Font("Segoe UI Light", 25F);
            public static readonly Font Glyph = new Font("Segoe MDL2 Assets", 11F);
            public static readonly Font GlyphLarge = new Font("Segoe MDL2 Assets", 22F);
        }
    }
}