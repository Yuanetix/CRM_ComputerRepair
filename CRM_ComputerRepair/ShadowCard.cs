using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Flat white card with a 1px hairline. No shadow, no layered paths —
    /// matches the SurfaceCard used in every list module now.
    /// </summary>
    [DesignerCategory("Code")]
    public class ShadowCard : Panel
    {
        public ShadowCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            Padding = new Padding(AppTheme.CardPadding);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Erase to the page background first.
            using (var bg = new SolidBrush(AppTheme.Background))
                g.FillRectangle(bg, ClientRectangle);

            // Flat card with a hairline.
            var body = new Rectangle(0, 0, Width - 1, Height - 1);

            using (var path = GetRoundedPath(body, AppTheme.Radius))
            using (var brush = new SolidBrush(AppTheme.Surface))
                g.FillPath(brush, path);

            using (var path = GetRoundedPath(body, AppTheme.Radius))
            using (var pen = new Pen(AppTheme.Border, 1f))
                g.DrawPath(pen, path);
        }

        private static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;
            if (d <= 0) { path.AddRectangle(rect); return path; }

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }
    }
}