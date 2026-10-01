namespace CRM_ComputerRepair.domain.Entities;

public static class SubscriptionChangeTypes
{
    public const string InitialSetup = "INITIAL_SETUP";
    public const string Upgrade = "UPGRADE";
    public const string Downgrade = "DOWNGRADE";
    public const string AddonAdded = "ADDON_ADDED";
    public const string AddonRemoved = "ADDON_REMOVED";
    public const string StatusChange = "STATUS_CHANGE";
    public const string PlanModified = "PLAN_MODIFIED";
}

public class SubscriptionHistory
{
    public int SubscriptionHistoryId { get; set; }
    public int CompanyId { get; set; }
    public string? PreviousPlanName { get; set; }
    public string? NewPlanName { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public decimal PreviousPrice { get; set; }
    public decimal NewPrice { get; set; }
    public string? Notes { get; set; }
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
    public string ChangedBy { get; set; } = "Super Admin";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation
    public Company? Company { get; set; }
}
