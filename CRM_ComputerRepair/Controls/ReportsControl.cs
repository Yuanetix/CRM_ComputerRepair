using CRM.winforms;
using CRM.winforms.Auth;
using CRM.winforms.Reports;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Reports page — clean, minimal, and clear business intelligence workspace.
    /// Matches the dashboard design language:
    /// - Clean typography with ample breathing room (zero cut-off text)
    /// - Uncluttered metric cards with clear numbers and zero noisy emojis/icons
    /// - Minimalist segmented module tabs matching DashboardControl's SegmentedFilter
    /// - Full-width date pickers and columns with zero truncation
    /// </summary>
    [DesignerCategory("Code")]
    public class ReportsControl : UserControl
    {
        // ═══════════ ENUMS & VIEW MODELS ═══════════

        public enum ReportType
        {
            Sales,
            Repairs,
            Customers,
            Services,
            Interactions,
            Loyalty,
            Retention
        }

        public class SalesReportItem
        {
            public string RequestNumber { get; set; } = "";
            public string Customer { get; set; } = "";
            public string Service { get; set; } = "";
            public DateTime? PaidOn { get; set; }
            public string Method { get; set; } = "";
            public string Reference { get; set; } = "";
            public decimal Amount { get; set; }

            public string PaidOnDisplay => PaidOn.HasValue ? PaidOn.Value.ToString("MMM d, yyyy") : "—";
            public string AmountDisplay => $"₱{Amount:N2}";
        }

        public class ServicesReportItem
        {
            public string Service { get; set; } = "";
            public int Requests { get; set; }
            public decimal Revenue { get; set; }
            public decimal AvgValue { get; set; }
            public DateTime? LastRequest { get; set; }

            public string RevenueDisplay => $"₱{Revenue:N2}";
            public string AvgValueDisplay => $"₱{AvgValue:N2}";
            public string LastRequestDisplay => LastRequest.HasValue ? LastRequest.Value.ToString("MMM d, yyyy") : "—";
        }

        // ═══════════ STATE & DATA CACHES ═══════════

        private readonly ApiClient _api = new();
        private ReportType _currentType = ReportType.Sales;
        private string _currentSubFilter = "All";

        // Raw loaded datasets
        private List<CustomerDto> _rawCustomers = new();
        private List<RepairRequestDto> _rawRepairs = new();
        private List<InteractionDto> _rawInteractions = new();
        private List<SalesReportItem> _rawSales = new();
        private List<ServicesReportItem> _rawServices = new();
        private List<LoyaltyMemberDetailDto> _rawLoyalty = new();
        private List<RetentionRecommendationDto> _rawRetention = new();

        // Filtered views
        private List<SalesReportItem> _viewSales = new();
        private List<RepairRequestDto> _viewRepairs = new();
        private List<CustomerDto> _viewCustomers = new();
        private List<ServicesReportItem> _viewServices = new();
        private List<InteractionDto> _viewInteractions = new();
        private List<LoyaltyMemberDetailDto> _viewLoyalty = new();
        private List<RetentionRecommendationDto> _viewRetention = new();

        private int _hoverRow = -1;

        // ═══════════ UI CONTROLS ═══════════

        // Header
        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private ReportDateRangeBar dateRangeBar = null!;
        private SaasButton btnRefresh = null!;
        private SaasButton btnExport = null!;

        // KPI Strip (4 Minimalist Metric Cards)
        private readonly List<ReportKpiCard> _kpiCards = new();

        // Tab Navigation
        private ReportTabSegments tabSegments = null!;

        // Main Workbench Card
        private WorkbenchCard card = null!;
        private Label lblGridTitle = null!;
        private Label lblCount = null!;
        private ReportSubFilter subFilter = null!;
        private WorkbenchSearch search = null!;
        private DataGridView dgv = null!;
        private ReportSummaryBar summaryBar = null!;
        private TablePagination pager = null!;
        private WorkbenchState state = null!;

        // ═══════════ CONSTRUCTOR ═══════════

        public ReportsControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            AutoScaleMode = AutoScaleMode.Font;

            BuildUi();

            Load += async (s, e) => await ReloadAsync();
        }

        // ═══════════ UI INITIALIZATION ═══════════

        private void BuildUi()
        {
            // ── Header Title & Subtitle ──
            lblTitle = new Label
            {
                Text = "Reports",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = AppTheme.Background,
                UseMnemonic = false
            };

            lblSubtitle = new Label
            {
                Text = "Detailed business intelligence and operational audits",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = AppTheme.Background,
                UseMnemonic = false
            };

            // ── Header Action Controls ──
            dateRangeBar = new ReportDateRangeBar();
            dateRangeBar.RangeChanged += async (s, e) => await ReloadAsync();

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            btnExport = new SaasButton("Export PDF", SaasButtonVariant.Primary, "\uE74E");
            btnExport.Click += (s, e) => ExportPdf();

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(dateRangeBar);
            Controls.Add(btnRefresh);
            Controls.Add(btnExport);

            // ── Executive KPI Cards (Row of 4) ──
            for (int i = 0; i < 4; i++)
            {
                var kpi = new ReportKpiCard();
                kpi.ContentChanged += (s, e) => LayoutUi();
                _kpiCards.Add(kpi);
                Controls.Add(kpi);
            }

            // ── Minimal Segmented Navigation Tabs ──
            tabSegments = new ReportTabSegments();
            tabSegments.SelectedChanged += (s, e) => SwitchTab(tabSegments.SelectedType);
            Controls.Add(tabSegments);

            // ── Main Workbench Card ──
            card = new WorkbenchCard();

            lblGridTitle = new Label
            {
                Text = "Sales ledger",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };

            lblCount = new Label
            {
                Text = "0 records",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };

            subFilter = new ReportSubFilter();
            subFilter.FilterChanged += (s, e) =>
            {
                _currentSubFilter = subFilter.Selected;
                ApplyFilter(resetPage: true);
            };

            search = new WorkbenchSearch
            {
                PlaceholderText = "Search records..."
            };
            search.Inner.TextChanged += (s, e) => ApplyFilter(resetPage: true);

            dgv = new DataGridView();
            TableKit.StyleGrid(dgv);
            dgv.CellPainting += Dgv_CellPainting;
            dgv.CellMouseMove += (s, e) =>
            {
                if (e.RowIndex != _hoverRow)
                {
                    _hoverRow = e.RowIndex;
                    dgv.Invalidate();
                }
            };
            dgv.MouseLeave += (s, e) =>
            {
                if (_hoverRow != -1)
                {
                    _hoverRow = -1;
                    dgv.Invalidate();
                }
            };

            summaryBar = new ReportSummaryBar();

            pager = new TablePagination();
            pager.PageChanged += (s, e) => ApplyFilter(resetPage: false);

            state = new WorkbenchState { Visible = false };

            card.Controls.Add(lblGridTitle);
            card.Controls.Add(lblCount);
            card.Controls.Add(subFilter);
            card.Controls.Add(search);
            card.Controls.Add(dgv);
            card.Controls.Add(summaryBar);
            card.Controls.Add(pager);
            card.Controls.Add(state);

            Controls.Add(card);

            ConfigureSubFiltersForTab(_currentType);

            Resize += (s, e) => LayoutUi();
            LayoutUi();
        }

        // ═══════════ LAYOUT MANAGEMENT ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            int sidePad = 0;
            lblTitle.Location = new Point(sidePad, 0);

            int subtitleY = lblTitle.PreferredHeight + 4;
            lblSubtitle.Location = new Point(sidePad, subtitleY);

            // Right-aligned header controls
            int btnY = 2;
            int rightX = Width;

            btnExport.Size = new Size(btnExport.PreferredWidth, UiKit.T.ButtonHeight);
            btnExport.Location = new Point(rightX - btnExport.Width, btnY);
            rightX = btnExport.Left - 8;

            btnRefresh.Size = new Size(btnRefresh.PreferredWidth, UiKit.T.ButtonHeight);
            btnRefresh.Location = new Point(rightX - btnRefresh.Width, btnY);
            rightX = btnRefresh.Left - 12;

            dateRangeBar.Size = new Size(dateRangeBar.PreferredWidth, UiKit.T.ButtonHeight);
            dateRangeBar.Location = new Point(Math.Max(lblTitle.Right + 16, rightX - dateRangeBar.Width), btnY);

            // Row 1: KPI Cards (4 evenly distributed cards, spacious height with zero clipping)
            int kpiTop = Math.Max(subtitleY + lblSubtitle.PreferredHeight + 14, btnY + UiKit.T.ButtonHeight + 14);
            int kpiGap = 12;
            int totalKpiGap = kpiGap * 3;
            int cardW = Math.Max(120, (Width - sidePad * 2 - totalKpiGap) / 4);
            int kpiH = Math.Max(116, _kpiCards.Count > 0 ? _kpiCards.Max(c => c.HeightFor(cardW)) : 116);

            for (int i = 0; i < _kpiCards.Count; i++)
            {
                int x = sidePad + i * (cardW + kpiGap);
                _kpiCards[i].Location = new Point(x, kpiTop);
                _kpiCards[i].Size = new Size(cardW, kpiH);
            }

            // Row 2: Minimal Segmented Navigation Tabs
            int tabsTop = kpiTop + kpiH + 14;
            tabSegments.Location = new Point(sidePad, tabsTop);
            tabSegments.Size = new Size(tabSegments.PreferredWidth, 36);

            // Row 3: Main Workbench Card
            int cardTop = tabsTop + 36 + 12;
            int cardH = Math.Max(260, Height - cardTop - 4);

            card.Location = new Point(sidePad, cardTop);
            card.Size = new Size(Width, cardH);

            int cp = UiKit.T.S5; // 24px internal card padding

            // Title & Count
            lblGridTitle.Location = new Point(cp, cp);
            lblCount.Location = new Point(lblGridTitle.Right + 10, lblGridTitle.Top + (lblGridTitle.Height - lblCount.Height) / 2);

            // Toolbar: Sub-filter segments on left, Search box on right
            int toolbarY = lblGridTitle.Bottom + 12;
            int toolbarH = TableKit.ToolbarH;

            int maxSearchW = 280;
            int minSearchW = 180;
            int availSubFilterW = card.Width - cp * 2 - minSearchW - 16;
            int actualSubW = Math.Min(subFilter.PreferredWidth, Math.Max(120, availSubFilterW));

            subFilter.Size = new Size(actualSubW, toolbarH);
            subFilter.Location = new Point(cp, toolbarY);

            int searchW = Math.Min(maxSearchW, Math.Max(minSearchW, card.Width - cp * 2 - actualSubW - 16));
            search.Size = new Size(searchW, TableKit.InputH);
            search.Location = new Point(card.Width - cp - searchW, toolbarY + (toolbarH - TableKit.InputH) / 2);

            // Table, Summary Bar, Pager, and State
            int summaryH = 34;
            int footerH = TableKit.FooterH;
            int gridTop = toolbarY + toolbarH + 12;
            int gridW = Math.Max(100, card.Width - cp * 2);
            int gridH = Math.Max(60, card.Height - gridTop - cp - summaryH - footerH);

            dgv.Location = new Point(cp, gridTop);
            dgv.Size = new Size(gridW, gridH);

            summaryBar.Location = new Point(cp, gridTop + gridH);
            summaryBar.Size = new Size(gridW, summaryH);

            pager.Location = new Point(cp, gridTop + gridH + summaryH);
            pager.Size = new Size(gridW, footerH);

            state.Location = new Point(cp, gridTop);
            state.Size = new Size(gridW, gridH + summaryH + footerH);
        }

        // ═══════════ DATA LOAD & SYNC ═══════════

        public async Task ReloadAsync()
        {
            btnRefresh.Loading = true;
            state.ShowLoading("Loading report data…", "Fetching records.");
            dgv.Visible = false;

            try
            {
                var from = dateRangeBar.From;
                var to = dateRangeBar.To;

                var custTask = _api.GetCustomersAsync(includeArchived: true);
                var repairsTask = _api.GetRepairRequestsAsync();
                var interTask = _api.GetInteractionsAsync(null, includeArchived: true);
                var salesTask = _api.GetAnalyticsDetailsAsync("sales", from, to);
                var servicesTask = _api.GetAnalyticsDetailsAsync("services");
                var loyaltyTask = _api.GetLoyaltyMembersAsync();
                var retentionTask = _api.GetRetentionRecommendationsAsync();

                await Task.WhenAll(custTask, repairsTask, interTask, salesTask, servicesTask, loyaltyTask, retentionTask);

                var custResult = await custTask ?? new();
                var repResult = await repairsTask ?? new();
                var interResult = await interTask ?? new();
                var salesResult = await salesTask;
                var servicesResult = await servicesTask;
                var loyaltyResult = await loyaltyTask ?? new();
                var retentionResult = await retentionTask ?? new();

                // Apply date filters where appropriate
                _rawCustomers = custResult.Where(c => c.CreatedAt >= from && c.CreatedAt <= to).ToList();
                _rawRepairs = repResult.Where(r => r.RequestDate >= from && r.RequestDate <= to).ToList();
                _rawInteractions = interResult.Where(i => i.InteractionDate >= from && i.InteractionDate <= to).ToList();

                // Parse Sales
                _rawSales = salesResult?.Rows.Select(r => new SalesReportItem
                {
                    RequestNumber = Val(r, "requestNumber"),
                    Customer = Val(r, "customer"),
                    Service = Val(r, "service"),
                    PaidOn = DateTime.TryParse(Val(r, "paidOn"), out var dt) ? dt : null,
                    Method = Val(r, "method"),
                    Reference = Val(r, "reference"),
                    Amount = decimal.TryParse(Val(r, "amount"), out var a) ? a : 0m
                }).ToList() ?? new();

                // Parse Services
                _rawServices = servicesResult?.Rows.Select(r => new ServicesReportItem
                {
                    Service = Val(r, "service"),
                    Requests = int.TryParse(Val(r, "count"), out var c) ? c : 0,
                    Revenue = decimal.TryParse(Val(r, "revenue"), out var rev) ? rev : 0m,
                    AvgValue = decimal.TryParse(Val(r, "avgValue"), out var avg) ? avg : 0m,
                    LastRequest = DateTime.TryParse(Val(r, "lastRequest"), out var lr) ? lr : null
                }).ToList() ?? new();

                _rawLoyalty = loyaltyResult;
                _rawRetention = retentionResult;

                UpdateKpiCards();
                ApplyFilter(resetPage: true);
            }
            catch (Exception ex)
            {
                state.Show("\uE711", "Failed to load report", ex.Message);
                Toast.Notify(FindForm(), "Connection Problem", "Couldn't load report data from the server.", ToastKind.Error);
            }
            finally
            {
                btnRefresh.Loading = false;
            }
        }

        // ═══════════ KPI EXECUTIVE STRIP RECALCULATION ═══════════

        private void UpdateKpiCards()
        {
            if (_kpiCards.Count < 4) return;

            switch (_currentType)
            {
                case ReportType.Sales:
                    {
                        decimal gross = _rawSales.Sum(s => s.Amount);
                        int count = _rawSales.Count;
                        decimal avg = count > 0 ? gross / count : 0m;
                        var topMethod = _rawSales
                            .GroupBy(s => string.IsNullOrWhiteSpace(s.Method) ? "Cash" : s.Method)
                            .OrderByDescending(g => g.Count())
                            .Select(g => $"{g.Key} ({g.Count()})")
                            .FirstOrDefault() ?? "—";

                        _kpiCards[0].Set("Gross revenue", $"₱{gross:N2}", $"{count} payments", AppTheme.Success);
                        _kpiCards[1].Set("Total transactions", count.ToString(), "In selected period", AppTheme.Primary);
                        _kpiCards[2].Set("Average ticket", $"₱{avg:N2}", "Per payment", AppTheme.Warning);
                        _kpiCards[3].Set("Top method", topMethod, "Most used", AppTheme.Info);
                        break;
                    }

                case ReportType.Repairs:
                    {
                        int total = _rawRepairs.Count;
                        int completed = _rawRepairs.Count(r => r.Status == 3);
                        int active = _rawRepairs.Count(r => r.Status is 1 or 2);
                        decimal pipeline = _rawRepairs.Sum(r => (r.ActualCost ?? r.EstimatedCost) ?? 0m);
                        double rate = total > 0 ? (completed * 100.0 / total) : 0.0;

                        _kpiCards[0].Set("Total tickets", total.ToString(), "Intake requests", AppTheme.Primary);
                        _kpiCards[1].Set("Completed", $"{completed} ({rate:F0}%)", "Finished repairs", AppTheme.Success);
                        _kpiCards[2].Set("In progress", active.ToString(), "Active on bench", AppTheme.Warning);
                        _kpiCards[3].Set("Pipeline value", $"₱{pipeline:N2}", "Est. parts & labor", AppTheme.Info);
                        break;
                    }

                case ReportType.Customers:
                    {
                        int total = _rawCustomers.Count;
                        int active = _rawCustomers.Count(c => c.IsActive);
                        int archived = total - active;
                        int totalPoints = _rawCustomers.Sum(c => c.LoyaltyPoints ?? 0);

                        _kpiCards[0].Set("Total registered", total.ToString(), "Accounts", AppTheme.Primary);
                        _kpiCards[1].Set("Active accounts", active.ToString(), "In good standing", AppTheme.Success);
                        _kpiCards[2].Set("Archived", archived.ToString(), "Inactive", AppTheme.TextMuted);
                        _kpiCards[3].Set("Loyalty points", $"{totalPoints:N0} pts", "Active points", AppTheme.Warning);
                        break;
                    }

                case ReportType.Services:
                    {
                        int lines = _rawServices.Count;
                        decimal rev = _rawServices.Sum(s => s.Revenue);
                        var topRev = _rawServices.OrderByDescending(s => s.Revenue).FirstOrDefault();
                        var topVol = _rawServices.OrderByDescending(s => s.Requests).FirstOrDefault();

                        _kpiCards[0].Set("Service offerings", lines.ToString(), "Catalog items", AppTheme.Primary);
                        _kpiCards[1].Set("Total revenue", $"₱{rev:N2}", "Service revenue", AppTheme.Success);
                        _kpiCards[2].Set("Top service", topRev?.Service ?? "—", topRev != null ? $"₱{topRev.Revenue:N2}" : "—", AppTheme.Warning);
                        _kpiCards[3].Set("Highest volume", topVol?.Service ?? "—", topVol != null ? $"{topVol.Requests} jobs" : "—", AppTheme.Info);
                        break;
                    }

                case ReportType.Interactions:
                    {
                        int total = _rawInteractions.Count;
                        int closed = _rawInteractions.Count(i => i.Status == 2);
                        int open = total - closed;
                        int urgent = _rawInteractions.Count(i => i.Priority == 2);
                        double resolveRate = total > 0 ? (closed * 100.0 / total) : 0.0;

                        _kpiCards[0].Set("Total logs", total.ToString(), "Support entries", AppTheme.Primary);
                        _kpiCards[1].Set("Resolved", $"{closed} ({resolveRate:F0}%)", "Closed tickets", AppTheme.Success);
                        _kpiCards[2].Set("Open & in progress", open.ToString(), "Pending attention", AppTheme.Warning);
                        _kpiCards[3].Set("Urgent priority", urgent.ToString(), "High priority", AppTheme.Danger);
                        break;
                    }

                case ReportType.Loyalty:
                    {
                        int members = _rawLoyalty.Count;
                        int active = _rawLoyalty.Count(m => m.IsActive);
                        decimal spend = _rawLoyalty.Sum(m => m.TotalSpent);
                        int points = _rawLoyalty.Sum(m => m.Points);

                        _kpiCards[0].Set("Enrolled members", members.ToString(), "Participants", AppTheme.Primary);
                        _kpiCards[1].Set("Active members", active.ToString(), "Active status", AppTheme.Success);
                        _kpiCards[2].Set("Lifetime spend", $"₱{spend:N2}", "Member spending", AppTheme.Warning);
                        _kpiCards[3].Set("Points balance", $"{points:N0} pts", "In circulation", AppTheme.Info);
                        break;
                    }

                case ReportType.Retention:
                    {
                        int total = _rawRetention.Count;
                        int atRisk = _rawRetention.Count(r => r.Category.Contains("Risk", StringComparison.OrdinalIgnoreCase));
                        int inactive = _rawRetention.Count(r => r.Category.Contains("Inactive", StringComparison.OrdinalIgnoreCase));
                        decimal spendAtRisk = _rawRetention.Where(r => r.Category.Contains("Risk", StringComparison.OrdinalIgnoreCase) || r.Category.Contains("Inactive", StringComparison.OrdinalIgnoreCase)).Sum(r => r.TotalSpent);

                        _kpiCards[0].Set("Actionable leads", total.ToString(), "Recommendations", AppTheme.Primary);
                        _kpiCards[1].Set("At-risk customers", atRisk.ToString(), "Drifting away", AppTheme.Warning);
                        _kpiCards[2].Set("Inactive customers", inactive.ToString(), "90+ days inactive", AppTheme.Danger);
                        _kpiCards[3].Set("Past spend at risk", $"₱{spendAtRisk:N2}", "Recoverable LTV", AppTheme.Info);
                        break;
                    }
            }

            LayoutUi();
        }

        // ═══════════ TAB SWITCHING ═══════════

        private void SwitchTab(ReportType type)
        {
            if (_currentType == type) return;
            _currentType = type;

            lblGridTitle.Text = type switch
            {
                ReportType.Sales => "Sales ledger",
                ReportType.Repairs => "Repair operations",
                ReportType.Customers => "Customer directory",
                ReportType.Services => "Service catalog",
                ReportType.Interactions => "Support communications",
                ReportType.Loyalty => "Loyalty roster",
                ReportType.Retention => "Retention recommendations",
                _ => "Audit report"
            };

            search.PlaceholderText = type switch
            {
                ReportType.Sales => "Search ticket, customer, method...",
                ReportType.Repairs => "Search ticket, customer, device...",
                ReportType.Customers => "Search customer, email, phone...",
                ReportType.Services => "Search service name...",
                ReportType.Interactions => "Search subject, customer, type...",
                ReportType.Loyalty => "Search member or program...",
                ReportType.Retention => "Search customer, reason, reward...",
                _ => "Search..."
            };

            ConfigureSubFiltersForTab(type);
            UpdateKpiCards();
            LayoutUi();
            ApplyFilter(resetPage: true);
        }

        private void ConfigureSubFiltersForTab(ReportType type)
        {
            string[] items = type switch
            {
                ReportType.Sales => new[] { "All", "Cash", "GCash", "Card", "Bank Transfer" },
                ReportType.Repairs => new[] { "All", "Pending", "Approved", "In Progress", "Completed", "Urgent" },
                ReportType.Customers => new[] { "All", "Active", "Archived" },
                ReportType.Services => new[] { "All", "Top Volume", "Top Revenue" },
                ReportType.Interactions => new[] { "All", "Open", "In Progress", "Closed", "Urgent" },
                ReportType.Loyalty => new[] { "All", "Active", "Inactive" },
                ReportType.Retention => new[] { "All", "Loyal", "Returning", "At-Risk", "Inactive" },
                _ => new[] { "All" }
            };

            subFilter.SetItems(items);
            _currentSubFilter = "All";
        }

        // ═══════════ FILTERING & PAGINATION ═══════════

        private void ApplyFilter(bool resetPage = true)
        {
            if (resetPage) pager.Reset();
            var term = search.Inner.Text?.Trim() ?? "";

            switch (_currentType)
            {
                case ReportType.Sales:
                    {
                        var q = _rawSales.AsEnumerable();

                        if (_currentSubFilter != "All")
                            q = q.Where(s => s.Method.Equals(_currentSubFilter, StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrEmpty(term))
                            q = q.Where(s =>
                                s.RequestNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                s.Customer.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                s.Service.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                s.Method.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                s.Reference.Contains(term, StringComparison.OrdinalIgnoreCase));

                        _viewSales = q.OrderByDescending(s => s.PaidOn).ToList();
                        pager.SetTotal(_viewSales.Count);
                        var page = pager.Slice(_viewSales);

                        dgv.DataSource = null;
                        dgv.DataSource = page;
                        ConfigureSalesColumns();

                        lblCount.Text = $"{_viewSales.Count} records";
                        decimal sum = _viewSales.Sum(s => s.Amount);
                        decimal avg = _viewSales.Count > 0 ? sum / _viewSales.Count : 0m;
                        summaryBar.SetStats($"Total: ₱{sum:N2}   ·   Average: ₱{avg:N2}   ·   {_viewSales.Count} payments");

                        ShowStateOrGrid(_viewSales.Count);
                        break;
                    }

                case ReportType.Repairs:
                    {
                        var q = _rawRepairs.AsEnumerable();

                        if (_currentSubFilter != "All")
                        {
                            q = _currentSubFilter switch
                            {
                                "Pending" => q.Where(r => r.Status == 0),
                                "Approved" => q.Where(r => r.Status == 1),
                                "In Progress" => q.Where(r => r.Status == 2),
                                "Completed" => q.Where(r => r.Status == 3),
                                "Urgent" => q.Where(r => r.Priority is 2 or 3),
                                _ => q
                            };
                        }

                        if (!string.IsNullOrEmpty(term))
                            q = q.Where(r =>
                                r.RequestNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                (r.CustomerName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                r.DeviceModel.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                r.SerialNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                r.IssueDescription.Contains(term, StringComparison.OrdinalIgnoreCase));

                        _viewRepairs = q.OrderByDescending(r => r.RequestDate).ToList();
                        pager.SetTotal(_viewRepairs.Count);
                        var page = pager.Slice(_viewRepairs);

                        dgv.DataSource = null;
                        dgv.DataSource = page;
                        ConfigureRepairColumns();

                        lblCount.Text = $"{_viewRepairs.Count} records";
                        decimal pipeline = _viewRepairs.Sum(r => (r.ActualCost ?? r.EstimatedCost) ?? 0m);
                        int completed = _viewRepairs.Count(r => r.Status == 3);
                        summaryBar.SetStats($"Pipeline: ₱{pipeline:N2}   ·   Completed: {completed}   ·   {_viewRepairs.Count} tickets");

                        ShowStateOrGrid(_viewRepairs.Count);
                        break;
                    }

                case ReportType.Customers:
                    {
                        var q = _rawCustomers.AsEnumerable();

                        if (_currentSubFilter == "Active") q = q.Where(c => c.IsActive);
                        else if (_currentSubFilter == "Archived") q = q.Where(c => !c.IsActive);

                        if (!string.IsNullOrEmpty(term))
                            q = q.Where(c =>
                                c.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                (c.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                (c.Phone ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                (c.Address ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));

                        _viewCustomers = q.OrderByDescending(c => c.CreatedAt).ToList();
                        pager.SetTotal(_viewCustomers.Count);
                        var page = pager.Slice(_viewCustomers);

                        dgv.DataSource = null;
                        dgv.DataSource = page;
                        ConfigureCustomerColumns();

                        lblCount.Text = $"{_viewCustomers.Count} records";
                        int active = _viewCustomers.Count(c => c.IsActive);
                        int points = _viewCustomers.Sum(c => c.LoyaltyPoints ?? 0);
                        summaryBar.SetStats($"Active: {active}   ·   Points: {points:N0} pts   ·   {_viewCustomers.Count} customers");

                        ShowStateOrGrid(_viewCustomers.Count);
                        break;
                    }

                case ReportType.Services:
                    {
                        var q = _rawServices.AsEnumerable();

                        if (_currentSubFilter == "Top Volume")
                            q = q.OrderByDescending(s => s.Requests);
                        else if (_currentSubFilter == "Top Revenue")
                            q = q.OrderByDescending(s => s.Revenue);

                        if (!string.IsNullOrEmpty(term))
                            q = q.Where(s => s.Service.Contains(term, StringComparison.OrdinalIgnoreCase));

                        _viewServices = q.ToList();
                        pager.SetTotal(_viewServices.Count);
                        var page = pager.Slice(_viewServices);

                        dgv.DataSource = null;
                        dgv.DataSource = page;
                        ConfigureServiceColumns();

                        lblCount.Text = $"{_viewServices.Count} records";
                        decimal rev = _viewServices.Sum(s => s.Revenue);
                        int jobs = _viewServices.Sum(s => s.Requests);
                        summaryBar.SetStats($"Revenue: ₱{rev:N2}   ·   Volume: {jobs} jobs   ·   {_viewServices.Count} services");

                        ShowStateOrGrid(_viewServices.Count);
                        break;
                    }

                case ReportType.Interactions:
                    {
                        var q = _rawInteractions.AsEnumerable();

                        if (_currentSubFilter != "All")
                        {
                            q = _currentSubFilter switch
                            {
                                "Open" => q.Where(i => i.Status == 0),
                                "In Progress" => q.Where(i => i.Status == 1),
                                "Closed" => q.Where(i => i.Status == 2),
                                "Urgent" => q.Where(i => i.Priority == 2),
                                _ => q
                            };
                        }

                        if (!string.IsNullOrEmpty(term))
                            q = q.Where(i =>
                                i.Subject.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                (i.CustomerName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                (i.Notes ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                i.TypeText.Contains(term, StringComparison.OrdinalIgnoreCase));

                        _viewInteractions = q.OrderByDescending(i => i.InteractionDate).ToList();
                        pager.SetTotal(_viewInteractions.Count);
                        var page = pager.Slice(_viewInteractions);

                        dgv.DataSource = null;
                        dgv.DataSource = page;
                        ConfigureInteractionColumns();

                        lblCount.Text = $"{_viewInteractions.Count} records";
                        int closed = _viewInteractions.Count(i => i.Status == 2);
                        int urgent = _viewInteractions.Count(i => i.Priority == 2);
                        summaryBar.SetStats($"Closed: {closed}   ·   Urgent: {urgent}   ·   {_viewInteractions.Count} entries");

                        ShowStateOrGrid(_viewInteractions.Count);
                        break;
                    }

                case ReportType.Loyalty:
                    {
                        var q = _rawLoyalty.AsEnumerable();

                        if (_currentSubFilter == "Active") q = q.Where(m => m.IsActive);
                        else if (_currentSubFilter == "Inactive") q = q.Where(m => !m.IsActive);

                        if (!string.IsNullOrEmpty(term))
                            q = q.Where(m =>
                                m.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                m.ProgramName.Contains(term, StringComparison.OrdinalIgnoreCase));

                        _viewLoyalty = q.OrderByDescending(m => m.TotalSpent).ToList();
                        pager.SetTotal(_viewLoyalty.Count);
                        var page = pager.Slice(_viewLoyalty);

                        dgv.DataSource = null;
                        dgv.DataSource = page;
                        ConfigureLoyaltyColumns();

                        lblCount.Text = $"{_viewLoyalty.Count} records";
                        decimal spend = _viewLoyalty.Sum(m => m.TotalSpent);
                        int active = _viewLoyalty.Count(m => m.IsActive);
                        summaryBar.SetStats($"Total Spend: ₱{spend:N2}   ·   Active: {active}   ·   {_viewLoyalty.Count} members");

                        ShowStateOrGrid(_viewLoyalty.Count);
                        break;
                    }

                case ReportType.Retention:
                    {
                        var q = _rawRetention.AsEnumerable();

                        if (_currentSubFilter != "All")
                            q = q.Where(r => r.Category.Contains(_currentSubFilter, StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrEmpty(term))
                            q = q.Where(r =>
                                r.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                (r.Basis ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                r.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                (r.Reward ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));

                        _viewRetention = q.OrderByDescending(r => r.TotalSpent).ToList();
                        pager.SetTotal(_viewRetention.Count);
                        var page = pager.Slice(_viewRetention);

                        dgv.DataSource = null;
                        dgv.DataSource = page;
                        ConfigureRetentionColumns();

                        lblCount.Text = $"{_viewRetention.Count} records";
                        decimal atRiskVal = _viewRetention.Sum(r => r.TotalSpent);
                        summaryBar.SetStats($"Spend at Risk: ₱{atRiskVal:N2}   ·   {_viewRetention.Count} leads");

                        ShowStateOrGrid(_viewRetention.Count);
                        break;
                    }
            }
        }

        private void ShowStateOrGrid(int count)
        {
            if (count > 0)
            {
                state.Clear();
                dgv.Visible = true;
                summaryBar.Visible = true;
                pager.Visible = true;
            }
            else
            {
                dgv.Visible = false;
                summaryBar.Visible = false;
                pager.Visible = false;

                string kw = search.Inner.Text?.Trim() ?? "";
                if (!string.IsNullOrEmpty(kw))
                    state.Show("\uE721", "No matching records", $"Nothing matched \"{kw}\".");
                else if (_currentSubFilter != "All")
                    state.Show("\uE71C", "No records found", $"No entries match \"{_currentSubFilter}\".");
                else
                    state.Show("\uE9D9", "No data", "There are no records for the selected period.");
            }
        }

        // ═══════════ GRID COLUMN CONFIGURATIONS ═══════════

        private void HideUnneededColumns(params string[] keep)
        {
            var keepSet = new HashSet<string>(keep);
            foreach (DataGridViewColumn col in dgv.Columns)
            {
                if (!keepSet.Contains(col.Name))
                    col.Visible = false;
            }
        }

        private void ConfigureSalesColumns()
        {
            HideUnneededColumns("RequestNumber", "Customer", "Service", "PaidOnDisplay", "Method", "Reference", "AmountDisplay");

            SetupColumn("RequestNumber", "Ticket #", 110, 0);
            SetupColumn("Customer", "Customer", 180, 1);
            SetupColumn("Service", "Service", 0, 2, fill: true);
            SetupColumn("PaidOnDisplay", "Paid Date", 140, 3);
            SetupColumn("Method", "Method", 120, 4);
            SetupColumn("Reference", "Reference", 120, 5);
            SetupColumn("AmountDisplay", "Amount", 130, 6, alignRight: true);
        }

        private void ConfigureRepairColumns()
        {
            HideUnneededColumns("PriorityText", "RequestNumber", "DeviceDisplay", "CustomerDisplay", "IssueDescription", "StatusText", "RequestDate", "CostDisplay");

            SetupColumn("PriorityText", "Priority", 95, 0);
            SetupColumn("RequestNumber", "Ticket #", 110, 1);
            SetupColumn("DeviceDisplay", "Device", 180, 2);
            SetupColumn("CustomerDisplay", "Customer", 180, 3);
            SetupColumn("IssueDescription", "Issue", 0, 4, fill: true);
            SetupColumn("StatusText", "Status", 110, 5);
            SetupColumn("RequestDate", "Date", 140, 6, format: "MMM d, yyyy");
            SetupColumn("CostDisplay", "Cost", 120, 7, alignRight: true);
        }

        private void ConfigureCustomerColumns()
        {
            HideUnneededColumns("CustomerId", "NameDisplay", "Email", "Phone", "Address", "LoyaltyPoints", "StatusDisplay", "CreatedAt");

            SetupColumn("CustomerId", "ID", 70, 0);
            SetupColumn("NameDisplay", "Customer", 180, 1);
            SetupColumn("Email", "Email", 180, 2);
            SetupColumn("Phone", "Phone", 130, 3);
            SetupColumn("Address", "Address", 0, 4, fill: true);
            SetupColumn("LoyaltyPoints", "Points", 90, 5, alignRight: true);
            SetupColumn("StatusDisplay", "Status", 100, 6);
            SetupColumn("CreatedAt", "Registered", 140, 7, format: "MMM d, yyyy");
        }

        private void ConfigureServiceColumns()
        {
            HideUnneededColumns("Service", "Requests", "RevenueDisplay", "AvgValueDisplay", "LastRequestDisplay");

            SetupColumn("Service", "Service", 0, 0, fill: true);
            SetupColumn("Requests", "Requests", 110, 1, alignRight: true);
            SetupColumn("RevenueDisplay", "Revenue", 140, 2, alignRight: true);
            SetupColumn("AvgValueDisplay", "Avg Value", 130, 3, alignRight: true);
            SetupColumn("LastRequestDisplay", "Last Request", 140, 4);
        }

        private void ConfigureInteractionColumns()
        {
            HideUnneededColumns("TypeText", "PriorityText", "CustomerDisplay", "Subject", "StatusText", "InteractionDate", "ClosedAtDisplay");

            SetupColumn("TypeText", "Type", 100, 0);
            SetupColumn("PriorityText", "Priority", 95, 1);
            SetupColumn("CustomerDisplay", "Customer", 180, 2);
            SetupColumn("Subject", "Subject", 0, 3, fill: true);
            SetupColumn("StatusText", "Status", 110, 4);
            SetupColumn("InteractionDate", "Date", 140, 5, format: "MMM d, yyyy");
            SetupColumn("ClosedAtDisplay", "Closed", 140, 6);
        }

        private void ConfigureLoyaltyColumns()
        {
            HideUnneededColumns("CustomerName", "ProgramName", "Points", "TotalSpent", "JoinedDate", "IsActive");

            SetupColumn("CustomerName", "Customer", 180, 0);
            SetupColumn("ProgramName", "Program", 160, 1);
            SetupColumn("Points", "Points", 110, 2, alignRight: true);
            SetupColumn("TotalSpent", "Lifetime Spend", 130, 3, alignRight: true, format: "₱#,##0.00");
            SetupColumn("JoinedDate", "Joined", 140, 4, format: "MMM d, yyyy");
            SetupColumn("IsActive", "Status", 90, 5);
        }

        private void ConfigureRetentionColumns()
        {
            HideUnneededColumns("CustomerName", "Category", "Action", "Basis", "Reward", "TotalSpent", "DaysSinceLastTransaction");

            SetupColumn("CustomerName", "Customer", 180, 0);
            SetupColumn("Category", "Segment", 120, 1);
            SetupColumn("Action", "Recommended Action", 170, 2);
            SetupColumn("Basis", "Reason", 0, 3, fill: true);
            SetupColumn("Reward", "Incentive", 140, 4);
            SetupColumn("TotalSpent", "Past Spend", 120, 5, alignRight: true, format: "₱#,##0.00");
            SetupColumn("DaysSinceLastTransaction", "Days Inactive", 110, 6, alignRight: true);
        }

        private void SetupColumn(string name, string header, int width, int displayIndex, bool fill = false, bool alignRight = false, string? format = null)
        {
            var col = dgv.Columns[name];
            if (col == null) return;

            col.Visible = true;
            col.HeaderText = header;
            col.DisplayIndex = displayIndex;
            col.SortMode = DataGridViewColumnSortMode.Automatic;

            if (fill)
            {
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                col.MinimumWidth = 180;
            }
            else
            {
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                col.Width = width;
            }

            if (alignRight)
            {
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            else
            {
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
            }

            if (format != null)
                col.DefaultCellStyle.Format = format;
        }

        // ═══════════ CELL PAINTING (PILLS, CURRENCIES, HOVER) ═══════════

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

            string colName = dgv.Columns[e.ColumnIndex].Name;
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool hovered = e.RowIndex == _hoverRow;

            Color bg = TableKit.RowBackground(e, selected, hovered);
            TableKit.PaintRowShell(g, e.CellBounds, bg);

            bool isPill = colName is "Method" or "StatusText" or "PriorityText" or "StatusDisplay" or "TypeText" or "Category" or "IsActive";
            bool isCurrency = colName is "AmountDisplay" or "CostDisplay" or "RevenueDisplay" or "AvgValueDisplay" or "TotalSpent";

            if (isPill)
            {
                string text = e.FormattedValue?.ToString() ?? "";
                if (colName == "IsActive")
                {
                    bool val = e.Value is true or "True";
                    text = val ? "Active" : "Archived";
                }
                TableKit.PaintPill(g, e.CellBounds, text);
                e.Handled = true;
                return;
            }

            if (isCurrency)
            {
                UiKit.Quality(g);
                string text = e.FormattedValue?.ToString() ?? "";
                var r = new Rectangle(e.CellBounds.Left, e.CellBounds.Top, e.CellBounds.Width - UiKit.T.S3, e.CellBounds.Height);
                UiKit.Text(g, text, UiKit.T.BodyStrong, UiKit.T.Ink, r,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                e.Handled = true;
                return;
            }

            e.PaintContent(e.CellBounds);
            e.Handled = true;
        }

        // ═══════════ EXPORT IMPLEMENTATION (PDF) ═══════════

        private void ExportPdf()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = $"Fixory_{_currentType}_Report_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
            };

            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                var doc = new PdfReportBuilder.ReportDocument
                {
                    Orientation = PdfSharp.PageOrientation.Landscape,
                    Metadata = new PdfReportBuilder.ReportMetadata
                    {
                        CompanyName = "FIXORY COMPUTER REPAIR",
                        SystemTagline = "Executive Business Intelligence & Operational Audit",
                        ReportTitle = _currentType switch
                        {
                            ReportType.Sales => "Sales Ledger & Revenue Performance",
                            ReportType.Repairs => "Repair Operations & Workorder Audit",
                            ReportType.Customers => "Customer Directory & Account Audit",
                            ReportType.Services => "Service Catalog & Yield Performance",
                            ReportType.Interactions => "Support Communications & Interaction Audit",
                            ReportType.Loyalty => "Loyalty Programs & Member Roster",
                            ReportType.Retention => "Customer Retention & Win-Back Intelligence",
                            _ => $"{_currentType} Report"
                        },
                        Subtitle = _currentType switch
                        {
                            ReportType.Sales => "Audit of paid repair requests and counter transactions",
                            ReportType.Repairs => "Intake status, bench queue, and service estimates",
                            ReportType.Customers => "Registered clients, contact channels, and loyalty status",
                            ReportType.Services => "Demand analysis, ticket revenue, and average ticket yield",
                            ReportType.Interactions => "Client inquiry logs, resolution velocity, and case priority",
                            ReportType.Loyalty => "Tier enrollment, active points balances, and customer lifetime value",
                            ReportType.Retention => "Churn risk assessment, automated triggers, and re-engagement strategies",
                            _ => ""
                        },
                        PeriodText = $"{dateRangeBar.From:MMM dd, yyyy} – {dateRangeBar.To:MMM dd, yyyy}",
                        GeneratedBy = !string.IsNullOrWhiteSpace(UserSession.FullName) ? UserSession.FullName : (!string.IsNullOrWhiteSpace(UserSession.Username) ? UserSession.Username : "Administrator"),
                        GeneratedAt = DateTime.Now
                    }
                };

                // Add current 4 KPI metrics
                foreach (var kpi in _kpiCards)
                {
                    doc.Kpis.Add(new PdfReportBuilder.KpiItem
                    {
                        Label = kpi.Label,
                        Value = kpi.Number,
                        Subtext = kpi.Sub,
                        AccentColor = PdfSharp.Drawing.XColor.FromArgb(kpi.Accent.R, kpi.Accent.G, kpi.Accent.B)
                    });
                }

                // Configure columns and rows per report type
                switch (_currentType)
                {
                    case ReportType.Sales:
                        doc.Columns.AddRange(new[]
                        {
                            new PdfReportBuilder.ColumnDef { Header = "Ticket #", Width = 75, Alignment = PdfSharp.Drawing.XStringAlignment.Near, IsBold = true },
                            new PdfReportBuilder.ColumnDef { Header = "Customer", Width = 115, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Service", Width = 130, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Paid Date", Width = 80, Alignment = PdfSharp.Drawing.XStringAlignment.Center },
                            new PdfReportBuilder.ColumnDef { Header = "Payment Method", Width = 90, Alignment = PdfSharp.Drawing.XStringAlignment.Center, IsPillBadge = true },
                            new PdfReportBuilder.ColumnDef { Header = "Reference #", Width = 110, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Amount (PHP)", Width = 120, Alignment = PdfSharp.Drawing.XStringAlignment.Far, IsBold = true }
                        });
                        foreach (var s in _viewSales)
                        {
                            doc.Rows.Add(new[]
                            {
                                s.RequestNumber ?? "—",
                                s.Customer ?? "—",
                                s.Service ?? "—",
                                s.PaidOnDisplay ?? "—",
                                string.IsNullOrWhiteSpace(s.Method) ? "Cash" : s.Method,
                                s.Reference ?? "—",
                                $"₱{s.Amount:N2}"
                            });
                        }
                        doc.SummaryFooterText = $"Total Transactions: {_viewSales.Count:N0} • Gross Revenue: ₱{_viewSales.Sum(s => s.Amount):N2}";
                        break;

                    case ReportType.Repairs:
                        doc.Columns.AddRange(new[]
                        {
                            new PdfReportBuilder.ColumnDef { Header = "Ticket #", Width = 75, Alignment = PdfSharp.Drawing.XStringAlignment.Near, IsBold = true },
                            new PdfReportBuilder.ColumnDef { Header = "Priority", Width = 65, Alignment = PdfSharp.Drawing.XStringAlignment.Center, IsPillBadge = true },
                            new PdfReportBuilder.ColumnDef { Header = "Device Model", Width = 110, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Customer", Width = 110, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Issue Description", Width = 140, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Status", Width = 80, Alignment = PdfSharp.Drawing.XStringAlignment.Center, IsPillBadge = true },
                            new PdfReportBuilder.ColumnDef { Header = "Requested Date", Width = 75, Alignment = PdfSharp.Drawing.XStringAlignment.Center },
                            new PdfReportBuilder.ColumnDef { Header = "Est. Cost (PHP)", Width = 65, Alignment = PdfSharp.Drawing.XStringAlignment.Far, IsBold = true }
                        });
                        foreach (var r in _viewRepairs)
                        {
                            doc.Rows.Add(new[]
                            {
                                r.RequestNumber ?? "—",
                                r.PriorityText ?? "Normal",
                                r.DeviceModel ?? "—",
                                r.CustomerName ?? "—",
                                r.IssueDescription ?? "—",
                                r.StatusText ?? "—",
                                r.RequestDate.ToString("yyyy-MM-dd"),
                                $"₱{(r.EstimatedCost ?? 0m):N2}"
                            });
                        }
                        doc.SummaryFooterText = $"Total Tickets: {_viewRepairs.Count:N0} • Est. Pipeline Value: ₱{_viewRepairs.Sum(r => (r.ActualCost ?? r.EstimatedCost) ?? 0m):N2}";
                        break;

                    case ReportType.Customers:
                        doc.Columns.AddRange(new[]
                        {
                            new PdfReportBuilder.ColumnDef { Header = "ID", Width = 45, Alignment = PdfSharp.Drawing.XStringAlignment.Center, IsBold = true },
                            new PdfReportBuilder.ColumnDef { Header = "First Name", Width = 80, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Last Name", Width = 80, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Email Address", Width = 135, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Phone Number", Width = 95, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Address", Width = 130, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Points", Width = 50, Alignment = PdfSharp.Drawing.XStringAlignment.Far, IsBold = true },
                            new PdfReportBuilder.ColumnDef { Header = "Status", Width = 50, Alignment = PdfSharp.Drawing.XStringAlignment.Center, IsPillBadge = true },
                            new PdfReportBuilder.ColumnDef { Header = "Registered", Width = 55, Alignment = PdfSharp.Drawing.XStringAlignment.Center }
                        });
                        foreach (var c in _viewCustomers)
                        {
                            doc.Rows.Add(new[]
                            {
                                c.CustomerId.ToString(),
                                c.FirstName ?? "",
                                c.LastName ?? "",
                                c.Email ?? "—",
                                c.Phone ?? "—",
                                c.Address ?? "—",
                                $"{(c.LoyaltyPoints ?? 0):N0}",
                                c.IsActive ? "Active" : "Archived",
                                c.CreatedAt.ToString("yyyy-MM-dd")
                            });
                        }
                        doc.SummaryFooterText = $"Total Customers: {_viewCustomers.Count:N0} • Active: {_viewCustomers.Count(c => c.IsActive):N0} • Archived: {_viewCustomers.Count(c => !c.IsActive):N0}";
                        break;

                    case ReportType.Services:
                        doc.Columns.AddRange(new[]
                        {
                            new PdfReportBuilder.ColumnDef { Header = "Service Offering", Width = 220, Alignment = PdfSharp.Drawing.XStringAlignment.Near, IsBold = true },
                            new PdfReportBuilder.ColumnDef { Header = "Total Requests", Width = 100, Alignment = PdfSharp.Drawing.XStringAlignment.Center, IsBold = true },
                            new PdfReportBuilder.ColumnDef { Header = "Total Revenue (PHP)", Width = 130, Alignment = PdfSharp.Drawing.XStringAlignment.Far, IsBold = true },
                            new PdfReportBuilder.ColumnDef { Header = "Average Yield (PHP)", Width = 130, Alignment = PdfSharp.Drawing.XStringAlignment.Far },
                            new PdfReportBuilder.ColumnDef { Header = "Last Requested", Width = 140, Alignment = PdfSharp.Drawing.XStringAlignment.Center }
                        });
                        foreach (var s in _viewServices)
                        {
                            doc.Rows.Add(new[]
                            {
                                s.Service ?? "—",
                                s.Requests.ToString("N0"),
                                $"₱{s.Revenue:N2}",
                                $"₱{s.AvgValue:N2}",
                                s.LastRequestDisplay ?? "—"
                            });
                        }
                        doc.SummaryFooterText = $"Service Offerings: {_viewServices.Count:N0} • Aggregate Revenue: ₱{_viewServices.Sum(s => s.Revenue):N2} • Total Workorders: {_viewServices.Sum(s => s.Requests):N0}";
                        break;

                    case ReportType.Interactions:
                        doc.Columns.AddRange(new[]
                        {
                            new PdfReportBuilder.ColumnDef { Header = "Type", Width = 80, Alignment = PdfSharp.Drawing.XStringAlignment.Center, IsPillBadge = true },
                            new PdfReportBuilder.ColumnDef { Header = "Priority", Width = 70, Alignment = PdfSharp.Drawing.XStringAlignment.Center, IsPillBadge = true },
                            new PdfReportBuilder.ColumnDef { Header = "Customer", Width = 130, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Subject / Details", Width = 220, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Status", Width = 80, Alignment = PdfSharp.Drawing.XStringAlignment.Center, IsPillBadge = true },
                            new PdfReportBuilder.ColumnDef { Header = "Logged Date", Width = 70, Alignment = PdfSharp.Drawing.XStringAlignment.Center },
                            new PdfReportBuilder.ColumnDef { Header = "Closed Date", Width = 70, Alignment = PdfSharp.Drawing.XStringAlignment.Center }
                        });
                        foreach (var i in _viewInteractions)
                        {
                            doc.Rows.Add(new[]
                            {
                                i.TypeText ?? "—",
                                i.PriorityText ?? "Normal",
                                i.CustomerDisplay ?? "—",
                                i.Subject ?? "—",
                                i.StatusText ?? "—",
                                i.InteractionDate.ToString("yyyy-MM-dd"),
                                i.ClosedAtDisplay ?? "—"
                            });
                        }
                        doc.SummaryFooterText = $"Total Cases: {_viewInteractions.Count:N0} • Resolved: {_viewInteractions.Count(i => i.Status == 2):N0} • Open / In Progress: {_viewInteractions.Count(i => i.Status != 2):N0}";
                        break;

                    case ReportType.Loyalty:
                        doc.Columns.AddRange(new[]
                        {
                            new PdfReportBuilder.ColumnDef { Header = "Customer Name", Width = 150, Alignment = PdfSharp.Drawing.XStringAlignment.Near, IsBold = true },
                            new PdfReportBuilder.ColumnDef { Header = "Program Tier", Width = 120, Alignment = PdfSharp.Drawing.XStringAlignment.Center, IsPillBadge = true },
                            new PdfReportBuilder.ColumnDef { Header = "Points Balance", Width = 100, Alignment = PdfSharp.Drawing.XStringAlignment.Far, IsBold = true },
                            new PdfReportBuilder.ColumnDef { Header = "Lifetime Spend (PHP)", Width = 140, Alignment = PdfSharp.Drawing.XStringAlignment.Far, IsBold = true },
                            new PdfReportBuilder.ColumnDef { Header = "Member Since", Width = 120, Alignment = PdfSharp.Drawing.XStringAlignment.Center },
                            new PdfReportBuilder.ColumnDef { Header = "Status", Width = 90, Alignment = PdfSharp.Drawing.XStringAlignment.Center, IsPillBadge = true }
                        });
                        foreach (var l in _viewLoyalty)
                        {
                            doc.Rows.Add(new[]
                            {
                                l.CustomerName ?? "—",
                                l.ProgramName ?? "—",
                                l.Points.ToString("N0"),
                                $"₱{l.TotalSpent:N2}",
                                l.JoinedDate.ToString("yyyy-MM-dd"),
                                l.IsActive ? "Active" : "Inactive"
                            });
                        }
                        doc.SummaryFooterText = $"Enrolled Members: {_viewLoyalty.Count:N0} • Circulating Points: {_viewLoyalty.Sum(l => l.Points):N0} pts • Member Spend: ₱{_viewLoyalty.Sum(l => l.TotalSpent):N2}";
                        break;

                    case ReportType.Retention:
                        doc.Columns.AddRange(new[]
                        {
                            new PdfReportBuilder.ColumnDef { Header = "Customer Name", Width = 120, Alignment = PdfSharp.Drawing.XStringAlignment.Near, IsBold = true },
                            new PdfReportBuilder.ColumnDef { Header = "Segment", Width = 95, Alignment = PdfSharp.Drawing.XStringAlignment.Center, IsPillBadge = true },
                            new PdfReportBuilder.ColumnDef { Header = "Recommended Action", Width = 140, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Trigger Basis", Width = 135, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Incentive Reward", Width = 90, Alignment = PdfSharp.Drawing.XStringAlignment.Near },
                            new PdfReportBuilder.ColumnDef { Header = "Past Spend (PHP)", Width = 80, Alignment = PdfSharp.Drawing.XStringAlignment.Far, IsBold = true },
                            new PdfReportBuilder.ColumnDef { Header = "Days Inactive", Width = 60, Alignment = PdfSharp.Drawing.XStringAlignment.Far }
                        });
                        foreach (var r in _viewRetention)
                        {
                            doc.Rows.Add(new[]
                            {
                                r.CustomerName ?? "—",
                                r.Category ?? "—",
                                r.Action ?? "—",
                                r.Basis ?? "—",
                                r.Reward ?? "—",
                                $"₱{r.TotalSpent:N2}",
                                r.DaysSinceLastTransaction.ToString()
                            });
                        }
                        doc.SummaryFooterText = $"Actionable Accounts: {_viewRetention.Count:N0} • Recoverable Past Spend: ₱{_viewRetention.Sum(r => r.TotalSpent):N2}";
                        break;
                }

                PdfReportBuilder.GenerateReport(doc, sfd.FileName);
                Toast.Notify(FindForm(), "Report Exported", $"Saved {Path.GetFileName(sfd.FileName)} successfully.", ToastKind.Success);
            }
            catch (Exception ex)
            {
                Toast.Notify(FindForm(), "Export Failed", ex.Message, ToastKind.Error);
            }
        }

        private static string Val(Dictionary<string, object?> row, string key) =>
            row.TryGetValue(key, out var v) ? (v?.ToString() ?? "") : "";

        // ═══════════════════════════════════════════════════════════════
        //  CLEAN & MINIMAL SUPPORTING CONTROLS (DASHBOARD STYLE)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Minimal KPI Card — Clean card matching Dashboard styling, ample vertical room, zero clipped numbers.
        /// </summary>
        [DesignerCategory("Code")]
        private sealed class ReportKpiCard : Control
        {
            private const int Pad = 18;

            private static readonly Font[] NumFonts =
            {
                AppFonts.Strong(22F),
                AppFonts.Strong(18F),
                AppFonts.Strong(15F),
                AppFonts.Strong(13F),
                AppFonts.Strong(11F)
            };

            private string _label = "";
            private string _number = "0";
            private string _sub = "";
            private Color _accent = AppTheme.Primary;
            private bool _hover;

            public event EventHandler? ContentChanged;

            public ReportKpiCard()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
            }

            public void Set(string label, string number, string sub, Color accent)
            {
                _label = label;
                _number = number;
                _sub = sub;
                _accent = accent;
                Invalidate();
                ContentChanged?.Invoke(this, EventArgs.Empty);
            }

            public string Label => _label;
            public string Number => _number;
            public string Sub => _sub;
            public Color Accent => _accent;

            public int HeightFor(int width) => Math.Max(116, Measure(width).Total);

            private (Font NumFont, int TopH, int NumH, int CapH, int Total) Measure(int width)
            {
                int inner = Math.Max(20, width - Pad * 2);
                int topH = Math.Max(16, UiKit.T.SmallStrong.Height);

                Font numFont = NumFonts[^1];
                foreach (var f in NumFonts)
                {
                    var size = TextRenderer.MeasureText(_number, f, new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                    if (size.Width <= inner) { numFont = f; break; }
                }

                int numH = Math.Max(numFont.Height + 2, TextRenderer.MeasureText(_number, numFont, new Size(inner, int.MaxValue),
                    TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height + 2);

                int capH = string.IsNullOrEmpty(_sub) ? 0 : Math.Max(UiKit.T.Small.Height + 2,
                    TextRenderer.MeasureText(_sub, UiKit.T.Small, new Size(inner, int.MaxValue),
                        TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height + 2);

                int total = Pad + topH + 8 + numH + (capH > 0 ? 4 + capH : 0) + Pad;
                return (numFont, topH, numH, capH, total);
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var bg = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(bg, ClientRectangle);

                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                UiKit.Card(g, r, 10, UiKit.T.Surface, _hover ? _accent : UiKit.T.Line);

                var m = Measure(Width);
                int inner = Math.Max(20, Width - Pad * 2);

                // 1. Accent Dot + Label
                UiKit.Dot(g, Pad + 3, Pad + m.TopH / 2f, 6, _accent);
                var lblRect = new Rectangle(Pad + 12, Pad, inner - 12, m.TopH);
                UiKit.Text(g, _label, UiKit.T.SmallStrong, UiKit.T.InkMuted, lblRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                // 2. Large Number (Rendered with Top alignment so top is NEVER clipped)
                int numTop = Pad + m.TopH + 8;
                var numRect = new Rectangle(Pad, numTop, inner, m.NumH);
                UiKit.Text(g, _number, m.NumFont, UiKit.T.Ink, numRect,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                // 3. Subtitle (Rendered with Top alignment so bottom is NEVER clipped)
                if (!string.IsNullOrEmpty(_sub) && m.CapH > 0)
                {
                    int capTop = numTop + m.NumH + 4;
                    var subRect = new Rectangle(Pad, capTop, inner, m.CapH);
                    UiKit.Text(g, _sub, UiKit.T.Small, UiKit.T.InkFaint, subRect,
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                }
            }
        }

        /// <summary>
        /// Date Range Bar — Clean preset selector and full-width date pickers (zero cut off).
        /// </summary>
        [DesignerCategory("Code")]
        private sealed class ReportDateRangeBar : Panel
        {
            private readonly ComboBox _cboPresets;
            private readonly DateTimePicker _dtpFrom;
            private readonly Label _lblTo;
            private readonly DateTimePicker _dtpTo;
            private bool _suppressPreset = false;

            public event EventHandler? RangeChanged;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public DateTime From => _dtpFrom.Value.Date;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public DateTime To => _dtpTo.Value.Date.AddDays(1).AddSeconds(-1);

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public int PreferredWidth => 434;

            public ReportDateRangeBar()
            {
                BackColor = AppTheme.Background;
                Height = 36;

                _cboPresets = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = UiKit.T.SmallStrong,
                    ForeColor = UiKit.T.Ink,
                    BackColor = UiKit.T.Surface,
                    Width = 120
                };
                _cboPresets.Items.AddRange(new object[]
                {
                    "Last 30 Days",
                    "This Month",
                    "Last 90 Days",
                    "Year to Date",
                    "All Time",
                    "Custom Range"
                });
                _cboPresets.SelectedIndex = 0; // Default: Last 30 Days

                _dtpFrom = new DateTimePicker
                {
                    Format = DateTimePickerFormat.Short,
                    Font = UiKit.T.Small,
                    Width = 130, // 130px prevents truncation of "30/08/2026"
                    Value = DateTime.Today.AddDays(-30)
                };

                _lblTo = new Label
                {
                    Text = "to",
                    Font = UiKit.T.SmallStrong,
                    ForeColor = UiKit.T.InkMuted,
                    AutoSize = true,
                    BackColor = AppTheme.Background,
                    UseMnemonic = false
                };

                _dtpTo = new DateTimePicker
                {
                    Format = DateTimePickerFormat.Short,
                    Font = UiKit.T.Small,
                    Width = 130, // 130px prevents truncation of "29/09/2026"
                    Value = DateTime.Today
                };

                _cboPresets.SelectedIndexChanged += OnPresetChanged;
                _dtpFrom.ValueChanged += (s, e) => OnDateManuallyChanged();
                _dtpTo.ValueChanged += (s, e) => OnDateManuallyChanged();

                Controls.Add(_cboPresets);
                Controls.Add(_dtpFrom);
                Controls.Add(_lblTo);
                Controls.Add(_dtpTo);

                Resize += (s, e) => LayoutBar();
                LayoutBar();
            }

            private void LayoutBar()
            {
                int y = (Height - _cboPresets.Height) / 2;
                _cboPresets.Location = new Point(0, y);
                _dtpFrom.Location = new Point(_cboPresets.Right + 8, (Height - _dtpFrom.Height) / 2);
                _lblTo.Location = new Point(_dtpFrom.Right + 6, (Height - _lblTo.Height) / 2);
                _dtpTo.Location = new Point(_lblTo.Right + 6, (Height - _dtpTo.Height) / 2);
            }

            private void OnPresetChanged(object? sender, EventArgs e)
            {
                if (_suppressPreset) return;

                var selected = _cboPresets.SelectedItem?.ToString();
                if (selected == "Custom Range") return;

                _suppressPreset = true;
                var today = DateTime.Today;

                switch (selected)
                {
                    case "Last 30 Days":
                        _dtpFrom.Value = today.AddDays(-30);
                        _dtpTo.Value = today;
                        break;
                    case "This Month":
                        _dtpFrom.Value = new DateTime(today.Year, today.Month, 1);
                        _dtpTo.Value = today;
                        break;
                    case "Last 90 Days":
                        _dtpFrom.Value = today.AddDays(-90);
                        _dtpTo.Value = today;
                        break;
                    case "Year to Date":
                        _dtpFrom.Value = new DateTime(today.Year, 1, 1);
                        _dtpTo.Value = today;
                        break;
                    case "All Time":
                        _dtpFrom.Value = new DateTime(2020, 1, 1);
                        _dtpTo.Value = today;
                        break;
                }

                _suppressPreset = false;
                RangeChanged?.Invoke(this, EventArgs.Empty);
            }

            private void OnDateManuallyChanged()
            {
                if (_suppressPreset) return;
                _suppressPreset = true;
                _cboPresets.SelectedItem = "Custom Range";
                _suppressPreset = false;
                RangeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Minimal Segmented Navigation Bar — Clean text buttons, no icons/emojis, fits with zero truncation.
        /// </summary>
        [DesignerCategory("Code")]
        private sealed class ReportTabSegments : Control
        {
            private static readonly (string Label, ReportType Type)[] Items = new[]
            {
                ("Sales", ReportType.Sales),
                ("Repairs", ReportType.Repairs),
                ("Customers", ReportType.Customers),
                ("Services", ReportType.Services),
                ("Interactions", ReportType.Interactions),
                ("Loyalty", ReportType.Loyalty),
                ("Retention", ReportType.Retention)
            };

            private int _selected = 0;
            private int _hover = -1;

            public event EventHandler? SelectedChanged;

            public ReportType SelectedType => Items[_selected].Type;

            public ReportTabSegments()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
                Cursor = Cursors.Hand;
                Height = 36;
            }

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public int SelectedIndex
            {
                get => _selected;
                set
                {
                    if (value < 0 || value >= Items.Length || value == _selected) return;
                    _selected = value;
                    Invalidate();
                    SelectedChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            private int ItemWidth(int i) => UiKit.Measure(Items[i].Label, UiKit.T.SmallStrong).Width + 28;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public int PreferredWidth => Items.Select((_, i) => ItemWidth(i)).Sum() + 8;

            private Rectangle ItemRect(int i)
            {
                int x = 4;
                for (int k = 0; k < i; k++) x += ItemWidth(k);
                return new Rectangle(x, 3, ItemWidth(i), Height - 6);
            }

            private int HitTest(Point p)
            {
                for (int i = 0; i < Items.Length; i++)
                    if (ItemRect(i).Contains(p)) return i;
                return -1;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                int idx = HitTest(e.Location);
                if (idx != _hover) { _hover = idx; Invalidate(); }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                int idx = HitTest(e.Location);
                if (idx >= 0 && idx != _selected)
                {
                    _selected = idx;
                    Invalidate();
                    SelectedChanged?.Invoke(this, EventArgs.Empty);
                }
                base.OnMouseDown(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var bg = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(bg, ClientRectangle);

                int totalW = PreferredWidth;
                var barRect = new Rectangle(0, 0, totalW, Height);
                UiKit.Card(g, barRect, 8, UiKit.T.Surface, UiKit.T.Line);

                for (int i = 0; i < Items.Length; i++)
                {
                    var r = ItemRect(i);
                    bool sel = i == _selected;

                    if (sel)
                        UiKit.FillRounded(g, r, 6, AppTheme.Primary);
                    else if (i == _hover)
                        UiKit.FillRounded(g, r, 6, UiKit.T.RowHover);

                    Color fg = sel ? Color.White : (i == _hover ? UiKit.T.Ink : UiKit.T.InkMuted);
                    UiKit.Text(g, Items[i].Label, UiKit.T.SmallStrong, fg, r, UiKit.Center);
                }
            }
        }

        /// <summary>
        /// Secondary quick filter pill bar (e.g. All, Cash, Card, GCash).
        /// </summary>
        [DesignerCategory("Code")]
        private sealed class ReportSubFilter : Control
        {
            private string[] _items = Array.Empty<string>();
            private int _selected = 0;
            private int _hover = -1;

            public event EventHandler? FilterChanged;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public string Selected => _selected >= 0 && _selected < _items.Length ? _items[_selected] : "All";

            public ReportSubFilter()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = UiKit.T.Surface;
                Cursor = Cursors.Hand;
            }

            public void SetItems(string[] items)
            {
                _items = items ?? Array.Empty<string>();
                _selected = 0;
                _hover = -1;
                Invalidate();
            }

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public int PreferredWidth
            {
                get
                {
                    int total = 4;
                    foreach (var it in _items)
                        total += UiKit.Measure(it, UiKit.T.SmallStrong).Width + 24;
                    return total;
                }
            }

            private Rectangle GetPillRect(int index)
            {
                int x = 2;
                for (int i = 0; i < _items.Length; i++)
                {
                    int w = UiKit.Measure(_items[i], UiKit.T.SmallStrong).Width + 20;
                    if (i == index)
                        return new Rectangle(x, (Height - 26) / 2, w, 26);
                    x += w + 6;
                }
                return Rectangle.Empty;
            }

            private int IndexAt(Point pt)
            {
                for (int i = 0; i < _items.Length; i++)
                {
                    if (GetPillRect(i).Contains(pt)) return i;
                }
                return -1;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                int idx = IndexAt(e.Location);
                if (idx != _hover) { _hover = idx; Invalidate(); }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _hover = -1;
                Invalidate();
                base.OnMouseLeave(e);
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                int idx = IndexAt(e.Location);
                if (idx >= 0 && idx != _selected)
                {
                    _selected = idx;
                    Invalidate();
                    FilterChanged?.Invoke(this, EventArgs.Empty);
                }
                base.OnMouseClick(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var bgBrush = new SolidBrush(UiKit.T.Surface))
                    g.FillRectangle(bgBrush, ClientRectangle);

                for (int i = 0; i < _items.Length; i++)
                {
                    var pr = GetPillRect(i);
                    bool active = i == _selected;
                    bool hot = i == _hover;

                    if (active)
                    {
                        UiKit.FillRounded(g, pr, 13, AppTheme.Primary);
                        UiKit.Text(g, _items[i], UiKit.T.SmallStrong, Color.White, pr, UiKit.Center);
                    }
                    else
                    {
                        Color bg = hot ? UiKit.T.RowHover : UiKit.T.Surface;
                        UiKit.FillRounded(g, pr, 13, bg);
                        using var pen = new Pen(UiKit.T.Line, 1);
                        using var path = UiKit.Rounded(new Rectangle(pr.X, pr.Y, pr.Width - 1, pr.Height - 1), 13);
                        g.DrawPath(pen, path);
                        UiKit.Text(g, _items[i], UiKit.T.Small, hot ? UiKit.T.Ink : UiKit.T.InkMuted, pr, UiKit.Center);
                    }
                }
            }
        }

        /// <summary>
        /// Summary Aggregate Bar — Minimalist text summary at the table footer, zero noisy icons.
        /// </summary>
        [DesignerCategory("Code")]
        private sealed class ReportSummaryBar : Control
        {
            private string _stats = "";

            public ReportSummaryBar()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = UiKit.T.Surface;
            }

            public void SetStats(string stats)
            {
                _stats = stats;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                // Top hairline divider
                using (var pen = new Pen(UiKit.T.LineSoft, 1))
                    g.DrawLine(pen, 0, 0, Width, 0);

                var textRect = new Rectangle(UiKit.T.S4, 0, Width - UiKit.T.S4 * 2, Height);
                UiKit.Text(g, _stats, UiKit.T.SmallStrong, UiKit.T.InkMuted, textRect, UiKit.Left);
            }
        }
    }
}