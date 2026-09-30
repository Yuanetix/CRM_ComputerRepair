namespace CRM_ComputerRepair.domain.Entities;

/// <summary>
/// Tracks data mutations recorded locally while offline or queued for dual-store
/// synchronization with the MonsterASP cloud database.
/// </summary>
public class SyncQueueItem
{
    public long Id { get; set; }
    public int CompanyId { get; set; } = 1;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Operation { get; set; } = "Upsert"; // "Upsert" or "Delete"
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";   // "Pending", "Synced", "Failed"
    public int RetryCount { get; set; } = 0;
    public string? LastError { get; set; }
    public DateTime? SyncedAtUtc { get; set; }
}
