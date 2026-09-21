using CRM.winforms;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Auth
{
    [DesignerCategory("Code")]
    public class LoginForm : Form
    {
        private readonly ApiClient _api = new ApiClient();

        private Panel pnlCard = null!;
        private Label lblBrand = null!;
        private Label lblTitle = null!;
        private Label lblSubtitle = null!;

        private Label lblUsername = null!;
        private Label lblPassword = null!;
        private Label lblError = null!;

        private TextField inpUsername = null!;
        private TextField inpPassword = null!;

        private Button btnLogin = null!;

        private Label lblDemoHint = null!;

        private const int CardWidth = 420;
        private const int CardHeight = 500;

        public LoginForm()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Sign in — Fixory CRM";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(CardWidth + 80, CardHeight + 80);
            BackColor = AppTheme.Background;
            Font = AppTheme.FontBody;

            pnlCard = new Panel
            {
                Size = new Size(CardWidth, CardHeight),
                BackColor = Color.Transparent
            };
            pnlCard.Paint += (s, e) =>
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                var rect = new Rectangle(0, 0, pnlCard.Width - 1, pnlCard.Height - 1);

                using (var path = UiKit.Rounded(rect, UiKit.Radius))
                using (var brush = new SolidBrush(AppTheme.Surface))
                    g.FillPath(brush, path);

                using (var path = UiKit.Rounded(rect, UiKit.Radius))
                using (var pen = new Pen(AppTheme.Border, 1))
                    g.DrawPath(pen, path);
            };

            Controls.Add(pnlCard);

            // Center the card on first layout and on resize
            void CenterCard()
            {
                pnlCard.Location = new Point(
                    (ClientSize.Width - pnlCard.Width) / 2,
                    (ClientSize.Height - pnlCard.Height) / 2);
            }
            this.Resize += (s, e) => CenterCard();
            this.Shown += (s, e) => CenterCard();

            int x = 40;
            int w = CardWidth - 80;
            int y = 40;

            // ── Brand ──
            lblBrand = new Label
            {
                Text = "FIXORY",
                Font = new Font("Segoe UI Semibold", 9F),
                ForeColor = AppTheme.Primary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
            pnlCard.Controls.Add(lblBrand);
            y += 28;

            // ── Title ──
            lblTitle = new Label
            {
                Text = "Welcome back",
                Font = new Font("Segoe UI Semibold", 20F),
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
            pnlCard.Controls.Add(lblTitle);
            y += 44;

            // ── Subtitle ──
            lblSubtitle = new Label
            {
                Text = "Sign in to your CRM workspace.",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
            pnlCard.Controls.Add(lblSubtitle);
            y += 40;

            // ── Username ──
            lblUsername = MakeLabel("Username", x, y);
            y += 20;
            inpUsername = new TextField
            {
                PlaceholderText = "e.g. staff",
                Location = new Point(x, y),
                Size = new Size(w, 38)
            };
            pnlCard.Controls.Add(inpUsername);
            y += 38 + 18;

            // ── Password ──
            lblPassword = MakeLabel("Password", x, y);
            y += 20;
            inpPassword = new TextField
            {
                PlaceholderText = "••••••••",
                Location = new Point(x, y),
                Size = new Size(w, 38)
            };
            inpPassword.InnerTextBox.UseSystemPasswordChar = true;
            pnlCard.Controls.Add(inpPassword);
            y += 38 + 8;

            // ── Error label ──
            lblError = new Label
            {
                Text = "",
                Font = AppTheme.FontError,
                ForeColor = AppTheme.Danger,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 20),
                Visible = false
            };
            pnlCard.Controls.Add(lblError);
            y += 28;

            // ── Login button ──
            btnLogin = new Button
            {
                Text = "Sign in",
                Font = new Font("Segoe UI Semibold", 10F),
                BackColor = AppTheme.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Location = new Point(x, y),
                Size = new Size(w, 42)
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.FlatAppearance.MouseOverBackColor = AppTheme.PrimaryHover;
            btnLogin.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(btnLogin, 8);
            btnLogin.Click += async (s, e) => await LoginAsync();
            pnlCard.Controls.Add(btnLogin);
            y += 42 + 24;

            // ── Demo hint ──
            lblDemoHint = new Label
            {
                Text = "Demo accounts:\n" +
                       "superadmin / SuperAdmin@123\n" +
                       "admin / Admin@123\n" +
                       "manager / Manager@123\n" +
                       "staff / Staff@123",
                Font = new Font("Segoe UI", 8F),
                ForeColor = AppTheme.TextMuted,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 80)
            };
            pnlCard.Controls.Add(lblDemoHint);

            // ── Enter key triggers login ──
            AcceptButton = btnLogin;

            // ── Focus on first field ──
            Shown += (s, e) => inpUsername.Focus();
        }

        private Label MakeLabel(string text, int x, int y)
        {
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 8.5F),
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
            pnlCard.Controls.Add(lbl);
            return lbl;
        }

        private async Task LoginAsync()
        {
            ClearError();

            var username = inpUsername.Text?.Trim() ?? "";
            var password = inpPassword.Text ?? "";

            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("Username is required.");
                inpUsername.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Password is required.");
                inpPassword.Focus();
                return;
            }

            btnLogin.Enabled = false;
            btnLogin.Text = "Signing in…";

            try
            {
                var result = await _api.LoginAsync(username, password);

                if (result == null)
                {
                    ShowError("Unexpected empty response from server.");
                    return;
                }

                // Populate session
                UserSession.UserId = result.UserId;
                UserSession.Username = result.Username;
                UserSession.FullName = result.FullName;
                UserSession.Email = result.Email;
                UserSession.Role = result.Role;
                UserSession.CompanyId = result.CompanyId;

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                btnLogin.Enabled = true;
                btnLogin.Text = "Sign in";
            }
        }

        private void ShowError(string message)
        {
            lblError.Text = "⚠  " + message;
            lblError.Visible = true;
        }

        private void ClearError()
        {
            lblError.Text = "";
            lblError.Visible = false;
        }
    }
}