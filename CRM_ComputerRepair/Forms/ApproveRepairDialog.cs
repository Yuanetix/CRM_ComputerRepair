using CRM.winforms.Auth;
using CRM.winforms.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Modal dialog for Managers to approve pending repair requests,
    /// verify diagnostic estimates, and assign staff technicians.
    /// </summary>
    [DesignerCategory("Code")]
    public class ApproveRepairDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly RepairRequestDto _item;
        private List<UserSummaryDto> _staffUsers = new();

        private Label lblSummary = null!;
        private Label lblEstCost = null!;
        private TextField inpEstCost = null!;
        private Label lblStaff = null!;
        private ComboBox cmbStaff = null!;
        private Label lblNotes = null!;
        private TextField inpNotes = null!;

        private SaasButton btnApprove = null!;
        private SaasButton btnCancel = null!;

        public ApproveRepairDialog(RepairRequestDto item)
        {
            _item = item;

            BuildCard(
                "Approve Repair Request",
                "Authorize hardware servicing, confirm estimated cost, and assign technician.",
                width: 600,
                height: 580);

            BuildContent();
            this.Load += async (s, e) => await LoadStaffAsync();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // ── Info Summary Banner ──
            lblSummary = new Label
            {
                Text = $"Customer: {_item.CustomerDisplay}  ({_item.ContactDisplay})\n" +
                       $"Ticket:   {_item.RequestNumber} · {_item.DeviceDisplay} (SN: {_item.SerialDisplay})\n" +
                       $"Issue:    {_item.IssueDescription}",
                Font = UiKit.T.SmallStrong,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                MaximumSize = new Size(w - 28, 0),
                BackColor = Color.Transparent,
                Padding = new Padding(14, 12, 14, 12)
            };

            int bannerH = Math.Max(88, lblSummary.PreferredSize.Height + 24);
            var pnlSummary = ModalKit.MakeBanner(pnlBody, x, y, w, bannerH, UiKit.Wash(AppTheme.Primary));
            lblSummary.Dock = DockStyle.Fill;
            pnlSummary.Controls.Add(lblSummary);
            y += bannerH + 14;

            // ── Estimated Cost & Assign Technician ──
            int halfW = (w - ModalKit.HalfGap) / 2;

            lblEstCost = ModalKit.MakeLabel(pnlBody, "Approved Estimated Cost (₱)", x, y);
            lblStaff = ModalKit.MakeLabel(pnlBody, "Assign Staff Technician *", x + halfW + ModalKit.HalfGap, y);
            y += 20;

            inpEstCost = ModalKit.MakeField(pnlBody, x, y, halfW, "0.00");
            inpEstCost.Height = 36;
            if (_item.EstimatedCost.HasValue && _item.EstimatedCost > 0)
                inpEstCost.Text = _item.EstimatedCost.Value.ToString("F2");

            cmbStaff = MakeCombo(x + halfW + ModalKit.HalfGap, y, halfW, new[] { "Loading technicians..." }, 0);
            pnlBody.Controls.Add(cmbStaff);
            y += 44;

            // ── Manager Approval Notes ──
            lblNotes = ModalKit.MakeLabel(pnlBody, "Manager Approval Notes / Bench Instructions", x, y);
            y += 20;

            inpNotes = ModalKit.MakeField(pnlBody, x, y, w, "Enter bench priorities, parts approval, or customer instructions...", multiline: true);
            inpNotes.Height = 80;
            y += 88;

            // ── Footer Buttons ──
            btnCancel = new SaasButton("Cancel", SaasButtonVariant.Secondary);
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnApprove = new SaasButton("Approve & Authorize", SaasButtonVariant.Primary, "\uE73E");
            btnApprove.Click += async (s, e) => await SaveApprovalAsync();

            LayoutFooter(btnApprove, btnCancel, saveW: 180);

            AcceptButton = btnApprove;
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
                cmbStaff.Items.Add("— Select Staff Technician —");

                int selectIdx = 0;
                for (int i = 0; i < _staffUsers.Count; i++)
                {
                    var u = _staffUsers[i];
                    string role = u.Roles.Count > 0 ? $" ({u.Roles[0]})" : "";
                    cmbStaff.Items.Add($"{u.FullName} - @{u.UserName}{role}");

                    if (!string.IsNullOrEmpty(_item.AssignedToStaffId) &&
                        (string.Equals(u.Id, _item.AssignedToStaffId, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(u.UserName, _item.AssignedToStaffId, StringComparison.OrdinalIgnoreCase)))
                    {
                        selectIdx = i + 1;
                    }
                }

                cmbStaff.SelectedIndex = selectIdx;
                ModalKit.AdjustDropDownWidth(cmbStaff);
            }
            catch
            {
                cmbStaff.Items.Clear();
                cmbStaff.Items.Add("— Default Company Technician —");
                cmbStaff.SelectedIndex = 0;
            }
        }

        private async Task SaveApprovalAsync()
        {
            decimal? estCost = null;
            if (decimal.TryParse(inpEstCost.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedCost) ||
                decimal.TryParse(inpEstCost.Text.Trim(), out parsedCost))
            {
                estCost = parsedCost;
            }

            string? assignedStaffId = null;
            if (cmbStaff.SelectedIndex > 0 && cmbStaff.SelectedIndex - 1 < _staffUsers.Count)
            {
                assignedStaffId = _staffUsers[cmbStaff.SelectedIndex - 1].UserName ?? _staffUsers[cmbStaff.SelectedIndex - 1].Id;
            }

            string notes = inpNotes.Text.Trim();

            btnApprove.Enabled = false;
            try
            {
                await _api.ApproveRepairRequestAsync(_item.RepairRequestId, assignedStaffId, estCost, notes);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to approve repair request:\n{ex.Message}", "Approval Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnApprove.Enabled = true;
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
