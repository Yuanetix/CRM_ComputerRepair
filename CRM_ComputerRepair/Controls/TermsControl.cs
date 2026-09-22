using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    [DesignerCategory("Code")]
    public class TermsControl : UserControl
    {
        private readonly ApiClient _api = new ApiClient();
        private List<TermsDto> _all = new();
        private TermsDto? _selected;

        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private FlatButton btnNew = null!;
        private FlatButton btnRefresh = null!;

        private SurfaceCard card = null!;

        private Panel pnlList = null!;
        private ListBox lstTerms = null!;
        private Label lblListTitle = null!;

        private Panel pnlViewer = null!;
        private Label lblViewerTitle = null!;
        private Label lblViewerMeta = null!;
        private RichTextBox txtContent = null!;
        private FlatButton btnEdit = null!;
        private FlatButton btnActivate = null!;

        private StateView state = null!;

        public TermsControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            AutoScaleMode = AutoScaleMode.Font;

            BuildUi();

            this.Load += async (s, e) => await ReloadAsync();
        }

        private void BuildUi()
        {
            lblTitle = new Label
            {
                Text = "Terms & Conditions",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Company terms and conditions, versioned",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnNew = new FlatButton("New version", "\uE710");
            btnNew.Click += async (s, e) => await NewVersionAsync();

            btnRefresh = new FlatButton("Refresh", "\uE72C");
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnNew);
            Controls.Add(btnRefresh);

            card = new SurfaceCard();

            // Left: list
            pnlList = new Panel { BackColor = UiKit.T.Surface };

            lblListTitle = new Label
            {
                Text = "Versions",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(UiKit.T.S4, UiKit.T.S4)
            };
            pnlList.Controls.Add(lblListTitle);

            lstTerms = new ListBox
            {
                Font = UiKit.T.Body,
                BorderStyle = BorderStyle.None,
                BackColor = UiKit.T.Surface,
                ForeColor = UiKit.T.Ink,
                IntegralHeight = false,
                ItemHeight = 42
            };
            lstTerms.SelectedIndexChanged += (s, e) =>
            {
                if (lstTerms.SelectedIndex < 0 || lstTerms.SelectedIndex >= _all.Count) return;
                _selected = _all[lstTerms.SelectedIndex];
                ShowSelected();
            };
            pnlList.Controls.Add(lstTerms);

            // Right: viewer
            pnlViewer = new Panel { BackColor = UiKit.T.Surface };

            lblViewerTitle = new Label
            {
                Text = "Select a version",
                Font = new Font("Segoe UI Semibold", 14F),
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(UiKit.T.S4, UiKit.T.S4)
            };
            pnlViewer.Controls.Add(lblViewerTitle);

            lblViewerMeta = new Label
            {
                Text = "",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(UiKit.T.S4, UiKit.T.S4 + 30)
            };
            pnlViewer.Controls.Add(lblViewerMeta);

            btnActivate = new FlatButton("Activate", "\uE73E");
            btnActivate.Click += async (s, e) => await ActivateAsync();

            btnEdit = new FlatButton("Edit", "\uE70F");
            btnEdit.Click += async (s, e) => await EditAsync();

            pnlViewer.Controls.Add(btnActivate);
            pnlViewer.Controls.Add(btnEdit);

            txtContent = new RichTextBox
            {
                Font = new Font("Segoe UI", 10F),
                BorderStyle = BorderStyle.None,
                BackColor = UiKit.T.Surface,
                ForeColor = UiKit.T.Ink,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true
            };
            pnlViewer.Controls.Add(txtContent);

            card.Controls.Add(pnlList);
            card.Controls.Add(pnlViewer);

            state = new StateView { Visible = false };
            card.Controls.Add(state);

            Controls.Add(card);

            Resize += (s, e) => LayoutUi();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            UiKit.Quality(e.Graphics);
            int y = lblSubtitle.Bottom + UiKit.T.S4;
            using var pen = new Pen(UiKit.T.Line, 1);
            e.Graphics.DrawLine(pen, 0, y, Width, y);
        }

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            lblTitle.Location = new Point(0, 0);
            int subtitleY = lblTitle.PreferredHeight + UiKit.T.S1;
            lblSubtitle.Location = new Point(1, subtitleY);

            int rightX = Width;
            btnRefresh.Size = new Size(btnRefresh.PreferredWidth, UiKit.T.ButtonHeight);
            btnRefresh.Location = new Point(rightX - btnRefresh.Width, UiKit.T.S1);
            rightX -= btnRefresh.Width + 8;

            btnNew.Size = new Size(btnNew.PreferredWidth, UiKit.T.ButtonHeight);
            btnNew.Location = new Point(rightX - btnNew.Width, UiKit.T.S1);

            int dividerY = subtitleY + lblSubtitle.PreferredHeight + UiKit.T.S4;
            int cardTop = dividerY + UiKit.T.S5;
            int cardH = Math.Max(300, Height - cardTop);

            card.Location = new Point(0, cardTop);
            card.Size = new Size(Width, cardH);

            const int cp = UiKit.T.S5;
            int innerTop = cp;
            int innerBottom = card.Height - cp;
            int innerLeft = cp;
            int innerRight = card.Width - cp;

            int listW = 280;
            pnlList.Location = new Point(innerLeft, innerTop);
            pnlList.Size = new Size(listW, innerBottom - innerTop);

            lblListTitle.Location = new Point(UiKit.T.S4, UiKit.T.S4);
            lstTerms.Location = new Point(UiKit.T.S4, UiKit.T.S4 + 30);
            lstTerms.Size = new Size(listW - UiKit.T.S4 * 2, pnlList.Height - UiKit.T.S4 - 30 - UiKit.T.S4);

            int viewerLeft = innerLeft + listW + UiKit.T.S5;
            pnlViewer.Location = new Point(viewerLeft, innerTop);
            pnlViewer.Size = new Size(
                Math.Max(200, innerRight - viewerLeft),
                innerBottom - innerTop);

            lblViewerTitle.Location = new Point(UiKit.T.S4, UiKit.T.S4);
            lblViewerMeta.Location = new Point(UiKit.T.S4, UiKit.T.S4 + 30);

            int btnY = UiKit.T.S4;
            int btnRight = pnlViewer.Width - UiKit.T.S4;

            btnEdit.Size = new Size(btnEdit.PreferredWidth, UiKit.T.ButtonHeight);
            btnEdit.Location = new Point(btnRight - btnEdit.Width, btnY);
            btnRight -= btnEdit.Width + 8;

            btnActivate.Size = new Size(btnActivate.PreferredWidth, UiKit.T.ButtonHeight);
            btnActivate.Location = new Point(btnRight - btnActivate.Width, btnY);

            int contentTop = UiKit.T.S4 + 60;
            txtContent.Location = new Point(UiKit.T.S4, contentTop);
            txtContent.Size = new Size(
                pnlViewer.Width - UiKit.T.S4 * 2,
                Math.Max(80, pnlViewer.Height - contentTop - UiKit.T.S4));

            state.Location = new Point(innerLeft, innerTop);
            state.Size = new Size(innerRight - innerLeft, innerBottom - innerTop);
        }

        public async Task ReloadAsync()
        {
            try
            {
                _all = await _api.GetTermsAsync();

                lstTerms.Items.Clear();
                foreach (var t in _all)
                {
                    var prefix = t.IsActive ? "● " : "○ ";
                    lstTerms.Items.Add($"{prefix}{t.Title}  ({t.VersionDisplay})");
                }

                if (_all.Count > 0)
                {
                    lstTerms.SelectedIndex = 0;
                    pnlList.Visible = true;
                    pnlViewer.Visible = true;
                    state.Visible = false;
                }
                else
                {
                    _selected = null;
                    pnlList.Visible = false;
                    pnlViewer.Visible = false;
                    state.Show("\uE8A5", "No terms yet",
                        "Use New version to publish the first set of Terms & Conditions.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't load terms.\n\n{ex.Message}",
                    "Connection problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowSelected()
        {
            if (_selected == null) return;

            lblViewerTitle.Text = _selected.Title;
            lblViewerMeta.Text =
                $"Version: {_selected.VersionDisplay}  ·  Status: {_selected.StatusText}" +
                (string.IsNullOrWhiteSpace(_selected.CreatedByUserId)
                    ? ""
                    : $"  ·  By: {_selected.CreatedByUserId}");

            txtContent.Text = _selected.Content ?? "";

            btnActivate.Visible = !_selected.IsActive;
        }

        private async Task NewVersionAsync()
        {
            using var dlg = new TermsFormDialog(null);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                await ReloadAsync();
        }

        private async Task EditAsync()
        {
            if (_selected == null) return;

            using var dlg = new TermsFormDialog(_selected);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                await ReloadAsync();
        }

        private async Task ActivateAsync()
        {
            if (_selected == null) return;

            try
            {
                await _api.ActivateTermsAsync(_selected.TermsId);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Activate failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═══════════ NESTED UI ═══════════

        [DesignerCategory("Code")]
        private sealed class SurfaceCard : Panel
        {
            public SurfaceCard()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                UiKit.Quality(e.Graphics);
                using (var b = new SolidBrush(AppTheme.Background))
                    e.Graphics.FillRectangle(b, ClientRectangle);
                UiKit.Card(e.Graphics, ClientRectangle, UiKit.T.Radius, UiKit.T.Surface, UiKit.T.Line);
                base.OnPaint(e);
            }
        }

        [DesignerCategory("Code")]
        private sealed class FlatButton : Control
        {
            private readonly string _glyph;
            private bool _hover, _down;

            public FlatButton(string text, string glyph)
            {
                _glyph = glyph;
                Text = text;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
                Cursor = Cursors.Hand;
                Font = UiKit.T.BodyStrong;
                TabStop = true;
            }

            public int PreferredWidth => UiKit.Measure(Text, UiKit.T.BodyStrong).Width + 56;

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);
                using (var b = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(b, ClientRectangle);

                Color bg = _down ? AppTheme.PrimaryActive : _hover ? AppTheme.PrimaryHover : AppTheme.Primary;
                UiKit.FillRounded(g, ClientRectangle, 8, bg);

                UiKit.Text(g, _glyph, new Font("Segoe MDL2 Assets", 10F), Color.White,
                    new Rectangle(16, 0, 18, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                UiKit.Text(g, Text, UiKit.T.BodyStrong, Color.White,
                    new Rectangle(36, 0, Width - 46, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        [DesignerCategory("Code")]
        private sealed class StateView : Control
        {
            private string _glyph = "";
            private string _title = "";
            private string _message = "";

            public StateView()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = UiKit.T.Surface;
            }

            public void Show(string glyph, string title, string message)
            {
                _glyph = glyph; _title = title; _message = message;
                Visible = true; BringToFront(); Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);
                using (var b = new SolidBrush(UiKit.T.Surface))
                    g.FillRectangle(b, ClientRectangle);

                int cy = Height / 2 - 40;
                var circle = new Rectangle(Width / 2 - 26, cy, 52, 52);
                UiKit.FillRounded(g, circle, 26, UiKit.T.LineSoft);
                UiKit.Text(g, _glyph, UiKit.T.GlyphLarge, UiKit.T.InkFaint, circle,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                UiKit.Text(g, _title, UiKit.T.Section, UiKit.T.Ink,
                    new Rectangle(0, circle.Bottom + UiKit.T.S4, Width, 24),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);

                int msgW = Math.Min(420, Width - UiKit.T.S6 * 2);
                UiKit.Text(g, _message, UiKit.T.Body, UiKit.T.InkMuted,
                    new Rectangle((Width - msgW) / 2, circle.Bottom + UiKit.T.S4 + 28, msgW, 60),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
            }
        }
    }
}