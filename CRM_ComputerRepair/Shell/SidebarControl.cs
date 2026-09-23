using CRM.winforms.Auth;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Role-aware navigation rail.
    ///
    /// UI notes
    /// --------
    /// • Brand lives here, at the top of the left column, so the app has one anchor point
    ///   instead of two competing ones (top bar now carries the page title).
    /// • One device marks the current page: a tinted pill with accent text. The extra left
    ///   accent bar was saying the same thing twice.
    /// • Collapsible to a 72px icon rail; labels become tooltips, so the choice costs the
    ///   user nothing in recognition.
    /// • Nav area scrolls independently — long role menus never push sign-out off-screen.
    /// </summary>
    [DesignerCategory("Code")]
    public class SidebarControl : Panel
    {
        // ═══════════ EVENTS ═══════════

        public event EventHandler<string>? MenuSelected;
        public event EventHandler? LogoutClicked;
        public event EventHandler? CollapsedChanged;

        // ═══════════ STATE ═══════════

        private readonly Dictionary<string, NavButton> _buttons = new();
        private readonly List<SectionLabel> _sections = new();
        private readonly ToolTip _tips = new ToolTip { InitialDelay = 400, ReshowDelay = 150 };

        private Panel _nav = null!;
        private NavButton _logout = null!;
        private IconButton _toggle = null!;

        private string _activeKey = "dashboard";
        private bool _collapsed;
        private int _navY;

        // ═══════════ PROPERTIES ═══════════

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string BrandName { get; set; } = "Fixory";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string UserName { get; set; } = "Alex Rivera";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string UserRole { get; set; } = "Staff";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string ActiveKey
        {
            get => _activeKey;
            set { _activeKey = value; UpdateActiveState(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public bool Collapsed
        {
            get => _collapsed;
            set
            {
                if (_collapsed == value) return;
                _collapsed = value;
                ApplyCollapsed();
                CollapsedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Optional count badge on a nav item, e.g. open follow-ups.</summary>
        public void SetBadge(string key, int count)
        {
            if (_buttons.TryGetValue(key, out var b)) { b.Badge = count; b.Invalidate(); }
        }

        // ═══════════ CONSTRUCTOR ═══════════

        public SidebarControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = UiKit.Surface;
            Width = UiKit.SidebarWidth;
            Dock = DockStyle.Left;

            BuildUi();
        }

        // ═══════════ UI BUILD ═══════════

        private void BuildUi()
        {
            _nav = new Panel
            {
                BackColor = UiKit.Surface,
                AutoScroll = true
            };
            Controls.Add(_nav);

            _toggle = new IconButton(IconFont.ChevronDown, 9F)
            {
                Size = new Size(28, 28),
                Rotate = 90          // chevron points left when expanded
            };
            _toggle.Click += (s, e) => Collapsed = !Collapsed;
            _tips.SetToolTip(_toggle, "Collapse menu");
            Controls.Add(_toggle);

            _navY = 0;

            switch (UserSession.Role)
            {
                case "Super Admin": BuildSuperAdminMenu(); break;
                case "Admin": BuildAdminMenu(); break;
                case "Manager": BuildManagerMenu(); break;
                case "Staff":
                default: BuildStaffMenu(); break;
            }

            _logout = new NavButton("logout", IconFont.SignOut, "Sign out") { IsDanger = true };
            _logout.Click += (s, e) => LogoutClicked?.Invoke(this, EventArgs.Empty);
            _tips.SetToolTip(_logout, "Sign out");
            Controls.Add(_logout);

            Resize += (s, e) => LayoutUi();
            LayoutUi();
            UpdateActiveState();
        }

        // ═══════════ ROLE MENUS ═══════════
        // Super Admin: manage admin accounts, monitor system, terms, subscriptions

        private void BuildSuperAdminMenu()
        {
            AddSection("Main");
            AddNav("dashboard", IconFont.Dashboard, "Dashboard");

            AddSection("Administration");
            AddNav("admin-accounts", IconFont.Profile, "Admin accounts");
            AddNav("system-monitor", IconFont.Dashboard, "System monitor");
            AddNav("subscriptions", IconFont.Reports, "Subscriptions");
            AddNav("terms", IconFont.Reports, "Terms & conditions");
        }

        // Admin: user accounts, loyalty programs, reports, terms

        private void BuildAdminMenu()
        {
            AddSection("Main");
            AddNav("dashboard", IconFont.Dashboard, "Dashboard");
            AddNav("reports", IconFont.Reports, "Reports");
            AddNav("retention", IconFont.Loyalty, "Retention");

            AddSection("Administration");
            AddNav("user-accounts", IconFont.Customers, "User accounts");
            AddNav("loyalty", IconFont.Loyalty, "Loyalty programs");
            AddNav("terms", IconFont.Reports, "Terms & conditions");
        }

        // Manager: reports, staff activity, repair approvals, loyalty

        private void BuildManagerMenu()
        {
            AddSection("Main");
            AddNav("dashboard", IconFont.Dashboard, "Dashboard");
            AddNav("reports", IconFont.Reports, "Reports");
            AddNav("retention", IconFont.Loyalty, "Retention");

            AddSection("Operations");
            AddNav("repairs", IconFont.Repairs, "Repair requests");
            AddNav("staff-activity", IconFont.Customers, "Staff activity");
            AddNav("loyalty", IconFont.Loyalty, "Loyalty programs");
        }

        // Staff: dashboard, customers, follow-ups, interactions, repairs, history

        private void BuildStaffMenu()
        {
            AddSection("Main");
            AddNav("dashboard", IconFont.Dashboard, "Dashboard");
            AddNav("customers", IconFont.Customers, "Customers");

            AddSection("Customer care");
            AddNav("follow-ups", IconFont.Dashboard, "Follow-ups");
            AddNav("interactions", IconFont.Customers, "Interactions");
            AddNav("repairs", IconFont.Repairs, "Repair requests");
            AddNav("customer-history", IconFont.Dashboard, "Customer history");
            AddNav("retention", IconFont.Loyalty, "Retention");
        }

        // ═══════════ ITEM FACTORIES ═══════════

        private void AddSection(string text)
        {
            if (_sections.Count > 0) _navY += UiKit.S3;

            var lbl = new SectionLabel(text) { Location = new Point(UiKit.S3, _navY) };
            _nav.Controls.Add(lbl);
            _sections.Add(lbl);

            _navY += 26;
        }

        private void AddNav(string key, string glyph, string title)
        {
            var btn = new NavButton(key, glyph, title) { Location = new Point(UiKit.S3, _navY) };
            btn.Click += (s, e) => HandleMenuClick(key);

            _nav.Controls.Add(btn);
            _buttons[key] = btn;
            _tips.SetToolTip(btn, title);

            _navY += UiKit.NavHeight + UiKit.NavGap;
        }

        // ═══════════ LAYOUT ═══════════

        private void ApplyCollapsed()
        {
            Width = _collapsed ? UiKit.SidebarRail : UiKit.SidebarWidth;

            foreach (var b in _buttons.Values) b.Compact = _collapsed;
            foreach (var s in _sections) s.Compact = _collapsed;
            _logout.Compact = _collapsed;
            _toggle.Rotate = _collapsed ? 270 : 90;
            _tips.SetToolTip(_toggle, _collapsed ? "Expand menu" : "Collapse menu");

            LayoutUi();
            Invalidate(true);
        }

        private void LayoutUi()
        {
            int pad = UiKit.S3;
            int itemW = Width - pad * 2;

            int brandBottom = 52;

            _toggle.Location = _collapsed
                ? new Point((Width - _toggle.Width) / 2, UiKit.S3)
                : new Point(Width - _toggle.Width - pad, UiKit.S3);

            int logoutH = UiKit.NavHeight;
            int logoutTop = Height - pad - logoutH;

            _logout.Location = new Point(pad, logoutTop);
            _logout.Size = new Size(itemW, logoutH);

            _nav.Location = new Point(0, brandBottom);
            _nav.Size = new Size(Width, Math.Max(0, logoutTop - UiKit.S3 - brandBottom));

            foreach (var b in _buttons.Values) b.Size = new Size(itemW, UiKit.NavHeight);
            foreach (var s in _sections) s.Size = new Size(itemW, 20);
        }

        // ═══════════ PAINT ═══════════

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UiKit.Quality(g);

            using (var b = new SolidBrush(UiKit.Surface))
                g.FillRectangle(b, ClientRectangle);

            // Right hairline separates the rail from the canvas — no shadow needed.
            UiKit.VLine(g, Width - 1, 0, Height, UiKit.Line);

            // Hairline above sign out
            UiKit.HLine(g, UiKit.S3, Width - UiKit.S3, _logout.Top - UiKit.S3, UiKit.LineSoft);
        }

        // ═══════════ ACTIONS ═══════════

        private void HandleMenuClick(string key)
        {
            ActiveKey = key;
            MenuSelected?.Invoke(this, key);
        }

        private void UpdateActiveState()
        {
            foreach (var kv in _buttons)
                kv.Value.IsActive = kv.Key == _activeKey;
        }

        public string TitleFor(string key) =>
            _buttons.TryGetValue(key, out var b) ? b.Title : key;

        // ═══════════════════════════════════════════════════════════════
        //  NESTED CONTROLS
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class SectionLabel : Control
        {
            private readonly string _text;
            private bool _compact;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public bool Compact
            {
                get => _compact;
                set { _compact = value; Invalidate(); }
            }

            public SectionLabel(string text)
            {
                _text = text;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = UiKit.Surface;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var b = new SolidBrush(UiKit.Surface))
                    g.FillRectangle(b, ClientRectangle);

                if (_compact)
                {
                    // On the rail a hairline carries the grouping instead of the words.
                    UiKit.HLine(g, 14, Math.Max(20, Width - 14), Height / 2, UiKit.Line);
                    return;
                }

                UiKit.Text(g, _text, UiKit.Micro, UiKit.InkFaint,
                    new Rectangle(UiKit.S3, 0, Width - UiKit.S3, Height), UiKit.Left);
            }
        }

        [DesignerCategory("Code")]
        private sealed class NavButton : Control
        {
            private bool _isActive, _hover, _down, _compact;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public string Key { get; }

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public string Title { get; }

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public string Glyph { get; }

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public bool IsDanger { get; set; }

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public int Badge { get; set; }

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public bool IsActive
            {
                get => _isActive;
                set { _isActive = value; Invalidate(); }
            }

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public bool Compact
            {
                get => _compact;
                set { _compact = value; Invalidate(); }
            }

            public NavButton(string key, string glyph, string title)
            {
                Key = key; Glyph = glyph; Title = title;

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
            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

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

                Color accent = IsDanger ? UiKit.Danger : UiKit.Accent;

                Color bg = Color.Empty;
                if (_isActive) bg = UiKit.Wash(accent);
                else if (_down) bg = UiKit.Line;
                else if (_hover) bg = UiKit.Hover;

                if (bg != Color.Empty)
                    UiKit.FillRounded(g, ClientRectangle, UiKit.RadiusSm, bg);

                if (Focused)
                    UiKit.StrokeRounded(g, ClientRectangle, UiKit.RadiusSm, UiKit.Mix(accent, Color.White, 0.4f));

                Color fg = _isActive ? accent
                         : IsDanger ? (_hover ? UiKit.Danger : UiKit.InkMuted)
                         : _hover ? UiKit.Ink : UiKit.InkMuted;

                var iconRect = _compact
                    ? new Rectangle(0, 0, Width, Height)
                    : new Rectangle(UiKit.S3, 0, 22, Height);

                using (var f = UiKit.GlyphFont(11F))
                    UiKit.Text(g, Glyph, f, fg, iconRect, UiKit.Center);

                if (!_compact)
                {
                    var font = _isActive ? UiKit.BodyStrong : UiKit.Body;
                    int right = Badge > 0 ? 44 : UiKit.S3;
                    UiKit.Text(g, Title, font, fg,
                        new Rectangle(44, 0, Width - 44 - right, Height), UiKit.Left);
                }

                if (Badge > 0)
                {
                    string txt = Badge > 99 ? "99+" : Badge.ToString();

                    if (_compact)
                    {
                        UiKit.Dot(g, Width - 14, 13, 7, accent);
                    }
                    else
                    {
                        var size = UiKit.Measure(txt, UiKit.Micro);
                        var pill = new Rectangle(Width - UiKit.S3 - Math.Max(20, size.Width + 12),
                                                 (Height - 18) / 2,
                                                 Math.Max(20, size.Width + 12), 18);
                        UiKit.FillRounded(g, pill, 9, _isActive ? accent : UiKit.Wash(accent));
                        UiKit.Text(g, txt, UiKit.SmallStrong, _isActive ? Color.White : accent, pill, UiKit.Center);
                    }
                }
            }
        }

        /// <summary>Small square glyph button (collapse toggle).</summary>
        [DesignerCategory("Code")]
        private sealed class IconButton : Control
        {
            private readonly string _glyph;
            private readonly float _size;
            private bool _hover, _down;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public int Rotate { get; set; }

            public IconButton(string glyph, float size)
            {
                _glyph = glyph; _size = size;

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

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var b = new SolidBrush(UiKit.Surface))
                    g.FillRectangle(b, ClientRectangle);

                if (_down) UiKit.FillRounded(g, ClientRectangle, 6, UiKit.Line);
                else if (_hover) UiKit.FillRounded(g, ClientRectangle, 6, UiKit.Hover);

                var state = g.Save();
                g.TranslateTransform(Width / 2f, Height / 2f);
                g.RotateTransform(Rotate);
                g.TranslateTransform(-Width / 2f, -Height / 2f);

                using (var f = UiKit.GlyphFont(_size))
                using (var brush = new SolidBrush(_hover ? UiKit.Ink : UiKit.InkFaint))
                using (var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                })
                {
                    g.DrawString(_glyph, f, brush, new RectangleF(0, 0, Width, Height), sf);
                }

                g.Restore(state);
            }
        }
    }
}