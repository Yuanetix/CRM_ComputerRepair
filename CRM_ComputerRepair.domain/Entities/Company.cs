namespace CRM_ComputerRepair.domain.Entities;

public class Company
{
    public int CompanyId { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactFirstName { get; set; }
    public string? ContactLastName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? StateOrProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; } = "Philippines";
    public int? SubscriptionId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public string? ContactPerson =>
        string.IsNullOrWhiteSpace($"{ContactFirstName} {ContactLastName}") ? null : $"{ContactFirstName} {ContactLastName}".Trim();

    // Navigation properties
    public Subscription? Subscription { get; set; }
    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<LoyaltyProgram> LoyaltyPrograms { get; set; } = new List<LoyaltyProgram>();
    public ICollection<CompanyDatabase> CompanyDatabases { get; set; } = new List<CompanyDatabase>();
}