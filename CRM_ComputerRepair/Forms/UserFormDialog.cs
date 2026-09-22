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

        private Label lblSubtitle = null!;

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

        private Button btnSave = null!;
        private Button btnCancel = null!;

        public UserFormDialog(UserSummaryDto? existing)
        {
            _editing = existing;
            _isEditMode = existing != null;

            BuildCard(
                _isEditMode ? "Edit User" : "Add User",
                width: 560,
                height: _isEditMode ? 660 : 720);

            BuildContent();

            Shown += (s, e) => inpFirstName.Focus();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;
            int halfW = (w - 12) / 2;

            // ─── Subtitle ───
            lblSubtitle = new Label
            {
                Text = _isEditMode
                    ? "Update this user's details or role."
                    : "Create a new user account with a role.",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 20)
            };
            pnlCard.Controls.Add(lblSubtitle);
            y += 32;

            // ─── Username + Email ───
            lblUsername = MakeLabel("Username *", x, y);
            lblEmail = MakeLabel("Email *", x + halfW + 12, y);
            y += 20;

            inpUsername = MakeField(x, y, halfW, "e.g. jdoe");
            inpEmail = MakeField(x + halfW + 12, y, halfW, "name@example.com");
            y += 38 + 4;

            lblErrorUsername = MakeErrorLabel(x, y);
            lblErrorEmail = MakeErrorLabel(x + halfW + 12, y);
            y += 20;

            // ─── Password (create mode only) ───
            if (!_isEditMode)
            {
                lblPassword = MakeLabel("Password * (min 6 characters)", x, y);
                y += 20;

                inpPassword = MakeField(x, y, w, "••••••");
                inpPassword.InnerTextBox.UseSystemPasswordChar = true;
                y += 38 + 4;

                lblErrorPassword = MakeErrorLabel(x, y);
                y += 20;
            }

            // ─── First + Last name ───
            lblFirstName = MakeLabel("First name *", x, y);
            lblLastName = MakeLabel("Last name *", x + halfW + 12, y);
            y += 20;

            inpFirstName = MakeField(x, y, halfW, "Juan");
            inpLastName = MakeField(x + halfW + 12, y, halfW, "Dela Cruz");
            y += 38 + 4;

            lblErrorFirst = MakeErrorLabel(x, y);
            lblErrorLast = MakeErrorLabel(x + halfW + 12, y);
            y += 20;

            // ─── Role + Active ───
            lblRole = MakeLabel("Role *", x, y);
            var lblActiveCaption = MakeLabel("Active", x + halfW + 12, y);
            y += 20;

            cmbRole = MakeCombo(x, y, halfW,
                new[] { "Super Admin", "Admin", "Manager", "Staff" }, 3);

            chkActive = new CheckBox
            {
                Text = "",
                Font = UiKit.T.Body,
                ForeColor = AppTheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(x + halfW + 12, y + 4),
                Size = new Size(24, 24),
                Checked = true
            };
            pnlCard.Controls.Add(chkActive);

            y += 38 + 20;

            // ─── Buttons ───
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
                !inpEmail.Text.Contains('@'))
            {
                ShowError(inpEmail, lblErrorEmail, "A valid email is required.");
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

        private TextField MakeField(int x, int y, int width, string placeholder)
        {
            var tf = new TextField
            {
                PlaceholderText = placeholder,
                Location = new Point(x, y),
                Size = new Size(width, 38)
            };
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