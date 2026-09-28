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
        private Button btnApprove = null!;
        private Button btnReject = null!;
        private Button btnDispatch = null!;
        private Label lblDispatchStatus = null!;

        public bool StateChanged { get; private set; }

        public RetentionReviewDialog(RetentionRequestDto request)
        {
            _request = request;
            _canReview = (UserSession.Role == "Admin" || UserSession.Role == "Super Admin");

            BuildCard(
                _request.Status == 0 ? "Review Retention Request" : "Retention Request Details",
                width: 680,
                height: 740);

            BuildContent();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // ── Section 1: Customer & Segment Banner ──
            var pnlCustomer = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, 64),
                BackColor = AppTheme.Neutral
            };

            lblCustomerInfo = new Label
            {
                Text = $"{_request.CustomerName}   ·   {_request.CustomerEmail ?? "No email"}   ·   {_request.CustomerPhone ?? "No phone"}\n" +
                       $"Target Segment: {_request.TargetSegmentName}   ·   Action: {_request.ActionType}   ·   Discount: {_request.ProposedDiscountPercent:0.#}%",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextPrimary,
                Location = new Point(14, 12),
                Size = new Size(w - 28, 40)
            };
            pnlCustomer.Controls.Add(lblCustomerInfo);
            pnlCard.Controls.Add(pnlCustomer);
            y += 74;

            // ── Section 2: Proposal Details & Reason ──
            var lblSectionProposal = MakeLabel("PROPOSAL & RETENTION REASON", x, y);
            lblSectionProposal.Font = AppTheme.FontSection;
            lblSectionProposal.ForeColor = AppTheme.Primary;
            y += 22;

            var txtDetails = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(w, 64),
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.White,
                Font = AppTheme.FontInput,
                Text = $"Reason Category: {_request.ReasonCategory}" +
                       (!string.IsNullOrWhiteSpace(_request.ReasonNote) ? $" ({_request.ReasonNote})" : "") + "\r\n" +
                       $"Details: {_request.RetentionDetails}"
            };
            pnlCard.Controls.Add(txtDetails);
            y += 74;

            // ── Section 3: Approval History & Timeline ──
            var lblSectionTimeline = MakeLabel("APPROVAL AUDIT TIMELINE", x, y);
            lblSectionTimeline.Font = AppTheme.FontSection;
            lblSectionTimeline.ForeColor = AppTheme.Primary;
            y += 22;

            pnlTimeline = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, 80),
                BackColor = Color.FromArgb(248, 249, 252),
                BorderStyle = BorderStyle.FixedSingle
            };

            RenderTimeline();
            pnlCard.Controls.Add(pnlTimeline);
            y += 90;

            // ── Section 4: Decision Actions (If Pending) OR Campaign Dispatch (If Approved) ──
            if (_request.Status == 0) // Pending
            {
                pnlActions = new Panel
                {
                    Location = new Point(x, y),
                    Size = new Size(w, 190),
                    BackColor = Color.Transparent
                };

                BuildPendingActionsPanel(pnlActions, w);
                pnlCard.Controls.Add(pnlActions);
                y += 200;
            }
            else if (_request.Status == 1) // Approved
            {
                pnlCampaign = new Panel
                {
                    Location = new Point(x, y),
                    Size = new Size(w, 190),
                    BackColor = Color.Transparent
                };

                BuildApprovedCampaignPanel(pnlCampaign, w);
                pnlCard.Controls.Add(pnlCampaign);
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
                pnlCard.Controls.Add(lblRej);
                y += 70;
            }

            // ── Close button ──
            var btnCloseBottom = MakeSecondaryButton("Close", x + w - 100, y + 10, 100);
            btnCloseBottom.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            pnlCard.Controls.Add(btnCloseBottom);
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

            var lblRem = MakeLabel("Review Remarks (Optional)", 0, 0);
            pnl.Controls.Add(lblRem);

            txtRemarks = new TextBox
            {
                Location = new Point(0, 20),
                Size = new Size(w, 42),
                Multiline = true,
                Font = AppTheme.FontInput
            };
            pnl.Controls.Add(txtRemarks);

            var lblRej = MakeLabel("Reason for Rejection (Required if rejecting)", 0, 68);
            pnl.Controls.Add(lblRej);

            txtRejectionReason = new TextBox
            {
                Location = new Point(0, 88),
                Size = new Size(w, 42),
                Multiline = true,
                Font = AppTheme.FontInput
            };
            pnl.Controls.Add(txtRejectionReason);

            btnReject = MakeSecondaryButton("Reject Request", w - 240, 142, 110);
            btnReject.ForeColor = AppTheme.Danger;
            btnReject.Click += async (s, e) => await RejectAsync();

            btnApprove = MakePrimaryButton("Approve Request", w - 120, 142, 120);
            btnApprove.Click += async (s, e) => await ApproveAsync();

            pnl.Controls.Add(btnReject);
            pnl.Controls.Add(btnApprove);
        }

        private void BuildApprovedCampaignPanel(Panel pnl, int w)
        {
            var lblSec = MakeLabel("AUTOMATED EMAIL CAMPAIGN", 0, 0);
            lblSec.Font = AppTheme.FontSection;
            lblSec.ForeColor = AppTheme.Success;
            pnl.Controls.Add(lblSec);

            var lblDesc = new Label
            {
                Text = "This retention request was automatically added to Email Campaigns upon Admin approval.",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                Location = new Point(0, 24),
                Size = new Size(w, 20)
            };
            pnl.Controls.Add(lblDesc);

            btnDispatch = MakePrimaryButton("Dispatch Retention Email Now", 0, 52, 240);
            btnDispatch.Click += async (s, e) => await DispatchEmailAsync();
            pnl.Controls.Add(btnDispatch);

            lblDispatchStatus = new Label
            {
                Text = _request.IsDispatched
                    ? "Retention email has been dispatched via SMTP."
                    : "Ready for dispatch. Click above to send immediately via SMTP.",
                Font = AppTheme.FontSubtitle,
                ForeColor = _request.IsDispatched ? AppTheme.Success : AppTheme.TextSecondary,
                Location = new Point(250, 58),
                Size = new Size(w - 260, 30)
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
                BackColor = Color.White,
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
                MessageBox.Show("Reason for rejection is mandatory.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtRejectionReason?.Focus();
                return;
            }

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

        private Button MakePrimaryButton(string text, int x, int y, int width)
        {
            var b = MakePrimaryButton(text);
            b.Location = new Point(x, y);
            b.Size = new Size(width, 36);
            return b;
        }

        private Button MakeSecondaryButton(string text, int x, int y, int width)
        {
            var b = MakeSecondaryButton(text);
            b.Location = new Point(x, y);
            b.Size = new Size(width, 36);
            return b;
        }
    }
}
