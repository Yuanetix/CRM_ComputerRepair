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
    /// • 10 clickable KPI tiles — each opens a drill-down dialog with the actual
    ///   customer / transaction records behind the number.
    /// • 6 interactive charts — clicking a data point opens the matching records.
    /// • Every figure comes from the live database via /analytics/dashboard.
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

        // Chart data caches (drill-down by selected point)
        private List<(string Label, double Value)> _salesPoints = new();
        private List<(string Label, double Value)> _transactionsPoints = new();
        private List<(string Label, double Value)> _activityPoints = new();
        private List<(string Label, double Value)> _retentionPoints = new();

        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private FlatButton btnRefresh = null!;
        private Label lblLoading = null!;

        // ═══════════ CONSTRUCTOR ═══════════

        public DashboardControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            AutoScaleMode = AutoScaleMode.Font;

            BuildUi();

            this.Load += async (s, e) => await ReloadAsync();
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
                Text = "Business intelligence from your live CRM data  ·  click any tile or chart point to see the records behind it",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnRefresh = new FlatButton("Refresh", "\uE72C");
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnRefresh);

            // ── KPI tiles (2 rows × 5) ──
            AddTile(() => DrillCustomersAll());
            AddTile(() => DrillCustomersActive());
            AddTile(() => DrillInactive());
            AddTile(() => DrillCustomersReturning());
            AddTile(() => DrillTransactionsAll());
            AddTile(() => DrillSales());
            AddTile(() => DrillServices());
            AddTile(() => DrillVisits());
            AddTile(() => DrillLoyaltyMembers(null));
            AddTile(() => RaiseAction("retention"));

            // ── Charts ──
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
                Text = "Loading…",
                Font = UiKit.T.Body,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Visible = false
            };
            Controls.Add(lblLoading);

            Resize += (s, e) => LayoutUi();
        }

        private void AddTile(Action onClick)
        {
            var tile = new KpiTile { Tag = onClick };
            tile.Click += (s, e) => ((Action)tile.Tag!).Invoke();
            _tiles.Add(tile);
        }

        private void AddChart(string title, ChartKind kind, Action<string?> onPointClicked)
        {
            var card = new ChartCard(title, kind);
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

        // ═══════════ HAIRLINE UNDER HEADER ═══════════

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            UiKit.Quality(e.Graphics);

            int y = lblSubtitle.Bottom + UiKit.T.S4;
            using var pen = new Pen(UiKit.T.Line, 1);
            e.Graphics.DrawLine(pen, 0, y, Width, y);
        }

        // ═══════════ LAYOUT ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            lblTitle.Location = new Point(0, 0);

            int subtitleY = lblTitle.PreferredHeight + UiKit.T.S1;
            lblSubtitle.Location = new Point(1, subtitleY);

            btnRefresh.Size = new Size(btnRefresh.PreferredWidth, UiKit.T.ButtonHeight);
            btnRefresh.Location = new Point(Width - btnRefresh.Width, UiKit.T.S1);

            int dividerY = subtitleY + lblSubtitle.PreferredHeight + UiKit.T.S4;
            int contentTop = dividerY + UiKit.T.S5;

            // KPI tiles — 2 rows × 5 columns
            int gap = 12;
            int tileHeight = 104;
            int tileWidth = (Width - gap * 4) / 5;

            for (int i = 0; i < _tiles.Count; i++)
            {
                int row = i / 5;
                int col = i % 5;
                _tiles[i].Location = new Point(col * (tileWidth + gap),
                    contentTop + row * (tileHeight + gap));
                _tiles[i].Size = new Size(tileWidth, tileHeight);
            }

            int tileRows = (_tiles.Count + 4) / 5;
            int chartsTop = contentTop + tileRows * (tileHeight + gap);

            // Charts in a 2-column × 3-row grid
            int rows = (_charts.Count + 1) / 2;
            int chartH = Math.Max(160, (Height - chartsTop - gap * (rows - 1)) / rows);
            int chartW = (Width - gap) / 2;

            for (int i = 0; i < _charts.Count; i++)
            {
                int row = i / 2;
                int col = i % 2;
                _charts[i].Location = new Point(col * (chartW + gap), chartsTop + row * (chartH + gap));
                _charts[i].Size = new Size(chartW, chartH);
            }

            lblLoading.Location = new Point((Width - lblLoading.PreferredWidth) / 2, Height / 2);
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

            _charts[0].SetLineData(_salesPoints, AppTheme.Success, currency: true);
            _charts[1].SetLineData(_transactionsPoints, AppTheme.Primary);
            _charts[2].SetBarData(d.PopularServices
                .Select((p, i) => (p.Service, (double)p.Count, Palette(i))).ToList(), clickable: true);
            _charts[3].SetLineData(_activityPoints, AppTheme.Primary);
            _charts[4].SetBarData(d.LoyaltyPerformance
                .Select((p, i) => (p.ProgramName, (double)p.Members, Palette(i + 2))).ToList(), clickable: true);
            _charts[5].SetLineData(_retentionPoints, AppTheme.Success);
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
        //  KPI TILE — clickable
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class KpiTile : Control
        {
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
                Invalidate();
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

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

                var body = ClientRectangle;
                body.Width -= 1;
                body.Height -= 1;

                if (_hover && !_down)
                {
                    using var wash = new SolidBrush(UiKit.Wash(_accent));
                    using var path = UiKit.Rounded(body, UiKit.T.Radius);
                    g.FillPath(wash, path);
                }

                UiKit.Card(g, ClientRectangle, UiKit.T.Radius, UiKit.T.Surface, UiKit.T.Line);

                int pad = UiKit.T.S3;

                var iconRect = new Rectangle(pad, pad, 30, 30);
                UiKit.FillRounded(g, iconRect, 8, UiKit.Wash(_accent));

                using (var f = UiKit.GlyphFont(12F))
                    UiKit.Text(g, _glyph, f, _accent, iconRect, UiKit.Center);

                UiKit.Text(g, _label, UiKit.T.Small, UiKit.T.InkMuted,
                    new Rectangle(pad + 38, pad + 2, Width - pad * 2 - 38, 30),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                var numFont = _number.Length > 10 ? UiKit.T.Section
                            : _number.Length > 7 ? new Font("Segoe UI Semibold", 15F)
                            : new Font("Segoe UI Semibold", 18F);
                UiKit.Text(g, _number, numFont, UiKit.T.Ink,
                    new Rectangle(pad, pad + 34, Width - pad * 2, 30),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                UiKit.Text(g, _sub, UiKit.Micro, UiKit.T.InkFaint,
                    new Rectangle(pad, pad + 64, Width - pad * 2, 26),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                if (_hover)
                {
                    var hint = new Rectangle(Width - 24, Height - 22, 14, 14);
                    UiKit.Text(g, "\uE72A", UiKit.T.Glyph, _accent, hint, UiKit.Center);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  CHART CARD — hover tooltip + clickable data points
        // ═══════════════════════════════════════════════════════════════

        private enum ChartKind { Line, Bar }

        [DesignerCategory("Code")]
        private sealed class ChartCard : Control
        {
            private readonly string _title;
            private readonly ChartKind _kind;

            private List<(string Label, double Value)> _lineData = new();
            private List<(string Label, double Value, Color Color)> _barData = new();
            private Color _lineColor = AppTheme.Primary;
            private bool _clickableBars;
            private bool _currency;

            private readonly List<(Rectangle Hit, string Label, string Value)> _hits = new();
            private int _hoverIndex = -1;
            private string? _selectedLabel;

            /// <summary>Raised with the label of the clicked data point.</summary>
            public event Action<string>? PointClicked;

            public ChartCard(string title, ChartKind kind)
            {
                _title = title;
                _kind = kind;

                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
            }

            public void SetLineData(List<(string, double)> data, Color color, bool currency = false)
            {
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

                // Click elsewhere on the card → drill into the selected / latest point.
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

                UiKit.Card(g, ClientRectangle, UiKit.T.Radius, UiKit.T.Surface, UiKit.T.Line);

                int pad = UiKit.T.S4;

                UiKit.Text(g, _title, UiKit.T.Section, UiKit.T.Ink,
                    new Rectangle(pad, pad, Width - pad * 2 - 116, 22),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                UiKit.Text(g, "\uE72A click a point", UiKit.Micro, UiKit.T.InkFaint,
                    new Rectangle(Width - 130, pad, 114, 20),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                var plot = new Rectangle(pad, pad + 28, Width - pad * 2, Height - pad * 2 - 28);

                _hits.Clear();

                if (_kind == ChartKind.Line) DrawLine(g, plot);
                else DrawBar(g, plot);

                if (_hoverIndex >= 0 && _hoverIndex < _hits.Count)
                {
                    var (_, label, value) = _hits[_hoverIndex];
                    var text = $"{label}:  {value}";
                    var size = UiKit.Measure(text, UiKit.T.SmallStrong);
                    var tip = new Rectangle(
                        Math.Min(Width - size.Width - 18, _hits[_hoverIndex].Hit.X),
                        Math.Max(0, _hits[_hoverIndex].Hit.Y - 30),
                        size.Width + 14, 24);
                    UiKit.FillRounded(g, tip, 6, UiKit.T.Ink);
                    UiKit.Text(g, text, UiKit.T.SmallStrong, Color.White, tip, UiKit.Center);
                }
            }

            private string FormatValue(double v) => _currency
                ? $"\u20b1{v:N0}"
                : v % 1 == 0 ? $"{v:N0}" : $"{v:0.##}";

            private void DrawLine(Graphics g, Rectangle plot)
            {
                if (_lineData.Count == 0) { DrawEmpty(g, plot); return; }

                int labelH = 20;
                int chartH = plot.Height - labelH;
                double max = Math.Max(1, _lineData.Max(p => p.Value));

                int n = _lineData.Count;
                var points = new PointF[n];
                for (int i = 0; i < n; i++)
                {
                    float x = plot.Left + (n == 1 ? plot.Width / 2f : (float)i / (n - 1) * plot.Width);
                    float y = plot.Top + chartH - (float)(_lineData[i].Value / max) * (chartH - 10) - 4;
                    points[i] = new PointF(x, y);
                }

                using (var area = new GraphicsPath())
                {
                    area.AddLines(points);
                    area.AddLine(points[^1].X, plot.Top + chartH, points[0].X, plot.Top + chartH);
                    area.CloseFigure();

                    using var brush = new LinearGradientBrush(
                        new Rectangle(plot.Left, plot.Top, plot.Width, chartH),
                        UiKit.Wash(_lineColor), Color.White, LinearGradientMode.Vertical);
                    g.FillPath(brush, area);
                }

                using (var pen = new Pen(_lineColor, 2f))
                {
                    pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, points);
                }

                for (int i = 0; i < n; i++)
                {
                    bool hot = i == _hoverIndex || _lineData[i].Label == _selectedLabel;
                    int dotSize = hot ? 11 : 7;

                    UiKit.Dot(g, points[i].X, points[i].Y, dotSize, _lineColor);

                    if (hot)
                    {
                        using var halo = new Pen(UiKit.Wash(_lineColor), 2f);
                        g.DrawEllipse(halo, points[i].X - dotSize - 2, points[i].Y - dotSize - 2,
                            (dotSize + 2) * 2f, (dotSize + 2) * 2f);
                    }

                    _hits.Add((
                        new Rectangle((int)points[i].X - 14, (int)points[i].Y - 14, 28, 28),
                        _lineData[i].Label,
                        FormatValue(_lineData[i].Value)));
                }

                int step = Math.Max(1, n / 6);
                for (int i = 0; i < n; i += step)
                {
                    var r = new Rectangle((int)(points[i].X - 30), plot.Top + chartH + 2, 60, labelH);
                    UiKit.Text(g, _lineData[i].Label, UiKit.T.Small, UiKit.T.InkFaint, r, UiKit.Center);
                }
            }

            private void DrawBar(Graphics g, Rectangle plot)
            {
                if (_barData.Count == 0) { DrawEmpty(g, plot); return; }

                double max = Math.Max(1, _barData.Max(b => b.Value));
                int gap = 10;
                int barW = Math.Max(8, (plot.Width - gap * (_barData.Count - 1)) / _barData.Count);
                int labelH = 18;

                for (int i = 0; i < _barData.Count; i++)
                {
                    var (label, value, color) = _barData[i];

                    int x = plot.Left + i * (barW + gap);
                    int h = Math.Max(3, (int)(value / max * (plot.Height - labelH - 24)));
                    var bar = new Rectangle(x, plot.Bottom - h - labelH, barW, h);

                    bool hot = i == _hoverIndex || label == _selectedLabel;

                    UiKit.FillRounded(g, bar, 6, hot ? color : UiKit.Wash(color));
                    if (label == _selectedLabel)
                        UiKit.StrokeRounded(g, bar, 6, color, 1.6f);

                    var valueText = FormatValue(value);
                    UiKit.Text(g, valueText, UiKit.Micro, hot ? color : UiKit.T.InkMuted,
                        new Rectangle(x - 6, bar.Top - 16, barW + 12, 14), UiKit.Center);

                    UiKit.Text(g, label, UiKit.Micro, UiKit.T.InkMuted,
                        new Rectangle(x - 8, plot.Bottom - labelH, barW + 16, labelH),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);

                    if (_clickableBars)
                        _hits.Add((new Rectangle(x, bar.Top - 18, barW, h + labelH + 18), label, valueText));
                }
            }

            private void DrawEmpty(Graphics g, Rectangle plot)
            {
                UiKit.Text(g, "No data yet", UiKit.T.Body, UiKit.T.InkFaint, plot, UiKit.Center);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  FLAT BUTTON
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class FlatButton : Control
        {
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
            }

            public int PreferredWidth => UiKit.Measure(Text, UiKit.T.BodyStrong).Width + 56;

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

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

                UiKit.Text(g, _glyph, new Font("Segoe MDL2 Assets", 10F), Color.White,
                    new Rectangle(16, 0, 18, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                UiKit.Text(g, Text, UiKit.T.BodyStrong, Color.White,
                    new Rectangle(36, 0, Width - 46, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
