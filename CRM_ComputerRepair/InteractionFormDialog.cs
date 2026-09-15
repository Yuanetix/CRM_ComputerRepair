using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Modal dialog for Add / Edit on Inquiry, Complaint, or Feedback.
    /// Same look and feel as CustomerFormDialog.
    /// </summary>
    [DesignerCategory("Code")]
    public class InteractionFormDialog : ModalForm
    {
        // ═══════════ STATE ═══════════

        private readonly ApiClient _api = new ApiClient();
        private readonly InteractionTypeFilter _type;
        private readonly InteractionDto? _editing;
        private readonly bool _isEditMode;

        // ═══════════ CONTROLS ═══════════

        private Label lblSubtitle = null!;

        private Label lblSubject = null!;
        private Label lblNotes = null!;
        private Label lblPriority = null!;
        private Label lblStatus = null!;
        private Label lblResolution = null!;

        private TextField inpSubject = null!;
        private TextField inpNotes = null!;
        private ComboBox cmbPriority = null!;
        private ComboBox cmbStatus = null!;
        private TextField inpResolution = null!;

        private Label lblErrorSubject = null!;
        private Label lblErrorNotes = null!;
        private Label lblErrorResolution = null!;

        private Button btnSave = null!;
        private Button btnCancel = null!;
        private Button btnArchive = null!;

        // ═══════════ CONSTRUCTOR ═══════════

        public InteractionFormDialog(InteractionTypeFilter type, InteractionDto? existing = null)
        {
            _type = type;
            _editing = existing;
            _isEditMode = existing != null;

            BuildCard(
                _isEditMode ? $"Edit {SingularText}" : $"Add {SingularText}",
                width: 560,
                height: _isEditMode ? 700 : 680);

            BuildContent();
        }

        // ═══════════ TYPE-AWARE LABELS ═══════════

        private string SingularText => _type switch
        {
            InteractionTypeFilter.Inquiry => "Inquiry",
            InteractionTypeFilter.Complaint => "Complaint",
            InteractionTypeFilter.Feedback => "Feedback",
            _ => "Interaction"
        };

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
                    ? $"Update the {SingularText.ToLower()}'s information below."
                    : $"Enter the {SingularText.ToLower()}'s information below. Fields marked * are required.",
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
            inpNotes = MakeField(x, y, w, "Describe the details...", multiline: true);
            y += 74 + 4;
            lblErrorNotes = MakeErrorLabel(x, y);
            y += 20;

            // ── Priority + Status (side by side) ──
            int halfW = (w - 12) / 2;

            lblPriority = MakeLabel("Priority", x, y);
            lblStatus = MakeLabel("Status", x + halfW + 12, y);
            y += 20;

            cmbPriority = MakeCombo(x, y, halfW,
                new[] { "Low", "Medium", "High" }, 1);
            cmbStatus = MakeCombo(x + halfW + 12, y, halfW,
                new[] { "Open", "In Progress", "Closed" }, 0);

            y += 38 + 14;

            // ── Resolution (multiline) ──
            lblResolution = MakeLabel("Resolution (required when Closed)", x, y);
            y += 20;
            inpResolution = MakeField(x, y, w, "How was it resolved?", multiline: true);
            y += 74 + 4;
            lblErrorResolution = MakeErrorLabel(x, y);
            y += 20;

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
                cmbPriority.SelectedIndex = Clamp(_editing.Priority, 0, 2);
                cmbStatus.SelectedIndex = Clamp(_editing.Status, 0, 2);
                inpResolution.Text = _editing.Resolution ?? "";
            }

            cmbStatus.SelectedIndexChanged += (s, e) => UpdateResolutionState();
            UpdateResolutionState();

            Shown += (s, e) => inpSubject.Focus();
        }

        private static int Clamp(int value, int min, int max)
            => value < min ? min : (value > max ? max : value);

        private void UpdateResolutionState()
        {
            bool isClosed = cmbStatus.SelectedIndex == 2;
            lblResolution.ForeColor = isClosed ? AppTheme.TextSecondary : AppTheme.TextMuted;
            inpResolution.InnerTextBox.Enabled = isClosed;

            if (!isClosed)
            {
                inpResolution.Text = "";
                ClearError(inpResolution, lblErrorResolution);
            }
        }

        // ═══════════ SAVE ═══════════

        private async Task SaveAsync()
        {
            if (!ValidateInputs()) return;

            try
            {
                var dto = new InteractionDto
                {
                    InteractionType = (int)_type,
                    Subject = inpSubject.Text.Trim(),
                    Notes = inpNotes.Text.Trim(),
                    Priority = cmbPriority.SelectedIndex,
                    Status = cmbStatus.SelectedIndex,
                    Resolution = string.IsNullOrWhiteSpace(inpResolution.Text)
                        ? null
                        : inpResolution.Text.Trim()
                };

                if (_isEditMode && _editing != null)
                {
                    dto.CustomerInteractionId = _editing.CustomerInteractionId;
                    await _api.UpdateInteractionAsync(_editing.CustomerInteractionId, dto);
                }
                else
                {
                    await _api.CreateInteractionAsync(dto);
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
                $"Archive this {SingularText.ToLower()}?\n\n" +
                $"\"{_editing.Subject}\"\n\n" +
                "Data is preserved and can be restored.",
                "Confirm Archive",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                await _api.ArchiveInteractionAsync(_editing.CustomerInteractionId);
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

            if (cmbStatus.SelectedIndex == 2 &&
                string.IsNullOrWhiteSpace(inpResolution.Text))
            {
                ShowError(inpResolution, lblErrorResolution,
                    "Resolution is required when status is Closed.");
                firstInvalid ??= inpResolution;
                valid = false;
            }

            firstInvalid?.Focus();
            return valid;
        }

        private void ClearErrors()
        {
            ClearError(inpSubject, lblErrorSubject);
            ClearError(inpNotes, lblErrorNotes);
            ClearError(inpResolution, lblErrorResolution);
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