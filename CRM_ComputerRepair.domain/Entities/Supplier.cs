namespace CRM_ComputerRepair.domain.Entities;

public class Supplier
{
    public int SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? ContactFirstName { get; set; }
    public string? ContactLastName { get; set; }
    public string? ContactNumber { get; set; }
    public string? EmailAddress { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? StateOrProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; } = "Philippines";
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Helper property maintaining backward compatibility with 1NF/3NF atomicity
    public string? ContactPerson
    {
        get
        {
            var name = $"{ContactFirstName} {ContactLastName}".Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ContactFirstName = null;
                ContactLastName = null;
                return;
            }
            var parts = value.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            ContactFirstName = parts[0];
            ContactLastName = parts.Length > 1 ? parts[1] : string.Empty;
        }
    }

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
    public ICollection<Part> Parts { get; set; } = new List<Part>();
}