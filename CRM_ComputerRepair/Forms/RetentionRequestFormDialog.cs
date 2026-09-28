using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CRM.winforms.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Modal dialog for Manager / Admin to submit a new Customer Retention Request.
    /// </summary>
    [DesignerCategory("Code")]
    public class RetentionRequestFormDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly List<CustomerDto> _customers;
        private readonly CustomerDto? _preselectedCustomer;
        private readonly int? _preselectedSegment;

        private ComboBox cmbCustomer = null!;
        private ComboBox cmbSegment = null!;
        private ComboBox cmbAction = null!;
        private NumericUpDown numDiscount = null!;
        private ComboBox cmbReasonCategory = null!;
        private TextField inpReasonNote = null!;
        private TextBox txtDetails = null!;

        private Label lblErrorCustomer = null!;
        private Label lblErrorDetails = null!;
        private Label lblErrorReasonNote = null!;

        private Button btnSubmit = null!;
        private Button btnCancel = null!;

        public RetentionRequestFormDialog(
            List<CustomerDto> customers,
            CustomerDto? preselected = null,
            int? preselectedSegment = null)
        {
            _customers = customers ?? new List<CustomerDto>();
            _preselectedCustomer = preselected;
            _preselectedSegment = preselectedSegment;

            BuildCard("New Retention Request", width: 620, height: 720);
            BuildContent();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            var lblSub = new Label
            {
                Text = "Submit a formal customer retention offer for administrative review.",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                Location = new Point(x, y),
                Size = new Size(w, 20)
            };
            pnlCard.Controls.Add(lblSub);
            y += 28;

            // ── Customer selection ──
            var lblCust = MakeLabel("Customer *", x, y);
            y += 20;

            cmbCustomer = new ComboBox
            {
                Location = new Point(x, y),
                Size = new Size(w, 32),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = AppTheme.FontInput
            };
            foreach (var c in _customers)
            {
                cmbCustomer.Items.Add($"{c.FullName} ({c.Email ?? c.Phone ?? "No contact"})");
            }

            if (_preselectedCustomer != null)
            {
                int idx = _customers.FindIndex(c => c.CustomerId == _preselectedCustomer.CustomerId);
                if (idx >= 0) cmbCustomer.SelectedIndex = idx;
            }
            else if (cmbCustomer.Items.Count > 0)
            {
                cmbCustomer.SelectedIndex = 0;
            }
            pnlCard.Controls.Add(cmbCustomer);
            y += 34;

            lblErrorCustomer = MakeErrorLabel(x, y);
            y += 18;

            // ── Segment + Action Type (Half Widths) ──
            int halfW = (w - 14) / 2;

            var lblSeg = MakeLabel("Target Segment *", x, y);
            var lblAct = MakeLabel("Action Type *", x + halfW + 14, y);
            y += 20;

            cmbSegment = new ComboBox
            {
                Location = new Point(x, y),
                Size = new Size(halfW, 32),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = AppTheme.FontInput
            };
            cmbSegment.Items.AddRange(new object[] { "New", "Returning", "Loyal", "At Risk", "Inactive" });
            cmbSegment.SelectedIndex = (_preselectedSegment.HasValue && _preselectedSegment.Value >= 0 && _preselectedSegment.Value <= 4)
                ? _preselectedSegment.Value
                : 2; // Default Loyal
            pnlCard.Controls.Add(cmbSegment);

            cmbAction = new ComboBox
            {
                Location = new Point(x + halfW + 14, y),
                Size = new Size(halfW, 32),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = AppTheme.FontInput
            };
            cmbAction.Items.AddRange(new object[] { "Discount", "Free Diagnostic", "Upgrade Incentive", "Win-Back Checkup" });
            cmbAction.SelectedIndex = 0;
            pnlCard.Controls.Add(cmbAction);
            y += 40;

            // ── Proposed Discount % + Reason Category ──
            var lblDisc = MakeLabel("Proposed Discount % *", x, y);
            var lblCat = MakeLabel("Reason for Retention Category *", x + 180, y);
            y += 20;

            numDiscount = new NumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(160, 32),
                Font = AppTheme.FontInput,
                Minimum = 0,
                Maximum = 100,
                DecimalPlaces = 0,
                Value = 10
            };
            pnlCard.Controls.Add(numDiscount);

            cmbReasonCategory = new ComboBox
            {
                Location = new Point(x + 180, y),
                Size = new Size(w - 180, 32),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = AppTheme.FontInput
            };
            cmbReasonCategory.Items.AddRange(new object[]
            {
                "Improve Customer Retention",
                "Prevent Customer Churn",
                "Increase Customer Lifetime Value",
                "Maximize Profit Margins",
                "Lower Marketing Costs",
                "Monthly Repair Target Not Met",
                "Promotional or Strategic Decision",
                "Other (specific note required)"
            });
            cmbReasonCategory.SelectedIndex = 0;
            cmbReasonCategory.SelectedIndexChanged += (s, e) =>
            {
                inpReasonNote.Enabled = cmbReasonCategory.Text.StartsWith("Other");
                if (inpReasonNote.Enabled) inpReasonNote.Focus();
            };
            pnlCard.Controls.Add(cmbReasonCategory);
            y += 40;

            // ── Reason Note (Optional or required if Other) ──
            var lblNote = MakeLabel("Reason Note (mandatory if Other)", x, y);
            y += 20;
            inpReasonNote = MakeField(x, y, w, "Additional rationale or specific strategic justification...", false);
            inpReasonNote.Enabled = false;
            y += 38;
            lblErrorReasonNote = MakeErrorLabel(x, y);
            y += 18;

            // ── Retention Details ──
            var lblDet = MakeLabel("Retention Details / Context *", x, y);
            y += 20;

            txtDetails = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(w, 80),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = AppTheme.FontInput,
                Text = "Customer previous repair was completed satisfactorily. Recommending preventative maintenance discount to maintain relationship."
            };
            pnlCard.Controls.Add(txtDetails);
            y += 86;

            lblErrorDetails = MakeErrorLabel(x, y);
            y += 24;

            // ── Bottom Buttons ──
            btnCancel = MakeSecondaryButton("Cancel", x + w - 210, y, 100);
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            btnSubmit = MakePrimaryButton("Submit Request", x + w - 100, y, 100);
            btnSubmit.Click += async (s, e) => await SubmitAsync();

            pnlCard.Controls.Add(btnCancel);
            pnlCard.Controls.Add(btnSubmit);
        }

        private async Task SubmitAsync()
        {
            lblErrorCustomer.Text = "";
            lblErrorDetails.Text = "";
            lblErrorReasonNote.Text = "";

            if (cmbCustomer.SelectedIndex < 0 || cmbCustomer.SelectedIndex >= _customers.Count)
            {
                lblErrorCustomer.Text = "Please select a valid customer.";
                return;
            }

            var customer = _customers[cmbCustomer.SelectedIndex];
            var category = cmbReasonCategory.Text;
            var note = inpReasonNote.InnerTextBox.Text?.Trim();

            if (category.StartsWith("Other") && string.IsNullOrWhiteSpace(note))
            {
                lblErrorReasonNote.Text = "Specific note is required when category is 'Other'.";
                return;
            }

            var details = txtDetails.Text?.Trim();
            if (string.IsNullOrWhiteSpace(details))
            {
                lblErrorDetails.Text = "Retention details/context are required.";
                return;
            }

            var req = new CreateRetentionRequestDto
            {
                CustomerId = customer.CustomerId,
                TargetSegment = cmbSegment.SelectedIndex,
                ActionType = cmbAction.Text,
                ProposedDiscountPercent = numDiscount.Value,
                RetentionDetails = details,
                ReasonCategory = category,
                ReasonNote = note
            };

            btnSubmit.Enabled = false;
            btnSubmit.Text = "Submitting...";

            try
            {
                await _api.CreateRetentionRequestAsync(req);
                MessageBox.Show(
                    $"Retention request for {customer.FullName} has been submitted for Admin approval.",
                    "Request Submitted",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                btnSubmit.Enabled = true;
                btnSubmit.Text = "Submit Request";
                MessageBox.Show($"Failed to submit retention request:\n\n{ex.Message}", "Submission Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

        private TextField MakeField(int x, int y, int width, string placeholder, bool multiline = false)
        {
            var tf = new TextField
            {
                PlaceholderText = placeholder,
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

        private Button MakePrimaryButton(string text, int x, int y, int width)
        {
            var b = MakePrimaryButton(text);
            b.Location = new Point(x, y);
            b.Size = new Size(width, 36);
            return b;
        }

        private Button MakeSecondaryButton(string text, int x, int y, int width)
        {
            var b = MakeSecondaryButton(text);
            b.Location = new Point(x, y);
            b.Size = new Size(width, 36);
            return b;
        }
    }
}
