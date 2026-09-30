using CRM.winforms.Auth;
using CRM.winforms.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Modal dialog for Managers to reassign a repair request to a different staff technician.
    /// </summary>
    [DesignerCategory("Code")]
    public class ReassignRepairDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly RepairRequestDto _item;
        private List<UserSummaryDto> _staffUsers = new();

        private Label lblSummary = null!;
        private Label lblStaff = null!;
        private ComboBox cmbStaff = null!;
        private Label lblNotes = null!;
        private TextField inpNotes = null!;

        private SaasButton btnReassign = null!;
        private SaasButton btnCancel = null!;

        public ReassignRepairDialog(RepairRequestDto item)
        {
            _item = item;

            BuildCard(
                "Reassign Repair Ticket",
                "Transfer hardware diagnostic and servicing responsibility to another technician.",
                width: 580,
                height: 520);

            BuildContent();
            this.Load += async (s, e) => await LoadStaffAsync();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // ── Info Summary Banner ──
            string currentTech = !string.IsNullOrWhiteSpace(_item.AssignedToStaffId) ? _item.AssignedToStaffId : "Unassigned";
            lblSummary = new Label
            {
                Text = $"Customer:     {_item.CustomerDisplay}\n" +
                       $"Ticket:       {_item.RequestNumber} · {_item.DeviceDisplay}\n" +
                       $"Current Tech: {currentTech}\n" +
                       $"Status:       {_item.StatusText} (Priority: {_item.PriorityText})",
                Font = UiKit.T.SmallStrong,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                MaximumSize = new Size(w - 28, 0),
                BackColor = Color.Transparent,
                Padding = new Padding(14, 12, 14, 12)
            };

            int bannerH = Math.Max(88, lblSummary.PreferredSize.Height + 24);
            var pnlSummary = ModalKit.MakeBanner(pnlBody, x, y, w, bannerH, UiKit.Wash(AppTheme.Warning));
            lblSummary.Dock = DockStyle.Fill;
            pnlSummary.Controls.Add(lblSummary);
            y += bannerH + 16;

            // ── New Assigned Technician ──
            lblStaff = ModalKit.MakeLabel(pnlBody, "New Assigned Technician / Staff Member *", x, y);
            y += 20;

            cmbStaff = MakeCombo(x, y, w, new[] { "Loading technicians..." }, 0);
            pnlBody.Controls.Add(cmbStaff);
            y += 44;

            // ── Reassignment Reason ──
            lblNotes = ModalKit.MakeLabel(pnlBody, "Reassignment Reason / Transfer Notes", x, y);
            y += 20;

            inpNotes = ModalKit.MakeField(pnlBody, x, y, w, "Specify why this ticket is being reassigned (e.g. workload balancing, specialist skill, staff absence)...", multiline: true);
            inpNotes.Height = 76;
            y += 84;

            // ── Footer Buttons ──
            btnCancel = new SaasButton("Cancel", SaasButtonVariant.Secondary);
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnReassign = new SaasButton("Reassign Ticket", SaasButtonVariant.Primary, "\uE716");
            btnReassign.Click += async (s, e) => await SaveReassignmentAsync();

            LayoutFooter(btnReassign, btnCancel, saveW: 160);

            AcceptButton = btnReassign;
            CancelButton = btnCancel;
        }

        private async Task LoadStaffAsync()
        {
            try
            {
                var users = await _api.GetUsersAsync(activeOnly: true);
                _staffUsers = users.Where(u => u.Roles.Any(r => r.Equals("Staff", StringComparison.OrdinalIgnoreCase) || r.Equals("Manager", StringComparison.OrdinalIgnoreCase))).ToList();

                if (_staffUsers.Count == 0)
                    _staffUsers = users.ToList();

                cmbStaff.Items.Clear();
                cmbStaff.Items.Add("— Select New Staff Technician —");

                for (int i = 0; i < _staffUsers.Count; i++)
                {
                    var u = _staffUsers[i];
                    string role = u.Roles.Count > 0 ? $" ({u.Roles[0]})" : "";
                    cmbStaff.Items.Add($"{u.FullName} - @{u.UserName}{role}");
                }

                if (cmbStaff.Items.Count > 1)
                    cmbStaff.SelectedIndex = 1;
                else
                    cmbStaff.SelectedIndex = 0;

                ModalKit.AdjustDropDownWidth(cmbStaff);
            }
            catch
            {
                cmbStaff.Items.Clear();
                cmbStaff.Items.Add("— Default Company Technician —");
                cmbStaff.SelectedIndex = 0;
            }
        }

        private async Task SaveReassignmentAsync()
        {
            if (cmbStaff.SelectedIndex <= 0 || cmbStaff.SelectedIndex - 1 >= _staffUsers.Count)
            {
                MessageBox.Show("Please select a technician to reassign this repair ticket to.", "Technician Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedUser = _staffUsers[cmbStaff.SelectedIndex - 1];
            string assignedStaffId = selectedUser.UserName ?? selectedUser.Id;
            string notes = inpNotes.Text.Trim();

            btnReassign.Enabled = false;
            try
            {
                await _api.ReassignRepairRequestAsync(_item.RepairRequestId, assignedStaffId, notes);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to reassign repair request:\n{ex.Message}", "Reassign Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnReassign.Enabled = true;
            }
        }

        private ComboBox MakeCombo(int x, int y, int width, string[] items, int selectedIndex)
        {
            var cmb = new ComboBox
            {
                Font = AppTheme.FontInput,
                DropDownStyle = ComboBoxStyle.DropDownList,
                DropDownWidth = Math.Max(width, 320),
                Location = new Point(x, y),
                Size = new Size(width, 32),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat
            };
            cmb.Items.AddRange(items);
            if (selectedIndex >= 0 && selectedIndex < items.Length)
                cmb.SelectedIndex = selectedIndex;

            pnlBody.Controls.Add(cmb);
            return cmb;
        }
    }
}
