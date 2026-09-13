namespace CRM_ComputerRepair.domain.Entities;

public class Subscription
{
    public int SubscriptionId { get; set; }
    public int? CompanyId { get; set; }
    public string SubscriptionName { get; set; } = string.Empty;
    public decimal PricePerMonth { get; set; }
    public int MaxUsers { get; set; }
    public int MaxDevices { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? BillingCycle { get; set; } = "Monthly";

    // Navigation
    public Company? Company { get; set; }
}