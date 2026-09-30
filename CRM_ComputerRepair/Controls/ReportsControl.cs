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
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Reports page — professional business intelligence workspace.
    /// UI aligned with Tenants / Subscriptions / Admin / System Monitor / Customers / Repairs.
    /// All data fetching, filtering, tab switching, and export logic is unchanged.
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

        private List<CustomerDto> _rawCustomers = new();
        private List<RepairRequestDto> _rawRepairs = new();
        private List<InteractionDto> _rawInteractions = new();
        private List<SalesReportItem> _rawSales = new();
        private List<ServicesReportItem> _rawServices = new();
        private List<LoyaltyMemberDetailDto> _rawLoyalty = new();
        private List<RetentionRecommendationDto> _rawRetention = new();

        private List<SalesReportItem> _viewSales = new();
        private List<RepairRequestDto> _viewRepairs = new();
        private List<CustomerDto> _viewCustomers = new();
        private List<ServicesReportItem> _viewServices = new();
        private List<InteractionDto> _viewInteractions = new();
        private List<LoyaltyMemberDetailDto> _viewLoyalty = new();
        private List<RetentionRecommendationDto> _viewRetention = new();

        private int _hoverRow = -1;

        // ═══════════ UI CONTROLS ═══════════

        private Label lblTitle = null!;
        private Label lblSummary = null!;
        private ReportDateRangeBar dateRangeBar = null!;
        private SaasButton btnRefresh = null!;
        private SaasButton btnExport = null!;

        private readonly List<ReportKpiCard> _kpiCards = new();

        private ReportTabSegments tabSegments = null!;

        private WorkbenchCard card = null!;
        private Label lblGridTitle = null!;
        private Label lblCount = null!;
        private ReportSubFilter subFilter = null!;
        private WorkbenchSearch search = null!;
        private DataGridView dgv = null!;
        private ReportSummaryBar summaryBar = null!;
        private TablePagination pager = null!;
        private WorkbenchState state = null!;

        // Shared drawing helpers
        private const int CellPadX = 14;
        private const TextFormatFlags Flat = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        private const TextFormatFlags CellText = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | Flat;
        private static readonly Font MonoFont = new Font("Consolas", 9F);

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
            lblTitle = new Label
            {
                Text = "Reports",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            lblSummary = new Label
            {
                Text = "Business intelligence  ·  revenue, repairs, customers, loyalty & retention",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            dateRangeBar = new ReportDateRangeBar();
            dateRangeBar.RangeChanged += async (s, e) => await ReloadAsync();

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Size = new Size(100, 36);
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            btnExport = new SaasButton("Export PDF", SaasButtonVariant.Primary, "\uE74E");
            btnExport.Size = new Size(140, 36);
            btnExport.Click += (s, e) => ExportPdf();

            Controls.Add(lblTitle);
            Controls.Add(lblSummary);
            Controls.Add(dateRangeBar);
            Controls.Add(btnRefresh);
            Controls.Add(btnExport);

            for (int i = 0; i < 4; i++)
            {
                var kpi = new ReportKpiCard();
                kpi.ContentChanged += (s, e) => LayoutUi();
                _kpiCards.Add(kpi);
                Controls.Add(kpi);
            }

            _kpiCards[0].Click += (s, e) => SwitchToTab(ReportType.Sales);
            _kpiCards[1].Click += (s, e) => SwitchToTab(ReportType.Repairs);
            _kpiCards[2].Click += (s, e) => SwitchToTab(ReportType.Customers);

            tabSegments = new ReportTabSegments();
            tabSegments.SelectedChanged += (s, e) => SwitchTab(tabSegments.SelectedType);
            Controls.Add(tabSegments);

            card = new WorkbenchCard { BackColor = UiKit.T.Surface };

            lblGridTitle = new Label
            {
                Text = "Sales ledger",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            lblCount = new Label
            {
                Text = "0 records",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
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
                Placeholder = "Search records..."
            };
            search.QueryChanged += (s, e) => ApplyFilter(resetPage: true);

            dgv = new DataGridView();
            StyleGrid(dgv);
            dgv.CellPainting += (s, e) => PaintCell(dgv, e);
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

        // ═══════════ LAYOUT ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            int pad = UiKit.T.S6;
            int contentW = Math.Max(760, Width - pad * 2);

            lblTitle.Location = new Point(pad, pad);

            int btnY = pad;
            btnExport.Location = new Point(pad + contentW - btnExport.Width, btnY);
            btnRefresh.Location = new Point(btnExport.Left - btnRefresh.Width - 10, btnY);

            int dateW = dateRangeBar.PreferredWidth;
            dateRangeBar.Size = new Size(dateW, 36);
            dateRangeBar.Location = new Point(btnRefresh.Left - dateW - 12, btnY);

            lblSummary.Location = new Point(pad + 1, lblTitle.Bottom + 6);

            int kpiTop = lblSummary.Bottom + 22;
            int kpiGap = 16;
            int cardW = Math.Max(160, (contentW - kpiGap * 3) / 4);
            int kpiH = Math.Max(132, _kpiCards.Count > 0 ? _kpiCards.Max(c => c.HeightFor(cardW)) : 132);

            for (int i = 0; i < _kpiCards.Count; i++)
            {
                int x = pad + i * (cardW + kpiGap);
                _kpiCards[i].SetBounds(x, kpiTop, cardW, kpiH);
            }

            int tabsTop = kpiTop + kpiH + 16;
            tabSegments.SetBounds(pad, tabsTop, tabSegments.PreferredWidth, 36);

            int cardPad = UiKit.T.S6;
            int cardTop = tabsTop + 36 + 14;
            int cardH = Math.Max(300, Height - cardTop - pad);
            card.SetBounds(pad, cardTop, contentW, cardH);

            int innerW = contentW - cardPad * 2;

            int filterW = Math.Min(subFilter.PreferredWidth, Math.Max(140, innerW / 2));
            int searchW = Math.Max(240, Math.Min(440, innerW - filterW - 20));

            search.SetBounds(cardPad, cardPad, searchW, 38);
            subFilter.SetBounds(cardPad + innerW - filterW, cardPad + 1, filterW, 36);

            int gridY = search.Bottom + 14;
            const int summaryH = 34;
            int footerH = pager.Visible ? TableKit.FooterH : 0;
            int gridH = cardH - gridY - cardPad - summaryH - footerH - 6;

            dgv.SetBounds(cardPad, gridY, innerW, Math.Max(120, gridH));
            summaryBar.SetBounds(cardPad, gridY + Math.Max(0, gridH), innerW, summaryH);
            if (pager.Visible)
                pager.SetBounds(cardPad, gridY + Math.Max(0, gridH) + summaryH, innerW, TableKit.FooterH);
            state.SetBounds(cardPad, gridY, innerW, Math.Max(120, gridH) + summaryH + footerH);

            lblCount.Location = new Point(cardPad, gridY - 22);
        }

        // ═══════════ DATA LOAD (unchanged) ═══════════

        public async Task ReloadAsync()
        {
            state.Show("\uE895", "Loading report data…", "Fetching records.");
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

                _rawCustomers = custResult.Where(c => c.CreatedAt >= from && c.CreatedAt <= to).ToList();
                _rawRepairs = repResult.Where(r => r.RequestDate >= from && r.RequestDate <= to).ToList();
                _rawInteractions = interResult.Where(i => i.InteractionDate >= from && i.InteractionDate <= to).ToList();

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

                lblSummary.Text = $"Business intelligence  ·  {_rawSales.Count} sales  ·  {_rawRepairs.Count} repairs  ·  {_rawCustomers.Count} customers  ·  {_rawLoyalty.Count} loyalty members";
            }
            catch (Exception ex)
            {
                state.Show("\uE711", "Failed to load report", ex.Message);
                SaasToast.Show(FindForm(), $"Couldn't load report data: {ex.Message}", ToastKind.Danger);
            }
        }

        // ═══════════ KPI CARDS ═══════════

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

                        _kpiCards[0].Set("Gross revenue", $"₱{gross:N2}", $"{count} payments", AppTheme.Success, "\uE8C7");
                        _kpiCards[1].Set("Total transactions", count.ToString(), "In selected period", AppTheme.Primary, "\uE9D5");
                        _kpiCards[2].Set("Average ticket", $"₱{avg:N2}", "Per payment", AppTheme.Warning, "\uE8EF");
                        _kpiCards[3].Set("Top method", topMethod, "Most used", AppTheme.Info, "\uE8AB");
                        break;
                    }

                case ReportType.Repairs:
                    {
                        int total = _rawRepairs.Count;
                        int completed = _rawRepairs.Count(r => r.Status == 3);
                        int active = _rawRepairs.Count(r => r.Status is 1 or 2);
                        decimal pipeline = _rawRepairs.Sum(r => (r.ActualCost ?? r.EstimatedCost) ?? 0m);
                        double rate = total > 0 ? (completed * 100.0 / total) : 0.0;

                        _kpiCards[0].Set("Total tickets", total.ToString(), "Intake requests", AppTheme.Primary, "\uE9D5");
                        _kpiCards[1].Set("Completed", $"{completed} ({rate:F0}%)", "Finished repairs", AppTheme.Success, "\uE73E");
                        _kpiCards[2].Set("In progress", active.ToString(), "Active on bench", AppTheme.Warning, "\uE9F5");
                        _kpiCards[3].Set("Pipeline value", $"₱{pipeline:N2}", "Est. parts & labor", AppTheme.Info, "\uE8C7");
                        break;
                    }

                case ReportType.Customers:
                    {
                        int total = _rawCustomers.Count;
                        int active = _rawCustomers.Count(c => c.IsActive);
                        int archived = total - active;
                        int totalPoints = _rawCustomers.Sum(c => c.LoyaltyPoints ?? 0);

                        _kpiCards[0].Set("Total registered", total.ToString(), "Accounts", AppTheme.Primary, "\uE716");
                        _kpiCards[1].Set("Active accounts", active.ToString(), "In good standing", AppTheme.Success, "\uE73E");
                        _kpiCards[2].Set("Archived", archived.ToString(), "Inactive", UiKit.T.InkMuted, "\uE7B8");
                        _kpiCards[3].Set("Loyalty points", $"{totalPoints:N0} pts", "Active points", AppTheme.Warning, "\uE8EF");
                        break;
                    }

                case ReportType.Services:
                    {
                        int lines = _rawServices.Count;
                        decimal rev = _rawServices.Sum(s => s.Revenue);
                        var topRev = _rawServices.OrderByDescending(s => s.Revenue).FirstOrDefault();
                        var topVol = _rawServices.OrderByDescending(s => s.Requests).FirstOrDefault();

                        _kpiCards[0].Set("Service offerings", lines.ToString(), "Catalog items", AppTheme.Primary, "\uE9D5");
                        _kpiCards[1].Set("Total revenue", $"₱{rev:N2}", "Service revenue", AppTheme.Success, "\uE8C7");
                        _kpiCards[2].Set("Top service", topRev?.Service ?? "—", topRev != null ? $"₱{topRev.Revenue:N2}" : "—", AppTheme.Warning, "\uE8EF");
                        _kpiCards[3].Set("Highest volume", topVol?.Service ?? "—", topVol != null ? $"{topVol.Requests} jobs" : "—", AppTheme.Info, "\uE716");
                        break;
                    }

                case ReportType.Interactions:
                    {
                        int total = _rawInteractions.Count;
                        int closed = _rawInteractions.Count(i => i.Status == 2);
                        int open = total - closed;
                        int urgent = _rawInteractions.Count(i => i.Priority == 2);
                        double resolveRate = total > 0 ? (closed * 100.0 / total) : 0.0;

                        _kpiCards[0].Set("Total logs", total.ToString(), "Support entries", AppTheme.Primary, "\uE8BD");
                        _kpiCards[1].Set("Resolved", $"{closed} ({resolveRate:F0}%)", "Closed tickets", AppTheme.Success, "\uE73E");
                        _kpiCards[2].Set("Open & in progress", open.ToString(), "Pending attention", AppTheme.Warning, "\uE9F5");
                        _kpiCards[3].Set("Urgent priority", urgent.ToString(), "High priority", AppTheme.Danger, "\uE7BA");
                        break;
                    }

                case ReportType.Loyalty:
                    {
                        int members = _rawLoyalty.Count;
                        int active = _rawLoyalty.Count(m => m.IsActive);
                        decimal spend = _rawLoyalty.Sum(m => m.TotalSpent);
                        int points = _rawLoyalty.Sum(m => m.Points);

                        _kpiCards[0].Set("Enrolled members", members.ToString(), "Participants", AppTheme.Primary, "\uE716");
                        _kpiCards[1].Set("Active members", active.ToString(), "Active status", AppTheme.Success, "\uE73E");
                        _kpiCards[2].Set("Lifetime spend", $"₱{spend:N2}", "Member spending", AppTheme.Warning, "\uE8C7");
                        _kpiCards[3].Set("Points balance", $"{points:N0} pts", "In circulation", AppTheme.Info, "\uE8EF");
                        break;
                    }

                case ReportType.Retention:
                    {
                        int total = _rawRetention.Count;
                        int atRisk = _rawRetention.Count(r => r.Category.Contains("Risk", StringComparison.OrdinalIgnoreCase));
                        int inactive = _rawRetention.Count(r => r.Category.Contains("Inactive", StringComparison.OrdinalIgnoreCase));
                        decimal spendAtRisk = _rawRetention
                            .Where(r => r.Category.Contains("Risk", StringComparison.OrdinalIgnoreCase)
                                     || r.Category.Contains("Inactive", StringComparison.OrdinalIgnoreCase))
                            .Sum(r => r.TotalSpent);

                        _kpiCards[0].Set("Actionable leads", total.ToString(), "Recommendations", AppTheme.Primary, "\uE716");
                        _kpiCards[1].Set("At-risk customers", atRisk.ToString(), "Drifting away", AppTheme.Warning, "\uE7BA");
                        _kpiCards[2].Set("Inactive customers", inactive.ToString(), "90+ days inactive", AppTheme.Danger, "\uE711");
                        _kpiCards[3].Set("Past spend at risk", $"₱{spendAtRisk:N2}", "Recoverable LTV", AppTheme.Info, "\uE8C7");
                        break;
                    }
            }

            LayoutUi();
        }

        // ═══════════ TAB SWITCHING (unchanged) ═══════════

        private void SwitchToTab(ReportType type)
        {
            var items = new[] { ReportType.Sales, ReportType.Repairs, ReportType.Customers, ReportType.Services,
                                ReportType.Interactions, ReportType.Loyalty, ReportType.Retention };
            int idx = Array.IndexOf(items, type);
            if (idx >= 0) tabSegments.SelectedIndex = idx;
            else SwitchTab(type);
        }

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

            search.Placeholder = type switch
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

        // ═══════════ FILTERING (unchanged) ═══════════

        private void ApplyFilter(bool resetPage = true)
        {
            if (resetPage) pager.Reset();
            var term = (search.Query ?? "").Trim();

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
                        dgv.DataSource = null;
                        dgv.DataSource = pager.Slice(_viewSales);
                        ConfigureSalesColumns();

                        lblCount.Text = $"Showing {_viewSales.Count} of {_rawSales.Count} sales";
                        decimal sum = _viewSales.Sum(s => s.Amount);
                        decimal avg = _viewSales.Count > 0 ? sum / _viewSales.Count : 0m;
                        summaryBar.SetStats($"Total  ₱{sum:N2}   ·   Average  ₱{avg:N2}   ·   {_viewSales.Count} payments");
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
                        dgv.DataSource = null;
                        dgv.DataSource = pager.Slice(_viewRepairs);
                        ConfigureRepairColumns();

                        lblCount.Text = $"Showing {_viewRepairs.Count} of {_rawRepairs.Count} repairs";
                        decimal pipeline = _viewRepairs.Sum(r => (r.ActualCost ?? r.EstimatedCost) ?? 0m);
                        int completed = _viewRepairs.Count(r => r.Status == 3);
                        summaryBar.SetStats($"Pipeline  ₱{pipeline:N2}   ·   Completed  {completed}   ·   {_viewRepairs.Count} tickets");
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
                        dgv.DataSource = null;
                        dgv.DataSource = pager.Slice(_viewCustomers);
                        ConfigureCustomerColumns();

                        lblCount.Text = $"Showing {_viewCustomers.Count} of {_rawCustomers.Count} customers";
                        int active = _viewCustomers.Count(c => c.IsActive);
                        int points = _viewCustomers.Sum(c => c.LoyaltyPoints ?? 0);
                        summaryBar.SetStats($"Active  {active}   ·   Points  {points:N0} pts   ·   {_viewCustomers.Count} customers");
                        ShowStateOrGrid(_viewCustomers.Count);
                        break;
                    }

                case ReportType.Services:
                    {
                        var q = _rawServices.AsEnumerable();
                        if (_currentSubFilter == "Top Volume") q = q.OrderByDescending(s => s.Requests);
                        else if (_currentSubFilter == "Top Revenue") q = q.OrderByDescending(s => s.Revenue);
                        if (!string.IsNullOrEmpty(term))
                            q = q.Where(s => s.Service.Contains(term, StringComparison.OrdinalIgnoreCase));

                        _viewServices = q.ToList();
                        pager.SetTotal(_viewServices.Count);
                        dgv.DataSource = null;
                        dgv.DataSource = pager.Slice(_viewServices);
                        ConfigureServiceColumns();

                        lblCount.Text = $"Showing {_viewServices.Count} of {_rawServices.Count} services";
                        decimal rev = _viewServices.Sum(s => s.Revenue);
                        int jobs = _viewServices.Sum(s => s.Requests);
                        summaryBar.SetStats($"Revenue  ₱{rev:N2}   ·   Volume  {jobs} jobs   ·   {_viewServices.Count} services");
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
                        dgv.DataSource = null;
                        dgv.DataSource = pager.Slice(_viewInteractions);
                        ConfigureInteractionColumns();

                        lblCount.Text = $"Showing {_viewInteractions.Count} of {_rawInteractions.Count} interactions";
                        int closed = _viewInteractions.Count(i => i.Status == 2);
                        int urgent = _viewInteractions.Count(i => i.Priority == 2);
                        summaryBar.SetStats($"Closed  {closed}   ·   Urgent  {urgent}   ·   {_viewInteractions.Count} entries");
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
                        dgv.DataSource = null;
                        dgv.DataSource = pager.Slice(_viewLoyalty);
                        ConfigureLoyaltyColumns();

                        lblCount.Text = $"Showing {_viewLoyalty.Count} of {_rawLoyalty.Count} members";
                        decimal spend = _viewLoyalty.Sum(m => m.TotalSpent);
                        int active = _viewLoyalty.Count(m => m.IsActive);
                        summaryBar.SetStats($"Total spend  ₱{spend:N2}   ·   Active  {active}   ·   {_viewLoyalty.Count} members");
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
                        dgv.DataSource = null;
                        dgv.DataSource = pager.Slice(_viewRetention);
                        ConfigureRetentionColumns();

                        lblCount.Text = $"Showing {_viewRetention.Count} of {_rawRetention.Count} leads";
                        decimal atRiskVal = _viewRetention.Sum(r => r.TotalSpent);
                        summaryBar.SetStats($"Spend at risk  ₱{atRiskVal:N2}   ·   {_viewRetention.Count} leads");
                        ShowStateOrGrid(_viewRetention.Count);
                        break;
                    }
            }
        }

        private void ShowStateOrGrid(int count)
        {
            if (count > 0)
            {
                state.Visible = false;
                dgv.Visible = true;
                summaryBar.Visible = true;
                pager.Visible = true;
            }
            else
            {
                dgv.Visible = false;
                summaryBar.Visible = false;
                pager.Visible = false;

                string kw = (search.Query ?? "").Trim();
                if (!string.IsNullOrEmpty(kw))
                    state.Show("\uE721", "No matching records", $"Nothing matched \u201c{kw}\u201d.");
                else if (_currentSubFilter != "All")
                    state.Show("\uE71C", "No records found", $"No entries match \u201c{_currentSubFilter}\u201d.");
                else
                    state.Show("\uE9D9", "No data", "There are no records for the selected period.");
            }
        }

        // ═══════════ GRID STYLE ═══════════

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

        // ═══════════ COLUMN CONFIGURATIONS (unchanged logic) ═══════════

        private void HideUnneededColumns(params string[] keep)
        {
            var keepSet = new HashSet<string>(keep);
            foreach (DataGridViewColumn col in dgv.Columns)
                if (!keepSet.Contains(col.Name))
                    col.Visible = false;
        }

        private void ConfigureSalesColumns()
        {
            HideUnneededColumns("RequestNumber", "Customer", "Service", "PaidOnDisplay", "Method", "Reference", "AmountDisplay");

            SetupColumn("RequestNumber", "TICKET #", 120, 0);
            SetupColumn("Customer", "CUSTOMER", 200, 1);
            SetupColumn("Service", "SERVICE", 0, 2, fill: true);
            SetupColumn("PaidOnDisplay", "PAID", 140, 3);
            SetupColumn("Method", "METHOD", 130, 4);
            SetupColumn("Reference", "REFERENCE", 140, 5);
            SetupColumn("AmountDisplay", "AMOUNT", 140, 6, alignRight: true);
        }

        private void ConfigureRepairColumns()
        {
            HideUnneededColumns("PriorityText", "RequestNumber", "DeviceDisplay", "CustomerDisplay", "IssueDescription", "StatusText", "RequestDate", "CostDisplay");

            SetupColumn("PriorityText", "PRIORITY", 120, 0);
            SetupColumn("RequestNumber", "TICKET #", 140, 1);
            SetupColumn("DeviceDisplay", "DEVICE", 200, 2);
            SetupColumn("CustomerDisplay", "CUSTOMER", 200, 3);
            SetupColumn("IssueDescription", "ISSUE", 0, 4, fill: true);
            SetupColumn("StatusText", "STATUS", 140, 5);
            SetupColumn("RequestDate", "DATE", 130, 6);
            SetupColumn("CostDisplay", "COST", 130, 7, alignRight: true);
        }

        private void ConfigureCustomerColumns()
        {
            HideUnneededColumns("CustomerId", "NameDisplay", "Email", "Phone", "Address", "LoyaltyPoints", "StatusDisplay", "CreatedAt");

            SetupColumn("CustomerId", "ID", 70, 0);
            SetupColumn("NameDisplay", "CUSTOMER", 200, 1);
            SetupColumn("Email", "EMAIL", 200, 2);
            SetupColumn("Phone", "PHONE", 140, 3);
            SetupColumn("Address", "ADDRESS", 0, 4, fill: true);
            SetupColumn("LoyaltyPoints", "POINTS", 100, 5, alignRight: true);
            SetupColumn("StatusDisplay", "STATUS", 130, 6);
            SetupColumn("CreatedAt", "REGISTERED", 140, 7);
        }

        private void ConfigureServiceColumns()
        {
            HideUnneededColumns("Service", "Requests", "RevenueDisplay", "AvgValueDisplay", "LastRequestDisplay");

            SetupColumn("Service", "SERVICE", 0, 0, fill: true);
            SetupColumn("Requests", "REQUESTS", 120, 1, alignRight: true);
            SetupColumn("RevenueDisplay", "REVENUE", 140, 2, alignRight: true);
            SetupColumn("AvgValueDisplay", "AVG VALUE", 140, 3, alignRight: true);
            SetupColumn("LastRequestDisplay", "LAST REQUEST", 150, 4);
        }

        private void ConfigureInteractionColumns()
        {
            HideUnneededColumns("TypeText", "PriorityText", "CustomerDisplay", "Subject", "StatusText", "InteractionDate", "ClosedAtDisplay");

            SetupColumn("TypeText", "TYPE", 130, 0);
            SetupColumn("PriorityText", "PRIORITY", 120, 1);
            SetupColumn("CustomerDisplay", "CUSTOMER", 200, 2);
            SetupColumn("Subject", "SUBJECT", 0, 3, fill: true);
            SetupColumn("StatusText", "STATUS", 140, 4);
            SetupColumn("InteractionDate", "DATE", 130, 5);
            SetupColumn("ClosedAtDisplay", "CLOSED", 130, 6);
        }

        private void ConfigureLoyaltyColumns()
        {
            HideUnneededColumns("CustomerName", "ProgramName", "Points", "TotalSpent", "JoinedDate", "IsActive");

            SetupColumn("CustomerName", "CUSTOMER", 200, 0);
            SetupColumn("ProgramName", "PROGRAM", 180, 1);
            SetupColumn("Points", "POINTS", 110, 2, alignRight: true);
            SetupColumn("TotalSpent", "LIFETIME SPEND", 150, 3, alignRight: true);
            SetupColumn("JoinedDate", "JOINED", 130, 4);
            SetupColumn("IsActive", "STATUS", 130, 5);
        }

        private void ConfigureRetentionColumns()
        {
            HideUnneededColumns("CustomerName", "Category", "Action", "Basis", "Reward", "TotalSpent", "DaysSinceLastTransaction");

            SetupColumn("CustomerName", "CUSTOMER", 200, 0);
            SetupColumn("Category", "SEGMENT", 130, 1);
            SetupColumn("Action", "ACTION", 180, 2);
            SetupColumn("Basis", "REASON", 0, 3, fill: true);
            SetupColumn("Reward", "INCENTIVE", 140, 4);
            SetupColumn("TotalSpent", "PAST SPEND", 140, 5, alignRight: true);
            SetupColumn("DaysSinceLastTransaction", "DAYS INACTIVE", 130, 6, alignRight: true);
        }

        private void SetupColumn(string name, string header, int width, int displayIndex, bool fill = false, bool alignRight = false, string? format = null)
        {
            var col = dgv.Columns[name];
            if (col == null) return;

            col.Visible = true;
            col.HeaderText = header;
            col.DisplayIndex = displayIndex;
            col.SortMode = DataGridViewColumnSortMode.NotSortable;

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

            col.DefaultCellStyle.Alignment = alignRight
                ? DataGridViewContentAlignment.MiddleRight
                : DataGridViewContentAlignment.MiddleLeft;
            col.HeaderCell.Style.Alignment = alignRight
                ? DataGridViewContentAlignment.MiddleRight
                : DataGridViewContentAlignment.MiddleLeft;

            if (format != null)
                col.DefaultCellStyle.Format = format;
        }

        // ═══════════ CELL PAINTING ═══════════

        private void PaintCell(DataGridView g, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < 0) return;
            var gr = e.Graphics;
            var cell = e.CellBounds;

            // Header
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

            string colName = g.Columns[e.ColumnIndex].Name;
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

            bool isPill = colName is "Method" or "StatusText" or "PriorityText"
                       or "StatusDisplay" or "TypeText" or "Category" or "IsActive";
            bool isCurrency = colName is "AmountDisplay" or "CostDisplay"
                           or "RevenueDisplay" or "AvgValueDisplay" or "TotalSpent";
            bool isDate = colName is "PaidOnDisplay" or "LastRequestDisplay" or "RequestDate"
                       or "InteractionDate" or "ClosedAtDisplay" or "CreatedAt" or "JoinedDate";

            if (isPill)
            {
                Color accent = colName switch
                {
                    "Method" => MethodColor(text),
                    "StatusText" or "StatusDisplay" => StatusColor(text),
                    "PriorityText" => PriorityColor(text),
                    "TypeText" => TypeColor(text),
                    "Category" => CategoryColor(text),
                    "IsActive" => IsActiveBool(e.Value) ? AppTheme.Success : UiKit.T.InkMuted,
                    _ => UiKit.T.InkMuted
                };
                string shown = colName == "IsActive" ? (IsActiveBool(e.Value) ? "Active" : "Archived") : text;
                PaintDotPill(gr, rect, cy, string.IsNullOrWhiteSpace(shown) ? "—" : shown, accent, UiKit.Micro);
                e.Handled = true;
                return;
            }

            if (isCurrency)
            {
                UiKit.Text(gr, string.IsNullOrWhiteSpace(text) ? "—" : text,
                    UiKit.T.BodyStrong, UiKit.T.Ink, rect,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | Flat);
                e.Handled = true;
                return;
            }

            if (isDate)
            {
                UiKit.Text(gr, string.IsNullOrWhiteSpace(text) ? "—" : text,
                    MonoFont, UiKit.T.InkMuted, rect, CellText);
                e.Handled = true;
                return;
            }

            e.PaintContent(e.CellBounds);
            e.Handled = true;
        }

        private static bool IsActiveBool(object? v) =>
            v is true || (v is string s && s.Equals("True", StringComparison.OrdinalIgnoreCase));

        private static Color MethodColor(string text)
        {
            var t = (text ?? "").ToLowerInvariant();
            if (t.Contains("cash")) return AppTheme.Success;
            if (t.Contains("gcash") || t.Contains("card")) return AppTheme.Primary;
            if (t.Contains("bank") || t.Contains("transfer")) return Color.FromArgb(139, 92, 246);
            return UiKit.T.InkMuted;
        }

        private static Color StatusColor(string text)
        {
            var t = (text ?? "").ToLowerInvariant();
            if (t.Contains("completed") || t.Contains("closed") || t.Contains("active") || t.Contains("resolved"))
                return AppTheme.Success;
            if (t.Contains("progress") || t.Contains("scheduled") || t.Contains("approved"))
                return AppTheme.Primary;
            if (t.Contains("pending") || t.Contains("open"))
                return AppTheme.Warning;
            if (t.Contains("rejected") || t.Contains("cancelled") || t.Contains("archived"))
                return UiKit.T.InkMuted;
            return UiKit.T.InkMuted;
        }

        private static Color PriorityColor(string text)
        {
            var t = (text ?? "").ToLowerInvariant();
            if (t.Contains("urgent")) return AppTheme.Danger;
            if (t.Contains("high")) return AppTheme.Warning;
            if (t.Contains("medium")) return AppTheme.Primary;
            return UiKit.T.InkMuted;
        }

        private static Color TypeColor(string text)
        {
            var t = (text ?? "").ToLowerInvariant();
            if (t.Contains("inquiry") || t.Contains("question")) return AppTheme.Primary;
            if (t.Contains("complaint") || t.Contains("concern")) return AppTheme.Danger;
            if (t.Contains("review") || t.Contains("feedback")) return AppTheme.Warning;
            if (t.Contains("follow")) return Color.FromArgb(139, 92, 246);
            return UiKit.T.InkMuted;
        }

        private static Color CategoryColor(string text)
        {
            var t = (text ?? "").ToLowerInvariant();
            if (t.Contains("risk")) return AppTheme.Danger;
            if (t.Contains("inactive")) return UiKit.T.InkMuted;
            if (t.Contains("loyal")) return AppTheme.Success;
            if (t.Contains("return")) return AppTheme.Primary;
            return UiKit.T.InkMuted;
        }

        private static void PaintDotPill(Graphics gr, Rectangle rect, int cy, string text, Color fg, Font font)
        {
            int textW = TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
                Flat | TextFormatFlags.SingleLine).Width;
            int pillW = Math.Min(rect.Width, textW + 34);
            var pill = new Rectangle(rect.Left, cy - 12, pillW, 24);

            UiKit.FillRounded(gr, pill, 12, UiKit.Wash(fg));
            UiKit.FillRounded(gr, new Rectangle(pill.Left + 11, cy - 3, 6, 6), 3, fg);
            UiKit.Text(gr, text, font, fg,
                new Rectangle(pill.Left + 23, pill.Top, Math.Max(0, pill.Width - 31), pill.Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | Flat);
        }

        // ═══════════ EXPORT PDF (unchanged) ═══════════

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
                        GeneratedBy = !string.IsNullOrWhiteSpace(UserSession.FullName) ? UserSession.FullName :
                                      (!string.IsNullOrWhiteSpace(UserSession.Username) ? UserSession.Username : "Administrator"),
                        GeneratedAt = DateTime.Now
                    }
                };

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
                            doc.Rows.Add(new[]
                            {
                                s.Service ?? "—",
                                s.Requests.ToString("N0"),
                                $"₱{s.Revenue:N2}",
                                $"₱{s.AvgValue:N2}",
                                s.LastRequestDisplay ?? "—"
                            });
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
                            doc.Rows.Add(new[]
                            {
                                l.CustomerName ?? "—",
                                l.ProgramName ?? "—",
                                l.Points.ToString("N0"),
                                $"₱{l.TotalSpent:N2}",
                                l.JoinedDate.ToString("yyyy-MM-dd"),
                                l.IsActive ? "Active" : "Inactive"
                            });
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
                        doc.SummaryFooterText = $"Actionable Accounts: {_viewRetention.Count:N0} • Recoverable Past Spend: ₱{_viewRetention.Sum(r => r.TotalSpent):N2}";
                        break;
                }

                PdfReportBuilder.GenerateReport(doc, sfd.FileName);
                SaasToast.Show(FindForm(), $"Saved {Path.GetFileName(sfd.FileName)} successfully.", ToastKind.Success);
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(), $"Export failed: {ex.Message}", ToastKind.Danger);
            }
        }

        private static string Val(Dictionary<string, object?> row, string key) =>
            row.TryGetValue(key, out var v) ? (v?.ToString() ?? "") : "";

        // ═══════════════════════════════════════════════════════════════
        //  SUPPORTING CONTROLS
        // ═══════════════════════════════════════════════════════════════

        private sealed class ReportKpiCard : Control
        {
            private const int Pad = 22;
            private const int IconSize = 36;
            private const int ChevronW = 16;
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

            private string _label = "";
            private string _number = "0";
            private string _sub = "";
            private Color _accent = AppTheme.Primary;
            private string _glyph = "";
            private bool _hover;
            private bool _down;

            public event EventHandler? ContentChanged;

            public ReportKpiCard()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
                Cursor = Cursors.Hand;
                TabStop = true;
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
                ContentChanged?.Invoke(this, EventArgs.Empty);
            }

            public string Label => _label;
            public string Number => _number;
            public string Sub => _sub;
            public Color Accent => _accent;

            public int HeightFor(int width) =>
                string.IsNullOrEmpty(_label) ? MinHeight : Math.Max(MinHeight, Measure(width).Total);

            private static int TextH(string text, Font font, int width, bool wrap)
            {
                if (string.IsNullOrEmpty(text)) return font.Height;
                var size = TextRenderer.MeasureText(text, font,
                    new Size(Math.Max(10, width - 4), int.MaxValue), wrap ? Wrapped : One);
                return Math.Max(font.Height, size.Height) + 2;
            }

            private (Font NumFont, bool NumWrap, string Main, int TopH, int NumH, int CapH, int Total) Measure(int width)
            {
                int inner = Math.Max(40, width - Pad * 2);
                int labelW = Math.Max(40, inner - IconSize - 12 - ChevronW);

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
                return (numFont, numWrap, _number, topH, numH, capH, total);
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

        [DesignerCategory("Code")]
        private sealed class ReportDateRangeBar : Panel
        {
            private readonly ComboBox _cboPresets;
            private readonly DateTimePicker _dtpFrom;
            private readonly Label _lblTo;
            private readonly DateTimePicker _dtpTo;
            private bool _suppressPreset;

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
                    Width = 130,
                    FlatStyle = FlatStyle.Flat
                };
                _cboPresets.Items.AddRange(new object[]
                {
                    "Last 30 Days", "This Month", "Last 90 Days", "Year to Date", "All Time", "Custom Range"
                });
                _cboPresets.SelectedIndex = 0;

                _dtpFrom = new DateTimePicker
                {
                    Format = DateTimePickerFormat.Short,
                    Font = UiKit.T.Small,
                    Width = 130,
                    Value = DateTime.Today.AddDays(-30)
                };

                _lblTo = new Label
                {
                    Text = "to",
                    Font = UiKit.T.SmallStrong,
                    ForeColor = UiKit.T.InkMuted,
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    UseMnemonic = false
                };

                _dtpTo = new DateTimePicker
                {
                    Format = DateTimePickerFormat.Short,
                    Font = UiKit.T.Small,
                    Width = 130,
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
                _cboPresets.Location = new Point(0, (Height - _cboPresets.Height) / 2);
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

                var barRect = new Rectangle(0, 0, PreferredWidth, Height);
                UiKit.Card(g, barRect, 9, UiKit.T.Surface, UiKit.T.Line);

                for (int i = 0; i < Items.Length; i++)
                {
                    var r = ItemRect(i);
                    bool sel = i == _selected;

                    if (sel)
                    {
                        UiKit.FillRounded(g, r, 7, AppTheme.Primary);
                        // active dot marker
                        UiKit.FillRounded(g, new Rectangle(r.Left + 8, r.Top + r.Height / 2 - 2, 4, 4), 2, Color.White);
                    }
                    else if (i == _hover)
                    {
                        UiKit.FillRounded(g, r, 7, UiKit.T.RowHover);
                    }

                    Color fg = sel ? Color.White : (i == _hover ? UiKit.T.Ink : UiKit.T.InkMuted);
                    var textRect = sel ? new Rectangle(r.Left + 16, r.Top, r.Width - 20, r.Height) : r;

                    UiKit.Text(g, Items[i].Label, UiKit.T.SmallStrong, fg, textRect,
                        sel
                            ? (TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix)
                            : (TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix));
                }
            }
        }

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
                    if (GetPillRect(i).Contains(pt)) return i;
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

                using (var pen = new Pen(UiKit.T.LineSoft, 1))
                    g.DrawLine(pen, 0, 0, Width, 0);

                var textRect = new Rectangle(UiKit.T.S4, 0, Width - UiKit.T.S4 * 2, Height);
                UiKit.Text(g, _stats, UiKit.T.SmallStrong, UiKit.T.InkMuted, textRect, UiKit.Left);
            }
        }
    }
}