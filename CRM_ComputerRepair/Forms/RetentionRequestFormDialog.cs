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

        private SaasButton btnSubmit = null!;
        private SaasButton btnCancel = null!;

        public RetentionRequestFormDialog(
            List<CustomerDto> customers,
            CustomerDto? preselected = null,
            int? preselectedSegment = null)
        {
            _customers = customers ?? new List<CustomerDto>();
            _preselectedCustomer = preselected;
            _preselectedSegment = preselectedSegment;

            BuildCard(
                "New Retention Request",
                "Submit a formal customer retention offer for administrative review.",
                width: 640,
                height: 660);

            BuildContent();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // ── Customer selection ──
            var lblCust = ModalKit.MakeLabel(pnlBody, "Customer *", x, y);
            y += 20;

            cmbCustomer = new ComboBox
            {
                Location = new Point(x, y),
                Size = new Size(w, 32),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DropDownWidth = Math.Max(w, 460),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = AppTheme.FontInput
            };
            cmbCustomer.Leave += (s, e) =>
            {
                if (cmbCustomer.SelectedIndex < 0 && !string.IsNullOrWhiteSpace(cmbCustomer.Text))
                {
                    int idx = cmbCustomer.FindStringExact(cmbCustomer.Text.Trim());
                    if (idx < 0) idx = cmbCustomer.FindString(cmbCustomer.Text.Trim());
                    if (idx >= 0) cmbCustomer.SelectedIndex = idx;
                }
            };
            cmbCustomer.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (cmbCustomer.SelectedIndex < 0 && !string.IsNullOrWhiteSpace(cmbCustomer.Text))
                    {
                        int idx = cmbCustomer.FindStringExact(cmbCustomer.Text.Trim());
                        if (idx < 0) idx = cmbCustomer.FindString(cmbCustomer.Text.Trim());
                        if (idx >= 0) cmbCustomer.SelectedIndex = idx;
                    }
                }
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
            ModalKit.AdjustDropDownWidth(cmbCustomer);
            pnlBody.Controls.Add(cmbCustomer);
            y += 34;

            lblErrorCustomer = ModalKit.MakeErrorLabel(pnlBody, x, y);
            lblErrorCustomer.MaximumSize = new Size(w, 0);
            y += 18;

            // ── Segment + Action Type (Half Widths) ──
            int halfW = (w - 14) / 2;

            var lblSeg = ModalKit.MakeLabel(pnlBody, "Target Segment *", x, y);
            var lblAct = ModalKit.MakeLabel(pnlBody, "Action Type *", x + halfW + 14, y);
            y += 20;

            cmbSegment = new ComboBox
            {
                Location = new Point(x, y),
                Size = new Size(halfW, 32),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DropDownWidth = Math.Max(halfW, 200),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = AppTheme.FontInput
            };
            cmbSegment.Items.AddRange(new object[] { "New", "Returning", "Loyal", "At Risk", "Inactive" });
            cmbSegment.SelectedIndex = (_preselectedSegment.HasValue && _preselectedSegment.Value >= 0 && _preselectedSegment.Value <= 4)
                ? _preselectedSegment.Value
                : 2; // Default Loyal
            ModalKit.AdjustDropDownWidth(cmbSegment);
            pnlBody.Controls.Add(cmbSegment);

            cmbAction = new ComboBox
            {
                Location = new Point(x + halfW + 14, y),
                Size = new Size(halfW, 32),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DropDownWidth = Math.Max(halfW, 200),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = AppTheme.FontInput
            };
            cmbAction.Items.AddRange(new object[] { "Discount", "Free Diagnostic", "Upgrade Incentive", "Win-Back Checkup" });
            cmbAction.SelectedIndex = 0;
            ModalKit.AdjustDropDownWidth(cmbAction);
            pnlBody.Controls.Add(cmbAction);
            y += 40;

            // ── Proposed Discount % + Reason Category ──
            var lblDisc = ModalKit.MakeLabel(pnlBody, "Proposed Discount % *", x, y);
            var lblCat = ModalKit.MakeLabel(pnlBody, "Reason for Retention Category *", x + 180, y);
            y += 20;

            numDiscount = new NumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(160, 32),
                Font = AppTheme.FontInput,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Minimum = 0,
                Maximum = 100,
                DecimalPlaces = 0,
                Value = 10
            };
            pnlBody.Controls.Add(numDiscount);

            cmbReasonCategory = new ComboBox
            {
                Location = new Point(x + 180, y),
                Size = new Size(w - 180, 32),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DropDownWidth = Math.Max(w - 180, 360),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
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
            ModalKit.AdjustDropDownWidth(cmbReasonCategory);
            cmbReasonCategory.SelectedIndexChanged += (s, e) =>
            {
                inpReasonNote.Enabled = cmbReasonCategory.Text.StartsWith("Other");
                if (inpReasonNote.Enabled) inpReasonNote.Focus();
            };
            pnlBody.Controls.Add(cmbReasonCategory);
            y += 40;

            // ── Reason Note (Optional or required if Other) ──
            var lblNote = ModalKit.MakeLabel(pnlBody, "Reason Note (mandatory if Other)", x, y);
            y += 20;
            inpReasonNote = ModalKit.MakeField(pnlBody, x, y, w, "Additional rationale or specific strategic justification...", false);
            inpReasonNote.Height = 36;
            inpReasonNote.Enabled = false;
            y += 38;
            lblErrorReasonNote = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 18;

            // ── Retention Details ──
            var lblDet = ModalKit.MakeLabel(pnlBody, "Retention Details / Context *", x, y);
            y += 20;

            txtDetails = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(w, 80),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = AppTheme.FontInput,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "Customer previous repair was completed satisfactorily. Recommending preventative maintenance discount to maintain relationship."
            };
            pnlBody.Controls.Add(txtDetails);
            y += 86;

            lblErrorDetails = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 24;

            // ── Bottom Buttons ──
            btnCancel = ModalKit.AddSecondary(pnlCard, "Cancel");
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            btnSubmit = ModalKit.AddPrimary(pnlCard, "Submit Request");
            btnSubmit.Click += async (s, e) => await SubmitAsync();
            LayoutFooter(btnSubmit, btnCancel, saveW: 140);

            AcceptButton = btnSubmit;
            CancelButton = btnCancel;

            Shown += (s, e) => cmbCustomer.Focus();
        }

        private async Task SubmitAsync()
        {
            lblErrorCustomer.Text = "";
            lblErrorCustomer.Visible = false;
            lblErrorDetails.Text = "";
            lblErrorDetails.Visible = false;
            lblErrorReasonNote.Text = "";
            lblErrorReasonNote.Visible = false;

            if (cmbCustomer.SelectedIndex < 0 || cmbCustomer.SelectedIndex >= _customers.Count)
            {
                lblErrorCustomer.Text = "Please select a valid customer.";
                lblErrorCustomer.Visible = true;
                cmbCustomer.Focus();
                return;
            }

            var customer = _customers[cmbCustomer.SelectedIndex];
            var category = cmbReasonCategory.Text;
            var note = inpReasonNote.InnerTextBox.Text?.Trim();

            if (category.StartsWith("Other") && string.IsNullOrWhiteSpace(note))
            {
                lblErrorReasonNote.Text = "Specific note is required when category is 'Other'.";
                lblErrorReasonNote.Visible = true;
                inpReasonNote.Focus();
                return;
            }

            var details = txtDetails.Text?.Trim();
            if (string.IsNullOrWhiteSpace(details))
            {
                lblErrorDetails.Text = "Retention details/context are required.";
                lblErrorDetails.Visible = true;
                txtDetails.Focus();
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







    }
}
