using System.ComponentModel.DataAnnotations;

namespace CRM_ComputerRepair.api.Dtos;

public class CreateInteractionRequest
{
    public int? CustomerId { get; set; }
    public int? RepairRequestId { get; set; }

    [Range(0, 2, ErrorMessage = "Interaction type must be 0 (Inquiry), 1 (Complaint) or 2 (Feedback).")]
    public int InteractionType { get; set; }

    [Range(0, 2, ErrorMessage = "Priority must be 0 (Low), 1 (Medium) or 2 (High).")]
    public int Priority { get; set; } = 1;

    [Required(ErrorMessage = "Subject is required.")]
    [MaxLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Notes are required.")]
    [MaxLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters.")]
    public string Notes { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? InteractionByUserId { get; set; }
}

public class UpdateInteractionRequest
{
    public int? CustomerId { get; set; }
    public int? RepairRequestId { get; set; }

    [Range(0, 2)]
    public int InteractionType { get; set; }

    [Range(0, 2)]
    public int Status { get; set; }

    [Range(0, 2)]
    public int Priority { get; set; }

    [Required(ErrorMessage = "Subject is required.")]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Notes are required.")]
    [MaxLength(2000)]
    public string Notes { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Resolution { get; set; }
}