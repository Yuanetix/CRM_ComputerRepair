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
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Professional Customer Interactions Command Center for Staff.
    /// Manages customer questions, service concerns, and reviews with complete
    /// customer & repair order linkages, SLA urgency tracking, rapid resolution logging,
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
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Client questions, repair inquiries, service concerns, and customer reviews",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Click += async (s, e) => await ReloadAsync();
            _tips.SetToolTip(btnRefresh, "Refresh list (F5)");

            btnAdd = new SaasButton("Log interaction", SaasButtonVariant.Primary, "\uE710");
            btnAdd.Click += (s, e) => OpenAddDialog();
            _tips.SetToolTip(btnAdd, "Log new customer interaction (Ctrl+N)");

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnRefresh);
            Controls.Add(btnAdd);

            // ── Metric strip (5 Interactive KPI Tiles) ──
            strip = new MetricStrip();
            strip.AddItem("Total", null, AppTheme.Primary);
            strip.AddItem("Open", 0, AppTheme.Warning);
            strip.AddItem("In progress", 1, AppTheme.Primary);
            strip.AddItem("High priority", 99, AppTheme.Danger);
            strip.AddItem("Closed", 2, AppTheme.Success);

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
                Text = "All interactions",
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

            segments = new SegmentedFilter(new[]
            {
                ("All", (InteractionTypeFilter?)null),
                ("Questions", InteractionTypeFilter.Inquiry),
                ("Concerns", InteractionTypeFilter.Complaint),
                ("Reviews", InteractionTypeFilter.Feedback)
            });
            segments.SelectionChanged += (s, e) => SetFilter(segments.Selected);

            search = new SearchBox { PlaceholderText = "Search interactions... (Ctrl+F)" };
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

        // ═══════════ TYPE FILTER ═══════════

        private void SetFilter(InteractionTypeFilter? filter)
        {
            _currentFilter = filter;

            lblGridTitle.Text = filter switch
            {
                InteractionTypeFilter.Inquiry => "Client questions & turnaround inquiries",
                InteractionTypeFilter.Complaint => "Service concerns & hardware complaints",
                InteractionTypeFilter.Feedback => "Client reviews & satisfaction feedback",
                _ => "All customer interactions"
            };

            lblSubtitle.Text = filter switch
            {
                InteractionTypeFilter.Inquiry => "Questions regarding diagnostics, estimates, and repair turnaround",
                InteractionTypeFilter.Complaint => "Concerns regarding repairs, part delays, or warranty issues",
                InteractionTypeFilter.Feedback => "Reviews, post-service satisfaction notes, and customer ratings",
                _ => "Client questions, repair inquiries, service concerns, and customer reviews"
            };

            btnAdd.Text = filter switch
            {
                InteractionTypeFilter.Inquiry => "Log question",
                InteractionTypeFilter.Complaint => "Log concern",
                InteractionTypeFilter.Feedback => "Log review",
                _ => "Log interaction"
            };

            LayoutUi();
            Invalidate();

            _ = ReloadAsync();
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
            state.ShowLoading("Loading interactions…");
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
            strip.SetValue(0, _all.Count);
            strip.SetValue(1, _all.Count(x => x.Status == 0));
            strip.SetValue(2, _all.Count(x => x.Status == 1));
            strip.SetValue(3, _all.Count(x => x.Priority == 2 && x.Status != 2));
            strip.SetValue(4, _all.Count(x => x.Status == 2));
        }

        private void ApplySearch(bool resetPage = true)
        {
            if (resetPage) pager.Reset();
            var term = search.Inner.Text?.Trim() ?? string.Empty;

            IEnumerable<InteractionDto> q = _all;

            // Status filter
            if (_statusFilter.HasValue)
            {
                if (_statusFilter.Value == 99)
                {
                    // High priority open/in-progress
                    q = q.Where(x => x.Priority == 2 && x.Status != 2);
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

        private static void StyleGrid(DataGridView g) => TableKit.StyleGrid(g);

        private void ConfigureColumns()
        {
            foreach (var hidden in new[]
            {
                "CustomerInteractionId", "CustomerId", "RepairRequestId",
                "InteractionType", "Status", "Priority",
                "InteractionByUserId", "UpdatedAt", "ClosedAt", "IsActive",
                "ActivityStatus", "ClosedAtDisplay", "CustomerName",
                "CustomerPhone", "CustomerEmail", "RepairRequestNumber",
                "DeviceModel", "ContactDisplay", "Notes", "Resolution"
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

            Setup("TypeText", "Type", 125, 0);
            Setup("CustomerDisplay", "Customer & Contact", 220, 1);
            Setup("RepairDisplay", "Linked Repair", 200, 2);
            Setup("Subject", "Subject & Summary", 0, 3, fill: true);
            Setup("PriorityText", "Priority", 110, 4);
            Setup("StatusText", "Status", 130, 5);
            Setup("InteractionDate", "Created", 140, 6);

            if (dgv.Columns["InteractionDate"] is { } dateCol)
            {
                dateCol.DefaultCellStyle.Format = "MMM d  HH:mm";
                dateCol.DefaultCellStyle.ForeColor = UiKit.T.InkMuted;
                dateCol.DefaultCellStyle.SelectionForeColor = UiKit.T.InkMuted;
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

            bool custom = col is "TypeText" or "CustomerDisplay" or "RepairDisplay" or "Subject" or "PriorityText" or "StatusText" || col == ColActions;

            if (!custom)
            {
                e.PaintContent(e.CellBounds);
                e.Handled = true;
                return;
            }

            UiKit.Quality(g);
            var r = e.CellBounds;
            string text = e.FormattedValue?.ToString() ?? string.Empty;
            var rowItem = dgv.Rows[e.RowIndex].DataBoundItem as InteractionDto;

            // 1. Actions menu button
            if (col == ColActions)
            {
                TableKit.PaintActionCell(g, r, hovered);
                e.Handled = true;
                return;
            }

            // 2. Type Dot + Label
            if (col == "TypeText")
            {
                Color accent = text switch
                {
                    "Inquiry" => AppTheme.Primary,
                    "Complaint" => AppTheme.Danger,
                    "Feedback" => AppTheme.Success,
                    _ => UiKit.T.InkFaint
                };

                string label = text switch
                {
                    "Inquiry" => "Question",
                    "Complaint" => "Concern",
                    "Feedback" => "Review",
                    _ => text
                };

                TableKit.PaintDotText(g, r, accent, label);
                e.Handled = true;
                return;
            }

            // 3. Customer Display (Name on line 1, Phone/Email on line 2)
            if (col == "CustomerDisplay" && rowItem != null)
            {
                TableKit.PaintTwoLine(g, r, rowItem.CustomerDisplay, rowItem.ContactDisplay);
                e.Handled = true;
                return;
            }

            // 4. Linked Repair (Ticket # on line 1, Device model on line 2)
            if (col == "RepairDisplay" && rowItem != null)
            {
                string line1 = !string.IsNullOrWhiteSpace(rowItem.RepairRequestNumber) ? rowItem.RepairRequestNumber : "General Support";
                string line2 = !string.IsNullOrWhiteSpace(rowItem.DeviceModel) ? rowItem.DeviceModel : "No device linked";
                TableKit.PaintTwoLine(g, r, line1, line2);
                e.Handled = true;
                return;
            }

            // 5. Subject & Notes Snippet (Subject in bold on line 1, Notes snippet on line 2)
            if (col == "Subject" && rowItem != null)
            {
                string snippet = !string.IsNullOrWhiteSpace(rowItem.Notes)
                    ? rowItem.Notes.Replace("\r", " ").Replace("\n", " ")
                    : "No notes provided";
                TableKit.PaintTwoLine(g, r, rowItem.Subject, snippet);
                e.Handled = true;
                return;
            }

            // 6. Priority — always a pill (High = urgent red, Medium = info blue).
            if (col == "PriorityText")
            {
                if (text == "High" || text == "Medium")
                {
                    Color accent = text == "High" ? AppTheme.Danger : AppTheme.Primary;
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

            // 7. Status Pill
            if (col == "StatusText")
            {
                Color accent = text switch
                {
                    "Open" => AppTheme.Warning,
                    "In Progress" => AppTheme.Primary,
                    "Closed" => AppTheme.Success,
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

            // 0: Resolve
            var miResolve = _actionsMenu.Items.Add("Resolve / Close Ticket");
            miResolve.Click += OnMenuQuickResolve;

            // 1: Schedule Follow-up
            var miFollowUp = _actionsMenu.Items.Add("Schedule Customer Follow-Up");
            miFollowUp.Click += OnMenuScheduleFollowUp;

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
            var miEdit = _actionsMenu.Items.Add("Edit Interaction");
            miEdit.Click += (s, e) =>
            {
                if (_menuRowIndex >= 0 && dgv.Rows[_menuRowIndex].DataBoundItem is InteractionDto dto)
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
                Channel = 0 // Call
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
            {
                ActionRequested?.Invoke(this, $"customer-history:{dto.CustomerId.Value}");
            }
        }

        private void OnMenuRepairDetails(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not InteractionDto dto) return;

            if (dto.RepairRequestId.HasValue)
            {
                ActionRequested?.Invoke(this, "repairs");
            }
        }

        private async void OnMenuArchive(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.RowCount) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not InteractionDto dto) return;

            if (dto.IsActive)
                await ArchiveAsync(dto);
            else
                await RestoreAsync(dto);
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
            if (dgv.Rows[e.RowIndex].DataBoundItem is not InteractionDto dto) return;
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

        // ═══════════ ARCHIVE / RESTORE ═══════════

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
                MessageBox.Show(
                    $"Couldn't archive this interaction.\n\n{ex.Message}",
                    "Archive failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
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
                MessageBox.Show(
                    $"Couldn't restore this interaction.\n\n{ex.Message}",
                    "Restore failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ═══════════ MODAL LAUNCHERS ═══════════

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
        //  NESTED UI COMPONENTS
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class SurfaceCard : WorkbenchCard { }
        [DesignerCategory("Code")]
        private sealed class MetricStrip : WorkbenchMetrics { }
        [DesignerCategory("Code")]
        private sealed class SegmentedFilter : WorkbenchSegments<InteractionTypeFilter?>
        {
            public SegmentedFilter((string, InteractionTypeFilter?)[] items) : base(items) { }
        }
        [DesignerCategory("Code")]
        private sealed class SearchBox : WorkbenchSearch { }
        [DesignerCategory("Code")]
        private sealed class StateView : WorkbenchState { }
        private sealed class QuietMenuRenderer : WorkbenchMenuRenderer { }

    }
}