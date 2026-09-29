using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Stat tile — modern balanced SaaS KPI card with icon chip, uppercase label, and bold value.
    /// </summary>
    [DesignerCategory("Code")]
    public class StatTile : UserControl
    {
        private Color _iconFg = Color.Empty;
        private string _icon = "";
        private string _number = "0";
        private string _label = "";
        private string _delta = "";

        public StatTile()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.Surface;
            Size = new Size(220, 92);
        }

        public StatTile(string label, string number, string icon, string delta = "") : this()
        {
            _label = label;
            _number = number;
            _icon = icon;
            _delta = delta;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Value
        {
            get => _number;
            set { _number = value; Invalidate(); }
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
            UiKit.Quality(g);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Card background + clean subtle border
            using (var path = GetRoundedPath(rect, 10))
            {
                using (var brush = new SolidBrush(BackColor != Color.Empty ? BackColor : AppTheme.Surface))
                    g.FillPath(brush, path);

                using (var pen = new Pen(UiKit.T.LineSoft, 1))
                    g.DrawPath(pen, path);
            }

            int pad = 16;
            int iconSize = 42;
            int iconY = Math.Max(8, (Height - iconSize) / 2);
            var iconRect = new Rectangle(pad, iconY, iconSize, iconSize);

            Color accent = !_iconFg.IsEmpty && _iconFg != Color.Transparent ? _iconFg : AppTheme.Primary;

            // Icon chip with tinted background
            using (var path = GetRoundedPath(iconRect, 10))
            using (var brush = new SolidBrush(UiKit.Wash(accent)))
                g.FillPath(brush, path);

            using (var iconFont = IconFont.Create(16F))
            using (var iconBrush = new SolidBrush(accent))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(_icon, iconFont, iconBrush, iconRect, sf);
            }

            // Text block (Label + Big Number) to the right of icon
            int textX = iconRect.Right + 14;
            int textW = Math.Max(20, Width - textX - pad);

            var lblFont = UiKit.MicroStrong;
            var numFont = AppTheme.FontStatNumber;

            int contentH = lblFont.Height + numFont.Height + 2;
            int textY = Math.Max(6, (Height - contentH) / 2);

            // Label (uppercase, clean muted)
            using (var lblBrush = new SolidBrush(AppTheme.TextSecondary))
            {
                var lblRect = new RectangleF(textX, textY, textW, lblFont.Height + 2);
                g.DrawString(_label.ToUpperInvariant(), lblFont, lblBrush, lblRect);
            }

            // Big Number (bold, primary dark)
            using (var numBrush = new SolidBrush(AppTheme.TextPrimary))
            {
                var numRect = new RectangleF(textX - 1, textY + lblFont.Height + 2, textW, numFont.Height + 4);
                g.DrawString(_number, numFont, numBrush, numRect);
            }

            // Delta badge (if short, e.g. "+12%")
            if (!string.IsNullOrEmpty(_delta) && _delta.Length <= 8)
            {
                using var deltaBrush = new SolidBrush(accent);
                var deltaSize = g.MeasureString(_delta, AppTheme.FontStatDelta);
                g.DrawString(_delta, AppTheme.FontStatDelta, deltaBrush,
                    new PointF(Width - deltaSize.Width - pad, pad));
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