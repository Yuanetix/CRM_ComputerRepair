using System.ComponentModel.DataAnnotations;

namespace CRM_ComputerRepair.api.Dtos;

public class CreateCustomerRequest
{
    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters.")]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email address is not valid.")]
    [MaxLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
    public string? Email { get; set; }

    [MaxLength(50, ErrorMessage = "Phone cannot exceed 50 characters.")]
    public string? Phone { get; set; }

    [MaxLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
    public string? Address { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Loyalty points cannot be negative.")]
    public int? LoyaltyPoints { get; set; }
}

public class UpdateCustomerRequest
{
    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email address is not valid.")]
    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }
}