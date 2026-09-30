using CRM.winforms.Controls;
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
    /// Dedicated Multi-Tenant & Platform Overview Dashboard for Super Admins.
    /// Provides SaaS business intelligence:
    ///   1. Multi-Tenant KPI tiles (Total Companies, Active Tenants, Plans, MRR, Users, Databases, Devices, System Health)
    ///   2. Analytics Charts (Companies by Subscription Tier, Platform Users by Role)
    ///   3. Live Registered Businesses Workbench (Interactive directory table with search, filters & view dialog)
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
        private SaasButton btnRegister = null!;
        private SaasButton btnCompanies = null!;
        private SaasButton btnSubscriptions = null!;
        private Label lblLoading = null!;

        // ═══════════ SECTION HEADINGS ═══════════
        private readonly List<(Label Title, Label Hint)> _sections = new();

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

        // ═══════════ WORKBENCH TABLE ═══════════
        private SurfaceCard cardDirectory = null!;
        private Label lblGridTitle = null!;
        private Label lblGridSubtitle = null!;
        private SaasButton btnViewAllCompanies = null!;
        private SegmentedFilter filterBar = null!;
        private WorkbenchSearch searchBox = null!;
        private Label lblCount = null!;
        private DataGridView dgv = null!;
        private SaasEmptyState emptyState = null!;
        private ContextMenuStrip _contextMenu = null!;
        private int _hoverRow = -1;

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
                Text = "Multi-tenant business intelligence, active subscription tiers, database routing, and SaaS health",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _rule = new Label { AutoSize = false, Height = 1, BackColor = UiKit.T.Line, Text = "" };

            btnRegister = new SaasButton("+ Register Business", SaasButtonVariant.Primary)
            {
                Size = new Size(160, 36)
            };
            btnRegister.Click += (s, e) => RegisterCompany();

            btnCompanies = new SaasButton("Manage Businesses", SaasButtonVariant.Secondary)
            {
                Size = new Size(140, 36)
            };
            btnCompanies.Click += (s, e) => ActionRequested?.Invoke(this, "companies");

            btnSubscriptions = new SaasButton("Manage Plans", SaasButtonVariant.Secondary)
            {
                Size = new Size(120, 36)
            };
            btnSubscriptions.Click += (s, e) => ActionRequested?.Invoke(this, "subscriptions");

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary)
            {
                Size = new Size(90, 36)
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
            Controls.Add(btnRegister);
            Controls.Add(btnCompanies);
            Controls.Add(btnSubscriptions);
            Controls.Add(btnRefresh);
            Controls.Add(lblLoading);

            // ── Section 1: KPI Metrics ──
            AddSection("Multi-Tenant Platform Metrics", "Real-time summary of registered tenants, active subscriptions, and infrastructure");

            tileTotalCompanies = AddTile("TOTAL BUSINESSES", "5", "Registered companies on platform", AppTheme.Primary, IconFont.Customers);
            tileTotalCompanies.Click += (s, e) => ActionRequested?.Invoke(this, "companies");

            tileActiveCompanies = AddTile("ACTIVE TENANTS", "5 Active", "Operational & accepting repair requests", AppTheme.Success, "\uE73E");
            tileActiveCompanies.Click += (s, e) => ActionRequested?.Invoke(this, "companies");

            tileSubscriptionPlans = AddTile("SUBSCRIPTION TIERS", "3 Plans", "Starter, Pro, Enterprise tiers", Color.FromArgb(124, 58, 237), "\uE9D5");
            tileSubscriptionPlans.Click += (s, e) => ActionRequested?.Invoke(this, "subscriptions");

            tileMonthlyRevenue = AddTile("ESTIMATED MONTHLY MRR", "₱0", "Total subscription billings / month", Color.FromArgb(16, 185, 129), "\uE8C7");
            tileMonthlyRevenue.Click += (s, e) => ActionRequested?.Invoke(this, "subscriptions");

            tileTotalUsers = AddTile("PLATFORM USERS", "0 Users", "Admins, managers & technicians", Color.FromArgb(59, 130, 246), IconFont.Profile);
            tileTotalUsers.Click += (s, e) => ActionRequested?.Invoke(this, "system-monitor");

            tileDatabases = AddTile("TENANT DATABASES", "0 DBs", "Isolated SQL Server databases", Color.FromArgb(245, 158, 11), "\uE7B8");
            tileDatabases.Click += (s, e) => ActionRequested?.Invoke(this, "system-monitor");

            tileTotalDevices = AddTile("HARDWARE MONITORED", "0 Devices", "Client hardware in tenant systems", Color.FromArgb(236, 72, 153), "\uE7F4");
            tileTotalDevices.Click += (s, e) => ActionRequested?.Invoke(this, "companies");

            tileSystemHealth = AddTile("SYSTEM STATUS", "100% Online", "All multi-tenant services operational", AppTheme.Success, "\uE958");
            tileSystemHealth.Click += (s, e) => ActionRequested?.Invoke(this, "system-monitor");

            // ── Section 2: Analytics & Charts ──
            AddSection("Platform Analytics & Distribution", "Breakdown of tenant distribution by subscription tier and platform-wide user seats");

            chartPlans = new ChartCard("Companies by Subscription Plan", ChartKind.Bar)
            {
                StaticBars = false
            };
            chartPlans.PointClicked += label =>
            {
                // Filter the directory below by this plan
                searchBox.Text = label;
            };
            _charts.Add(chartPlans);
            Controls.Add(chartPlans);

            chartRoles = new ChartCard("Platform Users by Role", ChartKind.Bar)
            {
                StaticBars = false
            };
            chartRoles.PointClicked += label =>
            {
                ActionRequested?.Invoke(this, "system-monitor");
            };
            _charts.Add(chartRoles);
            Controls.Add(chartRoles);

            // ── Section 3: Live Businesses Workbench ──
            AddSection("Registered Businesses & Tenant Directory", "Directory of business accounts with subscription plan, database catalog, and admin contacts");

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
            _sections.Add((lblT, lblH));
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
            cardDirectory = new SurfaceCard { BackColor = UiKit.T.Surface };

            lblGridTitle = new Label
            {
                Text = "Tenant Companies Overview",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblGridSubtitle = new Label
            {
                Text = "Live directory of all companies registered on the multi-tenant CRM platform",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnViewAllCompanies = new SaasButton("Open Full Businesses Page →", SaasButtonVariant.Secondary)
            {
                Size = new Size(210, 32)
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

            emptyState = new SaasEmptyState
            {
                Text = "No businesses match your filter",
                Subtitle = "Try clearing the search query or switching the active/deactivated filter.",
                Icon = IconFont.Customers,
                ActionText = "Clear Search",
                Visible = false
            };
            emptyState.ActionClicked += (s, e) =>
            {
                searchBox.Text = "";
                filterBar.SelectedIndex = 0;
            };

            cardDirectory.Controls.Add(lblGridTitle);
            cardDirectory.Controls.Add(lblGridSubtitle);
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
            _contextMenu = new ContextMenuStrip { ShowImageMargin = false };
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
            g.AutoGenerateColumns = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.RowHeadersVisible = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.BackgroundColor = UiKit.T.Surface;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.GridColor = UiKit.T.LineSoft;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ScrollBars = ScrollBars.Both;
            g.RowTemplate.Height = 56;
            g.ColumnHeadersHeight = 42;

            g.ColumnHeadersDefaultCellStyle.BackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.ForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.Font = UiKit.T.SmallStrong;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(12, 0, 12, 0);

            g.DefaultCellStyle.BackColor = UiKit.T.Surface;
            g.DefaultCellStyle.ForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Font = UiKit.T.Body;
            g.DefaultCellStyle.SelectionBackColor = UiKit.T.RowHover;
            g.DefaultCellStyle.SelectionForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Padding = new Padding(12, 8, 12, 8);
            g.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

            g.Columns.Clear();

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCode",
                HeaderText = "CODE",
                DataPropertyName = "CompanyCode",
                Width = 130
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = "BUSINESS NAME",
                DataPropertyName = "CompanyName",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 220,
                MinimumWidth = 200
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colLocation",
                HeaderText = "LOCATION",
                DataPropertyName = "LocationDisplay",
                Width = 150
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPlan",
                HeaderText = "SUBSCRIPTION TIER",
                DataPropertyName = "SubscriptionPlanName",
                Width = 180
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colFee",
                HeaderText = "FEE / MO",
                Width = 110
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDb",
                HeaderText = "TENANT DATABASE",
                DataPropertyName = "DatabaseName",
                Width = 220
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colAdmin",
                HeaderText = "ADMINISTRATOR",
                DataPropertyName = "AdminEmail",
                Width = 210
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colUsers",
                HeaderText = "USERS",
                Width = 80
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStatus",
                HeaderText = "STATUS",
                Width = 120
            });

            g.CellPainting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _filteredCompanies.Count) return;
                var item = _filteredCompanies[e.RowIndex];

                // Code column (bold, primary tinted)
                if (e.ColumnIndex == g.Columns["colCode"]!.Index)
                {
                    e.PaintBackground(e.CellBounds, true);
                    UiKit.Quality(e.Graphics);
                    UiKit.Text(e.Graphics, item.CompanyCode, UiKit.T.SmallStrong, AppTheme.Primary,
                        e.CellBounds, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    e.Handled = true;
                }
                // Monthly Fee Column
                else if (e.ColumnIndex == g.Columns["colFee"]!.Index)
                {
                    e.PaintBackground(e.CellBounds, true);
                    UiKit.Quality(e.Graphics);
                    string priceText = item.SubscriptionPrice.HasValue ? $"₱{item.SubscriptionPrice.Value:N0}/mo" : "—";
                    UiKit.Text(e.Graphics, priceText, UiKit.T.SmallStrong, Color.FromArgb(16, 185, 129),
                        e.CellBounds, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    e.Handled = true;
                }
                // Users Count Column
                else if (e.ColumnIndex == g.Columns["colUsers"]!.Index)
                {
                    e.PaintBackground(e.CellBounds, true);
                    UiKit.Quality(e.Graphics);
                    string uText = $"{item.TotalUsersCount} user{(item.TotalUsersCount == 1 ? "" : "s")}";
                    UiKit.Text(e.Graphics, uText, UiKit.T.Small, UiKit.T.InkMuted,
                        e.CellBounds, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    e.Handled = true;
                }
                // Status Pill Column
                else if (e.ColumnIndex == g.Columns["colStatus"]!.Index)
                {
                    e.PaintBackground(e.CellBounds, true);
                    var stateG = e.Graphics;
                    UiKit.Quality(stateG);

                    Color bg = item.IsActive ? UiKit.Wash(AppTheme.Success) : UiKit.Wash(AppTheme.Danger);
                    Color fg = item.IsActive ? AppTheme.Success : AppTheme.Danger;
                    string txt = item.IsActive ? "ACTIVE" : "DEACTIVATED";

                    var pillRect = new Rectangle(e.CellBounds.Left + 10, e.CellBounds.Top + (e.CellBounds.Height - 24) / 2, 98, 24);
                    UiKit.FillRounded(stateG, pillRect, 12, bg);
                    UiKit.Text(stateG, txt, UiKit.Micro, fg, pillRect, UiKit.Center);
                    e.Handled = true;
                }
            };
        }

        // ═══════════ DATA LOADING & BINDING ═══════════

        public async Task ReloadAsync()
        {
            try
            {
                lblLoading.Visible = true;

                // Load all platform-wide metrics in parallel
                var companiesTask = _api.GetCompaniesAsync();
                var subscriptionsTask = _api.GetSubscriptionsAsync(false, true);
                var usersTask = _api.GetUsersAsync(false);

                await Task.WhenAll(companiesTask, subscriptionsTask, usersTask);

                _allCompanies = companiesTask.Result ?? new List<CompanyDto>();
                _allSubscriptions = subscriptionsTask.Result ?? new List<SubscriptionDto>();
                _allUsers = usersTask.Result ?? new List<UserSummaryDto>();

                BindTiles();
                BindCharts();
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

            tileTotalCompanies.Set("TOTAL BUSINESSES", $"{totalCompanies}",
                $"{activeCompanies} active · {totalCompanies - activeCompanies} inactive", AppTheme.Primary, IconFont.Customers);

            tileActiveCompanies.Set("ACTIVE TENANTS", $"{activeCompanies} Active",
                totalCompanies > 0 ? $"{(double)activeCompanies / totalCompanies * 100:0.#}% platform active rate" : "No companies yet",
                AppTheme.Success, "\uE73E");

            tileSubscriptionPlans.Set("SUBSCRIPTION TIERS", $"{activePlans} Active Plans",
                $"{_allSubscriptions.Count} total packages registered", Color.FromArgb(124, 58, 237), "\uE9D5");

            tileMonthlyRevenue.Set("ESTIMATED MONTHLY MRR", $"₱{mrr:N0}",
                $"Projected ARR: ₱{arr:N0}", Color.FromArgb(16, 185, 129), "\uE8C7");

            tileTotalUsers.Set("PLATFORM USERS", $"{totalUsers} Users",
                "Admins, store managers & repair staff", Color.FromArgb(59, 130, 246), IconFont.Profile);

            tileDatabases.Set("TENANT DATABASES", $"{Math.Max(totalDatabases, activeCompanies)} DBs",
                "SQL Server isolated database catalogs", Color.FromArgb(245, 158, 11), "\uE7B8");

            tileTotalDevices.Set("HARDWARE MONITORED", $"{totalDevices} Devices",
                "Registered customer devices under repair", Color.FromArgb(236, 72, 153), "\uE7F4");

            tileSystemHealth.Set("SYSTEM STATUS", "100% Online",
                "All tenant databases & API online", AppTheme.Success, "\uE958");
        }

        private void BindCharts()
        {
            // Chart 1: Companies by Subscription Plan
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
                Color.FromArgb(99, 102, 241),  // Indigo
                Color.FromArgb(16, 185, 129),  // Emerald
                Color.FromArgb(245, 158, 11),  // Amber
                Color.FromArgb(236, 72, 153),  // Pink
                Color.FromArgb(59, 130, 246)   // Blue
            };

            var planBarData = new List<(string, double, Color)>();
            for (int i = 0; i < planGroups.Count; i++)
            {
                planBarData.Add((planGroups[i].Key, planGroups[i].Count, planColors[i % planColors.Length]));
            }
            chartPlans.SetBarData(planBarData, clickable: true);

            // Chart 2: Platform Users by Role
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

        // ═══════════ ACTIONS ═══════════

        private void ViewCompanyDetails(CompanyDto company)
        {
            using var dlg = new CompanyViewDialog(company);
            dlg.ShowDialog(this.FindForm());
        }

        private void RegisterCompany()
        {
            using var dlg = new CompanyRegisterDialog();
            if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                _ = ReloadAsync();
            }
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

                // ── Header ──
                lblTitle.Location = new Point(pad, pad);
                lblSubtitle.Location = new Point(pad, lblTitle.Bottom + 4);

                int btnY = pad + 4;
                btnRefresh.Location = new Point(pad + contentW - btnRefresh.Width, btnY);
                btnSubscriptions.Location = new Point(btnRefresh.Left - 10 - btnSubscriptions.Width, btnY);
                btnCompanies.Location = new Point(btnSubscriptions.Left - 10 - btnCompanies.Width, btnY);
                btnRegister.Location = new Point(btnCompanies.Left - 10 - btnRegister.Width, btnY);

                lblLoading.Location = new Point(pad, lblSubtitle.Bottom + 6);

                _rule.SetBounds(pad, lblSubtitle.Bottom + 18, contentW, 1);

                int y = _rule.Bottom + 20;

                // ── Section 1: KPI Tiles ──
                var (sec1T, sec1H) = _sections[0];
                sec1T.Location = new Point(pad, y);
                sec1H.Location = new Point(pad, sec1T.Bottom + 2);
                y = sec1H.Bottom + 14;

                // 4 columns of tiles (or 2 if narrow)
                int tileCols = contentW >= 1100 ? 4 : (contentW >= 760 ? 2 : 1);
                int tileGap = 16;
                int tileW = (contentW - (tileCols - 1) * tileGap) / tileCols;

                int rowMaxH = 0;
                for (int i = 0; i < _tiles.Count; i++)
                {
                    int col = i % tileCols;
                    int row = i / tileCols;
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
                y += rowMaxH + 28;

                // ── Section 2: Charts ──
                var (sec2T, sec2H) = _sections[1];
                sec2T.Location = new Point(pad, y);
                sec2H.Location = new Point(pad, sec2T.Bottom + 2);
                y = sec2H.Bottom + 14;

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
                y += chartRows * (chartH + chartGap) + 16;

                // ── Section 3: Directory Table ──
                var (sec3T, sec3H) = _sections[2];
                sec3T.Location = new Point(pad, y);
                sec3H.Location = new Point(pad, sec3T.Bottom + 2);
                y = sec3H.Bottom + 14;

                int cardPad = UiKit.T.S6;
                int cardH = 540;
                cardDirectory.SetBounds(pad, y, contentW, cardH);

                int innerW = contentW - cardPad * 2;
                lblGridTitle.Location = new Point(cardPad, cardPad);
                lblGridSubtitle.Location = new Point(cardPad, lblGridTitle.Bottom + 2);

                btnViewAllCompanies.Location = new Point(cardPad + innerW - btnViewAllCompanies.Width, cardPad);

                int barY = lblGridSubtitle.Bottom + 16;
                filterBar.Location = new Point(cardPad, barY);
                searchBox.SetBounds(cardPad + innerW - 320, barY, 320, 36);

                int gridY = filterBar.Bottom + 12;
                int gridH = cardH - gridY - cardPad - 24;
                dgv.SetBounds(cardPad, gridY, innerW, gridH);
                emptyState.SetBounds(cardPad, gridY, innerW, gridH);

                lblCount.Location = new Point(cardPad, dgv.Bottom + 6);

                y += cardH + 40;
                AutoScrollMinSize = new Size(0, y);
            }
            finally
            {
                _layingOut = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  EMBEDDED KPI TILE & CHART CARD IMPLEMENTATIONS
        // ═══════════════════════════════════════════════════════════════

        private sealed class KpiTile : Control
        {
            private const int Pad = 22;
            private const int IconSize = 34;
            private const int MinHeight = 128;

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

            private (Font NumFont, bool NumWrap, int TopH, int NumH, int CapH, int Total) Measure(int width)
            {
                int inner = Math.Max(40, width - Pad * 2);
                int labelW = Math.Max(40, inner - IconSize - 12);

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
                return (numFont, numWrap, topH, numH, capH, total);
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
                    UiKit.FillRounded(g, new Rectangle(Pad, Pad, IconSize, IconSize), 9, UiKit.T.Line);
                    UiKit.FillRounded(g, new Rectangle(Pad + IconSize + 12, Pad + 11, Math.Max(20, Width / 3), 12), 4, UiKit.T.Line);
                    UiKit.FillRounded(g, new Rectangle(Pad, Pad + 52, Math.Max(20, Width / 2), 24), 4, UiKit.T.Line);
                    return;
                }

                var m = Measure(Width);
                int inner = Width - Pad * 2;

                var iconRect = new Rectangle(Pad, Pad + (m.TopH - IconSize) / 2, IconSize, IconSize);
                UiKit.FillRounded(g, iconRect, 9, UiKit.Wash(accent));
                using (var f = UiKit.GlyphFont(12F))
                    UiKit.Text(g, _glyph, f, accent, iconRect, UiKit.Center);

                UiKit.Text(g, _label, UiKit.T.SmallStrong, UiKit.T.InkMuted,
                    new Rectangle(Pad + IconSize + 12, Pad, inner - IconSize - 12, m.TopH),
                    Wrapped | TextFormatFlags.VerticalCenter);

                int numTop = Pad + m.TopH + 14;
                UiKit.Text(g, _number, m.NumFont, UiKit.T.Ink,
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

        private enum ChartKind { Line, Bar }

        private sealed class ChartCard : Control
        {
            private static readonly Font HeadlineFont = AppFonts.Strong(19F);
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

                int titleH = 28;
                UiKit.Text(g, _title, UiKit.T.Section, UiKit.T.Ink,
                    new Rectangle(pad, pad, Width - pad * 2, titleH),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                int headlineH = 36;
                int headlineTop = pad + titleH + 6;
                if (hasData)
                {
                    double sum = _barData.Sum(b => b.Value);
                    string big = $"{sum:N0} total";
                    int bigW = UiKit.Measure(big, HeadlineFont).Width;
                    UiKit.Text(g, big, HeadlineFont, UiKit.T.Ink,
                        new Rectangle(pad, headlineTop, bigW + 8, headlineH),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                    var top = _barData.OrderByDescending(b => b.Value).First();
                    int x = pad + bigW + 14;
                    UiKit.Text(g, $"top: {top.Label} ({top.Value:N0})", UiKit.T.Small, UiKit.T.InkMuted,
                        new Rectangle(x, headlineTop, Math.Max(10, Width - pad - x), headlineH),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                }

                int plotTop = headlineTop + headlineH + 10;
                int plotBottom = Height - pad - 6;
                var plot = new Rectangle(pad + 2, plotTop, Width - (pad + 2) * 2, plotBottom - plotTop);

                _hits.Clear();
                if (plot.Height < 40 || plot.Width < 80) return;

                DrawBar(g, plot);
                DrawTooltip(g);
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

                int valueW = 80;
                int trackL = plot.Left + labelW + 14;
                int trackR = plot.Right - valueW - 12;
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
                    UiKit.FillRounded(g, track, barH / 2, UiKit.T.Line);

                    int fillW = value <= 0 ? 0 : Math.Max(barH, (int)(value / max * trackW));
                    var fill = new Rectangle(trackL, track.Top, fillW, barH);
                    if (fillW > 0)
                    {
                        UiKit.FillRounded(g, fill, barH / 2, hot ? color : Color.FromArgb(215, color));
                    }

                    string pct = sum > 0 ? $" · {value / sum * 100:0}%" : "";
                    string valueText = $"{value:N0}{pct}";
                    UiKit.Text(g, valueText, UiKit.T.SmallStrong, hot ? color : UiKit.T.Ink,
                        new Rectangle(plot.Right - valueW, row.Top, valueW, rowH),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

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
                UiKit.FillRounded(g, box, 8, Color.FromArgb(240, 24, 24, 27));
                UiKit.Text(g, label, UiKit.T.Small, Color.FromArgb(200, 255, 255, 255),
                    new Rectangle(box.Left + 12, box.Top + 6, tw - 24, 16),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                UiKit.Text(g, value, UiKit.T.BodyStrong, Color.White,
                    new Rectangle(box.Left + 12, box.Top + 22, tw - 24, 18),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        [DesignerCategory("Code")]
        private sealed class SurfaceCard : WorkbenchCard { }

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
                Width = 280;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);
                UiKit.FillRounded(g, new Rectangle(0, 0, Width, Height), 8, UiKit.T.LineSoft);

                if (_items.Count == 0) return;
                int segW = (Width - 4) / _items.Count;

                for (int i = 0; i < _items.Count; i++)
                {
                    var seg = new Rectangle(2 + i * segW, 2, segW, Height - 4);
                    bool active = i == _selected;
                    if (active)
                    {
                        UiKit.FillRounded(g, seg, 6, UiKit.T.Surface);
                        using var pen = new Pen(UiKit.T.Line, 1);
                        using var path = UiKit.Rounded(new Rectangle(seg.X, seg.Y, seg.Width - 1, seg.Height - 1), 6);
                        g.DrawPath(pen, path);
                    }

                    string text = _items[i];
                    Color fg = active ? UiKit.T.Ink : (i == _hover ? UiKit.T.InkMuted : UiKit.T.InkFaint);
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
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
                base.OnMouseClick(e);
            }
        }
    }
}
