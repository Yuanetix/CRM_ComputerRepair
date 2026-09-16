using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Customers list, styled to match the Interactions module.
    /// Data, API calls, and dialogs are unchanged.
    /// </summary>
    [DesignerCategory("Code")]
    public class CustomerListControl : UserControl
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private List<CustomerDto> _all = new List<CustomerDto>();
        private int? _statusFilter = null;   // null = All, 1 = Active, 0 = Archived

        // ═══════════ CONTROLS ═══════════

        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private FlatButton btnAdd = null!;

        private MetricStrip strip = null!;

        private SurfaceCard card = null!;
        private Label lblGridTitle = null!;
        private Label lblCount = null!;
        private SegmentedFilter segments = null!;
        private SearchBox search = null!;

        private DataGridView dgv = null!;
        private StateView state = null!;

        private const string ColActions = "colActions";
        private ContextMenuStrip _actionsMenu = null!;
        private int _menuRowIndex = -1;
        private int _hoverRow = -1;
        private readonly ToolTip _tips = new ToolTip { InitialDelay = 500, ReshowDelay = 200 };

        // ═══════════ CONSTRUCTOR ═══════════

        public CustomerListControl()
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

        // ═══════════ UI BUILD ═══════════

        private void BuildUi()
        {
            // ── Header ──
            lblTitle = new Label
            {
                Text = "Customers",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Customer records collected by your team",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnAdd = new FlatButton("Add customer", "\uE710");
            btnAdd.Click += (s, e) => OpenAddDialog();
            _tips.SetToolTip(btnAdd, "Add customer  (Ctrl+N)");

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnAdd);

            // ── Metric strip ──
            strip = new MetricStrip();
            strip.AddItem("Total", null, AppTheme.Primary);
            strip.AddItem("Active", 1, AppTheme.Success);
            strip.AddItem("New this month", 2, AppTheme.Warning);
            strip.AddItem("Archived", 0, AppTheme.TextMuted);
            strip.SelectionChanged += (s, e) =>
            {
                _statusFilter = strip.SelectedStatus;
                ApplySearch();
            };
            Controls.Add(strip);

            // ── Workbench card ──
            card = new SurfaceCard();

            lblGridTitle = new Label
            {
                Text = "All customers",
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

            segments = new SegmentedFilter(new (string, int?)[]
            {
                ("All", null),
                ("Active", 1),
                ("Archived", 0)
            });
            segments.SelectionChanged += (s, e) => SetTypeFilter(segments.Selected);

            search = new SearchBox { PlaceholderText = "Search name, email, phone, address" };
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
            dgv.KeyDown += Dgv_KeyDown;

            state = new StateView { Visible = false };

            card.Controls.Add(lblGridTitle);
            card.Controls.Add(lblCount);
            card.Controls.Add(segments);
            card.Controls.Add(search);
            card.Controls.Add(dgv);
            card.Controls.Add(state);

            Controls.Add(card);

            Resize += (s, e) => LayoutUi();
        }

        // ═══════════ HAIRLINE UNDER HEADER ═══════════

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            UiKit.Quality(e.Graphics);

            int y = lblSubtitle.Bottom + UiKit.T.S4;
            using var pen = new Pen(UiKit.T.Line, 1);
            e.Graphics.DrawLine(pen, 0, y, Width, y);
        }

        // ═══════════ KEYBOARD ═══════════

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.F:
                    search.Inner.Focus();
                    search.Inner.SelectAll();
                    return true;

                case Keys.Control | Keys.N:
                    OpenAddDialog();
                    return true;

                case Keys.Escape:
                    if (search.Inner.Text.Length > 0)
                    {
                        search.Inner.Clear();
                        return true;
                    }
                    break;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Dgv_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Apps && e.KeyCode != Keys.Enter) return;
            if (dgv.CurrentRow == null) return;

            _menuRowIndex = dgv.CurrentRow.Index;
            var r = dgv.GetRowDisplayRectangle(_menuRowIndex, true);
            _actionsMenu.Show(dgv, new Point(r.Right - 180, r.Bottom));
            e.Handled = true;
        }

        // ═══════════ FILTER ═══════════

        private void SetTypeFilter(int? status)
        {
            _statusFilter = status;

            lblGridTitle.Text = status switch
            {
                1 => "Active customers",
                0 => "Archived customers",
                _ => "All customers"
            };

            lblSubtitle.Text = status switch
            {
                1 => "Customers with an active record",
                0 => "Archived customers — restorable anytime",
                _ => "Customer records collected by your team"
            };

            LayoutUi();
            Invalidate();

            _ = ReloadAsync();
        }

        // ═══════════ LAYOUT ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            // Header
            lblTitle.Location = new Point(0, 0);

            int subtitleY = lblTitle.PreferredHeight + UiKit.T.S1;
            lblSubtitle.Location = new Point(1, subtitleY);

            btnAdd.Size = new Size(btnAdd.PreferredWidth, UiKit.T.ButtonHeight);
            btnAdd.Location = new Point(Width - btnAdd.Width, UiKit.T.S1);

            int dividerY = subtitleY + lblSubtitle.PreferredHeight + UiKit.T.S4;

            // Metric strip
            int stripTop = dividerY + UiKit.T.S5;
            int stripH = Math.Max(UiKit.T.StripHeight, strip.PreferredContentHeight());
            strip.Location = new Point(0, stripTop);
            strip.Size = new Size(Width, stripH);

            // Workbench card
            int cardTop = stripTop + stripH + UiKit.T.S5;
            int cardHeight = Math.Max(240, Height - cardTop);

            card.Location = new Point(0, cardTop);
            card.Size = new Size(Width, cardHeight);

            const int cp = UiKit.T.S5;

            lblGridTitle.Location = new Point(cp, UiKit.T.S5 - 2);
            lblCount.Location = new Point(lblGridTitle.Right + UiKit.T.S2,
                                          lblGridTitle.Top + lblGridTitle.PreferredHeight - lblCount.PreferredHeight - 2);

            int toolbarY = lblGridTitle.Bottom + UiKit.T.S4;

            segments.Size = new Size(segments.PreferredWidth, 34);
            segments.Location = new Point(cp, toolbarY);

            int searchW = Math.Min(280, Math.Max(180, card.Width - cp * 2 - segments.Width - UiKit.T.S4));
            search.Size = new Size(searchW, UiKit.T.InputHeight);
            search.Location = new Point(card.Width - cp - searchW, toolbarY + (34 - UiKit.T.InputHeight) / 2);

            int gridTop = toolbarY + 34 + UiKit.T.S4;
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
                // Always fetch all (active + archived) so counters are complete.
                _all = await _api.GetCustomersAsync(includeArchived: true);

                UpdateStats();
                ApplySearch();
            }
            catch (Exception ex)
            {
                _all = new List<CustomerDto>();
                UpdateStats();
                ApplySearch();

                MessageBox.Show(
                    $"Couldn't load customers.\n\n{ex.Message}",
                    "Connection problem",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void UpdateStats()
        {
            var now = DateTime.UtcNow;

            strip.SetValue(0, _all.Count);
            strip.SetValue(1, _all.Count(x => x.IsActive));
            strip.SetValue(2, _all.Count(x => x.CreatedAt.Year == now.Year && x.CreatedAt.Month == now.Month));
            strip.SetValue(3, _all.Count(x => !x.IsActive));
        }

        private void ApplySearch()
        {
            var term = search.Inner.Text?.Trim() ?? string.Empty;

            IEnumerable<CustomerDto> q = _all;

            // Metric strip filter
            if (_statusFilter.HasValue)
            {
                bool wantActive = _statusFilter.Value == 1;
                if (_statusFilter.Value == 2)
                {
                    // "New this month" — filter by creation date
                    var now = DateTime.UtcNow;
                    q = q.Where(x => x.CreatedAt.Year == now.Year && x.CreatedAt.Month == now.Month);
                }
                else
                {
                    q = q.Where(x => x.IsActive == wantActive);
                }
            }

            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(x =>
                    (x.FirstName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.LastName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Phone ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Address ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            var view = q.ToList();

            dgv.DataSource = null;
            dgv.DataSource = view;
            ConfigureColumns();
            AddActionsColumn();

            lblCount.Text = view.Count == _all.Count
                ? $"{view.Count} {(view.Count == 1 ? "record" : "records")}"
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
                else if (_statusFilter.HasValue)
                    state.Show("\uE71C", "Nothing here yet",
                        "No customers with this filter. Pick Total to see them all.");
                else
                    state.Show("\uE716", "No customers yet",
                        $"Use \u201c{btnAdd.Text}\u201d to add the first one.");
            }
        }

        // ═══════════ GRID STYLE ═══════════

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
            g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

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
                "CustomerId", "LoyaltyPoints", "IsActive",
                "FullName", "Status", "FirstName", "LastName"
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
                else c.MinimumWidth = 180;
                c.DisplayIndex = displayIndex;
            }

            Setup("NameDisplay", "Name", 220, 0);
            Setup("Email", "Email", 0, 1, fill: true);
            Setup("Phone", "Phone", 140, 2);
            Setup("Address", "Address", 220, 3);
            Setup("StatusDisplay", "Status", 130, 4);
            Setup("CreatedAt", "Created", 120, 5);

            if (dgv.Columns["CreatedAt"] != null)
            {
                dgv.Columns["CreatedAt"].DefaultCellStyle.Format = "MMM d";
                dgv.Columns["CreatedAt"].DefaultCellStyle.ForeColor = UiKit.T.InkMuted;
                dgv.Columns["CreatedAt"].DefaultCellStyle.SelectionForeColor = UiKit.T.InkMuted;
            }
        }

        // ═══════════ DISPLAY HELPERS ═══════════

        private static string FullNameOf(CustomerDto c)
            => $"{c.FirstName} {c.LastName}".Trim();

        private static string StatusOf(CustomerDto c)
            => c.IsActive ? "Active" : "Archived";

        // ═══════════ ACTIONS ═══════════

        private void BuildActionsMenu()
        {
            _actionsMenu = new ContextMenuStrip
            {
                Font = UiKit.T.Body,
                ShowImageMargin = false,
                BackColor = UiKit.T.Surface,
                ForeColor = UiKit.T.Ink,
                DropShadowEnabled = true,
                RenderMode = ToolStripRenderMode.Professional,
                Renderer = new QuietMenuRenderer()
            };

            _actionsMenu.Items.Add("View details");
            _actionsMenu.Items.Add("Edit");
            _actionsMenu.Items.Add(new ToolStripSeparator());
            _actionsMenu.Items.Add("Archive");

            foreach (ToolStripItem item in _actionsMenu.Items)
                item.Padding = new Padding(UiKit.T.S2, UiKit.T.S1, UiKit.T.S2, UiKit.T.S1);

            _actionsMenu.Items[0].Click += OnMenuView;
            _actionsMenu.Items[1].Click += OnMenuUpdate;
            _actionsMenu.Items[3].Click += OnMenuArchive;

            _actionsMenu.Opening += (s, e) =>
            {
                if (_menuRowIndex < 0) return;
                if (dgv.Rows[_menuRowIndex].DataBoundItem is not CustomerDto c) return;

                var archiveItem = _actionsMenu.Items[3];
                archiveItem.Text = c.IsActive ? "Archive" : "Restore";
                archiveItem.ForeColor = c.IsActive ? AppTheme.Danger : UiKit.T.Ink;
            };

            _actionsMenu.Closed += (s, e) => { dgv.Invalidate(); };
        }

        private void OnMenuView(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not CustomerDto c) return;

            MessageBox.Show(
                $"{FullNameOf(c)}\n\n" +
                $"Email       {c.Email ?? "\u2014"}\n" +
                $"Phone       {c.Phone ?? "\u2014"}\n" +
                $"Address     {c.Address ?? "\u2014"}\n" +
                $"Points      {c.LoyaltyPoints?.ToString() ?? "0"}\n" +
                $"Status      {StatusOf(c)}\n" +
                $"Created     {c.CreatedAt:MMM d, yyyy  HH:mm}\n" +
                $"Record      #{c.CustomerId}",
                "Customer details",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OnMenuUpdate(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not CustomerDto c) return;
            OpenEditDialog(c);
        }

        private async void OnMenuArchive(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not CustomerDto c) return;

            if (c.IsActive)
                await ArchiveAsync(c);
            else
                await RestoreAsync(c);
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

        private void Dgv_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (dgv.Columns[e.ColumnIndex].Name != ColActions) return;

            _menuRowIndex = e.RowIndex;

            var cellRect = dgv.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
            _actionsMenu.Show(dgv, new Point(cellRect.Right - 160, cellRect.Bottom));
        }

        private void Dgv_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgv.Rows[e.RowIndex].DataBoundItem is not CustomerDto c) return;
            OpenEditDialog(c);
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

        // ═══════════ ARCHIVE / RESTORE ═══════════

        private async Task ArchiveAsync(CustomerDto c)
        {
            var confirm = MessageBox.Show(
                $"Archive \u201c{FullNameOf(c)}\u201d?\n\n" +
                "It leaves the list but nothing is deleted — you can restore it later.",
                "Archive customer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                await _api.ArchiveCustomerAsync(c.CustomerId);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Couldn't archive this customer.\n\n{ex.Message}",
                    "Archive failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async Task RestoreAsync(CustomerDto c)
        {
            try
            {
                await _api.RestoreCustomerAsync(c.CustomerId);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Couldn't restore this customer.\n\n{ex.Message}",
                    "Restore failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ═══════════ CELL PAINTING ═══════════

        private void Dgv_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;

            var g = e.Graphics;

            // Header
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

        // ═══════════ MODAL LAUNCHERS ═══════════

        private void OpenAddDialog()
        {
            using var dlg = new CustomerFormDialog(null);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private void OpenEditDialog(CustomerDto c)
        {
            using var dlg = new CustomerFormDialog(c.CustomerId, c);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        //  NESTED UI COMPONENTS
        // ═══════════════════════════════════════════════════════════════

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
        private sealed class MetricStrip : Control
        {
            private sealed class Item
            {
                public int? Status;
                public Color Accent = Color.Black;
                public Label CapLabel = null!;
                public Label ValLabel = null!;
            }

            private const int SegPad = UiKit.T.S5;
            private const int DotOffset = SegPad + 3;
            private const int CapOffset = SegPad + UiKit.T.S4 - 2;

            private readonly List<Item> _items = new();
            private int _hover = -1;
            private int _selected = 0;

            public event EventHandler? SelectionChanged;

            public int? SelectedStatus =>
                _selected >= 0 && _selected < _items.Count ? _items[_selected].Status : null;

            public MetricStrip()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
                Resize += (s, e) => LayoutItems();
            }

            public void AddItem(string caption, int? status, Color accent)
            {
                int index = _items.Count;

                var cap = new Label
                {
                    Text = caption,
                    Font = UiKit.T.Small,
                    ForeColor = UiKit.T.InkMuted,
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                var val = new Label
                {
                    Text = "0",
                    Font = UiKit.T.Metric,
                    ForeColor = UiKit.T.Ink,
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                cap.Click += (s, e) => Select(index);
                val.Click += (s, e) => Select(index);
                cap.MouseEnter += (s, e) => { _hover = index; Invalidate(); };
                val.MouseEnter += (s, e) => { _hover = index; Invalidate(); };
                cap.MouseLeave += (s, e) => { if (_hover == index) { _hover = -1; Invalidate(); } };
                val.MouseLeave += (s, e) => { if (_hover == index) { _hover = -1; Invalidate(); } };

                Controls.Add(cap);
                Controls.Add(val);

                _items.Add(new Item { Status = status, Accent = accent, CapLabel = cap, ValLabel = val });
                UpdateColors();
                LayoutItems();
            }

            public void SetValue(int index, int value)
            {
                if (index < 0 || index >= _items.Count) return;
                _items[index].ValLabel.Text = value.ToString();
            }

            private void Select(int i)
            {
                if (i < 0 || i == _selected) return;
                _selected = i;
                UpdateColors();
                Invalidate();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }

            private void UpdateColors()
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    bool active = i == _selected;
                    var it = _items[i];
                    it.CapLabel.ForeColor = active ? UiKit.T.Ink : UiKit.T.InkMuted;
                    it.ValLabel.ForeColor = active ? it.Accent : UiKit.T.Ink;
                }
            }

            private void LayoutItems()
            {
                if (_items.Count == 0 || Width <= 0) return;

                int segW = Width / _items.Count;

                for (int i = 0; i < _items.Count; i++)
                {
                    var it = _items[i];
                    int segLeft = i * segW;

                    it.CapLabel.Location = new Point(segLeft + CapOffset, UiKit.T.S4);
                    it.ValLabel.Location = new Point(segLeft + SegPad, it.CapLabel.Bottom + UiKit.T.S1);
                }
            }

            public int PreferredContentHeight()
            {
                if (_items.Count == 0) return UiKit.T.StripHeight;

                int capH = _items.Max(x => x.CapLabel.Height);
                int valH = _items.Max(x => x.ValLabel.Height);

                return UiKit.T.S4 + capH + UiKit.T.S1 + valH + UiKit.T.S3;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var b = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(b, ClientRectangle);

                UiKit.Card(g, ClientRectangle, UiKit.T.Radius, UiKit.T.Surface, UiKit.T.Line);

                if (_items.Count == 0) return;

                int segW = Width / _items.Count;

                for (int i = 0; i < _items.Count; i++)
                {
                    var it = _items[i];
                    var seg = new Rectangle(i * segW, 0, segW, Height);
                    bool active = i == _selected;
                    bool hot = i == _hover;

                    if (hot && !active)
                    {
                        var wash = Rectangle.Inflate(seg, -UiKit.T.S2, -UiKit.T.S2);
                        UiKit.FillRounded(g, wash, 8, UiKit.T.RowHover);
                    }

                    if (i > 0)
                    {
                        using var pen = new Pen(UiKit.T.Line, 1);
                        g.DrawLine(pen, seg.Left, UiKit.T.S5, seg.Left, Height - UiKit.T.S5);
                    }

                    UiKit.Dot(g, seg.Left + DotOffset, it.CapLabel.Top + it.CapLabel.Height / 2, 7,
                        active ? it.Accent : UiKit.Mix(it.Accent, Color.White, 0.45));

                    if (active)
                    {
                        var bar = new Rectangle(seg.Left + SegPad, Height - 4, 28, 2);
                        UiKit.FillRounded(g, bar, 1, it.Accent);
                    }
                }
            }
        }

        [DesignerCategory("Code")]
        private sealed class SegmentedFilter : Control
        {
            private readonly (string Label, int? Value)[] _items;
            private readonly int[] _widths;
            private int _hover = -1;
            private int _selected = 0;

            public event EventHandler? SelectionChanged;
            public int? Selected => _items[_selected].Value;

            public SegmentedFilter((string, int?)[] items)
            {
                _items = items;
                _widths = new int[items.Length];

                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = UiKit.T.Surface;
                Cursor = Cursors.Hand;
                Font = UiKit.T.SmallStrong;

                for (int i = 0; i < _items.Length; i++)
                    _widths[i] = UiKit.Measure(_items[i].Label, UiKit.T.SmallStrong).Width + UiKit.T.S5;
            }

            public int PreferredWidth => _widths.Sum() + UiKit.T.S1 * 2;

            private int IndexAt(Point p)
            {
                int x = UiKit.T.S1;
                for (int i = 0; i < _items.Length; i++)
                {
                    if (p.X >= x && p.X < x + _widths[i]) return i;
                    x += _widths[i];
                }
                return -1;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                int i = IndexAt(e.Location);
                if (i != _hover) { _hover = i; Invalidate(); }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _hover = -1; Invalidate(); base.OnMouseLeave(e);
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                int i = IndexAt(e.Location);
                if (i >= 0 && i != _selected)
                {
                    _selected = i;
                    Invalidate();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
                base.OnMouseClick(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var b = new SolidBrush(UiKit.T.Surface))
                    g.FillRectangle(b, ClientRectangle);

                UiKit.FillRounded(g, new Rectangle(0, 0, Width, Height), 8, UiKit.T.LineSoft);

                int x = UiKit.T.S1;
                for (int i = 0; i < _items.Length; i++)
                {
                    var seg = new Rectangle(x, UiKit.T.S1 - 1, _widths[i], Height - (UiKit.T.S1 - 1) * 2);
                    bool active = i == _selected;

                    if (active)
                    {
                        UiKit.FillRounded(g, seg, 6, UiKit.T.Surface);
                        using var pen = new Pen(UiKit.T.Line, 1);
                        using var path = UiKit.Rounded(new Rectangle(seg.X, seg.Y, seg.Width - 1, seg.Height - 1), 6);
                        g.DrawPath(pen, path);
                    }

                    Color fg = active ? UiKit.T.Ink : (i == _hover ? UiKit.T.InkMuted : UiKit.T.InkFaint);
                    UiKit.Text(g, _items[i].Label, UiKit.T.SmallStrong, fg, seg,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                    x += _widths[i];
                }
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
            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

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

                if (Focused)
                {
                    var ring = Rectangle.Inflate(ClientRectangle, -3, -3);
                    using var path = UiKit.Rounded(ring, 6);
                    using var pen = new Pen(Color.FromArgb(120, Color.White), 1.2f);
                    g.DrawPath(pen, path);
                }

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