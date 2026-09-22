namespace CRM_ComputerRepair.api.Dtos;

public class StaffActivityDto
{
    public int RepairStatusHistoryId { get; set; }
    public int RepairRequestId { get; set; }
    public string RequestNumber { get; set; } = "";
    public string DeviceModel { get; set; } = "";
    public int OldStatus { get; set; }
    public int NewStatus { get; set; }
    public string? ChangedByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTime ChangedAt { get; set; }
}