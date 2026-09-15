using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Modal dialog with a dimmed backdrop showing the parent form behind.
    /// Captures the parent into a bitmap once, then paints it manually
    /// in OnPaint — no flicker, no sparkle on first frame.
    /// </summary>
    [DesignerCategory("Code")]
    public class ModalForm : Form
    {
        private int _cardWidth = 520;
        private int _cardHeight = 500;

        protected Panel pnlCard = null!;
        protected Label lblTitle = null!;
        protected Button btnClose = null!;

        protected const int ShadowPad = 16;

        protected int ContentTopY => ShadowPad + 72;
        protected int ContentLeftX => ShadowPad + 32;
        protected int ContentRightX => pnlCard.Width - ShadowPad - 32;
        protected int ContentWidth => ContentRightX - ContentLeftX;

        // Backdrop dim strength (0 = no dim, 255 = black)
        private const int DimAlpha = 110;

        // Fallback if capture fails
        private static readonly Color FallbackBackdrop = Color.FromArgb(200, 208, 220);

        // Captured backdrop — drawn manually in OnPaint (no flicker)
        private Bitmap? _backdrop;

        public ModalForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;

            BackColor = FallbackBackdrop;
            Opacity = 1.0;

            SetStyle(ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint, true);

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };
        }

        // ═══════════ DISABLE FADE-IN (no transition) ═══════════

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;   // WS_EX_COMPOSITED — double buffer + no fade
                return cp;
            }
        }

        // ═══════════ MANUAL BACKDROP PAINT (no flicker) ═══════════

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Do nothing — we handle background in OnPaint
            // This prevents the "sparkle" flash on first frame.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;

            // 1. Paint the captured backdrop (parent form, dimmed)
            if (_backdrop != null)
            {
                g.DrawImageUnscaled(_backdrop, 0, 0);
            }
            else
            {
                // Fallback: solid dim color
                using var brush = new SolidBrush(FallbackBackdrop);
                g.FillRectangle(brush, ClientRectangle);
            }

            // 2. Children (card panel) are painted by the base
            base.OnPaint(e);
        }

        // ═══════════ PUBLIC SHOW ═══════════

        public DialogResult ShowModal(IWin32Window? owner)
        {
            var ownerForm = owner as Form;

            if (ownerForm != null)
            {
                var screen = Screen.FromControl(ownerForm);
                var cover = ownerForm.WindowState == FormWindowState.Maximized
                    ? screen.Bounds
                    : ownerForm.Bounds;

                Location = cover.Location;
                Size = cover.Size;

                // Capture BEFORE showing — this is the key fix
                _backdrop = CaptureParentDimmed(ownerForm);

                return ShowDialog(ownerForm);
            }

            return ShowDialog();
        }

        // ═══════════ CAPTURE PARENT (fast) ═══════════

        private static Bitmap? CaptureParentDimmed(Form parent)
        {
            try
            {
                int w = parent.Width;
                int h = parent.Height;
                if (w <= 0 || h <= 0) return null;

                // 1. Draw parent into a bitmap
                var snap = new Bitmap(w, h);
                parent.DrawToBitmap(snap, new Rectangle(0, 0, w, h));

                // 2. Composite a dark overlay
                using (var g = Graphics.FromImage(snap))
                using (var brush = new SolidBrush(Color.FromArgb(DimAlpha, 0, 0, 0)))
                {
                    g.FillRectangle(brush, 0, 0, w, h);
                }

                return snap;
            }
            catch
            {
                return null;
            }
        }

        // ═══════════ BUILD CARD ═══════════

        protected void BuildCard(string title, int width, int height)
        {
            _cardWidth = width;
            _cardHeight = height;

            var pnlHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            // Card
            pnlCard = new Panel
            {
                Size = new Size(_cardWidth, _cardHeight),
                BackColor = Color.Transparent
            };

            pnlCard.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var body = new Rectangle(
                    ShadowPad,
                    ShadowPad,
                    pnlCard.Width - ShadowPad * 2,
                    pnlCard.Height - ShadowPad * 2);

                // Soft shadow
                for (int i = 0; i < 6; i++)
                {
                    int alpha = 14 + i * 4;
                    var shadowRect = new Rectangle(
                        body.X - i - 1,
                        body.Y - i + 2,
                        body.Width + i * 2 + 2,
                        body.Height + i * 2 + 2);

                    using var path = GetRoundedPath(shadowRect, 10 + i);
                    using var brush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
                    g.FillPath(brush, path);
                }

                // Solid white card
                using (var path = GetRoundedPath(body, 10))
                using (var brush = new SolidBrush(Color.White))
                    g.FillPath(brush, path);

                // Hairline
                using (var path = GetRoundedPath(body, 10))
                using (var pen = new Pen(Color.FromArgb(228, 231, 236), 1))
                    g.DrawPath(pen, path);
            };

            // Title
            lblTitle = new Label
            {
                Text = "🔧  " + title,
                Font = new Font("Segoe UI Semibold", 13F),
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            // Close
            btnClose = new Button
            {
                Text = "✕",
                Font = new Font("Segoe UI Semibold", 10F),
                ForeColor = AppTheme.TextMuted,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(32, 32),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = AppTheme.Neutral;
            btnClose.FlatAppearance.MouseDownBackColor = AppTheme.Border;
            btnClose.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            pnlCard.Controls.Add(lblTitle);
            pnlCard.Controls.Add(btnClose);

            pnlHost.Controls.Add(pnlCard);
            Controls.Add(pnlHost);

            void CenterCard()
            {
                pnlCard.Location = new Point(
                    (pnlHost.Width - pnlCard.Width) / 2,
                    (pnlHost.Height - pnlCard.Height) / 2);

                lblTitle.Location = new Point(
                    ShadowPad + 28,
                    ShadowPad + 22);

                btnClose.Location = new Point(
                    pnlCard.Width - ShadowPad - 32,
                    ShadowPad + 16);
            }

            pnlHost.Resize += (s, e) => CenterCard();
            Shown += (s, e) => CenterCard();
            CenterCard();
        }

        // ═══════════ HELPERS ═══════════

        protected static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
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

        // ═══════════ CLEANUP ═══════════

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _backdrop?.Dispose();
                _backdrop = null;
            }
            base.Dispose(disposing);
        }
    }
}