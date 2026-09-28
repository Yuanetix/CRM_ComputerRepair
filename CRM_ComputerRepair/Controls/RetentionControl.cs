using CRM.winforms.Auth;
using CRM.winforms.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Customer Retention & Email Campaigns — Complete Workspace.
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

        // Current user permissions
        private readonly bool _isAdmin;
        private readonly bool _isManager;
        private readonly bool _hasAccess;
        private bool _isPopulating = false;

        // ═══════════ SHELL & HEADER CONTROLS ═══════════
        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private FlatButton btnRefresh = null!;
        private FlatButton btnNewRequest = null!;

        // Top Navigation Tabs
        private Panel pnlTabBar = null!;
        private readonly List<TabButton> _navTabs = new();
        private string _activeTabKey = "segments";

        // Tab Content Host
        private Panel pnlTabHost = null!;
        private Panel pnlTabSegments = null!;
        private Panel pnlTabApprovals = null!;
        private Panel pnlTabCampaigns = null!;
        private Panel pnlTabManual = null!;
        private Panel pnlTabSettings = null!;
        private Panel pnlAccessDenied = null!;

        // ═══════════ TAB 1: SEGMENTS & OPPORTUNITIES ═══════════
        private StatTile tileTotal = null!;
        private StatTile tileLoyal = null!;
        private StatTile tileReturning = null!;
        private StatTile tileAtRisk = null!;
        private StatTile tileInactive = null!;

        private SurfaceCard cardSegments = null!;
        private Label lblSegmentsTitle = null!;
        private Label lblSegmentsCount = null!;
        private SearchBox searchSegments = null!;
        private Panel pnlSegmentPills = null!;
        private readonly List<TabButton> _segmentPills = new();
        private string _selectedSegmentCategory = "All";
        private DataGridView dgvSegments = null!;
        private StateView stateSegments = null!;

        // ═══════════ TAB 2: RETENTION APPROVALS ═══════════
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

        // ═══════════ TAB 3: EMAIL CAMPAIGNS ═══════════
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
        private ComboBox cmbManualCustomer = null!;
        private Label lblManualCustDetails = null!;
        private Panel pnlCooldownAlert = null!;
        private Label lblCooldownAlert = null!;
        private CheckBox chkOverrideCooldown = null!;

        private ComboBox cmbManualTemplate = null!;
        private TextBox txtManualSubject = null!;
        private NumericUpDown numManualDiscount = null!;
        private NumericUpDown numManualValidity = null!;
        private TextBox txtManualPromoCode = null!;
        private TextBox txtManualBody = null!;
        private TextBox txtManualPreview = null!;
        private Button btnManualSend = null!;

        // ═══════════ TAB 5: SETTINGS & TEMPLATES ═══════════
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

            // ── Header Title & Actions ──
            lblTitle = new Label
            {
                Text = "Customer Retention & Email Campaigns",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Dynamic segment calculations, administrative approval governance, anti-fatigue cooldown, and live SMTP delivery.",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnRefresh = new FlatButton("Refresh", "\uE72C");
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            btnNewRequest = new FlatButton("+ New Retention Request", "\uE710");
            btnNewRequest.Click += (s, e) => OpenNewRequestDialog();

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnRefresh);
            Controls.Add(btnNewRequest);

            // ── Top Navigation Tabs ──
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

            // ── Tab Content Host ──
            pnlTabHost = new Panel
            {
                BackColor = AppTheme.Background,
                Dock = DockStyle.None
            };
            Controls.Add(pnlTabHost);

            // Build individual tab views
            BuildSegmentsTab();
            BuildApprovalsTab();
            BuildCampaignsTab();
            BuildManualEmailTab();
            BuildSettingsTab();

            // Initial view
            SwitchTab("segments");

            Resize += (s, e) => LayoutUi();
        }

        private void BuildAccessDeniedUi()
        {
            pnlAccessDenied = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.Background
            };

            var state = new StateView();
            state.Dock = DockStyle.Fill;
            state.Show("\uE72E", "Access Restricted",
                "Customer Retention & Email Campaigns are restricted to Manager and Administrator roles. Contact your system administrator if you require access.");
            pnlAccessDenied.Controls.Add(state);
            Controls.Add(pnlAccessDenied);
        }

        // ═══════════════════════════════════════════════════════
        // TAB 1: SEGMENTS & OPPORTUNITIES
        // ═══════════════════════════════════════════════════════

        private void BuildSegmentsTab()
        {
            pnlTabSegments = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Background, AutoScroll = true };

            // Metric Tiles Strip
            tileTotal = new StatTile { Label = "Active Opportunities", Number = "0", Icon = "\uE716" };
            tileTotal.SetColors(Color.White, AppTheme.Primary);

            tileLoyal = new StatTile { Label = "Loyal (≥3 repairs)", Number = "0", Icon = "\uE735" };
            tileLoyal.SetColors(Color.White, Color.FromArgb(79, 70, 229));

            tileReturning = new StatTile { Label = "Returning (2 repairs)", Number = "0", Icon = "\uE777" };
            tileReturning.SetColors(Color.White, Color.FromArgb(14, 116, 144));

            tileAtRisk = new StatTile { Label = "At Risk (91–180d)", Number = "0", Icon = "\uE7BA" };
            tileAtRisk.SetColors(Color.White, Color.FromArgb(194, 65, 12));

            tileInactive = new StatTile { Label = "Inactive (>180d)", Number = "0", Icon = "\uE74C" };
            tileInactive.SetColors(Color.White, Color.FromArgb(107, 114, 128));

            pnlTabSegments.Controls.Add(tileTotal);
            pnlTabSegments.Controls.Add(tileLoyal);
            pnlTabSegments.Controls.Add(tileReturning);
            pnlTabSegments.Controls.Add(tileAtRisk);
            pnlTabSegments.Controls.Add(tileInactive);

            // Opportunities Card
            cardSegments = new SurfaceCard();

            lblSegmentsTitle = new Label
            {
                Text = "Retention Recommendations",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSegmentsCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            // Category Filter Pills
            pnlSegmentPills = new Panel { BackColor = Color.Transparent };
            foreach (var cat in new[] { "All", "Loyal", "Returning", "New", "AtRisk", "Inactive" })
            {
                string display = cat switch
                {
                    "AtRisk" => "At-Risk",
                    _ => cat
                };
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

            searchSegments = new SearchBox { PlaceholderText = "Search customer name, contact or trigger basis..." };
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

            dgvSegments.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CustomerName",
                HeaderText = "Customer",
                Width = 160,
                ReadOnly = true,
                DefaultCellStyle = { Font = UiKit.T.BodyStrong }
            });

            dgvSegments.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "SegmentName",
                HeaderText = "Segment",
                Width = 110,
                ReadOnly = true
            });

            dgvSegments.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Action",
                HeaderText = "Recommended Action",
                Width = 180,
                ReadOnly = true
            });

            dgvSegments.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Basis",
                HeaderText = "Trigger Basis & History",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 220,
                ReadOnly = true,
                DefaultCellStyle = { ForeColor = UiKit.T.InkMuted }
            });

            dgvSegments.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TotalSpent",
                HeaderText = "Total Spend",
                Width = 110,
                ReadOnly = true,
                DefaultCellStyle = { Format = "₱#,##0.00", Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dgvSegments.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "DaysSinceLastTransaction",
                HeaderText = "Last Visit",
                Width = 100,
                ReadOnly = true,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            var btnCol = new DataGridViewButtonColumn
            {
                Name = "colAction",
                HeaderText = "Action",
                Text = "Create Request",
                UseColumnTextForButtonValue = true,
                Width = 120,
                FlatStyle = FlatStyle.Flat
            };
            btnCol.DefaultCellStyle.ForeColor = AppTheme.Primary;
            dgvSegments.Columns.Add(btnCol);
        }

        private void DgvSegments_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            // Segment badge column
            if (dgvSegments.Columns[e.ColumnIndex].DataPropertyName == "SegmentName")
            {
                e.PaintBackground(e.ClipBounds, true);
                string text = e.Value?.ToString() ?? "";
                PillBadgeRenderer.PaintSegmentBadge(e.Graphics, e.CellBounds, text);
                e.Handled = true;
            }
            // Last visit days formatting
            else if (dgvSegments.Columns[e.ColumnIndex].DataPropertyName == "DaysSinceLastTransaction")
            {
                e.PaintBackground(e.ClipBounds, true);
                if (e.Value is int days)
                {
                    string text = days == 0 ? "Today" : $"{days}d ago";
                    Color color = days > 90 ? Color.FromArgb(194, 65, 12) : UiKit.T.InkMuted;
                    UiKit.Text(e.Graphics, text, UiKit.T.Small, color, e.CellBounds, UiKit.Center);
                    e.Handled = true;
                }
            }
        }

        private void DgvSegments_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dgvSegments.Columns[e.ColumnIndex].Name == "colAction")
            {
                if (dgvSegments.Rows[e.RowIndex].DataBoundItem is RetentionRecommendationDto item)
                {
                    OpenNewRequestDialog(item.CustomerId);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // TAB 2: RETENTION APPROVALS
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
                BackColor = Color.Transparent
            };

            lblApprovalsCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            // Status Filter Pills
            pnlApprovalPills = new Panel { BackColor = Color.Transparent };
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

            // Date Pickers
            dtpApprovalFrom = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today.AddDays(-60),
                Width = 100,
                Font = UiKit.T.Body
            };
            dtpApprovalFrom.ValueChanged += (s, e) => ApplyApprovalsFilter();

            dtpApprovalTo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Width = 100,
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
                BackColor = Color.Transparent
            };
            chkApprovalAllDates.CheckedChanged += (s, e) =>
            {
                dtpApprovalFrom.Enabled = !chkApprovalAllDates.Checked;
                dtpApprovalTo.Enabled = !chkApprovalAllDates.Checked;
                ApplyApprovalsFilter();
            };
            dtpApprovalFrom.Enabled = false;
            dtpApprovalTo.Enabled = false;

            searchApprovals = new SearchBox { PlaceholderText = "Search customer, reason, submitter or remarks..." };
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

            dgvApprovals.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CustomerName",
                HeaderText = "Customer",
                Width = 160,
                ReadOnly = true,
                DefaultCellStyle = { Font = UiKit.T.BodyStrong }
            });

            dgvApprovals.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TargetSegmentName",
                HeaderText = "Segment",
                Width = 110,
                ReadOnly = true
            });

            dgvApprovals.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ProposedDiscountPercent",
                HeaderText = "Offer",
                Width = 90,
                ReadOnly = true,
                DefaultCellStyle = { Format = "0.#\\%", Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dgvApprovals.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ReasonCategory",
                HeaderText = "Reason & Strategic Context",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 200,
                ReadOnly = true,
                DefaultCellStyle = { ForeColor = UiKit.T.InkMuted }
            });

            dgvApprovals.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "StatusText",
                HeaderText = "Status",
                Width = 110,
                ReadOnly = true
            });

            dgvApprovals.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "SubmittedByName",
                HeaderText = "Submitted By",
                Width = 130,
                ReadOnly = true,
                DefaultCellStyle = { ForeColor = UiKit.T.InkMuted }
            });

            dgvApprovals.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "SubmittedAt",
                HeaderText = "Submitted Date",
                Width = 120,
                ReadOnly = true,
                DefaultCellStyle = { Format = "MMM d, yyyy", Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            var btnCol = new DataGridViewButtonColumn
            {
                Name = "colReview",
                HeaderText = "Action",
                Text = "Review / View",
                UseColumnTextForButtonValue = true,
                Width = 115,
                FlatStyle = FlatStyle.Flat
            };
            btnCol.DefaultCellStyle.ForeColor = AppTheme.Primary;
            dgvApprovals.Columns.Add(btnCol);
        }

        private void DgvApprovals_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            // Status Badge Column
            if (dgvApprovals.Columns[e.ColumnIndex].DataPropertyName == "StatusText")
            {
                e.PaintBackground(e.ClipBounds, true);
                string text = e.Value?.ToString() ?? "";
                PillBadgeRenderer.PaintApprovalStatusBadge(e.Graphics, e.CellBounds, text);
                e.Handled = true;
            }
            // Target segment badge column
            else if (dgvApprovals.Columns[e.ColumnIndex].DataPropertyName == "TargetSegmentName")
            {
                e.PaintBackground(e.ClipBounds, true);
                string text = e.Value?.ToString() ?? "";
                PillBadgeRenderer.PaintSegmentBadge(e.Graphics, e.CellBounds, text);
                e.Handled = true;
            }
        }

        private void DgvApprovals_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dgvApprovals.Columns[e.ColumnIndex].Name == "colReview")
            {
                if (dgvApprovals.Rows[e.RowIndex].DataBoundItem is RetentionRequestDto req)
                {
                    OpenReviewDialog(req);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // TAB 3: EMAIL CAMPAIGNS
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
                BackColor = Color.Transparent
            };

            lblCampaignsCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            pnlCampaignPills = new Panel { BackColor = Color.Transparent };
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
                Width = 100,
                Font = UiKit.T.Body
            };
            dtpCampaignFrom.ValueChanged += (s, e) => ApplyCampaignsFilter();

            dtpCampaignTo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Width = 100,
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
                BackColor = Color.Transparent
            };
            chkCampaignAllDates.CheckedChanged += (s, e) =>
            {
                dtpCampaignFrom.Enabled = !chkCampaignAllDates.Checked;
                dtpCampaignTo.Enabled = !chkCampaignAllDates.Checked;
                ApplyCampaignsFilter();
            };
            dtpCampaignFrom.Enabled = false;
            dtpCampaignTo.Enabled = false;

            searchCampaigns = new SearchBox { PlaceholderText = "Search recipient, subject, promo code..." };
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

            dgvCampaigns.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "RecipientName",
                HeaderText = "Recipient",
                Width = 150,
                ReadOnly = true,
                DefaultCellStyle = { Font = UiKit.T.BodyStrong }
            });

            dgvCampaigns.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "RecipientEmail",
                HeaderText = "Email Address",
                Width = 170,
                ReadOnly = true,
                DefaultCellStyle = { ForeColor = UiKit.T.InkMuted }
            });

            dgvCampaigns.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "SegmentName",
                HeaderText = "Segment",
                Width = 100,
                ReadOnly = true
            });

            dgvCampaigns.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "PromoCode",
                HeaderText = "Promo Code",
                Width = 140,
                ReadOnly = true,
                DefaultCellStyle = { Font = UiKit.T.SmallStrong }
            });

            dgvCampaigns.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Subject",
                HeaderText = "Subject",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 180,
                ReadOnly = true
            });

            dgvCampaigns.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "DeliveryStatus",
                HeaderText = "Delivery Status",
                Width = 120,
                ReadOnly = true
            });

            dgvCampaigns.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CreatedAt",
                HeaderText = "Created",
                Width = 110,
                ReadOnly = true,
                DefaultCellStyle = { Format = "MMM d, yyyy", Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            var btnCol = new DataGridViewButtonColumn
            {
                Name = "colDispatch",
                HeaderText = "Action",
                Text = "Dispatch Now",
                UseColumnTextForButtonValue = true,
                Width = 130,
                FlatStyle = FlatStyle.Flat
            };
            btnCol.DefaultCellStyle.ForeColor = AppTheme.Primary;
            dgvCampaigns.Columns.Add(btnCol);
        }

        private void DgvCampaigns_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            // Delivery Status Badge Column
            if (dgvCampaigns.Columns[e.ColumnIndex].DataPropertyName == "DeliveryStatus")
            {
                e.PaintBackground(e.ClipBounds, true);
                string text = e.Value?.ToString() ?? "";
                PillBadgeRenderer.PaintCampaignStatusBadge(e.Graphics, e.CellBounds, text);
                e.Handled = true;
            }
            // Segment Column
            else if (dgvCampaigns.Columns[e.ColumnIndex].DataPropertyName == "SegmentName")
            {
                e.PaintBackground(e.ClipBounds, true);
                string text = e.Value?.ToString() ?? "";
                PillBadgeRenderer.PaintSegmentBadge(e.Graphics, e.CellBounds, text);
                e.Handled = true;
            }
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
                        await _api.DispatchRetentionEmailAsync(c.RetentionEmailLogId);
                        MessageBox.Show(
                            $"Campaign dispatched to {c.RecipientEmail} via SMTP.",
                            "Dispatch Complete",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        await ReloadAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to dispatch email:\n\n{ex.Message}", "Dispatch Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ShowCampaignEmailPreview(RetentionCampaignDto c)
        {
            var dlg = new Form
            {
                Text = $"Email Campaign Preview — {c.Subject}",
                Size = new Size(640, 540),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable,
                ShowInTaskbar = false,
                BackColor = Color.White
            };

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = AppTheme.Neutral, Padding = new Padding(16) };
            var lblInfo = new Label
            {
                Dock = DockStyle.Fill,
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextPrimary,
                Text = $"To: {c.RecipientName} <{c.RecipientEmail}>\n" +
                       $"Segment: {c.SegmentName}   ·   Offer: {c.DiscountPercent:0.#}% OFF   ·   Promo Code: {c.PromoCode ?? "—"}\n" +
                       $"Delivery: {c.DeliveryStatus}   ·   Created: {c.CreatedAt:MMM d, yyyy h:mm tt}"
            };
            pnlTop.Controls.Add(lblInfo);

            var txtContent = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White,
                Font = new Font("Consolas", 9.5F),
                Text = c.FormattedBody
            };

            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.White, Padding = new Padding(10) };
            var btnClose = new Button
            {
                Text = "Close",
                Dock = DockStyle.Right,
                Width = 90,
                DialogResult = DialogResult.OK
            };
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

            var lblHeading = new Label
            {
                Text = "Direct Retention Email Outreach",
                Font = UiKit.T.Section,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                Location = new Point(UiKit.T.S5, UiKit.T.S5)
            };
            cardManualCompose.Controls.Add(lblHeading);

            var lblDesc = new Label
            {
                Text = "Select a customer to evaluate dynamic segment metrics and anti-fatigue cooldown eligibility.",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                Location = new Point(UiKit.T.S5, lblHeading.Bottom + 4)
            };
            cardManualCompose.Controls.Add(lblDesc);

            int y = lblDesc.Bottom + 24;

            // ── Customer Selector ──
            var lblCust = new Label { Text = "Customer *", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, y), AutoSize = true };
            cardManualCompose.Controls.Add(lblCust);
            y += 22;

            cmbManualCustomer = new ComboBox
            {
                Location = new Point(UiKit.T.S5, y),
                Size = new Size(420, 32),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UiKit.T.Body
            };
            cmbManualCustomer.SelectedIndexChanged += (s, e) => OnManualCustomerSelected();
            cardManualCompose.Controls.Add(cmbManualCustomer);

            lblManualCustDetails = new Label
            {
                Location = new Point(cmbManualCustomer.Right + 20, y - 4),
                Size = new Size(500, 40),
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                Text = "Select a customer to view history & segment."
            };
            cardManualCompose.Controls.Add(lblManualCustDetails);
            y += 52;

            // ── Anti-fatigue Alert Box ──
            pnlCooldownAlert = new Panel
            {
                Location = new Point(UiKit.T.S5, y),
                Size = new Size(880, 56),
                BackColor = Color.FromArgb(254, 243, 199)
            };

            lblCooldownAlert = new Label
            {
                Text = "Anti-Fatigue Cooldown Status",
                Font = UiKit.T.BodyStrong,
                ForeColor = Color.FromArgb(146, 64, 14),
                Location = new Point(14, 8),
                Size = new Size(580, 40)
            };
            pnlCooldownAlert.Controls.Add(lblCooldownAlert);

            chkOverrideCooldown = new CheckBox
            {
                Text = "Override 14-day anti-fatigue rule",
                Font = UiKit.T.SmallStrong,
                ForeColor = Color.FromArgb(146, 64, 14),
                Location = new Point(600, 14),
                Size = new Size(260, 24),
                Visible = false
            };
            chkOverrideCooldown.CheckedChanged += (s, e) =>
            {
                UpdateManualSendButtonState();
            };
            pnlCooldownAlert.Controls.Add(chkOverrideCooldown);
            cardManualCompose.Controls.Add(pnlCooldownAlert);
            y += 68;

            // ── Template selection ──
            var lblTpl = new Label { Text = "Campaign Template", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, y), AutoSize = true };
            cardManualCompose.Controls.Add(lblTpl);

            var lblDisc = new Label { Text = "Discount %", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(360, y), AutoSize = true };
            cardManualCompose.Controls.Add(lblDisc);

            var lblValid = new Label { Text = "Validity (Days)", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(480, y), AutoSize = true };
            cardManualCompose.Controls.Add(lblValid);

            var lblCode = new Label { Text = "Promo Code", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(610, y), AutoSize = true };
            cardManualCompose.Controls.Add(lblCode);
            y += 22;

            cmbManualTemplate = new ComboBox
            {
                Location = new Point(UiKit.T.S5, y),
                Size = new Size(330, 32),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UiKit.T.Body
            };
            cmbManualTemplate.SelectedIndexChanged += (s, e) => OnManualTemplateChanged();
            cardManualCompose.Controls.Add(cmbManualTemplate);

            numManualDiscount = new NumericUpDown
            {
                Location = new Point(360, y),
                Size = new Size(100, 30),
                Minimum = 0,
                Maximum = 100,
                Value = 10,
                DecimalPlaces = 0,
                Font = UiKit.T.Body
            };
            numManualDiscount.ValueChanged += (s, e) => UpdateManualPreview();
            cardManualCompose.Controls.Add(numManualDiscount);

            numManualValidity = new NumericUpDown
            {
                Location = new Point(480, y),
                Size = new Size(110, 30),
                Minimum = 1,
                Maximum = 365,
                Value = 14,
                Font = UiKit.T.Body
            };
            numManualValidity.ValueChanged += (s, e) => UpdateManualPreview();
            cardManualCompose.Controls.Add(numManualValidity);

            txtManualPromoCode = new TextBox
            {
                Location = new Point(610, y),
                Size = new Size(270, 30),
                Font = UiKit.T.Body,
                Text = $"FIXORY-RET-{Guid.NewGuid().ToString()[..6].ToUpper()}"
            };
            txtManualPromoCode.TextChanged += (s, e) => UpdateManualPreview();
            cardManualCompose.Controls.Add(txtManualPromoCode);
            y += 48;

            // ── Subject Line ──
            var lblSubj = new Label { Text = "Email Subject *", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, y), AutoSize = true };
            cardManualCompose.Controls.Add(lblSubj);
            y += 22;

            txtManualSubject = new TextBox
            {
                Location = new Point(UiKit.T.S5, y),
                Size = new Size(880, 30),
                Font = UiKit.T.Body,
                Text = "Special Care & Maintenance Offer from Fixory Computer Repair"
            };
            txtManualSubject.TextChanged += (s, e) => UpdateManualPreview();
            cardManualCompose.Controls.Add(txtManualSubject);
            y += 46;

            // ── Body & Live Preview (Side by Side) ──
            var lblBody = new Label { Text = "Email Body Template (Tokens: {CustomerName}, {DiscountPercent}, {PromoCode}, {ValidUntil})", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, y), AutoSize = true };
            cardManualCompose.Controls.Add(lblBody);

            var lblPrev = new Label { Text = "Live Rendered Email Preview (via SMTP)", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(460, y), AutoSize = true };
            cardManualCompose.Controls.Add(lblPrev);
            y += 22;

            txtManualBody = new TextBox
            {
                Location = new Point(UiKit.T.S5, y),
                Size = new Size(430, 220),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = UiKit.T.Body,
                Text = "Dear {CustomerName},\r\n\r\nThank you for choosing Fixory for your computer repairs. As a valued client, we're pleased to offer you {DiscountPercent}% off your next hardware tune-up or diagnostic service.\r\n\r\nUse Promo Code: {PromoCode}\r\nValid until: {ValidUntil}\r\n\r\nBest regards,\r\nFixory Repair Team"
            };
            txtManualBody.TextChanged += (s, e) => UpdateManualPreview();
            cardManualCompose.Controls.Add(txtManualBody);

            txtManualPreview = new TextBox
            {
                Location = new Point(460, y),
                Size = new Size(425, 220),
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(249, 250, 251),
                ScrollBars = ScrollBars.Vertical,
                Font = UiKit.T.Body
            };
            cardManualCompose.Controls.Add(txtManualPreview);
            y += 244;

            // ── Send Button ──
            btnManualSend = new Button
            {
                Text = "Send Retention Email via SMTP",
                Font = UiKit.T.BodyStrong,
                BackColor = AppTheme.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(UiKit.T.S5, y),
                Size = new Size(260, 40),
                Cursor = Cursors.Hand
            };
            btnManualSend.FlatAppearance.BorderSize = 0;
            btnManualSend.Click += async (s, e) => await SendManualRetentionEmailAsync();
            cardManualCompose.Controls.Add(btnManualSend);

            pnlTabManual.Controls.Add(cardManualCompose);
            pnlTabHost.Controls.Add(pnlTabManual);
        }

        private void OnManualCustomerSelected()
        {
            if (_isPopulating) return;
            if (cmbManualCustomer == null || cmbManualCustomer.SelectedIndex < 0 || cmbManualCustomer.SelectedIndex >= _cachedCustomers.Count)
                return;

            var cust = _cachedCustomers[cmbManualCustomer.SelectedIndex];

            // Look up recommendation/metrics for customer
            var rec = _cachedRecommendations.FirstOrDefault(r => r.CustomerId == cust.CustomerId);

            string seg = rec?.SegmentName ?? "New";
            int visits = rec?.TransactionCount ?? 0;
            decimal spent = rec?.TotalSpent ?? 0;
            int lastDays = rec?.DaysSinceLastTransaction ?? 0;

            lblManualCustDetails.Text = $"Segment: {seg}  ·  Repairs: {visits}  ·  Total Spent: ₱{spent:N2}  ·  Last Repair: {lastDays}d ago\n" +
                                        $"Contact: {cust.Email ?? "No email"} | {cust.Phone ?? "No phone"}";

            // Check Cooldown in campaigns
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
                lblCooldownAlert.ForeColor = Color.FromArgb(146, 64, 14);
                lblCooldownAlert.Text = $"Anti-Fatigue Warning: This customer received an email {daysSince} day(s) ago. Policy requires a {cooldownDays}-day gap to prevent spam.";
                chkOverrideCooldown.Visible = true;
                chkOverrideCooldown.Checked = false;
            }
            else
            {
                pnlCooldownAlert.BackColor = Color.FromArgb(209, 250, 229);
                lblCooldownAlert.ForeColor = Color.FromArgb(6, 95, 70);
                lblCooldownAlert.Text = "Eligible for outreach. No emails sent within the anti-fatigue cooldown window.";
                chkOverrideCooldown.Visible = false;
                chkOverrideCooldown.Checked = true;
            }

            // Auto-select template matching segment
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
            numManualDiscount.Value = tpl.DefaultDiscountPercent > 0 ? tpl.DefaultDiscountPercent : 10;
            numManualValidity.Value = tpl.ValidityDays > 0 ? tpl.ValidityDays : 14;

            UpdateManualPreview();
        }

        private void UpdateManualPreview()
        {
            string custName = cmbManualCustomer.SelectedIndex >= 0 && cmbManualCustomer.SelectedIndex < _cachedCustomers.Count
                ? _cachedCustomers[cmbManualCustomer.SelectedIndex].FullName
                : "Customer";

            string code = txtManualPromoCode.Text.Trim();
            string discount = $"{numManualDiscount.Value:0.#}";
            string validDate = DateTime.Now.AddDays((double)numManualValidity.Value).ToString("MMMM d, yyyy");

            string rendered = (txtManualBody.Text ?? "")
                .Replace("{CustomerName}", custName)
                .Replace("{DiscountPercent}", discount)
                .Replace("{PromoCode}", code)
                .Replace("{ValidUntil}", validDate);

            txtManualPreview.Text = $"SUBJECT: {txtManualSubject.Text}\r\n\r\n" + rendered;
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
            if (string.IsNullOrWhiteSpace(cust.Email))
            {
                MessageBox.Show("Selected customer does not have a registered email address.", "No Email Address", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int segment = 0;
            if (cmbManualTemplate.SelectedIndex >= 0 && cmbManualTemplate.SelectedIndex < _cachedTemplates.Count)
                segment = _cachedTemplates[cmbManualTemplate.SelectedIndex].Segment;

            var req = new SendManualRetentionEmailRequestDto
            {
                CustomerId = cust.CustomerId,
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
                    MessageBox.Show(
                        $"Retention email successfully dispatched via SMTP to {cust.Email}!\nPromo code: {req.PromoCode}",
                        "Email Sent",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    // Reset code and refresh
                    txtManualPromoCode.Text = $"FIXORY-RET-{Guid.NewGuid().ToString()[..6].ToUpper()}";
                    await ReloadAsync();
                }
                else if (result.InCooldown)
                {
                    MessageBox.Show(
                        $"Sending blocked by Anti-Fatigue cooldown:\n\n{result.Message}\n\nCheck the override box if you explicitly wish to bypass this rule.",
                        "Anti-Fatigue Cooldown Active",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show($"Could not send email: {result.Message}", "Delivery Problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delivery error:\n\n{ex.Message}", "SMTP Delivery Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            // Notice banner if manager
            if (!_isAdmin)
            {
                var pnlNotice = new Panel
                {
                    Location = new Point(0, 0),
                    Size = new Size(1100, 44),
                    BackColor = Color.FromArgb(238, 242, 255)
                };
                var lblNotice = new Label
                {
                    Text = "ℹ View-Only: Retention thresholds, SMTP configurations, and campaign templates are editable only by Administrators.",
                    Font = UiKit.T.SmallStrong,
                    ForeColor = Color.FromArgb(55, 48, 163),
                    Location = new Point(16, 12),
                    AutoSize = true
                };
                pnlNotice.Controls.Add(lblNotice);
                pnlTabSettings.Controls.Add(pnlNotice);
            }

            int topOffset = _isAdmin ? 0 : 50;

            // ── Card 1: Retention Rules & Thresholds ──
            cardSettingsRules = new SurfaceCard
            {
                Location = new Point(0, topOffset),
                Size = new Size(540, 380)
            };

            var lblRulesTitle = new Label { Text = "Retention Segmentation Rules", Font = UiKit.T.Section, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, UiKit.T.S5), AutoSize = true };
            cardSettingsRules.Controls.Add(lblRulesTitle);

            int ry = lblRulesTitle.Bottom + 16;

            var lblInact = new Label { Text = "Inactive Threshold (days without repair)", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, ry), AutoSize = true };
            cardSettingsRules.Controls.Add(lblInact);
            ry += 22;
            numInactiveDays = new NumericUpDown { Location = new Point(UiKit.T.S5, ry), Size = new Size(160, 30), Minimum = 30, Maximum = 730, Value = 180, Font = UiKit.T.Body, Enabled = _isAdmin };
            cardSettingsRules.Controls.Add(numInactiveDays);
            ry += 42;

            var lblAtRisk = new Label { Text = "At-Risk Threshold (days without repair)", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, ry), AutoSize = true };
            cardSettingsRules.Controls.Add(lblAtRisk);
            ry += 22;
            numAtRiskDays = new NumericUpDown { Location = new Point(UiKit.T.S5, ry), Size = new Size(160, 30), Minimum = 15, Maximum = 365, Value = 90, Font = UiKit.T.Body, Enabled = _isAdmin };
            cardSettingsRules.Controls.Add(numAtRiskDays);
            ry += 42;

            var lblCooldown = new Label { Text = "Anti-Fatigue Cooldown (minimum days between emails)", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, ry), AutoSize = true };
            cardSettingsRules.Controls.Add(lblCooldown);
            ry += 22;
            numAntiFatigueDays = new NumericUpDown { Location = new Point(UiKit.T.S5, ry), Size = new Size(160, 30), Minimum = 1, Maximum = 90, Value = 14, Font = UiKit.T.Body, Enabled = _isAdmin };
            cardSettingsRules.Controls.Add(numAntiFatigueDays);
            ry += 42;

            var lblValidity = new Label { Text = "Default Offer Validity (days until promo expires)", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, ry), AutoSize = true };
            cardSettingsRules.Controls.Add(lblValidity);
            ry += 22;
            numOfferValidityDays = new NumericUpDown { Location = new Point(UiKit.T.S5, ry), Size = new Size(160, 30), Minimum = 1, Maximum = 90, Value = 14, Font = UiKit.T.Body, Enabled = _isAdmin };
            cardSettingsRules.Controls.Add(numOfferValidityDays);
            ry += 42;

            btnSaveRules = new Button
            {
                Text = "Save Retention Rules",
                Font = UiKit.T.BodyStrong,
                BackColor = AppTheme.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(UiKit.T.S5, ry),
                Size = new Size(180, 36),
                Enabled = _isAdmin,
                Cursor = Cursors.Hand
            };
            btnSaveRules.FlatAppearance.BorderSize = 0;
            btnSaveRules.Click += async (s, e) => await SaveRetentionRulesAsync();
            cardSettingsRules.Controls.Add(btnSaveRules);

            pnlTabSettings.Controls.Add(cardSettingsRules);

            // ── Card 2: SMTP Configuration ──
            cardSettingsSmtp = new SurfaceCard
            {
                Location = new Point(560, topOffset),
                Size = new Size(540, 380)
            };

            var lblSmtpTitle = new Label { Text = "SMTP Email Delivery Configuration", Font = UiKit.T.Section, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, UiKit.T.S5), AutoSize = true };
            cardSettingsSmtp.Controls.Add(lblSmtpTitle);

            int sy = lblSmtpTitle.Bottom + 16;

            var lblHost = new Label { Text = "SMTP Server Host", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, sy), AutoSize = true };
            var lblPort = new Label { Text = "Port", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(340, sy), AutoSize = true };
            cardSettingsSmtp.Controls.Add(lblHost);
            cardSettingsSmtp.Controls.Add(lblPort);
            sy += 22;

            txtSmtpHost = new TextBox { Location = new Point(UiKit.T.S5, sy), Size = new Size(310, 30), Font = UiKit.T.Body, Text = "localhost", Enabled = _isAdmin };
            numSmtpPort = new NumericUpDown { Location = new Point(340, sy), Size = new Size(120, 30), Minimum = 1, Maximum = 65535, Value = 25, Font = UiKit.T.Body, Enabled = _isAdmin };
            cardSettingsSmtp.Controls.Add(txtSmtpHost);
            cardSettingsSmtp.Controls.Add(numSmtpPort);
            sy += 40;

            var lblUser = new Label { Text = "Username (Optional)", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, sy), AutoSize = true };
            var lblPass = new Label { Text = "Password", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(270, sy), AutoSize = true };
            cardSettingsSmtp.Controls.Add(lblUser);
            cardSettingsSmtp.Controls.Add(lblPass);
            sy += 22;

            txtSmtpUsername = new TextBox { Location = new Point(UiKit.T.S5, sy), Size = new Size(240, 30), Font = UiKit.T.Body, Enabled = _isAdmin };
            txtSmtpPassword = new TextBox { Location = new Point(270, sy), Size = new Size(240, 30), Font = UiKit.T.Body, UseSystemPasswordChar = true, Enabled = _isAdmin };
            cardSettingsSmtp.Controls.Add(txtSmtpUsername);
            cardSettingsSmtp.Controls.Add(txtSmtpPassword);
            sy += 40;

            var lblFrom = new Label { Text = "From Email Address", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, sy), AutoSize = true };
            var lblFromName = new Label { Text = "Sender Display Name", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(270, sy), AutoSize = true };
            cardSettingsSmtp.Controls.Add(lblFrom);
            cardSettingsSmtp.Controls.Add(lblFromName);
            sy += 22;

            txtSmtpFromEmail = new TextBox { Location = new Point(UiKit.T.S5, sy), Size = new Size(240, 30), Font = UiKit.T.Body, Text = "retention@fixorycrm.local", Enabled = _isAdmin };
            txtSmtpFromName = new TextBox { Location = new Point(270, sy), Size = new Size(240, 30), Font = UiKit.T.Body, Text = "Fixory Computer Repair", Enabled = _isAdmin };
            cardSettingsSmtp.Controls.Add(txtSmtpFromEmail);
            cardSettingsSmtp.Controls.Add(txtSmtpFromName);
            sy += 40;

            chkSmtpSsl = new CheckBox { Text = "Enable SSL / TLS", Location = new Point(UiKit.T.S5, sy), Size = new Size(200, 24), Font = UiKit.T.Small, Enabled = _isAdmin };
            cardSettingsSmtp.Controls.Add(chkSmtpSsl);

            btnSaveSmtp = new Button
            {
                Text = "Save SMTP Settings",
                Font = UiKit.T.BodyStrong,
                BackColor = AppTheme.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(270, sy),
                Size = new Size(180, 36),
                Enabled = _isAdmin,
                Cursor = Cursors.Hand
            };
            btnSaveSmtp.FlatAppearance.BorderSize = 0;
            btnSaveSmtp.Click += async (s, e) => await SaveSmtpSettingsAsync();
            cardSettingsSmtp.Controls.Add(btnSaveSmtp);

            pnlTabSettings.Controls.Add(cardSettingsSmtp);

            // ── Card 3: Email Templates ──
            cardSettingsTemplates = new SurfaceCard
            {
                Location = new Point(0, cardSettingsRules.Bottom + 20),
                Size = new Size(1100, 360)
            };

            var lblTplHead = new Label { Text = "Segment Email Templates", Font = UiKit.T.Section, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, UiKit.T.S5), AutoSize = true };
            cardSettingsTemplates.Controls.Add(lblTplHead);

            int ty = lblTplHead.Bottom + 16;

            var lblSeg = new Label { Text = "Target Segment", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, ty), AutoSize = true };
            var lblTName = new Label { Text = "Template Name", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(220, ty), AutoSize = true };
            var lblTDisc = new Label { Text = "Default Discount %", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(620, ty), AutoSize = true };
            var lblTVal = new Label { Text = "Validity Days", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(780, ty), AutoSize = true };
            cardSettingsTemplates.Controls.Add(lblSeg);
            cardSettingsTemplates.Controls.Add(lblTName);
            cardSettingsTemplates.Controls.Add(lblTDisc);
            cardSettingsTemplates.Controls.Add(lblTVal);
            ty += 22;

            cmbTemplateSegment = new ComboBox
            {
                Location = new Point(UiKit.T.S5, ty),
                Size = new Size(190, 32),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UiKit.T.Body
            };
            foreach (var seg in new[] { "New", "Returning", "Loyal", "At-Risk", "Inactive" })
                cmbTemplateSegment.Items.Add(seg);
            cmbTemplateSegment.SelectedIndex = 2; // Loyal
            cmbTemplateSegment.SelectedIndexChanged += (s, e) => OnTemplateSegmentSelectionChanged();
            cardSettingsTemplates.Controls.Add(cmbTemplateSegment);

            txtTemplateName = new TextBox { Location = new Point(220, ty), Size = new Size(380, 30), Font = UiKit.T.Body, Enabled = _isAdmin };
            cardSettingsTemplates.Controls.Add(txtTemplateName);

            numTemplateDiscount = new NumericUpDown { Location = new Point(620, ty), Size = new Size(130, 30), Minimum = 0, Maximum = 100, Value = 15, Font = UiKit.T.Body, Enabled = _isAdmin };
            cardSettingsTemplates.Controls.Add(numTemplateDiscount);

            numTemplateValidity = new NumericUpDown { Location = new Point(780, ty), Size = new Size(130, 30), Minimum = 1, Maximum = 365, Value = 14, Font = UiKit.T.Body, Enabled = _isAdmin };
            cardSettingsTemplates.Controls.Add(numTemplateValidity);
            ty += 42;

            var lblTSubj = new Label { Text = "Subject Line", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, ty), AutoSize = true };
            cardSettingsTemplates.Controls.Add(lblTSubj);
            ty += 22;

            txtTemplateSubject = new TextBox { Location = new Point(UiKit.T.S5, ty), Size = new Size(900, 30), Font = UiKit.T.Body, Enabled = _isAdmin };
            cardSettingsTemplates.Controls.Add(txtTemplateSubject);
            ty += 40;

            var lblTBody = new Label { Text = "Email Body (Tokens: {CustomerName}, {DiscountPercent}, {PromoCode}, {ValidUntil})", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, Location = new Point(UiKit.T.S5, ty), AutoSize = true };
            cardSettingsTemplates.Controls.Add(lblTBody);
            ty += 22;

            txtTemplateBody = new TextBox
            {
                Location = new Point(UiKit.T.S5, ty),
                Size = new Size(900, 100),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = UiKit.T.Body,
                Enabled = _isAdmin
            };
            cardSettingsTemplates.Controls.Add(txtTemplateBody);
            ty += 116;

            btnSaveTemplate = new Button
            {
                Text = "Save Template Changes",
                Font = UiKit.T.BodyStrong,
                BackColor = AppTheme.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(UiKit.T.S5, ty),
                Size = new Size(200, 36),
                Enabled = _isAdmin,
                Cursor = Cursors.Hand
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

            pnlTabSegments.Visible = (key == "segments");
            pnlTabApprovals.Visible = (key == "approvals");
            pnlTabCampaigns.Visible = (key == "campaigns");
            pnlTabManual.Visible = (key == "manual");
            pnlTabSettings.Visible = (key == "settings");

            // Bring active to front
            if (key == "segments") pnlTabSegments.BringToFront();
            else if (key == "approvals") pnlTabApprovals.BringToFront();
            else if (key == "campaigns") pnlTabCampaigns.BringToFront();
            else if (key == "manual") pnlTabManual.BringToFront();
            else if (key == "settings") pnlTabSettings.BringToFront();

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

            // Layout Tab 1: Segments
            LayoutSegmentsTab();

            // Layout Tab 2: Approvals
            LayoutApprovalsTab();

            // Layout Tab 3: Campaigns
            LayoutCampaignsTab();

            // Layout Tab 4: Manual Email
            if (cardManualCompose != null)
            {
                cardManualCompose.Location = new Point(0, 0);
                cardManualCompose.Size = new Size(Math.Max(960, pnlTabHost.Width - 10), 800);
            }

            // Layout Tab 5: Settings
            if (pnlTabSettings != null)
            {
                int setW = (pnlTabHost.Width - 20) / 2;
                if (cardSettingsRules != null)
                {
                    cardSettingsRules.Width = Math.Max(420, setW);
                    if (cardSettingsSmtp != null)
                    {
                        cardSettingsSmtp.Location = new Point(cardSettingsRules.Right + 16, cardSettingsRules.Top);
                        cardSettingsSmtp.Width = Math.Max(420, setW);
                    }
                    if (cardSettingsTemplates != null)
                    {
                        cardSettingsTemplates.Location = new Point(0, cardSettingsRules.Bottom + 16);
                        cardSettingsTemplates.Width = Math.Max(860, pnlTabHost.Width - 10);
                    }
                }
            }
        }

        private void LayoutSegmentsTab()
        {
            if (cardSegments == null) return;

            int stripW = (pnlTabHost.Width - 32) / 5;
            stripW = Math.Max(140, stripW);

            tileTotal.Location = new Point(0, 0);
            tileTotal.Size = new Size(stripW, 116);

            tileLoyal.Location = new Point(tileTotal.Right + 8, 0);
            tileLoyal.Size = new Size(stripW, 116);

            tileReturning.Location = new Point(tileLoyal.Right + 8, 0);
            tileReturning.Size = new Size(stripW, 116);

            tileAtRisk.Location = new Point(tileReturning.Right + 8, 0);
            tileAtRisk.Size = new Size(stripW, 116);

            tileInactive.Location = new Point(tileAtRisk.Right + 8, 0);
            tileInactive.Size = new Size(stripW, 116);

            int cardTop = 128;
            int cardH = Math.Max(200, pnlTabHost.Height - cardTop - 10);
            cardSegments.Location = new Point(0, cardTop);
            cardSegments.Size = new Size(pnlTabHost.Width, cardH);

            const int cp = UiKit.T.S5;
            lblSegmentsTitle.Location = new Point(cp, cp);
            lblSegmentsCount.Location = new Point(lblSegmentsTitle.Right + UiKit.T.S2, lblSegmentsTitle.Top + 2);

            int toolbarY = lblSegmentsTitle.Bottom + 14;

            // Pills layout
            int px = 0;
            foreach (var pill in _segmentPills)
            {
                int pw = pill.GetPreferredWidth();
                pill.Location = new Point(px, 0);
                pill.Size = new Size(pw, 32);
                px += pw + 4;
            }
            pnlSegmentPills.Location = new Point(cp, toolbarY);
            pnlSegmentPills.Size = new Size(px, 32);

            int searchW = Math.Min(340, Math.Max(220, cardSegments.Width - px - cp * 3));
            searchSegments.Size = new Size(searchW, UiKit.T.InputHeight);
            searchSegments.Location = new Point(cardSegments.Width - cp - searchW, toolbarY);

            int gridTop = toolbarY + UiKit.T.InputHeight + 14;
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
            if (cardApprovals == null) return;

            cardApprovals.Location = new Point(0, 0);
            cardApprovals.Size = new Size(pnlTabHost.Width, Math.Max(200, pnlTabHost.Height - 10));

            const int cp = UiKit.T.S5;
            lblApprovalsTitle.Location = new Point(cp, cp);
            lblApprovalsCount.Location = new Point(lblApprovalsTitle.Right + UiKit.T.S2, lblApprovalsTitle.Top + 2);

            int toolbarY = lblApprovalsTitle.Bottom + 14;

            int px = 0;
            foreach (var pill in _approvalPills)
            {
                int pw = pill.GetPreferredWidth();
                pill.Location = new Point(px, 0);
                pill.Size = new Size(pw, 32);
                px += pw + 4;
            }
            pnlApprovalPills.Location = new Point(cp, toolbarY);
            pnlApprovalPills.Size = new Size(px, 32);

            // Date pickers
            int dx = pnlApprovalPills.Right + 16;
            dtpApprovalFrom.Location = new Point(dx, toolbarY + 3);
            dtpApprovalTo.Location = new Point(dtpApprovalFrom.Right + 6, toolbarY + 3);
            chkApprovalAllDates.Location = new Point(dtpApprovalTo.Right + 6, toolbarY + 6);

            int searchW = Math.Min(320, Math.Max(180, cardApprovals.Width - chkApprovalAllDates.Right - cp * 2));
            searchApprovals.Size = new Size(searchW, UiKit.T.InputHeight);
            searchApprovals.Location = new Point(cardApprovals.Width - cp - searchW, toolbarY);

            int gridTop = toolbarY + UiKit.T.InputHeight + 14;
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
            if (cardCampaigns == null) return;

            cardCampaigns.Location = new Point(0, 0);
            cardCampaigns.Size = new Size(pnlTabHost.Width, Math.Max(200, pnlTabHost.Height - 10));

            const int cp = UiKit.T.S5;
            lblCampaignsTitle.Location = new Point(cp, cp);
            lblCampaignsCount.Location = new Point(lblCampaignsTitle.Right + UiKit.T.S2, lblCampaignsTitle.Top + 2);

            int toolbarY = lblCampaignsTitle.Bottom + 14;

            int px = 0;
            foreach (var pill in _campaignPills)
            {
                int pw = pill.GetPreferredWidth();
                pill.Location = new Point(px, 0);
                pill.Size = new Size(pw, 32);
                px += pw + 4;
            }
            pnlCampaignPills.Location = new Point(cp, toolbarY);
            pnlCampaignPills.Size = new Size(px, 32);

            int dx = pnlCampaignPills.Right + 16;
            dtpCampaignFrom.Location = new Point(dx, toolbarY + 3);
            dtpCampaignTo.Location = new Point(dtpCampaignFrom.Right + 6, toolbarY + 3);
            chkCampaignAllDates.Location = new Point(dtpCampaignTo.Right + 6, toolbarY + 6);

            int searchW = Math.Min(320, Math.Max(180, cardCampaigns.Width - chkCampaignAllDates.Right - cp * 2));
            searchCampaigns.Size = new Size(searchW, UiKit.T.InputHeight);
            searchCampaigns.Location = new Point(cardCampaigns.Width - cp - searchW, toolbarY);

            int gridTop = toolbarY + UiKit.T.InputHeight + 14;
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

        // ═══════════════════════════════════════════════════════
        // DATA LOADING & FILTERING
        // ═══════════════════════════════════════════════════════

        public async Task ReloadAsync()
        {
            try
            {
                // Fetch recommendations
                _cachedRecommendations = await _api.GetRetentionRecommendationsAsync();

                // Fetch requests
                _cachedRequests = await _api.GetRetentionRequestsAsync();

                // Fetch campaigns
                _cachedCampaigns = await _api.GetRetentionCampaignsAsync();

                // Fetch customers
                _cachedCustomers = await _api.GetCustomersAsync();

                // Fetch templates
                _cachedTemplates = await _api.GetRetentionTemplatesAsync();

                // Fetch settings
                var set = await _api.GetRetentionSettingsAsync();
                if (set != null) _cachedSettings = set;

                // Update metric tiles
                UpdateMetricTiles();

                // Update tab badges
                int pendingApprovals = _cachedRequests.Count(r => r.Status == 0);
                var approvalsTab = _navTabs.FirstOrDefault(t => t.Category == "approvals");
                if (approvalsTab != null)
                {
                    approvalsTab.Text = pendingApprovals > 0 ? $"Approvals ({pendingApprovals})" : "Approvals";
                    approvalsTab.Invalidate();
                }

                // Populate manual email customer dropdown
                PopulateManualEmailControls();

                // Populate settings controls
                PopulateSettingsControls();

                // Apply filters to active grids
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
                // Populate templates dropdown first so template items exist
                string? prevTpl = cmbManualTemplate.SelectedItem?.ToString();
                cmbManualTemplate.Items.Clear();
                foreach (var t in _cachedTemplates)
                {
                    cmbManualTemplate.Items.Add($"{t.SegmentName}: {t.TemplateName}");
                }
                if (cmbManualTemplate.Items.Count > 0)
                {
                    if (prevTpl != null && cmbManualTemplate.Items.Contains(prevTpl))
                        cmbManualTemplate.SelectedItem = prevTpl;
                    else
                        cmbManualTemplate.SelectedIndex = 0;
                }

                // Populate customers dropdown
                string? prevCust = cmbManualCustomer.SelectedItem?.ToString();
                cmbManualCustomer.Items.Clear();
                foreach (var c in _cachedCustomers)
                {
                    cmbManualCustomer.Items.Add($"{c.FullName} ({c.Email ?? c.Phone ?? "No contact"})");
                }

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

            // Explicitly sync manual email controls now that population is complete
            if (cmbManualCustomer.Items.Count > 0 && cmbManualCustomer.SelectedIndex >= 0)
            {
                OnManualCustomerSelected();
            }
            else if (cmbManualTemplate.Items.Count > 0 && cmbManualTemplate.SelectedIndex >= 0)
            {
                OnManualTemplateChanged();
            }
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
                {
                    numOfferValidityDays.Value = Math.Max(numOfferValidityDays.Minimum, Math.Min(numOfferValidityDays.Maximum, _cachedSettings.DefaultOfferValidityDays > 0 ? _cachedSettings.DefaultOfferValidityDays : 14));
                }

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
            {
                q = q.Where(r => r.StatusText.Equals(_selectedApprovalStatus, StringComparison.OrdinalIgnoreCase));
            }

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
            {
                q = q.Where(c => c.DeliveryStatus.Equals(_selectedCampaignStatus, StringComparison.OrdinalIgnoreCase));
            }

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
        // MODALS & DIALOGS
        // ═══════════════════════════════════════════════════════

        private void OpenNewRequestDialog(int? preselectedCustomerId = null)
        {
            CustomerDto? preselected = null;
            if (preselectedCustomerId.HasValue)
                preselected = _cachedCustomers.FirstOrDefault(c => c.CustomerId == preselectedCustomerId.Value);

            var dlg = new RetentionRequestFormDialog(_cachedCustomers, preselected);
            if (dlg.ShowModal(this.FindForm()) == DialogResult.OK)
            {
                _ = ReloadAsync();
            }
        }

        private void OpenReviewDialog(RetentionRequestDto req)
        {
            var dlg = new RetentionReviewDialog(req);
            dlg.ShowModal(this.FindForm());
            if (dlg.StateChanged)
            {
                _ = ReloadAsync();
            }
        }

        // ═══════════════════════════════════════════════════════
        // GRID STYLING
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
            g.ScrollBars = ScrollBars.Vertical;
            g.RowTemplate.Height = UiKit.T.RowHeight;
            g.ColumnHeadersHeight = UiKit.T.HeaderHeight;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            g.ColumnHeadersDefaultCellStyle.BackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.ForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.Font = UiKit.T.SmallStrong;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(UiKit.T.S3, 0, UiKit.T.S3, 0);

            g.DefaultCellStyle.BackColor = UiKit.T.Surface;
            g.DefaultCellStyle.ForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Font = UiKit.T.Body;
            g.DefaultCellStyle.SelectionBackColor = UiKit.Wash(AppTheme.Primary);
            g.DefaultCellStyle.SelectionForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Padding = new Padding(UiKit.T.S3, 0, UiKit.T.S3, 0);
            g.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            g.RowsDefaultCellStyle.BackColor = UiKit.T.Surface;
        }

        // ═══════════════════════════════════════════════════════
        // PILL BADGE RENDERER & NESTED CONTROLS
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
                int pillH = 24;
                int pillW = Math.Min(bounds.Width - 16, UiKit.Measure(text, UiKit.T.SmallStrong).Width + 18);
                pillW = Math.Max(56, pillW);
                int pillX = bounds.X + (bounds.Width - pillW) / 2;
                int pillY = bounds.Y + (bounds.Height - pillH) / 2;

                var rect = new Rectangle(pillX, pillY, pillW, pillH);
                UiKit.FillRounded(g, rect, pillH / 2, bg);
                UiKit.Text(g, text, UiKit.T.SmallStrong, fg, rect, UiKit.Center);
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

            public int GetPreferredWidth() => UiKit.Measure(Text, UiKit.T.SmallStrong).Width + 28;

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
                    UiKit.Text(g, Text, UiKit.T.SmallStrong, Color.White, ClientRectangle, UiKit.Center);
                }
                else
                {
                    if (_hover)
                        UiKit.FillRounded(g, ClientRectangle, 8, UiKit.T.RowHover);
                    UiKit.Text(g, Text, UiKit.T.SmallStrong, UiKit.T.InkMuted, ClientRectangle, UiKit.Center);
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
                Inner.Location = new Point(34, (Height - Inner.PreferredHeight) / 2);
                Inner.Width = Width - 46;
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

            public int PreferredWidth => UiKit.Measure(Text, UiKit.T.BodyStrong).Width + 56;

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
                    new Rectangle(34, 0, Width - 42, Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
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

                int cy = Height / 2 - 40;

                var circle = new Rectangle(Width / 2 - 26, cy, 52, 52);
                UiKit.FillRounded(g, circle, 26, UiKit.T.LineSoft);
                UiKit.Text(g, _glyph, UiKit.T.GlyphLarge, UiKit.T.InkFaint, circle,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                UiKit.Text(g, _title, UiKit.T.Section, UiKit.T.Ink,
                    new Rectangle(0, circle.Bottom + UiKit.T.S4, Width, 24),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);

                int msgW = Math.Min(420, Width - UiKit.T.S6 * 2);
                UiKit.Text(g, _message, UiKit.T.Body, UiKit.T.InkMuted,
                    new Rectangle((Width - msgW) / 2, circle.Bottom + UiKit.T.S4 + 28, msgW, 60),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
            }
        }
    }
}
