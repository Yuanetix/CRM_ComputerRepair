using CRM.winforms.Controls;
using CRM.winforms.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    [DesignerCategory("Code")]
    public class CompanyRegisterDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private List<SubscriptionDto> _plans = new();

        // ── Controls ──
        private TextField inpName = null!;
        private TextField inpCode = null!;
        private SaasButton btnGenerateCode = null!;
        private TextField inpPhone = null!;
        private TextField inpEmail = null!;

        private TextField inpAddress = null!;
        private TextField inpCity = null!;
        private TextField inpProvince = null!;
        private TextField inpPostal = null!;

        private ComboBox cmbPlan = null!;
        private TextField inpServer = null!;
        private TextField inpDbName = null!;

        private TextField inpAdminFirst = null!;
        private TextField inpAdminLast = null!;
        private TextField inpAdminEmail = null!;
        private TextField inpAdminUser = null!;
        private TextField inpAdminPass = null!;
        private CheckBox chkShowPass = null!;

        private Label lblError = null!;
        private SaasButton btnRegister = null!;
        private SaasButton btnCancel = null!;

        public CompanyDto? RegisteredCompany { get; private set; }

        public CompanyRegisterDialog()
        {
            BuildCard(
                "Register Business",
                subtitle: null,
                width: 680,
                height: 680);

            BuildContent();
            this.Load += async (s, e) =>
            {
                await LoadPlansAsync();
                await AutoGenerateCodeAsync();
            };
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
            ModalKit.MakeLabel(pnlBody, "Company Code *", x + halfW + gap, y);
            y += 20;

            inpName = ModalKit.MakeField(pnlBody, x, y, halfW, "Company name");
            inpName.TextChanged += (s, e) => SuggestDatabaseName();

            int codeFieldW = halfW - 88;
            inpCode = ModalKit.MakeField(pnlBody, x + halfW + gap, y, codeFieldW, "CMP-YYYY-XXXX");
            inpCode.TextChanged += (s, e) => SuggestDatabaseName();

            btnGenerateCode = new SaasButton("Generate", SaasButtonVariant.Secondary)
            {
                Location = new Point(inpCode.Right + 8, y),
                Size = new Size(80, 38)
            };
            btnGenerateCode.Click += async (s, e) => await AutoGenerateCodeAsync();
            pnlBody.Controls.Add(btnGenerateCode);
            y += 46;

            ModalKit.MakeLabel(pnlBody, "Phone", x, y);
            ModalKit.MakeLabel(pnlBody, "Email", x + halfW + gap, y);
            y += 20;

            inpPhone = ModalKit.MakeField(pnlBody, x, y, halfW, "+63 900 000 0000");
            inpEmail = ModalKit.MakeField(pnlBody, x + halfW + gap, y, halfW, "contact@company.com");
            y += 48;

            // ── LOCATION ──
            ModalKit.MakeSection(pnlBody, "LOCATION", x, y, w);
            y += 24;

            ModalKit.MakeLabel(pnlBody, "Street Address", x, y);
            y += 20;
            inpAddress = ModalKit.MakeField(pnlBody, x, y, w, "Building, street, area");
            y += 46;

            ModalKit.MakeLabel(pnlBody, "City", x, y);
            ModalKit.MakeLabel(pnlBody, "Province / State", x + halfW + gap, y);
            y += 20;

            inpCity = ModalKit.MakeField(pnlBody, x, y, halfW, "City");
            inpProvince = ModalKit.MakeField(pnlBody, x + halfW + gap, y, halfW, "Province / State");
            y += 46;

            ModalKit.MakeLabel(pnlBody, "Postal Code", x, y);
            y += 20;
            inpPostal = ModalKit.MakeField(pnlBody, x, y, halfW, "Postal code");
            y += 48;

            // ── SUBSCRIPTION & DATABASE ──
            ModalKit.MakeSection(pnlBody, "SUBSCRIPTION & DATABASE", x, y, w);
            y += 24;

            ModalKit.MakeLabel(pnlBody, "Subscription Plan *", x, y);
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

            ModalKit.MakeLabel(pnlBody, "Database Server *", x, y);
            ModalKit.MakeLabel(pnlBody, "Tenant Database", x + halfW + gap, y);
            y += 20;

            inpServer = ModalKit.MakeField(pnlBody, x, y, halfW, @"(localdb)\MSSQLLocalDB");
            inpServer.Text = @"(localdb)\MSSQLLocalDB";

            inpDbName = ModalKit.MakeField(pnlBody, x + halfW + gap, y, halfW, "DB_TenantRepairs_...");
            y += 48;

            // ── ADMINISTRATOR ──
            ModalKit.MakeSection(pnlBody, "ADMINISTRATOR ACCOUNT", x, y, w);
            y += 24;

            ModalKit.MakeLabel(pnlBody, "First Name *", x, y);
            ModalKit.MakeLabel(pnlBody, "Last Name *", x + halfW + gap, y);
            y += 20;

            inpAdminFirst = ModalKit.MakeField(pnlBody, x, y, halfW, "First name");
            inpAdminLast = ModalKit.MakeField(pnlBody, x + halfW + gap, y, halfW, "Last name");
            y += 46;

            ModalKit.MakeLabel(pnlBody, "Admin Email *", x, y);
            ModalKit.MakeLabel(pnlBody, "Username *", x + halfW + gap, y);
            y += 20;

            inpAdminEmail = ModalKit.MakeField(pnlBody, x, y, halfW, "admin@company.com");
            inpAdminEmail.TextChanged += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(inpAdminUser.Text) || inpAdminUser.Tag as string == "auto")
                {
                    string suggested = inpAdminEmail.Text.Contains("@") ? inpAdminEmail.Text.Split('@')[0] : inpAdminEmail.Text;
                    inpAdminUser.Text = Regex.Replace(suggested.ToLowerInvariant(), @"[^a-z0-9_.]", "");
                    inpAdminUser.Tag = "auto";
                }
            };

            inpAdminUser = ModalKit.MakeField(pnlBody, x + halfW + gap, y, halfW, "username");
            inpAdminUser.KeyDown += (s, e) => inpAdminUser.Tag = "manual";
            y += 46;

            ModalKit.MakeLabel(pnlBody, "Password *", x, y);
            y += 20;

            inpAdminPass = ModalKit.MakeField(pnlBody, x, y, halfW, "••••••••");
            inpAdminPass.IsPassword = true;
            inpAdminPass.Text = "Admin@123";

            chkShowPass = new CheckBox
            {
                Text = "Show password",
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x + halfW + gap, y + 8)
            };
            chkShowPass.CheckedChanged += (s, e) => inpAdminPass.IsPassword = !chkShowPass.Checked;
            pnlBody.Controls.Add(chkShowPass);
            y += 48;

            // Error banner
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

            // Footer buttons
            btnCancel = new SaasButton("Cancel", SaasButtonVariant.Secondary);
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

            btnRegister = new SaasButton("Register Business", SaasButtonVariant.Primary);
            btnRegister.Click += async (s, e) => await SaveAsync();

            LayoutFooter(btnRegister, btnCancel, saveW: 160, cancelW: 100);
        }

        private async Task LoadPlansAsync()
        {
            try
            {
                _plans = await _api.GetSubscriptionsAsync(activeOnly: true);
                cmbPlan.Items.Clear();

                foreach (var p in _plans)
                {
                    string branchTxt = p.EnableMultiBranching ? " [Multi-Branch]" : "";
                    cmbPlan.Items.Add($"{p.SubscriptionName} — ₱{p.PricePerMonth:N0} / {p.DurationDisplay}{branchTxt}");
                }

                ModalKit.AdjustDropDownWidth(cmbPlan);

                if (cmbPlan.Items.Count > 0)
                {
                    // Select Professional by default if found, else first
                    int defaultIdx = _plans.FindIndex(p => p.SubscriptionName.Contains("Professional", StringComparison.OrdinalIgnoreCase));
                    cmbPlan.SelectedIndex = defaultIdx >= 0 ? defaultIdx : 0;
                }
            }
            catch (Exception ex)
            {
                lblError.Text = $"Failed to load subscription plans: {ex.Message}";
                lblError.Visible = true;
            }
        }

        private async Task AutoGenerateCodeAsync()
        {
            try
            {
                string code = await _api.GenerateCompanyCodeAsync();
                inpCode.Text = code;
                SuggestDatabaseName();
            }
            catch
            {
                inpCode.Text = $"CMP-{DateTime.UtcNow.Year}-{new Random().Next(1000, 9999)}";
                SuggestDatabaseName();
            }
        }

        private void SuggestDatabaseName()
        {
            string baseCode = !string.IsNullOrWhiteSpace(inpCode.Text)
                ? inpCode.Text.Trim()
                : inpName.Text.Trim();

            string clean = Regex.Replace(baseCode, @"[^a-zA-Z0-9_]", "_");
            if (string.IsNullOrWhiteSpace(clean)) clean = "NewTenant";
            inpDbName.Text = $"DB_TenantRepairs_{clean}";
        }

        private async Task SaveAsync()
        {
            lblError.Visible = false;

            if (string.IsNullOrWhiteSpace(inpName.Text))
            {
                ShowError("Company Name is required.");
                inpName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(inpCode.Text))
            {
                ShowError("Company Code is required.");
                inpCode.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(inpAdminFirst.Text))
            {
                ShowError("Admin First Name is required.");
                inpAdminFirst.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(inpAdminLast.Text))
            {
                ShowError("Admin Last Name is required.");
                inpAdminLast.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(inpAdminEmail.Text) || !inpAdminEmail.Text.Contains("@"))
            {
                ShowError("A valid Admin Email address is required.");
                inpAdminEmail.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(inpAdminUser.Text))
            {
                ShowError("Admin Username is required.");
                inpAdminUser.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(inpAdminPass.Text) || inpAdminPass.Text.Length < 6)
            {
                ShowError("Admin Password must be at least 6 characters.");
                inpAdminPass.Focus();
                return;
            }

            int? selectedPlanId = null;
            if (cmbPlan.SelectedIndex >= 0 && cmbPlan.SelectedIndex < _plans.Count)
            {
                selectedPlanId = _plans[cmbPlan.SelectedIndex].SubscriptionId;
            }

            var req = new RegisterCompanyRequestDto
            {
                CompanyName = inpName.Text.Trim(),
                CompanyCode = inpCode.Text.Trim().ToUpperInvariant(),
                ContactPhone = inpPhone.Text.Trim(),
                ContactEmail = inpEmail.Text.Trim(),
                Address = inpAddress.Text.Trim(),
                City = inpCity.Text.Trim(),
                StateOrProvince = inpProvince.Text.Trim(),
                PostalCode = inpPostal.Text.Trim(),
                Country = "Philippines",
                DatabaseServer = string.IsNullOrWhiteSpace(inpServer.Text) ? @"(localdb)\MSSQLLocalDB" : inpServer.Text.Trim(),
                DatabaseName = string.IsNullOrWhiteSpace(inpDbName.Text) ? $"DB_TenantRepairs_{inpCode.Text.Trim().Replace("-", "_")}" : inpDbName.Text.Trim(),
                SubscriptionId = selectedPlanId,
                AdminFirstName = inpAdminFirst.Text.Trim(),
                AdminLastName = inpAdminLast.Text.Trim(),
                AdminEmail = inpAdminEmail.Text.Trim(),
                AdminUsername = inpAdminUser.Text.Trim(),
                AdminPassword = inpAdminPass.Text
            };

            btnRegister.Enabled = false;
            btnRegister.Text = "Registering...";

            try
            {
                RegisteredCompany = await _api.RegisterCompanyAsync(req);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                btnRegister.Enabled = true;
                btnRegister.Text = "Register Business";
            }
        }

        private void ShowError(string msg)
        {
            lblError.Text = "• " + msg;
            lblError.Visible = true;
        }
    }
}
