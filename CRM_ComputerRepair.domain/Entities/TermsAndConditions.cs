namespace CRM_ComputerRepair.domain.Entities;

public class TermsAndConditions
{
    public int TermsId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Version { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}