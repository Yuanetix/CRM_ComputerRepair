using CRM.winforms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Reports page — filterable tables with CSV export.
    /// Three report types: Customers / Repairs / Interactions.
    /// </summary>
    [DesignerCategory("Code")]
    public class ReportsControl : UserControl
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();

        private ReportType _currentType = ReportType.Customers;

        private List<CustomerDto> _customers = new();
        private List<RepairRequestDto> _repairs = new();
        private List<InteractionDto> _interactions = new();

        // ═══════════ CONTROLS ═══════════

        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private FlatButton btnExport = null!;
        private FlatButton btnRefresh = null!;

        private Panel pnlTabs = null!;
        private readonly List<TabButton> _tabs = new();

        private SurfaceCard card = null!;
        private Label lblGridTitle = null!;
        private Label lblCount = null!;
        private DateRangePicker dateRange = null!;
        private SearchBox search = null!;

        private DataGridView dgv = null!;
        private Label lblEmpty = null!;

        // ═══════════ CONSTRUCTOR ═══════════

        public ReportsControl()
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
                Text = "Reports",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Detailed views of your customers, repairs, and interactions",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnRefresh = new FlatButton("Refresh", "\uE72C");
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            btnExport = new FlatButton("Export CSV", "\uE74E");
            btnExport.Click += (s, e) => ExportCurrentView();

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnRefresh);
            Controls.Add(btnExport);

            // Tabs
            pnlTabs = new Panel { BackColor = AppTheme.Background };

            _tabs.Add(new TabButton("Customers", ReportType.Customers));
            _tabs.Add(new TabButton("Repairs", ReportType.Repairs));
            _tabs.Add(new TabButton("Interactions", ReportType.Interactions));

            foreach (var tab in _tabs)
            {
                tab.Click += (s, e) => SwitchTab(tab.Type);
                pnlTabs.Controls.Add(tab);
            }

            _tabs[0].IsActive = true;
            Controls.Add(pnlTabs);

            // Workbench card
            card = new SurfaceCard();

            lblGridTitle = new Label
            {
                Text = "Customer report",
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

            dateRange = new DateRangePicker();
            dateRange.RangeChanged += (s, e) => ApplyFilter();

            search = new SearchBox { PlaceholderText = "Search within report..." };
            search.Inner.TextChanged += (s, e) => ApplyFilter();

            dgv = new DataGridView();
            StyleGrid(dgv);

            lblEmpty = new Label
            {
                Text = "No data for this filter.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = UiKit.T.InkMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Visible = false
            };

            card.Controls.Add(lblGridTitle);
            card.Controls.Add(lblCount);
            card.Controls.Add(dateRange);
            card.Controls.Add(search);
            card.Controls.Add(dgv);
            card.Controls.Add(lblEmpty);

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

        // ═══════════ TAB SWITCH ═══════════

        private void SwitchTab(ReportType type)
        {
            if (_currentType == type) return;
            _currentType = type;

            foreach (var tab in _tabs)
                tab.IsActive = tab.Type == type;

            lblGridTitle.Text = type switch
            {
                ReportType.Customers => "Customer report",
                ReportType.Repairs => "Repair report",
                ReportType.Interactions => "Interaction report",
                _ => "Report"
            };

            search.PlaceholderText = type switch
            {
                ReportType.Customers => "Search by name, email, phone...",
                ReportType.Repairs => "Search by request #, device, issue...",
                ReportType.Interactions => "Search by subject, notes...",
                _ => "Search..."
            };

            LayoutUi();
            ApplyFilter();
        }

        // ═══════════ LAYOUT ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            lblTitle.Location = new Point(0, 0);

            int subtitleY = lblTitle.PreferredHeight + UiKit.T.S1;
            lblSubtitle.Location = new Point(1, subtitleY);

            int btnY = UiKit.T.S1;

            btnExport.Size = new Size(btnExport.PreferredWidth, UiKit.T.ButtonHeight);
            btnExport.Location = new Point(Width - btnExport.Width, btnY);

            btnRefresh.Size = new Size(btnRefresh.PreferredWidth, UiKit.T.ButtonHeight);
            btnRefresh.Location = new Point(btnExport.Left - btnRefresh.Width - 8, btnY);

            // Tabs row
            int dividerY = subtitleY + lblSubtitle.PreferredHeight + UiKit.T.S4;
            int tabsY = dividerY + UiKit.T.S5;

            int tabsX = 0;
            foreach (var tab in _tabs)
            {
                int w = tab.GetPreferredWidth();
                tab.Location = new Point(tabsX, 0);
                tab.Size = new Size(w, 34);
                tabsX += w + 4;
            }

            pnlTabs.Location = new Point(0, tabsY);
            pnlTabs.Size = new Size(tabsX, 34);

            // Card
            int cardTop = tabsY + 34 + UiKit.T.S5;
            int cardH = Math.Max(200, Height - cardTop);

            card.Location = new Point(0, cardTop);
            card.Size = new Size(Width, cardH);

            int cp = UiKit.T.S5;

            lblGridTitle.Location = new Point(cp, UiKit.T.S5 - 2);
            lblCount.Location = new Point(lblGridTitle.Right + UiKit.T.S2,
                lblGridTitle.Top + lblGridTitle.PreferredHeight - lblCount.PreferredHeight - 2);

            int toolbarY = lblGridTitle.Bottom + UiKit.T.S4;

            dateRange.Size = new Size(dateRange.PreferredWidth, 34);
            dateRange.Location = new Point(cp, toolbarY);

            int searchW = Math.Min(280, Math.Max(180, card.Width - cp * 2 - dateRange.Width - UiKit.T.S4));
            search.Size = new Size(searchW, UiKit.T.InputHeight);
            search.Location = new Point(card.Width - cp - searchW, toolbarY + (34 - UiKit.T.InputHeight) / 2);

            int gridTop = toolbarY + 34 + UiKit.T.S4;
            int gridW = card.Width - cp * 2;
            int gridH = card.Height - gridTop - cp;

            if (gridW > 100 && gridH > 60)
            {
                dgv.Location = new Point(cp, gridTop);
                dgv.Size = new Size(gridW, gridH);
                lblEmpty.Location = new Point(cp, gridTop);
                lblEmpty.Size = new Size(gridW, gridH);
            }
        }

        // ═══════════ DATA ═══════════

        public async Task ReloadAsync()
        {
            try
            {
                var from = dateRange.From;
                var to = dateRange.To;

                _customers = await _api.GetCustomersAsync(includeArchived: true);
                _repairs = await _api.GetRepairRequestsAsync();
                _interactions = await _api.GetInteractionsAsync(null, includeArchived: true);

                // Apply date filter on load data
                _customers = _customers.Where(c => c.CreatedAt >= from && c.CreatedAt <= to).ToList();
                _repairs = _repairs.Where(r => r.RequestDate >= from && r.RequestDate <= to).ToList();
                _interactions = _interactions.Where(i => i.InteractionDate >= from && i.InteractionDate <= to).ToList();

                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Couldn't load reports.\n\n{ex.Message}",
                    "Connection problem",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ApplyFilter()
        {
            var term = search.Inner.Text?.Trim() ?? "";

            switch (_currentType)
            {
                case ReportType.Customers:
                    {
                        var view = _customers
                            .Where(c => MatchCustomer(c, term))
                            .ToList();

                        dgv.DataSource = null;
                        dgv.DataSource = view;
                        ConfigureCustomerColumns();
                        lblCount.Text = $"{view.Count} of {_customers.Count}";
                        ShowEmpty(view.Count == 0);
                        break;
                    }

                case ReportType.Repairs:
                    {
                        var view = _repairs
                            .Where(r => MatchRepair(r, term))
                            .ToList();

                        dgv.DataSource = null;
                        dgv.DataSource = view;
                        ConfigureRepairColumns();
                        lblCount.Text = $"{view.Count} of {_repairs.Count}";
                        ShowEmpty(view.Count == 0);
                        break;
                    }

                case ReportType.Interactions:
                    {
                        var view = _interactions
                            .Where(i => MatchInteraction(i, term))
                            .ToList();

                        dgv.DataSource = null;
                        dgv.DataSource = view;
                        ConfigureInteractionColumns();
                        lblCount.Text = $"{view.Count} of {_interactions.Count}";
                        ShowEmpty(view.Count == 0);
                        break;
                    }
            }
        }

        private void ShowEmpty(bool empty)
        {
            lblEmpty.Visible = empty;
            dgv.Visible = !empty;
        }

        private static bool MatchCustomer(CustomerDto c, string term)
        {
            if (string.IsNullOrEmpty(term)) return true;
            return
                (c.FirstName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (c.LastName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (c.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (c.Phone ?? "").Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchRepair(RepairRequestDto r, string term)
        {
            if (string.IsNullOrEmpty(term)) return true;
            return
                (r.RequestNumber ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (r.DeviceModel ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (r.IssueDescription ?? "").Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchInteraction(InteractionDto i, string term)
        {
            if (string.IsNullOrEmpty(term)) return true;
            return
                (i.Subject ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (i.Notes ?? "").Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        // ═══════════ EXPORT ═══════════

        private void ExportCurrentView()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "CSV files|*.csv",
                FileName = $"{_currentType}_report_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                var sb = new StringBuilder();

                switch (_currentType)
                {
                    case ReportType.Customers:
                        sb.AppendLine("ID,First Name,Last Name,Email,Phone,Address,Loyalty Points,Status,Created");
                        foreach (var c in _customers)
                            sb.AppendLine($"{c.CustomerId},\"{c.FirstName}\",\"{c.LastName}\",\"{c.Email}\",\"{c.Phone}\",\"{c.Address}\",{c.LoyaltyPoints},{(c.IsActive ? "Active" : "Archived")},{c.CreatedAt:yyyy-MM-dd}");
                        break;

                    case ReportType.Repairs:
                        sb.AppendLine("Request #,Device,Status,Priority,Requested,Completed,Est Cost");
                        foreach (var r in _repairs)
                            sb.AppendLine($"\"{r.RequestNumber}\",\"{r.DeviceModel}\",{r.StatusText},{r.PriorityText},{r.RequestDate:yyyy-MM-dd},{(r.CompletionDate?.ToString("yyyy-MM-dd") ?? "")},{r.EstimatedCost}");
                        break;

                    case ReportType.Interactions:
                        sb.AppendLine("ID,Type,Subject,Status,Priority,Created,Closed");
                        foreach (var i in _interactions)
                            sb.AppendLine($"{i.CustomerInteractionId},{i.TypeText},\"{i.Subject}\",{i.StatusText},{i.PriorityText},{i.InteractionDate:yyyy-MM-dd},{(i.ClosedAt?.ToString("yyyy-MM-dd") ?? "")}");
                        break;
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);

                MessageBox.Show(
                    $"Report exported to:\n\n{sfd.FileName}",
                    "Export complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Export failed.\n\n{ex.Message}",
                    "Export error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ═══════════ GRID STYLE + COLUMNS ═══════════

        private static void StyleGrid(DataGridView g)
        {
            g.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            g.BackgroundColor = UiKit.T.Surface;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.GridColor = UiKit.T.LineSoft;

            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.ReadOnly = true;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.ScrollBars = ScrollBars.Vertical;
            g.RowTemplate.Height = UiKit.T.RowHeight;
            g.ColumnHeadersHeight = UiKit.T.HeaderHeight;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            g.ColumnHeadersDefaultCellStyle.BackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.ForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.Font = UiKit.T.SmallStrong;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(UiKit.T.S3, 0, UiKit.T.S3, 0);

            g.DefaultCellStyle.BackColor = UiKit.T.Surface;
            g.DefaultCellStyle.ForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Font = UiKit.T.Body;
            g.DefaultCellStyle.SelectionBackColor = UiKit.Wash(AppTheme.Primary);
            g.DefaultCellStyle.SelectionForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Padding = new Padding(UiKit.T.S3, 0, UiKit.T.S3, 0);
        }

        private void ConfigureCustomerColumns()
        {
            HideColumns("CustomerId", "LoyaltyPoints", "IsActive", "FullName", "Status", "NameDisplay", "StatusDisplay");

            ShowColumn("NameDisplay", "Name", 220);
            ShowColumn("Email", "Email", 0, fill: true);
            ShowColumn("Phone", "Phone", 140);
            ShowColumn("Address", "Address", 220);
            ShowColumn("StatusDisplay", "Status", 100);
            ShowColumn("CreatedAt", "Created", 120, format: "MMM d, yyyy");
        }

        private void ConfigureRepairColumns()
        {
            HideColumns(
                "RepairRequestId", "CustomerId", "DeviceId",
                "Status", "Priority",
                "ActualCost", "PartsCost", "LaborCost",
                "AssignedToManagerId", "TechnicianNotes");

            ShowColumn("RequestNumber", "Request #", 140);
            ShowColumn("DeviceModel", "Device", 0, fill: true);
            ShowColumn("SerialNumber", "Serial", 130);
            ShowColumn("StatusText", "Status", 110);
            ShowColumn("PriorityText", "Priority", 100);
            ShowColumn("RequestDate", "Requested", 120, format: "MMM d, yyyy");
            ShowColumn("EstimatedCost", "Est. Cost", 110);
        }

        private void ConfigureInteractionColumns()
        {
            HideColumns(
                "CustomerInteractionId", "CustomerId", "RepairRequestId",
                "InteractionType", "Status", "Priority",
                "InteractionByUserId", "UpdatedAt", "ClosedAt", "IsActive",
                "Notes", "Resolution", "ActivityStatus");

            ShowColumn("TypeText", "Type", 110);
            ShowColumn("Subject", "Subject", 0, fill: true);
            ShowColumn("StatusText", "Status", 120);
            ShowColumn("PriorityText", "Priority", 100);
            ShowColumn("InteractionDate", "Created", 130, format: "MMM d, yyyy");
            ShowColumn("ClosedAt", "Closed", 130, format: "MMM d, yyyy");
        }

        private void HideColumns(params string[] names)
        {
            foreach (var n in names)
                if (dgv.Columns[n] != null) dgv.Columns[n].Visible = false;
        }

        private void ShowColumn(string name, string header, int width, bool fill = false, string? format = null)
        {
            var c = dgv.Columns[name];
            if (c == null) return;

            c.Visible = true;
            c.HeaderText = header;
            c.AutoSizeMode = fill
                ? DataGridViewAutoSizeColumnMode.Fill
                : DataGridViewAutoSizeColumnMode.None;
            if (!fill) c.Width = width;
            else c.MinimumWidth = 180;
            if (format != null) c.DefaultCellStyle.Format = format;
        }

        // ═══════════════════════════════════════════════════════════════
        //  SUPPORTING CONTROLS
        // ═══════════════════════════════════════════════════════════════

        private enum ReportType { Customers, Repairs, Interactions }

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
                UiKit.Card(e.Graphics, ClientRectangle, UiKit.T.Radius, UiKit.T.Surface, UiKit.T.Line);
                base.OnPaint(e);
            }
        }

        [DesignerCategory("Code")]
        private sealed class TabButton : Control
        {
            public ReportType Type { get; }
            private bool _active, _hover;
            private readonly string _label;

            public TabButton(string label, ReportType type)
            {
                _label = label;
                Type = type;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
                Cursor = Cursors.Hand;
                Font = UiKit.T.SmallStrong;
            }

            public int GetPreferredWidth() => UiKit.Measure(_label, UiKit.T.SmallStrong).Width + 32;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public bool IsActive
            {
                get => _active;
                set { _active = value; Invalidate(); }
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var b = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(b, ClientRectangle);

                if (_active)
                {
                    UiKit.FillRounded(g, ClientRectangle, 8, AppTheme.Primary);
                    UiKit.Text(g, _label, UiKit.T.SmallStrong, Color.White,
                        ClientRectangle, UiKit.Center);
                }
                else
                {
                    if (_hover)
                        UiKit.FillRounded(g, ClientRectangle, 8, UiKit.T.RowHover);

                    UiKit.Text(g, _label, UiKit.T.SmallStrong, UiKit.T.InkMuted,
                        ClientRectangle, UiKit.Center);
                }
            }
        }

        [DesignerCategory("Code")]
        private sealed class DateRangePicker : Control
        {
            private DateTimePicker dtpFrom = null!;
            private DateTimePicker dtpTo = null!;
            private Label lblDash = null!;

            public event EventHandler? RangeChanged;

            public DateTime From => dtpFrom.Value.Date;
            public DateTime To => dtpTo.Value.Date.AddDays(1).AddSeconds(-1);

            public int PreferredWidth => 300;

            public DateRangePicker()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;

                dtpFrom = new DateTimePicker
                {
                    Font = UiKit.T.Body,
                    Format = DateTimePickerFormat.Short,
                    Value = DateTime.Now.AddMonths(-6),
                    Width = 120
                };
                dtpTo = new DateTimePicker
                {
                    Font = UiKit.T.Body,
                    Format = DateTimePickerFormat.Short,
                    Value = DateTime.Now,
                    Width = 120
                };
                lblDash = new Label
                {
                    Text = "→",
                    Font = UiKit.T.Body,
                    ForeColor = UiKit.T.InkMuted,
                    AutoSize = true,
                    BackColor = Color.Transparent
                };

                dtpFrom.ValueChanged += (s, e) => RangeChanged?.Invoke(this, EventArgs.Empty);
                dtpTo.ValueChanged += (s, e) => RangeChanged?.Invoke(this, EventArgs.Empty);

                Controls.Add(dtpFrom);
                Controls.Add(lblDash);
                Controls.Add(dtpTo);

                Resize += (s, e) => LayoutChildren();
                LayoutChildren();
            }

            private void LayoutChildren()
            {
                dtpFrom.Location = new Point(0, (Height - dtpFrom.Height) / 2);
                lblDash.Location = new Point(dtpFrom.Right + 6, Height / 2 - 10);
                dtpTo.Location = new Point(lblDash.Right + 6, (Height - dtpTo.Height) / 2);
            }
        }

        [DesignerCategory("Code")]
        private sealed class SearchBox : Control
        {
            public TextBox Inner { get; }
            private bool _focused;

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

                Controls.Add(Inner);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                Inner.Location = new Point(34, (Height - Inner.PreferredHeight) / 2);
                Inner.Width = Width - 46;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var b = new SolidBrush(UiKit.T.Surface))
                    g.FillRectangle(b, ClientRectangle);

                UiKit.FillRounded(g, new Rectangle(0, 0, Width, Height), 8, UiKit.T.Surface);

                var border = _focused ? AppTheme.Primary : UiKit.T.Line;
                using (var path = UiKit.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 8))
                using (var pen = new Pen(border, _focused ? 1.4f : 1f))
                    g.DrawPath(pen, path);

                UiKit.Text(g, "\uE721", UiKit.T.Glyph, _focused ? AppTheme.Primary : UiKit.T.InkFaint,
                    new Rectangle(10, 0, 20, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

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