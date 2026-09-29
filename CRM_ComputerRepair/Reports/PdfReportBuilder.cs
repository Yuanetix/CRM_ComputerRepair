using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using CRM.winforms.Common;

namespace CRM.winforms.Reports
{
    /// <summary>
    /// Executive-grade PDF report document generator.
    /// Produces clean, minimalist, industry-standard business intelligence reports
    /// matching the Fixory design system with KPI metric cards, tabular grids,
    /// pill badges, and pagination.
    /// </summary>
    public static class PdfReportBuilder
    {
        // ── Brand & Theme Palette ──
        private static readonly XColor ColorSlate900 = XColor.FromArgb(15, 23, 42);      // #0F172A Primary Ink
        private static readonly XColor ColorSlate700 = XColor.FromArgb(51, 65, 85);      // #334155 Secondary Text
        private static readonly XColor ColorSlate500 = XColor.FromArgb(100, 116, 139);   // #64748B Muted Text
        private static readonly XColor ColorSlate400 = XColor.FromArgb(148, 163, 184);   // #94A3B8 Subtle Text
        private static readonly XColor ColorSlate200 = XColor.FromArgb(226, 232, 240);   // #E2E8F0 Borders / Dividers
        private static readonly XColor ColorSlate100 = XColor.FromArgb(241, 245, 249);   // #F1F5F9 Header Background
        private static readonly XColor ColorSlate50  = XColor.FromArgb(248, 250, 252);   // #F8FAFC Alternate Row
        private static readonly XColor ColorWhite    = XColor.FromArgb(255, 255, 255);
        private static readonly XColor ColorBrandBlue= XColor.FromArgb(37, 99, 235);     // #2563EB Fixory Accent Blue

        // Pill badge palette (Soft pastel background + saturated foreground)
        private static readonly XColor PillSuccessBg = XColor.FromArgb(220, 252, 231);   // #DCFCE7
        private static readonly XColor PillSuccessFg = XColor.FromArgb(21, 128, 61);     // #15803D
        private static readonly XColor PillWarningBg = XColor.FromArgb(254, 243, 199);   // #FEF3C7
        private static readonly XColor PillWarningFg = XColor.FromArgb(180, 83, 9);      // #B45309
        private static readonly XColor PillDangerBg  = XColor.FromArgb(254, 226, 226);   // #FEE2E2
        private static readonly XColor PillDangerFg  = XColor.FromArgb(185, 28, 28);     // #B91C1C
        private static readonly XColor PillInfoBg    = XColor.FromArgb(224, 242, 254);   // #E0F2FE
        private static readonly XColor PillInfoFg    = XColor.FromArgb(3, 105, 161);     // #0369A1
        private static readonly XColor PillNeutralBg = XColor.FromArgb(241, 245, 249);   // #F1F5F9
        private static readonly XColor PillNeutralFg = XColor.FromArgb(71, 85, 105);     // #475569

        // ── Data Transfer Models ──
        public sealed class ReportMetadata
        {
            public string CompanyName { get; set; } = "FIXORY COMPUTER REPAIR";
            public string SystemTagline { get; set; } = "Executive Business Intelligence & Operational Audit";
            public string ReportTitle { get; set; } = "Business Report";
            public string Subtitle { get; set; } = "";
            public string PeriodText { get; set; } = "All Time";
            public string GeneratedBy { get; set; } = "Administrator";
            public DateTime GeneratedAt { get; set; } = DateTime.Now;
        }

        public sealed class KpiItem
        {
            public string Label { get; set; } = "";
            public string Value { get; set; } = "";
            public string Subtext { get; set; } = "";
            public XColor AccentColor { get; set; } = XColor.FromArgb(37, 99, 235);
        }

        public sealed class ColumnDef
        {
            public string Header { get; set; } = "";
            public double Width { get; set; }
            public XStringAlignment Alignment { get; set; } = XStringAlignment.Near;
            public bool IsPillBadge { get; set; }
            public bool IsBold { get; set; }
        }

        public sealed class ReportDocument
        {
            public ReportMetadata Metadata { get; set; } = new();
            public List<KpiItem> Kpis { get; set; } = new();
            public List<ColumnDef> Columns { get; set; } = new();
            public List<string[]> Rows { get; set; } = new();
            public string? SummaryFooterText { get; set; }
            public PageOrientation Orientation { get; set; } = PageOrientation.Landscape;
        }

        /// <summary>
        /// Generates and writes the PDF report to the specified file path.
        /// </summary>
        public static void GenerateReport(ReportDocument docModel, string targetPath)
        {
            WindowsFontResolver.EnsureRegistered();

            using var pdf = new PdfDocument();
            pdf.Info.Title = docModel.Metadata.ReportTitle;
            pdf.Info.Author = docModel.Metadata.CompanyName;
            pdf.Info.Subject = docModel.Metadata.Subtitle;
            pdf.Info.Creator = "Fixory CRM System";

            // Typography — same family as the app (Inter via AppFonts, Segoe fallback).
            string pf = AppFonts.PdfFamily;
            var fontBrand = new XFont(pf, 13, XFontStyleEx.Bold);
            var fontSub = new XFont(pf, 8, XFontStyleEx.Regular);
            var fontTitle = new XFont(pf, 12, XFontStyleEx.Bold);
            var fontMeta = new XFont(pf, 7.5, XFontStyleEx.Regular);
            var fontKpiLabel = new XFont(pf, 7, XFontStyleEx.Bold);
            var fontKpiValue = new XFont(pf, 11.5, XFontStyleEx.Bold);
            var fontKpiSub = new XFont(pf, 6.8, XFontStyleEx.Regular);
            var fontTh = new XFont(pf, 8, XFontStyleEx.Bold);
            var fontTd = new XFont(pf, 7.8, XFontStyleEx.Regular);
            var fontTdBold = new XFont(pf, 7.8, XFontStyleEx.Bold);
            var fontPill = new XFont(pf, 7, XFontStyleEx.Bold);
            var fontFooter = new XFont(pf, 7.5, XFontStyleEx.Regular);

            // Reusable string formats
            var sfNear = new XStringFormat { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.Center };
            var sfCenter = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Center };
            var sfFar = new XStringFormat { Alignment = XStringAlignment.Far, LineAlignment = XLineAlignment.Center };

            // Page dimensions
            const double marginX = 36.0;
            const double marginY = 32.0;

            double pageWidth = docModel.Orientation == PageOrientation.Landscape ? 792.0 : 612.0;
            double pageHeight = docModel.Orientation == PageOrientation.Landscape ? 612.0 : 792.0;
            double printableWidth = pageWidth - (marginX * 2);

            // Normalize column widths to exact printableWidth
            NormalizeColumnWidths(docModel.Columns, printableWidth);

            // Create initial page
            var page = pdf.AddPage();
            page.Orientation = docModel.Orientation;
            var gfx = XGraphics.FromPdfPage(page);

            double curY = marginY;

            // ── Draw Header (Page 1) ──
            DrawHeader(gfx, docModel.Metadata, marginX, ref curY, printableWidth, fontBrand, fontSub, fontTitle, fontMeta);

            // ── Draw KPI Cards (Page 1, if any) ──
            if (docModel.Kpis.Count > 0)
            {
                DrawKpiCards(gfx, docModel.Kpis, marginX, ref curY, printableWidth, fontKpiLabel, fontKpiValue, fontKpiSub);
            }

            // ── Prepare Table Layout ──
            const double thHeight = 22.0;
            const double trHeight = 19.0;
            double bottomLimit = pageHeight - marginY - 26.0; // Reserve room for footer

            // Draw Table Header on Page 1
            DrawTableHeader(gfx, docModel.Columns, marginX, curY, thHeight, fontTh);
            curY += thHeight;

            int pageNumber = 1;
            var pagesList = new List<PdfPage> { page };

            // ── Draw Table Rows ──
            for (int r = 0; r < docModel.Rows.Count; r++)
            {
                // Check if row exceeds page height
                if (curY + trHeight > bottomLimit)
                {
                    // Draw running footer on previous page
                    DrawFooter(gfx, docModel.Metadata, marginX, pageHeight - marginY - 14.0, printableWidth, pageNumber, -1, fontFooter);
                    gfx.Dispose();

                    // Add new page
                    page = pdf.AddPage();
                    page.Orientation = docModel.Orientation;
                    pagesList.Add(page);
                    pageNumber++;
                    gfx = XGraphics.FromPdfPage(page);

                    curY = marginY;

                    // Draw running header on continuation page
                    DrawRunningHeader(gfx, docModel.Metadata, marginX, ref curY, printableWidth, fontTitle, fontMeta);

                    // Re-draw table column headers
                    DrawTableHeader(gfx, docModel.Columns, marginX, curY, thHeight, fontTh);
                    curY += thHeight;
                }

                // Draw Table Row
                string[] rowData = docModel.Rows[r];
                bool isEven = (r % 2 == 0);
                DrawTableRow(gfx, rowData, docModel.Columns, marginX, curY, trHeight, isEven, fontTd, fontTdBold, fontPill);
                curY += trHeight;
            }

            // ── Draw Summary Footer Strip if space permits ──
            if (!string.IsNullOrWhiteSpace(docModel.SummaryFooterText))
            {
                if (curY + 20.0 > bottomLimit)
                {
                    DrawFooter(gfx, docModel.Metadata, marginX, pageHeight - marginY - 14.0, printableWidth, pageNumber, -1, fontFooter);
                    gfx.Dispose();

                    page = pdf.AddPage();
                    page.Orientation = docModel.Orientation;
                    pagesList.Add(page);
                    pageNumber++;
                    gfx = XGraphics.FromPdfPage(page);
                    curY = marginY;
                }

                var summaryRect = new XRect(marginX, curY + 4, printableWidth, 18);
                gfx.DrawRoundedRectangle(new XSolidBrush(ColorSlate50), summaryRect, new XSize(3, 3));
                gfx.DrawRoundedRectangle(new XPen(ColorSlate200, 0.5), summaryRect, new XSize(3, 3));

                var rSummary = new XRect(marginX + 8, curY + 4, printableWidth - 16, 18);
                gfx.DrawString(docModel.SummaryFooterText, fontTdBold, new XSolidBrush(ColorSlate700), rSummary, sfNear);
                curY += 24;
            }

            // Draw footer on final page
            DrawFooter(gfx, docModel.Metadata, marginX, pageHeight - marginY - 14.0, printableWidth, pageNumber, -1, fontFooter);
            gfx.Dispose();

            // ── Re-stamp final "Page X of Y" on all pages ──
            int totalPages = pagesList.Count;
            for (int i = 0; i < totalPages; i++)
            {
                using var pGfx = XGraphics.FromPdfPage(pagesList[i]);
                DrawPageNumber(pGfx, marginX, pageHeight - marginY - 14.0, printableWidth, i + 1, totalPages, fontFooter);
            }

            // Save PDF
            pdf.Save(targetPath);
        }

        // ══════════════ DRAWING PRIMITIVES & HELPERS ══════════════

        private static void DrawHeader(
            XGraphics gfx,
            ReportMetadata meta,
            double x,
            ref double y,
            double width,
            XFont fontBrand,
            XFont fontSub,
            XFont fontTitle,
            XFont fontMeta)
        {
            double headerHeight = 44.0;

            // Brand Logo & Title (Left side)
            gfx.DrawString("FIXORY", fontBrand, new XSolidBrush(ColorBrandBlue), new XPoint(x, y + 14));
            double brandW = gfx.MeasureString("FIXORY", fontBrand).Width;
            gfx.DrawString(" COMPUTER REPAIR", fontBrand, new XSolidBrush(ColorSlate900), new XPoint(x + brandW, y + 14));
            gfx.DrawString(meta.SystemTagline, fontSub, new XSolidBrush(ColorSlate500), new XPoint(x, y + 26));

            // Metadata Block (Right side)
            var sfFar = new XStringFormat { Alignment = XStringAlignment.Far, LineAlignment = XLineAlignment.Near };
            var rTitle = new XRect(x, y + 1, width, 14);
            gfx.DrawString(meta.ReportTitle, fontTitle, new XSolidBrush(ColorSlate900), rTitle, sfFar);

            var rPeriod = new XRect(x, y + 15, width, 12);
            gfx.DrawString($"Period: {meta.PeriodText}", fontMeta, new XSolidBrush(ColorSlate700), rPeriod, sfFar);

            var rGen = new XRect(x, y + 27, width, 12);
            string genInfo = $"Generated: {meta.GeneratedAt:dd/MM/yyyy HH:mm} • User: {meta.GeneratedBy}";
            gfx.DrawString(genInfo, fontMeta, new XSolidBrush(ColorSlate400), rGen, sfFar);

            // Divider Line
            y += headerHeight;
            gfx.DrawLine(new XPen(ColorSlate200, 0.8), x, y, x + width, y);
            y += 8.0;
        }

        private static void DrawRunningHeader(
            XGraphics gfx,
            ReportMetadata meta,
            double x,
            ref double y,
            double width,
            XFont fontTitle,
            XFont fontMeta)
        {
            var sfNear = new XStringFormat { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.Center };
            var sfFar = new XStringFormat { Alignment = XStringAlignment.Far, LineAlignment = XLineAlignment.Center };

            var rLeft = new XRect(x, y, width * 0.6, 16);
            gfx.DrawString($"Fixory Computer Repair — {meta.ReportTitle}", fontTitle, new XSolidBrush(ColorSlate900), rLeft, sfNear);

            var rRight = new XRect(x + width * 0.4, y, width * 0.6, 16);
            gfx.DrawString($"Period: {meta.PeriodText} • {meta.GeneratedAt:dd/MM/yyyy}", fontMeta, new XSolidBrush(ColorSlate500), rRight, sfFar);

            y += 18.0;
            gfx.DrawLine(new XPen(ColorSlate200, 0.6), x, y, x + width, y);
            y += 8.0;
        }

        private static void DrawKpiCards(
            XGraphics gfx,
            List<KpiItem> kpis,
            double startX,
            ref double y,
            double totalWidth,
            XFont fontLabel,
            XFont fontVal,
            XFont fontSub)
        {
            int count = Math.Min(4, kpis.Count);
            if (count == 0) return;

            const double gap = 10.0;
            double cardWidth = (totalWidth - (gap * (count - 1))) / count;
            const double cardHeight = 44.0;

            var sfNear = new XStringFormat { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.Center };

            for (int i = 0; i < count; i++)
            {
                var kpi = kpis[i];
                double cx = startX + (i * (cardWidth + gap));
                var cardRect = new XRect(cx, y, cardWidth, cardHeight);

                // Card background & subtle border
                gfx.DrawRoundedRectangle(new XSolidBrush(ColorSlate50), cardRect, new XSize(3.5, 3.5));
                gfx.DrawRoundedRectangle(new XPen(ColorSlate200, 0.6), cardRect, new XSize(3.5, 3.5));

                // Left Accent Indicator
                var barRect = new XRect(cx, y + 3, 3.0, cardHeight - 6);
                gfx.DrawRoundedRectangle(new XSolidBrush(kpi.AccentColor), barRect, new XSize(1, 1));

                // Text padding
                double tx = cx + 8.5;
                double tw = cardWidth - 12.0;

                // Label
                var rLabel = new XRect(tx, y + 4, tw, 10);
                gfx.DrawString(kpi.Label.ToUpperInvariant(), fontLabel, new XSolidBrush(ColorSlate500), rLabel, sfNear);

                // Value
                var rVal = new XRect(tx, y + 14, tw, 16);
                gfx.DrawString(kpi.Value, fontVal, new XSolidBrush(ColorSlate900), rVal, sfNear);

                // Subtext
                var rSub = new XRect(tx, y + 30, tw, 10);
                gfx.DrawString(kpi.Subtext, fontSub, new XSolidBrush(ColorSlate400), rSub, sfNear);
            }

            y += cardHeight + 10.0;
        }

        private static void DrawTableHeader(
            XGraphics gfx,
            List<ColumnDef> columns,
            double startX,
            double y,
            double height,
            XFont font)
        {
            var headerRect = new XRect(startX, y, columns.Sum(c => c.Width), height);
            gfx.DrawRoundedRectangle(new XSolidBrush(ColorSlate100), headerRect, new XSize(2, 2));

            // Bottom border for header
            gfx.DrawLine(new XPen(XColor.FromArgb(203, 213, 225), 0.8), startX, y + height, startX + headerRect.Width, y + height);

            double curX = startX;
            foreach (var col in columns)
            {
                var colRect = new XRect(curX + 6, y, col.Width - 12, height);
                var sf = new XStringFormat { Alignment = col.Alignment, LineAlignment = XLineAlignment.Center };
                gfx.DrawString(col.Header.ToUpperInvariant(), font, new XSolidBrush(ColorSlate700), colRect, sf);
                curX += col.Width;
            }
        }

        private static void DrawTableRow(
            XGraphics gfx,
            string[] rowData,
            List<ColumnDef> columns,
            double startX,
            double y,
            double height,
            bool isEven,
            XFont fontRegular,
            XFont fontBold,
            XFont fontPill)
        {
            double totalWidth = columns.Sum(c => c.Width);
            var rowRect = new XRect(startX, y, totalWidth, height);

            // Alternating row background
            if (!isEven)
            {
                gfx.DrawRectangle(new XSolidBrush(ColorSlate50), rowRect);
            }
            // Bottom hairline border
            gfx.DrawLine(new XPen(ColorSlate100, 0.5), startX, y + height, startX + totalWidth, y + height);

            double curX = startX;
            for (int c = 0; c < columns.Count; c++)
            {
                var col = columns[c];
                string cellText = (c < rowData.Length ? rowData[c] : "") ?? "";

                if (col.IsPillBadge && !string.IsNullOrWhiteSpace(cellText))
                {
                    DrawPillBadge(gfx, cellText, curX, y, col.Width, height, fontPill);
                }
                else
                {
                    var cellRect = new XRect(curX + 6, y, col.Width - 12, height);
                    var sf = new XStringFormat { Alignment = col.Alignment, LineAlignment = XLineAlignment.Center };
                    var font = col.IsBold ? fontBold : fontRegular;
                    var brush = col.IsBold ? new XSolidBrush(ColorSlate900) : new XSolidBrush(ColorSlate700);

                    // Truncate cleanly if string is too wide for column
                    string display = TruncateString(gfx, cellText, font, col.Width - 14);
                    gfx.DrawString(display, font, brush, cellRect, sf);
                }

                curX += col.Width;
            }
        }

        private static void DrawPillBadge(
            XGraphics gfx,
            string text,
            double x,
            double y,
            double colWidth,
            double rowHeight,
            XFont font)
        {
            var (bg, fg) = GetPillPalette(text);

            double measuredW = gfx.MeasureString(text, font).Width;
            double pillW = Math.Min(colWidth - 8, Math.Max(28.0, measuredW + 12.0));
            double pillH = 13.5;
            double pillX = x + ((colWidth - pillW) / 2.0);
            double pillY = y + ((rowHeight - pillH) / 2.0);

            var pillRect = new XRect(pillX, pillY, pillW, pillH);
            gfx.DrawRoundedRectangle(new XSolidBrush(bg), pillRect, new XSize(6.75, 6.75));

            var sf = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Center };
            gfx.DrawString(text, font, new XSolidBrush(fg), pillRect, sf);
        }

        private static (XColor bg, XColor fg) GetPillPalette(string text)
        {
            string clean = text.Trim().ToLowerInvariant();

            if (clean.Contains("paid") || clean.Contains("completed") || clean.Contains("active") ||
                clean == "yes" || clean.Contains("resolved") || clean.Contains("closed") || clean.Contains("approved"))
            {
                return (PillSuccessBg, PillSuccessFg);
            }

            if (clean.Contains("progress") || clean.Contains("pending") || clean.Contains("risk") ||
                clean.Contains("drifting") || clean.Contains("warn") || clean.Contains("medium"))
            {
                return (PillWarningBg, PillWarningFg);
            }

            if (clean.Contains("urgent") || clean.Contains("inactive") || clean.Contains("archived") ||
                clean.Contains("cancel") || clean == "no" || clean.Contains("danger") || clean.Contains("high risk"))
            {
                return (PillDangerBg, PillDangerFg);
            }

            if (clean.Contains("cash") || clean.Contains("gcash") || clean.Contains("card") ||
                clean.Contains("transfer") || clean.Contains("elite") || clean.Contains("vip") ||
                clean.Contains("gold") || clean.Contains("silver") || clean.Contains("bronze"))
            {
                return (PillInfoBg, PillInfoFg);
            }

            return (PillNeutralBg, PillNeutralFg);
        }

        private static void DrawFooter(
            XGraphics gfx,
            ReportMetadata meta,
            double x,
            double y,
            double width,
            int pageNumber,
            int totalPages,
            XFont font)
        {
            // Divider line above footer
            gfx.DrawLine(new XPen(ColorSlate200, 0.6), x, y - 6, x + width, y - 6);

            var sfNear = new XStringFormat { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.Center };
            var rLeft = new XRect(x, y, width * 0.5, 12);
            gfx.DrawString($"{meta.CompanyName} CRM • Confidential & Proprietary", font, new XSolidBrush(ColorSlate400), rLeft, sfNear);
        }

        private static void DrawPageNumber(
            XGraphics gfx,
            double x,
            double y,
            double width,
            int pageNumber,
            int totalPages,
            XFont font)
        {
            var sfFar = new XStringFormat { Alignment = XStringAlignment.Far, LineAlignment = XLineAlignment.Center };
            var rRight = new XRect(x + width * 0.5, y, width * 0.5, 12);
            string pageText = $"Page {pageNumber} of {totalPages}";
            gfx.DrawString(pageText, font, new XSolidBrush(ColorSlate500), rRight, sfFar);
        }

        private static void NormalizeColumnWidths(List<ColumnDef> columns, double targetWidth)
        {
            if (columns.Count == 0) return;

            double sum = columns.Sum(c => c.Width);
            if (Math.Abs(sum - targetWidth) < 0.01) return;

            if (sum <= 0)
            {
                double each = targetWidth / columns.Count;
                foreach (var c in columns) c.Width = each;
                return;
            }

            double scale = targetWidth / sum;
            double accumulated = 0;
            for (int i = 0; i < columns.Count; i++)
            {
                if (i == columns.Count - 1)
                {
                    columns[i].Width = targetWidth - accumulated;
                }
                else
                {
                    double w = Math.Round(columns[i].Width * scale, 1);
                    columns[i].Width = w;
                    accumulated += w;
                }
            }
        }

        private static string TruncateString(XGraphics gfx, string text, XFont font, double maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (maxWidth <= 10) return text;

            double w = gfx.MeasureString(text, font).Width;
            if (w <= maxWidth) return text;

            string ellipsis = "…";
            double ellW = gfx.MeasureString(ellipsis, font).Width;
            if (ellW >= maxWidth) return "";

            int low = 0, high = text.Length;
            int best = 0;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                string candidate = text.Substring(0, mid) + ellipsis;
                if (gfx.MeasureString(candidate, font).Width <= maxWidth)
                {
                    best = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return text.Substring(0, best) + ellipsis;
        }
    }
}
