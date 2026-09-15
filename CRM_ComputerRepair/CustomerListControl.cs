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
    public class CustomerListControl : UserControl
    {
        private readonly ApiClient _api = new ApiClient();
        private List<CustomerDto> _allCustomers = new List<CustomerDto>();

        // Stat tiles
        private StatTile tileTotal = null!;
        private StatTile tileActive = null!;
        private StatTile tileNew = null!;
        private StatTile tileArchived = null!;

        // Header
        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private Button btnAdd = null!;
        private Button btnRefresh = null!;

        // Grid card
        private ShadowCard cardGrid = null!;
        private Label lblGridTitle = null!;
        private IconInput inpSearch = null!;
        private CheckBox chkShowArchived = null!;
        private DataGridView dgv = null!;
        private Label lblEmpty = null!;

        // Actions column
        private const string ColActions = "colActions";
        private ContextMenuStrip _actionsMenu = null!;
        private int _menuRowIndex = -1;

        public CustomerListControl()
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
            _actionsMenu.Items.Add(new ToolStripSeparator());
            _actionsMenu.Items.Add("Archive");

            _actionsMenu.Items[0].Click += OnMenuView;
            _actionsMenu.Items[1].Click += OnMenuUpdate;
            _actionsMenu.Items[3].Click += OnMenuArchive;

            _actionsMenu.Opening += (s, e) =>
            {
                if (_menuRowIndex < 0) return;

                if (dgv.Rows[_menuRowIndex].DataBoundItem is not CustomerDto c)
                    return;

                var archiveItem = _actionsMenu.Items[3];
                archiveItem.Text = c.IsActive ? "Archive" : "Restore";
            };
        }

        private void OnMenuView(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not CustomerDto c) return;

            MessageBox.Show(
                $"Customer details\n\n" +
                $"ID:       {c.CustomerId}\n" +
                $"Name:     {c.FullName}\n" +
                $"Email:    {c.Email ?? "—"}\n" +
                $"Phone:    {c.Phone ?? "—"}\n" +
                $"Address:  {c.Address ?? "—"}\n" +
                $"Status:   {c.Status}\n" +
                $"Created:  {c.CreatedAt:yyyy-MM-dd}",
                "View Customer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OnMenuUpdate(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not CustomerDto c) return;
            OpenEditDialog(c);
        }

        private async void OnMenuArchive(object? sender, EventArgs e)
        {
            if (_menuRowIndex < 0) return;
            if (dgv.Rows[_menuRowIndex].DataBoundItem is not CustomerDto c) return;

            if (c.IsActive)
                await ArchiveCustomerAsync(c);
            else
                await RestoreCustomerAsync(c);
        }

        // ═══════════ UI BUILD ═══════════

        private void BuildUi()
        {
            // ── Header ──
            lblTitle = new Label
            {
                Text = "Customers",
                Font = AppTheme.FontTitle,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Data Collection Summary",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnAdd = MakePrimaryButton("+   Add Customer");
            btnAdd.Click += (s, e) => OpenAddDialog();

            btnRefresh = MakeSecondaryButton("Refresh");
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnAdd);
            Controls.Add(btnRefresh);

            // ── Stat tiles ──
            tileTotal = new StatTile { Icon = "\uE716", Label = "Total Customers" };
            tileActive = new StatTile { Icon = "\uE73E", Label = "Active" };
            tileNew = new StatTile { Icon = "\uE710", Label = "New This Month" };
            tileArchived = new StatTile { Icon = "\uE74D", Label = "Archived" };

            tileTotal.SetColors(AppTheme.TilePinkBg, AppTheme.TilePinkFg);
            tileActive.SetColors(AppTheme.TileGreenBg, AppTheme.TileGreenFg);
            tileNew.SetColors(AppTheme.TileOrangeBg, AppTheme.TileOrangeFg);
            tileArchived.SetColors(AppTheme.TilePurpleBg, AppTheme.TilePurpleFg);

            Controls.Add(tileTotal);
            Controls.Add(tileActive);
            Controls.Add(tileNew);
            Controls.Add(tileArchived);

            // ── Grid card ──
            cardGrid = new ShadowCard
            {
                Padding = new Padding(24, 20, 24, 20)
            };

            lblGridTitle = new Label
            {
                Text = "Customer List",
                Font = AppTheme.FontSection,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(24, 18)
            };

            inpSearch = new IconInput
            {
                Icon = "\uE721",
                PlaceholderText = "Search by name, email, phone, or address...",
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

            // ── Empty state ──
            lblEmpty = new Label
            {
                Text = "No customers yet.\nClick \"Add Customer\" to get started.",
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

        // ═══════════ LAYOUT (35% cards / 65% table) ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            int pad = 0;

            // ── Header ──
            int headerY = pad;
            lblTitle.Location = new Point(pad, headerY);

            int subtitleY = headerY + lblTitle.PreferredHeight + 4;
            lblSubtitle.Location = new Point(pad + 2, subtitleY);

            int btnY = headerY + 4;
            btnRefresh.Location = new Point(Width - btnRefresh.Width - pad, btnY);
            btnAdd.Location = new Point(btnRefresh.Left - btnAdd.Width - 12, btnY);

            int headerBottom = subtitleY + lblSubtitle.PreferredHeight;
            int contentTop = headerBottom + 24;

            // ── Available content area ──
            int contentAvail = Height - contentTop - pad;

            // 35% cards / 65% table
            int cardsHeight = (int)(contentAvail * 0.35);
            int tableHeight = contentAvail - cardsHeight;

            // Ensure minimums
            if (cardsHeight < 160) cardsHeight = 160;
            if (tableHeight < 220) tableHeight = 220;

            // ── Cards row ──
            int tileGap = 16;
            int tileHeight = cardsHeight - 12;

            int avail = Width - pad * 2;
            int tileWidth = (avail - tileGap * 3) / 4;

            int tilesTop = contentTop;

            tileTotal.Location = new Point(pad, tilesTop);
            tileActive.Location = new Point(pad + (tileWidth + tileGap), tilesTop);
            tileNew.Location = new Point(pad + (tileWidth + tileGap) * 2, tilesTop);
            tileArchived.Location = new Point(pad + (tileWidth + tileGap) * 3, tilesTop);

            tileTotal.Size = new Size(tileWidth, tileHeight);
            tileActive.Size = new Size(tileWidth, tileHeight);
            tileNew.Size = new Size(tileWidth, tileHeight);
            tileArchived.Size = new Size(tileWidth, tileHeight);

            // ── Grid card ──
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
                _allCustomers = await _api.GetCustomersAsync(chkShowArchived.Checked);
                UpdateStats();
                ApplySearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load customers:\n\n{ex.Message}",
                    "API Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void UpdateStats()
        {
            var now = DateTime.UtcNow;
            tileTotal.Number = _allCustomers.Count.ToString();
            tileActive.Number = _allCustomers.Count(c => c.IsActive).ToString();
            tileNew.Number = _allCustomers.Count(c =>
                c.CreatedAt.Year == now.Year &&
                c.CreatedAt.Month == now.Month).ToString();
            tileArchived.Number = _allCustomers.Count(c => !c.IsActive).ToString();
        }

        private void ApplySearch()
        {
            var term = inpSearch.Text?.Trim() ?? string.Empty;

            List<CustomerDto> view;

            if (string.IsNullOrEmpty(term))
            {
                view = _allCustomers;
            }
            else
            {
                view = _allCustomers.Where(c =>
                    (c.FirstName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (c.LastName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (c.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (c.Phone ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (c.Address ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
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
            foreach (var hidden in new[] { "LoyaltyPoints", "CreatedAt", "FullName", "IsActive" })
                if (dgv.Columns[hidden] != null)
                    dgv.Columns[hidden].Visible = false;

            if (dgv.Columns["CustomerId"] != null)
            {
                dgv.Columns["CustomerId"].HeaderText = "ID";
                dgv.Columns["CustomerId"].Width = 60;
                dgv.Columns["CustomerId"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            }
            if (dgv.Columns["Status"] != null)
            {
                dgv.Columns["Status"].HeaderText = "Status";
                dgv.Columns["Status"].Width = 90;
                dgv.Columns["Status"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            }
            if (dgv.Columns["FirstName"] != null)
                dgv.Columns["FirstName"].HeaderText = "First Name";
            if (dgv.Columns["LastName"] != null)
                dgv.Columns["LastName"].HeaderText = "Last Name";
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

                var cellRect = dgv.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                var menuPos = new Point(cellRect.Left, cellRect.Bottom);
                _actionsMenu.Show(dgv, menuPos);
            }
        }

        // ═══════════ ARCHIVE / RESTORE ═══════════

        private async Task ArchiveCustomerAsync(CustomerDto customer)
        {
            var confirm = MessageBox.Show(
                $"Archive \"{customer.FullName}\"?\n\nData is preserved and can be restored.",
                "Confirm Archive",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                await _api.ArchiveCustomerAsync(customer.CustomerId);
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

        private async Task RestoreCustomerAsync(CustomerDto customer)
        {
            try
            {
                await _api.RestoreCustomerAsync(customer.CustomerId);
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

        // ═══════════ STYLE GRID ═══════════

        private void Dgv_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dgv.Columns[e.ColumnIndex].Name == "Status" && e.Value is string status)
            {
                bool isActive = status == "Active";
                e.CellStyle.ForeColor = isActive ? AppTheme.Success : AppTheme.TextMuted;
                e.CellStyle.Font = new Font(AppTheme.FontBody, FontStyle.Bold);
            }
        }

        // ═══════════ MODAL LAUNCHERS ═══════════

        private void OpenAddDialog()
        {
            using var dlg = new CustomerFormDialog(null);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                _ = ReloadAsync();
            }
        }

        private void OpenEditDialog(CustomerDto customer)
        {
            using var dlg = new CustomerFormDialog(customer.CustomerId, customer);
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
                Size = new Size(170, AppTheme.ButtonHeight),
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