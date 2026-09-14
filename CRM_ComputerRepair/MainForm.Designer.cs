using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private Panel pnlTopBar;
        private Label lblLogo;
        private Label lblUserName;
        private Panel pnlTopBarDivider;

        private Panel pnlSidebar;
        private Label lblSidebarBrand;
        private Button btnLogout;
        private Panel pnlSidebarDivider;

        private Panel pnlContent;

        private Panel pnlStatus;
        private Label lblStatus;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // ═══════════ FORM ═══════════
            this.Text = "Fixory — CRM for Computer Repair";
            this.ClientSize = new Size(1920, 1080);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(1400, 800);
            this.BackColor = FixoryTheme.Background;
            this.Font = FixoryTheme.FontBody;

            // ═══════════ TOP BAR (white) ═══════════
            pnlTopBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = FixoryTheme.TopBarBg
            };

            lblLogo = new Label
            {
                Text = "🔧  Fixory",
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = FixoryTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(28, 20)
            };

            lblUserName = new Label
            {
                Text = "👤  User",
                Font = new Font("Segoe UI", 10F),
                ForeColor = FixoryTheme.TextSecondary,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            pnlTopBarDivider = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = FixoryTheme.Border
            };

            pnlTopBar.Controls.Add(lblLogo);
            pnlTopBar.Controls.Add(lblUserName);
            pnlTopBar.Controls.Add(pnlTopBarDivider);

            // ═══════════ SIDEBAR (dark) ═══════════
            pnlSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 260,
                BackColor = FixoryTheme.SidebarBg
            };

            lblSidebarBrand = new Label
            {
                Text = "NAVIGATION",
                Font = FixoryTheme.FontSidebarBrand,
                ForeColor = FixoryTheme.SidebarMuted,
                AutoSize = true,
                Location = new Point(24, 24)
            };

            btnLogout = new Button
            {
                Text = "   🚪    Logout",
                Font = FixoryTheme.FontSidebar,
                ForeColor = Color.FromArgb(252, 165, 165),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(120, 40, 40);
            btnLogout.Click += btnLogout_Click;

            pnlSidebar.Controls.Add(lblSidebarBrand);
            pnlSidebar.Controls.Add(btnLogout);

            // ═══════════ STATUS BAR ═══════════
            pnlStatus = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColor = FixoryTheme.Surface
            };

            lblStatus = new Label
            {
                Text = "Ready.",
                Font = FixoryTheme.FontStatus,
                ForeColor = FixoryTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(28, 8)
            };
            pnlStatus.Controls.Add(lblStatus);

            // ═══════════ CONTENT AREA ═══════════
            pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = FixoryTheme.Background,
                Padding = new Padding(24)
            };

            // ═══════════ ADD IN ORDER ═══════════
            this.Controls.Add(pnlContent);
            this.Controls.Add(pnlSidebar);
            this.Controls.Add(pnlStatus);
            this.Controls.Add(pnlTopBar);

            this.ResumeLayout(false);
        }
    }
}