using CRM.winforms.Auth;
using CRM.winforms.Forms;
using CRM.winforms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Professional Repair Requests Operations Command Center for Staff.
    /// Manages intake, diagnostics, bench progress, parts/labor costing,
    /// 1-click status transitions, and rapid customer pickup follow-ups.
    /// UI aligned with Companies / Subscriptions / Admin / System Monitor.
    /// </summary>
    [DesignerCategory("Code")]
    public class RepairRequestListControl : UserControl
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private List<RepairRequestDto> _all = new();
        private List<CustomerDto> _cachedCustomers = new();
        private int? _statusFilter = null; // null = All, 0 = Pending, 1 = Approved, 2 = In Progress, 3 = Completed, 99 = Urgent

        // ═══════════ CONTROLS ═══════════

        private Label lblTitle = null!;
        private Label lblSummary = null!;
        private SaasButton btnAdd = null!;
        private SaasButton btnRefresh = null!;

        // ── KPI Tiles (also act as quick filters) ──
        private KpiTile tileTotal = null!;
        private KpiTile tilePending = null!;
        private KpiTile tileApproved = null!;
        private KpiTile tileInProgress = null!;
        private KpiTile tileUrgent = null!;
        private KpiTile tileCompleted = null!;
        private readonly List<KpiTile> _tiles = new();

        // ── Workbench Card ──
        private WorkbenchCard card = null!;
        private WorkbenchSearch searchBox = null!;
        private RepairSegmentedFilter filterBar = null!;
        private Label lblCount = null!;
        private DataGridView dgv = null!;
        private WorkbenchState emptyState = null!;
        private TablePagination pager = null!;

        private const string ColActions = "colActions";
        private ContextMenuStrip _actionsMenu = null!;
        private int _menuRowIndex = -1;
        private int _hoverRow = -1;

        public event EventHandler<string>? ActionRequested;

        // ═══════════ SHARED DRAWING HELPERS ═══════════
        private const int CellPadX = 14;
        private const TextFormatFlags Flat = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        private const TextFormatFlags CellText = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | Flat;

        private static readonly Font MonoFont = new Font("Consolas", 9F);

        private static int MeasureW(string text, Font font) =>
            TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
                Flat | TextFormatFlags.SingleLine).Width;

        // ═══════════ CONSTRUCTOR ═══════════

        public RepairRequestListControl()
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
                Text = "Repair Requests",
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

            btnAdd = new SaasButton("New repair", SaasButtonVariant.Primary, "\uE710");
            btnAdd.Size = new Size(140, 36);
            btnAdd.Click += (s, e) => OpenAddDialog();

            Controls.Add(lblTitle);
            Controls.Add(lblSummary);
            Controls.Add(btnRefresh);
            Controls.Add(btnAdd);

            // ── KPI Tiles (also act as quick filters) ──
            tileTotal = AddTile("TOTAL", "0", "All repair requests", AppTheme.Primary, "\uE9D5");
            tileTotal.Click += (s, e) => SetStatusFilter(null);

            tilePending = AddTile("PENDING", "0", "Awaiting intake review", AppTheme.Warning, "\uE823");
            tilePending.Click += (s, e) => SetStatusFilter(0);

            tileApproved = AddTile("APPROVED", "0", "Ready for workbench", AppTheme.Primary, "\uE73E");
            tileApproved.Click += (s, e) => SetStatusFilter(1);

            tileInProgress = AddTile("IN PROGRESS", "0", "On the bench now", Color.FromArgb(59, 130, 246), "\uE9F5");
            tileInProgress.Click += (s, e) => SetStatusFilter(2);

            tileUrgent = AddTile("URGENT", "0", "High-priority open tickets", AppTheme.Danger, "\uE7BA");
            tileUrgent.Click += (s, e) => SetStatusFilter(99);

            tileCompleted = AddTile("COMPLETED", "0", "Ready for pickup", AppTheme.Success, "\uE930");
            tileCompleted.Click += (s, e) => SetStatusFilter(3);

            // ── Workbench Card ──
            card = new WorkbenchCard { BackColor = UiKit.T.Surface };

            searchBox = new WorkbenchSearch
            {
                Placeholder = "Search by ticket #, customer, device, serial, or notes..."
            };
            searchBox.QueryChanged += (s, e) => ApplySearch();

            filterBar = new RepairSegmentedFilter(new (string, int?)[]
            {
                ("All", null),
                ("Pending", 0),
                ("Approved", 1),
                ("In Progress", 2),
                ("Completed", 3)
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

        private void SetStatusFilter(int? tag)
        {
            _statusFilter = tag;
            filterBar.SetKey(tag);
            ApplySearch();
        }

        // ═══════════ KEYBOARD SHORTCUTS ═══════════

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.F:
                    searchBox.Focus();
                    return true;

                case Keys.Control | Keys.N:
                    OpenAddDialog();
                    return true;

                case Keys.F5:
                    _ = ReloadAsync();
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

            // ── KPI Tiles (6 across wide, then 3 / 2 / 1) ──
            int tileCols = contentW >= 1360 ? 6 : contentW >= 1000 ? 3 : contentW >= 700 ? 2 : 1;
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

        // ═══════════ DATA LOAD ═══════════

        public async Task ReloadAsync()
        {
            try
            {
                var repTask = _api.GetRepairRequestsAsync();
                var custTask = _cachedCustomers.Count == 0
                    ? _api.GetCustomersAsync()
                    : Task.FromResult(_cachedCustomers);

                await Task.WhenAll(repTask, custTask);

                _all = await repTask ?? new List<RepairRequestDto>();
                _cachedCustomers = await custTask ?? new List<CustomerDto>();

                UpdateTiles();
                ApplySearch();
            }
            catch (Exception ex)
            {
                _all = new List<RepairRequestDto>();
                UpdateTiles();
                ApplySearch();

                SaasToast.Show(FindForm(),
                    $"Couldn't load repair requests: {ex.Message}",
                    ToastKind.Danger);
            }
        }

        private void UpdateTiles()
        {
            int total = _all.Count;
            int pending = _all.Count(x => x.Status == 0);
            int approved = _all.Count(x => x.Status == 1);
            int inProgress = _all.Count(x => x.Status == 2);
            int urgent = _all.Count(x => (x.Priority == 2 || x.Priority == 3) && x.Status != 3 && x.Status != 4);
            int completed = _all.Count(x => x.Status == 3);

            tileTotal.Set("TOTAL", total.ToString(),
                $"{pending} pending · {inProgress} on bench", AppTheme.Primary, "\uE9D5");
            tilePending.Set("PENDING", pending.ToString(),
                "Awaiting intake review", AppTheme.Warning, "\uE823");
            tileApproved.Set("APPROVED", approved.ToString(),
                "Ready for workbench", AppTheme.Primary, "\uE73E");
            tileInProgress.Set("IN PROGRESS", inProgress.ToString(),
                "On the bench now", Color.FromArgb(59, 130, 246), "\uE9F5");
            tileUrgent.Set("URGENT", urgent.ToString(),
                "High-priority open tickets", AppTheme.Danger, "\uE7BA");
            tileCompleted.Set("COMPLETED", completed.ToString(),
                "Ready for pickup", AppTheme.Success, "\uE930");

            lblSummary.Text =
                $"{total} total  ·  {pending} pending  ·  {inProgress} in progress  ·  {urgent} urgent  ·  {completed} completed";

            filterBar.UpdateCounts(
                (null, total),
                (0, pending),
                (1, approved),
                (2, inProgress),
                (3, completed));
        }

        private void ApplySearch(bool resetPage = true)
        {
            if (resetPage) pager.Reset();
            var term = (searchBox.Query ?? "").Trim();

            IEnumerable<RepairRequestDto> q = _all;

            if (_statusFilter.HasValue)
            {
                if (_statusFilter.Value == 99)
                    q = q.Where(x => (x.Priority == 2 || x.Priority == 3) && x.Status != 3 && x.Status != 4);
                else
                    q = q.Where(x => x.Status == _statusFilter.Value);
            }

            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(x =>
                    (x.RequestNumber ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.CustomerName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.CustomerPhone ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.CustomerEmail ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.DeviceModel ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.SerialNumber ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.IssueDescription ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.TechnicianNotes ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            var view = q.OrderByDescending(x => x.RequestDate).ToList();

            pager.SetTotal(view.Count);
            var page = pager.Slice(view);

            dgv.DataSource = null;
            dgv.DataSource = page;
            ConfigureColumns();
            EnsureActionsColumn();

            lblCount.Text = $"Showing {view.Count} of {_all.Count} repair requests";

            bool hasData = view.Count > 0;
            dgv.Visible = hasData;
            emptyState.Visible = !hasData;
            pager.Visible = hasData && view.Count > TableKit.PageSize;

            if (!hasData)
            {
                if (!string.IsNullOrEmpty(term))
                    emptyState.Show("\uE721", "No matches",
                        $"Nothing matches \u201c{term}\u201d. Try a shorter keyword.");
                else if (_statusFilter == 99)
                    emptyState.Show("\uE73E", "No urgent repairs",
                        "Great work! There are no overdue or urgent repair tickets on the workbench.");
                else if (_statusFilter.HasValue)
                    emptyState.Show("\uE71C", "Nothing here yet",
                        "No repair tickets matching this status. Select Total to view all.");
                else
                    emptyState.Show("\uE8BD", "No repair requests yet",
                        "Click 'New repair' to intake the first computer repair job.");
            }
            else
            {
                dgv.ClearSelection();
            }

            LayoutUi();
        }

        // ═══════════ GRID STYLE & COLUMNS ═══════════

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
                "RepairRequestId", "CustomerId", "DeviceId",
                "Status", "Priority", "CompletionDate",
                "EstimatedCost", "ActualCost", "PartsCost", "LaborCost",
                "TechnicianNotes", "AssignedToStaffId", "AssignedToManagerId",
                "CustomerName", "CustomerPhone", "CustomerEmail",
                "DeviceModel", "SerialNumber", "ContactDisplay", "DeviceDisplay",
                "SerialDisplay", "CompletionDisplay"
            })
            {
                var col = dgv.Columns[hidden];
                if (col != null) col.Visible = false;
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
                else c.MinimumWidth = 220;
                c.DisplayIndex = displayIndex;
            }

            Setup("PriorityText", "PRIORITY", 120, 0);
            Setup("RequestNumber", "TICKET", 170, 1);
            Setup("CustomerDisplay", "CUSTOMER", 220, 2);
            Setup("DeviceDisplay", "DEVICE", 220, 3);
            Setup("IssueDescription", "ISSUE & NOTES", 0, 4, fill: true);
            Setup("StatusText", "STATUS", 150, 5);
            Setup("CostDisplay", "COST", 120, 6);
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
            var rowItem = dgv.Rows[e.RowIndex].DataBoundItem as RepairRequestDto;
            if (rowItem == null) return;

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
                case "PriorityText":
                    {
                        Color accent = text switch
                        {
                            "Urgent" => AppTheme.Danger,
                            "High" => AppTheme.Warning,
                            "Medium" => AppTheme.Primary,
                            _ => UiKit.T.InkMuted
                        };
                        PaintDotPill(gr, rect, cy, string.IsNullOrWhiteSpace(text) ? "Low" : text, accent, UiKit.Micro);
                        break;
                    }

                case "RequestNumber":
                    PaintTwoLine(gr, rect,
                        rowItem.RequestNumber ?? "",
                        rowItem.RequestDate.ToString("MMM d  HH:mm"),
                        AppTheme.Primary,
                        UiKit.T.InkMuted,
                        mono: true);
                    break;

                case "CustomerDisplay":
                    PaintTwoLine(gr, rect,
                        rowItem.CustomerDisplay ?? "",
                        rowItem.ContactDisplay ?? "",
                        UiKit.T.Ink, UiKit.T.InkMuted);
                    break;

                case "DeviceDisplay":
                    {
                        string serial = !string.IsNullOrWhiteSpace(rowItem.SerialNumber)
                            ? $"SN: {rowItem.SerialNumber}"
                            : "No serial saved";
                        PaintTwoLine(gr, rect,
                            rowItem.DeviceDisplay ?? "",
                            serial,
                            UiKit.T.Ink, UiKit.T.InkMuted);
                        break;
                    }

                case "IssueDescription":
                    {
                        string snippet = !string.IsNullOrWhiteSpace(rowItem.TechnicianNotes)
                            ? rowItem.TechnicianNotes.Replace("\r", " ").Replace("\n", " ")
                            : "No technician notes logged";
                        PaintTwoLine(gr, rect,
                            rowItem.IssueDescription ?? "",
                            snippet,
                            UiKit.T.Ink, UiKit.T.InkMuted);
                        break;
                    }

                case "StatusText":
                    {
                        Color accent = text switch
                        {
                            "Pending" => AppTheme.Warning,
                            "Approved" => AppTheme.Primary,
                            "In Progress" => Color.FromArgb(59, 130, 246),
                            "Completed" => AppTheme.Success,
                            "Rejected" => AppTheme.Danger,
                            _ => UiKit.T.InkMuted
                        };
                        PaintDotPill(gr, rect, cy, text, accent, UiKit.Micro);
                        break;
                    }

                case "CostDisplay":
                    {
                        var textRect = new Rectangle(cell.Left + CellPadX, cell.Top,
                            Math.Max(0, cell.Width - CellPadX * 2), cell.Height - 1);
                        bool hasCost = rowItem.ActualCost.HasValue && rowItem.ActualCost > 0;
                        Color fg = hasCost ? UiKit.T.Ink : UiKit.T.InkFaint;
                        Font font = hasCost ? UiKit.T.BodyStrong : UiKit.T.Body;

                        UiKit.Text(gr, rowItem.CostDisplay ?? "—", font, fg, textRect,
                            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | Flat);
                        break;
                    }

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

        private static void PaintTwoLine(
            Graphics gr, Rectangle rect, string line1, string line2,
            Color fg1, Color fg2, bool mono = false)
        {
            int h1 = UiKit.T.BodyStrong.Height + 2;
            int h2 = UiKit.T.Small.Height + 2;
            int y0 = rect.Top + (rect.Height - (h1 + h2)) / 2;

            UiKit.Text(gr,
                string.IsNullOrWhiteSpace(line1) ? "—" : line1,
                UiKit.T.BodyStrong, fg1, new Rectangle(rect.Left, y0, rect.Width, h1),
                CellText);

            UiKit.Text(gr,
                string.IsNullOrWhiteSpace(line2) ? "" : line2,
                mono ? MonoFont : UiKit.T.Small, fg2,
                new Rectangle(rect.Left, y0 + h1, rect.Width, h2),
                CellText);
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

        // ═══════════ ACTIONS MENU ═══════════

        private void BuildActionsMenu()
        {
            _actionsMenu = new ContextMenuStrip { ShowImageMargin = false, Font = UiKit.T.Body };
            _actionsMenu.Renderer = new QuietMenuRenderer();

            var miApprove = new ToolStripMenuItem("Approve Repair Request (Manager)") { Height = 32 };
            miApprove.Click += OnMenuApproveRepair;

            var miReassign = new ToolStripMenuItem("Reassign Repair Ticket (Manager)") { Height = 32 };
            miReassign.Click += OnMenuReassignRepair;

            var miStart = new ToolStripMenuItem("Start Repair (Put On Workbench)") { Height = 32 };
            miStart.Click += async (s, e) => await QuickStatusAsync(2);

            var miComplete = new ToolStripMenuItem("Complete Repair & Enter Final Cost") { Height = 32 };
            miComplete.Click += OnMenuQuickComplete;

            var miFollowUp = new ToolStripMenuItem("Schedule Ready-for-Pickup / Follow-Up") { Height = 32 };
            miFollowUp.Click += OnMenuScheduleFollowUp;

            var miInteraction = new ToolStripMenuItem("Log Customer Inquiry / Concern") { Height = 32 };
            miInteraction.Click += OnMenuLogInteraction;

            var miCall = new ToolStripMenuItem("Call Customer") { Height = 32 };
            miCall.Click += OnMenuCall;

            var miEmail = new ToolStripMenuItem("Email Customer") { Height = 32 };
            miEmail.Click += OnMenuEmail;

            var miHistory = new ToolStripMenuItem("View Customer History") { Height = 32 };
            miHistory.Click += OnMenuCustomerHistory;

            var miEdit = new ToolStripMenuItem("Edit Repair Details") { Height = 32 };
            miEdit.Click += (s, e) =>
            {
                if (_menuRowIndex >= 0 && dgv.Rows[_menuRowIndex].DataBoundItem is RepairRequestDto dto)
                    OpenEditDialog(dto);
            };

            _actionsMenu.Items.AddRange(new ToolStripItem[]
            {
                miApprove, miReassign, new ToolStripSeparator(),
                miStart, miComplete, miFollowUp, miInteraction, new ToolStripSeparator(),
                miCall, miEmail, new ToolStripSeparator(),
                miHistory, miEdit
            });

            _actionsMenu.Opening += (s, e) =>
            {
                if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
                if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;

                bool isManager = string.Equals(UserSession.Role, "Manager", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(UserSession.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(UserSession.Role, "Super Admin", StringComparison.OrdinalIgnoreCase);

                miApprove.Visible = isManager;
                miApprove.Enabled = dto.Status == 0 || dto.Status == 5;

                miReassign.Visible = isManager;
                miReassign.Enabled = dto.Status != 3 && dto.Status != 4;

                miStart.Enabled = dto.Status == 0 || dto.Status == 1 || dto.Status == 5;
                miComplete.Enabled = dto.Status != 3 && dto.Status != 4;

                miCall.Enabled = !string.IsNullOrWhiteSpace(dto.CustomerPhone);
                miCall.Text = !string.IsNullOrWhiteSpace(dto.CustomerPhone) ? $"Call Customer: {dto.CustomerPhone}" : "Call Customer";

                miEmail.Enabled = !string.IsNullOrWhiteSpace(dto.CustomerEmail);
                miEmail.Text = !string.IsNullOrWhiteSpace(dto.CustomerEmail) ? $"Email Customer: {dto.CustomerEmail}" : "Email Customer";

                miHistory.Enabled = dto.CustomerId > 0;
            };

            _actionsMenu.Closed += (s, e) => dgv.Invalidate();
        }

        private void OnMenuApproveRepair(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;

            using var dlg = new ApproveRepairDialog(dto);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private void OnMenuReassignRepair(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;

            using var dlg = new ReassignRepairDialog(dto);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private async Task QuickStatusAsync(int newStatus)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;

            try
            {
                await _api.ChangeRepairStatusAsync(dto.RepairRequestId, newStatus);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(),
                    $"Failed to update repair status: {ex.Message}",
                    ToastKind.Danger);
            }
        }

        private void OnMenuQuickComplete(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;

            using var dlg = new QuickCompleteRepairDialog(dto);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK)
            {
                _ = ReloadAsync();

                if (dlg.ShouldSchedulePickupFollowUp)
                {
                    var prefillFollowUp = new FollowUpDto
                    {
                        CustomerId = dto.CustomerId,
                        RepairRequestId = dto.RepairRequestId,
                        Subject = $"Ready for Pickup: {dto.RequestNumber} · {dto.DeviceDisplay}",
                        ScheduledAt = DateTime.Today.AddHours(14),
                        Channel = 0
                    };

                    using var followUpDlg = new FollowUpFormDialog(prefillFollowUp, _cachedCustomers, _all);
                    if (followUpDlg.ShowModal(FindForm()) == DialogResult.OK)
                        ActionRequested?.Invoke(this, "follow-ups");
                }
            }
        }

        private void OnMenuScheduleFollowUp(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;

            var prefillFollowUp = new FollowUpDto
            {
                CustomerId = dto.CustomerId,
                RepairRequestId = dto.RepairRequestId,
                Subject = $"Follow-up: {dto.RequestNumber} · {dto.DeviceDisplay}",
                ScheduledAt = DateTime.Today.AddDays(1).AddHours(10),
                Channel = 0
            };

            using var dlg = new FollowUpFormDialog(prefillFollowUp, _cachedCustomers, _all);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK)
                ActionRequested?.Invoke(this, "follow-ups");
        }

        private void OnMenuLogInteraction(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;

            var prefillInteraction = new InteractionDto
            {
                CustomerId = dto.CustomerId,
                RepairRequestId = dto.RepairRequestId,
                Subject = $"Inquiry on {dto.RequestNumber} ({dto.DeviceDisplay})"
            };

            using var dlg = new InteractionFormDialog(prefillInteraction, null, _cachedCustomers, _all);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK)
                ActionRequested?.Invoke(this, "interactions");
        }

        private void OnMenuCall(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;
            if (string.IsNullOrWhiteSpace(dto.CustomerPhone)) return;

            try
            {
                Clipboard.SetText(dto.CustomerPhone);
                Process.Start(new ProcessStartInfo($"tel:{dto.CustomerPhone}") { UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show($"Customer phone number: {dto.CustomerPhone}\n(Copied to clipboard)",
                    "Phone Contact", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnMenuEmail(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;
            if (string.IsNullOrWhiteSpace(dto.CustomerEmail)) return;

            try
            {
                string subject = Uri.EscapeDataString($"Update on your computer repair: {dto.RequestNumber}");
                Process.Start(new ProcessStartInfo($"mailto:{dto.CustomerEmail}?subject={subject}") { UseShellExecute = true });
            }
            catch
            {
                Clipboard.SetText(dto.CustomerEmail);
                MessageBox.Show($"Customer email: {dto.CustomerEmail}\n(Copied to clipboard)",
                    "Email Contact", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnMenuCustomerHistory(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;

            if (dto.CustomerId > 0)
                ActionRequested?.Invoke(this, $"customer-history:{dto.CustomerId}");
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
            if (dgv.Rows[e.RowIndex].DataBoundItem is not RepairRequestDto dto) return;
            OpenEditDialog(dto);
        }

        // ═══════════ MODAL LAUNCHERS ═══════════

        private void OpenAddDialog()
        {
            using var dlg = new RepairRequestFormDialog(null, null, _cachedCustomers);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private void OpenEditDialog(RepairRequestDto dto)
        {
            using var dlg = new RepairRequestFormDialog(dto, null, _cachedCustomers);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        //  EMBEDDED KPI TILE (matches Companies / Subscriptions / Admin)
        // ═══════════════════════════════════════════════════════════════

        private sealed class KpiTile : Control
        {
            private const int Pad = 22;
            private const int IconSize = 36;
            private const int ChevronW = 16;
            private const int UnitGap = 6;
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

            private static readonly Font UnitFont = AppFonts.Strong(11F);

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

            private static bool TrySplit(string number, out string main, out string unit)
            {
                main = number;
                unit = "";
                if (string.IsNullOrWhiteSpace(number)) return false;

                int sp = number.IndexOf(' ');
                if (sp <= 0 || sp >= number.Length - 1) return false;

                string head = number.Substring(0, sp);
                if (!head.Any(char.IsDigit)) return false;

                main = head;
                unit = number.Substring(sp + 1);
                return true;
            }

            private (Font NumFont, bool NumWrap, string Main, string Unit, int TopH, int NumH, int CapH, int Total) Measure(int width)
            {
                int inner = Math.Max(40, width - Pad * 2);
                int labelW = Math.Max(40, inner - IconSize - 12 - ChevronW);

                int labelH = TextH(_label, UiKit.T.SmallStrong, labelW, true);
                int topH = Math.Max(IconSize, labelH);

                bool split = TrySplit(_number, out string main, out string unit);
                int unitW = split
                    ? TextRenderer.MeasureText(unit, UnitFont, new Size(int.MaxValue, int.MaxValue), One).Width + UnitGap
                    : 0;

                Font numFont = NumFonts[^1];
                bool numWrap = true;
                foreach (var f in NumFonts)
                {
                    var w = TextRenderer.MeasureText(main, f, new Size(int.MaxValue, int.MaxValue), One).Width + unitW;
                    if (w <= inner - 4) { numFont = f; numWrap = false; break; }
                }

                if (numWrap) { main = _number; unit = ""; }

                int numH = TextH(main, numFont, inner, numWrap);
                int capH = TextH(_sub, UiKit.T.Small, inner, true);

                int total = Pad + topH + 14 + numH + 6 + capH + Pad;
                return (numFont, numWrap, main, unit, topH, numH, capH, total);
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
        //  SEGMENTED FILTER (int? tag, matches other screens)
        //  Counts stored in a parallel List<int?> — null keys are safe.
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class RepairSegmentedFilter : Control
        {
            private readonly List<(string Key, int? Tag)> _items = new();
            private readonly List<int?> _counts = new();
            private int _selected = 0;
            private int _hover = -1;

            public event EventHandler<int?>? SelectionChanged;

            public RepairSegmentedFilter((string Key, int? Tag)[] items)
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
                    return Math.Max(360, total);
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