using CRM.winforms.Auth;
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
    /// Modal dialog for Add / Edit on a Follow-Up.
    /// Provides computer repair industry standard templates, customer selection,
    /// linked repair orders, date shortcuts, and outcome logging.
    /// </summary>
    [DesignerCategory("Code")]
    public class FollowUpFormDialog : ModalForm
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private readonly FollowUpDto? _editing;
        private readonly bool _isEditMode;

        private List<CustomerDto> _customers = new();
        private List<RepairRequestDto> _repairs = new();
        private bool _isInitializing = true;

        // ═══════════ CONTROLS ═══════════

        // Quick Presets
        private Label lblPresets = null!;
        private FlowLayoutPanel pnlPresets = null!;

        // Customer & Repair
        private Label lblCustomer = null!;
        private Label lblRepair = null!;
        private ComboBox cmbCustomer = null!;
        private ComboBox cmbRepair = null!;
        private Label lblCustomerContact = null!;

        // Subject & Notes
        private Label lblSubject = null!;
        private Label lblNotes = null!;
        private TextField inpSubject = null!;
        private TextField inpNotes = null!;

        // Schedule & Channel
        private Label lblScheduledAt = null!;
        private Label lblChannel = null!;
        private DateTimePicker dtpScheduledAt = null!;
        private ComboBox cmbChannel = null!;
        private FlowLayoutPanel pnlDateChips = null!;

        // Status & Assigned
        private Label lblStatus = null!;
        private Label lblAssignedTo = null!;
        private ComboBox cmbStatus = null!;
        private TextField inpAssignedTo = null!;

        // Outcome Panel (shown when Completed)
        private Panel pnlOutcome = null!;
        private Label lblOutcome = null!;
        private TextField inpOutcome = null!;
        private CheckBox chkLogInteraction = null!;

        // Error labels
        private Label lblErrorCustomer = null!;
        private Label lblErrorSubject = null!;
        private Label lblErrorNotes = null!;

        // Actions
        private SaasButton btnSave = null!;
        private SaasButton btnCancel = null!;
        private SaasButton btnArchive = null!;

        // ═══════════ CONSTRUCTOR ═══════════

        public FollowUpFormDialog(
            FollowUpDto? existing = null,
            List<CustomerDto>? cachedCustomers = null,
            List<RepairRequestDto>? cachedRepairs = null)
        {
            _editing = existing;
            _isEditMode = existing != null && existing.FollowUpId > 0;

            if (cachedCustomers != null) _customers = new List<CustomerDto>(cachedCustomers);
            if (cachedRepairs != null) _repairs = new List<RepairRequestDto>(cachedRepairs);

            BuildCard(
                _isEditMode ? "Edit Follow-Up" : "Schedule Follow-Up",
                _isEditMode
                    ? "Update follow-up scheduling, customer response, or recorded outcome."
                    : "Schedule proactive customer outreach, quality check, or repair reminder.",
                width: 660,
                height: 780);

            BuildContent();

            this.Load += async (s, e) => await InitializeDataAsync();
        }

        // ═══════════ DATA INITIALIZATION ═══════════

        private async Task InitializeDataAsync()
        {
            try
            {
                if (_customers.Count == 0 || _repairs.Count == 0)
                {
                    var custTask = _customers.Count == 0 ? _api.GetCustomersAsync() : Task.FromResult(_customers);
                    var repTask = _repairs.Count == 0 ? _api.GetRepairRequestsAsync() : Task.FromResult(_repairs);
                    await Task.WhenAll(custTask, repTask);

                    _customers = await custTask ?? new List<CustomerDto>();
                    _repairs = await repTask ?? new List<RepairRequestDto>();
                }

                PopulateCustomers();
                ApplyPrefill();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load customer details:\n{ex.Message}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void PopulateCustomers()
        {
            cmbCustomer.Items.Clear();
            cmbCustomer.Items.Add("— Select a Customer —");

            foreach (var c in _customers.OrderBy(x => x.FullName))
            {
                string contact = !string.IsNullOrWhiteSpace(c.Phone) ? c.Phone : (!string.IsNullOrWhiteSpace(c.Email) ? c.Email : "No contact");
                cmbCustomer.Items.Add($"{c.FullName} ({contact})");
            }

            if (cmbCustomer.Items.Count > 0)
                cmbCustomer.SelectedIndex = 0;

            ModalKit.AdjustDropDownWidth(cmbCustomer);
        }

        private void PopulateRepairsForCustomer(int? customerId)
        {
            cmbRepair.Items.Clear();
            cmbRepair.Items.Add("None — General Customer Outreach");

            if (customerId.HasValue && customerId.Value > 0)
            {
                var matchingRepairs = _repairs
                    .Where(r => r.CustomerId == customerId.Value)
                    .OrderByDescending(r => r.RepairRequestId)
                    .ToList();

                foreach (var r in matchingRepairs)
                {
                    string status = r.StatusText;
                    string device = string.IsNullOrWhiteSpace(r.DeviceModel) ? "PC / Device" : r.DeviceModel;
                    cmbRepair.Items.Add($"{r.RequestNumber} · {device} ({status})");
                }
            }

            cmbRepair.SelectedIndex = 0;
            ModalKit.AdjustDropDownWidth(cmbRepair);
        }

        private void ApplyPrefill()
        {
            if (_editing != null)
            {
                // Customer selection
                if (_editing.CustomerId.HasValue)
                {
                    int custIdx = _customers.FindIndex(c => c.CustomerId == _editing.CustomerId.Value);
                    if (custIdx >= 0)
                        cmbCustomer.SelectedIndex = custIdx + 1; // +1 for "Select a Customer"
                }

                // Repair selection
                if (_editing.RepairRequestId.HasValue)
                {
                    var matching = _repairs.Where(r => r.CustomerId == _editing.CustomerId).ToList();
                    int repIdx = matching.FindIndex(r => r.RepairRequestId == _editing.RepairRequestId.Value);
                    if (repIdx >= 0)
                        cmbRepair.SelectedIndex = repIdx + 1;
                }

                inpSubject.Text = _editing.Subject ?? "";
                inpNotes.Text = _editing.Notes ?? "";
                dtpScheduledAt.Value = _editing.ScheduledAt > DateTime.MinValue ? _editing.ScheduledAt : DateTime.Now.AddDays(1);
                cmbChannel.SelectedIndex = Math.Clamp(_editing.Channel, 0, 3);
                cmbStatus.SelectedIndex = Math.Clamp(_editing.Status, 0, 2);
                inpAssignedTo.Text = string.IsNullOrWhiteSpace(_editing.AssignedToUserId) ? (UserSession.Username ?? "staff") : _editing.AssignedToUserId;
            }
            else
            {
                dtpScheduledAt.Value = DateTime.Now.AddDays(1);
                cmbChannel.SelectedIndex = 0; // Call
                cmbStatus.SelectedIndex = 0;  // Scheduled
                inpAssignedTo.Text = UserSession.Username ?? "staff";
            }

            UpdateCustomerContactInfo();
            UpdateOutcomeVisibility();
        }

        // ═══════════ CONTENT BUILD ═══════════

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // ── Quick Presets (Only if adding new) ──
            if (!_isEditMode)
            {
                lblPresets = new Label
                {
                    Text = "Quick Presets:",
                    Font = UiKit.T.SmallStrong,
                    ForeColor = UiKit.T.InkMuted,
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Location = new Point(x, y)
                };
                pnlBody.Controls.Add(lblPresets);
                y += 18;

                pnlPresets = new FlowLayoutPanel
                {
                    Location = new Point(x, y),
                    Width = w,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    BackColor = Color.Transparent,
                    WrapContents = true,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                };

                AddPresetChip("Post-Repair Check", "Post-Repair Quality Check", "Check in with customer 48h after pickup to verify computer performance, heat, and service satisfaction.", channel: 0, days: 2);
                AddPresetChip("Quote Approval", "Diagnostic & Estimate Approval", "Follow up with customer regarding diagnostic report, hardware replacement quote, and labor estimate.", channel: 0, days: 1);
                AddPresetChip("Pickup Ready", "Ready for Pickup Reminder", "Remind customer that device repair is completed and available for pickup at the workbench.", channel: 2, days: 1); // SMS
                AddPresetChip("Parts In Stock", "Replacement Parts In Stock", "Notify customer that required replacement component has arrived; schedule drop-off time.", channel: 1, days: 0); // Email
                AddPresetChip("Maintenance", "6-Month Tune-Up Check", "Proactive checkup: recommended thermal repaste, fan dust cleaning, and Windows optimization.", channel: 1, days: 90);

                pnlBody.Controls.Add(pnlPresets);
                y += Math.Max(32, pnlPresets.PreferredSize.Height) + 8;
            }

            int halfW = (w - 14) / 2;

            // ── Customer & Linked Repair (Side-by-Side) ──
            lblCustomer = ModalKit.MakeLabel(pnlBody, "Customer *", x, y);
            lblRepair = ModalKit.MakeLabel(pnlBody, "Linked Repair Order (Optional)", x + halfW + 14, y);
            y += 18;

            cmbCustomer = new ComboBox
            {
                Location = new Point(x, y),
                Size = new Size(halfW, 32),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DropDownWidth = Math.Max(halfW, 460),
                Font = AppTheme.FontInput,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat
            };
            cmbCustomer.SelectedIndexChanged += OnCustomerChanged;
            cmbCustomer.Leave += (s, e) =>
            {
                if (cmbCustomer.SelectedIndex <= 0 && !string.IsNullOrWhiteSpace(cmbCustomer.Text))
                {
                    int idx = cmbCustomer.FindStringExact(cmbCustomer.Text.Trim());
                    if (idx < 0) idx = cmbCustomer.FindString(cmbCustomer.Text.Trim());
                    if (idx >= 0) cmbCustomer.SelectedIndex = idx;
                }
            };
            cmbCustomer.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (cmbCustomer.SelectedIndex <= 0 && !string.IsNullOrWhiteSpace(cmbCustomer.Text))
                    {
                        int idx = cmbCustomer.FindStringExact(cmbCustomer.Text.Trim());
                        if (idx < 0) idx = cmbCustomer.FindString(cmbCustomer.Text.Trim());
                        if (idx >= 0) cmbCustomer.SelectedIndex = idx;
                    }
                }
            };
            pnlBody.Controls.Add(cmbCustomer);

            cmbRepair = new ComboBox
            {
                Location = new Point(x + halfW + 14, y),
                Size = new Size(halfW, 32),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DropDownWidth = Math.Max(halfW, 460),
                Font = AppTheme.FontInput,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat
            };
            cmbRepair.Items.Add("None — General Customer Outreach");
            cmbRepair.SelectedIndex = 0;
            cmbRepair.SelectedIndexChanged += OnRepairChanged;
            cmbRepair.Leave += (s, e) =>
            {
                if (cmbRepair.SelectedIndex < 0 && !string.IsNullOrWhiteSpace(cmbRepair.Text))
                {
                    int idx = cmbRepair.FindStringExact(cmbRepair.Text.Trim());
                    if (idx < 0) idx = cmbRepair.FindString(cmbRepair.Text.Trim());
                    if (idx >= 0) cmbRepair.SelectedIndex = idx;
                }
            };
            cmbRepair.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (cmbRepair.SelectedIndex < 0 && !string.IsNullOrWhiteSpace(cmbRepair.Text))
                    {
                        int idx = cmbRepair.FindStringExact(cmbRepair.Text.Trim());
                        if (idx < 0) idx = cmbRepair.FindString(cmbRepair.Text.Trim());
                        if (idx >= 0) cmbRepair.SelectedIndex = idx;
                    }
                }
            };
            pnlBody.Controls.Add(cmbRepair);

            y += 32;

            // Customer Contact Pill & Error
            lblCustomerContact = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = AppTheme.Primary,
                AutoSize = true,
                MaximumSize = new Size(w, 0),
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
            pnlBody.Controls.Add(lblCustomerContact);

            lblErrorCustomer = ModalKit.MakeErrorLabel(pnlBody, x + halfW + 14, y);
            y += 20;

            // ── Subject * ──
            lblSubject = ModalKit.MakeLabel(pnlBody, "Subject *", x, y);
            y += 18;
            inpSubject = ModalKit.MakeField(pnlBody, x, y, w, "e.g. Post-Repair Quality Check");
            y += 36;
            lblErrorSubject = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 16;

            // ── Notes * (multiline) ──
            lblNotes = ModalKit.MakeLabel(pnlBody, "Follow-Up Notes & Discussion Points *", x, y);
            y += 18;
            inpNotes = ModalKit.MakeField(pnlBody, x, y, w, "Describe the purpose of this follow-up, talking points, or customer requirements...", multiline: true);
            inpNotes.Height = 60;
            y += 64;
            lblErrorNotes = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 16;

            // ── Scheduled For + Channel (Side-by-Side) ──
            lblScheduledAt = ModalKit.MakeLabel(pnlBody, "Scheduled For *", x, y);
            lblChannel = ModalKit.MakeLabel(pnlBody, "Communication Channel *", x + halfW + 14, y);
            y += 18;

            dtpScheduledAt = new DateTimePicker
            {
                Font = AppTheme.FontInput,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd  HH:mm",
                ShowUpDown = true,
                Location = new Point(x, y),
                Size = new Size(halfW, 30)
            };
            pnlBody.Controls.Add(dtpScheduledAt);

            cmbChannel = MakeCombo(x + halfW + 14, y, halfW,
                new[] { "Call (Phone)", "Email", "SMS (Text)", "Visit (In-Store)" }, 0);
            pnlBody.Controls.Add(cmbChannel);

            y += 32;

            // Date Shortcut Chips
            pnlDateChips = new FlowLayoutPanel
            {
                Location = new Point(x, y),
                Width = w,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                WrapContents = true,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            AddDateChip("Today", 0);
            AddDateChip("Tomorrow", 1);
            AddDateChip("+2 Days", 2);
            AddDateChip("+3 Days", 3);
            AddDateChip("+1 Week", 7);
            pnlBody.Controls.Add(pnlDateChips);
            y += Math.Max(28, pnlDateChips.PreferredSize.Height) + 8;

            // ── Status + Assigned To (Side-by-Side) ──
            lblStatus = ModalKit.MakeLabel(pnlBody, "Status", x, y);
            lblAssignedTo = ModalKit.MakeLabel(pnlBody, "Assigned Staff", x + halfW + 14, y);
            y += 18;

            cmbStatus = MakeCombo(x, y, halfW,
                new[] { "Scheduled (Pending)", "Completed", "Cancelled" }, 0);
            cmbStatus.SelectedIndexChanged += (s, e) => UpdateOutcomeVisibility();
            pnlBody.Controls.Add(cmbStatus);

            inpAssignedTo = ModalKit.MakeField(pnlBody, x + halfW + 14, y, halfW, "staff");
            y += 36;

            // ── Outcome Panel (Shown when Completed) ──
            pnlOutcome = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, 82),
                BackColor = Color.Transparent,
                Visible = false
            };

            lblOutcome = new Label
            {
                Text = "Outcome & Resolution Notes (Customer feedback or result)",
                Font = UiKit.T.SmallStrong,
                ForeColor = AppTheme.Success,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            pnlOutcome.Controls.Add(lblOutcome);

            inpOutcome = new TextField
            {
                Location = new Point(0, 18),
                Size = new Size(w, 42),
                PlaceholderText = "e.g. Customer confirmed PC booted smoothly, temperature is cool, gave positive review.",
                Multiline = true
            };
            pnlOutcome.Controls.Add(inpOutcome);

            chkLogInteraction = new CheckBox
            {
                Text = "Also log this completed follow-up in Customer Interactions record",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                Checked = true,
                Location = new Point(2, 64)
            };
            pnlOutcome.Controls.Add(chkLogInteraction);

            pnlBody.Controls.Add(pnlOutcome);

            // ── Bottom Action Buttons ──
            btnCancel = new SaasButton("Cancel", SaasButtonVariant.Secondary);
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnSave = new SaasButton(_isEditMode ? "Update" : "Schedule", SaasButtonVariant.Primary);
            btnSave.Click += async (s, e) => await SaveAsync();

            SaasButton? archive = null;
            if (_isEditMode)
            {
                btnArchive = new SaasButton("Archive", SaasButtonVariant.DangerOutline);
                btnArchive.Click += async (s, e) => await ArchiveAsync();
                archive = btnArchive;
            }

            LayoutFooter(btnSave, btnCancel, archive);

            AcceptButton = btnSave;
            CancelButton = btnCancel;

            Shown += (s, e) => inpSubject.Focus();
        }

        // ═══════════ UI HELPERS ═══════════

        private void AddPresetChip(string title, string subject, string notes, int channel, int days)
        {
            var btn = ModalKit.MakeFlowChip(pnlPresets, title);
            btn.Click += (s, e) =>
            {
                inpSubject.Text = subject;
                inpNotes.Text = notes;
                cmbChannel.SelectedIndex = Math.Clamp(channel, 0, 3);
                dtpScheduledAt.Value = DateTime.Now.Date.AddDays(days).AddHours(14); // 2:00 PM default
            };
        }

        private void AddDateChip(string title, int addDays)
        {
            var btn = ModalKit.MakeFlowChip(pnlDateChips, title);
            btn.Click += (s, e) =>
            {
                dtpScheduledAt.Value = DateTime.Now.Date.AddDays(addDays).AddHours(14);
            };
        }

        private void OnCustomerChanged(object? sender, EventArgs e)
        {
            if (_isInitializing) return;

            int selectedCustIndex = cmbCustomer.SelectedIndex - 1; // 0 is placeholder
            if (selectedCustIndex >= 0 && selectedCustIndex < _customers.Count)
            {
                var cust = _customers[selectedCustIndex];
                PopulateRepairsForCustomer(cust.CustomerId);
                UpdateCustomerContactInfo();
            }
            else
            {
                PopulateRepairsForCustomer(null);
                lblCustomerContact.Text = "";
            }
        }

        private void OnRepairChanged(object? sender, EventArgs e)
        {
            if (_isInitializing) return;

            int selectedCustIndex = cmbCustomer.SelectedIndex - 1;
            if (selectedCustIndex < 0 || selectedCustIndex >= _customers.Count) return;

            var cust = _customers[selectedCustIndex];
            int repIndex = cmbRepair.SelectedIndex - 1;
            if (repIndex >= 0)
            {
                var matchingRepairs = _repairs.Where(r => r.CustomerId == cust.CustomerId).OrderByDescending(r => r.RepairRequestId).ToList();
                if (repIndex < matchingRepairs.Count)
                {
                    var rep = matchingRepairs[repIndex];
                    if (string.IsNullOrWhiteSpace(inpSubject.Text) || inpSubject.Text.StartsWith("Follow-up:"))
                    {
                        inpSubject.Text = $"Follow-up: Repair {rep.RequestNumber} ({rep.DeviceModel})";
                    }
                }
            }
        }

        private void UpdateCustomerContactInfo()
        {
            int selectedCustIndex = cmbCustomer.SelectedIndex - 1;
            if (selectedCustIndex >= 0 && selectedCustIndex < _customers.Count)
            {
                var cust = _customers[selectedCustIndex];
                int activeRepairs = _repairs.Count(r => r.CustomerId == cust.CustomerId && r.Status != 3 && r.Status != 4);
                string contact = !string.IsNullOrWhiteSpace(cust.Phone) ? $"Phone: {cust.Phone}" : "";
                if (!string.IsNullOrWhiteSpace(cust.Email))
                    contact += (string.IsNullOrEmpty(contact) ? "" : "  ·  ") + $"Email: {cust.Email}";
                if (activeRepairs > 0)
                    contact += $"  ·  Active Repairs: {activeRepairs}";

                lblCustomerContact.Text = contact;
            }
            else
            {
                lblCustomerContact.Text = "";
            }
        }

        private void UpdateOutcomeVisibility()
        {
            bool isCompleted = cmbStatus.SelectedIndex == 1;
            pnlOutcome.Visible = isCompleted;
        }

        // ═══════════ VALIDATION & SAVE ═══════════

        private bool ValidateInputs()
        {
            ClearErrors();
            bool valid = true;
            TextField? firstInvalid = null;

            if (cmbCustomer.SelectedIndex <= 0)
            {
                ShowError(lblErrorCustomer, "Please select a customer for this follow-up.");
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(inpSubject.Text))
            {
                ModalKit.ShowError(inpSubject, lblErrorSubject, "Subject is required.");
                firstInvalid ??= inpSubject;
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(inpNotes.Text))
            {
                ModalKit.ShowError(inpNotes, lblErrorNotes, "Notes are required.");
                firstInvalid ??= inpNotes;
                valid = false;
            }

            if (!valid)
            {
                if (firstInvalid != null) firstInvalid.Focus();
                else cmbCustomer.Focus();
            }
            return valid;
        }

        private void ClearErrors()
        {
            lblErrorCustomer.Text = "";
            lblErrorCustomer.Visible = false;
            ModalKit.ClearError(inpSubject, lblErrorSubject);
            ModalKit.ClearError(inpNotes, lblErrorNotes);
        }

        private static void ShowError(Label lbl, string msg)
        {
            lbl.Text = msg;
            lbl.Visible = true;
        }

        private async Task SaveAsync()
        {
            if (!ValidateInputs()) return;

            btnSave.Enabled = false;
            btnSave.Text = "Saving...";

            try
            {
                int? customerId = null;
                int custIdx = cmbCustomer.SelectedIndex - 1;
                if (custIdx >= 0 && custIdx < _customers.Count)
                    customerId = _customers[custIdx].CustomerId;

                int? repairId = null;
                int repIdx = cmbRepair.SelectedIndex - 1;
                if (customerId.HasValue && repIdx >= 0)
                {
                    var matching = _repairs.Where(r => r.CustomerId == customerId.Value).OrderByDescending(r => r.RepairRequestId).ToList();
                    if (repIdx < matching.Count)
                        repairId = matching[repIdx].RepairRequestId;
                }

                string notes = inpNotes.Text.Trim();
                if (cmbStatus.SelectedIndex == 1 && !string.IsNullOrWhiteSpace(inpOutcome.Text))
                {
                    string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                    notes += $"\n\n[Outcome {stamp}]: {inpOutcome.Text.Trim()}";
                }

                var dto = new FollowUpDto
                {
                    CustomerId = customerId,
                    RepairRequestId = repairId,
                    Subject = inpSubject.Text.Trim(),
                    Notes = notes,
                    ScheduledAt = dtpScheduledAt.Value,
                    Channel = Math.Clamp(cmbChannel.SelectedIndex, 0, 3),
                    Status = Math.Clamp(cmbStatus.SelectedIndex, 0, 2),
                    AssignedToUserId = string.IsNullOrWhiteSpace(inpAssignedTo.Text) ? UserSession.Username : inpAssignedTo.Text.Trim()
                };

                if (_isEditMode && _editing != null)
                {
                    dto.FollowUpId = _editing.FollowUpId;
                    await _api.UpdateFollowUpAsync(_editing.FollowUpId, dto);
                }
                else
                {
                    var created = await _api.CreateFollowUpAsync(dto);
                    if (created != null && dto.Status == 1 && chkLogInteraction.Checked && customerId.HasValue)
                    {
                        // Also log as interaction
                        try
                        {
                            await _api.CreateInteractionAsync(new InteractionDto
                            {
                                CustomerId = customerId.Value,
                                RepairRequestId = repairId,
                                InteractionType = 0, // Inquiry
                                Subject = $"Follow-up: {dto.Subject}",
                                Notes = string.IsNullOrWhiteSpace(inpOutcome.Text) ? dto.Notes : inpOutcome.Text.Trim(),
                                Priority = 1, // Medium
                                Status = 2,   // Closed
                                Resolution = "Follow-up completed successfully."
                            });
                        }
                        catch { /* non-blocking */ }
                    }
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSave.Enabled = true;
                btnSave.Text = _isEditMode ? "Update" : "Schedule";
            }
        }

        // ═══════════ ARCHIVE ═══════════

        private async Task ArchiveAsync()
        {
            if (_editing == null) return;

            var confirm = MessageBox.Show(
                $"Archive this follow-up?\n\n\"{_editing.Subject}\"\n\nData is preserved and can be restored later.",
                "Confirm Archive",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                await _api.ArchiveFollowUpAsync(_editing.FollowUpId);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Archive failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═══════════ CONTROL FACTORIES ═══════════







        private ComboBox MakeCombo(int x, int y, int width, string[] items, int selectedIndex)
        {
            var cmb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DropDownWidth = Math.Max(width, 300),
                Font = AppTheme.FontInput,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(x, y),
                Size = new Size(width, 36)
            };
            cmb.Items.AddRange(items);
            if (selectedIndex >= 0 && selectedIndex < items.Length)
                cmb.SelectedIndex = selectedIndex;

            cmb.Leave += (s, e) =>
            {
                if (cmb.SelectedIndex < 0 && !string.IsNullOrWhiteSpace(cmb.Text))
                {
                    int idx = cmb.FindStringExact(cmb.Text.Trim());
                    if (idx < 0) idx = cmb.FindString(cmb.Text.Trim());
                    if (idx >= 0) cmb.SelectedIndex = idx;
                }
            };
            cmb.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (cmb.SelectedIndex < 0 && !string.IsNullOrWhiteSpace(cmb.Text))
                    {
                        int idx = cmb.FindStringExact(cmb.Text.Trim());
                        if (idx < 0) idx = cmb.FindString(cmb.Text.Trim());
                        if (idx >= 0) cmb.SelectedIndex = idx;
                    }
                }
            };

            pnlBody.Controls.Add(cmb);
            return cmb;
        }
    }
}
