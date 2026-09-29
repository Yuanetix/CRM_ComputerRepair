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
    [DesignerCategory("Code")]
    public class CompanyEditDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly CompanyDto _company;
        private List<SubscriptionDto> _plans = new();

        private TextField inpName = null!;
        private TextField inpPhone = null!;
        private TextField inpEmail = null!;

        private TextField inpAddress = null!;
        private TextField inpCity = null!;
        private TextField inpProvince = null!;
        private TextField inpPostal = null!;

        private ComboBox cmbPlan = null!;
        private TextField inpServer = null!;
        private TextField inpDbName = null!;
        private CheckBox chkIsActive = null!;

        private Label lblError = null!;
        private SaasButton btnSave = null!;
        private SaasButton btnCancel = null!;

        public CompanyDto? UpdatedCompany { get; private set; }

        public CompanyEditDialog(CompanyDto company)
        {
            _company = company;

            BuildCard(
                "Edit Business",
                subtitle: null,
                width: 680,
                height: 640);

            BuildContent();
            this.Load += async (s, e) => await LoadPlansAsync();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            int gap = 16;
            int halfW = (w - gap) / 2;

            // ── COMPANY ──
            ModalKit.MakeSection(pnlBody, "COMPANY DETAILS", x, y, w);
            y += 24;

            ModalKit.MakeLabel(pnlBody, "Company Name *", x, y);
            ModalKit.MakeLabel(pnlBody, "Company Code", x + halfW + gap, y);
            y += 20;

            inpName = ModalKit.MakeField(pnlBody, x, y, halfW, "Company name");
            inpName.Text = _company.CompanyName;

            var txtCode = ModalKit.MakeField(pnlBody, x + halfW + gap, y, halfW, "");
            txtCode.Text = _company.CompanyCode;
            txtCode.Enabled = false;
            y += 46;

            ModalKit.MakeLabel(pnlBody, "Phone", x, y);
            ModalKit.MakeLabel(pnlBody, "Email", x + halfW + gap, y);
            y += 20;

            inpPhone = ModalKit.MakeField(pnlBody, x, y, halfW, "+63 900 000 0000");
            inpPhone.Text = _company.ContactPhone ?? "";

            inpEmail = ModalKit.MakeField(pnlBody, x + halfW + gap, y, halfW, "contact@company.com");
            inpEmail.Text = _company.ContactEmail ?? "";
            y += 48;

            // ── LOCATION ──
            ModalKit.MakeSection(pnlBody, "LOCATION", x, y, w);
            y += 24;

            ModalKit.MakeLabel(pnlBody, "Street Address", x, y);
            y += 20;
            inpAddress = ModalKit.MakeField(pnlBody, x, y, w, "Building, street, area");
            inpAddress.Text = _company.Address ?? "";
            y += 46;

            ModalKit.MakeLabel(pnlBody, "City", x, y);
            ModalKit.MakeLabel(pnlBody, "Province / State", x + halfW + gap, y);
            y += 20;

            inpCity = ModalKit.MakeField(pnlBody, x, y, halfW, "City");
            inpCity.Text = _company.City ?? "";

            inpProvince = ModalKit.MakeField(pnlBody, x + halfW + gap, y, halfW, "Province / State");
            inpProvince.Text = _company.StateOrProvince ?? "";
            y += 46;

            ModalKit.MakeLabel(pnlBody, "Postal Code", x, y);
            y += 20;
            inpPostal = ModalKit.MakeField(pnlBody, x, y, halfW, "Postal code");
            inpPostal.Text = _company.PostalCode ?? "";
            y += 48;

            // ── SUBSCRIPTION & DATABASE ──
            ModalKit.MakeSection(pnlBody, "SUBSCRIPTION & DATABASE", x, y, w);
            y += 24;

            ModalKit.MakeLabel(pnlBody, "Subscription Plan", x, y);
            y += 20;

            cmbPlan = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = AppTheme.FontInput,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(x, y),
                Size = new Size(w, 38)
            };
            pnlBody.Controls.Add(cmbPlan);
            y += 46;

            ModalKit.MakeLabel(pnlBody, "Database Server", x, y);
            ModalKit.MakeLabel(pnlBody, "Tenant Database", x + halfW + gap, y);
            y += 20;

            inpServer = ModalKit.MakeField(pnlBody, x, y, halfW, @"(localdb)\MSSQLLocalDB");
            inpServer.Text = _company.DatabaseServer;

            inpDbName = ModalKit.MakeField(pnlBody, x + halfW + gap, y, halfW, "DB_TenantRepairs_...");
            inpDbName.Text = _company.DatabaseName;
            y += 48;

            // ── STATUS ──
            ModalKit.MakeSection(pnlBody, "ACCOUNT STATUS", x, y, w);
            y += 24;

            chkIsActive = new CheckBox
            {
                Text = "Business workspace is active and accessible",
                Font = AppTheme.FontMedium,
                ForeColor = AppTheme.TextPrimary,
                Checked = _company.IsActive,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
            pnlBody.Controls.Add(chkIsActive);
            y += 40;

            // Error label
            lblError = new Label
            {
                Text = "",
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.Danger,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 24),
                Visible = false
            };
            pnlBody.Controls.Add(lblError);
            y += 32;

            // Buttons
            btnCancel = new SaasButton("Cancel", SaasButtonVariant.Secondary);
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

            btnSave = new SaasButton("Save Changes", SaasButtonVariant.Primary);
            btnSave.Click += async (s, e) => await SaveAsync();

            LayoutFooter(btnSave, btnCancel, saveW: 140, cancelW: 100);
        }

        private async Task LoadPlansAsync()
        {
            try
            {
                _plans = await _api.GetSubscriptionsAsync(activeOnly: false);
                cmbPlan.Items.Clear();
                cmbPlan.Items.Add("— None / No Plan —");

                int selectedIdx = 0;
                for (int i = 0; i < _plans.Count; i++)
                {
                    var p = _plans[i];
                    string branch = p.EnableMultiBranching ? " [Multi-Branch]" : "";
                    string archived = p.IsArchived ? " (Archived)" : "";
                    cmbPlan.Items.Add($"{p.SubscriptionName} — ₱{p.PricePerMonth:N0} / {p.DurationDisplay}{branch}{archived}");

                    if (_company.SubscriptionId.HasValue && p.SubscriptionId == _company.SubscriptionId.Value)
                    {
                        selectedIdx = i + 1;
                    }
                }

                ModalKit.AdjustDropDownWidth(cmbPlan);
                cmbPlan.SelectedIndex = selectedIdx;
            }
            catch (Exception ex)
            {
                lblError.Text = $"Failed to load plans: {ex.Message}";
                lblError.Visible = true;
            }
        }

        private async Task SaveAsync()
        {
            lblError.Visible = false;

            if (string.IsNullOrWhiteSpace(inpName.Text))
            {
                lblError.Text = "• Company Name is required.";
                lblError.Visible = true;
                inpName.Focus();
                return;
            }

            int? planId = null;
            if (cmbPlan.SelectedIndex > 0 && cmbPlan.SelectedIndex - 1 < _plans.Count)
            {
                planId = _plans[cmbPlan.SelectedIndex - 1].SubscriptionId;
            }

            var req = new UpdateCompanyRequestDto
            {
                CompanyName = inpName.Text.Trim(),
                ContactPhone = inpPhone.Text.Trim(),
                ContactEmail = inpEmail.Text.Trim(),
                Address = inpAddress.Text.Trim(),
                City = inpCity.Text.Trim(),
                StateOrProvince = inpProvince.Text.Trim(),
                PostalCode = inpPostal.Text.Trim(),
                Country = "Philippines",
                SubscriptionId = planId,
                DatabaseServer = inpServer.Text.Trim(),
                DatabaseName = inpDbName.Text.Trim(),
                IsActive = chkIsActive.Checked
            };

            btnSave.Enabled = false;
            btnSave.Text = "Saving...";

            try
            {
                UpdatedCompany = await _api.UpdateCompanyAsync(_company.CompanyId, req);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                lblError.Text = "• " + ex.Message;
                lblError.Visible = true;
                btnSave.Enabled = true;
                btnSave.Text = "Save Changes";
            }
        }
    }
}
