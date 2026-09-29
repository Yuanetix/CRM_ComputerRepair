using CRM.winforms.Forms;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Fast modal dialog for Technicians to mark a repair job as completed,
    /// record final parts & labor costs, document work done, and optionally
    /// prompt for a customer pickup notification callback.
    /// </summary>
    [DesignerCategory("Code")]
    public class QuickCompleteRepairDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly RepairRequestDto _item;

        private Label lblSummary = null!;
        private Label lblPartsCost = null!;
        private Label lblLaborCost = null!;
        private Label lblActualCost = null!;
        private TextField inpPartsCost = null!;
        private TextField inpLaborCost = null!;
        private TextField inpActualCost = null!;

        private Label lblNotes = null!;
        private TextField inpNotes = null!;

        private CheckBox chkSchedulePickup = null!;
        private SaasButton btnComplete = null!;
        private SaasButton btnCancel = null!;

        public bool ShouldSchedulePickupFollowUp => chkSchedulePickup.Checked;

        public QuickCompleteRepairDialog(RepairRequestDto item)
        {
            _item = item;

            BuildCard(
                "Complete Repair Job",
                "Close out technician workbench, calculate final cost, and notify customer.",
                width: 580,
                height: 550);

            BuildContent();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // ── Info Summary Box ──
            lblSummary = new Label
            {
                Text = $"Customer: {_item.CustomerDisplay}  ({_item.ContactDisplay})\n" +
                       $"Ticket:   {_item.RequestNumber} · {_item.DeviceDisplay} (SN: {_item.SerialDisplay})\n" +
                       $"Issue:    {_item.IssueDescription}",
                Font = UiKit.T.SmallStrong,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                MaximumSize = new Size(w - 28, 0),
                BackColor = Color.Transparent,
                Padding = new Padding(14, 12, 14, 12)
            };

            int bannerH = Math.Max(90, lblSummary.PreferredSize.Height + 24);
            var pnlSummary = ModalKit.MakeBanner(pnlBody, x, y, w, bannerH, UiKit.Wash(AppTheme.Success));
            lblSummary.Dock = DockStyle.Fill;
            pnlSummary.Controls.Add(lblSummary);
            y += bannerH + 12;

            // ── Cost Breakdown (3 Columns) ──
            int colW = (w - ModalKit.HalfGap * 2) / 3;

            lblPartsCost = ModalKit.MakeLabel(pnlBody, "Parts Cost (₱)", x, y);
            lblLaborCost = ModalKit.MakeLabel(pnlBody, "Labor Cost (₱)", x + colW + ModalKit.HalfGap, y);
            lblActualCost = ModalKit.MakeLabel(pnlBody, "Total Cost (₱)", x + (colW + ModalKit.HalfGap) * 2, y);
            y += 20;

            inpPartsCost = ModalKit.MakeField(pnlBody, x, y, colW, "0.00");
            inpPartsCost.Height = 36;
            inpLaborCost = ModalKit.MakeField(pnlBody, x + colW + ModalKit.HalfGap, y, colW, "0.00");
            inpLaborCost.Height = 36;
            inpActualCost = ModalKit.MakeField(pnlBody, x + (colW + ModalKit.HalfGap) * 2, y, colW, "0.00");
            inpActualCost.Height = 36;

            if (_item.PartsCost.HasValue && _item.PartsCost > 0)
                inpPartsCost.Text = _item.PartsCost.Value.ToString("F2");
            if (_item.LaborCost.HasValue && _item.LaborCost > 0)
                inpLaborCost.Text = _item.LaborCost.Value.ToString("F2");
            if (_item.ActualCost.HasValue && _item.ActualCost > 0)
                inpActualCost.Text = _item.ActualCost.Value.ToString("F2");
            else if (_item.EstimatedCost.HasValue && _item.EstimatedCost > 0)
                inpActualCost.Text = _item.EstimatedCost.Value.ToString("F2");

            inpPartsCost.InnerTextBox.TextChanged += (s, e) => RecalculateTotal();
            inpLaborCost.InnerTextBox.TextChanged += (s, e) => RecalculateTotal();

            y += 44;

            // ── Work Done & Final Technician Notes ──
            lblNotes = ModalKit.MakeLabel(pnlBody, "Work Done & Diagnostic Outcome Notes *", x, y);
            y += 20;

            inpNotes = ModalKit.MakeField(pnlBody, x, y, w, "Detail the work done, components replaced, and final testing results...", multiline: true);
            inpNotes.Height = 80;
            if (!string.IsNullOrWhiteSpace(_item.TechnicianNotes))
                inpNotes.Text = _item.TechnicianNotes;
            y += 88;

            // ── Checkbox: Schedule Pickup Follow-up ──
            chkSchedulePickup = new CheckBox
            {
                Text = "Immediately schedule Ready-for-Pickup callback with customer",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                Checked = true,
                Location = new Point(x + 2, y)
            };
            pnlBody.Controls.Add(chkSchedulePickup);

            // ── Buttons ──
            btnCancel = ModalKit.AddSecondary(pnlCard, "Cancel");
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnComplete = ModalKit.AddPrimary(pnlCard, "Complete Repair");
            btnComplete.Click += async (s, e) => await SubmitAsync();
            LayoutFooter(btnComplete, btnCancel, saveW: 150);

            AcceptButton = btnComplete;
            CancelButton = btnCancel;

            Shown += (s, e) => inpNotes.Focus();
        }

        private void RecalculateTotal()
        {
            decimal.TryParse(inpPartsCost.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parts);
            decimal.TryParse(inpLaborCost.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal labor);

            if (parts > 0 || labor > 0)
            {
                inpActualCost.Text = (parts + labor).ToString("F2");
            }
        }

        private async Task SubmitAsync()
        {
            decimal.TryParse(inpPartsCost.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parts);
            decimal.TryParse(inpLaborCost.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal labor);
            decimal.TryParse(inpActualCost.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal total);

            string notes = inpNotes.Text.Trim();
            if (string.IsNullOrWhiteSpace(notes))
            {
                notes = "Repair completed and tested functional.";
            }

            btnComplete.Enabled = false;
            btnComplete.Text = "Completing...";

            try
            {
                await _api.QuickCompleteRepairAsync(
                    _item.RepairRequestId,
                    total > 0 ? total : (decimal?)null,
                    parts > 0 ? parts : (decimal?)null,
                    labor > 0 ? labor : (decimal?)null,
                    notes);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to complete repair:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnComplete.Enabled = true;
                btnComplete.Text = "Complete Repair";
            }
        }

        // ═══════════ CONTROL FACTORIES ═══════════




    }
}
