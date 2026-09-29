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