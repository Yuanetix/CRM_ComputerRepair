using System.ComponentModel.DataAnnotations;
using CRM_ComputerRepair.domain.Entities;

namespace CRM_ComputerRepair.api.Dtos;

public class CreateLoyaltyProgramRequest
{
    [Required(ErrorMessage = "Program name is required.")]
    [MaxLength(200)]
    public string ProgramName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int PointsPerPeso { get; set; } = 1;

    [Range(0, 100)]
    public decimal DiscountPercentage { get; set; }

    [Range(0, 1_000_000_000)]
    public decimal MinimumSpend { get; set; }

    // Validity
    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Range(1, 3650)]
    public int? PointsValidityDays { get; set; }

    [Range(1, 100_000_000)]
    public int? RedeemPointsRequired { get; set; }

    // Eligibility criteria
    [Range(1, 100_000)]
    public int? MinTransactions { get; set; }

    [Range(1, 1_000_000_000)]
    public decimal? MinTotalSpent { get; set; }

    [Range(1, 3650)]
    public int? MaxInactiveDays { get; set; }

    [Range(1, 100_000)]
    public int? MinVisitsPerPeriod { get; set; }

    [Range(1, 3650)]
    public int? VisitPeriodDays { get; set; }

    // Reward
    public LoyaltyRewardType RewardType { get; set; } = LoyaltyRewardType.DiscountPercent;

    [Range(0, 1_000_000_000)]
    public decimal RewardValue { get; set; }

    [Range(1, 100_000)]
    public int? MaxRedemptionsPerCustomer { get; set; }
}

public class UpdateLoyaltyProgramRequest
{
    [Required]
    [MaxLength(200)]
    public string ProgramName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int PointsPerPeso { get; set; }

    [Range(0, 100)]
    public decimal DiscountPercentage { get; set; }

    [Range(0, 1_000_000_000)]
    public decimal MinimumSpend { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Range(1, 3650)]
    public int? PointsValidityDays { get; set; }

    [Range(1, 100_000_000)]
    public int? RedeemPointsRequired { get; set; }

    [Range(1, 100_000)]
    public int? MinTransactions { get; set; }

    [Range(1, 1_000_000_000)]
    public decimal? MinTotalSpent { get; set; }

    [Range(1, 3650)]
    public int? MaxInactiveDays { get; set; }

    [Range(1, 100_000)]
    public int? MinVisitsPerPeriod { get; set; }

    [Range(1, 3650)]
    public int? VisitPeriodDays { get; set; }

    public LoyaltyRewardType RewardType { get; set; } = LoyaltyRewardType.DiscountPercent;

    [Range(0, 1_000_000_000)]
    public decimal RewardValue { get; set; }

    [Range(1, 100_000)]
    public int? MaxRedemptionsPerCustomer { get; set; }

    public bool IsActive { get; set; }
}

// ═══════════ Membership ═══════════

public class LoyaltyMemberDto
{
    public int CustomerLoyaltyAccountId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int Points { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime JoinedDate { get; set; }
    public bool IsActive { get; set; }
}

public class EnrollCustomerRequest
{
    [Required(ErrorMessage = "Customer is required.")]
    public int CustomerId { get; set; }

    [Range(0, 100_000_000)]
    public int InitialPoints { get; set; }
}

public class AdjustPointsRequest
{
    [Required(ErrorMessage = "Point change is required.")]
    public int PointsChange { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}