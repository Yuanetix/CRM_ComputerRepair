using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Top bar — page title on the left, notifications and the user chip on the right.
    ///
    /// UI notes
    /// --------
    /// • The brand moved to the sidebar, so this bar answers the one question it should:
    ///   "where am I?" Title plus optional context line, left aligned to the content.
    /// • Bell carries a dot for unread and a count pill past one, so the state is readable
    ///   without opening it.
    /// • Same neutrals, radii and type scale as the sidebar and the page content.
    /// </summary>
    [DesignerCategory("Code")]
    public class TopBarControl : Panel
    {
        // ═══════════ EVENTS ═══════════

        public event EventHandler? ProfileClicked;
        public event EventHandler? BellClicked;

        // ═══════════ CONTROLS ═══════════

        private BellButton _bell = null!;
        private UserChip _chip = null!;
        private readonly ToolTip _tips = new ToolTip { InitialDelay = 400, ReshowDelay = 150 };

        // ═══════════ PROPERTIES ═══════════

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string BrandName { get; set; } = "Fixory";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string UserName { get; private set; } = "Alex Rivera";

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

        // ═══════════ CONSTRUCTOR ═══════════

        public TopBarControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = UiKit.Surface;
            Height = UiKit.TopBarHeight;
            Dock = DockStyle.Top;
            Padding = new Padding(0);

            BuildUi();
            LayoutUi();
        }

        // ═══════════ UI BUILD ═══════════

        private void BuildUi()
        {
            _bell = new BellButton { Size = new Size(36, 36) };
            _bell.Click += (s, e) => BellClicked?.Invoke(this, EventArgs.Empty);
            _tips.SetToolTip(_bell, "Notifications");

            _chip = new UserChip { Size = new Size(252, 56) };
            _chip.Click += (s, e) => ProfileClicked?.Invoke(this, EventArgs.Empty);
            _tips.SetToolTip(_chip, "Account");

            Controls.Add(_bell);
            Controls.Add(_chip);

            Resize += (s, e) => LayoutUi();
        }

        // ═══════════ LAYOUT ═══════════

        private void LayoutUi()
        {
            _chip.Location = new Point(Width - _chip.Width - UiKit.S5, (Height - _chip.Height) / 2);
            _bell.Location = new Point(_chip.Left - _bell.Width - UiKit.S5, (Height - _bell.Height) / 2);
        }

        // ═══════════ PAINT ═══════════

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UiKit.Quality(g);

            using (var b = new SolidBrush(UiKit.Surface))
                g.FillRectangle(b, ClientRectangle);

            UiKit.HLine(g, 0, Width, Height - 1, UiKit.Line);

            // Brand wordmark, left aligned with a clear margin from the edge.
            UiKit.Text(g, BrandName, UiKit.Brand, UiKit.Ink,
                new Rectangle(UiKit.S6, 0, 200, Height - 1), UiKit.Left);

            // Hairline between the utilities and the user chip.
            UiKit.VLine(g, _chip.Left - UiKit.S3, 18, Height - 19, UiKit.Line);
        }

        // ═══════════ PUBLIC METHODS ═══════════

        public void SetUser(string fullName, string role)
        {
            UserName = fullName;
            UserRole = role;
            _chip.Invalidate();
            Invalidate();
        }

        // ═══════════════════════════════════════════════════════════════
        //  NESTED CONTROLS
        // ═══════════════════════════════════════════════════════════════

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
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode is Keys.Enter or Keys.Space)
                    InvokeOnClick(this, EventArgs.Empty);
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

                if (Focused)
                    UiKit.StrokeRounded(g, ClientRectangle, UiKit.RadiusSm, UiKit.Mix(UiKit.Accent, Color.White, 0.4));

                using (var f = UiKit.GlyphFont(11F))
                    UiKit.Text(g, IconFont.Bell, f, _hover ? UiKit.Ink : UiKit.InkMuted,
                        ClientRectangle, UiKit.Center);

                if (Count <= 0) return;

                if (Count == 1)
                {
                    UiKit.Dot(g, Width - 11, 11, 8, UiKit.Danger);
                    UiKit.Dot(g, Width - 11, 11, 4, UiKit.Danger);
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
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode is Keys.Enter or Keys.Space)
                    InvokeOnClick(this, EventArgs.Empty);
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

                if (Focused)
                    UiKit.StrokeRounded(g, ClientRectangle, UiKit.RadiusSm, UiKit.Mix(UiKit.Accent, Color.White, 0.4));

                var bar = Parent as TopBarControl;
                string name = bar?.UserName ?? "User";
                string role = bar?.UserRole ?? "";

                var avatar = new Rectangle(UiKit.S3, (Height - 36) / 2, 36, 36);
                UiKit.Initials(g, avatar, name, UiKit.Accent, UiKit.SmallStrong);

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