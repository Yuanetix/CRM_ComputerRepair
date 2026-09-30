using System;

namespace CRM.winforms
{
    public class RetentionRecommendationDto
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string Category { get; set; } = "";
        public string Action { get; set; } = "";
        public string Basis { get; set; } = "";
        public int? LoyaltyProgramId { get; set; }
        public string? ProgramName { get; set; }
        public string? Reward { get; set; }
        public int TransactionCount { get; set; }
        public decimal TotalSpent { get; set; }
        public int DaysSinceLastTransaction { get; set; }
        public int Points { get; set; }

        public int Segment { get; set; }
        public string SegmentName { get; set; } = "";
        public bool InCooldown { get; set; }
        public int? DaysUntilNextEligible { get; set; }
        public DateTime? LastEmailSentDate { get; set; }

        public string CategoryDisplay => Category switch
        {
            "Discount" => "Discount",
            "Reward" => "Loyalty reward",
            "Follow-up" => "Follow-up",
            "Re-engagement" => "Re-engagement",
            _ => Category
        };
        public string LastVisitDisplay => $"{DaysSinceLastTransaction} days ago";
        public string SpentDisplay => $"\u20b1{TotalSpent:N2}";
    }

    public class CustomerRetentionMetricsDto
    {
        public int CustomerId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int? LoyaltyPoints { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }

        public int CompletedTransactions { get; set; }
        public decimal TotalSpent { get; set; }
        public DateTime? LastTransactionDate { get; set; }
        public int DaysSinceLastTransaction { get; set; }
        public int ActiveMonths { get; set; }
        public int TransactionsLast90Days { get; set; }
        public string PreviousServices { get; set; } = "";
        public string? LastService { get; set; }

        public string FullName => $"{FirstName} {LastName}".Trim();
        public int SegmentEnum { get; set; }
        public string Segment { get; set; } = "New";

        public bool InCooldown { get; set; }
        public DateTime? LastEmailSentDate { get; set; }
        public int? DaysUntilNextEligible { get; set; }
    }

    public class RetentionRequestDto
    {
        public int RetentionRequestId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        public int TargetSegment { get; set; }
        public string TargetSegmentName { get; set; } = "";
        public string ActionType { get; set; } = "";
        public decimal ProposedDiscountPercent { get; set; }
        public string RetentionDetails { get; set; } = "";
        public string ReasonCategory { get; set; } = "";
        public string? ReasonNote { get; set; }
        public int Status { get; set; } // 0 Pending, 1 Approved, 2 Rejected
        public string StatusText => Status switch
        {
            0 => "Pending",
            1 => "Approved",
            2 => "Rejected",
            _ => "Unknown"
        };
        public string SubmittedByUserId { get; set; } = "";
        public string SubmittedByName { get; set; } = "";
        public DateTime SubmittedAt { get; set; }
        public string? ReviewedByUserId { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewRemarks { get; set; }
        public string? RejectionReason { get; set; }
        public bool AddedToCampaign { get; set; }
        public DateTime? CampaignAddedAt { get; set; }
        public int? CampaignEmailLogId { get; set; }
        public bool IsDispatched { get; set; }
    }

    public class CreateRetentionRequestDto
    {
        public int CustomerId { get; set; }
        public int TargetSegment { get; set; }
        public string ActionType { get; set; } = "Discount";
        public decimal ProposedDiscountPercent { get; set; } = 10m;
        public string RetentionDetails { get; set; } = "";
        public string ReasonCategory { get; set; } = "Improve Customer Retention";
        public string? ReasonNote { get; set; }
    }

    public class RetentionCampaignDto
    {
        public int RetentionEmailLogId { get; set; }
        public int? RetentionRequestId { get; set; }
        public int CustomerId { get; set; }
        public string RecipientName { get; set; } = "";
        public string RecipientEmail { get; set; } = "";
        public string Subject { get; set; } = "";
        public string FormattedBody { get; set; } = "";
        public int Segment { get; set; }
        public string SegmentName { get; set; } = "";
        public decimal DiscountPercent { get; set; }
        public string? PromoCode { get; set; }
        public DateTime? ValidUntil { get; set; }
        public bool IsDispatched { get; set; }
        public DateTime? DispatchedAt { get; set; }
        public string? DispatchedByUserId { get; set; }
        public bool IsAutomated { get; set; }
        public DateTime CreatedAt { get; set; }
        public string DeliveryStatus { get; set; } = "Pending";
        public string? DeliveryError { get; set; }
    }

    public class DispatchResultDto
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string DeliveryStatus { get; set; } = "Sent";
        public string? OutboxFilePath { get; set; }
        public bool WasFallback { get; set; }
    }

    public class ManualSendResultDto
    {
        public bool Success { get; set; }
        public bool InCooldown { get; set; }
        public string? Message { get; set; }
        public string? OutboxFilePath { get; set; }
        public bool WasFallback { get; set; }
        public string DeliveryStatus { get; set; } = "Sent";
    }

    public class SendManualRetentionEmailRequestDto
    {
        public int CustomerId { get; set; }
        public string? RecipientEmail { get; set; }
        public int Segment { get; set; }
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public decimal DiscountPercent { get; set; } = 10m;
        public string? PromoCode { get; set; }
        public int ValidityDays { get; set; } = 14;
        public bool OverrideCooldown { get; set; } = false;
    }

    public class TestSmtpSettingsRequestDto
    {
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? FromEmail { get; set; }
        public string? FromName { get; set; }
        public bool? EnableSsl { get; set; }
        public string? TestRecipientEmail { get; set; }
    }

    public class RetentionTemplateDto
    {
        public int RetentionEmailTemplateId { get; set; }
        public int Segment { get; set; }
        public string SegmentName { get; set; } = "";
        public string TemplateName { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public decimal DefaultDiscountPercent { get; set; }
        public int ValidityDays { get; set; }
        public bool IsActive { get; set; }
    }

    public class UpdateRetentionTemplateRequestDto
    {
        public string TemplateName { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public decimal DefaultDiscountPercent { get; set; }
        public int ValidityDays { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class RetentionSettingsDto
    {
        public int InactiveThresholdDays { get; set; } = 180;
        public int AtRiskThresholdDays { get; set; } = 90;
        public int AntiFatigueDays { get; set; } = 14;
        public int DefaultOfferValidityDays { get; set; } = 14;
        public string? SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public string? SmtpUsername { get; set; }
        public string? SmtpPassword { get; set; }
        public string? SmtpFromEmail { get; set; }
        public string? SmtpFromName { get; set; }
        public bool SmtpEnableSsl { get; set; }
    }

    public class UpdateRetentionSettingsRequestDto
    {
        public int InactiveThresholdDays { get; set; } = 180;
        public int AtRiskThresholdDays { get; set; } = 90;
        public int AntiFatigueDays { get; set; } = 14;
        public int DefaultOfferValidityDays { get; set; } = 14;
        public string? SmtpHost { get; set; }
        public int SmtpPort { get; set; } = 25;
        public string? SmtpUsername { get; set; }
        public string? SmtpPassword { get; set; }
        public string? SmtpFromEmail { get; set; }
        public string? SmtpFromName { get; set; }
        public bool SmtpEnableSsl { get; set; }
    }

    public class RetentionCandidateDto
    {
        public int CustomerId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int? LoyaltyPoints { get; set; }
        public DateTime CreatedAt { get; set; }

        public string FullName => $"{FirstName} {LastName}".Trim();
        public string DisplayName => FullName;
        public int InactiveDays => (int)(DateTime.UtcNow - CreatedAt).TotalDays;
        public string LastContactDisplay => CreatedAt.ToString("MMM d, yyyy");
    }
}
