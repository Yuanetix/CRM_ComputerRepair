using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Clean single-line input — plain border, focus ring, error state.
    /// No icon, full-width text like a modern web form field.
    /// </summary>
    [DesignerCategory("Code")]
    public class TextField : Panel
    {
        private TextBox _inner = null!;
        private bool _isFocused;
        private bool _hasError;

        public TextField()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.Surface;
            Height = 38;
            Padding = new Padding(12, 0, 12, 0);

            _inner = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = AppTheme.TextPrimary,
                BackColor = AppTheme.Surface
            };

            _inner.GotFocus += (s, e) => { _isFocused = true; Invalidate(); };
            _inner.LostFocus += (s, e) => { _isFocused = false; Invalidate(); };
            _inner.TextChanged += (s, e) => OnTextChanged(EventArgs.Empty);

            Controls.Add(_inner);
            Resize += (s, e) => LayoutInner();
            LayoutInner();
        }

        // ═══════════ PROPERTIES ═══════════

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public override string Text
        {
            get => _inner.Text;
            set => _inner.Text = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string PlaceholderText
        {
            get => _inner.PlaceholderText;
            set => _inner.PlaceholderText = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public bool HasError
        {
            get => _hasError;
            set { _hasError = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public TextBox InnerTextBox => _inner;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public bool Multiline
        {
            get => _inner.Multiline;
            set
            {
                _inner.Multiline = value;
                if (value) _inner.ScrollBars = ScrollBars.Vertical;
                LayoutInner();
            }
        }

        public new void Focus() => _inner.Focus();

        // ═══════════ LAYOUT ═══════════

        private void LayoutInner()
        {
            if (_inner == null || Height <= 0) return;

            int textX = Padding.Left;
            int textW = Width - Padding.Left - Padding.Right;
            if (textW < 20) textW = 20;

            int textY;
            int textH;

            if (_inner.Multiline)
            {
                textY = Padding.Top > 0 ? Padding.Top : 8;
                textH = Height - textY - 8;
                if (textH < 20) textH = 20;
            }
            else
            {
                textH = _inner.PreferredHeight;
                textY = (Height - textH) / 2;
            }

            _inner.Location = new Point(textX, textY);
            _inner.Size = new Size(textW, textH);
        }

        // ═══════════ PAINT ═══════════

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            Color bg = _hasError ? AppTheme.DangerSoft : AppTheme.Surface;

            using (var path = GetRoundedPath(rect, 8))
            using (var brush = new SolidBrush(bg))
                g.FillPath(brush, path);

            Color borderColor =
                _hasError ? AppTheme.Danger :
                _isFocused ? AppTheme.Primary :
                Color.FromArgb(218, 220, 228);

            using (var path = GetRoundedPath(rect, 8))
            using (var pen = new Pen(borderColor, _isFocused ? 1.6f : 1f))
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