using CRM.winforms;
using CRM.winforms.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Professional Customer Interactions Command Center for Staff.
    /// Manages customer questions, service concerns, and reviews with complete
    /// customer &amp; repair order linkages, SLA urgency tracking, rapid resolution logging,
    /// seamless follow-up scheduling, and direct customer contact shortcuts.
    /// </summary>
    [DesignerCategory("Code")]
    public class InteractionListControl : UserControl
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private List<InteractionDto> _all = new();
        private List<CustomerDto> _cachedCustomers = new();
        private List<RepairRequestDto> _cachedRepairs = new();

        private InteractionTypeFilter? _currentFilter = null; // null = All, 0 = Questions, 1 = Concerns, 2 = Reviews
        private int? _statusFilter = null;                    // null = All, 0 = Open, 1 = In Progress, 99 = High Priority, 2 = Closed

        // ═══════════ CONTROLS ═══════════

        private Label lblTitle = null!;
        private SaasButton btnAdd = null!;
        private SaasButton btnRefresh = null!;

        // ── KPI Tiles ──
        private KpiTile tileTotal = null!;
        private KpiTile tileOpen = null!;
        private KpiTile tileHigh = null!;
        private KpiTile tileClosed = null!;
        private readonly List<KpiTile> _tiles = new();

        // ── Workbench Card ──
        private WorkbenchCard card = null!;
        private Label lblCardTitle = null!;
        private WorkbenchSearch search = null!;
        private InteractionsSegmentedFilter segments = null!;
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

        public InteractionListControl()
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
                Text = "Customer Interactions",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            btnAdd = new SaasButton("Log interaction", SaasButtonVariant.Primary, "\uE710");
            btnAdd.Size = new Size(170, 36);
            btnAdd.Click += (s, e) => OpenAddDialog();
            _tips.SetToolTip(btnAdd, "Log new customer interaction (Ctrl+N)");

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Size = new Size(100, 36);
            btnRefresh.Click += async (s, e) => await ReloadAsync();
            _tips.SetToolTip(btnRefresh, "Refresh list (F5)");

            Controls.Add(lblTitle);
            Controls.Add(btnAdd);
            Controls.Add(btnRefresh);

            // ── KPI Tiles ──
            tileTotal = AddTile("TOTAL INTERACTIONS", "0", "All logged customer touchpoints", AppTheme.Primary, "\uE716");
            tileTotal.Click += (s, e) => { _statusFilter = null; ApplySearch(); };

            tileOpen = AddTile("OPEN", "0", "Awaiting staff response", AppTheme.Warning, "\uE7BA");
            tileOpen.Click += (s, e) => { _statusFilter = 0; ApplySearch(); };

            tileHigh = AddTile("HIGH PRIORITY", "0", "Urgent, unresolved concerns", AppTheme.Danger, "\uE7BA");
            tileHigh.Click += (s, e) => { _statusFilter = 99; ApplySearch(); };

            tileClosed = AddTile("CLOSED", "0", "Resolved & archived", AppTheme.Success, "\uE73E");
            tileClosed.Click += (s, e) => { _statusFilter = 2; ApplySearch(); };

            // ── Workbench Card ──
            card = new WorkbenchCard { BackColor = UiKit.T.Surface };

            lblCardTitle = new Label
            {
                Text = "Interaction Records",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            lblCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            search = new WorkbenchSearch
            {
                Placeholder = "Search by customer, ticket, device, subject, notes..."
            };
            search.Inner.TextChanged += (s, e) => ApplySearch();
            search.QueryChanged += (s, e) => ApplySearch();
            _tips.SetToolTip(search, "Search (Ctrl+F, Esc to clear)");

            segments = new InteractionsSegmentedFilter(new (string, int?)[]
            {
                ("All", null),
                ("Questions", null),
                ("Concerns", null),
                ("Reviews", null)
            });
            segments.SelectionChanged += (s, key) =>
            {
                _currentFilter = key switch
                {
                    "Questions" => InteractionTypeFilter.Inquiry,
                    "Concerns" => InteractionTypeFilter.Complaint,
                    "Reviews" => InteractionTypeFilter.Feedback,
                    _ => (InteractionTypeFilter?)null
                };

                btnAdd.Text = _currentFilter switch
                {
                    InteractionTypeFilter.Inquiry => "Log question",
                    InteractionTypeFilter.Complaint => "Log concern",
                    InteractionTypeFilter.Feedback => "Log review",
                    _ => "Log interaction"
                };
                LayoutUi();
                _ = ReloadAsync();
            };

            dgv = new DataGridView();
            StyleGrid(dgv);
            ConfigureColumns();
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
            state.Show("\uE8BD", "No interactions logged yet",
                "Use \u201cLog interaction\u201d to record the first customer inquiry or concern.");

            pager = new TablePagination();
            pager.PageChanged += (s, e) => ApplySearch(resetPage: false);

            card.Controls.Add(lblCardTitle);
            card.Controls.Add(lblCount);
            card.Controls.Add(segments);
            card.Controls.Add(search);
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
            var r = dgv.GetRowDisplayRectangle(_menuRowIndex, true);
            _actionsMenu.Show(dgv, new Point(r.Right - 180, r.Bottom));
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

            // ── KPI Tiles ──
            int tileCols = contentW >= 1100 ? 4 : (contentW >= 760 ? 2 : 1);
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
            int cardPad = UiKit.T.S5;
            int cardH = Math.Max(320, Height - y - pad);
            card.SetBounds(pad, y, contentW, cardH);

            int innerW = contentW - cardPad * 2;

            lblCardTitle.Location = new Point(cardPad, cardPad);
            lblCount.Location = new Point(lblCardTitle.Right + 8, lblCardTitle.Top + 4);

            int toolbarY = lblCardTitle.Bottom + 12;
            int filterW = segments.PreferredWidth;
            int searchW = Math.Max(220, Math.Min(380, innerW - filterW - 16));

            segments.SetBounds(cardPad, toolbarY, filterW, 36);
            search.SetBounds(cardPad + innerW - searchW, toolbarY, searchW, 36);

            int gridY = toolbarY + 44;
            int footerH = pager.Visible ? TableKit.FooterH : 0;
            int gridH = cardH - gridY - cardPad - footerH;

            if (gridH > 50 && innerW > 100)
            {
                dgv.SetBounds(cardPad, gridY, innerW, gridH);
                state.SetBounds(cardPad, gridY, innerW, gridH);
            }

            if (pager.Visible)
            {
                pager.SetBounds(cardPad, gridY + gridH, innerW, TableKit.FooterH);
            }
        }

        // ═══════════ DATA LOAD (logic unchanged) ═══════════

        public async Task ReloadAsync()
        {
            state.Show("\uE895", "Loading interactions…", "Fetching latest customer touchpoints.");
            dgv.Visible = false;
            try
            {
                var intTask = _api.GetInteractionsAsync(_currentFilter, includeArchived: false);
                var custTask = _cachedCustomers.Count == 0 ? _api.GetCustomersAsync() : Task.FromResult(_cachedCustomers);
                var repTask = _cachedRepairs.Count == 0 ? _api.GetRepairRequestsAsync() : Task.FromResult(_cachedRepairs);

                await Task.WhenAll(intTask, custTask, repTask);

                _all = await intTask ?? new List<InteractionDto>();
                _cachedCustomers = await custTask ?? new List<CustomerDto>();
                _cachedRepairs = await repTask ?? new List<RepairRequestDto>();

                UpdateStats();
                ApplySearch();
            }
            catch (Exception ex)
            {
                _all = new List<InteractionDto>();
                UpdateStats();
                ApplySearch();

                MessageBox.Show(
                    $"Couldn't load customer interactions.\n\n{ex.Message}",
                    "Connection problem",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void UpdateStats()
        {
            int total = _all.Count;
            int open = _all.Count(x => x.Status == 0);
            int inProg = _all.Count(x => x.Status == 1);
            int high = _all.Count(x => x.Priority == 2 && x.Status != 2);
            int closed = _all.Count(x => x.Status == 2);

            tileTotal.Set("TOTAL INTERACTIONS", total.ToString(),
                $"{open} open · {inProg} in progress", AppTheme.Primary, "\uE716");
            tileOpen.Set("OPEN", open.ToString(),
                open == 0 ? "No pending items" : "Awaiting staff response", AppTheme.Warning, "\uE7BA");
            tileHigh.Set("HIGH PRIORITY", high.ToString(),
                high == 0 ? "No urgent concerns" : "Urgent, unresolved concerns", AppTheme.Danger, "\uE7BA");
            tileClosed.Set("CLOSED", closed.ToString(),
                closed == 0 ? "Nothing resolved yet" : "Resolved & archived", AppTheme.Success, "\uE73E");

            // The DTO's InteractionType is an int — compare against the enum cast to int.
            segments.UpdateCounts(new Dictionary<string, int?>
            {
                ["All"] = total,
                ["Questions"] = _all.Count(x => x.InteractionType == (int)InteractionTypeFilter.Inquiry),
                ["Concerns"] = _all.Count(x => x.InteractionType == (int)InteractionTypeFilter.Complaint),
                ["Reviews"] = _all.Count(x => x.InteractionType == (int)InteractionTypeFilter.Feedback)
            });
        }

        private void ApplySearch(bool resetPage = true)
        {
            if (resetPage) pager.Reset();
            var term = search.Inner.Text?.Trim() ?? string.Empty;

            IEnumerable<InteractionDto> q = _all;

            if (_statusFilter.HasValue)
            {
                if (_statusFilter.Value == 99)
                    q = q.Where(x => x.Priority == 2 && x.Status != 2);
                else
                    q = q.Where(x => x.Status == _statusFilter.Value);
            }

            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(x =>
                    (x.CustomerName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.CustomerPhone ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.CustomerEmail ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.RepairRequestNumber ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.DeviceModel ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Subject ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Notes ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Resolution ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            var view = q.OrderByDescending(x => x.InteractionDate).ToList();

            pager.SetTotal(view.Count);
            var page = pager.Slice(view);

            dgv.DataSource = null;
            dgv.DataSource = page;

            bool hasData = view.Count > 0;
            dgv.Visible = hasData;
            state.Visible = !hasData;
            pager.Visible = hasData && view.Count > TableKit.PageSize;
            LayoutUi();

            lblCount.Text = $"Showing {view.Count} of {_all.Count} interactions";

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
                    state.Show("\uE721", "No matches",
                               $"Nothing matches \u201c{term}\u201d. Try a shorter keyword or press Esc to clear.");
                else if (_statusFilter == 99)
                    state.Show("\uE73E", "Zero urgent concerns",
                               "Great work! There are currently no high-priority open concerns.");
                else if (_statusFilter.HasValue)
                    state.Show("\uE71C", "Nothing here yet",
                               "No interactions with this status filter. Select Total to view all.");
                else
                    state.Show("\uE8BD", "No interactions logged yet",
                               $"Use \u201c{btnAdd.Text}\u201d to record the first customer inquiry or concern.");
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
            g.AllowUserToOrderColumns = false;
            g.RowHeadersVisible = false;
            g.ReadOnly = true;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.BackgroundColor = UiKit.T.Surface;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.None;
            g.GridColor = UiKit.T.LineSoft;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ScrollBars = ScrollBars.Both;
            g.RowTemplate.Height = 56;
            g.ColumnHeadersHeight = 44;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

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
            g.RowsDefaultCellStyle.BackColor = UiKit.T.Surface;
        }

        private void ConfigureColumns()
        {
            dgv.AutoGenerateColumns = false;
            dgv.Columns.Clear();

            void Col(string prop, string name, string header, int width, bool fill = false,
                DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft)
            {
                var c = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = prop,
                    Name = name,
                    HeaderText = header,
                    Width = width,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                };
                if (fill)
                {
                    c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    c.MinimumWidth = Math.Max(180, width);
                }
                c.DefaultCellStyle.Alignment = align;
                dgv.Columns.Add(c);
            }

            Col("TypeText", "TypeText", "TYPE", 115, align: DataGridViewContentAlignment.MiddleCenter);
            Col("CustomerDisplay", "CustomerDisplay", "CUSTOMER & CONTACT", 220);
            Col("RepairDisplay", "RepairDisplay", "LINKED REPAIR", 190);
            Col("Subject", "Subject", "SUBJECT & SUMMARY", 220, fill: true);
            Col("PriorityText", "PriorityText", "PRIORITY", 105, align: DataGridViewContentAlignment.MiddleCenter);
            Col("StatusText", "StatusText", "STATUS", 115, align: DataGridViewContentAlignment.MiddleCenter);
            Col("InteractionDate", "InteractionDate", "CREATED", 130);

            var btnCol = new DataGridViewButtonColumn
            {
                Name = ColActions,
                HeaderText = "ACTION",
                Text = "Manage",
                UseColumnTextForButtonValue = true,
                Width = 90,
                MinimumWidth = 80,
                FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            btnCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            btnCol.DefaultCellStyle.BackColor = UiKit.T.Surface;
            btnCol.DefaultCellStyle.SelectionBackColor = UiKit.T.RowHover;
            btnCol.DefaultCellStyle.Font = UiKit.T.SmallStrong;
            btnCol.DefaultCellStyle.ForeColor = AppTheme.Primary;
            dgv.Columns.Add(btnCol);
        }

        // ═══════════ CELL PAINTING (Retention table UI style) ═══════════

        private void Dgv_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;
            var gr = e.Graphics;
            var cell = e.CellBounds;

            // 1. Header row
            if (e.RowIndex == -1)
            {
                PaintHeaderCell(e);
                return;
            }

            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            // 2. Row background & soft bottom divider
            PaintRowBackground(e);

            string col = dgv.Columns[e.ColumnIndex].Name;
            var inner = new Rectangle(cell.Left + CellPadX, cell.Top, Math.Max(0, cell.Width - CellPadX * 2), cell.Height);
            string text = Convert.ToString(e.FormattedValue) ?? string.Empty;
            var rowItem = dgv.Rows[e.RowIndex].DataBoundItem as InteractionDto;

            switch (col)
            {
                case ColActions:
                    PaintActionButton(gr, cell, "Manage", e.RowIndex == _hoverRow);
                    e.Handled = true;
                    break;

                case "TypeText":
                    {
                        (Color bg, Color fg, string label) = text switch
                        {
                            "Inquiry" => (Color.FromArgb(238, 242, 255), Color.FromArgb(79, 70, 229), "Question"),
                            "Complaint" => (Color.FromArgb(254, 242, 242), Color.FromArgb(220, 38, 38), "Concern"),
                            "Feedback" => (Color.FromArgb(240, 253, 244), Color.FromArgb(22, 163, 74), "Review"),
                            _ => (Color.FromArgb(243, 244, 246), Color.FromArgb(107, 114, 128), text)
                        };
                        PillBadgeRenderer.DrawPill(gr, cell, label, bg, fg);
                        e.Handled = true;
                        break;
                    }

                case "CustomerDisplay":
                    if (rowItem != null)
                    {
                        PaintCustomerCell(gr, rowItem.CustomerDisplay, rowItem.ContactDisplay, inner);
                        e.Handled = true;
                    }
                    break;

                case "RepairDisplay":
                    if (rowItem != null)
                    {
                        string line1 = !string.IsNullOrWhiteSpace(rowItem.RepairRequestNumber) ? rowItem.RepairRequestNumber : "General Support";
                        string line2 = !string.IsNullOrWhiteSpace(rowItem.DeviceModel) ? rowItem.DeviceModel : "No device linked";
                        PaintTwoLine(gr, inner, line1, line2);
                        e.Handled = true;
                    }
                    break;

                case "Subject":
                    if (rowItem != null)
                    {
                        string snippet = !string.IsNullOrWhiteSpace(rowItem.Notes)
                            ? rowItem.Notes.Replace("\r", " ").Replace("\n", " ")
                            : "No additional notes";
                        PaintTwoLine(gr, inner, rowItem.Subject, snippet);
                        e.Handled = true;
                    }
                    break;

                case "PriorityText":
                    {
                        (Color bg, Color fg) = text switch
                        {
                            "High" => (Color.FromArgb(254, 242, 242), Color.FromArgb(220, 38, 38)),
                            "Medium" => (Color.FromArgb(255, 251, 235), Color.FromArgb(217, 119, 6)),
                            _ => (Color.FromArgb(243, 244, 246), Color.FromArgb(107, 114, 128))
                        };
                        PillBadgeRenderer.DrawPill(gr, cell, text, bg, fg);
                        e.Handled = true;
                        break;
                    }

                case "StatusText":
                    {
                        (Color bg, Color fg) = text switch
                        {
                            "Open" => (Color.FromArgb(255, 251, 235), Color.FromArgb(217, 119, 6)),
                            "In Progress" => (Color.FromArgb(238, 242, 255), Color.FromArgb(79, 70, 229)),
                            "Closed" => (Color.FromArgb(240, 253, 244), Color.FromArgb(22, 163, 74)),
                            _ => (Color.FromArgb(243, 244, 246), Color.FromArgb(107, 114, 128))
                        };
                        PillBadgeRenderer.DrawPill(gr, cell, text, bg, fg);
                        e.Handled = true;
                        break;
                    }

                case "InteractionDate":
                    if (rowItem != null)
                    {
                        string dStr = rowItem.InteractionDate.ToString("MMM d, yyyy");
                        string tStr = rowItem.InteractionDate.ToString("HH:mm");
                        PaintTwoLine(gr, inner, dStr, tStr);
                        e.Handled = true;
                    }
                    break;

                default:
                    e.PaintContent(cell);
                    e.Handled = true;
                    break;
            }
        }

        private static void PaintHeaderCell(DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;
            var cell = e.CellBounds;
            using (var b = new SolidBrush(UiKit.T.Surface))
                e.Graphics.FillRectangle(b, cell);
            using (var p = new Pen(UiKit.T.Line, 1))
                e.Graphics.DrawLine(p, cell.Left, cell.Bottom - 1, cell.Right, cell.Bottom - 1);

            UiKit.Quality(e.Graphics);
            string hdr = Convert.ToString(e.Value) ?? "";
            UiKit.Text(e.Graphics, hdr, UiKit.T.SmallStrong, UiKit.T.InkMuted,
                new Rectangle(cell.Left + CellPadX, cell.Top, Math.Max(0, cell.Width - CellPadX * 2), cell.Height - 1),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            e.Handled = true;
        }

        private void PaintRowBackground(DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;
            var cell = e.CellBounds;
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool hovered = e.RowIndex == _hoverRow;

            Color rowBg = selected ? UiKit.T.RowHover : (hovered ? UiKit.T.RowHover : UiKit.T.Surface);
            using (var b = new SolidBrush(rowBg))
                e.Graphics.FillRectangle(b, cell);
            using (var p = new Pen(UiKit.T.LineSoft, 1))
                e.Graphics.DrawLine(p, cell.Left, cell.Bottom - 1, cell.Right, cell.Bottom - 1);

            UiKit.Quality(e.Graphics);
        }

        private static void PaintActionButton(Graphics gr, Rectangle cell, string text, bool isHovered)
        {
            int btnH = 26;
            int btnW = Math.Min(76, cell.Width - 14);
            int bx = cell.X + (cell.Width - btnW) / 2;
            int by = cell.Y + (cell.Height - btnH) / 2;
            var btnRect = new Rectangle(bx, by, btnW, btnH);

            Color btnBg = isHovered ? AppTheme.Primary : UiKit.Wash(AppTheme.Primary);
            Color btnFg = isHovered ? Color.White : AppTheme.Primary;

            UiKit.FillRounded(gr, btnRect, 6, btnBg);
            using var p = new Pen(isHovered ? AppTheme.Primary : Color.FromArgb(210, 215, 235), 1);
            using var path = UiKit.Rounded(btnRect, 6);
            gr.DrawPath(p, path);

            UiKit.Text(gr, text, UiKit.T.SmallStrong, btnFg, btnRect, UiKit.Center);
        }

        private static class PillBadgeRenderer
        {
            public static void DrawPill(Graphics g, Rectangle bounds, string text, Color bg, Color fg)
            {
                if (string.IsNullOrEmpty(text)) text = "—";

                int pillH = 24;
                int textW = UiKit.Measure(text, UiKit.T.SmallStrong).Width;
                int pillW = Math.Min(bounds.Width - 14, textW + 20);
                pillW = Math.Max(56, pillW);
                int pillX = bounds.X + (bounds.Width - pillW) / 2;
                int pillY = bounds.Y + (bounds.Height - pillH) / 2;

                var rect = new Rectangle(pillX, pillY, pillW, pillH);
                UiKit.FillRounded(g, rect, pillH / 2, bg);
                UiKit.Text(g, text, UiKit.T.SmallStrong, fg, rect,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
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

            UiKit.Text(gr, line1 ?? "", UiKit.T.BodyStrong, UiKit.T.Ink,
                new Rectangle(rect.Left, y0, rect.Width, h1), CellText);
            UiKit.Text(gr, line2 ?? "", UiKit.T.Small, UiKit.T.InkMuted,
                new Rectangle(rect.Left, y0 + h1, rect.Width, h2), CellText);
        }

        // ═══════════ ACTIONS MENU (logic unchanged) ═══════════

        private void BuildActionsMenu()
        {
            _actionsMenu = new ContextMenuStrip { ShowImageMargin = false, Font = UiKit.T.Body };
            _actionsMenu.Renderer = new QuietMenuRenderer();

            var miResolve = _actionsMenu.Items.Add("Resolve / Close Ticket") as ToolStripMenuItem;
            miResolve!.Click += OnMenuQuickResolve;

            var miFollowUp = _actionsMenu.Items.Add("Schedule Customer Follow-Up") as ToolStripMenuItem;
            miFollowUp!.Click += OnMenuScheduleFollowUp;

            _actionsMenu.Items.Add(new ToolStripSeparator());

            var miCall = _actionsMenu.Items.Add("Call Customer") as ToolStripMenuItem;
            miCall!.Click += OnMenuCall;

            var miEmail = _actionsMenu.Items.Add("Email Customer") as ToolStripMenuItem;
            miEmail!.Click += OnMenuEmail;

            _actionsMenu.Items.Add(new ToolStripSeparator());

            var miHistory = _actionsMenu.Items.Add("View Customer History") as ToolStripMenuItem;
            miHistory!.Click += OnMenuCustomerHistory;

            var miRepair = _actionsMenu.Items.Add("View Linked Repair Ticket") as ToolStripMenuItem;
            miRepair!.Click += OnMenuRepairDetails;

            var miEdit = _actionsMenu.Items.Add("Edit Interaction") as ToolStripMenuItem;
            miEdit!.Click += (s, e) =>
            {
                if (_menuRowIndex >= 0 && dgv.Rows[_menuRowIndex].DataBoundItem is InteractionDto dto)
                    OpenEditDialog(dto);
            };

            _actionsMenu.Items.Add(new ToolStripSeparator());

            var miArchive = _actionsMenu.Items.Add("Archive") as ToolStripMenuItem;
            miArchive!.Click += OnMenuArchive;

            _actionsMenu.Opening += (s, e) =>
            {
                if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
                if (dgv.Rows[_menuRowIndex].DataBoundItem is not InteractionDto dto) return;

                miResolve.Enabled = dto.Status != 2 && dto.IsActive;

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

        private void OnMenuQuickResolve(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not InteractionDto dto) return;

            using var dlg = new QuickResolveInteractionDialog(dto);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                _ = ReloadAsync();
            }
        }

        private void OnMenuScheduleFollowUp(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not InteractionDto dto) return;

            var prefillFollowUp = new FollowUpDto
            {
                CustomerId = dto.CustomerId,
                RepairRequestId = dto.RepairRequestId,
                Subject = $"Follow-up: {dto.Subject}",
                ScheduledAt = DateTime.Today.AddDays(1).AddHours(10),
                Channel = 0
            };

            using var dlg = new FollowUpFormDialog(prefillFollowUp, _cachedCustomers, _cachedRepairs);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                ActionRequested?.Invoke(this, "follow-ups");
            }
        }

        private void OnMenuCall(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not InteractionDto dto) return;
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
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not InteractionDto dto) return;
            if (string.IsNullOrWhiteSpace(dto.CustomerEmail)) return;

            try
            {
                string subject = Uri.EscapeDataString($"Regarding your inquiry: {dto.Subject}");
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
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not InteractionDto dto) return;

            if (dto.CustomerId.HasValue)
                ActionRequested?.Invoke(this, $"customer-history:{dto.CustomerId.Value}");
        }

        private void OnMenuRepairDetails(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not InteractionDto dto) return;

            if (dto.RepairRequestId.HasValue)
                ActionRequested?.Invoke(this, "repairs");
        }

        private async void OnMenuArchive(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not InteractionDto dto) return;

            if (dto.IsActive) await ArchiveAsync(dto);
            else await RestoreAsync(dto);
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
            if (dgv.Rows[e.RowIndex].DataBoundItem is not InteractionDto dto) return;
            OpenEditDialog(dto);
        }

        private void Dgv_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) { _hoverRow = -1; return; }
            _hoverRow = e.RowIndex;
            dgv.Cursor = e.ColumnIndex >= 0 && dgv.Columns[e.ColumnIndex].Name == ColActions
                ? Cursors.Hand : Cursors.Default;
            dgv.InvalidateRow(e.RowIndex);
        }

        private void Dgv_CellMouseLeave(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            _hoverRow = -1;
            dgv.InvalidateRow(e.RowIndex);
        }

        // ═══════════ ARCHIVE / RESTORE (logic unchanged) ═══════════

        private async Task ArchiveAsync(InteractionDto dto)
        {
            var confirm = MessageBox.Show(
                $"Archive \u201c{dto.Subject}\u201d?\n\n" +
                "It leaves the list but nothing is deleted — you can restore it later.",
                "Archive interaction",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                await _api.ArchiveInteractionAsync(dto.CustomerInteractionId);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't archive this interaction.\n\n{ex.Message}",
                    "Archive failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task RestoreAsync(InteractionDto dto)
        {
            try
            {
                await _api.RestoreInteractionAsync(dto.CustomerInteractionId);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't restore this interaction.\n\n{ex.Message}",
                    "Restore failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═══════════ MODAL LAUNCHERS (logic unchanged) ═══════════

        private void OpenAddDialog()
        {
            using var dlg = new InteractionFormDialog(null, _currentFilter, _cachedCustomers, _cachedRepairs);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private void OpenEditDialog(InteractionDto dto)
        {
            using var dlg = new InteractionFormDialog(dto, null, _cachedCustomers, _cachedRepairs);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        //  EMBEDDED KPI TILE (identical to CompaniesControl)
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
        //  SEGMENTED FILTER (consistent with CompaniesControl)
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class InteractionsSegmentedFilter : Control
        {
            private readonly List<(string Key, int? Count)> _items = new();
            private int _selected = 0;
            private int _hover = -1;

            public event EventHandler<string>? SelectionChanged;

            public InteractionsSegmentedFilter((string Key, int? Count)[] items)
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
                        total += Math.Max(104, sz.Width + 28);
                    }
                    return Math.Max(330, total);
                }
            }

            public void UpdateCounts(Dictionary<string, int?> counts)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (counts.TryGetValue(_items[i].Key, out var c))
                        _items[i] = (_items[i].Key, c);
                }
                Invalidate();
            }

            public void SetKey(string key)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (string.Equals(_items[i].Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (_selected != i)
                        {
                            _selected = i;
                            Invalidate();
                            SelectionChanged?.Invoke(this, _items[_selected].Key);
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
                    SelectionChanged?.Invoke(this, _items[_selected].Key);
                }
                base.OnMouseClick(e);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  QUIET MENU RENDERER (consistent with CompaniesControl)
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