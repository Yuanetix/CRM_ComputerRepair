using System.ComponentModel.DataAnnotations;

namespace CRM_ComputerRepair.api.Dtos;

public class CreatePaymentRequest
{
    [Required(ErrorMessage = "Repair request is required.")]
    public int RepairRequestId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Amount must be zero or greater.")]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public bool IsPaid { get; set; } = true;

    public DateTime? PaymentDate { get; set; }
}

public class UpdatePaymentRequest
{
    [Range(0, double.MaxValue, ErrorMessage = "Amount must be zero or greater.")]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public bool IsPaid { get; set; } = true;

    public DateTime? PaymentDate { get; set; }
}