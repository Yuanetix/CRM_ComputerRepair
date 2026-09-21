using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Stat tile — icon chip at top-left, label, then big number below.
    /// </summary>
    [DesignerCategory("Code")]
    public class StatTile : UserControl
    {
        private Color _iconFg;
        private string _icon = "";
        private string _number = "0";
        private string _label = "";
        private string _delta = "";

        public StatTile()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.Surface;
            Size = new Size(220, 150);   // ← taller default
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Icon
        {
            get => _icon;
            set { _icon = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Number
        {
            get => _number;
            set { _number = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Label
        {
            get => _label;
            set { _label = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Delta
        {
            get => _delta;
            set { _delta = value; Invalidate(); }
        }

        public void SetColors(Color background, Color iconForeground)
        {
            BackColor = background;
            _iconFg = iconForeground;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            using (var path = GetRoundedPath(rect, AppTheme.Radius))
            using (var brush = new SolidBrush(BackColor))
                g.FillPath(brush, path);

            // ── Layout dimensions ──
            int pad = 20;
            int iconSize = 40;

            // Icon chip
            var iconRect = new Rectangle(pad, pad, iconSize, iconSize);

            using (var path = GetRoundedPath(iconRect, 10))
            using (var brush = new SolidBrush(_iconFg))
                g.FillPath(brush, path);

            using (var iconFont = IconFont.Create(14F))
            using (var iconBrush = new SolidBrush(Color.White))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(_icon, iconFont, iconBrush, iconRect, sf);
            }

            // Label
            int labelY = pad + iconSize + 14;

            using (var lblBrush = new SolidBrush(AppTheme.TextSecondary))
            {
                g.DrawString(_label, AppTheme.FontStatLabel, lblBrush,
                    new PointF(pad, labelY));
            }

            // Number
            int numberY = labelY + 22;

            using (var numBrush = new SolidBrush(AppTheme.TextPrimary))
            {
                g.DrawString(_number, AppTheme.FontStatNumber, numBrush,
                    new PointF(pad - 2, numberY));
            }

            // Delta (top-right)
            if (!string.IsNullOrEmpty(_delta))
            {
                using var deltaBrush = new SolidBrush(_iconFg);
                var deltaSize = g.MeasureString(_delta, AppTheme.FontStatDelta);
                g.DrawString(_delta, AppTheme.FontStatDelta, deltaBrush,
                    new PointF(Width - deltaSize.Width - pad, pad + 12));
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