using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Clean sidebar — role-aware, sectioned nav + sign out.
    /// </summary>
    [DesignerCategory("Code")]
    public class SidebarControl : Panel
    {
        // ═══════════ EVENTS ═══════════

        public event EventHandler<string>? MenuSelected;
        public event EventHandler? LogoutClicked;

        // ═══════════ STATE ═══════════

        private string _activeKey = "dashboard";
        private readonly Dictionary<string, SidebarButton> _buttons = new();

        private Button btnLogout = null!;
        private Panel pnlDivider = null!;

        // ═══════════ PROPERTIES ═══════════

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
            set
            {
                _activeKey = value;
                UpdateActiveState();
            }
        }

        // ═══════════ CONSTRUCTOR ═══════════

        public SidebarControl()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.SidebarBg;
            Width = AppTheme.SidebarWidth;
            Dock = DockStyle.Left;

            BuildUi();
        }

        // ═══════════ UI BUILD ═══════════

        private void BuildUi()
        {
            int pad = AppTheme.SidebarSidePad;
            int y = 24;

            // Route by role (only Staff is fully implemented)
            switch (UserSession.Role)
            {
                case "Super Admin":
                    BuildSuperAdminMenu(pad, ref y);
                    break;
                case "Admin":
                    BuildAdminMenu(pad, ref y);
                    break;
                case "Manager":
                    BuildManagerMenu(pad, ref y);
                    break;
                case "Staff":
                default:
                    BuildStaffMenu(pad, ref y);
                    break;
            }

            // ── Divider (above sign-out) ──
            pnlDivider = new Panel
            {
                BackColor = AppTheme.Divider,
                Height = 1,
                Width = Width - pad * 2
            };
            Controls.Add(pnlDivider);

            // ── Sign out ──
            btnLogout = new Button
            {
                Text = "  " + IconFont.SignOut + "   Sign out",
                Font = AppTheme.FontSidebar,
                ForeColor = AppTheme.TextSecondary,
                BackColor = AppTheme.SidebarBg,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(AppTheme.Space12, 0, 0, 0),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Size = new Size(Width - pad * 2, 42),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.FlatAppearance.MouseOverBackColor = AppTheme.Neutral;
            btnLogout.FlatAppearance.MouseDownBackColor = AppTheme.Border;
            btnLogout.Click += (s, e) => LogoutClicked?.Invoke(this, EventArgs.Empty);
            Controls.Add(btnLogout);

            // ── Bottom layout ──
            void LayoutBottom()
            {
                int bottomPad = pad;

                btnLogout.Location = new Point(pad, Height - bottomPad - 42);
                btnLogout.Size = new Size(Width - pad * 2, 42);

                pnlDivider.Location = new Point(pad, btnLogout.Top - 12);
                pnlDivider.Size = new Size(Width - pad * 2, 1);
            }

            Resize += (s, e) => LayoutBottom();
            LayoutBottom();
        }

        // ═══════════ ROLE-SPECIFIC MENUS ═══════════

        /// <summary>
        /// STAFF — maps 1:1 to Staff use cases:
        ///   View Dashboard, Manage Customer and follow ups,
        ///   Manage Repair Request, Manage Customer Interactions,
        ///   View Customer History
        /// </summary>
        private void BuildStaffMenu(int pad, ref int y)
        {
            // ── MAIN ──
            AddSectionLabel("MAIN", pad, ref y);

            AddNavItem("dashboard", IconFont.Dashboard, "Dashboard", pad, ref y);
            AddNavItem("customers", IconFont.Customers, "Customers", pad, ref y);

            y += 14;

            // ── CUSTOMER CARE ──
            AddSectionLabel("CUSTOMER CARE", pad, ref y);

            AddNavItem("follow-ups", IconFont.Dashboard, "Follow-Ups", pad, ref y);
            AddNavItem("interactions", IconFont.Customers, "Interactions", pad, ref y);
            AddNavItem("repairs", IconFont.Repairs, "Repair Requests", pad, ref y);
            AddNavItem("customer-history", IconFont.Dashboard, "Customer History", pad, ref y);
        }

        /// <summary>
        /// SUPER ADMIN — maps to Super Admin use cases:
        ///   Manage admin accounts, Monitor system,
        ///   Terms & Conditions, Manage Subscriptions
        /// </summary>
        private void BuildSuperAdminMenu(int pad, ref int y)
        {
            AddSectionLabel("MAIN", pad, ref y);
            AddNavItem("dashboard", IconFont.Dashboard, "Dashboard", pad, ref y);

            y += 14;

            AddSectionLabel("ADMIN", pad, ref y);
            AddNavItem("admin-accounts", IconFont.Profile, "Admin Accounts", pad, ref y);
            AddNavItem("system-monitor", IconFont.Dashboard, "System Monitor", pad, ref y);
            AddNavItem("subscriptions", IconFont.Reports, "Subscriptions", pad, ref y);
            AddNavItem("terms", IconFont.Reports, "Terms & Conditions", pad, ref y);
        }

        /// <summary>
        /// ADMIN — maps to Admin use cases:
        ///   Manage User account, Manage Loyalty programs,
        ///   View Reports and Dashboard, Terms & Conditions
        /// </summary>
        private void BuildAdminMenu(int pad, ref int y)
        {
            AddSectionLabel("MAIN", pad, ref y);
            AddNavItem("dashboard", IconFont.Dashboard, "Dashboard", pad, ref y);

            y += 14;

            AddSectionLabel("ADMIN", pad, ref y);
            AddNavItem("user-accounts", IconFont.Customers, "User Accounts", pad, ref y);
            AddNavItem("loyalty", IconFont.Loyalty, "Loyalty Programs", pad, ref y);
            AddNavItem("reports", IconFont.Reports, "Reports", pad, ref y);
            AddNavItem("terms", IconFont.Reports, "Terms & Conditions", pad, ref y);
        }

        /// <summary>
        /// MANAGER — maps to Manager use cases:
        ///   Manage Loyalty programs, View Reports and Dashboard,
        ///   Monitor Staff Activities, Approve/Reassign Repair Request
        /// </summary>
        private void BuildManagerMenu(int pad, ref int y)
        {
            AddSectionLabel("MAIN", pad, ref y);
            AddNavItem("dashboard", IconFont.Dashboard, "Dashboard", pad, ref y);

            y += 14;

            AddSectionLabel("OPERATIONS", pad, ref y);
            AddNavItem("loyalty", IconFont.Loyalty, "Loyalty Programs", pad, ref y);
            AddNavItem("reports", IconFont.Reports, "Reports", pad, ref y);
            AddNavItem("staff-activity", IconFont.Customers, "Staff Activity", pad, ref y);
            AddNavItem("repairs", IconFont.Repairs, "Repair Requests", pad, ref y);
        }

        // ═══════════ SECTION LABEL ═══════════

        private void AddSectionLabel(string text, int pad, ref int y)
        {
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 7.5F),
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(pad + 12, y)
            };
            Controls.Add(lbl);
            y += 22;
        }

        // ═══════════ NAV ITEM ═══════════

        private void AddNavItem(string key, string iconCode, string title, int pad, ref int y)
        {
            var btn = new SidebarButton(key, iconCode, title);

            int itemH = AppTheme.SidebarItemHeight;
            btn.Location = new Point(pad, y);
            btn.Size = new Size(Width - pad * 2, itemH);
            btn.Click += (s, e) => HandleMenuClick(key);

            Controls.Add(btn);
            _buttons[key] = btn;

            y += itemH + AppTheme.SidebarItemGap;
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

        // ═══════════ ROUNDED HELPERS ═══════════

        public static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
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

        // ═══════════ NAV BUTTON ═══════════

        [DesignerCategory("Code")]
        private class SidebarButton : Control
        {
            private readonly string _key;
            private readonly string _iconCode;
            private readonly string _title;

            private bool _isActive;
            private bool _isHover;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public bool IsActive
            {
                get => _isActive;
                set { _isActive = value; Invalidate(); }
            }

            public SidebarButton(string key, string iconCode, string title)
            {
                _key = key;
                _iconCode = iconCode;
                _title = title;
                DoubleBuffered = true;
                Cursor = Cursors.Hand;
                TabStop = true;
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                if (Parent != null)
                {
                    var state = e.Graphics.Save();
                    e.Graphics.TranslateTransform(-Left, -Top);
                    var pe = new PaintEventArgs(e.Graphics, new Rectangle(Left, Top, Width, Height));
                    InvokePaintBackground(Parent, pe);
                    e.Graphics.Restore(state);
                }
                else base.OnPaintBackground(e);
            }

            protected override void OnMouseEnter(EventArgs e) { _isHover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _isHover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var rect = new Rectangle(0, 0, Width - 1, Height - 1);

                Color bg = Color.Empty;
                if (_isActive) bg = AppTheme.SidebarActiveBg;
                else if (_isHover) bg = AppTheme.Neutral;

                if (bg != Color.Empty)
                {
                    using var path = GetRoundedPath(rect, 8);
                    using var brush = new SolidBrush(bg);
                    g.FillPath(brush, path);
                }

                if (_isActive)
                {
                    var barRect = new Rectangle(0, Height / 2 - 9, 3, 18);
                    using var barPath = GetRoundedPath(barRect, 2);
                    using var barBrush = new SolidBrush(AppTheme.SidebarAccentBar);
                    g.FillPath(barBrush, barPath);
                }

                Color fg = _isActive ? AppTheme.SidebarActiveTx : AppTheme.SidebarText;
                if (_isHover && !_isActive) fg = AppTheme.SidebarTextHov;

                using (var iconFont = IconFont.Create(11F))
                using (var iconBrush = new SolidBrush(fg))
                {
                    var sf = new StringFormat
                    {
                        LineAlignment = StringAlignment.Center,
                        Alignment = StringAlignment.Center
                    };
                    g.DrawString(_iconCode, iconFont, iconBrush,
                        new RectangleF(10, 0, 28, Height), sf);
                }

                Font labelFont = _isActive ? AppTheme.FontSidebarActive : AppTheme.FontSidebar;
                using (var textBrush = new SolidBrush(fg))
                {
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                    g.DrawString(_title, labelFont, textBrush,
                        new RectangleF(44, 0, Width - 52, Height), sf);
                }
            }
        }
    }
}