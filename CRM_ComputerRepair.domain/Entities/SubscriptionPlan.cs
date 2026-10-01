namespace CRM_ComputerRepair.domain.Entities;

public static class PlanCodes
{
    public const string Enterprise = "ENTERPRISE";
    public const string Operations = "OPERATIONS";
    public const string Intelligence = "INTELLIGENCE";
    public const string Branch = "BRANCH";

    public static readonly string[] All =
    {
        Enterprise,
        Operations,
        Intelligence,
        Branch
    };
}

public class SubscriptionPlan
{
    public int SubscriptionPlanId { get; set; }
    public int PlanId
    {
        get => SubscriptionPlanId;
        set => SubscriptionPlanId = value;
    }

    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public decimal PricePerMonth { get; set; }
    public decimal Price
    {
        get => PricePerMonth;
        set => PricePerMonth = value;
    }

    public string BillingCycle { get; set; } = "Monthly";
    public string BillingInterval
    {
        get => BillingCycle;
        set => BillingCycle = value;
    }

    public string Status { get; set; } = "Active";
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; } = false;

    // Configurable limits
    public int MaxUsers { get; set; } = 10;
    public int MaxBranches { get; set; } = 1;
    public int MaxDevices { get; set; } = 500;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<PlanModule> PlanModules { get; set; } = new List<PlanModule>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
