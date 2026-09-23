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

        private Label lblSubtitle = null!;

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

        private Button btnSave = null!;
        private Button btnCancel = null!;

        public LoyaltyFormDialog(LoyaltyProgramDto? existing)
        {
            _editing = existing;
            _isEditMode = existing != null;

            BuildCard(
                _isEditMode ? "Edit Loyalty Program" : "Add Loyalty Program",
                width: 620,
                height: 800);

            BuildContent();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            lblSubtitle = new Label
            {
                Text = _isEditMode
                    ? "Update this program's details and eligibility criteria."
                    : "Define the reward, validity and eligibility criteria. The system automatically determines which customers qualify based on their history.",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 34)
            };
            pnlCard.Controls.Add(lblSubtitle);
            y += 44;

            // Program name
            lblProgramName = MakeLabel("Program name *", x, y);
            y += 20;
            inpProgramName = MakeField(x, y, w, "e.g. Gold Rewards");
            y += 38 + 4;
            lblErrorName = MakeErrorLabel(x, y);
            y += 20;

            // Description
            lblDescription = MakeLabel("Description", x, y);
            y += 20;
            inpDescription = MakeField(x, y, w, "Short description", multiline: true);
            y += 74 + 10;

            // ── Reward ──
            lblCriteria = MakeSectionLabel("Reward", x, y);
            y += 24;

            int halfW = (w - 12) / 2;
            int thirdW = (w - 24) / 3;

            lblRewardType = MakeLabel("Reward type", x, y);
            lblRewardValue = MakeLabel("Reward value", x + thirdW + 12, y);
            lblPoints = MakeLabel("Points per ₱1", x + (thirdW + 12) * 2, y);
            y += 20;

            cboRewardType = new ComboBox
            {
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(x, y),
                Size = new Size(thirdW, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
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
            pnlCard.Controls.Add(cboRewardType);

            numRewardValue = MakeNumeric(x + thirdW + 12, y, thirdW, 0, 1000000, 10, decimalPlaces: 1);
            numPoints = MakeNumeric(x + (thirdW + 12) * 2, y, thirdW, 1, 1000, 1);
            y += 38 + 12;

            // ── Eligibility criteria ──
            lblCriteria = MakeSectionLabel("Eligibility criteria — evaluated automatically against each customer's history (0 = disabled)",
                x, y);
            y += 24;

            lblMinTx = MakeLabel("Min completed transactions", x, y);
            lblMinSpent = MakeLabel("Min total spending (₱)", x + thirdW + 12, y);
            lblMaxIdle = MakeLabel("Max days since last visit", x + (thirdW + 12) * 2, y);
            y += 20;

            numMinTx = MakeNumeric(x, y, thirdW, 0, 10000, 0);
            numMinSpent = MakeNumeric(x + thirdW + 12, y, thirdW, 0, 1000000, 0, decimalPlaces: 2);
            numMaxIdle = MakeNumeric(x + (thirdW + 12) * 2, y, thirdW, 0, 3650, 0);
            y += 38 + 12;

            lblVisits = MakeLabel("Min visits in window", x, y);
            lblVisitWindow = MakeLabel("Visit window (days)", x + thirdW + 12, y);
            y += 20;

            numVisits = MakeNumeric(x, y, thirdW, 0, 1000, 0);
            numVisitWindow = MakeNumeric(x + thirdW + 12, y, thirdW, 0, 3650, 0);
            y += 38 + 12;

            // ── Validity period ──
            lblCriteria = MakeSectionLabel("Validity period", x, y);
            y += 24;

            lblStart = MakeLabel("Start date *", x, y);
            lblEnd = MakeLabel("End date *", x + halfW + 12, y);
            y += 20;

            dtpStart = MakeDate(x, y, halfW);
            dtpEnd = MakeDate(x + halfW + 12, y, halfW);
            y += 38 + 16;

            // Buttons
            int btnY = pnlCard.Height - ShadowPad - 60;
            int rightEdge = ContentRightX;
            int saveW = 110;
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

            btnSave = MakePrimaryButton(_isEditMode ? "Update" : "Save");
            btnSave.Size = new Size(saveW, 40);
            btnSave.Location = new Point(saveX, btnY);
            btnSave.Click += async (s, e) => await SaveAsync();

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
                lblErrorName.Text = "⚠  Program name is required.";
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

        private Label MakeSectionLabel(string text, int x, int y)
        {
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9.5F),
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
            pnlCard.Controls.Add(lbl);
            return lbl;
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

        private NumericUpDown MakeNumeric(int x, int y, int width, decimal min, decimal max, decimal initial, int decimalPlaces = 0)
        {
            var n = new NumericUpDown
            {
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(x, y),
                Size = new Size(width, 30),
                Minimum = min,
                Maximum = max,
                Value = initial,
                DecimalPlaces = decimalPlaces,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlCard.Controls.Add(n);
            return n;
        }

        private DateTimePicker MakeDate(int x, int y, int width)
        {
            var dtp = new DateTimePicker
            {
                Font = new Font("Segoe UI", 9.5F),
                Format = DateTimePickerFormat.Short,
                Location = new Point(x, y),
                Size = new Size(width, 30)
            };
            pnlCard.Controls.Add(dtp);
            return dtp;
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
