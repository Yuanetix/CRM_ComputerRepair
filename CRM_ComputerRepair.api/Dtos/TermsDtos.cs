using System.ComponentModel.DataAnnotations;

namespace CRM_ComputerRepair.api.Dtos;

public class CreateTermsRequest
{
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Content is required.")]
    public string Content { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }
}

public class UpdateTermsRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}