namespace CRM_ComputerRepair.domain.Entities;

public class Branch
{
    public int BranchId { get; set; }
    public int? CompanyId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? StateOrProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ManagerUserId { get; set; }
    public string? ManagerName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigations (Tenant DB)
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<RepairRequest> RepairRequests { get; set; } = new List<RepairRequest>();
}
