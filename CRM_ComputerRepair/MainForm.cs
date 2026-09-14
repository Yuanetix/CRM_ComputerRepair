using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms
{
    public partial class MainForm : Form
    {
        private List<SidebarItem> _menuItems;
        private string _activeKey = "customers";
        private readonly Dictionary<string, Button> _sidebarButtons = new();

        public MainForm()
        {
            InitializeComponent();

            _menuItems = SidebarItem.GetMenu();

            this.Load += MainForm_Load;
        }

        // ═══════════ LOAD ═══════════

        private void MainForm_Load(object sender, EventArgs e)
        {
            BuildSidebar();
            UpdateUserInfo();

            // Open default screen
            NavigateTo("customers");
        }

        // ═══════════ USER INFO ═══════════

        private void UpdateUserInfo()
        {
            lblUserName.Text = $"👤  {UserSession.FullName}  ({UserSession.Role})";

            // Re-center the label horizontally to the right of the top bar
            lblUserName.Location = new Point(
                pnlTopBar.Width - lblUserName.Width - 30,
                22);
        }

        // ═══════════ SIDEBAR ═══════════

        private void BuildSidebar()
        {
            // Remove existing dynamic controls (but keep lblSidebarBrand and btnLogout)
            var toRemove = new List<Control>();
            foreach (Control c in pnlSidebar.Controls)
            {
                if (c != lblSidebarBrand && c != btnLogout)
                    toRemove.Add(c);
            }
            foreach (var c in toRemove)
                pnlSidebar.Controls.Remove(c);

            _sidebarButtons.Clear();

            int y = 70;

            foreach (var item in _menuItems)
            {
                var btn = CreateSidebarButton(item);

                btn.Location = new Point(12, y);
                btn.Size = new Size(pnlSidebar.Width - 24, 44);
                btn.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                pnlSidebar.Controls.Add(btn);
                _sidebarButtons[item.Key] = btn;

                y += 48;
            }

            // Logout button at bottom
            btnLogout.Location = new Point(12, pnlSidebar.Height - 60);
            btnLogout.Size = new Size(pnlSidebar.Width - 24, 44);
            btnLogout.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        }

        private Button CreateSidebarButton(SidebarItem item)
        {
            var btn = new Button
            {
                Text = $"   {item.Icon}    {item.Title}",
                Font = FixoryTheme.FontSidebar,
                ForeColor = FixoryTheme.SidebarText,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                Cursor = Cursors.Hand,
                Tag = item.Key,
                UseVisualStyleBackColor = false
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = FixoryTheme.SidebarHover;
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 41, 59);

            btn.Click += (s, e) => NavigateTo(item.Key);

            return btn;
        }

        // ═══════════ NAVIGATION ═══════════

        private void NavigateTo(string key)
        {
            _activeKey = key;

            // Highlight active button (HCI: recognition of current state)
            foreach (var kv in _sidebarButtons)
            {
                var isActive = kv.Key == key;
                kv.Value.BackColor = isActive
                    ? FixoryTheme.SidebarActive
                    : Color.Transparent;
                kv.Value.ForeColor = isActive
                    ? Color.White
                    : FixoryTheme.SidebarText;
            }

            // Swap content
            pnlContent.Controls.Clear();

            if (key == "customers")
            {
                var control = new CustomerControl
                {
                    Dock = DockStyle.Fill
                };
                pnlContent.Controls.Add(control);

                SetStatus("Customer Data Collection loaded.");
            }
            else
            {
                var item = _menuItems.Find(m => m.Key == key);
                var title = item?.Title ?? "Coming Soon";

                var control = new PlaceholderControl(title)
                {
                    Dock = DockStyle.Fill
                };
                pnlContent.Controls.Add(control);

                SetStatus($"{title} — coming soon.");
            }
        }

        // ═══════════ LOGOUT ═══════════

        private void btnLogout_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Log out of Fixory?",
                "Confirm Logout",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                Application.Exit();
            }
        }

        // ═══════════ STATUS ═══════════

        public void SetStatus(string message)
        {
            lblStatus.ForeColor = FixoryTheme.TextSecondary;
            lblStatus.Text = message;
        }
    }
}