using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// White card with a visible hairline border and a soft shadow.
    /// </summary>
    [DesignerCategory("Code")]
    public class ShadowCard : Panel
    {
        private const int ShadowPad = 4;

        public ShadowCard()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.Surface;
            Padding = new Padding(AppTheme.CardPadding);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var body = new Rectangle(
                ShadowPad,
                ShadowPad,
                Width - ShadowPad * 2 - 1,
                Height - ShadowPad * 2 - 1);

            // Soft outer shadow
            for (int i = 0; i < ShadowPad; i++)
            {
                var shadowRect = new Rectangle(
                    body.X - i,
                    body.Y - i + 2,
                    body.Width + i * 2,
                    body.Height + i * 2);

                using var path = GetRoundedPath(shadowRect, AppTheme.Radius + i);
                using var brush = new SolidBrush(Color.FromArgb(12 + i * 4, 15, 23, 42));
                g.FillPath(brush, path);
            }

            // White card body
            using (var path = GetRoundedPath(body, AppTheme.Radius))
            using (var brush = new SolidBrush(AppTheme.Surface))
                g.FillPath(brush, path);

            // Visible border
            using (var path = GetRoundedPath(body, AppTheme.Radius))
            using (var pen = new Pen(Color.FromArgb(215, 219, 226), 1.2f))
                g.DrawPath(pen, path);
        }

        private static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }
    }
}