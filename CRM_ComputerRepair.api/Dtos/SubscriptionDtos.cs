using System.ComponentModel.DataAnnotations;

namespace CRM_ComputerRepair.api.Dtos;

public class CreateSubscriptionRequest
{
    [Required(ErrorMessage = "Subscription name is required.")]
    [MaxLength(200)]
    public string SubscriptionName { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "Price must be non-negative.")]
    public decimal PricePerMonth { get; set; }

    [Range(1, 120, ErrorMessage = "Duration in months must be between 1 and 120.")]
    public int DurationMonths { get; set; } = 1;

    [MaxLength(100)]
    public string? Duration { get; set; } = "1 Month";

    [Range(1, int.MaxValue, ErrorMessage = "Max users must be at least 1.")]
    public int MaxUsers { get; set; } = 5;

    [Range(1, int.MaxValue, ErrorMessage = "Max devices must be at least 1.")]
    public int MaxDevices { get; set; } = 100;

    public bool EnableMultiBranching { get; set; } = false;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? BillingCycle { get; set; } = "Monthly";
}

public class UpdateSubscriptionRequest
{
    [Required(ErrorMessage = "Subscription name is required.")]
    [MaxLength(200)]
    public string SubscriptionName { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal PricePerMonth { get; set; }

    [Range(1, 120)]
    public int DurationMonths { get; set; } = 1;

    [MaxLength(100)]
    public string? Duration { get; set; }

    [Range(1, int.MaxValue)]
    public int MaxUsers { get; set; }

    [Range(1, int.MaxValue)]
    public int MaxDevices { get; set; }

    public bool EnableMultiBranching { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public bool IsArchived { get; set; }

    [MaxLength(50)]
    public string? BillingCycle { get; set; }
}

public class SubscriptionPlanDetailDto
{
    public int SubscriptionId { get; set; }
    public int? CompanyId { get; set; }
    public string SubscriptionName { get; set; } = string.Empty;
    public decimal PricePerMonth { get; set; }
    public int DurationMonths { get; set; } = 1;
    public string Duration { get; set; } = "1 Month";
    public int MaxUsers { get; set; }
    public int MaxDevices { get; set; }
    public bool EnableMultiBranching { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }
    public string? BillingCycle { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int SubscribedCompaniesCount { get; set; }
}

public class AppModuleDto
{
    public int ModuleId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PricePerMonth { get; set; }
    public decimal DefaultMonthlyPrice => PricePerMonth;
    public bool IsActive { get; set; }
}

public class UpdateModulePriceRequest
{
    [Range(0, double.MaxValue, ErrorMessage = "Price must be non-negative.")]
    public decimal PricePerMonth { get; set; }
}

public class SubscriptionPlanDto
{
    public int PlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal PricePerMonth => Price;
    public string BillingInterval { get; set; } = "Monthly";
    public string BillingCycle => BillingInterval;
    public string Status { get; set; } = "Active";
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; } = false;
    public int MaxUsers { get; set; } = 10;
    public int MaxBranches { get; set; } = 1;
    public int MaxDevices { get; set; } = 500;
    public DateTime CreatedAt { get; set; }
    public List<AppModuleDto> IncludedModules { get; set; } = new();
    public int SubscribedCompaniesCount { get; set; }

    public string PriceDisplay => $"₱{Price:N2}/mo";
    public string ModulesSummary => IncludedModules.Count > 0
        ? string.Join(", ", IncludedModules.ConvertAll(m => m.ModuleName))
        : "None";
}

public class CreatePlanRequest
{
    [Required]
    [MaxLength(50)]
    public string PlanCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string PlanName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(0, 1000000)]
    public decimal Price { get; set; }

    public string BillingInterval { get; set; } = "Monthly";
    public int MaxUsers { get; set; } = 10;
    public int MaxBranches { get; set; } = 1;
    public int MaxDevices { get; set; } = 500;

    public List<string> ModuleCodes { get; set; } = new();
}

public class UpdatePlanRequest
{
    [Required]
    [MaxLength(200)]
    public string PlanName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(0, 1000000)]
    public decimal Price { get; set; }

    public string BillingInterval { get; set; } = "Monthly";
    public string Status { get; set; } = "Active";
    public int MaxUsers { get; set; } = 10;
    public int MaxBranches { get; set; } = 1;
    public int MaxDevices { get; set; } = 500;

    public List<string> ModuleCodes { get; set; } = new();
}

public class CompanySubscribedModuleDto
{
    public int CompanySubscriptionModuleId { get; set; }
    public int ModuleId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public bool IsActive { get; set; }
    public bool IsAddon { get; set; }
    public DateTime SubscribedAt { get; set; }
    public string PriceDisplay => $"₱{MonthlyPrice:N2}/mo";
}

public class CompanySubscriptionDetailDto
{
    public int CompanyId { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public int? SubscriptionId { get; set; }

    // Plan info
    public int? PlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public decimal PlanPrice { get; set; }
    public string SubscriptionName { get; set; } = string.Empty;

    public string Status { get; set; } = "Active";
    public bool IsActive { get; set; } = true;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BillingCycle { get; set; } = "Monthly";

    // Module breakdown
    public List<AppModuleDto> PlanModules { get; set; } = new();
    public List<CompanySubscribedModuleDto> ActiveAddons { get; set; } = new();
    public List<string> EffectiveModules { get; set; } = new();

    public decimal AddonTotal { get; set; }
    public decimal MonthlyTotal { get; set; }

    // Legacy compatibility properties
    public List<CompanySubscribedModuleDto> SubscribedModules { get; set; } = new();
    public List<AppModuleDto> AvailableModules { get; set; } = new();

    public string TotalDisplay => $"₱{MonthlyTotal:N2}/mo";
    public string DatesDisplay => $"{StartDate:MMM dd, yyyy} – {EndDate:MMM dd, yyyy}";
    public string StatusDisplay => IsActive ? (Status ?? "Active") : "Inactive";
    public string ModulesSummaryDisplay => EffectiveModules.Count > 0
        ? string.Join(", ", EffectiveModules)
        : "None";
}

public class ChangeCompanyPlanRequest
{
    public string? NewPlanCode { get; set; }
    public int? NewPlanId { get; set; }
    public string? Reason { get; set; }
}

public class AddModuleAddonRequest
{
    [Required]
    public string ModuleCode { get; set; } = string.Empty;
}

public class AddModuleToCompanyRequest
{
    [Required]
    public string ModuleCode { get; set; } = string.Empty;
}

public class UpdateCompanySubscriptionRequest
{
    public string? PlanCode { get; set; }
    public int? PlanId { get; set; }
    public List<string>? AddonCodes { get; set; }
    public List<string>? ModuleCodes { get; set; }
    public bool? IsActive { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? BillingCycle { get; set; }
}

public class SubscriptionHistoryDto
{
    public int SubscriptionHistoryId { get; set; }
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? PreviousPlanName { get; set; }
    public string? NewPlanName { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public decimal PreviousPrice { get; set; }
    public decimal NewPrice { get; set; }
    public string? Notes { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}