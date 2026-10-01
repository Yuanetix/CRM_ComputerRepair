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
    public class ModulePricingDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private List<AppModuleDto> _modules = new();
        private Panel pnlModuleRows = null!;
        private Label lblError = null!;
        private SaasButton btnDone = null!;
        private readonly Dictionary<int, NumericUpDown> _priceInputs = new();

        public ModulePricingDialog()
        {
            BuildCard(
                "App Modules & Base Pricing",
                "View and configure the monthly subscription price for each platform module",
                width: 680,
                height: 580);

            BuildContent();
            this.Load += async (s, e) => await LoadModulesAsync();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            var lblHeader = new Label
            {
                Text = "Available Subscription Modules (master_db)",
                Font = UiKit.Section,
                ForeColor = UiKit.Ink,
                Location = new Point(x, y),
                Size = new Size(w, 24)
            };
            pnlBody.Controls.Add(lblHeader);
            y += 26;

            var lblDesc = new Label
            {
                Text = "Updating the base price updates the catalog. Existing companies can retain or adopt the updated price when their subscription is modified.",
                Font = UiKit.Small,
                ForeColor = UiKit.InkMuted,
                Location = new Point(x, y),
                Size = new Size(w, 28)
            };
            pnlBody.Controls.Add(lblDesc);
            y += 32;

            pnlModuleRows = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, 320),
                AutoScroll = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlBody.Controls.Add(pnlModuleRows);
            y += 328;

            lblError = new Label
            {
                Font = UiKit.Small,
                ForeColor = Color.FromArgb(239, 68, 68),
                Location = new Point(x, y),
                Size = new Size(w, 20),
                Visible = false
            };
            pnlBody.Controls.Add(lblError);

            btnDone = new SaasButton("Done", SaasButtonVariant.Primary);
            btnDone.Size = new Size(100, 36);
            btnDone.Location = new Point(ContentRightX - 100, 12);
            btnDone.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            pnlFooter.Controls.Add(btnDone);
        }

        private async Task LoadModulesAsync()
        {
            try
            {
                lblError.Visible = false;
                _modules = (await _api.GetAppModulesAsync()) ?? new List<AppModuleDto>();
                RenderModuleRows();
            }
            catch (Exception ex)
            {
                lblError.Text = $"Failed to load modules: {ex.Message}";
                lblError.Visible = true;
            }
        }

        private void RenderModuleRows()
        {
            pnlModuleRows.Controls.Clear();
            _priceInputs.Clear();

            int itemY = 10;
            int itemW = pnlModuleRows.ClientSize.Width - 24;

            foreach (var mod in _modules)
            {
                var rowCard = new Panel
                {
                    Location = new Point(10, itemY),
                    Size = new Size(itemW, 56),
                    BackColor = Color.FromArgb(249, 250, 251),
                    BorderStyle = BorderStyle.FixedSingle
                };

                var lblName = new Label
                {
                    Text = mod.ModuleName,
                    Font = UiKit.Section,
                    ForeColor = UiKit.Ink,
                    Location = new Point(12, 8),
                    Size = new Size(240, 22)
                };
                rowCard.Controls.Add(lblName);

                var lblCode = new Label
                {
                    Text = mod.ModuleCode,
                    Font = UiKit.Small,
                    ForeColor = UiKit.InkMuted,
                    Location = new Point(12, 30),
                    Size = new Size(240, 18)
                };
                rowCard.Controls.Add(lblCode);

                var numPrice = new NumericUpDown
                {
                    DecimalPlaces = 2,
                    Minimum = 0,
                    Maximum = 1000000,
                    Value = mod.PricePerMonth,
                    Font = UiKit.Body,
                    Location = new Point(itemW - 190, 14),
                    Size = new Size(100, 26)
                };
                rowCard.Controls.Add(numPrice);
                _priceInputs[mod.ModuleId] = numPrice;

                var btnSavePrice = new SaasButton("Update", SaasButtonVariant.Secondary);
                btnSavePrice.Size = new Size(70, 28);
                btnSavePrice.Location = new Point(itemW - 80, 13);
                btnSavePrice.Click += async (s, e) =>
                {
                    try
                    {
                        btnSavePrice.Enabled = false;
                        await _api.UpdateModulePriceAsync(mod.ModuleId, numPrice.Value);
                        mod.PricePerMonth = numPrice.Value;

                        Toast.Notify(this, "Price Updated",
                            $"{mod.ModuleName} base price updated to ₱{mod.PricePerMonth:N2}/mo.",
                            ToastKind.Success);
                    }
                    catch (Exception ex)
                    {
                        Toast.Notify(this, "Error", ex.Message, ToastKind.Danger);
                    }
                    finally
                    {
                        btnSavePrice.Enabled = true;
                    }
                };
                rowCard.Controls.Add(btnSavePrice);

                pnlModuleRows.Controls.Add(rowCard);
                itemY += 64;
            }
        }
    }
}
