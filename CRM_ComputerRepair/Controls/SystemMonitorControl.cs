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
    /// <summary>
    /// Super Admin use case — read the audit log of all system actions.
    /// Flat console-style layout: no cards, no tiles, just the table.
    /// </summary>
    [DesignerCategory("Code")]
    public class SystemMonitorControl : UserControl
    {
        private readonly ApiClient _api = new ApiClient();
        private List<AuditLogDto> _all = new();
        private List<AuditLogDto> _filtered = new();
        private string _activeFilter = "All";

        // ── Header ──
        private Label lblTitle = null!;
        private Label lblSummary = null!;
        private SaasButton btnRefresh = null!;

        // ── Toolbar ──
        private WorkbenchSearch searchBox = null!;
        private SystemSegmentedFilter filterBar = null!;
        private Label lblCount = null!;

        // ── Grid ──
        private DataGridView dgv = null!;
        private WorkbenchState emptyState = null!;

        private int _hoverRow = -1;

        // ═══════════ SHARED DRAWING HELPERS ═══════════
        private const int CellPadX = 16;
        private const TextFormatFlags Flat = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        private const TextFormatFlags CellText = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | Flat;

        private static readonly Font MonoFont = new Font("Consolas", 9F);
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

        private static Color AvatarColor(string? seed)
        {
            if (string.IsNullOrWhiteSpace(seed)) return AvatarPalette[0];
            int h = 0;
            foreach (char ch in seed) h = (h * 31 + ch) & 0x7FFFFFFF;
            return AvatarPalette[h % AvatarPalette.Length];
        }

        private static Color ActionColor(string? action)
        {
            var a = (action ?? "").ToLowerInvariant();
            if (a.Contains("create") || a.Contains("add") || a.Contains("register"))
                return AppTheme.Success;
            if (a.Contains("update") || a.Contains("edit") || a.Contains("modify"))
                return AppTheme.Primary;
            if (a.Contains("archive") || a.Contains("delete") || a.Contains("remove"))
                return AppTheme.Danger;
            if (a.Contains("login") || a.Contains("logout"))
                return Color.FromArgb(139, 92, 246);
            return UiKit.T.InkMuted;
        }

        public SystemMonitorControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            AutoScaleMode = AutoScaleMode.Font;

            BuildUi();

            this.Load += async (s, e) => await ReloadAsync();
        }

        private void BuildUi()
        {
            // ── Header ──
            lblTitle = new Label
            {
                Text = "System Monitor",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            lblSummary = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Size = new Size(100, 36);
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            Controls.Add(lblTitle);
            Controls.Add(lblSummary);
            Controls.Add(btnRefresh);

            // ── Toolbar ──
            searchBox = new WorkbenchSearch
            {
                Placeholder = "Filter by user, action, entity, or details..."
            };
            searchBox.QueryChanged += (s, e) => ApplyFilter();

            filterBar = new SystemSegmentedFilter(new (string, int?)[]
            {
                ("All", null),
                ("Creates", null),
                ("Updates", null),
                ("Archives", null)
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
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            Controls.Add(searchBox);
            Controls.Add(filterBar);
            Controls.Add(lblCount);

            // ── Grid ──
            dgv = new DataGridView();
            StyleGrid(dgv);

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
            emptyState.Show("\uE9D9", "No activity found",
                "Nothing has been logged yet. Create or edit a customer or repair request to generate activity.");

            Controls.Add(dgv);
            Controls.Add(emptyState);

            Resize += (s, e) => LayoutUi();
            LayoutUi();
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

            lblSummary.Location = new Point(pad + 1, lblTitle.Bottom + 6);

            // ── Toolbar ──
            int toolbarY = lblSummary.Bottom + 22;

            int filterW = filterBar.PreferredWidth;
            int searchW = Math.Max(240, Math.Min(420, contentW - filterW - 20));

            searchBox.SetBounds(pad, toolbarY, searchW, 38);
            filterBar.SetBounds(pad + contentW - filterW, toolbarY + 1, filterW, 36);

            // ── Count line ──
            int countY = searchBox.Bottom + 14;
            lblCount.Location = new Point(pad, countY);

            // ── Grid ──
            int gridY = lblCount.Bottom + 10;
            int gridH = Math.Max(120, Height - gridY - pad);

            dgv.SetBounds(pad, gridY, contentW, gridH);
            emptyState.SetBounds(pad, gridY, contentW, gridH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            UiKit.Quality(e.Graphics);

            // hairline under toolbar row
            using var pen = new Pen(UiKit.T.LineSoft, 1);
            int y = lblCount.Bottom + 4;
            e.Graphics.DrawLine(pen, UiKit.T.S6, y, Width - UiKit.T.S6, y);
        }

        // ═══════════ GRID ═══════════

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
            g.BackgroundColor = AppTheme.Background;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.None;
            g.GridColor = UiKit.T.LineSoft;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ScrollBars = ScrollBars.Both;
            g.RowTemplate.Height = 56;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 44;

            g.ColumnHeadersDefaultCellStyle.BackColor = AppTheme.Background;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = AppTheme.Background;
            g.ColumnHeadersDefaultCellStyle.ForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.Font = UiKit.T.SmallStrong;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(CellPadX, 0, CellPadX, 0);

            g.DefaultCellStyle.BackColor = AppTheme.Background;
            g.DefaultCellStyle.ForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Font = UiKit.T.Body;
            g.DefaultCellStyle.SelectionBackColor = UiKit.T.RowHover;
            g.DefaultCellStyle.SelectionForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Padding = new Padding(CellPadX, 0, CellPadX, 0);
            g.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

            g.CellPainting += (s, e) => PaintCell(g, e);

            g.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= g.RowCount || e.ColumnIndex < 0) return;
                switch (g.Columns[e.ColumnIndex].Name)
                {
                    case "WhenDisplay":
                    case "UserDisplay":
                    case "Action":
                    case "EntityDisplay":
                    case "Details":
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

            // ── Header row ──
            if (e.RowIndex == -1)
            {
                using (var b = new SolidBrush(AppTheme.Background))
                    gr.FillRectangle(b, cell);
                using (var p = new Pen(UiKit.T.Line, 1))
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

            Color rowBg = AppTheme.Background;
            if (selected && focused) rowBg = UiKit.T.RowHover;
            else if (selected && !focused) rowBg = UiKit.T.LineSoft;
            else if (hovered) rowBg = UiKit.T.RowHover;

            using (var b = new SolidBrush(rowBg))
                gr.FillRectangle(b, cell);
            using (var p = new Pen(UiKit.T.LineSoft))
                gr.DrawLine(p, cell.Left, cell.Bottom - 1, cell.Right, cell.Bottom - 1);

            UiKit.Quality(gr);

            if (e.ColumnIndex == 0 && selected && focused)
                UiKit.FillRounded(gr, new Rectangle(cell.Left, cell.Top + 10, 3, cell.Height - 21), 1, AppTheme.Primary);

            var rect = new Rectangle(cell.Left + CellPadX, cell.Top,
                Math.Max(0, cell.Width - CellPadX * 2), cell.Height - 1);
            int cy = rect.Top + rect.Height / 2;
            string text = Convert.ToString(e.FormattedValue) ?? "";

            switch (g.Columns[e.ColumnIndex].Name)
            {
                case "WhenDisplay":
                    if (string.IsNullOrWhiteSpace(text))
                        DrawMuted(gr, "—", rect, UiKit.T.Small, UiKit.T.InkFaint);
                    else
                        UiKit.Text(gr, text, MonoFont, UiKit.T.InkMuted, rect, CellText);
                    break;

                case "UserDisplay":
                    PaintUserCell(gr, item, rect);
                    break;

                case "Action":
                    {
                        Color fg = ActionColor(item.Action);
                        string txt = string.IsNullOrWhiteSpace(text) ? "—" : text;
                        PaintDotPill(gr, rect, cy, txt, fg, UiKit.Micro);
                        break;
                    }

                case "EntityDisplay":
                    if (string.IsNullOrWhiteSpace(text))
                        DrawMuted(gr, "—", rect, UiKit.T.Small, UiKit.T.InkFaint);
                    else
                        UiKit.Text(gr, text, UiKit.T.Body, UiKit.T.Ink, rect, CellText);
                    break;

                case "Details":
                    if (string.IsNullOrWhiteSpace(text))
                        DrawMuted(gr, "—", rect, UiKit.T.Small, UiKit.T.InkFaint);
                    else
                        UiKit.Text(gr, text, UiKit.T.Small, UiKit.T.InkMuted, rect, CellText);
                    break;

                default:
                    return;
            }

            e.Handled = true;
        }

        private static void PaintUserCell(Graphics gr, AuditLogDto item, Rectangle rect)
        {
            const int av = 30;
            string user = item.UserDisplay ?? "";

            if (string.IsNullOrWhiteSpace(user))
            {
                DrawMuted(gr, "System", rect, UiKit.T.Small, UiKit.T.InkFaint);
                return;
            }

            var avRect = new Rectangle(rect.Left, rect.Top + (rect.Height - av) / 2, av, av);
            Color ac = AvatarColor(user);
            UiKit.FillRounded(gr, avRect, 8, UiKit.Wash(ac));

            string initial = user.Trim().Substring(0, 1).ToUpperInvariant();
            UiKit.Text(gr, initial, UiKit.T.SmallStrong, ac, avRect, UiKit.Center);

            int tx = avRect.Right + 10;
            int tw = Math.Max(0, rect.Right - tx);
            UiKit.Text(gr, user, UiKit.T.BodyStrong, UiKit.T.Ink,
                new Rectangle(tx, rect.Top, tw, rect.Height), CellText);
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

        public async Task ReloadAsync()
        {
            try
            {
                _all = await _api.GetAuditLogAsync(null, null, 500);

                int total = _all.Count;
                int creates = _all.Count(a => ActionMatches(a.Action, "create", "add", "register"));
                int updates = _all.Count(a => ActionMatches(a.Action, "update", "edit", "modify"));
                int archives = _all.Count(a => ActionMatches(a.Action, "archive", "delete", "remove"));
                int users = _all.Select(a => a.UserDisplay ?? "").Where(s => s.Length > 0).Distinct().Count();

                lblSummary.Text = $"{total} events  ·  {creates} creates  ·  {updates} updates  ·  {archives} archives  ·  {users} users";

                filterBar.UpdateCounts(new Dictionary<string, int?>
                {
                    ["All"] = total,
                    ["Creates"] = creates,
                    ["Updates"] = updates,
                    ["Archives"] = archives
                });

                ApplyFilter();
            }
            catch (Exception ex)
            {
                _all = new List<AuditLogDto>();
                lblSummary.Text = "Couldn't load audit log";
                ApplyFilter();

                SaasToast.Show(FindForm(),
                    $"Couldn't load audit log: {ex.Message}",
                    ToastKind.Danger);
            }
        }

        private static bool ActionMatches(string? action, params string[] keys)
        {
            var a = (action ?? "").ToLowerInvariant();
            foreach (var k in keys)
                if (a.Contains(k)) return true;
            return false;
        }

        private void ApplyFilter()
        {
            var term = (searchBox.Query ?? "").Trim();

            IEnumerable<AuditLogDto> q = _all;

            if (_activeFilter == "Creates")
                q = q.Where(x => ActionMatches(x.Action, "create", "add", "register"));
            else if (_activeFilter == "Updates")
                q = q.Where(x => ActionMatches(x.Action, "update", "edit", "modify"));
            else if (_activeFilter == "Archives")
                q = q.Where(x => ActionMatches(x.Action, "archive", "delete", "remove"));

            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(x =>
                    (x.UserDisplay ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Action ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Entity ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.EntityDisplay ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Details ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            _filtered = q.ToList();

            dgv.DataSource = null;
            dgv.DataSource = _filtered;
            ConfigureColumns();

            bool hasData = _filtered.Count > 0;
            dgv.Visible = hasData;
            emptyState.Visible = !hasData;

            if (!hasData)
            {
                string msg = !string.IsNullOrEmpty(term)
                    ? $"Nothing matches \u201c{term}\u201d."
                    : "Nothing has been logged yet. Create or edit a customer or repair request to generate activity.";
                emptyState.Show("\uE9D9", "No activity found", msg);
            }
            else
            {
                dgv.ClearSelection();
            }

            lblCount.Text = _filtered.Count == _all.Count
                ? $"Showing {_filtered.Count} {(_filtered.Count == 1 ? "entry" : "entries")}"
                : $"Showing {_filtered.Count} of {_all.Count} entries";
        }

        private void ConfigureColumns()
        {
            foreach (var hidden in new[]
            {
                "AuditLogId", "UserId", "Entity", "EntityId", "Timestamp"
            })
            {
                var hCol = dgv.Columns[hidden];
                if (hCol != null)
                    hCol.Visible = false;
            }

            void Setup(string name, string header, int width, int idx, bool fill = false)
            {
                var c = dgv.Columns[name];
                if (c == null) return;
                c.HeaderText = header;
                c.SortMode = DataGridViewColumnSortMode.NotSortable;
                c.AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None;
                if (!fill) c.Width = width;
                else c.MinimumWidth = 220;
                c.DisplayIndex = idx;
            }

            Setup("WhenDisplay", "WHEN", 190, 0);
            Setup("UserDisplay", "USER", 180, 1);
            Setup("Action", "ACTION", 140, 2);
            Setup("EntityDisplay", "ENTITY", 190, 3);
            Setup("Details", "DETAILS", 0, 4, fill: true);
        }

        // ═══════════════════════════════════════════════════════════════
        //  SEGMENTED FILTER
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class SystemSegmentedFilter : Control
        {
            private readonly List<(string Key, int? Count)> _items = new();
            private int _selected = 0;
            private int _hover = -1;

            public event EventHandler<string>? SelectionChanged;

            public SystemSegmentedFilter((string Key, int? Count)[] items)
            {
                _items.AddRange(items);
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
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
                        UiKit.FillRounded(g, seg, 7, AppTheme.Background);
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
    }
}