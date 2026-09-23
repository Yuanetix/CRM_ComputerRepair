using System.ComponentModel.DataAnnotations;

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

    public string Segment => IsActive switch
    {
        _ when CompletedTransactions == 0 => "Never purchased",
        _ when DaysSinceLastTransaction >= 90 => "Inactive",
        _ when CompletedTransactions >= 2 => "Returning",
        _ => "Active"
    };
}