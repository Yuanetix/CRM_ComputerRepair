using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using CRM.winforms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// SaaS empty state — icon in a soft circle, title, explanation, one CTA.
    /// HCI: empty screens must teach (H10/help) not just report "no data".
    /// Replaces bare "Coming Soon" placeholders and ad-hoc grid empty labels.
    /// </summary>
    [DesignerCategory("Code")]
    public class SaasEmptyState : Control
    {
        private string _icon = "\uE897";
        private string _title = "Nothing here yet";
        private string _subtitle = "";
        private string _actionText = "";

        public event EventHandler? ActionClicked;

        public SaasEmptyState()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = AppTheme.Surface;
            TabStop = false;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Icon { get => _icon; set { _icon = value; Invalidate(); } }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public new string Text { get => _title; set { _title = value; Invalidate(); } }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Subtitle { get => _subtitle; set { _subtitle = value; Invalidate(); } }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string ActionText { get => _actionText; set { _actionText = value; Invalidate(); } }

        private SaasButton? _btn;

        public void SetAction(string text, EventHandler handler)
        {
            ActionText = text;
            ActionClicked += handler;
            EnsureButton();
            LayoutButton();
        }

        private void EnsureButton()
        {
            if (_btn != null || string.IsNullOrEmpty(_actionText)) return;
            _btn = new SaasButton(_actionText, SaasButtonVariant.Primary);
            _btn.Click += (s, e) => ActionClicked?.Invoke(this, EventArgs.Empty);
            Controls.Add(_btn);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutButton(); }

        private void LayoutButton()
        {
            if (_btn == null) return;
            if (Width <= 0 || Height <= 0) return;
            _btn.Text = _actionText;
            int w = Math.Max(140, _btn.PreferredWidth);
            w = Math.Min(w, Math.Max(40, Width - 16));
            _btn.Size = new Size(w, 36);
            _btn.Location = new Point(Math.Max(8, (Width - w) / 2), Height / 2 + 52);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 60 || Height < 80) return;
            var g = e.Graphics;
            using (var b = new SolidBrush(BackColor))
                g.FillRectangle(b, ClientRectangle);
            UiKit.Quality(g);
            int cx = Width / 2, cy = Height / 2 - 30;

            var circle = new Rectangle(cx - 30, cy - 30, 60, 60);
            if (circle.Width > 0 && circle.Height > 0)
            {
                UiKit.FillRounded(g, circle, 30, UiKit.Wash(UiKit.Accent));
                using (var f = UiKit.GlyphFont(20F))
                    UiKit.Text(g, _icon, f, UiKit.Accent, circle, UiKit.Center);
            }

            int titleW = Math.Max(10, Width - 48);
            UiKit.Text(g, _title, UiKit.T.Section, UiKit.T.Ink,
                new Rectangle(24, cy + 40, titleW, 28),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (!string.IsNullOrEmpty(_subtitle))
            {
                int subW = Math.Max(10, Width - 96);
                UiKit.Text(g, _subtitle, UiKit.T.Small, UiKit.T.InkMuted,
                    new Rectangle(48, cy + 66, subW, 44),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
            }
        }
    }
}
