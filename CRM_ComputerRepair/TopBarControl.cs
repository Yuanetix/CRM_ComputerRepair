using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Clean top bar — brand on the left, notifications + user chip on the right.
    /// No page title (sidebar shows it), no search (context-specific).
    /// </summary>
    [DesignerCategory("Code")]
    public class TopBarControl : Panel
    {
        // ═══════════ EVENTS ═══════════

        public event EventHandler? ProfileClicked;
        public event EventHandler? BellClicked;

        // ═══════════ CONTROLS ═══════════

        private Label lblBrand = null!;
        private Button btnBell = null!;
        private UserChip userChip = null!;
        private Panel pnlDivider = null!;

        // ═══════════ PROPERTIES ═══════════

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string UserName { get; private set; } = "Alex Rivera";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string UserRole { get; private set; } = "Administrator";

        public TopBarControl()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.Surface;
            Height = AppTheme.TopBarHeight;
            Dock = DockStyle.Top;
            Padding = new Padding(0);

            BuildUi();
            LayoutUi();
        }

        // ═══════════ UI BUILD ═══════════

        private void BuildUi()
        {
            // ── Brand ──
            lblBrand = new Label
            {
                Text = "Fixory",
                Font = new Font("Segoe UI Semibold", 15F),
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            // ── Bell ──
            btnBell = new Button
            {
                Text = IconFont.Bell,
                Font = IconFont.Create(12F),
                ForeColor = AppTheme.TextSecondary,
                BackColor = AppTheme.Neutral,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(40, 40),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnBell.FlatAppearance.BorderSize = 0;
            btnBell.FlatAppearance.MouseOverBackColor = AppTheme.Border;
            btnBell.FlatAppearance.MouseDownBackColor = AppTheme.BorderStrong;
            btnBell.Click += (s, e) => BellClicked?.Invoke(this, EventArgs.Empty);
            btnBell.Resize += (s, e) =>
                UiHelpers.ApplyRoundedRegion(btnBell, 20);

            // ── User chip ──
            userChip = new UserChip
            {
                Size = new Size(220, 48),
                Cursor = Cursors.Hand
            };
            userChip.Click += (s, e) => ProfileClicked?.Invoke(this, EventArgs.Empty);

            // ── Bottom divider ──
            pnlDivider = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = AppTheme.Divider
            };

            Controls.Add(lblBrand);
            Controls.Add(btnBell);
            Controls.Add(userChip);
            Controls.Add(pnlDivider);

            Resize += (s, e) => LayoutUi();
        }

        // ═══════════ LAYOUT ═══════════

        private void LayoutUi()
        {
            // Brand (left)
            lblBrand.Location = new Point(28, (Height - lblBrand.Height) / 2);

            // User chip (right-most)
            userChip.Location = new Point(
                Width - userChip.Width - 20,
                (Height - userChip.Height) / 2);

            // Bell (left of user chip)
            btnBell.Location = new Point(
                userChip.Left - btnBell.Width - 12,
                (Height - btnBell.Height) / 2);
        }

        // ═══════════ PUBLIC METHODS ═══════════

        public void SetUser(string fullName, string role)
        {
            UserName = fullName;
            UserRole = role;
            userChip.Invalidate();
        }

        // ═══════════ USER CHIP (custom control) ═══════════

        [DesignerCategory("Code")]
        private class UserChip : Control
        {
            private bool _hover;

            public UserChip()
            {
                DoubleBuffered = true;
               
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                _hover = true; Invalidate(); base.OnMouseEnter(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _hover = false; Invalidate(); base.OnMouseLeave(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var rect = new Rectangle(0, 0, Width - 1, Height - 1);

                // Hover background
                if (_hover)
                {
                    using var path = GetRoundedPath(rect, 8);
                    using var brush = new SolidBrush(AppTheme.Neutral);
                    g.FillPath(brush, path);
                }

                var topBar = Parent as TopBarControl;
                string name = topBar?.UserName ?? "User";
                string role = topBar?.UserRole ?? "";

                // Avatar
                var avatar = new Rectangle(8, (Height - 36) / 2, 36, 36);
                using (var path = GetRoundedPath(avatar, 18))
                using (var brush = new SolidBrush(AppTheme.Primary))
                {
                    g.FillPath(brush, path);
                }

                // Initials
                var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string initials = parts.Length >= 2
                    ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
                    : (parts.Length == 1 ? parts[0].Substring(0, 1).ToUpperInvariant() : "?");

                using (var f = new Font("Segoe UI Semibold", 10F))
                using (var b = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString(initials, f, b, avatar, sf);
                }

                // Name
                using (var nameBrush = new SolidBrush(AppTheme.TextPrimary))
                    g.DrawString(name, AppTheme.FontSidebar, nameBrush,
                        new PointF(54, 11));

                // Role
                using (var roleBrush = new SolidBrush(AppTheme.TextMuted))
                    g.DrawString(role, AppTheme.FontLabel, roleBrush,
                        new PointF(54, 29));

                // Chevron down
                using (var chevFont = IconFont.Create(9F))
                using (var chevBrush = new SolidBrush(AppTheme.TextMuted))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString(IconFont.ChevronDown, chevFont, chevBrush,
                        new RectangleF(Width - 24, 0, 20, Height), sf);
                }
            }

            private static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
            {
                var path = new GraphicsPath();
                int d = radius * 2;
                if (d > rect.Width) d = rect.Width;
                if (d > rect.Height) d = rect.Height;

                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();

                return path;
            }
        }
    }
}