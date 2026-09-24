using CRM.winforms;
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
    /// Business Intelligence Dashboard.
    ///
    ///   1. Key metrics          – 10 clickable KPI tiles (full, wrapped text — nothing is cut off)
    ///   2. Trends & performance – 6 professional charts (headline value, change vs previous,
    ///                             nice axis scale, smooth area lines, ranked horizontal bars)
    ///
    /// UI/UX only: all loading, binding and drill-down logic is unchanged.
    /// </summary>
    [DesignerCategory("Code")]
    public class DashboardControl : UserControl
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private DashboardDto? _data;

        /// <summary>Raised when a tile maps to a whole page (e.g. the retention page).</summary>
        public event EventHandler<string>? ActionRequested;

        private readonly List<KpiTile> _tiles = new();
        private readonly List<ChartCard> _charts = new();
        private readonly List<(Label Title, Label Hint)> _sections = new();
        private readonly ToolTip _tip = new ToolTip { InitialDelay = 400, ReshowDelay = 120, ShowAlways = true };

        // Chart data caches (drill-down by selected point)
        private List<(string Label, double Value)> _salesPoints = new();
        private List<(string Label, double Value)> _transactionsPoints = new();
        private List<(string Label, double Value)> _activityPoints = new();
        private List<(string Label, double Value)> _retentionPoints = new();

        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private Label _rule = null!;
        private FlatButton btnRefresh = null!;
        private Label lblLoading = null!;
        private bool _layingOut;

        // Period filter (applies to the line / trend charts). 0 = all time.
        private Label _periodLabel = null!;
        private SegmentedFilter _periodFilter = null!;
        private int _periodMonths;

        // ═══════════ CONSTRUCTOR ═══════════

        public DashboardControl()
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

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tip.Dispose();
            base.Dispose(disposing);
        }

        // ═══════════ UI BUILD ═══════════

        private void BuildUi()
        {
            lblTitle = new Label
            {
                Text = "Dashboard",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Live business intelligence from your CRM data",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _rule = new Label { AutoSize = false, Height = 1, BackColor = UiKit.T.Line, Text = "" };

            btnRefresh = new FlatButton("Refresh", "\uE72C") { AccessibleName = "Refresh dashboard" };
            btnRefresh.Click += async (s, e) => await ReloadAsync();
            _tip.SetToolTip(btnRefresh, "Reload the latest data");

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(_rule);
            Controls.Add(btnRefresh);

            // ── Section headings ──
            AddSection("Key metrics", "Click a tile to see the records behind the number");
            AddSection("Trends & performance", "Hover to inspect values  ·  click a point or bar to open its records");

            // ── Period filter (sits on the Trends heading, next to what it filters) ──
            _periodLabel = new Label
            {
                Text = "Period",
                Font = UiKit.T.SmallStrong,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _periodFilter = new SegmentedFilter(new[] { "3 months", "6 months", "12 months", "All time" }, 3)
            {
                AccessibleName = "Period filter"
            };
            _periodFilter.SelectionChanged += (s, e) => OnPeriodChanged();
            _tip.SetToolTip(_periodFilter, "Filter the trend (line) charts by period");
            Controls.Add(_periodLabel);
            Controls.Add(_periodFilter);

            // ── KPI tiles (2 rows × 5) ──
            AddTile(() => DrillCustomersAll(), "View all customer records");
            AddTile(() => DrillCustomersActive(), "View active customers");
            AddTile(() => DrillInactive(), "View customers with no completed transaction in 90 days");
            AddTile(() => DrillCustomersReturning(), "View customers with 2+ transactions");
            AddTile(() => DrillTransactionsAll(), "View all transactions");
            AddTile(() => DrillSales(), "View all payments");
            AddTile(() => DrillServices(), "View service popularity and revenue");
            AddTile(() => DrillVisits(), "View visits per customer");
            AddTile(() => DrillLoyaltyMembers(null), "View loyalty program members");
            AddTile(() => RaiseAction("retention"), "Open the retention page");

            // ── Charts ── (all interactive; service popularity matches loyalty performance)
            AddChart("Sales trend", ChartKind.Line, label => DrillSalesMonth(label));
            AddChart("Transaction trend", ChartKind.Line, label => DrillTransactionsMonth(label));
            AddChart("Service popularity", ChartKind.Bar, label => DrillService(label));
            AddChart("Customer activity", ChartKind.Line, label => DrillActivityMonth(label));
            AddChart("Loyalty performance", ChartKind.Bar, label => DrillLoyaltyMembers(label));
            AddChart("Customer retention trend", ChartKind.Line, label => DrillActivityMonth(label));

            foreach (var t in _tiles) Controls.Add(t);
            foreach (var c in _charts) Controls.Add(c);

            lblLoading = new Label
            {
                Text = "Loading latest data…",
                Font = UiKit.T.Body,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Visible = false
            };
            Controls.Add(lblLoading);
            lblLoading.BringToFront();

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
                BackColor = Color.Transparent
            };
            var h = new Label
            {
                Text = hint,
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _sections.Add((t, h));
            Controls.Add(t);
            Controls.Add(h);
        }

        private void AddTile(Action onClick, string tooltip)
        {
            var tile = new KpiTile { Tag = onClick };
            tile.Click += (s, e) => ((Action)tile.Tag!).Invoke();
            tile.ContentChanged += (s, e) => LayoutUi();     // re-measure when text arrives
            _tip.SetToolTip(tile, tooltip);
            _tiles.Add(tile);
        }

        private void AddChart(string title, ChartKind kind, Action<string?> onPointClicked, bool staticBars = false)
        {
            var card = new ChartCard(title, kind) { StaticBars = staticBars };
            card.PointClicked += label =>
            {
                try { onPointClicked(label); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Couldn't open drill-down.\n\n{ex.Message}",
                        "Drill-down", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            _charts.Add(card);
        }

        private void RaiseAction(string key) => ActionRequested?.Invoke(this, key);

        // ═══════════ DRILL-DOWN ACTIONS ═══════════

        private void DrillCustomersAll() =>
            OpenDrillDown("Customers — all records",
                () => _api.GetAnalyticsDetailsAsync("customers"));

        private void DrillCustomersActive() =>
            OpenDrillDown("Active customers", async () =>
            {
                var details = await _api.GetAnalyticsDetailsAsync("customers");
                if (details == null) return null;
                details.Rows = details.Rows
                    .Where(r => r.TryGetValue("status", out var s) && s?.ToString() == "Active")
                    .ToList();
                details.Title = $"Active customers — {details.Rows.Count} records";
                return details;
            });

        private void DrillCustomersReturning() =>
            OpenDrillDown("Returning customers (2+ transactions)", async () =>
            {
                var details = await _api.GetAnalyticsDetailsAsync("customers");
                if (details == null) return null;
                details.Rows = details.Rows
                    .Where(r => r.TryGetValue("transactions", out var t) && ToInt(t) >= 2)
                    .ToList();
                details.Title = $"Returning customers — {details.Rows.Count} records";
                return details;
            });

        private static int ToInt(object? value)
        {
            if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Number)
                return je.TryGetInt32(out var i) ? i : 0;
            try { return Convert.ToInt32(value ?? 0); }
            catch { return 0; }
        }

        private void DrillInactive() =>
            OpenDrillDown("Inactive customers — no completed transaction in 90 days",
                () => _api.GetInactiveCustomersAsync());

        private void DrillTransactionsAll() =>
            OpenDrillDown("Transactions — all records",
                () => _api.GetAnalyticsDetailsAsync("transactions"));

        private void DrillSales() =>
            OpenDrillDown("Sales — all payments",
                () => _api.GetAnalyticsDetailsAsync("sales"));

        private void DrillServices() =>
            OpenDrillDown("Services — popularity and revenue",
                () => _api.GetAnalyticsDetailsAsync("services"));

        private void DrillService(string? service)
        {
            if (string.IsNullOrEmpty(service)) { DrillServices(); return; }
            OpenDrillDown($"Transactions — {service}",
                () => _api.GetAnalyticsDetailsAsync("transactions", service: service));
        }

        private void DrillSalesMonth(string? month)
        {
            if (string.IsNullOrEmpty(month)) { DrillSales(); return; }
            var (from, to) = MonthRange(month);
            OpenDrillDown($"Sales — {month}", () => _api.GetAnalyticsDetailsAsync("sales", from, to));
        }

        private void DrillTransactionsMonth(string? month)
        {
            if (string.IsNullOrEmpty(month)) { DrillTransactionsAll(); return; }
            var (from, to) = MonthRange(month);
            OpenDrillDown($"Transactions — {month}", () => _api.GetAnalyticsDetailsAsync("transactions", from, to));
        }

        private void DrillActivityMonth(string? month)
        {
            if (string.IsNullOrEmpty(month))
            {
                OpenDrillDown("Customer activity — interactions",
                    () => _api.GetAnalyticsDetailsAsync("interactions"));
                return;
            }
            var (from, to) = MonthRange(month);
            OpenDrillDown($"Interactions — {month}", () => _api.GetAnalyticsDetailsAsync("interactions", from, to));
        }

        private void DrillVisits() =>
            OpenDrillDown("Customer visit frequency", async () =>
            {
                var visits = await _api.GetCustomerVisitsAsync();
                return new AnalyticsDetailsDto
                {
                    Metric = "visits",
                    Title = "Visits per customer — from completed transactions",
                    Columns = new List<DetailColumnDto>
                    {
                        new() { Key = "name", Label = "Customer", Type = "text" },
                        new() { Key = "email", Label = "Email", Type = "text" },
                        new() { Key = "visits", Label = "Visits", Type = "number" },
                        new() { Key = "perMonth", Label = "Visits / month", Type = "number" },
                        new() { Key = "first", Label = "First visit", Type = "date" },
                        new() { Key = "last", Label = "Last visit", Type = "date" },
                        new() { Key = "spent", Label = "Total spent", Type = "currency" }
                    },
                    Rows = visits.Select(v => (Dictionary<string, object?>)new Dictionary<string, object?>
                    {
                        ["name"] = v.CustomerName,
                        ["email"] = v.Email,
                        ["visits"] = v.VisitCount,
                        ["perMonth"] = v.VisitsPerMonth,
                        ["first"] = v.FirstVisit,
                        ["last"] = v.LastVisit,
                        ["spent"] = v.TotalSpent
                    }).ToList()
                };
            });

        private void DrillLoyaltyMembers(string? programName) =>
            OpenDrillDown("Loyalty members", async () =>
            {
                var members = await _api.GetLoyaltyMembersAsync();
                if (!string.IsNullOrEmpty(programName))
                    members = members.Where(m => m.ProgramName == programName).ToList();

                return new AnalyticsDetailsDto
                {
                    Metric = "loyalty-members",
                    Title = string.IsNullOrEmpty(programName)
                        ? "All loyalty program members"
                        : $"Loyalty members — {programName}",
                    Columns = new List<DetailColumnDto>
                    {
                        new() { Key = "name", Label = "Customer", Type = "text" },
                        new() { Key = "program", Label = "Program", Type = "text" },
                        new() { Key = "points", Label = "Points", Type = "number" },
                        new() { Key = "joined", Label = "Joined", Type = "date" },
                        new() { Key = "spent", Label = "Total spent", Type = "currency" },
                        new() { Key = "active", Label = "Active", Type = "text" }
                    },
                    Rows = members.Select(m => (Dictionary<string, object?>)new Dictionary<string, object?>
                    {
                        ["name"] = m.CustomerName,
                        ["program"] = m.ProgramName,
                        ["points"] = m.Points,
                        ["joined"] = m.JoinedDate,
                        ["spent"] = m.TotalSpent,
                        ["active"] = m.IsActive ? "Yes" : "No"
                    }).ToList()
                };
            });

        private static (DateTime from, DateTime to) MonthRange(string label)
        {
            // Chart labels are "MMM yyyy" (e.g. "Sep 2026").
            if (DateTime.TryParseExact(label + " 1", "MMM yyyy d",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var m))
            {
                var start = new DateTime(m.Year, m.Month, 1);
                return (start, start.AddMonths(1).AddSeconds(-1));
            }
            var now = DateTime.UtcNow;
            var s = new DateTime(now.Year, now.Month, 1);
            return (s, s.AddMonths(1).AddSeconds(-1));
        }

        private void OpenDrillDown(string title, Func<Task<AnalyticsDetailsDto?>> loader)
        {
            using var dlg = new DrillDownDialog(title, loader);
            dlg.ShowModal(this.FindForm());
        }

        // ═══════════ LAYOUT (responsive + scrollable) ═══════════

        private void LayoutUi()
        {
            if (_layingOut) return;
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            _layingOut = true;
            try
            {
                // A scrollbar appearing changes ClientSize.Width, so settle in up to 3 passes.
                for (int pass = 0; pass < 3; pass++)
                {
                    int w0 = ClientSize.Width;
                    LayoutCore();
                    if (ClientSize.Width == w0) break;
                }
            }
            finally { _layingOut = false; }
        }

        private void LayoutCore()
        {
            int W = ClientSize.Width;
            int H = ClientSize.Height;
            const int gap = 14;

            var plan = new List<(Control C, int X, int Y, int W, int H)>();
            void P(Control c, int px, int py, int pw = 0, int ph = 0) => plan.Add((c, px, py, pw, ph));

            int Section(int idx, int top)
            {
                var (t, h) = _sections[idx];
                int th = t.PreferredHeight;
                P(t, 0, top);
                P(h, t.PreferredWidth + 12, top + Math.Max(0, th - h.PreferredHeight - 1));
                return top + th + 12;
            }

            // ── Header ──
            int subtitleY = lblTitle.PreferredHeight + UiKit.T.S1;
            int dividerY = subtitleY + lblSubtitle.PreferredHeight + UiKit.T.S4;

            P(lblTitle, 0, 0);
            P(lblSubtitle, 1, subtitleY);

            btnRefresh.Size = new Size(btnRefresh.PreferredWidth, UiKit.T.ButtonHeight);
            P(btnRefresh, W - btnRefresh.Width, UiKit.T.S1);
            P(_rule, 0, dividerY, W, 1);

            int y = dividerY + UiKit.T.S5;

            // ── 1. Key metrics ──
            y = Section(0, y);

            int cols = W >= 1100 ? 5 : W >= 820 ? 4 : W >= 560 ? 3 : 2;
            int tileW = (W - gap * (cols - 1)) / cols;
            int tileH = _tiles.Max(t => t.HeightFor(tileW));   // uniform height, fits the longest text

            for (int i = 0; i < _tiles.Count; i++)
            {
                int row = i / cols, col = i % cols;
                P(_tiles[i], col * (tileW + gap), y + row * (tileH + gap), tileW, tileH);
            }
            int tileRows = (_tiles.Count + cols - 1) / cols;
            y += tileRows * (tileH + gap) + 14;

            // ── 2. Trends & performance ──
            int secTop = y;
            y = Section(1, y);

            {
                var (t2, h2) = _sections[1];
                int segH = _periodFilter.PreferredHeight;
                int segW = _periodFilter.PreferredWidth;
                int filterW = _periodLabel.PreferredWidth + 10 + segW;
                int hintRight = t2.PreferredWidth + 12 + h2.PreferredWidth;

                if (W - hintRight >= filterW + 24)
                {
                    // Same row as the heading, right-aligned
                    int fy = secTop + (t2.PreferredHeight - segH) / 2;
                    P(_periodFilter, W - segW, fy, segW, segH);
                    P(_periodLabel, W - filterW, fy + (segH - _periodLabel.PreferredHeight) / 2);
                }
                else
                {
                    // Narrow window: its own row under the heading
                    P(_periodLabel, 0, y + (segH - _periodLabel.PreferredHeight) / 2);
                    P(_periodFilter, _periodLabel.PreferredWidth + 10, y, segW, segH);
                    y += segH + 12;
                }
            }

            int chartCols = W >= 940 ? 2 : 1;
            int chartRows = (_charts.Count + chartCols - 1) / chartCols;
            int available = H - y;
            int chartH = Math.Min(400, Math.Max(290, (available - gap * (chartRows - 1)) / chartRows));
            int chartW = (W - gap * (chartCols - 1)) / chartCols;

            for (int i = 0; i < _charts.Count; i++)
            {
                int row = i / chartCols, col = i % chartCols;
                P(_charts[i], col * (chartW + gap), y + row * (chartH + gap), chartW, chartH);
            }

            int total = y + chartRows * chartH + (chartRows - 1) * gap + 20;

            var min = new Size(0, total);
            if (AutoScrollMinSize != min) AutoScrollMinSize = min;

            var o = AutoScrollPosition;
            foreach (var p in plan)
            {
                if (p.W > 0) p.C.Bounds = new Rectangle(p.X + o.X, p.Y + o.Y, p.W, p.H);
                else p.C.Location = new Point(p.X + o.X, p.Y + o.Y);
            }

            lblLoading.Location = new Point((W - lblLoading.PreferredWidth) / 2, H / 2);
        }

        // ═══════════ DATA ═══════════

        public async Task ReloadAsync()
        {
            try
            {
                lblLoading.Visible = true;

                _data = await _api.GetDashboardAsync();

                if (_data == null)
                {
                    MessageBox.Show("Dashboard returned no data.",
                        "Empty response", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                BindTiles();
                BindCharts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Couldn't load dashboard.\n\n{ex.Message}",
                    "Connection problem",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                lblLoading.Visible = false;
            }
        }

        private void BindTiles()
        {
            var d = _data!;

            _tiles[0].Set("Total customers", d.TotalCustomers.ToString("N0"),
                $"{d.ActiveCustomers} active", AppTheme.Primary, "\uE716");
            _tiles[1].Set("Active customers", d.ActiveCustomers.ToString("N0"),
                $"{d.NewThisMonth} new this month", AppTheme.Success, "\uE7EE");
            _tiles[2].Set("Inactive customers", d.InactiveCustomers.ToString("N0"),
                $"{d.Inactive90Days} idle 90+ days", AppTheme.Danger, "\uE712");
            _tiles[3].Set("Returning customers", d.ReturningCustomers.ToString("N0"),
                $"{d.RepeatCustomerRate:0.#}% repeat rate", AppTheme.Warning, "\uE73E");
            _tiles[4].Set("Total transactions", d.TotalTransactions.ToString("N0"),
                $"{d.TransactionsThisMonth} this month", AppTheme.Primary, "\uE9D5");
            _tiles[5].Set("Total sales", $"\u20b1{d.TotalSales:N0}",
                $"avg \u20b1{d.AverageTransactionValue:N0} / transaction", AppTheme.Success, "\uE8C7");
            _tiles[6].Set("Top service",
                d.TopService.Count > 0 ? d.TopService.Name : "\u2014",
                d.TopService.Count > 0
                    ? $"{d.TopService.Count} requests \u00b7 \u20b1{d.TopService.Revenue:N0}"
                    : "No data yet",
                AppTheme.Warning, "\uE945");
            _tiles[7].Set("Visit frequency", $"{d.AverageVisitsPerCustomer:0.#}",
                "avg visits per buying customer", AppTheme.Primary, "\uE823");
            _tiles[8].Set("Loyalty members", d.LoyaltyMembers.ToString("N0"),
                $"{d.LoyaltyParticipationRate:0.#}% participation", AppTheme.Primary, "\uE8C7");
            _tiles[9].Set("Retention rate", $"{d.RetentionRate:0.#}%",
                $"{d.ChurnRisk} at churn risk", AppTheme.Success, "\uE73E");
        }

        private void BindCharts()
        {
            var d = _data!;

            _salesPoints = d.SalesOverTime.Select(p => (p.Label, (double)p.Sales)).ToList();
            _transactionsPoints = d.TransactionsOverTime.Select(p => (p.Label, (double)p.Count)).ToList();
            _activityPoints = d.CustomerActivityTrend
                .Select(p => (p.Label, (double)(p.Repairs + p.Interactions))).ToList();
            _retentionPoints = d.RetentionTrend.Select(p => (p.Label, (double)p.Active)).ToList();

            _charts[0].SetLineData(InPeriod(_salesPoints), AppTheme.Success, currency: true, filtered: _periodMonths > 0);
            _charts[1].SetLineData(InPeriod(_transactionsPoints), AppTheme.Primary, filtered: _periodMonths > 0);
            _charts[2].SetBarData(d.PopularServices
                .Select((p, i) => (p.Service, (double)p.Count, Palette(i))).ToList(), clickable: true);
            _charts[3].SetLineData(InPeriod(_activityPoints), AppTheme.Primary, filtered: _periodMonths > 0);
            _charts[4].SetBarData(d.LoyaltyPerformance
                .Select((p, i) => (p.ProgramName, (double)p.Members, Palette(i + 2))).ToList(), clickable: true);
            _charts[5].SetLineData(InPeriod(_retentionPoints), AppTheme.Success, filtered: _periodMonths > 0);
        }

        // ═══════════ PERIOD FILTER ═══════════

        private void OnPeriodChanged()
        {
            _periodMonths = _periodFilter.SelectedIndex switch { 0 => 3, 1 => 6, 2 => 12, _ => 0 };
            if (_data != null) BindCharts();          // re-draw from the data already loaded
        }

        /// <summary>Keeps only the months inside the selected period ("MMM yyyy" labels).</summary>
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

        private static Color Palette(int i) => i switch
        {
            0 => AppTheme.Primary,
            1 => AppTheme.Success,
            2 => AppTheme.Warning,
            3 => AppTheme.Danger,
            4 => Color.FromArgb(0x7C, 0x5C, 0xFC),
            _ => Color.FromArgb(0x0E, 0x9F, 0x9F)
        };

        // ═══════════════════════════════════════════════════════════════
        //  KPI TILE — clean card, generous margins, full text (auto height)
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class KpiTile : Control
        {
            private const int Pad = 22;          // same margin on every side
            private const int IconSize = 34;
            private const int MinHeight = 128;

            private const TextFormatFlags One =
                TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            private const TextFormatFlags Wrapped = One | TextFormatFlags.WordBreak;

            // The number uses the largest font that fits the tile width.
            private static readonly Font[] NumFonts =
            {
                new("Segoe UI Semibold", 26F),
                new("Segoe UI Semibold", 22F),
                new("Segoe UI Semibold", 18F),
                new("Segoe UI Semibold", 15F),
                new("Segoe UI Semibold", 12F)
            };

            private string _label = "";
            private string _number = "0";
            private string _sub = "";
            private Color _accent = AppTheme.Primary;
            private string _glyph = "";
            private bool _hover;
            private bool _down;

            /// <summary>Raised after Set() so the dashboard can re-measure tile height.</summary>
            public event EventHandler? ContentChanged;

            public KpiTile()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
                TabStop = true;
                Cursor = Cursors.Hand;
                AccessibleRole = AccessibleRole.PushButton;
                AccessibleName = "Loading metric";
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

            /// <summary>Height needed so that every line of text is fully visible at this width.</summary>
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

                using (var bg = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(bg, ClientRectangle);

                bool loading = string.IsNullOrEmpty(_label);
                Color accent = loading ? UiKit.T.InkFaint : _accent;

                // Plain white card. Hover = accent border, pressed = soft tint.
                UiKit.Card(g, ClientRectangle, UiKit.T.Radius,
                    _down && !loading ? UiKit.Wash(accent) : UiKit.T.Surface,
                    _hover && !loading ? accent : UiKit.T.Line);

                // Skeleton placeholder until the first data arrives.
                if (loading)
                {
                    UiKit.FillRounded(g, new Rectangle(Pad, Pad, IconSize, IconSize), 9, UiKit.T.Line);
                    UiKit.FillRounded(g, new Rectangle(Pad + IconSize + 12, Pad + 11, Math.Max(20, Width / 3), 12), 4, UiKit.T.Line);
                    UiKit.FillRounded(g, new Rectangle(Pad, Pad + 52, Math.Max(20, Width / 2), 24), 4, UiKit.T.Line);
                    UiKit.FillRounded(g, new Rectangle(Pad, Pad + 88, Math.Max(20, Width * 2 / 3), 10), 4, UiKit.T.Line);
                    return;
                }

                var m = Measure(Width);
                int inner = Width - Pad * 2;

                // Icon (soft tint, no bars or stripes)
                var iconRect = new Rectangle(Pad, Pad + (m.TopH - IconSize) / 2, IconSize, IconSize);
                UiKit.FillRounded(g, iconRect, 9, UiKit.Wash(accent));
                using (var f = UiKit.GlyphFont(12F))
                    UiKit.Text(g, _glyph, f, accent, iconRect, UiKit.Center);

                // Label — beside the icon, wraps instead of being cut
                UiKit.Text(g, _label, UiKit.T.SmallStrong, UiKit.T.InkMuted,
                    new Rectangle(Pad + IconSize + 12, Pad, inner - IconSize - 12, m.TopH),
                    Wrapped | TextFormatFlags.VerticalCenter);

                // Number
                int numTop = Pad + m.TopH + 14;
                UiKit.Text(g, _number, m.NumFont, UiKit.T.Ink,
                    new Rectangle(Pad, numTop, inner, m.NumH),
                    (m.NumWrap ? Wrapped : One) | TextFormatFlags.Top);

                // Caption — full text, wraps
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
        //  CHART CARD — headline metric, nice axis, smooth area line,
        //  ranked horizontal bars, crosshair + rich tooltip
        // ═══════════════════════════════════════════════════════════════

        private enum ChartKind { Line, Bar }

        [DesignerCategory("Code")]
        private sealed class ChartCard : Control
        {
            private static readonly Font HeadlineFont = new("Segoe UI Semibold", 19F);

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

            /// <summary>Optional: when true, the bar chart becomes non-interactive (default off).</summary>
            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public bool StaticBars { get; set; }

            /// <summary>Raised with the label of the clicked data point.</summary>
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
                _clickableBars = clickable && !StaticBars;
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

                // Optional static mode — never highlight or show a hand cursor.
                if (StaticBars)
                {
                    if (_hoverIndex != -1) { _hoverIndex = -1; Invalidate(); }
                    Cursor = Cursors.Default;
                    return;
                }

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

                // Optional static mode — never drill down.
                if (StaticBars) return;

                var hit = _hits.FirstOrDefault(h => h.Hit.Contains(e.Location));
                if (hit.Hit != Rectangle.Empty)
                {
                    _selectedLabel = hit.Label;
                    PointClicked?.Invoke(hit.Label);
                    return;
                }

                // Click elsewhere on the card → drill into the selected / latest point.
                var label = _selectedLabel
                    ?? (_kind == ChartKind.Line
                        ? (_lineData.Count > 0 ? _lineData[^1].Label : null)
                        : (_barData.Count > 0 ? _barData[0].Label : null));
                if (!string.IsNullOrEmpty(label))
                    PointClicked?.Invoke(label!);
            }

            // ───────────────────────── paint ─────────────────────────

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var bg = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(bg, ClientRectangle);

                UiKit.Card(g, ClientRectangle, UiKit.T.Radius, UiKit.T.Surface,
                    _cardHover ? UiKit.T.InkFaint : UiKit.T.Line);

                int pad = UiKit.T.S5;                     // ← more outer margin
                bool hasData = _kind == ChartKind.Line ? _lineData.Count > 0 : _barData.Count > 0;

                // ── Title row (own 28px band) ──
                int titleH = 28;
                UiKit.Text(g, _title, UiKit.T.Section, UiKit.T.Ink,
                    new Rectangle(pad, pad, Math.Max(10, Width - pad * 2 - 140), titleH),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                if (hasData && !StaticBars)
                {
                    string hint = _kind == ChartKind.Line ? "Click a point" : "Click a row";
                    int hintW = UiKit.Measure(hint, UiKit.Micro).Width + 6;
                    var hintRect = new Rectangle(Width - pad - hintW, pad + 2, hintW, titleH - 4);
                    UiKit.Text(g, hint, UiKit.Micro, UiKit.T.InkMuted, hintRect,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                    UiKit.Text(g, "\uE72A", UiKit.T.Glyph, UiKit.T.InkMuted,
                        new Rectangle(hintRect.Left - 16, pad + 8, 14, 14), UiKit.Center);
                }

                // ── Headline band (own 40px row, extra gap below title) ──
                int headlineH = 40;
                int headlineTop = pad + titleH + 8;
                if (hasData)
                    DrawHeadline(g, pad, headlineTop, headlineH);

                // ── Plot area (starts well below headline, keeps bottom margin) ──
                int plotTop = headlineTop + (hasData ? headlineH + 8 : 0);
                int plotBottom = Height - pad - 6;
                var plot = new Rectangle(pad + 2, plotTop, Width - (pad + 2) * 2, plotBottom - plotTop);

                _hits.Clear();
                if (plot.Height < 60 || plot.Width < 90) return;

                if (_kind == ChartKind.Line) DrawLine(g, plot);
                else DrawBar(g, plot);

                DrawTooltip(g);
            }

            private string FormatValue(double v) => _currency
                ? $"\u20b1{v:N0}"
                : v % 1 == 0 ? $"{v:N0}" : $"{v:0.##}";

            /// <summary>Compact axis label: 1.2K, 3.4M …</summary>
            private string AxisText(double v)
            {
                string n = v >= 1_000_000 ? $"{v / 1_000_000:0.#}M"
                         : v >= 1_000 ? $"{v / 1_000:0.#}K"
                         : $"{v:0.#}";
                return _currency ? "\u20b1" + n : n;
            }

            /// <summary>Round-number axis step (1, 2, 5, 10 × 10ⁿ) so gridlines read cleanly.</summary>
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

            // ─────────────── headline (big number + change vs previous) ───────────────

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

                    // Right-aligned summary stats (only if there's room)
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
                        string t = $"{(up ? "\u25B2" : "\u25BC")} {Math.Abs(pct):0.#}%";
                        int tw = UiKit.Measure(t, UiKit.T.SmallStrong).Width;
                        var pill = new Rectangle(x, y + (h - 22) / 2, tw + 16, 22);
                        UiKit.FillRounded(g, pill, 11, UiKit.Wash(c));
                        UiKit.Text(g, t, UiKit.T.SmallStrong, c, pill, UiKit.Center);
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
                    UiKit.Text(g, $"total  ·  top: {top.Label}", UiKit.T.Small, UiKit.T.InkMuted,
                        new Rectangle(x, y, Math.Max(10, Width - pad - x), h),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }

            // ─────────────── line / area chart ───────────────

            private void DrawLine(Graphics g, Rectangle plot)
            {
                if (_lineData.Count == 0) { DrawEmpty(g, plot); return; }

                int n = _lineData.Count;
                int labelH = 26;                               // ← taller X-label band
                int chartH = plot.Height - labelH - 4;
                if (chartH < 40) return;

                double dataMax = Math.Max(1, _lineData.Max(p => p.Value));
                bool allInt = _lineData.All(p => p.Value % 1 == 0);
                const int ticks = 4;
                double step = NiceStep(dataMax, ticks, allInt);
                double axisMax = step * ticks;

                int axisW = Math.Max(40, UiKit.Measure(AxisText(axisMax), UiKit.Micro).Width + 16);
                float topY = plot.Top + 14;                    // ← more headroom
                float baseY = plot.Top + chartH - 12;          // ← lift baseline off the axis
                float span = baseY - topY;
                float x0 = plot.Left + axisW + 14;             // ← wider left gutter
                float xw = Math.Max(1, plot.Right - 16 - x0);
                var clipRect = new Rectangle((int)(plot.Left + axisW + 6), plot.Top,
                                             (int)(plot.Width - axisW - 22), chartH);

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
                        new Rectangle(plot.Left, (int)gy - 8, axisW - 10, 16),
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

                // Hover column (wide, forgiving target) + crosshair
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

                // Smooth area + line (clipped to the plot so curve overshoot never leaks)
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
                            Color.FromArgb(90, _lineColor), Color.FromArgb(0, _lineColor), LinearGradientMode.Vertical);
                        g.FillPath(brush, path);
                    }

                    using var pen = new Pen(_lineColor, 2.5f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
                    if (n >= 3) g.DrawCurve(pen, points, 0.35f); else g.DrawLines(pen, points);
                }
                g.ResetClip();

                // Markers: hollow dots (few points) — always for hovered / selected / latest
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

                // X labels: as many as fit, always including the latest period
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

            // ─────────────── ranked horizontal bars ───────────────

            private void DrawBar(Graphics g, Rectangle plot)
            {
                if (_barData.Count == 0) { DrawEmpty(g, plot); return; }

                int n = _barData.Count;
                double max = Math.Max(1, _barData.Max(b => b.Value));
                double sum = _barData.Sum(b => b.Value);

                int rowH = Math.Max(24, Math.Min(46, plot.Height / n));
                int shown = Math.Min(n, Math.Max(1, plot.Height / rowH));

                // Label column sized to the longest label (full names, capped at 40% of the width)
                int labelW = 0;
                for (int i = 0; i < shown; i++)
                    labelW = Math.Max(labelW, UiKit.Measure(_barData[i].Label, UiKit.T.Small).Width);
                labelW = Math.Min(labelW + 18, (int)(plot.Width * 0.40));

                string sample = FormatValue(max) + (sum > 0 ? "  ·  100%" : "");
                int valueW = UiKit.Measure(sample, UiKit.T.SmallStrong).Width + 14;

                int trackL = plot.Left + labelW + 14;
                int trackR = plot.Right - valueW - 12;
                int trackW = Math.Max(20, trackR - trackL);
                int barH = Math.Max(8, Math.Min(14, rowH - 12));

                for (int i = 0; i < shown; i++)
                {
                    var (label, value, color) = _barData[i];
                    bool hot = !StaticBars && (i == _hoverIndex || label == _selectedLabel);

                    var row = new Rectangle(plot.Left, plot.Top + i * rowH, plot.Width, rowH);
                    int cy = row.Top + rowH / 2;

                    if (hot)
                        UiKit.FillRounded(g, row, 8, Color.FromArgb(18, color));

                    // Category label (left)
                    UiKit.Text(g, label, UiKit.T.Small, UiKit.T.Ink,
                        new Rectangle(plot.Left + 8, row.Top, labelW - 10, rowH),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    // Track + fill
                    var track = new Rectangle(trackL, cy - barH / 2, trackW, barH);
                    UiKit.FillRounded(g, track, barH / 2, UiKit.T.Line);

                    int fillW = value <= 0 ? 0 : Math.Max(barH, (int)(value / max * trackW));
                    var fill = new Rectangle(trackL, track.Top, fillW, barH);
                    if (fillW > 0)
                    {
                        UiKit.FillRounded(g, fill, barH / 2, hot ? color : Color.FromArgb(215, color));
                        if (!StaticBars && label == _selectedLabel)
                            UiKit.StrokeRounded(g, fill, barH / 2, color, 1.6f);
                    }

                    // Value + share of total (right)
                    string pct = sum > 0 ? $"  ·  {value / sum * 100:0}%" : "";
                    string valueText = FormatValue(value) + pct;
                    UiKit.Text(g, valueText, UiKit.T.SmallStrong, hot ? color : UiKit.T.Ink,
                        new Rectangle(plot.Right - valueW, row.Top, valueW, rowH),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                    if (_clickableBars && !StaticBars)
                        _hits.Add((row, label, valueText, new Point(fill.Right, cy)));
                }

                if (shown < n && plot.Height - shown * rowH >= 16)
                    UiKit.Text(g, $"+ {n - shown} more", UiKit.T.Small, UiKit.T.InkMuted,
                        new Rectangle(plot.Left, plot.Top + shown * rowH, plot.Width, 18),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }

            // ─────────────── tooltip (label + value, clamped) ───────────────

            private void DrawTooltip(Graphics g)
            {
                if (StaticBars) return;
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

        // ═══════════════════════════════════════════════════════════════
        //  SEGMENTED FILTER — one-click period switch, keyboard accessible
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class SegmentedFilter : Control
        {
            private const int Inset = 4;
            private readonly string[] _items;
            private int _selected;
            private int _hover = -1;

            public event EventHandler? SelectionChanged;

            public SegmentedFilter(string[] items, int selected)
            {
                _items = items;
                _selected = selected;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
                TabStop = true;
                Cursor = Cursors.Hand;
                AccessibleRole = AccessibleRole.Grouping;
            }

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public int SelectedIndex
            {
                get => _selected;
                set
                {
                    if (value < 0 || value >= _items.Length || value == _selected) return;
                    _selected = value;
                    Invalidate();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            public int PreferredHeight => 36;

            public int PreferredWidth
            {
                get
                {
                    int w = Inset * 2;
                    for (int i = 0; i < _items.Length; i++) w += ItemWidth(i);
                    return w;
                }
            }

            private int ItemWidth(int i) => UiKit.Measure(_items[i], UiKit.T.SmallStrong).Width + 30;

            private Rectangle ItemRect(int i)
            {
                int x = Inset;
                for (int k = 0; k < i; k++) x += ItemWidth(k);
                return new Rectangle(x, Inset, ItemWidth(i), Height - Inset * 2);
            }

            private int HitTest(Point p)
            {
                for (int i = 0; i < _items.Length; i++)
                    if (ItemRect(i).Contains(p)) return i;
                return -1;
            }

            protected override bool IsInputKey(Keys keyData) =>
                keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Left) SelectedIndex = Math.Max(0, _selected - 1);
                else if (e.KeyCode == Keys.Right) SelectedIndex = Math.Min(_items.Length - 1, _selected + 1);
                base.OnKeyDown(e);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int idx = HitTest(e.Location);
                if (idx != _hover) { _hover = idx; Invalidate(); }
            }

            protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                Focus();
                int idx = HitTest(e.Location);
                if (idx >= 0) SelectedIndex = idx;
                base.OnMouseDown(e);
            }

            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var bg = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(bg, ClientRectangle);

                UiKit.Card(g, ClientRectangle, UiKit.T.Radius, UiKit.T.Surface, UiKit.T.Line);

                for (int i = 0; i < _items.Length; i++)
                {
                    var r = ItemRect(i);
                    bool sel = i == _selected;

                    if (sel) UiKit.FillRounded(g, r, 8, AppTheme.Primary);
                    else if (i == _hover) UiKit.FillRounded(g, r, 8, UiKit.Wash(AppTheme.Primary));

                    UiKit.Text(g, _items[i], UiKit.T.SmallStrong,
                        sel ? Color.White : UiKit.T.InkMuted, r, UiKit.Center);
                }

                if (Focused)
                {
                    var ring = ClientRectangle;
                    ring.Inflate(-1, -1);
                    UiKit.StrokeRounded(g, ring, UiKit.T.Radius, AppTheme.Primary, 2f);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  FLAT BUTTON
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class FlatButton : Control
        {
            private static readonly Font GlyphFont10 = new("Segoe MDL2 Assets", 10F);

            private readonly string _glyph;
            private bool _hover, _down;

            public FlatButton(string text, string glyph)
            {
                _glyph = glyph;
                Text = text;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
                Cursor = Cursors.Hand;
                Font = UiKit.T.BodyStrong;
                TabStop = true;
                AccessibleRole = AccessibleRole.PushButton;
            }

            public int PreferredWidth => UiKit.Measure(Text, UiKit.T.BodyStrong).Width + 56;

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
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

                using (var b = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(b, ClientRectangle);

                Color bg = _down ? AppTheme.PrimaryActive
                         : _hover ? AppTheme.PrimaryHover
                         : AppTheme.Primary;

                UiKit.FillRounded(g, ClientRectangle, 8, bg);

                UiKit.Text(g, _glyph, GlyphFont10, Color.White,
                    new Rectangle(16, 0, 18, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                UiKit.Text(g, Text, UiKit.T.BodyStrong, Color.White,
                    new Rectangle(36, 0, Width - 46, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                if (Focused)
                {
                    var ring = ClientRectangle;
                    ring.Inflate(-3, -3);
                    UiKit.StrokeRounded(g, ring, 6, Color.White, 1.5f);
                }
            }
        }
    }
}