namespace CRM_ComputerRepair.domain.Entities;

public class Subscription
{
    public int SubscriptionId { get; set; }
    public int? CompanyId { get; set; }
    public string SubscriptionName { get; set; } = string.Empty;
    public decimal PricePerMonth { get; set; }
    public int DurationMonths { get; set; } = 1;
    public string? Duration { get; set; } = "1 Month";
    public int MaxUsers { get; set; } = 5;
    public int MaxDevices { get; set; } = 100;
    public bool EnableMultiBranching { get; set; } = false;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime EndDate { get; set; } = DateTime.UtcNow.AddMonths(1);
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; } = false;
    public string? BillingCycle { get; set; } = "Monthly";
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public int? SubscriptionPlanId { get; set; }
    public int? PlanId
    {
        get => SubscriptionPlanId;
        set => SubscriptionPlanId = value;
    }

    // Navigation
    public Company? Company { get; set; }
    public SubscriptionPlan? Plan { get; set; }
    public ICollection<CompanySubscriptionModule> SubscriptionModules { get; set; } = new List<CompanySubscriptionModule>();
}