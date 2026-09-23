namespace CRM_ComputerRepair.domain.Entities;

/// <summary>
/// The kind of reward a loyalty program grants when eligibility is met.
/// </summary>
public enum LoyaltyRewardType
{
    DiscountPercent,
    FreeService,
    PointsMultiplier,
    Voucher
}

public class LoyaltyProgram
{
    public int LoyaltyProgramId { get; set; }
    public int? CompanyId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // ─── Points / discount economics ───
    public int PointsPerPeso { get; set; } = 1;
    public decimal DiscountPercentage { get; set; } = 0;
    public decimal MinimumSpend { get; set; } = 0;

    // ─── Validity periods ───
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? PointsValidityDays { get; set; }
    public int? RedeemPointsRequired { get; set; }

    // ─── Eligibility criteria (analyzed against real customer history) ───
    public int? MinTransactions { get; set; }
    public decimal? MinTotalSpent { get; set; }
    public int? MaxInactiveDays { get; set; }
    public int? MinVisitsPerPeriod { get; set; }
    public int? VisitPeriodDays { get; set; }

    // ─── Reward definition ───
    public LoyaltyRewardType RewardType { get; set; } = LoyaltyRewardType.DiscountPercent;
    public decimal RewardValue { get; set; } = 0;
    public int? MaxRedemptionsPerCustomer { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Company? Company { get; set; }
}