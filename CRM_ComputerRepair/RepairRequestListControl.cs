using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    [DesignerCategory("Code")]
    public class RepairRequestListControl : UserControl
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private List<RepairRequestDto> _all = new List<RepairRequestDto>();

        // ═══════════ CONTROLS ═══════════

        private StatTile tileTotal = null!;
        private StatTile tilePending = null!;
        private StatTile tileInProgress = null!;
        private StatTile tileCompleted = null!;

        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private Button btnAdd = null!;
        private Button btnRefresh = null!;

        private ShadowCard cardGrid = null!;
        private Label lblGridTitle = null!;
        private IconInput inpSearch = null!;
        private DataGridView dgv = null!;
        private Label lblEmpty = null!;

        private const string ColActions = "colActions";
        private ContextMenuStrip _actionsMenu = null!;
        private int _menuRowIndex = -1;

        // ═══════════ CONSTRUCTOR ═══════════

        public RepairRequestListControl()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            AutoScaleMode = AutoScaleMode.Font;

            BuildActionsMenu();
            BuildUi();

            this.Load += async (s, e) => await ReloadAsync();
        }

        // ═══════════ ACTIONS MENU ═══════════

        private void BuildActionsMenu()
        {
            _actionsMenu = new ContextMenuStrip
            {
                Font = new Font("Segoe UI", 9F),
                ShowImageMargin = false,
                BackColor = Color.White,
                RenderMode = ToolStripRenderMode.System
            };

            _actionsMenu.Items.Add("View");
            _actionsMenu.Items.Add("Update");
            _actionsMenu.Items.Add("Approve");
            _actionsMenu.Items.Add("Start Repair");
            _actionsMenu.Items.Add("Complete");
            _actionsMenu.Items.Add("Reject");
            _actionsMenu.Items.Add(new ToolStripSeparator());
            _actionsMenu.Items.Add("Assign to me");

            _actionsMenu.Items[0].Click += OnMenuView;
            _actionsMenu.Items[1].Click += OnMenuUpdate;
            _actionsMenu.Items[2].Click += async (s, e) => await QuickStatusAsync(1);   // Approved
            _actionsMenu.Items[3].Click += async (s, e) => await QuickStatusAsync(2);   // InProgress
            _actionsMenu.Items[4].Click += async (s, e) => await QuickStatusAsync(3);   // Completed
            _actionsMenu.Items[5].Click += async (s, e) => await QuickStatusAsync(4);   // Rejected
            _actionsMenu.Items[7].Click += async (s, e) => await AssignToMeAsync();

            _actionsMenu.Opening += (s, e) =>
            {
                if (_menuRowIndex < 0) return;
                if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto)
                    return;

                // Enable/disable based on current status
                // Only Approved when Pending
                _actionsMenu.Items[2].Enabled = dto.Status == 0;
                // Only Start Repair when Approved
                _actionsMenu.Items[3].Enabled = dto.Status == 1;
                // Only Complete when InProgress
                _actionsMenu.Items[4].Enabled = dto.Status == 2;
                // Only Reject when Pending or Approved
                _actionsMenu.Items[5].Enabled = dto.Status == 0 || dto.Status == 1;
                // Only Assign when not Completed or Rejected
                _actionsMenu.Items[7].Enabled = dto.Status != 3 && dto.Status != 4;
            };
        }

        private void OnMenuView(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;

            var cost = dto.ActualCost.HasValue
                ? $"Actual:  ₱{dto.ActualCost.Value:N2}"
                : dto.EstimatedCost.HasValue
                    ? $"Est:     ₱{dto.EstimatedCost.Value:N2}"
                    : "Cost:    —";

            MessageBox.Show(
                $"Repair Request details\n\n" +
                $"Request #:   {dto.RequestNumber}\n" +
                $"Device:      {dto.DeviceModel}\n" +
                $"Serial:      {dto.SerialNumber}\n" +
                $"Status:      {dto.StatusText}\n" +
                $"Priority:    {dto.PriorityText}\n" +
                $"Requested:   {dto.RequestDate:yyyy-MM-dd HH:mm}\n" +
                (dto.CompletionDate.HasValue ? $"Completed:   {dto.CompletionDate.Value:yyyy-MM-dd HH:mm}\n" : "") +
                $"Assigned to: {dto.AssignedToStaffId ?? "—"}\n" +
                $"{cost}\n\n" +
                $"Issue:\n{dto.IssueDescription}\n\n" +
                (string.IsNullOrWhiteSpace(dto.TechnicianNotes) ? "" : $"Technician notes:\n{dto.TechnicianNotes}"),
                "View Repair Request",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OnMenuUpdate(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;
            OpenEditDialog(dto);
        }

        private async Task QuickStatusAsync(int newStatus)
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;

            try
            {
                dto.Status = newStatus;
                await _api.UpdateRepairRequestAsync(dto.RepairRequestId, dto);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Update failed:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async Task AssignToMeAsync()
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not RepairRequestDto dto) return;

            try
            {
                dto.AssignedToStaffId = UserSession.UserId;
                await _api.UpdateRepairRequestAsync(dto.RepairRequestId, dto);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Assign failed:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ═══════════ UI BUILD ═══════════

        private void BuildUi()
        {
            lblTitle = new Label
            {
                Text = "Repair Requests",
                Font = AppTheme.FontTitle,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Repair job intake and workflow",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnAdd = MakePrimaryButton("+   New Repair Request");
            btnAdd.Click += (s, e) => OpenAddDialog();

            btnRefresh = MakeSecondaryButton("Refresh");
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnAdd);
            Controls.Add(btnRefresh);

            tileTotal = new StatTile { Icon = "\uE716", Label = "Total" };
            tilePending = new StatTile { Icon = "\uE823", Label = "Pending" };
            tileInProgress = new StatTile { Icon = "\uE90F", Label = "In Progress" };
            tileCompleted = new StatTile { Icon = "\uE73E", Label = "Completed" };

            tileTotal.SetColors(AppTheme.TilePinkBg, AppTheme.TilePinkFg);
            tilePending.SetColors(AppTheme.TileOrangeBg, AppTheme.TileOrangeFg);
            tileInProgress.SetColors(AppTheme.TilePurpleBg, AppTheme.TilePurpleFg);
            tileCompleted.SetColors(AppTheme.TileGreenBg, AppTheme.TileGreenFg);

            Controls.Add(tileTotal);
            Controls.Add(tilePending);
            Controls.Add(tileInProgress);
            Controls.Add(tileCompleted);

            cardGrid = new ShadowCard
            {
                Padding = new Padding(24, 20, 24, 20)
            };

            lblGridTitle = new Label
            {
                Text = "Repair Request List",
                Font = AppTheme.FontSection,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(24, 18)
            };

            inpSearch = new IconInput
            {
                Icon = "\uE721",
                PlaceholderText = "Search by request #, device, serial, issue...",
                Size = new Size(360, AppTheme.InputHeight)
            };
            inpSearch.InnerTextBox.TextChanged += (s, e) => ApplySearch();

            dgv = new DataGridView
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                       | AnchorStyles.Left | AnchorStyles.Right
            };
            UiHelpers.StyleGrid(dgv);
            dgv.CellFormatting += Dgv_CellFormatting;
            dgv.CellClick += Dgv_CellClick;

            lblEmpty = new Label
            {
                Text = "No repair requests yet.\nClick \"New Repair Request\" to get started.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = AppTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Visible = false
            };

            cardGrid.Controls.Add(lblGridTitle);
            cardGrid.Controls.Add(inpSearch);
            cardGrid.Controls.Add(dgv);
            cardGrid.Controls.Add(lblEmpty);

            Controls.Add(cardGrid);

            Resize += (s, e) => LayoutUi();
        }

        // ═══════════ LAYOUT ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            int pad = 0;

            int headerY = pad;
            lblTitle.Location = new Point(pad, headerY);

            int subtitleY = headerY + lblTitle.PreferredHeight + 4;
            lblSubtitle.Location = new Point(pad + 2, subtitleY);

            int btnY = headerY + 4;
            btnRefresh.Location = new Point(Width - btnRefresh.Width - pad, btnY);
            btnAdd.Location = new Point(btnRefresh.Left - btnAdd.Width - 12, btnY);

            int headerBottom = subtitleY + lblSubtitle.PreferredHeight;
            int contentTop = headerBottom + 24;

            int contentAvail = Height - contentTop - pad;

            int cardsHeight = (int)(contentAvail * 0.35);
            int tableHeight = contentAvail - cardsHeight;

            if (cardsHeight < 160) cardsHeight = 160;
            if (tableHeight < 220) tableHeight = 220;

            int tileGap = 16;
            int tileHeight = cardsHeight - 12;

            int avail = Width - pad * 2;
            int tileWidth = (avail - tileGap * 3) / 4;

            int tilesTop = contentTop;

            tileTotal.Location = new Point(pad, tilesTop);
            tilePending.Location = new Point(pad + (tileWidth + tileGap), tilesTop);
            tileInProgress.Location = new Point(pad + (tileWidth + tileGap) * 2, tilesTop);
            tileCompleted.Location = new Point(pad + (tileWidth + tileGap) * 3, tilesTop);

            tileTotal.Size = new Size(tileWidth, tileHeight);
            tilePending.Size = new Size(tileWidth, tileHeight);
            tileInProgress.Size = new Size(tileWidth, tileHeight);
            tileCompleted.Size = new Size(tileWidth, tileHeight);

            int gridCardTop = contentTop + cardsHeight;

            cardGrid.Location = new Point(pad, gridCardTop);
            cardGrid.Size = new Size(Width - pad * 2, tableHeight);

            int cp = 24;

            inpSearch.Location = new Point(cp, 52);
            inpSearch.Size = new Size(480, AppTheme.InputHeight);

            int gridTop = 106;
            int gridW = cardGrid.Width - cp * 2;
            int gridH = cardGrid.Height - gridTop - cp;

            if (gridW > 100 && gridH > 50)
            {
                dgv.Location = new Point(cp, gridTop);
                dgv.Size = new Size(gridW, gridH);
                lblEmpty.Location = new Point(cp, gridTop);
                lblEmpty.Size = new Size(gridW, gridH);
            }
        }

        // ═══════════ DATA ═══════════

        public async Task ReloadAsync()
        {
            try
            {
                _all = await _api.GetRepairRequestsAsync();
                UpdateStats();
                ApplySearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load repair requests:\n\n{ex.Message}",
                    "API Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void UpdateStats()
        {
            tileTotal.Number = _all.Count.ToString();
            tilePending.Number = _all.Count(x => x.Status == 0).ToString();
            tileInProgress.Number = _all.Count(x => x.Status == 1 || x.Status == 2).ToString();
            tileCompleted.Number = _all.Count(x => x.Status == 3).ToString();
        }

        private void ApplySearch()
        {
            var term = inpSearch.Text?.Trim() ?? string.Empty;

            List<RepairRequestDto> view;

            if (string.IsNullOrEmpty(term))
            {
                view = _all;
            }
            else
            {
                view = _all.Where(x =>
                    (x.RequestNumber ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.DeviceModel ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.SerialNumber ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.IssueDescription ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.TechnicianNotes ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            dgv.DataSource = null;
            dgv.DataSource = view;
            HideInternalColumns();
            AddActionsColumn();

            bool empty = view.Count == 0;
            lblEmpty.Visible = empty;
            dgv.Visible = !empty;
        }

        private void HideInternalColumns()
        {
            foreach (var hidden in new[]
            {
                "RepairRequestId", "CustomerId", "DeviceId",
                "Status", "Priority",
                "ActualCost", "PartsCost", "LaborCost",
                "AssignedToManagerId"
            })
            {
                if (dgv.Columns[hidden] != null)
                    dgv.Columns[hidden].Visible = false;
            }

            if (dgv.Columns["RequestNumber"] != null)
                dgv.Columns["RequestNumber"].HeaderText = "Request #";

            if (dgv.Columns["DeviceModel"] != null)
                dgv.Columns["DeviceModel"].HeaderText = "Device";

            if (dgv.Columns["SerialNumber"] != null)
                dgv.Columns["SerialNumber"].HeaderText = "Serial";

            if (dgv.Columns["IssueDescription"] != null)
                dgv.Columns["IssueDescription"].HeaderText = "Issue";

            if (dgv.Columns["StatusText"] != null)
            {
                dgv.Columns["StatusText"].HeaderText = "Status";
                dgv.Columns["StatusText"].Width = 110;
                dgv.Columns["StatusText"].AutoSizeMode =
                    DataGridViewAutoSizeColumnMode.None;
            }

            if (dgv.Columns["PriorityText"] != null)
            {
                dgv.Columns["PriorityText"].HeaderText = "Priority";
                dgv.Columns["PriorityText"].Width = 90;
                dgv.Columns["PriorityText"].AutoSizeMode =
                    DataGridViewAutoSizeColumnMode.None;
            }

            if (dgv.Columns["RequestDate"] != null)
                dgv.Columns["RequestDate"].HeaderText = "Requested";

            if (dgv.Columns["CompletionDate"] != null)
                dgv.Columns["CompletionDate"].HeaderText = "Completed";

            if (dgv.Columns["EstimatedCost"] != null)
                dgv.Columns["EstimatedCost"].HeaderText = "Est. Cost";

            if (dgv.Columns["AssignedToStaffId"] != null)
                dgv.Columns["AssignedToStaffId"].HeaderText = "Assigned";
        }

        // ═══════════ ACTIONS COLUMN ═══════════

        private void AddActionsColumn()
        {
            if (dgv.Columns[ColActions] != null)
                dgv.Columns.Remove(ColActions);

            var col = new DataGridViewButtonColumn
            {
                Name = ColActions,
                HeaderText = "",
                Text = "⋮",
                UseColumnTextForButtonValue = true,
                FlatStyle = FlatStyle.Flat,
                Width = 45,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            col.DefaultCellStyle.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            col.DefaultCellStyle.ForeColor = AppTheme.TextSecondary;
            col.DefaultCellStyle.BackColor = Color.White;
            col.DefaultCellStyle.SelectionBackColor = AppTheme.PrimarySoft;
            col.DefaultCellStyle.SelectionForeColor = AppTheme.TextPrimary;
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            dgv.Columns.Add(col);
        }

        private void Dgv_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var colName = dgv.Columns[e.ColumnIndex].Name;

            if (colName == ColActions)
            {
                _menuRowIndex = e.RowIndex;

                var cellRect = dgv.GetCellDisplayRectangle(
                    e.ColumnIndex, e.RowIndex, true);
                var menuPos = new Point(cellRect.Left, cellRect.Bottom);
                _actionsMenu.Show(dgv, menuPos);
            }
        }

        // ═══════════ CELL FORMATTING ═══════════

        private void Dgv_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var colName = dgv.Columns[e.ColumnIndex].Name;

            if (colName == "StatusText" && e.Value is string status)
            {
                (Color fg, Color bg) = status switch
                {
                    "Pending" => (AppTheme.Warning, AppTheme.WarningSoft),
                    "Approved" => (AppTheme.Primary, AppTheme.PrimarySoft),
                    "In Progress" => (AppTheme.Primary, AppTheme.PrimarySoft),
                    "Completed" => (AppTheme.Success, AppTheme.SuccessSoft),
                    "Rejected" => (AppTheme.Danger, AppTheme.DangerSoft),
                    "Reassigned" => (AppTheme.TextSecondary, AppTheme.Neutral),
                    _ => (AppTheme.TextSecondary, Color.Transparent)
                };
                e.CellStyle.ForeColor = fg;
                e.CellStyle.Font = new Font(AppTheme.FontBody, FontStyle.Bold);
                e.CellStyle.SelectionForeColor = fg;
            }

            if (colName == "PriorityText" && e.Value is string priority)
            {
                e.CellStyle.ForeColor = priority switch
                {
                    "Urgent" => AppTheme.Danger,
                    "High" => AppTheme.Danger,
                    "Medium" => AppTheme.Warning,
                    "Low" => AppTheme.TextSecondary,
                    _ => AppTheme.TextSecondary
                };
                e.CellStyle.Font = new Font(AppTheme.FontBody, FontStyle.Bold);
                e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
            }
        }

        // ═══════════ MODAL LAUNCHERS ═══════════

        private void OpenAddDialog()
        {
            using var dlg = new RepairRequestFormDialog(null);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                _ = ReloadAsync();
            }
        }

        private void OpenEditDialog(RepairRequestDto dto)
        {
            using var dlg = new RepairRequestFormDialog(dto);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                _ = ReloadAsync();
            }
        }

        // ═══════════ BUTTON FACTORIES ═══════════

        private Button MakePrimaryButton(string text)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9F),
                BackColor = AppTheme.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(220, AppTheme.ButtonHeight),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = AppTheme.PrimaryHover;
            btn.FlatAppearance.MouseDownBackColor = AppTheme.PrimaryActive;
            btn.Resize += (s, e) =>
                UiHelpers.ApplyRoundedRegion(btn, AppTheme.ButtonRadius);
            return btn;
        }

        private Button MakeSecondaryButton(string text)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9F),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(120, AppTheme.ButtonHeight),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = AppTheme.BorderStrong;
            btn.FlatAppearance.MouseOverBackColor = AppTheme.Neutral;
            btn.Resize += (s, e) =>
                UiHelpers.ApplyRoundedRegion(btn, AppTheme.ButtonRadius);
            return btn;
        }
    }
}