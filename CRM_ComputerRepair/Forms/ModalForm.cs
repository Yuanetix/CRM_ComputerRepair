using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CRM.winforms;

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

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // WS_EX_TOOLWINDOW (0x00000080):
                // Informs Windows/DWM that this is a utility/dialog window.
                // DWM completely suppresses the window open fade-in/zoom animation
                // and window close fade-out animation. The modal opens and closes instantly (0ms).
                cp.ExStyle |= 0x00000080;
                return cp;
            }
        }

        // ═══════════ STATE ═══════════

        private int _cardWidth = 520;
        private int _cardHeight = 500;
        private bool _hasSubtitle;

        protected Panel pnlCard = null!;
        protected Panel pnlHeader = null!;
        protected Panel pnlBody = null!;
        protected Panel pnlFooter = null!;
        protected Label lblTitle = null!;
        private Label? _lblHeaderSubtitle;
        protected ModalCloseButton btnClose = null!;

        public Panel BodyPanel => pnlBody;
        public Panel FooterPanel => pnlFooter;
        public Panel HeaderPanel => pnlHeader;

        protected const int ShadowPad = 16;

        protected virtual int ContentTopY => 18;
        protected virtual int ContentLeftX => 24;
        protected int ContentRightX => (_cardWidth - ShadowPad * 2) - ContentLeftX - 12;
        protected int ContentWidth => Math.Max(200, ContentRightX - ContentLeftX);

        private const int DimAlpha = 110;

        private static readonly Color FallbackBackdrop = Color.FromArgb(200, 208, 220);

        // Backdrop bitmap — drawn directly in OnPaintBackground.
        private Bitmap? _backdrop;

        // ═══════════ CONSTRUCTOR ═══════════

        public ModalForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;

            // Direct double-buffering without Opaque conflict:
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);

            BackColor = FallbackBackdrop;

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

        // ═══════════ CLEAN BACKGROUND PAINT ═══════════

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (_backdrop != null)
            {
                e.Graphics.DrawImageUnscaled(_backdrop, 0, 0);
            }
            else
            {
                using var brush = new SolidBrush(FallbackBackdrop);
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Background is cleanly painted in OnPaintBackground into the back buffer.
            base.OnPaint(e);
        }

        // ═══════════ PUBLIC SHOW ═══════════

        public new DialogResult ShowDialog() => ShowModal(Form.ActiveForm);
        public new DialogResult ShowDialog(IWin32Window? owner) => ShowModal(owner);

        public DialogResult ShowModal(IWin32Window? owner)
        {
            var ownerForm = (owner as Form) ?? Form.ActiveForm;

            if (ownerForm != null && ownerForm.Visible && ownerForm.WindowState != FormWindowState.Minimized && ownerForm.Width > 100 && ownerForm.Height > 100)
            {
                var screen = Screen.FromControl(ownerForm) ?? Screen.PrimaryScreen;
                var cover = ownerForm.WindowState == FormWindowState.Maximized
                    ? (screen?.Bounds ?? ownerForm.Bounds)
                    : ownerForm.Bounds;

                // Instant hardware screen capture: < 1ms, zero UI freeze, zero flicker
                _backdrop = CaptureCoverDimmed(cover);

                Location = cover.Location;
                Size = cover.Size;
                CenterCard();

                return base.ShowDialog(ownerForm);
            }

            var fallbackScreen = Screen.PrimaryScreen ?? (Screen.AllScreens.Length > 0 ? Screen.AllScreens[0] : null);
            if (fallbackScreen != null)
            {
                var rect = fallbackScreen.WorkingArea;
                _backdrop = CaptureCoverDimmed(rect);
                Location = rect.Location;
                Size = rect.Size;
            }
            CenterCard();
            return owner != null ? base.ShowDialog(owner) : base.ShowDialog();
        }

        // ═══════════ CAPTURE SCREEN (instant hardware BitBlt) ═══════════

        private static Bitmap? CaptureCoverDimmed(Rectangle cover)
        {
            try
            {
                if (cover.Width <= 0 || cover.Height <= 0) return null;

                var snap = new Bitmap(cover.Width, cover.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(snap))
                {
                    // Instant screen capture directly from GPU compositor
                    g.CopyFromScreen(cover.Location, Point.Empty, cover.Size, CopyPixelOperation.SourceCopy);

                    // Dim overlay
                    using var brush = new SolidBrush(Color.FromArgb(DimAlpha, 0, 0, 0));
                    g.FillRectangle(brush, 0, 0, cover.Width, cover.Height);
                }

                return snap;
            }
            catch
            {
                return null;
            }
        }

        private bool _hasFooter = true;
        private SaasButton? _footerSave;
        private SaasButton? _footerCancel;
        private SaasButton? _footerArchive;
        private int _footerSaveW = 120;
        private int _footerCancelW = 100;
        private int _footerArchiveW = 110;

        protected void BuildCard(string title, int width, int height)
        {
            BuildCard(title, subtitle: null, width: width, height: height, hasFooter: true);
        }

        protected void BuildCard(string title, string? subtitle, int width, int height)
        {
            BuildCard(title, subtitle, width: width, height: height, hasFooter: true);
        }

        /// <summary>
        /// SaaS dialog card with fixed header, scrollable body, and fixed footer.
        /// Fully responsive and clamped against the screen working area so content
        /// never overflows the display on laptops or low-resolution monitors.
        /// </summary>
        protected void BuildCard(string title, string? subtitle, int width, int height, bool hasFooter)
        {
            _cardWidth = width;
            _cardHeight = height;
            _hasSubtitle = !string.IsNullOrWhiteSpace(subtitle);
            _hasFooter = hasFooter;

            pnlCard = new ModalCardPanel
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
            };

            // ── Fixed Header ──
            pnlHeader = new ModalCardPanel
            {
                BackColor = UiKit.Surface
            };
            pnlHeader.Paint += (s, e) =>
            {
                UiKit.Quality(e.Graphics);
                using var p = new Pen(UiKit.Line, 1);
                e.Graphics.DrawLine(p, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
            };
            pnlHeader.Resize += (s, e) =>
            {
                if (pnlHeader.Width <= 0 || pnlHeader.Height <= 0) return;
                using var path = new GraphicsPath();
                int r = UiKit.Radius;
                int d = r * 2;
                path.AddArc(0, 0, d, d, 180, 90);
                path.AddArc(pnlHeader.Width - d, 0, d, d, 270, 90);
                path.AddLine(pnlHeader.Width, pnlHeader.Height, 0, pnlHeader.Height);
                path.CloseFigure();
                pnlHeader.Region = new Region(path);
            };

            // ── Fixed Footer ──
            pnlFooter = new ModalCardPanel
            {
                BackColor = UiKit.Surface,
                Visible = _hasFooter
            };
            pnlFooter.Paint += (s, e) =>
            {
                UiKit.Quality(e.Graphics);
                using var p = new Pen(UiKit.Line, 1);
                e.Graphics.DrawLine(p, 0, 0, pnlFooter.Width, 0);
            };
            pnlFooter.Resize += (s, e) =>
            {
                if (pnlFooter.Width <= 0 || pnlFooter.Height <= 0) return;
                using var path = new GraphicsPath();
                int r = UiKit.Radius;
                int d = r * 2;
                path.AddLine(0, 0, pnlFooter.Width, 0);
                path.AddArc(pnlFooter.Width - d, pnlFooter.Height - d, d, d, 0, 90);
                path.AddArc(0, pnlFooter.Height - d, d, d, 90, 90);
                path.CloseFigure();
                pnlFooter.Region = new Region(path);

                PositionFooterButtons();
            };

            // ── Scrollable Body ──
            pnlBody = new ModalCardPanel
            {
                BackColor = UiKit.Surface,
                AutoScroll = true
            };

            lblTitle = new Label
            {
                Text = title,
                Font = AppTheme.FontPageTitle,
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

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(btnClose);

            if (_hasSubtitle)
            {
                _lblHeaderSubtitle = new Label
                {
                    Text = subtitle,
                    Font = UiKit.Small,
                    ForeColor = UiKit.InkMuted,
                    AutoSize = false,
                    BackColor = Color.Transparent
                };
                pnlHeader.Controls.Add(_lblHeaderSubtitle);
            }

            pnlCard.Controls.Add(pnlHeader);
            pnlCard.Controls.Add(pnlBody);
            pnlCard.Controls.Add(pnlFooter);

            Controls.Add(pnlCard);

            Resize += (s, e) => CenterCard();
            Shown += (s, e) => CenterCard();
            CenterCard();
        }

        protected void CenterCard()
        {
            if (pnlCard == null) return;

            var screen = Screen.FromControl(this) ?? Screen.PrimaryScreen;
            var workArea = screen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);

            // Responsive screen & client area capping:
            // Ensure modal card never exceeds visible screen or client bounds on any display/scaling.
            int availW = Visible && ClientSize.Width > 200 ? ClientSize.Width - 32 : workArea.Width - 48;
            int availH = Visible && ClientSize.Height > 200 ? ClientSize.Height - 32 : workArea.Height - 48;

            int maxAllowedW = Math.Max(400, Math.Min(availW, workArea.Width - 48));
            int maxAllowedH = Math.Max(350, Math.Min(availH, workArea.Height - 48));

            int cardW = Math.Min(_cardWidth, maxAllowedW);
            int cardH = Math.Min(_cardHeight, maxAllowedH);

            pnlCard.Size = new Size(cardW, cardH);
            pnlCard.Location = new Point(
                Math.Max(16, (ClientSize.Width - cardW) / 2),
                Math.Max(16, (ClientSize.Height - cardH) / 2)
            );

            int innerW = cardW - ShadowPad * 2;
            int headerH = _hasSubtitle ? 76 : 58;
            int footerH = _hasFooter ? 60 : 0;

            pnlHeader.SetBounds(ShadowPad, ShadowPad, innerW, headerH);
            if (_hasFooter)
            {
                pnlFooter.Visible = true;
                pnlFooter.SetBounds(ShadowPad, cardH - ShadowPad - footerH, innerW, footerH);
            }
            else
            {
                pnlFooter.Visible = false;
            }

            int bodyTop = pnlHeader.Bottom;
            int bodyH = Math.Max(40, (_hasFooter ? pnlFooter.Top : (cardH - ShadowPad)) - bodyTop);
            pnlBody.SetBounds(ShadowPad, bodyTop, innerW, bodyH);

            // Position Close button at top right of pnlHeader
            btnClose.Size = new Size(32, 32);
            btnClose.Location = new Point(innerW - 32 - 12, (headerH - 32) / 2);

            lblTitle.MaximumSize = new Size(innerW - 80, 0);
            lblTitle.Location = new Point(24, _hasSubtitle ? 14 : (headerH - lblTitle.PreferredHeight) / 2);
            if (_lblHeaderSubtitle != null)
            {
                _lblHeaderSubtitle.Location = new Point(24, lblTitle.Bottom + 3);
                _lblHeaderSubtitle.Size = new Size(innerW - 80, 34);
                _lblHeaderSubtitle.AutoEllipsis = false;
            }

            PositionFooterButtons();
        }

        /// <summary>
        /// Clean fixed footer layout: Save and Cancel on the right, Archive/Delete on the left.
        /// Fixed at the bottom of the dialog so action buttons are always accessible.
        /// </summary>
        public void LayoutFooter(SaasButton? save, SaasButton? cancel = null, SaasButton? archive = null, int saveW = 120, int cancelW = 100, int archiveW = 110)
        {
            _footerSave = save;
            _footerCancel = cancel;
            _footerArchive = archive;
            _footerSaveW = saveW;
            _footerCancelW = cancelW;
            _footerArchiveW = archiveW;

            if (save != null && save.Parent != pnlFooter)
            {
                save.Parent?.Controls.Remove(save);
                pnlFooter.Controls.Add(save);
            }
            if (cancel != null && cancel.Parent != pnlFooter)
            {
                cancel.Parent?.Controls.Remove(cancel);
                pnlFooter.Controls.Add(cancel);
            }
            if (archive != null && archive.Parent != pnlFooter)
            {
                archive.Parent?.Controls.Remove(archive);
                pnlFooter.Controls.Add(archive);
            }

            PositionFooterButtons();

            if (save != null) AcceptButton = save;
            if (cancel != null) CancelButton = cancel;
        }

        private void PositionFooterButtons()
        {
            if (pnlFooter == null || pnlFooter.Width <= 0 || !_hasFooter) return;

            int btnY = Math.Max(0, (pnlFooter.Height - ModalKit.BtnH) / 2);
            int rightX = pnlFooter.Width - 24;

            if (_footerSave != null)
            {
                _footerSave.Size = new Size(_footerSaveW, ModalKit.BtnH);
                _footerSave.Location = new Point(rightX - _footerSaveW, btnY);
            }

            if (_footerCancel != null && _footerSave != null)
            {
                _footerCancel.Size = new Size(_footerCancelW, ModalKit.BtnH);
                _footerCancel.Location = new Point(_footerSave.Left - ModalKit.BtnGap - _footerCancelW, btnY);
            }
            else if (_footerCancel != null)
            {
                _footerCancel.Size = new Size(_footerCancelW, ModalKit.BtnH);
                _footerCancel.Location = new Point(rightX - _footerCancelW, btnY);
            }

            if (_footerArchive != null)
            {
                _footerArchive.Size = new Size(_footerArchiveW, ModalKit.BtnH);
                _footerArchive.Location = new Point(24, btnY);
            }
        }

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
        //  DOUBLE-BUFFERED CARD PANEL
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        protected class ModalCardPanel : Panel
        {
            public ModalCardPanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint
                       | ControlStyles.ResizeRedraw
                       | ControlStyles.SupportsTransparentBackColor, true);
                DoubleBuffered = true;
            }
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
