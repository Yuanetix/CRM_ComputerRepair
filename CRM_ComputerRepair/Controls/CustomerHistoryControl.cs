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
using System.Drawing.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Professional Customer History 360° Dossier for Staff.
    /// Provides complete timeline and record of all computer repairs,
    /// customer inquiries & concerns, and scheduled follow-up outreaches for any customer.
    /// Designed to match the Customer List UI for strict SaaS consistency.
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
        private Label lblSubtitle = null!;
        private ComboBox cmbCustomer = null!;
        private SaasButton btnReload = null!;
        private SaasButton btnAdd = null!;

        // ═══════════ METRIC STRIP ═══════════

        private MetricStrip strip = null!;

        // ═══════════ WORKBENCH CARD ═══════════

        private SurfaceCard card = null!;

        // Customer Profile Banner (Top of Card)
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

        // Toolbar
        private Label lblGridTitle = null!;
        private Label lblCount = null!;
        private SegmentedFilter segments = null!;
        private SearchBox search = null!;

        // Grids
        private DataGridView dgvRepairs = null!;
        private DataGridView dgvInteractions = null!;
        private DataGridView dgvFollowUps = null!;

        private StateView state = null!;
        private TablePagination pager = null!;

        private const string ColActions = "colActions";
        private ContextMenuStrip _repairsMenu = null!;
        private ContextMenuStrip _interactionsMenu = null!;
        private ContextMenuStrip _followUpsMenu = null!;
        private int _menuRowIndex = -1;
        private int _hoverRow = -1;
        private readonly ToolTip _tips = new ToolTip { InitialDelay = 500, ReshowDelay = 200 };

        public event EventHandler<string>? ActionRequested;

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
            state.ShowLoading("Loading customers…", "Fetching the customer list for the dossier picker.");
            try
            {
                _allCustomers = await _api.GetCustomersAsync() ?? new List<CustomerDto>();
                PopulateCustomerDropdown();

                if (_currentHistory == null && _allCustomers.Count > 0)
                {
                    cmbCustomer.SelectedIndex = 1; // Select first real customer
                }
            }
            catch (Exception ex)
            {
                state.Show("\uE783", "Failed to Load Customers", ex.Message);
            }
        }

        private void PopulateCustomerDropdown()
        {
            cmbCustomer.Items.Clear();
            cmbCustomer.Items.Add("— Select a Customer Dossier —");

            foreach (var c in _allCustomers.OrderBy(x => x.FullName))
            {
                string contact = !string.IsNullOrWhiteSpace(c.Phone) ? c.Phone : (!string.IsNullOrWhiteSpace(c.Email) ? c.Email : "No contact");
                cmbCustomer.Items.Add($"{c.FullName} ({contact})");
            }

            if (cmbCustomer.Items.Count > 0)
                cmbCustomer.SelectedIndex = 0;
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
            {
                cmbCustomer.SelectedIndex = idx + 1;
            }
            else
            {
                await FetchAndDisplayHistory(customerId);
            }
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
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Comprehensive 360° timeline of repairs, interactions, and follow-ups",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            cmbCustomer = new ComboBox
            {
                Font = UiKit.T.Body,
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DropDownWidth = 540,
                Width = 360,
                Height = UiKit.T.ButtonHeight,
                BackColor = UiKit.T.Surface,
                ForeColor = UiKit.T.Ink,
                FlatStyle = FlatStyle.Flat
            };
            cmbCustomer.Items.Add("Loading registered customers...");
            cmbCustomer.SelectedIndex = 0;
            cmbCustomer.SelectedIndexChanged += async (s, e) =>
            {
                if (cmbCustomer.SelectedIndex > 0 && cmbCustomer.SelectedIndex - 1 < _allCustomers.Count)
                {
                    var cust = _allCustomers.OrderBy(c => c.FullName).ToList()[cmbCustomer.SelectedIndex - 1];
                    await FetchAndDisplayHistory(cust.CustomerId);
                }
                else
                {
                    ClearDossierDisplay();
                }
            };
            cmbCustomer.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    int foundIdx = cmbCustomer.FindStringExact(cmbCustomer.Text.Trim());
                    if (foundIdx < 0) foundIdx = cmbCustomer.FindString(cmbCustomer.Text.Trim());
                    if (foundIdx > 0)
                    {
                        cmbCustomer.SelectedIndex = foundIdx;
                    }
                }
            };
            cmbCustomer.Leave += async (s, e) =>
            {
                if (cmbCustomer.SelectedIndex <= 0 && !string.IsNullOrWhiteSpace(cmbCustomer.Text))
                {
                    int foundIdx = cmbCustomer.FindStringExact(cmbCustomer.Text.Trim());
                    if (foundIdx < 0) foundIdx = cmbCustomer.FindString(cmbCustomer.Text.Trim());
                    if (foundIdx > 0)
                    {
                        cmbCustomer.SelectedIndex = foundIdx;
                    }
                }
            };

            btnReload = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnReload.Click += async (s, e) =>
            {
                if (cmbCustomer.SelectedIndex > 0 && cmbCustomer.SelectedIndex - 1 < _allCustomers.Count)
                {
                    var cust = _allCustomers.OrderBy(c => c.FullName).ToList()[cmbCustomer.SelectedIndex - 1];
                    await FetchAndDisplayHistory(cust.CustomerId);
                }
                else
                {
                    await InitializeAsync();
                }
            };
            _tips.SetToolTip(btnReload, "Refresh history (F5)");

            btnAdd = new SaasButton("Intake repair", SaasButtonVariant.Primary, "\uE710");
            btnAdd.Click += (s, e) => OpenIntakeRepairDialog();
            _tips.SetToolTip(btnAdd, "Intake repair for this customer (Ctrl+N)");

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(cmbCustomer);
            Controls.Add(btnReload);
            Controls.Add(btnAdd);

            // ── Metric strip (4 Interactive KPI Tiles) ──
            strip = new MetricStrip();
            strip.AddItem("Total Repairs", 0, AppTheme.Primary);
            strip.AddItem("Active Bench", 1, AppTheme.Primary);
            strip.AddItem("Interactions", 2, AppTheme.Warning);
            strip.AddItem("Follow-Ups", 3, AppTheme.Success);
            strip.SelectionChanged += (s, e) =>
            {
                int? sel = strip.SelectedStatus;
                if (sel == 0 || sel == 1)
                    segments.FireSelectIndex(0);
                else if (sel == 2)
                    segments.FireSelectIndex(1);
                else if (sel == 3)
                    segments.FireSelectIndex(2);
            };
            Controls.Add(strip);

            // ── Workbench Card ──
            card = new SurfaceCard();

            // Customer Profile Dossier Banner
            pnlDossier = new Panel
            {
                BackColor = Color.Transparent,
                Height = 74
            };

            lblAvatar = new Label
            {
                Text = "C",
                Font = AppFonts.Strong(14F),
                ForeColor = Color.White,
                BackColor = AppTheme.Primary,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(42, 42),
                Location = new Point(0, 8)
            };
            lblAvatar.Paint += (s, e) => UiHelpers.ApplyRoundedRegion(lblAvatar, 21);
            pnlDossier.Controls.Add(lblAvatar);

            lblFullName = new Label
            {
                Text = "Select a customer from the dropdown above",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(lblAvatar.Right + 12, 6)
            };
            pnlDossier.Controls.Add(lblFullName);

            lblCustomerIdBadge = new Label
            {
                Text = "#0",
                Font = UiKit.T.SmallStrong,
                ForeColor = UiKit.T.InkMuted,
                BackColor = UiKit.T.LineSoft,
                Padding = new Padding(6, 2, 6, 2),
                AutoSize = true,
                Visible = false
            };
            pnlDossier.Controls.Add(lblCustomerIdBadge);

            lblLoyaltyBadge = new Label
            {
                Text = "★ 0 pts",
                Font = UiKit.T.SmallStrong,
                ForeColor = Color.FromArgb(0xB4, 0x53, 0x09),
                BackColor = Color.FromArgb(0xFE, 0xF3, 0xC7),
                Padding = new Padding(6, 2, 6, 2),
                AutoSize = true,
                Visible = false
            };
            pnlDossier.Controls.Add(lblLoyaltyBadge);

            lblContactInfo = new Label
            {
                Text = "No customer dossier selected",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(lblAvatar.Right + 12, lblFullName.Bottom + 4)
            };
            pnlDossier.Controls.Add(lblContactInfo);

            btnCallCustomer = new SaasButton("Call", SaasButtonVariant.Secondary, "\uE717");
            btnCallCustomer.Click += OnCallCustomer;
            btnCallCustomer.Visible = false;
            pnlDossier.Controls.Add(btnCallCustomer);

            btnEmailCustomer = new SaasButton("Email", SaasButtonVariant.Secondary, "\uE715");
            btnEmailCustomer.Click += OnEmailCustomer;
            btnEmailCustomer.Visible = false;
            pnlDossier.Controls.Add(btnEmailCustomer);

            btnIntakeRepair = new SaasButton("+ Intake repair", SaasButtonVariant.Primary, "\uE710");
            btnIntakeRepair.Click += (s, e) => OpenIntakeRepairDialog();
            btnIntakeRepair.Visible = false;
            pnlDossier.Controls.Add(btnIntakeRepair);

            btnLogInteraction = new SaasButton("+ Interaction", SaasButtonVariant.Secondary, "\uE8BD");
            btnLogInteraction.Click += (s, e) => OpenLogInteractionDialog();
            btnLogInteraction.Visible = false;
            pnlDossier.Controls.Add(btnLogInteraction);

            btnScheduleFollowUp = new SaasButton("+ Follow-up", SaasButtonVariant.Secondary, "\uE823");
            btnScheduleFollowUp.Click += (s, e) => OpenScheduleFollowUpDialog();
            btnScheduleFollowUp.Visible = false;
            pnlDossier.Controls.Add(btnScheduleFollowUp);

            card.Controls.Add(pnlDossier);

            // Toolbar
            lblGridTitle = new Label
            {
                Text = "Repair Requests",
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

            segments = new SegmentedFilter(new (string, int?)[]
            {
                ("Repair Requests", 0),
                ("Customer Interactions", 1),
                ("Scheduled Follow-Ups", 2)
            });
            segments.SelectionChanged += (s, e) => SetActiveSegment(segments.Selected ?? 0);

            search = new SearchBox { PlaceholderText = "Search history... (Ctrl+F)" };
            search.Inner.TextChanged += (s, e) => ApplySearch();
            _tips.SetToolTip(search, "Search records (Ctrl+F, Esc to clear)");

            // Grids
            dgvRepairs = MakeGrid();
            dgvInteractions = MakeGrid();
            dgvFollowUps = MakeGrid();

            InitGridColumns();
            HookGridEvents(dgvRepairs, 0);
            HookGridEvents(dgvInteractions, 1);
            HookGridEvents(dgvFollowUps, 2);

            state = new StateView { Visible = true };
            state.Show("\uE77B", "Customer Service Dossier", "Select a customer from the dropdown above to load their complete repair tickets, inquiries, and follow-up history.");

            pager = new TablePagination();
            pager.PageChanged += (s, e) => ApplySearch(resetPage: false);

            card.Controls.Add(lblGridTitle);
            card.Controls.Add(lblCount);
            card.Controls.Add(segments);
            card.Controls.Add(search);
            card.Controls.Add(dgvRepairs);
            card.Controls.Add(dgvInteractions);
            card.Controls.Add(dgvFollowUps);
            card.Controls.Add(state);
            card.Controls.Add(pager);

            Controls.Add(card);

            Resize += (s, e) => LayoutUi();
        }

        // ═══════════ HAIRLINE UNDER HEADER ═══════════

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            UiKit.Quality(e.Graphics);

            int y = lblSubtitle.Bottom + UiKit.T.S4;
            using var pen = new Pen(UiKit.T.Line, 1);
            e.Graphics.DrawLine(pen, 0, y, Width, y);
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
                    search.Inner.Focus();
                    search.Inner.SelectAll();
                    return true;

                case Keys.Control | Keys.N:
                    OpenIntakeRepairDialog();
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

        // ═══════════ LAYOUT ═══════════

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0) return;

            // ── Header ──
            lblTitle.Location = new Point(0, 0);
            int subtitleY = lblTitle.PreferredHeight + 6;
            lblSubtitle.Location = new Point(1, subtitleY);

            // Right header controls: [cmbCustomer] [btnReload] [btnAdd]
            btnAdd.Size = new Size(btnAdd.PreferredWidth, UiKit.T.ButtonHeight);
            btnAdd.Location = new Point(Width - btnAdd.Width, 2);

            btnReload.Size = new Size(btnReload.PreferredWidth, UiKit.T.ButtonHeight);
            btnReload.Location = new Point(btnAdd.Left - btnReload.Width - UiKit.T.S2, 2);

            int cmbW = Math.Min(380, Math.Max(260, btnReload.Left - lblTitle.Right - UiKit.T.S4));
            cmbCustomer.Size = new Size(cmbW, UiKit.T.ButtonHeight);
            cmbCustomer.Location = new Point(btnReload.Left - cmbW - UiKit.T.S2, 2 + (UiKit.T.ButtonHeight - cmbCustomer.Height) / 2);

            int dividerY = subtitleY + lblSubtitle.PreferredHeight + UiKit.T.S4;

            // ── Metric strip ──
            int stripTop = dividerY + UiKit.T.S5;
            int stripH = Math.Max(UiKit.T.StripHeight, strip.PreferredContentHeight());
            strip.Location = new Point(0, stripTop);
            strip.Size = new Size(Width, stripH);

            // ── Workbench card ──
            int cardTop = stripTop + stripH + UiKit.T.S5;
            int cardHeight = Math.Max(240, Height - cardTop);

            card.Location = new Point(0, cardTop);
            card.Size = new Size(Width, cardHeight);

            const int cp = UiKit.T.S5;

            // Dossier Profile Banner
            int dossierH = 74;
            pnlDossier.Location = new Point(cp, cp);
            pnlDossier.Size = new Size(card.Width - cp * 2, dossierH);

            lblAvatar.Location = new Point(0, (dossierH - lblAvatar.Height) / 2);
            lblFullName.Location = new Point(lblAvatar.Right + 12, 10);
            lblCustomerIdBadge.Location = new Point(lblFullName.Right + 8, 12);
            lblLoyaltyBadge.Location = new Point(lblCustomerIdBadge.Right + 6, 12);

            lblContactInfo.Location = new Point(lblAvatar.Right + 12, lblFullName.Bottom + 6);

            // Dossier action buttons (right-to-left)
            int btnX = pnlDossier.Width;
            foreach (var b in new[] { btnScheduleFollowUp, btnLogInteraction, btnIntakeRepair, btnEmailCustomer, btnCallCustomer })
            {
                if (!b.Visible) continue;
                b.Size = new Size(b.PreferredWidth, UiKit.T.ButtonHeight - 4);
                btnX -= b.Width + UiKit.T.S2;
                b.Location = new Point(btnX, (dossierH - b.Height) / 2);
            }

            // Hairline separator inside card
            int toolbarY = pnlDossier.Bottom + 16;

            lblGridTitle.Location = new Point(cp, toolbarY);
            lblCount.Location = new Point(lblGridTitle.Right + UiKit.T.S2,
                                          lblGridTitle.Top + lblGridTitle.PreferredHeight - lblCount.PreferredHeight - 2);

            int filterY = lblGridTitle.Bottom + TableKit.ToolbarGap;

            segments.Size = new Size(segments.PreferredWidth, TableKit.ToolbarH);
            segments.Location = new Point(cp, filterY);

            int searchW = TableKit.SearchWidth(card.Width, segments.Width);
            search.Size = new Size(searchW, TableKit.InputH);
            search.Location = new Point(card.Width - cp - searchW, filterY + (TableKit.ToolbarH - TableKit.InputH) / 2);

            int gridTop = filterY + TableKit.ToolbarH + TableKit.ToolbarGap;
            int gridW = card.Width - cp * 2;
            int gridH = card.Height - gridTop - cp - TableKit.FooterH;

            pager.Location = new Point(cp, gridTop + Math.Max(0, gridH));
            pager.Size = new Size(gridW, TableKit.FooterH);

            if (gridW > 100 && gridH > 60)
            {
                dgvRepairs.Location = new Point(cp, gridTop);
                dgvRepairs.Size = new Size(gridW, gridH);

                dgvInteractions.Location = new Point(cp, gridTop);
                dgvInteractions.Size = new Size(gridW, gridH);

                dgvFollowUps.Location = new Point(cp, gridTop);
                dgvFollowUps.Size = new Size(gridW, gridH);

                state.Location = new Point(cp, gridTop);
                state.Size = new Size(gridW, gridH + TableKit.FooterH);
            }
        }

        // ═══════════ SEGMENTS & TABS ═══════════

        private void SetActiveSegment(int index)
        {
            dgvRepairs.Visible = (index == 0);
            dgvInteractions.Visible = (index == 1);
            dgvFollowUps.Visible = (index == 2);

            lblGridTitle.Text = index switch
            {
                0 => "Repair Requests",
                1 => "Customer Interactions",
                2 => "Scheduled Follow-Ups",
                _ => "History Records"
            };

            // Sync metric strip highlight
            strip.SelectIndex(index switch
            {
                0 => 0,
                1 => 2,
                2 => 3,
                _ => 0
            });

            ApplySearch();
            LayoutUi();
        }

        // ═══════════ DATA FETCH & DISPLAY ═══════════

        private void ClearDossierDisplay()
        {
            _currentHistory = null;
            lblAvatar.Text = "C";
            lblFullName.Text = "Select a customer from the dropdown above";
            lblCustomerIdBadge.Visible = false;
            lblLoyaltyBadge.Visible = false;
            lblContactInfo.Text = "No customer dossier selected";

            btnCallCustomer.Visible = false;
            btnEmailCustomer.Visible = false;
            btnIntakeRepair.Visible = false;
            btnLogInteraction.Visible = false;
            btnScheduleFollowUp.Visible = false;

            strip.SetValue(0, 0);
            strip.SetValue(1, 0);
            strip.SetValue(2, 0);
            strip.SetValue(3, 0);

            dgvRepairs.Rows.Clear();
            dgvInteractions.Rows.Clear();
            dgvFollowUps.Rows.Clear();

            state.Show("\uE77B", "Customer Service Dossier", "Select a customer from the dropdown above to load their complete repair tickets, inquiries, and follow-up history.");
            state.Visible = true;
            dgvRepairs.Visible = false;
            dgvInteractions.Visible = false;
            dgvFollowUps.Visible = false;
        }

        private async Task FetchAndDisplayHistory(int customerId)
        {
            state.ShowLoading("Loading history…", "Fetching this customer's dossier.");
            dgvRepairs.Visible = false;
            dgvInteractions.Visible = false;
            dgvFollowUps.Visible = false;
            try
            {
                var data = await _api.GetCustomerHistoryAsync(customerId);
                if (data == null)
                {
                    state.Show("\uE77B", "Customer Not Found", $"No history records found for customer #{customerId}.");
                    state.Visible = true;
                    dgvRepairs.Visible = false;
                    dgvInteractions.Visible = false;
                    dgvFollowUps.Visible = false;
                    return;
                }

                _currentHistory = data;

                // Initials
                string initials = "C";
                if (!string.IsNullOrWhiteSpace(data.FirstName))
                    initials = data.FirstName[0].ToString().ToUpper();
                if (!string.IsNullOrWhiteSpace(data.LastName))
                    initials += data.LastName[0].ToString().ToUpper();
                lblAvatar.Text = initials;

                lblFullName.Text = data.FullName;
                lblCustomerIdBadge.Text = $"#{data.CustomerId}";
                lblCustomerIdBadge.Visible = true;

                lblLoyaltyBadge.Text = $"★ {data.LoyaltyPoints ?? 0} pts";
                lblLoyaltyBadge.Visible = true;

                string phone = !string.IsNullOrWhiteSpace(data.Phone) ? data.Phone : "No phone";
                string email = !string.IsNullOrWhiteSpace(data.Email) ? data.Email : "No email";
                string address = !string.IsNullOrWhiteSpace(data.FullAddress)
                    ? $" · {data.FullAddress}"
                    : (!string.IsNullOrWhiteSpace(data.Address) ? $" · {data.Address}" : "");
                string since = $" · Member since {data.CreatedAt:MMM yyyy}";
                lblContactInfo.Text = $"{phone}  ·  {email}{address}{since}";

                btnCallCustomer.Visible = !string.IsNullOrWhiteSpace(data.Phone);
                btnEmailCustomer.Visible = !string.IsNullOrWhiteSpace(data.Email);
                btnIntakeRepair.Visible = true;
                btnLogInteraction.Visible = true;
                btnScheduleFollowUp.Visible = true;

                // Metrics
                int activeJobs = data.Repairs.Count(r => r.Status != 3 && r.Status != 4);
                strip.SetValue(0, data.Repairs.Count);
                strip.SetValue(1, activeJobs);
                strip.SetValue(2, data.Interactions.Count);
                strip.SetValue(3, data.FollowUps.Count);

                ApplySearch();
                LayoutUi();
            }
            catch (Exception ex)
            {
                state.Show("\uE783", "Failed to Load History", ex.Message);
                state.Visible = true;
                dgvRepairs.Visible = false;
                dgvInteractions.Visible = false;
                dgvFollowUps.Visible = false;
            }
        }

        // ═══════════ SEARCH & FILTERING ═══════════

        private void ApplySearch(bool resetPage = true)
        {
            if (resetPage) pager.Reset();
            if (_currentHistory == null)
            {
                state.Visible = true;
                return;
            }

            string q = search.Inner.Text.Trim();
            int selectedTab = segments.Selected ?? 0;

            if (selectedTab == 0) // Repairs
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
                lblCount.Text = TableKit.FormatCount(list.Count, _currentHistory.Repairs.Count);

                dgvRepairs.Rows.Clear();
                foreach (var r in page)
                {
                    int idx = dgvRepairs.Rows.Add(
                        r.PriorityText,
                        r.RequestNumber,
                        r.DeviceModel,
                        r.IssueDescription,
                        r.StatusText,
                        r.RequestDate.ToString("MMM d, yyyy"),
                        r.CostDisplay,
                        ""
                    );
                    dgvRepairs.Rows[idx].Tag = r;
                }
                dgvRepairs.ClearSelection();

                bool hasData = list.Count > 0;
                dgvRepairs.Visible = hasData;
                state.Visible = !hasData;
                if (hasData) state.Clear();
                else
                    state.Show("\uE9D9", "No Repair Requests", string.IsNullOrEmpty(q) ? "This customer has no recorded repair orders yet." : $"No repair requests match \"{q}\".");
            }
            else if (selectedTab == 1) // Interactions
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
                lblCount.Text = TableKit.FormatCount(list.Count, _currentHistory.Interactions.Count);

                dgvInteractions.Rows.Clear();
                foreach (var i in page)
                {
                    int idx = dgvInteractions.Rows.Add(
                        i.TypeText,
                        i.Subject,
                        i.Notes,
                        i.StatusText,
                        i.InteractionDate.ToString("MMM d, yyyy"),
                        ""
                    );
                    dgvInteractions.Rows[idx].Tag = i;
                }
                dgvInteractions.ClearSelection();

                bool hasData = list.Count > 0;
                dgvInteractions.Visible = hasData;
                state.Visible = !hasData;
                if (hasData) state.Clear();
                else
                    state.Show("\uE8BD", "No Interactions", string.IsNullOrEmpty(q) ? "No inquiries, complaints, or reviews logged for this customer." : $"No interactions match \"{q}\".");
            }
            else if (selectedTab == 2) // Follow-Ups
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
                lblCount.Text = TableKit.FormatCount(list.Count, _currentHistory.FollowUps.Count);

                dgvFollowUps.Rows.Clear();
                foreach (var f in page)
                {
                    int idx = dgvFollowUps.Rows.Add(
                        f.ChannelText,
                        f.Subject,
                        f.Notes,
                        f.StatusText,
                        f.ScheduledAt.ToString("MMM d, HH:mm"),
                        ""
                    );
                    dgvFollowUps.Rows[idx].Tag = f;
                }
                dgvFollowUps.ClearSelection();

                bool hasData = list.Count > 0;
                dgvFollowUps.Visible = hasData;
                state.Visible = !hasData;
                if (hasData) state.Clear();
                else
                    state.Show("\uE823", "No Follow-Ups", string.IsNullOrEmpty(q) ? "No scheduled or completed follow-ups for this customer." : $"No follow-up tasks match \"{q}\".");
            }
        }

        // ═══════════ GRID INITIALIZATION & STYLING ═══════════

        private static DataGridView MakeGrid()
        {
            var g = new DataGridView();
            TableKit.StyleGrid(g);
            return g;
        }

        private void InitGridColumns()
        {
            // ── Repairs Grid Columns ──
            dgvRepairs.AutoGenerateColumns = false;
            dgvRepairs.Columns.Clear();
            var colRepPriority = new DataGridViewTextBoxColumn { Name = "Priority", HeaderText = "Priority", Width = 105 };
            var colRepTicket = new DataGridViewTextBoxColumn { Name = "Ticket", HeaderText = "Ticket #", Width = 120 };
            var colRepDevice = new DataGridViewTextBoxColumn { Name = "Device", HeaderText = "Device Model", Width = 220 };
            var colRepIssue = new DataGridViewTextBoxColumn { Name = "Issue", HeaderText = "Issue Description", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 200 };
            var colRepStatus = new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", Width = 120 };
            var colRepDate = new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Intake Date", Width = 140 };
            var colRepCost = new DataGridViewTextBoxColumn { Name = "Cost", HeaderText = "Cost", Width = 110 };
            colRepCost.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            colRepCost.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            var colRepActions = new DataGridViewTextBoxColumn { Name = ColActions, HeaderText = "", Width = TableKit.ActionWidth };

            dgvRepairs.Columns.AddRange(colRepPriority, colRepTicket, colRepDevice, colRepIssue, colRepStatus, colRepDate, colRepCost, colRepActions);

            // ── Interactions Grid Columns ──
            dgvInteractions.AutoGenerateColumns = false;
            dgvInteractions.Columns.Clear();
            var colIntType = new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Type", Width = 120 };
            var colIntSubject = new DataGridViewTextBoxColumn { Name = "Subject", HeaderText = "Subject", Width = 220 };
            var colIntNotes = new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "Conversation Notes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 200 };
            var colIntStatus = new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", Width = 120 };
            var colIntDate = new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date Logged", Width = 140 };
            var colIntActions = new DataGridViewTextBoxColumn { Name = ColActions, HeaderText = "", Width = TableKit.ActionWidth };

            dgvInteractions.Columns.AddRange(colIntType, colIntSubject, colIntNotes, colIntStatus, colIntDate, colIntActions);

            // ── Follow-Ups Grid Columns ──
            dgvFollowUps.AutoGenerateColumns = false;
            dgvFollowUps.Columns.Clear();
            var colFUpChannel = new DataGridViewTextBoxColumn { Name = "Channel", HeaderText = "Channel", Width = 110 };
            var colFUpSubject = new DataGridViewTextBoxColumn { Name = "Subject", HeaderText = "Follow-Up Objective", Width = 220 };
            var colFUpNotes = new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "Notes & Plan", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 200 };
            var colFUpStatus = new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", Width = 120 };
            var colFUpScheduled = new DataGridViewTextBoxColumn { Name = "Scheduled", HeaderText = "Scheduled For", Width = 150 };
            var colFUpActions = new DataGridViewTextBoxColumn { Name = ColActions, HeaderText = "", Width = TableKit.ActionWidth };

            dgvFollowUps.Columns.AddRange(colFUpChannel, colFUpSubject, colFUpNotes, colFUpStatus, colFUpScheduled, colFUpActions);

            // Consistent sorting across all history tabs (matches the workbench lists).
            foreach (var grid in new[] { dgvRepairs, dgvInteractions, dgvFollowUps })
                foreach (DataGridViewColumn c in grid.Columns)
                    c.SortMode = c.Name == ColActions
                        ? DataGridViewColumnSortMode.NotSortable
                        : DataGridViewColumnSortMode.Automatic;
        }

        private void HookGridEvents(DataGridView dgv, int tabType)
        {
            dgv.CellPainting += Dgv_CellPainting;
            dgv.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= dgv.RowCount) return;
                if (dgv.Columns[e.ColumnIndex].Name == ColActions)
                {
                    _menuRowIndex = e.RowIndex;
                    var r = dgv.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                    var menu = tabType switch
                    {
                        0 => _repairsMenu,
                        1 => _interactionsMenu,
                        _ => _followUpsMenu
                    };
                    menu.Show(dgv, new Point(r.Right - 180, r.Bottom));
                }
            };

            dgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= dgv.RowCount) return;
                var row = dgv.Rows[e.RowIndex];

                if (tabType == 0 && row.Tag is CustomerHistoryRepairDto rDto)
                    OpenEditRepairDialog(rDto);
                else if (tabType == 1 && row.Tag is CustomerHistoryInteractionDto iDto)
                    OpenEditInteractionDialog(iDto);
                else if (tabType == 2 && row.Tag is CustomerHistoryFollowUpDto fDto)
                    OpenEditFollowUpDialog(fDto);
            };

            dgv.CellMouseEnter += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                _hoverRow = e.RowIndex;
                dgv.InvalidateRow(e.RowIndex);
                if (e.ColumnIndex >= 0 && dgv.Columns[e.ColumnIndex].Name == ColActions)
                    dgv.Cursor = Cursors.Hand;
            };

            dgv.CellMouseLeave += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                _hoverRow = -1;
                dgv.InvalidateRow(e.RowIndex);
                dgv.Cursor = Cursors.Default;
            };

            dgv.MouseLeave += (s, e) => { _hoverRow = -1; dgv.Invalidate(); };

            dgv.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Apps && e.KeyCode != Keys.Enter) return;
                if (dgv.CurrentRow == null) return;

                _menuRowIndex = dgv.CurrentRow.Index;
                if (e.KeyCode == Keys.Enter)
                {
                    var row = dgv.CurrentRow;
                    if (tabType == 0 && row.Tag is CustomerHistoryRepairDto rDto)
                    {
                        OpenEditRepairDialog(rDto);
                        e.Handled = true;
                        return;
                    }
                    if (tabType == 1 && row.Tag is CustomerHistoryInteractionDto iDto)
                    {
                        OpenEditInteractionDialog(iDto);
                        e.Handled = true;
                        return;
                    }
                    if (tabType == 2 && row.Tag is CustomerHistoryFollowUpDto fDto)
                    {
                        OpenEditFollowUpDialog(fDto);
                        e.Handled = true;
                        return;
                    }
                }

                var r = dgv.GetRowDisplayRectangle(_menuRowIndex, true);
                var menu = tabType switch
                {
                    0 => _repairsMenu,
                    1 => _interactionsMenu,
                    _ => _followUpsMenu
                };
                menu.Show(dgv, new Point(r.Right - 180, r.Bottom));
                e.Handled = true;
            };
        }

        // ═══════════ CELL PAINTING ═══════════

        private void Dgv_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null || sender is not DataGridView dgv) return;
            var g = e.Graphics;

            // Header Row
            if (e.RowIndex == -1)
            {
                e.PaintBackground(e.CellBounds, false);
                e.PaintContent(e.CellBounds);
                TableKit.PaintHeaderRule(g, e.CellBounds);
                e.Handled = true;
                return;
            }

            if (e.RowIndex < 0 || e.RowIndex >= dgv.RowCount) return;

            string col = dgv.Columns[e.ColumnIndex].Name;
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool hovered = e.RowIndex == _hoverRow;

            Color bg = TableKit.RowBackground(e, selected, hovered);

            TableKit.PaintRowShell(g, e.CellBounds, bg);

            UiKit.Quality(g);
            var r = e.CellBounds;
            string text = e.FormattedValue?.ToString() ?? "";

            // Actions Meatball Column
            if (col == ColActions)
            {
                TableKit.PaintActionCell(g, r, hovered);
                e.Handled = true;
                return;
            }

            // Priority — always a pill (matches the Repairs workbench).
            if (col == "Priority")
            {
                if (text == "Urgent" || text == "High" || text == "Medium")
                {
                    Color accent = text switch
                    {
                        "Urgent" => AppTheme.Danger,
                        "High" => AppTheme.Warning,
                        _ => AppTheme.Primary
                    };
                    var size = UiKit.Measure(text, UiKit.T.SmallStrong);
                    var pill = new Rectangle(r.Left + UiKit.T.S3, r.Top + (r.Height - 22) / 2,
                        Math.Max(56, size.Width + 20), 22);
                    UiKit.FillRounded(g, pill, UiKit.T.PillRadius, UiKit.Wash(accent));
                    UiKit.Text(g, text, UiKit.T.SmallStrong, accent, pill,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    TableKit.PaintPill(g, r, text);
                }

                e.Handled = true;
                return;
            }

            // Status Pill
            if (col == "Status")
            {
                Color accent = text switch
                {
                    "Completed" or "Closed" => AppTheme.Success,
                    "In Progress" or "Scheduled" => AppTheme.Primary,
                    "Pending" or "Open" => AppTheme.Warning,
                    "Approved" => AppTheme.Primary,
                    "Rejected" or "Cancelled" => AppTheme.Danger,
                    _ => UiKit.T.InkMuted
                };

                var size = UiKit.Measure(text, UiKit.T.SmallStrong);
                int pillW = Math.Max(56, size.Width + 20);
                int pillH = 22;
                var pill = new Rectangle(r.Left + UiKit.T.S3, r.Top + (r.Height - pillH) / 2, pillW, pillH);

                UiKit.FillRounded(g, pill, UiKit.T.PillRadius, UiKit.Wash(accent));
                UiKit.Text(g, text, UiKit.T.SmallStrong, accent, pill,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                e.Handled = true;
                return;
            }

            // Cost Display
            if (col == "Cost")
            {
                UiKit.Text(g, text, UiKit.T.BodyStrong, UiKit.T.Ink,
                    new Rectangle(r.Left, r.Top, r.Width - UiKit.T.S3, r.Height),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                e.Handled = true;
                return;
            }

            e.PaintContent(e.CellBounds);
            e.Handled = true;
        }

        // ═══════════ CONTEXT MENUS ═══════════

        private void BuildMenus()
        {
            _repairsMenu = TableKit.MakeMenu();
            _repairsMenu.Items.Add("Edit Repair Order...", null, (s, e) =>
            {
                if (_menuRowIndex >= 0 && _menuRowIndex < dgvRepairs.RowCount &&
                    dgvRepairs.Rows[_menuRowIndex].Tag is CustomerHistoryRepairDto dto)
                {
                    OpenEditRepairDialog(dto);
                }
            });
            _repairsMenu.Items.Add("Open in Repair Workbench", null, (s, e) => ActionRequested?.Invoke(this, "repairs"));
            TableKit.StyleMenuItems(_repairsMenu);

            _interactionsMenu = TableKit.MakeMenu();
            _interactionsMenu.Items.Add("View / Edit Interaction...", null, (s, e) =>
            {
                if (_menuRowIndex >= 0 && _menuRowIndex < dgvInteractions.RowCount &&
                    dgvInteractions.Rows[_menuRowIndex].Tag is CustomerHistoryInteractionDto dto)
                {
                    OpenEditInteractionDialog(dto);
                }
            });
            _interactionsMenu.Items.Add("Open in Interactions Workbench", null, (s, e) => ActionRequested?.Invoke(this, "interactions"));
            TableKit.StyleMenuItems(_interactionsMenu);

            _followUpsMenu = TableKit.MakeMenu();
            _followUpsMenu.Items.Add("View / Edit Follow-Up...", null, (s, e) =>
            {
                if (_menuRowIndex >= 0 && _menuRowIndex < dgvFollowUps.RowCount &&
                    dgvFollowUps.Rows[_menuRowIndex].Tag is CustomerHistoryFollowUpDto dto)
                {
                    OpenEditFollowUpDialog(dto);
                }
            });
            _followUpsMenu.Items.Add("Open in Follow-Ups Workbench", null, (s, e) => ActionRequested?.Invoke(this, "follow-ups"));
            TableKit.StyleMenuItems(_followUpsMenu);
        }

        // ═══════════ MODALS & ACTIONS ═══════════

        private void OpenIntakeRepairDialog()
        {
            int custId = _currentHistory?.CustomerId ?? 0;
            using var dlg = new RepairRequestFormDialog(null, custId, _allCustomers);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                if (_currentHistory != null)
                    _ = FetchAndDisplayHistory(_currentHistory.CustomerId);
            }
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
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                if (_currentHistory != null)
                    _ = FetchAndDisplayHistory(_currentHistory.CustomerId);
            }
        }

        private void OpenLogInteractionDialog()
        {
            if (_currentHistory == null) return;
            var prefill = new InteractionDto { CustomerId = _currentHistory.CustomerId };
            using var dlg = new InteractionFormDialog(prefill, null, _allCustomers);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
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
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                if (_currentHistory != null)
                    _ = FetchAndDisplayHistory(_currentHistory.CustomerId);
            }
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
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
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
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                if (_currentHistory != null)
                    _ = FetchAndDisplayHistory(_currentHistory.CustomerId);
            }
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
                MessageBox.Show($"Phone: {_currentHistory.Phone} (Copied to clipboard)", "Customer Contact", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show($"Email: {_currentHistory.Email} (Copied to clipboard)", "Customer Contact", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  NESTED UI COMPONENTS (Matching CustomerListControl)
        // ═══════════════════════════════════════════════════════════════

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