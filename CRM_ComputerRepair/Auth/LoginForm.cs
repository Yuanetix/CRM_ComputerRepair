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
        //  Layout constants
        // ─────────────────────────────────────────────────────────────────
        private const int DefaultFormW = 1200;
        private const int DefaultFormH = 780;
        private const int BrandPanelW = 520;
        private const int FormColW = 420;
        private const int FieldH = 52;
        private const int FieldGap = 24;

        // ─────────────────────────────────────────────────────────────────
        //  Brand palette — light blue family (consistent with AppTheme.Primary)
        // ─────────────────────────────────────────────────────────────────
        private static readonly Color BrandTop = Color.FromArgb(59, 130, 246);   // blue-500
        private static readonly Color BrandMid = Color.FromArgb(37, 99, 235);    // blue-600
        private static readonly Color BrandBottom = Color.FromArgb(30, 58, 138);    // blue-900
        private static readonly Color BrandAccent = Color.FromArgb(147, 197, 253);  // blue-300

        // ─────────────────────────────────────────────────────────────────
        //  Win32: text inset
        // ─────────────────────────────────────────────────────────────────
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

        private Label lblUsername = null!;
        private Label lblPassword = null!;

        private ModernInput inpUsername = null!;
        private ModernInput inpPassword = null!;

        private CheckBox chkShowPassword = null!;
        private Label lblShowPassword = null!;
        private Label lblCaps = null!;

        private Label lblError = null!;
        private Button btnLogin = null!;

        public LoginForm()
        {
            BuildUi();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Root Window Setup
        // ─────────────────────────────────────────────────────────────────
        private void BuildUi()
        {
            Text = "Sign in — Fixory CRM";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;

            var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            int targetW = DefaultFormW;
            int targetH = DefaultFormH;
            if (workingArea.Height < 840)
            {
                targetH = Math.Min(760, workingArea.Height - 30);
                targetW = Math.Min(1180, workingArea.Width - 40);
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
                inpUsername.Focus();
            };
        }

        // ─────────────────────────────────────────────────────────────────
        //  LEFT — Brand panel (light blue, consistent with AppTheme.Primary)
        // ─────────────────────────────────────────────────────────────────
        private void BuildBrandPanel()
        {
            pnlBrand = new BufferedPanel
            {
                Dock = DockStyle.Left,
                Width = BrandPanelW,
                BackColor = BrandMid
            };

            const int brandPadX = 60;
            int brandTextW = BrandPanelW - brandPadX * 2;

            string[] featureTitles =
            {
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

                // 1. Light-blue vertical gradient (matches primary family)
                using (var bgBrush = new LinearGradientBrush(
                    bounds,
                    BrandTop,
                    BrandBottom,
                    LinearGradientMode.Vertical))
                {
                    g.FillRectangle(bgBrush, bounds);
                }

                // 2. Soft diagonal highlight for depth
                using (var sheen = new LinearGradientBrush(
                    bounds,
                    Color.FromArgb(40, 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255),
                    35f))
                {
                    g.FillRectangle(sheen, new Rectangle(0, 0, bounds.Width, bounds.Height / 2));
                }

                // 3. Quiet dot matrix, top-left
                using (var dotBrush = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                {
                    int dotCols = 6, dotRows = 4;
                    int startX = brandPadX, startY = 52;
                    int dotGap = 14;
                    for (int r = 0; r < dotRows; r++)
                        for (int c = 0; c < dotCols; c++)
                            g.FillEllipse(dotBrush, startX + c * dotGap, startY + r * dotGap, 3, 3);
                }

                // 4. Single soft wave
                using (var waveBrush = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
                {
                    using var path = new GraphicsPath();
                    path.AddBezier(0, pnlBrand.Height - 150,
                                   pnlBrand.Width * 0.40f, pnlBrand.Height - 210,
                                   pnlBrand.Width * 0.75f, pnlBrand.Height - 100,
                                   pnlBrand.Width, pnlBrand.Height - 140);
                    path.AddLine(pnlBrand.Width, pnlBrand.Height, 0, pnlBrand.Height);
                    path.CloseFigure();
                    g.FillPath(waveBrush, path);
                }

                // 5. Feature icon badges
                int featureStartY = 340;
                int featureStep = 60;
                for (int i = 0; i < featureTitles.Length; i++)
                {
                    int cy = featureStartY + i * featureStep;
                    var badgeRect = new Rectangle(brandPadX, cy, 36, 36);

                    using (var badgeFill = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                        UiKit.FillRounded(g, badgeRect, 10, Color.FromArgb(60, 255, 255, 255));

                    using (var badgeStroke = new Pen(Color.FromArgb(110, 255, 255, 255), 1.2f))
                        UiKit.StrokeRounded(g, badgeRect, 10, Color.FromArgb(110, 255, 255, 255), 1.2f);

                    DrawFeatureIcon(g, i, badgeRect);
                }
            };

            // ── Headline ──
            var lblHeadline = new Label
            {
                Text = "Fixory\r\nComputer Repair CRM",
                Font = AppFonts.Strong(27F),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(brandTextW, 100),
                BackColor = Color.Transparent,
                Location = new Point(brandPadX, 166),
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
            };
            pnlBrand.Controls.Add(lblHeadline);

            // ── Feature labels (fixed-width column) ──
            int featureLabelX = brandPadX + 36 + 16;
            int featureLabelW = brandTextW - 36 - 16;
            int featureStartY = 340;
            int featureStep = 60;

            for (int i = 0; i < featureTitles.Length; i++)
            {
                var lblFeature = new Label
                {
                    Text = featureTitles[i],
                    Font = AppFonts.Regular(10.5F),
                    ForeColor = Color.White,
                    AutoSize = false,
                    Size = new Size(featureLabelW, 36),
                    BackColor = Color.Transparent,
                    Location = new Point(featureLabelX, featureStartY + i * featureStep),
                    TextAlign = ContentAlignment.MiddleLeft,
                    UseCompatibleTextRendering = false
                };
                pnlBrand.Controls.Add(lblFeature);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Feature icons
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
                case 0:
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

                case 1:
                    g.DrawLine(pen, cx - 5.5f, cy + 5.5f, cx + 2f, cy - 2f);
                    g.DrawArc(pen, cx - 1f, cy - 8f, 8f, 8f, 45, 270);
                    g.FillEllipse(fillBrush, cx - 7f, cy + 5f, 3.5f, 3.5f);
                    break;

                case 2:
                    g.DrawEllipse(pen, cx - 3.5f, cy - 7f, 7f, 7f);
                    g.DrawArc(pen, cx - 7f, cy - 0.5f, 14f, 11f, 200, 140);
                    break;

                case 3:
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
        //  RIGHT — white canvas
        // ─────────────────────────────────────────────────────────────────
        private void BuildRightPanel()
        {
            pnlRight = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            pnlRight.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(238, 242, 247), 1);
                e.Graphics.DrawLine(pen, 0, 0, 0, pnlRight.Height);
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
                Math.Max(40, (pnlRight.ClientSize.Width - pnlFormContainer.Width) / 2),
                Math.Max(24, (pnlRight.ClientSize.Height - pnlFormContainer.Height) / 2));
        }

        // ─────────────────────────────────────────────────────────────────
        //  Form content
        // ─────────────────────────────────────────────────────────────────
        private void BuildFormContent()
        {
            int w = FormColW;
            int y = 0;

            // ── Welcome ──
            lblTitle = new Label
            {
                Text = "Welcome back",
                Font = AppFonts.Strong(28F),
                ForeColor = Color.FromArgb(15, 23, 42),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(0, y),
                UseCompatibleTextRendering = false
            };
            pnlFormContainer.Controls.Add(lblTitle);
            y = lblTitle.Bottom + 6;

            lblSubtitle = new Label
            {
                Text = "Sign in to continue to your workspace.",
                Font = AppFonts.Regular(10F),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(0, y),
                UseCompatibleTextRendering = false
            };
            pnlFormContainer.Controls.Add(lblSubtitle);
            y = lblSubtitle.Bottom + 36;

            // ── Username ──
            y = CreateFieldGroup("USERNAME", "Enter your username",
                                 out lblUsername, out inpUsername,
                                 w, y, 0, isPassword: false);

            // ── Password ──
            y = CreateFieldGroup("PASSWORD", "Enter your password",
                                 out lblPassword, out inpPassword,
                                 w, y, 1, isPassword: true);

            WireFocusFeedback(inpUsername, lblUsername);
            WireFocusFeedback(inpPassword, lblPassword);

            // ── Show password + caps lock row ──
            int pwdRowY = y - FieldGap + 2;

            chkShowPassword = new CheckBox
            {
                Appearance = Appearance.Normal,
                AutoSize = false,
                Size = new Size(18, 18),
                Location = new Point(2, pwdRowY + 2),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabIndex = 2,
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
                Cursor = Cursors.Hand,
                UseCompatibleTextRendering = false
            };
            lblShowPassword.Click += (s, e) => chkShowPassword.Checked = !chkShowPassword.Checked;
            pnlFormContainer.Controls.Add(lblShowPassword);

            lblCaps = new Label
            {
                Text = "Caps Lock is ON",
                Font = AppFonts.Strong(9F),
                ForeColor = Color.FromArgb(217, 119, 6),
                AutoSize = true,
                BackColor = Color.Transparent,
                Visible = false,
                UseCompatibleTextRendering = false
            };
            pnlFormContainer.Controls.Add(lblCaps);
            lblCaps.Location = new Point(w - lblCaps.PreferredWidth - 2, pwdRowY + 1);

            y = Math.Max(chkShowPassword.Bottom, lblShowPassword.Bottom) + 30;

            // ── Error banner ──
            lblError = new Label
            {
                Text = "",
                Font = AppFonts.Regular(9F),
                ForeColor = Color.FromArgb(185, 28, 28),
                BackColor = Color.FromArgb(254, 242, 242),
                AutoSize = false,
                Location = new Point(0, y),
                Size = new Size(w, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 6, 16, 6),
                Visible = false,
                UseCompatibleTextRendering = false
            };
            UiHelpers.ApplyRoundedRegion(lblError, 10);
            pnlFormContainer.Controls.Add(lblError);

            // ── Sign in ──
            btnLogin = new Button
            {
                Text = "SIGN IN",
                Font = AppFonts.Strong(11.5F),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Location = new Point(0, y),
                Size = new Size(w, 52),
                TabIndex = 3
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
            btnLogin.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 64, 175);
            btnLogin.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(btnLogin, 10);
            btnLogin.Click += async (s, e) => await LoginAsync();
            pnlFormContainer.Controls.Add(btnLogin);
            y = btnLogin.Bottom + 12;

            pnlFormContainer.Height = y;

            WireInteractions();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Field group helper — uses custom ModernInput
        // ─────────────────────────────────────────────────────────────────
        private int CreateFieldGroup(
            string labelText, string placeholder,
            out Label lbl, out ModernInput inp,
            int w, int y, int tabIndex, bool isPassword)
        {
            lbl = new Label
            {
                Text = labelText,
                Font = AppFonts.Strong(9F),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(2, y),
                UseCompatibleTextRendering = false
            };
            pnlFormContainer.Controls.Add(lbl);

            inp = new ModernInput
            {
                PlaceholderText = placeholder,
                Location = new Point(0, lbl.Bottom + 8),
                Size = new Size(w, FieldH),
                TabIndex = tabIndex,
                IsPassword = isPassword
            };
            pnlFormContainer.Controls.Add(inp);

            return inp.Bottom + FieldGap;
        }

        private static void WireFocusFeedback(ModernInput inp, Label lbl)
        {
            inp.InnerTextBox.GotFocus += (s, e) => lbl.ForeColor = Color.FromArgb(37, 99, 235);
            inp.InnerTextBox.LostFocus += (s, e) => lbl.ForeColor = Color.FromArgb(71, 85, 105);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Interactions
        // ─────────────────────────────────────────────────────────────────
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
            btnLogin.Text = busy ? "SIGNING IN..." : "SIGN IN";
            inpUsername.Enabled = !busy;
            inpPassword.Enabled = !busy;
            chkShowPassword.Enabled = !busy;
            lblShowPassword.Enabled = !busy;
            UseWaitCursor = busy;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Login flow — company id fixed to 1
        // ─────────────────────────────────────────────────────────────────
        private async Task LoginAsync()
        {
            ClearError();

            var username = inpUsername.Text?.Trim() ?? "";
            var password = inpPassword.Text ?? "";
            const int companyId = 1;

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
                UserSession.CompanyName = result.CompanyName ?? "";
                UserSession.HasAcceptedTerms = result.HasAcceptedTerms;
                UserSession.Token = result.Token;
                UserSession.SubscribedModules = result.SubscribedModules ?? new();
                UserSession.UserBranchId = result.BranchId;
                UserSession.UserBranchName = result.BranchName;

                if (!string.Equals(result.Role, "Admin", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(result.Role, "Super Admin", StringComparison.OrdinalIgnoreCase) &&
                    result.BranchId.HasValue)
                {
                    UserSession.SelectedBranchId = result.BranchId;
                    UserSession.SelectedBranchName = result.BranchName ?? $"Branch #{result.BranchId}";
                }
                else
                {
                    UserSession.SelectedBranchId = null;
                    UserSession.SelectedBranchName = "All Branches";
                }

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
        //  Error handling
        // ─────────────────────────────────────────────────────────────────
        private void ShowError(string message)
        {
            lblCaps.Visible = false;
            lblError.Text = message;

            using var g = lblError.CreateGraphics();
            var size = g.MeasureString(message, lblError.Font, FormColW - 32);
            int calculatedH = Math.Max(44, (int)Math.Ceiling(size.Height) + 14);

            lblError.Height = calculatedH;
            lblError.Visible = true;

            btnLogin.Location = new Point(0, lblError.Bottom + 16);
            pnlFormContainer.Height = btnLogin.Bottom + 12;
            CenterFormContainer();
        }

        private void ClearError()
        {
            if (!lblError.Visible) return;
            lblError.Text = "";
            lblError.Visible = false;
            lblError.Height = 0;

            btnLogin.Location = new Point(0, lblError.Top);
            pnlFormContainer.Height = btnLogin.Bottom + 12;
            CenterFormContainer();

            UpdateCapsWarning();
        }

        // ─────────────────────────────────────────────────────────────────
        //  ModernInput — clean rounded input with focus ring & placeholder
        // ─────────────────────────────────────────────────────────────────
        [DesignerCategory("Code")]
        private sealed class ModernInput : Control
        {
            private readonly TextBox _inner;
            private bool _hover;
            private bool _focused;
            private string _placeholder = "";
            private bool _isPassword;

            public TextBox InnerTextBox => _inner;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public string PlaceholderText
            {
                get => _placeholder;
                set { _placeholder = value ?? ""; UpdatePlaceholder(); Invalidate(); }
            }

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public bool IsPassword
            {
                get => _isPassword;
                set
                {
                    _isPassword = value;
                    _inner.UseSystemPasswordChar = value;
                }
            }

            public override string Text
            {
                get => _inner.Text;
                set => _inner.Text = value;
            }

            public ModernInput()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint
                       | ControlStyles.ResizeRedraw, true);
                BackColor = Color.White;
                Padding = new Padding(16, 0, 16, 0);

                _inner = new TextBox
                {
                    BorderStyle = BorderStyle.None,
                    Font = AppFonts.Regular(10.5F),
                    ForeColor = Color.FromArgb(15, 23, 42),
                    BackColor = Color.White,
                    ShortcutsEnabled = true
                };
                _inner.GotFocus += (s, e) => { _focused = true; UpdatePlaceholder(); Invalidate(); };
                _inner.LostFocus += (s, e) => { _focused = false; UpdatePlaceholder(); Invalidate(); };
                _inner.TextChanged += (s, e) => UpdatePlaceholder();

                Controls.Add(_inner);
                ApplyTextInset(_inner, 4);
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _inner.Focus(); base.OnMouseDown(e); }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                LayoutInner();
            }

            private void LayoutInner()
            {
                if (_inner == null) return;
                int padL = 16, padR = 16;
                _inner.Location = new Point(padL, (Height - _inner.PreferredHeight) / 2);
                _inner.Width = Math.Max(20, Width - padL - padR);
            }

            private void UpdatePlaceholder()
            {
                // Placeholder rendered via paint; no need to modify inner Text
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                // Background
                using (var b = new SolidBrush(Color.White))
                    g.FillRectangle(b, ClientRectangle);

                // Border
                Color border = _focused
                    ? Color.FromArgb(37, 99, 235)       // blue-600
                    : _hover
                        ? Color.FromArgb(148, 163, 184)  // slate-400
                        : Color.FromArgb(203, 213, 225); // slate-300

                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                float thickness = _focused ? 1.8f : 1.3f;

                using (var path = UiKit.Rounded(rect, 10))
                using (var pen = new Pen(border, thickness))
                    g.DrawPath(pen, path);

                // Focus ring (soft outer glow)
                if (_focused)
                {
                    var ring = new Rectangle(-1, -1, Width + 1, Height + 1);
                    using var ringPath = UiKit.Rounded(ring, 11);
                    using var ringPen = new Pen(Color.FromArgb(60, 37, 99, 235), 3f);
                    g.DrawPath(ringPen, ringPath);
                }

                // Placeholder
                if (string.IsNullOrEmpty(_inner.Text) && !string.IsNullOrEmpty(_placeholder))
                {
                    var textRect = new Rectangle(16, 0, Width - 32, Height);
                    TextRenderer.DrawText(g, _placeholder, _inner.Font, textRect,
                        Color.FromArgb(148, 163, 184),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                        | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Double-buffered panel
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