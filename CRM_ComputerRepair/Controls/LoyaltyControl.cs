using CRM.winforms.Auth;
using CRM.winforms.Controls;
using CRM.winforms.Forms;
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
    /// Loyalty Programs management control.
    /// Modern SaaS workbench matching Dashboard, Reports, Customer Retention, and User Accounts.
    /// </summary>
    [DesignerCategory("Code")]
    public class LoyaltyControl : UserControl
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private List<LoyaltyProgramDto> _all = new();
        private List<LoyaltyProgramDto> _filtered = new();
        private int? _filterTag = null; // null = All, 1 = Active, 2 = Discounts, 3 = Perks & Free, 0 = Archived

        private int _selectedCompanyId = UserSession.CompanyId > 0 ? UserSession.CompanyId : 1;
        private List<CompanyDto> _companies = new();

        // ═══════════ CONTROLS ═══════════

        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private Label? _lblCompanyPicker;
        private ComboBox? _cboCompany;
        private SaasButton btnRefresh = null!;
        private SaasButton btnAdd = null!;

        private MetricStrip strip = null!;

        private SurfaceCard card = null!;
        private Label lblGridTitle = null!;
        private Label lblCount = null!;
        private SegmentedFilter segments = null!;
        private SearchBox search = null!;

        private DataGridView dgv = null!;
        private StateView state = null!;
        private TablePagination pager = null!;

        private const string ColActions = "colActions";
        private ContextMenuStrip _actionsMenu = null!;
        private int _menuRowIndex = -1;
        private int _hoverRow = -1;
        private readonly ToolTip _tips = new ToolTip { InitialDelay = 500, ReshowDelay = 200 };

        // ═══════════ CONSTRUCTOR ═══════════

        public LoyaltyControl()
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
                Text = "Loyalty Programs",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = AppTheme.Background,
                UseMnemonic = false
            };

            lblSubtitle = new Label
            {
                Text = "Customer reward programs, points accumulation, and retention incentives",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = AppTheme.Background,
                UseMnemonic = false
            };

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Click += async (s, e) => await ReloadAsync();
            _tips.SetToolTip(btnRefresh, "Refresh loyalty programs (F5)");

            btnAdd = new SaasButton("Add Program", SaasButtonVariant.Primary, "\uE710");
            btnAdd.Click += async (s, e) => await AddAsync();
            _tips.SetToolTip(btnAdd, "Create new loyalty reward program (Ctrl+N)");

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);

            if (string.Equals(UserSession.Role, "Super Admin", StringComparison.OrdinalIgnoreCase))
            {
                _lblCompanyPicker = new Label
                {
                    Text = "Business:",
                    Font = UiKit.T.SmallStrong,
                    ForeColor = UiKit.T.InkMuted,
                    AutoSize = true,
                    BackColor = AppTheme.Background
                };

                _cboCompany = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = UiKit.T.Body,
                    BackColor = UiKit.T.Surface,
                    ForeColor = UiKit.T.Ink,
                    FlatStyle = FlatStyle.Flat
                };
                _cboCompany.SelectedIndexChanged += CboCompany_SelectedIndexChanged;

                Controls.Add(_lblCompanyPicker);
                Controls.Add(_cboCompany);
            }

            Controls.Add(btnRefresh);
            Controls.Add(btnAdd);

            // ── Metric Strip ──
            strip = new MetricStrip();
            strip.AddItem("Total Programs", null, AppTheme.Primary);
            strip.AddItem("Active", 1, AppTheme.Success);
            strip.AddItem("Discounts", 2, Color.FromArgb(99, 102, 241));
            strip.AddItem("Perks & Free", 3, Color.FromArgb(14, 116, 144));
            strip.AddItem("Archived", 0, UiKit.T.InkMuted);
            strip.SelectionChanged += (s, e) =>
            {
                _filterTag = strip.SelectedStatus;
                segments.SelectByValue(_filterTag);
                SetTypeFilter(_filterTag);
            };
            Controls.Add(strip);

            // ── Workbench Card ──
            card = new SurfaceCard();

            lblGridTitle = new Label
            {
                Text = "All Programs",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };

            lblCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };

            segments = new SegmentedFilter(new (string, int?)[]
            {
                ("All", null),
                ("Active", 1),
                ("Discounts", 2),
                ("Perks & Free", 3),
                ("Archived", 0)
            });
            segments.SelectionChanged += (s, e) => SetTypeFilter(segments.Selected);

            search = new SearchBox { PlaceholderText = "Search program by name, reward, criteria... (Ctrl+F)" };
            search.Inner.TextChanged += (s, e) => ApplyFilter();
            _tips.SetToolTip(search, "Search loyalty programs (Ctrl+F)");

            dgv = new DataGridView();
            TableKit.StyleGrid(dgv);
            dgv.CellPainting += Dgv_CellPainting;
            dgv.CellClick += Dgv_CellClick;
            dgv.CellDoubleClick += Dgv_CellDoubleClick;
            dgv.KeyDown += Dgv_KeyDown;
            dgv.CellMouseEnter += Dgv_CellMouseEnter;
            dgv.CellMouseLeave += Dgv_CellMouseLeave;
            dgv.MouseLeave += (s, e) => { _hoverRow = -1; dgv.Invalidate(); };

            state = new StateView { Visible = false };

            pager = new TablePagination();
            pager.PageChanged += (s, e) => RefreshGridPage();

            card.Controls.Add(lblGridTitle);
            card.Controls.Add(lblCount);
            card.Controls.Add(segments);
            card.Controls.Add(search);
            card.Controls.Add(dgv);
            card.Controls.Add(state);
            card.Controls.Add(pager);

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

        // ═══════════ KEYBOARD SHORTCUTS ═══════════

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F5:
                    _ = ReloadAsync();
                    return true;

                case Keys.Control | Keys.F:
                    search.Inner.Focus();
                    search.Inner.SelectAll();
                    return true;

                case Keys.Control | Keys.N:
                    _ = AddAsync();
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
            if (e.KeyCode == Keys.Enter)
            {
                if (dgv.CurrentRow?.DataBoundItem is LoyaltyProgramDto dto)
                {
                    using var dlg = new LoyaltyFormDialog(dto, _selectedCompanyId);
                    if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                        _ = ReloadAsync();
                }
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Apps)
            {
                if (dgv.CurrentRow != null)
                {
                    _menuRowIndex = dgv.CurrentRow.Index;
                    var r = dgv.GetRowDisplayRectangle(_menuRowIndex, true);
                    _actionsMenu.Show(dgv, new Point(r.Right - 160, r.Bottom));
                    e.Handled = true;
                }
            }
        }

        // ═══════════ FILTERING & LAYOUT ═══════════

        private void SetTypeFilter(int? filterTag)
        {
            _filterTag = filterTag;
            strip.SelectByValue(filterTag);

            lblGridTitle.Text = filterTag switch
            {
                1 => "Active Programs",
                2 => "Discount Programs",
                3 => "Perks & Free Services",
                0 => "Archived Programs",
                _ => "All Programs"
            };

            lblSubtitle.Text = filterTag switch
            {
                1 => "Reward programs currently active and qualifying customers",
                2 => "Percentage-based service discount incentive programs",
                3 => "Complimentary services, vouchers, and points multipliers",
                0 => "Archived or expired loyalty programs — restorable anytime",
                _ => "Customer reward programs, points accumulation, and retention incentives"
            };

            ApplyFilter();
            LayoutUi();
            Invalidate();
        }

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            // Header
            lblTitle.Location = new Point(0, 0);

            int subtitleY = lblTitle.PreferredHeight + 6;
            lblSubtitle.Location = new Point(1, subtitleY);

            btnAdd.Size = new Size(btnAdd.PreferredWidth, UiKit.T.ButtonHeight);
            btnAdd.Location = new Point(Width - btnAdd.Width, 2);

            btnRefresh.Size = new Size(btnRefresh.PreferredWidth, UiKit.T.ButtonHeight);
            btnRefresh.Location = new Point(btnAdd.Left - btnRefresh.Width - UiKit.T.S2, 2);

            if (_cboCompany != null && _cboCompany.Visible && _lblCompanyPicker != null)
            {
                _cboCompany.Size = new Size(240, UiKit.T.ButtonHeight);
                _cboCompany.Location = new Point(btnRefresh.Left - _cboCompany.Width - UiKit.T.S4, 3);
                _lblCompanyPicker.Location = new Point(_cboCompany.Left - _lblCompanyPicker.PreferredWidth - 6, 9);
            }

            int dividerY = subtitleY + lblSubtitle.PreferredHeight + UiKit.T.S4;

            // Metric strip
            int stripTop = dividerY + UiKit.T.S5;
            int stripH = Math.Max(UiKit.T.StripHeight, strip.PreferredContentHeight());
            strip.Location = new Point(0, stripTop);
            strip.Size = new Size(Width, stripH);

            // Workbench card
            int cardTop = stripTop + stripH + UiKit.T.S5;
            int cardHeight = Math.Max(240, Height - cardTop);

            card.Location = new Point(0, cardTop);
            card.Size = new Size(Width, cardHeight);

            const int cp = UiKit.T.S5;

            lblGridTitle.Location = new Point(cp, UiKit.T.S5);
            lblCount.Location = new Point(
                lblGridTitle.Right + UiKit.T.S2,
                lblGridTitle.Top + lblGridTitle.PreferredHeight - lblCount.PreferredHeight - 2);

            int toolbarY = lblGridTitle.Bottom + TableKit.ToolbarGap;

            segments.Size = new Size(segments.PreferredWidth, TableKit.ToolbarH);
            segments.Location = new Point(cp, toolbarY);

            int searchW = TableKit.SearchWidth(card.Width, segments.Width);
            search.Size = new Size(searchW, TableKit.InputH);
            search.Location = new Point(card.Width - cp - searchW, toolbarY + (TableKit.ToolbarH - TableKit.InputH) / 2);

            int gridTop = toolbarY + TableKit.ToolbarH + TableKit.ToolbarGap;
            int gridW = card.Width - cp * 2;
            int gridH = card.Height - gridTop - cp - (pager.Visible ? TableKit.FooterH : 0);

            if (pager.Visible)
            {
                pager.Location = new Point(cp, gridTop + Math.Max(0, gridH));
                pager.Size = new Size(gridW, TableKit.FooterH);
            }

            if (gridW > 100 && gridH > 60)
            {
                dgv.Location = new Point(cp, gridTop);
                dgv.Size = new Size(gridW, gridH);
                state.Location = new Point(cp, gridTop);
                state.Size = new Size(gridW, gridH + (pager.Visible ? TableKit.FooterH : 0));
            }
        }

        // ═══════════ DATA LOADING & FILTERING ═══════════

        public async Task ReloadAsync()
        {
            try
            {
                if (string.Equals(UserSession.Role, "Super Admin", StringComparison.OrdinalIgnoreCase) && _companies.Count == 0)
                {
                    _companies = await _api.GetCompaniesAsync();
                    PopulateCompanyPicker();
                }

                _all = await _api.GetLoyaltyProgramsAsync(companyId: _selectedCompanyId);
                UpdateMetricStrip();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                _all = new List<LoyaltyProgramDto>();
                UpdateMetricStrip();
                ApplyFilter();

                MessageBox.Show($"Could not load loyalty programs:\n\n{ex.Message}",
                    "Connection Problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateCompanyPicker()
        {
            if (_cboCompany == null || _companies.Count == 0) return;

            _cboCompany.SelectedIndexChanged -= CboCompany_SelectedIndexChanged;
            _cboCompany.DisplayMember = "CompanyName";
            _cboCompany.ValueMember = "CompanyId";
            _cboCompany.DataSource = null;
            _cboCompany.DataSource = _companies;

            var match = _companies.FirstOrDefault(c => c.CompanyId == _selectedCompanyId);
            if (match != null)
                _cboCompany.SelectedItem = match;
            else if (_companies.Count > 0)
            {
                _cboCompany.SelectedIndex = 0;
                _selectedCompanyId = _companies[0].CompanyId;
            }

            _cboCompany.SelectedIndexChanged += CboCompany_SelectedIndexChanged;
        }

        private void CboCompany_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_cboCompany?.SelectedItem is CompanyDto c)
            {
                _selectedCompanyId = c.CompanyId;
                lblSubtitle.Text = $"Customer reward programs and retention incentives for {c.CompanyName}";
                _ = ReloadAsync();
            }
        }

        private void UpdateMetricStrip()
        {
            int total = _all.Count;
            int active = _all.Count(p => p.IsActive);
            int discounts = _all.Count(p => p.RewardType == 0);
            int perks = _all.Count(p => p.RewardType is 1 or 2 or 3);
            int archived = _all.Count(p => !p.IsActive);

            strip.SetValue(0, total);
            strip.SetValue(1, active);
            strip.SetValue(2, discounts);
            strip.SetValue(3, perks);
            strip.SetValue(4, archived);
        }

        private void ApplyFilter()
        {
            var term = search.Text?.Trim() ?? "";
            IEnumerable<LoyaltyProgramDto> q = _all;

            // Apply category filter
            if (_filterTag.HasValue)
            {
                q = _filterTag.Value switch
                {
                    1 => q.Where(p => p.IsActive),
                    2 => q.Where(p => p.RewardType == 0),
                    3 => q.Where(p => p.RewardType is 1 or 2 or 3),
                    0 => q.Where(p => !p.IsActive),
                    _ => q
                };
            }

            // Apply search term
            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(x =>
                    (x.ProgramName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Description ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.RewardTypeText ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.RewardDisplay ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.EligibilityText ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            _filtered = q.ToList();
            pager.Reset();
            lblCount.Text = TableKit.FormatCount(_filtered.Count, _all.Count);
            RefreshGridPage();
        }

        private void RefreshGridPage()
        {
            pager.SetTotal(_filtered.Count);
            var pageItems = pager.Slice(_filtered);

            dgv.DataSource = null;
            dgv.DataSource = pageItems;
            ConfigureColumns();
            TableKit.AddActionsColumn(dgv, ColActions);

            if (_filtered.Count > 0)
            {
                state.Clear();
                dgv.Visible = true;
                pager.Visible = _filtered.Count > TableKit.PageSize;
                dgv.ClearSelection();
            }
            else
            {
                dgv.Visible = false;
                pager.Visible = false;
                var term = search.Text?.Trim() ?? "";
                if (_all.Count == 0)
                    state.Show("\uE8C7", "No Loyalty Programs", "There are currently no loyalty programs configured. Click \"Add Program\" to create one.");
                else
                    state.Show("\uE721", "No Matches Found", string.IsNullOrEmpty(term)
                        ? "No programs match the selected category."
                        : $"No loyalty programs match \"{term}\".");
            }

            LayoutUi();
        }

        private void ConfigureColumns()
        {
            foreach (var hidden in new[]
            {
                "LoyaltyProgramId", "CompanyId", "CreatedAt",
                "PointsPerPeso", "DiscountPercentage", "MinimumSpend",
                "IsActive", "StartDate", "EndDate",
                "PointsValidityDays", "RedeemPointsRequired", "MinTransactions",
                "MinTotalSpent", "MaxInactiveDays", "MinVisitsPerPeriod",
                "VisitPeriodDays", "RewardType", "RewardValue", "MaxRedemptionsPerCustomer",
                "Description", "DiscountDisplay", "MinSpendDisplay"
            })
            {
                var h = dgv.Columns[hidden];
                if (h != null)
                    h.Visible = false;
            }

            void Setup(string name, string header, int width, int idx, bool fill = false, DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft, string? format = null)
            {
                var c = dgv.Columns[name];
                if (c == null) return;
                c.Visible = true;
                c.HeaderText = header;
                c.SortMode = DataGridViewColumnSortMode.Automatic;
                c.AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None;
                if (!fill) c.Width = width;
                else c.MinimumWidth = 200;
                c.DefaultCellStyle.Alignment = align;
                if (format != null) c.DefaultCellStyle.Format = format;
                c.DisplayIndex = idx;
            }

            Setup("ProgramName", "Program Name", 200, 0);
            Setup("EligibilityText", "Eligibility Criteria", 0, 1, fill: true);
            Setup("RewardTypeText", "Reward Type", 140, 2);
            Setup("RewardDisplay", "Reward Value", 130, 3);
            Setup("PointsDisplay", "Points Rate", 120, 4);
            Setup("StartDateDisplay", "Start Date", 115, 5, align: DataGridViewContentAlignment.MiddleCenter);
            Setup("EndDateDisplay", "End Date", 115, 6, align: DataGridViewContentAlignment.MiddleCenter);
            Setup("StatusText", "Status", 115, 7);

            var cName = dgv.Columns["ProgramName"];
            if (cName != null)
                cName.DefaultCellStyle.Font = UiKit.T.BodyStrong;

            var cVal = dgv.Columns["RewardDisplay"];
            if (cVal != null)
                cVal.DefaultCellStyle.Font = UiKit.T.BodyStrong;

            var cPoints = dgv.Columns["PointsDisplay"];
            if (cPoints != null)
                cPoints.DefaultCellStyle.ForeColor = UiKit.T.InkMuted;

            var cElig = dgv.Columns["EligibilityText"];
            if (cElig != null)
                cElig.DefaultCellStyle.ForeColor = UiKit.T.InkMuted;
        }

        // ═══════════ CELL PAINTING ═══════════

        private void Dgv_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;
            var g = e.Graphics;

            // Header row
            if (e.RowIndex == -1)
            {
                e.PaintBackground(e.CellBounds, false);
                e.PaintContent(e.CellBounds);
                TableKit.PaintHeaderRule(g, e.CellBounds);
                e.Handled = true;
                return;
            }

            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string col = dgv.Columns[e.ColumnIndex].Name;
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool hovered = e.RowIndex == _hoverRow;

            Color bg = TableKit.RowBackground(e, selected, hovered);
            TableKit.PaintRowShell(g, e.CellBounds, bg);

            bool custom = col is "StatusText" or "RewardTypeText" || col == ColActions;
            if (!custom)
            {
                e.PaintContent(e.CellBounds);
                e.Handled = true;
                return;
            }

            UiKit.Quality(g);
            var r = e.CellBounds;
            string text = e.FormattedValue?.ToString() ?? string.Empty;

            if (col == "StatusText")
            {
                PaintStatusBadge(g, r, text);
                e.Handled = true;
                return;
            }

            if (col == "RewardTypeText")
            {
                PaintRewardTypeBadge(g, r, text);
                e.Handled = true;
                return;
            }

            if (col == ColActions)
            {
                TableKit.PaintActionCell(g, r, hovered);
                e.Handled = true;
            }
        }

        private static void PaintRewardTypeBadge(Graphics g, Rectangle cell, string rewardType)
        {
            (Color fg, Color bg) = rewardType.ToLowerInvariant() switch
            {
                var t when t.Contains("discount") => (Color.FromArgb(67, 56, 202), Color.FromArgb(224, 231, 255)), // Indigo
                var t when t.Contains("free") => (Color.FromArgb(21, 128, 61), Color.FromArgb(220, 252, 231)),      // Emerald
                var t when t.Contains("points") => (Color.FromArgb(126, 34, 206), Color.FromArgb(243, 232, 255)),  // Purple
                var t when t.Contains("voucher") => (Color.FromArgb(180, 83, 9), Color.FromArgb(254, 243, 199)),   // Amber
                _ => (UiKit.T.InkMuted, UiKit.T.LineSoft)
            };

            var size = UiKit.Measure(rewardType, UiKit.T.SmallStrong);
            int padX = 10;
            int badgeW = size.Width + padX * 2;
            int badgeH = 22;
            int bx = cell.Left + UiKit.T.S3;
            int by = cell.Top + (cell.Height - badgeH) / 2;
            var badgeRect = new Rectangle(bx, by, badgeW, badgeH);

            UiKit.FillRounded(g, badgeRect, badgeH / 2, bg);
            UiKit.Text(g, rewardType, UiKit.T.SmallStrong, fg, badgeRect,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private static void PaintStatusBadge(Graphics g, Rectangle cell, string status)
        {
            bool active = status.Equals("Active", StringComparison.OrdinalIgnoreCase);

            Color fg = active ? Color.FromArgb(21, 128, 61) : UiKit.T.InkMuted;      // Green-700 / Slate
            Color bg = active ? Color.FromArgb(220, 252, 231) : UiKit.T.LineSoft;     // Green-100 / Gray

            var size = UiKit.Measure(status, UiKit.T.SmallStrong);
            int padX = 10;
            int badgeW = size.Width + padX * 2;
            int badgeH = 22;
            int bx = cell.Left + UiKit.T.S3;
            int by = cell.Top + (cell.Height - badgeH) / 2;
            var badgeRect = new Rectangle(bx, by, badgeW, badgeH);

            UiKit.FillRounded(g, badgeRect, badgeH / 2, bg);
            UiKit.Text(g, status, UiKit.T.SmallStrong, fg, badgeRect,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // ═══════════ MODAL ACTIONS & MENUS ═══════════

        private async Task AddAsync()
        {
            using var dlg = new LoyaltyFormDialog(null, _selectedCompanyId);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                await ReloadAsync();
        }

        private void BuildActionsMenu()
        {
            _actionsMenu = TableKit.MakeMenu();

            var mnuEdit = new ToolStripMenuItem("Edit program details");
            var sep = new ToolStripSeparator();
            var mnuToggleArchive = new ToolStripMenuItem("Archive program");

            _actionsMenu.Items.Add(mnuEdit);
            _actionsMenu.Items.Add(sep);
            _actionsMenu.Items.Add(mnuToggleArchive);

            TableKit.StyleMenuItems(_actionsMenu);

            mnuEdit.Click += async (s, e) => await OnEditAsync();
            mnuToggleArchive.Click += async (s, e) => await OnToggleArchiveAsync();
            _actionsMenu.Opening += (s, e) =>
            {
                if (_menuRowIndex >= 0 && _menuRowIndex < dgv.Rows.Count &&
                    dgv.Rows[_menuRowIndex].DataBoundItem is LoyaltyProgramDto dto)
                {
                    mnuToggleArchive.Text = dto.IsActive ? "Archive program" : "Reactivate program";
                    mnuToggleArchive.ForeColor = dto.IsActive ? AppTheme.Danger : AppTheme.Success;
                }
            };
            _actionsMenu.Closed += (s, e) => dgv.Invalidate();
        }

        private async Task OnEditAsync()
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.Rows.Count) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not LoyaltyProgramDto dto) return;

            using var dlg = new LoyaltyFormDialog(dto, _selectedCompanyId);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                await ReloadAsync();
        }

        private async Task OnToggleArchiveAsync()
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.Rows.Count) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not LoyaltyProgramDto dto) return;

            if (dto.IsActive)
            {
                var confirm = MessageBox.Show(
                    $"Archive loyalty program \"{dto.ProgramName}\"?\n\nCustomers will no longer be eligible for this program until it is reactivated.",
                    "Archive Program",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm != DialogResult.Yes) return;

                try
                {
                    await _api.ArchiveLoyaltyProgramAsync(dto.LoyaltyProgramId);
                    await ReloadAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Archive failed:\n\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                try
                {
                    dto.IsActive = true;
                    await _api.UpdateLoyaltyProgramAsync(dto.LoyaltyProgramId, dto);
                    await ReloadAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Reactivation failed:\n\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void Dgv_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgv.Columns[e.ColumnIndex].Name != ColActions) return;

            _menuRowIndex = e.RowIndex;
            var r = dgv.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
            _actionsMenu.Show(dgv, new Point(r.Right - 160, r.Bottom));
        }

        private void Dgv_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgv.Rows[e.RowIndex].DataBoundItem is not LoyaltyProgramDto dto) return;

            using var dlg = new LoyaltyFormDialog(dto, _selectedCompanyId);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private void Dgv_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) { _hoverRow = -1; return; }
            _hoverRow = e.RowIndex;
            dgv.Cursor = e.ColumnIndex >= 0 && dgv.Columns[e.ColumnIndex].Name == ColActions
                ? Cursors.Hand : Cursors.Default;
            dgv.InvalidateRow(e.RowIndex);
        }

        private void Dgv_CellMouseLeave(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            _hoverRow = -1;
            dgv.InvalidateRow(e.RowIndex);
        }

        // ═══════════ NESTED UI ALIASES ═══════════

        [DesignerCategory("Code")]
        private sealed class SurfaceCard : WorkbenchCard { }

        [DesignerCategory("Code")]
        private sealed class MetricStrip : WorkbenchMetrics { }

        [DesignerCategory("Code")]
        private sealed class SegmentedFilter : WorkbenchSegments<int?>
        {
            public SegmentedFilter((string, int?)[] items) : base(items) { }
        }

        [DesignerCategory("Code")]
        private sealed class SearchBox : WorkbenchSearch { }

        [DesignerCategory("Code")]
        private sealed class StateView : WorkbenchState { }
    }
}