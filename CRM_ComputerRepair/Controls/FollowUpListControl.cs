using CRM.winforms.Auth;
using CRM.winforms.Forms;
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
    /// Professional Follow-Up Operations Command Center for Staff.
    /// Provides immediate SLA urgency tracking (Due Today, Overdue, Upcoming),
    /// rich customer &amp; repair order linkages, quick completion with outcome logging,
    /// 1-click rescheduling, and direct communication shortcuts.
    /// Follows the unified CompaniesControl SaaS workbench design system.
    /// </summary>
    [DesignerCategory("Code")]
    public class FollowUpListControl : UserControl
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private List<FollowUpDto> _all = new();
        private List<CustomerDto> _cachedCustomers = new();
        private List<RepairRequestDto> _cachedRepairs = new();

        // Filter values: null = All, 10 = Due Today, 20 = Overdue, 0 = Scheduled, 1 = Completed, 2 = Cancelled
        private int? _statusFilter = null;

        // ═══════════ CONTROLS ═══════════

        private Label lblTitle = null!;
        private SaasButton btnAdd = null!;
        private SaasButton btnRefresh = null!;

        // ── KPI Tiles ──
        private KpiTile tileTotal = null!;
        private KpiTile tileDueToday = null!;
        private KpiTile tileOverdue = null!;
        private KpiTile tileScheduled = null!;
        private KpiTile tileCompleted = null!;
        private readonly List<KpiTile> _tiles = new();

        // ── Workbench Card ──
        private WorkbenchCard card = null!;
        private WorkbenchSearch search = null!;
        private FollowUpsSegmentedFilter segments = null!;
        private Label lblCount = null!;
        private DataGridView dgv = null!;
        private WorkbenchState state = null!;
        private TablePagination pager = null!;

        private const string ColActions = "colActions";
        private ContextMenuStrip _actionsMenu = null!;
        private int _menuRowIndex = -1;
        private int _hoverRow = -1;
        private readonly ToolTip _tips = new ToolTip { InitialDelay = 500, ReshowDelay = 200 };

        public event EventHandler<string>? ActionRequested;

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

        public FollowUpListControl()
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
                Text = "Follow-Ups",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            btnAdd = new SaasButton("Add Follow-Up", SaasButtonVariant.Primary, "\uE710");
            btnAdd.Size = new Size(160, 36);
            btnAdd.Click += (s, e) => OpenAddDialog();
            _tips.SetToolTip(btnAdd, "Schedule new follow-up (Ctrl+N)");

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Size = new Size(100, 36);
            btnRefresh.Click += async (s, e) => await ReloadAsync();
            _tips.SetToolTip(btnRefresh, "Refresh list (F5)");

            Controls.Add(lblTitle);
            Controls.Add(btnAdd);
            Controls.Add(btnRefresh);

            // ── KPI Tiles (5 Interactive KPI Cards) ──
            tileTotal = AddTile("TOTAL FOLLOW-UPS", "0", "All customer touchpoints", AppTheme.Primary, "\uE716");
            tileTotal.Click += (s, e) => SetFilter(null);

            tileDueToday = AddTile("DUE TODAY", "0", "Requires customer contact today", AppTheme.Warning, "\uE823");
            tileDueToday.Click += (s, e) => SetFilter(10);

            tileOverdue = AddTile("OVERDUE", "0", "Past due SLA target", AppTheme.Danger, "\uE711");
            tileOverdue.Click += (s, e) => SetFilter(20);

            tileScheduled = AddTile("SCHEDULED", "0", "Upcoming scheduled checkups", Color.FromArgb(99, 102, 241), "\uE787");
            tileScheduled.Click += (s, e) => SetFilter(0);

            tileCompleted = AddTile("COMPLETED", "0", "Successfully contacted & resolved", AppTheme.Success, "\uE73E");
            tileCompleted.Click += (s, e) => SetFilter(1);

            // ── Workbench Card ──
            card = new WorkbenchCard { BackColor = UiKit.T.Surface };

            search = new WorkbenchSearch
            {
                Placeholder = "Search by customer, repair ticket, device, phone, or notes..."
            };
            search.Inner.TextChanged += (s, e) => ApplySearch();
            _tips.SetToolTip(search, "Search (Ctrl+F, Esc to clear)");

            segments = new FollowUpsSegmentedFilter(new (string, int?, int?)[]
            {
                ("All", null, null),
                ("Due Today", 10, null),
                ("Overdue", 20, null),
                ("Scheduled", 0, null),
                ("Completed", 1, null),
                ("Cancelled", 2, null)
            });
            segments.SelectionChanged += (s, val) =>
            {
                _statusFilter = val;
                ApplySearch();
            };

            lblCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            dgv = new DataGridView();
            StyleGrid(dgv);
            dgv.CellPainting += Dgv_CellPainting;
            dgv.CellClick += Dgv_CellClick;
            dgv.CellDoubleClick += Dgv_CellDoubleClick;
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
            dgv.KeyDown += Dgv_KeyDown;

            state = new WorkbenchState { Visible = false };
            state.Show("\uE8BD", "No follow-ups logged yet",
                "Schedule your first customer service check or post-repair follow-up.");

            pager = new TablePagination();
            pager.PageChanged += (s, e) => ApplySearch(resetPage: false);

            card.Controls.Add(search);
            card.Controls.Add(segments);
            card.Controls.Add(lblCount);
            card.Controls.Add(dgv);
            card.Controls.Add(state);
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

        private void SetFilter(int? val)
        {
            _statusFilter = val;
            segments.SetValue(val);
            ApplySearch();
        }

        // ═══════════ KEYBOARD SHORTCUTS ═══════════

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

                case Keys.F5:
                    _ = ReloadAsync();
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
            if (e.KeyCode == Keys.Enter && dgv.CurrentRow.DataBoundItem is FollowUpDto dto)
            {
                OpenEditDialog(dto);
                e.Handled = true;
                return;
            }

            var r = dgv.GetRowDisplayRectangle(_menuRowIndex, true);
            _actionsMenu.Show(dgv, new Point(r.Right - 220, r.Bottom));
            e.Handled = true;
        }

        // ═══════════ LAYOUT (matches CompaniesControl) ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            int pad = UiKit.T.S6;
            int contentW = Math.Max(700, Width - pad * 2);

            // ── Header ──
            lblTitle.Location = new Point(pad, pad);

            int btnY = pad;
            btnRefresh.Location = new Point(pad + contentW - btnRefresh.Width, btnY);
            btnAdd.Location = new Point(btnRefresh.Left - btnAdd.Width - 10, btnY);

            int y = lblTitle.Bottom + 20;

            // ── KPI Tiles (5 across at wide, then 3 / 2 / 1) ──
            int tileCols = contentW >= 1280 ? 5 : contentW >= 1000 ? 3 : contentW >= 700 ? 2 : 1;
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

            int filterW = segments.PreferredWidth;
            int searchW = Math.Max(240, Math.Min(440, innerW - filterW - 20));

            search.SetBounds(cardPad, cardPad, searchW, 38);
            segments.SetBounds(cardPad + innerW - filterW, cardPad + 1, filterW, 36);

            int gridY = search.Bottom + 14;
            int footerH = pager.Visible ? TableKit.FooterH : 0;
            int gridH = cardH - gridY - cardPad - 26 - footerH;

            dgv.SetBounds(cardPad, gridY, innerW, Math.Max(120, gridH));
            state.SetBounds(cardPad, gridY, innerW, Math.Max(120, gridH));

            if (pager.Visible)
            {
                pager.SetBounds(cardPad, gridY + Math.Max(0, gridH), innerW, TableKit.FooterH);
            }

            lblCount.Location = new Point(cardPad, (pager.Visible ? pager.Bottom : dgv.Bottom) + 6);
        }

        // ═══════════ DATA LOAD (unchanged logic) ═══════════

        public async Task ReloadAsync()
        {
            state.Show("\uE895", "Loading follow-ups…", "Fetching latest customer follow-up tasks.");
            dgv.Visible = false;
            try
            {
                var folTask = _api.GetFollowUpsAsync(null, includeArchived: false);
                var custTask = _api.GetCustomersAsync();
                var repTask = _api.GetRepairRequestsAsync();

                await Task.WhenAll(folTask, custTask, repTask);

                _all = await folTask ?? new List<FollowUpDto>();
                _cachedCustomers = await custTask ?? new List<CustomerDto>();
                _cachedRepairs = await repTask ?? new List<RepairRequestDto>();

                // Resilient fallback resolution for existing records
                var custMap = _cachedCustomers.ToDictionary(c => c.CustomerId);
                var repMap = _cachedRepairs.ToDictionary(r => r.RepairRequestId);

                foreach (var f in _all)
                {
                    if (string.IsNullOrWhiteSpace(f.CustomerName) && f.CustomerId.HasValue && custMap.TryGetValue(f.CustomerId.Value, out var c))
                    {
                        f.CustomerName = c.FullName;
                        f.CustomerPhone = c.Phone;
                        f.CustomerEmail = c.Email;
                    }
                    if (string.IsNullOrWhiteSpace(f.RepairRequestNumber) && f.RepairRequestId.HasValue && repMap.TryGetValue(f.RepairRequestId.Value, out var r))
                    {
                        f.RepairRequestNumber = r.RequestNumber;
                        f.DeviceModel = r.DeviceModel;
                    }
                }

                UpdateStats();
                ApplySearch();
            }
            catch (Exception ex)
            {
                _all = new List<FollowUpDto>();
                UpdateStats();
                ApplySearch();

                MessageBox.Show(
                    $"Couldn't load follow-ups.\n\n{ex.Message}",
                    "Connection problem",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void UpdateStats()
        {
            int total = _all.Count;
            int dueToday = _all.Count(x => x.IsDueToday);
            int overdue = _all.Count(x => x.IsOverdue);
            int scheduled = _all.Count(x => x.Status == 0);
            int completed = _all.Count(x => x.Status == 1);
            int cancelled = _all.Count(x => x.Status == 2);

            tileTotal.Set("TOTAL FOLLOW-UPS", total.ToString(),
                $"{scheduled} scheduled · {completed} completed", AppTheme.Primary, "\uE716");
            tileDueToday.Set("DUE TODAY", dueToday.ToString(),
                dueToday == 0 ? "All callbacks on schedule" : "Requires staff contact today", AppTheme.Warning, "\uE823");
            tileOverdue.Set("OVERDUE", overdue.ToString(),
                overdue == 0 ? "No overdue SLA targets" : "Past due SLA target", AppTheme.Danger, "\uE711");
            tileScheduled.Set("SCHEDULED", scheduled.ToString(),
                scheduled == 0 ? "No upcoming checkups" : "Upcoming scheduled checkups", Color.FromArgb(99, 102, 241), "\uE787");
            tileCompleted.Set("COMPLETED", completed.ToString(),
                completed == 0 ? "Nothing completed yet" : "Successfully resolved", AppTheme.Success, "\uE73E");

            segments.UpdateCounts(new Dictionary<string, int?>
            {
                ["All"] = total,
                ["Due Today"] = dueToday,
                ["Overdue"] = overdue,
                ["Scheduled"] = scheduled,
                ["Completed"] = completed,
                ["Cancelled"] = cancelled
            });
        }

        private void ApplySearch(bool resetPage = true)
        {
            if (resetPage) pager.Reset();
            var term = search.Inner.Text?.Trim() ?? string.Empty;

            IEnumerable<FollowUpDto> q = _all;

            // Apply Status / SLA Filter
            if (_statusFilter.HasValue)
            {
                q = _statusFilter.Value switch
                {
                    10 => q.Where(x => x.IsDueToday),
                    20 => q.Where(x => x.IsOverdue),
                    0 => q.Where(x => x.Status == 0),
                    1 => q.Where(x => x.Status == 1),
                    2 => q.Where(x => x.Status == 2),
                    _ => q
                };
            }

            // Apply Text Search
            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(x =>
                    (x.CustomerDisplay ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.CustomerPhone ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.CustomerEmail ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.RepairDisplay ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Subject ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Notes ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.AssignedToUserId ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            // Order by urgency: Overdue first, then Due Today, then scheduled chronological
            var view = q
                .OrderByDescending(x => x.IsOverdue)
                .ThenByDescending(x => x.IsDueToday)
                .ThenBy(x => x.ScheduledAt)
                .ToList();

            pager.SetTotal(view.Count);
            var page = pager.Slice(view);

            dgv.DataSource = null;
            dgv.DataSource = page;
            ConfigureColumns();
            AddActionsColumn();

            bool hasData = view.Count > 0;
            pager.Visible = hasData && view.Count > TableKit.PageSize;
            LayoutUi();

            lblCount.Text = $"Showing {view.Count} of {_all.Count} follow-ups";

            if (hasData)
            {
                state.Clear();
                dgv.Visible = true;
                dgv.ClearSelection();
            }
            else
            {
                dgv.Visible = false;

                if (!string.IsNullOrEmpty(term))
                    state.Show("\uE721", "No matches found",
                        $"Nothing matches \u201c{term}\u201d. Press Esc to clear the search.");
                else if (_statusFilter == 10)
                    state.Show("\uE73E", "No follow-ups due today",
                        "Great job! All scheduled customer callbacks for today are up to date.");
                else if (_statusFilter == 20)
                    state.Show("\uE73E", "Zero overdue follow-ups",
                        "No pending callbacks have passed their scheduled date.");
                else if (_statusFilter == 0)
                    state.Show("\uE71C", "No scheduled follow-ups",
                        "Use \u201cAdd Follow-Up\u201d to schedule proactive checkups with customers.");
                else
                    state.Show("\uE8BD", "No follow-ups yet",
                        "Schedule your first customer service check or post-repair follow-up.");
            }
        }

        // ═══════════ GRID STYLE & COLUMNS ═══════════

        private static void StyleGrid(DataGridView g)
        {
            typeof(DataGridView)
                .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(g, true);

            g.AutoGenerateColumns = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.RowHeadersVisible = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.BackgroundColor = UiKit.T.Surface;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.None;
            g.GridColor = UiKit.T.LineSoft;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ScrollBars = ScrollBars.Both;
            g.RowTemplate.Height = 64;
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
        }

        private void ConfigureColumns()
        {
            foreach (var hidden in new[]
            {
                "FollowUpId", "CustomerId", "RepairRequestId",
                "Channel", "Status", "CompletedAt", "UpdatedAt",
                "IsActive", "ActivityStatus", "CustomerName",
                "CustomerPhone", "CustomerEmail", "RepairRequestNumber",
                "DeviceModel", "Notes", "CompletedAtDisplay", "ContactDisplay",
                "IsOverdue", "IsDueToday"
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
                else c.MinimumWidth = 240;
                c.DisplayIndex = displayIndex;
            }

            Setup("UrgencyText", "URGENCY", 115, 0);
            Setup("CustomerDisplay", "CUSTOMER & CONTACT", 220, 1);
            Setup("RepairDisplay", "LINKED REPAIR", 200, 2);
            Setup("Subject", "SUBJECT & NOTES", 0, 3, fill: true);
            Setup("ChannelText", "CHANNEL", 105, 4);
            Setup("ScheduledAt", "SCHEDULED", 140, 5);
            Setup("AssignedToUserId", "ASSIGNED", 110, 6);
            Setup("StatusText", "STATUS", 125, 7);

            var cSched = dgv.Columns["ScheduledAt"];
            if (cSched != null)
            {
                cSched.DefaultCellStyle.Format = "MMM d  HH:mm";
                cSched.DefaultCellStyle.ForeColor = UiKit.T.InkMuted;
                cSched.DefaultCellStyle.SelectionForeColor = UiKit.T.InkMuted;
            }

            var cAssigned = dgv.Columns["AssignedToUserId"];
            if (cAssigned != null)
            {
                cAssigned.DefaultCellStyle.ForeColor = UiKit.T.InkMuted;
                cAssigned.DefaultCellStyle.SelectionForeColor = UiKit.T.InkMuted;
            }
        }

        private void AddActionsColumn()
        {
            if (dgv.Columns.Contains(ColActions)) return;

            var btnCol = new DataGridViewButtonColumn
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
            btnCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            btnCol.DefaultCellStyle.BackColor = UiKit.T.Surface;
            btnCol.DefaultCellStyle.SelectionBackColor = UiKit.T.RowHover;
            btnCol.DefaultCellStyle.Padding = new Padding(0);
            dgv.Columns.Add(btnCol);
        }

        // ═══════════ CELL PAINTING (CompaniesControl-style) ═══════════

        private void Dgv_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;
            var gr = e.Graphics;
            var cell = e.CellBounds;

            // Header row painting
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

            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string col = dgv.Columns[e.ColumnIndex].Name;
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool hovered = e.RowIndex == _hoverRow;
            bool focused = dgv.Focused;

            Color rowBg = UiKit.T.Surface;
            if (selected && focused) rowBg = UiKit.T.RowHover;
            else if (selected && !focused) rowBg = UiKit.T.LineSoft;
            else if (hovered) rowBg = UiKit.T.RowHover;

            using (var b = new SolidBrush(rowBg))
                gr.FillRectangle(b, cell);
            using (var p = new Pen(UiKit.T.LineSoft))
                gr.DrawLine(p, cell.Left, cell.Bottom - 1, cell.Right, cell.Bottom - 1);

            UiKit.Quality(gr);

            // Left vertical accent bar on col 0 for selected focused row
            if (e.ColumnIndex == 0 && selected && focused)
                UiKit.FillRounded(gr, new Rectangle(cell.Left, cell.Top + 12, 3, cell.Height - 25), 1, AppTheme.Primary);

            var r = new Rectangle(cell.Left + CellPadX, cell.Top,
                Math.Max(0, cell.Width - CellPadX * 2), cell.Height - 1);
            int cy = r.Top + r.Height / 2;
            string text = Convert.ToString(e.FormattedValue) ?? string.Empty;
            var rowItem = dgv.Rows[e.RowIndex].DataBoundItem as FollowUpDto;

            switch (col)
            {
                case ColActions:
                    {
                        int size = 30;
                        var btn = new Rectangle(r.Right - size, cy - size / 2, size, size);
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
                        break;
                    }

                case "UrgencyText":
                    {
                        Color accent = text switch
                        {
                            "Overdue" => AppTheme.Danger,
                            "Due Today" => AppTheme.Warning,
                            "Completed" => AppTheme.Success,
                            "Cancelled" => UiKit.T.InkFaint,
                            _ => AppTheme.Primary
                        };
                        PaintDotPill(gr, r, cy, text, accent, UiKit.T.SmallStrong);
                        break;
                    }

                case "CustomerDisplay":
                    if (rowItem != null)
                        PaintCustomerCell(gr, rowItem.CustomerDisplay, rowItem.ContactDisplay, r);
                    break;

                case "RepairDisplay":
                    if (rowItem != null)
                    {
                        string line1 = !string.IsNullOrWhiteSpace(rowItem.RepairRequestNumber) ? rowItem.RepairRequestNumber : "General Support";
                        string line2 = !string.IsNullOrWhiteSpace(rowItem.DeviceModel) ? rowItem.DeviceModel : "No device linked";
                        PaintTwoLine(gr, r, line1, line2);
                    }
                    break;

                case "Subject":
                    if (rowItem != null)
                    {
                        string snippet = !string.IsNullOrWhiteSpace(rowItem.Notes)
                            ? rowItem.Notes.Replace("\r", " ").Replace("\n", " ")
                            : "No notes provided";
                        PaintTwoLine(gr, r, rowItem.Subject, snippet);
                    }
                    break;

                case "ChannelText":
                    {
                        Color accent = text switch
                        {
                            "Call" => AppTheme.Primary,
                            "Email" => AppTheme.Success,
                            "SMS" => AppTheme.Warning,
                            "Visit" => Color.FromArgb(139, 92, 246),
                            _ => UiKit.T.InkMuted
                        };
                        PaintDotPill(gr, r, cy, text, accent, UiKit.T.SmallStrong);
                        break;
                    }

                case "ScheduledAt":
                    UiKit.Text(gr, text, UiKit.T.Small, UiKit.T.InkMuted, r, CellText);
                    break;

                case "AssignedToUserId":
                    UiKit.Text(gr, text, UiKit.T.Small, UiKit.T.InkMuted, r, CellText);
                    break;

                case "StatusText":
                    {
                        Color accent = text switch
                        {
                            "Scheduled" => AppTheme.Primary,
                            "Completed" => AppTheme.Success,
                            "Cancelled" => UiKit.T.InkMuted,
                            _ => UiKit.T.InkMuted
                        };
                        PaintDotPill(gr, r, cy, text, accent, UiKit.T.SmallStrong);
                        break;
                    }

                default:
                    e.PaintContent(cell);
                    break;
            }

            e.Handled = true;
        }

        private static void PaintCustomerCell(Graphics gr, string? name, string? contact, Rectangle rect)
        {
            const int av = 36;
            string cname = string.IsNullOrWhiteSpace(name) ? "Customer" : name;

            var avRect = new Rectangle(rect.Left, rect.Top + (rect.Height - av) / 2, av, av);
            Color ac = AvatarColor(cname);
            UiKit.FillRounded(gr, avRect, 10, UiKit.Wash(ac));

            string initial = string.IsNullOrWhiteSpace(cname) ? "?" : cname.Trim().Substring(0, 1).ToUpperInvariant();
            UiKit.Text(gr, initial, UiKit.T.BodyStrong, ac, avRect, UiKit.Center);

            int tx = avRect.Right + 12;
            int tw = Math.Max(0, rect.Right - tx);
            string sub = contact ?? "";

            if (string.IsNullOrWhiteSpace(sub))
            {
                UiKit.Text(gr, cname, UiKit.T.BodyStrong, UiKit.T.Ink,
                    new Rectangle(tx, rect.Top, tw, rect.Height), CellText);
                return;
            }

            int h1 = UiKit.T.BodyStrong.Height + 2;
            int h2 = UiKit.T.Small.Height + 2;
            int y0 = rect.Top + (rect.Height - (h1 + h2)) / 2;

            UiKit.Text(gr, cname, UiKit.T.BodyStrong, UiKit.T.Ink, new Rectangle(tx, y0, tw, h1), CellText);
            UiKit.Text(gr, sub, UiKit.T.Small, UiKit.T.InkMuted, new Rectangle(tx, y0 + h1, tw, h2), CellText);
        }

        private static void PaintTwoLine(Graphics gr, Rectangle rect, string line1, string line2)
        {
            bool hasSecond = !string.IsNullOrWhiteSpace(line2);
            if (!hasSecond)
            {
                UiKit.Text(gr, line1 ?? "", UiKit.T.BodyStrong, UiKit.T.Ink, rect, CellText);
                return;
            }

            int h1 = UiKit.T.BodyStrong.Height + 2;
            int h2 = UiKit.T.Small.Height + 2;
            int y0 = rect.Top + (rect.Height - (h1 + h2)) / 2;

            UiKit.Text(gr, line1, UiKit.T.BodyStrong, UiKit.T.Ink, new Rectangle(rect.Left, y0, rect.Width, h1), CellText);
            UiKit.Text(gr, line2, UiKit.T.Small, UiKit.T.InkMuted, new Rectangle(rect.Left, y0 + h1, rect.Width, h2), CellText);
        }

        private static void PaintDotPill(Graphics gr, Rectangle rect, int cy, string text, Color fg, Font font)
        {
            if (string.IsNullOrEmpty(text)) return;

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

        // ═══════════ ACTIONS MENU (logic unchanged) ═══════════

        private void BuildActionsMenu()
        {
            _actionsMenu = new ContextMenuStrip { ShowImageMargin = false, Font = UiKit.T.Body };
            _actionsMenu.Renderer = new QuietMenuRenderer();

            // 0: Complete
            var miComplete = _actionsMenu.Items.Add("Mark completed / Log outcome");
            miComplete.Click += OnMenuQuickComplete;

            // 1: Reschedule Submenu
            var miResched = new ToolStripMenuItem("Reschedule");
            var resched1d = miResched.DropDownItems.Add("+1 Day (Tomorrow)");
            var resched3d = miResched.DropDownItems.Add("+3 Days");
            var resched1w = miResched.DropDownItems.Add("+1 Week");

            resched1d.Click += async (s, e) => await QuickRescheduleAsync(1);
            resched3d.Click += async (s, e) => await QuickRescheduleAsync(3);
            resched1w.Click += async (s, e) => await QuickRescheduleAsync(7);

            _actionsMenu.Items.Add(miResched);

            _actionsMenu.Items.Add(new ToolStripSeparator());

            // 3: Call Customer
            var miCall = _actionsMenu.Items.Add("Call Customer");
            miCall.Click += OnMenuCall;

            // 4: Email Customer
            var miEmail = _actionsMenu.Items.Add("Email Customer");
            miEmail.Click += OnMenuEmail;

            _actionsMenu.Items.Add(new ToolStripSeparator());

            // 6: Customer History
            var miHistory = _actionsMenu.Items.Add("View Customer History");
            miHistory.Click += OnMenuCustomerHistory;

            // 7: Linked Repair
            var miRepair = _actionsMenu.Items.Add("View Linked Repair Ticket");
            miRepair.Click += OnMenuRepairDetails;

            // 8: Edit Full
            var miEdit = _actionsMenu.Items.Add("Edit Follow-Up");
            miEdit.Click += (s, e) =>
            {
                if (_menuRowIndex >= 0 && dgv.Rows[_menuRowIndex].DataBoundItem is FollowUpDto dto)
                    OpenEditDialog(dto);
            };

            _actionsMenu.Items.Add(new ToolStripSeparator());

            // 10: Archive / Restore
            var miArchive = _actionsMenu.Items.Add("Archive");
            miArchive.Click += OnMenuArchive;

            _actionsMenu.Opening += (s, e) =>
            {
                if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
                if (dgv.Rows[_menuRowIndex].DataBoundItem is not FollowUpDto dto) return;

                miComplete.Enabled = dto.Status != 1 && dto.IsActive;
                miResched.Enabled = dto.IsActive;

                miCall.Enabled = !string.IsNullOrWhiteSpace(dto.CustomerPhone);
                miCall.Text = !string.IsNullOrWhiteSpace(dto.CustomerPhone) ? $"Call Customer: {dto.CustomerPhone}" : "Call Customer";

                miEmail.Enabled = !string.IsNullOrWhiteSpace(dto.CustomerEmail);
                miEmail.Text = !string.IsNullOrWhiteSpace(dto.CustomerEmail) ? $"Email Customer: {dto.CustomerEmail}" : "Email Customer";

                miHistory.Enabled = dto.CustomerId.HasValue;
                miRepair.Enabled = dto.RepairRequestId.HasValue;

                miArchive.Text = dto.IsActive ? "Archive" : "Restore";
                miArchive.ForeColor = dto.IsActive ? AppTheme.Danger : UiKit.T.Ink;
            };

            _actionsMenu.Closed += (s, e) => { dgv.Invalidate(); };
        }

        // ═══════════ MENU HANDLERS (logic unchanged) ═══════════

        private void OnMenuQuickComplete(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not FollowUpDto dto) return;

            using var dlg = new QuickCompleteFollowUpDialog(dto);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                _ = ReloadAsync();
            }
        }

        private async Task QuickRescheduleAsync(int days)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not FollowUpDto dto) return;

            try
            {
                var newDate = DateTime.Now.Date.AddDays(days).AddHours(14); // 2 PM
                await _api.RescheduleFollowUpAsync(dto.FollowUpId, newDate, $"Rescheduled by staff +{days} day(s)");
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to reschedule:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnMenuCall(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not FollowUpDto dto) return;

            if (!string.IsNullOrWhiteSpace(dto.CustomerPhone))
            {
                Clipboard.SetText(dto.CustomerPhone);
                MessageBox.Show(
                    $"Customer Phone: {dto.CustomerPhone}\n\n(Copied to clipboard for calling)",
                    "Customer Contact",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void OnMenuEmail(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not FollowUpDto dto) return;

            if (!string.IsNullOrWhiteSpace(dto.CustomerEmail))
            {
                try
                {
                    string subject = Uri.EscapeDataString(dto.Subject);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"mailto:{dto.CustomerEmail}?subject={subject}",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    Clipboard.SetText(dto.CustomerEmail);
                    MessageBox.Show($"Copied {dto.CustomerEmail} to clipboard.", "Email Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void OnMenuCustomerHistory(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not FollowUpDto dto) return;

            if (dto.CustomerId.HasValue)
            {
                ActionRequested?.Invoke(this, $"customer-history:{dto.CustomerId.Value}");
            }
        }

        private void OnMenuRepairDetails(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not FollowUpDto dto) return;

            if (dto.RepairRequestId.HasValue)
            {
                var repair = _cachedRepairs.FirstOrDefault(r => r.RepairRequestId == dto.RepairRequestId.Value);
                if (repair != null)
                {
                    using var dlg = new RepairRequestFormDialog(repair);
                    dlg.ShowModal(this.FindForm());
                }
                else
                {
                    ActionRequested?.Invoke(this, "repairs");
                }
            }
        }

        private async void OnMenuArchive(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not FollowUpDto dto) return;

            if (dto.IsActive)
                await ArchiveAsync(dto);
            else
                await RestoreAsync(dto);
        }

        private async Task ArchiveAsync(FollowUpDto dto)
        {
            var confirm = MessageBox.Show(
                $"Archive \u201c{dto.Subject}\u201d?\n\nIt leaves the active view but remains safely stored in the database.",
                "Archive follow-up",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                await _api.ArchiveFollowUpAsync(dto.FollowUpId);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to archive:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task RestoreAsync(FollowUpDto dto)
        {
            try
            {
                await _api.RestoreFollowUpAsync(dto.FollowUpId);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restore:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═══════════ MODAL LAUNCHERS (logic unchanged) ═══════════

        private void OpenAddDialog()
        {
            using var dlg = new FollowUpFormDialog(null, _cachedCustomers, _cachedRepairs);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private void OpenEditDialog(FollowUpDto dto)
        {
            using var dlg = new FollowUpFormDialog(dto, _cachedCustomers, _cachedRepairs);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
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
            if (e.RowIndex < 0 || e.RowIndex >= dgv.RowCount) return;
            if (dgv.Rows[e.RowIndex].DataBoundItem is not FollowUpDto dto) return;
            OpenEditDialog(dto);
        }

        // ═══════════════════════════════════════════════════════════════
        //  EMBEDDED KPI TILE (matches CompaniesControl)
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
                if (m.Unit.Length > 0)
                {
                    int mw = TextRenderer.MeasureText(m.Main, m.NumFont, new Size(int.MaxValue, int.MaxValue), One).Width;
                    UiKit.Text(g, m.Main, m.NumFont, UiKit.T.Ink,
                        new Rectangle(Pad, numTop, mw + 4, m.NumH), One | TextFormatFlags.Top);

                    int uy = numTop + m.NumFont.Height - UnitFont.Height
                             - (int)Math.Round(Descent(m.NumFont) - Descent(UnitFont));
                    UiKit.Text(g, m.Unit, UnitFont, UiKit.T.InkMuted,
                        new Rectangle(Pad + mw + UnitGap, uy, Math.Max(10, inner - mw - UnitGap), UnitFont.Height + 2),
                        One | TextFormatFlags.Top);
                }
                else
                {
                    UiKit.Text(g, m.Main, m.NumFont, UiKit.T.Ink,
                        new Rectangle(Pad, numTop, inner, m.NumH),
                        (m.NumWrap ? Wrapped : One) | TextFormatFlags.Top);
                }

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
        //  SEGMENTED FILTER (matches CompaniesControl)
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class FollowUpsSegmentedFilter : Control
        {
            private readonly List<(string Key, int? Value, int? Count)> _items = new();
            private int _selected = 0;
            private int _hover = -1;

            public event EventHandler<int?>? SelectionChanged;

            public FollowUpsSegmentedFilter((string Key, int? Value, int? Count)[] items)
            {
                _items.AddRange(items);
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
                        string text = item.Count.HasValue ? $"{item.Key} ({item.Count})" : item.Key;
                        var sz = TextRenderer.MeasureText(text, Font);
                        total += Math.Max(84, sz.Width + 24);
                    }
                    return Math.Max(420, total);
                }
            }

            public void UpdateCounts(Dictionary<string, int?> counts)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (counts.TryGetValue(_items[i].Key, out var c))
                        _items[i] = (_items[i].Key, _items[i].Value, c);
                }
                Invalidate();
            }

            public void SetValue(int? val)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].Value == val)
                    {
                        if (_selected != i)
                        {
                            _selected = i;
                            Invalidate();
                            SelectionChanged?.Invoke(this, _items[_selected].Value);
                        }
                        return;
                    }
                }
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

                    string text = _items[i].Count.HasValue
                        ? $"{_items[i].Key} ({_items[i].Count.GetValueOrDefault()})"
                        : _items[i].Key;

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
                    SelectionChanged?.Invoke(this, _items[_selected].Value);
                }
                base.OnMouseClick(e);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  QUIET CONTEXT MENU RENDERER
        // ═══════════════════════════════════════════════════════════════

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