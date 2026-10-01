namespace CRM_ComputerRepair.domain.Entities;

public static class ModuleCodes
{
    public const string MainTransactions = "MAIN_TRANSACTIONS";
    public const string DataCollection = "DATA_COLLECTION";
    public const string BusinessIntelligence = "BUSINESS_INTELLIGENCE";
    public const string Actions = "ACTIONS";
    public const string Retention = "ACTIONS";
    public const string Branching = "BRANCHING";

    public static readonly string[] All =
    {
        MainTransactions,
        DataCollection,
        BusinessIntelligence,
        Actions,
        Branching
    };
}

public class AppModule
{
    public int ModuleId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PricePerMonth { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<CompanySubscriptionModule> SubscriptionModules { get; set; } = new List<CompanySubscriptionModule>();
    public ICollection<PlanModule> PlanModules { get; set; } = new List<PlanModule>();
}
