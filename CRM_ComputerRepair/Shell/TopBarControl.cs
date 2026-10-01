using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CRM.winforms.Auth;

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
        public event EventHandler? SyncClicked;

        private BellButton _bell = null!;
        private UserChip _chip = null!;
        private CloudSyncPill _syncPill = null!;
        private Panel _pnlBranchSelector = null!;
        private ComboBox _cmbBranchSelector = null!;
        private bool _isUpdatingBranchSelection;
        private readonly ApiClient _api = new();
        private readonly ToolTip _tips = new ToolTip { InitialDelay = 400, ReshowDelay = 150 };
        private Rectangle _brandBounds = Rectangle.Empty;
        private bool _brandHover;

        public sealed class BranchScopeItem
        {
            public int? BranchId { get; set; }
            public string BranchName { get; set; } = string.Empty;
            public string DisplayText { get; set; } = string.Empty;
            public override string ToString() => DisplayText;
        }

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

        public void UpdateSyncStatus(bool isOnline, int pendingCount, bool isSyncing = false, string? tooltip = null)
        {
            if (_syncPill == null) return;
            _syncPill.IsOnline = isOnline;
            _syncPill.PendingCount = pendingCount;
            _syncPill.IsSyncing = isSyncing;
            if (!string.IsNullOrEmpty(tooltip))
                _tips.SetToolTip(_syncPill, tooltip);
            _syncPill.Invalidate();
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
            _pnlBranchSelector = new Panel
            {
                Size = new Size(230, 36),
                BackColor = UiKit.Surface,
                Padding = new Padding(3, 4, 3, 4)
            };
            _pnlBranchSelector.Paint += (s, e) =>
            {
                var g = e.Graphics;
                UiKit.Quality(g);
                var r = new Rectangle(0, 0, _pnlBranchSelector.Width - 1, _pnlBranchSelector.Height - 1);
                UiKit.StrokeRounded(g, r, 6, UiKit.Line, 1f);
            };

            _cmbBranchSelector = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = AppFonts.Regular(9.5f),
                BackColor = UiKit.Surface,
                ForeColor = AppTheme.TextPrimary
            };
            _cmbBranchSelector.SelectedIndexChanged += CmbBranchSelector_SelectedIndexChanged;
            _pnlBranchSelector.Controls.Add(_cmbBranchSelector);

            UserSession.OnBranchScopeChanged += HandleBranchScopeChanged;

            _syncPill = new CloudSyncPill();
            _syncPill.Click += (s, e) => SyncClicked?.Invoke(this, EventArgs.Empty);
            _tips.SetToolTip(_syncPill, "MonsterASP Cloud Database: Local & Cloud synchronization status. Click to sync now.");

            _bell = new BellButton { Size = new Size(36, 36) };
            _bell.Click += (s, e) => BellClicked?.Invoke(this, EventArgs.Empty);
            _tips.SetToolTip(_bell, "Notifications");

            _chip = new UserChip { Size = new Size(210, 56) };
            _chip.Click += (s, e) => ProfileClicked?.Invoke(this, EventArgs.Empty);
            _tips.SetToolTip(_chip, "Account Profile");

            Controls.Add(_pnlBranchSelector);
            Controls.Add(_syncPill);
            Controls.Add(_bell);
            Controls.Add(_chip);
            Resize += (s, e) => LayoutUi();
        }

        private void LayoutUi()
        {
            _chip.Location = new Point(Width - _chip.Width - UiKit.S5, (Height - _chip.Height) / 2);
            _bell.Location = new Point(_chip.Left - _bell.Width - UiKit.S4, (Height - _bell.Height) / 2);
            _syncPill.Location = new Point(_bell.Left - _syncPill.Width - UiKit.S4, (Height - _syncPill.Height) / 2);
            _pnlBranchSelector.Location = new Point(_syncPill.Left - _pnlBranchSelector.Width - UiKit.S4, (Height - _pnlBranchSelector.Height) / 2);
        }

        public async Task RefreshBranchScopeAsync()
        {
            bool canUseBranching = UserSession.HasModule("BRANCHING") ||
                string.Equals(UserSession.Role, "Super Admin", StringComparison.OrdinalIgnoreCase);

            _pnlBranchSelector.Visible = canUseBranching;
            if (!canUseBranching) return;

            try
            {
                var branches = await _api.GetBranchesAsync(includeInactive: false);
                SetBranches(branches);
            }
            catch
            {
                SetBranches(new List<BranchDto>());
            }
        }

        public void SetBranches(List<BranchDto> branches)
        {
            _isUpdatingBranchSelection = true;
            try
            {
                _cmbBranchSelector.Items.Clear();

                bool isAdmin = string.Equals(UserSession.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(UserSession.Role, "Super Admin", StringComparison.OrdinalIgnoreCase);

                if (isAdmin)
                {
                    _cmbBranchSelector.Enabled = true;
                    _tips.SetToolTip(_cmbBranchSelector, "Switch active branch scope (company-wide or specific branch)");

                    var allItem = new BranchScopeItem
                    {
                        BranchId = null,
                        BranchName = "All Branches",
                        DisplayText = "🏢  All Branches"
                    };
                    _cmbBranchSelector.Items.Add(allItem);

                    int selectedIdx = 0;
                    foreach (var b in branches.OrderBy(x => x.BranchCode))
                    {
                        var item = new BranchScopeItem
                        {
                            BranchId = b.BranchId,
                            BranchName = b.BranchName,
                            DisplayText = $"📍  [{b.BranchCode}] {b.BranchName}"
                        };
                        int idx = _cmbBranchSelector.Items.Add(item);
                        if (UserSession.SelectedBranchId.HasValue && UserSession.SelectedBranchId.Value == b.BranchId)
                        {
                            selectedIdx = idx;
                        }
                    }
                    _cmbBranchSelector.SelectedIndex = selectedIdx;
                }
                else
                {
                    // Manager or Staff locked to assigned branch
                    _cmbBranchSelector.Enabled = false;

                    if (UserSession.UserBranchId.HasValue)
                    {
                        var userBranch = branches.FirstOrDefault(b => b.BranchId == UserSession.UserBranchId.Value);
                        string code = userBranch?.BranchCode ?? "BR";
                        string name = userBranch?.BranchName ?? UserSession.UserBranchName ?? "Assigned Branch";

                        var item = new BranchScopeItem
                        {
                            BranchId = UserSession.UserBranchId.Value,
                            BranchName = name,
                            DisplayText = $"🔒  [{code}] {name}"
                        };
                        _cmbBranchSelector.Items.Add(item);
                        _cmbBranchSelector.SelectedIndex = 0;
                        _tips.SetToolTip(_cmbBranchSelector, $"Scoped strictly to your appointed branch: {name}.");
                    }
                    else
                    {
                        var item = new BranchScopeItem
                        {
                            BranchId = null,
                            BranchName = "Unassigned",
                            DisplayText = "🔒  [—] No Branch Assigned"
                        };
                        _cmbBranchSelector.Items.Add(item);
                        _cmbBranchSelector.SelectedIndex = 0;
                        _tips.SetToolTip(_cmbBranchSelector, "Your account is not assigned to a specific branch.");
                    }
                }
            }
            finally
            {
                _isUpdatingBranchSelection = false;
            }
        }

        private void CmbBranchSelector_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingBranchSelection) return;
            if (_cmbBranchSelector.SelectedItem is BranchScopeItem item)
            {
                if (UserSession.SelectedBranchId != item.BranchId)
                {
                    UserSession.SetSelectedBranch(item.BranchId, item.BranchName);
                }
            }
        }

        private void HandleBranchScopeChanged()
        {
            if (_isUpdatingBranchSelection) return;
            _isUpdatingBranchSelection = true;
            try
            {
                for (int i = 0; i < _cmbBranchSelector.Items.Count; i++)
                {
                    if (_cmbBranchSelector.Items[i] is BranchScopeItem item && item.BranchId == UserSession.SelectedBranchId)
                    {
                        _cmbBranchSelector.SelectedIndex = i;
                        break;
                    }
                }
            }
            finally
            {
                _isUpdatingBranchSelection = false;
            }
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

        [DesignerCategory("Code")]
        private sealed class CloudSyncPill : Control
        {
            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public bool IsOnline { get; set; } = false;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public int PendingCount { get; set; } = 0;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public bool IsSyncing { get; set; } = false;
            private bool _hover;

            public CloudSyncPill()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = UiKit.Surface;
                Cursor = Cursors.Hand;
                TabStop = true;
                Size = new Size(160, 30);
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                Color bg, border, dotColor, textColor;
                string label;

                if (IsSyncing)
                {
                    bg = Color.FromArgb(239, 248, 255);
                    border = Color.FromArgb(186, 226, 255);
                    dotColor = Color.FromArgb(23, 92, 211);
                    textColor = Color.FromArgb(23, 92, 211);
                    label = "Syncing to Cloud...";
                }
                else if (IsOnline)
                {
                    bg = Color.FromArgb(236, 253, 243);
                    border = Color.FromArgb(166, 244, 197);
                    dotColor = Color.FromArgb(18, 183, 106);
                    textColor = Color.FromArgb(6, 118, 71);
                    label = PendingCount > 0 ? $"Syncing ({PendingCount})..." : "Cloud Synced";
                }
                else
                {
                    bg = Color.FromArgb(254, 246, 238);
                    border = Color.FromArgb(253, 205, 154);
                    dotColor = Color.FromArgb(247, 144, 9);
                    textColor = Color.FromArgb(181, 71, 8);
                    label = PendingCount > 0 ? $"Offline ({PendingCount} pending)" : "Offline (Local)";
                }

                if (_hover)
                {
                    border = dotColor;
                }

                var rect = new Rectangle(0, 0, Width, Height);
                UiKit.FillRounded(g, rect, 15, bg);
                UiKit.StrokeRounded(g, rect, 15, border, 1f);

                // Draw dot indicator
                using (var dotBrush = new SolidBrush(dotColor))
                    g.FillEllipse(dotBrush, 12, (Height - 8) / 2, 8, 8);

                // Text
                var textRect = new Rectangle(26, 0, Width - 32, Height);
                using (var textFont = new Font("Segoe UI", 8.25f, FontStyle.Bold))
                    TextRenderer.DrawText(g, label, textFont, textRect, textColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
