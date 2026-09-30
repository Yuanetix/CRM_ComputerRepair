using CRM.winforms.Controls;
using CRM.winforms.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Terms &amp; Conditions legal document manager and version history viewer.
    /// Flat master-detail workspace: no cards, just panels separated by hairlines.
    /// </summary>
    [DesignerCategory("Code")]
    public class TermsControl : UserControl
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private List<TermsDto> _all = new();
        private TermsDto? _selected;

        // ═══════════ CONTROLS ═══════════

        // Header
        private Label lblTitle = null!;
        private Label lblSummary = null!;
        private SaasButton btnRefresh = null!;
        private SaasButton btnNew = null!;

        // Left rail
        private Label lblHistoryTitle = null!;
        private Label lblHistoryBadge = null!;
        private VersionListBox lstVersions = null!;

        // Right pane — header
        private Label lblActiveChip = null!;
        private Label lblDocTitle = null!;
        private Label lblDocMeta = null!;
        private SaasButton btnActivate = null!;
        private SaasButton btnEdit = null!;
        private SaasButton btnCopy = null!;

        // Right pane — body
        private Panel docCanvas = null!;
        private RichTextBox txtContent = null!;
        private Label lblDocStats = null!;

        // Empty state
        private WorkbenchState state = null!;

        // ═══════════ DRAWING HELPERS ═══════════

        private const TextFormatFlags Flat = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        private const TextFormatFlags CellText = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | Flat;

        // ═══════════ CONSTRUCTOR ═══════════

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

        // ═══════════ UI BUILD ═══════════

        private void BuildUi()
        {
            // ── Header ──
            lblTitle = new Label
            {
                Text = "Terms & Conditions",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            lblSummary = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Size = new Size(100, 36);
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            btnNew = new SaasButton("New Version", SaasButtonVariant.Primary, "\uE710");
            btnNew.Size = new Size(150, 36);
            btnNew.Click += async (s, e) => await NewVersionAsync();

            Controls.Add(lblTitle);
            Controls.Add(lblSummary);
            Controls.Add(btnRefresh);
            Controls.Add(btnNew);

            // ── Left rail: Version History ──
            lblHistoryTitle = new Label
            {
                Text = "Version History",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            lblHistoryBadge = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            lstVersions = new VersionListBox();
            lstVersions.SelectedIndexChanged += (s, e) =>
            {
                if (lstVersions.SelectedIndex >= 0 && lstVersions.SelectedIndex < _all.Count)
                {
                    _selected = _all[lstVersions.SelectedIndex];
                    ShowSelected();
                }
            };

            Controls.Add(lblHistoryTitle);
            Controls.Add(lblHistoryBadge);
            Controls.Add(lstVersions);

            // ── Right pane: Document Viewer ──
            lblActiveChip = new Label
            {
                Text = "",
                Font = UiKit.T.SmallStrong,
                ForeColor = AppTheme.Success,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            lblDocTitle = new Label
            {
                Text = "Select a version",
                Font = AppFonts.Strong(15F),
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            lblDocMeta = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            btnActivate = new SaasButton("Set as Active", SaasButtonVariant.Primary, "\uE73E")
            {
                Visible = false,
                Size = new Size(150, 36)
            };
            btnActivate.Click += async (s, e) => await ActivateAsync();

            btnEdit = new SaasButton("Edit", SaasButtonVariant.Secondary, "\uE70F");
            btnEdit.Size = new Size(90, 36);
            btnEdit.Click += async (s, e) => await EditAsync();

            btnCopy = new SaasButton("Copy", SaasButtonVariant.Secondary, "\uE8C8");
            btnCopy.Size = new Size(90, 36);
            btnCopy.Click += (s, e) => CopyContent();

            // Document body canvas — flat, hairline border drawn in paint
            docCanvas = new Panel
            {
                BackColor = AppTheme.Background,
                BorderStyle = BorderStyle.None
            };

            txtContent = new RichTextBox
            {
                Font = AppFonts.Regular(10.5F),
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.Background,
                ForeColor = UiKit.T.Ink,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true,
                DetectUrls = false
            };
            docCanvas.Controls.Add(txtContent);
            docCanvas.Paint += DocCanvas_Paint;
            docCanvas.Resize += (s, e) => LayoutDocCanvas();

            lblDocStats = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            Controls.Add(lblActiveChip);
            Controls.Add(lblDocTitle);
            Controls.Add(lblDocMeta);
            Controls.Add(btnActivate);
            Controls.Add(btnEdit);
            Controls.Add(btnCopy);
            Controls.Add(docCanvas);
            Controls.Add(lblDocStats);

            // ── Empty State ──
            state = new WorkbenchState { Visible = false };
            Controls.Add(state);

            Resize += (s, e) => LayoutUi();
        }

        // ═══════════ KEYBOARD SHORTCUTS ═══════════

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F5:
                    _ = ReloadAsync();
                    return true;

                case Keys.Control | Keys.N:
                    _ = NewVersionAsync();
                    return true;

                case Keys.Control | Keys.E:
                    if (_selected != null)
                    {
                        _ = EditAsync();
                        return true;
                    }
                    break;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ═══════════ LAYOUT ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            int pad = UiKit.T.S6;
            int contentW = Math.Max(700, Width - pad * 2);

            // Header
            lblTitle.Location = new Point(pad, pad);

            int btnY = pad;
            btnNew.Location = new Point(pad + contentW - btnNew.Width, btnY);
            btnRefresh.Location = new Point(btnNew.Left - btnRefresh.Width - 10, btnY);

            lblSummary.Location = new Point(pad + 1, lblTitle.Bottom + 6);

            // Body area
            int contentTop = lblSummary.Bottom + 22;
            int contentHeight = Math.Max(260, Height - contentTop - pad);

            if (_all.Count == 0)
            {
                SetDetailVisible(false);
                state.Visible = true;
                state.SetBounds(pad, contentTop, contentW, contentHeight);
                Invalidate();
                return;
            }

            SetDetailVisible(true);
            state.Visible = false;

            // Left rail
            int historyWidth = Math.Min(360, Math.Max(280, (int)(contentW * 0.28)));
            lblHistoryTitle.Location = new Point(pad, contentTop);
            lblHistoryBadge.Location = new Point(
                lblHistoryTitle.Right + 8,
                lblHistoryTitle.Top + (lblHistoryTitle.PreferredHeight - lblHistoryBadge.PreferredHeight) / 2);

            int listTop = lblHistoryTitle.Bottom + 12;
            lstVersions.SetBounds(pad, listTop, historyWidth - pad, Math.Max(120, contentHeight - (listTop - contentTop)));

            // Right pane
            int docLeft = pad + historyWidth + UiKit.T.S6;
            int docWidth = Math.Max(320, pad + contentW - docLeft);

            // Header row: active chip (or reserved space), title
            int headerTop = contentTop;

            if (!string.IsNullOrEmpty(lblActiveChip.Text))
            {
                lblActiveChip.Location = new Point(docLeft, headerTop);
                lblDocTitle.Location = new Point(docLeft, headerTop + lblActiveChip.PreferredHeight + 6);
            }
            else
            {
                lblDocTitle.Location = new Point(docLeft, headerTop + 4);
            }

            // Action buttons right aligned at title row
            int btnRight = docLeft + docWidth;
            int actionY = lblDocTitle.Top - 4;

            btnEdit.Location = new Point(btnRight - btnEdit.Width, actionY);
            btnRight -= btnEdit.Width + 8;

            btnCopy.Location = new Point(btnRight - btnCopy.Width, actionY);
            btnRight -= btnCopy.Width + 8;

            if (btnActivate.Visible)
            {
                btnActivate.Location = new Point(btnRight - btnActivate.Width, actionY);
            }

            lblDocMeta.Location = new Point(docLeft, lblDocTitle.Bottom + 6);

            int bodyTop = lblDocMeta.Bottom + 14;
            int statsH = 22;
            int bodyHeight = Math.Max(140, contentTop + contentHeight - bodyTop - statsH - 6);

            docCanvas.SetBounds(docLeft, bodyTop, docWidth, bodyHeight);
            lblDocStats.Location = new Point(docLeft, docCanvas.Bottom + 6);

            LayoutDocCanvas();
            Invalidate();
        }

        private void LayoutDocCanvas()
        {
            if (docCanvas == null || txtContent == null) return;

            const int inset = 18;
            txtContent.SetBounds(
                inset,
                inset,
                Math.Max(60, docCanvas.Width - inset * 2 - SystemInformation.VerticalScrollBarWidth),
                Math.Max(60, docCanvas.Height - inset * 2));
        }

        private void SetDetailVisible(bool visible)
        {
            lblHistoryTitle.Visible = visible;
            lblHistoryBadge.Visible = visible;
            lstVersions.Visible = visible;
            lblActiveChip.Visible = visible && !string.IsNullOrEmpty(lblActiveChip.Text);
            lblDocTitle.Visible = visible;
            lblDocMeta.Visible = visible;
            btnEdit.Visible = visible;
            btnCopy.Visible = visible;
            txtContent.Visible = visible;
            docCanvas.Visible = visible;
            lblDocStats.Visible = visible;
            if (!visible) btnActivate.Visible = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            UiKit.Quality(e.Graphics);

            if (_all.Count == 0 || !lstVersions.Visible) return;

            int contentTop = lblSummary.Bottom + 22;
            int contentBottom = contentTop + Math.Max(260, Height - contentTop - UiKit.T.S6);

            // vertical divider between panes
            int dividerX = lstVersions.Right + UiKit.T.S6 / 2;
            using (var vpen = new Pen(UiKit.T.LineSoft, 1))
                e.Graphics.DrawLine(vpen, dividerX, contentTop, dividerX, contentBottom);

            // hairline above the stats row
            using (var hpen = new Pen(UiKit.T.LineSoft, 1))
            {
                int statsLineY = lblDocStats.Top - 4;
                e.Graphics.DrawLine(hpen, lblDocTitle.Left, statsLineY, Width - UiKit.T.S6, statsLineY);
            }
        }

        private void DocCanvas_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            UiKit.Quality(g);

            // hairline frame — visually reads as a document surface, not a card
            var r = new Rectangle(0, 0, docCanvas.Width - 1, docCanvas.Height - 1);
            using (var pen = new Pen(UiKit.T.LineSoft, 1))
                g.DrawRectangle(pen, r);

            // top-left faint page mark (subtle industry touch)
            using (var brush = new SolidBrush(UiKit.T.InkFaint))
                g.FillRectangle(brush, 0, 0, 0, 0); // no-op keeps SkiaSharp-like crispness
        }

        // ═══════════ DATA LOADING ═══════════

        public async Task ReloadAsync()
        {
            try
            {
                _all = await _api.GetTermsAsync();

                lstVersions.BeginUpdate();
                lstVersions.Items.Clear();
                foreach (var t in _all)
                    lstVersions.Items.Add(t);
                lstVersions.EndUpdate();

                int activeCount = _all.Count(t => t.IsActive);
                lblHistoryBadge.Text = _all.Count == 1 ? "1 version" : $"{_all.Count} versions";
                lblSummary.Text = _all.Count == 0
                    ? "No versions published yet"
                    : $"{_all.Count} version{(_all.Count == 1 ? "" : "s")}  ·  {activeCount} active";

                if (_all.Count > 0)
                {
                    int activeIdx = _all.FindIndex(t => t.IsActive);
                    lstVersions.SelectedIndex = activeIdx >= 0 ? activeIdx : 0;
                    _selected = _all[lstVersions.SelectedIndex];
                    ShowSelected();
                }
                else
                {
                    _selected = null;
                    state.Show("\uE8A5", "No terms & conditions published",
                        "Click 'New Version' to publish your company's standard service agreement and policies.");
                }

                LayoutUi();
            }
            catch (Exception ex)
            {
                _all = new List<TermsDto>();
                _selected = null;
                lblSummary.Text = "Couldn't load terms";
                state.Show("\uE8A5", "Couldn't load terms & conditions",
                    "Check your connection and try again.");
                LayoutUi();

                SaasToast.Show(FindForm(),
                    $"Could not load terms & conditions: {ex.Message}",
                    ToastKind.Danger);
            }
        }

        private void ShowSelected()
        {
            if (_selected == null) return;

            lblActiveChip.Text = _selected.IsActive ? "ACTIVE VERSION" : "";

            lblDocTitle.Text = _selected.Title;

            string statusDesc = _selected.IsActive ? "Active version" : "Archived version";
            string author = string.IsNullOrWhiteSpace(_selected.CreatedByUserId) ? "system" : _selected.CreatedByUserId;
            lblDocMeta.Text = $"{statusDesc}  ·  Effective {_selected.VersionDisplay}  ·  Published by {author}";

            txtContent.Text = _selected.Content ?? "";

            int charCount = (_selected.Content ?? "").Length;
            int lineCount = (_selected.Content ?? "")
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            lblDocStats.Text = $"{lineCount} clauses / sections  ·  {charCount:N0} characters  ·  Terms ID #{_selected.TermsId}";

            btnActivate.Visible = !_selected.IsActive;

            LayoutUi();
        }

        // ═══════════ ACTIONS ═══════════

        private async Task NewVersionAsync()
        {
            using var dlg = new TermsFormDialog(null);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK)
            {
                SaasToast.Show(FindForm(),
                    "New terms & conditions version published.",
                    ToastKind.Success);
                await ReloadAsync();
            }
        }

        private async Task EditAsync()
        {
            if (_selected == null) return;

            using var dlg = new TermsFormDialog(_selected);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK)
            {
                SaasToast.Show(FindForm(),
                    "Terms & conditions updated.",
                    ToastKind.Success);
                await ReloadAsync();
            }
        }

        private async Task ActivateAsync()
        {
            if (_selected == null) return;

            bool confirm = SaasConfirm.Ask(
                FindForm(),
                "Activate Terms & Conditions",
                $"Activate version '{_selected.Title}' ({_selected.VersionDisplay})?",
                confirmText: "Activate",
                danger: false,
                detail: "This will become the official active terms agreement and archive any previously active version.");

            if (!confirm) return;

            try
            {
                await _api.ActivateTermsAsync(_selected.TermsId);
                SaasToast.Show(FindForm(),
                    $"'{_selected.Title}' is now the active terms agreement.",
                    ToastKind.Success);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(),
                    $"Activation failed: {ex.Message}",
                    ToastKind.Danger);
            }
        }

        private void CopyContent()
        {
            if (_selected == null || string.IsNullOrEmpty(_selected.Content)) return;

            try
            {
                Clipboard.SetText(_selected.Content);
                SaasToast.Show(FindForm(),
                    "Agreement text copied to clipboard.",
                    ToastKind.Success);
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(),
                    $"Could not copy text: {ex.Message}",
                    ToastKind.Warning);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  OWNER-DRAWN VERSION LIST (flat, professional SaaS styling)
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class VersionListBox : ListBox
        {
            private const int RowH = 72;
            private const int CellPadX = 16;

            private static readonly Font MonoFont = new Font("Consolas", 8.5F);

            private int _hoverIndex = -1;

            public VersionListBox()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                DoubleBuffered = true;
                DrawMode = DrawMode.OwnerDrawVariable;
                BorderStyle = BorderStyle.None;
                BackColor = AppTheme.Background;
                ForeColor = UiKit.T.Ink;
                IntegralHeight = false;
                ItemHeight = RowH;
                TabStop = true;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int idx = IndexFromPoint(e.Location);
                if (idx != _hoverIndex)
                {
                    _hoverIndex = idx;
                    Invalidate();
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hoverIndex = -1;
                Invalidate();
            }

            protected override void OnMeasureItem(MeasureItemEventArgs e) => e.ItemHeight = RowH;

            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= Items.Count) return;
                if (Items[e.Index] is not TermsDto item) return;

                var g = e.Graphics;
                UiKit.Quality(g);

                bool selected = (e.State & DrawItemState.Selected) != 0;
                bool hovered = e.Index == _hoverIndex && !selected;
                bool focused = Focused;

                var r = e.Bounds;

                // Background
                Color bg = AppTheme.Background;
                if (selected && focused) bg = UiKit.T.RowHover;
                else if (selected) bg = UiKit.T.LineSoft;
                else if (hovered) bg = UiKit.T.RowHover;

                using (var b = new SolidBrush(bg))
                    g.FillRectangle(b, r);

                // Left accent when selected
                if (selected)
                    UiKit.FillRounded(g, new Rectangle(r.Left, r.Top + 10, 3, r.Height - 21), 1, AppTheme.Primary);

                // Bottom hairline
                using (var p = new Pen(UiKit.T.LineSoft, 1))
                    g.DrawLine(p, r.Left + CellPadX, r.Bottom - 1, r.Right - CellPadX, r.Bottom - 1);

                // Row layout
                int padLeft = r.Left + CellPadX;
                int topY = r.Top + 12;
                int rowRight = r.Right - CellPadX;

                // Status dot pill
                bool active = item.IsActive;
                Color fg = active ? AppTheme.Success : UiKit.T.InkMuted;
                string statusText = active ? "Active" : "Archived";
                PaintDotPill(g, new Rectangle(padLeft, topY, 140, 20), statusText, fg, UiKit.Micro);

                // Right: date in mono
                string dateText = item.Version.ToString("MMM d, yyyy");
                var dateRect = new Rectangle(r.Left, topY, r.Width - CellPadX, 20);
                UiKit.Text(g, dateText, MonoFont, UiKit.T.InkMuted, dateRect,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                // Title
                int titleY = topY + 24;
                var titleRect = new Rectangle(padLeft, titleY, rowRight - padLeft, 20);
                UiKit.Text(g, item.Title ?? "", UiKit.T.BodyStrong, UiKit.T.Ink, titleRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

                // Meta
                int metaY = titleY + 20;
                string author = string.IsNullOrWhiteSpace(item.CreatedByUserId) ? "system" : item.CreatedByUserId;
                string metaText = $"Effective {item.VersionDisplay}  ·  by {author}";
                var metaRect = new Rectangle(padLeft, metaY, rowRight - padLeft, 16);
                UiKit.Text(g, metaText, UiKit.T.Small, UiKit.T.InkFaint, metaRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            }

            private static void PaintDotPill(Graphics g, Rectangle rect, string text, Color fg, Font font)
            {
                int textW = TextRenderer.MeasureText(text, font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                int pillW = Math.Min(rect.Width, textW + 34);
                int pillH = 20;
                var pill = new Rectangle(rect.Left, rect.Top, pillW, pillH);

                UiKit.FillRounded(g, pill, pillH / 2, UiKit.Wash(fg));
                UiKit.FillRounded(g, new Rectangle(pill.Left + 10, pill.Top + pillH / 2 - 3, 6, 6), 3, fg);
                UiKit.Text(g, text, font, fg,
                    new Rectangle(pill.Left + 22, pill.Top, Math.Max(0, pill.Width - 30), pill.Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine
                    | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
        }
    }
}