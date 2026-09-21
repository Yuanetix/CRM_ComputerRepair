using System.ComponentModel.DataAnnotations;

namespace CRM_ComputerRepair.api.Dtos;

public class CreateFollowUpRequest
{
    public int? CustomerId { get; set; }
    public int? RepairRequestId { get; set; }

    [Required(ErrorMessage = "Subject is required.")]
    [MaxLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Notes are required.")]
    [MaxLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters.")]
    public string Notes { get; set; } = string.Empty;

    [Required(ErrorMessage = "Scheduled date is required.")]
    public DateTime ScheduledAt { get; set; }

    [Range(0, 3, ErrorMessage = "Channel must be 0 (Call), 1 (Email), 2 (SMS) or 3 (Visit).")]
    public int Channel { get; set; } = 0;

    [Range(0, 2, ErrorMessage = "Status must be 0 (Scheduled), 1 (Completed) or 2 (Cancelled).")]
    public int Status { get; set; } = 0;

    [MaxLength(450)]
    public string? AssignedToUserId { get; set; }
}

public class UpdateFollowUpRequest
{
    public int? CustomerId { get; set; }
    public int? RepairRequestId { get; set; }

    [Required(ErrorMessage = "Subject is required.")]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Notes are required.")]
    [MaxLength(2000)]
    public string Notes { get; set; } = string.Empty;

    [Required]
    public DateTime ScheduledAt { get; set; }

    [Range(0, 3)]
    public int Channel { get; set; }

    [Range(0, 2)]
    public int Status { get; set; }

    [MaxLength(450)]
    public string? AssignedToUserId { get; set; }
}