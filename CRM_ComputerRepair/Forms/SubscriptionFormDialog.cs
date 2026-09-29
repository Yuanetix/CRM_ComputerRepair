using CRM.winforms.Controls;
using CRM.winforms.Forms;
using System;
using System.ComponentModel;
using System.Drawing;
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

        private TextField inpName = null!;
        private NumericUpDown numPrice = null!;
        private ComboBox cmbDurationPreset = null!;
        private NumericUpDown numDurationMonths = null!;
        private NumericUpDown numMaxUsers = null!;
        private NumericUpDown numMaxDevices = null!;
        private CheckBox chkMultiBranch = null!;
        private TextField inpDescription = null!;
        private ComboBox cmbBilling = null!;
        private CheckBox chkIsActive = null!;

        private Label lblError = null!;
        private SaasButton btnSave = null!;
        private SaasButton btnCancel = null!;

        public SubscriptionDto? ResultPlan { get; private set; }

        public SubscriptionFormDialog(SubscriptionDto? existing)
        {
            _editing = existing;
            _isEditMode = existing != null;

            BuildCard(
                _isEditMode ? "Edit Plan" : "New Subscription Plan",
                subtitle: null,
                width: 640,
                height: 620);

            BuildContent();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // Plan Name
            ModalKit.MakeLabel(pnlBody, "Plan Name *", x, y);
            y += 20;
            inpName = ModalKit.MakeField(pnlBody, x, y, w, "Plan name");
            if (_isEditMode) inpName.Text = _editing!.SubscriptionName;
            y += 46;

            int gap = 16;
            int halfW = (w - gap) / 2;

            // Price (PHP) + Billing Cycle
            ModalKit.MakeLabel(pnlBody, "Price (PHP ₱) *", x, y);
            ModalKit.MakeLabel(pnlBody, "Billing Cycle", x + halfW + gap, y);
            y += 20;

            numPrice = MakeNumeric(pnlBody, x, y, halfW, 0, 1000000, _isEditMode ? _editing!.PricePerMonth : 999m, decimalPlaces: 2);
            cmbBilling = MakeCombo(pnlBody, x + halfW + gap, y, halfW, new[] { "Monthly", "Quarterly", "Yearly" }, 0);
            if (_isEditMode && !string.IsNullOrWhiteSpace(_editing!.BillingCycle))
            {
                int bIdx = cmbBilling.FindStringExact(_editing.BillingCycle);
                if (bIdx >= 0) cmbBilling.SelectedIndex = bIdx;
            }
            y += 46;

            // Duration Preset + Duration in Months
            ModalKit.MakeLabel(pnlBody, "Duration Preset", x, y);
            ModalKit.MakeLabel(pnlBody, "Duration (Months) *", x + halfW + gap, y);
            y += 20;

            cmbDurationPreset = MakeCombo(pnlBody, x, y, halfW, new[]
            {
                "1 Month",
                "3 Months (Quarterly)",
                "6 Months (Half-Year)",
                "12 Months (1 Year / Annual)",
                "24 Months (2 Years)",
                "Custom Months"
            }, 0);

            numDurationMonths = MakeNumeric(pnlBody, x + halfW + gap, y, halfW, 1, 120, _isEditMode ? Math.Max(1, _editing!.DurationMonths) : 1);

            cmbDurationPreset.SelectedIndexChanged += (s, e) =>
            {
                switch (cmbDurationPreset.SelectedIndex)
                {
                    case 0: numDurationMonths.Value = 1; break;
                    case 1: numDurationMonths.Value = 3; break;
                    case 2: numDurationMonths.Value = 6; break;
                    case 3: numDurationMonths.Value = 12; break;
                    case 4: numDurationMonths.Value = 24; break;
                }
            };

            if (_isEditMode)
            {
                int d = Math.Max(1, _editing!.DurationMonths);
                if (d == 1) cmbDurationPreset.SelectedIndex = 0;
                else if (d == 3) cmbDurationPreset.SelectedIndex = 1;
                else if (d == 6) cmbDurationPreset.SelectedIndex = 2;
                else if (d == 12) cmbDurationPreset.SelectedIndex = 3;
                else if (d == 24) cmbDurationPreset.SelectedIndex = 4;
                else cmbDurationPreset.SelectedIndex = 5;
            }
            y += 46;

            // Max Users + Max Devices
            ModalKit.MakeLabel(pnlBody, "Max Users *", x, y);
            ModalKit.MakeLabel(pnlBody, "Max Devices *", x + halfW + gap, y);
            y += 20;

            numMaxUsers = MakeNumeric(pnlBody, x, y, halfW, 1, 500, _isEditMode ? Math.Max(1, _editing!.MaxUsers) : 5);
            numMaxDevices = MakeNumeric(pnlBody, x + halfW + gap, y, halfW, 1, 10000, _isEditMode ? Math.Max(1, _editing!.MaxDevices) : 100);
            y += 46;

            // Multi-branching toggle
            chkMultiBranch = new CheckBox
            {
                Text = "Enable multi-branch management",
                Font = AppTheme.FontMedium,
                ForeColor = AppTheme.TextPrimary,
                Checked = _isEditMode && _editing!.EnableMultiBranching,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
            pnlBody.Controls.Add(chkMultiBranch);
            y += 34;

            // Description / Features
            ModalKit.MakeLabel(pnlBody, "Description", x, y);
            y += 20;
            inpDescription = ModalKit.MakeField(pnlBody, x, y, w, "Optional plan description");
            if (_isEditMode) inpDescription.Text = _editing!.Description ?? "";
            y += 46;

            // Active plan status
            chkIsActive = new CheckBox
            {
                Text = "Plan is active and available for subscription",
                Font = AppTheme.FontMedium,
                ForeColor = AppTheme.TextPrimary,
                Checked = !_isEditMode || _editing!.IsActive,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
            pnlBody.Controls.Add(chkIsActive);
            y += 34;

            // Error label
            lblError = new Label
            {
                Text = "",
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.Danger,
                AutoSize = true,
                MaximumSize = new Size(w, 0),
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Visible = false
            };
            pnlBody.Controls.Add(lblError);
            y += 32;

            // Buttons
            btnCancel = new SaasButton("Cancel", SaasButtonVariant.Secondary);
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            btnSave = new SaasButton(_isEditMode ? "Save Plan" : "Create Plan", SaasButtonVariant.Primary);
            btnSave.Click += async (s, e) => await SaveAsync();

            LayoutFooter(btnSave, btnCancel, saveW: 130, cancelW: 100);
        }

        private static NumericUpDown MakeNumeric(Control parent, int x, int y, int width, decimal min, decimal max, decimal value, int decimalPlaces = 0)
        {
            var num = new NumericUpDown
            {
                Font = AppTheme.FontInput,
                Location = new Point(x, y),
                Size = new Size(width, 36),
                Minimum = min,
                Maximum = max,
                Value = Math.Min(max, Math.Max(min, value)),
                DecimalPlaces = decimalPlaces,
                ThousandsSeparator = true
            };
            parent.Controls.Add(num);
            return num;
        }

        private static ComboBox MakeCombo(Control parent, int x, int y, int width, string[] items, int selectedIndex)
        {
            var cmb = new ComboBox
            {
                Font = AppTheme.FontInput,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(x, y),
                Size = new Size(width, 36),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat
            };
            cmb.Items.AddRange(items);
            if (selectedIndex >= 0 && selectedIndex < items.Length) cmb.SelectedIndex = selectedIndex;
            ModalKit.AdjustDropDownWidth(cmb);
            parent.Controls.Add(cmb);
            return cmb;
        }

        private async Task SaveAsync()
        {
            lblError.Visible = false;

            if (string.IsNullOrWhiteSpace(inpName.Text))
            {
                lblError.Text = "• Plan name is required.";
                lblError.Visible = true;
                inpName.Focus();
                return;
            }

            int months = (int)numDurationMonths.Value;
            string durationText = cmbDurationPreset.SelectedIndex < 5
                ? cmbDurationPreset.Text.Split('(')[0].Trim()
                : $"{months} Month(s)";

            var dto = new SubscriptionDto
            {
                SubscriptionId = _editing?.SubscriptionId ?? 0,
                SubscriptionName = inpName.Text.Trim(),
                PricePerMonth = numPrice.Value,
                DurationMonths = months,
                Duration = durationText,
                MaxUsers = (int)numMaxUsers.Value,
                MaxDevices = (int)numMaxDevices.Value,
                EnableMultiBranching = chkMultiBranch.Checked,
                Description = inpDescription.Text.Trim(),
                BillingCycle = cmbBilling.Text,
                IsActive = chkIsActive.Checked,
                IsArchived = _editing?.IsArchived ?? false,
                StartDate = _editing?.StartDate ?? DateTime.UtcNow,
                EndDate = _editing?.EndDate ?? DateTime.UtcNow.AddMonths(months)
            };

            btnSave.Enabled = false;
            btnSave.Text = "Saving...";

            try
            {
                if (_isEditMode)
                {
                    ResultPlan = await _api.UpdateSubscriptionAsync(_editing!.SubscriptionId, dto);
                }
                else
                {
                    ResultPlan = await _api.CreateSubscriptionAsync(dto);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                lblError.Text = "• " + ex.Message;
                lblError.Visible = true;
                btnSave.Enabled = true;
                btnSave.Text = _isEditMode ? "Save Plan" : "Create Plan";
            }
        }
    }
}
