using System.ComponentModel.DataAnnotations;

namespace CRM_ComputerRepair.api.Dtos;

public class CompanyDetailDto
{
    public int CompanyId { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? StateOrProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; } = "Philippines";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Multi-tenant database details
    public string DatabaseServer { get; set; } = @"(localdb)\MSSQLLocalDB";
    public string DatabaseName { get; set; } = string.Empty;
    public string? CredentialKey { get; set; }

    // Subscription Plan details
    public int? SubscriptionId { get; set; }
    public string? SubscriptionPlanName { get; set; }
    public decimal? SubscriptionPrice { get; set; }
    public string? SubscriptionDuration { get; set; }
    public int? MaxUsers { get; set; }
    public int? MaxDevices { get; set; }
    public bool EnableMultiBranching { get; set; }

    // Initial / Assigned Admin
    public string? AdminUserId { get; set; }
    public string? AdminFullName { get; set; }
    public string? AdminEmail { get; set; }
    public string? AdminUsername { get; set; }

    // Counts
    public int TotalUsersCount { get; set; }
    public int TotalDevicesCount { get; set; }
}

public class RegisterCompanyRequest
{
    // Business profile
    [Required(ErrorMessage = "Company name is required.")]
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Company code is required.")]
    [MaxLength(50)]
    public string CompanyCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ContactPhone { get; set; }

    [EmailAddress]
    [MaxLength(200)]
    public string? ContactEmail { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? StateOrProvince { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; } = "Philippines";

    // Multi-tenant database
    [MaxLength(200)]
    public string? DatabaseServer { get; set; } = @"(localdb)\MSSQLLocalDB";

    [MaxLength(200)]
    public string? DatabaseName { get; set; }

    [MaxLength(100)]
    public string? CredentialKey { get; set; }

    // Subscription Plan
    public int? SubscriptionId { get; set; }

    // Initial Business Admin account
    [Required(ErrorMessage = "Admin first name is required.")]
    [MaxLength(100)]
    public string AdminFirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Admin last name is required.")]
    [MaxLength(100)]
    public string AdminLastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Admin email is required.")]
    [EmailAddress]
    [MaxLength(256)]
    public string AdminEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Admin username is required.")]
    [MaxLength(100)]
    public string AdminUsername { get; set; } = string.Empty;

    [Required(ErrorMessage = "Admin password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string AdminPassword { get; set; } = string.Empty;
}

public class UpdateCompanyRequest
{
    [Required(ErrorMessage = "Company name is required.")]
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ContactPhone { get; set; }

    [EmailAddress]
    [MaxLength(200)]
    public string? ContactEmail { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? StateOrProvince { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; } = "Philippines";

    public int? SubscriptionId { get; set; }

    [MaxLength(200)]
    public string? DatabaseServer { get; set; }

    [MaxLength(200)]
    public string? DatabaseName { get; set; }

    public bool IsActive { get; set; }
}
