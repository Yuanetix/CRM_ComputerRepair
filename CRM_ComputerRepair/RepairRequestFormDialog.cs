using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Modal dialog for Add / Edit on a Repair Request.
    /// Supports status workflow: Pending → Approved → In Progress → Completed.
    /// </summary>
    [DesignerCategory("Code")]
    public class RepairRequestFormDialog : ModalForm
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private readonly RepairRequestDto? _editing;
        private readonly bool _isEditMode;

        // ═══════════ CONTROLS ═══════════

        private Label lblSubtitle = null!;

        private Label lblCustomerId = null!;
        private Label lblDeviceModel = null!;
        private Label lblSerialNumber = null!;
        private Label lblIssue = null!;
        private Label lblPriority = null!;
        private Label lblStatus = null!;
        private Label lblEstimatedCost = null!;
        private Label lblActualCost = null!;
        private Label lblTechnicianNotes = null!;

        private TextField inpCustomerId = null!;
        private TextField inpDeviceModel = null!;
        private TextField inpSerialNumber = null!;
        private TextField inpIssue = null!;
        private ComboBox cmbPriority = null!;
        private ComboBox cmbStatus = null!;
        private TextField inpEstimatedCost = null!;
        private TextField inpActualCost = null!;
        private TextField inpTechnicianNotes = null!;

        private Label lblErrorCustomerId = null!;
        private Label lblErrorDeviceModel = null!;
        private Label lblErrorIssue = null!;

        private Button btnSave = null!;
        private Button btnCancel = null!;

        // ═══════════ CONSTRUCTOR ═══════════

        public RepairRequestFormDialog(RepairRequestDto? existing = null)
        {
            _editing = existing;
            _isEditMode = existing != null;

            BuildCard(
                _isEditMode ? "Edit Repair Request" : "New Repair Request",
                width: 620,
                height: _isEditMode ? 760 : 740);

            BuildContent();
        }

        // ═══════════ CONTENT ═══════════

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // ── Subtitle ──
            lblSubtitle = new Label
            {
                Text = _isEditMode
                    ? "Update the repair request details below."
                    : "Enter the repair details below. Fields marked * are required.",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 20)
            };
            pnlCard.Controls.Add(lblSubtitle);

            y += 32;

            // ── Customer ID + Device Model ──
            int halfW = (w - 12) / 2;

            lblCustomerId = MakeLabel("Customer ID *", x, y);
            lblDeviceModel = MakeLabel("Device Model *", x + halfW + 12, y);
            y += 20;

            inpCustomerId = MakeField(x, y, halfW, "e.g. 1");
            inpDeviceModel = MakeField(x + halfW + 12, y, halfW, "e.g. Dell Latitude 5420");

            y += 38 + 4;

            lblErrorCustomerId = MakeErrorLabel(x, y);
            lblErrorDeviceModel = MakeErrorLabel(x + halfW + 12, y);

            y += 20;

            // ── Serial Number ──
            lblSerialNumber = MakeLabel("Serial Number", x, y);
            y += 20;
            inpSerialNumber = MakeField(x, y, w, "e.g. SN-ABC123");

            y += 38 + 14;

            // ── Issue Description * ──
            lblIssue = MakeLabel("Issue Description *", x, y);
            y += 20;
            inpIssue = MakeField(x, y, w, "Describe the problem...", multiline: true);
            y += 74 + 4;
            lblErrorIssue = MakeErrorLabel(x, y);
            y += 20;

            // ── Priority + Status ──
            lblPriority = MakeLabel("Priority", x, y);
            lblStatus = MakeLabel("Status", x + halfW + 12, y);
            y += 20;

            cmbPriority = MakeCombo(x, y, halfW,
                new[] { "Low", "Medium", "High", "Urgent" }, 1);
            cmbStatus = MakeCombo(x + halfW + 12, y, halfW,
                new[] { "Pending", "Approved", "In Progress", "Completed", "Rejected", "Reassigned" }, 0);

            y += 38 + 14;

            // ── Estimated Cost + Actual Cost ──
            lblEstimatedCost = MakeLabel("Estimated Cost (₱)", x, y);
            lblActualCost = MakeLabel("Actual Cost (₱)", x + halfW + 12, y);
            y += 20;

            inpEstimatedCost = MakeField(x, y, halfW, "0.00");
            inpActualCost = MakeField(x + halfW + 12, y, halfW, "0.00");

            y += 38 + 14;

            // ── Technician Notes ──
            lblTechnicianNotes = MakeLabel("Technician Notes", x, y);
            y += 20;
            inpTechnicianNotes = MakeField(x, y, w, "Notes from the technician...", multiline: true);
            y += 74 + 20;

            // ── Buttons ──
            int btnY = pnlCard.Height - ShadowPad - 60;
            int rightEdge = ContentRightX;

            int saveW = 110;
            int cancelW = 100;

            int saveX = rightEdge - saveW;
            int cancelX = saveX - cancelW - 10;

            btnCancel = MakeSecondaryButton("Cancel");
            btnCancel.Size = new Size(cancelW, 40);
            btnCancel.Location = new Point(cancelX, btnY);
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnSave = MakePrimaryButton(_isEditMode ? "Update" : "Save");
            btnSave.Size = new Size(saveW, 40);
            btnSave.Location = new Point(saveX, btnY);
            btnSave.Click += async (s, e) => await SaveAsync();

            // ── Prefill ──
            if (_editing != null)
            {
                inpCustomerId.Text = _editing.CustomerId.ToString();
                inpDeviceModel.Text = _editing.DeviceModel ?? "";
                inpSerialNumber.Text = _editing.SerialNumber ?? "";
                inpIssue.Text = _editing.IssueDescription ?? "";
                cmbPriority.SelectedIndex = Clamp(_editing.Priority, 0, 3);
                cmbStatus.SelectedIndex = Clamp(_editing.Status, 0, 5);
                inpEstimatedCost.Text = _editing.EstimatedCost?.ToString("0.00") ?? "";
                inpActualCost.Text = _editing.ActualCost?.ToString("0.00") ?? "";
                inpTechnicianNotes.Text = _editing.TechnicianNotes ?? "";
            }
            else
            {
                inpCustomerId.Text = "1";
                cmbPriority.SelectedIndex = 1;
                cmbStatus.SelectedIndex = 0;
            }

            Shown += (s, e) => inpCustomerId.Focus();
        }

        private static int Clamp(int value, int min, int max)
            => value < min ? min : (value > max ? max : value);

        // ═══════════ SAVE ═══════════

        private async Task SaveAsync()
        {
            if (!ValidateInputs()) return;

            try
            {
                var dto = new RepairRequestDto
                {
                    CustomerId = int.Parse(inpCustomerId.Text.Trim()),
                    DeviceModel = inpDeviceModel.Text.Trim(),
                    SerialNumber = inpSerialNumber.Text.Trim(),
                    IssueDescription = inpIssue.Text.Trim(),
                    Priority = cmbPriority.SelectedIndex,
                    Status = cmbStatus.SelectedIndex,
                    EstimatedCost = ParseDecimalOrNull(inpEstimatedCost.Text),
                    ActualCost = ParseDecimalOrNull(inpActualCost.Text),
                    TechnicianNotes = string.IsNullOrWhiteSpace(inpTechnicianNotes.Text)
                        ? null
                        : inpTechnicianNotes.Text.Trim(),
                    AssignedToStaffId = _editing?.AssignedToStaffId ?? UserSession.UserId
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
                MessageBox.Show(
                    $"Save failed:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static decimal? ParseDecimalOrNull(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (decimal.TryParse(text.Trim(), out var value)) return value;
            return null;
        }

        // ═══════════ VALIDATION ═══════════

        private bool ValidateInputs()
        {
            ClearErrors();
            bool valid = true;
            Control? firstInvalid = null;

            if (string.IsNullOrWhiteSpace(inpCustomerId.Text) ||
                !int.TryParse(inpCustomerId.Text.Trim(), out _))
            {
                ShowError(inpCustomerId, lblErrorCustomerId, "Valid Customer ID is required.");
                firstInvalid ??= inpCustomerId;
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(inpDeviceModel.Text))
            {
                ShowError(inpDeviceModel, lblErrorDeviceModel, "Device model is required.");
                firstInvalid ??= inpDeviceModel;
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(inpIssue.Text))
            {
                ShowError(inpIssue, lblErrorIssue, "Issue description is required.");
                firstInvalid ??= inpIssue;
                valid = false;
            }

            firstInvalid?.Focus();
            return valid;
        }

        private void ClearErrors()
        {
            ClearError(inpCustomerId, lblErrorCustomerId);
            ClearError(inpDeviceModel, lblErrorDeviceModel);
            ClearError(inpIssue, lblErrorIssue);
        }

        private static void ShowError(TextField input, Label err, string msg)
        {
            input.HasError = true;
            err.Text = "⚠  " + msg;
            err.Visible = true;
        }

        private static void ClearError(TextField input, Label err)
        {
            input.HasError = false;
            err.Text = "";
            err.Visible = false;
        }

        // ═══════════ CONTROL FACTORIES ═══════════

        private Label MakeLabel(string text, int x, int y)
        {
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 8.5F),
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
            pnlCard.Controls.Add(lbl);
            return lbl;
        }

        private TextField MakeField(int x, int y, int width, string placeholder,
                                    bool multiline = false)
        {
            var tf = new TextField
            {
                PlaceholderText = placeholder,
                Location = new Point(x, y),
                Size = new Size(width, multiline ? 68 : 38)
            };
            if (multiline) tf.Multiline = true;
            pnlCard.Controls.Add(tf);
            return tf;
        }

        private ComboBox MakeCombo(int x, int y, int width, string[] items, int selectedIndex)
        {
            var cmb = new ComboBox
            {
                Font = new Font("Segoe UI", 9.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(x, y),
                Size = new Size(width, 30),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat
            };
            cmb.Items.AddRange(items);
            if (selectedIndex >= 0 && selectedIndex < items.Length)
                cmb.SelectedIndex = selectedIndex;
            pnlCard.Controls.Add(cmb);
            return cmb;
        }

        private Label MakeErrorLabel(int x, int y)
        {
            var lbl = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8F),
                ForeColor = AppTheme.Danger,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Visible = false
            };
            pnlCard.Controls.Add(lbl);
            return lbl;
        }

        private Button MakePrimaryButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9.5F),
                BackColor = AppTheme.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = AppTheme.PrimaryHover;
            b.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(b, 8);
            pnlCard.Controls.Add(b);
            return b;
        }

        private Button MakeSecondaryButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9.5F),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = AppTheme.BorderStrong;
            b.FlatAppearance.MouseOverBackColor = AppTheme.Neutral;
            b.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(b, 8);
            pnlCard.Controls.Add(b);
            return b;
        }
    }
}