using CRM.winforms.Auth;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Simple navigation rail — role-aware sections, one active pill,
    /// collapsible to an icon rail. No brand header.
    /// </summary>
    [DesignerCategory("Code")]
    public class SidebarControl : Panel
    {
        public event EventHandler<string>? MenuSelected;
        public event EventHandler? LogoutClicked;
        public event EventHandler? CollapsedChanged;

        private readonly Dictionary<string, NavButton> _buttons = new();
        private readonly List<SectionLabel> _sections = new();
        private readonly ToolTip _tips = new ToolTip { InitialDelay = 400, ReshowDelay = 150 };

        private Panel _nav = null!;
        private NavButton _logout = null!;
        private IconButton _toggle = null!;

        private string _activeKey = "dashboard";
        private bool _collapsed;
        private int _navY;
        private string _builtRole = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string BrandName { get; set; } = "Fixory";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string UserName { get; set; } = "Alex Rivera";

        private string _userRole = "Staff";
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string UserRole
        {
            get => _userRole;
            set
            {
                if (_userRole == value) return;
                _userRole = value;
                RefreshForRole(value);
            }
        }

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

        public void SetBadge(string key, int count)
        {
            if (_buttons.TryGetValue(key, out var b)) { b.Badge = count; b.Invalidate(); }
        }

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

        // ── Distinct glyph per destination (H6: recognition, not recall) ──
        public static string IconFor(string key) => key switch
        {
            "dashboard" => IconFont.Dashboard,
            "reports" => IconFont.Reports,
            "retention" => IconFont.Loyalty,
            "customers" => IconFont.Customers,
            "follow-ups" => "\uE787",
            "interactions" => "\uE8BD",
            "repairs" => IconFont.Repairs,
            "customer-history" => "\uE81C",
            "staff-activity" => "\uE7EE",
            "loyalty" => "\uE734",
            "subscriptions" => "\uE9D5",
            "companies" => IconFont.Customers,
            "terms" => "\uE897",
            "user-accounts" => IconFont.Profile,
            "admin-accounts" => "\uE716",
            "system-monitor" => "\uE7BA",
            _ => IconFont.Dashboard
        };

        private void BuildUi()
        {
            _nav = new Panel { BackColor = UiKit.Surface, AutoScroll = true };
            Controls.Add(_nav);

            _toggle = new IconButton(IconFont.ChevronDown, 9F) { Size = new Size(28, 28), Rotate = 90 };
            _toggle.Click += (s, e) => Collapsed = !Collapsed;
            _tips.SetToolTip(_toggle, "Collapse menu");
            Controls.Add(_toggle);

            _navY = 0;

            _logout = new NavButton("logout", IconFont.SignOut, "Sign out") { IsDanger = true };
            _logout.Click += (s, e) => LogoutClicked?.Invoke(this, EventArgs.Empty);
            _tips.SetToolTip(_logout, "Sign out");
            Controls.Add(_logout);

            RefreshForRole(UserSession.Role ?? _userRole);

            Resize += (s, e) => LayoutUi();
            LayoutUi();
            UpdateActiveState();
        }

        /// <summary>
        /// Rebuilds the menu for the signed-in role. Safe to call repeatedly —
        /// no-op when the role hasn't changed since the last build.
        /// </summary>
        public void RefreshForRole(string? role)
        {
            role ??= "Staff";
            if (role == _builtRole && _buttons.Count > 0) return;
            _builtRole = role;
            _userRole = role;

            _nav.Controls.Clear();
            _buttons.Clear();
            _sections.Clear();
            _navY = 0;

            switch (role)
            {
                case "Super Admin": BuildSuperAdminMenu(); break;
                case "Admin": BuildAdminMenu(); break;
                case "Manager": BuildManagerMenu(); break;
                default: BuildStaffMenu(); break;
            }

            foreach (var kv in _buttons) _tips.SetToolTip(kv.Value, kv.Value.Title);
            LayoutUi();
            UpdateActiveState();
        }

        private void BuildSuperAdminMenu()
        {
            AddSection("Main");
            AddNav("dashboard", IconFor("dashboard"), "Dashboard");
            AddNav("companies", IconFor("companies"), "Tenants / Businesses");
            AddNav("subscriptions", IconFor("subscriptions"), "Subscriptions");
            AddSection("Administration");
            AddNav("system-monitor", IconFor("system-monitor"), "System monitor");
            AddNav("terms", IconFor("terms"), "Terms & conditions");
        }

        private void BuildAdminMenu()
        {
            AddSection("Main");
            AddNav("dashboard", IconFor("dashboard"), "Dashboard");
            AddNav("reports", IconFor("reports"), "Reports");
            AddNav("retention", IconFor("retention"), "Retention");
            AddSection("Administration");
            AddNav("user-accounts", IconFor("user-accounts"), "User accounts");
            AddNav("loyalty", IconFor("loyalty"), "Loyalty programs");
            AddNav("terms", IconFor("terms"), "Terms & conditions");
        }

        private void BuildManagerMenu()
        {
            AddSection("Main");
            AddNav("dashboard", IconFor("dashboard"), "Dashboard");
            AddNav("reports", IconFor("reports"), "Reports");
            AddNav("retention", IconFor("retention"), "Retention");
            AddSection("Operations");
            AddNav("repairs", IconFor("repairs"), "Repair requests");
            AddNav("staff-activity", IconFor("staff-activity"), "Staff activity");
            AddNav("loyalty", IconFor("loyalty"), "Loyalty programs");
        }

        private void BuildStaffMenu()
        {
            AddSection("Main");
            AddNav("dashboard", IconFor("dashboard"), "Dashboard");
            AddNav("customers", IconFor("customers"), "Customers");
            AddSection("Customer care");
            AddNav("follow-ups", IconFor("follow-ups"), "Follow-ups");
            AddNav("interactions", IconFor("interactions"), "Interactions");
            AddNav("repairs", IconFor("repairs"), "Repair requests");
            AddNav("customer-history", IconFor("customer-history"), "Customer history");
        }

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

        private void ApplyCollapsed()
        {
            Width = _collapsed ? UiKit.SidebarRail : UiKit.SidebarWidth;
            foreach (var b in _buttons.Values) b.Compact = _collapsed;
            foreach (var s in _sections) s.Compact = _collapsed;
            if (_logout != null) _logout.Compact = _collapsed;
            if (_toggle != null)
            {
                _toggle.Rotate = _collapsed ? 270 : 90;
                _tips.SetToolTip(_toggle, _collapsed ? "Expand menu" : "Collapse menu");
            }
            LayoutUi();
            Invalidate(true);
        }

        private void LayoutUi()
        {
            if (_nav == null || _logout == null || _toggle == null) return;
            int pad = UiKit.S3;
            int itemW = Width - pad * 2;

            if (_collapsed)
            {
                _toggle.Location = new Point((Width - _toggle.Width) / 2, 12);
            }
            else
            {
                _toggle.Location = new Point(Width - _toggle.Width - pad, 12);
            }

            int navTop = 52;
            int logoutH = UiKit.NavHeight;
            int logoutTop = Math.Max(navTop, Height - pad - logoutH);

            _logout.Location = new Point(pad, logoutTop);
            _logout.Size = new Size(itemW, logoutH);

            _nav.Location = new Point(0, navTop);
            _nav.Size = new Size(Width, Math.Max(0, logoutTop - UiKit.S3 - navTop));

            foreach (var b in _buttons.Values) b.Size = new Size(itemW, UiKit.NavHeight);
            foreach (var s in _sections) s.Size = new Size(itemW, 20);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UiKit.Quality(g);
            using (var b = new SolidBrush(UiKit.Surface))
                g.FillRectangle(b, ClientRectangle);
            UiKit.VLine(g, Width - 1, 0, Height, UiKit.Line);
            if (_logout != null)
                UiKit.HLine(g, UiKit.S3, Width - UiKit.S3, _logout.Top - UiKit.S3, UiKit.LineSoft);
        }

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

        public List<string> KeysInOrder => new(_buttons.Keys);

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Arrow-key roving between nav items (H7 flexibility + a11y).
            if (keyData is Keys.Down or Keys.Up)
            {
                var keys = new List<string>(_buttons.Keys);
                if (keys.Count == 0) return base.ProcessCmdKey(ref msg, keyData);
                int i = keys.IndexOf(_activeKey);
                i = i < 0 ? 0 : (i + (keyData == Keys.Down ? 1 : -1) + keys.Count) % keys.Count;
                if (_buttons.TryGetValue(keys[i], out var btn)) { btn.Focus(); }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

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
                AccessibleRole = AccessibleRole.MenuItem;
                AccessibleName = title;
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
                    UiKit.FocusRing(g, ClientRectangle, UiKit.RadiusSm);

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
                    if (_compact) UiKit.Dot(g, Width - 14, 13, 7, accent);
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
                AccessibleName = "Toggle menu";
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
                if (Focused) UiKit.FocusRing(g, ClientRectangle, 6);
                var state = g.Save();
                g.TranslateTransform(Width / 2f, Height / 2f);
                g.RotateTransform(Rotate);
                g.TranslateTransform(-Width / 2f, -Height / 2f);
                using (var f = UiKit.GlyphFont(_size))
                using (var brush = new SolidBrush(_hover ? UiKit.Ink : UiKit.InkFaint))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(_glyph, f, brush, new RectangleF(0, 0, Width, Height), sf);
                g.Restore(state);
            }
        }
    }
}
