using CRM.winforms.Controls;
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
    /// Modal dialog for adding or editing a company branch location.
    /// Supports Code, Name, Address, City, State/Province, Postal Code, Phone, Email,
    /// Appointed Manager, and Active status.
    /// </summary>
    [DesignerCategory("Code")]
    public class BranchFormDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly BranchDto? _editing;
        private readonly bool _isEditMode;

        private Label lblCode = null!;
        private Label lblName = null!;
        private Label lblAddress = null!;
        private Label lblCity = null!;
        private Label lblProvince = null!;
        private Label lblPostal = null!;
        private Label lblPhone = null!;
        private Label lblEmail = null!;
        private Label lblManager = null!;

        private TextField inpCode = null!;
        private TextField inpName = null!;
        private TextField inpAddress = null!;
        private TextField inpCity = null!;
        private TextField inpProvince = null!;
        private TextField inpPostal = null!;
        private TextField inpPhone = null!;
        private TextField inpEmail = null!;

        private ComboBox cmbManager = null!;
        private CheckBox chkActive = null!;

        private Label lblErrorCode = null!;
        private Label lblErrorName = null!;
        private Label lblErrorEmail = null!;

        private SaasButton btnSave = null!;
        private SaasButton btnCancel = null!;

        private readonly List<UserLookupItem> _managerUsers = new();

        private sealed class UserLookupItem
        {
            public string? Id { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public override string ToString() => DisplayName;
        }

        public BranchFormDialog(BranchDto? existing = null)
        {
            _editing = existing;
            _isEditMode = existing != null;

            BuildCard(
                _isEditMode ? "Edit Branch" : "Add Branch Location",
                _isEditMode
                    ? "Update location details, regional contacts, and assigned manager."
                    : "Create a new regional branch office or service satellite hub.",
                width: 640,
                height: 680);

            BuildContent();

            Shown += async (s, e) =>
            {
                inpCode.Focus();
                await LoadManagersAsync();
            };
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;
            int halfW = (w - 14) / 2;

            // ── Section 1: Identification ──
            ModalKit.MakeSection(pnlBody, "Branch Identification", x, y, w);
            y += 24;

            lblCode = ModalKit.MakeLabel(pnlBody, "Branch Code * (e.g. CEB-01, HQ-01)", x, y);
            lblName = ModalKit.MakeLabel(pnlBody, "Branch Name *", x + halfW + 14, y);
            y += 20;

            inpCode = ModalKit.MakeField(pnlBody, x, y, halfW, "e.g. CEB-01");
            inpName = ModalKit.MakeField(pnlBody, x + halfW + 14, y, halfW, "e.g. Cebu Central Hub");
            y += 38 + 2;

            lblErrorCode = ModalKit.MakeErrorLabel(pnlBody, x, y);
            lblErrorName = ModalKit.MakeErrorLabel(pnlBody, x + halfW + 14, y);
            y += 18;

            // ── Section 2: Regional Location ──
            ModalKit.MakeSection(pnlBody, "Location Details", x, y, w);
            y += 24;

            lblAddress = ModalKit.MakeLabel(pnlBody, "Street Address", x, y);
            y += 20;
            inpAddress = ModalKit.MakeField(pnlBody, x, y, w, "Building, Street, Barangay");
            y += 38 + 10;

            lblCity = ModalKit.MakeLabel(pnlBody, "City / Municipality", x, y);
            lblProvince = ModalKit.MakeLabel(pnlBody, "State / Province", x + halfW + 14, y);
            y += 20;

            inpCity = ModalKit.MakeField(pnlBody, x, y, halfW, "e.g. Cebu City");
            inpProvince = ModalKit.MakeField(pnlBody, x + halfW + 14, y, halfW, "e.g. Cebu");
            y += 38 + 10;

            lblPostal = ModalKit.MakeLabel(pnlBody, "Postal Code", x, y);
            lblPhone = ModalKit.MakeLabel(pnlBody, "Phone Number", x + halfW + 14, y);
            y += 20;

            inpPostal = ModalKit.MakeField(pnlBody, x, y, halfW, "e.g. 6000");
            inpPhone = ModalKit.MakeField(pnlBody, x + halfW + 14, y, halfW, "+63 32 123 4567");
            y += 38 + 10;

            // ── Section 3: Contact & Management ──
            ModalKit.MakeSection(pnlBody, "Management & Contact", x, y, w);
            y += 24;

            lblEmail = ModalKit.MakeLabel(pnlBody, "Official Branch Email", x, y);
            lblManager = ModalKit.MakeLabel(pnlBody, "Appointed Manager", x + halfW + 14, y);
            y += 20;

            inpEmail = ModalKit.MakeField(pnlBody, x, y, halfW, "branch@company.com");

            cmbManager = ModalKit.MakeCombo(pnlBody, x + halfW + 14, y, halfW);
            cmbManager.DropDownStyle = ComboBoxStyle.DropDownList;
            _managerUsers.Add(new UserLookupItem { Id = null, DisplayName = "— Unassigned —" });
            cmbManager.Items.Add(_managerUsers[0]);
            cmbManager.SelectedIndex = 0;

            y += 38 + 2;
            lblErrorEmail = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 18;

            // Active checkbox
            chkActive = new CheckBox
            {
                Text = "Branch is Active (Available in switcher and for new operations)",
                Font = UiKit.Body,
                ForeColor = AppTheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(x, y + 4),
                AutoSize = true,
                Checked = true
            };
            pnlBody.Controls.Add(chkActive);

            // ── Buttons ──
            btnCancel = ModalKit.AddSecondary(pnlCard, "Cancel");
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnSave = ModalKit.AddPrimary(pnlCard, _isEditMode ? "Save Changes" : "Create Branch");
            btnSave.Click += async (s, e) => await SaveAsync();
            LayoutFooter(btnSave, btnCancel);

            AcceptButton = btnSave;
            CancelButton = btnCancel;

            // ── Prefill if edit mode ──
            if (_editing != null)
            {
                inpCode.Text = _editing.BranchCode;
                inpName.Text = _editing.BranchName;
                inpAddress.Text = _editing.Address ?? "";
                inpCity.Text = _editing.City ?? "";
                inpProvince.Text = _editing.StateOrProvince ?? "";
                inpPostal.Text = _editing.PostalCode ?? "";
                inpPhone.Text = _editing.Phone ?? "";
                inpEmail.Text = _editing.Email ?? "";
                chkActive.Checked = _editing.IsActive;
            }
        }

        private async Task LoadManagersAsync()
        {
            try
            {
                var users = await _api.GetUsersAsync();
                _managerUsers.Clear();
                cmbManager.Items.Clear();

                _managerUsers.Add(new UserLookupItem { Id = null, DisplayName = "— Unassigned —" });
                cmbManager.Items.Add(_managerUsers[0]);

                int selectedIdx = 0;
                foreach (var u in users.Where(u => u.IsActive))
                {
                    string roleLabel = u.Roles != null && u.Roles.Count > 0 ? $" ({string.Join(", ", u.Roles)})" : "";
                    var item = new UserLookupItem
                    {
                        Id = u.Id,
                        DisplayName = $"{u.FullName}{roleLabel}"
                    };
                    _managerUsers.Add(item);
                    int itemIdx = cmbManager.Items.Add(item);

                    if (_editing != null && !string.IsNullOrEmpty(_editing.ManagerUserId) && _editing.ManagerUserId == u.Id)
                    {
                        selectedIdx = itemIdx;
                    }
                }

                if (selectedIdx >= 0 && selectedIdx < cmbManager.Items.Count)
                    cmbManager.SelectedIndex = selectedIdx;
            }
            catch
            {
                // Silently keep default unassigned if offline or failed
            }
        }

        private async Task SaveAsync()
        {
            if (!ValidateInputs()) return;

            btnSave.Enabled = false;
            btnCancel.Enabled = false;

            try
            {
                var selectedManager = cmbManager.SelectedItem as UserLookupItem;
                string? managerUserId = selectedManager?.Id;

                if (_isEditMode && _editing != null)
                {
                    var req = new UpdateBranchRequest
                    {
                        BranchCode = inpCode.Text.Trim().ToUpperInvariant(),
                        BranchName = inpName.Text.Trim(),
                        Address = string.IsNullOrWhiteSpace(inpAddress.Text) ? null : inpAddress.Text.Trim(),
                        City = string.IsNullOrWhiteSpace(inpCity.Text) ? null : inpCity.Text.Trim(),
                        StateOrProvince = string.IsNullOrWhiteSpace(inpProvince.Text) ? null : inpProvince.Text.Trim(),
                        PostalCode = string.IsNullOrWhiteSpace(inpPostal.Text) ? null : inpPostal.Text.Trim(),
                        Phone = string.IsNullOrWhiteSpace(inpPhone.Text) ? null : inpPhone.Text.Trim(),
                        Email = string.IsNullOrWhiteSpace(inpEmail.Text) ? null : inpEmail.Text.Trim(),
                        ManagerUserId = managerUserId,
                        IsActive = chkActive.Checked
                    };

                    await _api.UpdateBranchAsync(_editing.BranchId, req);
                }
                else
                {
                    var req = new CreateBranchRequest
                    {
                        BranchCode = inpCode.Text.Trim().ToUpperInvariant(),
                        BranchName = inpName.Text.Trim(),
                        Address = string.IsNullOrWhiteSpace(inpAddress.Text) ? null : inpAddress.Text.Trim(),
                        City = string.IsNullOrWhiteSpace(inpCity.Text) ? null : inpCity.Text.Trim(),
                        StateOrProvince = string.IsNullOrWhiteSpace(inpProvince.Text) ? null : inpProvince.Text.Trim(),
                        PostalCode = string.IsNullOrWhiteSpace(inpPostal.Text) ? null : inpPostal.Text.Trim(),
                        Phone = string.IsNullOrWhiteSpace(inpPhone.Text) ? null : inpPhone.Text.Trim(),
                        Email = string.IsNullOrWhiteSpace(inpEmail.Text) ? null : inpEmail.Text.Trim(),
                        ManagerUserId = managerUserId,
                        IsActive = chkActive.Checked
                    };

                    await _api.CreateBranchAsync(req);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to save branch:\n\n{ex.Message}",
                    "Save Branch Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnSave.Enabled = true;
                btnCancel.Enabled = true;
            }
        }

        private bool ValidateInputs()
        {
            ClearErrors();
            bool valid = true;
            Control? firstInvalid = null;

            if (string.IsNullOrWhiteSpace(inpCode.Text))
            {
                ShowError(inpCode, lblErrorCode, "Branch code is required (e.g. CEB-01).");
                firstInvalid ??= inpCode;
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(inpName.Text))
            {
                ShowError(inpName, lblErrorName, "Branch name is required.");
                firstInvalid ??= inpName;
                valid = false;
            }

            if (!string.IsNullOrWhiteSpace(inpEmail.Text) && !IsValidEmail(inpEmail.Text.Trim()))
            {
                ShowError(inpEmail, lblErrorEmail, "Please enter a valid email address.");
                firstInvalid ??= inpEmail;
                valid = false;
            }

            firstInvalid?.Focus();
            return valid;
        }

        private void ClearErrors()
        {
            ClearError(inpCode, lblErrorCode);
            ClearError(inpName, lblErrorName);
            ClearError(inpEmail, lblErrorEmail);
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

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email && email.Contains('.') && email.IndexOf('.') > email.IndexOf('@') + 1;
            }
            catch
            {
                return false;
            }
        }
    }
}
