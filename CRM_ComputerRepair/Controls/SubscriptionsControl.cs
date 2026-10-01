using CRM.winforms.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    [DesignerCategory("Code")]
    public class SubscriptionsControl : UserControl
    {
        private readonly ApiClient _api = new ApiClient();
        private List<SubscriptionDto> _all = new();
        private List<SubscriptionDto> _filtered = new();
        private string _activeFilter = "All";

        // ── Controls ──
        private Label lblTitle = null!;
        private SaasButton btnTenantSubs = null!;
        private SaasButton btnAuditHistory = null!;
        private SaasButton btnModulePricing = null!;
        private SaasButton btnAdd = null!;
        private SaasButton btnRefresh = null!;

        // ── Stat Tiles ──
        private KpiTile tileTotal = null!;
        private KpiTile tileActive = null!;
        private KpiTile tileMultiBranch = null!;
        private KpiTile tileSubscribers = null!;
        private readonly List<KpiTile> _tiles = new();

        // ── Workbench Card ──
        private WorkbenchCard card = null!;
        private WorkbenchSearch searchBox = null!;
        private SubscriptionsSegmentedFilter filterBar = null!;
        private Label lblCount = null!;
        private DataGridView dgv = null!;
        private WorkbenchState emptyState = null!;

        private ContextMenuStrip _actionsMenu = null!;
        private int _menuRowIndex = -1;
        private int _hoverRow = -1;

        // ═══════════ SHARED DRAWING HELPERS ═══════════
        private const int CellPadX = 14;
        private const TextFormatFlags Flat = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        private const TextFormatFlags CellText = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | Flat;

        private static readonly Font MonoFont = new Font("Consolas", 9F);
        private static readonly Color Emerald = Color.FromArgb(16, 185, 129);
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

        public SubscriptionsControl()
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

        private void BuildActionsMenu()
        {
            _actionsMenu = new ContextMenuStrip { ShowImageMargin = false, Font = UiKit.T.Body };
            _actionsMenu.Renderer = new QuietMenuRenderer();

            var miEdit = new ToolStripMenuItem("Edit Plan") { Height = 32 };
            miEdit.Click += (s, e) => EditCurrentPlan();

            var miToggle = new ToolStripMenuItem("Toggle Active / Inactive") { Height = 32 };
            miToggle.Click += async (s, e) => await TogglePlanStatusAsync();

            var miArchive = new ToolStripMenuItem("Archive Plan") { Height = 32 };
            miArchive.Click += async (s, e) => await ArchivePlanAsync();

            var miRestore = new ToolStripMenuItem("Restore Plan") { Height = 32 };
            miRestore.Click += async (s, e) => await RestorePlanAsync();

            _actionsMenu.Items.AddRange(new ToolStripItem[] { miEdit, miToggle, new ToolStripSeparator(), miArchive, miRestore });

            _actionsMenu.Opening += (s, e) =>
            {
                var item = CurrentItem;
                if (item != null)
                {
                    miArchive.Visible = !item.IsArchived;
                    miRestore.Visible = item.IsArchived;
                    miToggle.Enabled = !item.IsArchived;
                }
            };
        }

        private void BuildUi()
        {
            // ── Header ──
            lblTitle = new Label
            {
                Text = "Subscription Plans",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            btnAdd = new SaasButton("Add Plan", SaasButtonVariant.Secondary, "\uE710");
            btnAdd.Size = new Size(115, 36);
            btnAdd.Click += async (s, e) => await AddPlanAsync();

            btnModulePricing = new SaasButton("Module Pricing", SaasButtonVariant.Secondary, "\uE8A1");
            btnModulePricing.Size = new Size(140, 36);
            btnModulePricing.Click += (s, e) =>
            {
                using var dlg = new ModulePricingDialog();
                dlg.ShowDialog(this.FindForm());
            };

            btnAuditHistory = new SaasButton("Audit History", SaasButtonVariant.Secondary, "\uE81C");
            btnAuditHistory.Size = new Size(130, 36);
            btnAuditHistory.Click += (s, e) =>
            {
                using var dlg = new SubscriptionHistoryDialog();
                dlg.ShowDialog(this.FindForm());
            };

            btnTenantSubs = new SaasButton("Tenant Modules & Billing", SaasButtonVariant.Primary, "\uE716");
            btnTenantSubs.Size = new Size(200, 36);
            btnTenantSubs.Click += async (s, e) => await OpenTenantSubscriptionPickerAsync();

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Size = new Size(95, 36);
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            Controls.Add(lblTitle);
            Controls.Add(btnTenantSubs);
            Controls.Add(btnAuditHistory);
            Controls.Add(btnModulePricing);
            Controls.Add(btnAdd);
            Controls.Add(btnRefresh);

            // ── KPI Tiles ──
            tileTotal = AddTile("TOTAL PLANS", "0", "All subscription tiers", AppTheme.Primary, "\uE9D5");
            tileActive = AddTile("ACTIVE TIERS", "0", "Currently offered to tenants", AppTheme.Success, "\uE73E");
            tileMultiBranch = AddTile("MULTI-BRANCH", "0", "Plans with branching enabled", Color.FromArgb(139, 92, 246), "\uE716");
            tileSubscribers = AddTile("SUBSCRIBED TENANTS", "0", "Businesses on a plan", Color.FromArgb(59, 130, 246), "\uE716");

            // ── Workbench Card ──
            card = new WorkbenchCard { BackColor = UiKit.T.Surface };

            searchBox = new WorkbenchSearch
            {
                Placeholder = "Search by plan name, features, or billing cycle..."
            };
            searchBox.QueryChanged += (s, e) => ApplyFilter();

            filterBar = new SubscriptionsSegmentedFilter(new (string, int?)[]
            {
                ("All", null),
                ("Active", null),
                ("Inactive", null),
                ("Archived", null)
            });
            filterBar.SelectionChanged += (s, key) =>
            {
                _activeFilter = key;
                ApplyFilter();
            };

            lblCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            dgv = new DataGridView();
            StyleGrid(dgv);
            dgv.CellClick += Dgv_CellClick;
            dgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _filtered.Count)
                    EditCurrentPlan();
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

            emptyState = new WorkbenchState { Visible = false };
            emptyState.Show("\uE9D5", "No subscription plans found",
                "Create your first SaaS subscription tier with pricing in PHP and user limits.");

            card.Controls.Add(searchBox);
            card.Controls.Add(filterBar);
            card.Controls.Add(lblCount);
            card.Controls.Add(dgv);
            card.Controls.Add(emptyState);

            Controls.Add(card);

            Resize += (s, e) => LayoutUi();
            LayoutUi();
        }

        private KpiTile AddTile(string label, string number, string sub, Color accent, string glyph)
        {
            var t = new KpiTile();
            t.Set(label, number, sub, accent, glyph);
            _tiles.Add(t);
            Controls.Add(t);
            return t;
        }

        // ═══════════ LAYOUT ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            int pad = UiKit.T.S6;
            int contentW = Math.Max(700, Width - pad * 2);

            // ── Header ──
            lblTitle.Location = new Point(pad, pad);

            int btnY = pad;
            btnRefresh.Location = new Point(pad + contentW - btnRefresh.Width, btnY);
            btnAdd.Location = new Point(btnRefresh.Left - btnAdd.Width - 8, btnY);
            btnModulePricing.Location = new Point(btnAdd.Left - btnModulePricing.Width - 8, btnY);
            btnAuditHistory.Location = new Point(btnModulePricing.Left - btnAuditHistory.Width - 8, btnY);
            btnTenantSubs.Location = new Point(btnAuditHistory.Left - btnTenantSubs.Width - 8, btnY);

            int y = lblTitle.Bottom + 20;

            // ── KPI Tiles ──
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
            y += rowMaxH + 24;

            // ── Workbench Card ──
            int cardPad = UiKit.T.S6;
            int cardH = Math.Max(320, Height - y - pad);
            card.SetBounds(pad, y, contentW, cardH);

            int innerW = contentW - cardPad * 2;

            int filterW = filterBar.PreferredWidth;
            int searchW = Math.Max(240, Math.Min(440, innerW - filterW - 20));

            searchBox.SetBounds(cardPad, cardPad, searchW, 38);
            filterBar.SetBounds(cardPad + innerW - filterW, cardPad + 1, filterW, 36);

            int gridY = searchBox.Bottom + 14;
            int gridH = cardH - gridY - cardPad - 26;

            dgv.SetBounds(cardPad, gridY, innerW, Math.Max(120, gridH));
            emptyState.SetBounds(cardPad, gridY, innerW, Math.Max(120, gridH));
            lblCount.Location = new Point(cardPad, dgv.Bottom + 6);
        }

        // ═══════════ GRID ═══════════

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
            g.RowTemplate.Height = 64;
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
                    MinimumWidth = minWidth,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
            }

            Col("colName", "PLAN NAME", "SubscriptionName", 200, 240);
            Col("colPrice", "PRICE (PHP)", "PriceDisplay", 120, 130);
            Col("colDuration", "DURATION", "DurationDisplay", 110, 130);
            Col("colUsers", "QUOTAS", "", 170, 200);

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colMultiBranch",
                HeaderText = "MULTI-BRANCH",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = 150,
                MinimumWidth = 150,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colSubscribers",
                HeaderText = "SUBSCRIBED",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = 140,
                MinimumWidth = 140,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStatus",
                HeaderText = "STATUS",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = 150,
                MinimumWidth = 150,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            var btnCol = new DataGridViewButtonColumn
            {
                Name = "colActions",
                HeaderText = "",
                Text = "",
                UseColumnTextForButtonValue = true,
                Width = 56,
                MinimumWidth = 56,
                FlatStyle = FlatStyle.Flat,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            btnCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            btnCol.DefaultCellStyle.BackColor = UiKit.T.Surface;
            btnCol.DefaultCellStyle.SelectionBackColor = UiKit.T.RowHover;
            btnCol.DefaultCellStyle.Padding = new Padding(0);
            g.Columns.Add(btnCol);

            g.CellPainting += (s, e) => PaintCell(g, e);

            g.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= g.RowCount || e.ColumnIndex < 0) return;
                switch (g.Columns[e.ColumnIndex].Name)
                {
                    case "colName":
                    case "colUsers":
                    case "colDuration":
                        var v = Convert.ToString(g.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
                        if (!string.IsNullOrWhiteSpace(v)) e.ToolTipText = v;
                        break;
                }
            };

            g.CellMouseClick += (s, e) =>
            {
                if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.RowIndex >= _filtered.Count) return;
                g.ClearSelection();
                g.Rows[e.RowIndex].Selected = true;
                _menuRowIndex = e.RowIndex;
            };
        }

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

            if (e.RowIndex < 0 || e.RowIndex >= _filtered.Count) return;
            var item = _filtered[e.RowIndex];

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

            switch (g.Columns[e.ColumnIndex].Name)
            {
                case "colName":
                    PaintNameCell(gr, item, rect);
                    break;

                case "colPrice":
                    if (string.IsNullOrWhiteSpace(text))
                        DrawMuted(gr, "—", rect, UiKit.T.Small, UiKit.T.InkFaint);
                    else
                        UiKit.Text(gr, text, MonoFont, UiKit.T.Ink, rect, CellText);
                    break;

                case "colDuration":
                    if (string.IsNullOrWhiteSpace(text))
                        DrawMuted(gr, "—", rect, UiKit.T.Small, UiKit.T.InkFaint);
                    else
                        UiKit.Text(gr, text, UiKit.T.Small, UiKit.T.InkMuted, rect, CellText);
                    break;

                case "colUsers":
                    {
                        string quota = $"Max {item.MaxUsers} users  ·  {item.MaxDevices} devices";
                        UiKit.Text(gr, quota, UiKit.T.Small, UiKit.T.Ink, rect, CellText);
                        break;
                    }

                case "colMultiBranch":
                    {
                        bool ok = item.EnableMultiBranching;
                        Color fg = ok ? AppTheme.Primary : UiKit.T.InkMuted;
                        string txt = ok ? "Enabled" : "Single";
                        PaintDotPill(gr, rect, cy, txt, fg, UiKit.Micro);
                        break;
                    }

                case "colSubscribers":
                    {
                        int count = item.SubscribedCompaniesCount;
                        if (count <= 0)
                        {
                            DrawMuted(gr, "No subscribers", rect, UiKit.T.Small, UiKit.T.InkFaint);
                        }
                        else
                        {
                            string txt = $"{count} tenant{(count == 1 ? "" : "s")}";
                            UiKit.Text(gr, txt, UiKit.T.Small, UiKit.T.Ink, rect, CellText);
                        }
                        break;
                    }

                case "colStatus":
                    {
                        bool archived = item.IsArchived;
                        bool active = item.IsActive && !archived;
                        Color fg = archived ? UiKit.T.InkMuted : (active ? AppTheme.Success : AppTheme.Danger);
                        string txt = archived ? "Archived" : (active ? "Active" : "Inactive");
                        PaintDotPill(gr, rect, cy, txt, fg, UiKit.Micro);
                        break;
                    }

                case "colActions":
                    {
                        int size = 30;
                        var btn = new Rectangle(rect.Right - size, cy - size / 2, size, size);
                        bool hot = hovered;

                        UiKit.FillRounded(gr, btn, 8,
                            hot ? UiKit.Wash(AppTheme.Primary) : Color.Transparent);

                        int dotR = 2;
                        int dotGap = 6;
                        int dotY = cy - dotR;
                        int startX = btn.Left + (btn.Width / 2) - dotGap;
                        using (var b = new SolidBrush(hot ? AppTheme.Primary : UiKit.T.InkMuted))
                        {
                            gr.FillEllipse(b, startX - dotR, dotY, dotR * 2, dotR * 2);
                            gr.FillEllipse(b, startX - dotR + dotGap, dotY, dotR * 2, dotR * 2);
                            gr.FillEllipse(b, startX - dotR + dotGap * 2, dotY, dotR * 2, dotR * 2);
                        }
                        e.Handled = true;
                        return;
                    }

                default:
                    return;
            }

            e.Handled = true;
        }

        private static void PaintNameCell(Graphics gr, SubscriptionDto item, Rectangle rect)
        {
            const int av = 36;
            string name = item.SubscriptionName ?? "";

            var avRect = new Rectangle(rect.Left, rect.Top + (rect.Height - av) / 2, av, av);
            Color ac = AvatarColor(name);
            UiKit.FillRounded(gr, avRect, 10, UiKit.Wash(ac));

            string initial = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim().Substring(0, 1).ToUpperInvariant();
            UiKit.Text(gr, initial, UiKit.T.BodyStrong, ac, avRect, UiKit.Center);

            int tx = avRect.Right + 12;
            int tw = Math.Max(0, rect.Right - tx);
            string sub = item.Description ?? "";

            if (string.IsNullOrWhiteSpace(sub))
            {
                UiKit.Text(gr, name, UiKit.T.BodyStrong, UiKit.T.Ink,
                    new Rectangle(tx, rect.Top, tw, rect.Height), CellText);
                return;
            }

            int h1 = UiKit.T.BodyStrong.Height + 2;
            int h2 = UiKit.T.Small.Height + 2;
            int y0 = rect.Top + (rect.Height - (h1 + h2)) / 2;

            UiKit.Text(gr, name, UiKit.T.BodyStrong, UiKit.T.Ink, new Rectangle(tx, y0, tw, h1), CellText);
            UiKit.Text(gr, sub, UiKit.T.Small, UiKit.T.InkMuted, new Rectangle(tx, y0 + h1, tw, h2), CellText);
        }

        private static void PaintDotPill(Graphics gr, Rectangle rect, int cy, string text, Color fg, Font font)
        {
            int textW = MeasureW(text, font);
            int pillW = Math.Min(rect.Width, textW + 34);
            var pill = new Rectangle(rect.Left, cy - 12, pillW, 24);

            UiKit.FillRounded(gr, pill, 12, UiKit.Wash(fg));
            UiKit.FillRounded(gr, new Rectangle(pill.Left + 11, cy - 3, 6, 6), 3, fg);
            UiKit.Text(gr, text, font, fg,
                new Rectangle(pill.Left + 23, pill.Top, Math.Max(0, pill.Width - 31), pill.Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | Flat);
        }

        private static void DrawMuted(Graphics gr, string text, Rectangle rect, Font font, Color color)
            => UiKit.Text(gr, text, font, color, rect, CellText);

        // ═══════════ DATA ═══════════

        private async Task ReloadAsync()
        {
            try
            {
                _all = await _api.GetSubscriptionsAsync(includeArchived: true);

                int total = _all.Count;
                int active = _all.Count(p => p.IsActive && !p.IsArchived);
                int inactive = _all.Count(p => !p.IsActive && !p.IsArchived);
                int archived = _all.Count(p => p.IsArchived);
                int multiBranch = _all.Count(p => p.EnableMultiBranching && !p.IsArchived);
                int totalSubs = _all.Sum(p => p.SubscribedCompaniesCount);

                tileTotal.Set("TOTAL PLANS", total.ToString(),
                    $"{active} active · {archived} archived", AppTheme.Primary, "\uE9D5");
                tileActive.Set("ACTIVE TIERS", active.ToString(),
                    total > 0 ? $"{(double)active / total * 100:0.#}% of catalog" : "No plans yet",
                    AppTheme.Success, "\uE73E");
                tileMultiBranch.Set("MULTI-BRANCH", multiBranch.ToString(),
                    "Plans with branching enabled", Color.FromArgb(139, 92, 246), "\uE716");
                tileSubscribers.Set("SUBSCRIBED TENANTS", totalSubs.ToString(),
                    "Businesses on a plan", Color.FromArgb(59, 130, 246), "\uE716");

                filterBar.UpdateCounts(new Dictionary<string, int?>
                {
                    ["All"] = total,
                    ["Active"] = active,
                    ["Inactive"] = inactive,
                    ["Archived"] = archived
                });

                ApplyFilter();
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(), $"Failed to load subscription plans: {ex.Message}", ToastKind.Danger);
            }
        }

        private void ApplyFilter()
        {
            string query = (searchBox.Query ?? "").Trim();

            _filtered = _all.Where(p =>
            {
                if (_activeFilter == "Active" && (!p.IsActive || p.IsArchived)) return false;
                if (_activeFilter == "Inactive" && (p.IsActive || p.IsArchived)) return false;
                if (_activeFilter == "Archived" && !p.IsArchived) return false;

                if (!string.IsNullOrWhiteSpace(query))
                {
                    bool matchName = (p.SubscriptionName ?? "").Contains(query, StringComparison.OrdinalIgnoreCase);
                    bool matchDesc = (p.Description ?? "").Contains(query, StringComparison.OrdinalIgnoreCase);
                    bool matchBilling = (p.BillingCycle ?? "").Contains(query, StringComparison.OrdinalIgnoreCase);
                    bool matchDuration = (p.DurationDisplay ?? "").Contains(query, StringComparison.OrdinalIgnoreCase);
                    if (!matchName && !matchDesc && !matchBilling && !matchDuration) return false;
                }

                return true;
            }).ToList();

            dgv.DataSource = null;
            dgv.DataSource = _filtered;

            bool hasData = _filtered.Count > 0;
            dgv.Visible = hasData;
            emptyState.Visible = !hasData;

            lblCount.Text = $"Showing {_filtered.Count} of {_all.Count} subscription plans";
        }

        // ═══════════ ACTIONS ═══════════

        private void Dgv_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _filtered.Count) return;

            if (e.ColumnIndex == dgv.Columns["colActions"]!.Index)
            {
                _menuRowIndex = e.RowIndex;
                var cellRect = dgv.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                _actionsMenu.Show(dgv, new Point(cellRect.Right - _actionsMenu.Width, cellRect.Bottom));
            }
        }

        private SubscriptionDto? CurrentItem => _menuRowIndex >= 0 && _menuRowIndex < _filtered.Count
            ? _filtered[_menuRowIndex]
            : (dgv.CurrentRow != null && dgv.CurrentRow.Index < _filtered.Count ? _filtered[dgv.CurrentRow.Index] : null);

        private async Task AddPlanAsync()
        {
            using var dlg = new SubscriptionPlanDialog();
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                SaasToast.Show(FindForm(), "Created plan successfully!", ToastKind.Success);
                await ReloadAsync();
            }
        }

        private async void EditCurrentPlan()
        {
            var item = CurrentItem;
            if (item == null) return;

            try
            {
                var plan = await _api.GetPlanByIdAsync(item.SubscriptionId);
                using var dlg = new SubscriptionPlanDialog(plan ?? new SubscriptionPlanDto
                {
                    PlanId = item.SubscriptionId,
                    PlanName = item.SubscriptionName,
                    Price = item.PricePerMonth,
                    MaxUsers = item.MaxUsers,
                    MaxDevices = item.MaxDevices,
                    BillingInterval = item.BillingCycle ?? "Monthly",
                    Description = item.Description
                });

                if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    SaasToast.Show(FindForm(), $"Updated plan '{item.SubscriptionName}'.", ToastKind.Success);
                    await ReloadAsync();
                }
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(), $"Failed to open plan editor: {ex.Message}", ToastKind.Danger);
            }
        }

        private async Task TogglePlanStatusAsync()
        {
            var item = CurrentItem;
            if (item == null) return;

            try
            {
                bool newState = await _api.ToggleSubscriptionStatusAsync(item.SubscriptionId);
                item.IsActive = newState;
                SaasToast.Show(FindForm(),
                    $"Plan '{item.SubscriptionName}' is now {(newState ? "active" : "inactive")}.",
                    newState ? ToastKind.Success : ToastKind.Warning);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(), $"Failed to update status: {ex.Message}", ToastKind.Danger);
            }
        }

        private async Task ArchivePlanAsync()
        {
            var item = CurrentItem;
            if (item == null) return;

            bool confirm = SaasConfirm.Ask(
                FindForm(),
                "Archive Subscription Plan",
                $"Archive '{item.SubscriptionName}'?",
                confirmText: "Archive Plan",
                danger: true,
                detail: "Existing subscribed businesses will maintain their current plan, but new registrations will no longer see this tier.");

            if (!confirm) return;

            try
            {
                await _api.ArchiveSubscriptionAsync(item.SubscriptionId);
                SaasToast.Show(FindForm(), $"Plan '{item.SubscriptionName}' archived.", ToastKind.Warning);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(), $"Failed to archive plan: {ex.Message}", ToastKind.Danger);
            }
        }

        private async Task RestorePlanAsync()
        {
            var item = CurrentItem;
            if (item == null) return;

            try
            {
                await _api.RestoreSubscriptionAsync(item.SubscriptionId);
                SaasToast.Show(FindForm(), $"Plan '{item.SubscriptionName}' restored and activated.", ToastKind.Success);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(), $"Failed to restore plan: {ex.Message}", ToastKind.Danger);
            }
        }

        private async Task OpenTenantSubscriptionPickerAsync()
        {
            try
            {
                var companies = await _api.GetCompaniesAsync();
                var form = FindForm();
                if (companies == null || companies.Count == 0)
                {
                    if (form != null) Toast.Notify(form, "No Tenants", "No companies registered yet.", ToastKind.Warning);
                    return;
                }

                var menu = new ContextMenuStrip { ShowImageMargin = false, Font = UiKit.Body };
                menu.Renderer = new QuietMenuRenderer();
                foreach (var c in companies)
                {
                    var item = new ToolStripMenuItem($"{c.CompanyName} (ID: {c.CompanyId})") { Height = 32 };
                    item.Click += (s, e) =>
                    {
                        using var dlg = new CompanySubscriptionDialog(c.CompanyId, c.CompanyName);
                        dlg.ShowDialog(FindForm());
                    };
                    menu.Items.Add(item);
                }
                menu.Show(btnTenantSubs, new Point(0, btnTenantSubs.Height + 2));
            }
            catch (Exception ex)
            {
                var f = FindForm();
                if (f != null) Toast.Notify(f, "Error", ex.Message, ToastKind.Danger);
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
        //  SEGMENTED FILTER
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class SubscriptionsSegmentedFilter : Control
        {
            private readonly List<(string Key, int? Count)> _items = new();
            private int _selected = 0;
            private int _hover = -1;

            public event EventHandler<string>? SelectionChanged;

            public SubscriptionsSegmentedFilter((string Key, int? Count)[] items)
            {
                _items.AddRange(items);
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = UiKit.T.Surface;
                Cursor = Cursors.Hand;
                Font = UiKit.T.SmallStrong;
                Height = 36;
            }

            public int PreferredWidth
            {
                get
                {
                    int total = 16;
                    foreach (var item in _items)
                    {
                        string text = item.Count.HasValue ? $"{item.Key} ({item.Count})" : item.Key;
                        var sz = TextRenderer.MeasureText(text, Font);
                        total += Math.Max(104, sz.Width + 28);
                    }
                    return Math.Max(330, total);
                }
            }

            public void UpdateCounts(Dictionary<string, int?> counts)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (counts.TryGetValue(_items[i].Key, out var c))
                        _items[i] = (_items[i].Key, c);
                }
                Invalidate();
            }

            public void SetKey(string key)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (string.Equals(_items[i].Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (_selected != i)
                        {
                            _selected = i;
                            Invalidate();
                            SelectionChanged?.Invoke(this, _items[_selected].Key);
                        }
                        return;
                    }
                }
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

                    string text = _items[i].Count.HasValue
                        ? $"{_items[i].Key} ({_items[i].Count.GetValueOrDefault()})"
                        : _items[i].Key;

                    Color fg = active ? UiKit.T.Ink : (i == _hover ? UiKit.T.Ink : UiKit.T.InkMuted);
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
                    SelectionChanged?.Invoke(this, _items[_selected].Key);
                }
                base.OnMouseClick(e);
            }
        }

        [DesignerCategory("Code")]
        private sealed class QuietMenuRenderer : ToolStripProfessionalRenderer
        {
            public QuietMenuRenderer() : base(new Colors()) { RoundedEdges = false; }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var r = new Rectangle(4, 0, e.Item.Width - 8, e.Item.Height);
                if (e.Item.Selected) UiKit.FillRounded(e.Graphics, r, 6, UiKit.T.RowHover);
            }
            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using var pen = new Pen(UiKit.T.LineSoft, 1);
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
            }
            private sealed class Colors : ProfessionalColorTable
            {
                public override Color ToolStripDropDownBackground => UiKit.T.Surface;
                public override Color MenuBorder => UiKit.T.Line;
                public override Color MenuItemBorder => UiKit.T.Surface;
                public override Color ImageMarginGradientBegin => UiKit.T.Surface;
                public override Color ImageMarginGradientMiddle => UiKit.T.Surface;
                public override Color ImageMarginGradientEnd => UiKit.T.Surface;
            }
        }
    }
}