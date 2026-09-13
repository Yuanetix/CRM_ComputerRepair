namespace CRM_ComputerRepair.domain.Entities;

public class RepairPart
{
    public int RepairPartId { get; set; }
    public int RepairRequestId { get; set; }
    public int PartId { get; set; }
    public int QuantityUsed { get; set; }
    public decimal UnitCostAtTime { get; set; }
    public decimal UnitPriceAtTime { get; set; }
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public RepairRequest? RepairRequest { get; set; }
    public Part? Part { get; set; }
}