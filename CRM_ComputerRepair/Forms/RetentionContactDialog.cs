using CRM.winforms.Forms;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Modal for logging a retention outreach contact.
    /// Writes a CustomerInteraction and (optionally) a scheduled FollowUp.
    /// </summary>
    [DesignerCategory("Code")]
    public class RetentionContactDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly int _customerId;
        private readonly string _customerName;
        private readonly string? _category;
        private readonly string? _basis;

        private Label lblBasis = null!;
        private Label lblSubject = null!;
        private Label lblNotes = null!;
        private Label lblFollowUp = null!;

        private TextField inpSubject = null!;
        private TextField inpNotes = null!;
        private NumericUpDown numFollowUpDays = null!;

        private Label lblErrorSubject = null!;
        private Label lblErrorNotes = null!;

        private SaasButton btnSave = null!;
        private SaasButton btnCancel = null!;

        public RetentionContactDialog(int customerId, string customerName,
            string? category = null, string? basis = null)
        {
            _customerId = customerId;
            _customerName = customerName;
            _category = category;
            _basis = basis;

            BuildCard(
                "Log Retention Outreach",
                $"Record outreach call or message for {_customerName}.",
                width: 560,
                height: 520);

            BuildContent();

            Shown += (s, e) => inpSubject.Focus();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // ── Basis banner (why this customer was identified) ──
            if (!string.IsNullOrWhiteSpace(_basis))
            {
                lblBasis = new Label
                {
                    Text = $"Basis: {_basis}",
                    Font = AppFonts.Regular(8.5F),
                    ForeColor = AppTheme.TextSecondary,
                    BackColor = AppTheme.Neutral,
                    UseMnemonic = false,
                    AutoSize = true,
                    MaximumSize = new Size(w - 24, 0),
                    Padding = new Padding(12, 6, 12, 6)
                };

                int basisH = Math.Max(50, lblBasis.PreferredSize.Height + 16);
                var pnlBasis = ModalKit.MakeBanner(pnlBody, x, y, w, basisH, AppTheme.Neutral);
                lblBasis.Dock = DockStyle.Fill;
                pnlBasis.Controls.Add(lblBasis);
                y += basisH + 12;
            }

            // ── Subject * ──
            lblSubject = ModalKit.MakeLabel(pnlBody, "Subject *", x, y);
            y += 20;
            inpSubject = ModalKit.MakeField(pnlBody, x, y, w,
                string.IsNullOrWhiteSpace(_category) ? "e.g. Win-back call" : $"{_category}: ",
                prefill: _category is null ? null : $"{_category} outreach");
            inpSubject.Height = 36;
            y += 38 + 4;
            lblErrorSubject = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 20;

            // ── Notes * ──
            lblNotes = ModalKit.MakeLabel(pnlBody, "Notes *", x, y);
            y += 20;
            inpNotes = ModalKit.MakeField(pnlBody, x, y, w, "What did you discuss?", multiline: true);
            inpNotes.Height = 74;
            y += 74 + 4;
            lblErrorNotes = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 20;

            // ── Follow-up days ──
            lblFollowUp = ModalKit.MakeLabel(pnlBody, "Schedule follow-up in (days) — 0 to skip", x, y);
            y += 20;

            numFollowUpDays = new NumericUpDown
            {
                Font = AppTheme.FontInput,
                Location = new Point(x, y),
                Size = new Size(w, 30),
                Minimum = 0,
                Maximum = 90,
                Value = 7,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlBody.Controls.Add(numFollowUpDays);

            y += 38 + 20;

            // ── Buttons ──
            btnCancel = ModalKit.AddSecondary(pnlCard, "Cancel");
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnSave = ModalKit.AddPrimary(pnlCard, "Log outreach");
            btnSave.Click += async (s, e) => await SaveAsync();
            LayoutFooter(btnSave, btnCancel, saveW: 140);

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private async Task SaveAsync()
        {
            if (!ValidateInputs()) return;

            try
            {
                int? followUpDays = numFollowUpDays.Value == 0
                    ? (int?)null
                    : (int)numFollowUpDays.Value;

                await _api.LogRetentionContactAsync(
                    _customerId,
                    inpSubject.Text.Trim(),
                    inpNotes.Text.Trim(),
                    followUpDays,
                    category: _category,
                    basis: _basis);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Save failed:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private bool ValidateInputs()
        {
            ClearErrors();
            bool valid = true;
            Control? firstInvalid = null;

            if (string.IsNullOrWhiteSpace(inpSubject.Text))
            {
                ShowError(inpSubject, lblErrorSubject, "Subject is required.");
                firstInvalid ??= inpSubject;
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(inpNotes.Text))
            {
                ShowError(inpNotes, lblErrorNotes, "Notes are required.");
                firstInvalid ??= inpNotes;
                valid = false;
            }

            firstInvalid?.Focus();
            return valid;
        }

        private void ClearErrors()
        {
            ClearError(inpSubject, lblErrorSubject);
            ClearError(inpNotes, lblErrorNotes);
        }

        private static void ShowError(TextField input, Label err, string msg)
        {
            input.HasError = true;
            err.Text = msg;
            err.Visible = true;
        }

        private static void ClearError(TextField input, Label err)
        {
            input.HasError = false;
            err.Text = "";
            err.Visible = false;
        }





    }
}
