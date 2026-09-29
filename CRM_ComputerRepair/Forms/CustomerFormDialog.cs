using CRM.winforms.Controls;
using CRM.winforms.Forms;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Modal dialog for Add / Edit customer with 3NF-compliant atomic fields.
    /// Captures First/Last Name, Email, Phone, Address, City, State/Province, Postal Code, Country.
    /// </summary>
    [DesignerCategory("Code")]
    public class CustomerFormDialog : ModalForm
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private readonly int? _editingCustomerId;
        private readonly bool _isEditMode;

        // ═══════════ CONTROLS ═══════════

        private Label lblFirstName = null!;
        private Label lblLastName = null!;
        private Label lblEmail = null!;
        private Label lblPhone = null!;
        private Label lblAddress = null!;
        private Label lblCity = null!;
        private Label lblProvince = null!;
        private Label lblPostal = null!;
        private Label lblCountry = null!;

        private TextField inpFirstName = null!;
        private TextField inpLastName = null!;
        private TextField inpEmail = null!;
        private TextField inpPhone = null!;
        private TextField inpAddress = null!;
        private TextField inpCity = null!;
        private TextField inpProvince = null!;
        private TextField inpPostal = null!;
        private TextField inpCountry = null!;

        private Label lblErrorFirstName = null!;
        private Label lblErrorLastName = null!;
        private Label lblErrorEmail = null!;

        private SaasButton btnSave = null!;
        private SaasButton btnCancel = null!;
        private SaasButton btnArchive = null!;

        // ═══════════ CONSTRUCTOR ═══════════

        public CustomerFormDialog(int? customerId, CustomerDto? existing = null)
        {
            _editingCustomerId = customerId;
            _isEditMode = customerId.HasValue;

            BuildCard(
                _isEditMode ? "Edit Customer" : "Add Customer",
                _isEditMode
                    ? "Update customer profile and contact details."
                    : "Register a new customer and contact details.",
                width: 640,
                height: _isEditMode ? 720 : 690);

            BuildContent(existing);
        }

        // ═══════════ CONTENT ═══════════

        private void BuildContent(CustomerDto? existing)
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            int halfW = (w - 14) / 2;

            // ── Section 1: Customer Details ──
            ModalKit.MakeSection(pnlBody, "Customer Details", x, y, w);
            y += 24;

            // Row 1: First Name * & Last Name *
            lblFirstName = ModalKit.MakeLabel(pnlBody, "First name *", x, y);
            lblLastName = ModalKit.MakeLabel(pnlBody, "Last name *", x + halfW + 14, y);
            y += 20;

            inpFirstName = ModalKit.MakeField(pnlBody, x, y, halfW, "Enter first name");
            inpLastName = ModalKit.MakeField(pnlBody, x + halfW + 14, y, halfW, "Enter last name");
            y += 38 + 2;

            lblErrorFirstName = ModalKit.MakeErrorLabel(pnlBody, x, y);
            lblErrorLastName = ModalKit.MakeErrorLabel(pnlBody, x + halfW + 14, y);
            y += 18;

            // Row 2: Email & Phone
            lblEmail = ModalKit.MakeLabel(pnlBody, "Email", x, y);
            lblPhone = ModalKit.MakeLabel(pnlBody, "Phone", x + halfW + 14, y);
            y += 20;

            inpEmail = ModalKit.MakeField(pnlBody, x, y, halfW, "name@example.com");
            inpPhone = ModalKit.MakeField(pnlBody, x + halfW + 14, y, halfW, "0917 123 4567");
            y += 38 + 2;

            lblErrorEmail = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 18;

            // ── Section 2: Address Information (3NF Normalized) ──
            ModalKit.MakeSection(pnlBody, "Address Information", x, y, w);
            y += 24;

            // Row 3: Street Address
            lblAddress = ModalKit.MakeLabel(pnlBody, "Street Address", x, y);
            y += 20;
            inpAddress = ModalKit.MakeField(pnlBody, x, y, w, "Building, Street, Barangay");
            y += 38 + 10;

            // Row 4: City & State / Province
            lblCity = ModalKit.MakeLabel(pnlBody, "City", x, y);
            lblProvince = ModalKit.MakeLabel(pnlBody, "State / Province", x + halfW + 14, y);
            y += 20;

            inpCity = ModalKit.MakeField(pnlBody, x, y, halfW, "e.g. Quezon City");
            inpProvince = ModalKit.MakeField(pnlBody, x + halfW + 14, y, halfW, "e.g. Metro Manila");
            y += 38 + 10;

            // Row 5: Postal Code & Country
            lblPostal = ModalKit.MakeLabel(pnlBody, "Postal Code", x, y);
            lblCountry = ModalKit.MakeLabel(pnlBody, "Country", x + halfW + 14, y);
            y += 20;

            inpPostal = ModalKit.MakeField(pnlBody, x, y, halfW, "e.g. 1112");
            inpCountry = ModalKit.MakeField(pnlBody, x + halfW + 14, y, halfW, "Philippines");
            inpCountry.Text = "Philippines";
            y += 38 + 20;

            // ── Buttons ──
            btnCancel = new SaasButton("Cancel", SaasButtonVariant.Secondary);
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnSave = new SaasButton(_isEditMode ? "Update" : "Save", SaasButtonVariant.Primary);
            btnSave.Click += async (s, e) => await SaveAsync();

            // ── Archive (edit mode) — bottom-left ──
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

            // ── Prefill ──
            if (existing != null)
            {
                inpFirstName.Text = existing.FirstName ?? "";
                inpLastName.Text = existing.LastName ?? "";
                inpEmail.Text = existing.Email ?? "";
                inpPhone.Text = existing.Phone ?? "";
                inpAddress.Text = existing.Address ?? "";
                inpCity.Text = existing.City ?? "";
                inpProvince.Text = existing.StateOrProvince ?? "";
                inpPostal.Text = existing.PostalCode ?? "";
                inpCountry.Text = string.IsNullOrWhiteSpace(existing.Country) ? "Philippines" : existing.Country;
            }

            Shown += (s, e) => inpFirstName.Focus();
        }

        // ═══════════ SAVE ═══════════

        private async Task SaveAsync()
        {
            if (!ValidateInputs()) return;

            try
            {
                var dto = new CustomerDto
                {
                    FirstName = inpFirstName.Text.Trim(),
                    LastName = inpLastName.Text.Trim(),
                    Email = inpEmail.Text.Trim(),
                    Phone = inpPhone.Text.Trim(),
                    Address = inpAddress.Text.Trim(),
                    City = inpCity.Text.Trim(),
                    StateOrProvince = inpProvince.Text.Trim(),
                    PostalCode = inpPostal.Text.Trim(),
                    Country = string.IsNullOrWhiteSpace(inpCountry.Text) ? "Philippines" : inpCountry.Text.Trim()
                };

                if (_isEditMode && _editingCustomerId.HasValue)
                {
                    dto.CustomerId = _editingCustomerId.Value;
                    await _api.UpdateCustomerAsync(_editingCustomerId.Value, dto);
                }
                else
                {
                    await _api.CreateCustomerAsync(dto);
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

        // ═══════════ ARCHIVE ═══════════

        private async Task ArchiveAsync()
        {
            if (!_editingCustomerId.HasValue) return;

            var confirm = MessageBox.Show(
                "Archive this customer?\n\nData is preserved and can be restored.",
                "Confirm Archive",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                await _api.ArchiveCustomerAsync(_editingCustomerId.Value);
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
            TextField? firstInvalid = null;

            if (string.IsNullOrWhiteSpace(inpFirstName.Text))
            {
                ShowError(inpFirstName, lblErrorFirstName, "First name is required.");
                firstInvalid ??= inpFirstName;
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(inpLastName.Text))
            {
                ShowError(inpLastName, lblErrorLastName, "Last name is required.");
                firstInvalid ??= inpLastName;
                valid = false;
            }

            if (!string.IsNullOrWhiteSpace(inpEmail.Text) &&
                !IsValidEmail(inpEmail.Text.Trim()))
            {
                ShowError(inpEmail, lblErrorEmail, "Please enter a valid email address (e.g. name@example.com).");
                firstInvalid ??= inpEmail;
                valid = false;
            }

            firstInvalid?.Focus();
            return valid;
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

        private void ClearErrors()
        {
            ClearError(inpFirstName, lblErrorFirstName);
            ClearError(inpLastName, lblErrorLastName);
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
    }
}
