using CRM.winforms.Auth;
using CRM.winforms.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Modal dialog for Add / Edit on a Repair Request.
    /// Provides computer repair industry standard customer selection, device templates,
    /// common issue presets, parts and labor cost calculations, and technician notes.
    /// </summary>
    [DesignerCategory("Code")]
    public class RepairRequestFormDialog : ModalForm
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private readonly RepairRequestDto? _editing;
        private readonly bool _isEditMode;
        private readonly int? _prefillCustomerId;

        private List<CustomerDto> _customers = new();
        private bool _isInitializing = true;

        // ═══════════ CONTROLS ═══════════

        // Customer
        private Label lblCustomer = null!;
        private ComboBox cmbCustomer = null!;
        private Label lblCustomerContact = null!;
        private Label lblErrorCustomer = null!;

        // Device
        private Label lblDevicePresets = null!;
        private FlowLayoutPanel pnlDevicePresets = null!;
        private Label lblDeviceModel = null!;
        private Label lblSerialNumber = null!;
        private TextField inpDeviceModel = null!;
        private TextField inpSerialNumber = null!;
        private Label lblErrorDeviceModel = null!;

        // Issue
        private Label lblIssuePresets = null!;
        private FlowLayoutPanel pnlIssuePresets = null!;
        private Label lblIssue = null!;
        private TextField inpIssue = null!;
        private Label lblErrorIssue = null!;

        // Priority & Status
        private Label lblPriority = null!;
        private Label lblStatus = null!;
        private ComboBox cmbPriority = null!;
        private ComboBox cmbStatus = null!;

        // Costs
        private Label lblEstCost = null!;
        private Label lblActualCost = null!;
        private TextField inpEstCost = null!;
        private TextField inpActualCost = null!;

        // Technician Notes
        private Label lblTechnicianNotes = null!;
        private TextField inpTechnicianNotes = null!;

        // Actions
        private SaasButton btnSave = null!;
        private SaasButton btnCancel = null!;

        // ═══════════ CONSTRUCTOR ═══════════

        public RepairRequestFormDialog(
            RepairRequestDto? existing = null,
            int? prefillCustomerId = null,
            List<CustomerDto>? cachedCustomers = null)
        {
            _editing = existing;
            _isEditMode = existing != null && existing.RepairRequestId > 0;
            _prefillCustomerId = prefillCustomerId;

            if (cachedCustomers != null)
                _customers = new List<CustomerDto>(cachedCustomers);

            BuildCard(
                _isEditMode ? $"Edit Repair ({_editing!.RequestNumber})" : "Intake New Repair Request",
                _isEditMode
                    ? "Update hardware diagnostics, repair status, parts/labor costs, and technician notes."
                    : "Intake ticket for computer hardware, diagnostic findings, and initial repair estimate.",
                width: 680,
                height: 820);

            BuildContent();

            this.Load += async (s, e) => await InitializeDataAsync();
        }

        // ═══════════ DATA INITIALIZATION ═══════════

        private async Task InitializeDataAsync()
        {
            try
            {
                if (_customers.Count == 0)
                {
                    _customers = await _api.GetCustomersAsync() ?? new List<CustomerDto>();
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
            cmbCustomer.Items.Add("— Select a Customer * —");

            foreach (var c in _customers.OrderBy(x => x.FullName))
            {
                string contact = !string.IsNullOrWhiteSpace(c.Phone) ? c.Phone : (!string.IsNullOrWhiteSpace(c.Email) ? c.Email : "No contact");
                cmbCustomer.Items.Add($"{c.FullName} ({contact})");
            }

            if (cmbCustomer.Items.Count > 0)
                cmbCustomer.SelectedIndex = 0;

            ModalKit.AdjustDropDownWidth(cmbCustomer);
        }

        private void ApplyPrefill()
        {
            if (_editing != null)
            {
                // Customer selection
                int custIdx = _customers.FindIndex(c => c.CustomerId == _editing.CustomerId);
                if (custIdx >= 0)
                    cmbCustomer.SelectedIndex = custIdx + 1;

                inpDeviceModel.Text = _editing.DeviceModel ?? "";
                inpSerialNumber.Text = _editing.SerialNumber ?? "";
                inpIssue.Text = _editing.IssueDescription ?? "";

                cmbPriority.SelectedIndex = Clamp(_editing.Priority, 0, 3);
                cmbStatus.SelectedIndex = Clamp(_editing.Status, 0, 5);

                if (_editing.EstimatedCost.HasValue && _editing.EstimatedCost > 0)
                    inpEstCost.Text = _editing.EstimatedCost.Value.ToString("F2");
                if (_editing.ActualCost.HasValue && _editing.ActualCost > 0)
                    inpActualCost.Text = _editing.ActualCost.Value.ToString("F2");

                inpTechnicianNotes.Text = _editing.TechnicianNotes ?? "";
            }
            else if (_prefillCustomerId.HasValue)
            {
                int custIdx = _customers.FindIndex(c => c.CustomerId == _prefillCustomerId.Value);
                if (custIdx >= 0)
                    cmbCustomer.SelectedIndex = custIdx + 1;
            }
        }

        // ═══════════ CONTENT BUILD ═══════════

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // ── Customer Selector ──
            lblCustomer = ModalKit.MakeLabel(pnlBody, "Customer *", x, y);
            y += 18;

            cmbCustomer = MakeCombo(x, y, w, new[] { "Loading registered customers..." }, 0);
            pnlBody.Controls.Add(cmbCustomer);
            y += 34;

            lblCustomerContact = new Label
            {
                Text = "Choose the customer who owns this computer or device",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                MaximumSize = new Size(w, 0),
                Location = new Point(x + 2, y)
            };
            pnlBody.Controls.Add(lblCustomerContact);

            lblErrorCustomer = ModalKit.MakeErrorLabel(pnlBody, x + 320, y);
            y += 20;

            cmbCustomer.SelectedIndexChanged += (s, e) =>
            {
                if (_isInitializing) return;
                ClearError(lblErrorCustomer);

                if (cmbCustomer.SelectedIndex > 0 && cmbCustomer.SelectedIndex - 1 < _customers.Count)
                {
                    var cust = _customers.OrderBy(c => c.FullName).ToList()[cmbCustomer.SelectedIndex - 1];
                    string contact = !string.IsNullOrWhiteSpace(cust.Phone) ? cust.Phone : (!string.IsNullOrWhiteSpace(cust.Email) ? cust.Email : "No contact saved");
                    lblCustomerContact.Text = $"Selected: {cust.FullName}  ·  {contact}  ·  Loyalty Points: {cust.LoyaltyPoints ?? 0}";
                    lblCustomerContact.ForeColor = AppTheme.Primary;
                }
                else
                {
                    lblCustomerContact.Text = "Please select a registered customer";
                    lblCustomerContact.ForeColor = UiKit.T.InkMuted;
                }
            };

            // ── Device Presets (Only in Add mode) ──
            if (!_isEditMode)
            {
                lblDevicePresets = ModalKit.MakeLabel(pnlBody, "Quick Device Presets:", x, y);
                y += 18;

                pnlDevicePresets = new FlowLayoutPanel
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

                AddPresetChip(pnlDevicePresets, "Desktop PC", () => inpDeviceModel.Text = "Desktop Tower PC (ATX)");
                AddPresetChip(pnlDevicePresets, "Laptop / Notebook", () => inpDeviceModel.Text = "Laptop / Notebook");
                AddPresetChip(pnlDevicePresets, "MacBook / iMac", () => inpDeviceModel.Text = "Apple MacBook / iMac");
                AddPresetChip(pnlDevicePresets, "Gaming PC", () => inpDeviceModel.Text = "Custom Gaming Rig");
                AddPresetChip(pnlDevicePresets, "All-In-One", () => inpDeviceModel.Text = "All-In-One PC");

                pnlBody.Controls.Add(pnlDevicePresets);
                y += Math.Max(32, pnlDevicePresets.PreferredSize.Height) + 8;
            }

            // ── Device Model & Serial Number (Side-by-side) ──
            int halfW = (w - 14) / 2;

            lblDeviceModel = ModalKit.MakeLabel(pnlBody, "Device Model / Specs *", x, y);
            lblSerialNumber = ModalKit.MakeLabel(pnlBody, "Serial Number / Service Tag", x + halfW + 14, y);
            y += 18;

            inpDeviceModel = ModalKit.MakeField(pnlBody, x, y, halfW, "e.g. Dell Latitude 5420 / i7 16GB");
            inpDeviceModel.Height = 36;
            inpSerialNumber = ModalKit.MakeField(pnlBody, x + halfW + 14, y, halfW, "e.g. SN-8F92A1 / Service Tag");
            inpSerialNumber.Height = 36;
            y += 38;

            lblErrorDeviceModel = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 16;

            // ── Issue Presets (Only in Add mode) ──
            if (!_isEditMode)
            {
                lblIssuePresets = ModalKit.MakeLabel(pnlBody, "Common Repair Issues:", x, y);
                y += 18;

                pnlIssuePresets = new FlowLayoutPanel
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

                AddPresetChip(pnlIssuePresets, "No Power / Won't Turn On", () => AppendIssue("Unit completely dead, no power LED or fan spin. Possible PSU or motherboard issue."));
                AddPresetChip(pnlIssuePresets, "Boot Failure / BSOD", () => AppendIssue("System boots into blue screen or automatic repair loop. Windows OS / drive failure."));
                AddPresetChip(pnlIssuePresets, "Broken Screen / Lines", () => AppendIssue("LCD panel cracked or flickering display lines. Requires screen replacement."));
                AddPresetChip(pnlIssuePresets, "Overheating / Fan Noise", () => AppendIssue("High thermal throttling, loud fan noise, system shuts down under load. Clean & repaste."));
                AddPresetChip(pnlIssuePresets, "Malware / Clean OS", () => AppendIssue("Severe malware infection, slow performance. Customer requested full backup and clean OS install."));

                pnlBody.Controls.Add(pnlIssuePresets);
                y += Math.Max(32, pnlIssuePresets.PreferredSize.Height) + 8;
            }

            // ── Issue Description * ──
            lblIssue = ModalKit.MakeLabel(pnlBody, "Issue Description & Customer Problem Statement *", x, y);
            y += 18;
            inpIssue = ModalKit.MakeField(pnlBody, x, y, w, "Detail the customer's reported symptoms, error codes, and condition upon drop-off...", multiline: true);
            inpIssue.Height = 64;
            y += 66;
            lblErrorIssue = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 16;

            // ── Priority & Status (Side-by-side) ──
            lblPriority = ModalKit.MakeLabel(pnlBody, "Priority", x, y);
            lblStatus = ModalKit.MakeLabel(pnlBody, "Repair Status", x + halfW + 14, y);
            y += 18;

            cmbPriority = MakeCombo(x, y, halfW, new[] { "Low", "Medium", "High", "Urgent" }, 1);
            pnlBody.Controls.Add(cmbPriority);
            cmbStatus = MakeCombo(x + halfW + 14, y, halfW, new[] { "Pending", "Approved", "In Progress", "Completed", "Rejected", "Reassigned" }, 0);
            pnlBody.Controls.Add(cmbStatus);
            y += 34 + 8;

            // ── Estimated Cost & Actual Cost ──
            lblEstCost = ModalKit.MakeLabel(pnlBody, "Estimated Cost (₱)", x, y);
            lblActualCost = ModalKit.MakeLabel(pnlBody, "Actual Cost (₱)", x + halfW + 14, y);
            y += 18;

            inpEstCost = ModalKit.MakeField(pnlBody, x, y, halfW, "0.00");
            inpEstCost.Height = 34;
            inpActualCost = ModalKit.MakeField(pnlBody, x + halfW + 14, y, halfW, "0.00");
            inpActualCost.Height = 34;
            y += 36 + 8;

            // ── Technician Notes ──
            lblTechnicianNotes = ModalKit.MakeLabel(pnlBody, "Technician Bench Notes & Diagnostic Log", x, y);
            y += 18;
            inpTechnicianNotes = ModalKit.MakeField(pnlBody, x, y, w, "Document diagnostic findings, replaced components, stress testing results...", multiline: true);
            inpTechnicianNotes.Height = 60;
            y += 62;

            // ── Buttons ──
            btnCancel = new SaasButton("Cancel", SaasButtonVariant.Secondary);
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnSave = new SaasButton(_isEditMode ? "Update Repair" : "Create Repair Ticket", SaasButtonVariant.Primary);
            btnSave.Click += async (s, e) => await SaveAsync();

            LayoutFooter(btnSave, btnCancel, saveW: 140);

            AcceptButton = btnSave;
            CancelButton = btnCancel;

            Shown += (s, e) =>
            {
                if (_isEditMode) inpDeviceModel.Focus();
                else cmbCustomer.Focus();
            };
        }

        private void AddPresetChip(FlowLayoutPanel panel, string title, Action onSelect)
        {
            var btn = ModalKit.MakeFlowChip(panel, title);
            btn.Click += (s, e) => onSelect();
        }

        private void AppendIssue(string text)
        {
            if (string.IsNullOrWhiteSpace(inpIssue.Text))
                inpIssue.Text = text;
            else
                inpIssue.Text = inpIssue.Text.TrimEnd() + " " + text;
            inpIssue.Focus();
            inpIssue.InnerTextBox.SelectionStart = inpIssue.Text.Length;
        }

        private static int Clamp(int value, int min, int max)
            => value < min ? min : (value > max ? max : value);

        // ═══════════ SAVE ═══════════

        private async Task SaveAsync()
        {
            if (!ValidateInputs()) return;

            int selectedCustomerId = _customers.OrderBy(c => c.FullName).ToList()[cmbCustomer.SelectedIndex - 1].CustomerId;

            decimal.TryParse(inpEstCost.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal estCost);
            decimal.TryParse(inpActualCost.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal actualCost);

            btnSave.Enabled = false;
            btnSave.Text = "Saving...";

            try
            {
                var dto = new RepairRequestDto
                {
                    CustomerId = selectedCustomerId,
                    DeviceModel = inpDeviceModel.Text.Trim(),
                    SerialNumber = inpSerialNumber.Text.Trim(),
                    IssueDescription = inpIssue.Text.Trim(),
                    Priority = cmbPriority.SelectedIndex,
                    Status = cmbStatus.SelectedIndex,
                    EstimatedCost = estCost > 0 ? estCost : (decimal?)null,
                    ActualCost = actualCost > 0 ? actualCost : (decimal?)null,
                    TechnicianNotes = string.IsNullOrWhiteSpace(inpTechnicianNotes.Text)
                        ? null
                        : inpTechnicianNotes.Text.Trim()
                };

                if (_isEditMode && _editing != null)
                {
                    dto.RepairRequestId = _editing.RepairRequestId;
                    dto.RequestNumber = _editing.RequestNumber;
                    await _api.UpdateRepairRequestAsync(_editing.RepairRequestId, dto);
                }
                else
                {
                    await _api.CreateRepairRequestAsync(dto);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save repair request:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSave.Enabled = true;
                btnSave.Text = _isEditMode ? "Update Repair" : "Create Repair Ticket";
            }
        }

        // ═══════════ VALIDATION ═══════════

        private bool ValidateInputs()
        {
            ClearErrors();
            bool valid = true;
            Control? firstInvalid = null;

            if (cmbCustomer.SelectedIndex <= 0)
            {
                lblErrorCustomer.Text = "Please select a registered customer.";
                lblErrorCustomer.Visible = true;
                firstInvalid ??= cmbCustomer;
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(inpDeviceModel.Text))
            {
                ShowError(inpDeviceModel, lblErrorDeviceModel, "Device model / computer specs are required.");
                firstInvalid ??= inpDeviceModel;
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(inpIssue.Text))
            {
                ShowError(inpIssue, lblErrorIssue, "Customer issue description is required.");
                firstInvalid ??= inpIssue;
                valid = false;
            }

            firstInvalid?.Focus();
            return valid;
        }

        private void ClearErrors()
        {
            ClearError(lblErrorCustomer);
            ClearError(inpDeviceModel, lblErrorDeviceModel);
            ClearError(inpIssue, lblErrorIssue);
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

        private static void ClearError(Label err)
        {
            err.Text = "";
            err.Visible = false;
        }

        // ═══════════ CONTROL FACTORIES ═══════════



        private ComboBox MakeCombo(int x, int y, int width, string[] items, int selectedIndex)
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

            pnlBody.Controls.Add(cmb);
            return cmb;
        }



    }
}
