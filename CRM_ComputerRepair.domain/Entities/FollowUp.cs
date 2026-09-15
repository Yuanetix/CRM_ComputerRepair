namespace CRM_ComputerRepair.domain.Entities;

public enum FollowUpStatus
{
    Scheduled,
    Completed,
    Cancelled
}

public enum FollowUpChannel
{
    Call,
    Email,
    SMS,
    Visit
}

public class FollowUp
{
    public int FollowUpId { get; set; }

    // Optional links
    public int? CustomerId { get; set; }
    public int? RepairRequestId { get; set; }

    // Content
    public string Subject { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    // When
    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Channel: Call / Email / SMS / Visit
    public FollowUpChannel Channel { get; set; } = FollowUpChannel.Call;

    // Status: Scheduled / Completed / Cancelled
    public FollowUpStatus Status { get; set; } = FollowUpStatus.Scheduled;

    // Who is assigned
    public string? AssignedToUserId { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Soft delete
    public bool IsActive { get; set; } = true;

    // Navigation
    public Customer? Customer { get; set; }
    public RepairRequest? RepairRequest { get; set; }
}