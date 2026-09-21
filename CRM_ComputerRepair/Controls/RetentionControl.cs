using CRM.winforms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Business Intelligence → Action → Retention page.
    /// Lists at-risk customers (no interaction in 90 days) with search
    /// and a "Log outreach" action per row.
    /// </summary>
    [DesignerCategory("Code")]
    public class RetentionControl : UserControl
    {
        // ═══════════ STATE ═══════════
        private readonly ApiClient _api = new ApiClient();
        private List<RetentionCandidateDto> _all = new();

        // ═══════════ CONTROLS ═══════════
        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private FlatButton btnRefresh = null!;
        private FlatButton btnLogOutreach = null!;

        private SurfaceCard card = null!;
        private Label lblGridTitle = null!;
        private Label lblCount = null!;
        private SearchBox search = null!;

        private DataGridView dgv = null!;
        private StateView state = null!;

        private const string ColActions = "colActions";
        private ContextMenuStrip _actionsMenu = null!;
        private int _menuRowIndex = -1;
        private int _hoverRow = -1;
        private readonly ToolTip _tips = new ToolTip { InitialDelay = 500, ReshowDelay = 200 };

        public RetentionControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            AutoScaleMode = AutoScaleMode.Font;

            BuildActionsMenu();
            BuildUi();

            this.Load += async (s, e) => await ReloadAsync();
        }

        // ═══════════ UI ═══════════
        private void BuildUi()
        {
            lblTitle = new Label
            {
                Text = "Retention",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Customers at risk — no interaction in the last 90 days",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnRefresh = new FlatButton("Refresh", "\uE72C");
            btnRefresh.Click += async (s, e) => await ReloadAsync();
            _tips.SetToolTip(btnRefresh, "Reload retention candidates");

            btnLogOutreach = new FlatButton("Log outreach", "\uE8BD");
            btnLogOutreach.Click += (s, e) => LogOutreachForSelected();
            _tips.SetToolTip(btnLogOutreach, "Log outreach for selected customer");

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnRefresh);
            Controls.Add(btnLogOutreach);

            card = new SurfaceCard();

            lblGridTitle = new Label
            {
                Text = "At-risk customers",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            search = new SearchBox { PlaceholderText = "Search name, email or phone" };
            search.Inner.TextChanged += (s, e) => ApplySearch();
            _tips.SetToolTip(search, "Search  (Ctrl+F)");

            dgv = new DataGridView();
            StyleGrid(dgv);
            dgv.CellPainting += Dgv_CellPainting;
            dgv.CellClick += Dgv_CellClick;
            dgv.CellDoubleClick += Dgv_CellDoubleClick;
            dgv.CellMouseEnter += Dgv_CellMouseEnter;
            dgv.CellMouseLeave += Dgv_CellMouseLeave;
            dgv.MouseLeave += (s, e) => { _hoverRow = -1; dgv.Invalidate(); };

            state = new StateView { Visible = false };

            card.Controls.Add(lblGridTitle);
            card.Controls.Add(lblCount);
            card.Controls.Add(search);
            card.Controls.Add(dgv);
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
            btnLogOutreach.Size = new Size(btnLogOutreach.PreferredWidth, UiKit.T.ButtonHeight);
            btnLogOutreach.Location = new Point(rightX - btnLogOutreach.Width, UiKit.T.S1);
            rightX -= btnLogOutreach.Width + 8;

            btnRefresh.Size = new Size(btnRefresh.PreferredWidth, UiKit.T.ButtonHeight);
            btnRefresh.Location = new Point(rightX - btnRefresh.Width, UiKit.T.S1);

            int dividerY = subtitleY + lblSubtitle.PreferredHeight + UiKit.T.S4;
            int cardTop = dividerY + UiKit.T.S5;
            int cardHeight = Math.Max(240, Height - cardTop);

            card.Location = new Point(0, cardTop);
            card.Size = new Size(Width, cardHeight);

            const int cp = UiKit.T.S5;

            lblGridTitle.Location = new Point(cp, UiKit.T.S5 - 2);
            lblCount.Location = new Point(
                lblGridTitle.Right + UiKit.T.S2,
                lblGridTitle.Top + lblGridTitle.PreferredHeight - lblCount.PreferredHeight - 2);

            int toolbarY = lblGridTitle.Bottom + UiKit.T.S4;

            int searchW = Math.Min(320, Math.Max(200, card.Width - cp * 2));
            search.Size = new Size(searchW, UiKit.T.InputHeight);
            search.Location = new Point(card.Width - cp - searchW, toolbarY);

            int gridTop = toolbarY + UiKit.T.InputHeight + UiKit.T.S4;
            int gridW = card.Width - cp * 2;
            int gridH = card.Height - gridTop - cp;

            if (gridW > 100 && gridH > 60)
            {
                dgv.Location = new Point(cp, gridTop);
                dgv.Size = new Size(gridW, gridH);
                state.Location = new Point(cp, gridTop);
                state.Size = new Size(gridW, gridH);
            }
        }

        // ═══════════ DATA ═══════════
        public async Task ReloadAsync()
        {
            try
            {
                _all = await _api.GetRetentionCandidatesAsync();
                ApplySearch();
            }
            catch (Exception ex)
            {
                _all = new List<RetentionCandidateDto>();
                ApplySearch();

                MessageBox.Show(
                    $"Couldn't load retention candidates.\n\n{ex.Message}",
                    "Connection problem",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ApplySearch()
        {
            var term = search.Inner.Text?.Trim() ?? string.Empty;

            IEnumerable<RetentionCandidateDto> q = _all;

            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(x =>
                    (x.FirstName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.LastName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Phone ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            var view = q.ToList();

            dgv.DataSource = null;
            dgv.DataSource = view;
            ConfigureColumns();
            AddActionsColumn();

            lblCount.Text = view.Count == _all.Count
                ? $"{view.Count} {(view.Count == 1 ? "customer" : "customers")}"
                : $"{view.Count} of {_all.Count}";

            if (view.Count > 0)
            {
                state.Visible = false;
                dgv.Visible = true;
                dgv.ClearSelection();
            }
            else
            {
                dgv.Visible = false;

                if (!string.IsNullOrEmpty(term))
                    state.Show("\uE721", "No matches",
                        $"Nothing matches \u201c{term}\u201d. Try a shorter word, or clear the search with Esc.");
                else
                    state.Show("\uE73E", "No one at risk",
                        "Every active customer has been contacted within the last 90 days. Great work!");
            }
        }

        // ═══════════ GRID ═══════════
        private static void StyleGrid(DataGridView g)
        {
            g.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            g.BackgroundColor = UiKit.T.Surface;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.None;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.GridColor = UiKit.T.LineSoft;

            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AllowUserToOrderColumns = false;
            g.ReadOnly = true;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            g.ScrollBars = ScrollBars.Vertical;
            g.RowTemplate.Height = UiKit.T.RowHeight;
            g.ColumnHeadersHeight = UiKit.T.HeaderHeight;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            g.ColumnHeadersDefaultCellStyle.BackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.ForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.Font = UiKit.T.SmallStrong;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(UiKit.T.S3, 0, UiKit.T.S3, 0);

            g.DefaultCellStyle.BackColor = UiKit.T.Surface;
            g.DefaultCellStyle.ForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Font = UiKit.T.Body;
            g.DefaultCellStyle.SelectionBackColor = UiKit.Wash(AppTheme.Primary);
            g.DefaultCellStyle.SelectionForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Padding = new Padding(UiKit.T.S3, 0, UiKit.T.S3, 0);
            g.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            g.RowsDefaultCellStyle.BackColor = UiKit.T.Surface;
        }

        private void ConfigureColumns()
        {
            foreach (var hidden in new[]
            {
                "CustomerId", "LoyaltyPoints", "CreatedAt",
                "FirstName", "LastName", "DisplayName"
            })
            {
                if (dgv.Columns[hidden] != null)
                    dgv.Columns[hidden].Visible = false;
            }

            void Setup(string name, string header, int width, int displayIndex, bool fill = false)
            {
                var c = dgv.Columns[name];
                if (c == null) return;

                c.HeaderText = header;
                c.SortMode = DataGridViewColumnSortMode.Automatic;
                c.AutoSizeMode = fill
                    ? DataGridViewAutoSizeColumnMode.Fill
                    : DataGridViewAutoSizeColumnMode.None;
                if (!fill) c.Width = width;
                else c.MinimumWidth = 200;
                c.DisplayIndex = displayIndex;
            }

            Setup("FullName", "Name", 220, 0);
            Setup("Email", "Email", 0, 1, fill: true);
            Setup("Phone", "Phone", 140, 2);
            Setup("LastContactDisplay", "Last contact", 130, 3);
            Setup("InactiveDays", "Inactive (days)", 130, 4);
            Setup("LoyaltyPoints", "Points", 90, 5);

            if (dgv.Columns["FullName"] != null)
                dgv.Columns["FullName"].DefaultCellStyle.Font = UiKit.T.BodyStrong;

            if (dgv.Columns["InactiveDays"] != null)
                dgv.Columns["InactiveDays"].DefaultCellStyle.ForeColor = AppTheme.Danger;
        }

        // ═══════════ ACTIONS MENU ═══════════
        private void BuildActionsMenu()
        {
            _actionsMenu = new ContextMenuStrip
            {
                Font = UiKit.T.Body,
                ShowImageMargin = false,
                BackColor = UiKit.T.Surface,
                ForeColor = UiKit.T.Ink,
                Renderer = new QuietMenuRenderer()
            };

            _actionsMenu.Items.Add("Log outreach…");
            _actionsMenu.Items.Add("View customer");

            foreach (ToolStripItem item in _actionsMenu.Items)
                item.Padding = new Padding(UiKit.T.S2, UiKit.T.S1, UiKit.T.S2, UiKit.T.S1);

            _actionsMenu.Items[0].Click += (s, e) => LogOutreachForMenuRow();
            _actionsMenu.Items[1].Click += (s, e) => ViewForMenuRow();

            _actionsMenu.Closed += (s, e) => { dgv.Invalidate(); };
        }

        private void AddActionsColumn()
        {
            if (dgv.Columns[ColActions] != null)
                dgv.Columns.Remove(ColActions);

            var col = new DataGridViewTextBoxColumn
            {
                Name = ColActions,
                HeaderText = "",
                Width = 44,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.False,
                ReadOnly = true
            };

            dgv.Columns.Add(col);
            col.DisplayIndex = dgv.Columns.Count - 1;
        }

        // ═══════════ OUTREACH ACTIONS ═══════════
        private void LogOutreachForSelected()
        {
            if (dgv.CurrentRow == null)
            {
                MessageBox.Show("Select a customer first.",
                    "No selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (dgv.CurrentRow.DataBoundItem is not RetentionCandidateDto dto) return;
            OpenDialogFor(dto);
        }

        private void LogOutreachForMenuRow()
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RetentionCandidateDto dto) return;
            OpenDialogFor(dto);
        }

        private void ViewForMenuRow()
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RetentionCandidateDto dto) return;

            MessageBox.Show(
                $"{dto.FullName}\n\n" +
                $"Email        {dto.Email ?? "—"}\n" +
                $"Phone        {dto.Phone ?? "—"}\n" +
                $"Points       {dto.LoyaltyPoints?.ToString() ?? "0"}\n" +
                $"Last contact {dto.LastContactDisplay}\n" +
                $"Inactive     {dto.InactiveDays} days\n" +
                $"Record       #{dto.CustomerId}",
                "Customer details",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OpenDialogFor(RetentionCandidateDto dto)
        {
            using var dlg = new RetentionContactDialog(dto.CustomerId, dto.FullName);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        // ═══════════ GRID EVENTS ═══════════
        private void Dgv_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgv.Columns[e.ColumnIndex].Name != ColActions) return;

            _menuRowIndex = e.RowIndex;
            var cellRect = dgv.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
            _actionsMenu.Show(dgv, new Point(cellRect.Right - 180, cellRect.Bottom));
        }

        private void Dgv_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgv.Rows[e.RowIndex].DataBoundItem is not RetentionCandidateDto dto) return;
            OpenDialogFor(dto);
        }

        private void Dgv_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) { _hoverRow = -1; return; }
            _hoverRow = e.RowIndex;
            dgv.Cursor = e.ColumnIndex >= 0 && dgv.Columns[e.ColumnIndex].Name == ColActions
                ? Cursors.Hand
                : Cursors.Default;
            dgv.InvalidateRow(e.RowIndex);
        }

        private void Dgv_CellMouseLeave(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            _hoverRow = -1;
            dgv.InvalidateRow(e.RowIndex);
        }

        // ═══════════ CELL PAINTING ═══════════
        private void Dgv_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;

            var g = e.Graphics;

            if (e.RowIndex == -1)
            {
                e.PaintBackground(e.CellBounds, false);
                e.PaintContent(e.CellBounds);
                using var hp = new Pen(UiKit.T.Line, 1);
                g.DrawLine(hp, e.CellBounds.Left, e.CellBounds.Bottom - 1,
                               e.CellBounds.Right, e.CellBounds.Bottom - 1);
                e.Handled = true;
                return;
            }

            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string col = dgv.Columns[e.ColumnIndex].Name;
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool hovered = e.RowIndex == _hoverRow;

            Color bg = selected
                ? e.CellStyle!.SelectionBackColor
                : hovered ? UiKit.T.RowHover : UiKit.T.Surface;

            bool custom = col == ColActions;

            using (var b = new SolidBrush(bg))
                g.FillRectangle(b, e.CellBounds);
            using (var p = new Pen(UiKit.T.LineSoft, 1))
                g.DrawLine(p, e.CellBounds.Left, e.CellBounds.Bottom - 1,
                              e.CellBounds.Right, e.CellBounds.Bottom - 1);

            if (!custom)
            {
                e.PaintContent(e.CellBounds);
                e.Handled = true;
                return;
            }

            UiKit.Quality(g);
            var r = e.CellBounds;

            if (col == ColActions)
            {
                var color = hovered ? UiKit.T.InkMuted : UiKit.T.InkFaint;
                UiKit.Text(g, "\u22EF", new Font("Segoe UI", 13F, FontStyle.Bold), color, r,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                e.Handled = true;
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
        private sealed class SearchBox : Control
        {
            public TextBox Inner { get; }
            private bool _focused;
            private bool _hoverClear;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public string PlaceholderText
            {
                get => Inner.PlaceholderText;
                set => Inner.PlaceholderText = value;
            }

            public SearchBox()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = UiKit.T.Surface;

                Inner = new TextBox
                {
                    BorderStyle = BorderStyle.None,
                    Font = UiKit.T.Body,
                    ForeColor = UiKit.T.Ink,
                    BackColor = UiKit.T.Surface
                };
                Inner.GotFocus += (s, e) => { _focused = true; Invalidate(); };
                Inner.LostFocus += (s, e) => { _focused = false; Invalidate(); };
                Inner.TextChanged += (s, e) => Invalidate();

                Controls.Add(Inner);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                int left = 34;
                Inner.Location = new Point(left, (Height - Inner.PreferredHeight) / 2);
                Inner.Width = Width - left - 34;
            }

            private Rectangle ClearRect => new Rectangle(Width - 28, (Height - 20) / 2, 20, 20);

            protected override void OnMouseMove(MouseEventArgs e)
            {
                bool hot = Inner.Text.Length > 0 && ClearRect.Contains(e.Location);
                if (hot != _hoverClear) { _hoverClear = hot; Invalidate(); }
                Cursor = hot ? Cursors.Hand : Cursors.IBeam;
                base.OnMouseMove(e);
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                if (Inner.Text.Length > 0 && ClearRect.Contains(e.Location))
                    Inner.Clear();
                Inner.Focus();
                base.OnMouseClick(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var b = new SolidBrush(UiKit.T.Surface))
                    g.FillRectangle(b, ClientRectangle);

                var box = new Rectangle(0, 0, Width, Height);
                UiKit.FillRounded(g, box, 8, UiKit.T.Surface);

                var border = _focused ? AppTheme.Primary : UiKit.T.Line;
                using (var path = UiKit.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 8))
                using (var pen = new Pen(border, _focused ? 1.4f : 1f))
                    g.DrawPath(pen, path);

                UiKit.Text(g, "\uE721", UiKit.T.Glyph, _focused ? AppTheme.Primary : UiKit.T.InkFaint,
                    new Rectangle(10, 0, 20, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                if (Inner.Text.Length > 0)
                {
                    UiKit.Text(g, "\uE711", new Font("Segoe MDL2 Assets", 9F),
                        _hoverClear ? UiKit.T.Ink : UiKit.T.InkFaint, ClearRect,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
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

            protected override void OnTextChanged(EventArgs e)
            {
                base.OnTextChanged(e);
                Width = PreferredWidth;
                Invalidate();
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

                using (var b = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(b, ClientRectangle);

                Color bg = _down ? AppTheme.PrimaryActive
                         : _hover ? AppTheme.PrimaryHover
                         : AppTheme.Primary;

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
                Visible = true;
                BringToFront();
                Invalidate();
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

        private sealed class QuietMenuRenderer : ToolStripProfessionalRenderer
        {
            public QuietMenuRenderer() : base(new Colors()) { RoundedEdges = false; }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var r = new Rectangle(4, 0, e.Item.Width - 8, e.Item.Height);
                if (e.Item.Selected)
                    UiKit.FillRounded(e.Graphics, r, 6, UiKit.T.RowHover);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using var pen = new Pen(UiKit.T.LineSoft, 1);
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
            }

            private sealed class Colors : ProfessionalColorTable
            {
                public override Color ToolStripDropDownBackground => UiKit.T.Surface;
                public override Color MenuBorder => UiKit.T.Line;
                public override Color MenuItemBorder => Color.Transparent;
                public override Color ImageMarginGradientBegin => UiKit.T.Surface;
                public override Color ImageMarginGradientMiddle => UiKit.T.Surface;
                public override Color ImageMarginGradientEnd => UiKit.T.Surface;
            }
        }
    }
}