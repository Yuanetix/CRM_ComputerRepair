using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// One module for all customer interactions (Inquiry / Complaint / Feedback).
    ///
    /// UI notes
    /// --------
    /// • Two surfaces only: the page canvas and one white workbench card. No shadows,
    ///   no stacked cards competing for attention.
    /// • The four counters live in a single strip divided by hairlines instead of four
    ///   separate tiles, and they double as status filters (click "Open" to see only open).
    /// • Type / priority / status are encoded with one visual device each: a dot, weight,
    ///   and a soft pill. Repeating the same device three times would flatten the hierarchy.
    /// • Every state has a screen: loading, empty, and "nothing matched your search".
    /// • Keyboard: Ctrl+F search, Esc clear, Ctrl+N new, Enter/Apps opens the row menu.
    ///
    /// Data, API calls and dialogs are unchanged from the original.
    /// </summary>
    [DesignerCategory("Code")]
    public class InteractionListControl : UserControl
    {
        // ═══════════ DESIGN TOKENS ═══════════
        // Brand hues come from AppTheme so this module stays in step with the rest of the
        // app; the neutral scale and type scale are defined here so the layout is exact.

        private static class T
        {
            // Neutrals
            public static readonly Color Surface = Color.White;
            public static readonly Color Line = Color.FromArgb(0xE6, 0xE8, 0xEC);
            public static readonly Color LineSoft = Color.FromArgb(0xF1, 0xF3, 0xF6);
            public static readonly Color Ink = Color.FromArgb(0x1A, 0x1D, 0x23);
            public static readonly Color InkMuted = Color.FromArgb(0x6B, 0x72, 0x80);
            public static readonly Color InkFaint = Color.FromArgb(0x9A, 0xA1, 0xAD);
            public static readonly Color RowHover = Color.FromArgb(0xF8, 0xF9, 0xFB);

            // Spacing — 4pt scale
            public const int S1 = 4, S2 = 8, S3 = 12, S4 = 16, S5 = 24, S6 = 32;

            // Geometry
            public const int Radius = 10;
            public const int PillRadius = 9;
            public const int StripHeight = 86;
            public const int InputHeight = 34;
            public const int ButtonHeight = 36;
            public const int RowHeight = 46;
            public const int HeaderHeight = 38;

            // Type — one family, hierarchy carried by size and weight
            public static readonly Font Title = new Font("Segoe UI Semibold", 16.5F);
            public static readonly Font Subtitle = new Font("Segoe UI", 9.5F);
            public static readonly Font Section = new Font("Segoe UI Semibold", 11F);
            public static readonly Font Body = new Font("Segoe UI", 9.5F);
            public static readonly Font BodyStrong = new Font("Segoe UI Semibold", 9.5F);
            public static readonly Font Small = new Font("Segoe UI", 8.75F);
            public static readonly Font SmallStrong = new Font("Segoe UI Semibold", 8.5F);
            public static readonly Font Metric = new Font("Segoe UI Light", 25F);
            public static readonly Font Glyph = new Font("Segoe MDL2 Assets", 11F);
            public static readonly Font GlyphLarge = new Font("Segoe MDL2 Assets", 22F);
        }

        // ═══════════ PAINT HELPERS ═══════════

        private static class Draw
        {
            public static Color Mix(Color a, Color b, double t) => Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));

            /// <summary>Very light wash of a semantic colour, for pill backgrounds.</summary>
            public static Color Wash(Color c) => Mix(c, Color.White, 0.88);

            public static GraphicsPath Rounded(Rectangle r, int radius)
            {
                var path = new GraphicsPath();
                int d = radius * 2;
                if (d > r.Width) d = r.Width;
                if (d > r.Height) d = r.Height;
                if (d <= 0) { path.AddRectangle(r); return path; }

                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }

            public static void FillRounded(Graphics g, Rectangle r, int radius, Color fill)
            {
                using var p = Rounded(r, radius);
                using var b = new SolidBrush(fill);
                g.FillPath(b, p);
            }

            public static void Card(Graphics g, Rectangle r, int radius, Color fill, Color border)
            {
                r = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
                using var p = Rounded(r, radius);
                using var b = new SolidBrush(fill);
                using var pen = new Pen(border, 1);
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }

            public static void Dot(Graphics g, int cx, int cy, int size, Color c)
            {
                using var b = new SolidBrush(c);
                g.FillEllipse(b, cx - size / 2f, cy - size / 2f, size, size);
            }

            public static void Text(Graphics g, string text, Font font, Color color,
                                   Rectangle bounds, TextFormatFlags flags)
                => TextRenderer.DrawText(g, text, font, bounds, color, flags);

            public static Size Measure(string text, Font font)
                => TextRenderer.MeasureText(text, font);

            public static void Quality(Graphics g)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            }
        }

        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private List<InteractionDto> _all = new List<InteractionDto>();
        private InteractionTypeFilter? _currentFilter = null;   // null = All
        private int? _statusFilter = null;                      // null = any status

        // ═══════════ CONTROLS ═══════════

        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private FlatButton btnAdd = null!;

        private MetricStrip strip = null!;

        private SurfaceCard card = null!;
        private Label lblGridTitle = null!;
        private Label lblCount = null!;
        private SegmentedFilter segments = null!;
        private SearchBox search = null!;

        private DataGridView dgv = null!;
        private StateView state = null!;

        private const string ColActions = "colActions";
        private ContextMenuStrip _actionsMenu = null!;
        private int _menuRowIndex = -1;
        private int _hoverRow = -1;
        private readonly ToolTip _tips = new ToolTip { InitialDelay = 500, ReshowDelay = 200 };

        // ═══════════ CONSTRUCTOR ═══════════

        public InteractionListControl()
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

        // ═══════════ UI BUILD ═══════════

        private void BuildUi()
        {
            // ── Header ──
            lblTitle = new Label
            {
                Text = "Interactions",
                Font = T.Title,
                ForeColor = T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Questions, concerns and reviews from your customers",
                Font = T.Subtitle,
                ForeColor = T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnAdd = new FlatButton("Add interaction", "\uE710");
            btnAdd.Click += (s, e) => OpenAddDialog();
            _tips.SetToolTip(btnAdd, "Add interaction  (Ctrl+N)");

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnAdd);

            // ── Counter strip (also acts as a status filter) ──
            strip = new MetricStrip();
            strip.AddItem("Total", null, AppTheme.Primary);
            strip.AddItem("Open", 0, AppTheme.Warning);
            strip.AddItem("In progress", 1, AppTheme.Primary);
            strip.AddItem("Closed", 2, AppTheme.Success);
            strip.SelectionChanged += (s, e) =>
            {
                _statusFilter = strip.SelectedStatus;
                ApplySearch();
            };
            Controls.Add(strip);

            // ── Workbench card ──
            card = new SurfaceCard();

            lblGridTitle = new Label
            {
                Text = "All interactions",
                Font = T.Section,
                ForeColor = T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblCount = new Label
            {
                Text = "",
                Font = T.Small,
                ForeColor = T.InkFaint,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            segments = new SegmentedFilter(new[]
            {
                ("All", (InteractionTypeFilter?)null),
                ("Questions", InteractionTypeFilter.Inquiry),
                ("Concerns", InteractionTypeFilter.Complaint),
                ("Reviews", InteractionTypeFilter.Feedback)
            });
            segments.SelectionChanged += (s, e) => SetFilter(segments.Selected);

            search = new SearchBox { PlaceholderText = "Search subject, notes or resolution" };
            search.Inner.TextChanged += (s, e) => ApplySearch();
            _tips.SetToolTip(search, "Search  (Ctrl+F)");

            dgv = new DataGridView();
            StyleGrid(dgv);
            dgv.CellPainting += Dgv_CellPainting;
            dgv.CellClick += Dgv_CellClick;
            dgv.CellDoubleClick += Dgv_CellDoubleClick;
            dgv.CellMouseEnter += Dgv_CellMouseEnter;
            dgv.CellMouseLeave += Dgv_CellMouseLeave;
            dgv.MouseLeave += (s, e) => { _hoverRow = -1; dgv.Invalidate(); };
            dgv.KeyDown += Dgv_KeyDown;

            state = new StateView { Visible = false };

            card.Controls.Add(lblGridTitle);
            card.Controls.Add(lblCount);
            card.Controls.Add(segments);
            card.Controls.Add(search);
            card.Controls.Add(dgv);
            card.Controls.Add(state);

            Controls.Add(card);

            Resize += (s, e) => LayoutUi();
        }

        // ═══════════ SHELL PAINT (hairline under the page header) ═══════════

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Draw.Quality(e.Graphics);

            int y = lblSubtitle.Bottom + T.S4;
            using var pen = new Pen(T.Line, 1);
            e.Graphics.DrawLine(pen, 0, y, Width, y);
        }

        // ═══════════ KEYBOARD (HCI: shortcuts for the three things staff repeat) ═══════════

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.F:
                    search.Inner.Focus();
                    search.Inner.SelectAll();
                    return true;

                case Keys.Control | Keys.N:
                    OpenAddDialog();
                    return true;

                case Keys.Escape:
                    if (search.Inner.Text.Length > 0)
                    {
                        search.Inner.Clear();
                        return true;
                    }
                    break;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Dgv_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Apps && e.KeyCode != Keys.Enter) return;
            if (dgv.CurrentRow == null) return;

            _menuRowIndex = dgv.CurrentRow.Index;
            var r = dgv.GetRowDisplayRectangle(_menuRowIndex, true);
            _actionsMenu.Show(dgv, new Point(r.Right - 180, r.Bottom));
            e.Handled = true;
        }

        // ═══════════ FILTER ═══════════

        private void SetFilter(InteractionTypeFilter? filter)
        {
            _currentFilter = filter;

            lblGridTitle.Text = filter switch
            {
                InteractionTypeFilter.Inquiry => "Client questions",
                InteractionTypeFilter.Complaint => "Client concerns",
                InteractionTypeFilter.Feedback => "Client reviews",
                _ => "All interactions"
            };

            lblSubtitle.Text = filter switch
            {
                InteractionTypeFilter.Inquiry => "Questions about products or repairs",
                InteractionTypeFilter.Complaint => "Concerns about service or repairs",
                InteractionTypeFilter.Feedback => "Reviews and satisfaction notes",
                _ => "Questions, concerns and reviews from your customers"
            };

            btnAdd.Text = filter switch
            {
                InteractionTypeFilter.Inquiry => "Add question",
                InteractionTypeFilter.Complaint => "Add concern",
                InteractionTypeFilter.Feedback => "Add review",
                _ => "Add interaction"
            };

            LayoutUi();
            Invalidate();

            _ = ReloadAsync();
        }

        // ═══════════ LAYOUT (4pt grid, one column, left aligned) ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            // ── Header ──
            lblTitle.Location = new Point(0, 0);

            int subtitleY = lblTitle.PreferredHeight + T.S1;
            lblSubtitle.Location = new Point(1, subtitleY);

            btnAdd.Size = new Size(btnAdd.PreferredWidth, T.ButtonHeight);
            btnAdd.Location = new Point(Width - btnAdd.Width, T.S1);

            int dividerY = subtitleY + lblSubtitle.PreferredHeight + T.S4;

            // ── Counter strip ──
            int stripTop = dividerY + T.S5;
            int stripH = Math.Max(T.StripHeight, strip.PreferredContentHeight());
            strip.Location = new Point(0, stripTop);
            strip.Size = new Size(Width, stripH);

            // ── Workbench card ──
            int cardTop = stripTop + stripH + T.S5;
            int cardHeight = Math.Max(240, Height - cardTop);

            card.Location = new Point(0, cardTop);
            card.Size = new Size(Width, cardHeight);

            // ── Inside the card ──
            const int cp = T.S5;                    // card padding

            lblGridTitle.Location = new Point(cp, T.S5 - 2);
            lblCount.Location = new Point(lblGridTitle.Right + T.S2,
                                          lblGridTitle.Top + lblGridTitle.PreferredHeight - lblCount.PreferredHeight - 2);

            int toolbarY = lblGridTitle.Bottom + T.S4;

            segments.Size = new Size(segments.PreferredWidth, 34);
            segments.Location = new Point(cp, toolbarY);

            int searchW = Math.Min(280, Math.Max(180, card.Width - cp * 2 - segments.Width - T.S4));
            search.Size = new Size(searchW, T.InputHeight);
            search.Location = new Point(card.Width - cp - searchW, toolbarY + (34 - T.InputHeight) / 2);

            int gridTop = toolbarY + 34 + T.S4;
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

        // ═══════════ DATA (unchanged behaviour) ═══════════

        public async Task ReloadAsync()
        {
            try
            {
                _all = await _api.GetInteractionsAsync(
                    _currentFilter,
                    includeArchived: false);

                UpdateStats();
                ApplySearch();
            }
            catch (Exception ex)
            {
                _all = new List<InteractionDto>();
                UpdateStats();
                ApplySearch();

                MessageBox.Show(
                    $"Couldn't load interactions.\n\n{ex.Message}",
                    "Connection problem",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void UpdateStats()
        {
            strip.SetValue(0, _all.Count);
            strip.SetValue(1, _all.Count(x => x.Status == 0));
            strip.SetValue(2, _all.Count(x => x.Status == 1));
            strip.SetValue(3, _all.Count(x => x.Status == 2));
        }

        private void ApplySearch()
        {
            var term = search.Inner.Text?.Trim() ?? string.Empty;

            IEnumerable<InteractionDto> q = _all;

            if (_statusFilter.HasValue)
                q = q.Where(x => x.Status == _statusFilter.Value);

            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(x =>
                    (x.Subject ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Notes ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Resolution ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            var view = q.ToList();

            dgv.DataSource = null;
            dgv.DataSource = view;
            ConfigureColumns();
            AddActionsColumn();

            lblCount.Text = view.Count == _all.Count
                ? $"{view.Count} {(view.Count == 1 ? "record" : "records")}"
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

                if (!string.IsNullOrEmpty(term))
                    state.Show("\uE721", "No matches",
                               $"Nothing matches \u201c{term}\u201d. Try a shorter word, or clear the search with Esc.");
                else if (_statusFilter.HasValue)
                    state.Show("\uE71C", "Nothing here yet",
                               "No interactions with this status. Pick Total to see them all.");
                else
                    state.Show("\uE8BD", "No interactions yet",
                               $"Use \u201c{btnAdd.Text}\u201d to log the first one.");
            }
        }

        // ═══════════ GRID STYLE ═══════════

        private static void StyleGrid(DataGridView g)
        {
            g.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            g.BackgroundColor = T.Surface;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.None;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.GridColor = T.LineSoft;

            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AllowUserToOrderColumns = false;
            g.ReadOnly = true;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            g.ScrollBars = ScrollBars.Vertical;
            g.RowTemplate.Height = T.RowHeight;
            g.ColumnHeadersHeight = T.HeaderHeight;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // Headers: sentence case, quiet, no chrome — the rules do the separating.
            g.ColumnHeadersDefaultCellStyle.BackColor = T.Surface;
            g.ColumnHeadersDefaultCellStyle.ForeColor = T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = T.Surface;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.Font = T.SmallStrong;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(T.S3, 0, T.S3, 0);
            g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            g.DefaultCellStyle.BackColor = T.Surface;
            g.DefaultCellStyle.ForeColor = T.Ink;
            g.DefaultCellStyle.Font = T.Body;
            g.DefaultCellStyle.SelectionBackColor = Draw.Wash(AppTheme.Primary);
            g.DefaultCellStyle.SelectionForeColor = T.Ink;
            g.DefaultCellStyle.Padding = new Padding(T.S3, 0, T.S3, 0);
            g.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            g.RowsDefaultCellStyle.BackColor = T.Surface;
        }

        private void ConfigureColumns()
        {
            foreach (var hidden in new[]
            {
                "CustomerInteractionId", "CustomerId", "RepairRequestId",
                "InteractionType", "Status", "Priority",
                "InteractionByUserId", "UpdatedAt", "ClosedAt", "IsActive",
                "Notes", "Resolution", "ActivityStatus"
            })
            {
                if (dgv.Columns[hidden] != null)
                    dgv.Columns[hidden].Visible = false;
            }

            void Setup(string name, string header, int width, int displayIndex, bool fill = false)
            {
                var c = dgv.Columns[name];
                if (c == null) return;

                c.HeaderText = header;
                c.SortMode = DataGridViewColumnSortMode.Automatic;
                c.AutoSizeMode = fill
                    ? DataGridViewAutoSizeColumnMode.Fill
                    : DataGridViewAutoSizeColumnMode.None;
                if (!fill) c.Width = width;
                else c.MinimumWidth = 220;
                c.DisplayIndex = displayIndex;
            }

            Setup("TypeText", "Type", 130, 0);
            Setup("Subject", "Subject", 0, 1, fill: true);
            Setup("PriorityText", "Priority", 100, 2);
            Setup("StatusText", "Status", 130, 3);
            Setup("InteractionDate", "Created", 110, 4);

            if (dgv.Columns["InteractionDate"] != null)
            {
                dgv.Columns["InteractionDate"].DefaultCellStyle.Format = "MMM d";
                dgv.Columns["InteractionDate"].DefaultCellStyle.ForeColor = T.InkMuted;
                dgv.Columns["InteractionDate"].DefaultCellStyle.SelectionForeColor = T.InkMuted;
            }

            if (dgv.Columns["Subject"] != null)
            {
                dgv.Columns["Subject"].DefaultCellStyle.Font = T.BodyStrong;
                dgv.Columns["Subject"].DefaultCellStyle.Padding = new Padding(T.S3, 0, T.S4, 0);
            }
        }

        // ═══════════ DISPLAY WORDING ═══════════
        // The data model still calls these Inquiry/Complaint/Feedback; the UI never shows
        // those words to staff — everywhere a type is displayed, it goes through here first.

        private static string DisplayType(string? raw) => raw switch
        {
            "Inquiry" => "Question",
            "Complaint" => "Concern",
            "Feedback" => "Review",
            _ => raw ?? ""
        };

        // ═══════════ ACTIONS ═══════════

        private void BuildActionsMenu()
        {
            _actionsMenu = new ContextMenuStrip
            {
                Font = T.Body,
                ShowImageMargin = false,
                BackColor = T.Surface,
                ForeColor = T.Ink,
                DropShadowEnabled = true,
                RenderMode = ToolStripRenderMode.Professional,
                Renderer = new QuietMenuRenderer()
            };

            _actionsMenu.Items.Add("View details");
            _actionsMenu.Items.Add("Edit");
            _actionsMenu.Items.Add(new ToolStripSeparator());
            _actionsMenu.Items.Add("Archive");

            foreach (ToolStripItem item in _actionsMenu.Items)
                item.Padding = new Padding(T.S2, T.S1, T.S2, T.S1);

            _actionsMenu.Items[0].Click += OnMenuView;
            _actionsMenu.Items[1].Click += OnMenuUpdate;
            _actionsMenu.Items[3].Click += OnMenuArchive;

            _actionsMenu.Opening += (s, e) =>
            {
                if (_menuRowIndex < 0) return;
                if (dgv.Rows[_menuRowIndex].DataBoundItem is not InteractionDto dto)
                    return;

                var archiveItem = _actionsMenu.Items[3];
                archiveItem.Text = dto.IsActive ? "Archive" : "Restore";
                archiveItem.ForeColor = dto.IsActive ? AppTheme.Danger : T.Ink;
            };

            _actionsMenu.Closed += (s, e) => { dgv.Invalidate(); };
        }

        private void OnMenuView(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not InteractionDto dto) return;

            MessageBox.Show(
                $"{dto.Subject}\n\n" +
                $"Type        {DisplayType(dto.TypeText)}\n" +
                $"Priority    {dto.PriorityText}\n" +
                $"Status      {dto.StatusText}\n" +
                $"Created     {dto.InteractionDate:MMM d, yyyy  HH:mm}\n" +
                (dto.UpdatedAt.HasValue ? $"Updated     {dto.UpdatedAt.Value:MMM d, yyyy  HH:mm}\n" : "") +
                (dto.ClosedAt.HasValue ? $"Closed      {dto.ClosedAt.Value:MMM d, yyyy  HH:mm}\n" : "") +
                $"Record      #{dto.CustomerInteractionId}  ·  {dto.ActivityStatus}\n\n" +
                $"Notes\n{dto.Notes}\n" +
                (string.IsNullOrWhiteSpace(dto.Resolution) ? "" : $"\nResolution\n{dto.Resolution}"),
                "Interaction details",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OnMenuUpdate(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not InteractionDto dto) return;
            OpenEditDialog(dto);
        }

        private async void OnMenuArchive(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not InteractionDto dto) return;

            if (dto.IsActive)
                await ArchiveAsync(dto);
            else
                await RestoreAsync(dto);
        }

        private void AddActionsColumn()
        {
            if (dgv.Columns[ColActions] != null)
                dgv.Columns.Remove(ColActions);

            var col = new DataGridViewTextBoxColumn
            {
                Name = ColActions,
                HeaderText = "",
                Width = 44,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.False,
                ReadOnly = true
            };

            dgv.Columns.Add(col);
            col.DisplayIndex = dgv.Columns.Count - 1;
        }

        private void Dgv_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (dgv.Columns[e.ColumnIndex].Name != ColActions) return;

            _menuRowIndex = e.RowIndex;

            var cellRect = dgv.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
            _actionsMenu.Show(dgv, new Point(cellRect.Right - 160, cellRect.Bottom));
        }

        private void Dgv_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgv.Rows[e.RowIndex].DataBoundItem is not InteractionDto dto) return;
            OpenEditDialog(dto);
        }

        private void Dgv_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) { _hoverRow = -1; return; }

            _hoverRow = e.RowIndex;
            dgv.Cursor = e.ColumnIndex >= 0 && dgv.Columns[e.ColumnIndex].Name == ColActions
                ? Cursors.Hand
                : Cursors.Default;
            dgv.InvalidateRow(e.RowIndex);
        }

        private void Dgv_CellMouseLeave(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            _hoverRow = -1;
            dgv.InvalidateRow(e.RowIndex);
        }

        // ═══════════ ARCHIVE / RESTORE ═══════════

        private async Task ArchiveAsync(InteractionDto dto)
        {
            var confirm = MessageBox.Show(
                $"Archive \u201c{dto.Subject}\u201d?\n\n" +
                "It leaves the list but nothing is deleted — you can restore it later.",
                "Archive interaction",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                await _api.ArchiveInteractionAsync(dto.CustomerInteractionId);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Couldn't archive this interaction.\n\n{ex.Message}",
                    "Archive failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async Task RestoreAsync(InteractionDto dto)
        {
            try
            {
                await _api.RestoreInteractionAsync(dto.CustomerInteractionId);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Couldn't restore this interaction.\n\n{ex.Message}",
                    "Restore failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ═══════════ CELL PAINTING ═══════════
        // Type = coloured dot, priority = weight, status = soft pill. One device each.

        private void Dgv_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;

            var g = e.Graphics;

            // Header: paint the baseline rule ourselves so it runs edge to edge.
            if (e.RowIndex == -1)
            {
                e.PaintBackground(e.CellBounds, false);
                e.PaintContent(e.CellBounds);
                using var hp = new Pen(T.Line, 1);
                g.DrawLine(hp, e.CellBounds.Left, e.CellBounds.Bottom - 1,
                               e.CellBounds.Right, e.CellBounds.Bottom - 1);
                e.Handled = true;
                return;
            }

            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string col = dgv.Columns[e.ColumnIndex].Name;
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool hovered = e.RowIndex == _hoverRow;

            Color bg = selected
                ? e.CellStyle!.SelectionBackColor
                : hovered ? T.RowHover : T.Surface;

            bool custom = col is "TypeText" or "StatusText" or "PriorityText" || col == ColActions;

            // Row background + hairline for every cell, so hover reads as a whole row.
            using (var b = new SolidBrush(bg))
                g.FillRectangle(b, e.CellBounds);
            using (var p = new Pen(T.LineSoft, 1))
                g.DrawLine(p, e.CellBounds.Left, e.CellBounds.Bottom - 1,
                              e.CellBounds.Right, e.CellBounds.Bottom - 1);

            if (!custom)
            {
                e.PaintContent(e.CellBounds);
                e.Handled = true;
                return;
            }

            Draw.Quality(g);
            var r = e.CellBounds;
            string text = e.FormattedValue?.ToString() ?? string.Empty;

            if (col == ColActions)
            {
                var color = hovered ? T.InkMuted : T.InkFaint;
                Draw.Text(g, "\u22EF", new Font("Segoe UI", 13F, FontStyle.Bold), color, r,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                e.Handled = true;
                return;
            }

            if (col == "TypeText")
            {
                Color accent = text switch
                {
                    "Inquiry" => AppTheme.Primary,
                    "Complaint" => AppTheme.Danger,
                    "Feedback" => AppTheme.Success,
                    _ => T.InkFaint
                };

                string label = DisplayType(text);

                int cx = r.Left + T.S3 + 3;
                Draw.Dot(g, cx, r.Top + r.Height / 2, 7, accent);

                var textRect = new Rectangle(cx + T.S3 - 2, r.Top, r.Width - (cx - r.Left) - T.S3, r.Height);
                Draw.Text(g, label, T.Body, T.Ink, textRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                e.Handled = true;
                return;
            }

            if (col == "PriorityText")
            {
                (Color fg, Font font) = text switch
                {
                    "High" => (AppTheme.Danger, T.BodyStrong),
                    "Medium" => (AppTheme.Warning, T.BodyStrong),
                    _ => (T.InkMuted, T.Body)
                };

                var textRect = new Rectangle(r.Left + T.S3, r.Top, r.Width - T.S3 * 2, r.Height);
                Draw.Text(g, text, font, fg, textRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                e.Handled = true;
                return;
            }

            if (col == "StatusText")
            {
                Color accent = text switch
                {
                    "Open" => AppTheme.Warning,
                    "In Progress" => AppTheme.Primary,
                    "Closed" => AppTheme.Success,
                    _ => T.InkMuted
                };

                var size = Draw.Measure(text, T.SmallStrong);
                int pillW = size.Width + T.S4;
                int pillH = 22;
                var pill = new Rectangle(r.Left + T.S3, r.Top + (r.Height - pillH) / 2, pillW, pillH);

                Draw.FillRounded(g, pill, T.PillRadius, Draw.Wash(accent));
                Draw.Text(g, text, T.SmallStrong, accent, pill,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                e.Handled = true;
            }
        }

        // ═══════════ MODAL LAUNCHERS ═══════════

        private void OpenAddDialog()
        {
            using var dlg = new InteractionFormDialog(null, _currentFilter);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private void OpenEditDialog(InteractionDto dto)
        {
            using var dlg = new InteractionFormDialog(dto);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        //  NESTED UI COMPONENTS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Flat white panel with a 1px hairline and rounded corners. No shadow.</summary>
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
                Draw.Quality(e.Graphics);
                using (var b = new SolidBrush(AppTheme.Background))
                    e.Graphics.FillRectangle(b, ClientRectangle);
                Draw.Card(e.Graphics, ClientRectangle, T.Radius, T.Surface, T.Line);
                base.OnPaint(e);
            }
        }

        /// <summary>
        /// Four counters in one strip, separated by hairlines. Clicking a counter filters
        /// the list by that status; the active one keeps a 2px underline so the filter is
        /// never invisible (visibility of system status).
        ///
        /// Caption and value are real Labels, not hand-painted text — framework text
        /// layout always reserves full ascender/descender space, so nothing gets clipped.
        /// </summary>
        [DesignerCategory("Code")]
        private sealed class MetricStrip : Control
        {
            private sealed class Item
            {
                public int? Status;
                public Color Accent = Color.Black;
                public Label CapLabel = null!;
                public Label ValLabel = null!;
            }

            private const int SegPad = T.S5;        // left inset of a segment
            private const int DotOffset = SegPad + 3;
            private const int CapOffset = SegPad + T.S4 - 2;

            private readonly List<Item> _items = new();
            private int _hover = -1;
            private int _selected = 0;

            public event EventHandler? SelectionChanged;

            public int? SelectedStatus =>
                _selected >= 0 && _selected < _items.Count ? _items[_selected].Status : null;

            public MetricStrip()
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
                    Font = T.Small,
                    ForeColor = T.InkMuted,
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                var val = new Label
                {
                    Text = "0",
                    Font = T.Metric,
                    ForeColor = T.Ink,
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
                    it.CapLabel.ForeColor = active ? T.Ink : T.InkMuted;
                    it.ValLabel.ForeColor = active ? it.Accent : T.Ink;
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

                    it.CapLabel.Location = new Point(segLeft + CapOffset, T.S4);
                    it.ValLabel.Location = new Point(segLeft + SegPad, it.CapLabel.Bottom + T.S1);
                }
            }

            /// <summary>
            /// Height the strip actually needs, measured from the real label sizes rather
            /// than an assumed constant — safe against font-metric differences across
            /// machines that could otherwise clip the value text at the bottom.
            /// </summary>
            public int PreferredContentHeight()
            {
                if (_items.Count == 0) return T.StripHeight;

                int capH = _items.Max(x => x.CapLabel.Height);
                int valH = _items.Max(x => x.ValLabel.Height);

                return T.S4 + capH + T.S1 + valH + T.S3;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Draw.Quality(g);

                using (var b = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(b, ClientRectangle);

                Draw.Card(g, ClientRectangle, T.Radius, T.Surface, T.Line);

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
                        var wash = Rectangle.Inflate(seg, -T.S2, -T.S2);
                        Draw.FillRounded(g, wash, 8, T.RowHover);
                    }

                    // Divider before every segment but the first.
                    if (i > 0)
                    {
                        using var pen = new Pen(T.Line, 1);
                        g.DrawLine(pen, seg.Left, T.S5, seg.Left, Height - T.S5);
                    }

                    Draw.Dot(g, seg.Left + DotOffset, it.CapLabel.Top + it.CapLabel.Height / 2, 7,
                        active ? it.Accent : Draw.Mix(it.Accent, Color.White, 0.45));

                    if (active)
                    {
                        var bar = new Rectangle(seg.Left + SegPad, Height - 4, 28, 2);
                        Draw.FillRounded(g, bar, 1, it.Accent);
                    }
                }
            }
        }

        /// <summary>
        /// Segmented control for the type filter: one track, the active segment lifted to
        /// white. Cheaper on attention than four competing pills.
        /// </summary>
        [DesignerCategory("Code")]
        private sealed class SegmentedFilter : Control
        {
            private readonly (string Label, InteractionTypeFilter? Filter)[] _items;
            private readonly int[] _widths;
            private int _hover = -1;
            private int _selected = 0;

            public event EventHandler? SelectionChanged;
            public InteractionTypeFilter? Selected => _items[_selected].Filter;

            public SegmentedFilter((string, InteractionTypeFilter?)[] items)
            {
                _items = items;
                _widths = new int[items.Length];

                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = T.Surface;
                Cursor = Cursors.Hand;
                Font = T.SmallStrong;

                for (int i = 0; i < _items.Length; i++)
                    _widths[i] = Draw.Measure(_items[i].Label, T.SmallStrong).Width + T.S5;
            }

            public int PreferredWidth => _widths.Sum() + T.S1 * 2;

            private int IndexAt(Point p)
            {
                int x = T.S1;
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
                Draw.Quality(g);

                using (var b = new SolidBrush(T.Surface))
                    g.FillRectangle(b, ClientRectangle);

                // Track
                Draw.FillRounded(g, new Rectangle(0, 0, Width, Height), 8, T.LineSoft);

                int x = T.S1;
                for (int i = 0; i < _items.Length; i++)
                {
                    var seg = new Rectangle(x, T.S1 - 1, _widths[i], Height - (T.S1 - 1) * 2);
                    bool active = i == _selected;

                    if (active)
                    {
                        Draw.FillRounded(g, seg, 6, T.Surface);
                        using var pen = new Pen(T.Line, 1);
                        using var path = Draw.Rounded(new Rectangle(seg.X, seg.Y, seg.Width - 1, seg.Height - 1), 6);
                        g.DrawPath(pen, path);
                    }

                    Color fg = active ? T.Ink : (i == _hover ? T.InkMuted : T.InkFaint);
                    Draw.Text(g, _items[i].Label, T.SmallStrong, fg, seg,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                    x += _widths[i];
                }
            }
        }

        /// <summary>Search field with a glyph, focus ring and an inline clear button.</summary>
        [DesignerCategory("Code")]
        private sealed class SearchBox : Control
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

            public SearchBox()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = T.Surface;

                Inner = new TextBox
                {
                    BorderStyle = BorderStyle.None,
                    Font = T.Body,
                    ForeColor = T.Ink,
                    BackColor = T.Surface
                };
                Inner.GotFocus += (s, e) => { _focused = true; Invalidate(); };
                Inner.LostFocus += (s, e) => { _focused = false; Invalidate(); };
                Inner.TextChanged += (s, e) => Invalidate();

                Controls.Add(Inner);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                int left = 34;
                Inner.Location = new Point(left, (Height - Inner.PreferredHeight) / 2);
                Inner.Width = Width - left - 34;
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
                Draw.Quality(g);

                using (var b = new SolidBrush(T.Surface))
                    g.FillRectangle(b, ClientRectangle);

                var box = new Rectangle(0, 0, Width, Height);
                Draw.FillRounded(g, box, 8, T.Surface);

                var border = _focused ? AppTheme.Primary : T.Line;
                using (var path = Draw.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 8))
                using (var pen = new Pen(border, _focused ? 1.4f : 1f))
                    g.DrawPath(pen, path);

                Draw.Text(g, "\uE721", T.Glyph, _focused ? AppTheme.Primary : T.InkFaint,
                    new Rectangle(10, 0, 20, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                if (Inner.Text.Length > 0)
                {
                    Draw.Text(g, "\uE711", new Font("Segoe MDL2 Assets", 9F),
                        _hoverClear ? T.Ink : T.InkFaint, ClearRect,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }

        /// <summary>Primary action button — flat, rounded, with a leading glyph.</summary>
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
                Font = T.BodyStrong;
                TabStop = true;
            }

            public int PreferredWidth => Draw.Measure(Text, T.BodyStrong).Width + 56;

            protected override void OnTextChanged(EventArgs e)
            {
                base.OnTextChanged(e);
                Width = PreferredWidth;
                Invalidate();
            }

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
                Draw.Quality(g);

                using (var b = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(b, ClientRectangle);

                Color bg = _down ? AppTheme.PrimaryActive
                         : _hover ? AppTheme.PrimaryHover
                         : AppTheme.Primary;

                Draw.FillRounded(g, ClientRectangle, 8, bg);

                if (Focused)
                {
                    var ring = Rectangle.Inflate(ClientRectangle, -3, -3);
                    using var path = Draw.Rounded(ring, 6);
                    using var pen = new Pen(Color.FromArgb(120, Color.White), 1.2f);
                    g.DrawPath(pen, path);
                }

                Draw.Text(g, _glyph, new Font("Segoe MDL2 Assets", 10F), Color.White,
                    new Rectangle(16, 0, 18, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                Draw.Text(g, Text, T.BodyStrong, Color.White,
                    new Rectangle(36, 0, Width - 46, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        /// <summary>Loading / empty / no-results screen. Every empty screen says what to do next.</summary>
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
                BackColor = T.Surface;
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
                Draw.Quality(g);

                using (var b = new SolidBrush(T.Surface))
                    g.FillRectangle(b, ClientRectangle);

                int cy = Height / 2 - 40;

                var circle = new Rectangle(Width / 2 - 26, cy, 52, 52);
                Draw.FillRounded(g, circle, 26, T.LineSoft);
                Draw.Text(g, _glyph, T.GlyphLarge, T.InkFaint, circle,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                Draw.Text(g, _title, T.Section, T.Ink,
                    new Rectangle(0, circle.Bottom + T.S4, Width, 24),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);

                int msgW = Math.Min(420, Width - T.S6 * 2);
                Draw.Text(g, _message, T.Body, T.InkMuted,
                    new Rectangle((Width - msgW) / 2, circle.Bottom + T.S4 + 28, msgW, 60),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
            }
        }

        /// <summary>Menu renderer without the grey gutter and blue system highlight.</summary>
        private sealed class QuietMenuRenderer : ToolStripProfessionalRenderer
        {
            public QuietMenuRenderer() : base(new Colors()) { RoundedEdges = false; }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var r = new Rectangle(4, 0, e.Item.Width - 8, e.Item.Height);
                if (e.Item.Selected)
                    Draw.FillRounded(e.Graphics, r, 6, T.RowHover);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using var pen = new Pen(T.LineSoft, 1);
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
            }

            private sealed class Colors : ProfessionalColorTable
            {
                public override Color ToolStripDropDownBackground => T.Surface;
                public override Color MenuBorder => T.Line;
                public override Color MenuItemBorder => Color.Transparent;
                public override Color ImageMarginGradientBegin => T.Surface;
                public override Color ImageMarginGradientMiddle => T.Surface;
                public override Color ImageMarginGradientEnd => T.Surface;
            }
        }
    }
}