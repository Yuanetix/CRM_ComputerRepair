namespace CRM_ComputerRepair.domain.Entities;

public class Device
{
    public int DeviceId { get; set; }
    public int CompanyId { get; set; }
    public string DeviceCode { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string? DeviceType { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public decimal? PurchasePrice { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? WarrantyStatus { get; set; }
    public DateTime? WarrantyExpiry { get; set; }
    public string? Status { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public int? CustomerId { get; set; }
    public int? BranchId { get; set; }

    // Navigation
    public Branch? Branch { get; set; }
    public Company? Company { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<RepairRequest> RepairRequests { get; set; } = new List<RepairRequest>();
}