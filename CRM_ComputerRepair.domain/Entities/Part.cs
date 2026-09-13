namespace CRM_ComputerRepair.domain.Entities;

public class Part
{
    public int PartId { get; set; }
    public string PartCode { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public int? SupplierId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Supplier? Supplier { get; set; }
    public ICollection<RepairPart> RepairParts { get; set; } = new List<RepairPart>();
}