using System.ComponentModel.DataAnnotations;
using CRM_ComputerRepair.domain.Entities;

namespace CRM_ComputerRepair.api.Dtos;

public class RetentionContactRequest
{
    [Required(ErrorMessage = "Subject is required.")]
    [MaxLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Notes are required.")]
    [MaxLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters.")]
    public string Notes { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? PerformedByUserId { get; set; }

    [Range(0, 365, ErrorMessage = "Follow-up days must be between 0 and 365.")]
    public int? ScheduleFollowUpInDays { get; set; }

    [MaxLength(2000)]
    public string? Basis { get; set; }

    [MaxLength(200)]
    public string? Category { get; set; }

    public int? LoyaltyProgramId { get; set; }
}

public class RetentionRecommendationDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }

    // Segment info
    public RetentionSegment Segment { get; set; }
    public string SegmentName { get; set; } = string.Empty;

    // Reward / Discount / Re-engagement / Follow-up
    public string Category { get; set; } = "";
    public string Action { get; set; } = "";
    public string Basis { get; set; } = "";

    public int? LoyaltyProgramId { get; set; }
    public string? ProgramName { get; set; }
    public string? Reward { get; set; }

    // Context used to build the basis
    public int TransactionCount { get; set; }
    public decimal TotalSpent { get; set; }
    public int DaysSinceLastTransaction { get; set; }
    public int Points { get; set; }

    // Anti-fatigue check
    public bool InCooldown { get; set; }
    public int? DaysUntilNextEligible { get; set; }
    public DateTime? LastEmailSentDate { get; set; }
}

public class RevenueFilter
{
    public string? Category { get; set; }
    public string? Search { get; set; }
    public decimal MinSpent { get; set; }
}

public class CustomerRetentionMetricsDto
{
    public int CustomerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
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

    public RetentionSegment SegmentEnum { get; set; }
    public string Segment => SegmentEnum switch
    {
        RetentionSegment.Inactive => "Inactive",
        RetentionSegment.AtRisk => "At Risk",
        RetentionSegment.Loyal => "Loyal",
        RetentionSegment.Returning => "Returning",
        RetentionSegment.New => "New",
        _ => "New"
    };

    // Cooldown
    public bool InCooldown { get; set; }
    public DateTime? LastEmailSentDate { get; set; }
    public int? DaysUntilNextEligible { get; set; }
}

// ═══════════════════════════════════════════════════════
// RETENTION REQUESTS & APPROVALS
// ═══════════════════════════════════════════════════════

public class RetentionRequestDto
{
    public int RetentionRequestId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }

    public RetentionSegment TargetSegment { get; set; }
    public string TargetSegmentName => TargetSegment.ToString();

    public string ActionType { get; set; } = string.Empty;
    public decimal ProposedDiscountPercent { get; set; }
    public string RetentionDetails { get; set; } = string.Empty;

    public string ReasonCategory { get; set; } = string.Empty;
    public string? ReasonNote { get; set; }

    public RetentionRequestStatus Status { get; set; }
    public string StatusText => Status.ToString();

    public string SubmittedByUserId { get; set; } = string.Empty;
    public string SubmittedByName { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }

    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewRemarks { get; set; }
    public string? RejectionReason { get; set; }

    public bool AddedToCampaign { get; set; }
    public DateTime? CampaignAddedAt { get; set; }

    // Associated campaign ID if generated
    public int? CampaignEmailLogId { get; set; }
    public bool IsDispatched { get; set; }
}

public class CreateRetentionRequest
{
    [Required]
    public int CustomerId { get; set; }

    public RetentionSegment TargetSegment { get; set; }

    [Required]
    [MaxLength(100)]
    public string ActionType { get; set; } = "Discount";

    [Range(0, 100, ErrorMessage = "Discount must be between 0 and 100%.")]
    public decimal ProposedDiscountPercent { get; set; } = 10m;

    [Required(ErrorMessage = "Retention details are required.")]
    [MaxLength(2000)]
    public string RetentionDetails { get; set; } = string.Empty;

    [Required(ErrorMessage = "Reason category is required.")]
    [MaxLength(150)]
    public string ReasonCategory { get; set; } = "Improve Customer Retention";

    [MaxLength(2000)]
    public string? ReasonNote { get; set; }
}

public class ApproveRetentionRequest
{
    [MaxLength(2000)]
    public string? ReviewRemarks { get; set; }
}

public class RejectRetentionRequest
{
    [Required(ErrorMessage = "Reason for rejection is mandatory.")]
    [MaxLength(2000)]
    public string RejectionReason { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ReviewRemarks { get; set; }
}

// ═══════════════════════════════════════════════════════
// EMAIL CAMPAIGNS & LOGS
// ═══════════════════════════════════════════════════════

public class RetentionCampaignDto
{
    public int RetentionEmailLogId { get; set; }
    public int? RetentionRequestId { get; set; }
    public int CustomerId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;
    public string FormattedBody { get; set; } = string.Empty;

    public RetentionSegment Segment { get; set; }
    public string SegmentName => Segment.ToString();

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

public class DispatchRetentionEmailRequest
{
    [MaxLength(300)]
    public string? CustomSubject { get; set; }

    public string? CustomBody { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? CustomRecipientEmail { get; set; }
}

public class SendManualRetentionEmailRequest
{
    [Required]
    public int CustomerId { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? RecipientEmail { get; set; }

    [Required]
    public RetentionSegment Segment { get; set; }

    [Required]
    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    [Range(0, 100)]
    public decimal DiscountPercent { get; set; } = 10m;

    [MaxLength(50)]
    public string? PromoCode { get; set; }

    [Range(1, 365)]
    public int ValidityDays { get; set; } = 14;

    public bool OverrideCooldown { get; set; } = false;
}

public class TestSmtpRequest
{
    [MaxLength(200)]
    public string? Host { get; set; }

    public int? Port { get; set; }

    [MaxLength(200)]
    public string? Username { get; set; }

    [MaxLength(200)]
    public string? Password { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? FromEmail { get; set; }

    [MaxLength(200)]
    public string? FromName { get; set; }

    public bool? EnableSsl { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? TestRecipientEmail { get; set; }
}

// ═══════════════════════════════════════════════════════
// TEMPLATES & SETTINGS
// ═══════════════════════════════════════════════════════

public class RetentionTemplateDto
{
    public int RetentionEmailTemplateId { get; set; }
    public RetentionSegment Segment { get; set; }
    public string SegmentName => Segment.ToString();
    public string TemplateName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public decimal DefaultDiscountPercent { get; set; }
    public int ValidityDays { get; set; }
    public bool IsActive { get; set; }
}

public class UpdateRetentionTemplateRequest
{
    [Required]
    [MaxLength(150)]
    public string TemplateName { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    [Range(0, 100)]
    public decimal DefaultDiscountPercent { get; set; }

    [Range(1, 365)]
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

public class UpdateRetentionSettingsRequest
{
    [Range(1, 365)]
    public int InactiveThresholdDays { get; set; } = 180;

    [Range(1, 365)]
    public int AtRiskThresholdDays { get; set; } = 90;

    [Range(1, 90)]
    public int AntiFatigueDays { get; set; } = 14;

    [Range(1, 90)]
    public int DefaultOfferValidityDays { get; set; } = 14;

    [MaxLength(200)]
    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 25;

    [MaxLength(200)]
    public string? SmtpUsername { get; set; }

    [MaxLength(200)]
    public string? SmtpPassword { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? SmtpFromEmail { get; set; }

    [MaxLength(200)]
    public string? SmtpFromName { get; set; }

    public bool SmtpEnableSsl { get; set; } = false;
}