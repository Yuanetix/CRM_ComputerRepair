using CRM.winforms.Auth;
using CRM.winforms.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Customer Retention &amp; Email Campaigns — Complete Workspace.
    /// Incorporates dynamic segment analysis, approval-governed requests,
    /// anti-fatigue cooldown enforcement, and real SMTP dispatching.
    /// </summary>
    [DesignerCategory("Code")]
    public class RetentionControl : UserControl
    {
        // ═══════════ SERVICES & CACHED DATA ═══════════
        private readonly ApiClient _api = new ApiClient();

        private List<RetentionRecommendationDto> _cachedRecommendations = new();
        private List<RetentionRequestDto> _cachedRequests = new();
        private List<RetentionCampaignDto> _cachedCampaigns = new();
        private List<CustomerDto> _cachedCustomers = new();
        private List<RetentionTemplateDto> _cachedTemplates = new();
        private RetentionSettingsDto _cachedSettings = new();

        private readonly bool _isAdmin;
        private readonly bool _isManager;
        private readonly bool _hasAccess;
        private bool _isPopulating = false;

        // ═══════════ SHELL & HEADER ═══════════
        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private FlatButton btnRefresh = null!;
        private FlatButton btnNewRequest = null!;

        private Panel pnlTabBar = null!;
        private readonly List<TabButton> _navTabs = new();
        private string _activeTabKey = "segments";

        private Panel pnlTabHost = null!;
        private Panel pnlTabSegments = null!;
        private Panel pnlTabApprovals = null!;
        private Panel pnlTabCampaigns = null!;
        private Panel pnlTabManual = null!;
        private Panel pnlTabSettings = null!;
        private Panel pnlAccessDenied = null!;

        // ═══════════ TAB 1: SEGMENTS ═══════════
        private RetentionKpiCard tileTotal = null!;
        private RetentionKpiCard tileLoyal = null!;
        private RetentionKpiCard tileReturning = null!;
        private RetentionKpiCard tileAtRisk = null!;
        private RetentionKpiCard tileInactive = null!;

        private SurfaceCard cardSegments = null!;
        private Label lblSegmentsTitle = null!;
        private Label lblSegmentsCount = null!;
        private SearchBox searchSegments = null!;
        private Panel pnlSegmentPills = null!;
        private readonly List<TabButton> _segmentPills = new();
        private string _selectedSegmentCategory = "All";
        private DataGridView dgvSegments = null!;
        private StateView stateSegments = null!;

        // ═══════════ TAB 2: APPROVALS ═══════════
        private SurfaceCard cardApprovals = null!;
        private Label lblApprovalsTitle = null!;
        private Label lblApprovalsCount = null!;
        private SearchBox searchApprovals = null!;
        private Panel pnlApprovalPills = null!;
        private readonly List<TabButton> _approvalPills = new();
        private string _selectedApprovalStatus = "All";
        private DateTimePicker dtpApprovalFrom = null!;
        private DateTimePicker dtpApprovalTo = null!;
        private CheckBox chkApprovalAllDates = null!;
        private DataGridView dgvApprovals = null!;
        private StateView stateApprovals = null!;

        // ═══════════ TAB 3: CAMPAIGNS ═══════════
        private SurfaceCard cardCampaigns = null!;
        private Label lblCampaignsTitle = null!;
        private Label lblCampaignsCount = null!;
        private SearchBox searchCampaigns = null!;
        private Panel pnlCampaignPills = null!;
        private readonly List<TabButton> _campaignPills = new();
        private string _selectedCampaignStatus = "All";
        private DateTimePicker dtpCampaignFrom = null!;
        private DateTimePicker dtpCampaignTo = null!;
        private CheckBox chkCampaignAllDates = null!;
        private DataGridView dgvCampaigns = null!;
        private StateView stateCampaigns = null!;

        // ═══════════ TAB 4: MANUAL EMAIL ═══════════
        private SurfaceCard cardManualCompose = null!;
        private Label lblManualHeading = null!;
        private Label lblManualDesc = null!;
        private Label lblCustTitle = null!;
        private ComboBox cmbManualCustomer = null!;
        private Label lblManualRecipientTitle = null!;
        private TextBox txtManualRecipientEmail = null!;
        private Panel pnlCustDetailsBox = null!;
        private Label lblManualCustDetails = null!;
        private Panel pnlCooldownAlert = null!;
        private Label lblCooldownAlert = null!;
        private CheckBox chkOverrideCooldown = null!;

        private Label lblTplTitle = null!;
        private Label lblDiscTitle = null!;
        private Label lblValidTitle = null!;
        private Label lblCodeTitle = null!;
        private ComboBox cmbManualTemplate = null!;
        private Label lblSubjTitle = null!;
        private TextBox txtManualSubject = null!;
        private NumericUpDown numManualDiscount = null!;
        private NumericUpDown numManualValidity = null!;
        private TextBox txtManualPromoCode = null!;
        private Label lblBodyTitle = null!;
        private Label lblBodyTokens = null!;
        private TextBox txtManualBody = null!;

        private SurfaceCard cardManualPreview = null!;
        private Label lblPreviewHeading = null!;
        private Label lblPreviewDesc = null!;
        private Panel pnlPreviewEnvelope = null!;
        private Label lblPreviewFrom = null!;
        private Label lblPreviewTo = null!;
        private Label lblPreviewSubject = null!;
        private Label lblPreviewDate = null!;
        private Panel pnlPreviewSheet = null!;
        private TextBox txtManualPreview = null!;
        private SaasButton btnManualSend = null!;

        // ═══════════ TAB 5: SETTINGS ═══════════
        private SurfaceCard cardSettingsRules = null!;
        private NumericUpDown numInactiveDays = null!;
        private NumericUpDown numAtRiskDays = null!;
        private NumericUpDown numAntiFatigueDays = null!;
        private NumericUpDown numOfferValidityDays = null!;
        private Button btnSaveRules = null!;

        private SurfaceCard cardSettingsSmtp = null!;
        private TextBox txtSmtpHost = null!;
        private NumericUpDown numSmtpPort = null!;
        private TextBox txtSmtpUsername = null!;
        private TextBox txtSmtpPassword = null!;
        private TextBox txtSmtpFromEmail = null!;
        private TextBox txtSmtpFromName = null!;
        private CheckBox chkSmtpSsl = null!;
        private Button btnTestSmtp = null!;
        private Button btnSaveSmtp = null!;

        private SurfaceCard cardSettingsTemplates = null!;
        private ComboBox cmbTemplateSegment = null!;
        private TextBox txtTemplateName = null!;
        private TextBox txtTemplateSubject = null!;
        private NumericUpDown numTemplateDiscount = null!;
        private NumericUpDown numTemplateValidity = null!;
        private TextBox txtTemplateBody = null!;
        private Button btnSaveTemplate = null!;

        public RetentionControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            AutoScaleMode = AutoScaleMode.Font;

            _isAdmin = UserSession.Role == "Admin" || UserSession.Role == "Super Admin";
            _isManager = UserSession.Role == "Manager";
            _hasAccess = _isAdmin || _isManager;

            BuildUi();

            this.Load += async (s, e) =>
            {
                if (_hasAccess)
                    await ReloadAsync();
            };
        }

        // ═══════════════════════════════════════════════════════
        // UI CONSTRUCTION
        // ═══════════════════════════════════════════════════════

        private void BuildUi()
        {
            if (!_hasAccess)
            {
                BuildAccessDeniedUi();
                return;
            }

            lblTitle = new Label
            {
                Text = "Customer Retention",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = AppTheme.Background,
                UseMnemonic = false
            };

            lblSubtitle = new Label
            {
                Text = "Dynamic segment calculations, administrative approval governance, and live campaigns.",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = AppTheme.Background,
                UseMnemonic = false
            };

            btnRefresh = new FlatButton("Refresh", "\uE72C");
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            btnNewRequest = new FlatButton("New Retention Request", "\uE710");
            btnNewRequest.Click += (s, e) => OpenNewRequestDialog();

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnRefresh);
            Controls.Add(btnNewRequest);

            pnlTabBar = new Panel { BackColor = AppTheme.Background };

            var tabs = new[]
            {
                ("segments", "Segments & Opportunities"),
                ("approvals", "Approvals"),
                ("campaigns", "Email Campaigns"),
                ("manual", "Manual Email"),
                ("settings", "Settings & Templates")
            };

            foreach (var (key, label) in tabs)
            {
                var tab = new TabButton(label, key);
                tab.Click += (s, e) => SwitchTab(key);
                _navTabs.Add(tab);
                pnlTabBar.Controls.Add(tab);
            }
            _navTabs[0].IsActive = true;
            Controls.Add(pnlTabBar);

            pnlTabHost = new Panel { BackColor = AppTheme.Background, Dock = DockStyle.None };
            Controls.Add(pnlTabHost);

            BuildSegmentsTab();
            BuildApprovalsTab();
            BuildCampaignsTab();
            BuildManualEmailTab();
            BuildSettingsTab();

            SwitchTab("segments");

            Resize += (s, e) => LayoutUi();
        }

        private void BuildAccessDeniedUi()
        {
            pnlAccessDenied = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background };

            var state = new StateView { Dock = DockStyle.Fill };
            state.Show("\uE72E", "Access Restricted",
                "Customer Retention & Email Campaigns are restricted to Manager and Administrator roles. Contact your system administrator if you require access.");
            pnlAccessDenied.Controls.Add(state);
            Controls.Add(pnlAccessDenied);
        }

        // ═══════════════════════════════════════════════════════
        // TAB 1: SEGMENTS
        // ═══════════════════════════════════════════════════════

        private void BuildSegmentsTab()
        {
            pnlTabSegments = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background, AutoScroll = true };

            tileTotal = new RetentionKpiCard();
            tileTotal.Set("Active Opportunities", "0", "All recommendations", AppTheme.Primary);

            tileLoyal = new RetentionKpiCard();
            tileLoyal.Set("Loyal", "0", "≥ 3 repairs", Color.FromArgb(79, 70, 229));

            tileReturning = new RetentionKpiCard();
            tileReturning.Set("Returning", "0", "2 repairs", Color.FromArgb(14, 116, 144));

            tileAtRisk = new RetentionKpiCard();
            tileAtRisk.Set("At-Risk", "0", "91–180d inactive", Color.FromArgb(194, 65, 12));

            tileInactive = new RetentionKpiCard();
            tileInactive.Set("Inactive", "0", "> 180d inactive", Color.FromArgb(107, 114, 128));

            tileTotal.ContentChanged += (s, e) => LayoutSegmentsTab();
            tileLoyal.ContentChanged += (s, e) => LayoutSegmentsTab();
            tileReturning.ContentChanged += (s, e) => LayoutSegmentsTab();
            tileAtRisk.ContentChanged += (s, e) => LayoutSegmentsTab();
            tileInactive.ContentChanged += (s, e) => LayoutSegmentsTab();

            pnlTabSegments.Controls.Add(tileTotal);
            pnlTabSegments.Controls.Add(tileLoyal);
            pnlTabSegments.Controls.Add(tileReturning);
            pnlTabSegments.Controls.Add(tileAtRisk);
            pnlTabSegments.Controls.Add(tileInactive);

            cardSegments = new SurfaceCard();

            lblSegmentsTitle = new Label
            {
                Text = "Retention Recommendations",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };

            lblSegmentsCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };

            pnlSegmentPills = new Panel { BackColor = UiKit.T.Surface };
            foreach (var cat in new[] { "All", "Loyal", "Returning", "New", "AtRisk", "Inactive" })
            {
                string display = cat switch { "AtRisk" => "At-Risk", _ => cat };
                var pill = new TabButton(display, cat);
                pill.Click += (s, e) =>
                {
                    _selectedSegmentCategory = cat;
                    foreach (var p in _segmentPills) p.IsActive = p.Category == cat;
                    ApplySegmentsFilter();
                };
                _segmentPills.Add(pill);
                pnlSegmentPills.Controls.Add(pill);
            }
            _segmentPills[0].IsActive = true;

            searchSegments = new SearchBox { PlaceholderText = "Search customer, trigger, segment..." };
            searchSegments.Inner.TextChanged += (s, e) => ApplySegmentsFilter();

            dgvSegments = new DataGridView();
            StyleGrid(dgvSegments);
            ConfigureSegmentsColumns();
            dgvSegments.CellPainting += DgvSegments_CellPainting;
            dgvSegments.CellContentClick += DgvSegments_CellContentClick;
            dgvSegments.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvSegments.Rows[e.RowIndex].DataBoundItem is RetentionRecommendationDto item)
                    OpenNewRequestDialog(item.CustomerId);
            };

            stateSegments = new StateView { Visible = false };

            cardSegments.Controls.Add(lblSegmentsTitle);
            cardSegments.Controls.Add(lblSegmentsCount);
            cardSegments.Controls.Add(pnlSegmentPills);
            cardSegments.Controls.Add(searchSegments);
            cardSegments.Controls.Add(dgvSegments);
            cardSegments.Controls.Add(stateSegments);

            pnlTabSegments.Controls.Add(cardSegments);
            pnlTabHost.Controls.Add(pnlTabSegments);
        }

        private void ConfigureSegmentsColumns()
        {
            dgvSegments.AutoGenerateColumns = false;
            dgvSegments.Columns.Clear();

            void Col(string prop, string header, int width, bool fill = false, DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft, Font? font = null)
            {
                var c = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = prop,
                    HeaderText = header,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                };
                if (fill)
                {
                    c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    c.MinimumWidth = Math.Max(180, width);
                }
                else
                {
                    c.Width = width;
                    c.MinimumWidth = width;
                }
                c.DefaultCellStyle.Alignment = align;
                if (font != null) c.DefaultCellStyle.Font = font;
                dgvSegments.Columns.Add(c);
            }

            Col("CustomerName", "CUSTOMER", 190, font: UiKit.T.BodyStrong);
            Col("SegmentName", "SEGMENT", 120);
            Col("Action", "RECOMMENDED ACTION", 240);
            Col("Basis", "TRIGGER BASIS & HISTORY", 260, fill: true);
            Col("TotalSpent", "TOTAL SPEND", 130, align: DataGridViewContentAlignment.MiddleRight);
            Col("DaysSinceLastTransaction", "LAST VISIT", 110, align: DataGridViewContentAlignment.MiddleCenter);

            var btnCol = new DataGridViewButtonColumn
            {
                Name = "colAction",
                HeaderText = "ACTION",
                Text = "Create Request",
                UseColumnTextForButtonValue = true,
                Width = 150,
                MinimumWidth = 150,
                FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            btnCol.DefaultCellStyle.ForeColor = AppTheme.Primary;
            btnCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            btnCol.DefaultCellStyle.BackColor = UiKit.T.Surface;
            btnCol.DefaultCellStyle.SelectionBackColor = UiKit.T.RowHover;
            btnCol.DefaultCellStyle.Padding = new Padding(0);
            dgvSegments.Columns.Add(btnCol);
        }

        private void DgvSegments_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.Graphics == null) return;

            // Header
            if (e.RowIndex == -1)
            {
                PaintHeaderCell(e);
                return;
            }

            // Row background
            PaintRowBackground(e);

            string prop = dgvSegments.Columns[e.ColumnIndex].DataPropertyName;
            var cell = e.CellBounds;
            var inner = new Rectangle(cell.Left + 14, cell.Top, Math.Max(0, cell.Width - 28), cell.Height);
            int cy = cell.Top + cell.Height / 2;

            if (prop == "SegmentName")
            {
                string text = e.Value?.ToString() ?? "";
                PillBadgeRenderer.PaintSegmentBadge(e.Graphics, cell, text);
                e.Handled = true;
                return;
            }

            if (prop == "DaysSinceLastTransaction")
            {
                if (e.Value is int days)
                {
                    string text = days == 0 ? "Today" : $"{days}d ago";
                    Color color = days > 90 ? Color.FromArgb(194, 65, 12) : UiKit.T.InkMuted;
                    UiKit.Text(e.Graphics, text, UiKit.T.Small, color, inner, UiKit.Center);
                    e.Handled = true;
                }
                return;
            }

            if (prop == "TotalSpent" && e.Value is decimal amount)
            {
                UiKit.Text(e.Graphics, $"₱{amount:N2}", UiKit.T.Body, UiKit.T.Ink, inner,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                e.Handled = true;
                return;
            }

            e.PaintContent(cell);
            e.Handled = true;
        }

        private void DgvSegments_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvSegments.Columns[e.ColumnIndex].Name == "colAction")
            {
                if (dgvSegments.Rows[e.RowIndex].DataBoundItem is RetentionRecommendationDto item)
                    OpenNewRequestDialog(item.CustomerId);
            }
        }

        // ═══════════════════════════════════════════════════════
        // TAB 2: APPROVALS
        // ═══════════════════════════════════════════════════════

        private void BuildApprovalsTab()
        {
            pnlTabApprovals = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
            cardApprovals = new SurfaceCard();

            lblApprovalsTitle = new Label
            {
                Text = "Retention Request Approvals",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };

            lblApprovalsCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };

            pnlApprovalPills = new Panel { BackColor = UiKit.T.Surface };
            foreach (var status in new[] { "All", "Pending", "Approved", "Rejected" })
            {
                var pill = new TabButton(status, status);
                pill.Click += (s, e) =>
                {
                    _selectedApprovalStatus = status;
                    foreach (var p in _approvalPills) p.IsActive = p.Category == status;
                    ApplyApprovalsFilter();
                };
                _approvalPills.Add(pill);
                pnlApprovalPills.Controls.Add(pill);
            }
            _approvalPills[0].IsActive = true;

            dtpApprovalFrom = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today.AddDays(-60),
                Width = 130,
                Font = UiKit.T.Body
            };
            dtpApprovalFrom.ValueChanged += (s, e) => ApplyApprovalsFilter();

            dtpApprovalTo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Width = 130,
                Font = UiKit.T.Body
            };
            dtpApprovalTo.ValueChanged += (s, e) => ApplyApprovalsFilter();

            chkApprovalAllDates = new CheckBox
            {
                Text = "All Time",
                Checked = true,
                AutoSize = true,
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                BackColor = UiKit.T.Surface
            };
            chkApprovalAllDates.CheckedChanged += (s, e) =>
            {
                dtpApprovalFrom.Enabled = !chkApprovalAllDates.Checked;
                dtpApprovalTo.Enabled = !chkApprovalAllDates.Checked;
                ApplyApprovalsFilter();
            };
            dtpApprovalFrom.Enabled = false;
            dtpApprovalTo.Enabled = false;

            searchApprovals = new SearchBox { PlaceholderText = "Search customer, reason, submitter..." };
            searchApprovals.Inner.TextChanged += (s, e) => ApplyApprovalsFilter();

            dgvApprovals = new DataGridView();
            StyleGrid(dgvApprovals);
            ConfigureApprovalsColumns();
            dgvApprovals.CellPainting += DgvApprovals_CellPainting;
            dgvApprovals.CellContentClick += DgvApprovals_CellContentClick;
            dgvApprovals.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvApprovals.Rows[e.RowIndex].DataBoundItem is RetentionRequestDto req)
                    OpenReviewDialog(req);
            };

            stateApprovals = new StateView { Visible = false };

            cardApprovals.Controls.Add(lblApprovalsTitle);
            cardApprovals.Controls.Add(lblApprovalsCount);
            cardApprovals.Controls.Add(pnlApprovalPills);
            cardApprovals.Controls.Add(dtpApprovalFrom);
            cardApprovals.Controls.Add(dtpApprovalTo);
            cardApprovals.Controls.Add(chkApprovalAllDates);
            cardApprovals.Controls.Add(searchApprovals);
            cardApprovals.Controls.Add(dgvApprovals);
            cardApprovals.Controls.Add(stateApprovals);

            pnlTabApprovals.Controls.Add(cardApprovals);
            pnlTabHost.Controls.Add(pnlTabApprovals);
        }

        private void ConfigureApprovalsColumns()
        {
            dgvApprovals.AutoGenerateColumns = false;
            dgvApprovals.Columns.Clear();

            void Col(string prop, string header, int width, bool fill = false, DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft, Font? font = null, string format = "")
            {
                var c = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = prop,
                    HeaderText = header,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                };
                if (fill)
                {
                    c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    c.MinimumWidth = Math.Max(180, width);
                }
                else
                {
                    c.Width = width;
                    c.MinimumWidth = width;
                }
                c.DefaultCellStyle.Alignment = align;
                if (font != null) c.DefaultCellStyle.Font = font;
                if (!string.IsNullOrEmpty(format)) c.DefaultCellStyle.Format = format;
                dgvApprovals.Columns.Add(c);
            }

            Col("CustomerName", "CUSTOMER", 190, font: UiKit.T.BodyStrong);
            Col("TargetSegmentName", "SEGMENT", 120);
            Col("ProposedDiscountPercent", "OFFER", 100, align: DataGridViewContentAlignment.MiddleCenter, format: "0.#\\%");
            Col("ReasonCategory", "REASON & STRATEGIC CONTEXT", 260, fill: true);
            Col("StatusText", "STATUS", 120);
            Col("SubmittedByName", "SUBMITTED BY", 150);
            Col("SubmittedAt", "SUBMITTED", 140, align: DataGridViewContentAlignment.MiddleCenter, format: "MMM d, yyyy");

            var btnCol = new DataGridViewButtonColumn
            {
                Name = "colReview",
                HeaderText = "ACTION",
                Text = "Review / View",
                UseColumnTextForButtonValue = true,
                Width = 140,
                MinimumWidth = 140,
                FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            btnCol.DefaultCellStyle.ForeColor = AppTheme.Primary;
            btnCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            btnCol.DefaultCellStyle.BackColor = UiKit.T.Surface;
            btnCol.DefaultCellStyle.SelectionBackColor = UiKit.T.RowHover;
            btnCol.DefaultCellStyle.Padding = new Padding(0);
            dgvApprovals.Columns.Add(btnCol);
        }

        private void DgvApprovals_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.Graphics == null) return;

            if (e.RowIndex == -1)
            {
                PaintHeaderCell(e);
                return;
            }

            PaintRowBackground(e);

            string prop = dgvApprovals.Columns[e.ColumnIndex].DataPropertyName;
            if (prop == "StatusText")
            {
                string text = e.Value?.ToString() ?? "";
                PillBadgeRenderer.PaintApprovalStatusBadge(e.Graphics, e.CellBounds, text);
                e.Handled = true;
                return;
            }

            if (prop == "TargetSegmentName")
            {
                string text = e.Value?.ToString() ?? "";
                PillBadgeRenderer.PaintSegmentBadge(e.Graphics, e.CellBounds, text);
                e.Handled = true;
                return;
            }

            e.PaintContent(e.CellBounds);
            e.Handled = true;
        }

        private void DgvApprovals_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvApprovals.Columns[e.ColumnIndex].Name == "colReview")
            {
                if (dgvApprovals.Rows[e.RowIndex].DataBoundItem is RetentionRequestDto req)
                    OpenReviewDialog(req);
            }
        }

        // ═══════════════════════════════════════════════════════
        // TAB 3: CAMPAIGNS
        // ═══════════════════════════════════════════════════════

        private void BuildCampaignsTab()
        {
            pnlTabCampaigns = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
            cardCampaigns = new SurfaceCard();

            lblCampaignsTitle = new Label
            {
                Text = "Retention Email Campaigns",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };

            lblCampaignsCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };

            pnlCampaignPills = new Panel { BackColor = UiKit.T.Surface };
            foreach (var status in new[] { "All", "Pending", "Sent", "Simulated", "Failed" })
            {
                var pill = new TabButton(status, status);
                pill.Click += (s, e) =>
                {
                    _selectedCampaignStatus = status;
                    foreach (var p in _campaignPills) p.IsActive = p.Category == status;
                    ApplyCampaignsFilter();
                };
                _campaignPills.Add(pill);
                pnlCampaignPills.Controls.Add(pill);
            }
            _campaignPills[0].IsActive = true;

            dtpCampaignFrom = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today.AddDays(-60),
                Width = 130,
                Font = UiKit.T.Body
            };
            dtpCampaignFrom.ValueChanged += (s, e) => ApplyCampaignsFilter();

            dtpCampaignTo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Width = 130,
                Font = UiKit.T.Body
            };
            dtpCampaignTo.ValueChanged += (s, e) => ApplyCampaignsFilter();

            chkCampaignAllDates = new CheckBox
            {
                Text = "All Time",
                Checked = true,
                AutoSize = true,
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                BackColor = UiKit.T.Surface
            };
            chkCampaignAllDates.CheckedChanged += (s, e) =>
            {
                dtpCampaignFrom.Enabled = !chkCampaignAllDates.Checked;
                dtpCampaignTo.Enabled = !chkCampaignAllDates.Checked;
                ApplyCampaignsFilter();
            };
            dtpCampaignFrom.Enabled = false;
            dtpCampaignTo.Enabled = false;

            searchCampaigns = new SearchBox { PlaceholderText = "Search recipient, subject, code..." };
            searchCampaigns.Inner.TextChanged += (s, e) => ApplyCampaignsFilter();

            dgvCampaigns = new DataGridView();
            StyleGrid(dgvCampaigns);
            ConfigureCampaignsColumns();
            dgvCampaigns.CellPainting += DgvCampaigns_CellPainting;
            dgvCampaigns.CellContentClick += DgvCampaigns_CellContentClick;
            dgvCampaigns.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvCampaigns.Rows[e.RowIndex].DataBoundItem is RetentionCampaignDto c)
                    ShowCampaignEmailPreview(c);
            };

            stateCampaigns = new StateView { Visible = false };

            cardCampaigns.Controls.Add(lblCampaignsTitle);
            cardCampaigns.Controls.Add(lblCampaignsCount);
            cardCampaigns.Controls.Add(pnlCampaignPills);
            cardCampaigns.Controls.Add(dtpCampaignFrom);
            cardCampaigns.Controls.Add(dtpCampaignTo);
            cardCampaigns.Controls.Add(chkCampaignAllDates);
            cardCampaigns.Controls.Add(searchCampaigns);
            cardCampaigns.Controls.Add(dgvCampaigns);
            cardCampaigns.Controls.Add(stateCampaigns);

            pnlTabCampaigns.Controls.Add(cardCampaigns);
            pnlTabHost.Controls.Add(pnlTabCampaigns);
        }

        private void ConfigureCampaignsColumns()
        {
            dgvCampaigns.AutoGenerateColumns = false;
            dgvCampaigns.Columns.Clear();

            void Col(string prop, string header, int width, bool fill = false, DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft, Font? font = null, string format = "")
            {
                var c = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = prop,
                    HeaderText = header,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                };
                if (fill)
                {
                    c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    c.MinimumWidth = Math.Max(180, width);
                }
                else
                {
                    c.Width = width;
                    c.MinimumWidth = width;
                }
                c.DefaultCellStyle.Alignment = align;
                if (font != null) c.DefaultCellStyle.Font = font;
                if (!string.IsNullOrEmpty(format)) c.DefaultCellStyle.Format = format;
                dgvCampaigns.Columns.Add(c);
            }

            Col("RecipientName", "RECIPIENT", 190, font: UiKit.T.BodyStrong);
            Col("RecipientEmail", "EMAIL ADDRESS", 200);
            Col("SegmentName", "SEGMENT", 120);
            Col("PromoCode", "PROMO CODE", 150, font: UiKit.T.SmallStrong);
            Col("Subject", "SUBJECT", 240, fill: true);
            Col("DeliveryStatus", "DELIVERY", 130);
            Col("CreatedAt", "CREATED", 140, align: DataGridViewContentAlignment.MiddleCenter, format: "MMM d, yyyy");

            var btnCol = new DataGridViewButtonColumn
            {
                Name = "colDispatch",
                HeaderText = "ACTION",
                Text = "Dispatch Now",
                UseColumnTextForButtonValue = true,
                Width = 150,
                MinimumWidth = 150,
                FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            btnCol.DefaultCellStyle.ForeColor = AppTheme.Primary;
            btnCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            btnCol.DefaultCellStyle.BackColor = UiKit.T.Surface;
            btnCol.DefaultCellStyle.SelectionBackColor = UiKit.T.RowHover;
            btnCol.DefaultCellStyle.Padding = new Padding(0);
            dgvCampaigns.Columns.Add(btnCol);
        }

        private void DgvCampaigns_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.Graphics == null) return;

            if (e.RowIndex == -1)
            {
                PaintHeaderCell(e);
                return;
            }

            PaintRowBackground(e);

            string prop = dgvCampaigns.Columns[e.ColumnIndex].DataPropertyName;
            if (prop == "DeliveryStatus")
            {
                string text = e.Value?.ToString() ?? "";
                PillBadgeRenderer.PaintCampaignStatusBadge(e.Graphics, e.CellBounds, text);
                e.Handled = true;
                return;
            }

            if (prop == "SegmentName")
            {
                string text = e.Value?.ToString() ?? "";
                PillBadgeRenderer.PaintSegmentBadge(e.Graphics, e.CellBounds, text);
                e.Handled = true;
                return;
            }

            e.PaintContent(e.CellBounds);
            e.Handled = true;
        }

        private async void DgvCampaigns_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dgvCampaigns.Columns[e.ColumnIndex].Name == "colDispatch")
            {
                if (dgvCampaigns.Rows[e.RowIndex].DataBoundItem is RetentionCampaignDto c)
                {
                    if (c.IsDispatched && c.DeliveryStatus == "Sent")
                    {
                        var confirm = MessageBox.Show(
                            $"This campaign has already been sent on {c.DispatchedAt:MMM d, yyyy h:mm tt}.\n\nDo you want to re-dispatch this email via SMTP?",
                            "Re-dispatch Confirmation",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);
                        if (confirm != DialogResult.Yes) return;
                    }

                    try
                    {
                        var dispatchResult = await _api.DispatchRetentionEmailAsync(c.RetentionEmailLogId);

                        if (!string.IsNullOrWhiteSpace(dispatchResult.OutboxFilePath) && File.Exists(dispatchResult.OutboxFilePath))
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = dispatchResult.OutboxFilePath,
                                    UseShellExecute = true
                                });
                            }
                            catch { }
                        }

                        string notice = dispatchResult.WasFallback
                            ? $"Campaign email generated and delivered to Local Outbox for {c.RecipientEmail} (opened in browser)!\n\nDelivery Status: {dispatchResult.DeliveryStatus}\n\nNote: To deliver directly to your real Gmail inbox, enter your 16-character Google App Password in Settings > SMTP Configuration."
                            : $"Campaign successfully dispatched to {c.RecipientEmail} via Gmail SMTP!";

                        MessageBox.Show(notice, "Dispatch Complete",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        await ReloadAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to dispatch email:\n\n{ex.Message}", "Dispatch Failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ShowCampaignEmailPreview(RetentionCampaignDto c)
        {
            var dlg = new Form
            {
                Text = $"Email Campaign Preview — {c.Subject}",
                Size = new Size(680, 580),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable,
                ShowInTaskbar = false,
                BackColor = UiKit.T.Surface,
                MinimizeBox = false
            };

            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 110,
                BackColor = UiKit.T.LineSoft,
                Padding = new Padding(20, 16, 20, 12)
            };
            var lblInfo = new Label
            {
                Dock = DockStyle.Fill,
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.Ink,
                BackColor = UiKit.T.LineSoft,
                Text = $"To: {c.RecipientName} <{c.RecipientEmail}>\r\n" +
                       $"Segment: {c.SegmentName}   ·   Offer: {c.DiscountPercent:0.#}% OFF   ·   Promo Code: {c.PromoCode ?? "—"}\r\n" +
                       $"Delivery: {c.DeliveryStatus}   ·   Created: {c.CreatedAt:MMM d, yyyy h:mm tt}"
            };
            pnlTop.Controls.Add(lblInfo);

            var txtContent = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = UiKit.T.Surface,
                ForeColor = UiKit.T.Ink,
                Font = AppFonts.Regular(10F),
                BorderStyle = BorderStyle.None,
                Text = StripOrFormatHtml(c.FormattedBody)
            };

            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = UiKit.T.Surface,
                Padding = new Padding(16, 10, 16, 10)
            };

            var btnViewBrowser = new Button
            {
                Text = "View in Browser",
                Dock = DockStyle.Left,
                Width = 160,
                Font = UiKit.T.SmallStrong,
                BackColor = AppTheme.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnViewBrowser.FlatAppearance.BorderSize = 0;
            btnViewBrowser.Click += (s, e) =>
            {
                try
                {
                    var tempFile = Path.Combine(Path.GetTempPath(), $"Fixory_Campaign_{c.RetentionEmailLogId}.html");
                    File.WriteAllText(tempFile, c.FormattedBody, System.Text.Encoding.UTF8);
                    Process.Start(new ProcessStartInfo { FileName = tempFile, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open browser: {ex.Message}");
                }
            };
            pnlBottom.Controls.Add(btnViewBrowser);

            var btnClose = new Button
            {
                Text = "Close",
                Dock = DockStyle.Right,
                Width = 100,
                DialogResult = DialogResult.OK,
                Font = UiKit.T.SmallStrong,
                FlatStyle = FlatStyle.Flat,
                BackColor = UiKit.T.LineSoft,
                ForeColor = UiKit.T.Ink,
                UseVisualStyleBackColor = false
            };
            btnClose.FlatAppearance.BorderColor = UiKit.T.Line;
            pnlBottom.Controls.Add(btnClose);

            dlg.Controls.Add(txtContent);
            dlg.Controls.Add(pnlTop);
            dlg.Controls.Add(pnlBottom);
            dlg.ShowDialog(this);
        }

        // ═══════════════════════════════════════════════════════
        // TAB 4: MANUAL EMAIL
        // ═══════════════════════════════════════════════════════

        private void BuildManualEmailTab()
        {
            pnlTabManual = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background, AutoScroll = true };

            cardManualCompose = new SurfaceCard();

            lblManualHeading = new Label
            {
                Text = "Direct Retention Email Outreach",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };
            cardManualCompose.Controls.Add(lblManualHeading);

            lblManualDesc = new Label
            {
                Text = "Compose personalized retention offers with dynamic customer tokens and anti-fatigue cooldown governance.",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };
            cardManualCompose.Controls.Add(lblManualDesc);

            lblCustTitle = new Label { Text = "Recipient Customer *", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, AutoSize = true, BackColor = UiKit.T.Surface, UseMnemonic = false };
            cardManualCompose.Controls.Add(lblCustTitle);

            cmbManualCustomer = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DropDownWidth = 520,
                Font = UiKit.T.Body
            };
            cmbManualCustomer.SelectedIndexChanged += (s, e) => OnManualCustomerSelected();
            cmbManualCustomer.Leave += (s, e) => NormalizeComboSelection(cmbManualCustomer);
            cmbManualCustomer.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) NormalizeComboSelection(cmbManualCustomer); };
            cardManualCompose.Controls.Add(cmbManualCustomer);

            lblManualRecipientTitle = new Label { Text = "Recipient Email Address *", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, AutoSize = true, BackColor = UiKit.T.Surface, UseMnemonic = false };
            cardManualCompose.Controls.Add(lblManualRecipientTitle);

            txtManualRecipientEmail = new TextBox { Font = UiKit.T.Body };
            txtManualRecipientEmail.TextChanged += (s, e) => UpdateManualPreview();
            cardManualCompose.Controls.Add(txtManualRecipientEmail);

            pnlCustDetailsBox = new Panel { BackColor = Color.FromArgb(248, 250, 252) };
            lblManualCustDetails = new Label
            {
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.Ink,
                BackColor = Color.FromArgb(248, 250, 252),
                Text = "Select a customer to view dynamic segment metrics, transaction history, and contact information.",
                UseMnemonic = false
            };
            pnlCustDetailsBox.Controls.Add(lblManualCustDetails);
            cardManualCompose.Controls.Add(pnlCustDetailsBox);

            pnlCooldownAlert = new Panel { BackColor = Color.FromArgb(209, 250, 229) };
            lblCooldownAlert = new Label
            {
                Text = "Eligible for outreach. Cooldown policy satisfied.",
                Font = UiKit.T.SmallStrong,
                ForeColor = Color.FromArgb(6, 95, 70),
                BackColor = Color.FromArgb(209, 250, 229),
                UseMnemonic = false
            };
            pnlCooldownAlert.Controls.Add(lblCooldownAlert);

            chkOverrideCooldown = new CheckBox
            {
                Text = "Override cooldown",
                Font = UiKit.T.SmallStrong,
                ForeColor = Color.FromArgb(146, 64, 14),
                BackColor = Color.FromArgb(254, 243, 199),
                Visible = false,
                UseMnemonic = false
            };
            chkOverrideCooldown.CheckedChanged += (s, e) => UpdateManualSendButtonState();
            pnlCooldownAlert.Controls.Add(chkOverrideCooldown);
            cardManualCompose.Controls.Add(pnlCooldownAlert);

            lblTplTitle = new Label { Text = "Campaign Template", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, AutoSize = true, BackColor = UiKit.T.Surface, UseMnemonic = false };
            lblDiscTitle = new Label { Text = "Discount %", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, AutoSize = true, BackColor = UiKit.T.Surface, UseMnemonic = false };
            lblValidTitle = new Label { Text = "Validity (Days)", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, AutoSize = true, BackColor = UiKit.T.Surface, UseMnemonic = false };
            lblCodeTitle = new Label { Text = "Promo Code", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, AutoSize = true, BackColor = UiKit.T.Surface, UseMnemonic = false };

            cardManualCompose.Controls.Add(lblTplTitle);
            cardManualCompose.Controls.Add(lblDiscTitle);
            cardManualCompose.Controls.Add(lblValidTitle);
            cardManualCompose.Controls.Add(lblCodeTitle);

            cmbManualTemplate = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DropDownWidth = 360,
                Font = UiKit.T.Body
            };
            cmbManualTemplate.SelectedIndexChanged += (s, e) => OnManualTemplateChanged();
            cardManualCompose.Controls.Add(cmbManualTemplate);

            numManualDiscount = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 10, DecimalPlaces = 0, Font = UiKit.T.Body };
            numManualDiscount.ValueChanged += (s, e) => UpdateManualPreview();
            cardManualCompose.Controls.Add(numManualDiscount);

            numManualValidity = new NumericUpDown { Minimum = 1, Maximum = 365, Value = 14, Font = UiKit.T.Body };
            numManualValidity.ValueChanged += (s, e) => UpdateManualPreview();
            cardManualCompose.Controls.Add(numManualValidity);

            txtManualPromoCode = new TextBox
            {
                Font = UiKit.T.Body,
                Text = $"FIXORY-RET-{Guid.NewGuid().ToString()[..6].ToUpper()}"
            };
            txtManualPromoCode.TextChanged += (s, e) => UpdateManualPreview();
            cardManualCompose.Controls.Add(txtManualPromoCode);

            lblSubjTitle = new Label { Text = "Email Subject *", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, AutoSize = true, BackColor = UiKit.T.Surface, UseMnemonic = false };
            cardManualCompose.Controls.Add(lblSubjTitle);

            txtManualSubject = new TextBox
            {
                Font = UiKit.T.Body,
                Text = "Special Care & Maintenance Offer from Fixory Computer Repair"
            };
            txtManualSubject.TextChanged += (s, e) => UpdateManualPreview();
            cardManualCompose.Controls.Add(txtManualSubject);

            lblBodyTitle = new Label { Text = "Email Message *", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, AutoSize = true, BackColor = UiKit.T.Surface, UseMnemonic = false };
            cardManualCompose.Controls.Add(lblBodyTitle);

            lblBodyTokens = new Label
            {
                Text = "Tokens: {CustomerName}, {DiscountPercent}, {PromoCode}, {ValidUntil}",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };
            cardManualCompose.Controls.Add(lblBodyTokens);

            txtManualBody = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = AppFonts.Regular(9.5F),
                Text = "Dear {CustomerName},\r\n\r\nThank you for choosing Fixory for your computer repairs. As a valued client, we're pleased to offer you {DiscountPercent}% off your next hardware tune-up or diagnostic service.\r\n\r\nUse Promo Code: {PromoCode}\r\nValid until: {ValidUntil}\r\n\r\nBest regards,\r\nFixory Repair Team"
            };
            txtManualBody.TextChanged += (s, e) => UpdateManualPreview();
            cardManualCompose.Controls.Add(txtManualBody);

            btnManualSend = new SaasButton("Send Retention Email via SMTP", SaasButtonVariant.Primary, "\uE715")
            {
                Width = 280,
                Height = 40
            };
            btnManualSend.Click += async (s, e) => await SendManualRetentionEmailAsync();
            cardManualCompose.Controls.Add(btnManualSend);

            pnlTabManual.Controls.Add(cardManualCompose);

            // ── Right: Preview Card ──
            cardManualPreview = new SurfaceCard();

            lblPreviewHeading = new Label
            {
                Text = "Live Customer Preview",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };
            cardManualPreview.Controls.Add(lblPreviewHeading);

            lblPreviewDesc = new Label
            {
                Text = "Simulated message as rendered in the customer's email inbox.",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };
            cardManualPreview.Controls.Add(lblPreviewDesc);

            pnlPreviewEnvelope = new Panel { BackColor = Color.FromArgb(248, 250, 252) };

            lblPreviewFrom = new Label { Text = "From: Fixory Computer Repair <service@fixory.com>", Font = UiKit.T.Small, ForeColor = UiKit.T.InkMuted, BackColor = Color.FromArgb(248, 250, 252), UseMnemonic = false };
            lblPreviewTo = new Label { Text = "To: Customer <email@example.com>", Font = UiKit.T.Small, ForeColor = UiKit.T.InkMuted, BackColor = Color.FromArgb(248, 250, 252), UseMnemonic = false };
            lblPreviewSubject = new Label { Text = "Subject: Special Care & Maintenance Offer from Fixory Computer Repair", Font = UiKit.T.BodyStrong, ForeColor = UiKit.T.Ink, BackColor = Color.FromArgb(248, 250, 252), UseMnemonic = false };
            lblPreviewDate = new Label { Text = $"Date: {DateTime.Now:MMM d, yyyy  h:mm tt}", Font = UiKit.T.Small, ForeColor = UiKit.T.InkFaint, BackColor = Color.FromArgb(248, 250, 252), UseMnemonic = false };

            pnlPreviewEnvelope.Controls.Add(lblPreviewFrom);
            pnlPreviewEnvelope.Controls.Add(lblPreviewTo);
            pnlPreviewEnvelope.Controls.Add(lblPreviewSubject);
            pnlPreviewEnvelope.Controls.Add(lblPreviewDate);
            cardManualPreview.Controls.Add(pnlPreviewEnvelope);

            pnlPreviewSheet = new Panel { BackColor = Color.White };

            txtManualPreview = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.White,
                ForeColor = UiKit.T.Ink,
                ScrollBars = ScrollBars.Vertical,
                Font = AppFonts.Regular(10F),
                BorderStyle = BorderStyle.None
            };
            pnlPreviewSheet.Controls.Add(txtManualPreview);
            cardManualPreview.Controls.Add(pnlPreviewSheet);

            pnlTabManual.Controls.Add(cardManualPreview);
            pnlTabHost.Controls.Add(pnlTabManual);
        }

        private static void NormalizeComboSelection(ComboBox combo)
        {
            if (combo.SelectedIndex >= 0) return;
            if (string.IsNullOrWhiteSpace(combo.Text)) return;
            int idx = combo.FindStringExact(combo.Text.Trim());
            if (idx < 0) idx = combo.FindString(combo.Text.Trim());
            if (idx >= 0) combo.SelectedIndex = idx;
        }

        private void OnManualCustomerSelected()
        {
            if (_isPopulating) return;
            if (cmbManualCustomer == null || cmbManualCustomer.SelectedIndex < 0 || cmbManualCustomer.SelectedIndex >= _cachedCustomers.Count)
                return;

            var cust = _cachedCustomers[cmbManualCustomer.SelectedIndex];
            var rec = _cachedRecommendations.FirstOrDefault(r => r.CustomerId == cust.CustomerId);

            string seg = rec?.SegmentName ?? "New";
            int visits = rec?.TransactionCount ?? 0;
            decimal spent = rec?.TotalSpent ?? 0;
            int lastDays = rec?.DaysSinceLastTransaction ?? 0;

            lblManualCustDetails.Text = $"Segment: {seg}  ·  Repairs: {visits}  ·  Spent: ₱{spent:N2}  ·  Last Repair: {lastDays}d ago\r\n" +
                                        $"Contact: {cust.Email ?? "No email"}  ·  Phone: {cust.Phone ?? "No phone"}";

            if (txtManualRecipientEmail != null)
                txtManualRecipientEmail.Text = cust.Email ?? "";

            var recentCampaign = _cachedCampaigns
                .Where(c => c.CustomerId == cust.CustomerId)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefault();

            int cooldownDays = _cachedSettings.AntiFatigueDays > 0 ? _cachedSettings.AntiFatigueDays : 14;
            bool inCooldown = false;
            DateTime? lastSent = null;

            if (recentCampaign != null)
            {
                var daysAgo = (DateTime.UtcNow - recentCampaign.CreatedAt).TotalDays;
                if (daysAgo < cooldownDays)
                {
                    inCooldown = true;
                    lastSent = recentCampaign.CreatedAt;
                }
            }

            if (inCooldown && lastSent.HasValue)
            {
                int daysSince = (int)(DateTime.UtcNow - lastSent.Value).TotalDays;
                pnlCooldownAlert.BackColor = Color.FromArgb(254, 243, 199);
                lblCooldownAlert.BackColor = Color.FromArgb(254, 243, 199);
                lblCooldownAlert.ForeColor = Color.FromArgb(146, 64, 14);
                lblCooldownAlert.Text = $"Anti-Fatigue Alert: Emailed {daysSince}d ago ({cooldownDays}d cooldown required).";
                chkOverrideCooldown.Text = $"Override {cooldownDays}-day cooldown";
                chkOverrideCooldown.Visible = true;
                chkOverrideCooldown.Checked = false;
            }
            else
            {
                pnlCooldownAlert.BackColor = Color.FromArgb(209, 250, 229);
                lblCooldownAlert.BackColor = Color.FromArgb(209, 250, 229);
                lblCooldownAlert.ForeColor = Color.FromArgb(6, 95, 70);
                lblCooldownAlert.Text = "Eligible for outreach. Cooldown policy satisfied.";
                chkOverrideCooldown.Visible = false;
                chkOverrideCooldown.Checked = true;
            }

            int tplIndex = -1;
            for (int i = 0; i < _cachedTemplates.Count; i++)
            {
                if (_cachedTemplates[i].SegmentName.Equals(seg, StringComparison.OrdinalIgnoreCase))
                {
                    tplIndex = i;
                    break;
                }
            }
            if (tplIndex >= 0 && cmbManualTemplate != null && tplIndex < cmbManualTemplate.Items.Count)
            {
                if (cmbManualTemplate.SelectedIndex == tplIndex)
                    OnManualTemplateChanged();
                else
                    cmbManualTemplate.SelectedIndex = tplIndex;
            }

            UpdateManualPreview();
            UpdateManualSendButtonState();
        }

        private void OnManualTemplateChanged()
        {
            if (_isPopulating) return;
            if (cmbManualTemplate == null || cmbManualTemplate.SelectedIndex < 0 || cmbManualTemplate.SelectedIndex >= _cachedTemplates.Count)
                return;

            var tpl = _cachedTemplates[cmbManualTemplate.SelectedIndex];
            txtManualSubject.Text = tpl.Subject;
            txtManualBody.Text = tpl.Body;
            numManualDiscount.Value = Math.Max(numManualDiscount.Minimum, Math.Min(numManualDiscount.Maximum, tpl.DefaultDiscountPercent > 0 ? tpl.DefaultDiscountPercent : 10));
            numManualValidity.Value = Math.Max(numManualValidity.Minimum, Math.Min(numManualValidity.Maximum, tpl.ValidityDays > 0 ? tpl.ValidityDays : 14));

            UpdateManualPreview();
        }

        private void UpdateManualPreview()
        {
            string custName = cmbManualCustomer.SelectedIndex >= 0 && cmbManualCustomer.SelectedIndex < _cachedCustomers.Count
                ? _cachedCustomers[cmbManualCustomer.SelectedIndex].FullName
                : "Customer";

            string custEmail = !string.IsNullOrWhiteSpace(txtManualRecipientEmail?.Text)
                ? txtManualRecipientEmail.Text.Trim()
                : (cmbManualCustomer.SelectedIndex >= 0 && cmbManualCustomer.SelectedIndex < _cachedCustomers.Count
                    ? (_cachedCustomers[cmbManualCustomer.SelectedIndex].Email ?? "customer@example.com")
                    : "customer@example.com");

            string code = txtManualPromoCode.Text.Trim();
            string discount = $"{numManualDiscount.Value:0.#}";
            string validDays = numManualValidity.Value.ToString();
            string validDate = DateTime.Now.AddDays((double)numManualValidity.Value).ToString("MMMM d, yyyy");

            string rendered = (txtManualBody.Text ?? "")
                .Replace("{CustomerName}", custName)
                .Replace("{{customer_name}}", custName)
                .Replace("{DiscountPercent}", discount)
                .Replace("{{discount_percent}}", discount)
                .Replace("{PromoCode}", code)
                .Replace("{{promo_code}}", code)
                .Replace("{ValidUntil}", validDate)
                .Replace("{{validity_days}}", validDays)
                .Replace("{{expiration_date}}", validDate);

            if (lblPreviewTo != null)
                lblPreviewTo.Text = $"To: {custName} <{custEmail}>";
            if (lblPreviewSubject != null)
                lblPreviewSubject.Text = $"Subject: {txtManualSubject.Text}";
            if (lblPreviewDate != null)
                lblPreviewDate.Text = $"Date: {DateTime.Now:MMM d, yyyy  h:mm tt}";

            if (txtManualPreview != null)
                txtManualPreview.Text = StripOrFormatHtml(rendered);
        }

        private static string StripOrFormatHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            if (!html.Contains('<') && !html.Contains('>'))
                return html;

            string text = html;
            text = System.Text.RegularExpressions.Regex.Replace(text, @"</p\s*>", "\r\n\r\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<br\s*/?>", "\r\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<div\s*>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"</div\s*>", "\r\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<li\s*>", " • ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"</li\s*>", "\r\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", string.Empty);
            text = System.Net.WebUtility.HtmlDecode(text);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(\r?\n){3,}", "\r\n\r\n");

            return text.Trim();
        }

        private void UpdateManualSendButtonState()
        {
            bool requiresOverride = chkOverrideCooldown.Visible;
            btnManualSend.Enabled = !requiresOverride || chkOverrideCooldown.Checked;
        }

        private async Task SendManualRetentionEmailAsync()
        {
            if (cmbManualCustomer.SelectedIndex < 0 || cmbManualCustomer.SelectedIndex >= _cachedCustomers.Count)
            {
                MessageBox.Show("Please select a recipient customer.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var cust = _cachedCustomers[cmbManualCustomer.SelectedIndex];
            var targetEmail = txtManualRecipientEmail?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(targetEmail) || !targetEmail.Contains('@'))
            {
                MessageBox.Show("Please enter a valid recipient email address.", "Recipient Email Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtManualRecipientEmail?.Focus();
                return;
            }

            int segment = 0;
            if (cmbManualTemplate.SelectedIndex >= 0 && cmbManualTemplate.SelectedIndex < _cachedTemplates.Count)
                segment = _cachedTemplates[cmbManualTemplate.SelectedIndex].Segment;

            var req = new SendManualRetentionEmailRequestDto
            {
                CustomerId = cust.CustomerId,
                RecipientEmail = targetEmail,
                Segment = segment,
                Subject = txtManualSubject.Text.Trim(),
                Body = txtManualBody.Text,
                DiscountPercent = numManualDiscount.Value,
                PromoCode = txtManualPromoCode.Text.Trim(),
                ValidityDays = (int)numManualValidity.Value,
                OverrideCooldown = chkOverrideCooldown.Checked
            };

            btnManualSend.Enabled = false;
            btnManualSend.Text = "Sending via SMTP...";

            try
            {
                var result = await _api.SendManualRetentionEmailAsync(req);
                if (result.Success)
                {
                    if (!string.IsNullOrWhiteSpace(result.OutboxFilePath) && File.Exists(result.OutboxFilePath))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo { FileName = result.OutboxFilePath, UseShellExecute = true });
                        }
                        catch { }
                    }

                    string notice = result.WasFallback
                        ? $"Retention email delivered to Local Outbox for {targetEmail} and opened in your browser!\r\n\r\nPromo Code: {req.PromoCode}\r\nValidity: {req.ValidityDays} days\r\n\r\nNote: To deliver directly to your real Gmail inbox, enter your 16-character Google App Password in Settings > SMTP Configuration."
                        : $"Retention email successfully dispatched via SMTP to {targetEmail}!\r\n\r\nPromo code: {req.PromoCode}\r\nValidity: {req.ValidityDays} days";

                    MessageBox.Show(notice, "Email Dispatched Successfully", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    txtManualPromoCode.Text = $"FIXORY-RET-{Guid.NewGuid().ToString()[..6].ToUpper()}";
                    await ReloadAsync();
                }
                else if (result.InCooldown)
                {
                    MessageBox.Show(
                        $"Sending blocked by Anti-Fatigue cooldown:\r\n\r\n{result.Message}\r\n\r\nCheck the override box if you explicitly wish to bypass this rule.",
                        "Anti-Fatigue Cooldown Active",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show(
                        $"Could not send retention email:\r\n\r\n{result.Message}\r\n\r\nTroubleshooting:\r\n• Go to Settings tab > SMTP Email Delivery Configuration.\r\n• Click 'Test Connection' to verify your mail server credentials.\r\n• If using Gmail, ensure you are using a 16-character Google App Password.",
                        "SMTP Delivery Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"SMTP delivery error:\r\n\r\n{ex.Message}\r\n\r\nTroubleshooting:\r\n• Verify your mail server host, port, and credentials in the Settings tab.\r\n• For Gmail, 2-Step Verification must be ON and an App Password must be configured.",
                    "SMTP Delivery Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnManualSend.Enabled = true;
                btnManualSend.Text = "Send Retention Email via SMTP";
                UpdateManualSendButtonState();
            }
        }

        // ═══════════════════════════════════════════════════════
        // TAB 5: SETTINGS & TEMPLATES
        // ═══════════════════════════════════════════════════════

        private void BuildSettingsTab()
        {
            pnlTabSettings = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background, AutoScroll = true };

            if (!_isAdmin)
            {
                var pnlNotice = new Panel
                {
                    Location = new Point(0, 0),
                    Height = 46,
                    BackColor = Color.FromArgb(238, 242, 255),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };
                var lblNotice = new Label
                {
                    Text = "View-Only: Retention thresholds, SMTP configurations, and campaign templates are editable only by Administrators.",
                    Font = UiKit.T.SmallStrong,
                    ForeColor = Color.FromArgb(55, 48, 163),
                    Location = new Point(16, 14),
                    AutoSize = true,
                    BackColor = Color.FromArgb(238, 242, 255)
                };
                pnlNotice.Controls.Add(lblNotice);
                pnlTabSettings.Controls.Add(pnlNotice);
            }

            int topOffset = _isAdmin ? 0 : 56;

            // ── Card 1: Retention Rules ──
            cardSettingsRules = new SurfaceCard
            {
                Location = new Point(0, topOffset),
                Size = new Size(540, 400)
            };

            var lblRulesTitle = new Label
            {
                Text = "Retention Segmentation Rules",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                Location = new Point(UiKit.T.S5, UiKit.T.S5),
                AutoSize = true,
                BackColor = UiKit.T.Surface
            };
            cardSettingsRules.Controls.Add(lblRulesTitle);

            int ry = lblRulesTitle.Bottom + 20;

            void RuleRow(string label, ref int y, NumericUpDown num)
            {
                var lbl = new Label
                {
                    Text = label,
                    Font = UiKit.T.SmallStrong,
                    ForeColor = UiKit.T.Ink,
                    Location = new Point(UiKit.T.S5, y),
                    AutoSize = true,
                    BackColor = UiKit.T.Surface
                };
                cardSettingsRules.Controls.Add(lbl);
                y += 22;

                num.Location = new Point(UiKit.T.S5, y);
                num.Size = new Size(170, 32);
                num.Font = UiKit.T.Body;
                num.Enabled = _isAdmin;
                cardSettingsRules.Controls.Add(num);
                y += 44;
            }

            numInactiveDays = new NumericUpDown { Minimum = 30, Maximum = 730, Value = 180 };
            RuleRow("Inactive Threshold (days without repair)", ref ry, numInactiveDays);

            numAtRiskDays = new NumericUpDown { Minimum = 15, Maximum = 365, Value = 90 };
            RuleRow("At-Risk Threshold (days without repair)", ref ry, numAtRiskDays);

            numAntiFatigueDays = new NumericUpDown { Minimum = 1, Maximum = 90, Value = 14 };
            RuleRow("Anti-Fatigue Cooldown (minimum days between emails)", ref ry, numAntiFatigueDays);

            numOfferValidityDays = new NumericUpDown { Minimum = 1, Maximum = 90, Value = 14 };
            RuleRow("Default Offer Validity (days until promo expires)", ref ry, numOfferValidityDays);

            btnSaveRules = new Button
            {
                Text = "Save Retention Rules",
                Font = UiKit.T.BodyStrong,
                BackColor = AppTheme.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(UiKit.T.S5, ry),
                Size = new Size(200, 38),
                Enabled = _isAdmin,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnSaveRules.FlatAppearance.BorderSize = 0;
            btnSaveRules.Click += async (s, e) => await SaveRetentionRulesAsync();
            cardSettingsRules.Controls.Add(btnSaveRules);

            pnlTabSettings.Controls.Add(cardSettingsRules);

            // ── Card 2: SMTP Configuration ──
            cardSettingsSmtp = new SurfaceCard
            {
                Location = new Point(560, topOffset),
                Size = new Size(540, 440)
            };

            var lblSmtpTitle = new Label
            {
                Text = "SMTP Email Delivery Configuration",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                Location = new Point(UiKit.T.S5, UiKit.T.S5),
                AutoSize = true,
                BackColor = UiKit.T.Surface
            };
            cardSettingsSmtp.Controls.Add(lblSmtpTitle);

            int sy = lblSmtpTitle.Bottom + 20;

            void SmtpField(string label, int x, int y, Control input)
            {
                var lbl = new Label
                {
                    Text = label,
                    Font = UiKit.T.SmallStrong,
                    ForeColor = UiKit.T.Ink,
                    Location = new Point(x, y),
                    AutoSize = true,
                    BackColor = UiKit.T.Surface
                };
                cardSettingsSmtp.Controls.Add(lbl);
                input.Location = new Point(x, y + 22);
                input.Font = UiKit.T.Body;
                cardSettingsSmtp.Controls.Add(input);
            }

            txtSmtpHost = new TextBox { Size = new Size(310, 32), Text = "localhost", Enabled = _isAdmin };
            numSmtpPort = new NumericUpDown { Size = new Size(120, 32), Minimum = 1, Maximum = 65535, Value = 25, Enabled = _isAdmin };
            SmtpField("SMTP Server Host", UiKit.T.S5, sy, txtSmtpHost);
            SmtpField("Port", 340, sy, numSmtpPort);
            sy += 62;

            txtSmtpUsername = new TextBox { Size = new Size(240, 32), Enabled = _isAdmin, PlaceholderText = "user@gmail.com" };
            txtSmtpPassword = new TextBox { Size = new Size(240, 32), UseSystemPasswordChar = true, Enabled = _isAdmin, PlaceholderText = "16-char app password" };
            SmtpField("SMTP Username", UiKit.T.S5, sy, txtSmtpUsername);
            SmtpField("Password (App Password for Gmail)", 270, sy, txtSmtpPassword);
            sy += 62;

            var lnkGoogleAppPass = new LinkLabel
            {
                Text = "Get 16-char App Password ↗",
                Font = UiKit.T.Small,
                Location = new Point(360, sy - 8),
                AutoSize = true,
                LinkColor = AppTheme.Primary,
                BackColor = UiKit.T.Surface
            };
            lnkGoogleAppPass.LinkClicked += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo { FileName = "https://myaccount.google.com/apppasswords", UseShellExecute = true }); }
                catch { }
            };
            cardSettingsSmtp.Controls.Add(lnkGoogleAppPass);

            txtSmtpFromEmail = new TextBox { Size = new Size(240, 32), Text = "retention@fixorycrm.local", Enabled = _isAdmin };
            txtSmtpFromName = new TextBox { Size = new Size(240, 32), Text = "Fixory Computer Repair", Enabled = _isAdmin };
            SmtpField("From Email Address", UiKit.T.S5, sy, txtSmtpFromEmail);
            SmtpField("Sender Display Name", 270, sy, txtSmtpFromName);
            sy += 62;

            chkSmtpSsl = new CheckBox
            {
                Text = "Enable SSL / TLS",
                Location = new Point(UiKit.T.S5, sy + 6),
                AutoSize = true,
                Font = UiKit.T.Small,
                Enabled = _isAdmin,
                BackColor = UiKit.T.Surface
            };
            cardSettingsSmtp.Controls.Add(chkSmtpSsl);

            btnTestSmtp = new Button
            {
                Text = "Test Connection",
                Font = UiKit.T.BodyStrong,
                BackColor = Color.FromArgb(71, 85, 105),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(170, sy),
                Size = new Size(160, 38),
                Enabled = _isAdmin,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnTestSmtp.FlatAppearance.BorderSize = 0;
            btnTestSmtp.Click += async (s, e) => await TestSmtpConnectionAsync();
            cardSettingsSmtp.Controls.Add(btnTestSmtp);

            btnSaveSmtp = new Button
            {
                Text = "Save SMTP Settings",
                Font = UiKit.T.BodyStrong,
                BackColor = AppTheme.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(340, sy),
                Size = new Size(190, 38),
                Enabled = _isAdmin,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnSaveSmtp.FlatAppearance.BorderSize = 0;
            btnSaveSmtp.Click += async (s, e) => await SaveSmtpSettingsAsync();
            cardSettingsSmtp.Controls.Add(btnSaveSmtp);

            pnlTabSettings.Controls.Add(cardSettingsSmtp);

            // ── Card 3: Templates ──
            cardSettingsTemplates = new SurfaceCard
            {
                Location = new Point(0, cardSettingsRules.Bottom + 20),
                Size = new Size(1100, 400)
            };

            var lblTplHead = new Label
            {
                Text = "Segment Email Templates",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                Location = new Point(UiKit.T.S5, UiKit.T.S5),
                AutoSize = true,
                BackColor = UiKit.T.Surface
            };
            cardSettingsTemplates.Controls.Add(lblTplHead);

            int ty = lblTplHead.Bottom + 20;

            // Row 1: Segment + Name + Discount + Validity
            var lblSeg = new Label { Text = "Target Segment", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, ty), AutoSize = true, BackColor = UiKit.T.Surface };
            var lblTName = new Label { Text = "Template Name", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(230, ty), AutoSize = true, BackColor = UiKit.T.Surface };
            var lblTDisc = new Label { Text = "Default Discount %", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(640, ty), AutoSize = true, BackColor = UiKit.T.Surface };
            var lblTVal = new Label { Text = "Validity Days", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(800, ty), AutoSize = true, BackColor = UiKit.T.Surface };
            cardSettingsTemplates.Controls.Add(lblSeg);
            cardSettingsTemplates.Controls.Add(lblTName);
            cardSettingsTemplates.Controls.Add(lblTDisc);
            cardSettingsTemplates.Controls.Add(lblTVal);
            ty += 22;

            cmbTemplateSegment = new ComboBox
            {
                Location = new Point(UiKit.T.S5, ty),
                Size = new Size(200, 32),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DropDownWidth = 220,
                Font = UiKit.T.Body
            };
            foreach (var seg in new[] { "New", "Returning", "Loyal", "At-Risk", "Inactive" })
                cmbTemplateSegment.Items.Add(seg);
            cmbTemplateSegment.SelectedIndex = 2;
            cmbTemplateSegment.SelectedIndexChanged += (s, e) => OnTemplateSegmentSelectionChanged();
            cmbTemplateSegment.Leave += (s, e) => NormalizeComboSelection(cmbTemplateSegment);
            cmbTemplateSegment.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) NormalizeComboSelection(cmbTemplateSegment); };
            cardSettingsTemplates.Controls.Add(cmbTemplateSegment);

            txtTemplateName = new TextBox { Location = new Point(230, ty), Size = new Size(390, 32), Font = UiKit.T.Body, Enabled = _isAdmin };
            cardSettingsTemplates.Controls.Add(txtTemplateName);

            numTemplateDiscount = new NumericUpDown { Location = new Point(640, ty), Size = new Size(140, 32), Minimum = 0, Maximum = 100, Value = 15, Font = UiKit.T.Body, Enabled = _isAdmin };
            cardSettingsTemplates.Controls.Add(numTemplateDiscount);

            numTemplateValidity = new NumericUpDown { Location = new Point(800, ty), Size = new Size(140, 32), Minimum = 1, Maximum = 365, Value = 14, Font = UiKit.T.Body, Enabled = _isAdmin };
            cardSettingsTemplates.Controls.Add(numTemplateValidity);
            ty += 46;

            // Subject
            var lblTSubj = new Label { Text = "Subject Line", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, ty), AutoSize = true, BackColor = UiKit.T.Surface };
            cardSettingsTemplates.Controls.Add(lblTSubj);
            ty += 22;

            txtTemplateSubject = new TextBox { Location = new Point(UiKit.T.S5, ty), Size = new Size(920, 32), Font = UiKit.T.Body, Enabled = _isAdmin };
            cardSettingsTemplates.Controls.Add(txtTemplateSubject);
            ty += 46;

            // Body
            var lblTBody = new Label
            {
                Text = "Email Body (Tokens: {CustomerName}, {DiscountPercent}, {PromoCode}, {ValidUntil})",
                Font = UiKit.T.SmallStrong,
                ForeColor = UiKit.T.Ink,
                Location = new Point(UiKit.T.S5, ty),
                AutoSize = true,
                BackColor = UiKit.T.Surface
            };
            cardSettingsTemplates.Controls.Add(lblTBody);
            ty += 22;

            txtTemplateBody = new TextBox
            {
                Location = new Point(UiKit.T.S5, ty),
                Size = new Size(920, 120),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = UiKit.T.Body,
                Enabled = _isAdmin
            };
            cardSettingsTemplates.Controls.Add(txtTemplateBody);
            ty += 136;

            btnSaveTemplate = new Button
            {
                Text = "Save Template Changes",
                Font = UiKit.T.BodyStrong,
                BackColor = AppTheme.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(UiKit.T.S5, ty),
                Size = new Size(220, 38),
                Enabled = _isAdmin,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnSaveTemplate.FlatAppearance.BorderSize = 0;
            btnSaveTemplate.Click += async (s, e) => await SaveTemplateChangesAsync();
            cardSettingsTemplates.Controls.Add(btnSaveTemplate);

            pnlTabSettings.Controls.Add(cardSettingsTemplates);
            pnlTabHost.Controls.Add(pnlTabSettings);
        }

        private void OnTemplateSegmentSelectionChanged()
        {
            if (_isPopulating) return;
            if (cmbTemplateSegment == null || cmbTemplateSegment.SelectedIndex < 0) return;
            int segIdx = cmbTemplateSegment.SelectedIndex;

            var tpl = _cachedTemplates.FirstOrDefault(t => t.Segment == segIdx);
            if (tpl != null)
            {
                txtTemplateName.Text = tpl.TemplateName;
                txtTemplateSubject.Text = tpl.Subject;
                txtTemplateBody.Text = tpl.Body;
                numTemplateDiscount.Value = Math.Max(numTemplateDiscount.Minimum, Math.Min(numTemplateDiscount.Maximum, tpl.DefaultDiscountPercent));
                numTemplateValidity.Value = Math.Max(numTemplateValidity.Minimum, Math.Min(numTemplateValidity.Maximum, tpl.ValidityDays));
            }
        }

        // ── Save handlers (logic unchanged) ──

        private async Task SaveRetentionRulesAsync()
        {
            try
            {
                var req = new UpdateRetentionSettingsRequestDto
                {
                    InactiveThresholdDays = (int)numInactiveDays.Value,
                    AtRiskThresholdDays = (int)numAtRiskDays.Value,
                    AntiFatigueDays = (int)numAntiFatigueDays.Value,
                    DefaultOfferValidityDays = numOfferValidityDays != null ? (int)numOfferValidityDays.Value : 14,
                    SmtpHost = txtSmtpHost.Text.Trim(),
                    SmtpPort = (int)numSmtpPort.Value,
                    SmtpUsername = txtSmtpUsername.Text.Trim(),
                    SmtpPassword = txtSmtpPassword.Text,
                    SmtpFromEmail = txtSmtpFromEmail.Text.Trim(),
                    SmtpFromName = txtSmtpFromName.Text.Trim(),
                    SmtpEnableSsl = chkSmtpSsl.Checked
                };

                await _api.UpdateRetentionSettingsAsync(req);
                MessageBox.Show("Retention segmentation rules updated successfully.", "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings:\n\n{ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task SaveSmtpSettingsAsync()
        {
            try
            {
                var req = new UpdateRetentionSettingsRequestDto
                {
                    InactiveThresholdDays = (int)numInactiveDays.Value,
                    AtRiskThresholdDays = (int)numAtRiskDays.Value,
                    AntiFatigueDays = (int)numAntiFatigueDays.Value,
                    DefaultOfferValidityDays = numOfferValidityDays != null ? (int)numOfferValidityDays.Value : 14,
                    SmtpHost = txtSmtpHost.Text.Trim(),
                    SmtpPort = (int)numSmtpPort.Value,
                    SmtpUsername = txtSmtpUsername.Text.Trim(),
                    SmtpPassword = txtSmtpPassword.Text,
                    SmtpFromEmail = txtSmtpFromEmail.Text.Trim(),
                    SmtpFromName = txtSmtpFromName.Text.Trim(),
                    SmtpEnableSsl = chkSmtpSsl.Checked
                };

                await _api.UpdateRetentionSettingsAsync(req);
                MessageBox.Show("SMTP configuration saved successfully.", "SMTP Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save SMTP settings:\n\n{ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task TestSmtpConnectionAsync()
        {
            var host = txtSmtpHost.Text.Trim();
            var port = (int)numSmtpPort.Value;
            var user = txtSmtpUsername.Text.Trim();
            var pass = txtSmtpPassword.Text;
            var from = txtSmtpFromEmail.Text.Trim();
            var fromName = txtSmtpFromName.Text.Trim();
            var ssl = chkSmtpSsl.Checked;

            if (string.IsNullOrWhiteSpace(host))
            {
                MessageBox.Show("Please enter the SMTP host address.", "Configuration Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSmtpHost.Focus();
                return;
            }

            string testRecipient = !string.IsNullOrWhiteSpace(from) ? from : (!string.IsNullOrWhiteSpace(user) ? user : "");
            if (string.IsNullOrWhiteSpace(testRecipient) || !testRecipient.Contains('@'))
            {
                MessageBox.Show("Please enter a valid From Email Address to receive the test message.", "Recipient Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSmtpFromEmail.Focus();
                return;
            }

            btnTestSmtp.Enabled = false;
            btnTestSmtp.Text = "Testing...";

            try
            {
                var req = new TestSmtpSettingsRequestDto
                {
                    Host = host,
                    Port = port,
                    Username = user,
                    Password = pass,
                    FromEmail = from,
                    FromName = fromName,
                    EnableSsl = ssl,
                    TestRecipientEmail = testRecipient
                };

                var (success, message) = await _api.TestSmtpSettingsAsync(req);
                if (success)
                {
                    MessageBox.Show(
                        $"SMTP Connection Succeeded!\r\n\r\nA test email was successfully sent via {host}:{port} to {testRecipient}.\r\n\r\nYour SMTP mail server configuration is fully operational.",
                        "SMTP Test Passed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"SMTP Connection Failed:\r\n\r\n{message}\r\n\r\nTroubleshooting Tips:\r\n• If using Gmail (smtp.gmail.com), ensure 2-Step Verification is active and you are using a 16-character Google App Password.\r\n• Ensure Port 587 is configured and 'Enable SSL / TLS' is checked.\r\n• Ensure 'From Email Address' matches your Gmail address.",
                        "SMTP Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection test encountered an error:\n\n{ex.Message}", "SMTP Test Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTestSmtp.Enabled = true;
                btnTestSmtp.Text = "Test Connection";
            }
        }

        private async Task SaveTemplateChangesAsync()
        {
            if (cmbTemplateSegment.SelectedIndex < 0) return;
            int segIdx = cmbTemplateSegment.SelectedIndex;

            var tpl = _cachedTemplates.FirstOrDefault(t => t.Segment == segIdx);
            if (tpl == null) return;

            try
            {
                var req = new UpdateRetentionTemplateRequestDto
                {
                    TemplateName = txtTemplateName.Text.Trim(),
                    Subject = txtTemplateSubject.Text.Trim(),
                    Body = txtTemplateBody.Text,
                    DefaultDiscountPercent = numTemplateDiscount.Value,
                    ValidityDays = (int)numTemplateValidity.Value,
                    IsActive = true
                };

                await _api.UpdateRetentionTemplateAsync(tpl.RetentionEmailTemplateId, req);
                MessageBox.Show($"Template for {cmbTemplateSegment.Text} saved successfully.", "Template Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update template:\n\n{ex.Message}", "Template Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═══════════════════════════════════════════════════════
        // TAB NAVIGATION & LAYOUT
        // ═══════════════════════════════════════════════════════

        private void SwitchTab(string key)
        {
            _activeTabKey = key;
            foreach (var t in _navTabs)
                t.IsActive = (t.Category == key);

            if (pnlTabSegments != null) pnlTabSegments.Visible = (key == "segments");
            if (pnlTabApprovals != null) pnlTabApprovals.Visible = (key == "approvals");
            if (pnlTabCampaigns != null) pnlTabCampaigns.Visible = (key == "campaigns");
            if (pnlTabManual != null) pnlTabManual.Visible = (key == "manual");
            if (pnlTabSettings != null) pnlTabSettings.Visible = (key == "settings");

            if (key == "segments" && pnlTabSegments != null) pnlTabSegments.BringToFront();
            else if (key == "approvals" && pnlTabApprovals != null) pnlTabApprovals.BringToFront();
            else if (key == "campaigns" && pnlTabCampaigns != null) pnlTabCampaigns.BringToFront();
            else if (key == "manual" && pnlTabManual != null) pnlTabManual.BringToFront();
            else if (key == "settings" && pnlTabSettings != null) pnlTabSettings.BringToFront();

            LayoutUi();
        }

        private void LayoutUi()
        {
            if (Width <= 0 || Height <= 0 || !_hasAccess) return;

            lblTitle.Location = new Point(0, 0);
            int subtitleY = lblTitle.PreferredHeight + 6;
            lblSubtitle.Location = new Point(1, subtitleY);

            int rightX = Width;
            btnNewRequest.Size = new Size(btnNewRequest.PreferredWidth, UiKit.T.ButtonHeight);
            btnNewRequest.Location = new Point(rightX - btnNewRequest.Width, UiKit.T.S1);
            rightX -= btnNewRequest.Width + 8;

            btnRefresh.Size = new Size(btnRefresh.PreferredWidth, UiKit.T.ButtonHeight);
            btnRefresh.Location = new Point(rightX - btnRefresh.Width, UiKit.T.S1);

            int tabsY = subtitleY + lblSubtitle.PreferredHeight + UiKit.T.S4 + 8;

            int tabsX = 0;
            foreach (var tab in _navTabs)
            {
                int w = tab.GetPreferredWidth();
                tab.Location = new Point(tabsX, 0);
                tab.Size = new Size(w, 34);
                tabsX += w + 6;
            }
            pnlTabBar.Location = new Point(0, tabsY);
            pnlTabBar.Size = new Size(tabsX, 34);

            int contentTop = tabsY + 34 + UiKit.T.S4 + 4;
            int contentHeight = Math.Max(240, Height - contentTop);

            pnlTabHost.Location = new Point(0, contentTop);
            pnlTabHost.Size = new Size(Width, contentHeight);

            LayoutSegmentsTab();
            LayoutApprovalsTab();
            LayoutCampaignsTab();
            LayoutManualEmailTab();
            LayoutSettingsTab();
        }

        private void LayoutSettingsTab()
        {
            if (pnlTabSettings == null || cardSettingsRules == null || cardSettingsSmtp == null || cardSettingsTemplates == null)
                return;

            int availW = pnlTabHost.ClientSize.Width;
            int availH = pnlTabHost.ClientSize.Height;
            if (availW <= 0) return;

            int noticeH = _isAdmin ? 0 : 56;
            int gap = 16;

            // Two-column rule / SMTP, then full-width templates below
            int colW = Math.Max(440, (availW - gap) / 2);
            int colH = 440;

            cardSettingsRules.Location = new Point(0, noticeH);
            cardSettingsRules.Size = new Size(colW, colH);

            cardSettingsSmtp.Location = new Point(cardSettingsRules.Right + gap, noticeH);
            cardSettingsSmtp.Size = new Size(colW, colH);

            cardSettingsTemplates.Location = new Point(0, cardSettingsRules.Bottom + gap);
            cardSettingsTemplates.Size = new Size(availW, 420);
        }

        private void LayoutSegmentsTab()
        {
            if (cardSegments == null || pnlTabHost == null) return;

            int hostW = pnlTabHost.ClientSize.Width;
            int hostH = pnlTabHost.ClientSize.Height;
            if (hostW <= 0 || hostH <= 0) return;

            int gap = 12;
            int tileCols = hostW >= 1200 ? 5 : hostW >= 900 ? 3 : hostW >= 560 ? 2 : 1;
            int stripW = (hostW - (tileCols - 1) * gap) / tileCols;
            stripW = Math.Max(180, stripW);

            var tiles = new[] { tileTotal, tileLoyal, tileReturning, tileAtRisk, tileInactive };
            int tileH = tiles.Max(t => t.HeightFor(stripW));

            int y = 0, col = 0, rowMax = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (col >= tileCols) { y += rowMax + gap; col = 0; rowMax = 0; }
                tiles[i].Location = new Point(col * (stripW + gap), y);
                tiles[i].Size = new Size(stripW, tileH);
                rowMax = Math.Max(rowMax, tileH);
                col++;
            }
            int stripBottom = y + rowMax;

            int cardTop = stripBottom + 14;
            int cardH = Math.Max(220, hostH - cardTop - 10);
            cardSegments.Location = new Point(0, cardTop);
            cardSegments.Size = new Size(hostW, cardH);

            const int cp = UiKit.T.S5;
            lblSegmentsTitle.Location = new Point(cp, cp);
            lblSegmentsCount.Location = new Point(lblSegmentsTitle.Right + UiKit.T.S2, lblSegmentsTitle.Top + 4);

            int toolbarY = lblSegmentsTitle.Bottom + 14;

            int px = 0;
            foreach (var pill in _segmentPills)
            {
                int pw = pill.GetPreferredWidth();
                pill.Location = new Point(px, 0);
                pill.Size = new Size(pw, 34);
                px += pw + 6;
            }
            pnlSegmentPills.Location = new Point(cp, toolbarY);
            pnlSegmentPills.Size = new Size(Math.Min(px, cardSegments.Width - cp * 2), 34);

            int toolbarBottom = toolbarY + 34;
            int searchW = Math.Min(340, Math.Max(220, cardSegments.Width - cp * 3));
            searchSegments.Size = new Size(searchW, UiKit.T.InputHeight);
            searchSegments.Location = new Point(cardSegments.Width - cp - searchW, toolbarY + (34 - UiKit.T.InputHeight) / 2);

            int gridTop = toolbarBottom + 14;
            int gridW = cardSegments.Width - cp * 2;
            int gridH = cardSegments.Height - gridTop - cp;

            if (gridW > 100 && gridH > 60)
            {
                dgvSegments.Location = new Point(cp, gridTop);
                dgvSegments.Size = new Size(gridW, gridH);
                stateSegments.Location = new Point(cp, gridTop);
                stateSegments.Size = new Size(gridW, gridH);
            }
        }

        private void LayoutApprovalsTab()
        {
            if (cardApprovals == null || pnlTabHost == null) return;
            int hostW = pnlTabHost.ClientSize.Width;
            int hostH = pnlTabHost.ClientSize.Height;
            if (hostW <= 0 || hostH <= 0) return;

            cardApprovals.Location = new Point(0, 0);
            cardApprovals.Size = new Size(hostW, Math.Max(220, hostH - 10));

            const int cp = UiKit.T.S5;
            lblApprovalsTitle.Location = new Point(cp, cp);
            lblApprovalsCount.Location = new Point(lblApprovalsTitle.Right + UiKit.T.S2, lblApprovalsTitle.Top + 4);

            int toolbarY = lblApprovalsTitle.Bottom + 14;

            int px = 0;
            foreach (var pill in _approvalPills)
            {
                int pw = pill.GetPreferredWidth();
                pill.Location = new Point(px, 0);
                pill.Size = new Size(pw, 34);
                px += pw + 6;
            }
            pnlApprovalPills.Location = new Point(cp, toolbarY);
            pnlApprovalPills.Size = new Size(Math.Min(px, cardApprovals.Width - cp * 2), 34);

            int dx = pnlApprovalPills.Right + 16;
            dtpApprovalFrom.Size = new Size(130, UiKit.T.InputHeight);
            dtpApprovalFrom.Location = new Point(dx, toolbarY + (34 - UiKit.T.InputHeight) / 2);
            dtpApprovalTo.Size = new Size(130, UiKit.T.InputHeight);
            dtpApprovalTo.Location = new Point(dtpApprovalFrom.Right + 8, dtpApprovalFrom.Top);
            chkApprovalAllDates.Location = new Point(dtpApprovalTo.Right + 8, toolbarY + 8);

            int searchW = Math.Min(320, Math.Max(180, cardApprovals.Width - chkApprovalAllDates.Right - cp * 2));
            searchApprovals.Size = new Size(searchW, UiKit.T.InputHeight);
            searchApprovals.Location = new Point(cardApprovals.Width - cp - searchW, toolbarY + (34 - UiKit.T.InputHeight) / 2);

            int gridTop = toolbarY + 34 + 14;
            int gridW = cardApprovals.Width - cp * 2;
            int gridH = cardApprovals.Height - gridTop - cp;

            if (gridW > 100 && gridH > 60)
            {
                dgvApprovals.Location = new Point(cp, gridTop);
                dgvApprovals.Size = new Size(gridW, gridH);
                stateApprovals.Location = new Point(cp, gridTop);
                stateApprovals.Size = new Size(gridW, gridH);
            }
        }

        private void LayoutCampaignsTab()
        {
            if (cardCampaigns == null || pnlTabHost == null) return;
            int hostW = pnlTabHost.ClientSize.Width;
            int hostH = pnlTabHost.ClientSize.Height;
            if (hostW <= 0 || hostH <= 0) return;

            cardCampaigns.Location = new Point(0, 0);
            cardCampaigns.Size = new Size(hostW, Math.Max(220, hostH - 10));

            const int cp = UiKit.T.S5;
            lblCampaignsTitle.Location = new Point(cp, cp);
            lblCampaignsCount.Location = new Point(lblCampaignsTitle.Right + UiKit.T.S2, lblCampaignsTitle.Top + 4);

            int toolbarY = lblCampaignsTitle.Bottom + 14;

            int px = 0;
            foreach (var pill in _campaignPills)
            {
                int pw = pill.GetPreferredWidth();
                pill.Location = new Point(px, 0);
                pill.Size = new Size(pw, 34);
                px += pw + 6;
            }
            pnlCampaignPills.Location = new Point(cp, toolbarY);
            pnlCampaignPills.Size = new Size(Math.Min(px, cardCampaigns.Width - cp * 2), 34);

            int dx = pnlCampaignPills.Right + 16;
            dtpCampaignFrom.Size = new Size(130, UiKit.T.InputHeight);
            dtpCampaignFrom.Location = new Point(dx, toolbarY + (34 - UiKit.T.InputHeight) / 2);
            dtpCampaignTo.Size = new Size(130, UiKit.T.InputHeight);
            dtpCampaignTo.Location = new Point(dtpCampaignFrom.Right + 8, dtpCampaignFrom.Top);
            chkCampaignAllDates.Location = new Point(dtpCampaignTo.Right + 8, toolbarY + 8);

            int searchW = Math.Min(320, Math.Max(180, cardCampaigns.Width - chkCampaignAllDates.Right - cp * 2));
            searchCampaigns.Size = new Size(searchW, UiKit.T.InputHeight);
            searchCampaigns.Location = new Point(cardCampaigns.Width - cp - searchW, toolbarY + (34 - UiKit.T.InputHeight) / 2);

            int gridTop = toolbarY + 34 + 14;
            int gridW = cardCampaigns.Width - cp * 2;
            int gridH = cardCampaigns.Height - gridTop - cp;

            if (gridW > 100 && gridH > 60)
            {
                dgvCampaigns.Location = new Point(cp, gridTop);
                dgvCampaigns.Size = new Size(gridW, gridH);
                stateCampaigns.Location = new Point(cp, gridTop);
                stateCampaigns.Size = new Size(gridW, gridH);
            }
        }

        private void LayoutManualEmailTab()
        {
            if (cardManualCompose == null || cardManualPreview == null || pnlTabHost == null) return;

            int availW = pnlTabHost.ClientSize.Width;
            int availH = pnlTabHost.ClientSize.Height;
            if (availW <= 0 || availH <= 0) return;

            bool sideBySide = availW >= 940;
            int gap = 16;

            if (sideBySide)
            {
                int composeW = Math.Min(660, Math.Max(500, (int)((availW - gap) * 0.54)));
                int previewW = Math.Max(380, availW - composeW - gap);
                int cardH = Math.Max(700, availH - 8);

                cardManualCompose.Location = new Point(0, 0);
                cardManualCompose.Size = new Size(composeW, cardH);

                cardManualPreview.Location = new Point(cardManualCompose.Right + gap, 0);
                cardManualPreview.Size = new Size(previewW, cardH);
            }
            else
            {
                int cardW = Math.Max(460, availW - 8);
                cardManualCompose.Location = new Point(0, 0);
                cardManualCompose.Size = new Size(cardW, 700);

                cardManualPreview.Location = new Point(0, cardManualCompose.Bottom + gap);
                cardManualPreview.Size = new Size(cardW, 560);
            }

            LayoutComposeCard();
            LayoutPreviewCard();
        }

        private void LayoutComposeCard()
        {
            if (cardManualCompose == null) return;

            const int cp = 20;
            int cw = Math.Max(200, cardManualCompose.Width - cp * 2);

            lblManualHeading.Location = new Point(cp, 18);
            lblManualDesc.Location = new Point(cp, lblManualHeading.Bottom + 6);
            lblManualDesc.MaximumSize = new Size(cw, 0);

            int y = lblManualDesc.Bottom + 18;

            // Recipient customer / email (two-column)
            int colGap = 14;
            int halfW = Math.Max(140, (cw - colGap) / 2);

            lblCustTitle.Location = new Point(cp, y);
            lblManualRecipientTitle.Location = new Point(cp + halfW + colGap, y);
            y += 22;

            cmbManualCustomer.Location = new Point(cp, y);
            cmbManualCustomer.Size = new Size(halfW, 32);
            txtManualRecipientEmail.Location = new Point(cp + halfW + colGap, y);
            txtManualRecipientEmail.Size = new Size(halfW, 32);
            y += 40;

            pnlCustDetailsBox.Location = new Point(cp, y);
            pnlCustDetailsBox.Size = new Size(cw, 54);
            lblManualCustDetails.Location = new Point(12, 6);
            lblManualCustDetails.Size = new Size(pnlCustDetailsBox.Width - 24, 42);
            y += pnlCustDetailsBox.Height + 12;

            pnlCooldownAlert.Location = new Point(cp, y);
            pnlCooldownAlert.Size = new Size(cw, 48);
            int chkW = Math.Min(220, Math.Max(160, (int)(pnlCooldownAlert.Width * 0.36)));
            chkOverrideCooldown.Size = new Size(chkW, 26);
            chkOverrideCooldown.Location = new Point(pnlCooldownAlert.Width - chkW - 12, 11);
            lblCooldownAlert.Location = new Point(12, 6);
            lblCooldownAlert.Size = new Size(Math.Max(80, chkOverrideCooldown.Left - 20), 36);
            y += pnlCooldownAlert.Height + 14;

            // Template + discount + validity + code (four column)
            int discW = 80;
            int validW = 100;
            int codeW = Math.Max(120, (int)(cw * 0.28));
            int tplW = Math.Max(140, cw - discW - validW - codeW - 36);

            lblTplTitle.Location = new Point(cp, y);
            lblDiscTitle.Location = new Point(cp + tplW + 12, y);
            lblValidTitle.Location = new Point(lblDiscTitle.Left + discW + 12, y);
            lblCodeTitle.Location = new Point(lblValidTitle.Left + validW + 12, y);
            y += 22;

            cmbManualTemplate.Location = new Point(cp, y);
            cmbManualTemplate.Size = new Size(tplW, 32);
            numManualDiscount.Location = new Point(lblDiscTitle.Left, y);
            numManualDiscount.Size = new Size(discW, 32);
            numManualValidity.Location = new Point(lblValidTitle.Left, y);
            numManualValidity.Size = new Size(validW, 32);
            txtManualPromoCode.Location = new Point(lblCodeTitle.Left, y);
            txtManualPromoCode.Size = new Size(codeW, 32);
            y += 46;

            // Subject
            lblSubjTitle.Location = new Point(cp, y);
            y += 22;
            txtManualSubject.Location = new Point(cp, y);
            txtManualSubject.Size = new Size(cw, 32);
            y += 46;

            // Body
            lblBodyTitle.Location = new Point(cp, y);
            lblBodyTokens.Location = new Point(lblBodyTitle.Right + 12, y + 2);
            y += 22;

            int bottomReserve = 60;
            int bodyH = Math.Max(120, cardManualCompose.Height - y - bottomReserve - 16);
            txtManualBody.Location = new Point(cp, y);
            txtManualBody.Size = new Size(cw, bodyH);
            y += bodyH + 16;

            btnManualSend.Location = new Point(cp, y);
            btnManualSend.Size = new Size(Math.Min(cw, 300), 40);
        }

        private void LayoutPreviewCard()
        {
            if (cardManualPreview == null) return;

            const int pp = 20;
            int pw = Math.Max(200, cardManualPreview.Width - pp * 2);

            lblPreviewHeading.Location = new Point(pp, 18);
            lblPreviewDesc.Location = new Point(pp, lblPreviewHeading.Bottom + 6);
            lblPreviewDesc.MaximumSize = new Size(pw, 0);

            int y = lblPreviewDesc.Bottom + 18;

            pnlPreviewEnvelope.Location = new Point(pp, y);
            pnlPreviewEnvelope.Size = new Size(pw, 110);

            int envW = pnlPreviewEnvelope.Width - 24;
            lblPreviewFrom.Location = new Point(12, 10);
            lblPreviewFrom.Size = new Size(envW, 18);
            lblPreviewTo.Location = new Point(12, 32);
            lblPreviewTo.Size = new Size(envW, 18);
            lblPreviewSubject.Location = new Point(12, 56);
            lblPreviewSubject.Size = new Size(envW, 22);
            lblPreviewDate.Location = new Point(12, 82);
            lblPreviewDate.Size = new Size(envW, 18);

            y += pnlPreviewEnvelope.Height + 14;

            int bodyH = Math.Max(140, cardManualPreview.Height - y - pp);
            pnlPreviewSheet.Location = new Point(pp, y);
            pnlPreviewSheet.Size = new Size(pw, bodyH);

            txtManualPreview.Location = new Point(14, 14);
            txtManualPreview.Size = new Size(pnlPreviewSheet.Width - 28, Math.Max(60, pnlPreviewSheet.Height - 28));
        }

        // ═══════════════════════════════════════════════════════
        // DATA LOADING & FILTERING (logic unchanged)
        // ═══════════════════════════════════════════════════════

        public async Task ReloadAsync()
        {
            try
            {
                _cachedRecommendations = await _api.GetRetentionRecommendationsAsync();
                _cachedRequests = await _api.GetRetentionRequestsAsync();
                _cachedCampaigns = await _api.GetRetentionCampaignsAsync();
                _cachedCustomers = await _api.GetCustomersAsync();
                _cachedTemplates = await _api.GetRetentionTemplatesAsync();

                var set = await _api.GetRetentionSettingsAsync();
                if (set != null) _cachedSettings = set;

                UpdateMetricTiles();

                int pendingApprovals = _cachedRequests.Count(r => r.Status == 0);
                var approvalsTab = _navTabs.FirstOrDefault(t => t.Category == "approvals");
                if (approvalsTab != null)
                {
                    approvalsTab.Text = pendingApprovals > 0 ? $"Approvals ({pendingApprovals})" : "Approvals";
                    approvalsTab.Invalidate();
                }

                PopulateManualEmailControls();
                PopulateSettingsControls();

                ApplySegmentsFilter();
                ApplyApprovalsFilter();
                ApplyCampaignsFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load customer retention data:\n\n{ex.Message}", "Connection Problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateMetricTiles()
        {
            tileTotal.Number = _cachedRecommendations.Count.ToString();
            tileLoyal.Number = _cachedRecommendations.Count(r => r.SegmentName.Equals("Loyal", StringComparison.OrdinalIgnoreCase)).ToString();
            tileReturning.Number = _cachedRecommendations.Count(r => r.SegmentName.Equals("Returning", StringComparison.OrdinalIgnoreCase)).ToString();
            tileAtRisk.Number = _cachedRecommendations.Count(r => r.SegmentName.Equals("At Risk", StringComparison.OrdinalIgnoreCase) || r.SegmentName.Equals("AtRisk", StringComparison.OrdinalIgnoreCase)).ToString();
            tileInactive.Number = _cachedRecommendations.Count(r => r.SegmentName.Equals("Inactive", StringComparison.OrdinalIgnoreCase)).ToString();
        }

        private void PopulateManualEmailControls()
        {
            if (cmbManualCustomer == null || cmbManualTemplate == null) return;

            _isPopulating = true;
            try
            {
                string? prevTpl = cmbManualTemplate.SelectedItem?.ToString();
                cmbManualTemplate.Items.Clear();
                foreach (var t in _cachedTemplates)
                    cmbManualTemplate.Items.Add($"{t.SegmentName}: {t.TemplateName}");

                if (cmbManualTemplate.Items.Count > 0)
                {
                    if (prevTpl != null && cmbManualTemplate.Items.Contains(prevTpl))
                        cmbManualTemplate.SelectedItem = prevTpl;
                    else
                        cmbManualTemplate.SelectedIndex = 0;
                }

                string? prevCust = cmbManualCustomer.SelectedItem?.ToString();
                cmbManualCustomer.Items.Clear();
                foreach (var c in _cachedCustomers)
                    cmbManualCustomer.Items.Add($"{c.FullName} ({c.Email ?? c.Phone ?? "No contact"})");

                if (cmbManualCustomer.Items.Count > 0)
                {
                    if (prevCust != null && cmbManualCustomer.Items.Contains(prevCust))
                        cmbManualCustomer.SelectedItem = prevCust;
                    else
                        cmbManualCustomer.SelectedIndex = 0;
                }
            }
            finally
            {
                _isPopulating = false;
            }

            if (cmbManualCustomer.Items.Count > 0 && cmbManualCustomer.SelectedIndex >= 0)
                OnManualCustomerSelected();
            else if (cmbManualTemplate.Items.Count > 0 && cmbManualTemplate.SelectedIndex >= 0)
                OnManualTemplateChanged();
        }

        private void PopulateSettingsControls()
        {
            if (numInactiveDays == null) return;

            _isPopulating = true;
            try
            {
                numInactiveDays.Value = Math.Max(numInactiveDays.Minimum, Math.Min(numInactiveDays.Maximum, _cachedSettings.InactiveThresholdDays > 0 ? _cachedSettings.InactiveThresholdDays : 180));
                numAtRiskDays.Value = Math.Max(numAtRiskDays.Minimum, Math.Min(numAtRiskDays.Maximum, _cachedSettings.AtRiskThresholdDays > 0 ? _cachedSettings.AtRiskThresholdDays : 90));
                numAntiFatigueDays.Value = Math.Max(numAntiFatigueDays.Minimum, Math.Min(numAntiFatigueDays.Maximum, _cachedSettings.AntiFatigueDays > 0 ? _cachedSettings.AntiFatigueDays : 14));
                if (numOfferValidityDays != null)
                    numOfferValidityDays.Value = Math.Max(numOfferValidityDays.Minimum, Math.Min(numOfferValidityDays.Maximum, _cachedSettings.DefaultOfferValidityDays > 0 ? _cachedSettings.DefaultOfferValidityDays : 14));

                txtSmtpHost.Text = _cachedSettings.SmtpHost ?? "localhost";
                numSmtpPort.Value = Math.Max(numSmtpPort.Minimum, Math.Min(numSmtpPort.Maximum, _cachedSettings.SmtpPort > 0 ? _cachedSettings.SmtpPort : 25));
                txtSmtpUsername.Text = _cachedSettings.SmtpUsername ?? "";
                txtSmtpPassword.Text = _cachedSettings.SmtpPassword ?? "";
                txtSmtpFromEmail.Text = _cachedSettings.SmtpFromEmail ?? "retention@fixorycrm.local";
                txtSmtpFromName.Text = _cachedSettings.SmtpFromName ?? "Fixory Computer Repair";
                chkSmtpSsl.Checked = _cachedSettings.SmtpEnableSsl;
            }
            finally
            {
                _isPopulating = false;
            }

            OnTemplateSegmentSelectionChanged();
        }

        private void ApplySegmentsFilter()
        {
            var term = searchSegments.Inner.Text?.Trim() ?? "";
            IEnumerable<RetentionRecommendationDto> q = _cachedRecommendations;

            if (_selectedSegmentCategory != "All")
            {
                q = q.Where(x => x.SegmentName.Replace(" ", "").Equals(_selectedSegmentCategory.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)
                              || x.Category.Equals(_selectedSegmentCategory, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(x =>
                    (x.CustomerName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Phone ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Basis ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (x.Action ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            var view = q.ToList();
            dgvSegments.DataSource = null;
            dgvSegments.DataSource = view;

            lblSegmentsCount.Text = view.Count == _cachedRecommendations.Count
                ? $"{view.Count} {(view.Count == 1 ? "opportunity" : "opportunities")}"
                : $"{view.Count} of {_cachedRecommendations.Count}";

            if (view.Count > 0)
            {
                stateSegments.Visible = false;
                dgvSegments.Visible = true;
                dgvSegments.ClearSelection();
            }
            else
            {
                dgvSegments.Visible = false;
                if (_cachedRecommendations.Count == 0)
                    stateSegments.Show("\uE73E", "No Retention Actions Needed", "No customers meet current retention thresholds. Complete repairs to compute new opportunities.");
                else
                    stateSegments.Show("\uE721", "No Matches Found", $"No opportunities match \"{term}\".");
            }
        }

        private void ApplyApprovalsFilter()
        {
            var term = searchApprovals.Inner.Text?.Trim() ?? "";
            IEnumerable<RetentionRequestDto> q = _cachedRequests;

            if (_selectedApprovalStatus != "All")
                q = q.Where(r => r.StatusText.Equals(_selectedApprovalStatus, StringComparison.OrdinalIgnoreCase));

            if (!chkApprovalAllDates.Checked)
            {
                var from = dtpApprovalFrom.Value.Date;
                var to = dtpApprovalTo.Value.Date.AddDays(1).AddTicks(-1);
                q = q.Where(r => r.SubmittedAt >= from && r.SubmittedAt <= to);
            }

            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(r =>
                    (r.CustomerName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (r.ReasonCategory ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (r.RetentionDetails ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (r.SubmittedByName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            var view = q.OrderByDescending(r => r.SubmittedAt).ToList();
            dgvApprovals.DataSource = null;
            dgvApprovals.DataSource = view;

            lblApprovalsCount.Text = view.Count == _cachedRequests.Count
                ? $"{view.Count} {(view.Count == 1 ? "request" : "requests")}"
                : $"{view.Count} of {_cachedRequests.Count}";

            if (view.Count > 0)
            {
                stateApprovals.Visible = false;
                dgvApprovals.Visible = true;
                dgvApprovals.ClearSelection();
            }
            else
            {
                dgvApprovals.Visible = false;
                if (_cachedRequests.Count == 0)
                    stateApprovals.Show("\uE73E", "No Retention Requests", "No retention requests have been submitted yet. Managers can submit new requests using the button above.");
                else
                    stateApprovals.Show("\uE721", "No Requests Match", "No requests match the selected status or date filters.");
            }
        }

        private void ApplyCampaignsFilter()
        {
            var term = searchCampaigns.Inner.Text?.Trim() ?? "";
            IEnumerable<RetentionCampaignDto> q = _cachedCampaigns;

            if (_selectedCampaignStatus != "All")
                q = q.Where(c => c.DeliveryStatus.Equals(_selectedCampaignStatus, StringComparison.OrdinalIgnoreCase));

            if (!chkCampaignAllDates.Checked)
            {
                var from = dtpCampaignFrom.Value.Date;
                var to = dtpCampaignTo.Value.Date.AddDays(1).AddTicks(-1);
                q = q.Where(c => c.CreatedAt >= from && c.CreatedAt <= to);
            }

            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(c =>
                    (c.RecipientName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (c.RecipientEmail ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (c.Subject ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (c.PromoCode ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            var view = q.OrderByDescending(c => c.CreatedAt).ToList();
            dgvCampaigns.DataSource = null;
            dgvCampaigns.DataSource = view;

            lblCampaignsCount.Text = view.Count == _cachedCampaigns.Count
                ? $"{view.Count} {(view.Count == 1 ? "campaign" : "campaigns")}"
                : $"{view.Count} of {_cachedCampaigns.Count}";

            if (view.Count > 0)
            {
                stateCampaigns.Visible = false;
                dgvCampaigns.Visible = true;
                dgvCampaigns.ClearSelection();
            }
            else
            {
                dgvCampaigns.Visible = false;
                if (_cachedCampaigns.Count == 0)
                    stateCampaigns.Show("\uE715", "No Email Campaigns Yet", "Campaigns are generated upon Admin request approvals or sent directly from the Manual Email tab.");
                else
                    stateCampaigns.Show("\uE721", "No Campaigns Match", "No email campaigns match the search criteria.");
            }
        }

        // ═══════════════════════════════════════════════════════
        // MODALS (logic unchanged)
        // ═══════════════════════════════════════════════════════

        private void OpenNewRequestDialog(int? preselectedCustomerId = null)
        {
            CustomerDto? preselected = null;
            if (preselectedCustomerId.HasValue)
                preselected = _cachedCustomers.FirstOrDefault(c => c.CustomerId == preselectedCustomerId.Value);

            var dlg = new RetentionRequestFormDialog(_cachedCustomers, preselected);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
                _ = ReloadAsync();
        }

        private void OpenReviewDialog(RetentionRequestDto req)
        {
            var dlg = new RetentionReviewDialog(req);
            dlg.ShowModal(this.FindForm());
            if (dlg.StateChanged)
                _ = ReloadAsync();
        }

        // ═══════════════════════════════════════════════════════
        // GRID STYLING & SHARED PAINTERS
        // ═══════════════════════════════════════════════════════

        private static void StyleGrid(DataGridView g)
        {
            g.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            g.BackgroundColor = UiKit.T.Surface;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.None;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.GridColor = UiKit.T.LineSoft;

            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AllowUserToOrderColumns = false;
            g.ReadOnly = true;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            g.ScrollBars = ScrollBars.Both;
            g.RowTemplate.Height = 56;
            g.ColumnHeadersHeight = 44;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            g.ColumnHeadersDefaultCellStyle.BackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.ForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.Font = UiKit.T.SmallStrong;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(14, 0, 14, 0);
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

            g.DefaultCellStyle.BackColor = UiKit.T.Surface;
            g.DefaultCellStyle.ForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Font = UiKit.T.Body;
            g.DefaultCellStyle.SelectionBackColor = UiKit.T.RowHover;
            g.DefaultCellStyle.SelectionForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Padding = new Padding(14, 0, 14, 0);
            g.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            g.RowsDefaultCellStyle.BackColor = UiKit.T.Surface;
        }

        private static void PaintHeaderCell(DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;
            var cell = e.CellBounds;
            using (var b = new SolidBrush(UiKit.T.Surface))
                e.Graphics.FillRectangle(b, cell);
            using (var p = new Pen(UiKit.T.Line, 1))
                e.Graphics.DrawLine(p, cell.Left, cell.Bottom - 1, cell.Right, cell.Bottom - 1);

            UiKit.Quality(e.Graphics);
            UiKit.Text(e.Graphics, Convert.ToString(e.Value) ?? "", UiKit.T.SmallStrong, UiKit.T.InkMuted,
                new Rectangle(cell.Left + 14, cell.Top, Math.Max(0, cell.Width - 28), cell.Height - 1),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            e.Handled = true;
        }

        private static void PaintRowBackground(DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics == null) return;
            var cell = e.CellBounds;
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;

            Color rowBg = selected ? UiKit.T.RowHover : UiKit.T.Surface;

            using (var b = new SolidBrush(rowBg))
                e.Graphics.FillRectangle(b, cell);
            using (var p = new Pen(UiKit.T.LineSoft, 1))
                e.Graphics.DrawLine(p, cell.Left, cell.Bottom - 1, cell.Right, cell.Bottom - 1);

            UiKit.Quality(e.Graphics);
        }

        // ═══════════════════════════════════════════════════════
        // PILL BADGES
        // ═══════════════════════════════════════════════════════

        private static class PillBadgeRenderer
        {
            public static void PaintSegmentBadge(Graphics g, Rectangle bounds, string segment)
            {
                (Color bg, Color fg) = segment.ToLowerInvariant() switch
                {
                    "loyal" => (Color.FromArgb(238, 242, 255), Color.FromArgb(55, 48, 163)),
                    "returning" => (Color.FromArgb(236, 254, 255), Color.FromArgb(21, 94, 117)),
                    "new" => (Color.FromArgb(240, 253, 244), Color.FromArgb(22, 101, 52)),
                    "at risk" or "atrisk" => (Color.FromArgb(255, 247, 237), Color.FromArgb(154, 52, 18)),
                    "inactive" => (Color.FromArgb(243, 244, 246), Color.FromArgb(75, 85, 99)),
                    _ => (Color.FromArgb(243, 244, 246), Color.FromArgb(75, 85, 99))
                };
                DrawPill(g, bounds, segment, bg, fg);
            }

            public static void PaintApprovalStatusBadge(Graphics g, Rectangle bounds, string status)
            {
                (Color bg, Color fg) = status.ToLowerInvariant() switch
                {
                    "approved" => (Color.FromArgb(209, 250, 229), Color.FromArgb(6, 95, 70)),
                    "pending" => (Color.FromArgb(254, 243, 199), Color.FromArgb(146, 64, 14)),
                    "rejected" => (Color.FromArgb(254, 226, 226), Color.FromArgb(153, 27, 27)),
                    _ => (Color.FromArgb(243, 244, 246), Color.FromArgb(75, 85, 99))
                };
                DrawPill(g, bounds, status, bg, fg);
            }

            public static void PaintCampaignStatusBadge(Graphics g, Rectangle bounds, string status)
            {
                (Color bg, Color fg) = status.ToLowerInvariant() switch
                {
                    "sent" => (Color.FromArgb(209, 250, 229), Color.FromArgb(6, 95, 70)),
                    "simulated" => (Color.FromArgb(224, 242, 254), Color.FromArgb(7, 89, 133)),
                    "pending" => (Color.FromArgb(254, 243, 199), Color.FromArgb(146, 64, 14)),
                    "failed" => (Color.FromArgb(254, 226, 226), Color.FromArgb(153, 27, 27)),
                    _ => (Color.FromArgb(243, 244, 246), Color.FromArgb(75, 85, 99))
                };
                DrawPill(g, bounds, status, bg, fg);
            }

            private static void DrawPill(Graphics g, Rectangle bounds, string text, Color bg, Color fg)
            {
                if (string.IsNullOrEmpty(text)) text = "—";

                int pillH = 24;
                int textW = UiKit.Measure(text, UiKit.T.SmallStrong).Width;
                int pillW = Math.Min(bounds.Width - 20, textW + 22);
                pillW = Math.Max(60, pillW);
                int pillX = bounds.X + (bounds.Width - pillW) / 2;
                int pillY = bounds.Y + (bounds.Height - pillH) / 2;

                var rect = new Rectangle(pillX, pillY, pillW, pillH);
                UiKit.FillRounded(g, rect, pillH / 2, bg);
                UiKit.Text(g, text, UiKit.T.SmallStrong, fg, rect,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }

        // ═══════════════════════════════════════════════════════
        // NESTED CONTROLS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Retention KPI Card — with guaranteed padding, no top/bottom clipping.
        /// </summary>
        [DesignerCategory("Code")]
        private sealed class RetentionKpiCard : Control
        {
            private const int Pad = 20;

            private static readonly Font[] NumFonts =
            {
                AppFonts.Strong(22F),
                AppFonts.Strong(18F),
                AppFonts.Strong(15F),
                AppFonts.Strong(13F),
                AppFonts.Strong(11F)
            };

            private string _label = "";
            private string _number = "0";
            private string _sub = "";
            private Color _accent = AppTheme.Primary;
            private bool _hover;

            public event EventHandler? ContentChanged;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public string Number
            {
                get => _number;
                set
                {
                    if (_number == value) return;
                    _number = value ?? "0";
                    Invalidate();
                    ContentChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            public RetentionKpiCard()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
            }

            public void Set(string label, string number, string sub, Color accent)
            {
                _label = label;
                _number = number;
                _sub = sub;
                _accent = accent;
                Invalidate();
                ContentChanged?.Invoke(this, EventArgs.Empty);
            }

            public int HeightFor(int width) => Math.Max(128, Measure(width).Total);

            private (Font NumFont, int TopH, int NumH, int CapH, int Total) Measure(int width)
            {
                int inner = Math.Max(20, width - Pad * 2);
                int topH = Math.Max(18, UiKit.T.SmallStrong.Height + 4);

                Font numFont = NumFonts[^1];
                foreach (var f in NumFonts)
                {
                    var size = TextRenderer.MeasureText(_number, f, new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                    if (size.Width <= inner) { numFont = f; break; }
                }

                int numH = Math.Max(numFont.Height + 6, TextRenderer.MeasureText(_number, numFont, new Size(inner, int.MaxValue),
                    TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height + 6);

                int capH = string.IsNullOrEmpty(_sub) ? 0 : Math.Max(UiKit.T.Small.Height + 6,
                    TextRenderer.MeasureText(_sub, UiKit.T.Small, new Size(inner, int.MaxValue),
                        TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height + 6);

                int total = Pad + topH + 10 + numH + (capH > 0 ? 6 + capH : 0) + Pad;
                return (numFont, topH, numH, capH, total);
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var bg = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(bg, ClientRectangle);

                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                UiKit.Card(g, r, 10, UiKit.T.Surface, _hover ? _accent : UiKit.T.Line);

                var m = Measure(Width);
                int inner = Math.Max(20, Width - Pad * 2);

                // Accent dot + label
                UiKit.Dot(g, Pad + 3, Pad + m.TopH / 2f, 6, _accent);
                var lblRect = new Rectangle(Pad + 14, Pad, inner - 14, m.TopH);
                UiKit.Text(g, _label, UiKit.T.SmallStrong, UiKit.T.InkMuted, lblRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                // Number
                int numTop = Pad + m.TopH + 10;
                var numRect = new Rectangle(Pad, numTop, inner, m.NumH);
                UiKit.Text(g, _number, m.NumFont, UiKit.T.Ink, numRect,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                // Subtitle
                if (!string.IsNullOrEmpty(_sub) && m.CapH > 0)
                {
                    int capTop = numTop + m.NumH + 6;
                    var subRect = new Rectangle(Pad, capTop, inner, m.CapH);
                    UiKit.Text(g, _sub, UiKit.T.Small, UiKit.T.InkFaint, subRect,
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                }
            }
        }

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
        private sealed class TabButton : Control
        {
            public string Category { get; }
            private bool _active, _hover;

            public TabButton(string label, string category)
            {
                Text = label;
                Category = category;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = AppTheme.Background;
                Cursor = Cursors.Hand;
                Font = UiKit.T.SmallStrong;
            }

            public int GetPreferredWidth() => UiKit.Measure(Text, UiKit.T.SmallStrong).Width + 32;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            [Browsable(false)]
            public bool IsActive
            {
                get => _active;
                set { _active = value; Invalidate(); }
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var b = new SolidBrush(AppTheme.Background))
                    g.FillRectangle(b, ClientRectangle);

                if (_active)
                {
                    UiKit.FillRounded(g, ClientRectangle, 8, AppTheme.Primary);
                    UiKit.Text(g, Text, UiKit.T.SmallStrong, Color.White, ClientRectangle,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                        | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                }
                else
                {
                    if (_hover) UiKit.FillRounded(g, ClientRectangle, 8, UiKit.T.RowHover);
                    UiKit.Text(g, Text, UiKit.T.SmallStrong, UiKit.T.InkMuted, ClientRectangle,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                        | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                }
            }
        }

        [DesignerCategory("Code")]
        private sealed class SearchBox : Control
        {
            public TextBox Inner { get; }
            private bool _focused;

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
                BackColor = UiKit.T.Surface;

                Inner = new TextBox
                {
                    BorderStyle = BorderStyle.None,
                    Font = UiKit.T.Body,
                    ForeColor = UiKit.T.Ink,
                    BackColor = UiKit.T.Surface
                };
                Inner.GotFocus += (s, e) => { _focused = true; Invalidate(); };
                Inner.LostFocus += (s, e) => { _focused = false; Invalidate(); };

                Controls.Add(Inner);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                // Vertical centering, inner text never clipped
                Inner.Location = new Point(34, Math.Max(2, (Height - Inner.PreferredHeight) / 2));
                Inner.Width = Math.Max(20, Width - 46);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                using (var b = new SolidBrush(UiKit.T.Surface))
                    g.FillRectangle(b, ClientRectangle);

                UiKit.FillRounded(g, new Rectangle(0, 0, Width, Height), 8, UiKit.T.Surface);

                var border = _focused ? AppTheme.Primary : UiKit.T.Line;
                using (var path = UiKit.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 8))
                using (var pen = new Pen(border, _focused ? 1.4f : 1f))
                    g.DrawPath(pen, path);

                UiKit.Text(g, "\uE721", UiKit.T.Glyph, _focused ? AppTheme.Primary : UiKit.T.InkFaint,
                    new Rectangle(10, 0, 20, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
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

            public int PreferredWidth => UiKit.Measure(Text, UiKit.T.BodyStrong).Width + 60;

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode is Keys.Enter or Keys.Space)
                    InvokeOnClick(this, EventArgs.Empty);
                base.OnKeyDown(e);
            }

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
                    new Rectangle(14, 0, 18, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                UiKit.Text(g, Text, UiKit.T.BodyStrong, Color.White,
                    new Rectangle(36, 0, Math.Max(10, Width - 44), Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
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

                int cy = Math.Max(20, Height / 2 - 60);
                int cx = Width / 2;

                var circle = new Rectangle(cx - 28, cy, 56, 56);
                UiKit.FillRounded(g, circle, 28, UiKit.T.LineSoft);
                UiKit.Text(g, _glyph, UiKit.T.GlyphLarge, UiKit.T.InkFaint, circle,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                UiKit.Text(g, _title, UiKit.T.Section, UiKit.T.Ink,
                    new Rectangle(0, circle.Bottom + UiKit.T.S4, Width, 26),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);

                int msgW = Math.Min(440, Math.Max(200, Width - UiKit.T.S6 * 2));
                UiKit.Text(g, _message, UiKit.T.Body, UiKit.T.InkMuted,
                    new Rectangle((Width - msgW) / 2, circle.Bottom + UiKit.T.S4 + 32, msgW, 80),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
            }
        }
    }
}