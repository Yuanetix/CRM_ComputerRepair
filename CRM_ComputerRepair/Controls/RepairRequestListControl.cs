using CRM.winforms.Auth;
using CRM.winforms.Forms;
using CRM.winforms;
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
    /// Professional Repair Requests Operations Command Center for Staff.
    /// Manages intake, diagnostics, bench progress, parts/labor costing,
    /// 1-click status transitions, and rapid customer pickup follow-ups.
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
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Hardware repair workbench, diagnostic tickets, and customer device servicing queue",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Click += async (s, e) => await ReloadAsync();
            _tips.SetToolTip(btnRefresh, "Refresh list (F5)");

            btnAdd = new SaasButton("New repair request", SaasButtonVariant.Primary, "\uE710");
            btnAdd.Click += (s, e) => OpenAddDialog();
            _tips.SetToolTip(btnAdd, "Intake new repair request (Ctrl+N)");

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnRefresh);
            Controls.Add(btnAdd);

            // ── Metric strip (5 Interactive KPI Tiles) ──
            strip = new MetricStrip();
            strip.AddItem("Total", null, AppTheme.Primary);
            strip.AddItem("Pending", 0, AppTheme.Warning);
            strip.AddItem("Approved", 1, AppTheme.Primary);
            strip.AddItem("In progress", 2, AppTheme.Primary);
            strip.AddItem("Urgent", 99, AppTheme.Danger);
            strip.AddItem("Completed", 3, AppTheme.Success);

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
                Text = "All repair requests",
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
                ("Pending", 0),
                ("Approved", 1),
                ("In Progress", 2),
                ("Completed", 3)
            });
            segments.SelectionChanged += (s, e) =>
            {
                _statusFilter = segments.Selected;
                strip.SelectByValue(_statusFilter);
                ApplySearch();
            };

            search = new SearchBox { PlaceholderText = "Search repairs... (Ctrl+F)" };
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
            if (_statusFilter == 99)
                segments.SelectByValue(null);
            else
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

        // ═══════════ LAYOUT (4pt grid) ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            // ── Header ──
            lblTitle.Location = new Point(0, 0);

            int subtitleY = lblTitle.PreferredHeight + 6;
            lblSubtitle.Location = new Point(1, subtitleY);

            int btnW = btnAdd.PreferredWidth;
            btnAdd.Size = new Size(btnW, UiKit.T.ButtonHeight);
            btnAdd.Location = new Point(Width - btnAdd.Width, 2);

            int refW = btnRefresh.PreferredWidth;
            btnRefresh.Size = new Size(refW, UiKit.T.ButtonHeight);
            btnRefresh.Location = new Point(btnAdd.Left - refW - UiKit.T.S2, 2);

            int dividerY = subtitleY + lblSubtitle.PreferredHeight + UiKit.T.S4;

            // ── Metric strip ──
            int stripTop = dividerY + UiKit.T.S5;
            int stripH = Math.Max(UiKit.T.StripHeight, strip.PreferredContentHeight());
            strip.Location = new Point(0, stripTop);
            strip.Size = new Size(Width, stripH);

            // ── Workbench card ──
            int cardTop = stripTop + stripH + UiKit.T.S5;
            int cardHeight = Math.Max(240, Height - cardTop);

            card.Location = new Point(0, cardTop);
            card.Size = new Size(Width, cardHeight);

            // ── Inside card ──
            const int cp = UiKit.T.S5;

            lblGridTitle.Location = new Point(cp, cp);
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
            state.ShowLoading("Loading repair requests…");
            dgv.Visible = false;
            try
            {
                var repTask = _api.GetRepairRequestsAsync();
                var custTask = _cachedCustomers.Count == 0 ? _api.GetCustomersAsync() : Task.FromResult(_cachedCustomers);

                await Task.WhenAll(repTask, custTask);

                _all = await repTask ?? new List<RepairRequestDto>();
                _cachedCustomers = await custTask ?? new List<CustomerDto>();

                UpdateStats();
                ApplySearch();
            }
            catch (Exception ex)
            {
                _all = new List<RepairRequestDto>();
                UpdateStats();
                ApplySearch();

                MessageBox.Show(
                    $"Couldn't load repair requests.\n\n{ex.Message}",
                    "Connection problem",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void UpdateStats()
        {
            strip.SetValue(0, _all.Count);
            strip.SetValue(1, _all.Count(x => x.Status == 0));
            strip.SetValue(2, _all.Count(x => x.Status == 1));
            strip.SetValue(3, _all.Count(x => x.Status == 2));
            strip.SetValue(4, _all.Count(x => (x.Priority == 2 || x.Priority == 3) && x.Status != 3 && x.Status != 4));
            strip.SetValue(5, _all.Count(x => x.Status == 3));
        }

        private void ApplySearch(bool resetPage = true)
        {
            if (resetPage) pager.Reset();
            var term = search.Inner.Text?.Trim() ?? string.Empty;

            IEnumerable<RepairRequestDto> q = _all;

            // Status filter
            if (_statusFilter.HasValue)
            {
                if (_statusFilter.Value == 99)
                {
                    // Urgent or High priority non-completed repairs
                    q = q.Where(x => (x.Priority == 2 || x.Priority == 3) && x.Status != 3 && x.Status != 4);
                }
                else
                {
                    q = q.Where(x => x.Status == _statusFilter.Value);
                }
            }

            // Keyword search
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
            AddActionsColumn();

            lblCount.Text = TableKit.FormatCount(view.Count, _all.Count);

            lblGridTitle.Text = _statusFilter switch
            {
                0 => "Pending intake",
                1 => "Approved repairs",
                2 => "Repairs in progress",
                3 => "Completed repairs",
                99 => "Urgent repairs",
                _ => "All repair requests"
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
                    state.Show("\uE721", "No matches",
                               $"Nothing matches \u201c{term}\u201d. Try a shorter keyword or press Esc to clear.");
                else if (_statusFilter == 99)
                    state.Show("\uE73E", "No urgent repairs",
                               "Great work! There are no overdue or urgent repair tickets on the workbench.");
                else if (_statusFilter.HasValue)
                    state.Show("\uE71C", "Nothing here yet",
                               "No repair tickets matching this status. Select Total to view all.");
                else
                    state.Show("\uE8BD", "No repair requests yet",
                               $"Click \u201c{btnAdd.Text}\u201d to intake the first computer repair job.");
            }
        }

        // ═══════════ GRID STYLE & COLUMNS ═══════════

        private static void StyleGrid(DataGridView g) => TableKit.StyleGrid(g);

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
                if (col != null)
                    col.Visible = false;
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

            Setup("PriorityText", "Priority", 110, 0);
            Setup("RequestNumber", "Ticket # & Date", 160, 1);
            Setup("CustomerDisplay", "Customer & Contact", 220, 2);
            Setup("DeviceDisplay", "Device & Serial", 210, 3);
            Setup("IssueDescription", "Issue & Diagnostics", 0, 4, fill: true);
            Setup("StatusText", "Status", 130, 5);
            Setup("CostDisplay", "Cost", 120, 6);

            if (dgv.Columns["CostDisplay"] is { } costCol)
            {
                costCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                costCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }

        private void AddActionsColumn() => TableKit.AddActionsColumn(dgv, ColActions);

        // ═══════════ CELL PAINTING (2-Line Rich Cells & Badges) ═══════════

        private void Dgv_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;
            var g = e.Graphics;

            // Header baseline rule
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

            TableKit.PaintRowShell(g, e.CellBounds, bg);

            bool custom = col is "PriorityText" or "RequestNumber" or "CustomerDisplay" or "DeviceDisplay" or "IssueDescription" or "StatusText" or "CostDisplay" || col == ColActions;

            if (!custom)
            {
                e.PaintContent(e.CellBounds);
                e.Handled = true;
                return;
            }

            UiKit.Quality(g);
            var r = e.CellBounds;
            string text = e.FormattedValue?.ToString() ?? string.Empty;
            var rowItem = dgv.Rows[e.RowIndex].DataBoundItem as RepairRequestDto;

            // 1. Actions menu button
            if (col == ColActions)
            {
                TableKit.PaintActionCell(g, r, hovered);
                e.Handled = true;
                return;
            }

            // 2. Priority — always a pill (Urgent red, High amber, Medium blue).
            if (col == "PriorityText")
            {
                if (text == "Urgent" || text == "High" || text == "Medium")
                {
                    Color accent = text switch
                    {
                        "Urgent" => AppTheme.Danger,
                        "High" => AppTheme.Warning,
                        _ => AppTheme.Primary
                    };
                    var size = UiKit.Measure(text, UiKit.T.SmallStrong);
                    var pill = new Rectangle(r.Left + UiKit.T.S3, r.Top + (r.Height - 22) / 2,
                        Math.Max(56, size.Width + 20), 22);
                    UiKit.FillRounded(g, pill, UiKit.T.PillRadius, UiKit.Wash(accent));
                    UiKit.Text(g, text, UiKit.T.SmallStrong, accent, pill,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    TableKit.PaintPill(g, r, text);
                }

                e.Handled = true;
                return;
            }

            // 3. Ticket # on line 1, Request Date on line 2
            if (col == "RequestNumber" && rowItem != null)
            {
                TableKit.PaintTwoLine(g, r, rowItem.RequestNumber, rowItem.RequestDate.ToString("MMM d  HH:mm"));
                e.Handled = true;
                return;
            }

            // 4. Customer Display (Name on line 1, Contact on line 2)
            if (col == "CustomerDisplay" && rowItem != null)
            {
                TableKit.PaintTwoLine(g, r, rowItem.CustomerDisplay, rowItem.ContactDisplay);
                e.Handled = true;
                return;
            }

            // 5. Device Display (Model on line 1, Serial on line 2)
            if (col == "DeviceDisplay" && rowItem != null)
            {
                string serial = !string.IsNullOrWhiteSpace(rowItem.SerialNumber) ? $"SN: {rowItem.SerialNumber}" : "No serial saved";
                TableKit.PaintTwoLine(g, r, rowItem.DeviceDisplay, serial);
                e.Handled = true;
                return;
            }

            // 6. Issue & Notes (Issue on line 1, Notes snippet on line 2)
            if (col == "IssueDescription" && rowItem != null)
            {
                string snippet = !string.IsNullOrWhiteSpace(rowItem.TechnicianNotes)
                    ? rowItem.TechnicianNotes.Replace("\r", " ").Replace("\n", " ")
                    : "No technician notes logged";
                TableKit.PaintTwoLine(g, r, rowItem.IssueDescription, snippet);
                e.Handled = true;
                return;
            }

            // 7. Status Pill
            if (col == "StatusText")
            {
                Color accent = text switch
                {
                    "Pending" => AppTheme.Warning,
                    "Approved" => AppTheme.Primary,
                    "In Progress" => AppTheme.Primary,
                    "Completed" => AppTheme.Success,
                    "Rejected" => AppTheme.Danger,
                    _ => UiKit.T.InkMuted
                };

                var size = UiKit.Measure(text, UiKit.T.SmallStrong);
                int pillW = Math.Max(56, size.Width + 20);
                int pillH = 22;
                var pill = new Rectangle(r.Left + UiKit.T.S3, r.Top + (r.Height - pillH) / 2, pillW, pillH);

                UiKit.FillRounded(g, pill, UiKit.T.PillRadius, UiKit.Wash(accent));
                UiKit.Text(g, text, UiKit.T.SmallStrong, accent, pill,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                e.Handled = true;
                return;
            }

            // 8. Cost Display
            if (col == "CostDisplay" && rowItem != null)
            {
                var textRect = new Rectangle(r.Left + UiKit.T.S3, r.Top, r.Width - UiKit.T.S3 * 2, r.Height);
                Color fg = rowItem.ActualCost.HasValue && rowItem.ActualCost > 0 ? UiKit.T.Ink : UiKit.T.InkMuted;
                Font font = rowItem.ActualCost.HasValue && rowItem.ActualCost > 0 ? UiKit.T.BodyStrong : UiKit.T.Body;

                UiKit.Text(g, rowItem.CostDisplay, font, fg, textRect,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                e.Handled = true;
            }
        }

        // ═══════════ ACTIONS MENU ═══════════

        private void BuildActionsMenu()
        {
            _actionsMenu = TableKit.MakeMenu();

            // 0: Start Repair (Status 1/0 -> 2)
            var miStart = _actionsMenu.Items.Add("Start Repair (Put On Workbench)");
            miStart.Click += async (s, e) => await QuickStatusAsync(2);

            // 1: Complete Repair
            var miComplete = _actionsMenu.Items.Add("Complete Repair & Enter Final Cost");
            miComplete.Click += OnMenuQuickComplete;

            // 2: Schedule Follow-Up
            var miFollowUp = _actionsMenu.Items.Add("Schedule Ready-for-Pickup / Follow-Up");
            miFollowUp.Click += OnMenuScheduleFollowUp;

            // 3: Log Interaction
            var miInteraction = _actionsMenu.Items.Add("Log Customer Inquiry / Concern");
            miInteraction.Click += OnMenuLogInteraction;

            _actionsMenu.Items.Add(new ToolStripSeparator());

            // 5: Call Customer
            var miCall = _actionsMenu.Items.Add("Call Customer");
            miCall.Click += OnMenuCall;

            // 6: Email Customer
            var miEmail = _actionsMenu.Items.Add("Email Customer");
            miEmail.Click += OnMenuEmail;

            _actionsMenu.Items.Add(new ToolStripSeparator());

            // 8: View Customer History
            var miHistory = _actionsMenu.Items.Add("View Customer History");
            miHistory.Click += OnMenuCustomerHistory;

            // 9: Edit Full Details
            var miEdit = _actionsMenu.Items.Add("Edit Repair Details");
            miEdit.Click += (s, e) =>
            {
                if (_menuRowIndex >= 0 && dgv.Rows[_menuRowIndex].DataBoundItem is RepairRequestDto dto)
                    OpenEditDialog(dto);
            };

            TableKit.StyleMenuItems(_actionsMenu);

            _actionsMenu.Opening += (s, e) =>
            {
                if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
                if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;

                miStart.Enabled = dto.Status == 0 || dto.Status == 1; // Pending or Approved
                miComplete.Enabled = dto.Status != 3 && dto.Status != 4; // Not Completed / Rejected

                miCall.Enabled = !string.IsNullOrWhiteSpace(dto.CustomerPhone);
                miCall.Text = !string.IsNullOrWhiteSpace(dto.CustomerPhone) ? $"Call Customer: {dto.CustomerPhone}" : "Call Customer";

                miEmail.Enabled = !string.IsNullOrWhiteSpace(dto.CustomerEmail);
                miEmail.Text = !string.IsNullOrWhiteSpace(dto.CustomerEmail) ? $"Email Customer: {dto.CustomerEmail}" : "Email Customer";

                miHistory.Enabled = dto.CustomerId > 0;
            };

            _actionsMenu.Closed += (s, e) => { dgv.Invalidate(); };
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
                MessageBox.Show($"Failed to update repair status:\n{ex.Message}", "Status Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnMenuQuickComplete(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;

            using var dlg = new QuickCompleteRepairDialog(dto);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
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
                        Channel = 0 // Phone Call
                    };

                    using var followUpDlg = new FollowUpFormDialog(prefillFollowUp, _cachedCustomers, _all);
                    if (followUpDlg.ShowModal(this.FindForm()) == DialogResult.OK)
                    {
                        ActionRequested?.Invoke(this, "follow-ups");
                    }
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
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                ActionRequested?.Invoke(this, "follow-ups");
            }
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
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                ActionRequested?.Invoke(this, "interactions");
            }
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
            {
                ActionRequested?.Invoke(this, $"customer-history:{dto.CustomerId}");
            }
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
            if (dgv.Rows[e.RowIndex].DataBoundItem is not RepairRequestDto dto) return;
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

        // ═══════════ MODAL LAUNCHERS ═══════════

        private void OpenAddDialog()
        {
            using var dlg = new RepairRequestFormDialog(null, null, _cachedCustomers);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private void OpenEditDialog(RepairRequestDto dto)
        {
            using var dlg = new RepairRequestFormDialog(dto, null, _cachedCustomers);
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