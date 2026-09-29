using CRM.winforms.Controls;
using CRM.winforms.Forms;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    [DesignerCategory("Code")]
    public class UserFormDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly UserSummaryDto? _editing;
        private readonly bool _isEditMode;

        private Label lblUsername = null!;
        private Label lblEmail = null!;
        private Label lblPassword = null!;
        private Label lblFirstName = null!;
        private Label lblLastName = null!;
        private Label lblRole = null!;

        private TextField inpUsername = null!;
        private TextField inpEmail = null!;
        private TextField inpPassword = null!;
        private TextField inpFirstName = null!;
        private TextField inpLastName = null!;
        private ComboBox cmbRole = null!;
        private CheckBox chkActive = null!;

        private Label lblErrorUsername = null!;
        private Label lblErrorEmail = null!;
        private Label lblErrorPassword = null!;
        private Label lblErrorFirst = null!;
        private Label lblErrorLast = null!;

        private SaasButton btnSave = null!;
        private SaasButton btnCancel = null!;

        public UserFormDialog(UserSummaryDto? existing)
        {
            _editing = existing;
            _isEditMode = existing != null;

            BuildCard(
                _isEditMode ? "Edit User" : "Add User",
                _isEditMode
                    ? "Update this user's details, active status, or role."
                    : "Create a new user account with an assigned system role.",
                width: 580,
                height: _isEditMode ? 560 : 620);

            BuildContent();

            Shown += (s, e) => inpUsername.Focus();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;
            int halfW = (w - 12) / 2;

            // ─── Username + Email ───
            lblUsername = ModalKit.MakeLabel(pnlBody, "Username *", x, y);
            lblEmail = ModalKit.MakeLabel(pnlBody, "Email *", x + halfW + 12, y);
            y += 20;

            inpUsername = ModalKit.MakeField(pnlBody, x, y, halfW, "e.g. jdoe");
            inpUsername.Height = 36;
            inpEmail = ModalKit.MakeField(pnlBody, x + halfW + 12, y, halfW, "name@example.com");
            inpEmail.Height = 36;
            y += 38 + 4;

            lblErrorUsername = ModalKit.MakeErrorLabel(pnlBody, x, y);
            lblErrorEmail = ModalKit.MakeErrorLabel(pnlBody, x + halfW + 12, y);
            y += 20;

            // ─── Password (create mode only) ───
            if (!_isEditMode)
            {
                lblPassword = ModalKit.MakeLabel(pnlBody, "Password * (min 6 characters)", x, y);
                y += 20;

                inpPassword = ModalKit.MakeField(pnlBody, x, y, w, "••••••");
                inpPassword.Height = 36;
                inpPassword.InnerTextBox.UseSystemPasswordChar = true;
                y += 38 + 4;

                lblErrorPassword = ModalKit.MakeErrorLabel(pnlBody, x, y);
                y += 20;
            }

            // ─── First + Last name ───
            lblFirstName = ModalKit.MakeLabel(pnlBody, "First name *", x, y);
            lblLastName = ModalKit.MakeLabel(pnlBody, "Last name *", x + halfW + 12, y);
            y += 20;

            inpFirstName = ModalKit.MakeField(pnlBody, x, y, halfW, "Juan");
            inpFirstName.Height = 36;
            inpLastName = ModalKit.MakeField(pnlBody, x + halfW + 12, y, halfW, "Dela Cruz");
            inpLastName.Height = 36;
            y += 38 + 4;

            lblErrorFirst = ModalKit.MakeErrorLabel(pnlBody, x, y);
            lblErrorLast = ModalKit.MakeErrorLabel(pnlBody, x + halfW + 12, y);
            y += 20;

            // ─── Role + Active ───
            lblRole = ModalKit.MakeLabel(pnlBody, "Role *", x, y);
            y += 20;

            cmbRole = MakeCombo(pnlBody, x, y, halfW,
                new[] { "Super Admin", "Admin", "Manager", "Staff" }, 3);

            chkActive = new CheckBox
            {
                Text = "Active account (allow login)",
                Font = UiKit.Body,
                ForeColor = AppTheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(x + halfW + 12, y + 8),
                AutoSize = true,
                Checked = true
            };
            pnlBody.Controls.Add(chkActive);

            y += 38 + 20;

            // ─── Buttons ───
            btnCancel = ModalKit.AddSecondary(pnlCard, "Cancel");
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnSave = ModalKit.AddPrimary(pnlCard, _isEditMode ? "Update" : "Save");
            btnSave.Click += async (s, e) => await SaveAsync();
            LayoutFooter(btnSave, btnCancel);

            AcceptButton = btnSave;
            CancelButton = btnCancel;

            // ─── Prefill (edit mode) ───
            if (_editing != null)
            {
                inpUsername.Text = _editing.UserName ?? "";
                inpEmail.Text = _editing.Email ?? "";
                inpFirstName.Text = _editing.FirstName ?? "";
                inpLastName.Text = _editing.LastName ?? "";
                chkActive.Checked = _editing.IsActive;

                var currentRole = _editing.Roles.FirstOrDefault() ?? "Staff";
                var idx = Array.IndexOf(
                    new[] { "Super Admin", "Admin", "Manager", "Staff" }, currentRole);
                cmbRole.SelectedIndex = idx >= 0 ? idx : 3;
            }
        }

        private async Task SaveAsync()
        {
            if (!ValidateInputs()) return;

            try
            {
                var role = cmbRole.SelectedItem?.ToString() ?? "Staff";

                if (_isEditMode && _editing != null)
                {
                    await _api.UpdateUserAsync(
                        _editing.Id,
                        inpFirstName.Text.Trim(),
                        inpLastName.Text.Trim(),
                        inpEmail.Text.Trim(),
                        inpUsername.Text.Trim(),
                        chkActive.Checked,
                        role);
                }
                else
                {
                    await _api.CreateUserAsync(
                        inpUsername.Text.Trim(),
                        inpEmail.Text.Trim(),
                        inpPassword.Text,
                        inpFirstName.Text.Trim(),
                        inpLastName.Text.Trim(),
                        role);
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

        private bool ValidateInputs()
        {
            ClearErrors();
            bool valid = true;
            Control? firstInvalid = null;

            if (string.IsNullOrWhiteSpace(inpUsername.Text))
            {
                ShowError(inpUsername, lblErrorUsername, "Username is required.");
                firstInvalid ??= inpUsername;
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(inpEmail.Text) ||
                !IsValidEmail(inpEmail.Text.Trim()))
            {
                ShowError(inpEmail, lblErrorEmail, "A valid email is required (e.g. name@example.com).");
                firstInvalid ??= inpEmail;
                valid = false;
            }

            if (!_isEditMode)
            {
                if (string.IsNullOrWhiteSpace(inpPassword.Text) ||
                    inpPassword.Text.Length < 6)
                {
                    ShowError(inpPassword, lblErrorPassword,
                        "Password must be at least 6 characters.");
                    firstInvalid ??= inpPassword;
                    valid = false;
                }
            }

            if (string.IsNullOrWhiteSpace(inpFirstName.Text))
            {
                ShowError(inpFirstName, lblErrorFirst, "First name is required.");
                firstInvalid ??= inpFirstName;
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(inpLastName.Text))
            {
                ShowError(inpLastName, lblErrorLast, "Last name is required.");
                firstInvalid ??= inpLastName;
                valid = false;
            }

            firstInvalid?.Focus();
            return valid;
        }

        private void ClearErrors()
        {
            ClearError(inpUsername, lblErrorUsername);
            ClearError(inpEmail, lblErrorEmail);
            ClearError(inpFirstName, lblErrorFirst);
            ClearError(inpLastName, lblErrorLast);
            if (inpPassword != null && lblErrorPassword != null)
                ClearError(inpPassword, lblErrorPassword);
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

        // ═══════════ CONTROL FACTORIES ═══════════



        private ComboBox MakeCombo(Control parent, int x, int y, int width, string[] items, int selectedIndex)
        {
            var cmb = ModalKit.MakeCombo(parent, x, y, width);
            cmb.DropDownStyle = ComboBoxStyle.DropDown;
            cmb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cmb.AutoCompleteSource = AutoCompleteSource.ListItems;
            cmb.DropDownWidth = Math.Max(width, 260);
            cmb.Items.AddRange(items);
            if (selectedIndex >= 0 && selectedIndex < items.Length)
                cmb.SelectedIndex = selectedIndex;
            ModalKit.AdjustDropDownWidth(cmb);

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

            return cmb;
        }



    }
}
