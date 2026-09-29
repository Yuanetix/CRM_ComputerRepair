using CRM.winforms.Auth;
using CRM.winforms.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Professional Follow-Up Operations Command Center for Staff.
    /// Provides immediate SLA urgency tracking (Due Today, Overdue, Upcoming),
    /// rich customer & repair order linkages, quick completion with outcome logging,
    /// 1-click rescheduling, and direct communication shortcuts.
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
        private Label lblSubtitle = null!;
        private SaasButton btnAdd = null!;
        private SaasButton btnRefresh = null!;

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

        public event EventHandler<string>? ActionRequested;

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
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Proactive customer outreach, post-repair satisfaction checks, and service reminders",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Click += async (s, e) => await ReloadAsync();
            _tips.SetToolTip(btnRefresh, "Refresh list (F5)");

            btnAdd = new SaasButton("Add follow-up", SaasButtonVariant.Primary, "\uE710");
            btnAdd.Click += (s, e) => OpenAddDialog();
            _tips.SetToolTip(btnAdd, "Schedule new follow-up (Ctrl+N)");

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnRefresh);
            Controls.Add(btnAdd);

            // ── Metric strip (5 Interactive KPI Tiles) ──
            strip = new MetricStrip();
            strip.AddItem("Total", null, AppTheme.Primary);
            strip.AddItem("Due Today", 10, AppTheme.Warning);
            strip.AddItem("Overdue", 20, AppTheme.Danger);
            strip.AddItem("Scheduled", 0, AppTheme.Primary);
            strip.AddItem("Completed", 1, AppTheme.Success);
            strip.AddItem("Cancelled", 2, UiKit.T.InkFaint);

            strip.SelectionChanged += (s, e) =>
            {
                _statusFilter = strip.SelectedStatus;
                SyncSegmentsFromStrip();
                ApplySearch();
            };
            Controls.Add(strip);

            // ── Workbench card ──
            card = new SurfaceCard();

            lblGridTitle = new Label
            {
                Text = "All follow-up tasks",
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
                ("Due Today", 10),
                ("Overdue", 20),
                ("Scheduled", 0),
                ("Completed", 1),
                ("Cancelled", 2)
            });
            segments.SelectionChanged += (s, e) =>
            {
                _statusFilter = segments.Selected;
                strip.SelectByValue(_statusFilter);
                ApplySearch();
            };

            search = new SearchBox { PlaceholderText = "Search follow-ups... (Ctrl+F)" };
            search.Inner.TextChanged += (s, e) => ApplySearch();
            _tips.SetToolTip(search, "Search (Ctrl+F, Esc to clear)");

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

        private void SyncSegmentsFromStrip()
        {
            segments.SelectByValue(_statusFilter);
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

        // ═══════════ LAYOUT ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            lblTitle.Location = new Point(0, 0);

            int subtitleY = lblTitle.PreferredHeight + 6;
            lblSubtitle.Location = new Point(1, subtitleY);

            // Right-aligned header buttons
            btnAdd.Size = new Size(btnAdd.PreferredWidth, UiKit.T.ButtonHeight);
            btnAdd.Location = new Point(Width - btnAdd.Width, 2);

            btnRefresh.Size = new Size(btnRefresh.PreferredWidth, UiKit.T.ButtonHeight);
            btnRefresh.Location = new Point(btnAdd.Left - btnRefresh.Width - UiKit.T.S2, 2);

            int dividerY = subtitleY + lblSubtitle.PreferredHeight + UiKit.T.S4;

            int stripTop = dividerY + UiKit.T.S5;
            int stripH = Math.Max(UiKit.T.StripHeight, strip.PreferredContentHeight());
            strip.Location = new Point(0, stripTop);
            strip.Size = new Size(Width, stripH);

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

        // ═══════════ DATA LOAD ═══════════

        public async Task ReloadAsync()
        {
            state.ShowLoading("Loading follow-ups…");
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
            strip.SetValue(0, _all.Count);
            strip.SetValue(1, _all.Count(x => x.IsDueToday));
            strip.SetValue(2, _all.Count(x => x.IsOverdue));
            strip.SetValue(3, _all.Count(x => x.Status == 0));
            strip.SetValue(4, _all.Count(x => x.Status == 1));
            strip.SetValue(5, _all.Count(x => x.Status == 2));
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

            lblCount.Text = TableKit.FormatCount(view.Count, _all.Count);

            lblGridTitle.Text = _statusFilter switch
            {
                10 => "Follow-ups due today",
                20 => "Overdue follow-ups requiring callback",
                0 => "Upcoming scheduled follow-ups",
                1 => "Completed follow-ups",
                2 => "Cancelled follow-ups",
                _ => "All follow-up tasks"
            };

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
                        "Use \u201cAdd follow-up\u201d to schedule proactive checkups with customers.");
                else
                    state.Show("\uE8BD", "No follow-ups yet",
                        "Schedule your first customer service check or post-repair follow-up.");
            }
        }

        // ═══════════ GRID STYLE & COLUMNS ═══════════

        private static void StyleGrid(DataGridView g) => TableKit.StyleGrid(g);

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

            Setup("UrgencyText", "Urgency", 110, 0);
            Setup("CustomerDisplay", "Customer & Contact", 220, 1);
            Setup("RepairDisplay", "Linked Repair", 200, 2);
            Setup("Subject", "Subject", 0, 3, fill: true);
            Setup("ChannelText", "Channel", 110, 4);
            Setup("ScheduledAt", "Scheduled", 150, 5);
            Setup("AssignedToUserId", "Assigned", 115, 6);
            Setup("StatusText", "Status", 130, 7);

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

        private void AddActionsColumn() => TableKit.AddActionsColumn(dgv, ColActions);

        // ═══════════ CELL PAINTING (2-Line Rich Cells & Badges) ═══════════

        private void Dgv_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;
            var g = e.Graphics;

            // Header row painting
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

            // Background & hairline divider
            TableKit.PaintRowShell(g, e.CellBounds, bg);

            bool custom = col is "UrgencyText" or "CustomerDisplay" or "RepairDisplay" or "ChannelText" or "StatusText" || col == ColActions;

            if (!custom)
            {
                e.PaintContent(e.CellBounds);
                e.Handled = true;
                return;
            }

            UiKit.Quality(g);
            var r = e.CellBounds;
            string text = e.FormattedValue?.ToString() ?? string.Empty;
            var rowItem = dgv.Rows[e.RowIndex].DataBoundItem as FollowUpDto;

            // 1. Actions menu button
            if (col == ColActions)
            {
                TableKit.PaintActionCell(g, r, hovered);
                e.Handled = true;
                return;
            }

            // 2. Urgency Pill
            if (col == "UrgencyText" && rowItem != null)
            {
                Color accent = text switch
                {
                    "Overdue" => AppTheme.Danger,
                    "Due Today" => AppTheme.Warning,
                    "Completed" => AppTheme.Success,
                    "Cancelled" => UiKit.T.InkFaint,
                    _ => AppTheme.Primary // Upcoming
                };

                int pillW = 86;
                int pillH = 22;
                var pill = new Rectangle(r.Left + UiKit.T.S3, r.Top + (r.Height - pillH) / 2, pillW, pillH);

                UiKit.FillRounded(g, pill, UiKit.T.PillRadius, UiKit.Wash(accent));
                UiKit.Text(g, text, UiKit.T.SmallStrong, accent, pill,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                e.Handled = true;
                return;
            }

            // 3. Customer Display (Name on top, Phone/Email underneath)
            if (col == "CustomerDisplay" && rowItem != null)
            {
                TableKit.PaintTwoLine(g, r, rowItem.CustomerDisplay, rowItem.ContactDisplay);
                e.Handled = true;
                return;
            }

            // 4. Linked Repair (Ticket # on top, Device model underneath)
            if (col == "RepairDisplay" && rowItem != null)
            {
                string line1 = !string.IsNullOrWhiteSpace(rowItem.RepairRequestNumber) ? rowItem.RepairRequestNumber : "General";
                string line2 = !string.IsNullOrWhiteSpace(rowItem.DeviceModel) ? rowItem.DeviceModel : "No device linked";
                TableKit.PaintTwoLine(g, r, line1, line2);
                e.Handled = true;
                return;
            }

            // 5. Channel Dot + Text
            if (col == "ChannelText")
            {
                Color accent = text switch
                {
                    "Call" => AppTheme.Primary,
                    "Email" => AppTheme.Success,
                    "SMS" => AppTheme.Warning,
                    "Visit" => Color.FromArgb(0x7C, 0x5C, 0xFC),
                    _ => UiKit.T.InkFaint
                };

                TableKit.PaintDotText(g, r, accent, text);
                e.Handled = true;
                return;
            }

            // 6. Status Text Pill
            if (col == "StatusText")
            {
                Color accent = text switch
                {
                    "Scheduled" => AppTheme.Primary,
                    "Completed" => AppTheme.Success,
                    "Cancelled" => UiKit.T.InkMuted,
                    _ => UiKit.T.InkMuted
                };

                var size = UiKit.Measure(text, UiKit.T.SmallStrong);
                int pillW = size.Width + UiKit.T.S4;
                int pillH = 22;
                var pill = new Rectangle(r.Left + UiKit.T.S3, r.Top + (r.Height - pillH) / 2, pillW, pillH);

                UiKit.FillRounded(g, pill, UiKit.T.PillRadius, UiKit.Wash(accent));
                UiKit.Text(g, text, UiKit.T.SmallStrong, accent, pill,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                e.Handled = true;
            }
        }

        // ═══════════ ACTIONS MENU ═══════════

        private void BuildActionsMenu()
        {
            _actionsMenu = TableKit.MakeMenu();

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

            TableKit.StyleMenuItems(_actionsMenu);

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

        // ═══════════ MODAL LAUNCHERS ═══════════

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
            _actionsMenu.Show(dgv, new Point(cellRect.Right - 200, cellRect.Bottom));
        }

        private void Dgv_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgv.RowCount) return;
            if (dgv.Rows[e.RowIndex].DataBoundItem is not FollowUpDto dto) return;
            OpenEditDialog(dto);
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