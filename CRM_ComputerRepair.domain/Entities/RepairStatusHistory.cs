namespace CRM_ComputerRepair.domain.Entities;

public class RepairStatusHistory
{
    public int RepairStatusHistoryId { get; set; }
    public int RepairRequestId { get; set; }
    public RepairStatus OldStatus { get; set; }
    public RepairStatus NewStatus { get; set; }
    public string? ChangedByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public RepairRequest? RepairRequest { get; set; }
}