using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Simple SaaS top bar — notifications + user chip on the right.
    /// No search, no page title header.
    /// </summary>
    [DesignerCategory("Code")]
    public class TopBarControl : Panel
    {
        public event EventHandler? ProfileClicked;
        public event EventHandler? BellClicked;
        public event EventHandler? BrandClicked;

        private BellButton _bell = null!;
        private UserChip _chip = null!;
        private readonly ToolTip _tips = new ToolTip { InitialDelay = 400, ReshowDelay = 150 };
        private Rectangle _brandBounds = Rectangle.Empty;
        private bool _brandHover;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string BrandName { get; set; } = "Fixory";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string UserName { get; private set; } = "Admin";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string UserRole { get; private set; } = "Administrator";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public int NotificationCount
        {
            get => _bell.Count;
            set { _bell.Count = value; _bell.Invalidate(); }
        }

        public TopBarControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = UiKit.Surface;
            Height = UiKit.TopBarHeight;
            Dock = DockStyle.Top;

            BuildUi();
            LayoutUi();
        }

        private void BuildUi()
        {
            _bell = new BellButton { Size = new Size(36, 36) };
            _bell.Click += (s, e) => BellClicked?.Invoke(this, EventArgs.Empty);
            _tips.SetToolTip(_bell, "Notifications");

            _chip = new UserChip { Size = new Size(210, 56) };
            _chip.Click += (s, e) => ProfileClicked?.Invoke(this, EventArgs.Empty);
            _tips.SetToolTip(_chip, "Account Profile");

            Controls.Add(_bell);
            Controls.Add(_chip);
            Resize += (s, e) => LayoutUi();
        }

        private void LayoutUi()
        {
            _chip.Location = new Point(Width - _chip.Width - UiKit.S5, (Height - _chip.Height) / 2);
            _bell.Location = new Point(_chip.Left - _bell.Width - UiKit.S4, (Height - _bell.Height) / 2);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hover = _brandBounds.Contains(e.Location);
            if (hover != _brandHover)
            {
                _brandHover = hover;
                Cursor = hover ? Cursors.Hand : Cursors.Default;
                Invalidate(_brandBounds);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_brandHover)
            {
                _brandHover = false;
                Cursor = Cursors.Default;
                Invalidate(_brandBounds);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _brandBounds.Contains(e.Location))
            {
                BrandClicked?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UiKit.Quality(g);
            using (var b = new SolidBrush(UiKit.Surface))
                g.FillRectangle(b, ClientRectangle);
            UiKit.HLine(g, 0, Width, Height - 1, UiKit.Line);

            // ── Left Brand: Fixory name only horizontally ──
            int brandX = UiKit.S6; // 24px
            Color blueColor = AppTheme.Primary; // #2563EB Fixory Royal Blue

            using (var brandFont = AppFonts.Strong(18F))
            {
                var size = TextRenderer.MeasureText(BrandName, brandFont);
                var textRect = new Rectangle(brandX, (Height - size.Height) / 2, size.Width + 20, size.Height);
                TextRenderer.DrawText(g, BrandName, brandFont, textRect, blueColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                _brandBounds = textRect;
            }

            // Divider before User Chip
            UiKit.VLine(g, _chip.Left - UiKit.S3, 18, Height - 19, UiKit.Line);
        }

        public void SetPage(string title, string subtitle = "")
        {
            AccessibleName = title;
        }

        public static string CleanUserDisplayName(string? fullName, string? role)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return !string.IsNullOrWhiteSpace(role) ? role : "User";

            string trimmed = fullName.Trim();
            if (trimmed.Equals("Admin User", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Administrator User", StringComparison.OrdinalIgnoreCase))
            {
                return "Admin";
            }
            if (trimmed.Equals("Super Admin User", StringComparison.OrdinalIgnoreCase))
            {
                return "Super Admin";
            }
            if (trimmed.Equals("Manager User", StringComparison.OrdinalIgnoreCase))
            {
                return "Manager";
            }
            if (trimmed.Equals("Staff User", StringComparison.OrdinalIgnoreCase))
            {
                return "Staff";
            }
            return trimmed;
        }

        public void SetUser(string fullName, string role, int companyId = 1)
        {
            UserName = CleanUserDisplayName(fullName, role);
            // Company #1 removed as requested
            UserRole = role ?? "";
            _chip.Invalidate();
            Invalidate();
        }

        [DesignerCategory("Code")]
        private sealed class BellButton : Control
        {
            private bool _hover, _down;
            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public int Count { get; set; }

            public BellButton()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = UiKit.Surface;
                Cursor = Cursors.Hand;
                TabStop = true;
                AccessibleName = "Notifications";
            }
            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode is Keys.Enter or Keys.Space) InvokeOnClick(this, EventArgs.Empty);
                base.OnKeyDown(e);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);
                using (var b = new SolidBrush(UiKit.Surface))
                    g.FillRectangle(b, ClientRectangle);
                if (_down) UiKit.FillRounded(g, ClientRectangle, UiKit.RadiusSm, UiKit.Line);
                else if (_hover) UiKit.FillRounded(g, ClientRectangle, UiKit.RadiusSm, UiKit.Hover);
                if (Focused) UiKit.FocusRing(g, ClientRectangle, UiKit.RadiusSm);
                using (var f = UiKit.GlyphFont(11F))
                    UiKit.Text(g, IconFont.Bell, f, _hover ? UiKit.Ink : UiKit.InkMuted, ClientRectangle, UiKit.Center);
                if (Count <= 0) return;
                if (Count == 1)
                {
                    UiKit.Dot(g, Width - 11, 11, 8, UiKit.Danger);
                }
                else
                {
                    string txt = Count > 99 ? "99+" : Count.ToString();
                    var size = UiKit.Measure(txt, UiKit.Micro);
                    int w = Math.Max(16, size.Width + 8);
                    var pill = new Rectangle(Width - w - 1, 1, w, 16);
                    UiKit.FillRounded(g, pill, 8, UiKit.Danger);
                    UiKit.Text(g, txt, UiKit.Micro, Color.White, pill, UiKit.Center);
                }
            }
        }

        [DesignerCategory("Code")]
        private sealed class UserChip : Control
        {
            private bool _hover, _down;
            public UserChip()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = UiKit.Surface;
                Cursor = Cursors.Hand;
                TabStop = true;
                AccessibleName = "Account";
            }
            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode is Keys.Enter or Keys.Space) InvokeOnClick(this, EventArgs.Empty);
                base.OnKeyDown(e);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);
                using (var b = new SolidBrush(UiKit.Surface))
                    g.FillRectangle(b, ClientRectangle);
                if (_down) UiKit.FillRounded(g, ClientRectangle, UiKit.RadiusSm, UiKit.Line);
                else if (_hover) UiKit.FillRounded(g, ClientRectangle, UiKit.RadiusSm, UiKit.Hover);
                if (Focused) UiKit.FocusRing(g, ClientRectangle, UiKit.RadiusSm);

                var bar = Parent as TopBarControl;
                string name = bar?.UserName ?? "User";
                string role = bar?.UserRole ?? "";

                var avatar = new Rectangle(UiKit.S3, (Height - 36) / 2, 36, 36);
                UiKit.Initials(g, avatar, name, UiKit.Accent, UiKit.SmallStrong);
                // Presence dot — SaaS "who's online" signal.
                UiKit.Dot(g, avatar.Right - 4, avatar.Bottom - 4, 10, AppTheme.Success);
                using (var p = new Pen(Color.White, 2f))
                    g.DrawEllipse(p, avatar.Right - 6, avatar.Bottom - 6, 10, 10);

                int textX = avatar.Right + UiKit.S4;
                int textW = Width - textX - UiKit.S5;
                UiKit.Text(g, name, UiKit.BodyStrong, UiKit.Ink,
                    new Rectangle(textX, Height / 2 - 21, textW, 20), UiKit.Left);
                UiKit.Text(g, role, UiKit.Small, UiKit.InkMuted,
                    new Rectangle(textX, Height / 2 + 1, textW, 20), UiKit.Left);
                using (var f = UiKit.GlyphFont(8F))
                    UiKit.Text(g, IconFont.ChevronDown, f, UiKit.InkFaint,
                        new Rectangle(Width - 28, 0, 24, Height), UiKit.Center);
            }
        }
    }
}
