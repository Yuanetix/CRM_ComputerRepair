using CRM.winforms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Customers list, aligned with Tenants / Subscriptions / Admin / System Monitor
    /// and the Repair Requests command center. Data, API calls, and dialogs are unchanged.
    /// </summary>
    [DesignerCategory("Code")]
    public class CustomerListControl : UserControl
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private List<CustomerDto> _all = new List<CustomerDto>();
        private int? _statusFilter = null;   // null = All, 1 = Active, 0 = Archived, 2 = New this month

        // ═══════════ CONTROLS ═══════════

        private Label lblTitle = null!;
        private Label lblSummary = null!;
        private SaasButton btnRefresh = null!;
        private SaasButton btnAdd = null!;

        // ── KPI Tiles (also act as quick filters) ──
        private KpiTile tileTotal = null!;
        private KpiTile tileActive = null!;
        private KpiTile tileNewThisMonth = null!;
        private KpiTile tileArchived = null!;
        private readonly List<KpiTile> _tiles = new();

        // ── Workbench Card ──
        private WorkbenchCard card = null!;
        private WorkbenchSearch searchBox = null!;
        private CustomerSegmentedFilter filterBar = null!;
        private Label lblCount = null!;
        private DataGridView dgv = null!;
        private WorkbenchState emptyState = null!;
        private TablePagination pager = null!;

        private const string ColActions = "colActions";
        private ContextMenuStrip _actionsMenu = null!;
        private int _menuRowIndex = -1;
        private int _hoverRow = -1;

        // ═══════════ SHARED DRAWING HELPERS ═══════════
        private const int CellPadX = 14;
        private const TextFormatFlags Flat = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        private const TextFormatFlags CellText = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | Flat;

        private static readonly Font MonoFont = new Font("Consolas", 9F);
        private static readonly Color[] AvatarPalette =
        {
            Color.FromArgb(99, 102, 241),
            Color.FromArgb(16, 185, 129),
            Color.FromArgb(245, 158, 11),
            Color.FromArgb(236, 72, 153),
            Color.FromArgb(59, 130, 246),
            Color.FromArgb(124, 58, 237)
        };

        private static int MeasureW(string text, Font font) =>
            TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
                Flat | TextFormatFlags.SingleLine).Width;

        private static float Descent(Font f)
        {
            var fam = f.FontFamily;
            int asc = fam.GetCellAscent(f.Style);
            int desc = fam.GetCellDescent(f.Style);
            return f.Height * desc / (float)Math.Max(1, asc + desc);
        }

        private static Color AvatarColor(string? seed)
        {
            if (string.IsNullOrWhiteSpace(seed)) return AvatarPalette[0];
            int h = 0;
            foreach (char ch in seed) h = (h * 31 + ch) & 0x7FFFFFFF;
            return AvatarPalette[h % AvatarPalette.Length];
        }

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

            btnAdd = new SaasButton("Add customer", SaasButtonVariant.Primary, "\uE710");
            btnAdd.Size = new Size(150, 36);
            btnAdd.Click += (s, e) => OpenAddDialog();

            Controls.Add(lblTitle);
            Controls.Add(lblSummary);
            Controls.Add(btnRefresh);
            Controls.Add(btnAdd);

            // ── KPI Tiles (also act as quick filters) ──
            tileTotal = AddTile("TOTAL CUSTOMERS", "0", "All active and archived records", AppTheme.Primary, "\uE716");
            tileTotal.Click += (s, e) => SetTypeFilter(null);

            tileActive = AddTile("ACTIVE", "0", "Customers with an active record", AppTheme.Success, "\uE73E");
            tileActive.Click += (s, e) => SetTypeFilter(1);

            tileNewThisMonth = AddTile("NEW THIS MONTH", "0", "Created in the current month", AppTheme.Warning, "\uE787");
            tileNewThisMonth.Click += (s, e) => SetTypeFilter(2);

            tileArchived = AddTile("ARCHIVED", "0", "Archived — restorable anytime", UiKit.T.InkMuted, "\uE7B8");
            tileArchived.Click += (s, e) => SetTypeFilter(0);

            // ── Workbench Card ──
            card = new WorkbenchCard { BackColor = UiKit.T.Surface };

            searchBox = new WorkbenchSearch
            {
                Placeholder = "Search by name, email, phone, or address..."
            };
            searchBox.QueryChanged += (s, e) => ApplySearch();

            filterBar = new CustomerSegmentedFilter(new (string, int?)[]
            {
                ("All", null),
                ("Active", 1),
                ("Archived", 0)
            });
            filterBar.SelectionChanged += (s, tag) =>
            {
                _statusFilter = tag;
                ApplySearch();
            };

            lblCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            dgv = new DataGridView();
            StyleGrid(dgv);
            dgv.CellPainting += (s, e) => PaintCell(dgv, e);
            dgv.CellClick += Dgv_CellClick;
            dgv.CellDoubleClick += Dgv_CellDoubleClick;
            dgv.KeyDown += Dgv_KeyDown;
            dgv.CellMouseMove += (s, e) =>
            {
                if (e.RowIndex != _hoverRow)
                {
                    int old = _hoverRow;
                    _hoverRow = e.RowIndex;
                    if (old >= 0 && old < dgv.RowCount) dgv.InvalidateRow(old);
                    if (_hoverRow >= 0 && _hoverRow < dgv.RowCount) dgv.InvalidateRow(_hoverRow);
                }
            };
            dgv.CellMouseLeave += (s, e) =>
            {
                if (_hoverRow >= 0 && _hoverRow < dgv.RowCount)
                {
                    int old = _hoverRow;
                    _hoverRow = -1;
                    dgv.InvalidateRow(old);
                }
            };

            emptyState = new WorkbenchState { Visible = false };

            pager = new TablePagination();
            pager.PageChanged += (s, e) => ApplySearch(resetPage: false);

            card.Controls.Add(searchBox);
            card.Controls.Add(filterBar);
            card.Controls.Add(lblCount);
            card.Controls.Add(dgv);
            card.Controls.Add(emptyState);
            card.Controls.Add(pager);

            Controls.Add(card);

            Resize += (s, e) => LayoutUi();
            LayoutUi();
        }

        private KpiTile AddTile(string label, string number, string sub, Color accent, string glyph)
        {
            var t = new KpiTile();
            t.Set(label, number, sub, accent, glyph);
            _tiles.Add(t);
            Controls.Add(t);
            return t;
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
                    searchBox.Focus();
                    return true;

                case Keys.Control | Keys.N:
                    OpenAddDialog();
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Dgv_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Apps && e.KeyCode != Keys.Enter) return;
            if (dgv.CurrentRow == null) return;

            _menuRowIndex = dgv.CurrentRow.Index;
            var r = dgv.GetRowDisplayRectangle(_menuRowIndex, true);
            _actionsMenu.Show(dgv, new Point(r.Right - _actionsMenu.Width, r.Bottom));
            e.Handled = true;
        }

        // ═══════════ FILTER ═══════════

        private void SetTypeFilter(int? tag)
        {
            _statusFilter = tag;
            filterBar.SetKey(tag == 2 ? null : tag);
            ApplySearch();
        }

        // ═══════════ LAYOUT ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            int pad = UiKit.T.S6;
            int contentW = Math.Max(700, Width - pad * 2);

            // ── Header ──
            lblTitle.Location = new Point(pad, pad);

            int btnY = pad;
            btnAdd.Location = new Point(pad + contentW - btnAdd.Width, btnY);
            btnRefresh.Location = new Point(btnAdd.Left - btnRefresh.Width - 10, btnY);

            lblSummary.Location = new Point(pad + 1, lblTitle.Bottom + 6);

            int y = lblSummary.Bottom + 22;

            // ── KPI Tiles ──
            int tileCols = contentW >= 1100 ? 4 : contentW >= 760 ? 2 : 1;
            int tileGap = 16;
            int tileW = (contentW - (tileCols - 1) * tileGap) / tileCols;

            int rowMaxH = 0;
            for (int i = 0; i < _tiles.Count; i++)
            {
                int col = i % tileCols;
                if (col == 0 && i > 0)
                {
                    y += rowMaxH + tileGap;
                    rowMaxH = 0;
                }

                int tx = pad + col * (tileW + tileGap);
                int th = _tiles[i].HeightFor(tileW);
                _tiles[i].SetBounds(tx, y, tileW, th);
                rowMaxH = Math.Max(rowMaxH, th);
            }
            y += rowMaxH + 24;

            // ── Workbench Card ──
            int cardPad = UiKit.T.S6;
            int cardH = Math.Max(320, Height - y - pad);
            card.SetBounds(pad, y, contentW, cardH);

            int innerW = contentW - cardPad * 2;

            int filterW = filterBar.PreferredWidth;
            int searchW = Math.Max(240, Math.Min(440, innerW - filterW - 20));

            searchBox.SetBounds(cardPad, cardPad, searchW, 38);
            filterBar.SetBounds(cardPad + innerW - filterW, cardPad + 1, filterW, 36);

            int gridY = searchBox.Bottom + 14;
            int footerH = pager.Visible ? TableKit.FooterH : 0;
            int gridH = cardH - gridY - cardPad - 26 - footerH;

            dgv.SetBounds(cardPad, gridY, innerW, Math.Max(120, gridH));
            emptyState.SetBounds(cardPad, gridY, innerW, Math.Max(120, gridH));

            if (pager.Visible)
                pager.SetBounds(cardPad, gridY + Math.Max(0, gridH), innerW, TableKit.FooterH);

            lblCount.Location = new Point(cardPad, gridY - 22);
        }

        // ═══════════ DATA ═══════════

        public async Task ReloadAsync()
        {
            try
            {
                // Always fetch all (active + archived) so counters are complete.
                _all = await _api.GetCustomersAsync(includeArchived: true);

                UpdateTiles();
                ApplySearch();
            }
            catch (Exception ex)
            {
                _all = new List<CustomerDto>();
                UpdateTiles();
                ApplySearch();

                SaasToast.Show(FindForm(),
                    $"Couldn't load customers: {ex.Message}",
                    ToastKind.Danger);
            }
        }

        private void UpdateTiles()
        {
            var now = DateTime.UtcNow;
            int total = _all.Count;
            int active = _all.Count(x => x.IsActive);
            int archived = _all.Count(x => !x.IsActive);
            int newThisMonth = _all.Count(x => x.CreatedAt.Year == now.Year && x.CreatedAt.Month == now.Month);

            tileTotal.Set("TOTAL CUSTOMERS", total.ToString(),
                $"{active} active · {archived} archived", AppTheme.Primary, "\uE716");
            tileActive.Set("ACTIVE", active.ToString(),
                total > 0 ? $"{(double)active / total * 100:0.#}% of customer base" : "No customers yet",
                AppTheme.Success, "\uE73E");
            tileNewThisMonth.Set("NEW THIS MONTH", newThisMonth.ToString(),
                "Created in the current month", AppTheme.Warning, "\uE787");
            tileArchived.Set("ARCHIVED", archived.ToString(),
                archived == 0 ? "No archived customers" : "Restorable anytime",
                UiKit.T.InkMuted, "\uE7B8");

            lblSummary.Text =
                $"{total} total  ·  {active} active  ·  {newThisMonth} new this month  ·  {archived} archived";

            filterBar.UpdateCounts(
                (null, total),
                (1, active),
                (0, archived));
        }

        private void ApplySearch(bool resetPage = true)
        {
            if (resetPage) pager.Reset();
            var term = (searchBox.Query ?? "").Trim();

            IEnumerable<CustomerDto> q = _all;

            if (_statusFilter.HasValue)
            {
                if (_statusFilter.Value == 2)
                {
                    var now = DateTime.UtcNow;
                    q = q.Where(x => x.CreatedAt.Year == now.Year && x.CreatedAt.Month == now.Month);
                }
                else
                {
                    bool wantActive = _statusFilter.Value == 1;
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
            EnsureActionsColumn();

            lblCount.Text = $"Showing {view.Count} of {_all.Count} customers";

            bool hasData = view.Count > 0;
            dgv.Visible = hasData;
            emptyState.Visible = !hasData;
            pager.Visible = hasData && view.Count > TableKit.PageSize;

            if (!hasData)
            {
                if (!string.IsNullOrEmpty(term))
                    emptyState.Show("\uE721", "No matches",
                        $"Nothing matches \u201c{term}\u201d. Try a shorter word.");
                else if (_statusFilter.HasValue)
                    emptyState.Show("\uE71C", "Nothing here yet",
                        "No customers with this filter. Select Total to see them all.");
                else
                    emptyState.Show("\uE716", "No customers yet",
                        "Click 'Add customer' to add the first one.");
            }
            else
            {
                dgv.ClearSelection();
            }

            LayoutUi();
        }

        // ═══════════ GRID STYLE ═══════════

        private void StyleGrid(DataGridView g)
        {
            typeof(DataGridView)
                .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(g, true);

            g.AutoGenerateColumns = true;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.RowHeadersVisible = false;
            g.ReadOnly = true;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.BackgroundColor = UiKit.T.Surface;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.None;
            g.GridColor = UiKit.T.LineSoft;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ScrollBars = ScrollBars.Both;
            g.RowTemplate.Height = 56;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 46;

            g.ColumnHeadersDefaultCellStyle.BackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.ForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.Font = UiKit.T.SmallStrong;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(CellPadX, 0, CellPadX, 0);

            g.DefaultCellStyle.BackColor = UiKit.T.Surface;
            g.DefaultCellStyle.ForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Font = UiKit.T.Body;
            g.DefaultCellStyle.SelectionBackColor = UiKit.T.RowHover;
            g.DefaultCellStyle.SelectionForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Padding = new Padding(CellPadX, 0, CellPadX, 0);
            g.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

            g.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= g.RowCount || e.ColumnIndex < 0) return;
                var v = Convert.ToString(g.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
                if (!string.IsNullOrWhiteSpace(v)) e.ToolTipText = v;
            };
        }

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
                if (hCol != null) hCol.Visible = false;
            }

            void Setup(string name, string header, int width, int displayIndex, bool fill = false)
            {
                var c = dgv.Columns[name];
                if (c == null) return;

                c.Visible = true;
                c.HeaderText = header;
                c.SortMode = DataGridViewColumnSortMode.NotSortable;
                c.AutoSizeMode = fill
                    ? DataGridViewAutoSizeColumnMode.Fill
                    : DataGridViewAutoSizeColumnMode.None;
                if (!fill) c.Width = width;
                else c.MinimumWidth = 180;
                c.DisplayIndex = displayIndex;
            }

            Setup("NameDisplay", "NAME", 240, 0);
            Setup("Email", "EMAIL", 0, 1, fill: true);
            Setup("Phone", "PHONE", 150, 2);
            Setup("FullAddress", "ADDRESS", 240, 3);
            Setup("StatusDisplay", "STATUS", 140, 4);
            Setup("CreatedAt", "CREATED", 130, 5);
        }

        private void EnsureActionsColumn()
        {
            if (dgv.Columns[ColActions] != null) return;

            var btn = new DataGridViewButtonColumn
            {
                Name = ColActions,
                HeaderText = "",
                Text = "",
                UseColumnTextForButtonValue = true,
                Width = 56,
                MinimumWidth = 56,
                FlatStyle = FlatStyle.Flat,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            btn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            btn.DefaultCellStyle.BackColor = UiKit.T.Surface;
            btn.DefaultCellStyle.SelectionBackColor = UiKit.T.RowHover;
            btn.DefaultCellStyle.Padding = new Padding(0);
            dgv.Columns.Add(btn);
        }

        // ═══════════ DISPLAY HELPERS ═══════════

        private static string FullNameOf(CustomerDto c)
            => $"{c.FirstName} {c.LastName}".Trim();

        // ═══════════ ACTIONS ═══════════

        private void BuildActionsMenu()
        {
            _actionsMenu = new ContextMenuStrip { ShowImageMargin = false, Font = UiKit.T.Body };
            _actionsMenu.Renderer = new QuietMenuRenderer();

            var miView = new ToolStripMenuItem("View details") { Height = 32 };
            miView.Click += OnMenuView;

            var miEdit = new ToolStripMenuItem("Edit") { Height = 32 };
            miEdit.Click += OnMenuUpdate;

            var sep = new ToolStripSeparator();

            var miArchive = new ToolStripMenuItem("Archive") { Height = 32 };
            miArchive.Click += OnMenuArchive;

            _actionsMenu.Items.AddRange(new ToolStripItem[] { miView, miEdit, sep, miArchive });

            _actionsMenu.Opening += (s, e) =>
            {
                if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
                if (dgv.Rows[_menuRowIndex].DataBoundItem is not CustomerDto c) return;

                miArchive.Text = c.IsActive ? "Archive" : "Restore";
                miArchive.ForeColor = c.IsActive ? AppTheme.Danger : AppTheme.Success;
            };

            _actionsMenu.Closed += (s, e) => dgv.Invalidate();
        }

        private void OnMenuView(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not CustomerDto c) return;

            MessageBox.Show(
                $"{FullNameOf(c)}\n\n" +
                $"Email       {c.Email ?? "\u2014"}\n" +
                $"Phone       {c.Phone ?? "\u2014"}\n" +
                $"Address     {c.Address ?? "\u2014"}\n" +
                $"Points      {c.LoyaltyPoints?.ToString() ?? "0"}\n" +
                $"Status      {(c.IsActive ? "Active" : "Archived")}\n" +
                $"Created     {c.CreatedAt:MMM d, yyyy  HH:mm}\n" +
                $"Record      #{c.CustomerId}",
                "Customer details",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OnMenuUpdate(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not CustomerDto c) return;
            OpenEditDialog(c);
        }

        private async void OnMenuArchive(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not CustomerDto c) return;

            if (c.IsActive)
                await ArchiveAsync(c);
            else
                await RestoreAsync(c);
        }

        private void Dgv_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgv.Columns[e.ColumnIndex].Name != ColActions) return;

            _menuRowIndex = e.RowIndex;
            var cellRect = dgv.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
            _actionsMenu.Show(dgv, new Point(cellRect.Right - _actionsMenu.Width, cellRect.Bottom));
        }

        private void Dgv_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgv.Rows[e.RowIndex].DataBoundItem is not CustomerDto c) return;
            OpenEditDialog(c);
        }

        // ═══════════ ARCHIVE / RESTORE ═══════════

        private async Task ArchiveAsync(CustomerDto c)
        {
            bool confirm = SaasConfirm.Ask(
                FindForm(),
                "Archive customer",
                $"Archive '{FullNameOf(c)}'?",
                confirmText: "Archive",
                danger: true,
                detail: "It leaves the list but nothing is deleted — you can restore it later.");

            if (!confirm) return;

            try
            {
                await _api.ArchiveCustomerAsync(c.CustomerId);
                SaasToast.Show(FindForm(),
                    $"'{FullNameOf(c)}' archived.", ToastKind.Warning);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(),
                    $"Couldn't archive this customer: {ex.Message}", ToastKind.Danger);
            }
        }

        private async Task RestoreAsync(CustomerDto c)
        {
            try
            {
                await _api.RestoreCustomerAsync(c.CustomerId);
                SaasToast.Show(FindForm(),
                    $"'{FullNameOf(c)}' restored.", ToastKind.Success);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(),
                    $"Couldn't restore this customer: {ex.Message}", ToastKind.Danger);
            }
        }

        // ═══════════ CELL PAINTING ═══════════

        private void PaintCell(DataGridView g, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < 0) return;
            var gr = e.Graphics;
            var cell = e.CellBounds;

            // ── Header ──
            if (e.RowIndex == -1)
            {
                using (var b = new SolidBrush(UiKit.T.Surface))
                    gr.FillRectangle(b, cell);
                using (var p = new Pen(UiKit.T.Line))
                    gr.DrawLine(p, cell.Left, cell.Bottom - 1, cell.Right, cell.Bottom - 1);

                UiKit.Quality(gr);
                UiKit.Text(gr, Convert.ToString(e.Value) ?? "", UiKit.T.SmallStrong, UiKit.T.InkMuted,
                    new Rectangle(cell.Left + CellPadX, cell.Top, Math.Max(0, cell.Width - CellPadX * 2), cell.Height - 1),
                    CellText);
                e.Handled = true;
                return;
            }

            if (e.RowIndex < 0) return;
            var item = dgv.Rows[e.RowIndex].DataBoundItem as CustomerDto;
            if (item == null) return;

            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool hovered = e.RowIndex == _hoverRow;
            bool focused = g.Focused;

            Color rowBg = UiKit.T.Surface;
            if (selected && focused) rowBg = UiKit.T.RowHover;
            else if (selected && !focused) rowBg = UiKit.T.LineSoft;
            else if (hovered) rowBg = UiKit.T.RowHover;

            using (var b = new SolidBrush(rowBg))
                gr.FillRectangle(b, cell);
            using (var p = new Pen(UiKit.T.LineSoft))
                gr.DrawLine(p, cell.Left, cell.Bottom - 1, cell.Right, cell.Bottom - 1);

            UiKit.Quality(gr);

            if (e.ColumnIndex == 0 && selected && focused)
                UiKit.FillRounded(gr, new Rectangle(cell.Left, cell.Top + 12, 3, cell.Height - 25), 1, AppTheme.Primary);

            var rect = new Rectangle(cell.Left + CellPadX, cell.Top,
                Math.Max(0, cell.Width - CellPadX * 2), cell.Height - 1);
            int cy = rect.Top + rect.Height / 2;
            string text = Convert.ToString(e.FormattedValue) ?? "";
            string col = g.Columns[e.ColumnIndex].Name;

            switch (col)
            {
                case "NameDisplay":
                    PaintNameCell(gr, item, rect);
                    break;

                case "Email":
                    if (string.IsNullOrWhiteSpace(text))
                        DrawMuted(gr, "—", rect, UiKit.T.Small, UiKit.T.InkFaint);
                    else
                        UiKit.Text(gr, text, UiKit.T.Small, UiKit.T.Ink, rect, CellText);
                    break;

                case "Phone":
                    if (string.IsNullOrWhiteSpace(text))
                        DrawMuted(gr, "—", rect, UiKit.T.Small, UiKit.T.InkFaint);
                    else
                        UiKit.Text(gr, text, MonoFont, UiKit.T.InkMuted, rect, CellText);
                    break;

                case "FullAddress":
                    if (string.IsNullOrWhiteSpace(text))
                        DrawMuted(gr, "—", rect, UiKit.T.Small, UiKit.T.InkFaint);
                    else
                        UiKit.Text(gr, text, UiKit.T.Small, UiKit.T.InkMuted, rect, CellText);
                    break;

                case "StatusDisplay":
                    {
                        bool active = item.IsActive;
                        Color fg = active ? AppTheme.Success : UiKit.T.InkMuted;
                        string txt = active ? "Active" : "Archived";
                        PaintDotPill(gr, rect, cy, txt, fg, UiKit.Micro);
                        break;
                    }

                case "CreatedAt":
                    UiKit.Text(gr, item.CreatedAt.ToString("MMM d, yyyy"),
                        MonoFont, UiKit.T.InkMuted, rect, CellText);
                    break;

                case ColActions:
                    {
                        int size = 30;
                        var btn = new Rectangle(rect.Right - size, cy - size / 2, size, size);
                        bool hot = hovered;

                        UiKit.FillRounded(gr, btn, 8, hot ? UiKit.Wash(AppTheme.Primary) : Color.Transparent);

                        int dotR = 2;
                        int dotGap = 6;
                        int dotY = cy - dotR;
                        int startX = btn.Left + (btn.Width / 2) - dotGap;
                        using (var b = new SolidBrush(hot ? AppTheme.Primary : UiKit.T.InkMuted))
                        {
                            gr.FillEllipse(b, startX - dotR, dotY, dotR * 2, dotR * 2);
                            gr.FillEllipse(b, startX - dotR + dotGap, dotY, dotR * 2, dotR * 2);
                            gr.FillEllipse(b, startX - dotR + dotGap * 2, dotY, dotR * 2, dotR * 2);
                        }
                        e.Handled = true;
                        return;
                    }

                default:
                    e.PaintContent(e.CellBounds);
                    e.Handled = true;
                    return;
            }

            e.Handled = true;
        }

        private static void PaintNameCell(Graphics gr, CustomerDto item, Rectangle rect)
        {
            const int av = 36;
            string name = FullNameOf(item);
            if (string.IsNullOrWhiteSpace(name)) name = "(unnamed)";

            var avRect = new Rectangle(rect.Left, rect.Top + (rect.Height - av) / 2, av, av);
            Color ac = AvatarColor(name);
            UiKit.FillRounded(gr, avRect, 10, UiKit.Wash(ac));

            string initial = name.Trim().Substring(0, 1).ToUpperInvariant();
            UiKit.Text(gr, initial, UiKit.T.BodyStrong, ac, avRect, UiKit.Center);

            int tx = avRect.Right + 12;
            int tw = Math.Max(0, rect.Right - tx);
            string sub = item.Email ?? "";

            if (string.IsNullOrWhiteSpace(sub))
            {
                UiKit.Text(gr, name, UiKit.T.BodyStrong, UiKit.T.Ink,
                    new Rectangle(tx, rect.Top, tw, rect.Height), CellText);
                return;
            }

            int h1 = UiKit.T.BodyStrong.Height + 2;
            int h2 = UiKit.T.Small.Height + 2;
            int y0 = rect.Top + (rect.Height - (h1 + h2)) / 2;

            UiKit.Text(gr, name, UiKit.T.BodyStrong, UiKit.T.Ink, new Rectangle(tx, y0, tw, h1), CellText);
            UiKit.Text(gr, sub, UiKit.T.Small, UiKit.T.InkMuted, new Rectangle(tx, y0 + h1, tw, h2), CellText);
        }

        private static void PaintDotPill(Graphics gr, Rectangle rect, int cy, string text, Color fg, Font font)
        {
            int textW = MeasureW(text, font);
            int pillW = Math.Min(rect.Width, textW + 34);
            var pill = new Rectangle(rect.Left, cy - 12, pillW, 24);

            UiKit.FillRounded(gr, pill, 12, UiKit.Wash(fg));
            UiKit.FillRounded(gr, new Rectangle(pill.Left + 11, cy - 3, 6, 6), 3, fg);
            UiKit.Text(gr, text, font, fg,
                new Rectangle(pill.Left + 23, pill.Top, Math.Max(0, pill.Width - 31), pill.Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | Flat);
        }

        private static void DrawMuted(Graphics gr, string text, Rectangle rect, Font font, Color color)
            => UiKit.Text(gr, text, font, color, rect, CellText);

        // ═══════════ MODAL LAUNCHERS ═══════════

        private void OpenAddDialog()
        {
            using var dlg = new CustomerFormDialog(null);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private void OpenEditDialog(CustomerDto c)
        {
            using var dlg = new CustomerFormDialog(c.CustomerId, c);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        //  EMBEDDED KPI TILE (matches the other modules)
        // ═══════════════════════════════════════════════════════════════

        private sealed class KpiTile : Control
        {
            private const int Pad = 22;
            private const int IconSize = 36;
            private const int ChevronW = 16;
            private const int MinHeight = 132;

            private const TextFormatFlags One = TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            private const TextFormatFlags Wrapped = One | TextFormatFlags.WordBreak;

            private static readonly Font[] NumFonts =
            {
                AppFonts.Strong(26F),
                AppFonts.Strong(22F),
                AppFonts.Strong(18F),
                AppFonts.Strong(15F),
                AppFonts.Strong(12F)
            };

            private string _label = "";
            private string _number = "0";
            private string _sub = "";
            private Color _accent = AppTheme.Primary;
            private string _glyph = "";
            private bool _hover;
            private bool _down;

            public KpiTile()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
                TabStop = true;
                Cursor = Cursors.Hand;
            }

            public void Set(string label, string number, string sub, Color accent, string glyph)
            {
                _label = label;
                _number = number;
                _sub = sub;
                _accent = accent;
                _glyph = glyph;
                AccessibleName = $"{label}: {number}. {sub}";
                Invalidate();
            }

            public int HeightFor(int width) =>
                string.IsNullOrEmpty(_label) ? MinHeight : Math.Max(MinHeight, Measure(width).Total);

            private static int TextH(string text, Font font, int width, bool wrap)
            {
                if (string.IsNullOrEmpty(text)) return font.Height;
                var size = TextRenderer.MeasureText(text, font,
                    new Size(Math.Max(10, width - 4), int.MaxValue), wrap ? Wrapped : One);
                return Math.Max(font.Height, size.Height) + 2;
            }

            private (Font NumFont, bool NumWrap, string Main, int TopH, int NumH, int CapH, int Total) Measure(int width)
            {
                int inner = Math.Max(40, width - Pad * 2);
                int labelW = Math.Max(40, inner - IconSize - 12 - ChevronW);

                int labelH = TextH(_label, UiKit.T.SmallStrong, labelW, true);
                int topH = Math.Max(IconSize, labelH);

                Font numFont = NumFonts[^1];
                bool numWrap = true;
                foreach (var f in NumFonts)
                {
                    var w = TextRenderer.MeasureText(_number, f, new Size(int.MaxValue, int.MaxValue), One).Width;
                    if (w <= inner - 4) { numFont = f; numWrap = false; break; }
                }

                int numH = TextH(_number, numFont, inner, numWrap);
                int capH = TextH(_sub, UiKit.T.Small, inner, true);

                int total = Pad + topH + 14 + numH + 6 + capH + Pad;
                return (numFont, numWrap, _number, topH, numH, capH, total);
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _down = true; Focus(); Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var bg = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(bg, ClientRectangle);

                bool loading = string.IsNullOrEmpty(_label);
                Color accent = loading ? UiKit.T.InkFaint : _accent;

                UiKit.Card(g, ClientRectangle, UiKit.T.Radius,
                    _down && !loading ? UiKit.Wash(accent) : UiKit.T.Surface,
                    _hover && !loading ? accent : UiKit.T.Line);

                if (loading)
                {
                    UiKit.FillRounded(g, new Rectangle(Pad, Pad, IconSize, IconSize), 10, UiKit.T.LineSoft);
                    UiKit.FillRounded(g, new Rectangle(Pad + IconSize + 12, Pad + 12, Math.Max(20, Width / 3), 12), 4, UiKit.T.LineSoft);
                    UiKit.FillRounded(g, new Rectangle(Pad, Pad + 56, Math.Max(20, Width / 2), 26), 4, UiKit.T.LineSoft);
                    return;
                }

                var m = Measure(Width);
                int inner = Width - Pad * 2;

                var iconRect = new Rectangle(Pad, Pad + (m.TopH - IconSize) / 2, IconSize, IconSize);
                UiKit.FillRounded(g, iconRect, 10, UiKit.Wash(accent));
                using (var f = UiKit.GlyphFont(12F))
                    UiKit.Text(g, _glyph, f, accent, iconRect, UiKit.Center);

                UiKit.Text(g, _label, UiKit.T.SmallStrong, UiKit.T.InkMuted,
                    new Rectangle(Pad + IconSize + 12, Pad, inner - IconSize - 12 - ChevronW, m.TopH),
                    Wrapped | TextFormatFlags.VerticalCenter);

                using (var cf = UiKit.GlyphFont(9F))
                    UiKit.Text(g, "\uE76C", cf, _hover ? accent : UiKit.T.InkFaint,
                        new Rectangle(Width - Pad - ChevronW + 2, Pad + (m.TopH - 20) / 2, ChevronW, 20), UiKit.Center);

                int numTop = Pad + m.TopH + 14;
                UiKit.Text(g, m.Main, m.NumFont, UiKit.T.Ink,
                    new Rectangle(Pad, numTop, inner, m.NumH),
                    (m.NumWrap ? Wrapped : One) | TextFormatFlags.Top);

                UiKit.Text(g, _sub, UiKit.T.Small, UiKit.T.InkMuted,
                    new Rectangle(Pad, numTop + m.NumH + 6, inner, m.CapH),
                    Wrapped | TextFormatFlags.Top);

                if (Focused)
                {
                    var ring = ClientRectangle;
                    ring.Inflate(-1, -1);
                    UiKit.StrokeRounded(g, ring, UiKit.T.Radius, accent, 2f);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  SEGMENTED FILTER (int? tag; parallel List<int?> for safe null)
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class CustomerSegmentedFilter : Control
        {
            private readonly List<(string Key, int? Tag)> _items = new();
            private readonly List<int?> _counts = new();
            private int _selected = 0;
            private int _hover = -1;

            public event EventHandler<int?>? SelectionChanged;

            public CustomerSegmentedFilter((string Key, int? Tag)[] items)
            {
                foreach (var it in items)
                {
                    _items.Add(it);
                    _counts.Add(null);
                }

                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = UiKit.T.Surface;
                Cursor = Cursors.Hand;
                Font = UiKit.T.SmallStrong;
                Height = 36;
            }

            public int PreferredWidth
            {
                get
                {
                    int total = 16;
                    foreach (var item in _items)
                    {
                        string text = Display(item);
                        var sz = TextRenderer.MeasureText(text, Font);
                        total += Math.Max(104, sz.Width + 28);
                    }
                    return Math.Max(330, total);
                }
            }

            public void UpdateCounts(params (int? Tag, int? Count)[] pairs)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    _counts[i] = null;
                    foreach (var p in pairs)
                    {
                        if (Nullable.Equals(_items[i].Tag, p.Tag))
                        {
                            _counts[i] = p.Count;
                            break;
                        }
                    }
                }
                Invalidate();
            }

            public void SetKey(int? tag)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (Nullable.Equals(_items[i].Tag, tag))
                    {
                        if (_selected != i)
                        {
                            _selected = i;
                            Invalidate();
                        }
                        return;
                    }
                }
            }

            private string Display((string Key, int? Tag) item)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (Nullable.Equals(_items[i].Tag, item.Tag))
                    {
                        if (_counts[i].HasValue)
                            return $"{item.Key} ({_counts[i]!.Value})";
                        return item.Key;
                    }
                }
                return item.Key;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);
                UiKit.FillRounded(g, new Rectangle(0, 0, Width, Height), 9, UiKit.T.LineSoft);

                if (_items.Count == 0) return;
                int segW = (Width - 4) / _items.Count;

                for (int i = 0; i < _items.Count; i++)
                {
                    var seg = new Rectangle(2 + i * segW, 2, segW, Height - 4);
                    bool active = i == _selected;
                    if (active)
                    {
                        UiKit.FillRounded(g, seg, 7, UiKit.T.Surface);
                        using var pen = new Pen(UiKit.T.Line, 1);
                        using var path = UiKit.Rounded(new Rectangle(seg.X, seg.Y, seg.Width - 1, seg.Height - 1), 7);
                        g.DrawPath(pen, path);
                    }

                    string text = Display(_items[i]);
                    Color fg = active ? UiKit.T.Ink : (i == _hover ? UiKit.T.Ink : UiKit.T.InkMuted);
                    UiKit.Text(g, text, UiKit.T.SmallStrong, fg, seg,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (_items.Count == 0) return;
                int segW = (Width - 4) / _items.Count;
                int idx = Math.Clamp((e.X - 2) / Math.Max(1, segW), 0, _items.Count - 1);
                if (idx != _hover) { _hover = idx; Invalidate(); }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _hover = -1; Invalidate(); base.OnMouseLeave(e);
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                if (_items.Count == 0) return;
                int segW = (Width - 4) / _items.Count;
                int idx = Math.Clamp((e.X - 2) / Math.Max(1, segW), 0, _items.Count - 1);
                if (idx >= 0 && idx < _items.Count && idx != _selected)
                {
                    _selected = idx;
                    Invalidate();
                    SelectionChanged?.Invoke(this, _items[_selected].Tag);
                }
                base.OnMouseClick(e);
            }
        }

        [DesignerCategory("Code")]
        private sealed class QuietMenuRenderer : ToolStripProfessionalRenderer
        {
            public QuietMenuRenderer() : base(new Colors()) { RoundedEdges = false; }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var r = new Rectangle(4, 0, e.Item.Width - 8, e.Item.Height);
                if (e.Item.Selected) UiKit.FillRounded(e.Graphics, r, 6, UiKit.T.RowHover);
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
                public override Color MenuItemBorder => UiKit.T.Surface;
                public override Color ImageMarginGradientBegin => UiKit.T.Surface;
                public override Color ImageMarginGradientMiddle => UiKit.T.Surface;
                public override Color ImageMarginGradientEnd => UiKit.T.Surface;
            }
        }
    }
}