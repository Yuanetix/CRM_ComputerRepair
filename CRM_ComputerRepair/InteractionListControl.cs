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
    public class InteractionListControl : UserControl
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private readonly InteractionTypeFilter _type;
        private List<InteractionDto> _all = new List<InteractionDto>();

        // ═══════════ CONTROLS ═══════════

        private StatTile tileTotal = null!;
        private StatTile tileOpen = null!;
        private StatTile tileInProgress = null!;
        private StatTile tileClosed = null!;

        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private Button btnAdd = null!;
        private Button btnRefresh = null!;

        private ShadowCard cardGrid = null!;
        private Label lblGridTitle = null!;
        private IconInput inpSearch = null!;
        private CheckBox chkShowArchived = null!;
        private DataGridView dgv = null!;
        private Label lblEmpty = null!;

        private const string ColActions = "colActions";
        private ContextMenuStrip _actionsMenu = null!;
        private int _menuRowIndex = -1;

        // ═══════════ CONSTRUCTOR ═══════════

        public InteractionListControl(InteractionTypeFilter type)
        {
            _type = type;

            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            AutoScaleMode = AutoScaleMode.Font;

            BuildActionsMenu();
            BuildUi();

            this.Load += async (s, e) => await ReloadAsync();
        }

        // ═══════════ TYPE-AWARE LABELS ═══════════

        private string TitleText => _type switch
        {
            InteractionTypeFilter.Inquiry => "Inquiries",
            InteractionTypeFilter.Complaint => "Complaints",
            InteractionTypeFilter.Feedback => "Feedback",
            _ => "Interactions"
        };

        private string SingularText => _type switch
        {
            InteractionTypeFilter.Inquiry => "Inquiry",
            InteractionTypeFilter.Complaint => "Complaint",
            InteractionTypeFilter.Feedback => "Feedback",
            _ => "Interaction"
        };

        private string SubtitleText => _type switch
        {
            InteractionTypeFilter.Inquiry => "Customer questions and information requests",
            InteractionTypeFilter.Complaint => "Customer complaints and issue reports",
            InteractionTypeFilter.Feedback => "Customer feedback and satisfaction notes",
            _ => "Data Collection"
        };

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
            _actionsMenu.Items.Add(new ToolStripSeparator());
            _actionsMenu.Items.Add("Archive");

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
            };
        }

        private void OnMenuView(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not InteractionDto dto) return;

            MessageBox.Show(
                $"{SingularText} details\n\n" +
                $"ID:          {dto.CustomerInteractionId}\n" +
                $"Type:        {dto.TypeText}\n" +
                $"Subject:     {dto.Subject}\n" +
                $"Priority:    {dto.PriorityText}\n" +
                $"Status:      {dto.StatusText}\n" +
                $"Created:     {dto.InteractionDate:yyyy-MM-dd HH:mm}\n" +
                (dto.UpdatedAt.HasValue ? $"Updated:     {dto.UpdatedAt.Value:yyyy-MM-dd HH:mm}\n" : "") +
                (dto.ClosedAt.HasValue ? $"Closed:      {dto.ClosedAt.Value:yyyy-MM-dd HH:mm}\n" : "") +
                $"Active:      {dto.ActivityStatus}\n\n" +
                $"Notes:\n{dto.Notes}\n\n" +
                (string.IsNullOrWhiteSpace(dto.Resolution) ? "" : $"Resolution:\n{dto.Resolution}"),
                $"View {SingularText}",
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

        // ═══════════ UI BUILD ═══════════

        private void BuildUi()
        {
            lblTitle = new Label
            {
                Text = TitleText,
                Font = AppTheme.FontTitle,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = SubtitleText,
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnAdd = MakePrimaryButton($"+   Add {SingularText}");
            btnAdd.Click += (s, e) => OpenAddDialog();

            btnRefresh = MakeSecondaryButton("Refresh");
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnAdd);
            Controls.Add(btnRefresh);

            tileTotal = new StatTile { Icon = "\uE716", Label = "Total" };
            tileOpen = new StatTile { Icon = "\uE710", Label = "Open" };
            tileInProgress = new StatTile { Icon = "\uE823", Label = "In Progress" };
            tileClosed = new StatTile { Icon = "\uE73E", Label = "Closed" };

            tileTotal.SetColors(AppTheme.TilePinkBg, AppTheme.TilePinkFg);
            tileOpen.SetColors(AppTheme.TileOrangeBg, AppTheme.TileOrangeFg);
            tileInProgress.SetColors(AppTheme.TilePurpleBg, AppTheme.TilePurpleFg);
            tileClosed.SetColors(AppTheme.TileGreenBg, AppTheme.TileGreenFg);

            Controls.Add(tileTotal);
            Controls.Add(tileOpen);
            Controls.Add(tileInProgress);
            Controls.Add(tileClosed);

            cardGrid = new ShadowCard
            {
                Padding = new Padding(24, 20, 24, 20)
            };

            lblGridTitle = new Label
            {
                Text = $"{TitleText} List",
                Font = AppTheme.FontSection,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(24, 18)
            };

            inpSearch = new IconInput
            {
                Icon = "\uE721",
                PlaceholderText = "Search by subject, notes, or resolution...",
                Size = new Size(360, AppTheme.InputHeight)
            };
            inpSearch.InnerTextBox.TextChanged += (s, e) => ApplySearch();

            chkShowArchived = new CheckBox
            {
                Text = "Show archived",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            chkShowArchived.CheckedChanged += async (s, e) => await ReloadAsync();

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
                Text = $"No {TitleText.ToLower()} yet.\nClick \"Add {SingularText}\" to get started.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = AppTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Visible = false
            };

            cardGrid.Controls.Add(lblGridTitle);
            cardGrid.Controls.Add(inpSearch);
            cardGrid.Controls.Add(chkShowArchived);
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
            tileOpen.Location = new Point(pad + (tileWidth + tileGap), tilesTop);
            tileInProgress.Location = new Point(pad + (tileWidth + tileGap) * 2, tilesTop);
            tileClosed.Location = new Point(pad + (tileWidth + tileGap) * 3, tilesTop);

            tileTotal.Size = new Size(tileWidth, tileHeight);
            tileOpen.Size = new Size(tileWidth, tileHeight);
            tileInProgress.Size = new Size(tileWidth, tileHeight);
            tileClosed.Size = new Size(tileWidth, tileHeight);

            int gridCardTop = contentTop + cardsHeight;

            cardGrid.Location = new Point(pad, gridCardTop);
            cardGrid.Size = new Size(Width - pad * 2, tableHeight);

            int cp = 24;

            inpSearch.Location = new Point(cp, 52);
            inpSearch.Size = new Size(360, AppTheme.InputHeight);
            chkShowArchived.Location = new Point(cp + 380, 58);

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
                _all = await _api.GetInteractionsAsync(
                    _type,
                    includeArchived: chkShowArchived.Checked);

                UpdateStats();
                ApplySearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load {TitleText.ToLower()}:\n\n{ex.Message}",
                    "API Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void UpdateStats()
        {
            tileTotal.Number = _all.Count.ToString();
            tileOpen.Number = _all.Count(x => x.Status == 0).ToString();
            tileInProgress.Number = _all.Count(x => x.Status == 1).ToString();
            tileClosed.Number = _all.Count(x => x.Status == 2).ToString();
        }

        private void ApplySearch()
        {
            var term = inpSearch.Text?.Trim() ?? string.Empty;

            List<InteractionDto> view;

            if (string.IsNullOrEmpty(term))
            {
                view = _all;
            }
            else
            {
                view = _all.Where(x =>
                    (x.Subject ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Notes ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Resolution ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
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
                "CustomerInteractionId", "CustomerId", "RepairRequestId",
                "InteractionType", "Status", "Priority",
                "InteractionByUserId", "UpdatedAt", "ClosedAt", "IsActive",
                "Notes", "Resolution"
            })
            {
                if (dgv.Columns[hidden] != null)
                    dgv.Columns[hidden].Visible = false;
            }

            if (dgv.Columns["Subject"] != null)
                dgv.Columns["Subject"].HeaderText = "Subject";

            if (dgv.Columns["TypeText"] != null)
                dgv.Columns["TypeText"].HeaderText = "Type";

            if (dgv.Columns["PriorityText"] != null)
                dgv.Columns["PriorityText"].HeaderText = "Priority";

            if (dgv.Columns["StatusText"] != null)
            {
                dgv.Columns["StatusText"].HeaderText = "Status";
                dgv.Columns["StatusText"].Width = 110;
                dgv.Columns["StatusText"].AutoSizeMode =
                    DataGridViewAutoSizeColumnMode.None;
            }

            if (dgv.Columns["ActivityStatus"] != null)
            {
                dgv.Columns["ActivityStatus"].HeaderText = "Record";
                dgv.Columns["ActivityStatus"].Width = 90;
                dgv.Columns["ActivityStatus"].AutoSizeMode =
                    DataGridViewAutoSizeColumnMode.None;
            }

            if (dgv.Columns["InteractionDate"] != null)
                dgv.Columns["InteractionDate"].HeaderText = "Created";
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

        // ═══════════ ARCHIVE / RESTORE ═══════════

        private async Task ArchiveAsync(InteractionDto dto)
        {
            var confirm = MessageBox.Show(
                $"Archive this {SingularText.ToLower()}?\n\n" +
                $"\"{dto.Subject}\"\n\n" +
                "Data is preserved and can be restored.",
                "Confirm Archive",
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
                    $"Archive failed:\n\n{ex.Message}",
                    "Error",
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
                    $"Restore failed:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
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
                    "Open" => (AppTheme.Warning, AppTheme.WarningSoft),
                    "In Progress" => (AppTheme.Primary, AppTheme.PrimarySoft),
                    "Closed" => (AppTheme.Success, AppTheme.SuccessSoft),
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
            using var dlg = new InteractionFormDialog(_type, null);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                _ = ReloadAsync();
            }
        }

        private void OpenEditDialog(InteractionDto dto)
        {
            using var dlg = new InteractionFormDialog(_type, dto);
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
                Size = new Size(190, AppTheme.ButtonHeight),
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