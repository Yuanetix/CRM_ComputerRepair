using CRM.winforms;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace CRM.winforms.Auth
{
    [DesignerCategory("Code")]
    public class LoginForm : Form
    {
        private readonly ApiClient _api = new ApiClient();

        // ─────────────────────────────────────────────
        //  Layout constants
        // ─────────────────────────────────────────────
        private const int FormW = 1100;
        private const int FormH = 700;
        private const int BrandPanelW = 495;
        private const int FormColW = 420;
        private const int FormPad = 8;        // inner left/right margin for the form column

        // ─────────────────────────────────────────────
        //  Left (brand) panel
        // ─────────────────────────────────────────────
        private BufferedPanel pnlBrand = null!;

        // ─────────────────────────────────────────────
        //  Right (form) panel
        // ─────────────────────────────────────────────
        private BufferedPanel pnlRight = null!;
        private Panel pnlForm = null!;

        private Panel pnlLogoBadge = null!;
        private Label lblBrandName = null!;
        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private Label lblUsername = null!;
        private Label lblPassword = null!;
        private Label lblError = null!;
        private Label lblCaps = null!;
        private Label lblHelp = null!;

        private TextField inpUsername = null!;
        private TextField inpPassword = null!;

        // Show / Hide — now a real CheckBox + Label, right side of password field
        private CheckBox chkShowPassword = null!;
        private Label lblShowPassword = null!;

        private Button btnLogin = null!;

        public LoginForm()
        {
            BuildUi();
        }

        // ─────────────────────────────────────────────
        //  UI
        // ─────────────────────────────────────────────
        private void BuildUi()
        {
            Text = "Sign in — Fixory CRM";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(FormW, FormH);
            BackColor = UiKit.Canvas;
            Font = UiKit.Body;
            DoubleBuffered = true;

            BuildBrandPanel();
            BuildFormPanel();

            Controls.Add(pnlRight);
            Controls.Add(pnlBrand);

            AcceptButton = btnLogin;
            Shown += (s, e) => inpUsername.Focus();
        }

        // ─────────────────────────────────────────────
        //  Left: brand panel
        // ─────────────────────────────────────────────
        private void BuildBrandPanel()
        {
            pnlBrand = new BufferedPanel
            {
                Dock = DockStyle.Left,
                Width = BrandPanelW,
                BackColor = UiKit.Accent
            };

            pnlBrand.Paint += (s, e) =>
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                var rect = pnlBrand.ClientRectangle;

                using (var brush = new LinearGradientBrush(
                    rect,
                    UiKit.Mix(UiKit.Accent, Color.White, 0.06),
                    UiKit.Mix(UiKit.Accent, Color.Black, 0.08),
                    LinearGradientMode.Vertical))
                {
                    g.FillRectangle(brush, rect);
                }

                using (var b = new SolidBrush(Color.FromArgb(20, Color.White)))
                {
                    g.FillEllipse(b, -140, -140, 380, 380);
                    g.FillEllipse(b, pnlBrand.Width - 220, pnlBrand.Height - 240, 420, 420);
                }
                using (var b = new SolidBrush(Color.FromArgb(12, Color.White)))
                {
                    g.FillEllipse(b, pnlBrand.Width - 160, 110, 220, 220);
                    g.FillEllipse(b, 40, pnlBrand.Height - 200, 180, 180);
                }
                using (var pen = new Pen(Color.FromArgb(28, Color.White), 1.2f))
                {
                    g.DrawEllipse(pen, pnlBrand.Width - 260, 60, 300, 300);
                }
            };

            var softText = Color.FromArgb(210, 225, 245);

            pnlLogoBadge = new Panel
            {
                Size = new Size(46, 46),
                Location = new Point(56, 56),
                BackColor = Color.FromArgb(40, Color.White)
            };
            pnlLogoBadge.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(pnlLogoBadge, 12);
            pnlLogoBadge.Paint += (s, e) =>
            {
                var g = e.Graphics;
                UiKit.Quality(g);
                using (var f = new Font("Segoe UI Semibold", 16F))
                using (var b = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString("F", f, b,
                        new RectangleF(0, 0, pnlLogoBadge.Width, pnlLogoBadge.Height), sf);
                }
            };
            pnlBrand.Controls.Add(pnlLogoBadge);

            lblBrandName = new Label
            {
                Text = "FIXORY CRM",
                Font = new Font("Segoe UI Semibold", 11F),
                ForeColor = Color.White,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(116, 68)
            };
            pnlBrand.Controls.Add(lblBrandName);

            var lblHeadline = new Label
            {
                Text = "Manage your business\r\nwith confidence.",
                Font = new Font("Segoe UI Semibold", 26F),
                ForeColor = Color.White,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(56, 240),
                Size = new Size(BrandPanelW - 112, 110),
                TextAlign = ContentAlignment.TopLeft,
                UseCompatibleTextRendering = false
            };
            pnlBrand.Controls.Add(lblHeadline);

            var lblTagline = new Label
            {
                Text = "Customers, deals and follow-ups —\r\norganised, searchable and always in sync.",
                Font = new Font("Segoe UI", 10.5F),
                ForeColor = softText,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(56, 372),
                Size = new Size(BrandPanelW - 96, 70),
                TextAlign = ContentAlignment.TopLeft,
                UseCompatibleTextRendering = false
            };
            pnlBrand.Controls.Add(lblTagline);

            var lblFooter = new Label
            {
                Text = "© " + DateTime.Now.Year + " Fixory CRM. All rights reserved.",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(170, 200, 235),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(56, FormH - 56)
            };
            pnlBrand.Controls.Add(lblFooter);
        }

        // ─────────────────────────────────────────────
        //  Right: sign-in form
        // ─────────────────────────────────────────────
        private void BuildFormPanel()
        {
            pnlRight = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = UiKit.Canvas
            };

            // The form column is wider than FormColW to account for FormPad on both sides
            pnlForm = new Panel
            {
                Size = new Size(FormColW + FormPad * 2, 580),
                BackColor = Color.Transparent
            };
            pnlRight.Controls.Add(pnlForm);

            void CenterForm() =>
                pnlForm.Location = new Point(
                    Math.Max(0, (pnlRight.ClientSize.Width - pnlForm.Width) / 2),
                    Math.Max(0, (pnlRight.ClientSize.Height - pnlForm.Height) / 2));

            pnlRight.Resize += (s, e) => CenterForm();
            Shown += (s, e) => CenterForm();

            // Content x-offset and content width after applying FormPad on both sides
            int x = FormPad;
            int w = FormColW;
            int y = 0;

            // ── Heading ──
            lblTitle = new Label
            {
                Text = "Welcome back",
                Font = new Font("Segoe UI Semibold", 24F),
                ForeColor = UiKit.Ink,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 44),
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
            };
            pnlForm.Controls.Add(lblTitle);
            y += 44;

            // ── Subtitle ──
            lblSubtitle = new Label
            {
                Text = "Sign in to your account to continue.",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.InkMuted,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
            };
            pnlForm.Controls.Add(lblSubtitle);
            y += 22 + 36;

            // ── Username ──
            lblUsername = MakeLabel("Username", x, y);
            y += 24;

            inpUsername = new TextField
            {
                PlaceholderText = "Enter your username",
                Location = new Point(x, y),
                Size = new Size(w, 50),
                TabIndex = 0
            };
            // Inner margin so typed text doesn't touch the border
            inpUsername.InnerTextBox.Margin = new Padding(FormPad, 0, FormPad, 0);
            inpUsername.InnerTextBox.Padding = new Padding(FormPad, 0, FormPad, 0);
            pnlForm.Controls.Add(inpUsername);
            y += 50 + 22;

            // ── Password ──
            lblPassword = MakeLabel("Password", x, y);
            y += 24;

            inpPassword = new TextField
            {
                PlaceholderText = "••••••••",
                Location = new Point(x, y),
                Size = new Size(w, 50),
                TabIndex = 1
            };
            inpPassword.InnerTextBox.UseSystemPasswordChar = true;
            inpPassword.InnerTextBox.Margin = new Padding(FormPad, 0, FormPad, 0);
            inpPassword.InnerTextBox.Padding = new Padding(FormPad, 0, FormPad, 0);
            pnlForm.Controls.Add(inpPassword);

            // ── Show password: CheckBox + Label, right side BELOW the field ──
            // Positioned just under the password input, aligned to the right edge.
            int rowBelowPwd = inpPassword.Bottom + 6;

            chkShowPassword = new CheckBox
            {
                Appearance = Appearance.Normal,
                AutoSize = false,
                Size = new Size(16, 16),
                Location = new Point(x + w - 130, rowBelowPwd + 1),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabIndex = 3,
                Text = string.Empty
            };
            // Flat, modern checkbox look
            chkShowPassword.FlatStyle = FlatStyle.Flat;
            chkShowPassword.ForeColor = UiKit.InkMuted;
            chkShowPassword.CheckedChanged += (s, e) => TogglePasswordVisibility();
            pnlForm.Controls.Add(chkShowPassword);

            lblShowPassword = new Label
            {
                Text = "Show password",
                Font = UiKit.Small,
                ForeColor = UiKit.InkMuted,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(chkShowPassword.Right + 6, rowBelowPwd),
                Size = new Size(110, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand,
                UseCompatibleTextRendering = false
            };
            // Clicking the label toggles the checkbox
            lblShowPassword.Click += (s, e) => chkShowPassword.Checked = !chkShowPassword.Checked;
            pnlForm.Controls.Add(lblShowPassword);

            // Move past the checkbox row
            y = rowBelowPwd + 20 + 10;

            // ── Status row (error + caps-lock) ──
            lblError = new Label
            {
                Text = "",
                Font = UiKit.Small,
                ForeColor = UiKit.Danger,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 22),
                Visible = false,
                UseCompatibleTextRendering = false
            };
            pnlForm.Controls.Add(lblError);

            lblCaps = new Label
            {
                Text = "Caps Lock is on",
                Font = UiKit.Small,
                ForeColor = UiKit.InkMuted,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 22),
                Visible = false,
                UseCompatibleTextRendering = false
            };
            pnlForm.Controls.Add(lblCaps);
            y += 22 + 14;

            // ── Primary CTA ──
            btnLogin = new Button
            {
                Text = "Sign In",
                Font = new Font("Segoe UI Semibold", 10.5F),
                BackColor = UiKit.Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Location = new Point(x, y),
                Size = new Size(w, 50),
                TabIndex = 2
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.FlatAppearance.MouseOverBackColor = UiKit.AccentHover;
            btnLogin.FlatAppearance.MouseDownBackColor = UiKit.AccentActive;
            btnLogin.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(btnLogin, UiKit.RadiusSm);
            btnLogin.Click += async (s, e) => await LoginAsync();
            pnlForm.Controls.Add(btnLogin);
            y += 50 + 26;

            // ── Help text ──
            lblHelp = new Label
            {
                Text = "Need access? Contact your administrator.",
                Font = UiKit.Small,
                ForeColor = UiKit.InkMuted,
                AutoSize = false,
                TextAlign = ContentAlignment.TopCenter,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 20),
                UseCompatibleTextRendering = false
            };
            pnlForm.Controls.Add(lblHelp);

            WireInteractions();
        }

        private Label MakeLabel(string text, int x, int y)
        {
            var lbl = new Label
            {
                Text = text,
                Font = UiKit.SmallStrong,
                ForeColor = UiKit.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
            pnlForm.Controls.Add(lbl);
            return lbl;
        }

        // ─────────────────────────────────────────────
        //  Interaction helpers
        // ─────────────────────────────────────────────
        private void WireInteractions()
        {
            inpUsername.InnerTextBox.TextChanged += (s, e) => ClearError();
            inpPassword.InnerTextBox.TextChanged += (s, e) => ClearError();

            inpPassword.InnerTextBox.GotFocus += (s, e) => UpdateCapsWarning();
            inpPassword.InnerTextBox.KeyUp += (s, e) => UpdateCapsWarning();
            inpPassword.InnerTextBox.LostFocus += (s, e) => lblCaps.Visible = false;
        }

        private void TogglePasswordVisibility()
        {
            // Checkbox checked = show password (i.e. NOT use SystemPasswordChar)
            inpPassword.InnerTextBox.UseSystemPasswordChar = !chkShowPassword.Checked;
            inpPassword.Focus();
        }

        private void UpdateCapsWarning()
        {
            lblCaps.Visible = !lblError.Visible
                              && inpPassword.InnerTextBox.Focused
                              && Control.IsKeyLocked(Keys.CapsLock);
        }

        private void SetBusy(bool busy)
        {
            btnLogin.Enabled = !busy;
            btnLogin.Text = busy ? "Signing in…" : "Sign In";
            inpUsername.Enabled = !busy;
            inpPassword.Enabled = !busy;
            chkShowPassword.Enabled = !busy;
            lblShowPassword.Enabled = !busy;
            UseWaitCursor = busy;
        }

        // ─────────────────────────────────────────────
        //  Login — LOGIC UNCHANGED
        // ─────────────────────────────────────────────
        private async Task LoginAsync()
        {
            ClearError();

            var username = inpUsername.Text?.Trim() ?? "";
            var password = inpPassword.Text ?? "";

            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("Please enter your username.");
                inpUsername.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter your password.");
                inpPassword.Focus();
                return;
            }

            SetBusy(true);
            bool failed = false;

            try
            {
                var result = await _api.LoginAsync(username, password);

                if (result == null)
                {
                    failed = true;
                    ShowError("No response from the server. Please try again.");
                    return;
                }

                UserSession.UserId = result.UserId;
                UserSession.Username = result.Username;
                UserSession.FullName = result.FullName;
                UserSession.Email = result.Email;
                UserSession.Role = result.Role;
                UserSession.CompanyId = result.CompanyId;
                UserSession.Token = result.Token;

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                failed = true;
                ShowError(ex.Message);
            }
            finally
            {
                SetBusy(false);

                if (failed && !IsDisposed)
                {
                    inpPassword.Focus();
                    inpPassword.InnerTextBox.SelectAll();
                }
            }
        }

        private void ShowError(string message)
        {
            lblCaps.Visible = false;
            lblError.Text = message;
            lblError.Visible = true;
        }

        private void ClearError()
        {
            if (!lblError.Visible) return;
            lblError.Text = "";
            lblError.Visible = false;
            UpdateCapsWarning();
        }

        private sealed class BufferedPanel : Panel
        {
            public BufferedPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }
        }
    }
}