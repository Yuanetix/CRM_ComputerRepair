using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms
{
    public partial class PlaceholderControl : UserControl
    {
        public PlaceholderControl(string title = "Coming Soon")
        {
            InitializeComponent();
            lblTitle.Text = title;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.BackColor = FixoryTheme.Background;

            // Center panel
            var pnlCenter = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = FixoryTheme.Background
            };

            lblIcon = new Label
            {
                Text = "🚧",
                Font = new Font("Segoe UI", 48F),
                AutoSize = true,
                ForeColor = FixoryTheme.TextSecondary
            };

            lblTitle = new Label
            {
                Text = "Coming Soon",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                AutoSize = true,
                ForeColor = FixoryTheme.TextPrimary
            };

            lblSubtitle = new Label
            {
                Text = "This feature is not yet available in Fixory.",
                Font = new Font("Segoe UI", 11F),
                AutoSize = true,
                ForeColor = FixoryTheme.TextSecondary
            };

            pnlCenter.Controls.Add(lblIcon);
            pnlCenter.Controls.Add(lblTitle);
            pnlCenter.Controls.Add(lblSubtitle);

            pnlCenter.Resize += (s, e) =>
            {
                int cx = pnlCenter.Width / 2;
                int cy = pnlCenter.Height / 2;

                lblIcon.Location = new Point(cx - lblIcon.Width / 2, cy - 100);
                lblTitle.Location = new Point(cx - lblTitle.Width / 2, cy - 20);
                lblSubtitle.Location = new Point(cx - lblSubtitle.Width / 2, cy + 30);
            };

            this.Controls.Add(pnlCenter);

            this.ResumeLayout(false);
        }

        private Label lblIcon;
        private Label lblTitle;
        private Label lblSubtitle;
    }
}