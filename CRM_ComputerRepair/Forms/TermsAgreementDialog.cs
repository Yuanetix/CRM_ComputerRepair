using CRM.winforms.Auth;
using CRM.winforms.Controls;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Forms
{
    /// <summary>
    /// Mandatory onboarding / first-login license agreement screen.
    /// Modeled after desktop application installer wizard agreements:
    /// tenant administrators and staff must review and accept the official
    /// platform Terms &amp; Conditions set by the Super Admin before accessing
    /// their business workspace.
    /// </summary>
    [DesignerCategory("Code")]
    public class TermsAgreementDialog : Form
    {
        private readonly ApiClient _api = new ApiClient();
        private TermsDto? _activeTerms;

        // UI Layout Elements
        private Panel pnlHeader = null!;
        private Panel pnlContent = null!;
        private Panel pnlFooter = null!;

        private Label lblHeaderTitle = null!;
        private Label lblHeaderSubtitle = null!;
        private Label lblHeaderIcon = null!;

        private Panel pnlInfoStrip = null!;
        private Label lblCompanyInfo = null!;
        private Label lblTermsVersion = null!;

        private Label lblInstruction = null!;
        private RichTextBox txtTermsContent = null!;
        private SaasButton btnCopy = null!;

        private Panel pnlDecision = null!;
        private RadioButton rbDecline = null!;
        private RadioButton rbAccept = null!;
        private Label lblAcceptNote = null!;

        private Label lblFooterStatus = null!;
        private SaasButton btnDecline = null!;
        private SaasButton btnAccept = null!;

        public TermsAgreementDialog()
        {
            InitializeComponent();
            ApplyTheme();
            this.Load += async (s, e) => await LoadActiveTermsAsync();
        }

        private void InitializeComponent()
        {
            this.Text = "Fixory CRM — Platform License Agreement";
            this.Size = new Size(820, 720);
            this.MinimumSize = new Size(760, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.BackColor = AppTheme.Background;
            this.Font = UiKit.T.Body;

            // ═══════════ HEADER PANEL (Installer Wizard Style) ═══════════
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Color.White
            };
            pnlHeader.Paint += (s, e) =>
            {
                using var pen = new Pen(UiKit.T.Line, 1);
                e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
            };

            lblHeaderIcon = new Label
            {
                Text = "\uE8A5", // Document / Legal glyph
                Font = IconFont.Create(28),
                ForeColor = AppTheme.Primary,
                Size = new Size(48, 48),
                Location = new Point(24, 18),
                TextAlign = ContentAlignment.MiddleCenter
            };

            lblHeaderTitle = new Label
            {
                Text = "Platform Terms of Service & License Agreement",
                Font = AppFonts.Strong(12.5f),
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                Location = new Point(82, 18)
            };

            string companyName = !string.IsNullOrWhiteSpace(UserSession.CompanyName)
                ? UserSession.CompanyName
                : $"Tenant #{UserSession.CompanyId}";

            lblHeaderSubtitle = new Label
            {
                Text = $"Please review and accept the official platform terms for {companyName} before proceeding.",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                Location = new Point(82, 44)
            };

            pnlHeader.Controls.Add(lblHeaderIcon);
            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderSubtitle);

            // ═══════════ FOOTER PANEL ═══════════
            pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                BackColor = Color.White
            };
            pnlFooter.Paint += (s, e) =>
            {
                using var pen = new Pen(UiKit.T.Line, 1);
                e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0);
            };

            lblFooterStatus = new Label
            {
                Text = $"Tenant Workspace ID: #{UserSession.CompanyId}  ·  Super Admin Platform Policy",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                Location = new Point(24, 23)
            };

            btnDecline = new SaasButton("Decline & Exit", SaasButtonVariant.Secondary, "\uE711")
            {
                Size = new Size(130, UiKit.T.ButtonHeight),
                Location = new Point(pnlFooter.Width - 310, 14),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnDecline.Click += async (s, e) => await OnDeclineAsync();

            btnAccept = new SaasButton("Accept & Continue", SaasButtonVariant.Primary, "\uE73E")
            {
                Size = new Size(160, UiKit.T.ButtonHeight),
                Location = new Point(pnlFooter.Width - 174, 14),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Enabled = false // Disabled by default until user selects "I accept"
            };
            btnAccept.Click += async (s, e) => await OnAcceptAsync();

            pnlFooter.Controls.Add(lblFooterStatus);
            pnlFooter.Controls.Add(btnDecline);
            pnlFooter.Controls.Add(btnAccept);

            // ═══════════ CONTENT PANEL ═══════════
            pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 16, 24, 16),
                BackColor = AppTheme.Background
            };

            // Info Strip
            pnlInfoStrip = new Panel
            {
                Height = 36,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(240, 246, 255)
            };
            pnlInfoStrip.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(190, 218, 255), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, pnlInfoStrip.Width - 1, pnlInfoStrip.Height - 1);
            };

            lblCompanyInfo = new Label
            {
                Text = $"\uE72E  Business Account: {companyName}",
                Font = UiKit.T.SmallStrong,
                ForeColor = Color.FromArgb(16, 68, 145),
                AutoSize = true,
                Location = new Point(12, 10),
                BackColor = Color.Transparent
            };

            lblTermsVersion = new Label
            {
                Text = "Loading active platform policy...",
                Font = UiKit.T.Small,
                ForeColor = Color.FromArgb(16, 68, 145),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(pnlInfoStrip.Width - 280, 10),
                BackColor = Color.Transparent
            };

            pnlInfoStrip.Controls.Add(lblCompanyInfo);
            pnlInfoStrip.Controls.Add(lblTermsVersion);

            lblInstruction = new Label
            {
                Text = "Please review the clauses below. You must accept these terms on behalf of your business to activate and enter your workspace.",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = false,
                Height = 30,
                Dock = DockStyle.Top
            };

            // Terms Content Box (scrollable rich display)
            txtTermsContent = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                ForeColor = UiKit.T.Ink,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Text = "Fetching official platform terms from server..."
            };

            btnCopy = new SaasButton("Copy Terms", SaasButtonVariant.Ghost, "\uE8C8")
            {
                Size = new Size(110, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(txtTermsContent.Text))
                {
                    Clipboard.SetText(txtTermsContent.Text);
                    Toast.Notify(this, "Copied", "Terms & conditions copied to clipboard.", ToastKind.Success);
                }
            };

            // Decision Panel (Installer-style Radio Buttons)
            pnlDecision = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 88,
                Padding = new Padding(0, 8, 0, 0)
            };

            rbDecline = new RadioButton
            {
                Text = "I do not accept the terms in the license agreement",
                Font = UiKit.T.Body,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                Location = new Point(4, 6),
                Checked = true // Classic installer behavior: default to decline
            };
            rbDecline.CheckedChanged += Decision_CheckedChanged;

            rbAccept = new RadioButton
            {
                Text = "I accept the terms in the license agreement",
                Font = UiKit.T.BodyStrong,
                ForeColor = AppTheme.Primary,
                AutoSize = true,
                Location = new Point(4, 32)
            };
            rbAccept.CheckedChanged += Decision_CheckedChanged;

            lblAcceptNote = new Label
            {
                Text = $"By selecting 'I accept', you confirm that you are authorized to agree to these terms on behalf of {companyName}.",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                Location = new Point(24, 58)
            };

            pnlDecision.Controls.Add(rbDecline);
            pnlDecision.Controls.Add(rbAccept);
            pnlDecision.Controls.Add(lblAcceptNote);

            // Assembly
            var pnlBodyWrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 8) };
            pnlBodyWrapper.Controls.Add(txtTermsContent);

            pnlContent.Controls.Add(pnlBodyWrapper);
            pnlContent.Controls.Add(pnlDecision);
            pnlContent.Controls.Add(lblInstruction);
            pnlContent.Controls.Add(pnlInfoStrip);

            Controls.Add(pnlContent);
            Controls.Add(pnlFooter);
            Controls.Add(pnlHeader);

            // Handle manual close
            this.FormClosing += (s, e) =>
            {
                if (this.DialogResult != DialogResult.OK && this.DialogResult != DialogResult.Cancel)
                {
                    this.DialogResult = DialogResult.Cancel;
                }
            };
        }

        private void ApplyTheme()
        {
            this.BackColor = AppTheme.Background;
            this.Font = UiKit.T.Body;
        }

        private void Decision_CheckedChanged(object? sender, EventArgs e)
        {
            btnAccept.Enabled = rbAccept.Checked;
            if (rbAccept.Checked)
            {
                lblAcceptNote.ForeColor = AppTheme.Success;
            }
            else
            {
                lblAcceptNote.ForeColor = UiKit.T.InkMuted;
            }
        }

        private async Task LoadActiveTermsAsync()
        {
            try
            {
                _activeTerms = await _api.GetActiveTermsAsync();
                if (_activeTerms != null)
                {
                    lblTermsVersion.Text = $"{_activeTerms.Title}  ·  v{_activeTerms.VersionDisplay}";
                    txtTermsContent.Text = $"{_activeTerms.Title.ToUpperInvariant()}\n" +
                                           $"Version: {_activeTerms.VersionDisplay}\n" +
                                           $"Published: {_activeTerms.Version:MMMM dd, yyyy}\n\n" +
                                           new string('=', 60) + "\n\n" +
                                           _activeTerms.Content;
                }
                else
                {
                    lblTermsVersion.Text = "Standard Platform Agreement";
                    txtTermsContent.Text = "STANDARD SERVICE & LICENSE AGREEMENT\n\n" +
                                           "1. A service fee applies to all repairs and is quoted only after diagnosis.\n" +
                                           "2. Parts replaced during repair are covered by manufacturer warranty.\n" +
                                           "3. Labor warranty of 30 days applies on completed repairs.\n" +
                                           "4. Data recovery is attempted on a best-effort basis without guarantee.\n" +
                                           "5. Abandoned devices after 60 days of completion notification may be disposed of according to local laws.\n" +
                                           "6. Platform access and data retention are governed by the tenant subscription tier.";
                }
            }
            catch (Exception ex)
            {
                lblTermsVersion.Text = "Offline / Default Policy";
                txtTermsContent.Text = $"Could not fetch online terms: {ex.Message}\n\n" +
                                       "By using this software, you agree to comply with Fixory CRM master service rules and acceptable use policies.";
            }
        }

        private async Task OnAcceptAsync()
        {
            if (!rbAccept.Checked) return;

            btnAccept.Loading = true;
            btnDecline.Enabled = false;
            rbAccept.Enabled = false;
            rbDecline.Enabled = false;

            try
            {
                await _api.AcceptTermsAsync();

                UserSession.HasAcceptedTerms = true;
                UserSession.TermsAcceptedAt = DateTime.UtcNow;

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to save terms acceptance:\n\n{ex.Message}",
                    "Terms & Conditions", MessageBoxButtons.OK, MessageBoxIcon.Error);

                btnAccept.Loading = false;
                btnDecline.Enabled = true;
                rbAccept.Enabled = true;
                rbDecline.Enabled = true;
            }
        }

        private async Task OnDeclineAsync()
        {
            var result = MessageBox.Show(
                $"Are you sure you want to decline the Platform Terms & Conditions?\n\n" +
                $"Without accepting these terms, you cannot access or configure your CRM workspace.\n\n" +
                $"Declining will log you out and return to the sign-in screen.",
                "Decline Terms & Conditions",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                btnDecline.Loading = true;
                btnAccept.Enabled = false;

                try
                {
                    await _api.RejectTermsAsync();
                }
                catch
                {
                    // Ignore background log error
                }

                UserSession.Clear();
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }
    }
}
