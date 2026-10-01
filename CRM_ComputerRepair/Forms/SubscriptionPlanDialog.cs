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
    public class SubscriptionPlanDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly SubscriptionPlanDto? _existingPlan;
        private readonly bool _isEdit;

        private TextBox txtPlanCode = null!;
        private TextBox txtPlanName = null!;
        private NumericUpDown numPrice = null!;
        private ComboBox cmbBilling = null!;
        private NumericUpDown numMaxUsers = null!;
        private NumericUpDown numMaxBranches = null!;
        private NumericUpDown numMaxDevices = null!;
        private TextBox txtDescription = null!;

        private Panel pnlModules = null!;
        private readonly Dictionary<string, CheckBox> _moduleCheckboxes = new();
        private Label lblSelectedModulesSummary = null!;
        private Label lblError = null!;
        private SaasButton btnSave = null!;
        private SaasButton btnCancel = null!;

        private List<AppModuleDto> _allModules = new();

        public SubscriptionPlanDialog(SubscriptionPlanDto? existingPlan = null)
        {
            _existingPlan = existingPlan;
            _isEdit = existingPlan != null;

            string title = _isEdit ? $"Edit Plan — {existingPlan!.PlanName}" : "Create Subscription Plan";
            string subtitle = _isEdit
                ? "Configure plan pricing, limits, and bundled modules"
                : "Define a new subscription package and select the modules included by default";

            BuildCard(title, subtitle, width: 720, height: 680);
            BuildContent();
            this.Load += async (s, e) => await LoadDataAsync();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int y = ContentTopY;
            int w = ContentWidth;
            int colW = (w - 20) / 2;

            // Row 1: Plan Code & Plan Name
            var lblCode = new Label { Text = "Plan Code (Identifier):", Font = UiKit.SmallStrong, ForeColor = UiKit.InkMuted, Location = new Point(x, y), Size = new Size(colW, 18) };
            pnlBody.Controls.Add(lblCode);

            var lblName = new Label { Text = "Plan Display Name:", Font = UiKit.SmallStrong, ForeColor = UiKit.InkMuted, Location = new Point(x + colW + 20, y), Size = new Size(colW, 18) };
            pnlBody.Controls.Add(lblName);
            y += 20;

            txtPlanCode = new TextBox
            {
                Font = UiKit.Body,
                Location = new Point(x, y),
                Size = new Size(colW, 26),
                CharacterCasing = CharacterCasing.Upper,
                Enabled = !_isEdit,
                Text = _existingPlan?.PlanCode ?? ""
            };
            pnlBody.Controls.Add(txtPlanCode);

            txtPlanName = new TextBox
            {
                Font = UiKit.Body,
                Location = new Point(x + colW + 20, y),
                Size = new Size(colW, 26),
                Text = _existingPlan?.PlanName ?? ""
            };
            pnlBody.Controls.Add(txtPlanName);
            y += 34;

            // Row 2: Price & Billing Interval
            var lblPrice = new Label { Text = "Monthly Base Price (₱):", Font = UiKit.SmallStrong, ForeColor = UiKit.InkMuted, Location = new Point(x, y), Size = new Size(colW, 18) };
            pnlBody.Controls.Add(lblPrice);

            var lblBilling = new Label { Text = "Billing Interval:", Font = UiKit.SmallStrong, ForeColor = UiKit.InkMuted, Location = new Point(x + colW + 20, y), Size = new Size(colW, 18) };
            pnlBody.Controls.Add(lblBilling);
            y += 20;

            numPrice = new NumericUpDown
            {
                Font = UiKit.Body,
                Location = new Point(x, y),
                Size = new Size(colW, 26),
                Maximum = 1000000,
                DecimalPlaces = 2,
                Increment = 100,
                Value = _existingPlan?.Price ?? 1500m
            };
            pnlBody.Controls.Add(numPrice);

            cmbBilling = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UiKit.Body,
                Location = new Point(x + colW + 20, y),
                Size = new Size(colW, 26)
            };
            cmbBilling.Items.AddRange(new object[] { "Monthly", "Quarterly", "Annual" });
            cmbBilling.SelectedItem = _existingPlan?.BillingInterval ?? "Monthly";
            if (cmbBilling.SelectedIndex < 0) cmbBilling.SelectedIndex = 0;
            pnlBody.Controls.Add(cmbBilling);
            y += 34;

            // Row 3: Limits (Max Users, Max Branches, Max Devices)
            int limitW = (w - 24) / 3;
            var lblUsers = new Label { Text = "Max Users:", Font = UiKit.SmallStrong, ForeColor = UiKit.InkMuted, Location = new Point(x, y), Size = new Size(limitW, 18) };
            var lblBranches = new Label { Text = "Max Branches:", Font = UiKit.SmallStrong, ForeColor = UiKit.InkMuted, Location = new Point(x + limitW + 12, y), Size = new Size(limitW, 18) };
            var lblDevices = new Label { Text = "Max Devices:", Font = UiKit.SmallStrong, ForeColor = UiKit.InkMuted, Location = new Point(x + (limitW + 12) * 2, y), Size = new Size(limitW, 18) };
            pnlBody.Controls.Add(lblUsers);
            pnlBody.Controls.Add(lblBranches);
            pnlBody.Controls.Add(lblDevices);
            y += 20;

            numMaxUsers = new NumericUpDown { Font = UiKit.Body, Location = new Point(x, y), Size = new Size(limitW, 26), Maximum = 1000, Value = _existingPlan?.MaxUsers ?? 10 };
            numMaxBranches = new NumericUpDown { Font = UiKit.Body, Location = new Point(x + limitW + 12, y), Size = new Size(limitW, 26), Maximum = 100, Value = _existingPlan?.MaxBranches ?? 1 };
            numMaxDevices = new NumericUpDown { Font = UiKit.Body, Location = new Point(x + (limitW + 12) * 2, y), Size = new Size(limitW, 26), Maximum = 50000, Increment = 50, Value = _existingPlan?.MaxDevices ?? 500 };
            pnlBody.Controls.Add(numMaxUsers);
            pnlBody.Controls.Add(numMaxBranches);
            pnlBody.Controls.Add(numMaxDevices);
            y += 34;

            // Description
            var lblDesc = new Label { Text = "Description:", Font = UiKit.SmallStrong, ForeColor = UiKit.InkMuted, Location = new Point(x, y), Size = new Size(w, 18) };
            pnlBody.Controls.Add(lblDesc);
            y += 20;

            txtDescription = new TextBox
            {
                Font = UiKit.Body,
                Location = new Point(x, y),
                Size = new Size(w, 44),
                Multiline = true,
                Text = _existingPlan?.Description ?? ""
            };
            pnlBody.Controls.Add(txtDescription);
            y += 50;

            // Section: Bundled Modules
            var lblModHeader = new Label
            {
                Text = "Included Modules (Bundled in this Plan):",
                Font = UiKit.Section,
                ForeColor = UiKit.Ink,
                Location = new Point(x, y),
                Size = new Size(w, 20)
            };
            pnlBody.Controls.Add(lblModHeader);
            y += 24;

            pnlModules = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, 180),
                AutoScroll = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlBody.Controls.Add(pnlModules);
            y += 186;

            lblSelectedModulesSummary = new Label
            {
                Text = "0 module(s) selected",
                Font = UiKit.Small,
                ForeColor = UiKit.InkMuted,
                Location = new Point(x, y),
                Size = new Size(w, 20)
            };
            pnlBody.Controls.Add(lblSelectedModulesSummary);
            y += 22;

            // Error Label
            lblError = new Label
            {
                Font = UiKit.Small,
                ForeColor = Color.FromArgb(239, 68, 68),
                Location = new Point(x, y),
                Size = new Size(w, 20),
                Visible = false
            };
            pnlBody.Controls.Add(lblError);

            // Footer
            btnCancel = new SaasButton("Cancel", SaasButtonVariant.Secondary);
            btnCancel.Size = new Size(100, 36);
            btnCancel.Location = new Point(ContentRightX - 220, 12);
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            pnlFooter.Controls.Add(btnCancel);

            btnSave = new SaasButton(_isEdit ? "Update Plan" : "Create Plan", SaasButtonVariant.Primary);
            btnSave.Size = new Size(130, 36);
            btnSave.Location = new Point(ContentRightX - 110, 12);
            btnSave.Click += async (s, e) => await SavePlanAsync();
            pnlFooter.Controls.Add(btnSave);
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _allModules = await _api.GetAppModulesAsync();
                pnlModules.Controls.Clear();
                _moduleCheckboxes.Clear();

                var includedCodes = new HashSet<string>(
                    _existingPlan?.IncludedModules.Select(m => m.ModuleCode) ?? Enumerable.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase);

                int itemY = 6;
                int itemW = pnlModules.ClientSize.Width - 20;

                foreach (var mod in _allModules)
                {
                    var chk = new CheckBox
                    {
                        Text = $"{mod.ModuleName}  ({mod.ModuleCode}) — ₱{mod.PricePerMonth:N2}/mo",
                        Font = UiKit.Body,
                        ForeColor = UiKit.Ink,
                        Location = new Point(10, itemY),
                        Size = new Size(itemW, 26),
                        Checked = includedCodes.Contains(mod.ModuleCode),
                        Tag = mod
                    };
                    chk.CheckedChanged += (s, e) => UpdateSummary();
                    pnlModules.Controls.Add(chk);
                    _moduleCheckboxes[mod.ModuleCode] = chk;
                    itemY += 32;
                }

                UpdateSummary();
            }
            catch (Exception ex)
            {
                lblError.Text = $"Failed loading modules: {ex.Message}";
                lblError.Visible = true;
            }
        }

        private void UpdateSummary()
        {
            int checkedCount = _moduleCheckboxes.Values.Count(c => c.Checked);
            lblSelectedModulesSummary.Text = $"{checkedCount} module(s) included in this plan package.";
        }

        private async Task SavePlanAsync()
        {
            try
            {
                lblError.Visible = false;
                btnSave.Enabled = false;

                string name = txtPlanName.Text.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    lblError.Text = "Plan name is required.";
                    lblError.Visible = true;
                    return;
                }

                var selectedModules = _moduleCheckboxes
                    .Where(kv => kv.Value.Checked)
                    .Select(kv => kv.Key)
                    .ToList();

                if (_isEdit)
                {
                    var req = new UpdatePlanRequest
                    {
                        PlanName = name,
                        Description = txtDescription.Text.Trim(),
                        Price = numPrice.Value,
                        BillingInterval = cmbBilling.SelectedItem?.ToString() ?? "Monthly",
                        MaxUsers = (int)numMaxUsers.Value,
                        MaxBranches = (int)numMaxBranches.Value,
                        MaxDevices = (int)numMaxDevices.Value,
                        ModuleCodes = selectedModules
                    };

                    await _api.UpdatePlanAsync(_existingPlan!.PlanId, req);
                    Toast.Notify(this.Owner ?? this, "Plan Updated", $"Saved changes for plan '{name}'.", ToastKind.Success);
                }
                else
                {
                    string code = txtPlanCode.Text.Trim().ToUpperInvariant();
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        lblError.Text = "Plan code is required.";
                        lblError.Visible = true;
                        return;
                    }

                    var req = new CreatePlanRequest
                    {
                        PlanCode = code,
                        PlanName = name,
                        Description = txtDescription.Text.Trim(),
                        Price = numPrice.Value,
                        BillingInterval = cmbBilling.SelectedItem?.ToString() ?? "Monthly",
                        MaxUsers = (int)numMaxUsers.Value,
                        MaxBranches = (int)numMaxBranches.Value,
                        MaxDevices = (int)numMaxDevices.Value,
                        ModuleCodes = selectedModules
                    };

                    await _api.CreatePlanAsync(req);
                    Toast.Notify(this.Owner ?? this, "Plan Created", $"Created new plan '{name}'.", ToastKind.Success);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                lblError.Text = ex.Message;
                lblError.Visible = true;
            }
            finally
            {
                btnSave.Enabled = true;
            }
        }
    }
}
