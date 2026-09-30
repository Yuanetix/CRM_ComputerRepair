using CRM.winforms.Auth;
using CRM.winforms.Controls;
using CRM.winforms.Forms;
using CRM.winforms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Professional Customer History 360° Dossier for Staff.
    /// Timeline of repairs, interactions, and follow-ups for any customer.
    /// UI aligned with Customers / Repairs / Subscriptions / Admin / System Monitor.
    /// </summary>
    [DesignerCategory("Code")]
    public class CustomerHistoryControl : UserControl
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private List<CustomerDto> _allCustomers = new();
        private CustomerHistoryDto? _currentHistory;

        // ═══════════ HEADER CONTROLS ═══════════

        private Label lblTitle = null!;
        private Label lblSummary = null!;
        private WorkbenchSearch cmbCustomer = null!;   // read: original was ComboBox; see picker note below
        private ComboBox customerPicker = null!;
        private SaasButton btnReload = null!;
        private SaasButton btnAdd = null!;

        // ═══════════ KPI TILES ═══════════

        private KpiTile tileRepairs = null!;
        private KpiTile tileActiveBench = null!;
        private KpiTile tileInteractions = null!;
        private KpiTile tileFollowUps = null!;
        private readonly List<KpiTile> _tiles = new();

        // ═══════════ WORKBENCH CARD ═══════════

        private WorkbenchCard card = null!;

        // ── Dossier banner (flat, no border) ──
        private Panel pnlDossier = null!;
        private Label lblAvatar = null!;
        private Label lblFullName = null!;
        private Label lblCustomerIdBadge = null!;
        private Label lblLoyaltyBadge = null!;
        private Label lblContactInfo = null!;
        private SaasButton btnCallCustomer = null!;
        private SaasButton btnEmailCustomer = null!;
        private SaasButton btnIntakeRepair = null!;
        private SaasButton btnLogInteraction = null!;
        private SaasButton btnScheduleFollowUp = null!;

        // ── Toolbar ──
        private Label lblCount = null!;
        private HistorySegmentedFilter filterBar = null!;
        private WorkbenchSearch searchBox = null!;

        // ── Grids ──
        private DataGridView dgvRepairs = null!;
        private DataGridView dgvInteractions = null!;
        private DataGridView dgvFollowUps = null!;

        private WorkbenchState emptyState = null!;
        private TablePagination pager = null!;

        private const string ColActions = "colActions";
        private ContextMenuStrip _repairsMenu = null!;
        private ContextMenuStrip _interactionsMenu = null!;
        private ContextMenuStrip _followUpsMenu = null!;
        private int _menuRowIndex = -1;
        private int _hoverRow = -1;

        public event EventHandler<string>? ActionRequested;

        // ═══════════ SHARED DRAWING HELPERS ═══════════
        private const int CellPadX = 14;
        private const TextFormatFlags Flat = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        private const TextFormatFlags CellText = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | Flat;

        private static readonly Font MonoFont = new Font("Consolas", 9F);
        private static readonly Color[] AvatarPalette =
        {
            Color.FromArgb(99, 102, 241),
            Color.FromArgb(16, 185, 129),
            Color.FromArgb(245, 158, 11),
            Color.FromArgb(236, 72, 153),
            Color.FromArgb(59, 130, 246),
            Color.FromArgb(124, 58, 237)
        };

        private static int MeasureW(string text, Font font) =>
            TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
                Flat | TextFormatFlags.SingleLine).Width;

        private static Color AvatarColor(string? seed)
        {
            if (string.IsNullOrWhiteSpace(seed)) return AvatarPalette[0];
            int h = 0;
            foreach (char ch in seed) h = (h * 31 + ch) & 0x7FFFFFFF;
            return AvatarPalette[h % AvatarPalette.Length];
        }

        // ═══════════ CONSTRUCTOR ═══════════

        public CustomerHistoryControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            AutoScaleMode = AutoScaleMode.Font;

            BuildMenus();
            BuildUi();

            this.Load += async (s, e) => await InitializeAsync();
        }

        // ═══════════ INITIALIZATION ═══════════

        private async Task InitializeAsync()
        {
            emptyState.Show("\uE77B", "Loading customers",
                "Fetching the customer list for the dossier picker…");
            try
            {
                _allCustomers = await _api.GetCustomersAsync() ?? new List<CustomerDto>();
                PopulateCustomerDropdown();

                if (_currentHistory == null && _allCustomers.Count > 0)
                    customerPicker.SelectedIndex = 1;
            }
            catch (Exception ex)
            {
                emptyState.Show("\uE783", "Failed to load customers", ex.Message);
            }
        }

        private void PopulateCustomerDropdown()
        {
            customerPicker.Items.Clear();
            customerPicker.Items.Add("— Select a customer dossier —");

            foreach (var c in _allCustomers.OrderBy(x => x.FullName))
            {
                string contact = !string.IsNullOrWhiteSpace(c.Phone)
                    ? c.Phone
                    : (!string.IsNullOrWhiteSpace(c.Email) ? c.Email : "No contact");
                customerPicker.Items.Add($"{c.FullName} ({contact})");
            }

            if (customerPicker.Items.Count > 0)
                customerPicker.SelectedIndex = 0;
        }

        public async Task LoadCustomerAsync(int customerId)
        {
            if (_allCustomers.Count == 0)
            {
                _allCustomers = await _api.GetCustomersAsync() ?? new List<CustomerDto>();
                PopulateCustomerDropdown();
            }

            int idx = _allCustomers.OrderBy(x => x.FullName).ToList().FindIndex(c => c.CustomerId == customerId);
            if (idx >= 0)
                customerPicker.SelectedIndex = idx + 1;
            else
                await FetchAndDisplayHistory(customerId);
        }

        // ═══════════ UI BUILD ═══════════

        private void BuildUi()
        {
            // ── Header ──
            lblTitle = new Label
            {
                Text = "Customer History",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            lblSummary = new Label
            {
                Text = "360° timeline of repairs, interactions, and follow-ups",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            customerPicker = new ComboBox
            {
                Font = UiKit.T.Body,
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DropDownWidth = 540,
                Width = 360,
                BackColor = UiKit.T.Surface,
                ForeColor = UiKit.T.Ink,
                FlatStyle = FlatStyle.Flat
            };
            customerPicker.Items.Add("Loading registered customers...");
            customerPicker.SelectedIndex = 0;
            customerPicker.SelectedIndexChanged += async (s, e) =>
            {
                if (customerPicker.SelectedIndex > 0 && customerPicker.SelectedIndex - 1 < _allCustomers.Count)
                {
                    var cust = _allCustomers.OrderBy(c => c.FullName).ToList()[customerPicker.SelectedIndex - 1];
                    await FetchAndDisplayHistory(cust.CustomerId);
                }
                else
                {
                    ClearDossierDisplay();
                }
            };
            customerPicker.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    int foundIdx = customerPicker.FindStringExact(customerPicker.Text.Trim());
                    if (foundIdx < 0) foundIdx = customerPicker.FindString(customerPicker.Text.Trim());
                    if (foundIdx > 0) customerPicker.SelectedIndex = foundIdx;
                }
            };
            customerPicker.Leave += (s, e) =>
            {
                if (customerPicker.SelectedIndex <= 0 && !string.IsNullOrWhiteSpace(customerPicker.Text))
                {
                    int foundIdx = customerPicker.FindStringExact(customerPicker.Text.Trim());
                    if (foundIdx < 0) foundIdx = customerPicker.FindString(customerPicker.Text.Trim());
                    if (foundIdx > 0) customerPicker.SelectedIndex = foundIdx;
                }
            };

            btnReload = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnReload.Size = new Size(100, 36);
            btnReload.Click += async (s, e) =>
            {
                if (customerPicker.SelectedIndex > 0 && customerPicker.SelectedIndex - 1 < _allCustomers.Count)
                {
                    var cust = _allCustomers.OrderBy(c => c.FullName).ToList()[customerPicker.SelectedIndex - 1];
                    await FetchAndDisplayHistory(cust.CustomerId);
                }
                else
                {
                    await InitializeAsync();
                }
            };

            btnAdd = new SaasButton("Intake repair", SaasButtonVariant.Primary, "\uE710");
            btnAdd.Size = new Size(150, 36);
            btnAdd.Click += (s, e) => OpenIntakeRepairDialog();

            Controls.Add(lblTitle);
            Controls.Add(lblSummary);
            Controls.Add(customerPicker);
            Controls.Add(btnReload);
            Controls.Add(btnAdd);

            // ── KPI Tiles (map to segments) ──
            tileRepairs = AddTile("TOTAL REPAIRS", "0", "All repair tickets for this customer", AppTheme.Primary, "\uE9D5");
            tileRepairs.Click += (s, e) => SelectSegment(0);

            tileActiveBench = AddTile("ACTIVE BENCH", "0", "Open tickets still in progress", Color.FromArgb(59, 130, 246), "\uE9F5");
            tileActiveBench.Click += (s, e) => SelectSegment(0);

            tileInteractions = AddTile("INTERACTIONS", "0", "Inquiries, concerns, reviews", AppTheme.Warning, "\uE8BD");
            tileInteractions.Click += (s, e) => SelectSegment(1);

            tileFollowUps = AddTile("FOLLOW-UPS", "0", "Scheduled and completed outreaches", AppTheme.Success, "\uE823");
            tileFollowUps.Click += (s, e) => SelectSegment(2);

            // ── Workbench Card ──
            card = new WorkbenchCard { BackColor = UiKit.T.Surface };

            // ── Dossier banner (flat) ──
            pnlDossier = new Panel
            {
                BackColor = Color.Transparent,
                Height = 84
            };
            pnlDossier.Paint += Dossier_Paint;

            lblAvatar = new Label
            {
                Text = "C",
                Font = AppFonts.Strong(14F),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(46, 46)
            };
            lblAvatar.Paint += Avatar_Paint;
            pnlDossier.Controls.Add(lblAvatar);

            lblFullName = new Label
            {
                Text = "Select a customer from the picker",
                Font = AppFonts.Strong(13F),
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            pnlDossier.Controls.Add(lblFullName);

            lblCustomerIdBadge = new Label
            {
                Text = "#0",
                Font = UiKit.T.SmallStrong,
                ForeColor = UiKit.T.InkMuted,
                BackColor = Color.Transparent,
                AutoSize = true,
                Visible = false
            };
            lblCustomerIdBadge.Paint += Badge_Paint;
            pnlDossier.Controls.Add(lblCustomerIdBadge);

            lblLoyaltyBadge = new Label
            {
                Text = "★ 0 pts",
                Font = UiKit.T.SmallStrong,
                ForeColor = Color.FromArgb(180, 83, 9),
                BackColor = Color.Transparent,
                AutoSize = true,
                Visible = false
            };
            lblLoyaltyBadge.Paint += Badge_Paint;
            pnlDossier.Controls.Add(lblLoyaltyBadge);

            lblContactInfo = new Label
            {
                Text = "No customer dossier selected",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            pnlDossier.Controls.Add(lblContactInfo);

            btnCallCustomer = new SaasButton("Call", SaasButtonVariant.Secondary, "\uE717") { Visible = false };
            btnCallCustomer.Click += OnCallCustomer;
            pnlDossier.Controls.Add(btnCallCustomer);

            btnEmailCustomer = new SaasButton("Email", SaasButtonVariant.Secondary, "\uE715") { Visible = false };
            btnEmailCustomer.Click += OnEmailCustomer;
            pnlDossier.Controls.Add(btnEmailCustomer);

            btnIntakeRepair = new SaasButton("+ Intake repair", SaasButtonVariant.Primary, "\uE710") { Visible = false };
            btnIntakeRepair.Click += (s, e) => OpenIntakeRepairDialog();
            pnlDossier.Controls.Add(btnIntakeRepair);

            btnLogInteraction = new SaasButton("+ Interaction", SaasButtonVariant.Secondary, "\uE8BD") { Visible = false };
            btnLogInteraction.Click += (s, e) => OpenLogInteractionDialog();
            pnlDossier.Controls.Add(btnLogInteraction);

            btnScheduleFollowUp = new SaasButton("+ Follow-up", SaasButtonVariant.Secondary, "\uE823") { Visible = false };
            btnScheduleFollowUp.Click += (s, e) => OpenScheduleFollowUpDialog();
            pnlDossier.Controls.Add(btnScheduleFollowUp);

            card.Controls.Add(pnlDossier);

            // ── Toolbar ──
            searchBox = new WorkbenchSearch
            {
                Placeholder = "Search history records..."
            };
            searchBox.QueryChanged += (s, e) => ApplySearch();

            filterBar = new HistorySegmentedFilter(new (string, int?)[]
            {
                ("Repairs", 0),
                ("Interactions", 1),
                ("Follow-Ups", 2)
            });
            filterBar.SelectionChanged += (s, tag) => SetActiveSegment(tag ?? 0);

            lblCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                UseMnemonic = false
            };

            // ── Grids ──
            dgvRepairs = MakeGrid();
            dgvInteractions = MakeGrid();
            dgvFollowUps = MakeGrid();

            InitGridColumns();
            HookGridEvents(dgvRepairs, 0);
            HookGridEvents(dgvInteractions, 1);
            HookGridEvents(dgvFollowUps, 2);

            emptyState = new WorkbenchState { Visible = true };
            emptyState.Show("\uE77B", "Customer service dossier",
                "Select a customer from the picker to load their complete repair tickets, inquiries, and follow-up history.");

            pager = new TablePagination();
            pager.PageChanged += (s, e) => ApplySearch(resetPage: false);

            card.Controls.Add(searchBox);
            card.Controls.Add(filterBar);
            card.Controls.Add(lblCount);
            card.Controls.Add(dgvRepairs);
            card.Controls.Add(dgvInteractions);
            card.Controls.Add(dgvFollowUps);
            card.Controls.Add(emptyState);
            card.Controls.Add(pager);

            Controls.Add(card);

            Resize += (s, e) => LayoutUi();
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

        private void SelectSegment(int index)
        {
            filterBar.SetKey(index);
            SetActiveSegment(index);
        }

        // ═══════════ KEYBOARD ═══════════

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F5:
                    if (_currentHistory != null)
                        _ = FetchAndDisplayHistory(_currentHistory.CustomerId);
                    else
                        _ = InitializeAsync();
                    return true;

                case Keys.Control | Keys.F:
                    searchBox.Focus();
                    return true;

                case Keys.Control | Keys.N:
                    OpenIntakeRepairDialog();
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ═══════════ LAYOUT ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            int pad = UiKit.T.S6;
            int contentW = Math.Max(760, Width - pad * 2);

            // Header
            lblTitle.Location = new Point(pad, pad);

            int btnY = pad;
            btnAdd.Location = new Point(pad + contentW - btnAdd.Width, btnY);
            btnReload.Location = new Point(btnAdd.Left - btnReload.Width - 10, btnY);

            int pickerW = Math.Max(280, Math.Min(380, btnReload.Left - pad - 340));
            customerPicker.Size = new Size(pickerW, 34);
            customerPicker.Location = new Point(btnReload.Left - pickerW - 10, btnY + 1);

            lblSummary.Location = new Point(pad + 1, lblTitle.Bottom + 6);

            int y = lblSummary.Bottom + 22;

            // KPI tiles
            int tileCols = contentW >= 1100 ? 4 : contentW >= 760 ? 2 : 1;
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
            y += rowMaxH + 24;

            // Card
            int cardPad = UiKit.T.S6;
            int cardH = Math.Max(340, Height - y - pad);
            card.SetBounds(pad, y, contentW, cardH);

            int innerW = contentW - cardPad * 2;

            // Dossier banner
            pnlDossier.SetBounds(cardPad, cardPad, innerW, 84);

            // Dossier layout
            lblAvatar.Location = new Point(0, (pnlDossier.Height - lblAvatar.Height) / 2);
            lblFullName.Location = new Point(lblAvatar.Right + 14, 14);
            lblCustomerIdBadge.Location = new Point(lblFullName.Right + 10, lblFullName.Top + 4);
            lblLoyaltyBadge.Location = new Point(lblCustomerIdBadge.Right + 8, lblFullName.Top + 4);
            lblContactInfo.Location = new Point(lblAvatar.Right + 14, lblFullName.Bottom + 6);

            // Right-aligned action buttons inside the dossier
            int btnRight = pnlDossier.Width;
            foreach (var b in new[] { btnScheduleFollowUp, btnLogInteraction, btnIntakeRepair, btnEmailCustomer, btnCallCustomer })
            {
                if (!b.Visible) continue;
                b.Size = new Size(b.PreferredWidth, 32);
                btnRight -= b.Width + 8;
                b.Location = new Point(btnRight, (pnlDossier.Height - b.Height) / 2);
            }

            // Toolbar row
            int toolbarY = pnlDossier.Bottom + 18;

            int filterW = filterBar.PreferredWidth;
            int searchW = Math.Max(240, Math.Min(440, innerW - filterW - 20));

            searchBox.SetBounds(cardPad, toolbarY, searchW, 38);
            filterBar.SetBounds(cardPad + innerW - filterW, toolbarY + 1, filterW, 36);

            int gridY = searchBox.Bottom + 14;
            int footerH = pager.Visible ? TableKit.FooterH : 0;
            int gridH = cardH - (gridY - (cardPad)) - cardPad - 26 - footerH;
            // gridY is in card coords; use card height relative to card top
            gridH = card.Height - gridY - cardPad - 26 - footerH;

            var gridBounds = new Rectangle(cardPad, gridY, innerW, Math.Max(120, gridH));

            dgvRepairs.SetBounds(gridBounds.X, gridBounds.Y, gridBounds.Width, gridBounds.Height);
            dgvInteractions.SetBounds(gridBounds.X, gridBounds.Y, gridBounds.Width, gridBounds.Height);
            dgvFollowUps.SetBounds(gridBounds.X, gridBounds.Y, gridBounds.Width, gridBounds.Height);
            emptyState.SetBounds(gridBounds.X, gridBounds.Y, gridBounds.Width, gridBounds.Height);

            if (pager.Visible)
                pager.SetBounds(cardPad, gridBounds.Bottom, innerW, TableKit.FooterH);

            lblCount.Location = new Point(cardPad, gridY - 22);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            UiKit.Quality(e.Graphics);

            // hairline under the picker row
            int y = lblSummary.Bottom + 14;
            using var pen = new Pen(UiKit.T.LineSoft, 1);
            e.Graphics.DrawLine(pen, UiKit.T.S6, y, Width - UiKit.T.S6, y);
        }

        // ═══════════ DOSSIER BANNER PAINT ═══════════

        private void Avatar_Paint(object? sender, PaintEventArgs e)
        {
            UiKit.Quality(e.Graphics);
            var lbl = (Label)sender!;
            var bg = AvatarColor(lblFullName.Text);
            UiKit.FillRounded(e.Graphics, new Rectangle(0, 0, lbl.Width - 1, lbl.Height - 1), 23, bg);
            UiKit.Text(e.Graphics, lbl.Text, lbl.Font, Color.White,
                new Rectangle(0, 0, lbl.Width, lbl.Height), UiKit.Center);
        }

        private void Badge_Paint(object? sender, PaintEventArgs e)
        {
            UiKit.Quality(e.Graphics);
            var lbl = (Label)sender!;
            Color bg = lbl == lblLoyaltyBadge
                ? Color.FromArgb(254, 243, 199)
                : UiKit.T.LineSoft;

            var r = new Rectangle(0, 0, lbl.Width - 1, lbl.Height - 1);
            UiKit.FillRounded(e.Graphics, r, r.Height / 2, bg);
            UiKit.Text(e.Graphics, lbl.Text, lbl.Font, lbl.ForeColor, r, UiKit.Center);
        }

        private void Dossier_Paint(object? sender, PaintEventArgs e)
        {
            UiKit.Quality(e.Graphics);

            // hairline above and below the dossier to separate it from the toolbar
            using var pen = new Pen(UiKit.T.LineSoft, 1);
            e.Graphics.DrawLine(pen, 0, pnlDossier.Height - 1, pnlDossier.Width, pnlDossier.Height - 1);
        }

        // ═══════════ SEGMENTS & TABS ═══════════

        private void SetActiveSegment(int index)
        {
            dgvRepairs.Visible = (index == 0);
            dgvInteractions.Visible = (index == 1);
            dgvFollowUps.Visible = (index == 2);

            ApplySearch();
            LayoutUi();
        }

        // ═══════════ DATA FETCH & DISPLAY ═══════════

        private void ClearDossierDisplay()
        {
            _currentHistory = null;
            lblAvatar.Text = "C";
            lblFullName.Text = "Select a customer from the picker";
            lblCustomerIdBadge.Visible = false;
            lblLoyaltyBadge.Visible = false;
            lblContactInfo.Text = "No customer dossier selected";

            btnCallCustomer.Visible = false;
            btnEmailCustomer.Visible = false;
            btnIntakeRepair.Visible = false;
            btnLogInteraction.Visible = false;
            btnScheduleFollowUp.Visible = false;

            tileRepairs.Set("TOTAL REPAIRS", "0", "All repair tickets for this customer", AppTheme.Primary, "\uE9D5");
            tileActiveBench.Set("ACTIVE BENCH", "0", "Open tickets still in progress", Color.FromArgb(59, 130, 246), "\uE9F5");
            tileInteractions.Set("INTERACTIONS", "0", "Inquiries, concerns, reviews", AppTheme.Warning, "\uE8BD");
            tileFollowUps.Set("FOLLOW-UPS", "0", "Scheduled and completed outreaches", AppTheme.Success, "\uE823");

            dgvRepairs.Rows.Clear();
            dgvInteractions.Rows.Clear();
            dgvFollowUps.Rows.Clear();

            emptyState.Show("\uE77B", "Customer service dossier",
                "Select a customer from the picker to load their complete repair tickets, inquiries, and follow-up history.");
            emptyState.Visible = true;
            dgvRepairs.Visible = false;
            dgvInteractions.Visible = false;
            dgvFollowUps.Visible = false;

            Invalidate();
        }

        private async Task FetchAndDisplayHistory(int customerId)
        {
            emptyState.Show("\uE77B", "Loading history…", "Fetching this customer's dossier.");
            emptyState.Visible = true;
            dgvRepairs.Visible = false;
            dgvInteractions.Visible = false;
            dgvFollowUps.Visible = false;

            try
            {
                var data = await _api.GetCustomerHistoryAsync(customerId);
                if (data == null)
                {
                    emptyState.Show("\uE77B", "Customer not found",
                        $"No history records found for customer #{customerId}.");
                    return;
                }

                _currentHistory = data;

                string initials = "C";
                if (!string.IsNullOrWhiteSpace(data.FirstName)) initials = data.FirstName[0].ToString().ToUpper();
                if (!string.IsNullOrWhiteSpace(data.LastName)) initials += data.LastName[0].ToString().ToUpper();
                lblAvatar.Text = initials;

                lblFullName.Text = data.FullName;
                lblCustomerIdBadge.Text = $"#{data.CustomerId}";
                lblCustomerIdBadge.Visible = true;

                lblLoyaltyBadge.Text = $"★ {data.LoyaltyPoints ?? 0} pts";
                lblLoyaltyBadge.Visible = true;

                string phone = !string.IsNullOrWhiteSpace(data.Phone) ? data.Phone : "No phone";
                string email = !string.IsNullOrWhiteSpace(data.Email) ? data.Email : "No email";
                string address = !string.IsNullOrWhiteSpace(data.FullAddress)
                    ? $"  ·  {data.FullAddress}"
                    : (!string.IsNullOrWhiteSpace(data.Address) ? $"  ·  {data.Address}" : "");
                string since = $"  ·  Member since {data.CreatedAt:MMM yyyy}";
                lblContactInfo.Text = $"{phone}  ·  {email}{address}{since}";

                btnCallCustomer.Visible = !string.IsNullOrWhiteSpace(data.Phone);
                btnEmailCustomer.Visible = !string.IsNullOrWhiteSpace(data.Email);
                btnIntakeRepair.Visible = true;
                btnLogInteraction.Visible = true;
                btnScheduleFollowUp.Visible = true;

                int activeJobs = data.Repairs.Count(r => r.Status != 3 && r.Status != 4);
                int totalRepairs = data.Repairs.Count;

                tileRepairs.Set("TOTAL REPAIRS", totalRepairs.ToString(),
                    "All repair tickets for this customer", AppTheme.Primary, "\uE9D5");
                tileActiveBench.Set("ACTIVE BENCH", activeJobs.ToString(),
                    "Open tickets still in progress", Color.FromArgb(59, 130, 246), "\uE9F5");
                tileInteractions.Set("INTERACTIONS", data.Interactions.Count.ToString(),
                    "Inquiries, concerns, reviews", AppTheme.Warning, "\uE8BD");
                tileFollowUps.Set("FOLLOW-UPS", data.FollowUps.Count.ToString(),
                    "Scheduled and completed outreaches", AppTheme.Success, "\uE823");

                lblSummary.Text =
                    $"{data.FullName}  ·  {totalRepairs} repairs  ·  {activeJobs} on bench  ·  {data.Interactions.Count} interactions  ·  {data.FollowUps.Count} follow-ups";

                filterBar.UpdateCounts(
                    (0, totalRepairs),
                    (1, data.Interactions.Count),
                    (2, data.FollowUps.Count));

                ApplySearch();
                LayoutUi();
            }
            catch (Exception ex)
            {
                emptyState.Show("\uE783", "Failed to load history", ex.Message);
                emptyState.Visible = true;
                dgvRepairs.Visible = false;
                dgvInteractions.Visible = false;
                dgvFollowUps.Visible = false;

                SaasToast.Show(FindForm(),
                    $"Couldn't load customer history: {ex.Message}",
                    ToastKind.Danger);
            }
        }

        // ═══════════ SEARCH & FILTERING ═══════════

        private void ApplySearch(bool resetPage = true)
        {
            if (resetPage) pager.Reset();
            if (_currentHistory == null)
            {
                emptyState.Visible = true;
                return;
            }

            string q = (searchBox.Query ?? "").Trim();
            int selectedTab = filterBar.SelectedTag ?? 0;

            if (selectedTab == 0)
            {
                var repairs = _currentHistory.Repairs.AsEnumerable();
                if (!string.IsNullOrEmpty(q))
                {
                    repairs = repairs.Where(r =>
                        (r.RequestNumber?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (r.DeviceModel?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (r.IssueDescription?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (r.StatusText?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (r.PriorityText?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
                }

                var list = repairs.ToList();
                pager.SetTotal(list.Count);
                var page = pager.Slice(list);
                lblCount.Text = $"Showing {list.Count} of {_currentHistory.Repairs.Count} repairs";

                dgvRepairs.Rows.Clear();
                foreach (var r in page)
                {
                    int idx = dgvRepairs.Rows.Add(
                        r.PriorityText, r.RequestNumber, r.DeviceModel,
                        r.IssueDescription, r.StatusText,
                        r.RequestDate.ToString("MMM d, yyyy"),
                        r.CostDisplay, "");
                    dgvRepairs.Rows[idx].Tag = r;
                }
                dgvRepairs.ClearSelection();

                bool hasData = list.Count > 0;
                dgvRepairs.Visible = hasData;
                emptyState.Visible = !hasData;

                if (hasData) emptyState.Clear();
                else
                    emptyState.Show("\uE9D9", "No repair requests",
                        string.IsNullOrEmpty(q)
                            ? "This customer has no recorded repair orders yet."
                            : $"No repair requests match \u201c{q}\u201d.");
            }
            else if (selectedTab == 1)
            {
                var interactions = _currentHistory.Interactions.AsEnumerable();
                if (!string.IsNullOrEmpty(q))
                {
                    interactions = interactions.Where(i =>
                        (i.Subject?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (i.Notes?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (i.TypeText?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (i.StatusText?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (i.Resolution?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
                }

                var list = interactions.ToList();
                pager.SetTotal(list.Count);
                var page = pager.Slice(list);
                lblCount.Text = $"Showing {list.Count} of {_currentHistory.Interactions.Count} interactions";

                dgvInteractions.Rows.Clear();
                foreach (var i in page)
                {
                    int idx = dgvInteractions.Rows.Add(
                        i.TypeText, i.Subject, i.Notes, i.StatusText,
                        i.InteractionDate.ToString("MMM d, yyyy"), "");
                    dgvInteractions.Rows[idx].Tag = i;
                }
                dgvInteractions.ClearSelection();

                bool hasData = list.Count > 0;
                dgvInteractions.Visible = hasData;
                emptyState.Visible = !hasData;

                if (hasData) emptyState.Clear();
                else
                    emptyState.Show("\uE8BD", "No interactions",
                        string.IsNullOrEmpty(q)
                            ? "No inquiries, complaints, or reviews logged for this customer."
                            : $"No interactions match \u201c{q}\u201d.");
            }
            else
            {
                var followUps = _currentHistory.FollowUps.AsEnumerable();
                if (!string.IsNullOrEmpty(q))
                {
                    followUps = followUps.Where(f =>
                        (f.Subject?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (f.Notes?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (f.ChannelText?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (f.StatusText?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
                }

                var list = followUps.ToList();
                pager.SetTotal(list.Count);
                var page = pager.Slice(list);
                lblCount.Text = $"Showing {list.Count} of {_currentHistory.FollowUps.Count} follow-ups";

                dgvFollowUps.Rows.Clear();
                foreach (var f in page)
                {
                    int idx = dgvFollowUps.Rows.Add(
                        f.ChannelText, f.Subject, f.Notes, f.StatusText,
                        f.ScheduledAt.ToString("MMM d, HH:mm"), "");
                    dgvFollowUps.Rows[idx].Tag = f;
                }
                dgvFollowUps.ClearSelection();

                bool hasData = list.Count > 0;
                dgvFollowUps.Visible = hasData;
                emptyState.Visible = !hasData;

                if (hasData) emptyState.Clear();
                else
                    emptyState.Show("\uE823", "No follow-ups",
                        string.IsNullOrEmpty(q)
                            ? "No scheduled or completed follow-ups for this customer."
                            : $"No follow-up tasks match \u201c{q}\u201d.");
            }
        }

        // ═══════════ GRID INIT & STYLING ═══════════

        private static DataGridView MakeGrid()
        {
            var g = new DataGridView();
            typeof(DataGridView)
                .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(g, true);

            g.AutoGenerateColumns = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.RowHeadersVisible = false;
            g.ReadOnly = true;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.BackgroundColor = UiKit.T.Surface;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.None;
            g.GridColor = UiKit.T.LineSoft;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ScrollBars = ScrollBars.Both;
            g.RowTemplate.Height = 56;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 46;

            g.ColumnHeadersDefaultCellStyle.BackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.ForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.Font = UiKit.T.SmallStrong;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(CellPadX, 0, CellPadX, 0);

            g.DefaultCellStyle.BackColor = UiKit.T.Surface;
            g.DefaultCellStyle.ForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Font = UiKit.T.Body;
            g.DefaultCellStyle.SelectionBackColor = UiKit.T.RowHover;
            g.DefaultCellStyle.SelectionForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Padding = new Padding(CellPadX, 0, CellPadX, 0);
            g.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

            g.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= g.RowCount || e.ColumnIndex < 0) return;
                var v = Convert.ToString(g.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
                if (!string.IsNullOrWhiteSpace(v)) e.ToolTipText = v;
            };

            return g;
        }

        private void InitGridColumns()
        {
            void AddText(DataGridView g, string name, string header, int width, bool fill = false)
            {
                var c = new DataGridViewTextBoxColumn
                {
                    Name = name,
                    HeaderText = header,
                    Width = width,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                };
                if (fill)
                {
                    c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    c.MinimumWidth = 200;
                }
                g.Columns.Add(c);
            }

            // Repairs
            dgvRepairs.Columns.Clear();
            AddText(dgvRepairs, "Priority", "PRIORITY", 120);
            AddText(dgvRepairs, "Ticket", "TICKET", 140);
            AddText(dgvRepairs, "Device", "DEVICE", 220);
            AddText(dgvRepairs, "Issue", "ISSUE & NOTES", 0, true);
            AddText(dgvRepairs, "Status", "STATUS", 140);
            AddText(dgvRepairs, "Date", "INTAKE", 130);
            AddText(dgvRepairs, "Cost", "COST", 110);
            dgvRepairs.Columns["Cost"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dgvRepairs.Columns["Cost"].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            dgvRepairs.Columns.Add(ActionsCol());

            // Interactions
            dgvInteractions.Columns.Clear();
            AddText(dgvInteractions, "Type", "TYPE", 130);
            AddText(dgvInteractions, "Subject", "SUBJECT", 220);
            AddText(dgvInteractions, "Notes", "NOTES", 0, true);
            AddText(dgvInteractions, "Status", "STATUS", 140);
            AddText(dgvInteractions, "Date", "LOGGED", 130);
            dgvInteractions.Columns.Add(ActionsCol());

            // Follow-Ups
            dgvFollowUps.Columns.Clear();
            AddText(dgvFollowUps, "Channel", "CHANNEL", 120);
            AddText(dgvFollowUps, "Subject", "SUBJECT", 220);
            AddText(dgvFollowUps, "Notes", "NOTES & PLAN", 0, true);
            AddText(dgvFollowUps, "Status", "STATUS", 140);
            AddText(dgvFollowUps, "Scheduled", "SCHEDULED", 150);
            dgvFollowUps.Columns.Add(ActionsCol());
        }

        private static DataGridViewButtonColumn ActionsCol()
        {
            var btn = new DataGridViewButtonColumn
            {
                Name = ColActions,
                HeaderText = "",
                Text = "",
                UseColumnTextForButtonValue = true,
                Width = 56,
                MinimumWidth = 56,
                FlatStyle = FlatStyle.Flat,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            btn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            btn.DefaultCellStyle.BackColor = UiKit.T.Surface;
            btn.DefaultCellStyle.SelectionBackColor = UiKit.T.RowHover;
            btn.DefaultCellStyle.Padding = new Padding(0);
            return btn;
        }

        private void HookGridEvents(DataGridView dgv, int tabType)
        {
            dgv.CellPainting += (s, e) => PaintCell(dgv, e);

            dgv.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= dgv.RowCount) return;
                if (e.ColumnIndex < 0) return;
                if (dgv.Columns[e.ColumnIndex].Name != ColActions) return;

                _menuRowIndex = e.RowIndex;
                var r = dgv.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                var menu = tabType switch { 0 => _repairsMenu, 1 => _interactionsMenu, _ => _followUpsMenu };
                menu.Show(dgv, new Point(r.Right - menu.Width, r.Bottom));
            };

            dgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= dgv.RowCount) return;
                var row = dgv.Rows[e.RowIndex];

                if (tabType == 0 && row.Tag is CustomerHistoryRepairDto rDto) OpenEditRepairDialog(rDto);
                else if (tabType == 1 && row.Tag is CustomerHistoryInteractionDto iDto) OpenEditInteractionDialog(iDto);
                else if (tabType == 2 && row.Tag is CustomerHistoryFollowUpDto fDto) OpenEditFollowUpDialog(fDto);
            };

            dgv.CellMouseMove += (s, e) =>
            {
                if (e.RowIndex != _hoverRow)
                {
                    int old = _hoverRow;
                    _hoverRow = e.RowIndex;
                    if (old >= 0 && old < dgv.RowCount) dgv.InvalidateRow(old);
                    if (_hoverRow >= 0 && _hoverRow < dgv.RowCount) dgv.InvalidateRow(_hoverRow);
                }
            };

            dgv.CellMouseLeave += (s, e) =>
            {
                if (_hoverRow >= 0 && _hoverRow < dgv.RowCount)
                {
                    int old = _hoverRow;
                    _hoverRow = -1;
                    dgv.InvalidateRow(old);
                }
            };

            dgv.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Apps && e.KeyCode != Keys.Enter) return;
                if (dgv.CurrentRow == null) return;

                _menuRowIndex = dgv.CurrentRow.Index;

                if (e.KeyCode == Keys.Enter)
                {
                    var row = dgv.CurrentRow;
                    if (tabType == 0 && row.Tag is CustomerHistoryRepairDto rDto)
                    { OpenEditRepairDialog(rDto); e.Handled = true; return; }
                    if (tabType == 1 && row.Tag is CustomerHistoryInteractionDto iDto)
                    { OpenEditInteractionDialog(iDto); e.Handled = true; return; }
                    if (tabType == 2 && row.Tag is CustomerHistoryFollowUpDto fDto)
                    { OpenEditFollowUpDialog(fDto); e.Handled = true; return; }
                }

                var r = dgv.GetRowDisplayRectangle(_menuRowIndex, true);
                var menu = tabType switch { 0 => _repairsMenu, 1 => _interactionsMenu, _ => _followUpsMenu };
                menu.Show(dgv, new Point(r.Right - menu.Width, r.Bottom));
                e.Handled = true;
            };
        }

        // ═══════════ CELL PAINTING ═══════════

        private void PaintCell(DataGridView dgv, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < 0) return;
            var g = e.Graphics;
            var cell = e.CellBounds;

            if (e.RowIndex == -1)
            {
                using (var b = new SolidBrush(UiKit.T.Surface))
                    g.FillRectangle(b, cell);
                using (var p = new Pen(UiKit.T.Line))
                    g.DrawLine(p, cell.Left, cell.Bottom - 1, cell.Right, cell.Bottom - 1);

                UiKit.Quality(g);
                UiKit.Text(g, Convert.ToString(e.Value) ?? "", UiKit.T.SmallStrong, UiKit.T.InkMuted,
                    new Rectangle(cell.Left + CellPadX, cell.Top, Math.Max(0, cell.Width - CellPadX * 2), cell.Height - 1),
                    CellText);
                e.Handled = true;
                return;
            }

            if (e.RowIndex < 0 || e.RowIndex >= dgv.RowCount) return;

            string col = dgv.Columns[e.ColumnIndex].Name;
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool hovered = e.RowIndex == _hoverRow;
            bool focused = dgv.Focused;

            Color rowBg = UiKit.T.Surface;
            if (selected && focused) rowBg = UiKit.T.RowHover;
            else if (selected && !focused) rowBg = UiKit.T.LineSoft;
            else if (hovered) rowBg = UiKit.T.RowHover;

            using (var b = new SolidBrush(rowBg))
                g.FillRectangle(b, cell);
            using (var p = new Pen(UiKit.T.LineSoft))
                g.DrawLine(p, cell.Left, cell.Bottom - 1, cell.Right, cell.Bottom - 1);

            UiKit.Quality(g);

            if (e.ColumnIndex == 0 && selected && focused)
                UiKit.FillRounded(g, new Rectangle(cell.Left, cell.Top + 12, 3, cell.Height - 25), 1, AppTheme.Primary);

            var rect = new Rectangle(cell.Left + CellPadX, cell.Top,
                Math.Max(0, cell.Width - CellPadX * 2), cell.Height - 1);
            int cy = rect.Top + rect.Height / 2;
            string text = Convert.ToString(e.FormattedValue) ?? "";

            if (col == ColActions)
            {
                int size = 30;
                var btn = new Rectangle(rect.Right - size, cy - size / 2, size, size);
                bool hot = hovered;

                UiKit.FillRounded(g, btn, 8, hot ? UiKit.Wash(AppTheme.Primary) : Color.Transparent);

                int dotR = 2, dotGap = 6, dotY = cy - dotR;
                int startX = btn.Left + (btn.Width / 2) - dotGap;
                using (var b = new SolidBrush(hot ? AppTheme.Primary : UiKit.T.InkMuted))
                {
                    g.FillEllipse(b, startX - dotR, dotY, dotR * 2, dotR * 2);
                    g.FillEllipse(b, startX - dotR + dotGap, dotY, dotR * 2, dotR * 2);
                    g.FillEllipse(b, startX - dotR + dotGap * 2, dotY, dotR * 2, dotR * 2);
                }
                e.Handled = true;
                return;
            }

            if (col == "Priority")
            {
                Color accent = text switch
                {
                    "Urgent" => AppTheme.Danger,
                    "High" => AppTheme.Warning,
                    "Medium" => AppTheme.Primary,
                    _ => UiKit.T.InkMuted
                };
                PaintDotPill(g, rect, cy, string.IsNullOrWhiteSpace(text) ? "Low" : text, accent, UiKit.Micro);
                e.Handled = true;
                return;
            }

            if (col == "Status")
            {
                Color accent = text switch
                {
                    "Completed" or "Closed" => AppTheme.Success,
                    "In Progress" or "Scheduled" => Color.FromArgb(59, 130, 246),
                    "Pending" or "Open" => AppTheme.Warning,
                    "Approved" => AppTheme.Primary,
                    "Rejected" or "Cancelled" => AppTheme.Danger,
                    _ => UiKit.T.InkMuted
                };
                PaintDotPill(g, rect, cy, text, accent, UiKit.Micro);
                e.Handled = true;
                return;
            }

            if (col == "Date" || col == "Scheduled")
            {
                UiKit.Text(g, text, MonoFont, UiKit.T.InkMuted, rect, CellText);
                e.Handled = true;
                return;
            }

            if (col == "Cost")
            {
                var textRect = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
                UiKit.Text(g, string.IsNullOrWhiteSpace(text) ? "—" : text,
                    UiKit.T.BodyStrong, UiKit.T.Ink, textRect,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | Flat);
                e.Handled = true;
                return;
            }

            e.PaintContent(e.CellBounds);
            e.Handled = true;
        }

        private static void PaintDotPill(Graphics gr, Rectangle rect, int cy, string text, Color fg, Font font)
        {
            int textW = MeasureW(text, font);
            int pillW = Math.Min(rect.Width, textW + 34);
            var pill = new Rectangle(rect.Left, cy - 12, pillW, 24);

            UiKit.FillRounded(gr, pill, 12, UiKit.Wash(fg));
            UiKit.FillRounded(gr, new Rectangle(pill.Left + 11, cy - 3, 6, 6), 3, fg);
            UiKit.Text(gr, text, font, fg,
                new Rectangle(pill.Left + 23, pill.Top, Math.Max(0, pill.Width - 31), pill.Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | Flat);
        }

        // ═══════════ CONTEXT MENUS ═══════════

        private void BuildMenus()
        {
            _repairsMenu = MakeMenu();
            _repairsMenu.Items.Add("Edit repair order…").Click += (s, e) =>
            {
                if (_menuRowIndex >= 0 && _menuRowIndex < dgvRepairs.RowCount &&
                    dgvRepairs.Rows[_menuRowIndex].Tag is CustomerHistoryRepairDto dto)
                    OpenEditRepairDialog(dto);
            };
            _repairsMenu.Items.Add("Open in repair workbench").Click += (s, e) => ActionRequested?.Invoke(this, "repairs");

            _interactionsMenu = MakeMenu();
            _interactionsMenu.Items.Add("View / edit interaction…").Click += (s, e) =>
            {
                if (_menuRowIndex >= 0 && _menuRowIndex < dgvInteractions.RowCount &&
                    dgvInteractions.Rows[_menuRowIndex].Tag is CustomerHistoryInteractionDto dto)
                    OpenEditInteractionDialog(dto);
            };
            _interactionsMenu.Items.Add("Open in interactions workbench").Click += (s, e) => ActionRequested?.Invoke(this, "interactions");

            _followUpsMenu = MakeMenu();
            _followUpsMenu.Items.Add("View / edit follow-up…").Click += (s, e) =>
            {
                if (_menuRowIndex >= 0 && _menuRowIndex < dgvFollowUps.RowCount &&
                    dgvFollowUps.Rows[_menuRowIndex].Tag is CustomerHistoryFollowUpDto dto)
                    OpenEditFollowUpDialog(dto);
            };
            _followUpsMenu.Items.Add("Open in follow-ups workbench").Click += (s, e) => ActionRequested?.Invoke(this, "follow-ups");
        }

        private static ContextMenuStrip MakeMenu()
        {
            var m = new ContextMenuStrip { ShowImageMargin = false, Font = UiKit.T.Body };
            m.Renderer = new QuietMenuRenderer();
            return m;
        }

        // ═══════════ MODALS & ACTIONS ═══════════

        private void OpenIntakeRepairDialog()
        {
            int custId = _currentHistory?.CustomerId ?? 0;
            using var dlg = new RepairRequestFormDialog(null, custId, _allCustomers);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK && _currentHistory != null)
                _ = FetchAndDisplayHistory(_currentHistory.CustomerId);
        }

        private void OpenEditRepairDialog(CustomerHistoryRepairDto rDto)
        {
            var repairDto = new RepairRequestDto
            {
                RepairRequestId = rDto.RepairRequestId,
                CustomerId = _currentHistory?.CustomerId ?? 0,
                CustomerName = _currentHistory?.FullName,
                CustomerPhone = _currentHistory?.Phone,
                CustomerEmail = _currentHistory?.Email,
                RequestNumber = rDto.RequestNumber,
                DeviceModel = rDto.DeviceModel,
                IssueDescription = rDto.IssueDescription,
                Status = rDto.Status,
                Priority = rDto.Priority,
                ActualCost = rDto.ActualCost,
                RequestDate = rDto.RequestDate,
                CompletionDate = rDto.CompletionDate
            };
            using var dlg = new RepairRequestFormDialog(repairDto, repairDto.CustomerId, _allCustomers);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK && _currentHistory != null)
                _ = FetchAndDisplayHistory(_currentHistory.CustomerId);
        }

        private void OpenLogInteractionDialog()
        {
            if (_currentHistory == null) return;
            var prefill = new InteractionDto { CustomerId = _currentHistory.CustomerId };
            using var dlg = new InteractionFormDialog(prefill, null, _allCustomers);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK)
                _ = FetchAndDisplayHistory(_currentHistory.CustomerId);
        }

        private void OpenEditInteractionDialog(CustomerHistoryInteractionDto iDto)
        {
            var interDto = new InteractionDto
            {
                CustomerInteractionId = iDto.CustomerInteractionId,
                CustomerId = _currentHistory?.CustomerId,
                CustomerName = _currentHistory?.FullName,
                CustomerPhone = _currentHistory?.Phone,
                CustomerEmail = _currentHistory?.Email,
                InteractionType = iDto.InteractionType,
                Subject = iDto.Subject,
                Notes = iDto.Notes,
                Status = iDto.Status,
                Priority = iDto.Priority,
                InteractionDate = iDto.InteractionDate
            };
            using var dlg = new InteractionFormDialog(interDto, null, _allCustomers);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK && _currentHistory != null)
                _ = FetchAndDisplayHistory(_currentHistory.CustomerId);
        }

        private void OpenScheduleFollowUpDialog()
        {
            if (_currentHistory == null) return;
            var prefill = new FollowUpDto
            {
                CustomerId = _currentHistory.CustomerId,
                ScheduledAt = DateTime.Today.AddDays(1).AddHours(10)
            };
            using var dlg = new FollowUpFormDialog(prefill, _allCustomers);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK)
                _ = FetchAndDisplayHistory(_currentHistory.CustomerId);
        }

        private void OpenEditFollowUpDialog(CustomerHistoryFollowUpDto fDto)
        {
            var fuDto = new FollowUpDto
            {
                FollowUpId = fDto.FollowUpId,
                CustomerId = _currentHistory?.CustomerId,
                CustomerName = _currentHistory?.FullName,
                CustomerPhone = _currentHistory?.Phone,
                CustomerEmail = _currentHistory?.Email,
                Subject = fDto.Subject,
                Notes = fDto.Notes,
                Channel = fDto.Channel,
                Status = fDto.Status,
                ScheduledAt = fDto.ScheduledAt,
                CompletedAt = fDto.CompletedAt
            };
            using var dlg = new FollowUpFormDialog(fuDto, _allCustomers);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK && _currentHistory != null)
                _ = FetchAndDisplayHistory(_currentHistory.CustomerId);
        }

        private void OnCallCustomer(object? sender, EventArgs e)
        {
            if (_currentHistory == null || string.IsNullOrWhiteSpace(_currentHistory.Phone)) return;
            try
            {
                Clipboard.SetText(_currentHistory.Phone);
                Process.Start(new ProcessStartInfo($"tel:{_currentHistory.Phone}") { UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show($"Phone: {_currentHistory.Phone} (Copied to clipboard)",
                    "Customer contact", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnEmailCustomer(object? sender, EventArgs e)
        {
            if (_currentHistory == null || string.IsNullOrWhiteSpace(_currentHistory.Email)) return;
            try
            {
                Clipboard.SetText(_currentHistory.Email);
                Process.Start(new ProcessStartInfo($"mailto:{_currentHistory.Email}") { UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show($"Email: {_currentHistory.Email} (Copied to clipboard)",
                    "Customer contact", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  EMBEDDED KPI TILE (matches Companies / Subscriptions / Admin)
        // ═══════════════════════════════════════════════════════════════

        private sealed class KpiTile : Control
        {
            private const int Pad = 22;
            private const int IconSize = 36;
            private const int ChevronW = 16;
            private const int MinHeight = 132;

            private const TextFormatFlags One = TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            private const TextFormatFlags Wrapped = One | TextFormatFlags.WordBreak;

            private static readonly Font[] NumFonts =
            {
                AppFonts.Strong(26F),
                AppFonts.Strong(22F),
                AppFonts.Strong(18F),
                AppFonts.Strong(15F),
                AppFonts.Strong(12F)
            };

            private string _label = "";
            private string _number = "0";
            private string _sub = "";
            private Color _accent = AppTheme.Primary;
            private string _glyph = "";
            private bool _hover;
            private bool _down;

            public KpiTile()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
                TabStop = true;
                Cursor = Cursors.Hand;
            }

            public void Set(string label, string number, string sub, Color accent, string glyph)
            {
                _label = label;
                _number = number;
                _sub = sub;
                _accent = accent;
                _glyph = glyph;
                AccessibleName = $"{label}: {number}. {sub}";
                Invalidate();
            }

            public int HeightFor(int width) =>
                string.IsNullOrEmpty(_label) ? MinHeight : Math.Max(MinHeight, Measure(width).Total);

            private static int TextH(string text, Font font, int width, bool wrap)
            {
                if (string.IsNullOrEmpty(text)) return font.Height;
                var size = TextRenderer.MeasureText(text, font,
                    new Size(Math.Max(10, width - 4), int.MaxValue), wrap ? Wrapped : One);
                return Math.Max(font.Height, size.Height) + 2;
            }

            private (Font NumFont, bool NumWrap, string Main, int TopH, int NumH, int CapH, int Total) Measure(int width)
            {
                int inner = Math.Max(40, width - Pad * 2);
                int labelW = Math.Max(40, inner - IconSize - 12 - ChevronW);

                int labelH = TextH(_label, UiKit.T.SmallStrong, labelW, true);
                int topH = Math.Max(IconSize, labelH);

                Font numFont = NumFonts[^1];
                bool numWrap = true;
                foreach (var f in NumFonts)
                {
                    var w = TextRenderer.MeasureText(_number, f, new Size(int.MaxValue, int.MaxValue), One).Width;
                    if (w <= inner - 4) { numFont = f; numWrap = false; break; }
                }

                int numH = TextH(_number, numFont, inner, numWrap);
                int capH = TextH(_sub, UiKit.T.Small, inner, true);

                int total = Pad + topH + 14 + numH + 6 + capH + Pad;
                return (numFont, numWrap, _number, topH, numH, capH, total);
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _down = true; Focus(); Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var bg = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(bg, ClientRectangle);

                bool loading = string.IsNullOrEmpty(_label);
                Color accent = loading ? UiKit.T.InkFaint : _accent;

                UiKit.Card(g, ClientRectangle, UiKit.T.Radius,
                    _down && !loading ? UiKit.Wash(accent) : UiKit.T.Surface,
                    _hover && !loading ? accent : UiKit.T.Line);

                if (loading)
                {
                    UiKit.FillRounded(g, new Rectangle(Pad, Pad, IconSize, IconSize), 10, UiKit.T.LineSoft);
                    UiKit.FillRounded(g, new Rectangle(Pad + IconSize + 12, Pad + 12, Math.Max(20, Width / 3), 12), 4, UiKit.T.LineSoft);
                    UiKit.FillRounded(g, new Rectangle(Pad, Pad + 56, Math.Max(20, Width / 2), 26), 4, UiKit.T.LineSoft);
                    return;
                }

                var m = Measure(Width);
                int inner = Width - Pad * 2;

                var iconRect = new Rectangle(Pad, Pad + (m.TopH - IconSize) / 2, IconSize, IconSize);
                UiKit.FillRounded(g, iconRect, 10, UiKit.Wash(accent));
                using (var f = UiKit.GlyphFont(12F))
                    UiKit.Text(g, _glyph, f, accent, iconRect, UiKit.Center);

                UiKit.Text(g, _label, UiKit.T.SmallStrong, UiKit.T.InkMuted,
                    new Rectangle(Pad + IconSize + 12, Pad, inner - IconSize - 12 - ChevronW, m.TopH),
                    Wrapped | TextFormatFlags.VerticalCenter);

                using (var cf = UiKit.GlyphFont(9F))
                    UiKit.Text(g, "\uE76C", cf, _hover ? accent : UiKit.T.InkFaint,
                        new Rectangle(Width - Pad - ChevronW + 2, Pad + (m.TopH - 20) / 2, ChevronW, 20), UiKit.Center);

                int numTop = Pad + m.TopH + 14;
                UiKit.Text(g, m.Main, m.NumFont, UiKit.T.Ink,
                    new Rectangle(Pad, numTop, inner, m.NumH),
                    (m.NumWrap ? Wrapped : One) | TextFormatFlags.Top);

                UiKit.Text(g, _sub, UiKit.T.Small, UiKit.T.InkMuted,
                    new Rectangle(Pad, numTop + m.NumH + 6, inner, m.CapH),
                    Wrapped | TextFormatFlags.Top);

                if (Focused)
                {
                    var ring = ClientRectangle;
                    ring.Inflate(-1, -1);
                    UiKit.StrokeRounded(g, ring, UiKit.T.Radius, accent, 2f);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  SEGMENTED FILTER (int? tag; parallel List<int?> for safe null)
        // ═══════════════════════════════════════════════════════════════

        [DesignerCategory("Code")]
        private sealed class HistorySegmentedFilter : Control
        {
            private readonly List<(string Key, int? Tag)> _items = new();
            private readonly List<int?> _counts = new();
            private int _selected = 0;
            private int _hover = -1;

            public event EventHandler<int?>? SelectionChanged;

            public HistorySegmentedFilter((string Key, int? Tag)[] items)
            {
                foreach (var it in items)
                {
                    _items.Add(it);
                    _counts.Add(null);
                }

                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = UiKit.T.Surface;
                Cursor = Cursors.Hand;
                Font = UiKit.T.SmallStrong;
                Height = 36;
            }

            public int? SelectedTag => _items.Count > 0 && _selected < _items.Count ? _items[_selected].Tag : null;

            public int PreferredWidth
            {
                get
                {
                    int total = 16;
                    foreach (var item in _items)
                    {
                        string text = Display(item);
                        var sz = TextRenderer.MeasureText(text, Font);
                        total += Math.Max(104, sz.Width + 28);
                    }
                    return Math.Max(330, total);
                }
            }

            public void UpdateCounts(params (int? Tag, int? Count)[] pairs)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    _counts[i] = null;
                    foreach (var p in pairs)
                    {
                        if (Nullable.Equals(_items[i].Tag, p.Tag))
                        {
                            _counts[i] = p.Count;
                            break;
                        }
                    }
                }
                Invalidate();
            }

            public void SetKey(int? tag)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (Nullable.Equals(_items[i].Tag, tag))
                    {
                        if (_selected != i)
                        {
                            _selected = i;
                            Invalidate();
                        }
                        return;
                    }
                }
            }

            private string Display((string Key, int? Tag) item)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (Nullable.Equals(_items[i].Tag, item.Tag))
                    {
                        if (_counts[i].HasValue)
                            return $"{item.Key} ({_counts[i]!.Value})";
                        return item.Key;
                    }
                }
                return item.Key;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);
                UiKit.FillRounded(g, new Rectangle(0, 0, Width, Height), 9, UiKit.T.LineSoft);

                if (_items.Count == 0) return;
                int segW = (Width - 4) / _items.Count;

                for (int i = 0; i < _items.Count; i++)
                {
                    var seg = new Rectangle(2 + i * segW, 2, segW, Height - 4);
                    bool active = i == _selected;
                    if (active)
                    {
                        UiKit.FillRounded(g, seg, 7, UiKit.T.Surface);
                        using var pen = new Pen(UiKit.T.Line, 1);
                        using var path = UiKit.Rounded(new Rectangle(seg.X, seg.Y, seg.Width - 1, seg.Height - 1), 7);
                        g.DrawPath(pen, path);
                    }

                    string text = Display(_items[i]);
                    Color fg = active ? UiKit.T.Ink : (i == _hover ? UiKit.T.Ink : UiKit.T.InkMuted);
                    UiKit.Text(g, text, UiKit.T.SmallStrong, fg, seg,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (_items.Count == 0) return;
                int segW = (Width - 4) / _items.Count;
                int idx = Math.Clamp((e.X - 2) / Math.Max(1, segW), 0, _items.Count - 1);
                if (idx != _hover) { _hover = idx; Invalidate(); }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _hover = -1; Invalidate(); base.OnMouseLeave(e);
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                if (_items.Count == 0) return;
                int segW = (Width - 4) / _items.Count;
                int idx = Math.Clamp((e.X - 2) / Math.Max(1, segW), 0, _items.Count - 1);
                if (idx >= 0 && idx < _items.Count && idx != _selected)
                {
                    _selected = idx;
                    Invalidate();
                    SelectionChanged?.Invoke(this, _items[_selected].Tag);
                }
                base.OnMouseClick(e);
            }
        }

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