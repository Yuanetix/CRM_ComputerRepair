using CRM.winforms.Controls;
using CRM.winforms.Forms;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
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
        private Label lblDiscount = null!;
        private Label lblMinSpend = null!;
        private Label lblStart = null!;
        private Label lblEnd = null!;

        private TextField inpProgramName = null!;
        private TextField inpDescription = null!;
        private NumericUpDown numPoints = null!;
        private NumericUpDown numDiscount = null!;
        private NumericUpDown numMinSpend = null!;
        private DateTimePicker dtpStart = null!;
        private DateTimePicker dtpEnd = null!;

        private Label lblErrorName = null!;

        private Button btnSave = null!;
        private Button btnCancel = null!;

        public LoyaltyFormDialog(LoyaltyProgramDto? existing)
        {
            _editing = existing;
            _isEditMode = existing != null;

            BuildCard(
                _isEditMode ? "Edit Loyalty Program" : "Add Loyalty Program",
                width: 560,
                height: _isEditMode ? 700 : 680);

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
                    ? "Update this program's details."
                    : "Create a new loyalty program for your customers.",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 20)
            };
            pnlCard.Controls.Add(lblSubtitle);
            y += 32;

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
            y += 74 + 14;

            // Points + Discount
            int halfW = (w - 12) / 2;

            lblPoints = MakeLabel("Points per ₱1", x, y);
            lblDiscount = MakeLabel("Discount %", x + halfW + 12, y);
            y += 20;

            numPoints = MakeNumeric(x, y, halfW, 1, 1000, 1);
            numDiscount = MakeNumeric(x + halfW + 12, y, halfW, 0, 100, 5, decimalPlaces: 1);
            y += 38 + 14;

            // Minimum spend
            lblMinSpend = MakeLabel("Minimum spend (₱)", x, y);
            y += 20;
            numMinSpend = MakeNumeric(x, y, w, 0, 1000000, 0, decimalPlaces: 2);
            y += 38 + 14;

            // Start / End
            lblStart = MakeLabel("Start date *", x, y);
            lblEnd = MakeLabel("End date *", x + halfW + 12, y);
            y += 20;

            dtpStart = MakeDate(x, y, halfW);
            dtpEnd = MakeDate(x + halfW + 12, y, halfW);
            y += 38 + 20;

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
                numPoints.Value = Math.Max(1, Math.Min(1000, _editing.PointsPerPeso));
                numDiscount.Value = Math.Max(0, Math.Min(100, _editing.DiscountPercentage));
                numMinSpend.Value = Math.Max(0, Math.Min(1000000, _editing.MinimumSpend));
                dtpStart.Value = _editing.StartDate;
                dtpEnd.Value = _editing.EndDate;
            }
            else
            {
                dtpStart.Value = DateTime.Today;
                dtpEnd.Value = DateTime.Today.AddYears(1);
            }

            Shown += (s, e) => inpProgramName.Focus();
        }

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
                    DiscountPercentage = numDiscount.Value,
                    MinimumSpend = numMinSpend.Value,
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

            return true;
        }

        // ─── Factories ───

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