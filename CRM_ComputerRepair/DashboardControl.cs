using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Business Intelligence Dashboard — KPI tiles + charts.
    /// KPI tiles are clickable: they raise an ActionRequested event
    /// so MainForm can navigate to the relevant page.
    /// </summary>
    [DesignerCategory("Code")]
    public class DashboardControl : UserControl
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private DashboardDto? _data;

        /// <summary>
        /// Raised when the user clicks a KPI tile that maps to an action.
        /// Payload is the navigation key, e.g. "retention", "repairs", "interactions".
        /// </summary>
        public event EventHandler<string>? ActionRequested;

        // ═══════════ CONTROLS ═══════════

        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private FlatButton btnRefresh = null!;

        private KpiTile tileCustomers = null!;
        private KpiTile tileRetention = null!;
        private KpiTile tileChurn = null!;
        private KpiTile tileTurnaround = null!;

        private ChartCard cardCustomersOverTime = null!;
        private ChartCard cardRepairsByStatus = null!;
        private ChartCard cardInteractionsByType = null!;
        private ChartCard cardRetentionTrend = null!;

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
                Text = "Business intelligence from your CRM data  ·  click a tile to take action",
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

            // KPI tiles
            tileCustomers = new KpiTile();
            tileRetention = new KpiTile();
            tileChurn = new KpiTile();
            tileTurnaround = new KpiTile();

            tileCustomers.Set("Total customers", "0", "Active customers", AppTheme.Primary, "\uE716");
            tileRetention.Set("Retention rate", "0%", "Customers with 2+ repairs", AppTheme.Success, "\uE73E");
            tileChurn.Set("Churn risk", "0", "No contact in 90 days", AppTheme.Danger, "\uE7BA");
            tileTurnaround.Set("Avg turnaround", "0 d", "Repair completion time", AppTheme.Warning, "\uE823");

            // ── Wire clicks ──
            tileCustomers.Click += (s, e) => RaiseAction("customers");
            tileRetention.Click += (s, e) => RaiseAction("customers");
            tileChurn.Click += (s, e) => RaiseAction("retention");
            tileTurnaround.Click += (s, e) => RaiseAction("repairs");

            tileCustomers.Cursor = Cursors.Hand;
            tileRetention.Cursor = Cursors.Hand;
            tileChurn.Cursor = Cursors.Hand;
            tileTurnaround.Cursor = Cursors.Hand;

            Controls.Add(tileCustomers);
            Controls.Add(tileRetention);
            Controls.Add(tileChurn);
            Controls.Add(tileTurnaround);

            // Chart cards
            cardCustomersOverTime = new ChartCard("Customers over time", ChartKind.Line);
            cardRepairsByStatus = new ChartCard("Repairs by status", ChartKind.Bar);
            cardInteractionsByType = new ChartCard("Interactions by type", ChartKind.Donut);
            cardRetentionTrend = new ChartCard("Retention trend", ChartKind.Line);

            Controls.Add(cardCustomersOverTime);
            Controls.Add(cardRepairsByStatus);
            Controls.Add(cardInteractionsByType);
            Controls.Add(cardRetentionTrend);

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

        private void RaiseAction(string key)
        {
            ActionRequested?.Invoke(this, key);
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

            int pad = 0;

            // KPI tiles
            int tileGap = 16;
            int tileHeight = 110;
            int tileWidth = (Width - tileGap * 3) / 4;

            tileCustomers.Location = new Point(pad, contentTop);
            tileRetention.Location = new Point(pad + (tileWidth + tileGap), contentTop);
            tileChurn.Location = new Point(pad + (tileWidth + tileGap) * 2, contentTop);
            tileTurnaround.Location = new Point(pad + (tileWidth + tileGap) * 3, contentTop);

            tileCustomers.Size = new Size(tileWidth, tileHeight);
            tileRetention.Size = new Size(tileWidth, tileHeight);
            tileChurn.Size = new Size(tileWidth, tileHeight);
            tileTurnaround.Size = new Size(tileWidth, tileHeight);

            // Charts in a 2x2 grid
            int chartsTop = contentTop + tileHeight + UiKit.T.S5;
            int chartsAvail = Height - chartsTop - pad;
            int chartH = (chartsAvail - 16) / 2;
            int chartW = (Width - 16) / 2;

            cardCustomersOverTime.Location = new Point(pad, chartsTop);
            cardRepairsByStatus.Location = new Point(pad + chartW + 16, chartsTop);
            cardInteractionsByType.Location = new Point(pad, chartsTop + chartH + 16);
            cardRetentionTrend.Location = new Point(pad + chartW + 16, chartsTop + chartH + 16);

            cardCustomersOverTime.Size = new Size(chartW, chartH);
            cardRepairsByStatus.Size = new Size(chartW, chartH);
            cardInteractionsByType.Size = new Size(chartW, chartH);
            cardRetentionTrend.Size = new Size(chartW, chartH);

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

                tileCustomers.Set("Total customers", _data.TotalCustomers.ToString(),
                    $"{_data.ActiveCustomers} active", AppTheme.Primary, "\uE716");

                tileRetention.Set("Retention rate", $"{_data.RetentionRate:0.#}%",
                    $"{_data.RepeatCustomerRate:0.#}% repeat buyers", AppTheme.Success, "\uE73E");

                tileChurn.Set("Churn risk", _data.ChurnRisk.ToString(),
                    $"{_data.OpenInteractions} open interactions", AppTheme.Danger, "\uE7BA");

                tileTurnaround.Set("Avg turnaround", $"{_data.AverageTurnaroundDays:0.#} d",
                    $"{_data.RepairsCompletedThisMonth} completed this month", AppTheme.Warning, "\uE823");

                cardCustomersOverTime.SetLineData(
                    _data.CustomersOverTime.Select(p => (p.Label, (double)p.Count)).ToList(),
                    AppTheme.Primary);

                cardRepairsByStatus.SetBarData(new List<(string, double, Color)>
                {
                    ("Pending",     _data.RepairsByStatus.Pending,     AppTheme.Warning),
                    ("Approved",    _data.RepairsByStatus.Approved,    AppTheme.Primary),
                    ("In progress", _data.RepairsByStatus.InProgress,  AppTheme.Primary),
                    ("Completed",   _data.RepairsByStatus.Completed,   AppTheme.Success),
                    ("Rejected",    _data.RepairsByStatus.Rejected,    AppTheme.Danger),
                    ("Reassigned",  _data.RepairsByStatus.Reassigned,  AppTheme.TextMuted)
                });

                cardInteractionsByType.SetDonutData(new List<(string, double, Color)>
                {
                    ("Inquiry",   _data.InteractionsByType.Inquiry,   AppTheme.Primary),
                    ("Complaint", _data.InteractionsByType.Complaint, AppTheme.Danger),
                    ("Feedback",  _data.InteractionsByType.Feedback,  AppTheme.Success)
                });

                cardRetentionTrend.SetLineData(
                    _data.RetentionTrend.Select(p => (p.Label, (double)p.Active)).ToList(),
                    AppTheme.Success);
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

        // ═══════════════════════════════════════════════════════════════
        //  KPI TILE — clickable, shows a hover "action" hint
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

                // Soft hover tint
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

                int pad = UiKit.T.S4;

                // Icon chip
                var iconRect = new Rectangle(pad, pad, 36, 36);
                UiKit.FillRounded(g, iconRect, 8, UiKit.Wash(_accent));

                using (var f = UiKit.GlyphFont(13F))
                    UiKit.Text(g, _glyph, f, _accent, iconRect, UiKit.Center);

                // Label
                UiKit.Text(g, _label, UiKit.T.Small, UiKit.T.InkMuted,
                    new Rectangle(pad + 48, pad + 4, Width - pad * 2 - 48, 20),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                // Number
                using (var f = new Font("Segoe UI Semibold", 22F))
                    UiKit.Text(g, _number, f, UiKit.T.Ink,
                        new Rectangle(pad, pad + 44, Width - pad * 2, 34),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                // Sub
                UiKit.Text(g, _sub, UiKit.T.Small, UiKit.T.InkFaint,
                    new Rectangle(pad, pad + 78, Width - pad * 2, 18),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                // Hint on hover
                if (_hover)
                {
                    var hint = new Rectangle(Width - 26, Height - 24, 16, 16);
                    UiKit.Text(g, "\uE72A", UiKit.T.Glyph, _accent, hint, UiKit.Center);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  CHART CARD — unchanged
        // ═══════════════════════════════════════════════════════════════

        private enum ChartKind { Line, Bar, Donut }

        [DesignerCategory("Code")]
        private sealed class ChartCard : Control
        {
            private readonly string _title;
            private readonly ChartKind _kind;

            private List<(string Label, double Value)> _lineData = new();
            private List<(string Label, double Value, Color Color)> _barData = new();
            private List<(string Label, double Value, Color Color)> _donutData = new();
            private Color _lineColor = AppTheme.Primary;

            public ChartCard(string title, ChartKind kind)
            {
                _title = title;
                _kind = kind;

                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
            }

            public void SetLineData(List<(string, double)> data, Color color)
            {
                _lineData = data;
                _lineColor = color;
                Invalidate();
            }

            public void SetBarData(List<(string, double, Color)> data)
            {
                _barData = data;
                Invalidate();
            }

            public void SetDonutData(List<(string, double, Color)> data)
            {
                _donutData = data;
                Invalidate();
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
                    new Rectangle(pad, pad, Width - pad * 2, 22),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                var plot = new Rectangle(
                    pad,
                    pad + 30,
                    Width - pad * 2,
                    Height - pad * 2 - 30);

                switch (_kind)
                {
                    case ChartKind.Line: DrawLine(g, plot); break;
                    case ChartKind.Bar: DrawBar(g, plot); break;
                    case ChartKind.Donut: DrawDonut(g, plot); break;
                }
            }

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

                foreach (var p in points)
                    UiKit.Dot(g, p.X, p.Y, 7, _lineColor);

                int step = Math.Max(1, n / 6);
                for (int i = 0; i < n; i += step)
                {
                    var r = new Rectangle(
                        (int)(points[i].X - 30),
                        plot.Top + chartH + 2,
                        60, labelH);
                    UiKit.Text(g, _lineData[i].Label, UiKit.T.Small, UiKit.T.InkFaint, r, UiKit.Center);
                }
            }

            private void DrawBar(Graphics g, Rectangle plot)
            {
                if (_barData.Count == 0) { DrawEmpty(g, plot); return; }

                double max = Math.Max(1, _barData.Max(b => b.Value));
                int gap = 10;
                int barW = (plot.Width - gap * (_barData.Count - 1)) / _barData.Count;

                for (int i = 0; i < _barData.Count; i++)
                {
                    var (label, value, color) = _barData[i];

                    int x = plot.Left + i * (barW + gap);
                    int h = (int)(value / max * (plot.Height - 40));
                    if (h < 3) h = 3;

                    var bar = new Rectangle(x, plot.Bottom - h - 20, barW, h);
                    UiKit.FillRounded(g, bar, 6, UiKit.Wash(color));

                    UiKit.Text(g, ((int)value).ToString(), UiKit.T.SmallStrong, color,
                        new Rectangle(x, bar.Top - 18, barW, 16), UiKit.Center);

                    UiKit.Text(g, label, UiKit.T.Small, UiKit.T.InkMuted,
                        new Rectangle(x, plot.Bottom - 18, barW, 16),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
                }
            }

            private void DrawDonut(Graphics g, Rectangle plot)
            {
                if (_donutData.Count == 0) { DrawEmpty(g, plot); return; }

                double total = _donutData.Sum(d => d.Value);
                if (total <= 0) { DrawEmpty(g, plot); return; }

                int size = Math.Min(plot.Height - 30, plot.Width / 2);
                int cx = plot.Left + plot.Width / 4;
                int cy = plot.Top + plot.Height / 2;
                var circle = new Rectangle(cx - size / 2, cy - size / 2, size, size);

                float start = -90;

                foreach (var (label, value, color) in _donutData)
                {
                    float sweep = (float)(value / total * 360);

                    using (var b = new SolidBrush(color))
                        g.FillPie(b, circle, start, sweep);

                    start += sweep;
                }

                int holeSize = size / 2;
                var hole = new Rectangle(cx - holeSize / 2, cy - holeSize / 2, holeSize, holeSize);
                using (var b = new SolidBrush(UiKit.T.Surface))
                    g.FillEllipse(b, hole);

                using (var f = new Font("Segoe UI Semibold", 16F))
                    UiKit.Text(g, ((int)total).ToString(), f, UiKit.T.Ink,
                        new Rectangle(cx - 40, cy - 16, 80, 32), UiKit.Center);

                int legendX = plot.Left + plot.Width / 2 + 20;
                int legendY = cy - (_donutData.Count * 26) / 2;

                for (int i = 0; i < _donutData.Count; i++)
                {
                    var (label, value, color) = _donutData[i];
                    int y = legendY + i * 26;

                    UiKit.Dot(g, legendX + 6, y + 10, 9, color);

                    UiKit.Text(g, label, UiKit.T.Body, UiKit.T.Ink,
                        new Rectangle(legendX + 20, y, plot.Right - legendX - 20, 20),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                    var pct = total > 0 ? value / total * 100 : 0;
                    UiKit.Text(g, $"{value:0} ({pct:0.#}%)", UiKit.T.Small, UiKit.T.InkMuted,
                        new Rectangle(legendX + 20, y + 2, plot.Right - legendX - 20, 20),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
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