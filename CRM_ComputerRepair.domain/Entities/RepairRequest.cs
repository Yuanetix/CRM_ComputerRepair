namespace CRM_ComputerRepair.domain.Entities;

public enum RepairStatus
{
    Pending,
    Approved,
    InProgress,
    Completed,
    Rejected,
    Reassigned
}

public enum Priority
{
    Low,
    Medium,
    High,
    Urgent
}

public class RepairRequest
{
    public int RepairRequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public int? DeviceId { get; set; }
    public string DeviceModel { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string IssueDescription { get; set; } = string.Empty;
    public RepairStatus Status { get; set; } = RepairStatus.Pending;
    public Priority Priority { get; set; } = Priority.Medium;
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    public DateTime? CompletionDate { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public decimal? PartsCost { get; set; }
    public decimal? LaborCost { get; set; }
    public string? TechnicianNotes { get; set; }
    public string? AssignedToStaffId { get; set; }
    public string? AssignedToManagerId { get; set; }

    // Navigation
    public Customer? Customer { get; set; }
    public Device? Device { get; set; }
    public ICollection<CustomerInteraction> CustomerInteractions { get; set; } = new List<CustomerInteraction>();
}