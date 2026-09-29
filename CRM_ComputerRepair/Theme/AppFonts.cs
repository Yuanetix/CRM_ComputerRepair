using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;

namespace CRM.winforms
{
    /// <summary>
    /// Single source of truth for the app's TEXT font family: Inter.
    ///
    /// Chain: embedded Inter (Fonts/*.ttf) → installed "Inter" →
    /// fallback "Segoe UI". Icon glyphs (Fluent/MDL2, see <see cref="IconFont"/>)
    /// and the monospace face (<see cref="MonoFamily"/>) are intentionally
    /// separate — they are not text and must never be forced into Inter.
    ///
    /// All text painting must go through <see cref="Regular"/>, <see cref="Strong"/>,
    /// <see cref="Mono"/> or the <see cref="AppTheme"/> / <see cref="UiKit"/> tokens
    /// (which themselves route through here). No other file may name a text family.
    ///
    /// NOTE: embedded fonts are NOT visible to GDI by family name — fonts must
    /// be constructed from the loaded <see cref="FontFamily"/> object, which is
    /// exactly what <see cref="Regular"/> and <see cref="Strong"/> do.
    /// </summary>
    public static class AppFonts
    {
        /// <summary>App text family. "Inter" when available, else "Segoe UI".</summary>
        public const string FamilyName = "Inter";

        /// <summary>Monospace face for logs/code. Kept separate by design.</summary>
        public const string MonoFamily = "Consolas";

        /// <summary>PDF family — matches <see cref="FamilyName"/> when Inter loads.</summary>
        public static string PdfFamily
        {
            get { EnsureLoaded(); return _interReady ? "Inter" : "Segoe UI"; }
        }

        /// <summary>Resolved text family name (Inter or the Segoe fallback).</summary>
        public static string Family
        {
            get { EnsureLoaded(); return _interReady ? FamilyName : "Segoe UI"; }
        }

        /// <summary>True when Inter (embedded or installed) is in effect.</summary>
        public static bool InterAvailable
        {
            get { EnsureLoaded(); return _interReady; }
        }

        private static readonly PrivateFontCollection _collection = new();
        private static readonly object _lock = new();
        private static readonly System.Collections.Generic.List<IntPtr> _pinned = new();
        private static bool _initialized;
        private static bool _interReady;
        private static FontFamily? _interFamily;

        /// <summary>Loads embedded Inter once. Safe to call on every startup path.</summary>
        public static void EnsureLoaded()
        {
            if (_initialized) return;
            lock (_lock)
            {
                if (_initialized) return;
                try
                {
                    LoadEmbedded();
                    _interFamily = _collection.Families
                        .FirstOrDefault(f => string.Equals(f.Name, FamilyName, StringComparison.OrdinalIgnoreCase));
                    _interReady = _interFamily != null || IsInstalled(FamilyName);
                }
                catch
                {
                    _interFamily = null;
                    _interReady = false;
                }
                _initialized = true;
            }
        }

        /// <summary>Regular-weight text. Preserves extra styles (Bold/Italic).</summary>
        public static Font Regular(float size, FontStyle style = FontStyle.Regular)
        {
            EnsureLoaded();
            if (_interFamily != null)
                return new Font(_interFamily, size, style);
            if (_interReady)
                return new Font(FamilyName, size, style); // installed system-wide
            return new Font("Segoe UI", size, style);
        }

        /// <summary>
        /// Semibold-equivalent text (what the old "Segoe UI Semibold" family meant).
        /// Inter has no separate GDI semibold family, so Bold weight is used.
        /// </summary>
        public static Font Strong(float size, FontStyle extra = FontStyle.Regular)
        {
            EnsureLoaded();
            if (_interFamily != null)
                return new Font(_interFamily, size, FontStyle.Bold | extra);
            if (_interReady)
                return new Font(FamilyName, size, FontStyle.Bold | extra);
            return new Font("Segoe UI Semibold", size, extra);
        }

        /// <summary>Monospace text (logs, references). Never Inter.</summary>
        public static Font Mono(float size, FontStyle style = FontStyle.Regular)
            => new(MonoFamily, size, style);

        private static void LoadEmbedded()
        {
            var asm = typeof(AppFonts).Assembly;
            string[] names;
            try { names = asm.GetManifestResourceNames(); }
            catch { return; }
            var fonts = names
                .Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                         || n.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            foreach (var name in fonts)
            {
                try
                {
                    using var stream = asm.GetManifestResourceStream(name);
                    if (stream == null) continue;
                    var bytes = new byte[stream.Length];
                    int read = 0;
                    while (read < bytes.Length)
                    {
                        int n = stream.Read(bytes, read, bytes.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    if (read <= 0) continue;
                    IntPtr ptr = Marshal.AllocCoTaskMem(read);
                    Marshal.Copy(bytes, 0, ptr, read);
                    _pinned.Add(ptr); // kept for process lifetime; freeing breaks the collection
                    _collection.AddMemoryFont(ptr, read);
                }
                catch
                {
                    // One bad file must not block the rest.
                }
            }
        }

        private static bool IsInstalled(string name)
        {
            try
            {
                using var probe = new Font(name, 10F);
                return string.Equals(probe.Name, name, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }
}
