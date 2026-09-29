using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using CRM.winforms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// SaaS page header — breadcrumb · title · subtitle on the left, actions
    /// on the right. One hierarchy for every page (H4 consistency, H2 match
    /// to the real world). Pages adopt incrementally; the shell mirrors
    /// Title/Subtitle into the top bar so "where am I?" is answered twice
    /// (top bar for glance, header for context) without duplication.
    /// </summary>
    [DesignerCategory("Code")]
    public class PageHeaderControl : Control
    {
        private string _breadcrumb = "Fixory";
        private string _title = "Page";
        private string _subtitle = "";
        private readonly FlowLayoutPanel _actions;

        public PageHeaderControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Height = 64;

            _actions = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent
            };
            Controls.Add(_actions);
            Resize += (s, e) => LayoutActions();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Breadcrumb { get => _breadcrumb; set { _breadcrumb = value; Invalidate(); } }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public new string Text { get => _title; set { _title = value; Invalidate(); } }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Subtitle { get => _subtitle; set { _subtitle = value; Invalidate(); } }

        public void SetPrimaryAction(string text, EventHandler handler, string glyph = "")
        {
            var b = new SaasButton(text, SaasButtonVariant.Primary, glyph) { Size = new Size(130, 36) };
            b.Click += handler;
            _actions.Controls.Add(b);
            LayoutActions();
        }

        public void AddAction(Control c)
        {
            _actions.Controls.Add(c);
            LayoutActions();
        }

        public void ClearActions() { _actions.Controls.Clear(); }

        private void LayoutActions()
        {
            _actions.Location = new Point(Math.Max(0, Width - _actions.PreferredSize.Width), (Height - 36) / 2);
            _actions.Size = new Size(_actions.PreferredSize.Width, 40);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UiKit.Quality(g);
            UiKit.Text(g, _breadcrumb.ToUpperInvariant(), AppTheme.FontBreadcrumb, UiKit.InkFaint,
                new Rectangle(0, 0, Width - _actions.Width - 16, 16),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            UiKit.Text(g, _title, AppTheme.FontPageTitle, UiKit.Ink,
                new Rectangle(0, 16, Width - _actions.Width - 16, 26),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (!string.IsNullOrEmpty(_subtitle))
                UiKit.Text(g, _subtitle, AppTheme.FontPageSubtitle, UiKit.InkMuted,
                    new Rectangle(1, 42, Width - _actions.Width - 16, 20),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
