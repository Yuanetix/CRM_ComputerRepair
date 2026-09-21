using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Input with an icon prefix (uses Segoe Fluent Icons).
    /// Rounded border, focus state, error state.
    /// </summary>
    [DesignerCategory("Code")]
    public class IconInput : Panel
    {
        private Label lblIcon = null!;
        private TextBox _inner = null!;

        private bool _isFocused;
        private bool _hasError;

        public IconInput()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.Surface;
            Height = AppTheme.InputHeight;

            lblIcon = new Label
            {
                Text = "",
                Font = IconFont.Create(11F),
                ForeColor = AppTheme.TextMuted,
                AutoSize = false,
                Width = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            _inner = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = AppTheme.FontInput,
                ForeColor = AppTheme.TextPrimary,
                BackColor = AppTheme.Surface
            };

            _inner.GotFocus += (s, e) => { _isFocused = true; Invalidate(); };
            _inner.LostFocus += (s, e) => { _isFocused = false; Invalidate(); };
            _inner.TextChanged += (s, e) => OnTextChanged(EventArgs.Empty);

            Controls.Add(lblIcon);
            Controls.Add(_inner);

            Resize += (s, e) => LayoutInner();
            LayoutInner();
        }

        // ═══════════ PROPERTIES ═══════════

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Icon
        {
            get => lblIcon.Text;
            set { lblIcon.Text = value; Invalidate(); }
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
        public override string Text
        {
            get => _inner.Text;
            set => _inner.Text = value;
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

        public new void Focus() => _inner.Focus();

        // ═══════════ LAYOUT ═══════════

        private void LayoutInner()
        {
            if (lblIcon == null || _inner == null) return;
            if (Height <= 0) return;

            lblIcon.Height = Height;
            lblIcon.Location = new Point(10, 0);

            int textX = 10 + lblIcon.Width + 4;
            int textW = Width - textX - 12;
            if (textW < 20) textW = 20;

            int textY = (Height - _inner.PreferredHeight) / 2;

            _inner.Location = new Point(textX, textY);
            _inner.Width = textW;
        }

        // ═══════════ PAINT ═══════════

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            Color bg = _hasError ? AppTheme.DangerSoft : AppTheme.Surface;

            using (var path = GetRoundedPath(rect, AppTheme.ButtonRadius))
            using (var brush = new SolidBrush(bg))
                g.FillPath(brush, path);

            Color borderColor =
                _hasError ? AppTheme.Danger :
                _isFocused ? AppTheme.Primary :
                AppTheme.BorderStrong;

            using (var path = GetRoundedPath(rect, AppTheme.ButtonRadius))
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