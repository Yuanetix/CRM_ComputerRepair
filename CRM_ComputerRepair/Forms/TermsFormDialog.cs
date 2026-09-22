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
    public class TermsFormDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly TermsDto? _editing;
        private readonly bool _isEditMode;

        private Label lblSubtitle = null!;
        private Label lblTitleLbl = null!;
        private Label lblContentLbl = null!;
        private Label lblErrorTitle = null!;
        private Label lblErrorContent = null!;

        private TextField inpTitle = null!;
        private RichTextBox inpContent = null!;

        private Button btnSave = null!;
        private Button btnCancel = null!;

        public TermsFormDialog(TermsDto? existing)
        {
            _editing = existing;
            _isEditMode = existing != null;

            BuildCard(
                _isEditMode ? "Edit Terms & Conditions" : "New Terms & Conditions",
                width: 720,
                height: 720);

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
                    ? "Edit the content of this terms version."
                    : "Publishing a new version deactivates the previous active one.",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 20)
            };
            pnlCard.Controls.Add(lblSubtitle);
            y += 32;

            // Title
            lblTitleLbl = MakeLabel("Title *", x, y);
            y += 20;
            inpTitle = MakeField(x, y, w, "e.g. Customer Agreement");
            y += 38 + 4;
            lblErrorTitle = MakeErrorLabel(x, y);
            y += 20;

            // Content
            lblContentLbl = MakeLabel("Content *", x, y);
            y += 20;

            inpContent = new RichTextBox
            {
                Font = new Font("Segoe UI", 10F),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                Location = new Point(x, y),
                Size = new Size(w, 320),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true
            };
            pnlCard.Controls.Add(inpContent);

            y += 320 + 4;
            lblErrorContent = MakeErrorLabel(x, y);
            y += 20;

            // Buttons
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

            btnSave = MakePrimaryButton(_isEditMode ? "Save changes" : "Publish");
            btnSave.Size = new Size(saveW, 40);
            btnSave.Location = new Point(saveX, btnY);
            btnSave.Click += async (s, e) => await SaveAsync();

            // Prefill
            if (_editing != null)
            {
                inpTitle.Text = _editing.Title ?? "";
                inpContent.Text = _editing.Content ?? "";
            }

            Shown += (s, e) => inpTitle.Focus();
        }

        private async Task SaveAsync()
        {
            if (!ValidateInputs()) return;

            try
            {
                if (_isEditMode && _editing != null)
                {
                    var update = new TermsDto
                    {
                        TermsId = _editing.TermsId,
                        Title = inpTitle.Text.Trim(),
                        Content = inpContent.Text,
                        IsActive = _editing.IsActive
                    };
                    await _api.UpdateTermsAsync(_editing.TermsId, update);
                }
                else
                {
                    var create = new TermsDto
                    {
                        Title = inpTitle.Text.Trim(),
                        Content = inpContent.Text
                    };
                    await _api.CreateTermsAsync(create);
                }

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
            lblErrorTitle.Visible = false;
            lblErrorContent.Visible = false;
            inpTitle.HasError = false;

            var valid = true;
            Control? firstInvalid = null;

            if (string.IsNullOrWhiteSpace(inpTitle.Text))
            {
                inpTitle.HasError = true;
                lblErrorTitle.Text = "⚠  Title is required.";
                lblErrorTitle.Visible = true;
                firstInvalid ??= inpTitle;
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(inpContent.Text))
            {
                lblErrorContent.Text = "⚠  Content is required.";
                lblErrorContent.Visible = true;
                firstInvalid ??= inpContent;
                valid = false;
            }

            firstInvalid?.Focus();
            return valid;
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

        private TextField MakeField(int x, int y, int width, string placeholder)
        {
            var tf = new TextField
            {
                PlaceholderText = placeholder,
                Location = new Point(x, y),
                Size = new Size(width, 38)
            };
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