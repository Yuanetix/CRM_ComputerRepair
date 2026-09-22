using System.ComponentModel.DataAnnotations;

namespace CRM_ComputerRepair.api.Dtos;

public class CreateLoyaltyProgramRequest
{
    [Required(ErrorMessage = "Program name is required.")]
    [MaxLength(200)]
    public string ProgramName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int PointsPerPeso { get; set; } = 1;

    [Range(0, 100)]
    public decimal DiscountPercentage { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MinimumSpend { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}

public class UpdateLoyaltyProgramRequest
{
    [Required]
    [MaxLength(200)]
    public string ProgramName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int PointsPerPeso { get; set; }

    [Range(0, 100)]
    public decimal DiscountPercentage { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MinimumSpend { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; }
}