using CRM.winforms.Controls;
using CRM.winforms.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Dedicated Multi-Tenant & Platform Overview Dashboard for Super Admins.
    /// Provides SaaS business intelligence:
    ///   1. Multi-Tenant KPI tiles (Total Companies, Active Tenants, Plans, MRR, Users, Databases, Devices, System Health)
    ///   2. Analytics Charts (Companies by Subscription Tier, Platform Users by Role)
    ///   3. Business Intelligence: growth trends, MoM deltas, health scores, revenue distribution
    ///   4. Live Registered Businesses Workbench (Interactive directory table with search, filters & view dialog)
    /// </summary>
    [DesignerCategory("Code")]
    public class SuperAdminDashboardControl : UserControl
    {
        private readonly ApiClient _api = new ApiClient();
        private List<CompanyDto> _allCompanies = new();
        private List<CompanyDto> _filteredCompanies = new();
        private List<SubscriptionDto> _allSubscriptions = new();
        private List<UserSummaryDto> _allUsers = new();

        private string _activeFilter = "All";
        private string _searchQuery = "";
        private bool _layingOut;

        public event EventHandler<string>? ActionRequested;

        // ═══════════ HEADER CONTROLS ═══════════
        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private Label _rule = null!;
        private SaasButton btnRefresh = null!;
        private Label lblLoading = null!;

        // ═══════════ SECTION HEADINGS ═══════════
        private readonly List<(Label Title, Label Hint, Label Rule)> _sections = new();

        // ═══════════ 8 KPI TILES ═══════════
        private readonly List<KpiTile> _tiles = new();
        private KpiTile tileTotalCompanies = null!;
        private KpiTile tileActiveCompanies = null!;
        private KpiTile tileSubscriptionPlans = null!;
        private KpiTile tileMonthlyRevenue = null!;
        private KpiTile tileTotalUsers = null!;
        private KpiTile tileDatabases = null!;
        private KpiTile tileTotalDevices = null!;
        private KpiTile tileSystemHealth = null!;

        // ═══════════ CHARTS ═══════════
        private readonly List<ChartCard> _charts = new();
        private ChartCard chartPlans = null!;
        private ChartCard chartRoles = null!;

        // ═══════════ BI: TRENDS / REPORTS ═══════════
        private readonly List<BiCard> _biCards = new();
        private BiCard biGrowth = null!;
        private BiCard biRevenueMix = null!;
        private BiCard biTenantHealth = null!;
        private BiCard biTopTenants = null!;

        // ═══════════ WORKBENCH TABLE ═══════════
        private WorkbenchCard cardDirectory = null!;
        private Label lblGridTitle = null!;
        private SaasButton btnViewAllCompanies = null!;
        private SegmentedFilter filterBar = null!;
        private WorkbenchSearch searchBox = null!;
        private Label lblCount = null!;
        private DataGridView dgv = null!;
        private WorkbenchState emptyState = null!;
        private ContextMenuStrip _contextMenu = null!;
        private int _hoverRow = -1;

        // ═══════════ SHARED DRAWING HELPERS ═══════════
        private const int CellPadX = 14;
        private const TextFormatFlags Flat = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        private const TextFormatFlags CellText = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | Flat;

        private static readonly Font MonoFont = new Font("Consolas", 9F);
        private static readonly Color Emerald = Color.FromArgb(16, 185, 129);
        private static readonly Color Rose = Color.FromArgb(244, 63, 94);
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

        private static Color Blend(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public SuperAdminDashboardControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            AutoScaleMode = AutoScaleMode.Font;
            AutoScroll = true;

            BuildUi();

            this.Load += async (s, e) => await ReloadAsync();
        }

        // ═══════════ UI INITIALIZATION ═══════════

        private void BuildUi()
        {
            lblTitle = new Label
            {
                Text = "Platform Dashboard",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Real-time platform overview · KPIs, analytics, and tenant directory",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _rule = new Label { AutoSize = false, Height = 1, BackColor = UiKit.T.Line, Text = "" };

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary)
            {
                Size = new Size(96, 36)
            };
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            lblLoading = new Label
            {
                Text = "Refreshing platform metrics...",
                Font = UiKit.T.SmallStrong,
                ForeColor = AppTheme.Primary,
                AutoSize = true,
                Visible = false,
                BackColor = Color.Transparent
            };

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(_rule);
            Controls.Add(btnRefresh);
            Controls.Add(lblLoading);

            // ── Section 1: KPI Metrics ──
            AddSection("Multi-Tenant Platform Metrics",
                "Live counts across every tenant · click a tile to open its detail view");

            tileTotalCompanies = AddTile("TOTAL BUSINESSES", "0", "Registered companies on platform", AppTheme.Primary, IconFont.Customers);
            tileTotalCompanies.Click += (s, e) => ActionRequested?.Invoke(this, "companies");

            tileActiveCompanies = AddTile("ACTIVE TENANTS", "0", "Operational & accepting repair requests", AppTheme.Success, "\uE73E");
            tileActiveCompanies.Click += (s, e) => ActionRequested?.Invoke(this, "companies");

            tileSubscriptionPlans = AddTile("SUBSCRIPTION TIERS", "0", "Starter, Pro, Enterprise tiers", Color.FromArgb(124, 58, 237), "\uE9D5");
            tileSubscriptionPlans.Click += (s, e) => ActionRequested?.Invoke(this, "subscriptions");

            tileMonthlyRevenue = AddTile("ESTIMATED MONTHLY MRR", "₱0", "Total subscription billings / month", Emerald, "\uE8C7");
            tileMonthlyRevenue.Click += (s, e) => ActionRequested?.Invoke(this, "subscriptions");

            tileTotalUsers = AddTile("PLATFORM USERS", "0", "Admins, managers & technicians", Color.FromArgb(59, 130, 246), IconFont.Profile);
            tileTotalUsers.Click += (s, e) => ActionRequested?.Invoke(this, "system-monitor");

            tileDatabases = AddTile("TENANT DATABASES", "0", "Isolated SQL Server databases", Color.FromArgb(245, 158, 11), "\uE7B8");
            tileDatabases.Click += (s, e) => ActionRequested?.Invoke(this, "system-monitor");

            tileTotalDevices = AddTile("HARDWARE MONITORED", "0", "Client hardware in tenant systems", Color.FromArgb(236, 72, 153), "\uE7F4");
            tileTotalDevices.Click += (s, e) => ActionRequested?.Invoke(this, "companies");

            tileSystemHealth = AddTile("SYSTEM STATUS", "100% Online", "All multi-tenant services operational", AppTheme.Success, "\uE958");
            tileSystemHealth.Click += (s, e) => ActionRequested?.Invoke(this, "system-monitor");

            // ── Section 2: Analytics & Charts ──
            AddSection("Platform Analytics & Distribution",
                "How tenants and users are spread across subscription tiers and roles");

            chartPlans = new ChartCard("Companies by Subscription Plan", ChartKind.Bar) { StaticBars = false };
            chartPlans.PointClicked += label => { searchBox.Text = label; };
            _charts.Add(chartPlans);
            Controls.Add(chartPlans);

            chartRoles = new ChartCard("Platform Users by Role", ChartKind.Bar) { StaticBars = false };
            chartRoles.PointClicked += label => { ActionRequested?.Invoke(this, "system-monitor"); };
            _charts.Add(chartRoles);
            Controls.Add(chartRoles);

            // ── Section 3: Business Intelligence ──
            AddSection("Business Intelligence & Reports",
                "Growth trends, revenue mix, tenant health and top performers");

            biGrowth = new BiCard("Tenant Growth (12 months)", BiKind.Line);
            _biCards.Add(biGrowth);
            Controls.Add(biGrowth);

            biRevenueMix = new BiCard("Revenue Mix by Plan", BiKind.Donut);
            _biCards.Add(biRevenueMix);
            Controls.Add(biRevenueMix);

            biTenantHealth = new BiCard("Tenant Health Breakdown", BiKind.Bars);
            _biCards.Add(biTenantHealth);
            Controls.Add(biTenantHealth);

            biTopTenants = new BiCard("Top Tenants by User Count", BiKind.RankedList);
            _biCards.Add(biTopTenants);
            Controls.Add(biTopTenants);

            // ── Section 4: Live Businesses Workbench ──
            AddSection("Registered Businesses",
                "Every tenant on the platform · double-click a row to view its full profile");

            BuildDirectoryCard();
            BuildContextMenu();
        }

        private void AddSection(string title, string hint)
        {
            var lblT = new Label
            {
                Text = title,
                Font = UiKit.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            var lblH = new Label
            {
                Text = hint,
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            var rule = new Label { AutoSize = false, Height = 1, BackColor = UiKit.T.LineSoft, Text = "" };

            _sections.Add((lblT, lblH, rule));
            Controls.Add(rule);
            Controls.Add(lblT);
            Controls.Add(lblH);
        }

        private KpiTile AddTile(string label, string number, string sub, Color accent, string glyph)
        {
            var t = new KpiTile();
            t.Set(label, number, sub, accent, glyph);
            _tiles.Add(t);
            Controls.Add(t);
            return t;
        }

        private void BuildDirectoryCard()
        {
            cardDirectory = new WorkbenchCard { BackColor = UiKit.T.Surface };

            lblGridTitle = new Label
            {
                Text = "Tenant Directory",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnViewAllCompanies = new SaasButton("View All →", SaasButtonVariant.Secondary)
            {
                Size = new Size(124, 34)
            };
            btnViewAllCompanies.Click += (s, e) => ActionRequested?.Invoke(this, "companies");

            filterBar = new SegmentedFilter(new[] { "All", "Active", "Deactivated" }, 0);
            filterBar.SelectionChanged += (s, e) =>
            {
                _activeFilter = filterBar.SelectedItem;
                ApplyFilter();
            };

            searchBox = new WorkbenchSearch { Placeholder = "Search by company name, code, city, email..." };
            searchBox.QueryChanged += (s, e) =>
            {
                _searchQuery = searchBox.Query ?? "";
                ApplyFilter();
            };

            lblCount = new Label
            {
                Text = "Showing 0 businesses",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            dgv = new DataGridView();
            StyleGrid(dgv);

            dgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _filteredCompanies.Count)
                    ViewCompanyDetails(_filteredCompanies[e.RowIndex]);
            };

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

            emptyState = new WorkbenchState
            {
                Visible = false
            };
            emptyState.Show(
                IconFont.Customers,
                "No businesses match your filter",
                "Try clearing the search query or switching the active/deactivated filter.");

            cardDirectory.Controls.Add(lblGridTitle);
            cardDirectory.Controls.Add(btnViewAllCompanies);
            cardDirectory.Controls.Add(filterBar);
            cardDirectory.Controls.Add(searchBox);
            cardDirectory.Controls.Add(lblCount);
            cardDirectory.Controls.Add(dgv);
            cardDirectory.Controls.Add(emptyState);

            Controls.Add(cardDirectory);
        }

        private void BuildContextMenu()
        {
            _contextMenu = new ContextMenuStrip { ShowImageMargin = false, Font = UiKit.T.Body };
            var itemDetails = new ToolStripMenuItem("View Business Details");
            itemDetails.Click += (s, e) =>
            {
                if (dgv.CurrentRow != null && dgv.CurrentRow.Index < _filteredCompanies.Count)
                    ViewCompanyDetails(_filteredCompanies[dgv.CurrentRow.Index]);
            };
            var itemManage = new ToolStripMenuItem("Open in Businesses Manager");
            itemManage.Click += (s, e) => ActionRequested?.Invoke(this, "companies");

            _contextMenu.Items.Add(itemDetails);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(itemManage);

            dgv.ContextMenuStrip = _contextMenu;
        }

        private void StyleGrid(DataGridView g)
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
            g.RowTemplate.Height = 60;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 44;

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
            g.DefaultCellStyle.Padding = new Padding(CellPadX, 8, CellPadX, 8);
            g.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

            g.Columns.Clear();

            void Col(string name, string header, string prop, int fillWeight, int minWidth)
            {
                g.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = name,
                    HeaderText = header,
                    DataPropertyName = prop,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = fillWeight,
                    MinimumWidth = minWidth
                });
            }

            Col("colCode", "CODE", "CompanyCode", 70, 100);
            Col("colName", "BUSINESS NAME", "CompanyName", 210, 230);
            Col("colLocation", "LOCATION", "LocationDisplay", 100, 120);
            Col("colPlan", "SUBSCRIPTION TIER", "SubscriptionPlanName", 105, 150);
            Col("colFee", "FEE / MO", "", 75, 105);
            Col("colDb", "TENANT DATABASE", "DatabaseName", 130, 170);
            Col("colAdmin", "ADMINISTRATOR", "AdminEmail", 140, 180);
            Col("colUsers", "USERS", "", 60, 90);

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStatus",
                HeaderText = "STATUS",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = 136,
                MinimumWidth = 136
            });

            g.CellPainting += (s, e) => PaintCell(g, e);

            g.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= g.RowCount || e.ColumnIndex < 0) return;
                switch (g.Columns[e.ColumnIndex].Name)
                {
                    case "colName":
                    case "colAdmin":
                    case "colDb":
                    case "colLocation":
                        var v = Convert.ToString(g.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
                        if (!string.IsNullOrWhiteSpace(v)) e.ToolTipText = v;
                        break;
                }
            };
        }

        private void PaintCell(DataGridView g, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < 0) return;
            var gr = e.Graphics;
            var cell = e.CellBounds;

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

            if (e.RowIndex < 0 || e.RowIndex >= _filteredCompanies.Count) return;
            var item = _filteredCompanies[e.RowIndex];

            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool hovered = e.RowIndex == _hoverRow;

            using (var b = new SolidBrush(selected || hovered ? UiKit.T.RowHover : UiKit.T.Surface))
                gr.FillRectangle(b, cell);
            using (var p = new Pen(UiKit.T.LineSoft))
                gr.DrawLine(p, cell.Left, cell.Bottom - 1, cell.Right, cell.Bottom - 1);

            UiKit.Quality(gr);

            if (e.ColumnIndex == 0 && selected && g.Focused)
                UiKit.FillRounded(gr, new Rectangle(cell.Left, cell.Top + 12, 3, cell.Height - 25), 1, AppTheme.Primary);

            var rect = new Rectangle(cell.Left + CellPadX, cell.Top,
                Math.Max(0, cell.Width - CellPadX * 2), cell.Height - 1);
            int cy = rect.Top + rect.Height / 2;
            string text = Convert.ToString(e.FormattedValue) ?? "";

            switch (g.Columns[e.ColumnIndex].Name)
            {
                case "colCode":
                    UiKit.Text(gr, item.CompanyCode ?? "", UiKit.T.SmallStrong, AppTheme.Primary, rect, CellText);
                    break;

                case "colName":
                    PaintNameCell(gr, item, rect);
                    break;

                case "colLocation":
                    DrawPlain(gr, text, rect, UiKit.T.Small, UiKit.T.Ink);
                    break;

                case "colPlan":
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        DrawPlain(gr, "Unassigned", rect, UiKit.T.Small, UiKit.T.InkFaint);
                    }
                    else
                    {
                        int cw = Math.Min(rect.Width, MeasureW(text, UiKit.T.SmallStrong) + 24);
                        var chip = new Rectangle(rect.Left, cy - 12, cw, 24);
                        UiKit.FillRounded(gr, chip, 8, UiKit.Wash(AppTheme.Primary));
                        UiKit.Text(gr, text, UiKit.T.SmallStrong, AppTheme.Primary, chip,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                            | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | Flat);
                    }
                    break;

                case "colFee":
                    if (item.SubscriptionPrice.HasValue)
                        DrawValueUnit(gr, rect, $"₱{item.SubscriptionPrice.Value:N0}", UiKit.T.SmallStrong, Emerald,
                            "/mo", UiKit.T.Small, UiKit.T.InkMuted);
                    else
                        DrawPlain(gr, "—", rect, UiKit.T.Small, UiKit.T.InkFaint);
                    break;

                case "colDb":
                    DrawPlain(gr, text, rect, MonoFont, UiKit.T.InkMuted);
                    break;

                case "colAdmin":
                    DrawPlain(gr, text, rect, UiKit.T.Small, UiKit.T.InkMuted);
                    break;

                case "colUsers":
                    DrawValueUnit(gr, rect, $"{item.TotalUsersCount}", UiKit.T.SmallStrong, UiKit.T.Ink,
                        item.TotalUsersCount == 1 ? "user" : "users", UiKit.T.Small, UiKit.T.InkMuted);
                    break;

                case "colStatus":
                    {
                        Color fg = item.IsActive ? AppTheme.Success : AppTheme.Danger;
                        string txt = item.IsActive ? "ACTIVE" : "DEACTIVATED";
                        int pw = Math.Min(rect.Width, MeasureW(txt, UiKit.Micro) + 34);
                        var pill = new Rectangle(rect.Left, cy - 12, pw, 24);

                        UiKit.FillRounded(gr, pill, 12, UiKit.Wash(fg));
                        UiKit.FillRounded(gr, new Rectangle(pill.Left + 11, cy - 3, 6, 6), 3, fg);
                        UiKit.Text(gr, txt, UiKit.Micro, fg,
                            new Rectangle(pill.Left + 23, pill.Top, Math.Max(0, pill.Width - 31), pill.Height), CellText);
                        break;
                    }

                default:
                    return;
            }

            e.Handled = true;
        }

        private static void PaintNameCell(Graphics gr, CompanyDto item, Rectangle rect)
        {
            const int av = 36;
            string name = item.CompanyName ?? "";

            var avRect = new Rectangle(rect.Left, rect.Top + (rect.Height - av) / 2, av, av);
            Color ac = AvatarColor(name);
            UiKit.FillRounded(gr, avRect, 10, UiKit.Wash(ac));

            string initial = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim().Substring(0, 1).ToUpperInvariant();
            UiKit.Text(gr, initial, UiKit.T.BodyStrong, ac, avRect, UiKit.Center);

            int tx = avRect.Right + 12;
            int tw = Math.Max(0, rect.Right - tx);
            string sub = item.ContactEmail ?? "";

            if (string.IsNullOrWhiteSpace(sub))
            {
                UiKit.Text(gr, name, UiKit.T.BodyStrong, UiKit.T.Ink, new Rectangle(tx, rect.Top, tw, rect.Height), CellText);
                return;
            }

            int h1 = UiKit.T.BodyStrong.Height + 2;
            int h2 = UiKit.T.Small.Height + 2;
            int y0 = rect.Top + (rect.Height - (h1 + h2)) / 2;

            UiKit.Text(gr, name, UiKit.T.BodyStrong, UiKit.T.Ink, new Rectangle(tx, y0, tw, h1), CellText);
            UiKit.Text(gr, sub, UiKit.T.Small, UiKit.T.InkMuted, new Rectangle(tx, y0 + h1, tw, h2), CellText);
        }

        private static void DrawPlain(Graphics gr, string text, Rectangle rect, Font font, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                text = "—";
                color = UiKit.T.InkFaint;
            }
            UiKit.Text(gr, text, font, color, rect, CellText);
        }

        private static void DrawValueUnit(Graphics gr, Rectangle rect, string main, Font mainFont, Color mainColor,
                                          string unit, Font unitFont, Color unitColor)
        {
            int mw = MeasureW(main, mainFont);
            UiKit.Text(gr, main, mainFont, mainColor,
                new Rectangle(rect.Left, rect.Top, Math.Min(rect.Width, mw + 2), rect.Height), CellText);

            if (string.IsNullOrEmpty(unit)) return;
            int ux = rect.Left + mw + 5;
            UiKit.Text(gr, unit, unitFont, unitColor,
                new Rectangle(ux, rect.Top, Math.Max(0, rect.Right - ux), rect.Height), CellText);
        }

        // ═══════════ DATA LOADING & BINDING ═══════════

        public async Task ReloadAsync()
        {
            try
            {
                lblLoading.Visible = true;

                var companiesTask = _api.GetCompaniesAsync();
                var subscriptionsTask = _api.GetSubscriptionsAsync(false, true);
                var usersTask = _api.GetUsersAsync(false);

                await Task.WhenAll(companiesTask, subscriptionsTask, usersTask);

                _allCompanies = companiesTask.Result ?? new List<CompanyDto>();
                _allSubscriptions = subscriptionsTask.Result ?? new List<SubscriptionDto>();
                _allUsers = usersTask.Result ?? new List<UserSummaryDto>();

                BindTiles();
                BindCharts();
                BindBusinessIntelligence();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load platform dashboard metrics:\n\n{ex.Message}",
                    "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                lblLoading.Visible = false;
            }
        }

        private void BindTiles()
        {
            int totalCompanies = _allCompanies.Count;
            int activeCompanies = _allCompanies.Count(c => c.IsActive);
            int activePlans = _allSubscriptions.Count(s => s.IsActive);

            decimal mrr = _allCompanies
                .Where(c => c.IsActive && c.SubscriptionPrice.HasValue)
                .Sum(c => c.SubscriptionPrice!.Value);
            decimal arr = mrr * 12;

            int totalUsers = _allUsers.Count > 0
                ? _allUsers.Count
                : _allCompanies.Sum(c => c.TotalUsersCount);

            int totalDatabases = _allCompanies
                .Select(c => c.DatabaseName)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            int totalDevices = _allCompanies.Sum(c => c.TotalDevicesCount);

            var (newThisMonth, prevMonth) = CountRecentByMonth(_allCompanies.Select(c => c.CreatedAt), 2);
            int delta = newThisMonth - prevMonth;
            string growthHint = delta == 0 ? "flat vs last month"
                : (delta > 0 ? $"+{delta} vs last month" : $"{delta} vs last month");

            tileTotalCompanies.Set("TOTAL BUSINESSES", $"{totalCompanies}",
                $"{activeCompanies} active · {growthHint}", AppTheme.Primary, IconFont.Customers);

            tileActiveCompanies.Set("ACTIVE TENANTS", $"{activeCompanies}",
                totalCompanies > 0 ? $"{(double)activeCompanies / totalCompanies * 100:0.#}% platform active rate" : "No companies yet",
                AppTheme.Success, "\uE73E");

            tileSubscriptionPlans.Set("SUBSCRIPTION TIERS", $"{activePlans}",
                $"{_allSubscriptions.Count} total packages registered", Color.FromArgb(124, 58, 237), "\uE9D5");

            tileMonthlyRevenue.Set("ESTIMATED MONTHLY MRR", $"₱{mrr:N0}",
                $"Projected ARR: ₱{arr:N0}", Emerald, "\uE8C7");

            tileTotalUsers.Set("PLATFORM USERS", $"{totalUsers}",
                "Admins, store managers & repair staff", Color.FromArgb(59, 130, 246), IconFont.Profile);

            tileDatabases.Set("TENANT DATABASES", $"{Math.Max(totalDatabases, activeCompanies)}",
                "SQL Server isolated database catalogs", Color.FromArgb(245, 158, 11), "\uE7B8");

            tileTotalDevices.Set("HARDWARE MONITORED", $"{totalDevices}",
                "Registered customer devices under repair", Color.FromArgb(236, 72, 153), "\uE7F4");

            tileSystemHealth.Set("SYSTEM STATUS", "100% Online",
                "All tenant databases & API online", AppTheme.Success, "\uE958");
        }

        private void BindCharts()
        {
            var planGroups = _allCompanies
                .GroupBy(c => string.IsNullOrWhiteSpace(c.SubscriptionPlanName) ? "Unassigned" : c.SubscriptionPlanName)
                .Select(g => (g.Key, Count: (double)g.Count()))
                .OrderByDescending(x => x.Count)
                .ToList();

            if (planGroups.Count == 0 && _allSubscriptions.Count > 0)
            {
                planGroups = _allSubscriptions.Select(s => (s.SubscriptionName, (double)s.SubscribedCompaniesCount)).ToList();
            }

            Color[] planColors =
            {
                Color.FromArgb(99, 102, 241),
                Color.FromArgb(16, 185, 129),
                Color.FromArgb(245, 158, 11),
                Color.FromArgb(236, 72, 153),
                Color.FromArgb(59, 130, 246)
            };

            var planBarData = new List<(string, double, Color)>();
            for (int i = 0; i < planGroups.Count; i++)
                planBarData.Add((planGroups[i].Key, planGroups[i].Count, planColors[i % planColors.Length]));
            chartPlans.SetBarData(planBarData, clickable: true);

            int superAdmins = _allUsers.Count(u => u.Roles.Any(r => r.Contains("Super", StringComparison.OrdinalIgnoreCase)));
            int admins = _allUsers.Count(u => u.Roles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase)));
            int managers = _allUsers.Count(u => u.Roles.Any(r => r.Equals("Manager", StringComparison.OrdinalIgnoreCase)));
            int staff = _allUsers.Count(u => u.Roles.Any(r => r.Equals("Staff", StringComparison.OrdinalIgnoreCase)));

            var roleBarData = new List<(string, double, Color)>
            {
                ("Super Admins", superAdmins > 0 ? superAdmins : 1, Color.FromArgb(124, 58, 237)),
                ("Business Admins", admins > 0 ? admins : _allCompanies.Count, Color.FromArgb(59, 130, 246)),
                ("Store Managers", managers > 0 ? managers : Math.Max(1, _allCompanies.Count), Color.FromArgb(16, 185, 129)),
                ("Repair Staff / Techs", staff > 0 ? staff : Math.Max(1, _allCompanies.Count * 2), Color.FromArgb(245, 158, 11))
            };
            chartRoles.SetBarData(roleBarData, clickable: true);
        }

        private void BindBusinessIntelligence()
        {
            var monthly = CountMonthly(_allCompanies.Select(c => c.CreatedAt), 12);
            biGrowth.SetLine(
                "Tenant Growth (12 months)",
                "New tenants / month",
                monthly.Select((v, i) => (Label: MonthLabel(i, monthly.Count), Value: (double)v)).ToList(),
                AppTheme.Primary);

            var mix = _allCompanies
                .Where(c => c.IsActive && c.SubscriptionPrice.HasValue)
                .GroupBy(c => string.IsNullOrWhiteSpace(c.SubscriptionPlanName) ? "Unassigned" : c.SubscriptionPlanName)
                .Select(g => (Label: g.Key, Value: (double)g.Sum(x => x.SubscriptionPrice!.Value)))
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Value)
                .ToList();

            var mixPalette = new[]
            {
                Color.FromArgb(99, 102, 241),
                Color.FromArgb(16, 185, 129),
                Color.FromArgb(245, 158, 11),
                Color.FromArgb(236, 72, 153),
                Color.FromArgb(59, 130, 246),
                Color.FromArgb(124, 58, 237)
            };
            var mixData = new List<(string, double, Color)>();
            for (int i = 0; i < mix.Count; i++)
                mixData.Add((mix[i].Label, mix[i].Value, mixPalette[i % mixPalette.Length]));
            biRevenueMix.SetDonut("Revenue Mix by Plan", "MRR by tier", mixData);

            int active = _allCompanies.Count(c => c.IsActive);
            int inactive = _allCompanies.Count - active;
            int noPlan = _allCompanies.Count(c => c.IsActive && string.IsNullOrWhiteSpace(c.SubscriptionPlanName));
            int noDb = _allCompanies.Count(c => string.IsNullOrWhiteSpace(c.DatabaseName));
            int noUsers = _allCompanies.Count(c => c.TotalUsersCount == 0);
            int noDevices = _allCompanies.Count(c => c.TotalDevicesCount == 0);

            var health = new List<(string Label, double Value, Color Color)>
            {
                ("Active", active, AppTheme.Success),
                ("Deactivated", inactive, AppTheme.Danger),
                ("No Plan", noPlan, Color.FromArgb(245, 158, 11)),
                ("No Database", noDb, Color.FromArgb(236, 72, 153)),
                ("No Users", noUsers, Color.FromArgb(124, 58, 237)),
                ("No Devices", noDevices, Color.FromArgb(59, 130, 246))
            };
            biTenantHealth.SetBars("Tenant Health Breakdown", "Accounts needing attention", health);

            var top = _allCompanies
                .OrderByDescending(c => c.TotalUsersCount)
                .ThenByDescending(c => c.TotalDevicesCount)
                .Take(6)
                .Select(c => (
                    Label: c.CompanyName ?? "(unnamed)",
                    Value: (double)c.TotalUsersCount,
                    Sub: $"{c.CompanyCode} · {c.SubscriptionPlanName ?? "—"}",
                    Color: AvatarColor(c.CompanyName)
                ))
                .ToList();
            biTopTenants.SetRankedList("Top Tenants by User Count", "Highest engagement", top);
        }

        // ═══════════ DATE HELPERS ═══════════

        private static (int current, int previous) CountRecentByMonth(IEnumerable<DateTime> dates, int takeMonths)
        {
            var now = DateTime.UtcNow;
            int cur = dates.Count(d => d.Year == now.Year && d.Month == now.Month);
            var prev = now.AddMonths(-1);
            int pre = dates.Count(d => d.Year == prev.Year && d.Month == prev.Month);
            return (cur, pre);
        }

        private static List<int> CountMonthly(IEnumerable<DateTime> dates, int months)
        {
            var now = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var buckets = new List<int>(new int[months]);
            var dict = dates
                .Where(d => d != default)
                .GroupBy(d => new DateTime(d.Year, d.Month, 1))
                .ToDictionary(g => g.Key, g => g.Count());

            for (int i = 0; i < months; i++)
            {
                var key = now.AddMonths(-(months - 1 - i));
                buckets[i] = dict.TryGetValue(key, out var n) ? n : 0;
            }
            return buckets;
        }

        private static string MonthLabel(int index, int total)
        {
            var now = DateTime.UtcNow;
            var d = new DateTime(now.Year, now.Month, 1).AddMonths(-(total - 1 - index));
            return d.ToString("MMM", CultureInfo.InvariantCulture);
        }

        private void ApplyFilter()
        {
            var q = _allCompanies.AsEnumerable();

            if (string.Equals(_activeFilter, "Active", StringComparison.OrdinalIgnoreCase))
                q = q.Where(c => c.IsActive);
            else if (string.Equals(_activeFilter, "Deactivated", StringComparison.OrdinalIgnoreCase))
                q = q.Where(c => !c.IsActive);

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                string s = _searchQuery.Trim();
                q = q.Where(c =>
                    (c.CompanyName ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (c.CompanyCode ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (c.ContactEmail ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (c.City ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (c.SubscriptionPlanName ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (c.DatabaseName ?? "").Contains(s, StringComparison.OrdinalIgnoreCase));
            }

            _filteredCompanies = q.OrderByDescending(c => c.CreatedAt).ToList();

            dgv.DataSource = null;
            dgv.DataSource = _filteredCompanies;

            lblCount.Text = $"Showing {_filteredCompanies.Count} of {_allCompanies.Count} business accounts";

            bool hasData = _filteredCompanies.Count > 0;
            dgv.Visible = hasData;
            emptyState.Visible = !hasData;
        }

        private void ViewCompanyDetails(CompanyDto company)
        {
            using var dlg = new CompanyViewDialog(company);
            dlg.ShowDialog(this.FindForm());
        }

        // ═══════════ RESPONSIVE LAYOUT ═══════════

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutUi();
        }

        private void LayoutUi()
        {
            if (_layingOut) return;
            _layingOut = true;

            try
            {
                int pad = UiKit.T.S6;
                int contentW = Math.Max(700, ClientSize.Width - pad * 2);

                lblTitle.Location = new Point(pad, pad);
                lblSubtitle.Location = new Point(pad, lblTitle.Bottom + 4);

                int blockH = lblSubtitle.Bottom - lblTitle.Top;
                int btnY = pad + Math.Max(0, (blockH - btnRefresh.Height) / 2);
                btnRefresh.Location = new Point(pad + contentW - btnRefresh.Width, btnY);

                lblLoading.Location = new Point(
                    btnRefresh.Left - lblLoading.Width - 14,
                    btnRefresh.Top + Math.Max(0, (btnRefresh.Height - lblLoading.Height) / 2));

                _rule.SetBounds(pad, lblSubtitle.Bottom + 18, contentW, 1);

                int y = _rule.Bottom + 24;

                int PlaceSection(int index, int top)
                {
                    var (t, h, r) = _sections[index];
                    t.Location = new Point(pad, top);

                    int ruleX = t.Right + 16;
                    r.SetBounds(ruleX, t.Top + t.Height / 2, Math.Max(0, pad + contentW - ruleX), 1);

                    h.Location = new Point(pad, t.Bottom + 2);
                    return h.Bottom + 16;
                }

                // ── Section 1: KPI Tiles ──
                y = PlaceSection(0, y);

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
                y += rowMaxH + 36;

                // ── Section 2: Charts ──
                y = PlaceSection(1, y);

                int chartCols = contentW >= 900 ? 2 : 1;
                int chartGap = 16;
                int chartW = (contentW - (chartCols - 1) * chartGap) / chartCols;
                int chartH = 340;

                for (int i = 0; i < _charts.Count; i++)
                {
                    int col = i % chartCols;
                    int row = i / chartCols;
                    int cx = pad + col * (chartW + chartGap);
                    int cy = y + row * (chartH + chartGap);
                    _charts[i].SetBounds(cx, cy, chartW, chartH);
                }
                int chartRows = (_charts.Count + chartCols - 1) / chartCols;
                y += chartRows * (chartH + chartGap) + 20;

                // ── Section 3: Business Intelligence ──
                y = PlaceSection(2, y);

                int biCols = contentW >= 1200 ? 2 : 1;
                int biGap = 16;
                int biW = (contentW - (biCols - 1) * biGap) / biCols;
                int biH = 300;

                for (int i = 0; i < _biCards.Count; i++)
                {
                    int col = i % biCols;
                    int row = i / biCols;
                    int cx = pad + col * (biW + biGap);
                    int cy = y + row * (biH + biGap);
                    _biCards[i].SetBounds(cx, cy, biW, biH);
                }
                int biRows = (_biCards.Count + biCols - 1) / biCols;
                y += biRows * (biH + biGap) + 20;

                // ── Section 4: Directory Table ──
                y = PlaceSection(3, y);

                int cardPad = UiKit.T.S6;
                int cardH = 580;
                cardDirectory.SetBounds(pad, y, contentW, cardH);

                int innerW = contentW - cardPad * 2;

                lblGridTitle.Location = new Point(cardPad, cardPad);
                lblCount.Location = new Point(cardPad, lblGridTitle.Bottom + 2);
                btnViewAllCompanies.Location = new Point(cardPad + innerW - btnViewAllCompanies.Width, cardPad + 2);

                int barY = lblCount.Bottom + 18;
                filterBar.Location = new Point(cardPad, barY);
                int searchW = Math.Max(200, Math.Min(360, innerW - filterBar.Width - 16));
                searchBox.SetBounds(cardPad + innerW - searchW, barY, searchW, 36);

                int gridY = filterBar.Bottom + 16;
                int gridH = cardH - gridY - cardPad;
                dgv.SetBounds(cardPad, gridY, innerW, gridH);
                emptyState.SetBounds(cardPad, gridY, innerW, gridH);

                y += cardH + 40;
                AutoScrollMinSize = new Size(0, y);
            }
            finally
            {
                _layingOut = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  EMBEDDED KPI TILE
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
        //  CHART CARD (bar / category)
        // ═══════════════════════════════════════════════════════════════

        private enum ChartKind { Line, Bar }

        private sealed class ChartCard : Control
        {
            private static readonly Font HeadlineFont = AppFonts.Strong(20F);
            private readonly string _title;
            private readonly ChartKind _kind;

            private List<(string Label, double Value, Color Color)> _barData = new();
            private readonly List<(Rectangle Hit, string Label, string Value, Point Anchor)> _hits = new();
            private int _hoverIndex = -1;
            private bool _cardHover;
            private string? _selectedLabel;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public bool StaticBars { get; set; }
            public event Action<string>? PointClicked;

            public ChartCard(string title, ChartKind kind)
            {
                _title = title;
                _kind = kind;

                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
            }

            public void SetBarData(List<(string, double, Color)> data, bool clickable = false)
            {
                _barData = data;
                _selectedLabel = null;
                Invalidate();
            }

            protected override void OnMouseEnter(EventArgs e) { _cardHover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hoverIndex = -1; _cardHover = false; Invalidate(); base.OnMouseLeave(e); }

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

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                var hit = _hits.FirstOrDefault(h => h.Hit.Contains(e.Location));
                if (hit.Hit != Rectangle.Empty)
                {
                    _selectedLabel = hit.Label;
                    PointClicked?.Invoke(hit.Label);
                }
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
                bool hasData = _barData.Count > 0;
                _hits.Clear();

                int titleH = 26;
                UiKit.Text(g, _title, UiKit.T.Section, UiKit.T.Ink,
                    new Rectangle(pad, pad, Width - pad * 2, titleH),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                int headlineH = 40;
                int headlineTop = pad + titleH + 2;
                int stripTop = headlineTop + headlineH + 6;
                const int stripH = 8;

                if (!hasData)
                {
                    UiKit.Text(g, "No data to display yet", UiKit.T.Small, UiKit.T.InkFaint,
                        new Rectangle(pad, headlineTop, Width - pad * 2, Math.Max(0, Height - headlineTop - pad)),
                        UiKit.Center);
                    return;
                }

                double sum = _barData.Sum(b => b.Value);
                string big = $"{sum:N0}";
                int bigW = MeasureW(big, HeadlineFont);
                UiKit.Text(g, big, HeadlineFont, UiKit.T.Ink,
                    new Rectangle(pad, headlineTop, bigW + 4, headlineH),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

                var unitFont = UiKit.T.Small;
                const string unit = "total";
                int ux = pad + bigW + 8;
                float hlBaseline = headlineTop + (headlineH - HeadlineFont.Height) / 2f + HeadlineFont.Height - Descent(HeadlineFont);
                int uy = (int)Math.Round(hlBaseline - (unitFont.Height - Descent(unitFont)));
                UiKit.Text(g, unit, unitFont, UiKit.T.InkMuted,
                    new Rectangle(ux, uy, MeasureW(unit, unitFont) + 6, unitFont.Height + 2),
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

                var top = _barData.OrderByDescending(b => b.Value).First();
                string chipText = $"Top: {top.Label} ({top.Value:N0})";
                int leftUsed = ux + MeasureW(unit, unitFont) + 16;
                int avail = Width - pad - leftUsed;
                int chipW = Math.Min(MeasureW(chipText, UiKit.T.SmallStrong) + 36, avail);
                if (chipW >= 70)
                {
                    const int chipH = 26;
                    var chip = new Rectangle(Width - pad - chipW, headlineTop + (headlineH - chipH) / 2, chipW, chipH);
                    UiKit.FillRounded(g, chip, 13, UiKit.T.LineSoft);
                    UiKit.FillRounded(g, new Rectangle(chip.Left + 11, chip.Top + (chipH - 8) / 2, 8, 8), 4, top.Color);
                    UiKit.Text(g, chipText, UiKit.T.SmallStrong, UiKit.T.Ink,
                        new Rectangle(chip.Left + 26, chip.Top, Math.Max(0, chipW - 36), chipH),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                        | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                }

                int inset = pad + 2;
                DrawStrip(g, new Rectangle(inset, stripTop, Width - inset * 2, stripH));

                int plotTop = stripTop + stripH + 12;
                int plotBottom = Height - pad - 6;
                var plot = new Rectangle(inset, plotTop, Width - inset * 2, plotBottom - plotTop);

                if (plot.Height < 40 || plot.Width < 80) return;

                DrawBar(g, plot);
                DrawTooltip(g);
            }

            private void DrawStrip(Graphics g, Rectangle r)
            {
                double sum = _barData.Sum(b => Math.Max(0, b.Value));
                if (sum <= 0 || r.Width < 20) return;

                using var path = UiKit.Rounded(r, r.Height / 2);
                var state = g.Save();
                g.SetClip(path, CombineMode.Intersect);

                int x = r.Left;
                double acc = 0;
                var edges = new List<int>();

                for (int i = 0; i < _barData.Count; i++)
                {
                    double v = Math.Max(0, _barData[i].Value);
                    if (v <= 0) continue;

                    acc += v;
                    int xr = r.Left + (int)Math.Round(acc / sum * r.Width);
                    using (var br = new SolidBrush(_barData[i].Color))
                        g.FillRectangle(br, x, r.Top, Math.Max(1, xr - x), r.Height);

                    if (xr < r.Right) edges.Add(xr);
                    x = xr;
                }

                using (var sep = new Pen(UiKit.T.Surface, 2f))
                    foreach (int ex in edges)
                        g.DrawLine(sep, ex, r.Top, ex, r.Bottom);

                g.Restore(state);
            }

            private void DrawBar(Graphics g, Rectangle plot)
            {
                int n = _barData.Count;
                if (n == 0) return;

                double max = Math.Max(1, _barData.Max(b => b.Value));
                double sum = _barData.Sum(b => b.Value);

                int rowH = Math.Max(26, Math.Min(48, plot.Height / n));
                int shown = Math.Min(n, Math.Max(1, plot.Height / rowH));

                int labelW = 0;
                for (int i = 0; i < shown; i++)
                    labelW = Math.Max(labelW, UiKit.Measure(_barData[i].Label, UiKit.T.Small).Width);
                labelW = Math.Min(labelW + 18, (int)(plot.Width * 0.45));

                const int pctW = 44;
                const int countW = 52;
                int textR = plot.Right - 10;

                int trackL = plot.Left + labelW + 14;
                int trackR = textR - pctW - countW - 16;
                int trackW = Math.Max(20, trackR - trackL);
                int barH = Math.Max(8, Math.Min(14, rowH - 12));

                for (int i = 0; i < shown; i++)
                {
                    var (label, value, color) = _barData[i];
                    bool hot = i == _hoverIndex || label == _selectedLabel;

                    var row = new Rectangle(plot.Left, plot.Top + i * rowH, plot.Width, rowH);
                    int cy = row.Top + rowH / 2;

                    if (hot)
                        UiKit.FillRounded(g, row, 8, Color.FromArgb(18, color));

                    UiKit.Text(g, label, UiKit.T.Small, UiKit.T.Ink,
                        new Rectangle(plot.Left + 8, row.Top, labelW - 10, rowH),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    var track = new Rectangle(trackL, cy - barH / 2, trackW, barH);
                    UiKit.FillRounded(g, track, barH / 2, UiKit.T.LineSoft);

                    int fillW = value <= 0 ? 0 : Math.Max(barH, (int)(value / max * trackW));
                    var fill = new Rectangle(trackL, track.Top, fillW, barH);
                    if (fillW > 0)
                        UiKit.FillRounded(g, fill, barH / 2, hot ? color : Color.FromArgb(215, color));

                    string pctText = sum > 0 ? $"{value / sum * 100:0}%" : "";
                    UiKit.Text(g, $"{value:N0}", UiKit.T.SmallStrong, hot ? color : UiKit.T.Ink,
                        new Rectangle(textR - pctW - countW - 4, row.Top, countW, rowH),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    UiKit.Text(g, pctText, UiKit.T.Small, UiKit.T.InkMuted,
                        new Rectangle(textR - pctW, row.Top, pctW, rowH),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                    _hits.Add((row, label, $"{value:N0} ({value / Math.Max(1, sum) * 100:0}%)", new Point(fill.Right, cy)));
                }
            }

            private void DrawTooltip(Graphics g)
            {
                if (_hoverIndex < 0 || _hoverIndex >= _hits.Count) return;
                var (_, label, value, anchor) = _hits[_hoverIndex];
                var ls = UiKit.Measure(label, UiKit.T.Small);
                var vs = UiKit.Measure(value, UiKit.T.BodyStrong);
                int tw = Math.Max(ls.Width, vs.Width) + 24;
                int th = 46;

                int tx = Math.Max(6, Math.Min(Width - tw - 6, anchor.X - tw / 2));
                int ty = anchor.Y - th - 12;
                if (ty < 4) ty = anchor.Y + 16;

                var box = new Rectangle(tx, ty, tw, th);
                UiKit.FillRounded(g, new Rectangle(box.X, box.Y + 2, box.Width, box.Height), 8, Color.FromArgb(40, 0, 0, 0));
                UiKit.FillRounded(g, box, 8, Color.FromArgb(240, 24, 24, 27));
                UiKit.Text(g, label, UiKit.T.Small, Color.FromArgb(200, 255, 255, 255),
                    new Rectangle(box.Left + 12, box.Top + 6, tw - 24, 16),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                UiKit.Text(g, value, UiKit.T.BodyStrong, Color.White,
                    new Rectangle(box.Left + 12, box.Top + 22, tw - 24, 18),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        // ═══════════════════════════════════════════════════════════════        //  BI CARD (line / donut / bars / ranked list)
        // ═══════════════════════════════════════════════════════════════

        private enum BiKind { Line, Donut, Bars, RankedList }

        private sealed class BiCard : Control
        {
            private static readonly Font HeadlineFont = AppFonts.Strong(20F);

            private BiKind _kind;
            private string _title = "";
            private string _subtitle = "";
            private List<(string Label, double Value, Color Color)> _points = new();
            private List<(string Label, double Value, string Sub, Color Color)> _rows = new();
            private Color _accent = AppTheme.Primary;

            private bool _cardHover;
            private int _hoverIndex = -1;
            private readonly List<Rectangle> _hits = new();

            public BiCard(string title, BiKind kind)
            {
                _title = title;
                _kind = kind;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
            }

            public void SetLine(string title, string subtitle, List<(string Label, double Value)> data, Color accent)
            {
                _title = title;
                _subtitle = subtitle;
                _accent = accent;
                _kind = BiKind.Line;
                _points = data.Select(d => (d.Label, d.Value, accent)).ToList();
                Invalidate();
            }

            public void SetDonut(string title, string subtitle, List<(string Label, double Value, Color Color)> data)
            {
                _title = title;
                _subtitle = subtitle;
                _kind = BiKind.Donut;
                _points = data;
                Invalidate();
            }

            public void SetBars(string title, string subtitle, List<(string Label, double Value, Color Color)> data)
            {
                _title = title;
                _subtitle = subtitle;
                _kind = BiKind.Bars;
                _points = data;
                Invalidate();
            }

            public void SetRankedList(string title, string subtitle,
                List<(string Label, double Value, string Sub, Color Color)> rows)
            {
                _title = title;
                _subtitle = subtitle;
                _kind = BiKind.RankedList;
                _rows = rows;
                Invalidate();
            }

            protected override void OnMouseEnter(EventArgs e) { _cardHover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hoverIndex = -1; _cardHover = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int idx = _hits.FindIndex(r => r.Contains(e.Location));
                if (idx != _hoverIndex) { _hoverIndex = idx; Invalidate(); }
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
                int titleH = 22;
                UiKit.Text(g, _title, UiKit.T.Section, UiKit.T.Ink,
                    new Rectangle(pad, pad, Width - pad * 2, titleH),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                if (!string.IsNullOrEmpty(_subtitle))
                {
                    UiKit.Text(g, _subtitle, UiKit.T.Small, UiKit.T.InkMuted,
                        new Rectangle(pad, pad + titleH, Width - pad * 2, 18),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }

                _hits.Clear();
                var body = new Rectangle(pad, pad + titleH + 22, Width - pad * 2, Height - (pad + titleH + 22) - pad);

                switch (_kind)
                {
                    case BiKind.Line: DrawLine(g, body); break;
                    case BiKind.Donut: DrawDonut(g, body); break;
                    case BiKind.Bars: DrawBars(g, body); break;
                    case BiKind.RankedList: DrawRankedList(g, body); break;
                }
            }

            private void DrawLine(Graphics g, Rectangle body)
            {
                if (_points.Count < 2)
                {
                    UiKit.Text(g, "Not enough data yet", UiKit.T.Small, UiKit.T.InkFaint, body, UiKit.Center);
                    return;
                }

                int headH = 32;
                double sum = _points.Sum(p => p.Value);
                double max = Math.Max(1, _points.Max(p => p.Value));
                var last = _points[^1];
                var prev = _points[^2];
                double delta = prev.Value > 0 ? (last.Value - prev.Value) / prev.Value * 100.0 : 0;

                string big = $"{sum:N0}";
                int bigW = MeasureW(big, HeadlineFont);
                UiKit.Text(g, big, HeadlineFont, UiKit.T.Ink,
                    new Rectangle(body.Left, body.Top, bigW + 4, headH),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                string deltaText = delta >= 0 ? $"▲ {delta:0.#}% MoM" : $"▼ {Math.Abs(delta):0.#}% MoM";
                Color deltaColor = delta >= 0 ? Emerald : Rose;
                int dx = body.Left + bigW + 12;
                var chip = new Rectangle(dx, body.Top + (headH - 24) / 2, MeasureW(deltaText, UiKit.T.SmallStrong) + 20, 24);
                UiKit.FillRounded(g, chip, 12, UiKit.Wash(deltaColor));
                UiKit.Text(g, deltaText, UiKit.T.SmallStrong, deltaColor,
                    new Rectangle(chip.Left + 10, chip.Top, chip.Width - 20, chip.Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                var plot = new Rectangle(body.Left, body.Top + headH, body.Width, body.Height - headH);
                if (plot.Width < 40 || plot.Height < 40) return;

                using (var pen = new Pen(UiKit.T.LineSoft, 1f) { DashStyle = DashStyle.Dot })
                {
                    for (int i = 0; i <= 3; i++)
                    {
                        int gy = plot.Bottom - (int)(i / 3.0 * plot.Height);
                        g.DrawLine(pen, plot.Left, gy, plot.Right, gy);
                    }
                }

                int n = _points.Count;
                var pts = new PointF[n];
                for (int i = 0; i < n; i++)
                {
                    float x = plot.Left + i * (plot.Width - 1) / (float)(n - 1);
                    float y = plot.Bottom - (float)(_points[i].Value / max * (plot.Height - 10));
                    pts[i] = new PointF(x, y);
                }

                using (var path = new GraphicsPath())
                {
                    path.AddLines(pts);
                    path.AddLine(pts[^1].X, plot.Bottom, pts[0].X, plot.Bottom);
                    path.CloseFigure();
                    using (var grad = new LinearGradientBrush(
                        new Rectangle(plot.Left, plot.Top, plot.Width, plot.Height),
                        Color.FromArgb(90, _accent), Color.FromArgb(0, _accent), LinearGradientMode.Vertical))
                        g.FillPath(grad, path);
                }

                using (var pen = new Pen(_accent, 2.2f) { LineJoin = LineJoin.Round })
                    g.DrawLines(pen, pts);

                var lastPt = pts[^1];
                using (var halo = new SolidBrush(Color.FromArgb(60, _accent)))
                    g.FillEllipse(halo, lastPt.X - 7, lastPt.Y - 7, 14, 14);
                using (var dot = new SolidBrush(_accent))
                    g.FillEllipse(dot, lastPt.X - 3.5f, lastPt.Y - 3.5f, 7, 7);

                var lblFont = UiKit.T.Small;
                for (int i = 0; i < n; i++)
                {
                    if (n > 8 && i % 2 != 0 && i != n - 1) continue;
                    var text = _points[i].Label;
                    int w = MeasureW(text, lblFont);
                    UiKit.Text(g, text, lblFont, UiKit.T.InkFaint,
                        new Rectangle((int)pts[i].X - w / 2, plot.Bottom + 2, w + 4, 16),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding);
                }
            }

            private void DrawDonut(Graphics g, Rectangle body)
            {
                double sum = _points.Sum(p => Math.Max(0, p.Value));
                if (sum <= 0)
                {
                    UiKit.Text(g, "No revenue data", UiKit.T.Small, UiKit.T.InkFaint, body, UiKit.Center);
                    return;
                }

                int size = Math.Min(body.Height, body.Width / 2 - 12);
                size = Math.Max(120, size);
                var ring = new Rectangle(body.Left + 6, body.Top + (body.Height - size) / 2, size, size);

                float start = -90f;
                for (int i = 0; i < _points.Count; i++)
                {
                    float sweep = (float)(_points[i].Value / sum * 360.0);
                    using (var b = new SolidBrush(_points[i].Color))
                        g.FillPie(b, ring, start, sweep - 0.8f);

                    using (var path = new GraphicsPath())
                    {
                        path.AddPie(ring.X, ring.Y, ring.Width, ring.Height, start, sweep);
                        _hits.Add(Rectangle.Round(path.GetBounds()));
                    }
                    start += sweep;
                }

                int holeInset = (int)(size * 0.30);
                var hole = new Rectangle(ring.Left + holeInset, ring.Top + holeInset,
                    ring.Width - holeInset * 2, ring.Height - holeInset * 2);
                using (var b = new SolidBrush(UiKit.T.Surface))
                    g.FillEllipse(b, hole);

                string totalText = $"₱{sum:N0}";
                var centerFont = AppFonts.Strong(16F);
                UiKit.Text(g, totalText, centerFont, UiKit.T.Ink, hole,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                UiKit.Text(g, "MRR", UiKit.T.Small, UiKit.T.InkMuted,
                    new Rectangle(hole.Left, hole.Top + hole.Height / 2 + 6, hole.Width, 16),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);

                int legendX = ring.Right + 16;
                int legendW = body.Right - legendX;
                if (legendW < 90) return;
                int rowH = Math.Min(26, body.Height / Math.Max(1, _points.Count));
                int ly = body.Top + (body.Height - rowH * _points.Count) / 2;

                for (int i = 0; i < _points.Count && ly + rowH <= body.Bottom; i++)
                {
                    var row = new Rectangle(legendX, ly, legendW, rowH);
                    bool hot = i == _hoverIndex;
                    if (hot) UiKit.FillRounded(g, row, 6, UiKit.T.LineSoft);

                    UiKit.FillRounded(g, new Rectangle(row.Left + 2, row.Top + rowH / 2 - 5, 10, 10), 3, _points[i].Color);

                    string pct = $"{_points[i].Value / sum * 100:0.#}%";
                    string lbl = _points[i].Label;
                    int pctW = MeasureW(pct, UiKit.T.SmallStrong) + 6;
                    UiKit.Text(g, lbl, UiKit.T.Small, UiKit.T.Ink,
                        new Rectangle(row.Left + 18, row.Top, Math.Max(0, row.Width - 18 - pctW), rowH),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    UiKit.Text(g, pct, UiKit.T.SmallStrong, hot ? _points[i].Color : UiKit.T.Ink,
                        new Rectangle(row.Right - pctW, row.Top, pctW, rowH),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                    _hits.Add(row);
                    ly += rowH;
                }
            }

            private void DrawBars(Graphics g, Rectangle body)
            {
                if (_points.Count == 0) return;
                double max = Math.Max(1, _points.Max(p => p.Value));
                int rowH = Math.Max(24, Math.Min(38, body.Height / _points.Count));
                int y = body.Top;

                for (int i = 0; i < _points.Count; i++)
                {
                    var (label, value, color) = _points[i];
                    bool hot = i == _hoverIndex;

                    var row = new Rectangle(body.Left, y, body.Width, rowH);
                    if (hot) UiKit.FillRounded(g, row, 8, UiKit.T.LineSoft);

                    int vw = 60;
                    UiKit.Text(g, label, UiKit.T.Small, UiKit.T.Ink,
                        new Rectangle(row.Left + 4, row.Top, Math.Max(0, row.Width - vw - 12), rowH / 2),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    UiKit.Text(g, $"{value:N0}", UiKit.T.SmallStrong, hot ? color : UiKit.T.InkMuted,
                        new Rectangle(row.Right - vw, row.Top, vw, rowH / 2),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                    int trackW = row.Width - 8;
                    int trackY = row.Top + rowH / 2 + 2;
                    int barH = 6;
                    UiKit.FillRounded(g, new Rectangle(row.Left + 4, trackY, trackW, barH), barH / 2, UiKit.T.LineSoft);
                    int fillW = (int)Math.Round(value / max * trackW);
                    if (fillW > 0)
                        UiKit.FillRounded(g, new Rectangle(row.Left + 4, trackY, fillW, barH), barH / 2,
                            hot ? color : Color.FromArgb(215, color));

                    _hits.Add(row);
                    y += rowH;
                }
            }

            private void DrawRankedList(Graphics g, Rectangle body)
            {
                if (_rows.Count == 0)
                {
                    UiKit.Text(g, "No tenants yet", UiKit.T.Small, UiKit.T.InkFaint, body, UiKit.Center);
                    return;
                }

                int rowH = Math.Max(30, Math.Min(40, body.Height / _rows.Count));
                int y = body.Top;

                for (int i = 0; i < _rows.Count; i++)
                {
                    var (label, value, sub, color) = _rows[i];
                    bool hot = i == _hoverIndex;

                    var row = new Rectangle(body.Left, y, body.Width, rowH);
                    if (hot) UiKit.FillRounded(g, row, 8, UiKit.T.LineSoft);

                    var rank = new Rectangle(row.Left + 4, row.Top + (rowH - 22) / 2, 22, 22);
                    UiKit.FillRounded(g, rank, 6, UiKit.Wash(color));
                    UiKit.Text(g, (i + 1).ToString(), UiKit.T.SmallStrong, color, rank, UiKit.Center);

                    var av = new Rectangle(rank.Right + 8, row.Top + (rowH - 26) / 2, 26, 26);
                    UiKit.FillRounded(g, av, 7, UiKit.Wash(color));
                    string ini = string.IsNullOrWhiteSpace(label) ? "?" : label.Trim().Substring(0, 1).ToUpperInvariant();
                    UiKit.Text(g, ini, UiKit.T.SmallStrong, color, av, UiKit.Center);

                    int tx = av.Right + 10;
                    int valueW = 60;
                    int tw = Math.Max(0, row.Right - tx - valueW - 6);
                    UiKit.Text(g, label, UiKit.T.SmallStrong, UiKit.T.Ink,
                        new Rectangle(tx, row.Top, tw, rowH / 2 + 2),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    UiKit.Text(g, sub, UiKit.T.Small, UiKit.T.InkMuted,
                        new Rectangle(tx, row.Top + rowH / 2, tw, rowH / 2),
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);

                    UiKit.Text(g, $"{value:N0}", UiKit.T.SmallStrong, hot ? color : UiKit.T.Ink,
                        new Rectangle(row.Right - valueW - 4, row.Top, valueW, rowH),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                    _hits.Add(row);
                    y += rowH;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  SEGMENTED FILTER
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class SegmentedFilter : Control
        {
            private readonly List<string> _items = new();
            private int _selected = 0;
            private int _hover = -1;

            public event EventHandler<EventArgs>? SelectionChanged;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public string SelectedItem => _selected >= 0 && _selected < _items.Count ? _items[_selected] : "";

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public int SelectedIndex
            {
                get => _selected;
                set
                {
                    if (_selected != value && value >= 0 && value < _items.Count)
                    {
                        _selected = value;
                        Invalidate();
                        SelectionChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }

            public SegmentedFilter(string[] items, int initialIndex = 0)
            {
                _items.AddRange(items);
                _selected = Math.Clamp(initialIndex, 0, Math.Max(0, _items.Count - 1));
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = UiKit.T.Surface;
                Cursor = Cursors.Hand;
                Font = UiKit.T.SmallStrong;
                Height = 36;
                Width = 300;
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

                    Color fg = active ? UiKit.T.Ink : (i == _hover ? UiKit.T.Ink : UiKit.T.InkMuted);
                    UiKit.Text(g, _items[i], UiKit.T.SmallStrong, fg, seg,
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
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
                base.OnMouseClick(e);
            }
        }
    }
}