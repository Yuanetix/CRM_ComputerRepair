using System.ComponentModel.DataAnnotations;

namespace CRM_ComputerRepair.api.Dtos;

public class CreateSubscriptionRequest
{
    [Required(ErrorMessage = "Subscription name is required.")]
    [MaxLength(200)]
    public string SubscriptionName { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal PricePerMonth { get; set; }

    [Range(1, int.MaxValue)]
    public int MaxUsers { get; set; }

    [Range(1, int.MaxValue)]
    public int MaxDevices { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [MaxLength(50)]
    public string? BillingCycle { get; set; } = "Monthly";
}

public class UpdateSubscriptionRequest
{
    [Required]
    [MaxLength(200)]
    public string SubscriptionName { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal PricePerMonth { get; set; }

    [Range(1, int.MaxValue)]
    public int MaxUsers { get; set; }

    [Range(1, int.MaxValue)]
    public int MaxDevices { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; }

    [MaxLength(50)]
    public string? BillingCycle { get; set; }
}