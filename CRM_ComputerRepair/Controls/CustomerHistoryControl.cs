using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Staff use case — full history for one customer.
    /// Shows profile + repairs + interactions + follow-ups.
    /// </summary>
    [DesignerCategory("Code")]
    public class CustomerHistoryControl : UserControl
    {
        private readonly ApiClient _api = new ApiClient();

        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private FlatButton btnLoad = null!;
        private FlatButton btnReload = null!;
        private TextField inpCustomerId = null!;

        private SurfaceCard card = null!;
        private Label lblName = null!;
        private Label lblMeta = null!;
        private Label lblCounts = null!;

        private TabControl tabs = null!;
        private DataGridView dgvRepairs = null!;
        private DataGridView dgvInteractions = null!;
        private DataGridView dgvFollowUps = null!;
        private StateView state = null!;

        public CustomerHistoryControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            AutoScaleMode = AutoScaleMode.Font;

            BuildUi();

            this.Load += (s, e) => LayoutUi();
        }

        private void BuildUi()
        {
            lblTitle = new Label
            {
                Text = "Customer History",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Complete record of repairs, interactions and follow-ups for one customer",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            inpCustomerId = new TextField
            {
                PlaceholderText = "Customer ID (e.g. 1)",
                Size = new Size(180, 34)
            };

            btnLoad = new FlatButton("Load", "\uE721");
            btnLoad.Click += async (s, e) => await LoadFromSelection();

            btnReload = new FlatButton("Refresh", "\uE72C");
            btnReload.Click += async (s, e) => await LoadFromSelection();

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(inpCustomerId);
            Controls.Add(btnLoad);
            Controls.Add(btnReload);

            // ── Card ──
            card = new SurfaceCard();

            lblName = new Label
            {
                Text = "No customer loaded",
                Font = new Font("Segoe UI Semibold", 14F),
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(UiKit.T.S5, UiKit.T.S5)
            };
            card.Controls.Add(lblName);

            lblMeta = new Label
            {
                Text = "",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(UiKit.T.S5, UiKit.T.S5 + 28)
            };
            card.Controls.Add(lblMeta);

            lblCounts = new Label
            {
                Text = "",
                Font = UiKit.T.SmallStrong,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            card.Controls.Add(lblCounts);

            tabs = new TabControl
            {
                Font = UiKit.T.Body,
                ItemSize = new Size(150, 30),
                SizeMode = TabSizeMode.Fixed,
                Padding = new Point(12, 6)
            };

            dgvRepairs = MakeGrid();
            dgvInteractions = MakeGrid();
            dgvFollowUps = MakeGrid();

            var pageRepairs = new TabPage("Repairs") { BackColor = UiKit.T.Surface };
            dgvRepairs.Dock = DockStyle.Fill;
            pageRepairs.Controls.Add(dgvRepairs);

            var pageInteractions = new TabPage("Interactions") { BackColor = UiKit.T.Surface };
            dgvInteractions.Dock = DockStyle.Fill;
            pageInteractions.Controls.Add(dgvInteractions);

            var pageFollowUps = new TabPage("Follow-ups") { BackColor = UiKit.T.Surface };
            dgvFollowUps.Dock = DockStyle.Fill;
            pageFollowUps.Controls.Add(dgvFollowUps);

            tabs.TabPages.Add(pageRepairs);
            tabs.TabPages.Add(pageInteractions);
            tabs.TabPages.Add(pageFollowUps);

            card.Controls.Add(tabs);

            state = new StateView { Visible = false };
            card.Controls.Add(state);

            Controls.Add(card);

            Resize += (s, e) => LayoutUi();
        }

        private static DataGridView MakeGrid()
        {
            var g = new DataGridView
            {
                BackgroundColor = UiKit.T.Surface,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                EnableHeadersVisualStyles = false,
                GridColor = UiKit.T.LineSoft,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.Vertical
            };

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

            return g;
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

            int rightX = Width;
            btnReload.Size = new Size(btnReload.PreferredWidth, UiKit.T.ButtonHeight);
            btnReload.Location = new Point(rightX - btnReload.Width, UiKit.T.S1);
            rightX -= btnReload.Width + 8;

            btnLoad.Size = new Size(btnLoad.PreferredWidth, UiKit.T.ButtonHeight);
            btnLoad.Location = new Point(rightX - btnLoad.Width, UiKit.T.S1);
            rightX -= btnLoad.Width + 8;

            inpCustomerId.Location = new Point(rightX - inpCustomerId.Width, UiKit.T.S1);

            int dividerY = subtitleY + lblSubtitle.PreferredHeight + UiKit.T.S4;
            int cardTop = dividerY + UiKit.T.S5;
            int cardH = Math.Max(300, Height - cardTop);

            card.Location = new Point(0, cardTop);
            card.Size = new Size(Width, cardH);

            lblCounts.Location = new Point(
                lblName.Right + UiKit.T.S3,
                lblName.Top + lblName.PreferredHeight - lblCounts.PreferredHeight - 2);

            int innerTop = UiKit.T.S5 + 60;
            tabs.Location = new Point(UiKit.T.S5, innerTop);
            tabs.Size = new Size(
                Math.Max(100, card.Width - UiKit.T.S5 * 2),
                Math.Max(100, card.Height - innerTop - UiKit.T.S5));

            state.Location = new Point(UiKit.T.S5, innerTop);
            state.Size = new Size(
                Math.Max(100, card.Width - UiKit.T.S5 * 2),
                Math.Max(100, card.Height - innerTop - UiKit.T.S5));
        }

        private async Task LoadFromSelection()
        {
            if (!int.TryParse(inpCustomerId.Text?.Trim(), out var id) || id <= 0)
            {
                MessageBox.Show("Enter a valid customer ID (number > 0).",
                    "Invalid input", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var data = await _api.GetCustomerHistoryAsync(id);

                if (data == null)
                {
                    SetEmpty("Customer not found", $"No customer with ID {id}.");
                    return;
                }

                lblName.Text = data.FullName;
                lblMeta.Text = $"{data.Email ?? "—"}  ·  {data.Phone ?? "—"}  ·  {data.Address ?? "—"}";
                lblCounts.Text = $"{data.Repairs.Count} repairs  ·  {data.Interactions.Count} interactions  ·  {data.FollowUps.Count} follow-ups";

                dgvRepairs.DataSource = null;
                dgvRepairs.DataSource = data.Repairs;
                StyleRepairs();

                dgvInteractions.DataSource = null;
                dgvInteractions.DataSource = data.Interactions;
                StyleInteractions();

                dgvFollowUps.DataSource = null;
                dgvFollowUps.DataSource = data.FollowUps;
                StyleFollowUps();

                tabs.Visible = true;
                state.Visible = false;

                LayoutUi();
            }
            catch (Exception ex)
            {
                SetEmpty("Connection problem", ex.Message);
            }
        }

        private void StyleRepairs()
        {
            Hide("RepairRequestId", "Priority", "CompletionDate", "ActualCost");
            Header("RequestNumber", "Request #");
            Header("DeviceModel", "Device");
            Header("IssueDescription", "Issue");
            Header("StatusText", "Status");
            Header("PriorityText", "Priority");
            Header("RequestDate", "Requested");
            Header("CostDisplay", "Cost");
        }

        private void StyleInteractions()
        {
            Hide("CustomerInteractionId", "InteractionType", "Status", "Priority",
                 "Resolution", "ClosedAt");
            Header("TypeText", "Type");
            Header("Subject", "Subject");
            Header("StatusText", "Status");
            Header("Notes", "Notes");
            Header("InteractionDate", "Date");
        }

        private void StyleFollowUps()
        {
            Hide("FollowUpId", "Channel", "Status", "CompletedAt");
            Header("ChannelText", "Channel");
            Header("Subject", "Subject");
            Header("StatusText", "Status");
            Header("Notes", "Notes");
            Header("ScheduledAt", "Scheduled");
        }

        private void Hide(params string[] names)
        {
            foreach (var n in names)
                if (dgvRepairs.Columns[n] != null || dgvInteractions.Columns[n] != null || dgvFollowUps.Columns[n] != null)
                {
                    if (dgvRepairs.Columns[n] != null) dgvRepairs.Columns[n].Visible = false;
                    if (dgvInteractions.Columns[n] != null) dgvInteractions.Columns[n].Visible = false;
                    if (dgvFollowUps.Columns[n] != null) dgvFollowUps.Columns[n].Visible = false;
                }
        }

        private static void Header(string name, string header) { }

        private void SetEmpty(string title, string message)
        {
            lblName.Text = "—";
            lblMeta.Text = "";
            lblCounts.Text = "";
            tabs.Visible = false;
            state.Show("\uE721", title, message);
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
                Visible = true;
                BringToFront();
                Invalidate();
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