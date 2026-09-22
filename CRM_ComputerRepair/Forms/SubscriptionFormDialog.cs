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
    public class SubscriptionFormDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly SubscriptionDto? _editing;
        private readonly bool _isEditMode;

        private Label lblSubtitle = null!;

        private Label lblName = null!;
        private Label lblPrice = null!;
        private Label lblMaxUsers = null!;
        private Label lblMaxDevices = null!;
        private Label lblBilling = null!;
        private Label lblStart = null!;
        private Label lblEnd = null!;

        private TextField inpName = null!;
        private NumericUpDown numPrice = null!;
        private NumericUpDown numMaxUsers = null!;
        private NumericUpDown numMaxDevices = null!;
        private ComboBox cmbBilling = null!;
        private DateTimePicker dtpStart = null!;
        private DateTimePicker dtpEnd = null!;

        private Label lblErrorName = null!;

        private Button btnSave = null!;
        private Button btnCancel = null!;

        public SubscriptionFormDialog(SubscriptionDto? existing)
        {
            _editing = existing;
            _isEditMode = existing != null;

            BuildCard(
                _isEditMode ? "Edit Subscription" : "Add Subscription",
                width: 560,
                height: _isEditMode ? 700 : 680);

            BuildContent();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            lblSubtitle = new Label
            {
                Text = _isEditMode
                    ? "Update this subscription plan."
                    : "Create a new subscription plan for a company.",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 20)
            };
            pnlCard.Controls.Add(lblSubtitle);
            y += 32;

            // Name
            lblName = MakeLabel("Subscription name *", x, y);
            y += 20;
            inpName = MakeField(x, y, w, "e.g. Business Plan");
            y += 38 + 4;
            lblErrorName = MakeErrorLabel(x, y);
            y += 20;

            // Price + Billing cycle
            int halfW = (w - 12) / 2;

            lblPrice = MakeLabel("Price per month (₱)", x, y);
            lblBilling = MakeLabel("Billing cycle", x + halfW + 12, y);
            y += 20;

            numPrice = MakeNumeric(x, y, halfW, 0, 1000000, 999, decimalPlaces: 2);
            cmbBilling = MakeCombo(x + halfW + 12, y, halfW,
                new[] { "Monthly", "Quarterly", "Yearly" }, 0);
            y += 38 + 14;

            // Max users + Max devices
            lblMaxUsers = MakeLabel("Max users", x, y);
            lblMaxDevices = MakeLabel("Max devices", x + halfW + 12, y);
            y += 20;

            numMaxUsers = MakeNumeric(x, y, halfW, 1, 100000, 10);
            numMaxDevices = MakeNumeric(x + halfW + 12, y, halfW, 1, 100000, 10);
            y += 38 + 14;

            // Start / End
            lblStart = MakeLabel("Start date *", x, y);
            lblEnd = MakeLabel("End date *", x + halfW + 12, y);
            y += 20;

            dtpStart = MakeDate(x, y, halfW);
            dtpEnd = MakeDate(x + halfW + 12, y, halfW);
            y += 38 + 20;

            // Buttons
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

            // Prefill
            if (_editing != null)
            {
                inpName.Text = _editing.SubscriptionName ?? "";
                numPrice.Value = Math.Max(0, Math.Min(1000000, _editing.PricePerMonth));
                numMaxUsers.Value = Math.Max(1, Math.Min(100000, _editing.MaxUsers));
                numMaxDevices.Value = Math.Max(1, Math.Min(100000, _editing.MaxDevices));

                var cycle = _editing.BillingCycle ?? "Monthly";
                var idx = Array.IndexOf(new[] { "Monthly", "Quarterly", "Yearly" }, cycle);
                cmbBilling.SelectedIndex = idx >= 0 ? idx : 0;

                dtpStart.Value = _editing.StartDate;
                dtpEnd.Value = _editing.EndDate;
            }
            else
            {
                dtpStart.Value = DateTime.Today;
                dtpEnd.Value = DateTime.Today.AddYears(1);
            }

            Shown += (s, e) => inpName.Focus();
        }

        private async Task SaveAsync()
        {
            if (!ValidateInputs()) return;

            try
            {
                var dto = new SubscriptionDto
                {
                    SubscriptionName = inpName.Text.Trim(),
                    PricePerMonth = numPrice.Value,
                    MaxUsers = (int)numMaxUsers.Value,
                    MaxDevices = (int)numMaxDevices.Value,
                    BillingCycle = cmbBilling.SelectedItem?.ToString() ?? "Monthly",
                    StartDate = dtpStart.Value,
                    EndDate = dtpEnd.Value,
                    IsActive = _editing?.IsActive ?? true
                };

                if (_isEditMode && _editing != null)
                    await _api.UpdateSubscriptionAsync(_editing.SubscriptionId, dto);
                else
                    await _api.CreateSubscriptionAsync(dto);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool ValidateInputs()
        {
            lblErrorName.Visible = false;
            inpName.HasError = false;

            if (string.IsNullOrWhiteSpace(inpName.Text))
            {
                inpName.HasError = true;
                lblErrorName.Text = "⚠  Subscription name is required.";
                lblErrorName.Visible = true;
                inpName.Focus();
                return false;
            }

            if (dtpEnd.Value <= dtpStart.Value)
            {
                MessageBox.Show("End date must be after start date.",
                    "Invalid dates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        // ─── Factories ───

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

        private NumericUpDown MakeNumeric(int x, int y, int width, decimal min, decimal max, decimal initial, int decimalPlaces = 0)
        {
            var n = new NumericUpDown
            {
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(x, y),
                Size = new Size(width, 30),
                Minimum = min,
                Maximum = max,
                Value = initial,
                DecimalPlaces = decimalPlaces,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlCard.Controls.Add(n);
            return n;
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

        private DateTimePicker MakeDate(int x, int y, int width)
        {
            var dtp = new DateTimePicker
            {
                Font = new Font("Segoe UI", 9.5F),
                Format = DateTimePickerFormat.Short,
                Location = new Point(x, y),
                Size = new Size(width, 30)
            };
            pnlCard.Controls.Add(dtp);
            return dtp;
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