namespace CRM_ComputerRepair.domain.Entities;

public class LoyaltyProgram
{
    public int LoyaltyProgramId { get; set; }
    public int? CompanyId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PointsPerPeso { get; set; } = 1;
    public decimal DiscountPercentage { get; set; } = 0;
    public decimal MinimumSpend { get; set; } = 0;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Company? Company { get; set; }
}