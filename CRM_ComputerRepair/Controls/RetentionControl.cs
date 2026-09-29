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
        private Label lblManualHeading = null!;
        private Label lblManualDesc = null!;
        private Label lblCustTitle = null!;
        private ComboBox cmbManualCustomer = null!;
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

        // Right Live Preview Card
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

            // Opportunities Card
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

            // Category Filter Pills
            pnlSegmentPills = new Panel { BackColor = UiKit.T.Surface };
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

            dgvSegments.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CustomerName",
                HeaderText = "Customer",
                Width = 190,
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
                Width = 240,
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
                Width = 135,
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
                Width = 145,
                FlatStyle = FlatStyle.Flat
            };
            btnCol.DefaultCellStyle.ForeColor = AppTheme.Primary;
            dgvSegments.Columns.Add(btnCol);
        }

        private void DgvSegments_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.Graphics == null) return;

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

            // Status Filter Pills
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

            // Date Pickers
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

            dgvApprovals.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CustomerName",
                HeaderText = "Customer",
                Width = 190,
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
                Width = 140,
                ReadOnly = true,
                DefaultCellStyle = { ForeColor = UiKit.T.InkMuted }
            });

            dgvApprovals.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "SubmittedAt",
                HeaderText = "Submitted Date",
                Width = 130,
                ReadOnly = true,
                DefaultCellStyle = { Format = "MMM d, yyyy", Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            var btnCol = new DataGridViewButtonColumn
            {
                Name = "colReview",
                HeaderText = "Action",
                Text = "Review / View",
                UseColumnTextForButtonValue = true,
                Width = 135,
                FlatStyle = FlatStyle.Flat
            };
            btnCol.DefaultCellStyle.ForeColor = AppTheme.Primary;
            dgvApprovals.Columns.Add(btnCol);
        }

        private void DgvApprovals_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.Graphics == null) return;

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

            dgvCampaigns.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "RecipientName",
                HeaderText = "Recipient",
                Width = 190,
                ReadOnly = true,
                DefaultCellStyle = { Font = UiKit.T.BodyStrong }
            });

            dgvCampaigns.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "RecipientEmail",
                HeaderText = "Email Address",
                Width = 180,
                ReadOnly = true,
                DefaultCellStyle = { ForeColor = UiKit.T.InkMuted }
            });

            dgvCampaigns.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "SegmentName",
                HeaderText = "Segment",
                Width = 110,
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
                Width = 130,
                ReadOnly = true,
                DefaultCellStyle = { Format = "MMM d, yyyy", Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            var btnCol = new DataGridViewButtonColumn
            {
                Name = "colDispatch",
                HeaderText = "Action",
                Text = "Dispatch Now",
                UseColumnTextForButtonValue = true,
                Width = 145,
                FlatStyle = FlatStyle.Flat
            };
            btnCol.DefaultCellStyle.ForeColor = AppTheme.Primary;
            dgvCampaigns.Columns.Add(btnCol);
        }

        private void DgvCampaigns_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.Graphics == null) return;

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
                Font = AppFonts.Mono(9.5F),
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

            // ── Left: Compose Card ──
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

            // Recipient Customer
            lblCustTitle = new Label
            {
                Text = "Recipient Customer *",
                Font = UiKit.T.SmallStrong,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = UiKit.T.Surface,
                UseMnemonic = false
            };
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
            cmbManualCustomer.Leave += (s, e) =>
            {
                if (cmbManualCustomer.SelectedIndex <= 0 && !string.IsNullOrWhiteSpace(cmbManualCustomer.Text))
                {
                    int idx = cmbManualCustomer.FindStringExact(cmbManualCustomer.Text.Trim());
                    if (idx < 0) idx = cmbManualCustomer.FindString(cmbManualCustomer.Text.Trim());
                    if (idx >= 0) cmbManualCustomer.SelectedIndex = idx;
                }
            };
            cmbManualCustomer.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (cmbManualCustomer.SelectedIndex <= 0 && !string.IsNullOrWhiteSpace(cmbManualCustomer.Text))
                    {
                        int idx = cmbManualCustomer.FindStringExact(cmbManualCustomer.Text.Trim());
                        if (idx < 0) idx = cmbManualCustomer.FindString(cmbManualCustomer.Text.Trim());
                        if (idx >= 0) cmbManualCustomer.SelectedIndex = idx;
                    }
                }
            };
            cardManualCompose.Controls.Add(cmbManualCustomer);

            // Customer details snapshot box
            pnlCustDetailsBox = new Panel
            {
                BackColor = Color.FromArgb(248, 250, 252)
            };
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

            // Anti-Fatigue Warning Banner
            pnlCooldownAlert = new Panel
            {
                BackColor = Color.FromArgb(209, 250, 229)
            };
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

            // Campaign Offer Configuration Row
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

            numManualDiscount = new NumericUpDown
            {
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
                Minimum = 1,
                Maximum = 365,
                Value = 14,
                Font = UiKit.T.Body
            };
            numManualValidity.ValueChanged += (s, e) => UpdateManualPreview();
            cardManualCompose.Controls.Add(numManualValidity);

            txtManualPromoCode = new TextBox
            {
                Font = UiKit.T.Body,
                Text = $"FIXORY-RET-{Guid.NewGuid().ToString()[..6].ToUpper()}"
            };
            txtManualPromoCode.TextChanged += (s, e) => UpdateManualPreview();
            cardManualCompose.Controls.Add(txtManualPromoCode);

            // Subject Line
            lblSubjTitle = new Label { Text = "Email Subject *", Font = UiKit.T.SmallStrong, ForeColor = UiKit.T.Ink, AutoSize = true, BackColor = UiKit.T.Surface, UseMnemonic = false };
            cardManualCompose.Controls.Add(lblSubjTitle);

            txtManualSubject = new TextBox
            {
                Font = UiKit.T.Body,
                Text = "Special Care & Maintenance Offer from Fixory Computer Repair"
            };
            txtManualSubject.TextChanged += (s, e) => UpdateManualPreview();
            cardManualCompose.Controls.Add(txtManualSubject);

            // Body
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

            // Send Button
            btnManualSend = new SaasButton("Send Retention Email via SMTP", SaasButtonVariant.Primary, "\uE715")
            {
                Width = 280,
                Height = 40
            };
            btnManualSend.Click += async (s, e) => await SendManualRetentionEmailAsync();
            cardManualCompose.Controls.Add(btnManualSend);

            pnlTabManual.Controls.Add(cardManualCompose);

            // ── Right: Live Customer Preview Card ──
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

            // Simulated envelope header frame
            pnlPreviewEnvelope = new Panel
            {
                BackColor = Color.FromArgb(248, 250, 252)
            };

            lblPreviewFrom = new Label
            {
                Text = "From: Fixory Computer Repair <service@fixory.com>",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                BackColor = Color.FromArgb(248, 250, 252),
                UseMnemonic = false
            };
            lblPreviewTo = new Label
            {
                Text = "To: Customer <email@example.com>",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                BackColor = Color.FromArgb(248, 250, 252),
                UseMnemonic = false
            };
            lblPreviewSubject = new Label
            {
                Text = "Subject: Special Care & Maintenance Offer from Fixory Computer Repair",
                Font = UiKit.T.BodyStrong,
                ForeColor = UiKit.T.Ink,
                BackColor = Color.FromArgb(248, 250, 252),
                UseMnemonic = false
            };
            lblPreviewDate = new Label
            {
                Text = $"Date: {DateTime.Now:MMM d, yyyy  h:mm tt}",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                BackColor = Color.FromArgb(248, 250, 252),
                UseMnemonic = false
            };

            pnlPreviewEnvelope.Controls.Add(lblPreviewFrom);
            pnlPreviewEnvelope.Controls.Add(lblPreviewTo);
            pnlPreviewEnvelope.Controls.Add(lblPreviewSubject);
            pnlPreviewEnvelope.Controls.Add(lblPreviewDate);
            cardManualPreview.Controls.Add(pnlPreviewEnvelope);

            // Simulated white reading canvas
            pnlPreviewSheet = new Panel
            {
                BackColor = Color.White
            };

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

            lblManualCustDetails.Text = $"Segment: {seg}  ·  Repairs: {visits}  ·  Spent: ₱{spent:N2}  ·  Last Repair: {lastDays}d ago\n" +
                                        $"Contact: {cust.Email ?? "No email"}  ·  Phone: {cust.Phone ?? "No phone"}";

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

            string custEmail = cmbManualCustomer.SelectedIndex >= 0 && cmbManualCustomer.SelectedIndex < _cachedCustomers.Count
                ? (_cachedCustomers[cmbManualCustomer.SelectedIndex].Email ?? "customer@example.com")
                : "customer@example.com";

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

            // Update preview headers
            if (lblPreviewTo != null)
                lblPreviewTo.Text = $"To: {custName} <{custEmail}>";
            if (lblPreviewSubject != null)
                lblPreviewSubject.Text = $"Subject: {txtManualSubject.Text}";
            if (lblPreviewDate != null)
                lblPreviewDate.Text = $"Date: {DateTime.Now:MMM d, yyyy  h:mm tt}";

            // Render clean formatted message for customer preview
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
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DropDownWidth = 220,
                Font = UiKit.T.Body
            };
            foreach (var seg in new[] { "New", "Returning", "Loyal", "At-Risk", "Inactive" })
                cmbTemplateSegment.Items.Add(seg);
            cmbTemplateSegment.SelectedIndex = 2; // Loyal
            cmbTemplateSegment.SelectedIndexChanged += (s, e) => OnTemplateSegmentSelectionChanged();
            cmbTemplateSegment.Leave += (s, e) =>
            {
                if (cmbTemplateSegment.SelectedIndex < 0 && !string.IsNullOrWhiteSpace(cmbTemplateSegment.Text))
                {
                    int idx = cmbTemplateSegment.FindStringExact(cmbTemplateSegment.Text.Trim());
                    if (idx < 0) idx = cmbTemplateSegment.FindString(cmbTemplateSegment.Text.Trim());
                    if (idx >= 0) cmbTemplateSegment.SelectedIndex = idx;
                }
            };
            cmbTemplateSegment.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (cmbTemplateSegment.SelectedIndex < 0 && !string.IsNullOrWhiteSpace(cmbTemplateSegment.Text))
                    {
                        int idx = cmbTemplateSegment.FindStringExact(cmbTemplateSegment.Text.Trim());
                        if (idx < 0) idx = cmbTemplateSegment.FindString(cmbTemplateSegment.Text.Trim());
                        if (idx >= 0) cmbTemplateSegment.SelectedIndex = idx;
                    }
                }
            };
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
            LayoutManualEmailTab();

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

            var tiles = new[] { tileTotal, tileLoyal, tileReturning, tileAtRisk, tileInactive };
            int tileH = 116;
            if (tiles.All(t => t != null))
            {
                tileH = Math.Max(116, tiles.Max(t => t.HeightFor(stripW)));
            }

            tileTotal.Location = new Point(0, 0);
            tileTotal.Size = new Size(stripW, tileH);

            tileLoyal.Location = new Point(tileTotal.Right + 8, 0);
            tileLoyal.Size = new Size(stripW, tileH);

            tileReturning.Location = new Point(tileLoyal.Right + 8, 0);
            tileReturning.Size = new Size(stripW, tileH);

            tileAtRisk.Location = new Point(tileReturning.Right + 8, 0);
            tileAtRisk.Size = new Size(stripW, tileH);

            tileInactive.Location = new Point(tileAtRisk.Right + 8, 0);
            tileInactive.Size = new Size(stripW, tileH);

            int cardTop = tileH + 14;
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
            dtpApprovalFrom.Size = new Size(130, UiKit.T.InputHeight);
            dtpApprovalFrom.Location = new Point(dx, toolbarY + 3);
            dtpApprovalTo.Size = new Size(130, UiKit.T.InputHeight);
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
            dtpCampaignFrom.Size = new Size(130, UiKit.T.InputHeight);
            dtpCampaignFrom.Location = new Point(dx, toolbarY + 3);
            dtpCampaignTo.Size = new Size(130, UiKit.T.InputHeight);
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

        private void LayoutManualEmailTab()
        {
            if (cardManualCompose == null || pnlTabManual == null || pnlTabHost == null) return;

            int availW = pnlTabHost.ClientSize.Width;
            int availH = pnlTabHost.ClientSize.Height;
            if (availW <= 0 || availH <= 0) return;

            bool sideBySide = availW >= 940;
            int gap = 16;

            if (sideBySide)
            {
                int composeW = Math.Min(620, Math.Max(480, (int)((availW - gap) * 0.54)));
                int previewW = Math.Max(380, availW - composeW - gap);
                int cardH = Math.Max(660, availH - 8);

                cardManualCompose.Location = new Point(0, 0);
                cardManualCompose.Size = new Size(composeW, cardH);

                if (cardManualPreview != null)
                {
                    cardManualPreview.Location = new Point(cardManualCompose.Right + gap, 0);
                    cardManualPreview.Size = new Size(previewW, cardH);
                }
            }
            else
            {
                int cardW = Math.Max(460, availW - 8);
                cardManualCompose.Location = new Point(0, 0);
                cardManualCompose.Size = new Size(cardW, 640);

                if (cardManualPreview != null)
                {
                    cardManualPreview.Location = new Point(0, cardManualCompose.Bottom + gap);
                    cardManualPreview.Size = new Size(cardW, 520);
                }
            }

            LayoutComposeCard();
            LayoutPreviewCard();
        }

        private void LayoutComposeCard()
        {
            if (cardManualCompose == null) return;

            const int cp = 18;
            int cw = Math.Max(200, cardManualCompose.Width - cp * 2);

            lblManualHeading.Location = new Point(cp, 16);
            lblManualDesc.Location = new Point(cp, lblManualHeading.Bottom + 4);

            int y = lblManualDesc.Bottom + 16;

            // Recipient Customer
            lblCustTitle.Location = new Point(cp, y);
            y += 20;

            cmbManualCustomer.Location = new Point(cp, y);
            cmbManualCustomer.Size = new Size(cw, 30);
            y += 36;

            // Customer details pill box
            pnlCustDetailsBox.Location = new Point(cp, y);
            pnlCustDetailsBox.Size = new Size(cw, 46);
            lblManualCustDetails.Location = new Point(10, 5);
            lblManualCustDetails.Size = new Size(pnlCustDetailsBox.Width - 20, 36);
            y += pnlCustDetailsBox.Height + 10;

            // Anti-Fatigue Warning
            pnlCooldownAlert.Location = new Point(cp, y);
            pnlCooldownAlert.Size = new Size(cw, 44);
            int chkW = Math.Min(200, Math.Max(160, (int)(pnlCooldownAlert.Width * 0.36)));
            chkOverrideCooldown.Size = new Size(chkW, 24);
            chkOverrideCooldown.Location = new Point(pnlCooldownAlert.Width - chkW - 10, 10);
            lblCooldownAlert.Location = new Point(10, 6);
            lblCooldownAlert.Size = new Size(Math.Max(80, chkOverrideCooldown.Left - 16), 32);
            y += pnlCooldownAlert.Height + 12;

            // Campaign Template & Offer Settings Row
            int discW = 75;
            int validW = 90;
            int codeW = Math.Max(110, (int)(cw * 0.28));
            int tplW = Math.Max(120, cw - discW - validW - codeW - 36);

            lblTplTitle.Location = new Point(cp, y);
            lblDiscTitle.Location = new Point(cp + tplW + 12, y);
            lblValidTitle.Location = new Point(lblDiscTitle.Left + discW + 12, y);
            lblCodeTitle.Location = new Point(lblValidTitle.Left + validW + 12, y);
            y += 20;

            cmbManualTemplate.Location = new Point(cp, y);
            cmbManualTemplate.Size = new Size(tplW, 30);

            numManualDiscount.Location = new Point(lblDiscTitle.Left, y);
            numManualDiscount.Size = new Size(discW, 30);

            numManualValidity.Location = new Point(lblValidTitle.Left, y);
            numManualValidity.Size = new Size(validW, 30);

            txtManualPromoCode.Location = new Point(lblCodeTitle.Left, y);
            txtManualPromoCode.Size = new Size(codeW, 30);
            y += 40;

            // Subject Line
            lblSubjTitle.Location = new Point(cp, y);
            y += 20;

            txtManualSubject.Location = new Point(cp, y);
            txtManualSubject.Size = new Size(cw, 30);
            y += 40;

            // Email Message Body
            lblBodyTitle.Location = new Point(cp, y);
            lblBodyTokens.Location = new Point(lblBodyTitle.Right + 12, y + 1);
            y += 20;

            int bottomReserve = 56;
            int bodyH = Math.Max(90, cardManualCompose.Height - y - bottomReserve - 14);
            txtManualBody.Location = new Point(cp, y);
            txtManualBody.Size = new Size(cw, bodyH);
            y += bodyH + 14;

            // Send Button
            btnManualSend.Location = new Point(cp, y);
            btnManualSend.Size = new Size(Math.Min(cw, 280), 38);
        }

        private void LayoutPreviewCard()
        {
            if (cardManualPreview == null) return;

            const int pp = 18;
            int pw = Math.Max(200, cardManualPreview.Width - pp * 2);

            lblPreviewHeading.Location = new Point(pp, 16);
            lblPreviewDesc.Location = new Point(pp, lblPreviewHeading.Bottom + 4);

            int y = lblPreviewDesc.Bottom + 16;

            // Email envelope header frame
            pnlPreviewEnvelope.Location = new Point(pp, y);
            pnlPreviewEnvelope.Size = new Size(pw, 100);

            int envW = pnlPreviewEnvelope.Width - 20;
            lblPreviewFrom.Location = new Point(10, 8);
            lblPreviewFrom.Size = new Size(envW, 18);

            lblPreviewTo.Location = new Point(10, 28);
            lblPreviewTo.Size = new Size(envW, 18);

            lblPreviewSubject.Location = new Point(10, 50);
            lblPreviewSubject.Size = new Size(envW, 20);

            lblPreviewDate.Location = new Point(10, 74);
            lblPreviewDate.Size = new Size(envW, 18);

            y += pnlPreviewEnvelope.Height + 12;

            // Email body reader canvas
            int bodyH = Math.Max(120, cardManualPreview.Height - y - pp);
            pnlPreviewSheet.Location = new Point(pp, y);
            pnlPreviewSheet.Size = new Size(pw, bodyH);

            txtManualPreview.Location = new Point(12, 12);
            txtManualPreview.Size = new Size(pnlPreviewSheet.Width - 24, Math.Max(40, pnlPreviewSheet.Height - 24));
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
            g.ScrollBars = ScrollBars.Both;
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
            g.DefaultCellStyle.Padding = new Padding(UiKit.T.S3, 6, UiKit.T.S3, 6);
            g.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
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

        /// <summary>
        /// Retention KPI Card — Clean, minimal card matching Dashboard and Reports styling.
        /// Dynamically measures fonts to prevent top/bottom clipping, with ample bottom padding.
        /// </summary>
        [DesignerCategory("Code")]
        private sealed class RetentionKpiCard : Control
        {
            private const int Pad = 18;

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

            public int HeightFor(int width) => Math.Max(116, Measure(width).Total);

            private (Font NumFont, int TopH, int NumH, int CapH, int Total) Measure(int width)
            {
                int inner = Math.Max(20, width - Pad * 2);
                int topH = Math.Max(16, UiKit.T.SmallStrong.Height);

                Font numFont = NumFonts[^1];
                foreach (var f in NumFonts)
                {
                    var size = TextRenderer.MeasureText(_number, f, new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                    if (size.Width <= inner) { numFont = f; break; }
                }

                int numH = Math.Max(numFont.Height + 2, TextRenderer.MeasureText(_number, numFont, new Size(inner, int.MaxValue),
                    TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height + 2);

                int capH = string.IsNullOrEmpty(_sub) ? 0 : Math.Max(UiKit.T.Small.Height + 2,
                    TextRenderer.MeasureText(_sub, UiKit.T.Small, new Size(inner, int.MaxValue),
                        TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height + 2);

                int total = Pad + topH + 8 + numH + (capH > 0 ? 4 + capH : 0) + Pad;
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

                // 1. Accent Dot + Label
                UiKit.Dot(g, Pad + 3, Pad + m.TopH / 2f, 6, _accent);
                var lblRect = new Rectangle(Pad + 12, Pad, inner - 12, m.TopH);
                UiKit.Text(g, _label, UiKit.T.SmallStrong, UiKit.T.InkMuted, lblRect,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                // 2. Large Number (Rendered with Top alignment so top is NEVER clipped)
                int numTop = Pad + m.TopH + 8;
                var numRect = new Rectangle(Pad, numTop, inner, m.NumH);
                UiKit.Text(g, _number, m.NumFont, UiKit.T.Ink, numRect,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                // 3. Subtitle (Rendered with Top alignment so bottom is NEVER clipped)
                if (!string.IsNullOrEmpty(_sub) && m.CapH > 0)
                {
                    int capTop = numTop + m.NumH + 4;
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
