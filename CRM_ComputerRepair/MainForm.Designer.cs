using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private SidebarControl sidebar;
        private TopBarControl topBar;
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
            this.MinimumSize = new Size(1400, 820);
            this.BackColor = AppTheme.Background;
            this.Font = AppTheme.FontBody;

            // ═══════════ STATUS BAR ═══════════
            pnlStatus = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                BackColor = AppTheme.Surface
            };

            lblStatus = new Label
            {
                Text = "Ready.",
                Font = AppTheme.FontStatus,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(24, 7)
            };
            pnlStatus.Controls.Add(lblStatus);

            var pnlStatusDivider = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = AppTheme.Border
            };
            pnlStatus.Controls.Add(pnlStatusDivider);

            // ═══════════ CONTENT ═══════════
            pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.Background,
                Padding = new Padding(24)
            };

            // ═══════════ TOP BAR ═══════════
            topBar = new TopBarControl
            {
                Dock = DockStyle.Top
            };

            // ═══════════ SIDEBAR ═══════════
            sidebar = new SidebarControl
            {
                Dock = DockStyle.Left
            };

            // ═══════════ ADD IN ORDER (Dock fill order matters) ═══════════
            this.Controls.Add(pnlContent);
            this.Controls.Add(sidebar);
            this.Controls.Add(topBar);
            this.Controls.Add(pnlStatus);

            this.ResumeLayout(false);
        }
    }
}