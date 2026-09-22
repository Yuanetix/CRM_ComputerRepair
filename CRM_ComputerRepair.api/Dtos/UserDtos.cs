using System.ComponentModel.DataAnnotations;

namespace CRM_ComputerRepair.api.Dtos;

public class UserSummaryDto
{
    public string Id { get; set; } = "";
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<string> Roles { get; set; } = new();

    public string FullName => $"{FirstName} {LastName}".Trim();
    public string RoleDisplay => Roles.Count > 0 ? string.Join(", ", Roles) : "—";
    public string StatusText => IsActive ? "Active" : "Inactive";
}

public class UpdateUserRequest
{
    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email is not valid.")]
    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(256)]
    public string? UserName { get; set; }

    public bool IsActive { get; set; }

    [MaxLength(50)]
    public string? Role { get; set; }
}

public class CreateUserRequest
{
    [Required(ErrorMessage = "Username is required.")]
    [MaxLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required.")]
    [MaxLength(50)]
    public string Role { get; set; } = "Staff";
}