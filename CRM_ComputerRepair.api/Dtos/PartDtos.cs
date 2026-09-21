using System.ComponentModel.DataAnnotations;

namespace CRM_ComputerRepair.api.Dtos;

public class CreatePartRequest
{
    [Required(ErrorMessage = "Part code is required.")]
    [MaxLength(50)]
    public string PartCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Part name is required.")]
    [MaxLength(200)]
    public string PartName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Unit cost cannot be negative.")]
    public decimal UnitCost { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Unit price cannot be negative.")]
    public decimal UnitPrice { get; set; }

    [Range(0, int.MaxValue)]
    public int QuantityOnHand { get; set; }

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; }

    public int? SupplierId { get; set; }
}