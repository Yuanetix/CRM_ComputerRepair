using CRM.winforms.Forms;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Fast modal dialog for Staff to mark a follow-up as completed,
    /// log the outcome response, and optionally record it into Customer Interactions.
    /// </summary>
    [DesignerCategory("Code")]
    public class QuickCompleteFollowUpDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly FollowUpDto _item;

        private Label lblSummary = null!;
        private Label lblOutcome = null!;
        private TextField inpOutcome = null!;
        private CheckBox chkLogInteraction = null!;
        private SaasButton btnComplete = null!;
        private SaasButton btnCancel = null!;

        public QuickCompleteFollowUpDialog(FollowUpDto item)
        {
            _item = item;

            BuildCard(
                "Complete Follow-Up",
                "Record customer response and close out this scheduled outreach task.",
                width: 560,
                height: 470);

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
                       $"Subject:   {_item.Subject}\n" +
                       $"Linked:    {_item.RepairDisplay}",
                Font = UiKit.T.SmallStrong,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                MaximumSize = new Size(w - 28, 0),
                BackColor = Color.Transparent,
                Padding = new Padding(14, 12, 14, 12)
            };

            int bannerH = Math.Max(80, lblSummary.PreferredSize.Height + 24);
            var pnlSummary = ModalKit.MakeBanner(pnlBody, x, y, w, bannerH, UiKit.Wash(AppTheme.Primary));
            lblSummary.Dock = DockStyle.Fill;
            pnlSummary.Controls.Add(lblSummary);
            y += bannerH + 12;

            // ── Outcome Notes ──
            lblOutcome = ModalKit.MakeLabel(pnlBody, "Outcome & Discussion Notes *", x, y);
            y += 20;

            inpOutcome = ModalKit.MakeField(pnlBody, x, y, w, "e.g. Customer answered call, confirmed PC boots in 15 seconds, satisfied with repair.", multiline: true);
            inpOutcome.Height = 75;
            y += 85;

            // ── Checkbox: Log Interaction ──
            chkLogInteraction = new CheckBox
            {
                Text = "Also log as Customer Interaction record (Inquiry / Feedback)",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                Checked = _item.CustomerId.HasValue,
                Enabled = _item.CustomerId.HasValue,
                Location = new Point(x + 2, y)
            };
            pnlBody.Controls.Add(chkLogInteraction);
            y += 30;

            // ── Buttons ──
            btnCancel = ModalKit.AddSecondary(pnlCard, "Cancel");
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnComplete = ModalKit.AddPrimary(pnlCard, "Mark Completed");
            btnComplete.Click += async (s, e) => await SubmitAsync();
            LayoutFooter(btnComplete, btnCancel, saveW: 150);

            AcceptButton = btnComplete;
            CancelButton = btnCancel;

            Shown += (s, e) => inpOutcome.Focus();
        }

        private async Task SubmitAsync()
        {
            string outcome = inpOutcome.Text.Trim();
            if (string.IsNullOrWhiteSpace(outcome))
            {
                outcome = "Follow-up completed successfully.";
            }

            btnComplete.Enabled = false;
            btnComplete.Text = "Completing...";

            try
            {
                await _api.CompleteFollowUpAsync(_item.FollowUpId, outcome, chkLogInteraction.Checked);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to complete follow-up:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnComplete.Enabled = true;
                btnComplete.Text = "Mark Completed";
            }
        }

        // ═══════════ CONTROL FACTORIES ═══════════




    }
}
