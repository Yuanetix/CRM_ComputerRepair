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
    /// Super Admin use case — view and manage users with Admin or Super Admin role.
    /// Modern SaaS layout matching Dashboard, Reports, Customer Retention, and User Accounts.
    /// </summary>
    [DesignerCategory("Code")]
    public class AdminAccountsControl : UserControl
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private List<UserSummaryDto> _all = new();
        private List<UserSummaryDto> _filtered = new();
        private int? _filterTag = null; // null = All, 1 = Super Admin, 2 = Admin, 3 = Active, 0 = Inactive

        // ═══════════ CONTROLS ═══════════

        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
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

        public AdminAccountsControl()
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
                Text = "Admin Accounts",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = AppTheme.Background,
                UseMnemonic = false
            };

            lblSubtitle = new Label
            {
                Text = "Workspace administrators, security privileges, and elevated system access",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = AppTheme.Background,
                UseMnemonic = false
            };

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Click += async (s, e) => await ReloadAsync();
            _tips.SetToolTip(btnRefresh, "Refresh administrator accounts (F5)");

            btnAdd = new SaasButton("Add Admin", SaasButtonVariant.Primary, "\uE710");
            btnAdd.Click += async (s, e) => await AddAsync();
            _tips.SetToolTip(btnAdd, "Create new administrator account (Ctrl+N)");

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnRefresh);
            Controls.Add(btnAdd);

            // ── Metric Strip ──
            strip = new MetricStrip();
            strip.AddItem("Total Admins", null, AppTheme.Primary);
            strip.AddItem("Super Admins", 1, Color.FromArgb(147, 51, 234));
            strip.AddItem("Admins", 2, Color.FromArgb(99, 102, 241));
            strip.AddItem("Active", 3, AppTheme.Success);
            strip.AddItem("Inactive", 0, UiKit.T.InkMuted);
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
                Text = "Administrative Users",
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
                ("Super Admin", 1),
                ("Admin", 2),
                ("Active", 3),
                ("Inactive", 0)
            });
            segments.SelectionChanged += (s, e) => SetTypeFilter(segments.Selected);

            search = new SearchBox { PlaceholderText = "Search admin by name, username, email... (Ctrl+F)" };
            search.Inner.TextChanged += (s, e) => ApplyFilter();
            _tips.SetToolTip(search, "Search administrators (Ctrl+F)");

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
                if (dgv.CurrentRow?.DataBoundItem is UserSummaryDto dto)
                {
                    using var dlg = new UserFormDialog(dto);
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
                1 => "Super Administrators",
                2 => "Administrators",
                3 => "Active Admins",
                0 => "Inactive Admins",
                _ => "Administrative Users"
            };

            lblSubtitle.Text = filterTag switch
            {
                1 => "Super Administrator accounts with full unrestricted system control",
                2 => "Workspace Administrator accounts with elevated operational permissions",
                3 => "Administrators currently active and authorized to sign in",
                0 => "Deactivated administrative accounts — restorable anytime",
                _ => "Workspace administrators, security privileges, and elevated system access"
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
                _all = await _api.GetAdminAccountsAsync();
                UpdateMetricStrip();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                _all = new List<UserSummaryDto>();
                UpdateMetricStrip();
                ApplyFilter();

                MessageBox.Show($"Could not load admin accounts:\n\n{ex.Message}",
                    "Connection Problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateMetricStrip()
        {
            int total = _all.Count;
            int superAdmins = _all.Count(u => u.Roles.Any(r => r.Contains("Super", StringComparison.OrdinalIgnoreCase)));
            int regularAdmins = _all.Count(u => u.Roles.Any(r => r.Contains("Admin", StringComparison.OrdinalIgnoreCase) && !r.Contains("Super", StringComparison.OrdinalIgnoreCase)));
            int active = _all.Count(u => u.IsActive);
            int inactive = _all.Count(u => !u.IsActive);

            strip.SetValue(0, total);
            strip.SetValue(1, superAdmins);
            strip.SetValue(2, regularAdmins);
            strip.SetValue(3, active);
            strip.SetValue(4, inactive);
        }

        private void ApplyFilter()
        {
            var term = search.Text?.Trim() ?? "";
            IEnumerable<UserSummaryDto> q = _all;

            // Apply filter tag
            if (_filterTag.HasValue)
            {
                q = _filterTag.Value switch
                {
                    1 => q.Where(u => u.Roles.Any(r => r.Contains("Super", StringComparison.OrdinalIgnoreCase))),
                    2 => q.Where(u => u.Roles.Any(r => r.Contains("Admin", StringComparison.OrdinalIgnoreCase) && !r.Contains("Super", StringComparison.OrdinalIgnoreCase))),
                    3 => q.Where(u => u.IsActive),
                    0 => q.Where(u => !u.IsActive),
                    _ => q
                };
            }

            // Apply text search
            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(x =>
                    (x.FullName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.UserName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.RoleDisplay ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
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
                    state.Show("\uE7EE", "No Admin Accounts Found", "There are currently no administrator accounts configured. Click \"Add Admin\" to create one.");
                else
                    state.Show("\uE721", "No Matches Found", string.IsNullOrEmpty(term)
                        ? "No administrator accounts match the selected category."
                        : $"No administrator accounts match \"{term}\".");
            }

            LayoutUi();
        }

        private void ConfigureColumns()
        {
            foreach (var hidden in new[]
            {
                "Id", "FirstName", "LastName", "IsActive", "UpdatedAt", "Roles"
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
                else c.MinimumWidth = 190;
                c.DefaultCellStyle.Alignment = align;
                if (format != null) c.DefaultCellStyle.Format = format;
                c.DisplayIndex = idx;
            }

            Setup("FullName", "Name", 220, 0);
            Setup("UserName", "Username", 150, 1);
            Setup("Email", "Email Address", 0, 2, fill: true);
            Setup("RoleDisplay", "Role", 160, 3);
            Setup("StatusText", "Status", 120, 4);
            Setup("CreatedAt", "Created Date", 130, 5, align: DataGridViewContentAlignment.MiddleCenter, format: "MMM d, yyyy");

            var cFull = dgv.Columns["FullName"];
            if (cFull != null)
                cFull.DefaultCellStyle.Font = UiKit.T.BodyStrong;

            var cUser = dgv.Columns["UserName"];
            if (cUser != null)
                cUser.DefaultCellStyle.ForeColor = UiKit.T.InkMuted;
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

            bool custom = col is "StatusText" or "RoleDisplay" || col == ColActions;
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

            if (col == "RoleDisplay")
            {
                PaintRoleBadge(g, r, text);
                e.Handled = true;
                return;
            }

            if (col == ColActions)
            {
                TableKit.PaintActionCell(g, r, hovered);
                e.Handled = true;
            }
        }

        private static void PaintRoleBadge(Graphics g, Rectangle cell, string role)
        {
            (Color fg, Color bg) = role.ToLowerInvariant() switch
            {
                var r when r.Contains("super") => (Color.FromArgb(126, 34, 206), Color.FromArgb(243, 232, 255)), // Purple-700 / Purple-100
                var r when r.Contains("admin") => (Color.FromArgb(67, 56, 202), Color.FromArgb(224, 231, 255)),  // Indigo-700 / Indigo-100
                _ => (UiKit.T.InkMuted, UiKit.T.LineSoft)
            };

            var size = UiKit.Measure(role, UiKit.T.SmallStrong);
            int padX = 10;
            int badgeW = size.Width + padX * 2;
            int badgeH = 22;
            int bx = cell.Left + UiKit.T.S3;
            int by = cell.Top + (cell.Height - badgeH) / 2;
            var badgeRect = new Rectangle(bx, by, badgeW, badgeH);

            UiKit.FillRounded(g, badgeRect, badgeH / 2, bg);
            UiKit.Text(g, role, UiKit.T.SmallStrong, fg, badgeRect,
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
            using var dlg = new UserFormDialog(null);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                await ReloadAsync();
        }

        private void BuildActionsMenu()
        {
            _actionsMenu = TableKit.MakeMenu();

            var mnuEdit = new ToolStripMenuItem("Edit administrator");
            var sep = new ToolStripSeparator();
            var mnuToggleStatus = new ToolStripMenuItem("Deactivate account");

            _actionsMenu.Items.Add(mnuEdit);
            _actionsMenu.Items.Add(sep);
            _actionsMenu.Items.Add(mnuToggleStatus);

            TableKit.StyleMenuItems(_actionsMenu);

            mnuEdit.Click += async (s, e) => await OnEditAsync();
            mnuToggleStatus.Click += async (s, e) => await OnDeactivateAsync();
            _actionsMenu.Opening += (s, e) =>
            {
                if (_menuRowIndex >= 0 && _menuRowIndex < dgv.Rows.Count &&
                    dgv.Rows[_menuRowIndex].DataBoundItem is UserSummaryDto u)
                {
                    mnuToggleStatus.Text = u.IsActive ? "Deactivate account" : "Activate account";
                    mnuToggleStatus.ForeColor = u.IsActive ? AppTheme.Danger : AppTheme.Success;
                }
            };
            _actionsMenu.Closed += (s, e) => dgv.Invalidate();
        }

        private async Task OnEditAsync()
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.Rows.Count) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not UserSummaryDto dto) return;

            using var dlg = new UserFormDialog(dto);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                await ReloadAsync();
        }

        private async Task OnDeactivateAsync()
        {
            if (_menuRowIndex < 0 || _menuRowIndex >= dgv.Rows.Count) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not UserSummaryDto dto) return;

            if (dto.IsActive)
            {
                var confirm = MessageBox.Show(
                    $"Deactivate administrative account for \"{dto.FullName}\" ({dto.UserName})?\n\nThis administrator will not be able to log in until reactivated.",
                    "Deactivate Administrator",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm != DialogResult.Yes) return;

                try
                {
                    await _api.DeactivateUserAsync(dto.Id);
                    await ReloadAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Deactivation failed:\n\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                try
                {
                    await _api.RestoreUserAsync(dto.Id);
                    await ReloadAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Activation failed:\n\n{ex.Message}",
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
            if (dgv.Rows[e.RowIndex].DataBoundItem is not UserSummaryDto dto) return;

            using var dlg = new UserFormDialog(dto);
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