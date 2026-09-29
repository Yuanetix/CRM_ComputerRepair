using CRM.winforms.Controls;
using CRM.winforms.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Terms & Conditions legal document manager and version history viewer.
    /// Modern SaaS master-detail workspace matching Dashboard, Reports, and Retention.
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
        private Label lblSubtitle = null!;
        private SaasButton btnRefresh = null!;
        private SaasButton btnNew = null!;

        // Left Pane: Version History
        private HistoryCard cardHistory = null!;
        private Label lblHistoryTitle = null!;
        private Label lblHistoryBadge = null!;
        private VersionListBox lstVersions = null!;

        // Right Pane: Document Viewer
        private DocumentCard cardDocument = null!;
        private Label lblDocTitle = null!;
        private Label lblDocMeta = null!;
        private SaasButton btnActivate = null!;
        private SaasButton btnEdit = null!;
        private SaasButton btnCopy = null!;
        private RichTextBox txtContent = null!;
        private Label lblDocStats = null!;

        // Empty state
        private StateView state = null!;

        private readonly ToolTip _tips = new ToolTip { InitialDelay = 400, ReshowDelay = 200 };

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
                BackColor = AppTheme.Background,
                UseMnemonic = false
            };

            lblSubtitle = new Label
            {
                Text = "Company service agreements, repair liability policies, and version history",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = AppTheme.Background,
                UseMnemonic = false
            };

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Click += async (s, e) => await ReloadAsync();
            _tips.SetToolTip(btnRefresh, "Refresh terms versions (F5)");

            btnNew = new SaasButton("New Version", SaasButtonVariant.Primary, "\uE710");
            btnNew.Click += async (s, e) => await NewVersionAsync();
            _tips.SetToolTip(btnNew, "Publish a new terms version (Ctrl+N)");

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnRefresh);
            Controls.Add(btnNew);

            // ── Left: Version History Panel ──
            cardHistory = new HistoryCard();

            lblHistoryTitle = new Label
            {
                Text = "Version History",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };

            lblHistoryBadge = new Label
            {
                Text = "",
                Font = UiKit.T.SmallStrong,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
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

            cardHistory.Controls.Add(lblHistoryTitle);
            cardHistory.Controls.Add(lblHistoryBadge);
            cardHistory.Controls.Add(lstVersions);
            Controls.Add(cardHistory);

            // ── Right: Document Viewer Panel ──
            cardDocument = new DocumentCard();

            lblDocTitle = new Label
            {
                Text = "Select a version",
                Font = AppFonts.Strong(13.5F),
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };

            lblDocMeta = new Label
            {
                Text = "",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };

            btnActivate = new SaasButton("Set as Active", SaasButtonVariant.Primary, "\uE73E")
            {
                Visible = false
            };
            btnActivate.Click += async (s, e) => await ActivateAsync();
            _tips.SetToolTip(btnActivate, "Set this version as the official active customer terms");

            btnEdit = new SaasButton("Edit", SaasButtonVariant.Secondary, "\uE70F");
            btnEdit.Click += async (s, e) => await EditAsync();
            _tips.SetToolTip(btnEdit, "Edit title or text of this version (Ctrl+E)");

            btnCopy = new SaasButton("Copy", SaasButtonVariant.Secondary, "\uE8C8");
            btnCopy.Click += (s, e) => CopyContent();
            _tips.SetToolTip(btnCopy, "Copy agreement text to clipboard");

            txtContent = new RichTextBox
            {
                Font = AppFonts.Regular(10.5F),
                BorderStyle = BorderStyle.None,
                BackColor = UiKit.T.Surface,
                ForeColor = UiKit.T.Ink,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true
            };

            lblDocStats = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };

            cardDocument.Controls.Add(lblDocTitle);
            cardDocument.Controls.Add(lblDocMeta);
            cardDocument.Controls.Add(btnActivate);
            cardDocument.Controls.Add(btnEdit);
            cardDocument.Controls.Add(btnCopy);
            cardDocument.Controls.Add(txtContent);
            cardDocument.Controls.Add(lblDocStats);
            Controls.Add(cardDocument);

            // ── Empty State ──
            state = new StateView { Visible = false };
            Controls.Add(state);

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

        // ═══════════ RESPONSIVE LAYOUT ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            // Header labels
            lblTitle.Location = new Point(0, 0);

            int subtitleY = lblTitle.PreferredHeight + 6;
            lblSubtitle.Location = new Point(1, subtitleY);

            // Header buttons
            btnNew.Size = new Size(btnNew.PreferredWidth, UiKit.T.ButtonHeight);
            btnNew.Location = new Point(Width - btnNew.Width, 2);

            btnRefresh.Size = new Size(btnRefresh.PreferredWidth, UiKit.T.ButtonHeight);
            btnRefresh.Location = new Point(btnNew.Left - btnRefresh.Width - UiKit.T.S2, 2);

            int dividerY = subtitleY + lblSubtitle.PreferredHeight + UiKit.T.S4;

            // Main Content Area
            int contentTop = dividerY + UiKit.T.S5;
            int contentHeight = Math.Max(260, Height - contentTop);

            if (_all.Count == 0)
            {
                cardHistory.Visible = false;
                cardDocument.Visible = false;
                state.Visible = true;
                state.Location = new Point(0, contentTop);
                state.Size = new Size(Width, contentHeight);
                return;
            }

            cardHistory.Visible = true;
            cardDocument.Visible = true;
            state.Visible = false;

            // Left Pane (Version History)
            int historyWidth = Math.Min(340, Math.Max(280, (int)(Width * 0.28)));
            cardHistory.Location = new Point(0, contentTop);
            cardHistory.Size = new Size(historyWidth, contentHeight);

            // History header controls
            const int hp = 16;
            lblHistoryTitle.Location = new Point(hp, 16);
            lblHistoryBadge.Location = new Point(
                lblHistoryTitle.Right + 8,
                lblHistoryTitle.Top + (lblHistoryTitle.Height - lblHistoryBadge.Height) / 2);

            int listTop = 52;
            lstVersions.Location = new Point(1, listTop);
            lstVersions.Size = new Size(cardHistory.Width - 2, Math.Max(40, cardHistory.Height - listTop - 1));

            // Right Pane (Document Viewer)
            int docLeft = cardHistory.Right + UiKit.T.S4;
            int docWidth = Math.Max(320, Width - docLeft);
            cardDocument.Location = new Point(docLeft, contentTop);
            cardDocument.Size = new Size(docWidth, contentHeight);

            LayoutDocumentViewer();
        }

        private void LayoutDocumentViewer()
        {
            const int dp = 20;

            lblDocTitle.Location = new Point(dp, 16);
            lblDocMeta.Location = new Point(dp, lblDocTitle.Bottom + 4);

            // Document action buttons (right-aligned in header)
            int btnRight = cardDocument.Width - dp;
            int btnY = 16;

            btnEdit.Size = new Size(btnEdit.PreferredWidth, UiKit.T.ButtonHeight);
            btnEdit.Location = new Point(btnRight - btnEdit.Width, btnY);
            btnRight -= btnEdit.Width + 8;

            btnCopy.Size = new Size(btnCopy.PreferredWidth, UiKit.T.ButtonHeight);
            btnCopy.Location = new Point(btnRight - btnCopy.Width, btnY);
            btnRight -= btnCopy.Width + 8;

            if (btnActivate.Visible)
            {
                btnActivate.Size = new Size(btnActivate.PreferredWidth, UiKit.T.ButtonHeight);
                btnActivate.Location = new Point(btnRight - btnActivate.Width, btnY);
            }

            // Divider position
            int headerBottom = Math.Max(lblDocMeta.Bottom + 16, 68);

            // Text content canvas
            int footerH = 40;
            int txtTop = headerBottom + 12;
            int txtHeight = Math.Max(80, cardDocument.Height - txtTop - footerH);

            txtContent.Location = new Point(dp, txtTop);
            txtContent.Size = new Size(cardDocument.Width - dp * 2, txtHeight);

            // Bottom stats footer
            lblDocStats.Location = new Point(dp, cardDocument.Height - 28);
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

                lblHistoryBadge.Text = _all.Count == 1 ? "1 version" : $"{_all.Count} versions";

                if (_all.Count > 0)
                {
                    // Select active one if exists, otherwise first
                    int activeIdx = _all.FindIndex(t => t.IsActive);
                    lstVersions.SelectedIndex = activeIdx >= 0 ? activeIdx : 0;
                    _selected = _all[lstVersions.SelectedIndex];
                    ShowSelected();
                }
                else
                {
                    _selected = null;
                    state.Show("\uE8A5", "No Terms & Conditions Published",
                        "Click \"New Version\" above to publish your company's standard service agreement and policies.");
                }

                LayoutUi();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load terms & conditions:\n\n{ex.Message}",
                    "Connection Problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowSelected()
        {
            if (_selected == null) return;

            lblDocTitle.Text = _selected.Title;

            string statusDesc = _selected.IsActive ? "Active version" : "Archived version";
            string author = string.IsNullOrWhiteSpace(_selected.CreatedByUserId) ? "system" : _selected.CreatedByUserId;
            lblDocMeta.Text = $"{statusDesc}  ·  Effective: {_selected.VersionDisplay}  ·  Published by {author}";

            txtContent.Text = _selected.Content ?? "";

            // Document statistics
            int charCount = (_selected.Content ?? "").Length;
            int lineCount = (_selected.Content ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            lblDocStats.Text = $"{lineCount} clauses / sections  ·  {charCount:N0} characters  ·  Terms ID: #{_selected.TermsId}";

            btnActivate.Visible = !_selected.IsActive;

            LayoutDocumentViewer();
            cardDocument.Invalidate();
        }

        // ═══════════ ACTIONS ═══════════

        private async Task NewVersionAsync()
        {
            using var dlg = new TermsFormDialog(null);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                Toast.Notify(FindForm(), "Version Published", "New terms & conditions version published successfully.", ToastKind.Success);
                await ReloadAsync();
            }
        }

        private async Task EditAsync()
        {
            if (_selected == null) return;

            using var dlg = new TermsFormDialog(_selected);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                Toast.Notify(FindForm(), "Terms Updated", "Terms & conditions updated successfully.", ToastKind.Success);
                await ReloadAsync();
            }
        }

        private async Task ActivateAsync()
        {
            if (_selected == null) return;

            var confirm = MessageBox.Show(
                $"Activate version \"{_selected.Title}\" ({_selected.VersionDisplay})?\n\nThis will become the official active terms agreement and archive any previously active version.",
                "Activate Terms & Conditions",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                await _api.ActivateTermsAsync(_selected.TermsId);
                Toast.Notify(FindForm(), "Terms Activated", $"\"{_selected.Title}\" is now the active terms agreement.", ToastKind.Success);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Activation failed:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CopyContent()
        {
            if (_selected == null || string.IsNullOrEmpty(_selected.Content)) return;

            try
            {
                Clipboard.SetText(_selected.Content);
                Toast.Notify(FindForm(), "Terms Copied", "The agreement text was copied to your clipboard.", ToastKind.Success);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not copy text to clipboard:\n\n{ex.Message}",
                    "Clipboard Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ═══════════ CUSTOM CONTROLS ═══════════

        /// <summary>
        /// Surface panel container for the Version History list.
        /// </summary>
        [DesignerCategory("Code")]
        private sealed class HistoryCard : Panel
        {
            public HistoryCard()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                DoubleBuffered = true;
                BackColor = UiKit.T.Surface;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                UiKit.Quality(e.Graphics);
                using (var b = new SolidBrush(AppTheme.Background))
                    e.Graphics.FillRectangle(b, ClientRectangle);

                UiKit.Card(e.Graphics, ClientRectangle, UiKit.T.Radius, UiKit.T.Surface, UiKit.T.Line);

                // Header divider rule
                using (var pen = new Pen(UiKit.T.LineSoft, 1))
                    e.Graphics.DrawLine(pen, 0, 50, Width, 50);

                base.OnPaint(e);
            }
        }

        /// <summary>
        /// Surface panel container for the Document Viewer.
        /// </summary>
        [DesignerCategory("Code")]
        private sealed class DocumentCard : Panel
        {
            public DocumentCard()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                DoubleBuffered = true;
                BackColor = UiKit.T.Surface;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                UiKit.Quality(e.Graphics);
                using (var b = new SolidBrush(AppTheme.Background))
                    e.Graphics.FillRectangle(b, ClientRectangle);

                UiKit.Card(e.Graphics, ClientRectangle, UiKit.T.Radius, UiKit.T.Surface, UiKit.T.Line);

                // Header divider rule
                using (var pen = new Pen(UiKit.T.LineSoft, 1))
                {
                    e.Graphics.DrawLine(pen, 20, 68, Width - 20, 68);
                    // Footer divider rule
                    e.Graphics.DrawLine(pen, 20, Height - 38, Width - 20, Height - 38);
                }

                base.OnPaint(e);
            }
        }

        /// <summary>
        /// Owner-drawn version list with pill badges, clean typography, and zero text cutoff.
        /// </summary>
        [DesignerCategory("Code")]
        private sealed class VersionListBox : ListBox
        {
            private int _hoverIndex = -1;

            public VersionListBox()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                DoubleBuffered = true;
                DrawMode = DrawMode.OwnerDrawVariable;
                BorderStyle = BorderStyle.None;
                BackColor = UiKit.T.Surface;
                ForeColor = UiKit.T.Ink;
                IntegralHeight = false;
                ItemHeight = 76;
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

            protected override void OnMeasureItem(MeasureItemEventArgs e)
            {
                e.ItemHeight = 76;
            }

            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= Items.Count) return;
                if (Items[e.Index] is not TermsDto item) return;

                var g = e.Graphics;
                UiKit.Quality(g);

                bool isSelected = (e.State & DrawItemState.Selected) != 0;
                bool isHovered = e.Index == _hoverIndex && !isSelected;

                Rectangle r = e.Bounds;

                // Item background
                Color bg = isSelected ? UiKit.Wash(AppTheme.Primary) : isHovered ? UiKit.T.RowHover : UiKit.T.Surface;
                using (var brush = new SolidBrush(bg))
                    g.FillRectangle(brush, r);

                // Left active indicator
                if (isSelected)
                {
                    using var barBrush = new SolidBrush(AppTheme.Primary);
                    g.FillRectangle(barBrush, new Rectangle(r.Left, r.Top + 6, 3, r.Height - 12));
                }

                // Bottom separator
                using (var sepPen = new Pen(UiKit.T.LineSoft, 1))
                    g.DrawLine(sepPen, r.Left + 16, r.Bottom - 1, r.Right - 16, r.Bottom - 1);

                // Top row: Status pill + Date
                int padLeft = r.Left + 16;
                int topY = r.Top + 10;

                bool active = item.IsActive;
                string statusText = active ? "Active" : "Archived";
                Color badgeFg = active ? Color.FromArgb(21, 128, 61) : UiKit.T.InkMuted;
                Color badgeBg = active ? Color.FromArgb(220, 252, 231) : UiKit.T.LineSoft;

                var statusSize = UiKit.Measure(statusText, UiKit.T.SmallStrong);
                int badgeW = statusSize.Width + 14;
                int badgeH = 18;
                var badgeRect = new Rectangle(padLeft, topY, badgeW, badgeH);
                UiKit.FillRounded(g, badgeRect, badgeH / 2, badgeBg);
                UiKit.Text(g, statusText, UiKit.T.SmallStrong, badgeFg, badgeRect,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                // Date (right-aligned)
                string dateText = item.Version.ToString("MMM d, yyyy");
                var dateRect = new Rectangle(r.Left, topY, r.Width - 16, badgeH);
                UiKit.Text(g, dateText, UiKit.T.Small, UiKit.T.InkMuted, dateRect,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                // Middle row: Title (anti-clipping, crisp ink)
                int titleY = topY + badgeH + 6;
                var titleRect = new Rectangle(padLeft, titleY, r.Width - 32, 20);
                UiKit.Text(g, item.Title, UiKit.T.BodyStrong, UiKit.T.Ink, titleRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                // Bottom row: Meta (version & author)
                int metaY = titleY + 20;
                string author = string.IsNullOrWhiteSpace(item.CreatedByUserId) ? "system" : item.CreatedByUserId;
                string metaText = $"Effective {item.VersionDisplay}  ·  by {author}";
                var metaRect = new Rectangle(padLeft, metaY, r.Width - 32, 16);
                UiKit.Text(g, metaText, UiKit.T.Small, UiKit.T.InkFaint, metaRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        [DesignerCategory("Code")]
        private sealed class StateView : WorkbenchState { }
    }
}