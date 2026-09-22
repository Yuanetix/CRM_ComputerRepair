using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Super Admin use case — read the audit log of all system actions.
    /// </summary>
    [DesignerCategory("Code")]
    public class SystemMonitorControl : UserControl
    {
        private readonly ApiClient _api = new ApiClient();
        private List<AuditLogDto> _all = new();

        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private FlatButton btnRefresh = null!;

        private SurfaceCard card = null!;
        private Label lblGridTitle = null!;
        private Label lblCount = null!;
        private TextField search = null!;

        private DataGridView dgv = null!;
        private StateView state = null!;

        private int _hoverRow = -1;

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
            lblTitle = new Label
            {
                Text = "System Monitor",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Audit log of all create, update, and archive actions",
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

            card = new SurfaceCard();

            lblGridTitle = new Label
            {
                Text = "Recent activity",
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

            search = new TextField { PlaceholderText = "Filter by user, action or entity" };
            search.InnerTextBox.TextChanged += (s, e) => ApplyFilter();

            dgv = new DataGridView();
            StyleGrid(dgv);
            dgv.CellMouseEnter += Dgv_CellMouseEnter;
            dgv.CellMouseLeave += Dgv_CellMouseLeave;
            dgv.MouseLeave += (s, e) => { _hoverRow = -1; dgv.Invalidate(); };

            state = new StateView { Visible = false };

            card.Controls.Add(lblGridTitle);
            card.Controls.Add(lblCount);
            card.Controls.Add(search);
            card.Controls.Add(dgv);
            card.Controls.Add(state);

            Controls.Add(card);

            Resize += (s, e) => LayoutUi();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            UiKit.Quality(e.Graphics);
            int y = lblSubtitle.Bottom + UiKit.T.S4;
            using var pen = new Pen(UiKit.T.Line, 1);
            e.Graphics.DrawLine(pen, 0, y, Width, y);
        }

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            lblTitle.Location = new Point(0, 0);
            int subtitleY = lblTitle.PreferredHeight + UiKit.T.S1;
            lblSubtitle.Location = new Point(1, subtitleY);

            btnRefresh.Size = new Size(btnRefresh.PreferredWidth, UiKit.T.ButtonHeight);
            btnRefresh.Location = new Point(Width - btnRefresh.Width, UiKit.T.S1);

            int dividerY = subtitleY + lblSubtitle.PreferredHeight + UiKit.T.S4;
            int cardTop = dividerY + UiKit.T.S5;
            int cardH = Math.Max(240, Height - cardTop);

            card.Location = new Point(0, cardTop);
            card.Size = new Size(Width, cardH);

            const int cp = UiKit.T.S5;
            lblGridTitle.Location = new Point(cp, UiKit.T.S5 - 2);
            lblCount.Location = new Point(
                lblGridTitle.Right + UiKit.T.S2,
                lblGridTitle.Top + lblGridTitle.PreferredHeight - lblCount.PreferredHeight - 2);

            int toolbarY = lblGridTitle.Bottom + UiKit.T.S4;
            int searchW = Math.Min(320, Math.Max(180, card.Width - cp * 2));
            search.Size = new Size(searchW, UiKit.T.InputHeight);
            search.Location = new Point(card.Width - cp - searchW, toolbarY);

            int gridTop = toolbarY + UiKit.T.InputHeight + UiKit.T.S4;
            int gridW = card.Width - cp * 2;
            int gridH = card.Height - gridTop - cp;

            if (gridW > 100 && gridH > 60)
            {
                dgv.Location = new Point(cp, gridTop);
                dgv.Size = new Size(gridW, gridH);
                state.Location = new Point(cp, gridTop);
                state.Size = new Size(gridW, gridH);
            }
        }

        public async Task ReloadAsync()
        {
            try
            {
                _all = await _api.GetAuditLogAsync(null, null, 500);
                ApplyFilter();
            }
            catch (Exception ex)
            {
                _all = new List<AuditLogDto>();
                ApplyFilter();

                MessageBox.Show($"Couldn't load audit log.\n\n{ex.Message}",
                    "Connection problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyFilter()
        {
            var term = search.Text?.Trim() ?? "";

            IEnumerable<AuditLogDto> q = _all;

            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(x =>
                    (x.UserDisplay ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Action ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Entity ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.EntityDisplay ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Details ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            var view = q.ToList();

            dgv.DataSource = null;
            dgv.DataSource = view;
            ConfigureColumns();

            lblCount.Text = view.Count == _all.Count
                ? $"{view.Count} {(view.Count == 1 ? "entry" : "entries")}"
                : $"{view.Count} of {_all.Count}";

            if (view.Count > 0)
            {
                state.Visible = false;
                dgv.Visible = true;
                dgv.ClearSelection();
            }
            else
            {
                dgv.Visible = false;
                state.Show("\uE9D9", "No activity found",
                    string.IsNullOrEmpty(term)
                        ? "Nothing has been logged yet. Create or edit a customer or repair request to generate activity."
                        : $"Nothing matches \u201c{term}\u201d.");
            }
        }

        private static void StyleGrid(DataGridView g)
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
            g.ReadOnly = true;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
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

        private void ConfigureColumns()
        {
            foreach (var hidden in new[]
            {
                "AuditLogId", "UserId", "Entity", "EntityId", "Timestamp"
            })
            {
                if (dgv.Columns[hidden] != null)
                    dgv.Columns[hidden].Visible = false;
            }

            void Setup(string name, string header, int width, int idx, bool fill = false)
            {
                var c = dgv.Columns[name];
                if (c == null) return;
                c.HeaderText = header;
                c.SortMode = DataGridViewColumnSortMode.Automatic;
                c.AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None;
                if (!fill) c.Width = width;
                else c.MinimumWidth = 180;
                c.DisplayIndex = idx;
            }

            Setup("WhenDisplay", "When", 180, 0);
            Setup("UserDisplay", "User", 130, 1);
            Setup("Action", "Action", 110, 2);
            Setup("EntityDisplay", "Entity", 170, 3);
            Setup("Details", "Details", 0, 4, fill: true);

            if (dgv.Columns["Action"] != null)
                dgv.Columns["Action"].DefaultCellStyle.Font = UiKit.T.BodyStrong;
        }

        private void Dgv_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) { _hoverRow = -1; return; }
            _hoverRow = e.RowIndex;
            dgv.InvalidateRow(e.RowIndex);
        }

        private void Dgv_CellMouseLeave(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            _hoverRow = -1;
            dgv.InvalidateRow(e.RowIndex);
        }

        // ═══════════ NESTED UI ═══════════

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

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);
                using (var b = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(b, ClientRectangle);

                Color bg = _down ? AppTheme.PrimaryActive : _hover ? AppTheme.PrimaryHover : AppTheme.Primary;
                UiKit.FillRounded(g, ClientRectangle, 8, bg);

                UiKit.Text(g, _glyph, new Font("Segoe MDL2 Assets", 10F), Color.White,
                    new Rectangle(16, 0, 18, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                UiKit.Text(g, Text, UiKit.T.BodyStrong, Color.White,
                    new Rectangle(36, 0, Width - 46, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        [DesignerCategory("Code")]
        private sealed class StateView : Control
        {
            private string _glyph = "";
            private string _title = "";
            private string _message = "";

            public StateView()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = UiKit.T.Surface;
            }

            public void Show(string glyph, string title, string message)
            {
                _glyph = glyph; _title = title; _message = message;
                Visible = true; BringToFront(); Invalidate();
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
    }
}