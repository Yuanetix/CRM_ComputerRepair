using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms
{
    partial class CustomerControl
    {
        private System.ComponentModel.IContainer components = null;

        // Form card
        private Panel pnlFormCard;
        private Label lblFormTitle;
        private Label lblFormSubtitle;

        private Label lblFirstName;
        private Label lblLastName;
        private Label lblEmail;
        private Label lblPhone;
        private Label lblAddress;

        private TextBox txtFirstName;
        private TextBox txtLastName;
        private TextBox txtEmail;
        private TextBox txtPhone;
        private TextBox txtAddress;

        private Button btnSave;
        private Button btnUpdate;
        private Button btnArchive;
        private Button btnRestore;
        private Button btnClear;

        // Grid card
        private Panel pnlGridCard;
        private Label lblListTitle;
        private Label lblSearchIcon;
        private TextBox txtSearch;
        private CheckBox chkShowArchived;
        private Button btnRefresh;
        private DataGridView dgvCustomers;

        // Status
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

            // ─────────── USER CONTROL ROOT ───────────
            this.BackColor = FixoryTheme.Background;
            this.Font = FixoryTheme.FontBody;
            this.AutoScaleMode = AutoScaleMode.Font;

            // ─────────── FORM CARD ───────────
            pnlFormCard = UiHelpers.CreateCard();
            pnlFormCard.Location = new Point(20, 20);
            pnlFormCard.Size = new Size(1360, 300);
            pnlFormCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            lblFormTitle = new Label
            {
                Text = "Customer Information",
                Font = FixoryTheme.FontTitle,
                ForeColor = FixoryTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(20, 16)
            };

            lblFormSubtitle = new Label
            {
                Text = "Enter the customer's details below. Fields marked with * are required.",
                Font = FixoryTheme.FontSubtitle,
                ForeColor = FixoryTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(20, 52)
            };

            // First Name
            lblFirstName = new Label
            {
                Text = "FIRST NAME *",
                Font = FixoryTheme.FontLabel,
                ForeColor = FixoryTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(20, 95)
            };

            txtFirstName = UiHelpers.CreateTextBox();
            txtFirstName.Location = new Point(20, 115);
            txtFirstName.Size = new Size(330, 32);

            // Last Name
            lblLastName = new Label
            {
                Text = "LAST NAME *",
                Font = FixoryTheme.FontLabel,
                ForeColor = FixoryTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(370, 95)
            };

            txtLastName = UiHelpers.CreateTextBox();
            txtLastName.Location = new Point(370, 115);
            txtLastName.Size = new Size(330, 32);

            // Email
            lblEmail = new Label
            {
                Text = "EMAIL",
                Font = FixoryTheme.FontLabel,
                ForeColor = FixoryTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(720, 95)
            };

            txtEmail = UiHelpers.CreateTextBox();
            txtEmail.Location = new Point(720, 115);
            txtEmail.Size = new Size(330, 32);

            // Phone
            lblPhone = new Label
            {
                Text = "PHONE",
                Font = FixoryTheme.FontLabel,
                ForeColor = FixoryTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(20, 160)
            };

            txtPhone = UiHelpers.CreateTextBox();
            txtPhone.Location = new Point(20, 180);
            txtPhone.Size = new Size(330, 32);

            // Address
            lblAddress = new Label
            {
                Text = "ADDRESS",
                Font = FixoryTheme.FontLabel,
                ForeColor = FixoryTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(370, 160)
            };

            txtAddress = UiHelpers.CreateTextBox();
            txtAddress.Location = new Point(370, 180);
            txtAddress.Size = new Size(680, 32);
            txtAddress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            // Buttons
            btnSave = UiHelpers.CreateButton("💾  Save New", FixoryTheme.Primary, Color.White);
            btnSave.Location = new Point(20, 235);
            btnSave.Size = new Size(160, 40);
            btnSave.Click += btnSave_Click;

            btnUpdate = UiHelpers.CreateButton("✏  Update", FixoryTheme.PrimaryLight, Color.White);
            btnUpdate.Location = new Point(190, 235);
            btnUpdate.Size = new Size(140, 40);
            btnUpdate.Click += btnUpdate_Click;

            btnArchive = UiHelpers.CreateButton("🗑  Archive", FixoryTheme.Danger, Color.White);
            btnArchive.Location = new Point(340, 235);
            btnArchive.Size = new Size(140, 40);
            btnArchive.Click += btnArchive_Click;

            btnRestore = UiHelpers.CreateButton("♻  Restore", FixoryTheme.Success, Color.White);
            btnRestore.Location = new Point(490, 235);
            btnRestore.Size = new Size(140, 40);
            btnRestore.Click += btnRestore_Click;

            btnClear = UiHelpers.CreateButton("Clear Form",
                Color.FromArgb(230, 230, 235), FixoryTheme.TextPrimary);
            btnClear.Location = new Point(640, 235);
            btnClear.Size = new Size(130, 40);
            btnClear.Click += btnClear_Click;

            pnlFormCard.Controls.AddRange(new Control[]
            {
                lblFormTitle, lblFormSubtitle,
                lblFirstName, txtFirstName,
                lblLastName, txtLastName,
                lblEmail, txtEmail,
                lblPhone, txtPhone,
                lblAddress, txtAddress,
                btnSave, btnUpdate, btnArchive, btnRestore, btnClear
            });

            // ─────────── GRID CARD ───────────
            pnlGridCard = UiHelpers.CreateCard();
            pnlGridCard.Location = new Point(20, 340);
            pnlGridCard.Size = new Size(1360, 480);
            pnlGridCard.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                                 | AnchorStyles.Left | AnchorStyles.Right;

            lblListTitle = new Label
            {
                Text = "Customer List",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = FixoryTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(20, 14)
            };

            lblSearchIcon = new Label
            {
                Text = "🔍",
                Font = new Font("Segoe UI", 11F),
                ForeColor = FixoryTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(500, 18)
            };

            txtSearch = UiHelpers.CreateTextBox();
            txtSearch.Location = new Point(530, 15);
            txtSearch.Size = new Size(380, 30);
            txtSearch.PlaceholderText = "Search by name, email, phone, or address...";
            txtSearch.TextChanged += txtSearch_TextChanged;

            chkShowArchived = new CheckBox
            {
                Text = "Show archived",
                Font = FixoryTheme.FontBody,
                ForeColor = FixoryTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(930, 20)
            };
            chkShowArchived.CheckedChanged += chkShowArchived_CheckedChanged;

            btnRefresh = UiHelpers.CreateButton("🔄  Refresh",
                FixoryTheme.PrimarySoft, FixoryTheme.Primary);
            btnRefresh.Location = new Point(1090, 14);
            btnRefresh.Size = new Size(130, 34);
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.Click += btnRefresh_Click;

            dgvCustomers = new DataGridView
            {
                Location = new Point(20, 62),
                Size = new Size(1320, 400),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                         | AnchorStyles.Left | AnchorStyles.Right
            };
            UiHelpers.StyleGrid(dgvCustomers);
            dgvCustomers.SelectionChanged += dgvCustomers_SelectionChanged;

            pnlGridCard.Controls.AddRange(new Control[]
            {
                lblListTitle, lblSearchIcon, txtSearch,
                chkShowArchived, btnRefresh, dgvCustomers
            });

            // ─────────── STATUS ───────────
            lblStatus = new Label
            {
                Text = "Ready.",
                Font = FixoryTheme.FontStatus,
                ForeColor = FixoryTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(24, 835)
            };

            // ─────────── ADD TO CONTROL ───────────
            this.Controls.Add(pnlFormCard);
            this.Controls.Add(pnlGridCard);
            this.Controls.Add(lblStatus);

            this.ResumeLayout(false);
        }
    }
}