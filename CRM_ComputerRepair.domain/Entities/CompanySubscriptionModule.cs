namespace CRM_ComputerRepair.domain.Entities;

public class CompanySubscriptionModule
{
    public int CompanySubscriptionModuleId { get; set; }
    public int SubscriptionId { get; set; }
    public int ModuleId { get; set; }
    public decimal MonthlyPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsAddon { get; set; } = false;
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Subscription? Subscription { get; set; }
    public AppModule? Module { get; set; }
}
