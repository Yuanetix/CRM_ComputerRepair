using CRM.winforms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Controls
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
        private SaasButton btnRefresh = null!;
        private SaasButton btnAdd = null!;

        private MetricStrip strip = null!;

        private SurfaceCard card = null!;
        private Label lblGridTitle = null!;
        private Label lblCount = null!;
        private SegmentedFilter segments = null!;
        private SearchBox search = null!;

        private DataGridView dgv = null!;
        private StateView state = null!;
        private TablePagination pager = null!;

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
                BackColor = AppTheme.Background
            };

            lblSubtitle = new Label
            {
                Text = "Customer records collected by your team",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = AppTheme.Background
            };

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Click += async (s, e) => await ReloadAsync();
            _tips.SetToolTip(btnRefresh, "Refresh customers  (F5)");

            btnAdd = new SaasButton("Add customer", SaasButtonVariant.Primary, "\uE710");
            btnAdd.Click += (s, e) => OpenAddDialog();
            _tips.SetToolTip(btnAdd, "Add customer  (Ctrl+N)");

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnRefresh);
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
                segments.SelectByValue(_statusFilter == 2 ? null : _statusFilter);
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
                BackColor = AppTheme.Surface
            };

            lblCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = AppTheme.Surface
            };

            segments = new SegmentedFilter(new (string, int?)[]
            {
                ("All", null),
                ("Active", 1),
                ("Archived", 0)
            });
            segments.SelectionChanged += (s, e) => SetTypeFilter(segments.Selected);

            search = new SearchBox { PlaceholderText = "Search customers... (Ctrl+F)" };
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

            pager = new TablePagination();
            pager.PageChanged += (s, e) => ApplySearch(resetPage: false);

            card.Controls.Add(lblGridTitle);
            card.Controls.Add(lblCount);
            card.Controls.Add(segments);
            card.Controls.Add(search);
            card.Controls.Add(dgv);
            card.Controls.Add(state);
            card.Controls.Add(pager);

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
                case Keys.F5:
                    _ = ReloadAsync();
                    return true;

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
            strip.SelectByValue(status);

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

            int subtitleY = lblTitle.PreferredHeight + 6;
            lblSubtitle.Location = new Point(1, subtitleY);

            btnAdd.Size = new Size(btnAdd.PreferredWidth, UiKit.T.ButtonHeight);
            btnAdd.Location = new Point(Width - btnAdd.Width, 2);

            btnRefresh.Size = new Size(btnRefresh.PreferredWidth, UiKit.T.ButtonHeight);
            btnRefresh.Location = new Point(btnAdd.Left - btnRefresh.Width - UiKit.T.S2, 2);

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

            lblGridTitle.Location = new Point(cp, UiKit.T.S5);
            lblCount.Location = new Point(lblGridTitle.Right + UiKit.T.S2,
                                          lblGridTitle.Top + lblGridTitle.PreferredHeight - lblCount.PreferredHeight - 2);

            int toolbarY = lblGridTitle.Bottom + TableKit.ToolbarGap;

            segments.Size = new Size(segments.PreferredWidth, TableKit.ToolbarH);
            segments.Location = new Point(cp, toolbarY);

            int searchW = TableKit.SearchWidth(card.Width, segments.Width);
            search.Size = new Size(searchW, TableKit.InputH);
            search.Location = new Point(card.Width - cp - searchW, toolbarY + (TableKit.ToolbarH - TableKit.InputH) / 2);

            int gridTop = toolbarY + TableKit.ToolbarH + TableKit.ToolbarGap;
            int gridW = card.Width - cp * 2;
            int gridH = card.Height - gridTop - cp - TableKit.FooterH;

            pager.Location = new Point(cp, gridTop + Math.Max(0, gridH));
            pager.Size = new Size(gridW, TableKit.FooterH);

            if (gridW > 100 && gridH > 60)
            {
                dgv.Location = new Point(cp, gridTop);
                dgv.Size = new Size(gridW, gridH);
                state.Location = new Point(cp, gridTop);
                state.Size = new Size(gridW, gridH + TableKit.FooterH);
            }
        }

        // ═══════════ DATA ═══════════

        public async Task ReloadAsync()
        {
            state.ShowLoading("Loading customers…");
            dgv.Visible = false;
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

        private void ApplySearch(bool resetPage = true)
        {
            if (resetPage) pager.Reset();
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
                    (x.Address ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.City ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.StateOrProvince ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            var view = q.ToList();

            pager.SetTotal(view.Count);
            var page = pager.Slice(view);

            dgv.DataSource = null;
            dgv.DataSource = page;
            ConfigureColumns();
            AddActionsColumn();

            lblCount.Text = TableKit.FormatCount(view.Count, _all.Count);

            if (view.Count > 0)
            {
                state.Clear();
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

        private static void StyleGrid(DataGridView g) => TableKit.StyleGrid(g);

        private void ConfigureColumns()
        {
            foreach (var hidden in new[]
            {
                "CustomerId", "LoyaltyPoints", "IsActive",
                "FullName", "Status", "FirstName", "LastName",
                "Address", "City", "StateOrProvince", "PostalCode", "Country"
            })
            {
                var hCol = dgv.Columns[hidden];
                if (hCol != null)
                    hCol.Visible = false;
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
            Setup("FullAddress", "Address", 230, 3);
            Setup("StatusDisplay", "Status", 130, 4);
            Setup("CreatedAt", "Created", 120, 5);

            var cCreated = dgv.Columns["CreatedAt"];
            if (cCreated != null)
            {
                cCreated.DefaultCellStyle.Format = "MMM d";
                cCreated.DefaultCellStyle.ForeColor = UiKit.T.InkMuted;
                cCreated.DefaultCellStyle.SelectionForeColor = UiKit.T.InkMuted;
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
            _actionsMenu = TableKit.MakeMenu();

            _actionsMenu.Items.Add("View details");
            _actionsMenu.Items.Add("Edit");
            _actionsMenu.Items.Add(new ToolStripSeparator());
            _actionsMenu.Items.Add("Archive");

            TableKit.StyleMenuItems(_actionsMenu);

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

        private void AddActionsColumn() => TableKit.AddActionsColumn(dgv, ColActions);

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
                TableKit.PaintHeaderRule(g, e.CellBounds);
                e.Handled = true;
                return;
            }

            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string col = dgv.Columns[e.ColumnIndex].Name;
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool hovered = e.RowIndex == _hoverRow;

            Color bg = TableKit.RowBackground(e, selected, hovered);

            bool custom = col is "StatusDisplay" || col == ColActions;

            TableKit.PaintRowShell(g, e.CellBounds, bg);

            if (!custom)
            {
                e.PaintContent(e.CellBounds);
                e.Handled = true;
                return;
            }

            UiKit.Quality(g);
            var r = e.CellBounds;
            string text = e.FormattedValue?.ToString() ?? string.Empty;

            if (col == "StatusDisplay")
            {
                if (text == "Archived")
                {
                    // Archived is inactive, not an error — neutral pill, not danger red.
                    var size = UiKit.Measure(text, UiKit.T.SmallStrong);
                    var pill = new Rectangle(r.Left + UiKit.T.S3, r.Top + (r.Height - 22) / 2,
                        Math.Max(56, size.Width + 20), 22);
                    UiKit.FillRounded(g, pill, UiKit.T.PillRadius, AppTheme.Neutral);
                    UiKit.Text(g, text, UiKit.T.SmallStrong, UiKit.T.InkMuted, pill,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    TableKit.PaintPill(g, r, text);
                }
                e.Handled = true;
                return;
            }

            if (col == ColActions)
            {
                TableKit.PaintActionCell(g, r, hovered);
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
        private sealed class SurfaceCard : WorkbenchCard { }
        [DesignerCategory("Code")]
        private sealed class MetricStrip : WorkbenchMetrics { }
        [DesignerCategory("Code")]
        private sealed class SegmentedFilter : WorkbenchSegments<int?>
        {
            public SegmentedFilter((string, int?)[] items) : base(items) { }
        }
        [DesignerCategory("Code")]
        private sealed class SearchBox : WorkbenchSearch { }
        [DesignerCategory("Code")]
        private sealed class StateView : WorkbenchState { }
        private sealed class QuietMenuRenderer : WorkbenchMenuRenderer { }

    }
}