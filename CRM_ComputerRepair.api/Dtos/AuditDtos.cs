namespace CRM_ComputerRepair.api.Dtos;

public class AuditLogDto
{
    public int AuditLogId { get; set; }
    public string? UserId { get; set; }
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }

    public string UserDisplay => string.IsNullOrWhiteSpace(UserId) ? "(system)" : UserId;
    public string WhenDisplay => Timestamp.ToString("MMM d, yyyy HH:mm:ss");
    public string EntityDisplay => string.IsNullOrWhiteSpace(EntityId)
        ? Entity : $"{Entity} #{EntityId}";
}