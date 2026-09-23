namespace CRM_ComputerRepair.domain.Entities;

public class Payment
{
    public int PaymentId { get; set; }
    public int RepairRequestId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string? PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public bool IsPaid { get; set; } = false;
    public bool IsVoid { get; set; } = false;

    // Navigation
    public RepairRequest? RepairRequest { get; set; }
}