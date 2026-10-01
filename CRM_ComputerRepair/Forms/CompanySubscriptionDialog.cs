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
    public class CompanySubscriptionDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly int _companyId;
        private readonly string _companyName;

        private CompanySubscriptionDetailDto? _detail;
        private List<SubscriptionPlanDto> _allPlans = new();
        private List<AppModuleDto> _allModules = new();

        private Label lblCompany = null!;
        private ComboBox cmbPlans = null!;
        private ComboBox cmbStatus = null!;
        private DateTimePicker dtpEndDate = null!;

        private Panel pnlIncludedModules = null!;

        private Panel pnlAddonsList = null!;
        private readonly Dictionary<string, CheckBox> _addonCheckboxes = new();

        private Label lblPlanPrice = null!;
        private Label lblAddonTotal = null!;
        private Label lblMonthlyTotal = null!;
        private Label lblCalculationBreakdown = null!;
        private Label lblError = null!;

        private SaasButton btnAuditHistory = null!;
        private SaasButton btnSave = null!;
        private SaasButton btnCancel = null!;

        public CompanySubscriptionDialog(int companyId, string companyName)
        {
            _companyId = companyId;
            _companyName = companyName;

            BuildCard(
                "Manage Company Subscription",
                $"Configure plan tier, optional module add-ons, and billing for {companyName}",
                width: 760,
                height: 700);

            BuildContent();
            this.Load += async (s, e) => await LoadDataAsync();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // Row 1: Company Header & History Button
            lblCompany = new Label
            {
                Text = $"Tenant: {_companyName} (ID: {_companyId})",
                Font = UiKit.Section,
                ForeColor = UiKit.Ink,
                Location = new Point(x, y),
                Size = new Size(w - 180, 24)
            };
            pnlBody.Controls.Add(lblCompany);

            btnAuditHistory = new SaasButton("Audit History", SaasButtonVariant.Secondary);
            btnAuditHistory.Size = new Size(130, 28);
            btnAuditHistory.Location = new Point(x + w - 130, y - 2);
            btnAuditHistory.Click += (s, e) =>
            {
                using var dlg = new SubscriptionHistoryDialog(_companyId, _companyName);
                dlg.ShowDialog(this);
            };
            pnlBody.Controls.Add(btnAuditHistory);
            y += 32;

            // Row 2: Plan Selection, Status, Expiry
            int col3W = (w - 24) / 3;

            var lblPlanTitle = new Label { Text = "Subscription Plan:", Font = UiKit.SmallStrong, ForeColor = UiKit.InkMuted, Location = new Point(x, y), Size = new Size(col3W, 18) };
            var lblStatusTitle = new Label { Text = "Status:", Font = UiKit.SmallStrong, ForeColor = UiKit.InkMuted, Location = new Point(x + col3W + 12, y), Size = new Size(col3W, 18) };
            var lblExpiryTitle = new Label { Text = "Valid Until:", Font = UiKit.SmallStrong, ForeColor = UiKit.InkMuted, Location = new Point(x + (col3W + 12) * 2, y), Size = new Size(col3W, 18) };

            pnlBody.Controls.Add(lblPlanTitle);
            pnlBody.Controls.Add(lblStatusTitle);
            pnlBody.Controls.Add(lblExpiryTitle);
            y += 20;

            cmbPlans = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UiKit.Body,
                Location = new Point(x, y),
                Size = new Size(col3W, 26)
            };
            cmbPlans.SelectedIndexChanged += (s, e) => OnPlanChanged();
            pnlBody.Controls.Add(cmbPlans);

            cmbStatus = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UiKit.Body,
                Location = new Point(x + col3W + 12, y),
                Size = new Size(col3W, 26)
            };
            cmbStatus.Items.AddRange(new object[] { "Active", "Suspended", "Inactive" });
            cmbStatus.SelectedIndex = 0;
            pnlBody.Controls.Add(cmbStatus);

            dtpEndDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Font = UiKit.Body,
                Location = new Point(x + (col3W + 12) * 2, y),
                Size = new Size(col3W, 26),
                Value = DateTime.UtcNow.AddYears(1)
            };
            pnlBody.Controls.Add(dtpEndDate);
            y += 36;

            // Section 1: Included Modules in Selected Plan
            var lblSec1 = new Label
            {
                Text = "Included Modules (Bundled with Plan):",
                Font = UiKit.SmallStrong,
                ForeColor = UiKit.Ink,
                Location = new Point(x, y),
                Size = new Size(w, 18)
            };
            pnlBody.Controls.Add(lblSec1);
            y += 20;

            pnlIncludedModules = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, 64),
                BackColor = Color.FromArgb(249, 250, 251),
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true
            };
            pnlBody.Controls.Add(pnlIncludedModules);
            y += 72;

            // Section 2: Optional Add-on Modules
            var lblSec2 = new Label
            {
                Text = "Optional Module Add-ons:",
                Font = UiKit.SmallStrong,
                ForeColor = UiKit.Ink,
                Location = new Point(x, y),
                Size = new Size(w, 18)
            };
            pnlBody.Controls.Add(lblSec2);
            y += 20;

            pnlAddonsList = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, 150),
                AutoScroll = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlBody.Controls.Add(pnlAddonsList);
            y += 158;

            // Pricing Summary Card
            var pnlSummary = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, 100),
                BackColor = Color.FromArgb(243, 244, 246),
                BorderStyle = BorderStyle.None
            };
            pnlBody.Controls.Add(pnlSummary);

            lblPlanPrice = new Label
            {
                Text = "Plan Price: ₱0.00/mo",
                Font = UiKit.SmallStrong,
                ForeColor = UiKit.InkMuted,
                Location = new Point(12, 10),
                Size = new Size(220, 20)
            };
            pnlSummary.Controls.Add(lblPlanPrice);

            lblAddonTotal = new Label
            {
                Text = "Add-on Total: ₱0.00/mo",
                Font = UiKit.SmallStrong,
                ForeColor = UiKit.InkMuted,
                Location = new Point(240, 10),
                Size = new Size(220, 20)
            };
            pnlSummary.Controls.Add(lblAddonTotal);

            lblMonthlyTotal = new Label
            {
                Text = "Company Monthly Total: ₱0.00 / month",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(12, 34),
                Size = new Size(w - 24, 26)
            };
            pnlSummary.Controls.Add(lblMonthlyTotal);

            lblCalculationBreakdown = new Label
            {
                Text = "Formula: Monthly Total = Plan Base Price + Active Add-ons",
                Font = UiKit.Small,
                ForeColor = UiKit.InkMuted,
                Location = new Point(12, 64),
                Size = new Size(w - 24, 28)
            };
            pnlSummary.Controls.Add(lblCalculationBreakdown);
            y += 108;

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

            // Footer Buttons
            btnCancel = new SaasButton("Cancel", SaasButtonVariant.Secondary);
            btnCancel.Size = new Size(110, 36);
            btnCancel.Location = new Point(ContentRightX - 250, 12);
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            pnlFooter.Controls.Add(btnCancel);

            btnSave = new SaasButton("Save Subscription", SaasButtonVariant.Primary);
            btnSave.Size = new Size(160, 36);
            btnSave.Location = new Point(ContentRightX - 130, 12);
            btnSave.Click += async (s, e) => await SaveChangesAsync();
            pnlFooter.Controls.Add(btnSave);
        }

        private async Task LoadDataAsync()
        {
            try
            {
                btnSave.Enabled = false;

                var plansTask = _api.GetPlansAsync();
                var modulesTask = _api.GetAppModulesAsync();
                var subTask = _api.GetCompanySubscriptionAsync(_companyId);

                await Task.WhenAll(plansTask, modulesTask, subTask);

                _allPlans = (await plansTask) ?? new List<SubscriptionPlanDto>();
                _allModules = (await modulesTask) ?? new List<AppModuleDto>();
                _detail = await subTask;

                // Populate Plans dropdown
                cmbPlans.Items.Clear();
                int selectIdx = -1;
                for (int i = 0; i < _allPlans.Count; i++)
                {
                    var p = _allPlans[i];
                    cmbPlans.Items.Add($"{p.PlanName} (₱{p.Price:N0}/mo)");
                    if (_detail?.PlanId == p.PlanId ||
                        (!string.IsNullOrEmpty(_detail?.PlanCode) && _detail.PlanCode.Equals(p.PlanCode, StringComparison.OrdinalIgnoreCase)))
                    {
                        selectIdx = i;
                    }
                }

                if (selectIdx >= 0)
                {
                    cmbPlans.SelectedIndex = selectIdx;
                }
                else if (cmbPlans.Items.Count > 0)
                {
                    cmbPlans.SelectedIndex = 0;
                }

                if (_detail != null)
                {
                    int sIdx = cmbStatus.FindStringExact(_detail.Status);
                    if (sIdx >= 0) cmbStatus.SelectedIndex = sIdx;
                    if (_detail.EndDate != default) dtpEndDate.Value = _detail.EndDate;
                }

                RenderAddonsAndIncluded();
            }
            catch (Exception ex)
            {
                lblError.Text = $"Failed to load subscription: {ex.Message}";
                lblError.Visible = true;
            }
            finally
            {
                btnSave.Enabled = true;
            }
        }

        private void OnPlanChanged()
        {
            RenderAddonsAndIncluded();
        }

        private void RenderAddonsAndIncluded()
        {
            pnlIncludedModules.Controls.Clear();
            pnlAddonsList.Controls.Clear();
            _addonCheckboxes.Clear();

            int planIdx = cmbPlans.SelectedIndex;
            SubscriptionPlanDto? selectedPlan = (planIdx >= 0 && planIdx < _allPlans.Count)
                ? _allPlans[planIdx]
                : null;

            var includedCodes = new HashSet<string>(
                selectedPlan?.IncludedModules.Select(m => m.ModuleCode) ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            // Render Included Modules as chips/badges
            int incX = 8;
            int incY = 12;
            if (includedCodes.Count == 0)
            {
                var lblNone = new Label
                {
                    Text = "No modules bundled with this plan.",
                    Font = UiKit.Small,
                    ForeColor = UiKit.InkMuted,
                    Location = new Point(10, 18),
                    Size = new Size(300, 20)
                };
                pnlIncludedModules.Controls.Add(lblNone);
            }
            else
            {
                foreach (var code in includedCodes)
                {
                    var mod = _allModules.FirstOrDefault(m => m.ModuleCode.Equals(code, StringComparison.OrdinalIgnoreCase));
                    string label = mod?.ModuleName ?? code;

                    var chip = new Label
                    {
                        Text = $"✓ {label}",
                        Font = UiKit.SmallStrong,
                        ForeColor = Color.FromArgb(4, 120, 87), // Dark emerald
                        BackColor = Color.FromArgb(209, 250, 229), // Soft green
                        TextAlign = ContentAlignment.MiddleCenter,
                        Location = new Point(incX, incY),
                        Size = new Size(label.Length * 8 + 36, 28),
                        BorderStyle = BorderStyle.FixedSingle
                    };
                    pnlIncludedModules.Controls.Add(chip);
                    incX += chip.Width + 8;
                }
            }

            // Existing active add-on codes for this company
            var existingAddonCodes = new HashSet<string>(
                _detail?.ActiveAddons.Where(a => a.IsActive).Select(a => a.ModuleCode) ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            // Render Add-on checkboxes for modules NOT in plan
            int addonY = 6;
            int addonW = pnlAddonsList.ClientSize.Width - 20;

            foreach (var mod in _allModules)
            {
                if (includedCodes.Contains(mod.ModuleCode))
                    continue; // Already bundled in plan!

                var chk = new CheckBox
                {
                    Text = $"{mod.ModuleName}  —  ₱{mod.PricePerMonth:N2}/mo add-on",
                    Font = UiKit.Body,
                    ForeColor = UiKit.Ink,
                    Location = new Point(10, addonY),
                    Size = new Size(addonW, 26),
                    Checked = existingAddonCodes.Contains(mod.ModuleCode),
                    Tag = mod
                };
                chk.CheckedChanged += (s, e) => RecalculateTotal();
                pnlAddonsList.Controls.Add(chk);
                _addonCheckboxes[mod.ModuleCode] = chk;
                addonY += 32;
            }

            if (_addonCheckboxes.Count == 0)
            {
                var lblAllBundled = new Label
                {
                    Text = "All system modules are already bundled in this plan (Full Enterprise Access).",
                    Font = UiKit.Small,
                    ForeColor = UiKit.InkMuted,
                    Location = new Point(10, 18),
                    Size = new Size(addonW, 24)
                };
                pnlAddonsList.Controls.Add(lblAllBundled);
            }

            RecalculateTotal();
        }

        private void RecalculateTotal()
        {
            int planIdx = cmbPlans.SelectedIndex;
            SubscriptionPlanDto? selectedPlan = (planIdx >= 0 && planIdx < _allPlans.Count)
                ? _allPlans[planIdx]
                : null;

            decimal planPrice = selectedPlan?.Price ?? 0m;
            decimal addonTotal = 0m;
            var addonNames = new List<string>();

            foreach (var kv in _addonCheckboxes)
            {
                if (kv.Value.Checked && kv.Value.Tag is AppModuleDto mod)
                {
                    addonTotal += mod.PricePerMonth;
                    addonNames.Add($"{mod.ModuleName} (₱{mod.PricePerMonth:N2})");
                }
            }

            decimal monthlyTotal = planPrice + addonTotal;

            lblPlanPrice.Text = $"Plan Base: ₱{planPrice:N2}/mo";
            lblAddonTotal.Text = $"Add-ons Total: ₱{addonTotal:N2}/mo";
            lblMonthlyTotal.Text = $"Company Monthly Total: ₱{monthlyTotal:N2} / month";

            if (addonNames.Count > 0)
            {
                lblCalculationBreakdown.Text = $"Formula: ₱{planPrice:N2} (Plan) + {string.Join(" + ", addonNames)} = ₱{monthlyTotal:N2}/mo";
            }
            else
            {
                lblCalculationBreakdown.Text = $"Formula: ₱{planPrice:N2} (Plan) + ₱0.00 (No Add-ons) = ₱{monthlyTotal:N2}/mo";
            }
        }

        private async Task SaveChangesAsync()
        {
            try
            {
                btnSave.Enabled = false;
                lblError.Visible = false;

                int planIdx = cmbPlans.SelectedIndex;
                if (planIdx < 0 || planIdx >= _allPlans.Count)
                {
                    lblError.Text = "Please select a subscription plan.";
                    lblError.Visible = true;
                    return;
                }

                var selectedPlan = _allPlans[planIdx];
                var selectedAddonCodes = _addonCheckboxes
                    .Where(kv => kv.Value.Checked)
                    .Select(kv => kv.Key)
                    .ToList();

                var updateReq = new UpdateCompanySubscriptionRequest
                {
                    PlanId = selectedPlan.PlanId,
                    PlanCode = selectedPlan.PlanCode,
                    AddonCodes = selectedAddonCodes,
                    Status = cmbStatus.SelectedItem?.ToString() ?? "Active",
                    EndDate = dtpEndDate.Value
                };

                var res = await _api.ChangeCompanyPlanAsync(_companyId, new ChangeCompanyPlanRequest
                {
                    NewPlanId = selectedPlan.PlanId,
                    NewPlanCode = selectedPlan.PlanCode,
                    Reason = $"Super Admin updated subscription to {selectedPlan.PlanName} with {selectedAddonCodes.Count} add-on(s)."
                });

                // Sync add-ons
                if (selectedAddonCodes.Count > 0 || (_detail?.ActiveAddons.Count ?? 0) > 0)
                {
                    // Add requested
                    foreach (var code in selectedAddonCodes)
                    {
                        await _api.AddModuleAddonAsync(_companyId, code);
                    }
                    // Remove unrequested
                    if (_detail?.ActiveAddons != null)
                    {
                        foreach (var oldAddon in _detail.ActiveAddons)
                        {
                            if (!selectedAddonCodes.Contains(oldAddon.ModuleCode, StringComparer.OrdinalIgnoreCase))
                            {
                                await _api.RemoveModuleAddonAsync(_companyId, oldAddon.ModuleCode);
                            }
                        }
                    }
                }

                Toast.Notify(this.Owner ?? this, "Subscription Updated",
                    $"Successfully updated {_companyName} to {selectedPlan.PlanName}. Monthly Total: ₱{res?.MonthlyTotal ?? selectedPlan.Price:N2}.",
                    ToastKind.Success);

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
