using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Modal dialog for Add / Edit on a Follow-Up.
    /// </summary>
    [DesignerCategory("Code")]
    public class FollowUpFormDialog : ModalForm
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private readonly FollowUpDto? _editing;
        private readonly bool _isEditMode;

        // ═══════════ CONTROLS ═══════════

        private Label lblSubtitle = null!;

        private Label lblSubject = null!;
        private Label lblNotes = null!;
        private Label lblScheduledAt = null!;
        private Label lblChannel = null!;
        private Label lblStatus = null!;
        private Label lblAssignedTo = null!;

        private TextField inpSubject = null!;
        private TextField inpNotes = null!;
        private DateTimePicker dtpScheduledAt = null!;
        private ComboBox cmbChannel = null!;
        private ComboBox cmbStatus = null!;
        private TextField inpAssignedTo = null!;

        private Label lblErrorSubject = null!;
        private Label lblErrorNotes = null!;

        private Button btnSave = null!;
        private Button btnCancel = null!;
        private Button btnArchive = null!;

        // ═══════════ CONSTRUCTOR ═══════════

        public FollowUpFormDialog(FollowUpDto? existing = null)
        {
            _editing = existing;
            _isEditMode = existing != null;

            BuildCard(
                _isEditMode ? "Edit Follow-Up" : "Add Follow-Up",
                width: 560,
                height: _isEditMode ? 700 : 680);

            BuildContent();
        }

        // ═══════════ CONTENT ═══════════

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // ── Subtitle ──
            lblSubtitle = new Label
            {
                Text = _isEditMode
                    ? "Update the follow-up information below."
                    : "Enter the follow-up information below. Fields marked * are required.",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 20)
            };
            pnlCard.Controls.Add(lblSubtitle);

            y += 32;

            // ── Subject * ──
            lblSubject = MakeLabel("Subject *", x, y);
            y += 20;
            inpSubject = MakeField(x, y, w, "Short summary");
            y += 38 + 4;
            lblErrorSubject = MakeErrorLabel(x, y);
            y += 20;

            // ── Notes (multiline) * ──
            lblNotes = MakeLabel("Notes *", x, y);
            y += 20;
            inpNotes = MakeField(x, y, w, "Describe what needs to be followed up...", multiline: true);
            y += 74 + 4;
            lblErrorNotes = MakeErrorLabel(x, y);
            y += 20;

            // ── Scheduled At + Channel (side by side) ──
            int halfW = (w - 12) / 2;

            lblScheduledAt = MakeLabel("Scheduled for *", x, y);
            lblChannel = MakeLabel("Channel", x + halfW + 12, y);
            y += 20;

            dtpScheduledAt = new DateTimePicker
            {
                Font = new Font("Segoe UI", 9.5F),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd HH:mm",
                ShowUpDown = true,
                Location = new Point(x, y),
                Size = new Size(halfW, 30)
            };
            pnlCard.Controls.Add(dtpScheduledAt);

            cmbChannel = MakeCombo(x + halfW + 12, y, halfW,
                new[] { "Call", "Email", "SMS", "Visit" }, 0);

            y += 38 + 14;

            // ── Status + Assigned To (side by side) ──
            lblStatus = MakeLabel("Status", x, y);
            lblAssignedTo = MakeLabel("Assigned to", x + halfW + 12, y);
            y += 20;

            cmbStatus = MakeCombo(x, y, halfW,
                new[] { "Scheduled", "Completed", "Cancelled" }, 0);
            inpAssignedTo = MakeField(x + halfW + 12, y, halfW, "staff-001");

            y += 38 + 20;

            // ── Buttons ──
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

            if (_isEditMode)
            {
                btnArchive = MakeDangerOutlineButton("Archive");
                btnArchive.Size = new Size(110, 40);
                btnArchive.Location = new Point(x, btnY);
                btnArchive.Click += async (s, e) => await ArchiveAsync();
            }

            // ── Prefill ──
            if (_editing != null)
            {
                inpSubject.Text = _editing.Subject ?? "";
                inpNotes.Text = _editing.Notes ?? "";
                dtpScheduledAt.Value = _editing.ScheduledAt;
                cmbChannel.SelectedIndex = Clamp(_editing.Channel, 0, 3);
                cmbStatus.SelectedIndex = Clamp(_editing.Status, 0, 2);
                inpAssignedTo.Text = _editing.AssignedToUserId ?? "";
            }
            else
            {
                dtpScheduledAt.Value = DateTime.Now.AddDays(1);
                inpAssignedTo.Text = "staff-001";
            }

            Shown += (s, e) => inpSubject.Focus();
        }

        private static int Clamp(int value, int min, int max)
            => value < min ? min : (value > max ? max : value);

        // ═══════════ SAVE ═══════════

        private async Task SaveAsync()
        {
            if (!ValidateInputs()) return;

            try
            {
                var dto = new FollowUpDto
                {
                    Subject = inpSubject.Text.Trim(),
                    Notes = inpNotes.Text.Trim(),
                    ScheduledAt = dtpScheduledAt.Value,
                    Channel = cmbChannel.SelectedIndex,
                    Status = cmbStatus.SelectedIndex,
                    AssignedToUserId = string.IsNullOrWhiteSpace(inpAssignedTo.Text)
                        ? null
                        : inpAssignedTo.Text.Trim()
                };

                if (_isEditMode && _editing != null)
                {
                    dto.FollowUpId = _editing.FollowUpId;
                    await _api.UpdateFollowUpAsync(_editing.FollowUpId, dto);
                }
                else
                {
                    await _api.CreateFollowUpAsync(dto);
                }

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

        // ═══════════ ARCHIVE ═══════════

        private async Task ArchiveAsync()
        {
            if (_editing == null) return;

            var confirm = MessageBox.Show(
                "Archive this follow-up?\n\n" +
                $"\"{_editing.Subject}\"\n\n" +
                "Data is preserved and can be restored.",
                "Confirm Archive",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                await _api.ArchiveFollowUpAsync(_editing.FollowUpId);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Archive failed:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ═══════════ VALIDATION ═══════════

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

        // ═══════════ CONTROL FACTORIES ═══════════

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
                                    bool multiline = false)
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

        private ComboBox MakeCombo(int x, int y, int width, string[] items, int selectedIndex)
        {
            var cmb = new ComboBox
            {
                Font = new Font("Segoe UI", 9.5F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(x, y),
                Size = new Size(width, 30),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat
            };
            cmb.Items.AddRange(items);
            if (selectedIndex >= 0 && selectedIndex < items.Length)
                cmb.SelectedIndex = selectedIndex;
            pnlCard.Controls.Add(cmb);
            return cmb;
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

        private Button MakeDangerOutlineButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9.5F),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Danger,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = AppTheme.Danger;
            b.FlatAppearance.MouseOverBackColor = AppTheme.DangerSoft;
            b.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(b, 8);
            pnlCard.Controls.Add(b);
            return b;
        }
    }
}