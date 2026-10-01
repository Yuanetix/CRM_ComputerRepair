namespace CRM_ComputerRepair.api.Dtos;

public class BranchDto
{
    public int BranchId { get; set; }
    public int? CompanyId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? StateOrProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ManagerUserId { get; set; }
    public string? ManagerName { get; set; }
    public bool IsActive { get; set; }
    public int StaffCount { get; set; }
    public int RecordsCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateBranchRequest
{
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? StateOrProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ManagerUserId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateBranchRequest
{
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? StateOrProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ManagerUserId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BranchSummaryStatsDto
{
    public int TotalActiveBranches { get; set; }
    public int TotalStaff { get; set; }
    public int ActivePipelineRecords { get; set; }
    public decimal TotalClosedRevenue { get; set; }
}
