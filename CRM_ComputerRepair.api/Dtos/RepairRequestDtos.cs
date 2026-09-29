using System.ComponentModel.DataAnnotations;

namespace CRM_ComputerRepair.api.Dtos;

public class CreateRepairRequestRequest
{
    [Required(ErrorMessage = "Customer ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Customer ID must be greater than 0.")]
    public int CustomerId { get; set; }

    public int? DeviceId { get; set; }

    [Required(ErrorMessage = "Device model is required.")]
    [MaxLength(200, ErrorMessage = "Device model cannot exceed 200 characters.")]
    public string DeviceModel { get; set; } = string.Empty;

    [MaxLength(100, ErrorMessage = "Serial number cannot exceed 100 characters.")]
    public string? SerialNumber { get; set; }

    [Required(ErrorMessage = "Issue description is required.")]
    [MaxLength(2000, ErrorMessage = "Issue description cannot exceed 2000 characters.")]
    public string IssueDescription { get; set; } = string.Empty;

    [Range(0, 3, ErrorMessage = "Priority must be 0 (Low) to 3 (Urgent).")]
    public int Priority { get; set; } = 1;

    [Range(0, double.MaxValue, ErrorMessage = "Estimated cost cannot be negative.")]
    public decimal? EstimatedCost { get; set; }
}

public class UpdateRepairRequestRequest
{
    [Required(ErrorMessage = "Customer ID is required.")]
    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }

    public int? DeviceId { get; set; }

    [Required(ErrorMessage = "Device model is required.")]
    [MaxLength(200)]
    public string DeviceModel { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [Required(ErrorMessage = "Issue description is required.")]
    [MaxLength(2000)]
    public string IssueDescription { get; set; } = string.Empty;

    [Range(0, 5, ErrorMessage = "Status must be 0 (Pending) to 5 (Reassigned).")]
    public int Status { get; set; }

    [Range(0, 3)]
    public int Priority { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? EstimatedCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ActualCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? PartsCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? LaborCost { get; set; }

    [MaxLength(2000)]
    public string? TechnicianNotes { get; set; }

    [MaxLength(450)]
    public string? AssignedToStaffId { get; set; }

    [MaxLength(450)]
    public string? AssignedToManagerId { get; set; }
}

public class ApproveRepairRequestRequest
{
    [MaxLength(450)]
    public string? AssignedToStaffId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? EstimatedCost { get; set; }

    [MaxLength(2000)]
    public string? ManagerNotes { get; set; }
}

public class ReassignRepairRequestRequest
{
    [Required(ErrorMessage = "Assigned technician (staff id) is required.")]
    [MaxLength(450)]
    public string AssignedToStaffId { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class ChangeRepairStatusRequest
{
    [Range(0, 5, ErrorMessage = "Status must be 0 (Pending) to 5 (Reassigned).")]
    public int Status { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class QuickCompleteRepairRequest
{
    [Range(0, double.MaxValue)]
    public decimal? ActualCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? PartsCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? LaborCost { get; set; }

    [MaxLength(2000)]
    public string? TechnicianNotes { get; set; }
}

public class RepairRequestResponseDto
{
    public int RepairRequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public int? DeviceId { get; set; }
    public string DeviceModel { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string IssueDescription { get; set; } = string.Empty;
    public int Status { get; set; }
    public int Priority { get; set; }
    public DateTime RequestDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public decimal? PartsCost { get; set; }
    public decimal? LaborCost { get; set; }
    public string? TechnicianNotes { get; set; }
    public string? AssignedToStaffId { get; set; }
    public string? AssignedToManagerId { get; set; }
}