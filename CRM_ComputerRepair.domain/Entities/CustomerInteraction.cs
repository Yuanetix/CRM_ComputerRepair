namespace CRM_ComputerRepair.domain.Entities;

public enum InteractionType
{
    Inquiry,
    Complaint,
    Feedback
}

public enum InteractionStatus
{
    Open,
    InProgress,
    Closed
}

public enum InteractionPriority
{
    Low,
    Medium,
    High
}

public class CustomerInteraction
{
    public int CustomerInteractionId { get; set; }

    // Optional link to Customer
    public int? CustomerId { get; set; }

    // Optional link to RepairRequest
    public int? RepairRequestId { get; set; }

    // Which kind of interaction: Inquiry / Complaint / Feedback
    public InteractionType InteractionType { get; set; }

    public InteractionStatus Status { get; set; } = InteractionStatus.Open;
    public InteractionPriority Priority { get; set; } = InteractionPriority.Medium;

    public string Subject { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string? Resolution { get; set; }

    public string? InteractionByUserId { get; set; }
    public DateTime InteractionDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    // Soft delete
    public bool IsActive { get; set; } = true;

    // Navigation
    public Customer? Customer { get; set; }
    public RepairRequest? RepairRequest { get; set; }
}