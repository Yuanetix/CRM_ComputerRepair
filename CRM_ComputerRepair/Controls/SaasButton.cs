using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms
{
    public enum SaasButtonVariant { Primary, Secondary, Danger, DangerOutline, Ghost }

    /// <summary>
    /// Single SaaS button for the whole system. HCI: 38px min hit target
    /// (Fitts), visible focus ring (WCAG 2.4.7), disabled state that still
    /// explains itself via tooltip, loading state that prevents double-submit.
    /// Replaces the 15+ hand-rolled FlatButton copies in list modules.
    /// </summary>
    [DesignerCategory("Code")]
    public class SaasButton : Control, IButtonControl
    {
        private SaasButtonVariant _variant = SaasButtonVariant.Primary;
        private bool _hover, _down, _loading;
        private string _glyph = "";
        private DialogResult _dialogResult = DialogResult.None;

        public SaasButton() : this("Button", SaasButtonVariant.Primary) { }

        public SaasButton(string text, SaasButtonVariant variant = SaasButtonVariant.Primary, string glyph = "")
        {
            Text = text; _variant = variant; _glyph = glyph;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            TabStop = true;
            Size = new Size(120, UiKit.T.ButtonHeight);
            AccessibleRole = AccessibleRole.PushButton;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public SaasButtonVariant Variant
        {
            get => _variant;
            set { _variant = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public bool Loading
        {
            get => _loading;
            set { _loading = value; Enabled = !value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Glyph
        {
            get => _glyph;
            set { _glyph = value; Invalidate(); }
        }

        public int PreferredWidth
        {
            get
            {
                int tw = UiKit.Measure(Text ?? "", AppTheme.FontButton).Width;
                int gw = string.IsNullOrEmpty(_glyph) ? 0 : 22;
                return tw + gw + 36;
            }
        }

        // IButtonControl — lets SaaS buttons act as Accept/Cancel in dialogs.
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DefaultValue(System.Windows.Forms.DialogResult.None)]
        public DialogResult DialogResult
        {
            get => _dialogResult;
            set => _dialogResult = value;
        }
        public void NotifyDefault(bool value) { }
        public void PerformClick()
        {
            if (Enabled && !_loading) InvokeOnClick(this, EventArgs.Empty);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!Enabled || _loading) return;
            if (e.KeyCode is Keys.Enter or Keys.Space) { InvokeOnClick(this, EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UiKit.Quality(g);
            var r = ClientRectangle;

            bool disabled = !Enabled || _loading;
            Color bg, fg, border = Color.Empty;
            switch (_variant)
            {
                case SaasButtonVariant.Primary:
                    bg = disabled ? UiKit.Line : _down ? AppTheme.PrimaryActive : _hover ? AppTheme.PrimaryHover : AppTheme.Primary;
                    fg = disabled ? UiKit.InkFaint : Color.White;
                    break;
                case SaasButtonVariant.Danger:
                    bg = disabled ? UiKit.Line : _down ? Color.FromArgb(185, 28, 28) : _hover ? Color.FromArgb(220, 50, 50) : AppTheme.Danger;
                    fg = disabled ? UiKit.InkFaint : Color.White;
                    break;
                case SaasButtonVariant.DangerOutline:
                    bg = disabled ? UiKit.Line : _down ? UiKit.Line : _hover ? AppTheme.DangerSoft : Color.White;
                    fg = disabled ? UiKit.InkFaint : AppTheme.Danger;
                    border = disabled ? UiKit.Line : AppTheme.Danger;
                    break;
                case SaasButtonVariant.Ghost:
                    bg = _down ? UiKit.Line : _hover ? UiKit.Hover : Color.Transparent;
                    fg = disabled ? UiKit.InkFaint : UiKit.Ink;
                    break;
                default:
                    bg = _down ? UiKit.Line : _hover ? UiKit.Hover : Color.White;
                    fg = disabled ? UiKit.InkFaint : UiKit.Ink;
                    border = UiKit.Line;
                    break;
            }

            UiKit.FillRounded(g, r, UiKit.RadiusSm, bg);
            if (border != Color.Empty) UiKit.StrokeRounded(g, r, UiKit.RadiusSm,
                _variant == SaasButtonVariant.DangerOutline && !disabled ? AppTheme.Danger :
                disabled ? UiKit.Line : AppTheme.BorderStrong);
            if (Focused && !disabled) UiKit.FocusRing(g, r, UiKit.RadiusSm);

            string label = _loading ? "Working…" : (Text ?? "");
            int glyphW = string.IsNullOrEmpty(_glyph) ? 0 : 20;
            int tw = UiKit.Measure(label, AppTheme.FontButton).Width;
            int total = tw + glyphW;
            int sx = (Width - total) / 2;
            int cy = Height / 2;

            if (!string.IsNullOrEmpty(_glyph))
            {
                using var f = UiKit.GlyphFont(11F);
                UiKit.Text(g, _glyph, f, fg, new Rectangle(sx, 0, 20, Height), UiKit.Center);
                sx += 20;
            }
            UiKit.Text(g, label, AppTheme.FontButton, fg,
                new Rectangle(sx, 0, tw + 4, Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
