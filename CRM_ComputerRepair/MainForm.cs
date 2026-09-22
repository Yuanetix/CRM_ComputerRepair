using CRM.winforms.Auth;
using CRM.winforms.Controls;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms
{
    [DesignerCategory("Code")]
    public partial class MainForm : Form
    {
        private ProfileMenuControl? _profileMenu;
        private BellMenuControl? _bellMenu;

        public MainForm()
        {
            InitializeComponent();

            sidebar.MenuSelected += Sidebar_MenuSelected;
            sidebar.LogoutClicked += Sidebar_LogoutClicked;

            topBar.ProfileClicked += TopBar_ProfileClicked;
            topBar.BellClicked += TopBar_BellClicked;

            this.Load += MainForm_Load;
        }

        // ═══════════ LOAD ═══════════

        private void MainForm_Load(object? sender, EventArgs e)
        {
            topBar.SetUser(UserSession.FullName, UserSession.Role);

            sidebar.UserName = UserSession.FullName;
            sidebar.UserRole = UserSession.Role;

            this.Text = $"Fixory — CRM for Computer Repair  ·  {UserSession.FullName} ({UserSession.Role})";

            NavigateTo("dashboard");
        }

        // ═══════════ NAVIGATION ═══════════

        private void Sidebar_MenuSelected(object? sender, string key)
        {
            HideAllMenus();
            NavigateTo(key);
        }

        private void NavigateTo(string key)
        {
            sidebar.ActiveKey = key;

            pnlContent.Controls.Clear();

            UserControl page;

            if (key == "dashboard")
            {
                var dashboard = new DashboardControl { Dock = DockStyle.Fill };

                // ── KPI tile click → jump to the relevant page ──
                dashboard.ActionRequested += (s, targetKey) =>
                {
                    if (!string.IsNullOrEmpty(targetKey))
                        NavigateTo(targetKey);
                };

                page = dashboard;
            }
            else if (key == "reports")
                page = new ReportsControl { Dock = DockStyle.Fill };
            else if (key == "retention")
                page = new RetentionControl { Dock = DockStyle.Fill };
            else if (key == "customers")
                page = new CustomerControl { Dock = DockStyle.Fill };
            else if (key == "follow-ups")
                page = new FollowUpListControl { Dock = DockStyle.Fill };
            else if (key == "interactions")
                page = new InteractionListControl { Dock = DockStyle.Fill };
            else if (key == "repairs")
                page = new RepairRequestListControl { Dock = DockStyle.Fill };
            else if (key == "customer-history")
                page = new CustomerHistoryControl { Dock = DockStyle.Fill };
            else if (key == "staff-activity")
                page = new StaffActivityControl { Dock = DockStyle.Fill };
            else if (key == "loyalty")
                page = new LoyaltyControl { Dock = DockStyle.Fill };
            else if (key == "subscriptions")
                page = new SubscriptionsControl { Dock = DockStyle.Fill };
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
            SetStatus($"{GetPageTitle(key)} loaded.");
        }

        private static string GetPageTitle(string key)
        {
            return key switch
            {
                // Common
                "dashboard" => "Dashboard",

                // BI
                "reports" => "Reports",
                "retention" => "Retention",

                // Staff
                "customers" => "Customers",
                "follow-ups" => "Follow-Ups",
                "interactions" => "Interactions",
                "repairs" => "Repair Requests",
                "customer-history" => "Customer History",

                // Manager
                "staff-activity" => "Staff Activity",

                // Admin / Manager / Super Admin
                "loyalty" => "Loyalty Programs",
                "subscriptions" => "Subscriptions",
                "terms" => "Terms & Conditions",

                // Admin / Super Admin
                "user-accounts" => "User Accounts",

                // Super Admin only
                "admin-accounts" => "Admin Accounts",
                "system-monitor" => "System Monitor",

                _ => "Fixory"
            };
        }

        // ═══════════ LOGOUT ═══════════

        private void Sidebar_LogoutClicked(object? sender, EventArgs e)
        {
            ConfirmLogout();
        }

        private void ConfirmLogout()
        {
            var result = MessageBox.Show(
                $"Log out of Fixory?\n\n" +
                $"Signed in as: {UserSession.FullName} ({UserSession.Role})",
                "Confirm Logout",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            UserSession.Clear();
            this.Hide();

            using (var login = new LoginForm())
            {
                if (login.ShowDialog() == DialogResult.OK && UserSession.IsAuthenticated)
                {
                    var next = new MainForm();
                    next.Show();
                }
            }

            this.Close();
        }

        // ═══════════ PROFILE MENU ═══════════

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

            int menuW = 200;
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

        // ═══════════ BELL MENU ═══════════

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

        // ═══════════ OUTSIDE CLICK ═══════════

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

        // ═══════════ PROFILE ACTIONS ═══════════

        private void ProfileMenu_ItemClicked(object? sender, string key)
        {
            HideProfileMenu();

            switch (key)
            {
                case "profile":
                    SetStatus("Profile viewed.");
                    MessageBox.Show(
                        $"Signed in as:\n\n" +
                        $"{UserSession.FullName}\n" +
                        $"Username: {UserSession.Username}\n" +
                        $"Role:     {UserSession.Role}\n" +
                        $"Email:    {UserSession.Email}\n" +
                        $"Company:  {UserSession.CompanyId}",
                        "Profile",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    break;

                case "settings":
                    SetStatus("Settings — coming soon.");
                    NavigateTo("settings");
                    break;

                case "signout":
                    ConfirmLogout();
                    break;
            }
        }

        // ═══════════ STATUS ═══════════

        public void SetStatus(string message)
        {
            lblStatus.ForeColor = AppTheme.TextSecondary;
            lblStatus.Text = message;
        }
    }
}