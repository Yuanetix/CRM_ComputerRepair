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

        // ─────────────────────────────────────────────────────────────────
        //  Layout constants — Increased dimensions for a spacious, modern portal
        // ─────────────────────────────────────────────────────────────────
        private const int DefaultFormW  = 1240;
        private const int DefaultFormH  = 800;
        private const int BrandPanelW   = 520;     // Branded left panel (~42% width)
        private const int FormColW      = 460;     // Login form column width
        private const int FieldH        = 50;      // Spacious input height
        private const int FieldGap      = 18;      // Space between field groups

        // ─────────────────────────────────────────────────────────────────
        //  Win32: Apply native left/right text inset inside TextBox
        // ─────────────────────────────────────────────────────────────────
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int EM_SETMARGINS  = 0xD3;
        private const int EC_LEFTMARGIN  = 0x1;
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
            tb.HandleCreated += (s, e) => Apply();
            Apply();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Panels
        // ─────────────────────────────────────────────────────────────────
        private BufferedPanel pnlBrand = null!;
        private BufferedPanel pnlRight = null!;
        private Panel pnlFormContainer = null!;

        // ─────────────────────────────────────────────────────────────────
        //  Right Panel Controls
        // ─────────────────────────────────────────────────────────────────
        private Label lblTitle = null!;
        private Label lblSubtitle = null!;

        private Label lblCompanyId = null!;
        private Label lblUsername = null!;
        private Label lblPassword = null!;

        private TextField inpCompanyId = null!;
        private TextField inpUsername = null!;
        private TextField inpPassword = null!;

        private CheckBox chkShowPassword = null!;
        private Label lblShowPassword = null!;
        private Label lblCaps = null!;

        private Label lblError = null!;
        private Button btnLogin = null!;
        private Label lblHelp = null!;

        public LoginForm()
        {
            BuildUi();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Root Window Setup
        // ─────────────────────────────────────────────────────────────────
        private void BuildUi()
        {
            Text = "Sign in — Fixory CRM Enterprise Portal";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;

            // Responsive sizing to ensure it fits comfortably on any screen while honoring the size increase
            var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            int targetW = DefaultFormW;
            int targetH = DefaultFormH;
            if (workingArea.Height < 840)
            {
                targetH = Math.Min(780, workingArea.Height - 30);
                targetW = Math.Min(1200, workingArea.Width - 40);
            }
            ClientSize = new Size(targetW, targetH);

            BackColor = Color.White;
            Font = UiKit.Body;
            DoubleBuffered = true;

            BuildBrandPanel();
            BuildRightPanel();

            Controls.Add(pnlRight);
            Controls.Add(pnlBrand);

            AcceptButton = btnLogin;

            Shown += (s, e) =>
            {
                CenterFormContainer();
                if (string.IsNullOrWhiteSpace(inpCompanyId.Text))
                    inpCompanyId.Focus();
                else
                    inpUsername.Focus();
            };
        }

        // ─────────────────────────────────────────────────────────────────
        //  Left Branded Panel (Tech-blue gradient, dot matrix, vector features)
        // ─────────────────────────────────────────────────────────────────
        private void BuildBrandPanel()
        {
            pnlBrand = new BufferedPanel
            {
                Dock = DockStyle.Left,
                Width = BrandPanelW,
                BackColor = Color.FromArgb(10, 110, 235)
            };

            const int brandPadX = 52;
            int brandTextW = BrandPanelW - brandPadX * 2;

            // 5 Features customized for Computer Repair CRM
            string[] featureTitles =
            {
                "Data Analytics & Insights",
                "Work Orders & Scheduling",
                "Diagnostics & Device Tracking",
                "Customer Retention & CRM",
                "Invoicing & Payments"
            };

            pnlBrand.Paint += (s, e) =>
            {
                var g = e.Graphics;
                UiKit.Quality(g);
                var bounds = pnlBrand.ClientRectangle;

                // ── 1. Vibrant Tech-Blue Gradient (matching modern enterprise portals) ──
                using (var bgBrush = new LinearGradientBrush(
                    bounds,
                    Color.FromArgb(11, 115, 238),   // Radiant electric blue at top
                    Color.FromArgb(2, 45, 130),     // Deep royal blue at bottom
                    LinearGradientMode.Vertical))
                {
                    g.FillRectangle(bgBrush, bounds);
                }

                // ── 2. Top-Left Circular Dot Grid Matrix (8 cols x 6 rows) ──
                using (var dotBrush = new SolidBrush(Color.FromArgb(45, 255, 255, 255)))
                {
                    int dotCols = 8, dotRows = 6;
                    int startX = brandPadX, startY = 44;
                    int dotGap = 16;
                    for (int r = 0; r < dotRows; r++)
                    {
                        for (int c = 0; c < dotCols; c++)
                        {
                            g.FillEllipse(dotBrush, startX + c * dotGap, startY + r * dotGap, 4, 4);
                        }
                    }
                }

                // ── 3. Bottom-Right Subtle Dot Grid Matrix ──
                using (var dotBrush2 = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
                {
                    int dotCols = 8, dotRows = 6;
                    int startX = pnlBrand.Width - 170, startY = pnlBrand.Height - 210;
                    int dotGap = 15;
                    for (int r = 0; r < dotRows; r++)
                    {
                        for (int c = 0; c < dotCols; c++)
                        {
                            g.FillEllipse(dotBrush2, startX + c * dotGap, startY + r * dotGap, 3.5f, 3.5f);
                        }
                    }
                }

                // ── 4. Organic Flowing Geometric Waves at Bottom ──
                using (var waveBrush1 = new SolidBrush(Color.FromArgb(16, 255, 255, 255)))
                {
                    using var path = new GraphicsPath();
                    path.AddBezier(0, pnlBrand.Height - 165,
                                   pnlBrand.Width * 0.35f, pnlBrand.Height - 225,
                                   pnlBrand.Width * 0.70f, pnlBrand.Height - 110,
                                   pnlBrand.Width, pnlBrand.Height - 150);
                    path.AddLine(pnlBrand.Width, pnlBrand.Height, 0, pnlBrand.Height);
                    path.CloseFigure();
                    g.FillPath(waveBrush1, path);
                }

                using (var waveBrush2 = new SolidBrush(Color.FromArgb(24, 56, 189, 248))) // Soft cyan-blue overlay
                {
                    using var path = new GraphicsPath();
                    path.AddBezier(0, pnlBrand.Height - 90,
                                   pnlBrand.Width * 0.40f, pnlBrand.Height - 145,
                                   pnlBrand.Width * 0.75f, pnlBrand.Height - 65,
                                   pnlBrand.Width, pnlBrand.Height - 95);
                    path.AddLine(pnlBrand.Width, pnlBrand.Height, 0, pnlBrand.Height);
                    path.CloseFigure();
                    g.FillPath(waveBrush2, path);
                }

                // ── 5. Render Translucent Badges and Clean Vector Icons (Zero Emojis) ──
                int featureStartY = 330;
                int featureStep = 52;
                for (int i = 0; i < featureTitles.Length; i++)
                {
                    int cy = featureStartY + i * featureStep;
                    var badgeRect = new Rectangle(brandPadX, cy, 34, 34);

                    // Badge fill + outline
                    using (var badgeFill = new SolidBrush(Color.FromArgb(35, 255, 255, 255)))
                        UiKit.FillRounded(g, badgeRect, 10, Color.FromArgb(35, 255, 255, 255));

                    using (var badgeStroke = new Pen(Color.FromArgb(60, 255, 255, 255), 1.2f))
                        UiKit.StrokeRounded(g, badgeRect, 10, Color.FromArgb(60, 255, 255, 255), 1.2f);

                    // Crisp geometric vector icon
                    DrawFeatureIcon(g, i, badgeRect);
                }
            };

            // ── Big Bold Portal Headline ──
            var lblHeadline = new Label
            {
                Text = "Fixory\r\nComputer Repair CRM",
                Font = AppFonts.Strong(28F),
                ForeColor = Color.White,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(brandPadX, 160),
                UseCompatibleTextRendering = false
            };
            pnlBrand.Controls.Add(lblHeadline);

            // ── Feature Text Labels (Auto-sized so text is never cut) ──
            int featureStartY = 310;
            int featureStep = 52;
            for (int i = 0; i < featureTitles.Length; i++)
            {
                var lblFeature = new Label
                {
                    Text = featureTitles[i],
                    Font = AppFonts.Regular(11F),
                    ForeColor = Color.White,
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Location = new Point(brandPadX + 34 + 16, featureStartY + i * featureStep + 5),
                    UseCompatibleTextRendering = false
                };
                pnlBrand.Controls.Add(lblFeature);
            }

            // ── Left Panel Bottom Footer ──
            var lblBrandFooter = new Label
            {
                Text = "Fixory \u00b7 Computer Repair CRM",
                Font = AppFonts.Regular(8.5F),
                ForeColor = Color.FromArgb(180, 215, 255),
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(brandPadX, pnlBrand.Height - 50),
                Size = new Size(brandTextW, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
            };
            pnlBrand.Resize += (s, e) =>
            {
                lblBrandFooter.Location = new Point(brandPadX, pnlBrand.Height - 50);
            };
            pnlBrand.Controls.Add(lblBrandFooter);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Crisp GDI+ Vector Icon Renderer (No Emojis, Sharp at all DPIs)
        // ─────────────────────────────────────────────────────────────────
        private static void DrawFeatureIcon(Graphics g, int index, Rectangle badgeRect)
        {
            using var pen = new Pen(Color.White, 1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            using var fillBrush = new SolidBrush(Color.White);

            float cx = badgeRect.Left + badgeRect.Width / 2f;
            float cy = badgeRect.Top + badgeRect.Height / 2f;

            switch (index)
            {
                case 0: // Data Analytics (3 ascending bars)
                    g.FillRectangle(fillBrush, cx - 7.5f, cy + 2f, 3f, 6f);
                    g.FillRectangle(fillBrush, cx - 1.5f, cy - 2.5f, 3f, 10.5f);
                    g.FillRectangle(fillBrush, cx + 4.5f, cy - 7f, 3f, 15f);
                    break;

                case 1: // Work Orders & Scheduling (Calendar)
                    var calRect = new RectangleF(cx - 7.5f, cy - 6.5f, 15f, 13f);
                    using (var calPen = new Pen(Color.White, 1.5f))
                    {
                        g.DrawRectangle(calPen, calRect.X, calRect.Y, calRect.Width, calRect.Height);
                        g.DrawLine(calPen, calRect.Left, calRect.Top + 4f, calRect.Right, calRect.Top + 4f);
                        g.DrawLine(pen, cx - 4f, cy - 8.5f, cx - 4f, cy - 6.5f);
                        g.DrawLine(pen, cx + 4f, cy - 8.5f, cx + 4f, cy - 6.5f);
                    }
                    g.FillEllipse(fillBrush, cx - 3.5f, cy + 1f, 2f, 2f);
                    g.FillEllipse(fillBrush, cx + 1.5f, cy + 1f, 2f, 2f);
                    break;

                case 2: // Diagnostics & Device Tracking (Diagnostic Wrench / Tool)
                    g.DrawLine(pen, cx - 5.5f, cy + 5.5f, cx + 2f, cy - 2f);
                    g.DrawArc(pen, cx - 1f, cy - 8f, 8f, 8f, 45, 270);
                    g.FillEllipse(fillBrush, cx - 7f, cy + 5f, 3.5f, 3.5f);
                    break;

                case 3: // Customer Retention & CRM (Customer Profile Silhouette)
                    g.DrawEllipse(pen, cx - 3.5f, cy - 7f, 7f, 7f);
                    g.DrawArc(pen, cx - 7f, cy - 0.5f, 14f, 11f, 200, 140);
                    break;

                case 4: // Invoicing & Payments (Document / Card)
                    var docRect = new RectangleF(cx - 6f, cy - 7.5f, 12f, 15f);
                    using (var docPen = new Pen(Color.White, 1.5f))
                    {
                        g.DrawRectangle(docPen, docRect.X, docRect.Y, docRect.Width, docRect.Height);
                        g.DrawLine(docPen, docRect.Left + 2.5f, docRect.Top + 4.5f, docRect.Right - 2.5f, docRect.Top + 4.5f);
                        g.DrawLine(docPen, docRect.Left + 2.5f, docRect.Top + 8f, docRect.Right - 4f, docRect.Top + 8f);
                        g.DrawLine(docPen, docRect.Left + 2.5f, docRect.Top + 11.5f, docRect.Right - 2.5f, docRect.Top + 11.5f);
                    }
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Right Panel (Clean white canvas, perfectly centered form container)
        // ─────────────────────────────────────────────────────────────────
        private void BuildRightPanel()
        {
            pnlRight = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            pnlFormContainer = new Panel
            {
                Width = FormColW,
                BackColor = Color.Transparent
            };
            pnlRight.Controls.Add(pnlFormContainer);

            pnlRight.Resize += (s, e) => CenterFormContainer();

            BuildFormContent();
        }

        private void CenterFormContainer()
        {
            if (pnlFormContainer == null || pnlRight == null) return;
            pnlFormContainer.Location = new Point(
                Math.Max(30, (pnlRight.ClientSize.Width - pnlFormContainer.Width) / 2),
                Math.Max(20, (pnlRight.ClientSize.Height - pnlFormContainer.Height) / 2));
        }

        // ─────────────────────────────────────────────────────────────────
        //  Form Content Layout (Company ID, Username, Password, Login button)
        // ─────────────────────────────────────────────────────────────────
        private void BuildFormContent()
        {
            int w = FormColW;
            int y = 0;

            // ── Main Heading "Login" (Direct task focus, no redundant titles) ──
            lblTitle = new Label
            {
                Text = "Login",
                Font = AppFonts.Strong(28F),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(0, y)
            };
            pnlFormContainer.Controls.Add(lblTitle);
            y = lblTitle.Bottom + 8;

            // ── Subtitle (User guidance) ──
            lblSubtitle = new Label
            {
                Text = "Enter your company and account credentials to sign in.",
                Font = AppFonts.Regular(10F),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(0, y)
            };
            pnlFormContainer.Controls.Add(lblSubtitle);
            y = lblSubtitle.Bottom + 28;

            // ── Field 1: Company ID ──
            y = CreateFieldGroup("Company ID", "e.g. 1",
                                 out lblCompanyId, out inpCompanyId,
                                 w, y, 0, isPassword: false);
            inpCompanyId.Text = "1";

            // ── Field 2: Username ──
            y = CreateFieldGroup("Username", "Enter your username",
                                 out lblUsername, out inpUsername,
                                 w, y, 1, isPassword: false);

            // ── Field 3: Password ──
            y = CreateFieldGroup("Password", "Enter your password",
                                 out lblPassword, out inpPassword,
                                 w, y, 2, isPassword: true);

            // Interactive focus label highlights
            WireFocusFeedback(inpCompanyId, lblCompanyId);
            WireFocusFeedback(inpUsername, lblUsername);
            WireFocusFeedback(inpPassword, lblPassword);

            // ── Row under Password: [ ] Show password ........ Caps Lock is ON ──
            int pwdRowY = y + 2;

            chkShowPassword = new CheckBox
            {
                Appearance = Appearance.Normal,
                AutoSize = false,
                Size = new Size(18, 18),
                Location = new Point(2, pwdRowY + 2),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabIndex = 3,
                Text = string.Empty,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            chkShowPassword.CheckedChanged += (s, e) => TogglePasswordVisibility();
            pnlFormContainer.Controls.Add(chkShowPassword);

            lblShowPassword = new Label
            {
                Text = "Show password",
                Font = AppFonts.Regular(9.5F),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(chkShowPassword.Right + 8, pwdRowY + 1),
                Cursor = Cursors.Hand
            };
            lblShowPassword.Click += (s, e) => chkShowPassword.Checked = !chkShowPassword.Checked;
            pnlFormContainer.Controls.Add(lblShowPassword);

            lblCaps = new Label
            {
                Text = "Caps Lock is ON",
                Font = AppFonts.Strong(9F),
                ForeColor = Color.FromArgb(217, 119, 6), // Amber-600
                AutoSize = true,
                BackColor = Color.Transparent,
                Visible = false
            };
            pnlFormContainer.Controls.Add(lblCaps);
            lblCaps.Location = new Point(w - lblCaps.PreferredWidth - 2, pwdRowY + 1);

            y = Math.Max(chkShowPassword.Bottom, lblShowPassword.Bottom) + 20;

            // ── Error Alert Banner (Padded, clean, auto-sizing for zero text cutting) ──
            lblError = new Label
            {
                Text = "",
                Font = AppFonts.Regular(9F),
                ForeColor = Color.FromArgb(185, 28, 28),         // Red-700
                BackColor = Color.FromArgb(254, 242, 242),       // Red-50
                AutoSize = false,
                Location = new Point(0, y),
                Size = new Size(w, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 6, 14, 6),
                Visible = false,
                UseCompatibleTextRendering = false
            };
            UiHelpers.ApplyRoundedRegion(lblError, 8);
            pnlFormContainer.Controls.Add(lblError);

            // ── Primary Action Button: "LOGIN" (Prominent, matching reference) ──
            btnLogin = new Button
            {
                Text = "LOGIN",
                Font = AppFonts.Strong(11.5F),
                BackColor = Color.FromArgb(37, 99, 235), // Vibrant royal blue
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Location = new Point(0, y),
                Size = new Size(w, 52),
                TabIndex = 4
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
            btnLogin.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 64, 175);
            btnLogin.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(btnLogin, 10);
            btnLogin.Click += async (s, e) => await LoginAsync();
            pnlFormContainer.Controls.Add(btnLogin);
            y = btnLogin.Bottom + 20;

            // ── Footer / Help text ──
            lblHelp = new Label
            {
                Text = "Need access? Contact your system administrator.",
                Font = AppFonts.Regular(9.5F),
                ForeColor = Color.FromArgb(148, 163, 184), // Slate-400
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Location = new Point(0, y),
                Size = new Size(w, 24),
                UseCompatibleTextRendering = false
            };
            pnlFormContainer.Controls.Add(lblHelp);
            y = lblHelp.Bottom + 16;

            // Set final height of form container
            pnlFormContainer.Height = y;

            WireInteractions();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Helper: Create Field Group (Label + Spacious TextField)
        // ─────────────────────────────────────────────────────────────────
        private int CreateFieldGroup(
            string labelText, string placeholder,
            out Label lbl, out TextField inp,
            int w, int y, int tabIndex, bool isPassword)
        {
            lbl = new Label
            {
                Text = labelText,
                Font = AppFonts.Strong(9.5F),
                ForeColor = Color.FromArgb(71, 85, 105), // Slate-600
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(0, y)
            };
            pnlFormContainer.Controls.Add(lbl);

            inp = new TextField
            {
                PlaceholderText = placeholder,
                Location = new Point(0, lbl.Bottom + 8),
                Size = new Size(w, FieldH),
                TabIndex = tabIndex,
                Padding = new Padding(16, 0, 16, 0)
            };
            inp.InnerTextBox.Font = AppFonts.Regular(10.5F);

            if (isPassword)
                inp.InnerTextBox.UseSystemPasswordChar = true;

            ApplyTextInset(inp.InnerTextBox, 4);
            pnlFormContainer.Controls.Add(inp);

            return inp.Bottom + FieldGap;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Focus Feedback: Label gets darker when its field is focused
        // ─────────────────────────────────────────────────────────────────
        private static void WireFocusFeedback(TextField inp, Label lbl)
        {
            inp.InnerTextBox.GotFocus += (s, e) => lbl.ForeColor = Color.FromArgb(15, 23, 42);
            inp.InnerTextBox.LostFocus += (s, e) => lbl.ForeColor = Color.FromArgb(71, 85, 105);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Interactive Wiring & Caps Lock detection
        // ─────────────────────────────────────────────────────────────────
        private void WireInteractions()
        {
            inpCompanyId.InnerTextBox.TextChanged += (s, e) => ClearError();
            inpUsername.InnerTextBox.TextChanged += (s, e) => ClearError();
            inpPassword.InnerTextBox.TextChanged += (s, e) => ClearError();

            inpPassword.InnerTextBox.GotFocus += (s, e) => UpdateCapsWarning();
            inpPassword.InnerTextBox.KeyUp += (s, e) => UpdateCapsWarning();
            inpPassword.InnerTextBox.LostFocus += (s, e) => lblCaps.Visible = false;
        }

        private void TogglePasswordVisibility()
        {
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
            btnLogin.Text = busy ? "SIGNING IN..." : "LOGIN";
            inpCompanyId.Enabled = !busy;
            inpUsername.Enabled = !busy;
            inpPassword.Enabled = !busy;
            chkShowPassword.Enabled = !busy;
            lblShowPassword.Enabled = !busy;
            UseWaitCursor = busy;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Login Flow
        // ─────────────────────────────────────────────────────────────────
        private async Task LoginAsync()
        {
            ClearError();

            var companyIdText = inpCompanyId.Text?.Trim() ?? "";
            var username = inpUsername.Text?.Trim() ?? "";
            var password = inpPassword.Text ?? "";

            if (string.IsNullOrWhiteSpace(companyIdText)
                || !int.TryParse(companyIdText, out var companyId)
                || companyId <= 0)
            {
                ShowError("Please enter a valid numeric Company ID (e.g. 1).");
                inpCompanyId.Focus();
                return;
            }

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
                var result = await _api.LoginAsync(username, password, companyId);

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
                UserSession.CompanyId = result.CompanyId > 0 ? result.CompanyId : companyId;
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

        // ─────────────────────────────────────────────────────────────────
        //  Error Handling (Adaptive height so error messages are never cut)
        // ─────────────────────────────────────────────────────────────────
        private void ShowError(string message)
        {
            lblCaps.Visible = false;
            lblError.Text = message;

            // Measure required height so multi-line errors are never truncated
            using var g = lblError.CreateGraphics();
            var size = g.MeasureString(message, lblError.Font, FormColW - 28);
            int calculatedH = Math.Max(42, (int)Math.Ceiling(size.Height) + 14);

            lblError.Height = calculatedH;
            lblError.Visible = true;

            // Shift login button & footer smoothly
            btnLogin.Location = new Point(0, lblError.Bottom + 16);
            lblHelp.Location = new Point(0, btnLogin.Bottom + 20);
            pnlFormContainer.Height = lblHelp.Bottom + 16;
            CenterFormContainer();
        }

        private void ClearError()
        {
            if (!lblError.Visible) return;
            lblError.Text = "";
            lblError.Visible = false;
            lblError.Height = 0;

            // Restore default locations
            btnLogin.Location = new Point(0, lblError.Top);
            lblHelp.Location = new Point(0, btnLogin.Bottom + 20);
            pnlFormContainer.Height = lblHelp.Bottom + 16;
            CenterFormContainer();

            UpdateCapsWarning();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Double-buffered panel for flicker-free rendering
        // ─────────────────────────────────────────────────────────────────
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