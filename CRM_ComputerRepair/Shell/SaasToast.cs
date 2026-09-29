using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// SaaS non-blocking feedback. HCI: visibility of system status (H1) without
    /// breaking flow — success/info use toasts, only destructive confirmations
    /// and hard errors block with a dialog.
    /// Host lives in MainForm (top-right, above content). Any page can call
    /// Toast.Notify("Customer saved.") without touching the shell.
    /// </summary>
    public enum ToastKind { Success, Info, Warning, Error, Danger = Error }

    [DesignerCategory("Code")]
    public sealed class ToastHost : Panel
    {
        private const int ToastW = 340;
        private const int Gap = 10;
        private readonly List<ToastCard> _cards = new();

        public ToastHost()
        {
            BackColor = AppTheme.Background;
            Size = new Size(ToastW + 16, 10);
            Visible = false;
        }

        public void Notify(string title, string? body = null, ToastKind kind = ToastKind.Success)
        {
            var card = new ToastCard(title, body ?? "", kind);
            card.CloseRequested += (s, e) => Remove(card);
            // Auto-dismiss: success/info 4s, warning 6s, error stays until dismissed.
            int ms = kind switch { ToastKind.Error => 0, ToastKind.Warning => 6000, _ => 4000 };
            if (ms > 0)
            {
                var t = new System.Windows.Forms.Timer { Interval = ms };
                t.Tick += (s, e) => { t.Stop(); t.Dispose(); Remove(card); };
                t.Start();
                card.Tag = t;
            }

            _cards.Insert(0, card);
            while (_cards.Count > 4) Remove(_cards[^1]);
            Controls.Clear();
            foreach (var c in _cards) Controls.Add(c);
            LayoutCards();
            Visible = true;
            BringToFront();
        }

        private void Remove(ToastCard card)
        {
            if (card.Tag is System.Windows.Forms.Timer t) { t.Stop(); t.Dispose(); }
            _cards.Remove(card);
            Controls.Remove(card);
            card.Dispose();
            LayoutCards();
            Visible = _cards.Count > 0;
        }

        private void LayoutCards()
        {
            int y = 0;
            foreach (var c in _cards)
            {
                c.Location = new Point(8, y);
                c.Size = new Size(ToastW, c.DesiredHeight(ToastW));
                y += c.Height + Gap;
            }
            Size = new Size(ToastW + 16, Math.Max(10, y));
        }

        public static void Show(Form? owner, string title, string? body = null, ToastKind kind = ToastKind.Success)
        {
            if (owner == null) return;
            var host = FindHost(owner) ?? Attach(owner);
            host.Notify(title, body, kind);
        }

        private static ToastHost? FindHost(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is ToastHost h) return h;
                var inner = FindHost(c);
                if (inner != null) return inner;
            }
            return null;
        }

        private static ToastHost Attach(Form owner)
        {
            var host = new ToastHost { Anchor = AnchorStyles.Top | AnchorStyles.Right };
            owner.Controls.Add(host);
            host.Location = new Point(owner.ClientSize.Width - host.Width - 16, 76);
            host.BringToFront();
            owner.Resize += (s, e) =>
                host.Location = new Point(owner.ClientSize.Width - host.Width - 16, 76);
            return host;
        }

        // ── Card ──
        [DesignerCategory("Code")]
        private sealed class ToastCard : Control
        {
            private readonly string _title;
            private readonly string _body;
            private readonly ToastKind _kind;
            private bool _hoverClose;
            public event EventHandler? CloseRequested;

            public ToastCard(string title, string body, ToastKind kind)
            {
                _title = title; _body = body; _kind = kind;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = Color.White;
                Cursor = Cursors.Default;
                AccessibleRole = AccessibleRole.Alert;
                AccessibleName = title;
            }

            public int DesiredHeight(int w)
            {
                int inner = w - 56 - 36;
                int th = TextRenderer.MeasureText(_title, AppTheme.FontToastTitle,
                    new Size(inner, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                int bh = string.IsNullOrEmpty(_body) ? 0 :
                    TextRenderer.MeasureText(_body, AppTheme.FontToastBody,
                    new Size(inner, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
                return Math.Max(64, 14 + th + bh + 14);
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                if (CloseRect().Contains(e.Location)) CloseRequested?.Invoke(this, EventArgs.Empty);
                base.OnMouseClick(e);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                bool h = CloseRect().Contains(e.Location);
                if (h != _hoverClose) { _hoverClose = h; Invalidate(); }
                base.OnMouseMove(e);
            }

            private Rectangle CloseRect() => new(Width - 30, 10, 20, 20);

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);
                var body = new Rectangle(0, 0, Width - 1, Height - 1);
                UiKit.Elevation(g, body, 10);
                UiKit.FillRounded(g, body, 10, Color.White);
                UiKit.StrokeRounded(g, body, 10, AppTheme.ToastBorder);

                Color accent = _kind switch
                {
                    ToastKind.Success => AppTheme.Success,
                    ToastKind.Warning => AppTheme.Warning,
                    ToastKind.Error => AppTheme.Danger,
                    _ => AppTheme.Info
                };
                string glyph = _kind switch
                {
                    ToastKind.Success => "\uE73E",
                    ToastKind.Warning => "\uE7BA",
                    ToastKind.Error => "\uE711",
                    _ => "\uE897"
                };
                // Accent bar
                using (var b = new SolidBrush(accent))
                    g.FillRectangle(b, new Rectangle(0, 10, 3, Height - 20));

                var iconBox = new Rectangle(14, 14, 28, 28);
                UiKit.FillRounded(g, iconBox, 8, UiKit.Wash(accent));
                using (var f = UiKit.GlyphFont(11F))
                    UiKit.Text(g, glyph, f, accent, iconBox, UiKit.Center);

                var tx = new Rectangle(50, 12, Width - 50 - 34, Height - 24);
                UiKit.Text(g, _title, AppTheme.FontToastTitle, AppTheme.ToastText,
                    new Rectangle(tx.X, tx.Y, tx.Width, 22),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                if (!string.IsNullOrEmpty(_body))
                    UiKit.Text(g, _body, AppTheme.FontToastBody, AppTheme.TextSecondary,
                        new Rectangle(tx.X, tx.Y + 22, tx.Width, tx.Height - 22),
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);

                using (var f = UiKit.GlyphFont(8F))
                    UiKit.Text(g, "\uE711", f,
                        _hoverClose ? UiKit.Ink : UiKit.InkFaint, CloseRect(), UiKit.Center);
            }
        }
    }

    /// <summary>One-line facade so pages don't need the host reference.</summary>
    public static class Toast
    {
        public static void Notify(Form? owner, string title, string? body = null, ToastKind kind = ToastKind.Success)
            => ToastHost.Show(owner, title, body, kind);
    }

    /// <summary>Convenience facade for Show calls from any Control or Form.</summary>
    public static class SaasToast
    {
        public static void Show(Control? owner, string title, ToastKind kind = ToastKind.Success, string? body = null)
        {
            Form? f = owner as Form ?? owner?.FindForm();
            Toast.Notify(f, title, body, kind);
        }

        public static void Show(Control? owner, string title, string body, ToastKind kind = ToastKind.Success)
        {
            Form? f = owner as Form ?? owner?.FindForm();
            Toast.Notify(f, title, body, kind);
        }
    }
}
