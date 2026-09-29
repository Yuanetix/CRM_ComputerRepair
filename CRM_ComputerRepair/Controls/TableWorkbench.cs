using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Shared table workbench — one consistent table system for Customers,
    /// Follow-ups, Interactions, Repairs and Customer History.
    ///
    /// Canonical tokens (do not re-tune per screen):
    ///   header 38px / SmallStrong / InkMuted, bottom hairline Line
    ///   row 46px / Body / Ink, hover RowHover, selected Wash(Primary)
    ///   divider LineSoft, cell padding S3, actions column 44px "…"
    ///   toolbar: segments 34px + search (320/200 clamp) + 14px gaps
    ///   card padding 24px, footer pagination 44px, page size 20
    ///   empty/loading: 52px circle + Section title + Body detail
    ///   pills: 22px, StatusColors(), SmallStrong
    ///   menus: Quiet inset-r6 renderer, Body font, S2/S1 padding
    /// </summary>
    public static class TableKit
    {
        public const int HeaderHeight = 42;
        public const int RowHeight = 52;
        public const int ActionWidth = 44;
        public const int CardPad = 24;
        public const int ToolbarH = 34;
        public const int ToolbarGap = 14;
        public const int InputH = 34;
        public const int FooterH = 44;
        public const int PageSize = 20;
        public const int SearchMax = 320;
        public const int SearchMin = 200;

        private static readonly Font EllipsisFont = AppFonts.Strong(13F);

        public static string FormatCount(int view, int total) =>
            view == total
                ? $"{view} {(view == 1 ? "record" : "records")}"
                : $"{view} of {total}";

        public static int SearchWidth(int cardWidth, int segmentsWidth)
        {
            int avail = cardWidth - CardPad * 2 - segmentsWidth - UiKit.T.S4;
            return Math.Min(SearchMax, Math.Max(SearchMin, avail));
        }

        public static string PageRangeText(int total, int page, int pageSize)
        {
            if (total <= 0) return "Showing 0 of 0";
            int from = page * pageSize + 1;
            int to = Math.Min(total, (page + 1) * pageSize);
            return $"Showing {from}–{to} of {total}";
        }

        // ── Grid ──

        public static void StyleGrid(DataGridView g)
        {
            g.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            g.BackgroundColor = UiKit.T.Surface;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.None;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.GridColor = UiKit.T.LineSoft;

            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AllowUserToOrderColumns = false;
            g.ReadOnly = true;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            g.ScrollBars = ScrollBars.Both;
            g.RowTemplate.Height = RowHeight;
            g.ColumnHeadersHeight = HeaderHeight;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            g.ColumnHeadersDefaultCellStyle.BackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.ForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.Font = UiKit.T.SmallStrong;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(UiKit.T.S3, 0, UiKit.T.S3, 0);
            g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            g.DefaultCellStyle.BackColor = UiKit.T.Surface;
            g.DefaultCellStyle.ForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Font = UiKit.T.Body;
            g.DefaultCellStyle.SelectionBackColor = UiKit.Wash(AppTheme.Primary);
            g.DefaultCellStyle.SelectionForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Padding = new Padding(UiKit.T.S3, 6, UiKit.T.S3, 6);
            g.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            g.RowsDefaultCellStyle.BackColor = UiKit.T.Surface;
        }

        public static void AddActionsColumn(DataGridView dgv, string name = "colActions")
        {
            if (dgv.Columns[name] != null)
                dgv.Columns.Remove(name);

            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = "",
                Width = ActionWidth,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.False,
                ReadOnly = true
            };

            dgv.Columns.Add(col);
            col.DisplayIndex = dgv.Columns.Count - 1;
        }

        // ── Cell painting ──

        public static Color RowBackground(DataGridViewCellPaintingEventArgs e, bool selected, bool hovered)
        {
            if (selected) return e.CellStyle!.SelectionBackColor;
            if (hovered) return UiKit.T.RowHover;
            return UiKit.T.Surface;
        }

        public static void PaintRowShell(Graphics g, Rectangle bounds, Color bg)
        {
            using var b = new SolidBrush(bg);
            g.FillRectangle(b, bounds);
            using var p = new Pen(UiKit.T.LineSoft, 1);
            g.DrawLine(p, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
        }

        public static void PaintHeaderRule(Graphics g, Rectangle bounds)
        {
            using var p = new Pen(UiKit.T.Line, 1);
            g.DrawLine(p, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
        }

        public static void PaintActionCell(Graphics g, Rectangle bounds, bool hovered)
        {
            UiKit.Quality(g);
            var color = hovered ? UiKit.T.InkMuted : UiKit.T.InkFaint;
            UiKit.Text(g, "…", EllipsisFont, color, bounds,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        public static Rectangle PillRect(Rectangle cell, string text)
        {
            var size = UiKit.Measure(text, UiKit.T.SmallStrong);
            int w = Math.Max(64, size.Width + 24);
            int h = 22;
            return new Rectangle(cell.Left + UiKit.T.S3, cell.Top + (cell.Height - h) / 2, w, h);
        }

        public static void PaintPill(Graphics g, Rectangle cell, string text)
        {
            var (fg, bg) = UiKit.StatusColors(text);
            var pill = PillRect(cell, text);
            UiKit.FillRounded(g, pill, UiKit.T.PillRadius, bg);
            UiKit.Text(g, text, UiKit.T.SmallStrong, fg, pill,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        public static void PaintDotText(Graphics g, Rectangle cell, Color dot, string text, Font? font = null)
        {
            UiKit.Dot(g, cell.Left + UiKit.T.S3 + 4, cell.Top + cell.Height / 2f, 7, dot);
            UiKit.Text(g, text, font ?? UiKit.T.Body, UiKit.T.Ink,
                new Rectangle(cell.Left + UiKit.T.S3 + 16, 0 + cell.Top, cell.Width - UiKit.T.S3 - 16, cell.Height),
                UiKit.Left);
        }

        /// <summary>Two-line cell: strong primary on top, muted secondary below. One line-height each.</summary>
        public static void PaintTwoLine(Graphics g, Rectangle cell, string primary, string? secondary)
        {
            int x = cell.Left + UiKit.T.S3;
            int w = cell.Width - UiKit.T.S3 * 2;
            if (string.IsNullOrEmpty(secondary))
            {
                UiKit.Text(g, primary, UiKit.T.Body, UiKit.T.Ink,
                    new Rectangle(x, cell.Top, w, cell.Height), UiKit.LeftWrap);
                return;
            }
            UiKit.Text(g, primary, UiKit.T.BodyStrong, UiKit.T.Ink,
                new Rectangle(x, cell.Top + 6, w, 20), UiKit.LeftWrap);
            UiKit.Text(g, secondary, UiKit.T.Small, UiKit.T.InkMuted,
                new Rectangle(x, cell.Top + 27, w, 19), UiKit.LeftWrap);
        }

        // ── Menus ──

        public static ContextMenuStrip MakeMenu()
        {
            return new ContextMenuStrip
            {
                Font = UiKit.T.Body,
                ShowImageMargin = false,
                BackColor = UiKit.T.Surface,
                ForeColor = UiKit.T.Ink,
                DropShadowEnabled = true,
                RenderMode = ToolStripRenderMode.Professional,
                Renderer = new WorkbenchMenuRenderer()
            };
        }

        public static void StyleMenuItems(ContextMenuStrip menu)
        {
            foreach (ToolStripItem item in menu.Items)
                item.Padding = new Padding(UiKit.T.S2, UiKit.T.S1, UiKit.T.S2, UiKit.T.S1);
        }
    }

    // ═══════════ CARD ═══════════

    [DesignerCategory("Code")]
    public class WorkbenchCard : Panel
    {
        public WorkbenchCard()
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

    // ═══════════ METRIC STRIP ═══════════

    [DesignerCategory("Code")]
    public class WorkbenchMetrics : Control
    {
        private sealed class Item
        {
            public int? Status;
            public Color Accent = Color.Black;
            public Label CapLabel = null!;
            public Label ValLabel = null!;
        }

        private const int SegPad = UiKit.T.S5;
        private const int DotOffset = SegPad + 3;
        private const int CapOffset = SegPad + UiKit.T.S4 - 2;

        private readonly List<Item> _items = new();
        private int _hover = -1;
        private int _selected;

        public event EventHandler? SelectionChanged;

        public int? SelectedStatus =>
            _selected >= 0 && _selected < _items.Count ? _items[_selected].Status : null;

        public WorkbenchMetrics()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = AppTheme.Background;
            Resize += (s, e) => LayoutItems();
        }

        public void AddItem(string caption, int? status, Color accent)
        {
            int index = _items.Count;

            var cap = new Label
            {
                Text = caption,
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            var val = new Label
            {
                Text = "0",
                Font = UiKit.T.Metric,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            cap.Click += (s, e) => Select(index);
            val.Click += (s, e) => Select(index);
            cap.MouseEnter += (s, e) => { _hover = index; Invalidate(); };
            val.MouseEnter += (s, e) => { _hover = index; Invalidate(); };
            cap.MouseLeave += (s, e) => { if (_hover == index) { _hover = -1; Invalidate(); } };
            val.MouseLeave += (s, e) => { if (_hover == index) { _hover = -1; Invalidate(); } };

            Controls.Add(cap);
            Controls.Add(val);

            _items.Add(new Item { Status = status, Accent = accent, CapLabel = cap, ValLabel = val });
            UpdateColors();
            LayoutItems();
        }

        public void SetValue(int index, int value)
        {
            if (index < 0 || index >= _items.Count) return;
            _items[index].ValLabel.Text = value.ToString();
        }

        /// <summary>Silent sync — selects without firing SelectionChanged (no filter loops).</summary>
        public void SelectByValue(int? val)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Status == val)
                {
                    if (_selected != i) { _selected = i; UpdateColors(); Invalidate(); }
                    return;
                }
            }
        }

        /// <summary>Silent sync by index (for tab-driven strips).</summary>
        public void SelectIndex(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            if (_selected != index) { _selected = index; UpdateColors(); Invalidate(); }
        }

        private void Select(int i)
        {
            if (i < 0 || i == _selected) return;
            _selected = i;
            UpdateColors();
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateColors()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                bool active = i == _selected;
                var it = _items[i];
                it.CapLabel.ForeColor = active ? UiKit.T.Ink : UiKit.T.InkMuted;
                it.ValLabel.ForeColor = active ? it.Accent : UiKit.T.Ink;
            }
        }

        private void LayoutItems()
        {
            if (_items.Count == 0 || Width <= 0) return;

            int segW = Width / _items.Count;

            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                int segLeft = i * segW;

                it.CapLabel.Location = new Point(segLeft + CapOffset, UiKit.T.S4);
                it.ValLabel.Location = new Point(segLeft + SegPad, it.CapLabel.Bottom + UiKit.T.S1);
            }
        }

        public int PreferredContentHeight()
        {
            if (_items.Count == 0) return UiKit.T.StripHeight;

            int capH = _items.Max(x => x.CapLabel.Height);
            int valH = _items.Max(x => x.ValLabel.Height);

            return UiKit.T.S4 + capH + UiKit.T.S1 + valH + UiKit.T.S3;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UiKit.Quality(g);

            using (var b = new SolidBrush(AppTheme.Background))
                g.FillRectangle(b, ClientRectangle);

            UiKit.Card(g, ClientRectangle, UiKit.T.Radius, UiKit.T.Surface, UiKit.T.Line);

            if (_items.Count == 0) return;

            int segW = Width / _items.Count;

            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                var seg = new Rectangle(i * segW, 0, segW, Height);
                bool active = i == _selected;
                bool hot = i == _hover;

                if (hot && !active)
                {
                    var wash = Rectangle.Inflate(seg, -UiKit.T.S2, -UiKit.T.S2);
                    UiKit.FillRounded(g, wash, 8, UiKit.T.RowHover);
                }

                if (i > 0)
                {
                    using var pen = new Pen(UiKit.T.Line, 1);
                    g.DrawLine(pen, seg.Left, UiKit.T.S5, seg.Left, Height - UiKit.T.S5);
                }

                UiKit.Dot(g, seg.Left + DotOffset, it.CapLabel.Top + it.CapLabel.Height / 2, 7,
                    active ? it.Accent : UiKit.Mix(it.Accent, Color.White, 0.45));

                if (active)
                {
                    var bar = new Rectangle(seg.Left + SegPad, Height - 4, 28, 2);
                    UiKit.FillRounded(g, bar, 1, it.Accent);
                }
            }
        }
    }

    // ═══════════ SEARCH ═══════════

    [DesignerCategory("Code")]
    public class WorkbenchSearch : Control
    {
        public TextBox Inner { get; }
        private bool _focused;
        private bool _hoverClear;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string PlaceholderText
        {
            get => Inner.PlaceholderText;
            set => Inner.PlaceholderText = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Placeholder
        {
            get => Inner.PlaceholderText;
            set => Inner.PlaceholderText = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public new string Text
        {
            get => Inner.Text;
            set => Inner.Text = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public string Query => Inner.Text;

        public event EventHandler? QueryChanged
        {
            add => Inner.TextChanged += value;
            remove => Inner.TextChanged -= value;
        }

        public WorkbenchSearch()
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
            Inner.TextChanged += (s, e) => Invalidate();

            Controls.Add(Inner);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int left = 36;
            int tbH = Inner.Font.Height + 4;
            int tbY = Math.Max(2, (Height - tbH) / 2);
            Inner.SetBounds(left, tbY, Math.Max(40, Width - left - 36), tbH);
        }

        private Rectangle ClearRect => new Rectangle(Width - 28, (Height - 20) / 2, 20, 20);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hot = Inner.Text.Length > 0 && ClearRect.Contains(e.Location);
            if (hot != _hoverClear) { _hoverClear = hot; Invalidate(); }
            Cursor = hot ? Cursors.Hand : Cursors.IBeam;
            base.OnMouseMove(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (Inner.Text.Length > 0 && ClearRect.Contains(e.Location))
                Inner.Clear();
            Inner.Focus();
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UiKit.Quality(g);

            using (var b = new SolidBrush(UiKit.T.Surface))
                g.FillRectangle(b, ClientRectangle);

            var box = new Rectangle(0, 0, Width, Height);
            UiKit.FillRounded(g, box, 8, UiKit.T.Surface);

            var border = _focused ? AppTheme.Primary : UiKit.T.Line;
            using (var path = UiKit.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 8))
            using (var pen = new Pen(border, _focused ? 1.4f : 1f))
                g.DrawPath(pen, path);

            UiKit.Text(g, IconFont.Search, UiKit.T.Glyph, _focused ? AppTheme.Primary : UiKit.T.InkFaint,
                new Rectangle(10, 0, 20, Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            if (Inner.Text.Length > 0)
            {
                UiKit.Text(g, IconFont.Close, UiKit.T.Glyph, _hoverClear ? UiKit.T.Ink : UiKit.T.InkFaint, ClearRect,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    // ═══════════ SEGMENTED FILTER ═══════════

    /// <summary>
    /// Shared pill-segment filter. Silent <c>SelectByValue</c>/<c>SelectIndex</c>
    /// for cross-control sync (no filter loops); <c>FireSelectIndex</c> when the
    /// selection must drive a tab change (Customer History strip → tabs).
    /// </summary>
    [DesignerCategory("Code")]
    public class WorkbenchSegments<T> : Control
    {
        private readonly (string Label, T Value)[] _items;
        private readonly int[] _widths;
        private int _hover = -1;
        private int _selected;

        public event EventHandler? SelectionChanged;
        public T Selected => _items[_selected].Value;

        public WorkbenchSegments((string, T)[] items)
        {
            _items = items.Select(t => (t.Item1, t.Item2)).ToArray();
            _widths = new int[items.Length];

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = UiKit.T.Surface;
            Cursor = Cursors.Hand;
            Font = UiKit.T.SmallStrong;

            for (int i = 0; i < _items.Length; i++)
                _widths[i] = UiKit.Measure(_items[i].Label, UiKit.T.SmallStrong).Width + UiKit.T.S5;
        }

        public int PreferredWidth => _widths.Sum() + UiKit.T.S1 * 2;

        public void SelectByValue(T val)
        {
            var cmp = EqualityComparer<T>.Default;
            for (int i = 0; i < _items.Length; i++)
            {
                if (cmp.Equals(_items[i].Value, val))
                {
                    if (_selected != i) { _selected = i; Invalidate(); }
                    return;
                }
            }
        }

        public void SelectIndex(int index)
        {
            if (index < 0 || index >= _items.Length) return;
            if (_selected != index) { _selected = index; Invalidate(); }
        }

        public void FireSelectIndex(int index)
        {
            if (index < 0 || index >= _items.Length || index == _selected) return;
            _selected = index;
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private int IndexAt(Point p)
        {
            int x = UiKit.T.S1;
            for (int i = 0; i < _items.Length; i++)
            {
                if (p.X >= x && p.X < x + _widths[i]) return i;
                x += _widths[i];
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = IndexAt(e.Location);
            if (i != _hover) { _hover = i; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = -1; Invalidate(); base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = IndexAt(e.Location);
            if (i >= 0 && i != _selected)
            {
                _selected = i;
                Invalidate();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UiKit.Quality(g);

            using (var b = new SolidBrush(UiKit.T.Surface))
                g.FillRectangle(b, ClientRectangle);

            UiKit.FillRounded(g, new Rectangle(0, 0, Width, Height), 8, UiKit.T.LineSoft);

            int x = UiKit.T.S1;
            for (int i = 0; i < _items.Length; i++)
            {
                var seg = new Rectangle(x, UiKit.T.S1 - 1, _widths[i], Height - (UiKit.T.S1 - 1) * 2);
                bool active = i == _selected;

                if (active)
                {
                    UiKit.FillRounded(g, seg, 6, UiKit.T.Surface);
                    using var pen = new Pen(UiKit.T.Line, 1);
                    using var path = UiKit.Rounded(new Rectangle(seg.X, seg.Y, seg.Width - 1, seg.Height - 1), 6);
                    g.DrawPath(pen, path);
                }

                Color fg = active ? UiKit.T.Ink : (i == _hover ? UiKit.T.InkMuted : UiKit.T.InkFaint);
                UiKit.Text(g, _items[i].Label, UiKit.T.SmallStrong, fg, seg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                x += _widths[i];
            }
        }
    }

    // ═══════════ EMPTY + LOADING ═══════════

    [DesignerCategory("Code")]
    public class WorkbenchState : Control
    {
        private string _glyph = "";
        private string _title = "";
        private string _message = "";
        private bool _loading;

        public WorkbenchState()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = UiKit.T.Surface;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public bool IsLoading => _loading && Visible;

        public void Show(string glyph, string title, string message)
        {
            _glyph = glyph; _title = title; _message = message;
            _loading = false;
            Visible = true;
            BringToFront();
            Invalidate();
        }

        public void ShowLoading(string title = "Loading…", string message = "Fetching the latest records.")
        {
            _glyph = "";
            _title = title; _message = message;
            _loading = true;
            Visible = true;
            BringToFront();
            Invalidate();
        }

        public void Clear()
        {
            _loading = false;
            Visible = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UiKit.Quality(g);

            using (var b = new SolidBrush(UiKit.T.Surface))
                g.FillRectangle(b, ClientRectangle);

            int cy = Height / 2 - 40;

            var circle = new Rectangle(Width / 2 - 26, cy, 52, 52);
            UiKit.FillRounded(g, circle, 26, UiKit.T.LineSoft);
            UiKit.Text(g, _glyph, UiKit.T.GlyphLarge, UiKit.T.InkFaint, circle,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            UiKit.Text(g, _title, UiKit.T.Section, UiKit.T.Ink,
                new Rectangle(0, circle.Bottom + UiKit.T.S4, Width, 24),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);

            int msgW = Math.Min(420, Width - UiKit.T.S6 * 2);
            UiKit.Text(g, _message, UiKit.T.Body, UiKit.T.InkMuted,
                new Rectangle((Width - msgW) / 2, circle.Bottom + UiKit.T.S4 + 28, msgW, 60),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
        }
    }

    // ═══════════ PAGINATION ═══════════

    [DesignerCategory("Code")]
    public class TablePagination : Control
    {
        private int _page;
        private int _total;
        private int _pageSize = TableKit.PageSize;
        private int _hover; // 0 none, 1 prev, 2 next

        public event EventHandler? PageChanged;

        public TablePagination()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = UiKit.T.Surface;
            Height = TableKit.FooterH;
            Cursor = Cursors.Hand;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public int PageSize
        {
            get => _pageSize;
            set { _pageSize = Math.Max(1, value); Clamp(); Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public int TotalCount => _total;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public int CurrentPage => _page;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public int PageCount => _total <= 0 ? 0 : (_total + _pageSize - 1) / _pageSize;

        public void SetTotal(int total)
        {
            _total = Math.Max(0, total);
            Clamp();
            Invalidate();
        }

        public void Reset()
        {
            if (_page != 0) { _page = 0; Invalidate(); }
            else Invalidate();
        }

        public List<T> Slice<T>(IList<T> full)
        {
            if (full.Count == 0) return new List<T>();
            Clamp();
            return full.Skip(_page * _pageSize).Take(_pageSize).ToList();
        }

        private void Clamp()
        {
            int max = Math.Max(0, PageCount - 1);
            if (_page > max) _page = max;
            if (_page < 0) _page = 0;
        }

        private Rectangle PrevRect => new Rectangle(Width - 196, (Height - 28) / 2, 64, 28);
        private Rectangle NextRect => new Rectangle(Width - 124, (Height - 28) / 2, 64, 28);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = PrevRect.Contains(e.Location) ? 1 : NextRect.Contains(e.Location) ? 2 : 0;
            if (h != _hover) { _hover = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = 0; Invalidate(); base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            bool canPrev = _page > 0;
            bool canNext = _page < PageCount - 1;
            if (PrevRect.Contains(e.Location) && canPrev) { _page--; Invalidate(); PageChanged?.Invoke(this, EventArgs.Empty); }
            else if (NextRect.Contains(e.Location) && canNext) { _page++; Invalidate(); PageChanged?.Invoke(this, EventArgs.Empty); }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UiKit.Quality(g);

            using (var b = new SolidBrush(UiKit.T.Surface))
                g.FillRectangle(b, ClientRectangle);
            UiKit.HLine(g, 0, Width, 0, UiKit.T.LineSoft);

            UiKit.Text(g, TableKit.PageRangeText(_total, _page, _pageSize), UiKit.T.Small, UiKit.T.InkMuted,
                new Rectangle(UiKit.T.S1, 0, 320, Height), UiKit.Left);

            string pageTxt = PageCount <= 0 ? "Page 0 of 0" : $"Page {_page + 1} of {PageCount}";
            UiKit.Text(g, pageTxt, UiKit.T.Small, UiKit.T.InkFaint,
                new Rectangle(Width - 320, 0, 116, Height),
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

            bool canPrev = _page > 0;
            bool canNext = _page < PageCount - 1;
            PaintNavButton(g, PrevRect, "‹ Prev", canPrev, _hover == 1);
            PaintNavButton(g, NextRect, "Next ›", canNext, _hover == 2);
        }

        private static void PaintNavButton(Graphics g, Rectangle r, string text, bool enabled, bool hot)
        {
            Color bg = !enabled ? UiKit.T.LineSoft : hot ? UiKit.T.RowHover : UiKit.T.Surface;
            Color fg = !enabled ? UiKit.T.InkFaint : UiKit.T.Ink;
            UiKit.FillRounded(g, r, 6, bg);
            using var pen = new Pen(UiKit.T.Line, 1);
            using var path = UiKit.Rounded(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), 6);
            g.DrawPath(pen, path);
            UiKit.Text(g, text, UiKit.T.SmallStrong, fg, r,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    // ═══════════ MENU RENDERER ═══════════

    public class WorkbenchMenuRenderer : ToolStripProfessionalRenderer
    {
        public WorkbenchMenuRenderer() : base(new Colors()) { RoundedEdges = false; }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var r = new Rectangle(4, 0, e.Item.Width - 8, e.Item.Height);
            if (e.Item.Selected && e.Item.Enabled)
                UiKit.FillRounded(e.Graphics, r, 6, UiKit.T.RowHover);
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
            public override Color MenuItemBorder => Color.Transparent;
            public override Color ImageMarginGradientBegin => UiKit.T.Surface;
            public override Color ImageMarginGradientMiddle => UiKit.T.Surface;
            public override Color ImageMarginGradientEnd => UiKit.T.Surface;
        }
    }
}
