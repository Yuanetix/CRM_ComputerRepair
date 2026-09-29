using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using CRM.winforms.Auth;
using CRM.winforms.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Admin modal dialog to review, approve, reject, and dispatch retention requests.
    /// Incorporates self-approval prevention, audit timeline, and live email dispatch.
    /// </summary>
    [DesignerCategory("Code")]
    public class RetentionReviewDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private RetentionRequestDto _request;
        private readonly bool _canReview;

        private Label lblCustomerInfo = null!;
        private Panel pnlTimeline = null!;
        private Panel pnlActions = null!;
        private Panel pnlCampaign = null!;

        private TextBox txtRemarks = null!;
        private TextBox txtRejectionReason = null!;
        private Label lblErrorReject = null!;
        private SaasButton btnApprove = null!;
        private SaasButton btnReject = null!;
        private SaasButton btnDispatch = null!;
        private Label lblDispatchStatus = null!;

        public bool StateChanged { get; private set; }

        public RetentionReviewDialog(RetentionRequestDto request)
        {
            _request = request;
            _canReview = (UserSession.Role == "Admin" || UserSession.Role == "Super Admin");

            BuildCard(
                _request.Status == 0 ? "Review Retention Request" : "Retention Request Details",
                "Administrative review, separation of duties, audit timeline, and campaign dispatch.",
                width: 680,
                height: 680);

            BuildContent();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // ── Section 1: Customer & Segment Banner ──
            var pnlCustomer = ModalKit.MakeBanner(pnlBody, x, y, w, 78, AppTheme.Neutral);

            lblCustomerInfo = new Label
            {
                Text = $"{_request.CustomerName}   ·   {_request.CustomerEmail ?? "No email"}   ·   {_request.CustomerPhone ?? "No phone"}\n" +
                       $"Target Segment: {_request.TargetSegmentName}   ·   Action: {_request.ActionType}   ·   Discount: {_request.ProposedDiscountPercent:0.#}%",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextPrimary,
                BackColor = AppTheme.Neutral,
                UseMnemonic = false,
                Location = new Point(14, 10),
                AutoSize = true,
                MaximumSize = new Size(w - 28, 0)
            };
            pnlCustomer.Controls.Add(lblCustomerInfo);
            pnlCustomer.Height = Math.Max(78, lblCustomerInfo.PreferredSize.Height + 20);
            y += pnlCustomer.Height + 10;

            // ── Section 2: Proposal Details & Reason ──
            ModalKit.MakeSection(pnlBody, "Proposal & retention reason", x, y, w);
            y += 22;

            var txtDetails = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(w, 64),
                Multiline = true,
                ReadOnly = true,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = AppTheme.FontInput,
                Text = $"Reason Category: {_request.ReasonCategory}" +
                       (!string.IsNullOrWhiteSpace(_request.ReasonNote) ? $" ({_request.ReasonNote})" : "") + "\r\n" +
                       $"Details: {_request.RetentionDetails}"
            };
            pnlBody.Controls.Add(txtDetails);
            y += 74;

            // ── Section 3: Approval History & Timeline ──
            ModalKit.MakeSection(pnlBody, "Approval audit timeline", x, y, w);
            y += 22;

            pnlTimeline = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, 80),
                BackColor = Color.FromArgb(248, 249, 252),
                BorderStyle = BorderStyle.FixedSingle
            };

            RenderTimeline();
            pnlBody.Controls.Add(pnlTimeline);
            y += 90;

            // ── Section 4: Decision Actions (If Pending) OR Campaign Dispatch (If Approved) ──
            if (_request.Status == 0) // Pending
            {
                pnlActions = new Panel
                {
                    Location = new Point(x, y),
                    Size = new Size(w, 190),
                    BackColor = AppTheme.Surface
                };

                BuildPendingActionsPanel(pnlActions, w);
                pnlBody.Controls.Add(pnlActions);
                y += 200;
            }
            else if (_request.Status == 1) // Approved
            {
                pnlCampaign = new Panel
                {
                    Location = new Point(x, y),
                    Size = new Size(w, 190),
                    BackColor = AppTheme.Surface
                };

                BuildApprovedCampaignPanel(pnlCampaign, w);
                pnlBody.Controls.Add(pnlCampaign);
                y += 200;
            }
            else // Rejected
            {
                var lblRej = new Label
                {
                    Text = $"Status: REJECTED\nReason: {_request.RejectionReason}\nRemarks: {_request.ReviewRemarks ?? "None"}",
                    Font = AppTheme.FontSubtitle,
                    ForeColor = AppTheme.Danger,
                    Location = new Point(x, y),
                    Size = new Size(w, 60)
                };
                pnlBody.Controls.Add(lblRej);
                y += 70;
            }

            // ── Close button ──
            var btnCloseBottom = ModalKit.AddSecondary(pnlCard, "Close");
            LayoutFooter(btnCloseBottom, null, saveW: 100);

            btnCloseBottom.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

            CancelButton = btnCloseBottom;
            Shown += (s, e) =>
            {
                if (_request.Status == 0 && _canReview && btnApprove != null) btnApprove.Focus();
                else btnCloseBottom.Focus();
            };
        }

        private void RenderTimeline()
        {
            pnlTimeline.Controls.Clear();

            var lblNode1 = new Label
            {
                Text = $"[1] Submitted: {_request.SubmittedByName} on {_request.SubmittedAt.ToLocalTime():MMM d, yyyy h:mm tt}",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextPrimary,
                Location = new Point(12, 10),
                AutoSize = true
            };
            pnlTimeline.Controls.Add(lblNode1);

            string node2Text;
            Color node2Color;

            if (_request.Status == 1)
            {
                node2Text = $"[2] Approved by {_request.ReviewedByName} on {_request.ReviewedAt?.ToLocalTime():MMM d, yyyy h:mm tt} — Remarks: {_request.ReviewRemarks ?? "Approved"}";
                node2Color = AppTheme.Success;
            }
            else if (_request.Status == 2)
            {
                node2Text = $"[2] Rejected by {_request.ReviewedByName} on {_request.ReviewedAt?.ToLocalTime():MMM d, yyyy h:mm tt} — Reason: {_request.RejectionReason}";
                node2Color = AppTheme.Danger;
            }
            else
            {
                node2Text = "[2] Pending Administrative Review (Waiting for Admin approval)";
                node2Color = AppTheme.Warning;
            }

            var lblNode2 = new Label
            {
                Text = node2Text,
                Font = AppTheme.FontBody,
                ForeColor = node2Color,
                Location = new Point(12, 38),
                AutoSize = true
            };
            pnlTimeline.Controls.Add(lblNode2);
        }

        private void BuildPendingActionsPanel(Panel pnl, int w)
        {
            bool isSelfSubmitter = string.Equals(_request.SubmittedByUserId, UserSession.UserId, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(_request.SubmittedByUserId, UserSession.Username, StringComparison.OrdinalIgnoreCase);

            if (!_canReview)
            {
                var lblNoRole = new Label
                {
                    Text = "Notice: Only users with the Admin or Super Admin role can approve or reject retention requests.",
                    Font = AppTheme.FontSubtitle,
                    ForeColor = AppTheme.TextSecondary,
                    Location = new Point(0, 10),
                    Size = new Size(w, 30)
                };
                pnl.Controls.Add(lblNoRole);
                return;
            }

            if (isSelfSubmitter)
            {
                var lblSelf = new Label
                {
                    Text = "Self-Approval Prevention Active:\nYou submitted this request. To ensure proper separation of duties, a different Administrator must review and approve it.",
                    Font = AppTheme.FontBody,
                    ForeColor = AppTheme.Danger,
                    Location = new Point(0, 10),
                    Size = new Size(w, 48)
                };
                pnl.Controls.Add(lblSelf);
                return;
            }

            ModalKit.MakeLabel(pnl, "Review Remarks (Optional)", 0, 0);

            txtRemarks = new TextBox
            {
                Location = new Point(0, 20),
                Size = new Size(w, 34),
                Multiline = true,
                Font = AppTheme.FontInput,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            pnl.Controls.Add(txtRemarks);

            ModalKit.MakeLabel(pnl, "Reason for Rejection (Required if rejecting)", 0, 58);

            txtRejectionReason = new TextBox
            {
                Location = new Point(0, 78),
                Size = new Size(w, 34),
                Multiline = true,
                Font = AppTheme.FontInput,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            pnl.Controls.Add(txtRejectionReason);

            lblErrorReject = ModalKit.MakeErrorLabel(pnl, 0, 114);
            lblErrorReject.Size = new Size(w, 20);

            btnReject = ModalKit.AddDangerOutline(pnl, "Reject Request");
            btnReject.Size = new Size(120, ModalKit.BtnH);
            btnReject.Location = new Point(w - 250, 142);
            btnReject.Click += async (s, e) => await RejectAsync();

            btnApprove = ModalKit.AddPrimary(pnl, "Approve Request");
            btnApprove.Size = new Size(120, ModalKit.BtnH);
            btnApprove.Location = new Point(w - 120, 142);
            btnApprove.Click += async (s, e) => await ApproveAsync();
        }

        private void BuildApprovedCampaignPanel(Panel pnl, int w)
        {
            ModalKit.MakeSection(pnl, "Automated email campaign", 0, 0, w);

            var lblDesc = new Label
            {
                Text = "This retention request was automatically added to Email Campaigns upon Admin approval.",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                Location = new Point(0, 24),
                AutoSize = true,
                MaximumSize = new Size(w, 0)
            };
            pnl.Controls.Add(lblDesc);

            btnDispatch = ModalKit.AddPrimary(pnl, "Dispatch Retention Email Now");
            btnDispatch.Size = new Size(240, ModalKit.BtnH);
            btnDispatch.Location = new Point(0, 52);
            btnDispatch.Click += async (s, e) => await DispatchEmailAsync();

            lblDispatchStatus = new Label
            {
                Text = _request.IsDispatched
                    ? "Retention email has been dispatched via SMTP."
                    : "Ready for dispatch. Click above to send immediately via SMTP.",
                Font = AppTheme.FontSubtitle,
                ForeColor = _request.IsDispatched ? AppTheme.Success : AppTheme.TextSecondary,
                Location = new Point(250, 58),
                AutoSize = true,
                MaximumSize = new Size(w - 260, 0)
            };
            pnl.Controls.Add(lblDispatchStatus);
            if (_request.IsDispatched) btnDispatch.Enabled = false;

            // Formatted preview snippet
            var txtPreview = new TextBox
            {
                Location = new Point(0, 96),
                Size = new Size(w, 80),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = AppTheme.FontInput,
                Text = $"To: {_request.CustomerEmail}\r\nSubject: {_request.ProposedDiscountPercent:0.#}% Off Your Next Fixory Computer Tune-Up or Repair\r\n\r\nDear {_request.CustomerName},\r\nThank you for choosing Fixory Computer Repair Services! Please enjoy an exclusive {_request.ProposedDiscountPercent:0.#}% discount on your next computer service.\r\nRedeem with promo code FIXORY-{_request.TargetSegmentName.ToUpperInvariant()}-... (Valid for 14 days)."
            };
            pnl.Controls.Add(txtPreview);
        }

        private async Task ApproveAsync()
        {
            btnApprove.Enabled = false;
            btnApprove.Text = "Approving...";

            try
            {
                await _api.ApproveRetentionRequestAsync(_request.RetentionRequestId, txtRemarks?.Text?.Trim());
                StateChanged = true;

                MessageBox.Show(
                    "Retention request has been approved!\n\nIt has been automatically generated and added to the Email Campaigns queue.",
                    "Request Approved",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Reload request details
                var updated = await _api.GetRetentionRequestAsync(_request.RetentionRequestId);
                if (updated != null) _request = updated;

                this.Controls.Clear();
                BuildCard("Retention Request Details", width: 680, height: 740);
                BuildContent();
                Invalidate(true);
            }
            catch (Exception ex)
            {
                btnApprove.Enabled = true;
                btnApprove.Text = "Approve Request";
                MessageBox.Show($"Failed to approve request:\n\n{ex.Message}", "Approval Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task RejectAsync()
        {
            var reason = txtRejectionReason?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                lblErrorReject.Text = "Reason for rejection is mandatory.";
                lblErrorReject.Visible = true;
                txtRejectionReason?.Focus();
                return;
            }
            lblErrorReject.Visible = false;

            btnReject.Enabled = false;
            btnReject.Text = "Rejecting...";

            try
            {
                await _api.RejectRetentionRequestAsync(_request.RetentionRequestId, reason, txtRemarks?.Text?.Trim());
                StateChanged = true;

                MessageBox.Show(
                    "Retention request has been marked as Rejected.",
                    "Request Rejected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                var updated = await _api.GetRetentionRequestAsync(_request.RetentionRequestId);
                if (updated != null) _request = updated;

                this.Controls.Clear();
                BuildCard("Retention Request Details", width: 680, height: 740);
                BuildContent();
                Invalidate(true);
            }
            catch (Exception ex)
            {
                btnReject.Enabled = true;
                btnReject.Text = "Reject Request";
                MessageBox.Show($"Failed to reject request:\n\n{ex.Message}", "Rejection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DispatchEmailAsync()
        {
            if (!_request.CampaignEmailLogId.HasValue)
            {
                MessageBox.Show("No campaign record found for this request.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnDispatch.Enabled = false;
            btnDispatch.Text = "Dispatching...";

            try
            {
                await _api.DispatchRetentionEmailAsync(_request.CampaignEmailLogId.Value);
                StateChanged = true;

                lblDispatchStatus.Text = "Retention email has been dispatched via SMTP.";
                lblDispatchStatus.ForeColor = AppTheme.Success;
                btnDispatch.Text = "Dispatched";

                MessageBox.Show(
                    $"Retention email has been successfully dispatched via SMTP to {_request.CustomerEmail}.",
                    "Email Dispatched",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                btnDispatch.Enabled = true;
                btnDispatch.Text = "Dispatch Retention Email Now";
                MessageBox.Show($"Failed to dispatch email via SMTP:\n\n{ex.Message}", "Dispatch Problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }





    }
}
