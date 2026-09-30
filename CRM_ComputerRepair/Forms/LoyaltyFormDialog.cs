using CRM.winforms.Auth;
using CRM.winforms.Controls;
using CRM.winforms.Forms;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Create / edit a loyalty program — including the eligibility criteria the
    /// retention engine evaluates against real customer history (min transactions,
    /// min spending, max inactivity, visit frequency), the reward definition and
    /// the validity period.
    /// </summary>
    [DesignerCategory("Code")]
    public class LoyaltyFormDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly LoyaltyProgramDto? _editing;
        private readonly bool _isEditMode;
        private readonly int _targetCompanyId;

        private Label lblProgramName = null!;
        private Label lblDescription = null!;
        private Label lblPoints = null!;
        private Label lblRewardType = null!;
        private Label lblRewardValue = null!;
        private Label lblStart = null!;
        private Label lblEnd = null!;

        // Eligibility criteria
        private Label lblCriteria = null!;
        private Label lblMinTx = null!;
        private Label lblMinSpent = null!;
        private Label lblMaxIdle = null!;
        private Label lblVisits = null!;
        private Label lblVisitWindow = null!;

        private TextField inpProgramName = null!;
        private TextField inpDescription = null!;
        private NumericUpDown numPoints = null!;
        private ComboBox cboRewardType = null!;
        private NumericUpDown numRewardValue = null!;
        private DateTimePicker dtpStart = null!;
        private DateTimePicker dtpEnd = null!;

        private NumericUpDown numMinTx = null!;
        private NumericUpDown numMinSpent = null!;
        private NumericUpDown numMaxIdle = null!;
        private NumericUpDown numVisits = null!;
        private NumericUpDown numVisitWindow = null!;

        private Label lblErrorName = null!;

        private SaasButton btnSave = null!;
        private SaasButton btnCancel = null!;

        public LoyaltyFormDialog(LoyaltyProgramDto? existing, int? companyId = null)
        {
            _editing = existing;
            _isEditMode = existing != null;
            _targetCompanyId = existing?.CompanyId ?? companyId ?? (UserSession.CompanyId > 0 ? UserSession.CompanyId : 1);

            BuildCard(
                _isEditMode ? "Edit Loyalty Program" : "Add Loyalty Program",
                _isEditMode
                    ? "Update this program's details and eligibility criteria."
                    : "Define the reward, validity, and automatic eligibility criteria.",
                width: 680,
                height: 720);

            BuildContent();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // Program name
            lblProgramName = ModalKit.MakeLabel(pnlBody, "Program name *", x, y);
            y += 20;
            inpProgramName = ModalKit.MakeField(pnlBody, x, y, w, "e.g. Gold Rewards");
            inpProgramName.Height = 36;
            y += 38 + 4;
            lblErrorName = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 20;

            // Description
            lblDescription = ModalKit.MakeLabel(pnlBody, "Description", x, y);
            y += 20;
            inpDescription = ModalKit.MakeField(pnlBody, x, y, w, "Short description", multiline: true);
            inpDescription.Height = 64;
            y += 66 + 10;

            // ── Reward ──
            lblCriteria = ModalKit.MakeSection(pnlBody, "Reward", x, y, w);
            y += 24;

            int halfW = (w - 12) / 2;
            int thirdW = (w - 24) / 3;

            lblRewardType = ModalKit.MakeLabel(pnlBody, "Reward type", x, y);
            lblRewardValue = ModalKit.MakeLabel(pnlBody, "Reward value", x + thirdW + 12, y);
            lblPoints = ModalKit.MakeLabel(pnlBody, "Points per ₱1", x + (thirdW + 12) * 2, y);
            y += 20;

            cboRewardType = new ComboBox
            {
                Font = AppTheme.FontInput,
                Location = new Point(x, y),
                Size = new Size(thirdW, 30),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                DropDownWidth = Math.Max(thirdW, 260),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat
            };
            cboRewardType.Items.AddRange(new object[]
            {
                "Discount % off next service",
                "Free service (value cap)",
                "Points multiplier",
                "Cash voucher"
            });
            ModalKit.AdjustDropDownWidth(cboRewardType);
            cboRewardType.Leave += (s, e) =>
            {
                if (cboRewardType.SelectedIndex < 0 && !string.IsNullOrWhiteSpace(cboRewardType.Text))
                {
                    int idx = cboRewardType.FindStringExact(cboRewardType.Text.Trim());
                    if (idx < 0) idx = cboRewardType.FindString(cboRewardType.Text.Trim());
                    if (idx >= 0) cboRewardType.SelectedIndex = idx;
                }
            };
            cboRewardType.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (cboRewardType.SelectedIndex < 0 && !string.IsNullOrWhiteSpace(cboRewardType.Text))
                    {
                        int idx = cboRewardType.FindStringExact(cboRewardType.Text.Trim());
                        if (idx < 0) idx = cboRewardType.FindString(cboRewardType.Text.Trim());
                        if (idx >= 0) cboRewardType.SelectedIndex = idx;
                    }
                }
            };
            pnlBody.Controls.Add(cboRewardType);

            numRewardValue = MakeNumeric(x + thirdW + 12, y, thirdW, 0, 1000000, 10, decimalPlaces: 1);
            numPoints = MakeNumeric(x + (thirdW + 12) * 2, y, thirdW, 1, 1000, 1);
            y += 38 + 12;

            // ── Eligibility criteria ──
            lblCriteria = ModalKit.MakeSection(pnlBody, "Eligibility criteria (0 = disabled)", x, y, w);
            y += 24;

            lblMinTx = ModalKit.MakeLabel(pnlBody, "Min transactions", x, y);
            lblMinSpent = ModalKit.MakeLabel(pnlBody, "Min spending (₱)", x + thirdW + 12, y);
            lblMaxIdle = ModalKit.MakeLabel(pnlBody, "Max idle days", x + (thirdW + 12) * 2, y);
            y += 20;

            numMinTx = MakeNumeric(x, y, thirdW, 0, 10000, 0);
            numMinSpent = MakeNumeric(x + thirdW + 12, y, thirdW, 0, 1000000, 0, decimalPlaces: 2);
            numMaxIdle = MakeNumeric(x + (thirdW + 12) * 2, y, thirdW, 0, 3650, 0);
            y += 38 + 12;

            lblVisits = ModalKit.MakeLabel(pnlBody, "Min visits in window", x, y);
            lblVisitWindow = ModalKit.MakeLabel(pnlBody, "Visit window (days)", x + thirdW + 12, y);
            y += 20;

            numVisits = MakeNumeric(x, y, thirdW, 0, 1000, 0);
            numVisitWindow = MakeNumeric(x + thirdW + 12, y, thirdW, 0, 3650, 0);
            y += 38 + 12;

            // ── Validity period ──
            lblCriteria = ModalKit.MakeSection(pnlBody, "Validity period", x, y, w);
            y += 24;

            lblStart = ModalKit.MakeLabel(pnlBody, "Start date *", x, y);
            lblEnd = ModalKit.MakeLabel(pnlBody, "End date *", x + halfW + 12, y);
            y += 20;

            dtpStart = MakeDate(x, y, halfW);
            dtpEnd = MakeDate(x + halfW + 12, y, halfW);
            y += 38 + 16;

            // Buttons
            btnCancel = ModalKit.AddSecondary(pnlCard, "Cancel");
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnSave = ModalKit.AddPrimary(pnlCard, _isEditMode ? "Update" : "Save");
            btnSave.Click += async (s, e) => await SaveAsync();
            LayoutFooter(btnSave, btnCancel);

            AcceptButton = btnSave;
            CancelButton = btnCancel;

            // Prefill
            if (_editing != null)
            {
                inpProgramName.Text = _editing.ProgramName ?? "";
                inpDescription.Text = _editing.Description ?? "";
                numPoints.Value = Clamp(_editing.PointsPerPeso, 1, 1000);
                cboRewardType.SelectedIndex = (int)Clamp(_editing.RewardType, 0, 3);
                numRewardValue.Value = Clamp(_editing.RewardValue, 0, 1000000);
                numMinTx.Value = Clamp(_editing.MinTransactions ?? 0, 0, 10000);
                numMinSpent.Value = Clamp(_editing.MinTotalSpent ?? 0, 0, 1000000);
                numMaxIdle.Value = Clamp(_editing.MaxInactiveDays ?? 0, 0, 3650);
                numVisits.Value = Clamp(_editing.MinVisitsPerPeriod ?? 0, 0, 1000);
                numVisitWindow.Value = Clamp(_editing.VisitPeriodDays ?? 0, 0, 3650);
                dtpStart.Value = _editing.StartDate;
                dtpEnd.Value = _editing.EndDate;
            }
            else
            {
                cboRewardType.SelectedIndex = 0;
                dtpStart.Value = DateTime.Today;
                dtpEnd.Value = DateTime.Today.AddYears(1);
            }

            Shown += (s, e) => inpProgramName.Focus();
        }

        private static decimal Clamp(decimal value, decimal min, decimal max) =>
            Math.Max(min, Math.Min(max, value));

        private async Task SaveAsync()
        {
            if (!ValidateInputs()) return;

            try
            {
                var dto = new LoyaltyProgramDto
                {
                    CompanyId = _targetCompanyId,
                    ProgramName = inpProgramName.Text.Trim(),
                    Description = inpDescription.Text.Trim(),
                    PointsPerPeso = (int)numPoints.Value,
                    RewardType = cboRewardType.SelectedIndex,
                    RewardValue = numRewardValue.Value,
                    MinTransactions = numMinTx.Value > 0 ? (int)numMinTx.Value : (int?)null,
                    MinTotalSpent = numMinSpent.Value > 0 ? numMinSpent.Value : (decimal?)null,
                    MaxInactiveDays = numMaxIdle.Value > 0 ? (int)numMaxIdle.Value : (int?)null,
                    MinVisitsPerPeriod = numVisits.Value > 0 ? (int)numVisits.Value : (int?)null,
                    VisitPeriodDays = numVisitWindow.Value > 0 ? (int)numVisitWindow.Value : (int?)null,
                    StartDate = dtpStart.Value,
                    EndDate = dtpEnd.Value,
                    IsActive = _editing?.IsActive ?? true
                };

                if (_isEditMode && _editing != null)
                    await _api.UpdateLoyaltyProgramAsync(_editing.LoyaltyProgramId, dto);
                else
                    await _api.CreateLoyaltyProgramAsync(dto);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool ValidateInputs()
        {
            lblErrorName.Visible = false;
            inpProgramName.HasError = false;

            if (string.IsNullOrWhiteSpace(inpProgramName.Text))
            {
                inpProgramName.HasError = true;
                lblErrorName.Text = "Program name is required.";
                lblErrorName.Visible = true;
                inpProgramName.Focus();
                return false;
            }

            if (dtpEnd.Value <= dtpStart.Value)
            {
                MessageBox.Show("End date must be after start date.",
                    "Invalid dates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (numVisits.Value > 0 && numVisitWindow.Value <= 0)
            {
                MessageBox.Show("Visit window (days) is required when a minimum visits value is set.",
                    "Invalid criteria", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        // ─── Factories ───




        private NumericUpDown MakeNumeric(int x, int y, int width, decimal min, decimal max, decimal initial, int decimalPlaces = 0)
        {
            var n = ModalKit.MakeNumeric(pnlBody, x, y, width);
            n.Minimum = min;
            n.Maximum = max;
            n.Value = initial;
            n.DecimalPlaces = decimalPlaces;
            return n;
        }

        private DateTimePicker MakeDate(int x, int y, int width)
        {
            return ModalKit.MakeDate(pnlBody, x, y, width);
        }



    }
}
