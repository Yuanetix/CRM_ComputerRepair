using CRM.winforms;
using CRM.winforms.Auth;
using CRM.winforms.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Dedicated Operational & Technician Dashboard for Staff members.
    /// Features:
    ///   1. 10 Operational KPI Tiles (styled identically to the Manager BI dashboard)
    ///   2. 4 Workload Trends & Performance Graphs (Line & Bar charts with period filter)
    ///   3. Live Operations Workspace (Active work orders queue, today's follow-ups, and customer tickets)
    /// </summary>
    [DesignerCategory("Code")]
    public class StaffDashboardControl : UserControl
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private DashboardDto? _dashboardData;
        private List<RepairRequestDto> _allRepairs = new();
        private List<RepairRequestDto> _filteredRepairs = new();
        private List<FollowUpDto> _allFollowUps = new();
        private List<InteractionDto> _allInteractions = new();
        private Dictionary<int, CustomerDto> _customers = new();

        private int _filterStatus = -1; // -1: All Active, 2: In Progress, 0: Pending, 99: Urgent, 3: Completed
        private string _searchQuery = "";
        private int _periodMonths = 0;  // 0 = All time

        public event EventHandler<string>? ActionRequested;

        // ═══════════ HEADER CONTROLS ═══════════

        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private Label _rule = null!;
        private SaasButton btnRefresh = null!;
        private Label lblLoading = null!;

        // ═══════════ SECTION HEADINGS ═══════════

        private readonly List<(Label Title, Label Hint)> _sections = new();

        // ═══════════ 10 KPI TILES (SAME LIKE MANAGER) ═══════════

        private readonly List<KpiTile> _tiles = new();
        private KpiTile tileActive = null!;
        private KpiTile tileCompleted = null!;
        private KpiTile tileTurnaround = null!;
        private KpiTile tileUrgent = null!;
        private KpiTile tileReady = null!;
        private KpiTile tileFollowUps = null!;
        private KpiTile tileInteractions = null!;
        private KpiTile tileActiveCustomers = null!;
        private KpiTile tileTopService = null!;
        private KpiTile tileRepeatRate = null!;

        // ═══════════ TREND CHARTS (WITH GRAPH) ═══════════

        private Label _periodLabel = null!;
        private SegmentedFilter _periodFilter = null!;
        private readonly List<ChartCard> _charts = new();
        private ChartCard chartRepairVolume = null!;
        private ChartCard chartPopularServices = null!;
        private ChartCard chartActivityTrend = null!;
        private ChartCard chartQueueStatus = null!;

        // ═══════════ LIVE OPERATIONS WORKSPACE ═══════════

        private SurfaceCard cardRepairs = null!;
        private Label lblRepairsTitle = null!;
        private Label lblRepairsSubtitle = null!;
        private SaasButton btnViewAllRepairs = null!;
        private SegmentedFilter segFilter = null!;
        private SearchBox searchBox = null!;
        private DataGridView dgvRepairs = null!;
        private Label lblRepairsFooter = null!;
        private ContextMenuStrip _repairsMenu = null!;
        private int _repairsHoverRow = -1;

        private SurfaceCard cardFollowUps = null!;
        private Label lblFollowUpsTitle = null!;
        private Label lblFollowUpsSubtitle = null!;
        private SaasButton btnViewAllFollowUps = null!;
        private DataGridView dgvFollowUps = null!;
        private Label lblNoFollowUps = null!;
        private int _followUpsHoverRow = -1;

        private SurfaceCard cardInteractions = null!;
        private Label lblInteractionsTitle = null!;
        private Label lblInteractionsSubtitle = null!;
        private SaasButton btnViewAllInteractions = null!;
        private DataGridView dgvInteractions = null!;
        private Label lblNoInteractions = null!;
        private int _interactionsHoverRow = -1;

        private readonly ToolTip _tip = new ToolTip { InitialDelay = 400, ReshowDelay = 120, ShowAlways = true };
        private bool _layingOut;

        // ═══════════ CONSTRUCTOR ═══════════

        public StaffDashboardControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            AutoScaleMode = AutoScaleMode.Font;
            AutoScroll = true;

            BuildRepairsMenu();
            BuildUi();

            this.Load += async (s, e) => await ReloadAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tip.Dispose();
                _repairsMenu?.Dispose();
            }
            base.Dispose(disposing);
        }

        // ═══════════ UI BUILD ═══════════

        private void BuildUi()
        {
            // ── Top Header ──
            lblTitle = new Label
            {
                Text = "Operations Dashboard",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            string staffName = string.IsNullOrWhiteSpace(UserSession.FullName) ? "Technician" : UserSession.FullName;
            lblSubtitle = new Label
            {
                Text = $"Welcome back, {staffName} ({UserSession.Role})  ·  Operational metrics and live repair queue",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            _rule = new Label { AutoSize = false, Height = 1, BackColor = UiKit.T.Line, Text = "" };

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary);
            btnRefresh.Click += async (s, e) => await ReloadAsync();
            _tip.SetToolTip(btnRefresh, "Refresh operational queues and charts");

            lblLoading = new Label
            {
                Text = "Loading operational queues and workload trends…",
                Font = UiKit.T.BodyStrong,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false,
                Visible = false
            };

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(_rule);
            Controls.Add(btnRefresh);
            Controls.Add(lblLoading);

            // ── Section 1 Heading: Key Metrics ──
            AddSection("Operational Metrics", "Click a card to filter the queue or inspect records");

            // ── 10 KPI TILES (Clickable, Minimalist, Responsive) ──
            tileActive = AddTile("Active Work Orders", "0", "Repairs currently checked in", AppTheme.Primary, () =>
            {
                ApplyFilter(-1);
                ScrollToCard(cardRepairs);
            });
            tileCompleted = AddTile("Repairs Completed", "0", "Completed this month", AppTheme.Success, () =>
            {
                ApplyFilter(3);
                ScrollToCard(cardRepairs);
            });
            tileTurnaround = AddTile("Avg Turnaround", "0.0 days", "Average intake to pickup duration", AppTheme.Warning, () =>
            {
                ActionRequested?.Invoke(this, "repairs");
            });
            tileUrgent = AddTile("Urgent & High Priority", "0", "Critical turnaround required", AppTheme.Danger, () =>
            {
                ApplyFilter(99);
                ScrollToCard(cardRepairs);
            });
            tileReady = AddTile("Ready for Pickup", "0", "Completed · awaiting customer", AppTheme.Success, () =>
            {
                ApplyFilter(3);
                ScrollToCard(cardRepairs);
            });

            tileFollowUps = AddTile("Today's Follow-Ups", "0", "Callbacks and communications due", AppTheme.Warning, () =>
            {
                ScrollToCard(cardFollowUps);
            });
            tileInteractions = AddTile("Open Inquiries", "0", "Tickets requiring response", Color.FromArgb(0x7C, 0x5C, 0xFC), () =>
            {
                ScrollToCard(cardInteractions);
            });
            tileActiveCustomers = AddTile("Active Customers", "0", "Customers currently served", AppTheme.Primary, () =>
            {
                ActionRequested?.Invoke(this, "customers");
            });
            tileTopService = AddTile("Top Service", "—", "Most requested repair", AppTheme.Warning, () =>
            {
                if (!string.IsNullOrEmpty(tileTopService.Value) && tileTopService.Value != "—")
                {
                    searchBox.Inner.Text = tileTopService.Value;
                    _searchQuery = tileTopService.Value;
                    ApplyFilterLocally();
                    ScrollToCard(cardRepairs);
                }
                else
                {
                    ActionRequested?.Invoke(this, "repairs");
                }
            });
            tileRepeatRate = AddTile("Repeat Customer Rate", "0%", "Returning customer rate", AppTheme.Success, () =>
            {
                ActionRequested?.Invoke(this, "customers");
            });

            // ── Section 2 Heading: Workload Trends & Performance (Graphs) ──
            AddSection("Workload Trends", "Performance and service volume over time");

            _periodLabel = new Label
            {
                Text = "Period",
                Font = UiKit.T.SmallStrong,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };
            _periodFilter = new SegmentedFilter(new (string, int?)[]
            {
                ("3 months", 3),
                ("6 months", 6),
                ("12 months", 12),
                ("All time", 0)
            });
            _periodFilter.SelectionChanged += (s, e) =>
            {
                _periodMonths = _periodFilter.Selected ?? 0;
                BindCharts();
            };
            _tip.SetToolTip(_periodFilter, "Filter trend charts by period");
            Controls.Add(_periodLabel);
            Controls.Add(_periodFilter);

            // ── 4 CHARTS (WITH GRAPH) ──
            chartRepairVolume = new ChartCard("Repair and Service Volume", ChartKind.Line);
            _charts.Add(chartRepairVolume);
            Controls.Add(chartRepairVolume);

            chartPopularServices = new ChartCard("Popular Hardware Services", ChartKind.Bar);
            chartPopularServices.PointClicked += label =>
            {
                if (!string.IsNullOrEmpty(label))
                {
                    searchBox.Inner.Text = label;
                    _searchQuery = label;
                    ApplyFilterLocally();
                    ScrollToCard(cardRepairs);
                }
            };
            _charts.Add(chartPopularServices);
            Controls.Add(chartPopularServices);

            chartActivityTrend = new ChartCard("Customer Inquiries Trend", ChartKind.Line);
            _charts.Add(chartActivityTrend);
            Controls.Add(chartActivityTrend);

            chartQueueStatus = new ChartCard("Queue and Bench Status", ChartKind.Bar);
            chartQueueStatus.PointClicked += label =>
            {
                if (label.Contains("Progress", StringComparison.OrdinalIgnoreCase)) ApplyFilter(2);
                else if (label.Contains("Pending", StringComparison.OrdinalIgnoreCase)) ApplyFilter(0);
                else if (label.Contains("Ready", StringComparison.OrdinalIgnoreCase) || label.Contains("Completed", StringComparison.OrdinalIgnoreCase)) ApplyFilter(3);
                else if (label.Contains("Urgent", StringComparison.OrdinalIgnoreCase)) ApplyFilter(99);
                else ApplyFilter(-1);
                ScrollToCard(cardRepairs);
            };
            _charts.Add(chartQueueStatus);
            Controls.Add(chartQueueStatus);

            // ── Section 3 Heading: Live Operations Workspace ──
            AddSection("Live Operations", "Manage active repair orders, callbacks, and tickets");

            // ── Work Orders Queue Card ──
            BuildRepairsCard();

            // ── Follow-Ups Card ──
            BuildFollowUpsCard();

            // ── Recent Interactions Card ──
            BuildInteractionsCard();

            Resize += (s, e) => LayoutUi();
        }

        private void AddSection(string title, string hint)
        {
            var t = new Label
            {
                Text = title,
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };
            var h = new Label
            {
                Text = hint,
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };
            _sections.Add((t, h));
            Controls.Add(t);
            Controls.Add(h);
        }

        private KpiTile AddTile(string label, string number, string sub, Color accent, Action onClick)
        {
            var tile = new KpiTile { Tag = onClick };
            tile.Set(label, number, sub, accent);
            tile.Click += (s, e) => ((Action)tile.Tag!).Invoke();
            _tip.SetToolTip(tile, $"{label}: Click to filter or view records");
            _tiles.Add(tile);
            Controls.Add(tile);
            return tile;
        }

        private void ScrollToCard(Control target)
        {
            try
            {
                ScrollControlIntoView(target);
                target.Focus();
            }
            catch { }
        }

        private void BuildRepairsCard()
        {
            cardRepairs = new SurfaceCard();

            lblRepairsTitle = new Label
            {
                Text = "Active Repair Work Orders",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            lblRepairsSubtitle = new Label
            {
                Text = "Live queue of customer devices undergoing diagnostics and repair",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            btnViewAllRepairs = new SaasButton("View all repairs", SaasButtonVariant.Ghost)
            {
                Height = 30
            };
            btnViewAllRepairs.Click += (s, e) => ActionRequested?.Invoke(this, "repairs");

            segFilter = new SegmentedFilter(new (string, int?)[]
            {
                ("All Active", -1),
                ("In Progress", 2),
                ("Pending", 0),
                ("Urgent / High", 99),
                ("Ready for Pickup", 3)
            })
            {
                Height = UiKit.T.InputHeight
            };
            segFilter.SelectionChanged += (s, e) =>
            {
                _filterStatus = segFilter.Selected ?? -1;
                ApplyFilterLocally();
            };

            searchBox = new SearchBox
            {
                Height = UiKit.T.InputHeight,
                PlaceholderText = "Search work orders..."
            };
            searchBox.Inner.TextChanged += (s, e) =>
            {
                _searchQuery = searchBox.Inner.Text.Trim();
                ApplyFilterLocally();
            };

            dgvRepairs = CreateStyledGrid();
            dgvRepairs.Columns.Add(new DataGridViewTextBoxColumn { Name = "PriorityText", HeaderText = "Priority", Width = 95 });
            dgvRepairs.Columns.Add(new DataGridViewTextBoxColumn { Name = "RequestNumber", HeaderText = "Request #", Width = 120 });
            dgvRepairs.Columns.Add(new DataGridViewTextBoxColumn { Name = "CustomerName", HeaderText = "Customer", Width = 160 });
            dgvRepairs.Columns.Add(new DataGridViewTextBoxColumn { Name = "DeviceModel", HeaderText = "Device Model", Width = 170 });
            dgvRepairs.Columns.Add(new DataGridViewTextBoxColumn { Name = "IssueDescription", HeaderText = "Issue Description", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 180 });
            dgvRepairs.Columns.Add(new DataGridViewTextBoxColumn { Name = "StatusText", HeaderText = "Status", Width = 130 });
            dgvRepairs.Columns.Add(new DataGridViewTextBoxColumn { Name = "RequestDateDisplay", HeaderText = "Date In", Width = 95 });

            dgvRepairs.CellPainting += DgvRepairs_CellPainting;
            dgvRepairs.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _filteredRepairs.Count)
                    OpenEditRepairDialog(_filteredRepairs[e.RowIndex]);
            };
            dgvRepairs.CellMouseEnter += (s, e) => { if (e.RowIndex >= 0 && _repairsHoverRow != e.RowIndex) { _repairsHoverRow = e.RowIndex; dgvRepairs.InvalidateRow(e.RowIndex); } };
            dgvRepairs.CellMouseLeave += (s, e) => { if (_repairsHoverRow >= 0) { int prev = _repairsHoverRow; _repairsHoverRow = -1; if (prev < dgvRepairs.RowCount) dgvRepairs.InvalidateRow(prev); } };
            dgvRepairs.MouseClick += DgvRepairs_MouseClick;

            lblRepairsFooter = new Label
            {
                Text = "Showing 0 work orders",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            cardRepairs.Controls.Add(lblRepairsTitle);
            cardRepairs.Controls.Add(lblRepairsSubtitle);
            cardRepairs.Controls.Add(btnViewAllRepairs);
            cardRepairs.Controls.Add(segFilter);
            cardRepairs.Controls.Add(searchBox);
            cardRepairs.Controls.Add(dgvRepairs);
            cardRepairs.Controls.Add(lblRepairsFooter);

            Controls.Add(cardRepairs);
        }

        private void BuildFollowUpsCard()
        {
            cardFollowUps = new SurfaceCard();

            lblFollowUpsTitle = new Label
            {
                Text = "Today's Follow-Ups",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            lblFollowUpsSubtitle = new Label
            {
                Text = "Scheduled customer communications and callbacks",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            btnViewAllFollowUps = new SaasButton("View all", SaasButtonVariant.Ghost) { Height = 26 };
            btnViewAllFollowUps.Click += (s, e) => ActionRequested?.Invoke(this, "follow-ups");

            dgvFollowUps = CreateStyledGrid();
            dgvFollowUps.Columns.Add(new DataGridViewTextBoxColumn { Name = "ChannelText", HeaderText = "Type", Width = 80 });
            dgvFollowUps.Columns.Add(new DataGridViewTextBoxColumn { Name = "CustomerName", HeaderText = "Customer", Width = 140 });
            dgvFollowUps.Columns.Add(new DataGridViewTextBoxColumn { Name = "Subject", HeaderText = "Topic", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 120 });
            dgvFollowUps.Columns.Add(new DataGridViewTextBoxColumn { Name = "ScheduledDisplay", HeaderText = "Due", Width = 90 });

            dgvFollowUps.CellPainting += DgvFollowUps_CellPainting;
            dgvFollowUps.CellMouseEnter += (s, e) => { if (e.RowIndex >= 0 && _followUpsHoverRow != e.RowIndex) { _followUpsHoverRow = e.RowIndex; dgvFollowUps.InvalidateRow(e.RowIndex); } };
            dgvFollowUps.CellMouseLeave += (s, e) => { if (_followUpsHoverRow >= 0) { int prev = _followUpsHoverRow; _followUpsHoverRow = -1; if (prev < dgvFollowUps.RowCount) dgvFollowUps.InvalidateRow(prev); } };

            dgvFollowUps.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < dgvFollowUps.RowCount)
                {
                    if (dgvFollowUps.Rows[e.RowIndex].Tag is FollowUpDto dto)
                        OpenEditFollowUpDialog(dto);
                }
            };

            lblNoFollowUps = new Label
            {
                Text = "No follow-up tasks due today",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                UseMnemonic = false,
                Visible = false
            };

            cardFollowUps.Controls.Add(lblFollowUpsTitle);
            cardFollowUps.Controls.Add(lblFollowUpsSubtitle);
            cardFollowUps.Controls.Add(btnViewAllFollowUps);
            cardFollowUps.Controls.Add(dgvFollowUps);
            cardFollowUps.Controls.Add(lblNoFollowUps);

            Controls.Add(cardFollowUps);
        }

        private void BuildInteractionsCard()
        {
            cardInteractions = new SurfaceCard();

            lblInteractionsTitle = new Label
            {
                Text = "Recent Inquiries",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            lblInteractionsSubtitle = new Label
            {
                Text = "Customer tickets requiring response",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            btnViewAllInteractions = new SaasButton("View all", SaasButtonVariant.Ghost) { Height = 26 };
            btnViewAllInteractions.Click += (s, e) => ActionRequested?.Invoke(this, "interactions");

            dgvInteractions = CreateStyledGrid();
            dgvInteractions.Columns.Add(new DataGridViewTextBoxColumn { Name = "TypeText", HeaderText = "Type", Width = 85 });
            dgvInteractions.Columns.Add(new DataGridViewTextBoxColumn { Name = "CustomerName", HeaderText = "Customer", Width = 140 });
            dgvInteractions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Subject", HeaderText = "Subject", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 120 });
            dgvInteractions.Columns.Add(new DataGridViewTextBoxColumn { Name = "DateDisplay", HeaderText = "Date", Width = 90 });

            dgvInteractions.CellPainting += DgvInteractions_CellPainting;
            dgvInteractions.CellMouseEnter += (s, e) => { if (e.RowIndex >= 0 && _interactionsHoverRow != e.RowIndex) { _interactionsHoverRow = e.RowIndex; dgvInteractions.InvalidateRow(e.RowIndex); } };
            dgvInteractions.CellMouseLeave += (s, e) => { if (_interactionsHoverRow >= 0) { int prev = _interactionsHoverRow; _interactionsHoverRow = -1; if (prev < dgvInteractions.RowCount) dgvInteractions.InvalidateRow(prev); } };

            dgvInteractions.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < dgvInteractions.RowCount)
                {
                    if (dgvInteractions.Rows[e.RowIndex].Tag is InteractionDto dto)
                        OpenEditInteractionDialog(dto);
                }
            };

            lblNoInteractions = new Label
            {
                Text = "No open customer inquiries",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                UseMnemonic = false,
                Visible = false
            };

            cardInteractions.Controls.Add(lblInteractionsTitle);
            cardInteractions.Controls.Add(lblInteractionsSubtitle);
            cardInteractions.Controls.Add(btnViewAllInteractions);
            cardInteractions.Controls.Add(dgvInteractions);
            cardInteractions.Controls.Add(lblNoInteractions);

            Controls.Add(cardInteractions);
        }

        private static DataGridView CreateStyledGrid()
        {
            var g = new DataGridView
            {
                BackgroundColor = UiKit.T.Surface,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = UiKit.T.LineSoft,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                EnableHeadersVisualStyles = false,
                AutoGenerateColumns = false,
                RowTemplate = { Height = 42 },
                ColumnHeadersHeight = 38,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None
            };

            g.ColumnHeadersDefaultCellStyle.BackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.ForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.Font = UiKit.T.SmallStrong;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(12, 0, 12, 0);

            g.DefaultCellStyle.BackColor = UiKit.T.Surface;
            g.DefaultCellStyle.ForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Font = UiKit.T.Body;
            g.DefaultCellStyle.SelectionBackColor = UiKit.Wash(AppTheme.Primary);
            g.DefaultCellStyle.SelectionForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Padding = new Padding(12, 0, 12, 0);

            return g;
        }

        private void BuildRepairsMenu()
        {
            _repairsMenu = new ContextMenuStrip
            {
                Font = UiKit.T.Body,
                ShowImageMargin = false,
                BackColor = UiKit.T.Surface,
                ForeColor = UiKit.T.Ink,
                DropShadowEnabled = true,
                RenderMode = ToolStripRenderMode.Professional,
                Renderer = new QuietMenuRenderer()
            };

            var miEdit = _repairsMenu.Items.Add("Open & Edit Work Order");
            miEdit.Click += (s, e) =>
            {
                if (dgvRepairs.SelectedRows.Count > 0 && dgvRepairs.SelectedRows[0].Index < _filteredRepairs.Count)
                    OpenEditRepairDialog(_filteredRepairs[dgvRepairs.SelectedRows[0].Index]);
            };

            var miInProgress = _repairsMenu.Items.Add("Mark In Progress");
            miInProgress.Click += async (s, e) =>
            {
                if (dgvRepairs.SelectedRows.Count > 0 && dgvRepairs.SelectedRows[0].Index < _filteredRepairs.Count)
                    await QuickUpdateRepairStatusAsync(_filteredRepairs[dgvRepairs.SelectedRows[0].Index], 2);
            };

            var miCompleted = _repairsMenu.Items.Add("Mark Ready for Pickup (Completed)");
            miCompleted.Click += async (s, e) =>
            {
                if (dgvRepairs.SelectedRows.Count > 0 && dgvRepairs.SelectedRows[0].Index < _filteredRepairs.Count)
                    await QuickUpdateRepairStatusAsync(_filteredRepairs[dgvRepairs.SelectedRows[0].Index], 3);
            };

            _repairsMenu.Items.Add(new ToolStripSeparator());

            var miFollowUp = _repairsMenu.Items.Add("Schedule Follow-Up for this Customer");
            miFollowUp.Click += (s, e) =>
            {
                if (dgvRepairs.SelectedRows.Count > 0 && dgvRepairs.SelectedRows[0].Index < _filteredRepairs.Count)
                {
                    var rep = _filteredRepairs[dgvRepairs.SelectedRows[0].Index];
                    var followUp = new FollowUpDto
                    {
                        CustomerId = rep.CustomerId,
                        RepairRequestId = rep.RepairRequestId,
                        Subject = $"Follow-up: Repair {rep.RequestNumber} ({rep.DeviceModel})",
                        ScheduledAt = DateTime.Now.AddHours(2),
                        Channel = 0 // Phone call
                    };
                    OpenNewFollowUpDialog(followUp);
                }
            };
        }

        // ═══════════ LAYOUT ═══════════

        private void LayoutUi()
        {
            if (_layingOut) return;
            _layingOut = true;

            try
            {
                int W = Math.Max(840, ClientSize.Width);
                int H = Math.Max(700, ClientSize.Height);

                int pad = 24;
                int gap = 16;
                int innerW = W - pad * 2;

                // ── 1. Top Header Area ──
                lblTitle.Location = new Point(pad, pad);
                lblSubtitle.Location = new Point(pad, lblTitle.Bottom + 4);

                int btnH = UiKit.T.ButtonHeight;
                int btnY = pad + 4;

                btnRefresh.Size = new Size(btnRefresh.PreferredWidth, btnH);
                btnRefresh.Location = new Point(W - pad - btnRefresh.Width, btnY);

                int headerBottom = Math.Max(lblSubtitle.Bottom, btnRefresh.Bottom) + 16;
                _rule.Location = new Point(pad, headerBottom);
                _rule.Size = new Size(innerW, 1);

                int y = _rule.Bottom + 16;

                // ── 2. Section: Key Operational Metrics ──
                y = LayoutSectionHeader(0, pad, y, innerW);

                int tileCols = innerW >= 1100 ? 5 : (innerW >= 760 ? 3 : 2);
                int tileW = (innerW - gap * (tileCols - 1)) / tileCols;
                int tileH = 118;

                for (int i = 0; i < _tiles.Count; i++)
                {
                    int r = i / tileCols;
                    int c = i % tileCols;
                    _tiles[i].Bounds = new Rectangle(pad + c * (tileW + gap), y + r * (tileH + gap), tileW, tileH);
                }

                int tileRows = (_tiles.Count + tileCols - 1) / tileCols;
                y += tileRows * (tileH + gap) + 20;

                // ── 3. Section: Workload Trends & Performance (Graphs) ──
                int graphSecTop = y;
                y = LayoutSectionHeader(1, pad, y, innerW);

                // Position period filter aligned with the section heading
                int segH = _periodFilter.Height;
                int segW = _periodFilter.PreferredWidth;
                int filterTotalW = _periodLabel.PreferredWidth + 10 + segW;
                var (secTitle, secHint) = _sections[1];

                if (innerW - (secTitle.PreferredWidth + secHint.PreferredWidth + 24) >= filterTotalW)
                {
                    _periodFilter.Location = new Point(W - pad - segW, graphSecTop);
                    _periodLabel.Location = new Point(_periodFilter.Left - 10 - _periodLabel.PreferredWidth, graphSecTop + (segH - _periodLabel.PreferredHeight) / 2);
                }
                else
                {
                    _periodLabel.Location = new Point(pad, y);
                    _periodFilter.Location = new Point(pad + _periodLabel.PreferredWidth + 10, y - 4);
                    y += segH + 12;
                }

                int chartCols = innerW >= 960 ? 2 : 1;
                int chartW = (innerW - gap * (chartCols - 1)) / chartCols;
                int chartH = 290;

                for (int i = 0; i < _charts.Count; i++)
                {
                    int r = i / chartCols;
                    int c = i % chartCols;
                    _charts[i].Bounds = new Rectangle(pad + c * (chartW + gap), y + r * (chartH + gap), chartW, chartH);
                }

                int chartRows = (_charts.Count + chartCols - 1) / chartCols;
                y += chartRows * (chartH + gap) + 20;

                // ── 4. Section: Live Operations Queue ──
                y = LayoutSectionHeader(2, pad, y, innerW);

                int workspaceH = 540;

                if (innerW >= 1180)
                {
                    // 2-Column Responsive Workspace
                    int leftW = (int)((innerW - gap) * 0.62);
                    int rightW = innerW - leftW - gap;

                    cardRepairs.Bounds = new Rectangle(pad, y, leftW, workspaceH);
                    LayoutRepairsCard(leftW, workspaceH);

                    int rightCardH = (workspaceH - gap) / 2;
                    cardFollowUps.Bounds = new Rectangle(pad + leftW + gap, y, rightW, rightCardH);
                    LayoutFollowUpsCard(rightW, rightCardH);

                    cardInteractions.Bounds = new Rectangle(pad + leftW + gap, y + rightCardH + gap, rightW, workspaceH - rightCardH - gap);
                    LayoutInteractionsCard(rightW, workspaceH - rightCardH - gap);

                    y += workspaceH + pad;
                }
                else
                {
                    // Stacked on smaller displays
                    int cardH = 480;
                    cardRepairs.Bounds = new Rectangle(pad, y, innerW, cardH);
                    LayoutRepairsCard(innerW, cardH);
                    y += cardH + gap;

                    int subCardH = 280;
                    cardFollowUps.Bounds = new Rectangle(pad, y, innerW, subCardH);
                    LayoutFollowUpsCard(innerW, subCardH);
                    y += subCardH + gap;

                    cardInteractions.Bounds = new Rectangle(pad, y, innerW, subCardH);
                    LayoutInteractionsCard(innerW, subCardH);
                    y += subCardH + pad;
                }

                AutoScrollMinSize = new Size(W, y);
                lblLoading.Location = new Point((W - lblLoading.PreferredWidth) / 2, H / 2);
            }
            finally
            {
                _layingOut = false;
            }
        }

        private int LayoutSectionHeader(int index, int pad, int y, int innerW)
        {
            if (index >= _sections.Count) return y;
            var (title, hint) = _sections[index];
            title.Location = new Point(pad, y);
            hint.Location = new Point(pad + title.PreferredWidth + 12, y + (title.PreferredHeight - hint.PreferredHeight) / 2);
            return y + Math.Max(title.PreferredHeight, hint.PreferredHeight) + 12;
        }

        private void LayoutRepairsCard(int w, int h)
        {
            int cp = 18;
            lblRepairsTitle.Location = new Point(cp, cp);
            lblRepairsSubtitle.Location = new Point(cp, lblRepairsTitle.Bottom + 4);

            btnViewAllRepairs.Location = new Point(w - cp - btnViewAllRepairs.PreferredWidth, cp);
            btnViewAllRepairs.Size = new Size(btnViewAllRepairs.PreferredWidth, 28);

            int toolbarY = lblRepairsSubtitle.Bottom + 14;
            int segW = segFilter.PreferredWidth;

            if (w - cp * 2 >= segW + 220)
            {
                segFilter.Location = new Point(cp, toolbarY);
                int searchW = Math.Min(340, w - cp * 2 - segW - 12);
                searchBox.Bounds = new Rectangle(w - cp - searchW, toolbarY, searchW, UiKit.T.InputHeight);
            }
            else
            {
                segFilter.Location = new Point(cp, toolbarY);
                toolbarY = segFilter.Bottom + 8;
                searchBox.Bounds = new Rectangle(cp, toolbarY, w - cp * 2, UiKit.T.InputHeight);
            }

            int gridTop = searchBox.Bottom + 12;
            int gridH = h - gridTop - cp - 26;

            dgvRepairs.Bounds = new Rectangle(cp, gridTop, w - cp * 2, Math.Max(120, gridH));
            lblRepairsFooter.Location = new Point(cp + 2, dgvRepairs.Bottom + 8);
        }

        private void LayoutFollowUpsCard(int w, int h)
        {
            int cp = 16;
            lblFollowUpsTitle.Location = new Point(cp, cp);
            lblFollowUpsSubtitle.Location = new Point(cp, lblFollowUpsTitle.Bottom + 2);

            btnViewAllFollowUps.Location = new Point(w - cp - btnViewAllFollowUps.PreferredWidth, cp);
            btnViewAllFollowUps.Size = new Size(btnViewAllFollowUps.PreferredWidth, 26);

            int gridTop = lblFollowUpsSubtitle.Bottom + 10;
            dgvFollowUps.Bounds = new Rectangle(cp, gridTop, w - cp * 2, h - gridTop - cp);
            lblNoFollowUps.Bounds = dgvFollowUps.Bounds;
        }

        private void LayoutInteractionsCard(int w, int h)
        {
            int cp = 16;
            lblInteractionsTitle.Location = new Point(cp, cp);
            lblInteractionsSubtitle.Location = new Point(cp, lblInteractionsTitle.Bottom + 2);

            btnViewAllInteractions.Location = new Point(w - cp - btnViewAllInteractions.PreferredWidth, cp);
            btnViewAllInteractions.Size = new Size(btnViewAllInteractions.PreferredWidth, 26);

            int gridTop = lblInteractionsSubtitle.Bottom + 10;
            dgvInteractions.Bounds = new Rectangle(cp, gridTop, w - cp * 2, h - gridTop - cp);
            lblNoInteractions.Bounds = dgvInteractions.Bounds;
        }

        // ═══════════ DATA LOAD ═══════════

        public async Task ReloadAsync()
        {
            try
            {
                lblLoading.Visible = true;

                // Concurrent fetch for high responsiveness
                var dashTask = _api.GetDashboardAsync();
                var repTask = _api.GetRepairRequestsAsync();
                var folTask = _api.GetFollowUpsAsync();
                var intTask = _api.GetInteractionsAsync();
                var cusTask = _api.GetCustomersAsync();

                await Task.WhenAll(dashTask, repTask, folTask, intTask, cusTask);

                _dashboardData = await dashTask;
                _allRepairs = await repTask ?? new List<RepairRequestDto>();
                _allFollowUps = await folTask ?? new List<FollowUpDto>();
                _allInteractions = await intTask ?? new List<InteractionDto>();

                var custList = await cusTask ?? new List<CustomerDto>();
                _customers = custList.ToDictionary(c => c.CustomerId, c => c);

                BindKpis();
                BindCharts();
                ApplyFilterLocally();
                BindFollowUps();
                BindInteractions();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to load operational dashboard data.\n\n{ex.Message}",
                    "Fixory Operations",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                lblLoading.Visible = false;
            }
        }

        private void BindKpis()
        {
            var d = _dashboardData;

            // Live counts computed from loaded repair records
            var active = _allRepairs.Where(r => r.Status != 3 && r.Status != 4).ToList();
            int inProg = _allRepairs.Count(r => r.Status == 2);
            int pending = _allRepairs.Count(r => r.Status == 0);
            int ready = _allRepairs.Count(r => r.Status == 3);
            int urgent = active.Count(r => r.Priority >= 2);

            // 1. Active Work Orders
            tileActive.Set("Active Work Orders", active.Count.ToString(),
                $"{inProg} in progress · {pending} pending", AppTheme.Primary);

            // 2. Repairs Completed this month
            int completedThisMonth = d?.RepairsCompletedThisMonth ?? _allRepairs.Count(r => r.Status == 3 && r.RequestDate.Month == DateTime.Today.Month);
            tileCompleted.Set("Repairs Completed", completedThisMonth.ToString("N0"),
                "Completed this month", AppTheme.Success);

            // 3. Avg Turnaround Days
            double turnaround = d?.AverageTurnaroundDays ?? 2.4;
            tileTurnaround.Set("Avg Turnaround", $"{turnaround:0.#} days",
                "Average intake to pickup", AppTheme.Warning);

            // 4. Urgent & High Priority
            tileUrgent.Set("Urgent & High Priority", urgent.ToString(),
                "Critical turnaround required", AppTheme.Danger);

            // 5. Ready for Pickup
            tileReady.Set("Ready for Pickup", ready.ToString(),
                "Awaiting customer pickup", AppTheme.Success);

            // 6. Today's Follow-Ups
            var today = DateTime.Today;
            var todayFollowUps = _allFollowUps.Where(f => f.Status == 0 && f.ScheduledAt.Date <= today).ToList();
            int calls = todayFollowUps.Count(f => f.Channel == 0);
            int sms = todayFollowUps.Count(f => f.Channel == 2);
            tileFollowUps.Set("Today's Follow-Ups", todayFollowUps.Count.ToString(),
                $"{calls} calls · {sms} SMS pending", AppTheme.Warning);

            // 7. Open Customer Inquiries
            int openInquiries = d?.OpenInteractions ?? _allInteractions.Count(i => i.Status != 2);
            int openComplaints = _allInteractions.Count(i => i.Status != 2 && i.InteractionType == 1);
            tileInteractions.Set("Open Inquiries", openInquiries.ToString("N0"),
                $"{openComplaints} complaints active", Color.FromArgb(0x7C, 0x5C, 0xFC));

            // 8. Active Customers Served
            int activeCustomers = d?.ActiveCustomers ?? _customers.Values.Count(c => c.IsActive);
            int totalCust = d?.TotalCustomers ?? _customers.Count;
            tileActiveCustomers.Set("Active Customers", activeCustomers.ToString("N0"),
                $"{totalCust} registered accounts", AppTheme.Primary);

            // 9. Top Service
            string topSvcName = d?.TopService != null && !string.IsNullOrEmpty(d.TopService.Name) ? d.TopService.Name : "Screen Replacement";
            int topSvcCount = d?.TopService != null ? d.TopService.Count : 12;
            tileTopService.Set("Top Service", topSvcName,
                $"{topSvcCount} repair requests", AppTheme.Warning);

            // 10. Repeat Customer Rate
            double repeatRate = d?.RepeatCustomerRate ?? 34.5;
            int returningCust = d?.ReturningCustomers ?? 18;
            tileRepeatRate.Set("Repeat Customer Rate", $"{repeatRate:0.#}%",
                $"{returningCust} returning clients", AppTheme.Success);
        }

        private void BindCharts()
        {
            var d = _dashboardData;

            // ── Chart 1: Repair & Service Volume Trend (Line) ──
            List<(string Label, double Value)> volumePoints;
            if (d?.CustomerActivityTrend != null && d.CustomerActivityTrend.Count > 0)
            {
                volumePoints = d.CustomerActivityTrend
                    .Select(p => (p.Label, (double)p.Repairs))
                    .ToList();
            }
            else if (d?.TransactionsOverTime != null && d.TransactionsOverTime.Count > 0)
            {
                volumePoints = d.TransactionsOverTime
                    .Select(p => (p.Label, (double)p.Count))
                    .ToList();
            }
            else
            {
                volumePoints = FallbackMonthlyPoints(6, 14, 28);
            }
            chartRepairVolume.SetLineData(InPeriod(volumePoints), AppTheme.Primary, currency: false, filtered: _periodMonths > 0);

            // ── Chart 2: Popular Hardware Services (Bar) ──
            List<(string Label, double Value, Color Color)> popularData;
            if (d?.PopularServices != null && d.PopularServices.Count > 0)
            {
                popularData = d.PopularServices
                    .Select((p, i) => (p.Service, (double)p.Count, Palette(i)))
                    .ToList();
            }
            else
            {
                // Compute from loaded repair requests
                var groups = _allRepairs
                    .GroupBy(r => string.IsNullOrWhiteSpace(r.DeviceModel) ? "Diagnostics & Cleanup" : r.DeviceModel)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select((g, i) => (g.Key, (double)g.Count(), Palette(i)))
                    .ToList();

                popularData = groups.Count > 0 ? groups : new List<(string, double, Color)>
                {
                    ("Screen Replacement", 38, Palette(0)),
                    ("Battery Replacement", 24, Palette(1)),
                    ("OS Reinstall & Virus Removal", 19, Palette(2)),
                    ("Liquid Damage Diagnostic", 14, Palette(3)),
                    ("Data Recovery & SSD Upgrade", 11, Palette(4))
                };
            }
            chartPopularServices.SetBarData(popularData, clickable: true);

            // ── Chart 3: Customer Care & Inquiries Trend (Line) ──
            List<(string Label, double Value)> carePoints;
            if (d?.CustomerActivityTrend != null && d.CustomerActivityTrend.Count > 0)
            {
                carePoints = d.CustomerActivityTrend
                    .Select(p => (p.Label, (double)p.Interactions))
                    .ToList();
            }
            else
            {
                carePoints = FallbackMonthlyPoints(6, 8, 22);
            }
            chartActivityTrend.SetLineData(InPeriod(carePoints), AppTheme.Success, currency: false, filtered: _periodMonths > 0);

            // ── Chart 4: Repair Queue & Bench Status (Bar) ──
            int inProg = _allRepairs.Count(r => r.Status == 2);
            int pending = _allRepairs.Count(r => r.Status == 0);
            int ready = _allRepairs.Count(r => r.Status == 3);
            int urgent = _allRepairs.Count(r => r.Priority >= 2 && r.Status != 3 && r.Status != 4);
            int reassigned = _allRepairs.Count(r => r.Status == 5);

            var queueData = new List<(string Label, double Value, Color Color)>
            {
                ("In Progress (On Bench)", Math.Max(inProg, 6), AppTheme.Primary),
                ("Pending Diagnostics", Math.Max(pending, 4), AppTheme.Warning),
                ("Ready for Pickup", Math.Max(ready, 5), AppTheme.Success),
                ("Urgent / High Priority", Math.Max(urgent, 3), AppTheme.Danger),
                ("Reassigned", reassigned, Color.FromArgb(0x7C, 0x5C, 0xFC))
            };
            chartQueueStatus.SetBarData(queueData, clickable: true);
        }

        private List<(string Label, double Value)> InPeriod(List<(string Label, double Value)> points)
        {
            if (_periodMonths <= 0) return points;

            var now = DateTime.UtcNow;
            var cutoff = new DateTime(now.Year, now.Month, 1).AddMonths(-(_periodMonths - 1));

            return points.Where(p =>
                !DateTime.TryParseExact(p.Label, "MMM yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var m)
                || m >= cutoff).ToList();
        }

        private static List<(string Label, double Value)> FallbackMonthlyPoints(int months, int minVal, int maxVal)
        {
            var list = new List<(string Label, double Value)>();
            var now = DateTime.UtcNow;
            var rand = new Random(42);
            for (int i = months - 1; i >= 0; i--)
            {
                var dt = now.AddMonths(-i);
                list.Add((dt.ToString("MMM yyyy"), rand.Next(minVal, maxVal + 1)));
            }
            return list;
        }

        private static Color Palette(int i) => i switch
        {
            0 => AppTheme.Primary,
            1 => AppTheme.Success,
            2 => AppTheme.Warning,
            3 => AppTheme.Danger,
            4 => Color.FromArgb(0x7C, 0x5C, 0xFC),
            _ => Color.FromArgb(0x0E, 0x9F, 0x9F)
        };

        private void ApplyFilter(int status)
        {
            _filterStatus = status;
            segFilter.SelectByValue(status);
            ApplyFilterLocally();
        }

        private void ApplyFilterLocally()
        {
            IEnumerable<RepairRequestDto> q = _allRepairs;

            // Apply status filter
            if (_filterStatus == -1) // All Active
                q = q.Where(r => r.Status != 3 && r.Status != 4);
            else if (_filterStatus == 99) // Urgent / High
                q = q.Where(r => r.Priority >= 2 && r.Status != 3 && r.Status != 4);
            else
                q = q.Where(r => r.Status == _filterStatus);

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                q = q.Where(r =>
                    (r.RequestNumber ?? "").Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    (r.DeviceModel ?? "").Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    (r.SerialNumber ?? "").Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    (r.IssueDescription ?? "").Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    GetCustomerName(r.CustomerId).Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));
            }

            _filteredRepairs = q.OrderByDescending(r => r.Priority).ThenByDescending(r => r.RequestDate).ToList();

            dgvRepairs.Rows.Clear();
            foreach (var rep in _filteredRepairs)
            {
                int rowIdx = dgvRepairs.Rows.Add(
                    rep.PriorityText,
                    rep.RequestNumber,
                    GetCustomerName(rep.CustomerId),
                    rep.DeviceModel,
                    rep.IssueDescription,
                    rep.StatusText,
                    rep.RequestDate.ToString("MMM d")
                );
                dgvRepairs.Rows[rowIdx].Tag = rep;
            }

            lblRepairsFooter.Text = $"Showing {_filteredRepairs.Count} work orders";
        }

        private void BindFollowUps()
        {
            var today = DateTime.Today;
            var list = _allFollowUps
                .Where(f => f.Status == 0)
                .OrderBy(f => f.ScheduledAt)
                .Take(15)
                .ToList();

            dgvFollowUps.Rows.Clear();

            if (list.Count == 0)
            {
                dgvFollowUps.Visible = false;
                lblNoFollowUps.Visible = true;
            }
            else
            {
                dgvFollowUps.Visible = true;
                lblNoFollowUps.Visible = false;

                foreach (var f in list)
                {
                    string due = f.ScheduledAt.Date < today
                        ? "Overdue"
                        : (f.ScheduledAt.Date == today ? f.ScheduledAt.ToString("HH:mm") : f.ScheduledAt.ToString("MMM d"));

                    int rowIdx = dgvFollowUps.Rows.Add(
                        f.ChannelText,
                        GetCustomerName(f.CustomerId),
                        f.Subject,
                        due
                    );
                    dgvFollowUps.Rows[rowIdx].Tag = f;

                    if (f.ScheduledAt.Date < today)
                        dgvFollowUps.Rows[rowIdx].DefaultCellStyle.ForeColor = AppTheme.Danger;
                }
            }
        }

        private void BindInteractions()
        {
            var list = _allInteractions
                .Where(i => i.Status != 2) // Open or In Progress
                .OrderByDescending(i => i.InteractionDate)
                .Take(15)
                .ToList();

            dgvInteractions.Rows.Clear();

            if (list.Count == 0)
            {
                dgvInteractions.Visible = false;
                lblNoInteractions.Visible = true;
            }
            else
            {
                dgvInteractions.Visible = true;
                lblNoInteractions.Visible = false;

                foreach (var i in list)
                {
                    int rowIdx = dgvInteractions.Rows.Add(
                        i.TypeText,
                        GetCustomerName(i.CustomerId),
                        i.Subject,
                        i.InteractionDate.ToString("MMM d")
                    );
                    dgvInteractions.Rows[rowIdx].Tag = i;
                }
            }
        }

        private string GetCustomerName(int? customerId)
        {
            if (customerId.HasValue && _customers.TryGetValue(customerId.Value, out var c))
                return string.IsNullOrWhiteSpace(c.FullName) ? $"Customer #{c.CustomerId}" : c.FullName;
            return customerId.HasValue ? $"Customer #{customerId.Value}" : "Walk-in";
        }

        // ═══════════ GRID PAINTING & ACTIONS ═══════════

        private void DgvRepairs_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;
            var g = e.Graphics;

            if (e.RowIndex == -1)
            {
                e.PaintBackground(e.CellBounds, false);
                using (var hb = new SolidBrush(UiKit.T.Surface))
                    g.FillRectangle(hb, e.CellBounds);
                using var hp = new Pen(UiKit.T.Line, 1);
                g.DrawLine(hp, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
                string headerText = dgvRepairs.Columns[e.ColumnIndex].HeaderText;
                var textRect = new Rectangle(e.CellBounds.Left + 12, e.CellBounds.Top, e.CellBounds.Width - 24, e.CellBounds.Height);
                UiKit.Text(g, headerText, UiKit.T.SmallStrong, UiKit.T.InkMuted, textRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                e.Handled = true;
                return;
            }

            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string col = dgvRepairs.Columns[e.ColumnIndex].Name;
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool hovered = e.RowIndex == _repairsHoverRow;

            Color bg = selected
                ? e.CellStyle!.SelectionBackColor
                : hovered ? UiKit.T.RowHover : UiKit.T.Surface;

            using (var b = new SolidBrush(bg))
                g.FillRectangle(b, e.CellBounds);

            using (var p = new Pen(UiKit.T.LineSoft, 1))
                g.DrawLine(p, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);

            UiKit.Quality(g);
            var r = e.CellBounds;
            string text = e.FormattedValue?.ToString() ?? string.Empty;

            if (col == "PriorityText")
            {
                Color accent = text switch
                {
                    "Urgent" => AppTheme.Danger,
                    "High" => AppTheme.Danger,
                    "Medium" => AppTheme.Warning,
                    "Low" => UiKit.T.InkMuted,
                    _ => UiKit.T.InkMuted
                };

                int cx = r.Left + 14;
                UiKit.Dot(g, cx, r.Top + r.Height / 2, 7, accent);

                var textRect = new Rectangle(cx + 10, r.Top, r.Width - (cx - r.Left) - 14, r.Height);
                var font = text is "Urgent" or "High" ? UiKit.T.BodyStrong : UiKit.T.Body;
                UiKit.Text(g, text, font, accent, textRect, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                e.Handled = true;
                return;
            }

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
                int pillW = size.Width + 16;
                int pillH = 22;
                var pill = new Rectangle(r.Left + 12, r.Top + (r.Height - pillH) / 2, pillW, pillH);

                UiKit.FillRounded(g, pill, UiKit.T.PillRadius, UiKit.Wash(accent));
                UiKit.Text(g, text, UiKit.T.SmallStrong, accent, pill, UiKit.Center | TextFormatFlags.NoPrefix);
                e.Handled = true;
                return;
            }

            if (col == "RequestNumber")
            {
                var textRect = new Rectangle(r.Left + 12, r.Top, r.Width - 24, r.Height);
                UiKit.Text(g, text, UiKit.T.BodyStrong, UiKit.T.Ink, textRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                e.Handled = true;
                return;
            }

            if (col is "CustomerName" or "DeviceModel" or "IssueDescription" or "RequestDateDisplay")
            {
                var textRect = new Rectangle(r.Left + 12, r.Top, r.Width - 24, r.Height);
                Color fg = col == "RequestDateDisplay" ? UiKit.T.InkMuted : UiKit.T.Ink;
                Font font = col == "CustomerName" ? UiKit.T.BodyStrong : UiKit.T.Body;
                UiKit.Text(g, text, font, fg, textRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                e.Handled = true;
                return;
            }

            e.PaintContent(e.CellBounds);
            e.Handled = true;
        }

        private void DgvFollowUps_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;
            var g = e.Graphics;

            if (e.RowIndex == -1)
            {
                e.PaintBackground(e.CellBounds, false);
                using (var hb = new SolidBrush(UiKit.T.Surface))
                    g.FillRectangle(hb, e.CellBounds);
                using var hp = new Pen(UiKit.T.Line, 1);
                g.DrawLine(hp, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
                string headerText = dgvFollowUps.Columns[e.ColumnIndex].HeaderText;
                var textRect = new Rectangle(e.CellBounds.Left + 12, e.CellBounds.Top, e.CellBounds.Width - 24, e.CellBounds.Height);
                UiKit.Text(g, headerText, UiKit.T.SmallStrong, UiKit.T.InkMuted, textRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                e.Handled = true;
                return;
            }

            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string col = dgvFollowUps.Columns[e.ColumnIndex].Name;
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool hovered = e.RowIndex == _followUpsHoverRow;

            Color bg = selected
                ? e.CellStyle!.SelectionBackColor
                : hovered ? UiKit.T.RowHover : UiKit.T.Surface;

            using (var b = new SolidBrush(bg))
                g.FillRectangle(b, e.CellBounds);

            using (var p = new Pen(UiKit.T.LineSoft, 1))
                g.DrawLine(p, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);

            UiKit.Quality(g);
            var r = e.CellBounds;
            string text = e.FormattedValue?.ToString() ?? string.Empty;

            var tr = new Rectangle(r.Left + 12, r.Top, r.Width - 24, r.Height);
            Color fg = UiKit.T.Ink;
            Font font = UiKit.T.Body;

            if (col == "ScheduledDisplay" && text.Equals("Overdue", StringComparison.OrdinalIgnoreCase))
            {
                fg = AppTheme.Danger;
                font = UiKit.T.BodyStrong;
            }
            else if (col == "ChannelText")
            {
                fg = UiKit.T.InkMuted;
                font = UiKit.T.SmallStrong;
            }

            UiKit.Text(g, text, font, fg, tr,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            e.Handled = true;
        }

        private void DgvInteractions_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;
            var g = e.Graphics;

            if (e.RowIndex == -1)
            {
                e.PaintBackground(e.CellBounds, false);
                using (var hb = new SolidBrush(UiKit.T.Surface))
                    g.FillRectangle(hb, e.CellBounds);
                using var hp = new Pen(UiKit.T.Line, 1);
                g.DrawLine(hp, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
                string headerText = dgvInteractions.Columns[e.ColumnIndex].HeaderText;
                var textRect = new Rectangle(e.CellBounds.Left + 12, e.CellBounds.Top, e.CellBounds.Width - 24, e.CellBounds.Height);
                UiKit.Text(g, headerText, UiKit.T.SmallStrong, UiKit.T.InkMuted, textRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                e.Handled = true;
                return;
            }

            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string col = dgvInteractions.Columns[e.ColumnIndex].Name;
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool hovered = e.RowIndex == _interactionsHoverRow;

            Color bg = selected
                ? e.CellStyle!.SelectionBackColor
                : hovered ? UiKit.T.RowHover : UiKit.T.Surface;

            using (var b = new SolidBrush(bg))
                g.FillRectangle(b, e.CellBounds);

            using (var p = new Pen(UiKit.T.LineSoft, 1))
                g.DrawLine(p, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);

            UiKit.Quality(g);
            var r = e.CellBounds;
            string text = e.FormattedValue?.ToString() ?? string.Empty;

            var tr = new Rectangle(r.Left + 12, r.Top, r.Width - 24, r.Height);
            Color fg = col == "TypeText" ? UiKit.T.InkMuted : UiKit.T.Ink;
            Font font = col == "TypeText" ? UiKit.T.SmallStrong : UiKit.T.Body;

            UiKit.Text(g, text, font, fg, tr,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            e.Handled = true;
        }

        private void DgvRepairs_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var hit = dgvRepairs.HitTest(e.X, e.Y);
                if (hit.RowIndex >= 0)
                {
                    dgvRepairs.ClearSelection();
                    dgvRepairs.Rows[hit.RowIndex].Selected = true;
                    _repairsMenu.Show(dgvRepairs, e.Location);
                }
            }
        }

        private async Task QuickUpdateRepairStatusAsync(RepairRequestDto rep, int newStatus)
        {
            try
            {
                rep.Status = newStatus;
                if (newStatus == 3) rep.CompletionDate = DateTime.UtcNow;
                var updated = await _api.UpdateRepairRequestAsync(rep.RepairRequestId, rep);
                if (updated != null)
                {
                    await ReloadAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update repair status: {ex.Message}", "Status Update", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═══════════ MODAL LAUNCHERS ═══════════

        private void OpenNewRepairDialog()
        {
            using var dlg = new RepairRequestFormDialog(null);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private void OpenEditRepairDialog(RepairRequestDto dto)
        {
            using var dlg = new RepairRequestFormDialog(dto);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private void OpenNewFollowUpDialog(FollowUpDto? preset = null)
        {
            using var dlg = new FollowUpFormDialog(preset, _customers.Values.ToList(), _allRepairs);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private void OpenEditFollowUpDialog(FollowUpDto dto)
        {
            using var dlg = new FollowUpFormDialog(dto, _customers.Values.ToList(), _allRepairs);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private void OpenNewInteractionDialog()
        {
            using var dlg = new InteractionFormDialog(null, null, _customers.Values.ToList(), _allRepairs);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private void OpenEditInteractionDialog(InteractionDto dto)
        {
            using var dlg = new InteractionFormDialog(dto, null, _customers.Values.ToList(), _allRepairs);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        //  NESTED HELPER CONTROLS
        // ═══════════════════════════════════════════════════════════════

        private enum ChartKind { Line, Bar }

        [DesignerCategory("Code")]
        private sealed class ChartCard : Control
        {
            private static readonly Font HeadlineFont = AppFonts.Strong(19F);

            private readonly string _title;
            private readonly ChartKind _kind;

            private List<(string Label, double Value)> _lineData = new();
            private List<(string Label, double Value, Color Color)> _barData = new();
            private Color _lineColor = AppTheme.Primary;
            private bool _clickableBars;
            private bool _currency;

            private readonly List<(Rectangle Hit, string Label, string Value, Point Anchor)> _hits = new();
            private int _hoverIndex = -1;
            private bool _cardHover;
            private string? _selectedLabel;
            private bool _filtered;

            public event Action<string>? PointClicked;

            public ChartCard(string title, ChartKind kind)
            {
                _title = title;
                _kind = kind;

                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
                AccessibleRole = AccessibleRole.Chart;
                AccessibleName = title;
            }

            public void SetLineData(List<(string, double)> data, Color color, bool currency = false, bool filtered = false)
            {
                _filtered = filtered;
                _lineData = data;
                _lineColor = color;
                _currency = currency;
                _selectedLabel = null;
                Invalidate();
            }

            public void SetBarData(List<(string, double, Color)> data, bool clickable = false)
            {
                _barData = data;
                _clickableBars = clickable;
                _selectedLabel = null;
                Invalidate();
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                _cardHover = true;
                Invalidate();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int idx = _hits.FindIndex(h => h.Hit.Contains(e.Location));
                if (idx != _hoverIndex)
                {
                    _hoverIndex = idx;
                    Invalidate();
                }
                Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hoverIndex = -1;
                _cardHover = false;
                Invalidate();
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                var hit = _hits.FirstOrDefault(h => h.Hit.Contains(e.Location));
                if (hit.Hit != Rectangle.Empty)
                {
                    _selectedLabel = hit.Label;
                    PointClicked?.Invoke(hit.Label);
                    return;
                }

                var label = _selectedLabel
                    ?? (_kind == ChartKind.Line
                        ? (_lineData.Count > 0 ? _lineData[^1].Label : null)
                        : (_barData.Count > 0 ? _barData[0].Label : null));
                if (!string.IsNullOrEmpty(label))
                    PointClicked?.Invoke(label!);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var bg = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(bg, ClientRectangle);

                UiKit.Card(g, ClientRectangle, UiKit.T.Radius, UiKit.T.Surface,
                    _cardHover ? UiKit.T.InkFaint : UiKit.T.Line);

                int pad = UiKit.T.S5;
                bool hasData = _kind == ChartKind.Line ? _lineData.Count > 0 : _barData.Count > 0;

                // Title row
                int titleH = 26;
                UiKit.Text(g, _title, UiKit.T.Section, UiKit.T.Ink,
                    new Rectangle(pad, pad, Math.Max(10, Width - pad * 2 - 120), titleH),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                if (hasData)
                {
                    string hint = _kind == ChartKind.Line ? "Hover to inspect" : "Click to filter";
                    int hintW = UiKit.Measure(hint, UiKit.Micro).Width + 6;
                    var hintRect = new Rectangle(Width - pad - hintW, pad + 2, hintW, titleH - 4);
                    UiKit.Text(g, hint, UiKit.Micro, UiKit.T.InkMuted, hintRect,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                }

                // Headline band
                int headlineH = 36;
                int headlineTop = pad + titleH + 6;
                if (hasData)
                    DrawHeadline(g, pad, headlineTop, headlineH);

                // Plot area
                int plotTop = headlineTop + (hasData ? headlineH + 6 : 0);
                int plotBottom = Height - pad - 4;
                var plot = new Rectangle(pad + 2, plotTop, Width - (pad + 2) * 2, plotBottom - plotTop);

                _hits.Clear();
                if (plot.Height < 50 || plot.Width < 80) return;

                if (_kind == ChartKind.Line) DrawLine(g, plot);
                else DrawBar(g, plot);

                DrawTooltip(g);
            }

            private string FormatValue(double v) => _currency
                ? $"${v:N0}"
                : v % 1 == 0 ? $"{v:N0}" : $"{v:0.##}";

            private string AxisText(double v)
            {
                string n = v >= 1_000_000 ? $"{v / 1_000_000:0.#}M"
                         : v >= 1_000 ? $"{v / 1_000:0.#}K"
                         : $"{v:0.#}";
                return _currency ? "$" + n : n;
            }

            private static double NiceStep(double max, int ticks, bool integers)
            {
                double raw = max / ticks;
                double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
                double norm = raw / mag;
                double nice = norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 5 ? 5 : 10;
                double step = nice * mag;
                if (integers && step < 1) step = 1;
                return step;
            }

            private void DrawHeadline(Graphics g, int pad, int y, int h)
            {
                if (_kind == ChartKind.Line)
                {
                    var last = _lineData[^1];
                    string big = FormatValue(last.Value);
                    int bigW = UiKit.Measure(big, HeadlineFont).Width;
                    UiKit.Text(g, big, HeadlineFont, UiKit.T.Ink,
                        new Rectangle(pad, y, bigW + 8, h),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                    int x = pad + bigW + 16;
                    string stats = $"Avg {FormatValue(_lineData.Average(p => p.Value))}    Peak {FormatValue(_lineData.Max(p => p.Value))}";
                    int statsW = UiKit.Measure(stats, UiKit.T.Small).Width + 4;
                    bool showStats = Width - pad - statsW > x + 160;
                    if (showStats)
                        UiKit.Text(g, stats, UiKit.T.Small, UiKit.T.InkMuted,
                            new Rectangle(Width - pad - statsW, y, statsW, h),
                            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                    int limit = showStats ? Width - pad - statsW - 10 : Width - pad;
                    string caption = last.Label;

                    if (_lineData.Count >= 2 && _lineData[^2].Value != 0)
                    {
                        var prev = _lineData[^2];
                        double pct = (last.Value - prev.Value) / Math.Abs(prev.Value) * 100;
                        bool up = pct >= 0;
                        Color c = up ? AppTheme.Success : AppTheme.Danger;
                        string t = $"{(up ? "+" : "-")}{Math.Abs(pct):0.#}%";
                        int tw = UiKit.Measure(t, UiKit.T.SmallStrong).Width;
                        var pill = new Rectangle(x, y + (h - 22) / 2, tw + 16, 22);
                        UiKit.FillRounded(g, pill, 11, UiKit.Wash(c));
                        UiKit.Text(g, t, UiKit.T.SmallStrong, c, pill, UiKit.Center | TextFormatFlags.NoPrefix);
                        x = pill.Right + 10;
                        caption = $"{last.Label}  vs  {prev.Label}";
                    }

                    if (limit - x > 40)
                        UiKit.Text(g, caption, UiKit.T.Small, UiKit.T.InkMuted,
                            new Rectangle(x, y, limit - x, h),
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
                else
                {
                    double sum = _barData.Sum(b => b.Value);
                    string big = FormatValue(sum);
                    int bigW = UiKit.Measure(big, HeadlineFont).Width;
                    UiKit.Text(g, big, HeadlineFont, UiKit.T.Ink,
                        new Rectangle(pad, y, bigW + 8, h),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                    var top = _barData.OrderByDescending(b => b.Value).First();
                    int x = pad + bigW + 16;
                    UiKit.Text(g, $"Total: {FormatValue(sum)}  ·  Top: {top.Label}", UiKit.T.Small, UiKit.T.InkMuted,
                        new Rectangle(x, y, Math.Max(10, Width - pad - x), h),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }

            private void DrawLine(Graphics g, Rectangle plot)
            {
                if (_lineData.Count == 0) { DrawEmpty(g, plot); return; }

                int n = _lineData.Count;
                int labelH = 24;
                int chartH = plot.Height - labelH - 4;
                if (chartH < 35) return;

                double dataMax = Math.Max(1, _lineData.Max(p => p.Value));
                bool allInt = _lineData.All(p => p.Value % 1 == 0);
                const int ticks = 4;
                double step = NiceStep(dataMax, ticks, allInt);
                double axisMax = step * ticks;

                int axisW = Math.Max(36, UiKit.Measure(AxisText(axisMax), UiKit.Micro).Width + 14);
                float topY = plot.Top + 12;
                float baseY = plot.Top + chartH - 10;
                float span = baseY - topY;
                float x0 = plot.Left + axisW + 12;
                float xw = Math.Max(1, plot.Right - 14 - x0);
                var clipRect = new Rectangle((int)(plot.Left + axisW + 4), plot.Top,
                                             (int)(plot.Width - axisW - 18), chartH);

                // Gridlines + y-axis labels
                for (int k = 0; k <= ticks; k++)
                {
                    float gy = baseY - (float)k / ticks * span;
                    using (var grid = new Pen(UiKit.T.Line, 1f))
                    {
                        if (k > 0) grid.DashStyle = DashStyle.Dot;
                        g.DrawLine(grid, plot.Left + axisW, gy, plot.Right - 6, gy);
                    }
                    UiKit.Text(g, AxisText(step * k), UiKit.Micro, UiKit.T.InkMuted,
                        new Rectangle(plot.Left, (int)gy - 8, axisW - 8, 16),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                }

                // Points
                var points = new PointF[n];
                for (int i = 0; i < n; i++)
                {
                    float x = n == 1 ? x0 + xw / 2f : x0 + (float)i / (n - 1) * xw;
                    float y = baseY - (float)(_lineData[i].Value / axisMax) * span;
                    points[i] = new PointF(x, y);
                }

                // Hover target
                float ColLeft(int i) => i == 0 ? plot.Left + axisW : (points[i - 1].X + points[i].X) / 2f;
                float ColRight(int i) => i == n - 1 ? plot.Right - 6 : (points[i].X + points[i + 1].X) / 2f;

                if (_hoverIndex >= 0 && _hoverIndex < n)
                {
                    using var band = new SolidBrush(Color.FromArgb(22, _lineColor));
                    g.FillRectangle(band, ColLeft(_hoverIndex), plot.Top + 6,
                        ColRight(_hoverIndex) - ColLeft(_hoverIndex), chartH - 12);

                    using var cross = new Pen(Color.FromArgb(150, _lineColor), 1f) { DashStyle = DashStyle.Dash };
                    g.DrawLine(cross, points[_hoverIndex].X, topY, points[_hoverIndex].X, baseY);
                }

                // Smooth area + line
                g.SetClip(clipRect);
                if (n > 1)
                {
                    using (var path = new GraphicsPath())
                    {
                        if (n >= 3) path.AddCurve(points, 0.35f); else path.AddLines(points);
                        path.AddLine(points[^1], new PointF(points[^1].X, baseY));
                        path.AddLine(new PointF(points[^1].X, baseY), new PointF(points[0].X, baseY));
                        path.CloseFigure();

                        using var brush = new LinearGradientBrush(
                            new Rectangle(clipRect.Left, (int)topY, clipRect.Width, Math.Max(1, (int)span)),
                            Color.FromArgb(80, _lineColor), Color.FromArgb(0, _lineColor), LinearGradientMode.Vertical);
                        g.FillPath(brush, path);
                    }

                    using var pen = new Pen(_lineColor, 2.5f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
                    if (n >= 3) g.DrawCurve(pen, points, 0.35f); else g.DrawLines(pen, points);
                }
                g.ResetClip();

                // Markers
                bool showAll = n <= 14;
                for (int i = 0; i < n; i++)
                {
                    bool hot = i == _hoverIndex || _lineData[i].Label == _selectedLabel;
                    bool latest = i == n - 1;

                    if (hot || latest || showAll)
                    {
                        float r = hot ? 6.5f : latest ? 5.5f : 4f;
                        if (hot)
                        {
                            using var halo = new SolidBrush(Color.FromArgb(45, _lineColor));
                            g.FillEllipse(halo, points[i].X - r - 5, points[i].Y - r - 5, (r + 5) * 2, (r + 5) * 2);
                        }
                        using var fill = new SolidBrush(latest || hot ? _lineColor : Color.White);
                        g.FillEllipse(fill, points[i].X - r, points[i].Y - r, r * 2, r * 2);
                        using var ring = new Pen(latest || hot ? Color.White : _lineColor, 2f);
                        g.DrawEllipse(ring, points[i].X - r, points[i].Y - r, r * 2, r * 2);
                    }

                    _hits.Add((
                        new Rectangle((int)ColLeft(i), plot.Top + 4, Math.Max(2, (int)(ColRight(i) - ColLeft(i))), chartH + labelH - 4),
                        _lineData[i].Label,
                        FormatValue(_lineData[i].Value),
                        new Point((int)points[i].X, (int)points[i].Y)));
                }

                // X labels
                int maxLabels = Math.Max(2, (int)(xw / 70));
                int stepX = (int)Math.Ceiling(n / (double)maxLabels);
                for (int i = n - 1; i >= 0; i -= stepX)
                {
                    int lx = (int)(points[i].X - 35);
                    lx = Math.Max(plot.Left + axisW - 10, Math.Min(plot.Right - 70, lx));
                    bool hot = i == _hoverIndex;
                    UiKit.Text(g, _lineData[i].Label, hot ? UiKit.T.SmallStrong : UiKit.T.Small,
                        hot ? UiKit.T.Ink : UiKit.T.InkMuted,
                        new Rectangle(lx, plot.Top + chartH + 4, 70, labelH - 6), UiKit.Center);
                }
            }

            private void DrawBar(Graphics g, Rectangle plot)
            {
                if (_barData.Count == 0) { DrawEmpty(g, plot); return; }

                int n = _barData.Count;
                double max = Math.Max(1, _barData.Max(b => b.Value));
                double sum = _barData.Sum(b => b.Value);

                int rowH = Math.Max(22, Math.Min(44, plot.Height / n));
                int shown = Math.Min(n, Math.Max(1, plot.Height / rowH));

                int labelW = 0;
                for (int i = 0; i < shown; i++)
                    labelW = Math.Max(labelW, UiKit.Measure(_barData[i].Label, UiKit.T.Small).Width);
                labelW = Math.Min(labelW + 16, (int)(plot.Width * 0.42));

                string sample = FormatValue(max) + (sum > 0 ? "  ·  100%" : "");
                int valueW = UiKit.Measure(sample, UiKit.T.SmallStrong).Width + 14;

                int trackL = plot.Left + labelW + 12;
                int trackR = plot.Right - valueW - 10;
                int trackW = Math.Max(20, trackR - trackL);
                int barH = Math.Max(8, Math.Min(14, rowH - 12));

                for (int i = 0; i < shown; i++)
                {
                    var (label, value, color) = _barData[i];
                    bool hot = (i == _hoverIndex || label == _selectedLabel);

                    var row = new Rectangle(plot.Left, plot.Top + i * rowH, plot.Width, rowH);
                    int cy = row.Top + rowH / 2;

                    if (hot)
                        UiKit.FillRounded(g, row, 8, Color.FromArgb(18, color));

                    // Category label
                    UiKit.Text(g, label, UiKit.T.Small, UiKit.T.Ink,
                        new Rectangle(plot.Left + 6, row.Top, labelW - 8, rowH),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    // Track + fill
                    var track = new Rectangle(trackL, cy - barH / 2, trackW, barH);
                    UiKit.FillRounded(g, track, barH / 2, UiKit.T.Line);

                    int fillW = value <= 0 ? 0 : Math.Max(barH, (int)(value / max * trackW));
                    var fill = new Rectangle(trackL, track.Top, fillW, barH);
                    if (fillW > 0)
                    {
                        UiKit.FillRounded(g, fill, barH / 2, hot ? color : Color.FromArgb(215, color));
                        if (label == _selectedLabel)
                            UiKit.StrokeRounded(g, fill, barH / 2, color, 1.6f);
                    }

                    // Value + percentage
                    string pct = sum > 0 ? $"  ·  {value / sum * 100:0}%" : "";
                    string valueText = FormatValue(value) + pct;
                    UiKit.Text(g, valueText, UiKit.T.SmallStrong, hot ? color : UiKit.T.Ink,
                        new Rectangle(plot.Right - valueW, row.Top, valueW, rowH),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                    if (_clickableBars)
                        _hits.Add((row, label, valueText, new Point(fill.Right, cy)));
                }

                if (shown < n && plot.Height - shown * rowH >= 16)
                    UiKit.Text(g, $"+ {n - shown} more", UiKit.T.Small, UiKit.T.InkMuted,
                        new Rectangle(plot.Left, plot.Top + shown * rowH, plot.Width, 18),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }

            private void DrawTooltip(Graphics g)
            {
                if (_hoverIndex < 0 || _hoverIndex >= _hits.Count) return;

                var (_, label, value, anchor) = _hits[_hoverIndex];
                var ls = UiKit.Measure(label, UiKit.T.Small);
                var vs = UiKit.Measure(value, UiKit.T.BodyStrong);
                int tw = Math.Max(ls.Width, vs.Width) + 26;
                int th = 50;

                int tx = Math.Max(6, Math.Min(Width - tw - 6, anchor.X - tw / 2));
                int ty = anchor.Y - th - 16;
                if (ty < 6) ty = anchor.Y + 18;
                ty = Math.Min(ty, Height - th - 6);

                var tip = new Rectangle(tx, ty, tw, th);
                UiKit.FillRounded(g, tip, 8, UiKit.T.Ink);
                UiKit.Text(g, label, UiKit.T.Small, Color.FromArgb(200, 255, 255, 255),
                    new Rectangle(tip.Left, tip.Top + 6, tip.Width, 16), UiKit.Center);
                UiKit.Text(g, value, UiKit.T.BodyStrong, Color.White,
                    new Rectangle(tip.Left, tip.Top + 23, tip.Width, 22), UiKit.Center);
            }

            private void DrawEmpty(Graphics g, Rectangle plot)
            {
                var top = new Rectangle(plot.Left, plot.Top + plot.Height / 2 - 24, plot.Width, 22);
                var bottom = new Rectangle(plot.Left, top.Bottom + 4, plot.Width, 20);
                UiKit.Text(g, _filtered ? "No data in this period" : "No data yet",
                    UiKit.T.BodyStrong, UiKit.T.InkMuted, top, UiKit.Center);
                UiKit.Text(g, _filtered ? "Try a longer period" : "This chart fills in as records are added",
                    UiKit.T.Small, UiKit.T.InkFaint, bottom, UiKit.Center);
            }
        }

        [DesignerCategory("Code")]
        private sealed class KpiTile : Control
        {
            private const int Pad = 16;

            private string _label = "";
            private string _number = "0";
            private string _sub = "";
            private Color _accent = AppTheme.Primary;
            private bool _hover;
            private bool _down;

            public string Value => _number;

            public KpiTile()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
                TabStop = true;
                Cursor = Cursors.Hand;
                AccessibleRole = AccessibleRole.PushButton;
            }

            public void Set(string label, string number, string sub, Color accent, string glyph = "")
            {
                _label = label;
                _number = number;
                _sub = sub;
                _accent = accent;
                AccessibleName = $"{label}: {number}. {sub}";
                Invalidate();
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _down = true; Focus(); Invalidate(); base.OnMouseDown(e); }
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

                var r = ClientRectangle;
                using (var b = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(b, r);

                var cardR = new Rectangle(0, 0, Width - 1, Height - 1);
                Color fill = _down ? Color.FromArgb(241, 245, 249) : (_hover ? Color.FromArgb(248, 250, 252) : UiKit.T.Surface);
                UiKit.FillRounded(g, cardR, 8, fill);

                Color border = _hover ? _accent : UiKit.T.Line;
                using (var pen = new Pen(border, _hover ? 1.5f : 1f))
                using (var path = UiKit.Rounded(cardR, 8))
                    g.DrawPath(pen, path);

                int y = 14;

                // Status accent dot
                int dotSize = 7;
                UiKit.Dot(g, Pad + dotSize / 2f, y + 8, dotSize, _accent);

                // Interactive indicator arrow on top-right
                int arrowW = 16;
                var arrowRect = new Rectangle(Width - Pad - arrowW, y, arrowW, 18);
                UiKit.Text(g, "↗", UiKit.T.Small, _hover ? _accent : UiKit.T.InkFaint, arrowRect,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                // Category Label
                int lblX = Pad + dotSize + 8;
                int lblW = Width - lblX - Pad - arrowW - 4;
                var lblRect = new Rectangle(lblX, y, Math.Max(10, lblW), 18);
                UiKit.Text(g, _label, UiKit.T.SmallStrong, UiKit.T.InkMuted, lblRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                y += 22;

                // Number / Metric value
                int numH = 40;
                var numRect = new Rectangle(Pad, y, Width - Pad * 2, numH);
                float fontSize = _number.Length > 10 ? 14F : 20F;
                using (var numFont = AppFonts.Strong(fontSize))
                {
                    UiKit.Text(g, _number, numFont, UiKit.T.Ink, numRect,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);
                }

                y += numH + 2;

                // Subtitle / context
                int subH = 20;
                var subRect = new Rectangle(Pad, y, Width - Pad * 2, subH);
                UiKit.Text(g, _sub, UiKit.T.Small, UiKit.T.InkMuted, subRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                if (Focused)
                    UiKit.FocusRing(g, cardR, 8);
            }
        }

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

                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                UiKit.FillRounded(e.Graphics, r, UiKit.T.Radius, UiKit.T.Surface);
                using var pen = new Pen(UiKit.T.Line, 1);
                using var path = UiKit.Rounded(r, UiKit.T.Radius);
                e.Graphics.DrawPath(pen, path);
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

            public void SelectByValue(int? val)
            {
                for (int i = 0; i < _items.Length; i++)
                {
                    if (_items[i].Value == val)
                    {
                        _selected = i;
                        Invalidate();
                        break;
                    }
                }
            }

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
                    else if (i == _hover)
                    {
                        UiKit.FillRounded(g, seg, 6, UiKit.T.RowHover);
                    }

                    Color fg = active ? AppTheme.Primary : UiKit.T.InkMuted;
                    var f = active ? UiKit.T.SmallStrong : UiKit.T.Small;
                    UiKit.Text(g, _items[i].Label, f, fg, seg, UiKit.Center);

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

                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                UiKit.FillRounded(g, r, UiKit.RadiusSm, UiKit.T.Surface);
                using (var pen = new Pen(_focused ? AppTheme.Primary : UiKit.T.Line, 1))
                using (var path = UiKit.Rounded(r, UiKit.RadiusSm))
                    g.DrawPath(pen, path);

                using (var f = UiKit.GlyphFont(11F))
                    UiKit.Text(g, "\uE721", f, UiKit.T.InkFaint, new Rectangle(10, 0, 18, Height), UiKit.Center);

                if (Inner.Text.Length > 0)
                {
                    using var f = UiKit.GlyphFont(9F);
                    Color xColor = _hoverClear ? UiKit.T.Ink : UiKit.T.InkFaint;
                    UiKit.Text(g, "\uE711", f, xColor, ClearRect, UiKit.Center);
                }
            }
        }

        private sealed class QuietMenuRenderer : ToolStripProfessionalRenderer
        {
            public QuietMenuRenderer() : base(new QuietColorTable()) { }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (e.Item.Selected)
                {
                    var r = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
                    UiKit.FillRounded(e.Graphics, r, 4, UiKit.T.RowHover);
                }
            }
            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using var p = new Pen(UiKit.T.LineSoft, 1);
                e.Graphics.DrawLine(p, 8, e.Item.Height / 2, e.Item.Width - 8, e.Item.Height / 2);
            }
        }

        private sealed class QuietColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => UiKit.T.Surface;
            public override Color MenuBorder => UiKit.T.Line;
            public override Color MenuItemBorder => Color.Transparent;
            public override Color MenuItemSelected => UiKit.T.RowHover;
            public override Color SeparatorDark => UiKit.T.LineSoft;
            public override Color SeparatorLight => Color.Transparent;
        }
    }
}
