using System.ComponentModel.DataAnnotations;

namespace CRM_ComputerRepair.api.Dtos;

public class CreateSupplierRequest
{
    [Required(ErrorMessage = "Supplier code is required.")]
    [MaxLength(50, ErrorMessage = "Supplier code cannot exceed 50 characters.")]
    public string SupplierCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Supplier name is required.")]
    [MaxLength(200)]
    public string SupplierName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ContactPerson { get; set; }

    [MaxLength(50)]
    public string? ContactNumber { get; set; }

    [EmailAddress(ErrorMessage = "Email address is not valid.")]
    [MaxLength(200)]
    public string? EmailAddress { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}