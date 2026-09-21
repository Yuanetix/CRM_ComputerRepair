using CRM.winforms.Forms;
using CRM.winforms;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Modal dialog for Add / Edit customer with clean spacing.
    /// </summary>
    [DesignerCategory("Code")]
    public class CustomerFormDialog : ModalForm
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private readonly int? _editingCustomerId;
        private readonly bool _isEditMode;

        // ═══════════ CONTROLS ═══════════

        private Label lblSubtitle = null!;

        private Label lblFirstName = null!;
        private Label lblLastName = null!;
        private Label lblEmail = null!;
        private Label lblPhone = null!;
        private Label lblAddress = null!;

        private TextField inpFirstName = null!;
        private TextField inpLastName = null!;
        private TextField inpEmail = null!;
        private TextField inpPhone = null!;
        private TextField inpAddress = null!;

        private Label lblErrorFirstName = null!;
        private Label lblErrorLastName = null!;
        private Label lblErrorEmail = null!;

        private Button btnSave = null!;
        private Button btnCancel = null!;
        private Button btnArchive = null!;

        // ═══════════ CONSTRUCTOR ═══════════

        public CustomerFormDialog(int? customerId, CustomerDto? existing = null)
        {
            _editingCustomerId = customerId;
            _isEditMode = customerId.HasValue;

            BuildCard(
                _isEditMode ? "Edit Customer" : "Add Customer",
                width: 560,
                height: _isEditMode ? 620 : 600);

            BuildContent(existing);
        }

        // ═══════════ CONTENT ═══════════

        private void BuildContent(CustomerDto? existing)
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // ── Subtitle ──
            lblSubtitle = new Label
            {
                Text = _isEditMode
                    ? "Update the customer's information below."
                    : "Enter the customer's information below. Fields marked * are required.",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 20)
            };
            pnlCard.Controls.Add(lblSubtitle);

            y += 32;

            // ── First Name * ──
            lblFirstName = MakeLabel("First name *", x, y);
            y += 20;
            inpFirstName = MakeField(x, y, w, "Enter first name");
            y += 38 + 4;
            lblErrorFirstName = MakeErrorLabel(x, y);
            y += 20;

            // ── Last Name * ──
            lblLastName = MakeLabel("Last name *", x, y);
            y += 20;
            inpLastName = MakeField(x, y, w, "Enter last name");
            y += 38 + 4;
            lblErrorLastName = MakeErrorLabel(x, y);
            y += 20;

            // ── Email ──
            lblEmail = MakeLabel("Email", x, y);
            y += 20;
            inpEmail = MakeField(x, y, w, "name@example.com");
            y += 38 + 4;
            lblErrorEmail = MakeErrorLabel(x, y);
            y += 20;

            // ── Phone ──
            lblPhone = MakeLabel("Phone", x, y);
            y += 20;
            inpPhone = MakeField(x, y, w, "0917 123 4567");
            y += 38 + 14;

            // ── Address (multiline) ──
            lblAddress = MakeLabel("Address", x, y);
            y += 20;
            inpAddress = MakeField(x, y, w, "Street, City, Province", multiline: true);
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

            // Archive (edit mode) — bottom-left
            if (_isEditMode)
            {
                btnArchive = MakeDangerOutlineButton("Archive");
                btnArchive.Size = new Size(110, 40);
                btnArchive.Location = new Point(x, btnY);
                btnArchive.Click += async (s, e) => await ArchiveAsync();
            }

            // ── Prefill ──
            if (existing != null)
            {
                inpFirstName.Text = existing.FirstName ?? "";
                inpLastName.Text = existing.LastName ?? "";
                inpEmail.Text = existing.Email ?? "";
                inpPhone.Text = existing.Phone ?? "";
                inpAddress.Text = existing.Address ?? "";
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
                    Address = inpAddress.Text.Trim()
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
                !inpEmail.Text.Contains('@'))
            {
                ShowError(inpEmail, lblErrorEmail, "Email must contain '@'.");
                firstInvalid ??= inpEmail;
                valid = false;
            }

            firstInvalid?.Focus();
            return valid;
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

        private Button MakeDangerOutlineButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9.5F),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Danger,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = AppTheme.Danger;
            b.FlatAppearance.MouseOverBackColor = AppTheme.DangerSoft;
            b.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(b, 8);
            pnlCard.Controls.Add(b);
            return b;
        }
    }
}