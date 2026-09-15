using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Simple "no notifications" dropdown that appears below the bell.
    /// </summary>
    [DesignerCategory("Code")]
    public class BellMenuControl : Panel
    {
        public BellMenuControl()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Size = new Size(300, 120);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Soft shadow
            for (int i = 0; i < 5; i++)
            {
                var shadow = new Rectangle(rect.X - i, rect.Y - i + 2,
                    rect.Width + i * 2, rect.Height + i * 2);
                using var sp = GetRoundedPath(shadow, 10 + i);
                using var sb = new SolidBrush(Color.FromArgb(10 + i * 4, 0, 0, 0));
                g.FillPath(sb, sp);
            }

            // Body
            using (var path = GetRoundedPath(rect, 10))
            using (var brush = new SolidBrush(Color.White))
                g.FillPath(brush, path);

            using (var path = GetRoundedPath(rect, 10))
            using (var pen = new Pen(Color.FromArgb(228, 231, 236), 1))
                g.DrawPath(pen, path);

            // Bell icon
            using (var iconFont = IconFont.Create(18F))
            using (var brush = new SolidBrush(AppTheme.TextMuted))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(IconFont.Bell, iconFont, brush,
                    new RectangleF(0, 16, Width, 32), sf);
            }

            // Message
            using (var brush = new SolidBrush(AppTheme.TextSecondary))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString("No new notifications", AppTheme.FontBody, brush,
                    new RectangleF(0, 54, Width, 24), sf);
            }

            using (var brush = new SolidBrush(AppTheme.TextMuted))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString("You're all caught up.", AppTheme.FontLabel, brush,
                    new RectangleF(0, 78, Width, 20), sf);
            }
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