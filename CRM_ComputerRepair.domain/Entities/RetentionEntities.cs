namespace CRM_ComputerRepair.domain.Entities;

public enum RetentionSegment
{
    New = 0,
    Returning = 1,
    Loyal = 2,
    AtRisk = 3,
    Inactive = 4
}

public enum RetentionRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public class RetentionRequest
{
    public int RetentionRequestId { get; set; }
    public int CustomerId { get; set; }

    public RetentionSegment TargetSegment { get; set; }
    public string ActionType { get; set; } = "Discount";
    public decimal ProposedDiscountPercent { get; set; } = 10m;
    public string RetentionDetails { get; set; } = string.Empty;

    // Reason category + specific notes
    public string ReasonCategory { get; set; } = "Improve Customer Retention";
    public string? ReasonNote { get; set; }

    public RetentionRequestStatus Status { get; set; } = RetentionRequestStatus.Pending;

    // Submitter
    public string SubmittedByUserId { get; set; } = string.Empty;
    public string SubmittedByName { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    // Reviewer
    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewRemarks { get; set; }
    public string? RejectionReason { get; set; }

    // Campaign linkage
    public bool AddedToCampaign { get; set; } = false;
    public DateTime? CampaignAddedAt { get; set; }

    // Navigation
    public Customer? Customer { get; set; }
    public ICollection<RetentionEmailLog> EmailLogs { get; set; } = new List<RetentionEmailLog>();
}

public class RetentionEmailLog
{
    public int RetentionEmailLogId { get; set; }
    public int? RetentionRequestId { get; set; }
    public int CustomerId { get; set; }

    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;
    public string FormattedBody { get; set; } = string.Empty;

    public RetentionSegment Segment { get; set; }
    public decimal DiscountPercent { get; set; }
    public string? PromoCode { get; set; }
    public DateTime? ValidUntil { get; set; }

    public bool IsDispatched { get; set; } = false;
    public DateTime? DispatchedAt { get; set; }
    public string? DispatchedByUserId { get; set; }
    public bool IsAutomated { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Delivery tracking
    public string DeliveryStatus { get; set; } = "Pending"; // Pending, Sent, Failed, Simulated
    public string? DeliveryError { get; set; }

    // Navigation
    public Customer? Customer { get; set; }
    public RetentionRequest? RetentionRequest { get; set; }
}

public class RetentionEmailTemplate
{
    public int RetentionEmailTemplateId { get; set; }
    public RetentionSegment Segment { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public decimal DefaultDiscountPercent { get; set; } = 10m;
    public int ValidityDays { get; set; } = 14;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class RetentionSettings
{
    public int RetentionSettingsId { get; set; }
    public int InactiveThresholdDays { get; set; } = 180;
    public int AtRiskThresholdDays { get; set; } = 90;
    public int AntiFatigueDays { get; set; } = 14;
    public int DefaultOfferValidityDays { get; set; } = 14;

    // SMTP Configuration
    public string? SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 25;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? SmtpFromEmail { get; set; } = "retention@fixorycrm.local";
    public string? SmtpFromName { get; set; } = "Fixory Computer Repair Services";
    public bool SmtpEnableSsl { get; set; } = false;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
