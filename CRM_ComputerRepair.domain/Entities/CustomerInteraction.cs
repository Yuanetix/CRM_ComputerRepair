namespace CRM_ComputerRepair.domain.Entities;

public enum InteractionType
{
    Call,
    Email,
    Visit,
    FollowUp,
    RepairUpdate
}

public class CustomerInteraction
{
    public int CustomerInteractionId { get; set; }
    public int CustomerId { get; set; }
    public int? RepairRequestId { get; set; }
    public InteractionType InteractionType { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime InteractionDate { get; set; } = DateTime.UtcNow;
    public string? InteractionByUserId { get; set; }

    // Navigation
    public Customer? Customer { get; set; }
    public RepairRequest? RepairRequest { get; set; }
}