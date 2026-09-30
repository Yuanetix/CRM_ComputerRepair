using CRM.winforms.Auth;
using CRM.winforms.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// SaaS shell — sidebar (where), top bar (where + command), content (work).
    /// HCI: one page registry drives sidebar, palette, top bar and status, so
    /// naming never drifts (H4). Ctrl+K palette for recognition (H6), toasts
    /// for non-blocking feedback (H1), SaasConfirm for destructive acts (H5).
    /// </summary>
    [DesignerCategory("Code")]
    public partial class MainForm : Form
    {
        private ProfileMenuControl? _profileMenu;
        private BellMenuControl? _bellMenu;
        private Label _lblMeta = null!;

        private sealed record PageMeta(string Title, string Subtitle, string Group);

        private static readonly Dictionary<string, PageMeta> Pages = new()
        {
            ["dashboard"] = new("Dashboard", "Live business intelligence from your CRM data", "Main"),
            ["reports"] = new("Reports", "Exports and performance summaries", "Insights"),
            ["retention"] = new("Retention", "Win back idle customers before they churn", "Insights"),
            ["customers"] = new("Customers", "Everyone you serve, searchable in one place", "Workspace"),
            ["follow-ups"] = new("Follow-Ups", "Never miss a promised callback", "Workspace"),
            ["interactions"] = new("Interactions", "Questions, concerns and reviews", "Workspace"),
            ["repairs"] = new("Repair Requests", "Track every device from intake to pickup", "Workspace"),
            ["customer-history"] = new("Customer History", "Full timeline per customer", "Workspace"),
            ["staff-activity"] = new("Staff Activity", "Who did what, when", "Manage"),
            ["loyalty"] = new("Loyalty Programs", "Reward repeat business", "Manage"),
            ["subscriptions"] = new("Subscriptions", "Plans and billing", "Admin"),
            ["companies"] = new("Tenants", "Register and manage client companies, database instances and subscription tiers", "Admin"),
            ["terms"] = new("Terms & Conditions", "Policies customers agree to", "Admin"),
            ["user-accounts"] = new("User Accounts", "Staff logins and roles", "Admin"),
            ["admin-accounts"] = new("Admin Accounts", "Administrators of the workspace", "Admin"),
            ["system-monitor"] = new("System Monitor", "Health and audit trail", "Admin"),
        };

        public MainForm()
        {
            InitializeComponent();

            sidebar.MenuSelected += Sidebar_MenuSelected;
            sidebar.LogoutClicked += Sidebar_LogoutClicked;

            topBar.ProfileClicked += TopBar_ProfileClicked;
            topBar.BellClicked += TopBar_BellClicked;
            topBar.BrandClicked += (s, e) => NavigateTo("dashboard");

            this.Load += MainForm_Load;
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            string displayUser = TopBarControl.CleanUserDisplayName(UserSession.FullName, UserSession.Role);

            // Sidebar rebuilds now that the session exists (fixes wrong menu for Admins).
            sidebar.UserName = displayUser;
            sidebar.UserRole = UserSession.Role;
            sidebar.RefreshForRole(UserSession.Role);

            topBar.SetUser(UserSession.FullName, UserSession.Role, UserSession.CompanyId);
            topBar.NotificationCount = 0;

            BuildStatusMeta();

            this.Text = $"Fixory — CRM for Computer Repair  ·  {displayUser} ({UserSession.Role})";

            NavigateTo("dashboard");
            Toast.Notify(this, $"Welcome back, {displayUser}.",
                "Press Ctrl+K to jump anywhere.", ToastKind.Info);
        }

        private void BuildStatusMeta()
        {
            _lblMeta = new Label
            {
                Font = AppTheme.FontStatus,
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                Anchor = AnchorStyles.Right
            };
            pnlStatus.Controls.Add(_lblMeta);
            UpdateStatusMeta();
            pnlStatus.Resize += (s, e) => PositionMeta();
            PositionMeta();
        }

        private void UpdateStatusMeta()
        {
            _lblMeta.Text = $"{UserSession.Role}  ·  v1.0  ·  Connected";
        }

        private void PositionMeta()
        {
            _lblMeta.Location = new Point(
                Math.Max(0, pnlStatus.Width - _lblMeta.PreferredWidth - 16),
                (pnlStatus.Height - _lblMeta.PreferredHeight) / 2);
        }

        // ── Global SaaS shortcuts ──
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.K))
            {
                OpenPalette();
                return true;
            }
            if (keyData == (Keys.Control | Keys.B))
            {
                sidebar.Collapsed = !sidebar.Collapsed;
                return true;
            }
            if (keyData == Keys.Escape && (_profileMenu != null || _bellMenu != null))
            {
                HideAllMenus();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OpenPalette()
        {
            var entries = Pages
                .Select(kv => new CommandPalette.Entry(
                    kv.Key, kv.Value.Title, kv.Value.Group,
                    SidebarControl.IconFor(kv.Key),
                    kv.Value.Subtitle))
                .ToList();
            // Most-used first: dashboard + the current role's pages.
            var order = sidebar.KeysInOrder;
            entries = entries
                .OrderBy(e => order.Contains(e.Key) ? order.IndexOf(e.Key) : 999)
                .ToList();

            string? picked = CommandPalette.Pick(this, entries);
            if (!string.IsNullOrEmpty(picked))
                NavigateTo(picked);
        }

        private void Sidebar_MenuSelected(object? sender, string key)
        {
            HideAllMenus();
            NavigateTo(key);
        }

        private void NavigateTo(string key)
        {
            int? targetCustomerId = null;
            if (key.StartsWith("customer-history:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = key.Split(':');
                if (parts.Length > 1 && int.TryParse(parts[1], out int cid))
                {
                    targetCustomerId = cid;
                }
                key = "customer-history";
            }

            sidebar.ActiveKey = key;

            pnlContent.Controls.Clear();

            UserControl page;

            if (key == "dashboard")
            {
                if (string.Equals(UserSession.Role, "Staff", StringComparison.OrdinalIgnoreCase))
                {
                    var staffDash = new StaffDashboardControl { Dock = DockStyle.Fill };
                    staffDash.ActionRequested += (s, targetKey) =>
                    {
                        if (!string.IsNullOrEmpty(targetKey))
                            NavigateTo(targetKey);
                    };
                    page = staffDash;
                }
                else if (string.Equals(UserSession.Role, "Super Admin", StringComparison.OrdinalIgnoreCase))
                {
                    var superDash = new SuperAdminDashboardControl { Dock = DockStyle.Fill };
                    superDash.ActionRequested += (s, targetKey) =>
                    {
                        if (!string.IsNullOrEmpty(targetKey))
                            NavigateTo(targetKey);
                    };
                    page = superDash;
                }
                else
                {
                    var dashboard = new DashboardControl { Dock = DockStyle.Fill };
                    dashboard.ActionRequested += (s, targetKey) =>
                    {
                        if (!string.IsNullOrEmpty(targetKey))
                            NavigateTo(targetKey);
                    };
                    page = dashboard;
                }
            }
            else if (key == "reports")
                page = new ReportsControl { Dock = DockStyle.Fill };
            else if (key == "retention")
                page = new RetentionControl { Dock = DockStyle.Fill };
            else if (key == "customers")
                page = new CustomerControl { Dock = DockStyle.Fill };
            else if (key == "follow-ups")
            {
                var followUps = new FollowUpListControl { Dock = DockStyle.Fill };
                followUps.ActionRequested += (s, targetKey) =>
                {
                    if (!string.IsNullOrEmpty(targetKey))
                        NavigateTo(targetKey);
                };
                page = followUps;
            }
            else if (key == "interactions")
            {
                var interactions = new InteractionListControl { Dock = DockStyle.Fill };
                interactions.ActionRequested += (s, targetKey) =>
                {
                    if (!string.IsNullOrEmpty(targetKey))
                        NavigateTo(targetKey);
                };
                page = interactions;
            }
            else if (key == "repairs")
            {
                var repairs = new RepairRequestListControl { Dock = DockStyle.Fill };
                repairs.ActionRequested += (s, targetKey) =>
                {
                    if (!string.IsNullOrEmpty(targetKey))
                        NavigateTo(targetKey);
                };
                page = repairs;
            }
            else if (key == "customer-history")
            {
                var history = new CustomerHistoryControl { Dock = DockStyle.Fill };
                history.ActionRequested += (s, targetKey) =>
                {
                    if (!string.IsNullOrEmpty(targetKey))
                        NavigateTo(targetKey);
                };
                page = history;

                if (targetCustomerId.HasValue)
                {
                    this.BeginInvoke(new Action(async () => await history.LoadCustomerAsync(targetCustomerId.Value)));
                }
            }
            else if (key == "staff-activity")
                page = new StaffActivityControl { Dock = DockStyle.Fill };
            else if (key == "loyalty")
                page = new LoyaltyControl { Dock = DockStyle.Fill };
            else if (key == "subscriptions")
                page = new SubscriptionsControl { Dock = DockStyle.Fill };
            else if (key == "companies")
                page = new CompaniesControl { Dock = DockStyle.Fill };
            else if (key == "terms")
                page = new TermsControl { Dock = DockStyle.Fill };
            else if (key == "user-accounts")
                page = new UserAccountsControl { Dock = DockStyle.Fill };
            else if (key == "admin-accounts")
                page = new AdminAccountsControl { Dock = DockStyle.Fill };
            else if (key == "system-monitor")
                page = new SystemMonitorControl { Dock = DockStyle.Fill };
            else
                page = new PlaceholderControl(GetPageTitle(key)) { Dock = DockStyle.Fill };

            pnlContent.Controls.Add(page);

            if (key == "dashboard" && string.Equals(UserSession.Role, "Staff", StringComparison.OrdinalIgnoreCase))
                topBar.SetPage("Operations Dashboard", "Daily repair queue, pending follow-ups, and active tickets");
            else if (key == "dashboard" && string.Equals(UserSession.Role, "Super Admin", StringComparison.OrdinalIgnoreCase))
                topBar.SetPage("Platform Dashboard", "Multi-tenant company overview, active subscriptions, and system health");
            else if (Pages.TryGetValue(key, out var meta))
                topBar.SetPage(meta.Title, meta.Subtitle);
            else
                topBar.SetPage(GetPageTitle(key));

            SetStatus($"{GetPageTitle(key)} loaded.");
        }

        private static string GetPageTitle(string key) =>
            Pages.TryGetValue(key, out var m) ? m.Title : "Fixory";

        private void Sidebar_LogoutClicked(object? sender, EventArgs e)
        {
            ConfirmLogout();
        }

        private void ConfirmLogout()
        {
            bool ok = SaasConfirm.Ask(this,
                "Log out of Fixory?",
                $"Signed in as {UserSession.FullName} ({UserSession.Role}).",
                confirmText: "Log out",
                danger: false,
                detail: "You can sign back in any time.");
            if (!ok) return;

            UserSession.Clear();
            UserSession.LogoutRequested = true;
            this.Close();
        }

        private void TopBar_ProfileClicked(object? sender, EventArgs e)
        {
            if (_profileMenu != null && _profileMenu.Visible)
            {
                HideProfileMenu();
                return;
            }
            ShowProfileMenu();
        }

        private void ShowProfileMenu()
        {
            HideAllMenus();
            _profileMenu = new ProfileMenuControl();
            int menuW = 220;
            int x = topBar.Width - menuW - 20;
            int y = topBar.Bottom + 8;
            _profileMenu.Location = new Point(x, y);
            _profileMenu.Size = new Size(menuW, _profileMenu.DesiredHeight);
            _profileMenu.ItemClicked += ProfileMenu_ItemClicked;
            this.Controls.Add(_profileMenu);
            _profileMenu.BringToFront();
            this.MouseDown += Form_ClickOutsideToCloseMenus;
        }

        private void HideProfileMenu()
        {
            if (_profileMenu != null)
            {
                this.Controls.Remove(_profileMenu);
                _profileMenu.Dispose();
                _profileMenu = null;
            }
            this.MouseDown -= Form_ClickOutsideToCloseMenus;
        }

        private void TopBar_BellClicked(object? sender, EventArgs e)
        {
            if (_bellMenu != null && _bellMenu.Visible)
            {
                HideBellMenu();
                return;
            }
            ShowBellMenu();
        }

        private void ShowBellMenu()
        {
            HideAllMenus();
            _bellMenu = new BellMenuControl();
            int x = topBar.Width - _bellMenu.Width - 20 - 60;
            int y = topBar.Bottom + 8;
            _bellMenu.Location = new Point(x, y);
            this.Controls.Add(_bellMenu);
            _bellMenu.BringToFront();
            this.MouseDown += Form_ClickOutsideToCloseMenus;
        }

        private void HideBellMenu()
        {
            if (_bellMenu != null)
            {
                this.Controls.Remove(_bellMenu);
                _bellMenu.Dispose();
                _bellMenu = null;
            }
            this.MouseDown -= Form_ClickOutsideToCloseMenus;
        }

        private void HideAllMenus()
        {
            HideProfileMenu();
            HideBellMenu();
        }

        private void Form_ClickOutsideToCloseMenus(object? sender, MouseEventArgs e)
        {
            bool clickedInsideProfile = _profileMenu != null &&
                _profileMenu.ClientRectangle.Contains(
                    _profileMenu.PointToClient(Cursor.Position));
            bool clickedInsideBell = _bellMenu != null &&
                _bellMenu.ClientRectangle.Contains(
                    _bellMenu.PointToClient(Cursor.Position));
            if (!clickedInsideProfile) HideProfileMenu();
            if (!clickedInsideBell) HideBellMenu();
        }

        private void ProfileMenu_ItemClicked(object? sender, string key)
        {
            HideProfileMenu();
            if (key == "profile")
            {
                SetStatus("Profile viewed.");
                string displayUser = TopBarControl.CleanUserDisplayName(UserSession.FullName, UserSession.Role);
                MessageBox.Show(
                    $"Signed in as:\n\n" +
                    $"Name:     {displayUser}\n" +
                    $"Username: {UserSession.Username}\n" +
                    $"Role:     {UserSession.Role}\n" +
                    $"Email:    {UserSession.Email}",
                    "User Profile",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        public void SetStatus(string message)
        {
            lblStatus.ForeColor = AppTheme.TextSecondary;
            lblStatus.Text = message;
        }
    }
}
