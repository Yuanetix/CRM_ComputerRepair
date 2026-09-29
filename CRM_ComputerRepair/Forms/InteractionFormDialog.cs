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
    /// Modal dialog for Add / Edit on an Interaction (Question, Concern, Review).
    /// Provides computer repair industry standard templates, customer selection,
    /// dynamic linked repair orders, priority/status management, and resolution tracking.
    /// </summary>
    [DesignerCategory("Code")]
    public class InteractionFormDialog : ModalForm
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private readonly InteractionDto? _editing;
        private readonly bool _isEditMode;
        private readonly InteractionTypeFilter? _presetType;

        private List<CustomerDto> _customers = new();
        private List<RepairRequestDto> _repairs = new();
        private bool _isInitializing = true;

        // ═══════════ CONTROLS ═══════════

        // Presets
        private Label lblPresets = null!;
        private FlowLayoutPanel pnlPresets = null!;

        // Customer & Repair
        private Label lblCustomer = null!;
        private Label lblRepair = null!;
        private ComboBox cmbCustomer = null!;
        private ComboBox cmbRepair = null!;
        private Label lblCustomerContact = null!;

        // Type & Priority
        private Label lblType = null!;
        private Label lblPriority = null!;
        private ComboBox cmbType = null!;
        private ComboBox cmbPriority = null!;

        // Subject & Notes
        private Label lblSubject = null!;
        private Label lblNotes = null!;
        private TextField inpSubject = null!;
        private TextField inpNotes = null!;

        // Status & Resolution
        private Label lblStatus = null!;
        private ComboBox cmbStatus = null!;
        private Label lblResolution = null!;
        private TextField inpResolution = null!;

        // Error labels
        private Label lblErrorSubject = null!;
        private Label lblErrorNotes = null!;
        private Label lblErrorResolution = null!;

        // Buttons
        private SaasButton btnSave = null!;
        private SaasButton btnCancel = null!;
        private SaasButton btnArchive = null!;

        // ═══════════ CONSTRUCTOR ═══════════

        public InteractionFormDialog(
            InteractionDto? existing = null,
            InteractionTypeFilter? presetType = null,
            List<CustomerDto>? cachedCustomers = null,
            List<RepairRequestDto>? cachedRepairs = null)
        {
            _editing = existing;
            _isEditMode = existing != null && existing.CustomerInteractionId > 0;
            _presetType = presetType;

            if (cachedCustomers != null) _customers = new List<CustomerDto>(cachedCustomers);
            if (cachedRepairs != null) _repairs = new List<RepairRequestDto>(cachedRepairs);

            BuildCard(
                _isEditMode ? "Edit Interaction" : "Log Customer Interaction",
                _isEditMode
                    ? "Update the interaction details and resolution notes below."
                    : "Log a customer question, concern, or feedback review. Fields marked * are required.",
                width: 660,
                height: 720);

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
                MessageBox.Show($"Failed to load customer information:\n{ex.Message}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void PopulateCustomers()
        {
            cmbCustomer.Items.Clear();
            cmbCustomer.Items.Add("— Walk-in / Unlinked Customer —");

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
            cmbRepair.Items.Add("None — General Support / Inquiry");

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
                        cmbCustomer.SelectedIndex = custIdx + 1; // +1 for "Walk-in"
                }

                // Type
                cmbType.SelectedIndex = Clamp(_editing.InteractionType, 0, 2);

                // Priority & Status
                cmbPriority.SelectedIndex = Clamp(_editing.Priority, 0, 2);
                cmbStatus.SelectedIndex = Clamp(_editing.Status, 0, 2);

                // Subject & Notes
                inpSubject.Text = _editing.Subject ?? "";
                inpNotes.Text = _editing.Notes ?? "";
                inpResolution.Text = _editing.Resolution ?? "";

                // Repair selection
                if (_editing.RepairRequestId.HasValue)
                {
                    int? custId = _editing.CustomerId;
                    var matching = _repairs
                        .Where(r => !custId.HasValue || r.CustomerId == custId.Value)
                        .OrderByDescending(r => r.RepairRequestId)
                        .ToList();

                    int repIdx = matching.FindIndex(r => r.RepairRequestId == _editing.RepairRequestId.Value);
                    if (repIdx >= 0)
                        cmbRepair.SelectedIndex = repIdx + 1;
                }
            }
            else if (_presetType.HasValue)
            {
                cmbType.SelectedIndex = (int)_presetType.Value;
            }

            UpdateResolutionState();
        }

        // ═══════════ CONTENT BUILD ═══════════

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // ── Quick Presets (Only in Add mode) ──
            if (!_isEditMode)
            {
                lblPresets = ModalKit.MakeLabel(pnlBody, "Quick Presets (Computer Repair Standard):", x, y);
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

                AddPresetChip("Turnaround Inquiry", 0, "Turnaround Time & Status Inquiry",
                    "Customer inquired regarding the estimated completion time and current diagnostic progress for their repair.");

                AddPresetChip("Quote Approval", 0, "Hardware Estimate / Quote Approval Inquiry",
                    "Customer reached out regarding the cost estimate for parts and labor. Reviewing repair authorization.");

                AddPresetChip("Post-Repair Concern", 1, "Post-Repair Hardware Concern",
                    "Customer called regarding hardware behavior after recent pickup. Documented symptoms for technician evaluation.");

                AddPresetChip("Customer Review", 2, "Customer Satisfaction & Praise",
                    "Customer expressed high satisfaction with repair turnaround time, communication, and system performance.");

                pnlBody.Controls.Add(pnlPresets);
                y += Math.Max(32, pnlPresets.PreferredSize.Height) + 8;
            }

            // ── Customer & Linked Repair (Side-by-side) ──
            int halfW = (w - 14) / 2;

            lblCustomer = ModalKit.MakeLabel(pnlBody, "Customer", x, y);
            lblRepair = ModalKit.MakeLabel(pnlBody, "Linked Repair Order", x + halfW + 14, y);
            y += 20;

            cmbCustomer = MakeCombo(pnlBody, x, y, halfW, new[] { "Loading customers..." }, 0);
            cmbRepair = MakeCombo(pnlBody, x + halfW + 14, y, halfW, new[] { "None — General Support / Inquiry" }, 0);
            y += 34;

            lblCustomerContact = new Label
            {
                Text = "Walk-in inquiry or select a customer above",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                MaximumSize = new Size(w, 0),
                Location = new Point(x + 2, y)
            };
            pnlBody.Controls.Add(lblCustomerContact);
            y += 22;

            cmbCustomer.SelectedIndexChanged += (s, e) =>
            {
                if (_isInitializing) return;

                if (cmbCustomer.SelectedIndex > 0 && cmbCustomer.SelectedIndex - 1 < _customers.Count)
                {
                    var cust = _customers.OrderBy(c => c.FullName).ToList()[cmbCustomer.SelectedIndex - 1];
                    string contact = !string.IsNullOrWhiteSpace(cust.Phone) ? cust.Phone : (!string.IsNullOrWhiteSpace(cust.Email) ? cust.Email : "No contact saved");
                    lblCustomerContact.Text = $"Selected: {cust.FullName}  ·  {contact}  ·  Points: {cust.LoyaltyPoints ?? 0}";
                    lblCustomerContact.ForeColor = AppTheme.Primary;
                    PopulateRepairsForCustomer(cust.CustomerId);
                }
                else
                {
                    lblCustomerContact.Text = "Walk-in inquiry or unlinked customer";
                    lblCustomerContact.ForeColor = UiKit.T.InkMuted;
                    PopulateRepairsForCustomer(null);
                }
            };

            // ── Type & Priority (Side-by-side) ──
            lblType = ModalKit.MakeLabel(pnlBody, "Interaction Type *", x, y);
            lblPriority = ModalKit.MakeLabel(pnlBody, "Priority", x + halfW + 14, y);
            y += 20;

            cmbType = MakeCombo(pnlBody, x, y, halfW, new[] { "Question (Inquiry)", "Concern (Complaint)", "Review (Feedback)" }, 0);
            cmbPriority = MakeCombo(pnlBody, x + halfW + 14, y, halfW, new[] { "Low", "Medium", "High" }, 1);
            y += 34 + 10;

            // ── Subject * ──
            lblSubject = ModalKit.MakeLabel(pnlBody, "Subject *", x, y);
            y += 20;
            inpSubject = ModalKit.MakeField(pnlBody, x, y, w, "Brief summary of customer inquiry, concern, or feedback...");
            inpSubject.Height = 36;
            y += 38;
            lblErrorSubject = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 18;

            // ── Notes (multiline) * ──
            lblNotes = ModalKit.MakeLabel(pnlBody, "Discussion Notes & Conversation Details *", x, y);
            y += 20;
            inpNotes = ModalKit.MakeField(pnlBody, x, y, w, "Detail the conversation, symptoms described, or feedback provided...", multiline: true);
            inpNotes.Height = 74;
            y += 76;
            lblErrorNotes = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 18;

            // ── Status & Resolution ──
            lblStatus = ModalKit.MakeLabel(pnlBody, "Status", x, y);
            y += 20;
            cmbStatus = MakeCombo(pnlBody, x, y, 160, new[] { "Open", "In Progress", "Closed" }, 0);
            cmbStatus.SelectedIndexChanged += (s, e) => UpdateResolutionState();
            y += 34 + 10;

            lblResolution = ModalKit.MakeLabel(pnlBody, "Resolution Details (Required when Status is Closed)", x, y);
            y += 20;
            inpResolution = ModalKit.MakeField(pnlBody, x, y, w, "Explain how the issue or inquiry was resolved...", multiline: true);
            inpResolution.Height = 68;
            y += 70;
            lblErrorResolution = ModalKit.MakeErrorLabel(pnlBody, x, y);

            // ── Buttons ──
            btnCancel = new SaasButton("Cancel", SaasButtonVariant.Secondary);
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnSave = new SaasButton(_isEditMode ? "Update" : "Save Interaction", SaasButtonVariant.Primary);
            btnSave.Click += async (s, e) => await SaveAsync();

            SaasButton? archive = null;
            if (_isEditMode)
            {
                btnArchive = new SaasButton("Archive", SaasButtonVariant.DangerOutline);
                btnArchive.Click += async (s, e) => await ArchiveAsync();
                archive = btnArchive;
            }
            LayoutFooter(btnSave, btnCancel, archive, saveW: 140);

            AcceptButton = btnSave;
            CancelButton = btnCancel;

            Shown += (s, e) => inpSubject.Focus();
        }

        private void AddPresetChip(string title, int typeIdx, string subject, string notes)
        {
            var btn = ModalKit.MakeFlowChip(pnlPresets, title);
            btn.Click += (s, e) =>
            {
                cmbType.SelectedIndex = typeIdx;
                inpSubject.Text = subject;
                inpNotes.Text = notes;
                inpNotes.Focus();
                inpNotes.InnerTextBox.SelectionStart = inpNotes.Text.Length;
            };
        }

        private static int Clamp(int value, int min, int max)
            => value < min ? min : (value > max ? max : value);

        private void UpdateResolutionState()
        {
            bool isClosed = cmbStatus.SelectedIndex == 2;
            lblResolution.ForeColor = isClosed ? AppTheme.TextSecondary : AppTheme.TextMuted;
            inpResolution.InnerTextBox.Enabled = isClosed;

            if (!isClosed)
            {
                ClearError(inpResolution, lblErrorResolution);
            }
        }

        // ═══════════ SAVE ═══════════

        private async Task SaveAsync()
        {
            if (!ValidateInputs()) return;

            int? selectedCustomerId = null;
            if (cmbCustomer.SelectedIndex > 0 && cmbCustomer.SelectedIndex - 1 < _customers.Count)
            {
                selectedCustomerId = _customers.OrderBy(c => c.FullName).ToList()[cmbCustomer.SelectedIndex - 1].CustomerId;
            }

            int? selectedRepairId = null;
            if (cmbRepair.SelectedIndex > 0)
            {
                var matchingRepairs = _repairs
                    .Where(r => !selectedCustomerId.HasValue || r.CustomerId == selectedCustomerId.Value)
                    .OrderByDescending(r => r.RepairRequestId)
                    .ToList();

                if (cmbRepair.SelectedIndex - 1 < matchingRepairs.Count)
                {
                    selectedRepairId = matchingRepairs[cmbRepair.SelectedIndex - 1].RepairRequestId;
                }
            }

            btnSave.Enabled = false;
            btnSave.Text = "Saving...";

            try
            {
                var dto = new InteractionDto
                {
                    CustomerId = selectedCustomerId,
                    RepairRequestId = selectedRepairId,
                    InteractionType = cmbType.SelectedIndex,
                    Subject = inpSubject.Text.Trim(),
                    Notes = inpNotes.Text.Trim(),
                    Priority = cmbPriority.SelectedIndex,
                    Status = cmbStatus.SelectedIndex,
                    Resolution = string.IsNullOrWhiteSpace(inpResolution.Text)
                        ? null
                        : inpResolution.Text.Trim()
                };

                if (_isEditMode && _editing != null)
                {
                    dto.CustomerInteractionId = _editing.CustomerInteractionId;
                    await _api.UpdateInteractionAsync(_editing.CustomerInteractionId, dto);
                }
                else
                {
                    await _api.CreateInteractionAsync(dto);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Save failed:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                btnSave.Enabled = true;
                btnSave.Text = _isEditMode ? "Update" : "Save Interaction";
            }
        }

        // ═══════════ ARCHIVE ═══════════

        private async Task ArchiveAsync()
        {
            if (_editing == null) return;

            var confirm = MessageBox.Show(
                "Archive this interaction?\n\n" +
                $"\"{_editing.Subject}\"\n\n" +
                "Data is preserved and can be restored.",
                "Confirm Archive",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                await _api.ArchiveInteractionAsync(_editing.CustomerInteractionId);
                DialogResult = DialogResult.OK;
                Close();
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

        // ═══════════ VALIDATION ═══════════

        private bool ValidateInputs()
        {
            ClearErrors();
            bool valid = true;
            Control? firstInvalid = null;

            if (cmbType.SelectedIndex < 0)
            {
                MessageBox.Show("Please choose an interaction type.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(inpSubject.Text))
            {
                ShowError(inpSubject, lblErrorSubject, "Subject is required.");
                firstInvalid ??= inpSubject;
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(inpNotes.Text))
            {
                ShowError(inpNotes, lblErrorNotes, "Discussion notes are required.");
                firstInvalid ??= inpNotes;
                valid = false;
            }

            if (cmbStatus.SelectedIndex == 2 &&
                string.IsNullOrWhiteSpace(inpResolution.Text))
            {
                ShowError(inpResolution, lblErrorResolution,
                    "Resolution details are required when closing an interaction.");
                firstInvalid ??= inpResolution;
                valid = false;
            }

            firstInvalid?.Focus();
            return valid;
        }

        private void ClearErrors()
        {
            ClearError(inpSubject, lblErrorSubject);
            ClearError(inpNotes, lblErrorNotes);
            ClearError(inpResolution, lblErrorResolution);
        }

        private static void ShowError(TextField input, Label err, string msg)
        {
            input.HasError = true;
            err.Text = msg;
            err.Visible = true;
        }

        private static void ClearError(TextField input, Label err)
        {
            input.HasError = false;
            err.Text = "";
            err.Visible = false;
        }

        // ═══════════ CONTROL FACTORIES ═══════════



        private ComboBox MakeCombo(Control parent, int x, int y, int width, string[] items, int selectedIndex)
        {
            var cmb = new ComboBox
            {
                Font = AppTheme.FontInput,
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DropDownWidth = Math.Max(width, 420),
                Location = new Point(x, y),
                Size = new Size(width, 30),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat
            };
            cmb.Items.AddRange(items);
            if (selectedIndex >= 0 && selectedIndex < items.Length)
                cmb.SelectedIndex = selectedIndex;

            cmb.Leave += (s, e) =>
            {
                if (cmb.SelectedIndex <= 0 && !string.IsNullOrWhiteSpace(cmb.Text))
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
                    if (cmb.SelectedIndex <= 0 && !string.IsNullOrWhiteSpace(cmb.Text))
                    {
                        int idx = cmb.FindStringExact(cmb.Text.Trim());
                        if (idx < 0) idx = cmb.FindString(cmb.Text.Trim());
                        if (idx >= 0) cmb.SelectedIndex = idx;
                    }
                }
            };

            parent.Controls.Add(cmb);
            return cmb;
        }




    }
}
