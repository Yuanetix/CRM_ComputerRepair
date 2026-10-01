namespace CRM_ComputerRepair.domain.Entities;

public class PlanModule
{
    public int PlanModuleId { get; set; }
    public int SubscriptionPlanId { get; set; }
    public int PlanId
    {
        get => SubscriptionPlanId;
        set => SubscriptionPlanId = value;
    }

    public int ModuleId { get; set; }

    // Navigation
    public SubscriptionPlan? Plan { get; set; }
    public AppModule? Module { get; set; }
}
