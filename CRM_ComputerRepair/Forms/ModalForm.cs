using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CRM.winforms.Forms
{
   
    [DesignerCategory("Code")]
    public class ModalForm : Form
    {
        // ═══════════ WIN32 / DWM ═══════════

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;

        // ═══════════ STATE ═══════════

        private int _cardWidth = 520;
        private int _cardHeight = 500;

        protected Panel pnlCard = null!;
        protected Label lblTitle = null!;
        protected ModalCloseButton btnClose = null!;

        protected const int ShadowPad = 16;

        protected static int ContentTopY => ShadowPad + 72;
        protected static int ContentLeftX => ShadowPad + 32;
        protected int ContentRightX => pnlCard.Width - ShadowPad - 32;
        protected int ContentWidth => ContentRightX - ContentLeftX;

        private const int DimAlpha = 110;

        private static readonly Color FallbackBackdrop = Color.FromArgb(200, 208, 220);

        // Backdrop bitmap — drawn directly in OnPaint.
        private Bitmap? _backdrop;

        // ═══════════ CONSTRUCTOR ═══════════

        public ModalForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;

            // Opaque + UserPaint so Windows never paints a default background.
            SetStyle(ControlStyles.Opaque, true);
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.ResizeRedraw, true);

            BackColor = FallbackBackdrop;

            // Never set Opacity — it adds WS_EX_LAYERED and reintroduces the sparkle.

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };
        }

        // ═══════════ DISABLE DWM OPEN ANIMATION ═══════════

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            try
            {
                int disable = 1;
                DwmSetWindowAttribute(
                    Handle,
                    DWMWA_TRANSITIONS_FORCEDISABLED,
                    ref disable,
                    sizeof(int));
            }
            catch
            {
                // DWM not present — OS default animation will apply.
            }
        }

        // ═══════════ NO DEFAULT BACKGROUND PAINT ═══════════

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // No-op. Prevents one visible frame of solid color before backdrop.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;

            if (_backdrop != null)
                g.DrawImageUnscaled(_backdrop, 0, 0);
            else
            {
                using var brush = new SolidBrush(FallbackBackdrop);
                g.FillRectangle(brush, ClientRectangle);
            }

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

                // Capture BEFORE the modal handle is created.
                _backdrop = CaptureParentDimmed(ownerForm);

                Location = cover.Location;
                Size = cover.Size;

                // Force handle creation so OnHandleCreated runs (disabling the
                // DWM animation) before ShowDialog presents the window.
                var _ = Handle;

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

                var snap = new Bitmap(w, h);
                parent.DrawToBitmap(snap, new Rectangle(0, 0, w, h));

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

            pnlCard = new Panel
            {
                Size = new Size(_cardWidth, _cardHeight),
                BackColor = Color.Transparent
            };

            pnlCard.Paint += (s, e) =>
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                var body = new Rectangle(
                    ShadowPad,
                    ShadowPad,
                    pnlCard.Width - ShadowPad * 2,
                    pnlCard.Height - ShadowPad * 2);

                for (int i = 0; i < 4; i++)
                {
                    int alpha = 10 + i * 5;
                    var shadowRect = new Rectangle(
                        body.X - 1,
                        body.Y + 2,
                        body.Width + 2,
                        body.Height + 2);
                    shadowRect.Inflate(i * 2, i * 2);

                    using var path = UiKit.Rounded(shadowRect, UiKit.Radius + i);
                    using var brush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
                    g.FillPath(brush, path);
                }

                UiKit.FillRounded(g, body, UiKit.Radius, UiKit.Surface);
                UiKit.StrokeRounded(g, body, UiKit.Radius, UiKit.Line);
                UiKit.HLine(g, body.X + 1, body.Right - 1, ShadowPad + 60, UiKit.Line);
            };

            lblTitle = new Label
            {
                Text = title,
                Font = UiKit.Title,
                ForeColor = UiKit.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnClose = new ModalCloseButton();
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

                lblTitle.Location = new Point(ShadowPad + 28, ShadowPad + 24);

                btnClose.Size = new Size(32, 32);
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
            => UiKit.Rounded(rect, radius);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _backdrop?.Dispose();
                _backdrop = null;
            }
            base.Dispose(disposing);
        }

        // ═══════════════════════════════════════════════════════════════
        //  CLOSE BUTTON
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        protected sealed class ModalCloseButton : Control
        {
            private bool _hover, _down;

            public ModalCloseButton()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint
                       | ControlStyles.ResizeRedraw
                       | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                Cursor = Cursors.Hand;
                TabStop = true;
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode is Keys.Enter or Keys.Space)
                    InvokeOnClick(this, EventArgs.Empty);
                base.OnKeyDown(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                if (_down) UiKit.FillRounded(g, ClientRectangle, UiKit.RadiusSm, UiKit.Line);
                else if (_hover) UiKit.FillRounded(g, ClientRectangle, UiKit.RadiusSm, UiKit.Hover);

                if (Focused)
                    UiKit.StrokeRounded(g, ClientRectangle, UiKit.RadiusSm,
                        UiKit.Mix(UiKit.Accent, Color.White, 0.4));

                using var f = UiKit.GlyphFont(10F);
                UiKit.Text(g, "\uE711", f,
                    _hover ? UiKit.Ink : UiKit.InkFaint,
                    ClientRectangle, UiKit.Center);
            }
        }
    }
}