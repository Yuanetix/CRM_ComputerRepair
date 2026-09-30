using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Multi-branch management control for enterprise tenants (TechRevive).
    /// Displays branch locations, operational status, inter-branch repair transfers,
    /// and regional business metrics.
    /// 
    /// Layout (flat, no cards):
    ///   1. Header          – page title + subtitle
    ///   2. Summary strip   – four network-wide figures on a single line
    ///   3. Toolbar         – section title + short explanation of branch switching
    ///   4. Branch directory – one table row per branch, "Switch Context" per row
    /// </summary>
    [DesignerCategory("Code")]
    public class BranchingControl : UserControl
    {
        private Panel pnlHeader = null!;
        private Panel pnlToolbar = null!;
        private StatStrip stripStats = null!;
        private DataGridView grid = null!;
        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private Label lblSection = null!;
        private Label lblHint = null!;

        private BranchInfo[] _branches = null!;
        private int _hoverRow = -1;

        // ── Layout constants ─────────────────────────────────────────────────
        private const int PageMargin = 24;
        private const int HeaderRowHeight = 38;
        private const int BodyRowHeight = 58;

        // ── Column indexes ───────────────────────────────────────────────────
        private const int ColBranch = 0;
        private const int ColLocation = 1;
        private const int ColTechnicians = 2;
        private const int ColRepairs = 3;
        private const int ColRevenue = 4;
        private const int ColStatus = 5;
        private const int ColAction = 6;

        // ── Fonts (created once) ─────────────────────────────────────────────
        private static readonly Font IconFont = new("Segoe MDL2 Assets", 16F);
        private static readonly Font SectionFont = new("Segoe UI Semibold", 11F);
        private static readonly Font RowTitleFont = new("Segoe UI Semibold", 9.5F);
        private static readonly Font RowValueFont = new("Segoe UI Semibold", 9.5F);
        private static readonly Font HeaderCellFont = new("Segoe UI Semibold", 7.5F);
        private static readonly Font StatValueFont = new("Segoe UI Semibold", 13F);

        private const TextFormatFlags TextFlags =
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

        private static readonly Color Green = Color.FromArgb(16, 185, 129);
        private static readonly Color Blue = Color.FromArgb(59, 130, 246);

        public BranchingControl()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            Dock = DockStyle.Fill;

            _branches = new[]
            {
                new BranchInfo(
                    "Main Flagship - BGC Taguig", "BR-BGC-01", "Main Operations Hub",
                    "G/F High Street South Corporate Plaza, 26th St, Taguig", "+63 920 555 3031",
                    "14", "112", "₱185,200", "PRIMARY HUB", Green),
                new BranchInfo(
                    "Makati Central Branch", "BR-MKT-02", "Full Service Center",
                    "Level 3 Ayala Malls Circuit, Theater Drive, Makati", "+63 920 555 3032",
                    "8", "64", "₱118,900", "ONLINE", Blue),
                new BranchInfo(
                    "Quezon City North Branch", "BR-QC-03", "Hardware & Component Repair",
                    "Unit 102 Gilmore Tech Plaza, Aurora Blvd, Quezon City", "+63 920 555 3033",
                    "10", "88", "₱124,400", "ONLINE", Blue),
                new BranchInfo(
                    "Alabang South Branch", "BR-ALB-04", "Express Diagnostics Hub",
                    "Unit 405 Filinvest Corporate Center, Alabang, Muntinlupa", "+63 920 555 3034",
                    "6", "45", "₱54,000", "ONLINE", Blue),
            };

            BuildUi();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            grid.ClearSelection();
            grid.CurrentCell = null;
        }

        // ════════════════════════════════════════════════════════════════════
        //  UI construction
        // ════════════════════════════════════════════════════════════════════

        private void BuildUi()
        {
            SuspendLayout();

            // WinForms docks in reverse add-order: Fill first, then the Top panels
            // from the bottom-most to the top-most.

            // ── 4. Branch directory (Fill) ───────────────────────────────────
            grid = BuildGrid();

            var gridFrame = new Panel
            {
                Dock = DockStyle.Top,
                Height = HeaderRowHeight + (_branches.Length * BodyRowHeight) + 2,
                Padding = new Padding(1),
                BackColor = AppTheme.Border
            };
            gridFrame.Controls.Add(grid);

            var pnlBody = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.Background,
                Padding = new Padding(PageMargin, 0, PageMargin, PageMargin),
                AutoScroll = true
            };
            pnlBody.Controls.Add(gridFrame);
            Controls.Add(pnlBody);

            // ── 3. Toolbar ───────────────────────────────────────────────────
            pnlToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 58,
                BackColor = AppTheme.Background,
                Padding = new Padding(PageMargin + 1, 12, PageMargin + 1, 0)
            };

            lblSection = new Label
            {
                Text = "Branch Directory",
                Font = SectionFont,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblHint = new Label
            {
                Text = $"{_branches.Length} locations  ·  Switch Context filters intakes, repair orders and inventory by branch",
                Font = AppTheme.FontStatus,
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight
            };

            pnlToolbar.Controls.Add(lblHint);
            pnlToolbar.Controls.Add(lblSection);
            Controls.Add(pnlToolbar);

            // ── 2. Summary strip ─────────────────────────────────────────────
            stripStats = new StatStrip(new[]
            {
                new StatItem("ACTIVE BRANCHES", "4 Locations", "\uE716", AppTheme.Primary),
                new StatItem("INTER-BRANCH TICKETS", "28 Transferred", "\uE8BD", Green),
                new StatItem("REGIONAL REVENUE", "₱482,500.00", "\uE9D5", Color.FromArgb(124, 58, 237)),
                new StatItem("CENTRALIZED SYNC", "100% Real-Time", "\uE7BA", Color.FromArgb(14, 165, 233)),
            }, IconFont, AppTheme.FontStatus, StatValueFont)
            {
                Dock = DockStyle.Top,
                Height = 84
            };
            Controls.Add(stripStats);

            // ── 1. Header ────────────────────────────────────────────────────
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = AppTheme.Surface,
                Padding = new Padding(28, 16, 28, 16)
            };

            lblTitle = new Label
            {
                Text = "Branch Operations & Regional Management",
                Font = AppTheme.FontTitle,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(28, 16)
            };

            lblSubtitle = new Label
            {
                Text = "Enterprise Multi-Branching tier active  ·  Centralized synchronization across 4 store locations",
                Font = AppTheme.FontPageSubtitle,
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                Location = new Point(28, 48)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            pnlHeader.Paint += (s, e) =>
            {
                using var p = new Pen(AppTheme.Border, 1);
                e.Graphics.DrawLine(p, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
            };
            Controls.Add(pnlHeader);

            ResumeLayout(true);
        }

        private DataGridView BuildGrid()
        {
            var dg = new BufferedGrid
            {
                Dock = DockStyle.Fill,
                BackgroundColor = AppTheme.Surface,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = AppTheme.Border,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = HeaderRowHeight,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ScrollBars = ScrollBars.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                StandardTab = true
            };

            dg.RowTemplate.Height = BodyRowHeight;

            // header style
            dg.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = AppTheme.Background,
                ForeColor = AppTheme.TextMuted,
                SelectionBackColor = AppTheme.Background,
                SelectionForeColor = AppTheme.TextMuted,
                Font = HeaderCellFont,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 8, 0)
            };

            // body style
            dg.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                SelectionBackColor = Blend(AppTheme.Surface, AppTheme.Primary, 0.07f),
                SelectionForeColor = AppTheme.TextPrimary,
                Font = AppTheme.FontBody,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 8, 0)
            };

            dg.Columns.Add(MakeColumn("colBranch", "BRANCH", 27, DataGridViewContentAlignment.MiddleLeft, 16));
            dg.Columns.Add(MakeColumn("colLocation", "ADDRESS  /  CONTACT", 31, DataGridViewContentAlignment.MiddleLeft, 8));
            dg.Columns.Add(MakeColumn("colTech", "TECHNICIANS", 9, DataGridViewContentAlignment.MiddleRight, 8));
            dg.Columns.Add(MakeColumn("colRepairs", "ACTIVE REPAIRS", 10, DataGridViewContentAlignment.MiddleRight, 8));
            dg.Columns.Add(MakeColumn("colRevenue", "MTD REVENUE", 11, DataGridViewContentAlignment.MiddleRight, 8));
            dg.Columns.Add(MakeColumn("colStatus", "STATUS", 12, DataGridViewContentAlignment.MiddleLeft, 24));
            dg.Columns.Add(MakeColumn("colAction", "", 14, DataGridViewContentAlignment.MiddleCenter, 8));

            foreach (var b in _branches)
            {
                int i = dg.Rows.Add(b.Name, b.Address, b.Technicians, b.Repairs, b.Revenue, b.Badge, string.Empty);
                dg.Rows[i].Tag = b;
            }

            dg.Columns[ColRevenue].DefaultCellStyle.Font = RowValueFont;
            dg.Columns[ColAction].MinimumWidth = 150;
            dg.Columns[ColBranch].MinimumWidth = 200;
            dg.Columns[ColLocation].MinimumWidth = 220;

            dg.CellPainting += Grid_CellPainting;
            dg.CellMouseMove += Grid_CellMouseMove;
            dg.CellMouseLeave += (s, e) => SetHoverRow(-1);
            dg.CellMouseClick += Grid_CellMouseClick;

            return dg;
        }

        private static DataGridViewTextBoxColumn MakeColumn(string name, string header, float weight,
            DataGridViewContentAlignment align, int paddingLeft)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                FillWeight = weight,
                MinimumWidth = 80,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };

            var pad = align == DataGridViewContentAlignment.MiddleRight
                ? new Padding(0, 0, 20, 0)
                : new Padding(paddingLeft, 0, 8, 0);

            col.HeaderCell.Style = new DataGridViewCellStyle { Alignment = align, Padding = pad };
            col.DefaultCellStyle = new DataGridViewCellStyle { Alignment = align, Padding = pad };
            return col;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Grid painting & interaction
        // ════════════════════════════════════════════════════════════════════

        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (grid.Rows[e.RowIndex].Tag is not BranchInfo b) return;

            switch (e.ColumnIndex)
            {
                case ColBranch:
                    PaintTwoLine(e, b.Name, RowTitleFont, AppTheme.TextPrimary,
                        $"{b.Code}  ·  {b.Role}", 16);
                    break;

                case ColLocation:
                    PaintTwoLine(e, b.Address, AppTheme.FontBody, AppTheme.TextSecondary,
                        b.Phone, 8);
                    break;

                case ColStatus:
                    PaintStatus(e, b);
                    break;

                case ColAction:
                    PaintActionButton(e);
                    break;
            }
        }

        private static void PaintBase(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds,
                DataGridViewPaintParts.Background |
                DataGridViewPaintParts.SelectionBackground |
                DataGridViewPaintParts.Border);
        }

        private static void PaintTwoLine(DataGridViewCellPaintingEventArgs e, string top, Font topFont,
            Color topColor, string bottom, int left)
        {
            PaintBase(e);
            var b = e.CellBounds;
            int w = b.Width - left - 12;

            TextRenderer.DrawText(e.Graphics, top, topFont,
                new Rectangle(b.X + left, b.Y + 11, w, 18), topColor, TextFlags);
            TextRenderer.DrawText(e.Graphics, bottom, AppTheme.FontStatus,
                new Rectangle(b.X + left, b.Y + 30, w, 16), AppTheme.TextMuted, TextFlags);
            e.Handled = true;
        }

        private static void PaintStatus(DataGridViewCellPaintingEventArgs e, BranchInfo b)
        {
            PaintBase(e);
            var cell = e.CellBounds;
            var g = e.Graphics;

            var oldMode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var br = new SolidBrush(b.BadgeColor))
                g.FillEllipse(br, cell.X + 24, cell.Y + (cell.Height - 8) / 2, 8, 8);
            g.SmoothingMode = oldMode;

            TextRenderer.DrawText(g, b.Badge, AppTheme.FontStatus,
                new Rectangle(cell.X + 40, cell.Y, cell.Width - 48, cell.Height),
                b.BadgeColor, TextFlags);
            e.Handled = true;
        }

        private void PaintActionButton(DataGridViewCellPaintingEventArgs e)
        {
            PaintBase(e);
            var g = e.Graphics;
            var rect = GetButtonRect(e.CellBounds);
            bool hot = e.RowIndex == _hoverRow;

            using (var fill = new SolidBrush(hot ? AppTheme.Primary : AppTheme.Surface))
                g.FillRectangle(fill, rect);
            using (var pen = new Pen(AppTheme.Primary, 1))
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);

            TextRenderer.DrawText(g, "Switch Context", AppTheme.FontStatus, rect,
                hot ? Color.White : AppTheme.Primary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            e.Handled = true;
        }

        private static Rectangle GetButtonRect(Rectangle cell)
        {
            const int w = 118, h = 30;
            return new Rectangle(cell.X + Math.Max(4, (cell.Width - w) / 2), cell.Y + (cell.Height - h) / 2, w, h);
        }

        private void Grid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != ColAction) { SetHoverRow(-1); return; }

            var cell = grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            var pt = new Point(cell.X + e.X, cell.Y + e.Y);
            SetHoverRow(GetButtonRect(cell).Contains(pt) ? e.RowIndex : -1);
        }

        private void SetHoverRow(int row)
        {
            if (_hoverRow == row) return;
            int old = _hoverRow;
            _hoverRow = row;
            grid.Cursor = row >= 0 ? Cursors.Hand : Cursors.Default;
            if (old >= 0) grid.InvalidateCell(ColAction, old);
            if (row >= 0) grid.InvalidateCell(ColAction, row);
        }

        private void Grid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || e.RowIndex < 0 || e.ColumnIndex != ColAction) return;
            if (grid.Rows[e.RowIndex].Tag is not BranchInfo b) return;

            var cell = grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            var pt = new Point(cell.X + e.X, cell.Y + e.Y);
            if (!GetButtonRect(cell).Contains(pt)) return;

            MessageBox.Show(
                $"Switched active branch operational context to: {b.Name}.\nAll intakes, repair orders, and inventory will now filter by this location.",
                "Branch Context Changed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private static Color Blend(Color baseColor, Color overlay, float amount)
        {
            int r = (int)(baseColor.R + (overlay.R - baseColor.R) * amount);
            int g = (int)(baseColor.G + (overlay.G - baseColor.G) * amount);
            int b = (int)(baseColor.B + (overlay.B - baseColor.B) * amount);
            return Color.FromArgb(r, g, b);
        }

        // ════════════════════════════════════════════════════════════════════
        //  Supporting types
        // ════════════════════════════════════════════════════════════════════

        private sealed class BranchInfo
        {
            public string Name { get; }
            public string Code { get; }
            public string Role { get; }
            public string Address { get; }
            public string Phone { get; }
            public string Technicians { get; }
            public string Repairs { get; }
            public string Revenue { get; }
            public string Badge { get; }
            public Color BadgeColor { get; }

            public BranchInfo(string name, string code, string role, string address, string phone,
                              string technicians, string repairs, string revenue, string badge, Color badgeColor)
            {
                Name = name; Code = code; Role = role; Address = address; Phone = phone;
                Technicians = technicians; Repairs = repairs; Revenue = revenue;
                Badge = badge; BadgeColor = badgeColor;
            }
        }

        private sealed class BufferedGrid : DataGridView
        {
            public BufferedGrid()
            {
                DoubleBuffered = true;
            }
        }

        private readonly struct StatItem
        {
            public string Label { get; }
            public string Value { get; }
            public string Glyph { get; }
            public Color Accent { get; }

            public StatItem(string label, string value, string glyph, Color accent)
            {
                Label = label; Value = value; Glyph = glyph; Accent = accent;
            }
        }

        /// <summary>Flat one-line KPI strip: icon + label + value, separated by hairlines.</summary>
        private sealed class StatStrip : Control
        {
            private readonly StatItem[] _items;
            private readonly Font _iconFont, _labelFont, _valueFont;

            public StatStrip(StatItem[] items, Font iconFont, Font labelFont, Font valueFont)
            {
                _items = items;
                _iconFont = iconFont;
                _labelFont = labelFont;
                _valueFont = valueFont;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Surface;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(AppTheme.Surface);

                using var line = new Pen(AppTheme.Border, 1);
                g.DrawLine(line, 0, Height - 1, Width, Height - 1);

                int n = _items.Length;
                int colW = Width / n;

                for (int i = 0; i < n; i++)
                {
                    int x = i * colW;
                    var item = _items[i];

                    if (i > 0)
                        g.DrawLine(line, x, 18, x, Height - 19);

                    int left = x + (i == 0 ? PageMargin + 4 : 28);

                    TextRenderer.DrawText(g, item.Glyph, _iconFont,
                        new Rectangle(left, 0, 28, Height - 1), item.Accent,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                    int textX = left + 40;
                    int textW = colW - (textX - x) - 12;

                    TextRenderer.DrawText(g, item.Label, _labelFont,
                        new Rectangle(textX, 18, textW, 16), AppTheme.TextMuted, TextFlags);
                    TextRenderer.DrawText(g, item.Value, _valueFont,
                        new Rectangle(textX, 36, textW, 26), AppTheme.TextPrimary, TextFlags);
                }
            }
        }
    }
}