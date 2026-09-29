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

        private Label lblTitleLbl = null!;
        private Label lblContentLbl = null!;
        private Label lblErrorTitle = null!;
        private Label lblErrorContent = null!;

        private TextField inpTitle = null!;
        private RichTextBox inpContent = null!;

        private SaasButton btnSave = null!;
        private SaasButton btnCancel = null!;

        public TermsFormDialog(TermsDto? existing)
        {
            _editing = existing;
            _isEditMode = existing != null;

            BuildCard(
                _isEditMode ? "Edit Terms & Conditions" : "New Terms & Conditions",
                _isEditMode
                    ? "Edit the content and clauses of this terms version."
                    : "Publishing a new version deactivates the previous active version.",
                width: 720,
                height: 640);

            BuildContent();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // Title
            lblTitleLbl = ModalKit.MakeLabel(pnlBody, "Title *", x, y);
            y += 20;
            inpTitle = ModalKit.MakeField(pnlBody, x, y, w, "e.g. Customer Agreement");
            inpTitle.Height = 36;
            y += 38 + 4;
            lblErrorTitle = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 20;

            // Content
            lblContentLbl = ModalKit.MakeLabel(pnlBody, "Content & Legal Clauses *", x, y);
            y += 20;

            inpContent = new RichTextBox
            {
                Font = AppTheme.FontInput,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                Location = new Point(x, y),
                Size = new Size(w, 280),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true
            };
            pnlBody.Controls.Add(inpContent);

            y += 280 + 4;
            lblErrorContent = ModalKit.MakeErrorLabel(pnlBody, x, y);
            y += 20;

            // Buttons
            btnCancel = ModalKit.AddSecondary(pnlCard, "Cancel");
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnSave = ModalKit.AddPrimary(pnlCard, _isEditMode ? "Save changes" : "Publish");
            btnSave.Click += async (s, e) => await SaveAsync();
            LayoutFooter(btnSave, btnCancel, saveW: 140);

            AcceptButton = btnSave;
            CancelButton = btnCancel;

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
                lblErrorTitle.Text = "Title is required.";
                lblErrorTitle.Visible = true;
                firstInvalid ??= inpTitle;
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(inpContent.Text))
            {
                lblErrorContent.Text = "Content is required.";
                lblErrorContent.Visible = true;
                firstInvalid ??= inpContent;
                valid = false;
            }

            firstInvalid?.Focus();
            return valid;
        }

        // ─── Factories ───





    }
}
