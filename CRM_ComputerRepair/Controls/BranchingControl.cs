using CRM.winforms.Auth;
using CRM.winforms.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Multi-Branching & Regional Operations Management Control.
    /// Provides top KPI summary cards, real-time search & filters,
    /// dynamic branch directory DataGridView with styled badges, manager avatars,
    /// and a 3-dot context menu for branch operations.
    /// </summary>
    [DesignerCategory("Code")]
    public class BranchingControl : UserControl
    {
        private readonly ApiClient _api = new();
        private List<BranchDto> _allBranches = new();
        private List<BranchDto> _filteredBranches = new();

        // ── Controls ──
        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private SaasButton btnNewBranch = null!;
        private SaasButton btnRefresh = null!;

        // ── KPI Summary Tiles ──
        private KpiTile tileActiveBranches = null!;
        private KpiTile tileTotalStaff = null!;
        private KpiTile tileActivePipeline = null!;
        private KpiTile tileRevenue = null!;
        private readonly List<KpiTile> _tiles = new();

        // ── Workbench Card ──
        private WorkbenchCard card = null!;
        private WorkbenchSearch searchBox = null!;
        private CheckBox chkIncludeInactive = null!;
        private Label lblCount = null!;
        private DataGridView dgv = null!;
        private WorkbenchState emptyState = null!;

        // ── Context Menu ──
        private ContextMenuStrip _actionsMenu = null!;
        private int _actionRowIndex = -1;
        private int _hoverRow = -1;
        private int _hoverCol = -1;

        // ── Constants & Helpers ──
        private const int ColCode = 0;
        private const int ColName = 1;
        private const int ColLocation = 2;
        private const int ColManager = 3;
        private const int ColStaff = 4;
        private const int ColRecords = 5;
        private const int ColStatus = 6;
        private const int ColActions = 7;

        private static readonly Color PrimaryAccent = Color.FromArgb(37, 99, 235);
        private static readonly Color Emerald = Color.FromArgb(16, 185, 129);
        private static readonly Color NeutralGray = Color.FromArgb(107, 114, 128);

        public BranchingControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            Dock = DockStyle.Fill;

            BuildActionsMenu();
            BuildUi();

            Load += async (s, e) => await ReloadAsync();
        }

        private void BuildActionsMenu()
        {
            _actionsMenu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                Font = UiKit.T.Body,
                Renderer = new QuietMenuRenderer()
            };

            var miEdit = new ToolStripMenuItem("✏️  Edit Branch") { Height = 32 };
            miEdit.Click += (s, e) => EditSelectedBranch();

            var miSwitch = new ToolStripMenuItem("📍  Switch Scope To This Branch") { Height = 32 };
            miSwitch.Click += (s, e) => SwitchScopeToSelectedBranch();

            var miToggle = new ToolStripMenuItem("⛔  Deactivate Branch") { Height = 32 };
            miToggle.Click += async (s, e) => await ToggleActiveStatusAsync();

            _actionsMenu.Items.AddRange(new ToolStripItem[] { miEdit, miSwitch, new ToolStripSeparator(), miToggle });

            _actionsMenu.Opening += (s, e) =>
            {
                var branch = SelectedBranch;
                if (branch == null)
                {
                    e.Cancel = true;
                    return;
                }

                bool isAdmin = string.Equals(UserSession.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(UserSession.Role, "Super Admin", StringComparison.OrdinalIgnoreCase);

                miEdit.Visible = isAdmin;
                miToggle.Visible = isAdmin;
                miToggle.Text = branch.IsActive ? "⛔  Deactivate Branch" : "✅  Reactivate Branch";

                // Non-admins can only see Switch if they are allowed (manager/staff locked)
                miSwitch.Enabled = isAdmin || (UserSession.UserBranchId.HasValue && UserSession.UserBranchId.Value == branch.BranchId);
            };
        }

        private BranchDto? SelectedBranch
        {
            get
            {
                if (_actionRowIndex >= 0 && _actionRowIndex < _filteredBranches.Count)
                    return _filteredBranches[_actionRowIndex];
                return null;
            }
        }

        private void BuildUi()
        {
            SuspendLayout();

            // ── Header ──
            lblTitle = new Label
            {
                Text = "Branch Management & Regional Directory",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            lblSubtitle = new Label
            {
                Text = "Centralized multi-store branch routing, localized performance metrics, and operational scoping",
                Font = AppTheme.FontPageSubtitle,
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            bool isAdmin = string.Equals(UserSession.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(UserSession.Role, "Super Admin", StringComparison.OrdinalIgnoreCase);

            btnNewBranch = new SaasButton("New Branch", SaasButtonVariant.Primary, "\uE710");
            btnNewBranch.Size = new Size(145, 36);
            btnNewBranch.Visible = isAdmin;
            btnNewBranch.Click += (s, e) => CreateNewBranch();

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Size = new Size(100, 36);
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnNewBranch);
            Controls.Add(btnRefresh);

            // ── KPI Summary Tiles (4 Cards) ──
            tileActiveBranches = AddTile("TOTAL ACTIVE BRANCHES", "0", "Operational store locations", PrimaryAccent, "\uE716");
            tileTotalStaff = AddTile("TOTAL STAFF", "0", "Assigned across branches", Emerald, "\uE77B");
            tileActivePipeline = AddTile("ACTIVE PIPELINE / RECORDS", "0", "Open tickets & customers", Color.FromArgb(139, 92, 246), "\uE8BD");
            tileRevenue = AddTile("REVENUE / PERFORMANCE", "₱0.00", "Total closed revenue", Color.FromArgb(245, 158, 11), "\uE9D5");

            // ── Workbench Card ──
            card = new WorkbenchCard { BackColor = UiKit.T.Surface };

            searchBox = new WorkbenchSearch
            {
                Placeholder = "Search by code, branch name, city, or manager..."
            };
            searchBox.QueryChanged += (s, e) => ApplyLocalFilter();

            chkIncludeInactive = new CheckBox
            {
                Text = "Include Inactive Branches",
                Font = UiKit.T.SmallStrong,
                ForeColor = AppTheme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Checked = false
            };
            chkIncludeInactive.CheckedChanged += async (s, e) => await ReloadAsync();

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

            emptyState = new WorkbenchState { Visible = false };
            emptyState.Show("\uE716", "No branch locations found",
                "Create a new branch location or adjust search query filters.");

            card.Controls.Add(searchBox);
            card.Controls.Add(chkIncludeInactive);
            card.Controls.Add(lblCount);
            card.Controls.Add(dgv);
            card.Controls.Add(emptyState);

            Controls.Add(card);

            Resize += (s, e) => LayoutUi();
            ResumeLayout(true);
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

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            int pad = UiKit.T.S6;
            int contentW = Math.Max(700, Width - pad * 2);

            lblTitle.Location = new Point(pad, pad);
            lblSubtitle.Location = new Point(pad, lblTitle.Bottom + 4);

            int btnY = pad;
            btnRefresh.Location = new Point(pad + contentW - btnRefresh.Width, btnY);
            btnNewBranch.Location = new Point(btnRefresh.Left - btnNewBranch.Width - 10, btnY);

            int y = lblSubtitle.Bottom + 20;

            // ── 4 KPI Tiles ──
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
            y += rowMaxH + 20;

            // ── Workbench Card ──
            int cardPad = UiKit.T.S6;
            int cardH = Math.Max(320, Height - y - pad);
            card.SetBounds(pad, y, contentW, cardH);

            int innerW = contentW - cardPad * 2;

            int chkWidth = chkIncludeInactive.PreferredSize.Width + 10;
            chkIncludeInactive.SetBounds(cardPad + innerW - chkWidth, cardPad + 8, chkWidth, 24);

            int searchW = Math.Max(260, Math.Min(480, innerW - chkWidth - 24));
            searchBox.SetBounds(cardPad, cardPad, searchW, 38);

            int gridY = searchBox.Bottom + 14;
            int gridH = cardH - gridY - cardPad - 24;

            dgv.SetBounds(cardPad, gridY, innerW, Math.Max(120, gridH));
            emptyState.SetBounds(cardPad, gridY, innerW, Math.Max(120, gridH));
            lblCount.Location = new Point(cardPad, dgv.Bottom + 6);
        }

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
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.GridColor = UiKit.T.LineSoft;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ScrollBars = ScrollBars.Both;
            g.RowTemplate.Height = 54;
            g.ColumnHeadersHeight = 38;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = AppTheme.Background,
                ForeColor = AppTheme.TextMuted,
                SelectionBackColor = AppTheme.Background,
                SelectionForeColor = AppTheme.TextMuted,
                Font = UiKit.T.SmallStrong,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0)
            };

            g.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = UiKit.T.Surface,
                ForeColor = AppTheme.TextPrimary,
                SelectionBackColor = Color.FromArgb(243, 244, 246),
                SelectionForeColor = AppTheme.TextPrimary,
                Font = AppTheme.FontBody,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0)
            };

            g.Columns.Add(MakeCol("colCode", "CODE", 12, DataGridViewContentAlignment.MiddleLeft));
            g.Columns.Add(MakeCol("colName", "BRANCH NAME", 24, DataGridViewContentAlignment.MiddleLeft));
            g.Columns.Add(MakeCol("colLocation", "LOCATION", 18, DataGridViewContentAlignment.MiddleLeft));
            g.Columns.Add(MakeCol("colManager", "APPOINTED MANAGER", 20, DataGridViewContentAlignment.MiddleLeft));
            g.Columns.Add(MakeCol("colStaff", "STAFF", 8, DataGridViewContentAlignment.MiddleCenter));
            g.Columns.Add(MakeCol("colRecords", "RECORDS", 8, DataGridViewContentAlignment.MiddleCenter));
            g.Columns.Add(MakeCol("colStatus", "STATUS", 10, DataGridViewContentAlignment.MiddleLeft));
            g.Columns.Add(MakeCol("colActions", "", 6, DataGridViewContentAlignment.MiddleCenter));

            g.CellPainting += Dgv_CellPainting;
            g.CellClick += Dgv_CellClick;
            g.CellMouseMove += Dgv_CellMouseMove;
            g.CellMouseLeave += (s, e) =>
            {
                _hoverRow = -1;
                _hoverCol = -1;
                dgv.Invalidate();
            };
        }

        private static DataGridViewTextBoxColumn MakeCol(string name, string header, float fillWeight, DataGridViewContentAlignment align)
        {
            return new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                FillWeight = fillWeight,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = align }
            };
        }

        public async Task ReloadAsync()
        {
            btnRefresh.Enabled = false;
            try
            {
                bool includeInactive = chkIncludeInactive.Checked;
                var branchesTask = _api.GetBranchesAsync(includeInactive: includeInactive);
                var statsTask = _api.GetBranchStatsAsync();

                await Task.WhenAll(branchesTask, statsTask);

                _allBranches = await branchesTask;
                var stats = await statsTask;

                if (stats != null)
                {
                    tileActiveBranches.Set("TOTAL ACTIVE BRANCHES", stats.TotalActiveBranches.ToString("N0"), "Operational locations", PrimaryAccent, "\uE716");
                    tileTotalStaff.Set("TOTAL STAFF", stats.TotalStaff.ToString("N0"), "Employees across branches", Emerald, "\uE77B");
                    tileActivePipeline.Set("ACTIVE PIPELINE / RECORDS", stats.ActivePipelineRecords.ToString("N0"), "Open tickets & customers", Color.FromArgb(139, 92, 246), "\uE8BD");
                    tileRevenue.Set("REVENUE / PERFORMANCE", $"₱{stats.TotalClosedRevenue:N2}", "Aggregated closed revenue", Color.FromArgb(245, 158, 11), "\uE9D5");
                }

                ApplyLocalFilter();

                // Also notify TopBar so persistent selector stays in sync
                var main = FindForm() as MainForm;
                if (main != null)
                {
                    var topBarField = main.Controls.Find("topBar", true).FirstOrDefault() as TopBarControl;
                    topBarField?.SetBranches(_allBranches.Where(b => b.IsActive).ToList());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load branch data:\n\n{ex.Message}", "Branch Directory Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnRefresh.Enabled = true;
            }
        }

        private void ApplyLocalFilter()
        {
            string q = searchBox.Query.Trim().ToLower();

            _filteredBranches = _allBranches.Where(b =>
            {
                if (string.IsNullOrWhiteSpace(q)) return true;
                return (b.BranchCode != null && b.BranchCode.ToLower().Contains(q)) ||
                       (b.BranchName != null && b.BranchName.ToLower().Contains(q)) ||
                       (b.City != null && b.City.ToLower().Contains(q)) ||
                       (b.StateOrProvince != null && b.StateOrProvince.ToLower().Contains(q)) ||
                       (b.ManagerName != null && b.ManagerName.ToLower().Contains(q));
            }).ToList();

            PopulateGrid();
        }

        private void PopulateGrid()
        {
            dgv.Rows.Clear();

            foreach (var b in _filteredBranches)
            {
                int idx = dgv.Rows.Add(
                    b.BranchCode,
                    b.BranchName,
                    b.LocationDisplay,
                    b.ManagerDisplay,
                    b.StaffCount.ToString(),
                    b.RecordsCount.ToString(),
                    b.StatusDisplay,
                    "⋮");

                dgv.Rows[idx].Tag = b;
            }

            lblCount.Text = $"Showing {_filteredBranches.Count} of {_allBranches.Count} branch locations";
            bool isEmpty = _filteredBranches.Count == 0;
            dgv.Visible = !isEmpty;
            emptyState.Visible = isEmpty;
        }

        private void Dgv_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (e.RowIndex >= _filteredBranches.Count) return;

            var b = _filteredBranches[e.RowIndex];
            var g = e.Graphics;
            UiKit.Quality(g);

            // Paint standard background & bottom border
            bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool isHovered = e.RowIndex == _hoverRow;

            Color bg = isSelected ? Color.FromArgb(240, 244, 255) : (isHovered ? Color.FromArgb(249, 250, 251) : UiKit.T.Surface);
            using (var brush = new SolidBrush(bg))
                g.FillRectangle(brush, e.CellBounds);

            using (var pen = new Pen(UiKit.T.LineSoft, 1))
                g.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);

            int cellX = e.CellBounds.X + 12;
            int cellY = e.CellBounds.Y;
            int cellW = e.CellBounds.Width - 24;
            int cellH = e.CellBounds.Height;

            switch (e.ColumnIndex)
            {
                case ColCode:
                    // Bold Code with distinct primary accent
                    using (var codeFont = AppFonts.Strong(10F))
                    {
                        TextRenderer.DrawText(g, b.BranchCode, codeFont,
                            new Rectangle(cellX, cellY, cellW, cellH), PrimaryAccent,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    }
                    e.Handled = true;
                    break;

                case ColName:
                    // Name on line 1, street address on line 2
                    using (var nameFont = AppFonts.Regular(9.5F))
                    {
                        var topRect = new Rectangle(cellX, cellY + 8, cellW, 18);
                        TextRenderer.DrawText(g, b.BranchName, nameFont, topRect, AppTheme.TextPrimary,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    }
                    var subRect = new Rectangle(cellX, cellY + 28, cellW, 16);
                    string addressText = !string.IsNullOrWhiteSpace(b.Address) ? b.Address : "—";
                    TextRenderer.DrawText(g, addressText, UiKit.T.Small, subRect, AppTheme.TextMuted,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    e.Handled = true;
                    break;

                case ColLocation:
                    TextRenderer.DrawText(g, b.LocationDisplay, UiKit.T.Body,
                        new Rectangle(cellX, cellY, cellW, cellH), AppTheme.TextSecondary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    e.Handled = true;
                    break;

                case ColManager:
                    // Initials avatar + Manager Name
                    if (!string.IsNullOrWhiteSpace(b.ManagerName))
                    {
                        int avatarSize = 28;
                        int avatarY = cellY + (cellH - avatarSize) / 2;
                        var avatarRect = new Rectangle(cellX, avatarY, avatarSize, avatarSize);

                        Color avatarBg = GetAvatarColor(b.ManagerName);
                        using (var ab = new SolidBrush(avatarBg))
                            g.FillEllipse(ab, avatarRect);

                        string initials = GetInitials(b.ManagerName);
                        UiKit.Text(g, initials, UiKit.T.SmallStrong, Color.White, avatarRect, UiKit.Center);

                        var textRect = new Rectangle(cellX + avatarSize + 10, cellY, cellW - avatarSize - 10, cellH);
                        TextRenderer.DrawText(g, b.ManagerName, AppFonts.Regular(9.5F), textRect, AppTheme.TextPrimary,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    }
                    else
                    {
                        TextRenderer.DrawText(g, "— Unassigned —", UiKit.T.Small,
                            new Rectangle(cellX, cellY, cellW, cellH), AppTheme.TextMuted,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    }
                    e.Handled = true;
                    break;

                case ColStaff:
                    // Centered count
                    TextRenderer.DrawText(g, b.StaffCount.ToString(), AppFonts.Regular(10F),
                        new Rectangle(e.CellBounds.X, cellY, e.CellBounds.Width, cellH), AppTheme.TextPrimary,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    e.Handled = true;
                    break;

                case ColRecords:
                    // Centered count
                    TextRenderer.DrawText(g, b.RecordsCount.ToString(), AppFonts.Regular(10F),
                        new Rectangle(e.CellBounds.X, cellY, e.CellBounds.Width, cellH), AppTheme.TextPrimary,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    e.Handled = true;
                    break;

                case ColStatus:
                    // Pill: Green for active, Gray for inactive
                    int pillW = 76;
                    int pillH = 24;
                    int pillX = cellX;
                    int pillY = cellY + (cellH - pillH) / 2;
                    var pillRect = new Rectangle(pillX, pillY, pillW, pillH);

                    Color pBg = b.IsActive ? Color.FromArgb(236, 253, 243) : Color.FromArgb(243, 244, 246);
                    Color pBorder = b.IsActive ? Color.FromArgb(166, 244, 197) : Color.FromArgb(209, 213, 219);
                    Color pText = b.IsActive ? Color.FromArgb(6, 118, 71) : NeutralGray;

                    UiKit.FillRounded(g, pillRect, 12, pBg);
                    using (var pPen = new Pen(pBorder, 1))
                    using (var path = UiKit.Rounded(new Rectangle(pillRect.X, pillRect.Y, pillRect.Width - 1, pillRect.Height - 1), 12))
                        g.DrawPath(pPen, path);

                    UiKit.Text(g, b.StatusDisplay, UiKit.T.SmallStrong, pText, pillRect, UiKit.Center);
                    e.Handled = true;
                    break;

                case ColActions:
                    // ⋮ Context menu button
                    int btnSize = 28;
                    int bx = e.CellBounds.X + (e.CellBounds.Width - btnSize) / 2;
                    int by = cellY + (cellH - btnSize) / 2;
                    var actionRect = new Rectangle(bx, by, btnSize, btnSize);

                    bool isActionHover = isHovered && _hoverCol == ColActions;
                    if (isActionHover)
                    {
                        UiKit.FillRounded(g, actionRect, 6, Color.FromArgb(229, 231, 235));
                    }

                    using (var actFont = AppFonts.Strong(12F))
                    {
                        TextRenderer.DrawText(g, "⋮", actFont, actionRect, AppTheme.TextSecondary,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                    e.Handled = true;
                    break;
            }
        }

        private void Dgv_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex != _hoverRow || e.ColumnIndex != _hoverCol)
            {
                int old = _hoverRow;
                _hoverRow = e.RowIndex;
                _hoverCol = e.ColumnIndex;
                if (old >= 0 && old < dgv.RowCount) dgv.InvalidateRow(old);
                if (_hoverRow >= 0 && _hoverRow < dgv.RowCount) dgv.InvalidateRow(_hoverRow);
            }
        }

        private void Dgv_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _filteredBranches.Count) return;

            if (e.ColumnIndex == ColActions)
            {
                _actionRowIndex = e.RowIndex;
                var cellRect = dgv.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
                _actionsMenu.Show(dgv, new Point(cellRect.Left - 100, cellRect.Bottom));
            }
        }

        private void CreateNewBranch()
        {
            using var dlg = new BranchFormDialog();
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _ = ReloadAsync();
                SaasToast.Show(this, "Branch Created", ToastKind.Success, "New branch location has been successfully registered.");
            }
        }

        private void EditSelectedBranch()
        {
            var branch = SelectedBranch;
            if (branch == null) return;

            using var dlg = new BranchFormDialog(branch);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _ = ReloadAsync();
                SaasToast.Show(this, "Branch Updated", ToastKind.Success, $"Branch '{branch.BranchCode}' was successfully updated.");
            }
        }

        private void SwitchScopeToSelectedBranch()
        {
            var branch = SelectedBranch;
            if (branch == null) return;

            UserSession.SetSelectedBranch(branch.BranchId, branch.BranchName);
            SaasToast.Show(this, "Branch Scope Changed", ToastKind.Info,
                $"Operational scope switched to [{branch.BranchCode}] {branch.BranchName}. Data is now scoped to this branch.");
        }

        private async Task ToggleActiveStatusAsync()
        {
            var branch = SelectedBranch;
            if (branch == null) return;

            string action = branch.IsActive ? "Deactivate" : "Reactivate";
            var form = FindForm();
            if (form == null) return;

            bool confirm = SaasConfirm.Ask(form,
                $"{action} Branch {branch.BranchCode}?",
                $"Are you sure you want to {action.ToLower()} '{branch.BranchName}'?\nLinked records will be preserved safely.",
                confirmText: action,
                danger: branch.IsActive);

            if (!confirm) return;

            try
            {
                await _api.ToggleBranchActiveAsync(branch.BranchId);
                await ReloadAsync();
                SaasToast.Show(this, $"Branch {action}d", ToastKind.Success,
                    $"Branch '{branch.BranchName}' has been {(branch.IsActive ? "deactivated" : "reactivated")}.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to change branch status:\n\n{ex.Message}", "Status Change Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string GetInitials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "—";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
        }

        private static Color GetAvatarColor(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Color.FromArgb(107, 114, 128);
            Color[] colors =
            {
                Color.FromArgb(99, 102, 241),
                Color.FromArgb(16, 185, 129),
                Color.FromArgb(245, 158, 11),
                Color.FromArgb(236, 72, 153),
                Color.FromArgb(59, 130, 246),
                Color.FromArgb(124, 58, 237)
            };
            int h = Math.Abs(name.GetHashCode());
            return colors[h % colors.Length];
        }

        // ═══════════ KPI TILE ═══════════

        [DesignerCategory("Code")]
        private sealed class KpiTile : Control
        {
            private const int Pad = 20;
            private const int IconSize = 36;
            private const int MinHeight = 126;

            private string _label = "";
            private string _number = "0";
            private string _sub = "";
            private Color _accent = AppTheme.Primary;
            private string _glyph = "";

            public KpiTile()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
            }

            public void Set(string label, string number, string sub, Color accent, string glyph)
            {
                _label = label;
                _number = number;
                _sub = sub;
                _accent = accent;
                _glyph = glyph;
                Invalidate();
            }

            public int HeightFor(int width) => MinHeight;

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var bg = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(bg, ClientRectangle);

                UiKit.Card(g, ClientRectangle, UiKit.T.Radius, UiKit.T.Surface, UiKit.T.Line);

                var iconRect = new Rectangle(Pad, Pad, IconSize, IconSize);
                UiKit.FillRounded(g, iconRect, 10, UiKit.Wash(_accent));
                using (var f = UiKit.GlyphFont(13F))
                    UiKit.Text(g, _glyph, f, _accent, iconRect, UiKit.Center);

                int textX = Pad + IconSize + 12;
                int textW = Width - textX - Pad;

                UiKit.Text(g, _label, UiKit.T.SmallStrong, UiKit.T.InkMuted,
                    new Rectangle(textX, Pad + 2, textW, 16),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                using (var numFont = AppFonts.Strong(18F))
                {
                    TextRenderer.DrawText(g, _number, numFont,
                        new Rectangle(textX, Pad + 24, textW, 28), UiKit.T.Ink,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }

                UiKit.Text(g, _sub, UiKit.T.Small, UiKit.T.InkMuted,
                    new Rectangle(textX, Pad + 56, textW, 16),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        // ═══════════ QUIET MENU RENDERER ═══════════

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