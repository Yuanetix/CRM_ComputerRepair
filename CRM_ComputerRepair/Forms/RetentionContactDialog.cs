using CRM.winforms.Forms;
using CRM.winforms;
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

        private Label lblSubtitle = null!;
        private Label lblBasis = null!;
        private Label lblSubject = null!;
        private Label lblNotes = null!;
        private Label lblFollowUp = null!;

        private TextField inpSubject = null!;
        private TextField inpNotes = null!;
        private NumericUpDown numFollowUpDays = null!;

        private Label lblErrorSubject = null!;
        private Label lblErrorNotes = null!;

        private Button btnSave = null!;
        private Button btnCancel = null!;

        public RetentionContactDialog(int customerId, string customerName,
            string? category = null, string? basis = null)
        {
            _customerId = customerId;
            _customerName = customerName;
            _category = category;
            _basis = basis;

            BuildCard("Log Retention Outreach", width: 560, height: 560);
            BuildContent();

            Shown += (s, e) => inpSubject.Focus();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            lblSubtitle = new Label
            {
                Text = $"Log a retention outreach for {_customerName}.",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 20)
            };
            pnlCard.Controls.Add(lblSubtitle);

            y += 26;

            // ── Basis banner (why this customer was identified) ──
            if (!string.IsNullOrWhiteSpace(_basis))
            {
                lblBasis = new Label
                {
                    Text = $"Basis: {_basis}",
                    Font = new Font("Segoe UI", 8.5F),
                    ForeColor = AppTheme.TextSecondary,
                    BackColor = AppTheme.Neutral,
                    AutoSize = false,
                    Location = new Point(x, y),
                    Size = new Size(w, 44),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(8, 0, 8, 0)
                };
                pnlCard.Controls.Add(lblBasis);
                y += 52;
            }

            // ── Subject * ──
            lblSubject = MakeLabel("Subject *", x, y);
            y += 20;
            inpSubject = MakeField(x, y, w,
                string.IsNullOrWhiteSpace(_category) ? "e.g. Win-back call" : $"{_category}: ",
                prefill: _category is null ? null : $"{_category} outreach");
            y += 38 + 4;
            lblErrorSubject = MakeErrorLabel(x, y);
            y += 20;

            // ── Notes * ──
            lblNotes = MakeLabel("Notes *", x, y);
            y += 20;
            inpNotes = MakeField(x, y, w, "What did you discuss?", multiline: true);
            y += 74 + 4;
            lblErrorNotes = MakeErrorLabel(x, y);
            y += 20;

            // ── Follow-up days ──
            lblFollowUp = MakeLabel("Schedule follow-up in (days) — 0 to skip", x, y);
            y += 20;

            numFollowUpDays = new NumericUpDown
            {
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(x, y),
                Size = new Size(w, 30),
                Minimum = 0,
                Maximum = 90,
                Value = 7,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlCard.Controls.Add(numFollowUpDays);

            y += 38 + 20;

            // ── Buttons ──
            int btnY = pnlCard.Height - ShadowPad - 60;
            int rightEdge = ContentRightX;
            int saveW = 140;
            int cancelW = 100;
            int saveX = rightEdge - saveW;
            int cancelX = saveX - cancelW - 10;

            btnCancel = MakeSecondaryButton("Cancel");
            btnCancel.Size = new Size(cancelW, 40);
            btnCancel.Location = new Point(cancelX, btnY);
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnSave = MakePrimaryButton("Log outreach");
            btnSave.Size = new Size(saveW, 40);
            btnSave.Location = new Point(saveX, btnY);
            btnSave.Click += async (s, e) => await SaveAsync();
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
            err.Text = "⚠  " + msg;
            err.Visible = true;
        }

        private static void ClearError(TextField input, Label err)
        {
            input.HasError = false;
            err.Text = "";
            err.Visible = false;
        }

        private Label MakeLabel(string text, int x, int y)
        {
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 8.5F),
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
            pnlCard.Controls.Add(lbl);
            return lbl;
        }

        private TextField MakeField(int x, int y, int width, string placeholder,
                                    bool multiline = false, string? prefill = null)
        {
            var tf = new TextField
            {
                PlaceholderText = placeholder,
                Text = prefill ?? "",
                Location = new Point(x, y),
                Size = new Size(width, multiline ? 68 : 38)
            };
            if (multiline) tf.Multiline = true;
            pnlCard.Controls.Add(tf);
            return tf;
        }

        private Label MakeErrorLabel(int x, int y)
        {
            var lbl = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8F),
                ForeColor = AppTheme.Danger,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Visible = false
            };
            pnlCard.Controls.Add(lbl);
            return lbl;
        }

        private Button MakePrimaryButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9.5F),
                BackColor = AppTheme.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = AppTheme.PrimaryHover;
            b.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(b, 8);
            pnlCard.Controls.Add(b);
            return b;
        }

        private Button MakeSecondaryButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9.5F),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = AppTheme.BorderStrong;
            b.FlatAppearance.MouseOverBackColor = AppTheme.Neutral;
            b.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(b, 8);
            pnlCard.Controls.Add(b);
            return b;
        }
    }
}