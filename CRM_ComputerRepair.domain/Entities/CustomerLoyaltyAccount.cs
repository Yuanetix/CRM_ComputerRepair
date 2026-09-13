namespace CRM_ComputerRepair.domain.Entities;

public class CustomerLoyaltyAccount
{
    public int CustomerLoyaltyAccountId { get; set; }
    public int CustomerId { get; set; }
    public int LoyaltyProgramId { get; set; }
    public int Points { get; set; } = 0;
    public decimal TotalSpent { get; set; } = 0;
    public DateTime JoinedDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation
    public Customer? Customer { get; set; }
    public LoyaltyProgram? LoyaltyProgram { get; set; }
}