using CRM.winforms.Forms;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Fast modal dialog for Staff to close and resolve an inquiry, complaint, or review ticket
    /// with resolution details and actions taken.
    /// </summary>
    [DesignerCategory("Code")]
    public class QuickResolveInteractionDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly InteractionDto _item;

        private Label lblSummary = null!;
        private Label lblResolution = null!;
        private TextField inpResolution = null!;
        private Label lblErrorResolution = null!;
        private SaasButton btnResolve = null!;
        private SaasButton btnCancel = null!;

        public QuickResolveInteractionDialog(InteractionDto item)
        {
            _item = item;

            BuildCard(
                "Resolve Interaction",
                "Record resolution details and close out this customer inquiry or concern.",
                width: 560,
                height: 480);

            BuildContent();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // ── Info Summary Box ──
            string typeDisplay = _item.TypeText switch
            {
                "Inquiry" => "Question",
                "Complaint" => "Concern",
                "Feedback" => "Review",
                _ => _item.TypeText
            };

            lblSummary = new Label
            {
                Text = $"Customer: {_item.CustomerDisplay}  ({_item.ContactDisplay})\n" +
                       $"Subject:   [{typeDisplay}] {_item.Subject}\n" +
                       $"Linked:    {_item.RepairDisplay}",
                Font = UiKit.T.SmallStrong,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                MaximumSize = new Size(w - 28, 0),
                BackColor = Color.Transparent,
                Padding = new Padding(14, 12, 14, 12)
            };

            int bannerH = Math.Max(80, lblSummary.PreferredSize.Height + 24);
            var pnlSummary = ModalKit.MakeBanner(pnlBody, x, y, w, bannerH, UiKit.Wash(AppTheme.Warning));
            lblSummary.Dock = DockStyle.Fill;
            pnlSummary.Controls.Add(lblSummary);
            y += bannerH + 12;

            // ── Quick Presets ──
            var pnlPresets = new FlowLayoutPanel
            {
                Location = new Point(x, y),
                Width = w,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                WrapContents = true,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            AddPresetChip(pnlPresets, "Issue resolved with customer");
            AddPresetChip(pnlPresets, "Explanation provided");
            AddPresetChip(pnlPresets, "Part replaced under warranty");

            pnlBody.Controls.Add(pnlPresets);
            y += Math.Max(32, pnlPresets.PreferredSize.Height) + 8;

            // ── Resolution Notes ──
            lblResolution = ModalKit.MakeLabel(pnlBody, "Resolution Details & Actions Taken *", x, y);
            y += 20;

            inpResolution = ModalKit.MakeField(pnlBody, x, y, w, "Detail the final resolution provided to the customer...", multiline: true);
            inpResolution.Height = 85;
            if (!string.IsNullOrWhiteSpace(_item.Resolution))
            {
                inpResolution.Text = _item.Resolution;
            }
            y += 92;

            lblErrorResolution = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 24;

            // ── Buttons ──
            btnCancel = ModalKit.AddSecondary(pnlCard, "Cancel");
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnResolve = ModalKit.AddPrimary(pnlCard, "Resolve & Close");
            btnResolve.Click += async (s, e) => await SubmitAsync();
            LayoutFooter(btnResolve, btnCancel, saveW: 150);

            AcceptButton = btnResolve;
            CancelButton = btnCancel;

            Shown += (s, e) => inpResolution.Focus();
        }

        private void AddPresetChip(FlowLayoutPanel panel, string text)
        {
            var btn = ModalKit.MakeFlowChip(panel, text);
            btn.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(inpResolution.Text))
                    inpResolution.Text = text + ". ";
                else
                    inpResolution.Text = inpResolution.Text.TrimEnd() + " " + text + ". ";
                inpResolution.InnerTextBox.SelectionStart = inpResolution.Text.Length;
                inpResolution.Focus();
            };
        }

        private async Task SubmitAsync()
        {
            string resolution = inpResolution.Text.Trim();
            if (string.IsNullOrWhiteSpace(resolution))
            {
                inpResolution.HasError = true;
                lblErrorResolution.Text = "Please provide resolution details before closing.";
                lblErrorResolution.Visible = true;
                inpResolution.Focus();
                return;
            }

            btnResolve.Enabled = false;
            btnResolve.Text = "Resolving...";

            try
            {
                await _api.ResolveInteractionAsync(_item.CustomerInteractionId, resolution);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to resolve interaction:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnResolve.Enabled = true;
                btnResolve.Text = "Resolve & Close";
            }
        }

        // ═══════════ CONTROL FACTORIES ═══════════





    }
}
