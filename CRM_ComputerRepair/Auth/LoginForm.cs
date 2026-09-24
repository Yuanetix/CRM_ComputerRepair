using CRM.winforms;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
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
        private const int BrandPad = 56;         // left/right margin of every brand-panel text
        private const int FormColW = 420;
        private const int FormPad = 8;           // inner left/right margin for the form column
        private const int FieldH = 52;           // input + button height
        private const int TextInset = 12;        // space between the input border and the typed text

        // ─────────────────────────────────────────────
        //  Native: real left/right text margin inside a TextBox
        //  (TextBox ignores Padding/Margin, this is the supported way)
        // ─────────────────────────────────────────────
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int EM_SETMARGINS = 0xD3;
        private const int EC_LEFTMARGIN = 0x1;
        private const int EC_RIGHTMARGIN = 0x2;

        private static void ApplyTextInset(TextBox tb, int px)
        {
            void Apply()
            {
                if (!tb.IsHandleCreated) return;
                SendMessage(tb.Handle, EM_SETMARGINS,
                    (IntPtr)(EC_LEFTMARGIN | EC_RIGHTMARGIN),
                    (IntPtr)((px << 16) | px));
            }

            // Re-applied whenever the handle is recreated (e.g. show/hide password).
            tb.HandleCreated += (s, e) => Apply();
            Apply();
        }

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

        // Show / Hide — a real CheckBox + Label under the password field
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

            int textW = BrandPanelW - BrandPad * 2;      // one shared text width → clean, equal margins
            int featureY = 452;                          // first feature row
            const int featureStep = 42;

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

                // Feature check marks (drawn, so no font-glyph dependency)
                for (int i = 0; i < 3; i++)
                {
                    int cy = featureY + i * featureStep;
                    var circle = new Rectangle(BrandPad, cy, 24, 24);
                    using (var fill = new SolidBrush(Color.FromArgb(46, Color.White)))
                        g.FillEllipse(fill, circle);
                    using var tick = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                    g.DrawLines(tick, new[]
                    {
                        new PointF(circle.Left + 7f,  circle.Top + 12.5f),
                        new PointF(circle.Left + 10.5f, circle.Top + 16f),
                        new PointF(circle.Left + 17f, circle.Top + 8.5f)
                    });
                }
            };

            var softText = Color.FromArgb(215, 228, 246);

            pnlLogoBadge = new Panel
            {
                Size = new Size(46, 46),
                Location = new Point(BrandPad, BrandPad),
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
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(BrandPad + 46 + 14, BrandPad),
                Size = new Size(textW - 60, 46),
                TextAlign = ContentAlignment.MiddleLeft,       // vertically centred on the badge
                UseCompatibleTextRendering = false
            };
            pnlBrand.Controls.Add(lblBrandName);

            var lblHeadline = new Label
            {
                Text = "Manage your business\r\nwith confidence.",
                Font = new Font("Segoe UI Semibold", 26F),
                ForeColor = Color.White,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(BrandPad, 214),
                Size = new Size(textW, 104),
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
                Location = new Point(BrandPad, 336),
                Size = new Size(textW, 60),
                TextAlign = ContentAlignment.TopLeft,
                UseCompatibleTextRendering = false
            };
            pnlBrand.Controls.Add(lblTagline);

            // Feature list (text; check marks are painted above)
            string[] features =
            {
                "All your customers in one place",
                "Never miss a follow-up",
                "Live reports and insights"
            };
            for (int i = 0; i < features.Length; i++)
            {
                pnlBrand.Controls.Add(new Label
                {
                    Text = features[i],
                    Font = new Font("Segoe UI", 10F),
                    ForeColor = Color.White,
                    AutoSize = false,
                    BackColor = Color.Transparent,
                    Location = new Point(BrandPad + 24 + 12, featureY + i * featureStep),
                    Size = new Size(textW - 36, 24),
                    TextAlign = ContentAlignment.MiddleLeft,
                    UseCompatibleTextRendering = false
                });
            }

            var lblFooter = new Label
            {
                Text = "© " + DateTime.Now.Year + " Fixory CRM. All rights reserved.",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(185, 210, 240),
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(BrandPad, FormH - BrandPad - 12),
                Size = new Size(textW, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
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
                Size = new Size(FormColW + FormPad * 2, 500),
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
                Size = new Size(w, 46),
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
            };
            pnlForm.Controls.Add(lblTitle);
            y += 46;

            // ── Subtitle ──
            lblSubtitle = new Label
            {
                Text = "Sign in to your account to continue.",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.InkMuted,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
            };
            pnlForm.Controls.Add(lblSubtitle);
            y += 24 + 34;

            // ── Username ──
            lblUsername = MakeLabel("Username", x, y);
            y += 28;

            inpUsername = new TextField
            {
                PlaceholderText = "Enter your username",
                Location = new Point(x, y),
                Size = new Size(w, FieldH),
                TabIndex = 0
            };
            ApplyTextInset(inpUsername.InnerTextBox, TextInset);
            pnlForm.Controls.Add(inpUsername);
            y += FieldH + 22;

            // ── Password ──
            lblPassword = MakeLabel("Password", x, y);
            y += 28;

            inpPassword = new TextField
            {
                PlaceholderText = "Enter your password",
                Location = new Point(x, y),
                Size = new Size(w, FieldH),
                TabIndex = 1
            };
            inpPassword.InnerTextBox.UseSystemPasswordChar = true;
            ApplyTextInset(inpPassword.InnerTextBox, TextInset);
            pnlForm.Controls.Add(inpPassword);

            // Label turns darker while its field has focus (clear "where am I" feedback)
            inpUsername.InnerTextBox.GotFocus += (s, e) => lblUsername.ForeColor = UiKit.Ink;
            inpUsername.InnerTextBox.LostFocus += (s, e) => lblUsername.ForeColor = UiKit.InkMuted;
            inpPassword.InnerTextBox.GotFocus += (s, e) => lblPassword.ForeColor = UiKit.Ink;
            inpPassword.InnerTextBox.LostFocus += (s, e) => lblPassword.ForeColor = UiKit.InkMuted;

            // ── Row under the password field:  [ ] Show password ........ Caps Lock is on ──
            int rowBelowPwd = inpPassword.Bottom + 12;

            chkShowPassword = new CheckBox
            {
                Appearance = Appearance.Normal,
                AutoSize = false,
                Size = new Size(18, 18),
                Location = new Point(x + 2, rowBelowPwd + 2),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabIndex = 3,
                Text = string.Empty
            };
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
                Location = new Point(chkShowPassword.Right + 8, rowBelowPwd),
                Size = new Size(130, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand,
                UseCompatibleTextRendering = false
            };
            // Clicking the label toggles the checkbox
            lblShowPassword.Click += (s, e) => chkShowPassword.Checked = !chkShowPassword.Checked;
            pnlForm.Controls.Add(lblShowPassword);

            lblCaps = new Label
            {
                Text = "Caps Lock is on",
                Font = UiKit.SmallStrong,
                ForeColor = Color.FromArgb(180, 83, 9),
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x + w - 170, rowBelowPwd),
                Size = new Size(170, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Visible = false,
                UseCompatibleTextRendering = false
            };
            pnlForm.Controls.Add(lblCaps);

            y = rowBelowPwd + 22 + 12;

            // ── Error banner (space is always reserved, so the layout never jumps) ──
            lblError = new Label
            {
                Text = "",
                Font = UiKit.Small,
                ForeColor = UiKit.Danger,
                BackColor = Color.FromArgb(254, 242, 242),
                AutoSize = false,
                Location = new Point(x, y),
                Size = new Size(w, 46),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 4, 14, 4),           // text margin inside the banner
                Visible = false,
                UseCompatibleTextRendering = false
            };
            UiHelpers.ApplyRoundedRegion(lblError, 10);
            pnlForm.Controls.Add(lblError);
            y += 46 + 16;

            // ── Primary CTA ──
            btnLogin = new Button
            {
                Text = "Sign In",
                Font = new Font("Segoe UI Semibold", 11F),
                BackColor = UiKit.Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Location = new Point(x, y),
                Size = new Size(w, FieldH),
                TabIndex = 2
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.FlatAppearance.MouseOverBackColor = UiKit.AccentHover;
            btnLogin.FlatAppearance.MouseDownBackColor = UiKit.AccentActive;
            btnLogin.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(btnLogin, UiKit.RadiusSm);
            btnLogin.Click += async (s, e) => await LoginAsync();
            pnlForm.Controls.Add(btnLogin);
            y += FieldH + 24;

            // ── Help text ──
            lblHelp = new Label
            {
                Text = "Need access? Contact your administrator.",
                Font = UiKit.Small,
                ForeColor = UiKit.InkMuted,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 22),
                UseCompatibleTextRendering = false
            };
            pnlForm.Controls.Add(lblHelp);
            y += 22;

            pnlForm.Height = y + 4;      // fit the content so it is centred exactly

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