using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Fonts;

namespace CRM.winforms.Common
{
    /// <summary>
    /// Thread-safe Windows system font resolver for PDFsharp 6.x.
    /// Maps font family names and styles to installed Windows TrueType fonts,
    /// with intelligent fallbacks so PDF generation never fails.
    /// </summary>
    public sealed class WindowsFontResolver : IFontResolver
    {
        private static readonly Dictionary<string, byte[]> FontDataCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly string WindowsFontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        private static readonly object SyncLock = new();
        private static bool _isRegistered;

        /// <summary>
        /// Ensures this font resolver is registered globally for PDFsharp once.
        /// </summary>
        public static void EnsureRegistered()
        {
            if (_isRegistered) return;
            lock (SyncLock)
            {
                if (_isRegistered) return;
                try
                {
                    GlobalFontSettings.FontResolver = new WindowsFontResolver();
                    _isRegistered = true;
                }
                catch
                {
                    // Already assigned or initialized
                    _isRegistered = true;
                }
            }
        }

        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            string clean = (familyName ?? "Segoe UI").Trim().ToLowerInvariant();

            string fileName = clean switch
            {
                // App text family — served from embedded Fonts/*.ttf (see GetFont).
                "inter" => (isBold, isItalic) switch
                {
                    (_, true) => "Inter-Italic.ttf",
                    (true, false) => "Inter-Bold.ttf",
                    _ => "Inter-Regular.ttf"
                },
                "segoe ui" or "segoe" => (isBold, isItalic) switch
                {
                    (true, true) => "segoeuiz.ttf",
                    (true, false) => "segoeuib.ttf",
                    (false, true) => "segoeuii.ttf",
                    _ => "segoeui.ttf"
                },
                "arial" => (isBold, isItalic) switch
                {
                    (true, true) => "arialbi.ttf",
                    (true, false) => "arialbd.ttf",
                    (false, true) => "ariali.ttf",
                    _ => "arial.ttf"
                },
                "calibri" => (isBold, isItalic) switch
                {
                    (true, true) => "calibriz.ttf",
                    (true, false) => "calibrib.ttf",
                    (false, true) => "calibrii.ttf",
                    _ => "calibri.ttf"
                },
                "tahoma" => (isBold, isItalic) switch
                {
                    (true, _) => "tahomabd.ttf",
                    _ => "tahoma.ttf"
                },
                "consolas" => (isBold, isItalic) switch
                {
                    (true, true) => "consolaz.ttf",
                    (true, false) => "consolab.ttf",
                    (false, true) => "consolai.ttf",
                    _ => "consola.ttf"
                },
                _ => (isBold, isItalic) switch
                {
                    (true, true) => "segoeuiz.ttf",
                    (true, false) => "segoeuib.ttf",
                    (false, true) => "segoeuii.ttf",
                    _ => "segoeui.ttf"
                }
            };

            return new FontResolverInfo(fileName);
        }

        public byte[]? GetFont(string faceName)
        {
            lock (SyncLock)
            {
                if (FontDataCache.TryGetValue(faceName, out var cached))
                    return cached;

                // Embedded app fonts (Fonts/*.ttf) take precedence so PDFs match
                // the UI even on machines without Inter installed.
                var embedded = ReadEmbedded(faceName);
                if (embedded != null)
                {
                    FontDataCache[faceName] = embedded;
                    return embedded;
                }

                string fontPath = Path.Combine(WindowsFontsDir, faceName);
                if (!File.Exists(fontPath))
                {
                    // Fallback search in Windows Fonts folder
                    fontPath = Path.Combine(WindowsFontsDir, "segoeui.ttf");
                    if (!File.Exists(fontPath))
                        fontPath = Path.Combine(WindowsFontsDir, "arial.ttf");
                }

                if (File.Exists(fontPath))
                {
                    byte[] data = File.ReadAllBytes(fontPath);
                    FontDataCache[faceName] = data;
                    return data;
                }

                return null;
            }
        }

        private static byte[]? ReadEmbedded(string faceName)
        {
            try
            {
                var asm = typeof(WindowsFontResolver).Assembly;
                var match = asm.GetManifestResourceNames().FirstOrDefault(
                    n => n.EndsWith("/" + faceName, StringComparison.OrdinalIgnoreCase)
                      || n.EndsWith("." + faceName, StringComparison.OrdinalIgnoreCase)
                      || string.Equals(n, faceName, StringComparison.OrdinalIgnoreCase));
                // Resource names use dots: CRM.winforms.Fonts.Inter-Regular.ttf
                match ??= asm.GetManifestResourceNames().FirstOrDefault(
                    n => n.EndsWith("Fonts." + faceName.Replace("-", ".").Replace("_", "."),
                        StringComparison.OrdinalIgnoreCase));
                if (match == null && faceName.StartsWith("Inter-", StringComparison.OrdinalIgnoreCase))
                    match = asm.GetManifestResourceNames().FirstOrDefault(
                        n => n.IndexOf("Inter", StringComparison.OrdinalIgnoreCase) >= 0
                          && n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                          && n.IndexOf(faceName.Contains("Bold") ? "Bold"
                              : faceName.Contains("Italic") ? "Italic" : "Regular",
                              StringComparison.OrdinalIgnoreCase) >= 0);
                if (match == null) return null;
                using var stream = asm.GetManifestResourceStream(match);
                if (stream == null) return null;
                var bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int n = stream.Read(bytes, read, bytes.Length - read);
                    if (n <= 0) break;
                    read += n;
                }
                return read > 0 ? bytes : null;
            }
            catch { return null; }
        }
    }
}
