using CRM.winforms.Forms;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// SaaS confirm dialog — replaces MessageBox for destructive / irreversible
    /// actions. HCI: error prevention (H5) + clear mapping (H2). Danger variant
    /// keeps the destructive action visually distinct; Esc always cancels.
    /// </summary>
    [DesignerCategory("Code")]
    public sealed class SaasConfirm : ModalForm
    {
        private bool _result;

        private SaasConfirm(string title, string message, string confirmText, bool danger, string? detail)
        {
            BuildCard(title, subtitle: null, width: 480, height: string.IsNullOrEmpty(detail) ? 230 : 270, hasFooter: true);
            int x = ContentLeftX, w = ContentWidth, y = ContentTopY;

            var lblMsg = new Label
            {
                Text = message,
                Font = UiKit.Body,
                ForeColor = UiKit.Ink,
                AutoSize = true,
                MaximumSize = new Size(w, 0),
                Location = new Point(x, y)
            };
            pnlBody.Controls.Add(lblMsg);
            y = lblMsg.Bottom + 12;

            if (!string.IsNullOrEmpty(detail))
            {
                var lblDetail = new Label
                {
                    Text = detail,
                    Font = UiKit.Small,
                    ForeColor = UiKit.InkMuted,
                    AutoSize = true,
                    MaximumSize = new Size(w, 0),
                    Location = new Point(x, y)
                };
                pnlBody.Controls.Add(lblDetail);
            }

            var btnCancel = ModalKit.AddSecondary(pnlCard, "Cancel");
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            var btnOk = new SaasButton(confirmText,
                danger ? SaasButtonVariant.Danger : SaasButtonVariant.Primary);
            pnlCard.Controls.Add(btnOk);
            btnOk.Click += (s, e) => { _result = true; DialogResult = DialogResult.OK; Close(); };

            int saveW = Math.Max(110, confirmText.Length * 11 + 24);
            LayoutFooter(btnOk, btnCancel, saveW: saveW, cancelW: 100);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
            Shown += (s, e) => btnCancel.Focus();
        }

        public static bool Ask(IWin32Window? owner, string title, string message,
            string confirmText = "Delete", bool danger = true, string? detail = null)
        {
            using var dlg = new SaasConfirm(title, message, confirmText, danger, detail);
            Form? f = owner as Form;
            var dr = f != null ? dlg.ShowModal(f) : dlg.ShowDialog();
            return dr == DialogResult.OK && dlg._result;
        }
    }
}
