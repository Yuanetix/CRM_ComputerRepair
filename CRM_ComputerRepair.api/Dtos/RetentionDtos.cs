using System.ComponentModel.DataAnnotations;

namespace CRM_ComputerRepair.api.Dtos;

public class RetentionContactRequest
{
    [Required(ErrorMessage = "Subject is required.")]
    [MaxLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Notes are required.")]
    [MaxLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters.")]
    public string Notes { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? PerformedByUserId { get; set; }

    [Range(0, 365, ErrorMessage = "Follow-up days must be between 0 and 365.")]
    public int? ScheduleFollowUpInDays { get; set; }
}