namespace CRM_ComputerRepair.domain.Entities;

public class Customer
{
    public int CustomerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? StateOrProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; } = "Philippines";
    public int? LoyaltyPoints { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Helper properties
    public string FullName => $"{FirstName} {LastName}".Trim();

    public string FullAddress
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Address)) parts.Add(Address.Trim());
            if (!string.IsNullOrWhiteSpace(City)) parts.Add(City.Trim());
            if (!string.IsNullOrWhiteSpace(StateOrProvince)) parts.Add(StateOrProvince.Trim());
            if (!string.IsNullOrWhiteSpace(PostalCode)) parts.Add(PostalCode.Trim());
            if (!string.IsNullOrWhiteSpace(Country)) parts.Add(Country.Trim());
            return parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
        }
    }

    // Navigation
    public ICollection<RepairRequest> RepairRequests { get; set; } = new List<RepairRequest>();
    public ICollection<CustomerInteraction> CustomerInteractions { get; set; } = new List<CustomerInteraction>();
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}